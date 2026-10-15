using System.Globalization;
using Ssalddel.Contracts.Common.Drivers;

namespace DriverApp.Services;

/// <summary>푸시는 조회할 의뢰의 힌트입니다. 금액·주소·수락 권한을 보관하지 않습니다.</summary>
public sealed record DriverRecommendationPushHint(string RequestId, string? OwnerId,
    int? RecommendationRound, DateTimeOffset? ExpiresAtUtc, int PendingCount);

public static class DriverRecommendationPushHintParser
{
    public static readonly IReadOnlyList<string> DataKeys =
    ["type", "appKey", "requestId", "topRequestId", "requestIds", "driverId", "userId",
        "recommendationRound", "expiresAtUtc", "recommendationCount"];

    public static DriverRecommendationPushHint? Parse(IReadOnlyDictionary<string, string> data, DateTimeOffset now)
    {
        if (!data.TryGetValue("type", out var type) || !기사배차추천알림계약.Is배차추천(type)) return null;
        if (data.TryGetValue("appKey", out var appKey) && appKey != 기사앱식별자.CargoYongdalDriverApp) return null;

        var direct = Read(data, "requestId");
        var top = Read(data, "topRequestId");
        if (direct is not null && top is not null && direct != top) return null;
        var ids = Read(data, "requestIds");
        if (ids?.Length > 20000) return null;
        var candidates = ids?.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries) ?? [];
        if (candidates.Length > 100 || candidates.Any(x => !ValidId(x))) return null;
        var requestId = direct ?? top ?? candidates.FirstOrDefault();
        if (!ValidId(requestId) || (candidates.Length > 0 && !candidates.Contains(requestId, StringComparer.Ordinal))) return null;

        var driver = Read(data, "driverId");
        var user = Read(data, "userId");
        if (driver is not null && user is not null && driver != user) return null;
        var owner = driver ?? user;
        if (owner is not null && !ValidId(owner)) return null;

        int? round = null;
        if (data.TryGetValue("recommendationRound", out var roundText))
        {
            if (!int.TryParse(roundText, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) || parsed < 1) return null;
            round = parsed;
        }
        DateTimeOffset? expiry = null;
        if (data.TryGetValue("expiresAtUtc", out var expiryText))
        {
            if (!DateTimeOffset.TryParse(expiryText, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
                || parsed <= now || parsed > now.AddHours(1)) return null;
            expiry = parsed.ToUniversalTime();
        }
        var count = 1;
        if (data.TryGetValue("recommendationCount", out var countText)
            && (!int.TryParse(countText, NumberStyles.None, CultureInfo.InvariantCulture, out count) || count is < 1 or > 1000)) return null;
        return new(requestId!, owner, round, expiry, count);
    }

    internal static bool ValidId(string? value) => !string.IsNullOrWhiteSpace(value)
        && value.Length <= 200 && !value.Any(char.IsControl);

    private static string? Read(IReadOnlyDictionary<string, string> data, string key)
        => data.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;
}

public sealed record DriverRecommendationPushTarget(DriverRecommendationPushHint Hint, string OwnerId,
    DateTimeOffset ReceivedAtUtc, bool OpenRequested);

public interface IDriverRecommendationPushTargetStore
{
    Task<DriverRecommendationPushTarget?> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(DriverRecommendationPushTarget target, CancellationToken cancellationToken = default);
    Task ClearAsync(CancellationToken cancellationToken = default);
}
