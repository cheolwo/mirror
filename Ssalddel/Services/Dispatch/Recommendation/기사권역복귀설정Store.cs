using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using Ssalddel.Contracts.Driver.Settings;
using 살뜰.Services.Options;

namespace 살뜰.Services.Dispatch.Recommendation;

public sealed record 기사권역복귀설정Snapshot(기사권역복귀설정 설정, bool 활성여부 = false, DateTime? 위치판정시각Utc = null);

public interface I기사권역복귀설정Store
{
    Task<기사권역복귀설정Snapshot> 조회Async(string driverId, CancellationToken cancellationToken = default);
    Task<기사권역복귀설정Snapshot> 저장Async(string driverId, 기사권역복귀설정 settings, long expectedRevision, CancellationToken cancellationToken = default);
    Task<bool> 활성상태갱신Async(string driverId, long revision, bool active, DateTime positionRecordedUtc, CancellationToken cancellationToken = default);
}

public sealed class 기사권역복귀설정ConflictException() : InvalidOperationException("DriverReturnAreaSettingsRevisionConflict");

public static class 기사권역복귀설정검증
{
    public static bool 유효(기사권역복귀설정? value, long expectedRevision)
        => value is not null && expectedRevision >= 0
           && value.Revision == expectedRevision && value.Mode is "off" or "automatic" or "on"
           && (value.기준위치명?.Length ?? 0) <= 80
           && value.기준위도.HasValue == value.기준경도.HasValue
           && (!value.기준위도.HasValue || value.기준위도 is >= -90 and <= 90 && value.기준경도 is >= -180 and <= 180)
           && (value.Mode == "off" || value.기준위도.HasValue)
           && value.해제반경Km > 0 && value.진입반경Km > value.해제반경Km && value.진입반경Km <= 100m;
}

/// <summary>설정만 보호된 Mongo 문서로 보관합니다. 운행 원장이나 현재 위치를 변경하지 않습니다.</summary>
public sealed class Mongo기사권역복귀설정Store : I기사권역복귀설정Store
{
    public const string CollectionName = "driver_return_area_settings_private";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly IMongoCollection<기사권역복귀설정Document> collection;
    private readonly IDataProtector protector;

    public Mongo기사권역복귀설정Store(IMongoClient mongo, IOptions<MongoDbOptions> options, IDataProtectionProvider protection)
    {
        if (string.IsNullOrWhiteSpace(options.Value.Database)) throw new InvalidOperationException("MongoDb:Database configuration is required.");
        collection = mongo.GetDatabase(options.Value.Database.Trim()).GetCollection<기사권역복귀설정Document>(CollectionName);
        protector = protection.CreateProtector("Ssalddel.DriverReturnArea.Private.v1");
    }

    public async Task<기사권역복귀설정Snapshot> 조회Async(string driverId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(driverId);
        var row = await collection.Find(x => x.Id == Hash(driverId)).FirstOrDefaultAsync(cancellationToken);
        return row is null ? new(new()) : Read(driverId, row);
    }

    public async Task<기사권역복귀설정Snapshot> 저장Async(string driverId, 기사권역복귀설정 settings, long expectedRevision, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(driverId);
        if (expectedRevision == long.MaxValue || !기사권역복귀설정검증.유효(settings, expectedRevision)) throw new ArgumentException("DriverReturnAreaSettingsInvalid", nameof(settings));
        // Copy before awaiting: the caller cannot mutate the persisted configuration or advance its revision.
        var copy = JsonSerializer.Deserialize<기사권역복귀설정>(JsonSerializer.Serialize(settings, Json), Json)!;
        copy.Revision = expectedRevision + 1;
        copy.기준위치명 = string.IsNullOrWhiteSpace(copy.기준위치명) ? null : copy.기준위치명.Trim();
        var row = Write(driverId, copy);
        if (expectedRevision == 0)
        {
            try { await collection.InsertOneAsync(row, cancellationToken: cancellationToken); }
            catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
            { throw new 기사권역복귀설정ConflictException(); }
        }
        else
        {
            var result = await collection.ReplaceOneAsync(x => x.Id == row.Id && x.Revision == expectedRevision, row, cancellationToken: cancellationToken);
            if (result.MatchedCount != 1) throw new 기사권역복귀설정ConflictException();
        }
        return Read(driverId, row);
    }

    public async Task<bool> 활성상태갱신Async(string driverId, long revision, bool active, DateTime positionRecordedUtc, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(driverId);
        if (revision <= 0 || positionRecordedUtc.Kind != DateTimeKind.Utc) return false;
        // Mongo dates use millisecond precision, including replay comparison.
        var recorded = new DateTime(positionRecordedUtc.Ticks / TimeSpan.TicksPerMillisecond * TimeSpan.TicksPerMillisecond, DateTimeKind.Utc);
        var filter = Builders<기사권역복귀설정Document>.Filter;
        var result = await collection.UpdateOneAsync(filter.Eq(x => x.Id, Hash(driverId)) & filter.Eq(x => x.Revision, revision)
                & (filter.Eq(x => x.위치판정시각Utc, null) | filter.Lt(x => x.위치판정시각Utc, recorded)),
            Builders<기사권역복귀설정Document>.Update.Set(x => x.Active, active).Set(x => x.위치판정시각Utc, recorded), cancellationToken: cancellationToken);
        if (result.MatchedCount == 1) return true;
        return await collection.Find(x => x.Id == Hash(driverId) && x.Revision == revision && x.Active == active && x.위치판정시각Utc == recorded)
            .AnyAsync(cancellationToken);
    }

    private 기사권역복귀설정Document Write(string driverId, 기사권역복귀설정 settings) => new()
    {
        Id = Hash(driverId), Revision = settings.Revision,
        ProtectedPayload = protector.Protect(JsonSerializer.Serialize(new 보호내용(driverId, settings), Json))
    };

    private 기사권역복귀설정Snapshot Read(string driverId, 기사권역복귀설정Document row)
    {
        var payload = JsonSerializer.Deserialize<보호내용>(protector.Unprotect(row.ProtectedPayload), Json)
                      ?? throw new InvalidDataException("DriverReturnAreaSettingsPayloadMissing");
        if (payload.DriverId != driverId || row.Id != Hash(driverId) || payload.설정.Revision != row.Revision
            || row.Revision <= 0 || !기사권역복귀설정검증.유효(payload.설정, row.Revision))
            throw new InvalidDataException("DriverReturnAreaSettingsPayloadBindingInvalid");
        return new(payload.설정, row.Active, row.위치판정시각Utc);
    }

    private static string Hash(string driverId) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(driverId))).ToLowerInvariant();
    private sealed record 보호내용(string DriverId, 기사권역복귀설정 설정);
    public sealed class 기사권역복귀설정Document
    {
        [BsonId] public string Id { get; set; } = string.Empty;
        public long Revision { get; set; }
        public string ProtectedPayload { get; set; } = string.Empty;
        public bool Active { get; set; }
        public DateTime? 위치판정시각Utc { get; set; }
    }
}
