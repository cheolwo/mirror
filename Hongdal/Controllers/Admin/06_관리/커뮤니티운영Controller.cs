using System.Diagnostics;
using System.Text.Json;
using Hongdal.ApiMetadata;
using Hongdal.Application.CommandProcessing;
using Hongdal.Contracts.Admin.Community;
using Hongdal.Contracts.Common.ViewSettings;
using Hongdal.Services.Community;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using 홍달.Data;
using 홍달.Services.Audit;

namespace Hongdal.Controllers.Admin.Master06;

[HongdalApiVersion(HongdalProductVersion.V0_0)]
[ApiController]
[Route("api/v1/admin/community-management")]
[Authorize(Policy = "서버관리자전용")]
public sealed class 커뮤니티운영Controller : ControllerBase
{
    private readonly HongdalContext _db;
    private readonly ICurrentUserAccessor _currentUser;
    private readonly I사용자행위로그Service _activityLog;

    public 커뮤니티운영Controller(
        HongdalContext db,
        ICurrentUserAccessor currentUser,
        I사용자행위로그Service activityLog)
    {
        _db = db;
        _currentUser = currentUser;
        _activityLog = activityLog;
    }

    [HttpGet("users/{userId}")]
    public async Task<IActionResult> 사용자조회(string userId, CancellationToken cancellationToken)
    {
        var normalizedUserId = NormalizeRequired(userId, 450);
        if (normalizedUserId is null)
        {
            return BadRequest("사용자 ID를 입력해야 합니다.");
        }

        var user = await _db.Users
            .AsNoTracking()
            .Where(x => x.Id == normalizedUserId)
            .Select(x => new
            {
                x.Id,
                x.UserName,
                x.Email,
                x.PhoneNumber
            })
            .FirstOrDefaultAsync(cancellationToken);

        var roles = user is null
            ? []
            : await (
                    from userRole in _db.UserRoles.AsNoTracking()
                    join role in _db.Roles.AsNoTracking() on userRole.RoleId equals role.Id
                    where userRole.UserId == normalizedUserId
                    orderby role.Name
                    select role.Name ?? string.Empty)
                .Where(x => x != string.Empty)
                .ToArrayAsync(cancellationToken);

        var posts = await _db.PlatformCommunityPosts
            .AsNoTracking()
            .Include(x => x.Comments)
            .Include(x => x.Attachments)
                .ThenInclude(x => x.Comments)
            .Where(x => x.AuthorUserId == normalizedUserId)
            .OrderByDescending(x => x.UpdatedAtUtc)
            .Take(100)
            .ToListAsync(cancellationToken);

        return Ok(new CommunityManagementUserResponse
        {
            UserId = normalizedUserId,
            AccountExists = user is not null,
            UserName = user?.UserName ?? string.Empty,
            Email = user?.Email ?? string.Empty,
            PhoneNumber = user?.PhoneNumber ?? string.Empty,
            Roles = roles,
            Posts = posts.Select(ToPostResponse).ToArray()
        });
    }

    [HttpPut("posts/{postId:long}")]
    public async Task<IActionResult> 게시글수정(
        long postId,
        [FromBody] CommunityManagementPostUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var reasonError = ValidateReason(request.Reason);
        if (reasonError is not null)
        {
            return BadRequest(reasonError);
        }

        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Trim().Length > 160)
        {
            return BadRequest("제목은 1자 이상 160자 이하로 입력해야 합니다.");
        }

        if (string.IsNullOrWhiteSpace(request.Body) || request.Body.Trim().Length > 4000)
        {
            return BadRequest("본문은 1자 이상 4000자 이하로 입력해야 합니다.");
        }

        var post = await _db.PlatformCommunityPosts
            .FirstOrDefaultAsync(x => x.Id == postId, cancellationToken);
        if (post is null)
        {
            return NotFound("게시글을 찾을 수 없습니다.");
        }

        if (CommunityLedgerCompletionPublication.IsSystemPost(post))
        {
            return StatusCode(StatusCodes.Status403Forbidden, "원장 성립 시스템 기록은 운영자가 수정할 수 없습니다.");
        }

        post.Title = request.Title.Trim();
        post.Body = request.Body.Trim();
        post.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        await RecordActionAsync(
            "CommunityPostEdit",
            "커뮤니티 게시글 운영자 수정",
            $"posts/{postId}",
            new { PostId = postId, post.AuthorUserId, Reason = request.Reason.Trim() },
            cancellationToken);

