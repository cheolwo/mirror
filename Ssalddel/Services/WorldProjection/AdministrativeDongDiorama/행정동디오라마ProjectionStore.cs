using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using Ssalddel.WorkflowRules.Contracts;
using 살뜰.Services.Options;

namespace Ssalddel.Services.WorldProjection.AdministrativeDongDiorama;

public interface I행정동디오라마ProjectionStore
{
    Task PublishAsync(행정동디오라마ProjectionBuildResult projection, CancellationToken cancellationToken);
    Task<AdministrativeDongDioramaManifest?> FindManifestAsync(string administrativeAreaStableId, CancellationToken cancellationToken);
    Task<AdministrativeDongDioramaTile?> FindTileAsync(string administrativeAreaStableId, string tileStableId, CancellationToken cancellationToken);
    Task<AdministrativeDongDisplayOverlayResponse?> FindDisplayOverlaysAsync(string administrativeAreaStableId, CancellationToken cancellationToken);
}

public interface I행정동디오라마DisplayOverlaySource
{
    Task<IReadOnlyList<AdministrativeDongDisplayOverlay>> FindActiveAsync(
        string administrativeAreaStableId,
        DateTime asOfUtc,
        CancellationToken cancellationToken);
}

public sealed class Empty행정동디오라마DisplayOverlaySource : I행정동디오라마DisplayOverlaySource
{
    public Task<IReadOnlyList<AdministrativeDongDisplayOverlay>> FindActiveAsync(
        string administrativeAreaStableId,
        DateTime asOfUtc,
        CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<AdministrativeDongDisplayOverlay>>([]);
}

internal sealed class 행정동디오라마CurrentReleaseDocument
{
    [BsonId]
    public string AdministrativeAreaStableId { get; set; } = string.Empty;
    public string ProjectionHashSha256 { get; set; } = string.Empty;
    public DateTime PublishedAtUtc { get; set; }
}

internal sealed class 행정동디오라마ManifestDocument
{
    [BsonId]
    public string Id { get; set; } = string.Empty;
    public string AdministrativeAreaStableId { get; set; } = string.Empty;
    public string ProjectionHashSha256 { get; set; } = string.Empty;
    public AdministrativeDongDioramaManifest Manifest { get; set; } = new();
}

internal sealed class 행정동디오라마TileDocument
{
    [BsonId]
    public string Id { get; set; } = string.Empty;
    public string AdministrativeAreaStableId { get; set; } = string.Empty;
    public string ProjectionHashSha256 { get; set; } = string.Empty;
    public string TileStableId { get; set; } = string.Empty;
    public AdministrativeDongDioramaTile Tile { get; set; } = new();
}

internal sealed class 행정동디오라마OverlayDocument
{
    [BsonId]
    public string Id { get; set; } = string.Empty;
    public string AdministrativeAreaStableId { get; set; } = string.Empty;
    public string ProjectionHashSha256 { get; set; } = string.Empty;
    public AdministrativeDongDisplayOverlayResponse Overlays { get; set; } = new();
}

/// <summary>
/// 투영 hash별 사본은 삽입만 하고 현재 판본 포인터만 교체합니다. Unity 읽기 자료가
/// 갱신 도중 서로 다른 판본의 manifest와 tile을 섞지 않도록 포인터를 마지막에 바꿉니다.
/// </summary>
public sealed class Mongo행정동디오라마ProjectionStore : I행정동디오라마ProjectionStore
{
    private const string CurrentCollection = "administrative_dong_diorama_current";
    private const string ManifestCollection = "administrative_dong_diorama_manifests";
    private const string TileCollection = "administrative_dong_diorama_tiles";
    private const string OverlayCollection = "administrative_dong_diorama_overlays";

    private readonly IMongoCollection<행정동디오라마CurrentReleaseDocument> current;
    private readonly IMongoCollection<행정동디오라마ManifestDocument> manifests;
    private readonly IMongoCollection<행정동디오라마TileDocument> tiles;
    private readonly IMongoCollection<행정동디오라마OverlayDocument> overlays;

    public Mongo행정동디오라마ProjectionStore(IMongoClient client, IOptions<MongoDbOptions> options)
    {
        var databaseName = string.IsNullOrWhiteSpace(options.Value.Database)
            ? throw new InvalidOperationException("AdministrativeDongDioramaDatabaseRequired")
            : options.Value.Database;
        var database = client.GetDatabase(databaseName);
        current = database.GetCollection<행정동디오라마CurrentReleaseDocument>(CurrentCollection);
        manifests = database.GetCollection<행정동디오라마ManifestDocument>(ManifestCollection);
        tiles = database.GetCollection<행정동디오라마TileDocument>(TileCollection);
        overlays = database.GetCollection<행정동디오라마OverlayDocument>(OverlayCollection);
    }

