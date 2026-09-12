using Ssalddel.Application.WorldProjection;
using Ssalddel.Services.WorldProjection.AdministrativeDongDiorama;
using Ssalddel.WorkflowRules.Contracts;
using 살뜰.Services.Versioning;

namespace Ssalddel.Tests.Application.WorldProjection;

public sealed class 행정동디오라마조회UseCaseTests
{
    private const string AreaId = "region:kr:hjd:1126057500";

    [Fact]
    public async Task 후원표시는_광고고지와활성기간이있을때만합친다()
    {
        var asOf = new DateTime(2026, 9, 12, 0, 0, 0, DateTimeKind.Utc);
        var useCase = new 행정동디오라마조회UseCase(
            new FakeStore(),
            new FakeOverlaySource(
            [
                Sponsor("overlay:accepted", "후원"),
                Sponsor("overlay:no-disclosure", string.Empty),
                Sponsor("overlay:expired", "광고", endsAt: asOf.AddSeconds(-1))
            ]),
            new EnabledFeatureFlags());

        var result = await useCase.DisplayOverlaysAsync(AreaId, asOf, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(2, result.Items.Length);
        Assert.Contains(result.Items, item => item.OverlayStableId == "overlay:public");
        Assert.Contains(result.Items, item => item.OverlayStableId == "overlay:accepted");
        Assert.DoesNotContain(result.Items, item => item.OverlayStableId == "overlay:no-disclosure");
        Assert.DoesNotContain(result.Items, item => item.OverlayStableId == "overlay:expired");
    }

    [Fact]
    public async Task Manifest에없는Tile은_저장소를조회하지않고없음으로처리한다()
    {
        var store = new FakeStore();
        var useCase = new 행정동디오라마조회UseCase(store, new FakeOverlaySource([]), new EnabledFeatureFlags());

        var result = await useCase.TileAsync(AreaId, "tile:unknown", CancellationToken.None);

        Assert.Null(result);
        Assert.False(store.TileRead);
    }

    [Fact]
    public async Task 후원기능이꺼져있으면_공개사업장만반환한다()
    {
        var useCase = new 행정동디오라마조회UseCase(
            new FakeStore(),
            new FakeOverlaySource([Sponsor("overlay:sponsor", "후원")]),
            new DisabledFeatureFlags());

        var result = await useCase.DisplayOverlaysAsync(AreaId, DateTime.UtcNow, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Single(result.Items);
        Assert.Equal(AdministrativeDongDisplayOverlayKinds.PublicBusiness, result.Items[0].OverlayKindCode);
    }

    private static AdministrativeDongDisplayOverlay Sponsor(string id, string badge, DateTime? endsAt = null)
        => new()
        {
            OverlayStableId = id,
            OverlayKindCode = AdministrativeDongDisplayOverlayKinds.Sponsorship,
            DisplayLabel = "후원 사업장",
            SemanticPlaceStableId = "place:sponsor",
            BadgeText = badge,
            AdvertisementDisclosureRequired = true,
            EndsAtUtc = endsAt
        };

    private sealed class EnabledFeatureFlags : IVersionFeatureFlagService
    {
        public bool IsEnabled(string featureKey) => true;
        public IReadOnlyDictionary<string, bool> GetAll() => new Dictionary<string, bool>();
    }

    private sealed class DisabledFeatureFlags : IVersionFeatureFlagService
    {
        public bool IsEnabled(string featureKey) => false;
        public IReadOnlyDictionary<string, bool> GetAll() => new Dictionary<string, bool>();
    }

    private sealed class FakeOverlaySource(IReadOnlyList<AdministrativeDongDisplayOverlay> items)
        : I행정동디오라마DisplayOverlaySource
    {
        public Task<IReadOnlyList<AdministrativeDongDisplayOverlay>> FindActiveAsync(
            string administrativeAreaStableId,
            DateTime asOfUtc,
            CancellationToken cancellationToken) => Task.FromResult(items);
    }

    private sealed class FakeStore : I행정동디오라마ProjectionStore
    {
        public bool TileRead { get; private set; }
        public Task PublishAsync(행정동디오라마ProjectionBuildResult projection, CancellationToken cancellationToken)
            => Task.CompletedTask;
        public Task<AdministrativeDongDioramaManifest?> FindManifestAsync(string area, CancellationToken cancellationToken)
            => Task.FromResult<AdministrativeDongDioramaManifest?>(new AdministrativeDongDioramaManifest
            {
                AdministrativeAreaStableId = AreaId,
                Tiles = [new AdministrativeDongDioramaTileSummary { TileStableId = "tile:known" }]
            });
        public Task<AdministrativeDongDioramaTile?> FindTileAsync(string area, string tile, CancellationToken cancellationToken)
        {
            TileRead = true;
            return Task.FromResult<AdministrativeDongDioramaTile?>(new AdministrativeDongDioramaTile { TileStableId = tile });
        }
        public Task<AdministrativeDongDisplayOverlayResponse?> FindDisplayOverlaysAsync(string area, CancellationToken cancellationToken)
            => Task.FromResult<AdministrativeDongDisplayOverlayResponse?>(new AdministrativeDongDisplayOverlayResponse
            {
                AdministrativeAreaStableId = AreaId,
                Items =
                [
                    new AdministrativeDongDisplayOverlay
                    {
                        OverlayStableId = "overlay:public",
                        OverlayKindCode = AdministrativeDongDisplayOverlayKinds.PublicBusiness,
                        DisplayLabel = "공개 사업장",
                        SemanticPlaceStableId = "place:public"
                    }
                ]
            });
    }
}
