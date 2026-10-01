using FluentResults;
using Hongdal.Contracts.Common.Community;
using Hongdal.Contracts.Common.Metadata;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using 홍달.Data;

namespace Hongdal.Services.Community;

[HongdalCommunityV0Module(
    HongdalCommunityV0ModuleKeys.Safety,
    HongdalModuleKind.Application,
    "게시글 고정과 일반·첨부 댓글의 신고 및 운영자 숨김 상태를 처리",
    ReleaseStage = HongdalCommunityV0ReleaseStages.Persistence,
    Boundary = "운영 상태만 변경하며 사용자 댓글의 작성·삭제 권한이나 게시글 발행 내용을 대리하지 않습니다.")]
public sealed class 커뮤니티게시글운영UseCase : I커뮤니티게시글운영UseCase
{
    private readonly HongdalContext _db;

    public 커뮤니티게시글운영UseCase(HongdalContext db)
    {
        _db = db;
    }

    public async Task<Result<PlatformCommunityPostResponse>> 운영자고정Async(
        long id,
        PlatformCommunityPostOperatorPinRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return Result.Fail<PlatformCommunityPostResponse>("request body is required");
        }

        var entity = await _db.PlatformCommunityPosts
            .Include(post => post.Attachments)
                .ThenInclude(attachment => attachment.Comments)
            .Include(post => post.Comments.Where(comment => !comment.IsDeleted && !comment.IsOperatorHidden))
            .FirstOrDefaultAsync(post => post.Id == id && !post.IsDeleted, cancellationToken);
        if (entity is null)
        {
            return NotFound<PlatformCommunityPostResponse>("게시글을 찾을 수 없습니다.");
        }

        var now = DateTime.UtcNow;
        entity.IsOperatorPinned = request.IsOperatorPinned;
        entity.OperatorPinnedAtUtc = request.IsOperatorPinned ? now : null;
        entity.UpdatedAtUtc = now;
        await _db.SaveChangesAsync(cancellationToken);

        return Result.Ok(CommunityPostResponseMapper.ToResponse(entity));
    }

    public async Task<Result> 댓글신고Async(long commentId, CancellationToken cancellationToken)
    {
        var comment = await _db.PlatformCommunityPostComments
            .FirstOrDefaultAsync(item => item.Id == commentId && !item.IsDeleted, cancellationToken);
        if (comment is null)
        {
            return NotFound("댓글을 찾을 수 없습니다.");
        }

        comment.ReportCount += 1;
        comment.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }

    public async Task<Result> 댓글운영자숨김Async(
        long commentId,
        PlatformCommunityOperatorHiddenRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return Result.Fail("request body is required");
        }

        var comment = await _db.PlatformCommunityPostComments
            .FirstOrDefaultAsync(item => item.Id == commentId && !item.IsDeleted, cancellationToken);
        if (comment is null)
        {
            return NotFound("댓글을 찾을 수 없습니다.");
        }

        comment.IsOperatorHidden = request.IsOperatorHidden;
        comment.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }

    public async Task<Result> 첨부댓글신고Async(long commentId, CancellationToken cancellationToken)
    {
        var comment = await _db.PlatformCommunityPostAttachmentComments
            .FirstOrDefaultAsync(item => item.Id == commentId && !item.IsDeleted, cancellationToken);
        if (comment is null)
        {
            return NotFound("첨부 댓글을 찾을 수 없습니다.");
        }

        comment.ReportCount += 1;
        comment.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }

    public async Task<Result> 첨부댓글운영자숨김Async(
        long commentId,
        PlatformCommunityOperatorHiddenRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return Result.Fail("request body is required");
        }

        var comment = await _db.PlatformCommunityPostAttachmentComments
            .FirstOrDefaultAsync(item => item.Id == commentId && !item.IsDeleted, cancellationToken);
        if (comment is null)
        {
            return NotFound("첨부 댓글을 찾을 수 없습니다.");
        }

        comment.IsOperatorHidden = request.IsOperatorHidden;
        comment.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }

    private static Result<T> NotFound<T>(string message)
        => Result.Fail<T>(new Error(message).WithMetadata("StatusCode", StatusCodes.Status404NotFound));

    private static Result NotFound(string message)
        => Result.Fail(new Error(message).WithMetadata("StatusCode", StatusCodes.Status404NotFound));
}
