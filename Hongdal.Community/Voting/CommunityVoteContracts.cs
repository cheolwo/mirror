using Hongdal.Contracts.Common.Community;

namespace Hongdal.Services.Community;

public interface ICommunityVoteService
{
    Task<CommunityVoteResponse> CreateAsync(CommunityVoteCreateRequest request, CancellationToken cancellationToken);

    Task<CommunityVoteListResponse> ListAsync(
        string? appKey,
        string? communityScope,
        string? hsCode,
        CancellationToken cancellationToken);

    Task<CommunityVoteListResponse> ListBySourcePostAsync(
        long sourcePostId,
        CancellationToken cancellationToken);

    Task<CommunityVoteResponse?> GetAsync(Guid voteId, CancellationToken cancellationToken);

    Task<CommunityInterestVotePromotionSnapshot?> GetInterestPromotionSnapshotAsync(
        Guid voteId,
        long sourcePostId,
        CancellationToken cancellationToken);

    Task<CommunityInterestVotePromotionSnapshot?> AttachProvisionalLedgerAsync(
        Guid voteId,
        long sourcePostId,
        string communityLedgerId,
        int minimumParticipantCount,
        string promotedByDisplayName,
        CancellationToken cancellationToken);

    Task<CommunityVoteResponse?> CastVoteAsync(Guid voteId, CommunityVoteCastRequest request, CancellationToken cancellationToken);

    Task<CommunityVoteResponse?> CloseAsync(Guid voteId, CommunityVoteCloseRequest request, CancellationToken cancellationToken);

    Task<CommunityVoteResolutionDocumentResponse?> CreateResolutionDraftAsync(
        Guid voteId,
        CommunityVoteResolutionDraftRequest request,
        CancellationToken cancellationToken);

    Task<CommunityVoteResolutionDocumentResponse?> SignResolutionAsync(
        Guid voteId,
        CommunityVoteResolutionSignRequest request,
        CancellationToken cancellationToken);

    Task<CommunityVoteResolutionDocumentResponse?> MarkResolutionReadyToSignAsync(
        Guid voteId,
        CommunityVoteResolutionReadyToSignRequest request,
        CancellationToken cancellationToken);
}
