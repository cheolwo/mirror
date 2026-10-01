using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Hongdal.Contracts.Common.Privacy;
using Hongdal.Services.Security;
using 홍달.도메인.공통;
using 홍달.Services.Storage.Local;

namespace Hongdal.Infrastructure.Storage.Memory;

internal sealed record ExpiringValue<T>(T Value, DateTimeOffset ExpiresAtUtc);

internal sealed class InMemoryExpiringStringStore(TimeSpan ttl)
{
    private readonly ConcurrentDictionary<string, ExpiringValue<string>> _values = new(StringComparer.Ordinal);

    public void Set(string key, string value)
        => _values[key] = new ExpiringValue<string>(value, DateTimeOffset.UtcNow.Add(ttl));

    public string? Get(string key)
    {
        if (!_values.TryGetValue(key, out var entry))
        {
            return null;
        }

        if (entry.ExpiresAtUtc > DateTimeOffset.UtcNow)
        {
            return entry.Value;
        }

        _values.TryRemove(key, out _);
        return null;
    }

    public void Remove(string key) => _values.TryRemove(key, out _);
}

public sealed class InMemoryDriverLocationStore : IDriverLocationStore
{
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromDays(7);
    private readonly ConcurrentDictionary<string, ExpiringValue<DriverLocationSnapshot>> _locations = new(StringComparer.Ordinal);

    public void Upsert(DriverLocationSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var driverId = snapshot.DriverId.Trim();
        if (string.IsNullOrWhiteSpace(driverId))
        {
            return;
        }

        _locations[driverId] = new ExpiringValue<DriverLocationSnapshot>(
            snapshot with { DriverId = driverId },
            DateTimeOffset.UtcNow.Add(DefaultTtl));
    }

    public bool TryGetLatest(string driverId, out DriverLocationSnapshot snapshot)
    {
        snapshot = default!;
        driverId = driverId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(driverId) || !_locations.TryGetValue(driverId, out var entry))
        {
            return false;
        }

        if (entry.ExpiresAtUtc <= DateTimeOffset.UtcNow)
        {
            _locations.TryRemove(driverId, out _);
            return false;
        }

        snapshot = entry.Value;
        return true;
    }
}

public sealed class InMemoryDriverWorkQueueStore : IDriverWorkQueueStore
{
    private readonly ConcurrentDictionary<string, DriverWorkQueueEntry> _entries = new(StringComparer.Ordinal);

    public Task UpsertAsync(DriverWorkQueueEntry entry, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(entry);
        var driverId = entry.DriverId.Trim();
        if (!string.IsNullOrWhiteSpace(driverId))
        {
            _entries[driverId] = entry with { DriverId = driverId };
        }

        return Task.CompletedTask;
    }

    public Task RemoveAsync(string driverId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        driverId = driverId?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(driverId))
        {
            _entries.TryRemove(driverId, out _);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<DriverWorkQueueEntry>> SnapshotAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<DriverWorkQueueEntry> snapshot = _entries.Values
            .OrderBy(entry => entry.StartedAtUtc)
            .ThenBy(entry => entry.DriverId, StringComparer.Ordinal)
            .ToArray();
        return Task.FromResult(snapshot);
    }
}

public sealed class InMemory국내화물운송기사상태Store : I국내화물운송기사상태Store
{
    private const double EarthRadiusKm = 6371.0088;
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromDays(7);
    private readonly ConcurrentDictionary<string, ExpiringValue<국내화물운송기사상태Snapshot>> _states = new(StringComparer.Ordinal);

    public Task UpsertAsync(국내화물운송기사상태Snapshot snapshot, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(snapshot);
        var driverId = snapshot.DriverId.Trim();
        if (!string.IsNullOrWhiteSpace(driverId))
        {
            _states[driverId] = new ExpiringValue<국내화물운송기사상태Snapshot>(
                snapshot with { DriverId = driverId },
                DateTimeOffset.UtcNow.Add(DefaultTtl));
        }

        return Task.CompletedTask;
    }

    public Task<국내화물운송기사상태Snapshot?> GetAsync(
        string driverId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        driverId = driverId?.Trim() ?? string.Empty;
        return Task.FromResult(TryGetActive(driverId, out var snapshot) ? snapshot : null);
    }

