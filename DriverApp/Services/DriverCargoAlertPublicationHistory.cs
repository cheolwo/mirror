using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DriverApp.Services;

public interface IDriverCargoAlertPublicationStorage
{
    string? Read();
    void Write(string value);
}

public enum DriverCargoAlertReservationResult { Reserved, Duplicate, Unavailable }

/// <summary>
/// 같은 설치의 본인·의뢰·추천 라운드를 OS 게시 전에 예약합니다.
/// 예약은 전달 성공 기록이 아니며, OS 게시 실패 때에도 재생을 반복하지 않도록 보존합니다.
/// </summary>
public sealed class DriverCargoAlertPublicationHistory(IDriverCargoAlertPublicationStorage storage)
{
    // All publisher instances in this process share the existing Preferences value.
    private static readonly object StorageGate = new();
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private sealed record Snapshot(int Version, Dictionary<string, DriverCargoPublishedAlert[]> Owners);
    private sealed record LegacySnapshot(string OwnerId, DriverCargoPublishedAlert[] Alerts);

    public DriverCargoAlertReservationResult TryReserve(string ownerId, string requestId, int round,
        DateTimeOffset expiresAtUtc, DateTimeOffset now)
    {
        if (!ValidId(ownerId) || !ValidId(requestId) || round < 1 || expiresAtUtc <= now)
            return DriverCargoAlertReservationResult.Unavailable;
        lock (StorageGate)
        try
        {
            var owners = ReadOwners(storage.Read());
            if (owners is null) return DriverCargoAlertReservationResult.Unavailable;
            var ownerKey = OwnerKey(ownerId);
            if (owners.TryGetValue(ownerKey, out var existing)
                && DriverCargoRecommendationPublicationGate.IsDuplicate(existing, requestId, round, now))
                return DriverCargoAlertReservationResult.Duplicate;

            // Keep every live reservation: evicting an unexpired one could replay that recommendation.
            owners = owners.Select(x => new { x.Key, Alerts = x.Value.Where(a => a.ExpiresAtUtc > now).ToArray() })
                .Where(x => x.Alerts.Length > 0).ToDictionary(x => x.Key, x => x.Alerts, StringComparer.Ordinal);
            var prior = owners.GetValueOrDefault(ownerKey) ?? [];
            owners[ownerKey] = [.. prior, new(requestId, round, expiresAtUtc)];
            var serialized = JsonSerializer.Serialize(new Snapshot(2, owners), JsonOptions);
            storage.Write(serialized);
            // A failed/unconfirmed save must not make a sound before the deduplication record exists.
            return string.Equals(storage.Read(), serialized, StringComparison.Ordinal)
                ? DriverCargoAlertReservationResult.Reserved : DriverCargoAlertReservationResult.Unavailable;
        }
        catch (Exception)
        {
            // Preserve any existing or partially committed reservation; never reset it to permit replay.
            return DriverCargoAlertReservationResult.Unavailable;
        }
    }

    private static Dictionary<string, DriverCargoPublishedAlert[]>? ReadOwners(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return new(StringComparer.Ordinal);
        using var document = JsonDocument.Parse(value);
        if (document.RootElement.ValueKind != JsonValueKind.Object) return null;
        if (document.RootElement.TryGetProperty("version", out var version))
        {
            if (!version.TryGetInt32(out var number) || number != 2) return null;
            var snapshot = JsonSerializer.Deserialize<Snapshot>(value, JsonOptions);
            if (snapshot?.Owners is null || snapshot.Owners.Any(x => !ValidOwnerKey(x.Key) || !ValidAlerts(x.Value)))
                return null;
            return new(snapshot.Owners, StringComparer.Ordinal);
        }
        // Upgrade the previous single-owner value in place, including that owner's live history.
        var legacy = JsonSerializer.Deserialize<LegacySnapshot>(value, JsonOptions);
        return legacy is not null && ValidId(legacy.OwnerId) && ValidAlerts(legacy.Alerts)
            ? new(StringComparer.Ordinal) { [OwnerKey(legacy.OwnerId)] = legacy.Alerts } : null;
    }

    private static bool ValidAlerts(DriverCargoPublishedAlert[]? alerts)
        => alerts is not null && alerts.All(x => x is not null && ValidId(x.RequestId) && x.Round > 0);
    private static bool ValidId(string? value)
        => !string.IsNullOrWhiteSpace(value) && !value.Any(char.IsControl);
    private static bool ValidOwnerKey(string value)
        => value.Length == 64 && value.All(x => x is >= '0' and <= '9' or >= 'A' and <= 'F');
    private static string OwnerKey(string ownerId) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ownerId)));
}
