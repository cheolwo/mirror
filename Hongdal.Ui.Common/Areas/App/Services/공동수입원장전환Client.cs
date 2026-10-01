using Hongdal.Contracts.Common.Community;

namespace Hongdal.Ui.Common.Areas.App.Services;

public interface I공동수입원장전환Client
{
    Task<CommunityGroupImportLedgerPlanResponse?> 조회Async(
        Guid campaignId,
        CancellationToken cancellationToken = default);

    Task<CommunityGroupImportLedgerPlanResponse?> 미리보기Async(
        Guid campaignId,
        CommunityGroupImportLedgerConversionRequest request,
        CancellationToken cancellationToken = default);

    Task<CommunityGroupImportLedgerPlanResponse?> 전환Async(
        Guid campaignId,
        CommunityGroupImportLedgerConversionRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class 공동수입원장전환Client(IHongdalJsonApiClient client) : I공동수입원장전환Client
{
    private const string BasePath = "api/v1/orderer/group-purchase-demand-votes";

    public Task<CommunityGroupImportLedgerPlanResponse?> 조회Async(
        Guid campaignId,
        CancellationToken cancellationToken = default)
        => client.GetAsync<CommunityGroupImportLedgerPlanResponse>(
            Path(campaignId),
            "공동수입 원장 조회",
            cancellationToken: cancellationToken);

    public Task<CommunityGroupImportLedgerPlanResponse?> 미리보기Async(
        Guid campaignId,
        CommunityGroupImportLedgerConversionRequest request,
        CancellationToken cancellationToken = default)
        => client.SendAsync<CommunityGroupImportLedgerConversionRequest, CommunityGroupImportLedgerPlanResponse>(
            HttpMethod.Post,
            $"{Path(campaignId)}/preview",
            request,
            "공동수입 물류 경로 미리보기",
            cancellationToken: cancellationToken);

    public Task<CommunityGroupImportLedgerPlanResponse?> 전환Async(
        Guid campaignId,
        CommunityGroupImportLedgerConversionRequest request,
        CancellationToken cancellationToken = default)
        => client.SendAsync<CommunityGroupImportLedgerConversionRequest, CommunityGroupImportLedgerPlanResponse>(
            HttpMethod.Post,
            Path(campaignId),
            request,
            "공동수입 원장 전환",
            cancellationToken: cancellationToken);

    private static string Path(Guid campaignId)
        => $"{BasePath}/{campaignId:D}/group-import-ledger";
}
