using System.Security.Cryptography;
using System.Text;
using Hongdal.Contracts.Common.Content;
using Hongdal.Domain.Content;
using Hongdal.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using 홍달.Data;
using 홍달.Services.Notifications;
using 홍달.Services.Options;

namespace Hongdal.Services.Content;

public sealed record HongikHakdangCardDeliveryCycleResult(
    int EnqueuedCount,
    int ProcessedCount,
    int SucceededCount,
    int FailedCount);

public interface IHongikHakdangCardDeliveryService
{
    Task<HongikHakdangCardCatalogDto> GetCatalogAsync(
        string? collectionKey,
        CancellationToken cancellationToken);

    Task<HongikHakdangTodayCardDto> GetTodayAsync(
        string? timeZoneId,
        CancellationToken cancellationToken);

    Task<HongikHakdangCardDeliveryPreferenceDto> GetPreferenceAsync(
        string userId,
        CancellationToken cancellationToken);

    Task<HongikHakdangCardDeliveryPreferenceDto> UpdatePreferenceAsync(
        string userId,
        HongikHakdangCardDeliveryPreferenceUpdateRequest request,
        CancellationToken cancellationToken);

    Task<HongikHakdangCardDeliveryCycleResult> RunDeliveryCycleAsync(
        CancellationToken cancellationToken);
}

public sealed class HongikHakdangCardDeliveryService : IHongikHakdangCardDeliveryService
{
    private readonly HongdalContext _db;
    private readonly IHongikHakdangCardVariantService _variantService;
    private readonly IHongikHakdangCardMediaTokenService _mediaTokenService;
    private readonly IHongikHakdangCardSelectionPolicy _selectionPolicy;
    private readonly IFcmPushService _fcmPushService;
    private readonly HongikHakdangCardOptions _options;
    private readonly ILogger<HongikHakdangCardDeliveryService> _logger;

