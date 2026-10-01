using System.Text;
using System.Text.Json;
using Hongdal.Contracts.Common.Metadata;
using Hongdal.Domain.Community;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using 홍달.Data;
using 홍달.Services.Notifications;
using 홍달.Services.Options;

namespace Hongdal.Services.Community;

public interface ICommunityKeywordNotificationProcessor
{
    Task<bool> ProcessNextScanAsync(CancellationToken cancellationToken);
    Task<bool> ProcessNextDeliveryAsync(CancellationToken cancellationToken);
}

[HongdalCommunityV0Module(
    HongdalCommunityV0ModuleKeys.Safety,
    HongdalModuleKind.BackgroundProcessing,
    "명시적으로 구독한 키워드의 게시글 scan과 알림 delivery를 lease·재시도 방식으로 처리",
    ReleaseStage = HongdalCommunityV0ReleaseStages.SafetyAndOperations,
    Boundary = "근접 위치나 거래 성사 가능성으로 자동 알림하지 않으며 사용자 구독과 공개 게시글 범위만 사용합니다.")]
public sealed class CommunityKeywordNotificationProcessor : ICommunityKeywordNotificationProcessor
{
    private readonly HongdalContext _db;
    private readonly ICommunityKeywordMatcher _matcher;
    private readonly IFcmPushService _fcmPushService;
    private readonly CommunityKeywordNotificationOptions _options;
    private readonly ILogger<CommunityKeywordNotificationProcessor> _logger;

    public CommunityKeywordNotificationProcessor(
        HongdalContext db,
        ICommunityKeywordMatcher matcher,
        IFcmPushService fcmPushService,
        IOptions<CommunityKeywordNotificationOptions> options,
        ILogger<CommunityKeywordNotificationProcessor> logger)
    {
        _db = db;
        _matcher = matcher;
        _fcmPushService = fcmPushService;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<bool> ProcessNextScanAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var leaseExpiredAt = now.AddMinutes(-Math.Max(1, _options.LeaseTimeoutMinutes));
        var candidateId = await _db.PlatformCommunityPostKeywordScans
            .AsNoTracking()
            .Where(x =>
                ((x.Status == CommunityKeywordScanStatuses.Pending
                  || x.Status == CommunityKeywordScanStatuses.RetryWaiting)
                 && (x.NextAttemptAtUtc == null || x.NextAttemptAtUtc <= now))
                || (x.Status == CommunityKeywordScanStatuses.Processing
                    && x.UpdatedAtUtc <= leaseExpiredAt))
            .OrderBy(x => x.NextAttemptAtUtc)
            .ThenBy(x => x.Id)
            .Select(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (candidateId == 0)
        {
            return false;
        }

        var token = Guid.NewGuid().ToString("N");
        var claimed = await _db.PlatformCommunityPostKeywordScans
            .Where(x => x.Id == candidateId &&
                ((((x.Status == CommunityKeywordScanStatuses.Pending
                    || x.Status == CommunityKeywordScanStatuses.RetryWaiting)
                   && (x.NextAttemptAtUtc == null || x.NextAttemptAtUtc <= now)))
                 || (x.Status == CommunityKeywordScanStatuses.Processing
                     && x.UpdatedAtUtc <= leaseExpiredAt)))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, CommunityKeywordScanStatuses.Processing)
                .SetProperty(x => x.ProcessingToken, token)
                .SetProperty(x => x.AttemptCount, x => x.AttemptCount + 1)
                .SetProperty(x => x.UpdatedAtUtc, now),
                cancellationToken);
        if (claimed == 0)
        {
            return true;
        }