    public async Task PublishAsync(행정동디오라마ProjectionBuildResult projection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(projection);
        ValidateProjection(projection);
        var areaId = projection.Manifest.AdministrativeAreaStableId;
        var hash = projection.Manifest.ProjectionHashSha256;

        foreach (var tile in projection.Tiles)
        {
            var document = new 행정동디오라마TileDocument
            {
                Id = VersionedId(areaId, hash, tile.TileStableId),
                AdministrativeAreaStableId = areaId,
                ProjectionHashSha256 = hash,
                TileStableId = tile.TileStableId,
                Tile = tile
            };
            await InsertOrVerifyAsync(tiles, document, document.Id, hash, cancellationToken);
        }

        var manifestDocument = new 행정동디오라마ManifestDocument
        {
            Id = VersionedId(areaId, hash),
            AdministrativeAreaStableId = areaId,
            ProjectionHashSha256 = hash,
            Manifest = projection.Manifest
        };
        await InsertOrVerifyAsync(manifests, manifestDocument, manifestDocument.Id, hash, cancellationToken);

        var overlayDocument = new 행정동디오라마OverlayDocument
        {
            Id = VersionedId(areaId, hash),
            AdministrativeAreaStableId = areaId,
            ProjectionHashSha256 = hash,
            Overlays = projection.DisplayOverlays
        };
        await InsertOrVerifyAsync(overlays, overlayDocument, overlayDocument.Id, hash, cancellationToken);

        await current.ReplaceOneAsync(
            x => x.AdministrativeAreaStableId == areaId,
            new 행정동디오라마CurrentReleaseDocument
            {
                AdministrativeAreaStableId = areaId,
                ProjectionHashSha256 = hash,
                PublishedAtUtc = DateTime.UtcNow
            },
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);
    }

    public async Task<AdministrativeDongDioramaManifest?> FindManifestAsync(
        string administrativeAreaStableId,
        CancellationToken cancellationToken)
    {
        var release = await CurrentAsync(administrativeAreaStableId, cancellationToken);
        if (release is null) return null;
        var id = VersionedId(release.AdministrativeAreaStableId, release.ProjectionHashSha256);
        return (await manifests.Find(x => x.Id == id).FirstOrDefaultAsync(cancellationToken))?.Manifest;
    }

    public async Task<AdministrativeDongDioramaTile?> FindTileAsync(
        string administrativeAreaStableId,
        string tileStableId,
        CancellationToken cancellationToken)
    {
        var release = await CurrentAsync(administrativeAreaStableId, cancellationToken);
        if (release is null) return null;
        var id = VersionedId(release.AdministrativeAreaStableId, release.ProjectionHashSha256, tileStableId);
        return (await tiles.Find(x => x.Id == id).FirstOrDefaultAsync(cancellationToken))?.Tile;
    }

    public async Task<AdministrativeDongDisplayOverlayResponse?> FindDisplayOverlaysAsync(
        string administrativeAreaStableId,
        CancellationToken cancellationToken)
    {
        var release = await CurrentAsync(administrativeAreaStableId, cancellationToken);
        if (release is null) return null;
        var id = VersionedId(release.AdministrativeAreaStableId, release.ProjectionHashSha256);
        return (await overlays.Find(x => x.Id == id).FirstOrDefaultAsync(cancellationToken))?.Overlays;
    }

    private async Task<행정동디오라마CurrentReleaseDocument?> CurrentAsync(string stableId, CancellationToken cancellationToken)
    {
        ValidateAreaStableId(stableId);
        return await current.Find(x => x.AdministrativeAreaStableId == stableId.Trim())
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static async Task InsertOrVerifyAsync<T>(
        IMongoCollection<T> collection,
        T document,
        string id,
        string expectedHash,
        CancellationToken cancellationToken)
    {
        try
        {
            await collection.InsertOneAsync(document, cancellationToken: cancellationToken);
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            var existing = await collection.Find(Builders<T>.Filter.Eq("_id", id))
                .FirstOrDefaultAsync(cancellationToken);
            var actualHash = existing switch
            {
                행정동디오라마ManifestDocument value => value.ProjectionHashSha256,
                행정동디오라마TileDocument value => value.ProjectionHashSha256,
                행정동디오라마OverlayDocument value => value.ProjectionHashSha256,
                _ => string.Empty
            };
            if (!string.Equals(actualHash, expectedHash, StringComparison.Ordinal))
                throw new InvalidDataException("AdministrativeDongDioramaImmutableVersionConflict", exception);
        }
    }

    private static void ValidateProjection(행정동디오라마ProjectionBuildResult projection)
    {
        var manifest = projection.Manifest;
        ValidateAreaStableId(manifest.AdministrativeAreaStableId);
        if (string.IsNullOrWhiteSpace(manifest.ProjectionHashSha256))
            throw new InvalidDataException("AdministrativeDongDioramaProjectionHashRequired");
        var summaries = manifest.Tiles.ToDictionary(x => x.TileStableId, StringComparer.Ordinal);
        if (summaries.Count != manifest.Tiles.Length || projection.Tiles.Length != manifest.Tiles.Length)
            throw new InvalidDataException("AdministrativeDongDioramaTileSetMismatch");
        foreach (var tile in projection.Tiles)
        {
            if (!string.Equals(tile.AdministrativeAreaStableId, manifest.AdministrativeAreaStableId, StringComparison.Ordinal)
                || !string.Equals(tile.ProjectionHashSha256, manifest.ProjectionHashSha256, StringComparison.Ordinal)
                || !summaries.TryGetValue(tile.TileStableId, out var summary)
                || !string.Equals(summary.TileHashSha256, tile.TileHashSha256, StringComparison.Ordinal))
                throw new InvalidDataException("AdministrativeDongDioramaTileVersionMismatch");
        }
        if (!string.Equals(projection.DisplayOverlays.AdministrativeAreaStableId, manifest.AdministrativeAreaStableId, StringComparison.Ordinal))
            throw new InvalidDataException("AdministrativeDongDioramaOverlayAreaMismatch");
    }

    private static void ValidateAreaStableId(string stableId)
    {
        if (!AdministrativeDongDioramaPolicy.IsAdministrativeAreaStableId(stableId))
            throw new ArgumentException("AdministrativeDongStableIdInvalid", nameof(stableId));
    }

    private static string VersionedId(params string[] parts)
        => string.Join("|", parts.Select(part => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(part)))));
}
