using Hongdal.Contracts.Common.Community;

namespace Hongdal.Services.Community;

public interface ICommunityPostOpportunityService
{
    Task<CommunityPostOpportunityListResponse?> GetAsync(
        long postId,
        string? displayLanguageCode,
        CancellationToken cancellationToken = default);

    Task<CommunityPostContextDiscoveryResponse?> GetContextDiscoveryAsync(
        long postId,
        CommunityPostContextDiscoveryRequest request,
        CancellationToken cancellationToken = default);

    Task<StartCommunityPostParticipationResponse> StartParticipationAsync(
        long postId,
        StartCommunityPostParticipationRequest request,
        string actorUserId,
        string actorDisplayName,
        CancellationToken cancellationToken = default);

    Task<PromoteCommunityPostParticipationResponse> PromoteParticipationAsync(
        long postId,
        PromoteCommunityPostParticipationRequest request,
        string actorUserId,
        string actorDisplayName,
        CancellationToken cancellationToken = default);

    Task<JoinCommunityPostProfessionalResponse> JoinProfessionalAsync(
        long postId,
        JoinCommunityPostProfessionalRequest request,
        string actorUserId,
        string actorDisplayName,
        CancellationToken cancellationToken = default);

    Task<JoinCommunityPostPartyRoleResponse> JoinPartyRoleAsync(
        long postId,
        JoinCommunityPostPartyRoleRequest request,
        string actorUserId,
        string actorDisplayName,
        CancellationToken cancellationToken = default);

    Task<StartCommunityMeatImportReadinessResponse> StartMeatImportReadinessAsync(
        long postId,
        StartCommunityMeatImportReadinessRequest request,
        string actorUserId,
        string actorDisplayName,
        CancellationToken cancellationToken = default);
}

public interface ICommunityPostOpportunityQueryUseCase
{
    Task<CommunityPostOpportunityListResponse?> GetAsync(
        long postId,
        string? displayLanguageCode,
        CancellationToken cancellationToken = default);

    Task<CommunityPostContextDiscoveryResponse?> GetContextDiscoveryAsync(
        long postId,
        CommunityPostContextDiscoveryRequest request,
        CancellationToken cancellationToken = default);
}

public interface ICommunityPostParticipationUseCase
{
    Task<StartCommunityPostParticipationResponse> StartParticipationAsync(
        long postId,
        StartCommunityPostParticipationRequest request,
        string actorUserId,
        string actorDisplayName,
        CancellationToken cancellationToken = default);

    Task<PromoteCommunityPostParticipationResponse> PromoteParticipationAsync(
        long postId,
        PromoteCommunityPostParticipationRequest request,
        string actorUserId,
        string actorDisplayName,
        CancellationToken cancellationToken = default);
}

public interface ICommunityPostMeatImportReadinessUseCase
{
    Task<StartCommunityMeatImportReadinessResponse> StartAsync(
        long postId,
        StartCommunityMeatImportReadinessRequest request,
        string actorUserId,
        string actorDisplayName,
        CancellationToken cancellationToken = default);
}

public interface ICommunityPostOpportunityAnalyzer
{
    CommunityPostOpportunityAnalysis Analyze(string? title, string? body);
}

public sealed record CommunityPostOpportunityAnalysis(
    bool SuggestMeatImportReadiness,
    IReadOnlyList<string> MatchedSignals);

public sealed record CommunityPostOpportunitySource(
    long PostId,
    string AppKey,
    string Title,
    string Body,
    string? AuthorUserId,
    string? LinkedLedgerId,
    bool IsReportBoardPost = false,
    string? SalesOfferJson = null,
    DateTime CreatedAtUtc = default,
    string? Category = null,
    string? WorkflowTag = null);

public enum CommunityPostLedgerLinkResult
{
    Linked,
    AlreadyLinked,
    NotFound,
    NotOwner,
    ConflictingLedger
}

public enum CommunityPostMomentumUpdateResult
{
    Updated,
    NotFound,
    ConflictingLedger
}

public interface ICommunityPostOpportunityStore
{
    Task<CommunityPostOpportunitySource?> GetAsync(long postId, CancellationToken cancellationToken = default);

    Task<CommunityPostLedgerLinkResult> LinkLedgerAsync(
        long postId,
        string actorUserId,
        string ledgerId,
        CancellationToken cancellationToken = default);

    Task<CommunityPostMomentumUpdateResult> SetMomentumPromotionAsync(
        long postId,
        string ledgerId,
        string momentumCode,
        string momentumMessage,
        int roleParticipantCount,
        CancellationToken cancellationToken = default);
}

public sealed class CommunityPostOpportunityConflictException : Exception
{
    public CommunityPostOpportunityConflictException(string message)
        : base(message)
    {
    }
}

public static class CommunityPostProvisionalLedgerIds
{
    public static string FromInterestVote(long postId, Guid voteId)
    {
        if (postId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(postId));
        }

        if (voteId == Guid.Empty)
        {
            throw new ArgumentException("관심 투표 ID가 필요합니다.", nameof(voteId));
        }

        return $"community-post-{postId}-interest-{voteId:N}";
    }
}
