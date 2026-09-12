using System.Security.Cryptography;

namespace Ssalddel.Services.WorldProjection.AdministrativeDongDiorama;

public sealed class 행정동디오라마PublicationRequest
{
    public byte[] FrozenAdministrativeBoundaryGeoJson { get; set; } = [];
    public byte[] FrozenAdministrativeBoundaryShapefileZip { get; set; } = [];
    public string ExpectedBoundaryContentHashSha256 { get; set; } = string.Empty;
    public 행정동디오라마ProjectionBuildInput ProjectionInput { get; set; } = new();
}

/// <summary>
/// 공식 행정동 경계의 동결 사본 hash를 검증한 뒤에만 투영을 게시합니다.
/// API 호출이나 임의 경계 생성은 이 서비스의 책임이 아닙니다.
/// </summary>
public sealed class 행정동디오라마PublicationService(I행정동디오라마ProjectionStore store)
{
    public async Task<행정동디오라마ProjectionBuildResult> PublishAsync(
        행정동디오라마PublicationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.ProjectionInput);
        var hasGeoJson = request.FrozenAdministrativeBoundaryGeoJson is { Length: > 0 };
        var hasShapefile = request.FrozenAdministrativeBoundaryShapefileZip is { Length: > 0 };
        if (hasGeoJson == hasShapefile)
            throw new InvalidDataException("AdministrativeBoundarySnapshotRequired");
        var frozenBoundary = hasShapefile
            ? request.FrozenAdministrativeBoundaryShapefileZip
            : request.FrozenAdministrativeBoundaryGeoJson;
        var actualHash = Convert.ToHexString(SHA256.HashData(frozenBoundary))
            .ToLowerInvariant();
        if (!string.Equals(actualHash, request.ExpectedBoundaryContentHashSha256?.Trim(), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("AdministrativeBoundaryContentHashMismatch");
        if (!(request.ProjectionInput.Sources ?? []).Any(source =>
                string.Equals(source.ContentHashSha256, actualHash, StringComparison.OrdinalIgnoreCase)
                && string.Equals(source.SourceRevision, request.ProjectionInput.SourceVintage, StringComparison.Ordinal)))
            throw new InvalidDataException("AdministrativeBoundarySourceAttributionMissing");

        var boundary = hasShapefile
            ? 행정동경계ShapefileZipReader.Read(
                frozenBoundary,
                [request.ProjectionInput.AdministrativeAreaStableId],
                request.ProjectionInput.SourceVintage,
                request.ProjectionInput.CoordinateFrame).Single()
            : 행정동경계GeoJsonReader.Read(
                frozenBoundary,
                request.ProjectionInput.AdministrativeAreaStableId,
                request.ProjectionInput.SourceVintage,
                request.ProjectionInput.CoordinateFrame);
        var source = request.ProjectionInput;
        var input = new 행정동디오라마ProjectionBuildInput
        {
            AdministrativeAreaStableId = source.AdministrativeAreaStableId,
            DisplayName = boundary.DisplayName,
            SourceVintage = source.SourceVintage,
            GeneratedAtUtc = source.GeneratedAtUtc,
            CoordinateFrame = source.CoordinateFrame,
            Boundary = boundary.Boundary,
            LegalAreaStableIds = source.LegalAreaStableIds,
            OperationalAreaStableIds = source.OperationalAreaStableIds,
            SemanticPlaceStableIds = source.SemanticPlaceStableIds,
            Sources = source.Sources ?? [],
            Buildings = source.Buildings,
            Roads = source.Roads,
            PublicBusinesses = source.PublicBusinesses,
            UnresolvedBuildingCount = source.UnresolvedBuildingCount
        };
        var projection = 행정동디오라마ProjectionBuilder.Build(input);
        await store.PublishAsync(projection, cancellationToken);
        return projection;
    }
}
