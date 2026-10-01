using Hongdal.Contracts.Common.Community;
using Hongdal.Ui.Common.Areas.App.Services;
using Hongdal.Ui.Common.Areas.App.ViewModels;

namespace Hongdal.Tests.Ui.Common;

public sealed class 공동구매화면상태ViewModel동시성Tests
{
    [Fact]
    public async Task 선택이바뀌면_늦게도착한이전원장절차응답을적용하지않는다()
    {
        var client = new DelayedLedgerProgressClient();
        using var viewModel = new 공동구매화면상태ViewModel(client);
        var first = Campaign("첫 번째");
        var second = Campaign("두 번째");

        var firstSelection = viewModel.선택적용Async(first);
        var secondSelection = viewModel.선택적용Async(second);
        client.Complete(second.Id, 공동구매절차코드.공급조건협상, revision: 7);
        await secondSelection;
        client.Complete(first.Id, 공동구매절차코드.수요모집, revision: 3);
        await firstSelection;

        Assert.Equal(second.Id, viewModel.선택된공동구매Id);
        Assert.Equal(공동구매절차코드.공급조건협상, viewModel.진행단계코드);
        Assert.Equal(7, viewModel.원장Revision);
    }

    [Fact]
    public async Task 단계진행은_현재원장Revision을_낙관적동시성값으로보낸다()
    {
        var client = new DelayedLedgerProgressClient();
        using var viewModel = new 공동구매화면상태ViewModel(client);
        var campaign = Campaign("감자 공동구매");
        var selection = viewModel.선택적용Async(campaign);
        client.Complete(campaign.Id, 공동구매절차코드.수요모집, revision: 11);
        await selection;

        await viewModel.단계진행Async(공동구매절차코드.거래상대연결);

        Assert.Equal(11, client.LastProgressRequest?.ExpectedRevision);
    }

    private static CommunityVoteResponse Campaign(string title)
        => new()
        {
            Id = Guid.NewGuid(),
            Title = title,
            GroupPurchase = new CommunityGroupPurchaseVoteResponse()
        };

    private sealed class DelayedLedgerProgressClient : I공동구매원장절차Client
    {
        private readonly Dictionary<Guid, TaskCompletionSource<CommunityGroupPurchaseLedgerProgressResponse?>> _responses = [];

        public CommunityGroupPurchaseLedgerProgressRequest? LastProgressRequest { get; private set; }

        public Task<CommunityGroupPurchaseLedgerProgressResponse?> 조회Async(
            Guid campaignId,
            CancellationToken cancellationToken = default)
        {
            if (!_responses.TryGetValue(campaignId, out var response))
            {
                response = new TaskCompletionSource<CommunityGroupPurchaseLedgerProgressResponse?>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                _responses[campaignId] = response;
            }

            // 서버가 취소를 늦게 처리하는 경우를 재현하기 위해 의도적으로 토큰을 무시합니다.
            return response.Task;
        }

        public Task<CommunityGroupPurchaseLedgerProgressResponse?> 진행Async(
            Guid campaignId,
            CommunityGroupPurchaseLedgerProgressRequest request,
            CancellationToken cancellationToken = default)
        {
            LastProgressRequest = request;
            return Task.FromResult<CommunityGroupPurchaseLedgerProgressResponse?>(new()
            {
                CommunityLedgerId = $"ledger-{campaignId:N}",
                CurrentStageCode = request.StageCode,
                Revision = (request.ExpectedRevision ?? 0) + 1
            });
        }

        public void Complete(Guid campaignId, string stageCode, long revision)
        {
            if (!_responses.TryGetValue(campaignId, out var response))
            {
                response = new TaskCompletionSource<CommunityGroupPurchaseLedgerProgressResponse?>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                _responses[campaignId] = response;
            }

            response.SetResult(new CommunityGroupPurchaseLedgerProgressResponse
            {
                CommunityLedgerId = $"ledger-{campaignId:N}",
                CurrentStageCode = stageCode,
                Revision = revision
            });
        }
    }
}