        return Ok(new CommunityManagementActionResponse
        {
            Succeeded = true,
            Message = "게시글을 수정하고 운영 기록을 남겼습니다.",
            RecordedAtUtc = DateTime.UtcNow
        });
    }

    [HttpPut("posts/{postId:long}/visibility")]
    public async Task<IActionResult> 게시글공개상태변경(
        long postId,
        [FromBody] CommunityManagementVisibilityRequest request,
        CancellationToken cancellationToken)
    {
        var reasonError = ValidateReason(request.Reason);
        if (reasonError is not null)
        {
            return BadRequest(reasonError);
        }

        var post = await _db.PlatformCommunityPosts
            .FirstOrDefaultAsync(x => x.Id == postId, cancellationToken);
        if (post is null)
        {
            return NotFound("게시글을 찾을 수 없습니다.");
        }

        if (CommunityLedgerCompletionPublication.IsSystemPost(post))
        {
            return StatusCode(StatusCodes.Status403Forbidden, "원장 성립 시스템 기록은 운영자가 숨길 수 없습니다.");
        }

        post.IsDeleted = request.Hidden;
        post.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        await RecordActionAsync(
            request.Hidden ? "CommunityPostHide" : "CommunityPostRestore",
            request.Hidden ? "커뮤니티 게시글 운영자 숨김" : "커뮤니티 게시글 운영자 복구",
            $"posts/{postId}/visibility",
            new { PostId = postId, post.AuthorUserId, request.Hidden, Reason = request.Reason.Trim() },
            cancellationToken);

        return Ok(new CommunityManagementActionResponse
        {
            Succeeded = true,
            Message = request.Hidden ? "게시글을 숨김 처리했습니다." : "게시글을 복구했습니다.",
            RecordedAtUtc = DateTime.UtcNow
        });
    }

    [HttpPut("comments/{commentId:long}/visibility")]
    public async Task<IActionResult> 댓글공개상태변경(
        long commentId,
        [FromBody] CommunityManagementVisibilityRequest request,
        CancellationToken cancellationToken)
    {
        var reasonError = ValidateReason(request.Reason);
        if (reasonError is not null)
        {
            return BadRequest(reasonError);
        }

        var comment = await _db.PlatformCommunityPostComments
            .Include(x => x.Post)
            .FirstOrDefaultAsync(x => x.Id == commentId, cancellationToken);
        if (comment is null)
        {
            return NotFound("댓글을 찾을 수 없습니다.");
        }

        comment.IsOperatorHidden = request.Hidden;
        comment.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        await RecordActionAsync(
            request.Hidden ? "CommunityCommentHide" : "CommunityCommentRestore",
            request.Hidden ? "커뮤니티 댓글 운영자 숨김" : "커뮤니티 댓글 운영자 복구",
            $"comments/{commentId}/visibility",
            new { CommentId = commentId, comment.PostId, comment.Post.AuthorUserId, request.Hidden, Reason = request.Reason.Trim() },
            cancellationToken);

        return Ok(new CommunityManagementActionResponse
        {
            Succeeded = true,
            Message = request.Hidden ? "댓글을 숨김 처리했습니다." : "댓글을 복구했습니다.",
            RecordedAtUtc = DateTime.UtcNow
        });
    }

    [HttpPut("attachment-comments/{commentId:long}/visibility")]
    public async Task<IActionResult> 첨부댓글공개상태변경(
        long commentId,
        [FromBody] CommunityManagementVisibilityRequest request,
        CancellationToken cancellationToken)
    {
        var reasonError = ValidateReason(request.Reason);
        if (reasonError is not null)
        {
            return BadRequest(reasonError);
        }

        var comment = await _db.PlatformCommunityPostAttachmentComments
            .Include(x => x.Attachment)
                .ThenInclude(x => x.Post)
            .FirstOrDefaultAsync(x => x.Id == commentId, cancellationToken);
        if (comment is null)
        {
            return NotFound("첨부 댓글을 찾을 수 없습니다.");
        }

        comment.IsOperatorHidden = request.Hidden;
        comment.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        await RecordActionAsync(
            request.Hidden ? "CommunityAttachmentCommentHide" : "CommunityAttachmentCommentRestore",
            request.Hidden ? "커뮤니티 첨부 댓글 운영자 숨김" : "커뮤니티 첨부 댓글 운영자 복구",
            $"attachment-comments/{commentId}/visibility",
            new
            {
                CommentId = commentId,
                comment.AttachmentId,
                comment.Attachment.PostId,
                AuthorUserId = comment.Attachment.Post?.AuthorUserId,
                request.Hidden,
                Reason = request.Reason.Trim()
            },
            cancellationToken);

        return Ok(new CommunityManagementActionResponse
        {
            Succeeded = true,
            Message = request.Hidden ? "첨부 댓글을 숨김 처리했습니다." : "첨부 댓글을 복구했습니다.",
            RecordedAtUtc = DateTime.UtcNow
        });
    }

    [HttpPost("users/{userId}/contact-actions")]
    public async Task<IActionResult> 연락조치기록(
        string userId,
        [FromBody] CommunityManagementContactRequest request,
        CancellationToken cancellationToken)
    {
        var normalizedUserId = NormalizeRequired(userId, 450);
        if (normalizedUserId is null)
        {
            return BadRequest("사용자 ID를 입력해야 합니다.");
        }

        var channel = request.Channel.Trim();
        if (channel is not ("Phone" or "Email" or "Other"))
        {
            return BadRequest("연락 수단은 Phone, Email, Other 중 하나여야 합니다.");
        }

        var reasonError = ValidateReason(request.Note);
        if (reasonError is not null)
        {
            return BadRequest(reasonError.Replace("조치 사유", "연락 메모", StringComparison.Ordinal));
        }

        var accountExists = await _db.Users
            .AsNoTracking()
            .AnyAsync(x => x.Id == normalizedUserId, cancellationToken);
        if (!accountExists)
        {
            return NotFound("사용자 계정을 찾을 수 없습니다.");
        }

        await RecordActionAsync(
            "CommunityUserContact",
            "커뮤니티 사용자 연락 조치",
            $"users/{normalizedUserId}/contact-actions",
            new { TargetUserId = normalizedUserId, Channel = channel, Note = request.Note.Trim() },
            cancellationToken);

        return Ok(new CommunityManagementActionResponse
        {
            Succeeded = true,
            Message = "연락 조치와 메모를 운영 기록에 남겼습니다.",
            RecordedAtUtc = DateTime.UtcNow
        });
    }

    private async Task RecordActionAsync(
        string actionType,
        string actionName,
        string routeSuffix,
        object metadata,
        CancellationToken cancellationToken)
    {
        await _activityLog.기록Async(new 사용자행위로그기록
        {
            AppKey = App식별자.HongdalAdmin,
            UserId = _currentUser.UserId ?? string.Empty,
            RoleName = _currentUser.Role ?? string.Empty,
            ActionType = actionType,
            ActionName = actionName,
            Route = $"/api/v1/admin/community-management/{routeSuffix}",
            TraceId = Activity.Current?.TraceId.ToString() ?? HttpContext.TraceIdentifier,
            IsSuccess = true,
            ClientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty,
            UserAgent = Request.Headers.UserAgent.ToString(),
            OccurredAtUtc = DateTime.UtcNow,
            MetadataJson = JsonSerializer.Serialize(metadata)
        }, cancellationToken);
    }

    private static CommunityManagementPostResponse ToPostResponse(Hongdal.Domain.Community.PlatformCommunityPost post)
        => new()
        {
            Id = post.Id,
            AppKey = post.AppKey,
            Category = post.Category,
            WorkflowTag = post.WorkflowTag,
            RoleTag = post.RoleTag,
            Title = post.Title,
            Body = post.Body,
            Nickname = post.Nickname,
            IsSystemGenerated = CommunityAutomatedPostPublication.GetSystemPostKind(post) is not null,
            IsDeleted = post.IsDeleted,
            CreatedAtUtc = post.CreatedAtUtc,
            UpdatedAtUtc = post.UpdatedAtUtc,
            Comments = post.Comments
                .OrderByDescending(x => x.CreatedAtUtc)
                .Select(x => new CommunityManagementCommentResponse
                {
                    Id = x.Id,
                    Nickname = x.Nickname,
                    Body = x.Body,
                    ReportCount = x.ReportCount,
                    IsOperatorHidden = x.IsOperatorHidden,
                    IsDeleted = x.IsDeleted,
                    CreatedAtUtc = x.CreatedAtUtc
                })
                .ToArray(),
            Attachments = post.Attachments
                .OrderByDescending(x => x.UploadedAtUtc)
                .Select(x => new CommunityManagementAttachmentResponse
                {
                    Id = x.Id,
                    OriginalFileName = x.OriginalFileName,
                    ContentType = x.ContentType,
                    FileSizeBytes = x.FileSizeBytes,
                    UploadedAtUtc = x.UploadedAtUtc,
                    Comments = x.Comments
                        .OrderByDescending(comment => comment.CreatedAtUtc)
                        .Select(comment => new CommunityManagementAttachmentCommentResponse
                        {
                            Id = comment.Id,
                            AttachmentId = comment.AttachmentId,
                            Nickname = comment.Nickname,
                            Body = comment.Body,
                            ReportCount = comment.ReportCount,
                            IsOperatorHidden = comment.IsOperatorHidden,
                            IsDeleted = comment.IsDeleted,
                            CreatedAtUtc = comment.CreatedAtUtc
                        })
                        .ToArray()
                })
                .ToArray()
        };

    private static string? NormalizeRequired(string? value, int maxLength)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) || normalized.Length > maxLength
            ? null
            : normalized;
    }

    private static string? ValidateReason(string? reason)
    {
        var length = reason?.Trim().Length ?? 0;
        return length is < 2 or > 1000
            ? "조치 사유는 2자 이상 1000자 이하로 입력해야 합니다."
            : null;
    }
}
