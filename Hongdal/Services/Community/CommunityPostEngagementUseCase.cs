using FluentResults;
using Hongdal.Application.CommandProcessing;
using Hongdal.Contracts.Common.Community;
using Hongdal.Contracts.Common.Metadata;
using Hongdal.Domain.Community;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using 홍달.Data;

namespace Hongdal.Services.Community;

[HongdalCommunityV0Module(
    HongdalCommunityV0ModuleKeys.Participation,
    HongdalModuleKind.Application,
    "게시글 추천과 일반·첨부 댓글의 조회·작성·본인 삭제를 처리",
    ReleaseStage = HongdalCommunityV0ReleaseStages.Persistence,
    Boundary = "자발적 참여 기록만 처리하며 운영자 심의, 계약 확정, 자동 상대 선택 또는 실행 상태를 변경하지 않습니다.")]
public sealed class 커뮤니티게시글참여UseCase : I커뮤니티게시글참여UseCase
{
    private readonly HongdalContext _db;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public 커뮤니티게시글참여UseCase(
        HongdalContext db,
        ICurrentUserAccessor currentUserAccessor)
    {
        _db = db;
        _currentUserAccessor = currentUserAccessor;
    }

    public async Task<Result<PlatformCommunityPostResponse>> 추천Async(
        long id,
        PlatformCommunityPostRecommendationRequest? request,
        string fallbackRecommenderKey,
        CancellationToken cancellationToken)
    {
        var recommenderKey = CommunityPostingIdentityPolicy.Normalize(
            request?.RecommenderKey,
            fallbackRecommenderKey,
            120);
        var entity = await _db.PlatformCommunityPosts
            .Include(post => post.Attachments)
                .ThenInclude(attachment => attachment.Comments)
            .Include(post => post.Comments.Where(comment => !comment.IsDeleted && !comment.IsOperatorHidden))
            .FirstOrDefaultAsync(post => post.Id == id && !post.IsDeleted, cancellationToken);
        if (entity is null)
        {
            return NotFound<PlatformCommunityPostResponse>("게시글을 찾을 수 없습니다.");
        }

        var alreadyRecommended = await _db.PlatformCommunityPostRecommendations
            .AnyAsync(
                recommendation => recommendation.PostId == id
                                  && recommendation.RecommenderKey == recommenderKey,
                cancellationToken);
        if (!alreadyRecommended)
        {
            var now = DateTime.UtcNow;
            _db.PlatformCommunityPostRecommendations.Add(new PlatformCommunityPostRecommendation
            {
                PostId = id,
                RecommenderKey = recommenderKey,
                CreatedAtUtc = now
            });
            entity.RecommendationCount += 1;
            entity.LastEngagedAtUtc = now;
            entity.UpdatedAtUtc = now;
            await _db.SaveChangesAsync(cancellationToken);
        }

        return Result.Ok(CommunityPostResponseMapper.ToResponse(entity));
    }

    public async Task<Result<IReadOnlyList<PlatformCommunityPostCommentResponse>>> 댓글목록Async(
        long id,
        CancellationToken cancellationToken)
    {
        var exists = await _db.PlatformCommunityPosts
            .AnyAsync(post => post.Id == id && !post.IsDeleted, cancellationToken);
        if (!exists)
        {
            return NotFound<IReadOnlyList<PlatformCommunityPostCommentResponse>>("게시글을 찾을 수 없습니다.");
        }

        var comments = await _db.PlatformCommunityPostComments
            .AsNoTracking()
            .Where(comment => comment.PostId == id && !comment.IsDeleted && !comment.IsOperatorHidden)
            .OrderByDescending(comment => comment.CreatedAtUtc)
            .Take(50)
            .ToListAsync(cancellationToken);
        return Result.Ok<IReadOnlyList<PlatformCommunityPostCommentResponse>>(
            comments.Select(CommunityPostResponseMapper.ToCommentResponse).ToArray());
    }

    public async Task<Result<PlatformCommunityPostCommentResponse>> 댓글작성Async(
        long id,
        PlatformCommunityPostCommentCreateRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return Result.Fail<PlatformCommunityPostCommentResponse>("request body is required");
        }

        var entity = await _db.PlatformCommunityPosts
            .FirstOrDefaultAsync(post => post.Id == id && !post.IsDeleted, cancellationToken);
        if (entity is null)
        {
            return NotFound<PlatformCommunityPostCommentResponse>("게시글을 찾을 수 없습니다.");
        }

        var requiresNickname = CommunityPostingIdentityPolicy.RequiresSuppliedNickname(
            entity.Category,
            _currentUserAccessor.UserId);
        var validation = CommunityPostingIdentityPolicy.ValidateComment(request, requiresNickname);
        if (validation is not null)
        {
            return Result.Fail<PlatformCommunityPostCommentResponse>(validation);
        }