    public Task<IReadOnlyList<국내화물운송기사상태Snapshot>> 위치반경조회Async(
        decimal latitude,
        decimal longitude,
        decimal radiusKm,
        int take,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (radiusKm <= 0m || take <= 0)
        {
            return Task.FromResult<IReadOnlyList<국내화물운송기사상태Snapshot>>([]);
        }

        IReadOnlyList<국내화물운송기사상태Snapshot> states = GetActiveStates()
            .Where(snapshot => snapshot.Latitude.HasValue && snapshot.Longitude.HasValue)
            .Select(snapshot => new
            {
                Snapshot = snapshot,
                DistanceKm = CalculateDistanceKm(
                    latitude,
                    longitude,
                    snapshot.Latitude!.Value,
                    snapshot.Longitude!.Value)
            })
            .Where(item => item.DistanceKm <= (double)radiusKm)
            .OrderBy(item => item.DistanceKm)
            .ThenBy(item => item.Snapshot.DriverId, StringComparer.Ordinal)
            .Take(take)
            .Select(item => item.Snapshot)
            .ToArray();
        return Task.FromResult(states);
    }

    public Task<IReadOnlyList<국내화물운송기사상태Snapshot>> 활성기사조회Async(
        int take,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (take <= 0)
        {
            return Task.FromResult<IReadOnlyList<국내화물운송기사상태Snapshot>>([]);
        }

        IReadOnlyList<국내화물운송기사상태Snapshot> states = GetActiveStates()
            .OrderBy(snapshot => snapshot.Aging기준시각Utc)
            .ThenBy(snapshot => snapshot.DriverId, StringComparer.Ordinal)
            .Take(take)
            .ToArray();
        return Task.FromResult(states);
    }

    public Task RemoveAsync(string driverId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        driverId = driverId?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(driverId))
        {
            _states.TryRemove(driverId, out _);
        }

        return Task.CompletedTask;
    }

    private bool TryGetActive(string driverId, out 국내화물운송기사상태Snapshot? snapshot)
    {
        snapshot = null;
        if (string.IsNullOrWhiteSpace(driverId) || !_states.TryGetValue(driverId, out var entry))
        {
            return false;
        }

        if (entry.ExpiresAtUtc <= DateTimeOffset.UtcNow)
        {
            _states.TryRemove(driverId, out _);
            return false;
        }

        snapshot = entry.Value;
        return true;
    }

    private IEnumerable<국내화물운송기사상태Snapshot> GetActiveStates()
    {
        foreach (var pair in _states)
        {
            if (pair.Value.ExpiresAtUtc <= DateTimeOffset.UtcNow)
            {
                _states.TryRemove(pair.Key, out _);
                continue;
            }

            if (string.Equals(pair.Value.Value.운행상태, 상태값.기사운행상태.운행중, StringComparison.OrdinalIgnoreCase))
            {
                yield return pair.Value.Value;
            }
        }
    }

    private static double CalculateDistanceKm(
        decimal latitude1,
        decimal longitude1,
        decimal latitude2,
        decimal longitude2)
    {
        var lat1 = DegreesToRadians((double)latitude1);
        var lat2 = DegreesToRadians((double)latitude2);
        var deltaLat = DegreesToRadians((double)(latitude2 - latitude1));
        var deltaLon = DegreesToRadians((double)(longitude2 - longitude1));
        var a = Math.Pow(Math.Sin(deltaLat / 2), 2)
                + Math.Cos(lat1) * Math.Cos(lat2) * Math.Pow(Math.Sin(deltaLon / 2), 2);
        return EarthRadiusKm * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180d;
}

public sealed class InMemoryDriverRejectedRequestStore : IDriverRejectedRequestStore
{
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromDays(14);
    private readonly ConcurrentDictionary<(string DriverId, string RequestId), DateTimeOffset> _rejections = new();

    public Task RejectAsync(string driverId, string requestId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        driverId = driverId?.Trim() ?? string.Empty;
        requestId = requestId?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(driverId) && !string.IsNullOrWhiteSpace(requestId))
        {
            _rejections[(driverId, requestId)] = DateTimeOffset.UtcNow.Add(DefaultTtl);
        }

