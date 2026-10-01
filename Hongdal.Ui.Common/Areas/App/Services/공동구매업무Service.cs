using Hongdal.Contracts.Common.Community;

namespace Hongdal.Ui.Common.Areas.App.Services;

/// <summary>
/// 공동구매 모집과 합의 화면에서 사용하는 API 경계를 정의합니다.
/// ViewModel은 커뮤니티 게시글, 주문자 수요 투표, 커뮤니티 결의 API의
/// 실제 전송 방식에 의존하지 않고 이 업무 단위 계약만 사용합니다.
/// </summary>
public interface I공동구매업무Service
{
    Task<CommunityVoteListResponse> 목록조회Async(
        string? communityScope = null,
        string? hsCode = null,
        CancellationToken cancellationToken = default);

    Task<CommunityVoteResponse?> 상세조회Async(
        Guid voteId,
        CancellationToken cancellationToken = default);

    Task<PlatformCommunityPostResponse?> 제안글생성Async(
        PlatformCommunityPostCreateRequest request,
        CancellationToken cancellationToken = default);

    Task<CommunityVoteResponse?> 공동구매생성Async(
        CommunityVoteCreateRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PlatformCommunityPostCommentResponse>> 의견조회Async(
        long postId,
        CancellationToken cancellationToken = default);

    Task<CommunityVoteResponse?> 수요참여Async(
        Guid voteId,
        CommunityVoteCastRequest request,
        CancellationToken cancellationToken = default);

    Task<PlatformCommunityPostCommentResponse?> 이의등록Async(
        long postId,
        PlatformCommunityPostCommentCreateRequest request,
        CancellationToken cancellationToken = default);

    Task<CommunityVoteResponse?> 모집마감Async(
        Guid voteId,
        CommunityVoteCloseRequest request,
        CancellationToken cancellationToken = default);

    Task<CommunityVoteResolutionDocumentResponse?> 결의문생성Async(
        Guid voteId,
        CommunityVoteResolutionDraftRequest request,
        CancellationToken cancellationToken = default);

    Task<CommunityVoteResolutionDocumentResponse?> 서명준비Async(
        Guid voteId,
        CommunityVoteResolutionReadyToSignRequest request,
        CancellationToken cancellationToken = default);

    Task<CommunityVoteResolutionDocumentResponse?> 전자서명Async(
        Guid voteId,
        CommunityVoteResolutionSignRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 기존 PlatformCommunityService를 공동구매 업무 계약에 맞추는 어댑터입니다.
/// </summary>
public sealed class PlatformCommunity공동구매업무Service(
    PlatformCommunityService communityService) : I공동구매업무Service
{
    public Task<CommunityVoteListResponse> 목록조회Async(
        string? communityScope = null,
        string? hsCode = null,
        CancellationToken cancellationToken = default)
        => communityService.GetGroupPurchaseVotesAsync(communityScope, hsCode, cancellationToken);

    public Task<CommunityVoteResponse?> 상세조회Async(
        Guid voteId,
        CancellationToken cancellationToken = default)
        => communityService.GetGroupPurchaseVoteAsync(voteId, cancellationToken);

    public Task<PlatformCommunityPostResponse?> 제안글생성Async(
        PlatformCommunityPostCreateRequest request,
        CancellationToken cancellationToken = default)
        => communityService.CreatePostAsync(request, cancellationToken);

    public Task<CommunityVoteResponse?> 공동구매생성Async(
        CommunityVoteCreateRequest request,
        CancellationToken cancellationToken = default)
        => communityService.CreateGroupPurchaseVoteAsync(request, cancellationToken);

    public Task<IReadOnlyList<PlatformCommunityPostCommentResponse>> 의견조회Async(
        long postId,
        CancellationToken cancellationToken = default)
        => communityService.GetCommentsAsync(postId, cancellationToken);

    public Task<CommunityVoteResponse?> 수요참여Async(
        Guid voteId,
        CommunityVoteCastRequest request,
        CancellationToken cancellationToken = default)
        => communityService.CastGroupPurchaseVoteAsync(voteId, request, cancellationToken);

    public Task<PlatformCommunityPostCommentResponse?> 이의등록Async(
        long postId,
        PlatformCommunityPostCommentCreateRequest request,
        CancellationToken cancellationToken = default)
        => communityService.CreateCommentAsync(postId, request, cancellationToken);

    public Task<CommunityVoteResponse?> 모집마감Async(
        Guid voteId,
        CommunityVoteCloseRequest request,
        CancellationToken cancellationToken = default)
        => communityService.CloseVoteAsync(voteId, request, cancellationToken);

    public Task<CommunityVoteResolutionDocumentResponse?> 결의문생성Async(
        Guid voteId,
        CommunityVoteResolutionDraftRequest request,
        CancellationToken cancellationToken = default)
        => communityService.CreateVoteResolutionAsync(voteId, request, cancellationToken);

    public Task<CommunityVoteResolutionDocumentResponse?> 서명준비Async(
        Guid voteId,
        CommunityVoteResolutionReadyToSignRequest request,
        CancellationToken cancellationToken = default)
        => communityService.MarkVoteResolutionReadyToSignAsync(voteId, request, cancellationToken);

    public Task<CommunityVoteResolutionDocumentResponse?> 전자서명Async(
        Guid voteId,
        CommunityVoteResolutionSignRequest request,
        CancellationToken cancellationToken = default)
        => communityService.SignVoteResolutionAsync(voteId, request, cancellationToken);
}