        var now = DateTime.UtcNow;
        var comment = new PlatformCommunityPostComment
        {
            PostId = id,
            Nickname = CommunityPostingIdentityPolicy.ResolveNickname(
                entity.Category,
                request.Nickname,
                null,
                _currentUserAccessor.UserId),
            Body = CommunityPostingIdentityPolicy.Normalize(request.Body, string.Empty, 1000),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password.Trim()),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        _db.PlatformCommunityPostComments.Add(comment);
        entity.CommentCount += 1;
        entity.LastEngagedAtUtc = now;
        entity.UpdatedAtUtc = now;
        await _db.SaveChangesAsync(cancellationToken);
        return Result.Ok(CommunityPostResponseMapper.ToCommentResponse(comment));
    }

    public async Task<Result> 댓글삭제Async(
        long id,
        long commentId,
        PlatformCommunityPostPasswordRequest? request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request?.Password))
        {
            return Result.Fail("Password is required.");
        }

        var comment = await _db.PlatformCommunityPostComments
            .Include(item => item.Post)
            .FirstOrDefaultAsync(
                item => item.Id == commentId
                        && item.PostId == id
                        && !item.IsDeleted
                        && item.Post != null
                        && !item.Post.IsDeleted,
                cancellationToken);
        if (comment is null)
        {
            return NotFound("댓글을 찾을 수 없습니다.");
        }

        if (!BCrypt.Net.BCrypt.Verify(request.Password.Trim(), comment.PasswordHash))
        {
            return Forbidden("댓글 비밀번호가 일치하지 않습니다.");
        }

        comment.IsDeleted = true;
        comment.UpdatedAtUtc = DateTime.UtcNow;
        comment.Post!.CommentCount = Math.Max(0, comment.Post.CommentCount - 1);
        comment.Post.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }

    public async Task<Result<IReadOnlyList<PlatformCommunityPostAttachmentCommentResponse>>> 첨부댓글목록Async(
        long attachmentId,
        CancellationToken cancellationToken)
    {
        var exists = await _db.PlatformCommunityPostAttachments
            .AnyAsync(
                attachment => attachment.Id == attachmentId
                              && attachment.Post != null
                              && !attachment.Post.IsDeleted,
                cancellationToken);
        if (!exists)
        {
            return NotFound<IReadOnlyList<PlatformCommunityPostAttachmentCommentResponse>>("첨부 이미지를 찾을 수 없습니다.");
        }

        var comments = await _db.PlatformCommunityPostAttachmentComments
            .AsNoTracking()
            .Where(comment => comment.AttachmentId == attachmentId
                              && !comment.IsDeleted
                              && !comment.IsOperatorHidden)
            .OrderByDescending(comment => comment.CreatedAtUtc)
            .Take(50)
            .ToListAsync(cancellationToken);
        return Result.Ok<IReadOnlyList<PlatformCommunityPostAttachmentCommentResponse>>(
            comments.Select(CommunityPostResponseMapper.ToAttachmentCommentResponse).ToArray());
    }

    public async Task<Result<PlatformCommunityPostAttachmentCommentResponse>> 첨부댓글작성Async(
        long attachmentId,
        PlatformCommunityPostAttachmentCommentCreateRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return Result.Fail<PlatformCommunityPostAttachmentCommentResponse>("request body is required");
        }

        var attachment = await _db.PlatformCommunityPostAttachments
            .Include(item => item.Post)
            .FirstOrDefaultAsync(
                item => item.Id == attachmentId && item.Post != null && !item.Post.IsDeleted,
                cancellationToken);
        if (attachment is null)
        {
            return NotFound<PlatformCommunityPostAttachmentCommentResponse>("첨부 이미지를 찾을 수 없습니다.");
        }

        var requiresNickname = CommunityPostingIdentityPolicy.RequiresSuppliedNickname(
            attachment.Post!.Category,
            _currentUserAccessor.UserId);
        var validation = CommunityPostingIdentityPolicy.ValidateAttachmentComment(
            request,
            requiresNickname);
        if (validation is not null)
        {
            return Result.Fail<PlatformCommunityPostAttachmentCommentResponse>(validation);
        }

        var now = DateTime.UtcNow;
        var comment = new PlatformCommunityPostAttachmentComment
        {
            AttachmentId = attachmentId,
            Nickname = CommunityPostingIdentityPolicy.ResolveNickname(
                attachment.Post.Category,
                request.Nickname,
                null,
                _currentUserAccessor.UserId),
            Body = CommunityPostingIdentityPolicy.Normalize(request.Body, string.Empty, 1000),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password.Trim()),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        _db.PlatformCommunityPostAttachmentComments.Add(comment);
        attachment.CommentCount += 1;
        attachment.Post.LastEngagedAtUtc = now;
        attachment.Post.UpdatedAtUtc = now;
        await _db.SaveChangesAsync(cancellationToken);
        return Result.Ok(CommunityPostResponseMapper.ToAttachmentCommentResponse(comment));
    }

    public async Task<Result> 첨부댓글삭제Async(
        long attachmentId,
        long commentId,
        PlatformCommunityPostPasswordRequest? request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request?.Password))
        {
            return Result.Fail("Password is required.");
        }

        var comment = await _db.PlatformCommunityPostAttachmentComments
            .Include(item => item.Attachment)
                .ThenInclude(attachment => attachment.Post)
            .FirstOrDefaultAsync(
                item => item.Id == commentId
                        && item.AttachmentId == attachmentId
                        && !item.IsDeleted
                        && item.Attachment.Post != null
                        && !item.Attachment.Post.IsDeleted,
                cancellationToken);
        if (comment is null)
        {
            return NotFound("첨부 댓글을 찾을 수 없습니다.");
        }

        if (!BCrypt.Net.BCrypt.Verify(request.Password.Trim(), comment.PasswordHash))
        {
            return Forbidden("첨부 댓글 비밀번호가 일치하지 않습니다.");
        }

        comment.IsDeleted = true;
        comment.UpdatedAtUtc = DateTime.UtcNow;
        comment.Attachment.CommentCount = Math.Max(0, comment.Attachment.CommentCount - 1);
        comment.Attachment.Post!.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }

    private static Result<T> NotFound<T>(string message)
        => Result.Fail<T>(new Error(message).WithMetadata("StatusCode", StatusCodes.Status404NotFound));

    private static Result NotFound(string message)
        => Result.Fail(new Error(message).WithMetadata("StatusCode", StatusCodes.Status404NotFound));

    private static Result Forbidden(string message)
        => Result.Fail(new Error(message).WithMetadata("StatusCode", StatusCodes.Status403Forbidden));
}