        return Task.CompletedTask;
    }

    public Task<bool> IsRejectedAsync(string driverId, string requestId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        driverId = driverId?.Trim() ?? string.Empty;
        requestId = requestId?.Trim() ?? string.Empty;
        var key = (driverId, requestId);
        if (_rejections.TryGetValue(key, out var expiresAtUtc) && expiresAtUtc > DateTimeOffset.UtcNow)
        {
            return Task.FromResult(true);
        }

        _rejections.TryRemove(key, out _);
        return Task.FromResult(false);
    }

    public Task<IReadOnlySet<string>> GetRejectedRequestIdsAsync(
        string driverId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        driverId = driverId?.Trim() ?? string.Empty;
        IReadOnlySet<string> result = GetActiveRejections()
            .Where(item => string.Equals(item.DriverId, driverId, StringComparison.Ordinal))
            .Select(item => item.RequestId)
            .ToHashSet(StringComparer.Ordinal);
        return Task.FromResult(result);
    }

    public Task<IReadOnlySet<string>> GetRejectedDriverIdsAsync(
        string requestId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        requestId = requestId?.Trim() ?? string.Empty;
        IReadOnlySet<string> result = GetActiveRejections()
            .Where(item => string.Equals(item.RequestId, requestId, StringComparison.Ordinal))
            .Select(item => item.DriverId)
            .ToHashSet(StringComparer.Ordinal);
        return Task.FromResult(result);
    }

    private IEnumerable<(string DriverId, string RequestId)> GetActiveRejections()
    {
        foreach (var pair in _rejections)
        {
            if (pair.Value <= DateTimeOffset.UtcNow)
            {
                _rejections.TryRemove(pair.Key, out _);
                continue;
            }

            yield return pair.Key;
        }
    }
}

public sealed class InMemoryDriverPushTokenStore : IDriverPushTokenStore
{
    private readonly InMemoryExpiringStringStore _tokens = new(TimeSpan.FromDays(30));

    public Task SetAsync(string driverId, string pushToken, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        driverId = driverId?.Trim() ?? string.Empty;
        pushToken = pushToken?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(driverId) && !string.IsNullOrWhiteSpace(pushToken))
        {
            _tokens.Set(driverId, pushToken);
        }

        return Task.CompletedTask;
    }

    public Task<string?> GetAsync(string driverId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        driverId = driverId?.Trim() ?? string.Empty;
        return Task.FromResult(string.IsNullOrWhiteSpace(driverId) ? null : _tokens.Get(driverId));
    }

    public Task ClearAsync(string driverId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        driverId = driverId?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(driverId))
        {
            _tokens.Remove(driverId);
        }

        return Task.CompletedTask;
    }
}

public sealed class InMemory사용자PushTokenStore : I사용자PushTokenStore
{
    private readonly InMemoryExpiringStringStore _tokens = new(TimeSpan.FromDays(30));

    public Task SetAsync(string userId, string pushToken, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        userId = userId?.Trim() ?? string.Empty;
        pushToken = pushToken?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(userId) && !string.IsNullOrWhiteSpace(pushToken))
        {
            _tokens.Set(userId, pushToken);
        }

        return Task.CompletedTask;
    }

    public Task<string?> GetAsync(string userId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        userId = userId?.Trim() ?? string.Empty;
        return Task.FromResult(string.IsNullOrWhiteSpace(userId) ? null : _tokens.Get(userId));
    }

    public Task ClearAsync(string userId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        userId = userId?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(userId))
        {
            _tokens.Remove(userId);
        }

        return Task.CompletedTask;
    }
}

public sealed class InMemoryDriverRecommendationPushStateStore : IDriverRecommendationPushStateStore
{
    private readonly ConcurrentDictionary<string, string> _signatures = new(StringComparer.Ordinal);

    public Task<bool> HasChangedAsync(
        string driverId,
        IReadOnlyList<string> recommendationIds,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        driverId = driverId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(driverId))
        {
            return Task.FromResult(false);
        }

