using Ssalddel.Contracts.Admin.Food;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.PrivacySupport;
using Ssalddel.Ui.Common.Areas.BackOffice.Services;
using Ssalddel.Ui.Common.Areas.BackOffice.ViewModels;

namespace Ssalddel.Tests.Ui.Common;

public sealed class FoodSupportFollowUpR76Tests
{
    [Fact]
    public async Task 음식지원후속은_사건과주문판본을확인하고_원요청영수증으로만완료한다()
    {
        var client = new Client();
        using var model = new 보호지원관리ViewModel(client, () => "admin");
        await model.LoadAsync("case-a");
        await model.LoadFoodFollowUpAsync();
        await model.SubmitFoodFollowUpAsync();
        Assert.Empty(client.Commands);
        model.FoodFollowUpConfirmed = true;
        await model.SubmitFoodFollowUpAsync();
        var request = Assert.Single(client.Commands);
        Assert.Equal("case-a", request.SupportCaseId);
        Assert.Equal(5, request.ExpectedCaseRevision);
        Assert.Equal(3, request.ExpectedOrderRevision);
        Assert.Equal(FoodSupportFollowUpActions.RetryDispatch, request.ActionCode);
        Assert.False(model.HasPending);
        Assert.Contains("환불·지급", model.Notice);
    }

    [Fact]
    public async Task 음식후속저장후응답유실은_GET영수증확인만하고_다시POST하지않는다()
    {
        var client = new Client { LoseResponse = true };
        using var model = new 보호지원관리ViewModel(client, () => "admin");
        await model.LoadAsync("case-a"); await model.LoadFoodFollowUpAsync();
        model.FoodFollowUpConfirmed = true;
        await model.SubmitFoodFollowUpAsync();
        Assert.True(model.HasFoodFollowUpPending);
        model.FoodFollowUpAction = FoodSupportFollowUpActions.OperatorReview;
        await model.SubmitFoodFollowUpAsync();
        Assert.Single(client.Commands);
        await model.CheckFoodFollowUpAsync();
        Assert.False(model.HasFoodFollowUpPending);
        Assert.Single(client.Commands);
        Assert.Equal(FoodSupportFollowUpActions.RetryDispatch, model.FoodFollowUp!.Receipt!.ActionCode);
    }

    [Fact]
    public async Task 음식후속영수증없이주문이변경됐으면_성공으로표시하지않고_다시검토한다()
    {
        var client = new Client { LoseResponse = true, SaveReceipt = false };
        using var model = new 보호지원관리ViewModel(client, () => "admin");
        await model.LoadAsync("case-a"); await model.LoadFoodFollowUpAsync(); model.FoodFollowUpConfirmed = true;
        await model.SubmitFoodFollowUpAsync();
        client.OrderRevision = 4;
        await model.CheckFoodFollowUpAsync();
        Assert.True(model.HasFoodFollowUpPending);
        Assert.False(model.CanRetryFoodFollowUp);
        Assert.True(model.CanReviewChangedFoodFollowUp);
        Assert.Contains("성공으로 처리하지", model.Notice);
        await model.ReviewChangedFoodFollowUpAsync();
        Assert.False(model.HasPending);
        Assert.Single(client.Commands);
    }

    [Fact]
    public async Task 음식지원결과통지미완료는_후속API를호출하지않는다()
    {
        var client = new Client { NoticesVerified = false };
        using var model = new 보호지원관리ViewModel(client, () => "admin");
        await model.LoadAsync("case-a"); await model.LoadFoodFollowUpAsync(); model.FoodFollowUpConfirmed = true;
        await model.SubmitFoodFollowUpAsync();
        Assert.Equal(0, client.FoodReads);
        Assert.Empty(client.Commands);
    }

    private sealed class Client : I보호지원관리Client
    {
        public bool LoseResponse { get; set; }
        public bool SaveReceipt { get; set; } = true;
        public bool NoticesVerified { get; set; } = true;
        public long OrderRevision { get; set; } = 3;
        public int FoodReads { get; private set; }
        public List<FoodSupportFollowUpRequest> Commands { get; } = [];
        private FoodSupportFollowUpResponse? _receipt;
        public Task<보호지원CaseResponse> 상세Async(string id, CancellationToken ct)
            => Task.FromResult(new 보호지원CaseResponse { CaseId = id, Kind = 보호지원종류Codes.분쟁, SourceKind = 보호지원출처Codes.음식주문,
                SourceId = "food-a", Revision = 5, StatusCode = "closed", PreparedResult = "처리방안 준비", ResultNotifiedAtUtc = NoticesVerified ? DateTime.UtcNow : null });
        public Task<FoodSupportFollowUpContext> 음식후속조회Async(string orderNo, string caseId, Guid? requestId, CancellationToken ct)
        {
            FoodReads++;
            return Task.FromResult(new FoodSupportFollowUpContext { OrderNo = orderNo, SupportCaseId = caseId, CaseRevision = 5,
                OrderRevision = OrderRevision, ResultNoticeVerified = true, CanRetryDispatch = true, CanRecordOperatorReview = true,
                OrderStatus = "주문확인", Receipt = requestId == _receipt?.ClientRequestId ? _receipt : null });
        }
        public Task<FoodSupportFollowUpResponse> 음식후속처리Async(string orderNo, FoodSupportFollowUpRequest request, CancellationToken ct)
        {
            Commands.Add(request);
            var response = new FoodSupportFollowUpResponse { ClientRequestId = request.ClientRequestId, OrderNo = orderNo,
                SupportCaseId = request.SupportCaseId, SupportCaseRevision = request.ExpectedCaseRevision, OrderRevision = request.ExpectedOrderRevision + 1,
                ActionCode = request.ActionCode, WorkStatusCode = "DispatchRetryScheduled", Message = "재탐색 요청을 확인했습니다. 환불·지급은 별도입니다." };
            if (SaveReceipt) { _receipt = response; OrderRevision = response.OrderRevision; }
            if (LoseResponse) throw new HttpRequestException("synthetic lost response");
            return Task.FromResult(response);
        }
        public Task<보호지원ListResponse> 목록Async(string kind, int page, CancellationToken ct) => throw new NotSupportedException();
        public Task<보호지원CaseResponse> 변경Async(string caseId, 보호지원CommandRequest request, CancellationToken ct) => throw new NotSupportedException();
        public Task<보호지원증거Response> 비공개확인Async(string caseId, CancellationToken ct) => throw new NotSupportedException();
        public Task<NeighborhoodMemberRecoveryContextResponse> 배송복구조회Async(string requestId, NeighborhoodMemberRecoveryRequest request, CancellationToken ct) => throw new NotSupportedException();
        public Task<NeighborhoodMemberRecoveryResponse> 배송복구Async(string requestId, NeighborhoodMemberRecoveryRequest request, CancellationToken ct) => throw new NotSupportedException();
    }
}
