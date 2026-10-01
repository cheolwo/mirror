using Hongdal.Contracts.Common.Community;
using Hongdal.Domain.Community;
using System.Text.Json;

namespace Hongdal.Services.Community;

internal static class CommunityPostResponseMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static PlatformCommunityPostResponse ToResponse(
        PlatformCommunityPost entity,
        PlatformCommunityPostLedgerContextResponse? ledgerContext = null)
    {
        var isReportBoardPost = IsReportCategory(entity.Category) || entity.IsReportBoardPost;
        var systemPostKind = CommunityAutomatedPostPublication.GetSystemPostKind(entity);
        var isSystemGenerated = systemPostKind is not null;
        var reporterDisplayName = isReportBoardPost ? "신고자" : entity.Nickname;
        var reportedDisplayName = isReportBoardPost ? "피신고자" : string.Empty;

        return new PlatformCommunityPostResponse
        {
            Id = entity.Id,
            AppKey = entity.AppKey,
            Category = entity.Category,
            WorkflowTag = isReportBoardPost ? "안전센터" : entity.WorkflowTag,
            RoleTag = isReportBoardPost ? "보호 기록" : entity.RoleTag,
            Title = isReportBoardPost ? "보호된 신고·분쟁 기록" : entity.Title,
            Body = isReportBoardPost
                ? "신고 원문과 첨부·댓글은 공개 게시판에서 제공하지 않습니다."
                : entity.Body,
            OriginalLanguageCode = isReportBoardPost
                ? CommunityDisplayLanguageCodes.Korean
                : CommunityPostLanguageResolver.Resolve(
                    entity.OriginalLanguageCode,
                    entity.Title,
                    entity.Body),
            SharedLinkUrl = isReportBoardPost ? null : entity.SharedLinkUrl,
            SalesOffer = isReportBoardPost ? null : DeserializeSalesOffer(entity.SalesOfferJson),
            커뮤니티원장Id = isReportBoardPost ? null : entity.커뮤니티원장Id,
            원장Context = isReportBoardPost ? null : ledgerContext,
            Nickname = isReportBoardPost ? reporterDisplayName : entity.Nickname,
            IsAuthorDisplayCountryPublic = !isReportBoardPost && entity.IsAuthorDisplayCountryPublic,
            AuthorDisplayCountryCode = !isReportBoardPost && entity.IsAuthorDisplayCountryPublic
                ? entity.AuthorDisplayCountryCode
                : null,
            AuthorDisplayCountryName = !isReportBoardPost && entity.IsAuthorDisplayCountryPublic
                ? entity.AuthorDisplayCountryName
                : null,
            IsSystemGenerated = isSystemGenerated,
            SystemPostKind = systemPostKind,
            PrivacyNotice = isReportBoardPost
                ? "신고·분쟁 기록은 공개 목록에서 제외되며 원문과 첨부·댓글을 공개하지 않습니다."
                : CommunityAutomatedPostPublication.GetPrivacyNotice(systemPostKind),
            IsReportBoardPost = isReportBoardPost,
            ReporterDisplayName = reporterDisplayName,
            ReportedDisplayName = reportedDisplayName,
            ViewerReportRole = PlatformCommunityReportViewerRoles.Observer,
            IsReportSubjectMasked = isReportBoardPost,
            IsOperatorPinned = entity.IsOperatorPinned,
            OperatorPinnedAtUtc = entity.OperatorPinnedAtUtc,
            IsCommunityMomentumPromoted = !isReportBoardPost && entity.IsCommunityMomentumPromoted,
            CommunityMomentumCode = !isReportBoardPost && entity.IsCommunityMomentumPromoted
                ? entity.CommunityMomentumCode
                : null,
            CommunityMomentumMessage = !isReportBoardPost && entity.IsCommunityMomentumPromoted
                ? entity.CommunityMomentumMessage
                : null,
            CommunityMomentumRoleParticipantCount = !isReportBoardPost && entity.IsCommunityMomentumPromoted
                ? entity.CommunityMomentumRoleParticipantCount
                : 0,
            CommunityMomentumUpdatedAtUtc = !isReportBoardPost && entity.IsCommunityMomentumPromoted
                ? entity.CommunityMomentumUpdatedAtUtc
                : null,
            RecommendationCount = isReportBoardPost ? 0 : entity.RecommendationCount,
            CommentCount = isReportBoardPost ? 0 : entity.CommentCount,
            LastEngagedAtUtc = isReportBoardPost ? null : entity.LastEngagedAtUtc,
            IsTrending = !isReportBoardPost
                         && !entity.IsOperatorPinned
                         && (entity.RecommendationCount >= 3 || entity.CommentCount >= 3),
            PublicationStatusCode = entity.PublicationStatusCode,
            ScheduledPublishAtUtc = entity.ScheduledPublishAtUtc,
            PublishedAtUtc = entity.PublishedAtUtc,
            PublicationAttemptCount = entity.PublicationAttemptCount,
            PublicationLastError = entity.PublicationLastError,
            CreatedAtUtc = entity.CreatedAtUtc,
            UpdatedAtUtc = entity.UpdatedAtUtc,
            Attachments = isReportBoardPost
                ? []
                : entity.Attachments
                    .OrderBy(attachment => attachment.UploadedAtUtc)
                    .Select(ToAttachmentResponse)
                    .ToArray(),
            RecentComments = isReportBoardPost
                ? []
                : entity.Comments
                    .Where(comment => !comment.IsDeleted && !comment.IsOperatorHidden)
                    .OrderByDescending(comment => comment.CreatedAtUtc)
                    .Take(3)
                    .Select(ToCommentResponse)
                    .ToArray()
        };
    }

    public static PlatformCommunityPostCommentResponse ToCommentResponse(
        PlatformCommunityPostComment entity)
        => new()
        {
            Id = entity.Id,
            Nickname = entity.Nickname,
            Body = entity.Body,
            ReportCount = entity.ReportCount,
            CreatedAtUtc = entity.CreatedAtUtc
        };

    public static PlatformCommunityPostAttachmentResponse ToAttachmentResponse(
        PlatformCommunityPostAttachment entity)
        => new()
        {
            Id = entity.Id,
            Url = entity.Url,
            BucketName = entity.BucketName,
            ObjectName = entity.ObjectName,
            OriginalFileName = entity.OriginalFileName,
            ContentType = entity.ContentType,
            FileSizeBytes = entity.FileSizeBytes,
            CommentCount = entity.CommentCount,
            UploadedAtUtc = entity.UploadedAtUtc,
            RecentComments = entity.Comments
                .Where(comment => !comment.IsDeleted && !comment.IsOperatorHidden)
                .OrderByDescending(comment => comment.CreatedAtUtc)
                .Take(3)
                .Select(ToAttachmentCommentResponse)
                .ToArray()
        };

    public static PlatformCommunityPostAttachmentCommentResponse ToAttachmentCommentResponse(
        PlatformCommunityPostAttachmentComment entity)
        => new()
        {
            Id = entity.Id,
            AttachmentId = entity.AttachmentId,
            Nickname = entity.Nickname,
            Body = entity.Body,
            ReportCount = entity.ReportCount,
            CreatedAtUtc = entity.CreatedAtUtc
        };

    private static PlatformCommunityPostSalesOfferResponse? DeserializeSalesOffer(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<PlatformCommunityPostSalesOfferResponse>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool IsReportCategory(string? category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return false;
        }

        return category.Contains("신고", StringComparison.OrdinalIgnoreCase)
               || category.Contains("분쟁", StringComparison.OrdinalIgnoreCase)
               || category.Contains("report", StringComparison.OrdinalIgnoreCase);
    }
}
