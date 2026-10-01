using Hongdal.Contracts.Common.Community;

namespace Hongdal.Ui.Common.Areas.App.Services;

public interface I공동구매원장절차Client
{
    Task<CommunityGroupPurchaseLedgerProgressResponse?> 조회Async(
        Guid campaignId,
        CancellationToken cancellationToken = default);

    Task<CommunityGroupPurchaseLedgerProgressResponse?> 진행Async(
        Guid campaignId,
        CommunityGroupPurchaseLedgerProgressRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class 공동구매원장절차Client(IHongdalJsonApiClient client) : I공동구매원장절차Client
{
    private const string BasePath = "api/v1/orderer/group-purchase-demand-votes";

    public Task<CommunityGroupPurchaseLedgerProgressResponse?> 조회Async(
        Guid campaignId,
        CancellationToken cancellationToken = default)
        => client.GetAsync<CommunityGroupPurchaseLedgerProgressResponse>(
            $"{BasePath}/{campaignId:D}/ledger-progress",
            "공동구매 원장 절차 조회",
            cancellationToken: cancellationToken);

    public Task<CommunityGroupPurchaseLedgerProgressResponse?> 진행Async(
        Guid campaignId,
        CommunityGroupPurchaseLedgerProgressRequest request,
        CancellationToken cancellationToken = default)
        => client.SendAsync<CommunityGroupPurchaseLedgerProgressRequest, CommunityGroupPurchaseLedgerProgressResponse>(
            HttpMethod.Post,
            $"{BasePath}/{campaignId:D}/ledger-progress",
            request,
            "공동구매 원장 절차 진행",
            cancellationToken: cancellationToken);
}
