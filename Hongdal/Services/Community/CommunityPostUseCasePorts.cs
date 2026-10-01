using FluentResults;
using Hongdal.Contracts.Common.Community;

namespace Hongdal.Services.Community;

public interface I커뮤니티게시글조회UseCase
{
    Task<Result<PlatformCommunityPostListResponse>> 목록Async(
        string? appKey,
        string? category,
        string? boardKey,
        string? workflowTag,
        string? roleTag,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<CommunityBoardSummaryResponse>>> 게시판요약목록Async(
        string? appKey,
        CancellationToken cancellationToken);

    Task<Result<PlatformCommunityPostResponse>> 상세Async(
        long id,
        CancellationToken cancellationToken);
}

public interface I커뮤니티게시글발행UseCase
{
    Task<Result<PlatformCommunityPostResponse>> 생성Async(
        PlatformCommunityPostCreateRequest? request,
        CancellationToken cancellationToken);

    Task<Result<PlatformCommunityPostResponse>> 수정Async(
        long id,
        PlatformCommunityPostUpdateRequest? request,
        CancellationToken cancellationToken);

    Task<Result> 삭제Async(
        long id,
        PlatformCommunityPostPasswordRequest? request,
        CancellationToken cancellationToken);
}

public interface I커뮤니티게시글예약발행UseCase
{
    Task<Result<PlatformCommunityPostResponse>> 예약Async(
        PlatformCommunityPostScheduleCreateRequest? request,
        CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<PlatformCommunityPostResponse>>> 예약목록Async(
        string? status,
        int take,
        CancellationToken cancellationToken);

    Task<Result<PlatformCommunityPostResponse>> 예약취소Async(
        long id,
        CancellationToken cancellationToken);
}

public interface I커뮤니티게시글첨부UseCase
{
    Task<Result<PlatformCommunityPostAttachmentResponse>> 첨부업로드Async(
        long id,
        커뮤니티게시글첨부업로드Command? command,
        CancellationToken cancellationToken);
}

public interface I커뮤니티게시글참여UseCase
{
    Task<Result<PlatformCommunityPostResponse>> 추천Async(
        long id,
        PlatformCommunityPostRecommendationRequest? request,
        string fallbackRecommenderKey,
        CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<PlatformCommunityPostCommentResponse>>> 댓글목록Async(
        long id,
        CancellationToken cancellationToken);

    Task<Result<PlatformCommunityPostCommentResponse>> 댓글작성Async(
        long id,
        PlatformCommunityPostCommentCreateRequest? request,
        CancellationToken cancellationToken);

    Task<Result> 댓글삭제Async(
        long id,
        long commentId,
        PlatformCommunityPostPasswordRequest? request,
        CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<PlatformCommunityPostAttachmentCommentResponse>>> 첨부댓글목록Async(
        long attachmentId,
        CancellationToken cancellationToken);

    Task<Result<PlatformCommunityPostAttachmentCommentResponse>> 첨부댓글작성Async(
        long attachmentId,
        PlatformCommunityPostAttachmentCommentCreateRequest? request,
        CancellationToken cancellationToken);

    Task<Result> 첨부댓글삭제Async(
        long attachmentId,
        long commentId,
        PlatformCommunityPostPasswordRequest? request,
        CancellationToken cancellationToken);
}

public interface I커뮤니티게시글운영UseCase
{
    Task<Result<PlatformCommunityPostResponse>> 운영자고정Async(
        long id,
        PlatformCommunityPostOperatorPinRequest? request,
        CancellationToken cancellationToken);

    Task<Result> 댓글신고Async(
        long commentId,
        CancellationToken cancellationToken);

    Task<Result> 댓글운영자숨김Async(
        long commentId,
        PlatformCommunityOperatorHiddenRequest? request,
        CancellationToken cancellationToken);

    Task<Result> 첨부댓글신고Async(
        long commentId,
        CancellationToken cancellationToken);

    Task<Result> 첨부댓글운영자숨김Async(
        long commentId,
        PlatformCommunityOperatorHiddenRequest? request,
        CancellationToken cancellationToken);
}

// 기존 소비자를 한 번에 깨지 않고 기능별 port로 옮길 수 있도록 남겨 둔 호환 경계입니다.
public interface I커뮤니티게시글UseCase :
    I커뮤니티게시글조회UseCase,
    I커뮤니티게시글발행UseCase,
    I커뮤니티게시글예약발행UseCase,
    I커뮤니티게시글첨부UseCase,
    I커뮤니티게시글참여UseCase,
    I커뮤니티게시글운영UseCase
{
}

public sealed record 커뮤니티게시글첨부업로드Command(
    string Password,
    Stream FileStream,
    string FileName,
    string ContentType,
    long Length);
