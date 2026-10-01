using FluentResults;
using Hongdal.Contracts.Common.Community;

namespace Hongdal.Services.Community;

public sealed partial class 커뮤니티게시글UseCase
{
    public Task<Result<IReadOnlyList<PlatformCommunityPostCommentResponse>>> 댓글목록Async(
        long id,
        CancellationToken cancellationToken)
        => _participationUseCase.댓글목록Async(id, cancellationToken);

    public Task<Result<PlatformCommunityPostCommentResponse>> 댓글작성Async(
        long id,
        PlatformCommunityPostCommentCreateRequest? request,
        CancellationToken cancellationToken)
        => _participationUseCase.댓글작성Async(id, request, cancellationToken);

    public Task<Result> 댓글삭제Async(
        long id,
        long commentId,
        PlatformCommunityPostPasswordRequest? request,
        CancellationToken cancellationToken)
        => _participationUseCase.댓글삭제Async(id, commentId, request, cancellationToken);

    public Task<Result> 댓글신고Async(long commentId, CancellationToken cancellationToken)
        => _moderationUseCase.댓글신고Async(commentId, cancellationToken);

    public Task<Result> 댓글운영자숨김Async(
        long commentId,
        PlatformCommunityOperatorHiddenRequest? request,
        CancellationToken cancellationToken)
        => _moderationUseCase.댓글운영자숨김Async(commentId, request, cancellationToken);

    public Task<Result<IReadOnlyList<PlatformCommunityPostAttachmentCommentResponse>>> 첨부댓글목록Async(
        long attachmentId,
        CancellationToken cancellationToken)
        => _participationUseCase.첨부댓글목록Async(attachmentId, cancellationToken);

    public Task<Result<PlatformCommunityPostAttachmentCommentResponse>> 첨부댓글작성Async(
        long attachmentId,
        PlatformCommunityPostAttachmentCommentCreateRequest? request,
        CancellationToken cancellationToken)
        => _participationUseCase.첨부댓글작성Async(attachmentId, request, cancellationToken);

    public Task<Result> 첨부댓글삭제Async(
        long attachmentId,
        long commentId,
        PlatformCommunityPostPasswordRequest? request,
        CancellationToken cancellationToken)
        => _participationUseCase.첨부댓글삭제Async(
            attachmentId,
            commentId,
            request,
            cancellationToken);

    public Task<Result> 첨부댓글신고Async(long commentId, CancellationToken cancellationToken)
        => _moderationUseCase.첨부댓글신고Async(commentId, cancellationToken);

    public Task<Result> 첨부댓글운영자숨김Async(
        long commentId,
        PlatformCommunityOperatorHiddenRequest? request,
        CancellationToken cancellationToken)
        => _moderationUseCase.첨부댓글운영자숨김Async(commentId, request, cancellationToken);
}
