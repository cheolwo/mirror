using Hongdal.Contracts.Common.Community;

namespace Hongdal.Ui.Common.Areas.App.Services;

/// <summary>입고·출고 커뮤니티 원장을 찾고 현재 원장 문맥을 조회합니다.</summary>
public interface I입출고원장조회Service
{
    Task<IReadOnlyList<PlatformCommunityPostLedgerChoiceResponse>> 내원장목록조회Async(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PlatformCommunityPostLedgerChoiceResponse>> 공유원장목록조회Async(
        CancellationToken cancellationToken = default);

    Task<PlatformCommunityPostLedgerContextResponse?> 원장상세조회Async(
        string ledgerId,
        CancellationToken cancellationToken = default);
}

/// <summary>기존 커뮤니티 원장 API를 입출고 원장 조회 계약에 맞추는 어댑터입니다.</summary>
public sealed class PlatformCommunity입출고원장조회Service(
    PlatformCommunityService communityService) : I입출고원장조회Service
{
    private const string WarehouseWorkflowTag = "창고 입출고";

    public Task<IReadOnlyList<PlatformCommunityPostLedgerChoiceResponse>> 내원장목록조회Async(
        CancellationToken cancellationToken = default)
        => communityService.GetMyLedgersAsync(WarehouseWorkflowTag, cancellationToken);

    public Task<IReadOnlyList<PlatformCommunityPostLedgerChoiceResponse>> 공유원장목록조회Async(
        CancellationToken cancellationToken = default)
        => communityService.GetSharedLedgersAsync(WarehouseWorkflowTag, cancellationToken);

    public Task<PlatformCommunityPostLedgerContextResponse?> 원장상세조회Async(
        string ledgerId,
        CancellationToken cancellationToken = default)
        => communityService.GetLedgerContextAsync(ledgerId, cancellationToken);
}
