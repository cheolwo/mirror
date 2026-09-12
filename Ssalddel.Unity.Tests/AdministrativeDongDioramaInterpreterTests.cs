using Ssalddel.Unity.Data.WorldProjection;
using Ssalddel.WorkflowRules.Contracts;

namespace Ssalddel.Unity.Tests;

[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
    "행정동 디오라마 판본과 비권위 운영 상태 결합을 순수 데이터 계층에서 검증한다.",
    Boundary = "Unity Scene, Play Mode, Game View 또는 실제 Mongo 게시 증거가 아니다.")]
public sealed class AdministrativeDongDioramaInterpreterTests
{
    private const string AreaId = "region:kr:hjd:1126057500";

    [Fact]
    public void 같은판본의타일만받고_행정동에연결되지않은운영객체는복제하지않는다()
    {
        var interpreter = new AdministrativeDongDioramaInterpreter();
        var manifest = Manifest();
        var tile = Tile();

        var applied = interpreter.Apply(manifest, [tile], Overlays());
        var binding = interpreter.BindOperationalItems(
        [
            Operational("snapshot:mapped", "place:restaurant:1"),
            Operational("snapshot:unresolved", "place:unknown")
        ]);

        Assert.True(applied.Accepted);
        Assert.Single(binding.MappedItems);
        Assert.Equal("snapshot:mapped", binding.MappedItems[0].SnapshotStableId);
        Assert.Equal(1, binding.UnresolvedItemCount);
    }

    [Fact]
    public void 광고고지가없는후원Overlay는_전체판본을거절하고_이전상태를유지한다()
    {
        var interpreter = new AdministrativeDongDioramaInterpreter();
        Assert.True(interpreter.Apply(Manifest(), [Tile()], Overlays()).Accepted);
        var invalid = Overlays();
        invalid.Items =
        [
            new AdministrativeDongDisplayOverlay
            {
                OverlayStableId = "sponsor:bad",
                OverlayKindCode = AdministrativeDongDisplayOverlayKinds.Sponsorship,
                DisplayLabel = "사업장",
                SemanticPlaceStableId = "place:restaurant:1",
                BadgeText = "추천",
                AdvertisementDisclosureRequired = true
            }
        ];

        var result = interpreter.Apply(Manifest(), [Tile()], invalid);

        Assert.False(result.Accepted);
        Assert.Equal("AdministrativeDongDioramaSponsorshipDisclosureRequired", result.ErrorCode);
        Assert.Single(result.Tiles);
    }

    [Fact]
    public void Clear는_메모리상태와운영결합을비운다()
    {
        var interpreter = new AdministrativeDongDioramaInterpreter();
        interpreter.Apply(Manifest(), [Tile()], Overlays());

        interpreter.Clear();
        var binding = interpreter.BindOperationalItems([Operational("snapshot:one", "place:restaurant:1")]);

        Assert.Empty(binding.MappedItems);
        Assert.Equal(1, binding.UnresolvedItemCount);
    }

    [Fact]
    public async Task Client는_행정동자료를_인증GET으로만읽고_해석기에전달한다()
    {
        var transport = new RecordingTransport();
        var client = new Ssalddel.Unity.WorldProjection.AdministrativeDongDioramaClient(
            transport,
            new StaticDecoder(),
            new AdministrativeDongDioramaInterpreter());

        var result = await client.RefreshAsync(AreaId, CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal(3, transport.Routes.Count);
        Assert.All(transport.RequiresAuthentication, Assert.True);
        Assert.Contains(transport.Routes, route => route.EndsWith("/diorama-manifest", StringComparison.Ordinal));
        Assert.Contains(transport.Routes, route => route.Contains("/diorama-tiles/", StringComparison.Ordinal));
        Assert.Contains(transport.Routes, route => route.EndsWith("/display-overlays", StringComparison.Ordinal));
    }

    private static AdministrativeDongDioramaManifest Manifest()
        => new()
        {
            AdministrativeAreaStableId = AreaId,
            ProjectionHashSha256 = "projection-hash",
            DataPolicyCode = AdministrativeDongDioramaPolicy.ObservationPresentationOnly,
            ObservationPresentationOnly = true,
            SemanticPlaceStableIds = ["place:restaurant:1"],
            Tiles =
            [
                new AdministrativeDongDioramaTileSummary
                {
                    TileStableId = "tile:one",
                    TileHashSha256 = "tile-hash"
                }
            ]
        };

    private static AdministrativeDongDioramaTile Tile()
        => new()
        {
            AdministrativeAreaStableId = AreaId,
            ProjectionHashSha256 = "projection-hash",
            TileStableId = "tile:one",
            TileHashSha256 = "tile-hash"
        };

    private static AdministrativeDongDisplayOverlayResponse Overlays()
        => new() { AdministrativeAreaStableId = AreaId, OverlayRevision = "overlay-hash" };

    private static OperationalWorldSceneItem Operational(string id, string place)
        => new() { SnapshotStableId = id, SemanticPlaceStableId = place };

    private sealed class RecordingTransport : Ssalddel.Unity.WorldProjection.IOperationalWorldProjectionTransport
    {
        public List<string> Routes { get; } = [];
        public List<bool> RequiresAuthentication { get; } = [];

        public Task<string?> GetAsync(
            string relativeRoute,
            bool allowNotFound,
            bool requiresAuthentication,
            CancellationToken cancellationToken = default)
        {
            Routes.Add(relativeRoute);
            RequiresAuthentication.Add(requiresAuthentication);
            return Task.FromResult<string?>("{}");
        }
    }

    private sealed class StaticDecoder : Ssalddel.Unity.WorldProjection.IAdministrativeDongDioramaDecoder
    {
        public AdministrativeDongDioramaManifest DecodeManifest(string json) => Manifest();
        public AdministrativeDongDioramaTile DecodeTile(string json) => Tile();
        public AdministrativeDongDisplayOverlayResponse DecodeDisplayOverlays(string json) => Overlays();
    }
}