    public HongikHakdangCardDeliveryService(
        HongdalContext db,
        IHongikHakdangCardVariantService variantService,
        IHongikHakdangCardMediaTokenService mediaTokenService,
        IHongikHakdangCardSelectionPolicy selectionPolicy,
        IFcmPushService fcmPushService,
        IOptions<HongikHakdangCardOptions> options,
        ILogger<HongikHakdangCardDeliveryService> logger)
    {
        _db = db;
        _variantService = variantService;
        _mediaTokenService = mediaTokenService;
        _selectionPolicy = selectionPolicy;
        _fcmPushService = fcmPushService;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<HongikHakdangCardCatalogDto> GetCatalogAsync(
        string? collectionKey,
        CancellationToken cancellationToken)
    {
        await _variantService.EnsureActiveVariantsAsync(cancellationToken);

        var normalizedCollectionKey = NormalizeOptional(collectionKey, 200);
        var query = _db.HongikHakdangCards
            .AsNoTracking()
            .Include(x => x.ImageVariants)
            .Include(x => x.Collections)
                .ThenInclude(x => x.Collection)
            .Where(x => x.IsActive && x.IsAdminEnabled);
        if (normalizedCollectionKey is not null)
        {
            query = query.Where(x => x.Collections.Any(item =>
                item.IsActive
                && item.Collection.IsActive
                && item.Collection.IsAdminEnabled
                && item.Collection.SourceKey == normalizedCollectionKey));
        }

        var cards = await query.OrderBy(x => x.Id).ToListAsync(cancellationToken);
        var readyCards = cards
            .Where(HasRequiredVariants)
            .Select(BuildMobileCard)
            .ToArray();

        return new HongikHakdangCardCatalogDto(
            BuildCatalogVersion(readyCards),
            DateTime.UtcNow,
            readyCards);
    }

    public async Task<HongikHakdangTodayCardDto> GetTodayAsync(
        string? timeZoneId,
        CancellationToken cancellationToken)
    {
        var normalizedTimeZoneId = NormalizeTimeZoneId(timeZoneId ?? _options.DefaultTimeZoneId);
        await _variantService.EnsureActiveVariantsAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var localDate = ResolveLocalDate(now, normalizedTimeZoneId);
        var selection = await GetOrCreateSelectionAsync(localDate, normalizedTimeZoneId, now, cancellationToken);

        var card = await _db.HongikHakdangCards
            .AsNoTracking()
            .Include(x => x.ImageVariants)
            .Include(x => x.Collections)
                .ThenInclude(x => x.Collection)
            .SingleAsync(x => x.Id == selection.CardId, cancellationToken);
        return new HongikHakdangTodayCardDto(localDate, normalizedTimeZoneId, BuildMobileCard(card));
    }

    public async Task<HongikHakdangCardDeliveryPreferenceDto> GetPreferenceAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        var normalizedUserId = NormalizeUserId(userId);
        var preference = await _db.HongikHakdangCardDeliveryPreferences
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.UserId == normalizedUserId, cancellationToken);
        return preference is null
            ? DefaultPreference()
            : ToDto(preference);
    }

    public async Task<HongikHakdangCardDeliveryPreferenceDto> UpdatePreferenceAsync(
        string userId,
        HongikHakdangCardDeliveryPreferenceUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var normalizedUserId = NormalizeUserId(userId);
        ArgumentNullException.ThrowIfNull(request);
        if (!HongikHakdangCardDeliveryModes.IsSupported(request.DeliveryMode))
        {
            throw new ArgumentException("지원하지 않는 카드 전달 방식입니다.", nameof(request));
        }

        if (request.LocalDeliveryMinute is < 0 or > 1439)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "알림 시각은 0~1439분 사이여야 합니다.");
        }

        var timeZoneId = NormalizeTimeZoneId(request.TimeZoneId);
        var collectionKey = NormalizeOptional(request.PreferredCollectionKey, 200);
        if (collectionKey is not null)
        {
            var exists = await _db.HongikHakdangCardCollections
                .AnyAsync(
                    x => x.IsActive && x.IsAdminEnabled && x.SourceKey == collectionKey,
                    cancellationToken);
            if (!exists)
            {
                throw new ArgumentException("선택한 카드 모음을 찾을 수 없습니다.", nameof(request));
            }
        }

        var now = DateTime.UtcNow;
        var preference = await _db.HongikHakdangCardDeliveryPreferences
            .SingleOrDefaultAsync(x => x.UserId == normalizedUserId, cancellationToken);
        if (preference is null)
        {
            preference = new HongikHakdangCardDeliveryPreference
            {
                UserId = normalizedUserId,
                CreatedAtUtc = now
            };
            _db.HongikHakdangCardDeliveryPreferences.Add(preference);
        }

        preference.Enabled = request.Enabled;
        preference.DeliveryMode = request.DeliveryMode;
        preference.PushEnabled = request.PushEnabled;
        preference.LocalDeliveryMinute = request.LocalDeliveryMinute;
        preference.TimeZoneId = timeZoneId;
        preference.ShuffleWithoutRepeats = request.ShuffleWithoutRepeats;
        preference.PreferredCollectionKey = collectionKey;
        preference.UpdatedAtUtc = now;
        await _db.SaveChangesAsync(cancellationToken);
        return ToDto(preference);
    }

    public async Task<HongikHakdangCardDeliveryCycleResult> RunDeliveryCycleAsync(
        CancellationToken cancellationToken)
    {
        if (!_options.Enabled || !_options.DeliveryEnabled)
        {
            return new HongikHakdangCardDeliveryCycleResult(0, 0, 0, 0);
        }

        var now = DateTime.UtcNow;
        var enqueued = await EnqueueDueAsync(now, cancellationToken);
        var dispatch = await DispatchDueAsync(now, cancellationToken);
        return new HongikHakdangCardDeliveryCycleResult(
            enqueued,
            dispatch.Processed,
            dispatch.Succeeded,
            dispatch.Failed);
    }

    private async Task<int> EnqueueDueAsync(DateTime nowUtc, CancellationToken cancellationToken)
    {
        var preferences = await _db.HongikHakdangCardDeliveryPreferences
            .AsNoTracking()
            .Where(x => x.Enabled && x.PushEnabled)
            .ToListAsync(cancellationToken);
        if (preferences.Count == 0)
        {
            return 0;
        }

        var userIds = preferences.Select(x => x.UserId).Distinct().ToArray();
        var installations = await _db.HongdalMobilePushInstallations
            .AsNoTracking()
            .Where(x => x.IsActive && userIds.Contains(x.UserId))
            .ToListAsync(cancellationToken);
        var installationsByUser = installations
            .GroupBy(x => x.UserId)
            .ToDictionary(x => x.Key, x => x.ToArray());
        var selectionByDateAndZone = new Dictionary<string, HongikHakdangDailyCardSelection>(StringComparer.Ordinal);
        var candidates = new List<HongikHakdangCardDeliveryOutbox>();

        foreach (var preference in preferences)
        {
            if (!installationsByUser.TryGetValue(preference.UserId, out var userInstallations))
            {
                continue;
            }

            var localNow = ResolveLocalDateTime(nowUtc, preference.TimeZoneId);
            if (localNow.Hour * 60 + localNow.Minute < preference.LocalDeliveryMinute)
            {
                continue;
            }

            var localDate = DateOnly.FromDateTime(localNow);
            var selectionKey = $"{localDate:yyyy-MM-dd}|{preference.TimeZoneId}";
            if (!selectionByDateAndZone.TryGetValue(selectionKey, out var selection))
            {
                selection = await GetOrCreateSelectionAsync(
                    localDate,
                    preference.TimeZoneId,
                    nowUtc,
                    cancellationToken);
                selectionByDateAndZone.Add(selectionKey, selection);
            }

            foreach (var installation in userInstallations)
            {
                candidates.Add(new HongikHakdangCardDeliveryOutbox
                {
                    IdempotencyKey = $"{localDate:yyyyMMdd}:{installation.Id}",
                    UserId = preference.UserId,
                    InstallationId = installation.Id,
                    CardId = selection.CardId,
                    SelectionDate = localDate,
                    Status = HongikHakdangCardDeliveryOutboxStatuses.Pending,
                    NextAttemptAtUtc = nowUtc,
                    CreatedAtUtc = nowUtc,
                    UpdatedAtUtc = nowUtc
                });
            }
        }

        if (candidates.Count == 0)
        {
            return 0;
        }

        var keys = candidates.Select(x => x.IdempotencyKey).ToArray();
        var existingKeys = await _db.HongikHakdangCardDeliveryOutbox
            .AsNoTracking()
            .Where(x => keys.Contains(x.IdempotencyKey))
            .Select(x => x.IdempotencyKey)
            .ToListAsync(cancellationToken);
        var existing = existingKeys.ToHashSet(StringComparer.Ordinal);
        var newItems = candidates.Where(x => !existing.Contains(x.IdempotencyKey)).ToArray();
        if (newItems.Length == 0)
        {
            return 0;
        }

        _db.HongikHakdangCardDeliveryOutbox.AddRange(newItems);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            return newItems.Length;
        }
        catch (DbUpdateException)
        {
            foreach (var item in newItems)
            {
                _db.Entry(item).State = EntityState.Detached;
            }

            var racedKeys = await _db.HongikHakdangCardDeliveryOutbox
                .AsNoTracking()
                .Where(x => keys.Contains(x.IdempotencyKey))
                .Select(x => x.IdempotencyKey)
                .ToListAsync(cancellationToken);
            var raced = racedKeys.ToHashSet(StringComparer.Ordinal);
            var retryItems = candidates.Where(x => !raced.Contains(x.IdempotencyKey)).ToArray();
            if (retryItems.Length == 0)
            {
                return 0;
            }

            _db.HongikHakdangCardDeliveryOutbox.AddRange(retryItems);
            await _db.SaveChangesAsync(cancellationToken);
            return retryItems.Length;
        }
    }

    private async Task<(int Processed, int Succeeded, int Failed)> DispatchDueAsync(
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var maxAttempts = Math.Clamp(_options.MaxDeliveryAttempts, 1, 20);
        var items = await _db.HongikHakdangCardDeliveryOutbox
            .Include(x => x.Installation)
            .Include(x => x.Card)
                .ThenInclude(x => x.ImageVariants)
            .Where(x => (x.Status == HongikHakdangCardDeliveryOutboxStatuses.Pending
                         || x.Status == HongikHakdangCardDeliveryOutboxStatuses.Failed)
                        && x.AttemptCount < maxAttempts
                        && x.NextAttemptAtUtc <= nowUtc)
            .OrderBy(x => x.CreatedAtUtc)
            .Take(Math.Clamp(_options.DeliveryBatchSize, 1, 500))
            .ToListAsync(cancellationToken);
        var succeeded = 0;
        var failed = 0;

        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            item.AttemptCount++;
            item.UpdatedAtUtc = nowUtc;

            if (!item.Installation.IsActive)
            {
                item.Status = HongikHakdangCardDeliveryOutboxStatuses.Skipped;
                item.LastError = "모바일 설치가 비활성화되어 있습니다.";
                continue;
            }

            var notificationVariant = item.Card.ImageVariants.FirstOrDefault(x =>
                x.VariantKind == HongikHakdangCardImageVariantKinds.Notification);
            var imageUrl = notificationVariant is null
                ? null
                : CreateAbsoluteMediaUrl(notificationVariant.Id);

            try
            {
                var sent = await _fcmPushService.SendAsync(
                    new FcmPushMessage(
                        item.Installation.PushToken,
                        "오늘의 홍익학당 카드",
                        BuildNotificationBody(item.Card),
                        new Dictionary<string, string>
                        {
                            ["type"] = "HongikHakdangDailyCard",
                            ["cardId"] = item.CardId.ToString(),
                            ["selectionDate"] = item.SelectionDate.ToString("yyyy-MM-dd"),
                            ["deepLink"] = $"hongdal://prajna/cards/{item.CardId}"
                        },
                        imageUrl,
                        HighPriority: false),
                    cancellationToken);
                if (sent)
                {
                    item.Status = HongikHakdangCardDeliveryOutboxStatuses.Succeeded;
                    item.SentAtUtc = nowUtc;
                    item.LastError = null;
                    succeeded++;
                }
                else
                {
                    MarkFailed(item, nowUtc, "FCM이 카드 알림을 전송하지 못했습니다.");
                    failed++;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                MarkFailed(item, nowUtc, ex.Message);
                failed++;
                _logger.LogWarning(
                    ex,
                    "홍익학당 카드 알림 발송 실패. OutboxId={OutboxId} InstallationId={InstallationId}",
                    item.Id,
                    item.InstallationId);
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        return (items.Count, succeeded, failed);
    }

    private async Task<HongikHakdangDailyCardSelection> GetOrCreateSelectionAsync(
        DateOnly selectionDate,
        string timeZoneId,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var normalizedTimeZoneId = NormalizeTimeZoneId(timeZoneId);
        var existing = await _db.HongikHakdangDailyCardSelections
            .SingleOrDefaultAsync(
                x => x.SelectionDate == selectionDate && x.TimeZoneId == normalizedTimeZoneId,
                cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var cards = await _db.HongikHakdangCards
            .AsNoTracking()
            .Where(x => x.IsActive
                        && x.IsAdminEnabled
                        && x.ImageVariants.Any(variant =>
                            variant.VariantKind == HongikHakdangCardImageVariantKinds.Notification)
                        && x.ImageVariants.Any(variant =>
                            variant.VariantKind == HongikHakdangCardImageVariantKinds.LockScreenPortrait))
            .Select(x => x.Id)
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);
        if (cards.Count == 0)
        {
            await _variantService.EnsureActiveVariantsAsync(cancellationToken);
            cards = await _db.HongikHakdangCards
                .AsNoTracking()
                .Where(x => x.IsActive
                            && x.IsAdminEnabled
                            && x.ImageVariants.Any(variant =>
                                variant.VariantKind == HongikHakdangCardImageVariantKinds.Notification)
                            && x.ImageVariants.Any(variant =>
                                variant.VariantKind == HongikHakdangCardImageVariantKinds.LockScreenPortrait))
                .Select(x => x.Id)
                .OrderBy(x => x)
                .ToListAsync(cancellationToken);
        }

        var recent = await _db.HongikHakdangDailyCardSelections
            .AsNoTracking()
            .Where(x => x.TimeZoneId == normalizedTimeZoneId && x.SelectionDate < selectionDate)
            .OrderByDescending(x => x.SelectionDate)
            .Take(Math.Max(0, cards.Count - 1))
            .Select(x => x.CardId)
            .ToListAsync(cancellationToken);
        var selectedCardId = _selectionPolicy.Select(
            selectionDate,
            normalizedTimeZoneId,
            cards,
            recent);
        var selection = new HongikHakdangDailyCardSelection
        {
            SelectionDate = selectionDate,
            TimeZoneId = normalizedTimeZoneId,
            CardId = selectedCardId,
            SelectedAtUtc = nowUtc
        };
        _db.HongikHakdangDailyCardSelections.Add(selection);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            return selection;
        }
        catch (DbUpdateException)
        {
            _db.Entry(selection).State = EntityState.Detached;
            var racedSelection = await _db.HongikHakdangDailyCardSelections
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x => x.SelectionDate == selectionDate && x.TimeZoneId == normalizedTimeZoneId,
                    cancellationToken);
            if (racedSelection is not null)
            {
                return racedSelection;
            }

            throw;
        }
    }

    private HongikHakdangMobileCardDto BuildMobileCard(HongikHakdangCard card)
    {
        var notification = card.ImageVariants.Single(x =>
            x.VariantKind == HongikHakdangCardImageVariantKinds.Notification);
        var lockScreen = card.ImageVariants.Single(x =>
            x.VariantKind == HongikHakdangCardImageVariantKinds.LockScreenPortrait);
        return new HongikHakdangMobileCardDto(
            card.Id,
            card.Title,
            card.Description,
            card.RelatedUrl,
            _mediaTokenService.CreateRelativeUrl(notification.Id),
            _mediaTokenService.CreateRelativeUrl(lockScreen.Id),
            lockScreen.Sha256,
            card.Collections
                .Where(x => x.IsActive && x.Collection.IsActive && x.Collection.IsAdminEnabled)
                .OrderBy(x => x.Collection.SortOrder)
                .Select(x => x.Collection.SourceKey)
                .Distinct(StringComparer.Ordinal)
                .ToArray());
    }

    private string? CreateAbsoluteMediaUrl(long variantId)
    {
        if (!Uri.TryCreate(_options.PublicBaseUrl, UriKind.Absolute, out var baseUri)
            || baseUri.Scheme != Uri.UriSchemeHttps)
        {
            return null;
        }

        return new Uri(baseUri, _mediaTokenService.CreateRelativeUrl(variantId)).AbsoluteUri;
    }

    private static bool HasRequiredVariants(HongikHakdangCard card)
        => card.ImageVariants.Any(x => x.VariantKind == HongikHakdangCardImageVariantKinds.Notification)
           && card.ImageVariants.Any(x => x.VariantKind == HongikHakdangCardImageVariantKinds.LockScreenPortrait);

    private static string BuildCatalogVersion(IEnumerable<HongikHakdangMobileCardDto> cards)
    {
        var source = string.Join('|', cards.Select(x => $"{x.Id}:{x.ImageSha256}"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source))).ToLowerInvariant();
    }

    private static string BuildNotificationBody(HongikHakdangCard card)
    {
        var text = !string.IsNullOrWhiteSpace(card.Title)
            ? card.Title.Trim()
            : !string.IsNullOrWhiteSpace(card.Description)
                ? card.Description.Trim()
                : "잠시 멈추어 오늘의 카드를 바라보세요.";
        return text.Length <= 160 ? text : text[..160];
    }

    private static void MarkFailed(HongikHakdangCardDeliveryOutbox item, DateTime nowUtc, string error)
    {
        item.Status = HongikHakdangCardDeliveryOutboxStatuses.Failed;
        item.LastError = error.Length <= 1000 ? error : error[..1000];
        var delaySeconds = Math.Min(3600, 30 * Math.Pow(2, Math.Max(0, item.AttemptCount - 1)));
        item.NextAttemptAtUtc = nowUtc.AddSeconds(delaySeconds);
    }

    private HongikHakdangCardDeliveryPreferenceDto DefaultPreference()
        => new(
            false,
            HongikHakdangCardDeliveryModes.EveryLock,
            false,
            8 * 60,
            NormalizeTimeZoneId(_options.DefaultTimeZoneId),
            true,
            null,
            null);

    private static HongikHakdangCardDeliveryPreferenceDto ToDto(
        HongikHakdangCardDeliveryPreference preference)
        => new(
            preference.Enabled,
            preference.DeliveryMode,
            preference.PushEnabled,
            preference.LocalDeliveryMinute,
            preference.TimeZoneId,
            preference.ShuffleWithoutRepeats,
            preference.PreferredCollectionKey,
            preference.UpdatedAtUtc);

    private static string NormalizeUserId(string userId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        var normalized = userId.Trim();
        if (normalized.Length > 450)
        {
            throw new ArgumentOutOfRangeException(nameof(userId));
        }

        return normalized;
    }

    private static string NormalizeTimeZoneId(string timeZoneId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZoneId);
        var normalized = timeZoneId.Trim();
        _ = ResolveTimeZone(normalized);
        return normalized;
    }

    private static DateOnly ResolveLocalDate(DateTime utcNow, string timeZoneId)
        => DateOnly.FromDateTime(ResolveLocalDateTime(utcNow, timeZoneId));

    private static DateTime ResolveLocalDateTime(DateTime utcNow, string timeZoneId)
        => TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(utcNow, DateTimeKind.Utc),
            ResolveTimeZone(timeZoneId));

    private static TimeZoneInfo ResolveTimeZone(string timeZoneId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException) when (timeZoneId == "Asia/Seoul")
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Korea Standard Time");
        }
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        return normalized;
    }
}
