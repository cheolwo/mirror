using System.Text.Json;
using StackExchange.Redis;
using 홍달.도메인.공통;
using 홍달.Services.Storage.Local;

namespace 홍달.Infrastructure.Storage.Redis;

public sealed class Redis국내화물운송기사상태Store : I국내화물운송기사상태Store
{
    private const string KeyPrefix = "hongdal:domestic-cargo-driver-state:";
    private const string ActiveIndexKey = "hongdal:domestic-cargo-driver-state:active-index";
    private const string GeoIndexKey = "hongdal:domestic-cargo-driver-state:geo-index";
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromDays(7);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IDatabase _database;

    public Redis국내화물운송기사상태Store(IConnectionMultiplexer connectionMultiplexer)
    {
        _database = connectionMultiplexer.GetDatabase();
    }

    public async Task UpsertAsync(국내화물운송기사상태Snapshot snapshot, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(snapshot, JsonOptions);
        await _database.StringSetAsync(BuildKey(snapshot.DriverId), json, DefaultTtl).ConfigureAwait(false);

        if (string.Equals(snapshot.운행상태, 상태값.기사운행상태.운행중, StringComparison.OrdinalIgnoreCase))
        {
            await _database.SortedSetAddAsync(
                ActiveIndexKey,
                snapshot.DriverId,
                snapshot.Aging기준시각Utc.ToUniversalTime().Ticks).ConfigureAwait(false);

            if (snapshot.Latitude.HasValue && snapshot.Longitude.HasValue)
            {
                await _database.GeoAddAsync(
                    GeoIndexKey,
                    (double)snapshot.Longitude.Value,
                    (double)snapshot.Latitude.Value,
                    snapshot.DriverId).ConfigureAwait(false);
            }
        }
        else
        {
            await RemoveIndexAsync(snapshot.DriverId).ConfigureAwait(false);
        }
    }

    public async Task<국내화물운송기사상태Snapshot?> GetAsync(string driverId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(driverId))
        {
            return null;
        }

        var json = await _database.StringGetAsync(BuildKey(driverId)).ConfigureAwait(false);
        if (json.IsNullOrEmpty)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<국내화물운송기사상태Snapshot>(json.ToString(), JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<국내화물운송기사상태Snapshot>> 위치반경조회Async(
        decimal latitude,
        decimal longitude,
        decimal radiusKm,
        int take,
        CancellationToken cancellationToken = default)
    {
        if (radiusKm <= 0m || take <= 0)
        {
            return [];
        }

        var results = await _database.GeoRadiusAsync(
            GeoIndexKey,
            (double)longitude,
            (double)latitude,
            (double)radiusKm,
            GeoUnit.Kilometers,
            count: take,
            order: Order.Ascending).ConfigureAwait(false);

        if (results.Length == 0)
        {
            return [];
        }

        var items = new List<국내화물운송기사상태Snapshot>(results.Length);
        foreach (var result in results)
        {
            var driverId = result.Member.ToString();
            var snapshot = await GetAsync(driverId, cancellationToken).ConfigureAwait(false);
            if (snapshot is not null && string.Equals(snapshot.운행상태, 상태값.기사운행상태.운행중, StringComparison.OrdinalIgnoreCase))
            {
                items.Add(snapshot);
            }
            else
            {
                await RemoveIndexAsync(driverId).ConfigureAwait(false);
            }
        }

        return items;
    }

    public async Task<IReadOnlyList<국내화물운송기사상태Snapshot>> 활성기사조회Async(
        int take,
        CancellationToken cancellationToken = default)
    {
        if (take <= 0)
        {
            return [];
        }

        var driverIds = await _database.SortedSetRangeByRankAsync(
            ActiveIndexKey,
            start: 0,
            stop: take - 1,
            order: Order.Ascending).ConfigureAwait(false);
        if (driverIds.Length == 0)
        {
            return [];
        }

        var items = new List<국내화물운송기사상태Snapshot>(driverIds.Length);
        foreach (var driverIdValue in driverIds)
        {
            var driverId = driverIdValue.ToString();
            var snapshot = await GetAsync(driverId, cancellationToken).ConfigureAwait(false);
            if (snapshot is not null && string.Equals(snapshot.운행상태, 상태값.기사운행상태.운행중, StringComparison.OrdinalIgnoreCase))
            {
                items.Add(snapshot);
            }
            else
            {
                await RemoveIndexAsync(driverId).ConfigureAwait(false);
            }
        }

        return items;
    }

    public async Task RemoveAsync(string driverId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(driverId))
        {
            return;
        }

        await _database.KeyDeleteAsync(BuildKey(driverId)).ConfigureAwait(false);
        await RemoveIndexAsync(driverId).ConfigureAwait(false);
    }

    private static string BuildKey(string driverId) => $"{KeyPrefix}{driverId.Trim()}";

    private async Task RemoveIndexAsync(string driverId)
    {
        driverId = driverId.Trim();
        if (string.IsNullOrWhiteSpace(driverId))
        {
            return;
        }

        await _database.SortedSetRemoveAsync(ActiveIndexKey, driverId).ConfigureAwait(false);
        await _database.SortedSetRemoveAsync(GeoIndexKey, driverId).ConfigureAwait(false);
    }
}