        try
        {
            var scan = await _db.PlatformCommunityPostKeywordScans
                .Include(x => x.Post)
                .SingleAsync(x => x.Id == candidateId && x.ProcessingToken == token, cancellationToken);
            if (scan.Post.IsDeleted)
            {
                CompleteScan(scan, now);
                await _db.SaveChangesAsync(cancellationToken);
                return true;
            }

            var post = scan.Post;
            var subscriptionsQuery = _db.CommunityKeywordSubscriptions
                .AsNoTracking()
                .Where(x => x.IsActive);
            if (!string.Equals(post.AppKey, "platform", StringComparison.OrdinalIgnoreCase))
            {
                subscriptionsQuery = subscriptionsQuery.Where(x =>
                    x.AppKey == post.AppKey || x.AppKey == "platform");
            }

            var subscriptions = await subscriptionsQuery.ToListAsync(cancellationToken);
            var matchedByUser = subscriptions
                .Where(x => !string.Equals(x.UserId, post.AuthorUserId, StringComparison.Ordinal))
                .Where(x => _matcher.IsMatch(x.NormalizedKeyword, post))
                .GroupBy(x => x.UserId, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .GroupBy(x => x.NormalizedKeyword, StringComparer.Ordinal)
                        .Select(keywordGroup => keywordGroup.First().Keyword)
                        .OrderBy(x => x, StringComparer.Ordinal)
                        .ToArray(),
                    StringComparer.Ordinal);
            if (matchedByUser.Count == 0)
            {
                CompleteScan(scan, now);
                await _db.SaveChangesAsync(cancellationToken);
                return true;
            }

            var matchedUserIds = matchedByUser.Keys.ToArray();
            var existingUserIds = await _db.CommunityKeywordNotifications
                .AsNoTracking()
                .Where(x => x.PostId == post.Id && matchedUserIds.Contains(x.UserId))
                .Select(x => x.UserId)
                .ToListAsync(cancellationToken);
            var existingUsers = existingUserIds.ToHashSet(StringComparer.Ordinal);

            var installationsQuery = _db.HongdalMobilePushInstallations
                .AsNoTracking()
                .Where(x => x.IsActive && matchedUserIds.Contains(x.UserId));
            if (!string.Equals(post.AppKey, "platform", StringComparison.OrdinalIgnoreCase))
            {
                installationsQuery = installationsQuery.Where(x =>
                    x.AppKey == post.AppKey || x.AppKey == "platform");
            }

            var installationsByUser = (await installationsQuery.ToListAsync(cancellationToken))
                .GroupBy(x => x.UserId, StringComparer.Ordinal)
                .ToDictionary(x => x.Key, x => x.ToArray(), StringComparer.Ordinal);

            foreach (var (userId, keywords) in matchedByUser)
            {
                if (existingUsers.Contains(userId))
                {
                    continue;
                }

                var notification = new CommunityKeywordNotification
                {
                    UserId = userId,
                    PostId = post.Id,
                    Post = post,
                    PostAppKey = post.AppKey,
                    PostCategory = post.Category,
                    PostTitle = post.Title,
                    PostExcerpt = BuildExcerpt(post.Body),
                    PostAuthorNickname = post.Nickname,
                    MatchedKeywordsJson = JsonSerializer.Serialize(keywords),
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                };
                if (installationsByUser.TryGetValue(userId, out var installations))
                {
                    foreach (var installation in installations)
                    {
                        notification.Deliveries.Add(new CommunityKeywordNotificationDelivery
                        {
                            InstallationId = installation.Id,
                            Status = CommunityKeywordDeliveryStatuses.Pending,
                            NextAttemptAtUtc = now,
                            CreatedAtUtc = now,
                            UpdatedAtUtc = now
                        });
                    }
                }

                _db.CommunityKeywordNotifications.Add(notification);
            }

            CompleteScan(scan, now);
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            await MarkScanFailedAsync(candidateId, ex, cancellationToken);
        }