        var currentSignature = BuildSignature(recommendationIds);
        var changed = !_signatures.TryGetValue(driverId, out var previousSignature)
                      || !string.Equals(previousSignature, currentSignature, StringComparison.Ordinal);
        _signatures[driverId] = currentSignature;
        return Task.FromResult(changed);
    }

    public Task<string?> GetSignatureAsync(string driverId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        driverId = driverId?.Trim() ?? string.Empty;
        return Task.FromResult(
            !string.IsNullOrWhiteSpace(driverId) && _signatures.TryGetValue(driverId, out var signature)
                ? signature
                : null);
    }

    private static string BuildSignature(IReadOnlyList<string> recommendationIds)
    {
        var normalized = recommendationIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .OrderBy(id => id, StringComparer.Ordinal);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('|', normalized)));
        return Convert.ToHexString(hash);
    }
}

public sealed class InMemoryDriverCallScopeStore : IDriverCallScopeStore
{
    private readonly ConcurrentDictionary<string, bool> _nationwideSettings = new(StringComparer.Ordinal);

    public Task SetNationwideEnabledAsync(string driverId, bool enabled, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        driverId = driverId?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(driverId))
        {
            _nationwideSettings[driverId] = enabled;
        }

        return Task.CompletedTask;
    }

    public Task<bool> IsNationwideEnabledAsync(string driverId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        driverId = driverId?.Trim() ?? string.Empty;
        return Task.FromResult(
            !string.IsNullOrWhiteSpace(driverId)
            && _nationwideSettings.TryGetValue(driverId, out var enabled)
            && enabled);
    }
}

public sealed class InMemoryDriverNotificationSettingsStore : IDriverNotificationSettingsStore
{
    private static readonly DriverNotificationSettings DefaultSettings = new(false, false, true, true, false, false);
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromDays(365);
    private readonly ConcurrentDictionary<string, ExpiringValue<DriverNotificationSettings>> _settings = new(StringComparer.Ordinal);

    public Task<DriverNotificationSettings> GetAsync(string driverId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        driverId = driverId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(driverId) || !_settings.TryGetValue(driverId, out var entry))
        {
            return Task.FromResult(DefaultSettings);
        }

        if (entry.ExpiresAtUtc <= DateTimeOffset.UtcNow)
        {
            _settings.TryRemove(driverId, out _);
            return Task.FromResult(DefaultSettings);
        }

        return Task.FromResult(entry.Value);
    }

    public Task SetAsync(
        string driverId,
        DriverNotificationSettings settings,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        driverId = driverId?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(driverId))
        {
            _settings[driverId] = new ExpiringValue<DriverNotificationSettings>(
                settings,
                DateTimeOffset.UtcNow.Add(DefaultTtl));
        }

        return Task.CompletedTask;
    }
}

public sealed class InMemoryIsmsPTransportKeyStatusStore : IIsmsPTransportKeyStatusStore
{
    private sealed record KeyStatus(string AlgorithmCode, DateTimeOffset ExpiresAtUtc);

    private readonly ConcurrentDictionary<string, KeyStatus> _keys = new(StringComparer.Ordinal);

    public Task MarkActiveAsync(
        IsmsPClientEncryptionPublicKeyResponse publicKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(publicKey);
        var keyId = publicKey.KeyId.Trim();
        if (!string.IsNullOrWhiteSpace(keyId) && publicKey.ExpiresAtUtc > DateTimeOffset.UtcNow)
        {
            _keys[keyId] = new KeyStatus(publicKey.AlgorithmCode, publicKey.ExpiresAtUtc);
        }

        return Task.CompletedTask;
    }

    public Task<bool> IsActiveAsync(
        string keyId,
        string algorithmCode,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        keyId = keyId?.Trim() ?? string.Empty;
        algorithmCode = algorithmCode?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(keyId)
            || string.IsNullOrWhiteSpace(algorithmCode)
            || !_keys.TryGetValue(keyId, out var status))
        {
            return Task.FromResult(false);
        }

        if (status.ExpiresAtUtc <= DateTimeOffset.UtcNow)
        {
            _keys.TryRemove(keyId, out _);
            return Task.FromResult(false);
        }

        return Task.FromResult(string.Equals(status.AlgorithmCode, algorithmCode, StringComparison.Ordinal));
    }
}