        return true;
    }

    public async Task<bool> ProcessNextDeliveryAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var leaseExpiredAt = now.AddMinutes(-Math.Max(1, _options.LeaseTimeoutMinutes));
        var candidateId = await _db.CommunityKeywordNotificationDeliveries
            .AsNoTracking()
            .Where(x =>
                ((x.Status == CommunityKeywordDeliveryStatuses.Pending
                  || x.Status == CommunityKeywordDeliveryStatuses.RetryWaiting)
                 && (x.NextAttemptAtUtc == null || x.NextAttemptAtUtc <= now))
                || (x.Status == CommunityKeywordDeliveryStatuses.Processing
                    && x.UpdatedAtUtc <= leaseExpiredAt))
            .OrderBy(x => x.NextAttemptAtUtc)
            .ThenBy(x => x.Id)
            .Select(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (candidateId == 0)
        {
            return false;
        }

        var token = Guid.NewGuid().ToString("N");
        var claimed = await _db.CommunityKeywordNotificationDeliveries
            .Where(x => x.Id == candidateId &&
                ((((x.Status == CommunityKeywordDeliveryStatuses.Pending
                    || x.Status == CommunityKeywordDeliveryStatuses.RetryWaiting)
                   && (x.NextAttemptAtUtc == null || x.NextAttemptAtUtc <= now)))
                 || (x.Status == CommunityKeywordDeliveryStatuses.Processing
                     && x.UpdatedAtUtc <= leaseExpiredAt)))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, CommunityKeywordDeliveryStatuses.Processing)
                .SetProperty(x => x.ProcessingToken, token)
                .SetProperty(x => x.AttemptCount, x => x.AttemptCount + 1)
                .SetProperty(x => x.UpdatedAtUtc, now),
                cancellationToken);
        if (claimed == 0)
        {
            return true;
        }

        var delivery = await _db.CommunityKeywordNotificationDeliveries
            .Include(x => x.Installation)
            .Include(x => x.Notification)
                .ThenInclude(x => x.Post)
            .SingleAsync(x => x.Id == candidateId && x.ProcessingToken == token, cancellationToken);

        if (!delivery.Installation.IsActive)
        {
            SkipDelivery(delivery, "모바일 푸시 설치가 비활성화되어 있습니다.", now);
            await _db.SaveChangesAsync(cancellationToken);
            return true;
        }

        if (delivery.Notification.IsRead || delivery.Notification.Post.IsDeleted)
        {
            SkipDelivery(delivery, "이미 확인했거나 삭제된 게시글 알림입니다.", now);
            await _db.SaveChangesAsync(cancellationToken);
            return true;
        }

        try
        {
            var keywords = CommunityKeywordInboxService.DeserializeKeywords(
                delivery.Notification.MatchedKeywordsJson);
            var primaryKeyword = keywords.FirstOrDefault() ?? "구독 키워드";
            var sent = await _fcmPushService.SendAsync(
                new FcmPushMessage(
                    delivery.Installation.PushToken,
                    $"‘{primaryKeyword}’ 새 게시글",
                    BuildPushBody(delivery.Notification),
                    new Dictionary<string, string>
                    {
                        ["type"] = "community_keyword_post",
                        ["notificationId"] = delivery.NotificationId.ToString(),
                        ["postId"] = delivery.Notification.PostId.ToString(),
                        ["keyword"] = primaryKeyword,
                        ["appKey"] = delivery.Notification.PostAppKey,
                        ["deepLink"] = $"hongdal://community/posts/{delivery.Notification.PostId}"
                    },
                    HighPriority: false),
                cancellationToken);
            if (sent)
            {
                delivery.Status = CommunityKeywordDeliveryStatuses.Succeeded;
                delivery.SentAtUtc = now;
                delivery.NextAttemptAtUtc = null;
                delivery.LastError = null;
                delivery.ProcessingToken = null;
                delivery.UpdatedAtUtc = now;
            }
            else
            {
                SetDeliveryFailure(delivery, "FCM이 키워드 알림을 전송하지 못했습니다.", now);
            }

            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            SetDeliveryFailure(delivery, ex.Message, DateTime.UtcNow);
            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogWarning(
                ex,
                "커뮤니티 키워드 푸시 발송에 실패했습니다. DeliveryId={DeliveryId}",
                delivery.Id);
        }

        return true;
    }

    private async Task MarkScanFailedAsync(
        long scanId,
        Exception exception,
        CancellationToken cancellationToken)
    {
        _db.ChangeTracker.Clear();
        var scan = await _db.PlatformCommunityPostKeywordScans
            .SingleAsync(x => x.Id == scanId, cancellationToken);
        var failedAt = DateTime.UtcNow;
        scan.ProcessingToken = null;
        scan.LastError = Truncate(exception.Message, 2000);
        scan.UpdatedAtUtc = failedAt;
        if (scan.AttemptCount >= Math.Clamp(_options.MaxAttempts, 1, 20))
        {
            scan.Status = CommunityKeywordScanStatuses.Failed;
            scan.NextAttemptAtUtc = null;
        }
        else
        {
            scan.Status = CommunityKeywordScanStatuses.RetryWaiting;
            scan.NextAttemptAtUtc = failedAt.Add(GetRetryDelay(scan.AttemptCount));
        }

        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogWarning(
            exception,
            "커뮤니티 게시글 키워드 매칭에 실패했습니다. ScanId={ScanId}, Attempt={Attempt}",
            scan.Id,
            scan.AttemptCount);
    }

    private void SetDeliveryFailure(
        CommunityKeywordNotificationDelivery delivery,
        string error,
        DateTime failedAt)
    {
        delivery.ProcessingToken = null;
        delivery.LastError = Truncate(error, 1000);
        delivery.UpdatedAtUtc = failedAt;
        if (delivery.AttemptCount >= Math.Clamp(_options.MaxAttempts, 1, 20))
        {
            delivery.Status = CommunityKeywordDeliveryStatuses.Failed;
            delivery.NextAttemptAtUtc = null;
        }
        else
        {
            delivery.Status = CommunityKeywordDeliveryStatuses.RetryWaiting;
            delivery.NextAttemptAtUtc = failedAt.Add(GetRetryDelay(delivery.AttemptCount));
        }
    }

    private TimeSpan GetRetryDelay(int attemptCount)
    {
        var baseSeconds = Math.Max(5, _options.RetryDelaySeconds);
        var seconds = Math.Min(3600, baseSeconds * Math.Pow(2, Math.Max(0, attemptCount - 1)));
        return TimeSpan.FromSeconds(seconds);
    }

    private static void CompleteScan(PlatformCommunityPostKeywordScan scan, DateTime completedAt)
    {
        scan.Status = CommunityKeywordScanStatuses.Completed;
        scan.CompletedAtUtc = completedAt;
        scan.NextAttemptAtUtc = null;
        scan.LastError = null;
        scan.ProcessingToken = null;
        scan.UpdatedAtUtc = completedAt;
    }

    private static void SkipDelivery(
        CommunityKeywordNotificationDelivery delivery,
        string reason,
        DateTime now)
    {
        delivery.Status = CommunityKeywordDeliveryStatuses.Skipped;
        delivery.LastError = reason;
        delivery.NextAttemptAtUtc = null;
        delivery.ProcessingToken = null;
        delivery.UpdatedAtUtc = now;
    }

    private static string BuildExcerpt(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(Math.Min(body.Length, 300));
        var previousWasWhitespace = false;
        foreach (var character in body.Trim())
        {
            if (char.IsWhiteSpace(character))
            {
                if (!previousWasWhitespace && builder.Length > 0)
                {
                    builder.Append(' ');
                }

                previousWasWhitespace = true;
            }
            else
            {
                builder.Append(character);
                previousWasWhitespace = false;
            }

            if (builder.Length >= 300)
            {
                break;
            }
        }

        return builder.ToString().TrimEnd();
    }

    private static string BuildPushBody(CommunityKeywordNotification notification)
    {
        var value = string.IsNullOrWhiteSpace(notification.PostAuthorNickname)
            ? notification.PostTitle
            : $"{notification.PostAuthorNickname} · {notification.PostTitle}";
        return value.Length <= 160 ? value : value[..160];
    }

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];
}
