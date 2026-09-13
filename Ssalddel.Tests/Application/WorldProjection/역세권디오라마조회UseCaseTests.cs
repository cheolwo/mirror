using Microsoft.Extensions.DependencyInjection;
using Ssalddel.Application.WorldProjection;
using Ssalddel.Extensions;
using Ssalddel.Services.WorldProjection.AdministrativeDongDiorama;
using Ssalddel.WorkflowRules.Contracts;

namespace Ssalddel.Tests.Application.WorldProjection;

public sealed class 역세권디오라마조회UseCaseTests
{
    [Fact]
    public void ApplicationCore는_catalog와scoped조회UseCase를해석한다()
    {
        var services = new ServiceCollection();
        services.AddSsalddelApplicationCore();
        services.AddSingleton<I지역ExperiencePackage조회UseCase>(new FakeRegionUseCase());
        services.AddSingleton<I행정동디오라마조회UseCase>(new FakeAdministrativeDongUseCase());
        using var provider = services.BuildServiceProvider();

        var catalog = provider.GetRequiredService<I역세권디오라마Catalog>();
        using var firstScope = provider.CreateScope();
        using var secondScope = provider.CreateScope();
        var first = firstScope.ServiceProvider.GetRequiredService<I역세권디오라마조회UseCase>();
        var sameScope = firstScope.ServiceProvider.GetRequiredService<I역세권디오라마조회UseCase>();
        var second = secondScope.ServiceProvider.GetRequiredService<I역세권디오라마조회UseCase>();

        Assert.IsType<역세권디오라마Catalog>(catalog);
        Assert.IsType<역세권디오라마조회UseCase>(first);
        Assert.Same(first, sameScope);
        Assert.NotSame(first, second);
    }

    [Fact]
    public async Task 공식세역은_서로충돌하지않는식별자와_표준1km창을사용한다()
    {
        var region = new FakeRegionUseCase();
        var administrativeDong = new FakeAdministrativeDongUseCase();
        var useCase = Create(region, administrativeDong);

        var catalog = await useCase.CatalogAsync(CancellationToken.None);

        Assert.Equal(
        [
            StationDioramaPolicy.MyeonmokTransitStationStableId,
            StationDioramaPolicy.SagajeongTransitStationStableId,
            StationDioramaPolicy.YongmasanTransitStationStableId
        ], catalog.Items.Select(item => item.TransitStationStableId));
        Assert.All(catalog.Items, item =>
        {
            Assert.Equal(StationDioramaStationTypeCodes.Normal, item.StationTypeCode);
            Assert.Equal(1000, item.WindowWidthMeters);
            Assert.Equal(1000, item.WindowDepthMeters);
            Assert.Equal(StationDioramaAvailabilityCodes.WaitingForSpatialCoverage, item.AvailabilityCode);
            Assert.False(item.DistributionApproved);
        });
        var yongmasan = catalog.Items.Single(item => item.TransitStationStableId ==
                                                     StationDioramaPolicy.YongmasanTransitStationStableId);
        Assert.Equal("용마산(용마폭포공원)", yongmasan.OfficialStationName);
        Assert.Equal("용마산역", yongmasan.DisplayName);
        Assert.Equal(
            StationDioramaNameConfirmationStatusCodes.UserConfirmedDisplayName,
            yongmasan.NameConfirmationStatusCode);
        Assert.Equal("station-diorama:kric:20260630:0723.r3", yongmasan.ManifestRevision);

        var expected = new Dictionary<string, (string Name, string Code, double Latitude, double Longitude, string RowHash)>
        {
            [StationDioramaPolicy.MyeonmokTransitStationStableId] =
                ("면목", "0721", 37.588671d, 127.087503d, "161013cbd3e805242ede1e7177bcd48489da45435f1430a88646156e04820eb8"),
            [StationDioramaPolicy.SagajeongTransitStationStableId] =
                ("사가정", "0722", 37.580912d, 127.088502d, "34d6a7f9682a3a836b4ed66c13fdb784ff5703ced915d7a4ce9b5d02578d8f5d"),
            [StationDioramaPolicy.YongmasanTransitStationStableId] =
                ("용마산(용마폭포공원)", "0723", 37.573752d, 127.086802d, "b6bafdb35285fa2c454e8608787bfa55b24373143354f44255bc74dee1ad760c")
        };
        var rowHashes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in expected)
        {
            var manifest = await useCase.ManifestAsync(item.Key, CancellationToken.None);
            Assert.NotNull(manifest);
            Assert.Equal(item.Value.Name, manifest.OfficialStationName);
            Assert.Equal("서울교통공사", manifest.Service.OperatorName);
            Assert.Equal("S1107", manifest.Service.LineCode);
            Assert.Equal(item.Value.Code, manifest.Service.StationCode);
            Assert.Equal(item.Value.Latitude, manifest.Anchor.Latitude, 6);
            Assert.Equal(item.Value.Longitude, manifest.Anchor.Longitude, 6);
            Assert.Equal(StationDioramaQualityCodes.SourceReportedWgs84Point, manifest.Anchor.QualityCode);
            Assert.Contains("현장측량값이 아님", manifest.Anchor.QualityNote, StringComparison.Ordinal);
            Assert.Equal(
                item.Value.Code == "0723"
                    ? StationDioramaNameConfirmationStatusCodes.UserConfirmedDisplayName
                    : StationDioramaNameConfirmationStatusCodes.SourceNameConfirmed,
                manifest.NameConfirmationStatusCode);
            Assert.Equal(
                item.Value.Code == "0723"
                    ? "용마산역"
                    : item.Value.Name + "역",
                manifest.DisplayName);
            Assert.Equal(
                item.Value.Code switch
                {
                    "0721" => "station-diorama:kric:20260630:0721.r2",
                    "0723" => "station-diorama:kric:20260630:0723.r3",
                    _ => "station-diorama:kric:20260630:" + item.Value.Code + ".r1"
                },
                manifest.ManifestRevision);
            Assert.Empty(manifest.LegacyAliases);
            var source = Assert.Single(manifest.StationSources);
            Assert.Equal("kric-urban-rail-stations", source.SourceId);
            Assert.Equal("data-go-kr-15093755", source.DatasetId);
            Assert.Equal("https://www.data.go.kr/data/15093755/fileData.do", source.OfficialSourcePageUrl);
            Assert.Equal("https://data.kric.go.kr/rips/M_01_01/detail.do?id=32", source.ProviderSourcePageUrl);
            Assert.Equal("jungnang-line7-station-reference.r1", source.DataRevision);
            Assert.Equal(StationDioramaQualityCodes.OfficialSourcePendingHumanReview, source.QualityCode);
            Assert.Equal("2024-12-31", source.TargetRowReferenceDate);
            Assert.Equal(
                "cdf1d84a7e5c898b2aacd622783ba8ba9af35c40bee0561dc97d55ce8e063f94",
                source.RawContentHashSha256);
            Assert.Equal(
                "file:20260630;target-row-reference:2024-12-31;sha256:" + source.RawContentHashSha256,
                source.SourceRevision);
            Assert.Equal(new DateTime(2026, 9, 13, 2, 2, 58, 672, DateTimeKind.Utc), source.CollectedAtUtc);
            Assert.Equal("line=S1107;station=" + item.Value.Code, source.SourceRowReference);
            Assert.Equal(StationDioramaPolicy.SourceRowHashCanonicalVersion, source.SourceRowHashCanonicalVersion);
            Assert.Equal(item.Value.RowHash, source.SourceRowHashSha256);
            Assert.True(rowHashes.Add(source.SourceRowHashSha256));
            Assert.Equal("이용허락범위 제한 없음 (공공데이터포털 dataset metadata)", source.LicenseObserved);
            Assert.Equal("PendingHumanReview", source.ReviewStatusCode);
            Assert.Equal(
                "PrivateReviewOnly;NoPublicDistribution;NoOperationalAuthority;" +
                "DatasetLicenseObserved;CurrentFileVersionAlignmentPending;" +
                "SourceReportedPointNotPlatformSurvey;NoAreaCoverageInference",
                source.LimitationCode);
            Assert.Equal(StationDioramaRightsCodes.PrivatePreviewOnly, source.RightsCode);
            Assert.False(source.DistributionApproved);
        }
        Assert.Equal(4, region.ManifestCallCount);
        Assert.Equal(0, administrativeDong.ManifestCallCount);

        var myeonmok = await useCase.ManifestAsync(
            StationDioramaPolicy.MyeonmokTransitStationStableId,
            CancellationToken.None);
        Assert.NotNull(myeonmok);
        Assert.Equal(RegionExperiencePackagePolicy.MyeonmokStationRegionStableId, myeonmok.RegionStableId);
        Assert.Equal(6, myeonmok.AdministrativeAreaStableIds.Length);
        Assert.Contains("region:kr:hjd:1126056500", myeonmok.AdministrativeAreaStableIds);
        AssertSpatialCandidate(
            myeonmok,
            "myeonmok-station-spatial-snapshot.private-review.r1",
            "48FED5BFA9B06075B27D7DCF22EFDF3AE3F98222D337D0298C01C098A62B913A",
            "4B8D45AE3414218228FEB1436AE8E99AB3999C83A30607292AC594C0099530B1",
            10_111_746,
            4_165,
            124,
            48,
            6,
            2,
            [
                "BuildingRightsConflictUnresolved",
                "LegalDongBoundaryCoverageUnverified",
                "RoadWidthUnavailable",
                "SurfaceCoverageIncomplete",
                "TraversalAuthorityUnavailable"
            ]);

        var yongmasanManifest = await useCase.ManifestAsync(
            StationDioramaPolicy.YongmasanTransitStationStableId,
            CancellationToken.None);
        Assert.NotNull(yongmasanManifest);
        Assert.Equal(string.Empty, yongmasanManifest.RegionStableId);
        Assert.Empty(yongmasanManifest.AdministrativeAreaStableIds);
        Assert.Empty(yongmasanManifest.AreaProjections);
        Assert.Equal(
            StationDioramaAvailabilityCodes.WaitingForSpatialCoverage,
            yongmasanManifest.AvailabilityCode);
        AssertSpatialCandidate(
            yongmasanManifest,
            "yongmasan-station-spatial-snapshot.private-review.r1",
            "43765866F70409D71EFD1599CA0A654B97CA01584A18F4C0EA6C0BE2AF0C6B0D",
            "6AC2E8595B913B6AE80D6426476F334225491E89555CEB9A5C1A17BD568120B6",
            7_069_044,
            2_859,
            116,
            41,
            4,
            10,
            [
                "BuildingRightsConflictUnresolved",
                "LegalDongBoundaryCoverageUnverified",
                "OsmRelationMembersMissing",
                "RoadWidthUnavailable",
                "SurfaceCoverageIncomplete",
                "TraversalAuthorityUnavailable"
            ]);
    }

    [Fact]
    public async Task 원천URL_권리검토_수집시각_행결속은_manifest_hash에포함된다()
    {
        Action<StationDioramaSourceEvidence>[] mutations =
        [
            value => value.OfficialSourcePageUrl += "/changed",
            value => value.ProviderSourcePageUrl += "/changed",
            value => value.SourceRevision += ":changed",
            value => value.DataRevision += ":changed",
            value => value.RawContentHashSha256 = new string('0', 64),
            value => value.CollectedAtUtc = value.CollectedAtUtc.AddSeconds(1),
            value => value.SourceRowReference += ";changed=true",
            value => value.SourceRowHashCanonicalVersion += ".changed",
            value => value.SourceRowHashSha256 = new string('1', 64),
            value => value.LicenseObserved += ":changed",
            value => value.QualityCode += ":changed",
            value => value.ReviewStatusCode += ":changed",
            value => value.LimitationCode += ":changed",
            value => value.RightsCode += ":changed",
            value => value.DistributionApproved = true
        ];

        foreach (var mutate in mutations)
        {
            var useCase = Create(new FakeRegionUseCase(), new FakeAdministrativeDongUseCase());
            var manifest = await useCase.ManifestAsync(
                StationDioramaPolicy.MyeonmokTransitStationStableId,
                CancellationToken.None);
            Assert.NotNull(manifest);
            var originalHash = manifest.ManifestHashSha256;

            mutate(Assert.Single(manifest.StationSources));

            Assert.NotEqual(originalHash, 역세권디오라마조회UseCase.ComputeManifestHash(manifest));
        }
    }

    [Fact]
    public async Task 역별_공간사본후보의_모든fingerprint와권위경계는_manifest_hash에포함된다()
    {
        Action<StationDioramaSpatialSnapshotCandidate>[] mutations =
        [
            value => value.SchemaVersion += ".changed",
            value => value.Revision += ".changed",
            value => value.ContentHashSha256 = new string('0', 64),
            value => value.PayloadHashSha256 = new string('1', 64),
            value => value.PayloadByteLength++,
            value => value.BuildingCount++,
            value => value.RoadCount++,
            value => value.SurfaceCount++,
            value => value.AdministrativeAreaCount++,
            value => value.CoverageCellCount++,
            value => value.MissingCoverageCellCount++,
            value => value.MissingCoverageCodes = [.. value.MissingCoverageCodes, "Changed"],
            value => value.ReviewStatusCode += ".changed",
            value => value.ServerLoadable = true,
            value => value.DistributionApproved = true,
            value => value.TraversalReady = true,
            value => value.GameplayReady = true
        ];

        foreach (var stationStableId in new[]
                 {
                     StationDioramaPolicy.MyeonmokTransitStationStableId,
                     StationDioramaPolicy.YongmasanTransitStationStableId
                 })
        foreach (var mutate in mutations)
        {
            var manifest = await Create(new FakeRegionUseCase(), new FakeAdministrativeDongUseCase())
                .ManifestAsync(stationStableId, CancellationToken.None);
            Assert.NotNull(manifest);
            var originalHash = manifest.ManifestHashSha256;

            mutate(Assert.IsType<StationDioramaSpatialSnapshotCandidate>(manifest.SpatialSnapshotCandidate));

            Assert.NotEqual(originalHash, 역세권디오라마조회UseCase.ComputeManifestHash(manifest));
        }
    }

    [Fact]
    public async Task 용마산은_사본후보가있어도_stationScoped서버범위는연결하지않는다()
    {
        var region = new FakeRegionUseCase(ReadyRegion());
        var administrativeDong = new FakeAdministrativeDongUseCase(ReadyAdministrativeDong());
        var useCase = Create(region, administrativeDong);

        var myeonmok = await useCase.ManifestAsync(
            StationDioramaPolicy.MyeonmokTransitStationStableId,
            CancellationToken.None);
        var yongmasan = await useCase.ManifestAsync(
            StationDioramaPolicy.YongmasanTransitStationStableId,
            CancellationToken.None);

        Assert.NotNull(myeonmok);
        Assert.NotNull(yongmasan);
        Assert.Equal(StationDioramaAvailabilityCodes.WaitingForSpatialCoverage, myeonmok.AvailabilityCode);
        Assert.Equal(StationDioramaAvailabilityCodes.WaitingForSpatialCoverage, yongmasan.AvailabilityCode);
        Assert.Empty(myeonmok.AreaProjections);
        Assert.Empty(yongmasan.AreaProjections);
        Assert.NotNull(yongmasan.SpatialSnapshotCandidate);
        Assert.False(yongmasan.SpatialSnapshotCandidate.ServerLoadable);
        Assert.Equal("", yongmasan.RegionStableId);
        Assert.Equal(1, region.ManifestCallCount);
        Assert.Equal(0, administrativeDong.ManifestCallCount);
    }

    [Fact]
    public async Task 사가정은_기존지역과_1km창에겹치는행정동타일만부분범위로참조한다()
    {
        var region = new FakeRegionUseCase(ReadyRegion());
        var administrativeDong = new FakeAdministrativeDongUseCase(ReadyAdministrativeDong());
        var useCase = Create(region, administrativeDong);

        var first = await useCase.ManifestAsync(
            StationDioramaPolicy.SagajeongTransitStationStableId,
            CancellationToken.None);
        var second = await useCase.ManifestAsync(
            StationDioramaPolicy.SagajeongTransitStationStableId,
            CancellationToken.None);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(StationDioramaAvailabilityCodes.PartialCoverage, first.AvailabilityCode);
        Assert.Equal(RegionExperiencePackagePolicy.SagajeongRegionStableId, first.RegionStableId);
        Assert.Equal("region-manifest-hash", first.RegionManifestHashSha256);
        Assert.Contains(AdministrativeDongDioramaPolicy.FirstAdministrativeAreaStableId, first.AdministrativeAreaStableIds);
        Assert.Contains(first.GeographyEndpoints, item => item.RouteTemplate == AdministrativeDongDioramaRoutes.Manifest);
        Assert.Contains(first.GeographyEndpoints, item => item.RouteTemplate == AdministrativeDongDioramaRoutes.Tile);
        var projection = Assert.Single(first.AreaProjections);
        Assert.Equal("admin-projection-hash", projection.ProjectionHashSha256);
        Assert.Equal(AdministrativeDongDioramaReadinessCodes.PrivateReviewOnly, projection.ReadinessCode);
        Assert.Equal(9, projection.SelectedTiles.Length);
        Assert.DoesNotContain(projection.SelectedTiles, tile => tile.TileStableId == "tile:outside");
        Assert.Equal(1000d, projection.WindowBounds.MaxX - projection.WindowBounds.MinX, 5);
        Assert.Equal(1000d, projection.WindowBounds.MaxZ - projection.WindowBounds.MinZ, 5);
        Assert.Equal("spatial-source", Assert.Single(projection.Sources).SourceId);
        Assert.Contains(StationDioramaQualityCodes.KnownPartialSpatialCoverage, first.QualityCodes);
        Assert.True(first.ObservationPresentationOnly);
        Assert.False(first.TraversalReady);
        Assert.False(first.GameplayReady);
        Assert.False(first.DistributionApproved);
        Assert.Empty(first.LegacyAliases);
        Assert.Equal(first.ManifestHashSha256, second.ManifestHashSha256);
        Assert.Equal(18, administrativeDong.TileCallCount);
    }

    [Fact]
    public async Task 지역응답이_대상schema와읽기전용manifest_tile경계를벗어나면_공간을연결하지않는다()
    {
        var wrongSchema = ReadyRegion();
        wrongSchema.SchemaVersion = "region-experience-package.future";
        var wrongRegion = ReadyRegion();
        wrongRegion.RegionStableId = "world-region:kr:seoul:jungnang:other.r1";
        var stateChanging = ReadyRegion();
        stateChanging.Layers[0].ChangesOperationalState = true;
        var postEndpoint = ReadyRegion();
        postEndpoint.Layers[0].Endpoints[0].HttpMethod = "POST";
        var extraEndpoint = ReadyRegion();
        extraEndpoint.Layers[0].Endpoints = extraEndpoint.Layers[0].Endpoints
            .Append(new RegionExperienceLayerEndpoint
            {
                PurposeCode = "Extra",
                HttpMethod = "GET",
                RouteTemplate = "api/v1/world/unsafe"
            })
            .ToArray();
        var unexpectedAdministrativeArea = ReadyRegion();
        unexpectedAdministrativeArea.AdministrativeAreaStableIds = unexpectedAdministrativeArea.AdministrativeAreaStableIds
            .Append("region:kr:hjd:1126057000")
            .ToArray();

        foreach (var unsafeRegion in new[]
                 {
                     wrongSchema, wrongRegion, stateChanging, postEndpoint, extraEndpoint,
                     unexpectedAdministrativeArea
                 })
        {
            var administrativeDong = new FakeAdministrativeDongUseCase(ReadyAdministrativeDong());
            var useCase = Create(new FakeRegionUseCase(unsafeRegion), administrativeDong);

            var manifest = await useCase.ManifestAsync(
                StationDioramaPolicy.SagajeongTransitStationStableId,
                CancellationToken.None);

            Assert.NotNull(manifest);
            Assert.Equal(StationDioramaAvailabilityCodes.WaitingForSpatialCoverage, manifest.AvailabilityCode);
            Assert.Empty(manifest.GeographyEndpoints);
            Assert.Empty(manifest.AreaProjections);
            Assert.Equal(0, administrativeDong.ManifestCallCount);
        }
    }

    [Fact]
    public async Task 행정동응답이_대상과비권위타일불변조건을벗어나면_부분범위로승격하지않는다()
    {
        var unsafeManifests = new[]
        {
            InvalidAdministrativeDong(value => value.AdministrativeAreaStableId = "region:kr:hjd:1126057000"),
            InvalidAdministrativeDong(value => value.SchemaVersion = "administrative-dong-diorama.future"),
            InvalidAdministrativeDong(value => value.DataPolicyCode = "OperationalAuthority"),
            InvalidAdministrativeDong(value => value.ObservationPresentationOnly = false),
            InvalidAdministrativeDong(value => value.TraversalReady = true),
            InvalidAdministrativeDong(value => value.GameplayReady = true),
            InvalidAdministrativeDong(value => value.DistributionApproved = true),
            InvalidAdministrativeDong(value => value.ReadinessCode = AdministrativeDongDioramaReadinessCodes.Ready),
            InvalidAdministrativeDong(value => value.Tiles[1].TileStableId = value.Tiles[0].TileStableId),
            InvalidAdministrativeDong(value =>
            {
                value.Tiles[1].TileIndexX = value.Tiles[0].TileIndexX;
                value.Tiles[1].TileIndexZ = value.Tiles[0].TileIndexZ;
            }),
            InvalidAdministrativeDong(value => value.Tiles[0].BuildingCount = -1),
            InvalidAdministrativeDong(value => value.Tiles[0].RoadSegmentCount = -1),
            InvalidAdministrativeDong(value => value.Bounds.MaxX = value.Bounds.MinX),
            InvalidAdministrativeDong(value => value.Boundary = []),
            InvalidAdministrativeDong(value => value.Boundary[0].X = double.NaN),
            InvalidAdministrativeDong(value => value.Boundary =
            [
                Point(0d, 0d), Point(100d, 100d), Point(200d, 200d)
            ]),
            InvalidAdministrativeDong(value => value.Boundary =
            [
                Point(2000d, 2000d), Point(2500d, 2000d),
                Point(2500d, 2500d), Point(2000d, 2500d)
            ])
        };

        foreach (var unsafeManifest in unsafeManifests)
        {
            var administrativeDong = new FakeAdministrativeDongUseCase(unsafeManifest);
            var useCase = Create(new FakeRegionUseCase(ReadyRegion()), administrativeDong);

            var manifest = await useCase.ManifestAsync(
                StationDioramaPolicy.SagajeongTransitStationStableId,
                CancellationToken.None);

            Assert.NotNull(manifest);
            Assert.Equal(StationDioramaAvailabilityCodes.WaitingForSpatialCoverage, manifest.AvailabilityCode);
            Assert.Empty(manifest.AreaProjections);
            Assert.Equal(1, administrativeDong.ManifestCallCount);
        }
    }

    [Fact]
    public async Task 실제tilepayload가_manifest요약과다르면_부분범위로승격하지않는다()
    {
        Action<AdministrativeDongDioramaTile>[] mutations =
        [
            value => value.SchemaVersion = "administrative-dong-diorama.future",
            value => value.AdministrativeAreaStableId = "region:kr:hjd:1126057000",
            value => value.ProjectionHashSha256 = "other-projection",
            value => value.TileStableId += ":other",
            value => value.TileHashSha256 = "other-tile-hash",
            value => value.TileIndexX++,
            value => value.TileIndexZ--,
            value => value.Buildings = value.Buildings.Skip(1).ToArray(),
            value => value.Roads = value.Roads.Skip(1).ToArray(),
            value => value.Bounds.MinX += 1d,
            value => value.Buildings[0].Footprint[0].X = double.NaN,
            value => value.Roads[0].From.Z = double.PositiveInfinity
        ];

        foreach (var mutate in mutations)
        {
            var administrativeManifest = ReadyAdministrativeDong();
            var payloads = ReadyTiles(administrativeManifest);
            var firstCandidate = payloads.Values
                .OrderBy(value => value.TileIndexX)
                .ThenBy(value => value.TileIndexZ)
                .First();
            mutate(firstCandidate);
            var useCase = Create(
                new FakeRegionUseCase(ReadyRegion()),
                new FakeAdministrativeDongUseCase(administrativeManifest, payloads));

            var manifest = await useCase.ManifestAsync(
                StationDioramaPolicy.SagajeongTransitStationStableId,
                CancellationToken.None);

            Assert.NotNull(manifest);
            Assert.Equal(StationDioramaAvailabilityCodes.WaitingForSpatialCoverage, manifest.AvailabilityCode);
            Assert.Empty(manifest.AreaProjections);
        }
    }

    [Fact]
    public async Task 격자는겹쳐도_tilepayload가없거나_창안실제도형이없으면_대기한다()
    {
        var missingPayloadManifest = ReadyAdministrativeDong();
        var missingPayloads = ReadyTiles(missingPayloadManifest);
        missingPayloads.Remove(missingPayloads.Keys.OrderBy(value => value, StringComparer.Ordinal).First());
        var missingPayload = Create(
            new FakeRegionUseCase(ReadyRegion()),
            new FakeAdministrativeDongUseCase(missingPayloadManifest, missingPayloads));

        var missing = await missingPayload.ManifestAsync(
            StationDioramaPolicy.SagajeongTransitStationStableId,
            CancellationToken.None);

        Assert.NotNull(missing);
        Assert.Equal(StationDioramaAvailabilityCodes.WaitingForSpatialCoverage, missing.AvailabilityCode);
        Assert.Empty(missing.AreaProjections);

        var emptyGeometryManifest = ReadyAdministrativeDong();
        foreach (var summary in emptyGeometryManifest.Tiles)
        {
            summary.BuildingCount = 0;
            summary.RoadSegmentCount = 0;
        }
        var emptyGeometry = Create(
            new FakeRegionUseCase(ReadyRegion()),
            new FakeAdministrativeDongUseCase(emptyGeometryManifest));

        var empty = await emptyGeometry.ManifestAsync(
            StationDioramaPolicy.SagajeongTransitStationStableId,
            CancellationToken.None);

        Assert.NotNull(empty);
        Assert.Equal(StationDioramaAvailabilityCodes.WaitingForSpatialCoverage, empty.AvailabilityCode);
        Assert.Empty(empty.AreaProjections);

        var outsideWindowManifest = ReadyAdministrativeDong();
        outsideWindowManifest.Tiles =
        [
            outsideWindowManifest.Tiles.Single(summary => summary.TileIndexX == -1 && summary.TileIndexZ == 0)
        ];
        var outsideWindowPayloads = ReadyTiles(outsideWindowManifest);
        var outsideWindowTile = Assert.Single(outsideWindowPayloads.Values);
        MoveGeometry(outsideWindowTile, 51d, 100d);
        var outsideWindow = Create(
            new FakeRegionUseCase(ReadyRegion()),
            new FakeAdministrativeDongUseCase(outsideWindowManifest, outsideWindowPayloads));

        var outside = await outsideWindow.ManifestAsync(
            StationDioramaPolicy.SagajeongTransitStationStableId,
            CancellationToken.None);

        Assert.NotNull(outside);
        Assert.Equal(StationDioramaAvailabilityCodes.WaitingForSpatialCoverage, outside.AvailabilityCode);
        Assert.Empty(outside.AreaProjections);
    }

    [Fact]
    public async Task 창과경계가따로겹쳐도_tile도형이_두범위의교차안에없으면_대기한다()
    {
        var administrativeManifest = ReadyAdministrativeDong();
        administrativeManifest.Tiles =
        [
            administrativeManifest.Tiles.Single(summary => summary.TileIndexX == 0 && summary.TileIndexZ == 0)
        ];
        administrativeManifest.Boundary =
        [
            Point(50d, -100d), Point(100d, -100d),
            Point(100d, 100d), Point(50d, 100d)
        ];
        var useCase = Create(
            new FakeRegionUseCase(ReadyRegion()),
            new FakeAdministrativeDongUseCase(administrativeManifest));

        var manifest = await useCase.ManifestAsync(
            StationDioramaPolicy.SagajeongTransitStationStableId,
            CancellationToken.None);

        Assert.NotNull(manifest);
        Assert.Equal(StationDioramaAvailabilityCodes.WaitingForSpatialCoverage, manifest.AvailabilityCode);
        Assert.Empty(manifest.AreaProjections);
    }

    [Fact]
    public async Task Catalog정의는_중복ID_서비스segment_좌표_일반역1km조건을거절한다()
    {
        var seed = new 역세권디오라마Catalog().All[0];
        var spatialSnapshot = Assert.IsType<역세권디오라마공간사본Definition>(seed.SpatialSnapshot);
        var cases = new (IReadOnlyList<역세권디오라마Definition> Definitions, string ErrorCode)[]
        {
            (new[] { seed, seed }, "TransitStationStableIdDuplicate"),
            (new[] { seed with { StationCode = "9999" } }, "TransitStationStableIdServiceMismatch"),
            (new[] { seed with { Latitude = double.NaN } }, "TransitStationCoordinateInvalid"),
            (new[] { seed with { Longitude = 181d } }, "TransitStationCoordinateInvalid"),
            (new[] { seed with { WindowWidthMeters = 999 } }, "NormalTransitStationWindowMustBe1000Meters"),
            (new[] { seed with { WindowDepthMeters = 1001 } }, "NormalTransitStationWindowMustBe1000Meters"),
            (new[] { seed with { LegacyAliases = ["anchor:sagajeong-station"] } },
                "TransitStationSpatialReferenceCannotBeLegacyAlias"),
            (new[] { seed with { SpatialSnapshot = spatialSnapshot with
                { TransitStationStableId = StationDioramaPolicy.SagajeongTransitStationStableId } } },
                "TransitStationSpatialSnapshotIdentityMismatch"),
            (new[] { seed with { SpatialSnapshot = spatialSnapshot with { SchemaVersion = "changed" } } },
                "TransitStationSpatialSnapshotSchemaInvalid"),
            (new[] { seed with { SpatialSnapshot = spatialSnapshot with { ContentHashSha256 = "not-a-hash" } } },
                "TransitStationSpatialSnapshotFingerprintInvalid"),
            (new[] { seed with { SpatialSnapshot = spatialSnapshot with { MissingCoverageCellCount = 101 } } },
                "TransitStationSpatialSnapshotCountsInvalid"),
            (new[] { seed with { SpatialSnapshot = spatialSnapshot with
                { MissingCoverageCodes = ["Repeated", "Repeated"] } } },
                "TransitStationSpatialSnapshotMissingCoverageInvalid")
        };

        foreach (var item in cases)
        {
            var useCase = new 역세권디오라마조회UseCase(
                new FakeCatalog(item.Definitions),
                new FakeRegionUseCase(),
                new FakeAdministrativeDongUseCase());

            var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                useCase.CatalogAsync(CancellationToken.None));
            Assert.Equal(item.ErrorCode, error.Message);
        }
    }

    [Fact]
    public async Task 사가정행정동manifest가없으면_가짜공간없이대기하고_잘못된식별자를거절한다()
    {
        var region = new FakeRegionUseCase(ReadyRegion());
        var useCase = Create(region, new FakeAdministrativeDongUseCase());

        var manifest = await useCase.ManifestAsync(
            StationDioramaPolicy.SagajeongTransitStationStableId,
            CancellationToken.None);

        Assert.NotNull(manifest);
        Assert.Equal(StationDioramaAvailabilityCodes.WaitingForSpatialCoverage, manifest.AvailabilityCode);
        Assert.Empty(manifest.AreaProjections);
        Assert.Contains(StationDioramaQualityCodes.SpatialCoverageNotPublished, manifest.QualityCodes);
        Assert.Null(await useCase.ManifestAsync("station:kr:kric:s1107:9999", CancellationToken.None));
        var error = await Assert.ThrowsAsync<ArgumentException>(() =>
            useCase.ManifestAsync("workstation:packing:01", CancellationToken.None));
        Assert.Equal("TransitStationDioramaStableIdInvalid", error.Message.Split(' ')[0]);
    }

    private static 역세권디오라마조회UseCase Create(
        FakeRegionUseCase region,
        FakeAdministrativeDongUseCase administrativeDong)
        => new(new 역세권디오라마Catalog(), region, administrativeDong);

    private static void AssertSpatialCandidate(
        StationDioramaManifest manifest,
        string revision,
        string contentHash,
        string payloadHash,
        long payloadByteLength,
        int buildingCount,
        int roadCount,
        int surfaceCount,
        int administrativeAreaCount,
        int missingCoverageCellCount,
        IReadOnlyList<string> missingCoverageCodes)
    {
        var candidate = Assert.IsType<StationDioramaSpatialSnapshotCandidate>(manifest.SpatialSnapshotCandidate);
        Assert.Equal("ssalddel.station-spatial-snapshot.v1", candidate.SchemaVersion);
        Assert.Equal(revision, candidate.Revision);
        Assert.Equal(contentHash, candidate.ContentHashSha256);
        Assert.Equal(payloadHash, candidate.PayloadHashSha256);
        Assert.Equal(payloadByteLength, candidate.PayloadByteLength);
        Assert.Equal(buildingCount, candidate.BuildingCount);
        Assert.Equal(roadCount, candidate.RoadCount);
        Assert.Equal(surfaceCount, candidate.SurfaceCount);
        Assert.Equal(administrativeAreaCount, candidate.AdministrativeAreaCount);
        Assert.Equal(100, candidate.CoverageCellCount);
        Assert.Equal(missingCoverageCellCount, candidate.MissingCoverageCellCount);
        Assert.Equal(missingCoverageCodes, candidate.MissingCoverageCodes);
        Assert.Equal(
            StationDioramaSpatialSnapshotStatusCodes.LocalPrivateReview,
            candidate.ReviewStatusCode);
        Assert.False(candidate.ServerLoadable);
        Assert.False(candidate.DistributionApproved);
        Assert.False(candidate.TraversalReady);
        Assert.False(candidate.GameplayReady);
    }

    private static RegionExperiencePackageManifest ReadyRegion()
        => new()
        {
            RegionStableId = RegionExperiencePackagePolicy.SagajeongRegionStableId,
            ManifestHashSha256 = "region-manifest-hash",
            SpatialRegistryStableId = "neighborhood-package-registry:seoul-east.v2",
            SpatialRegistryRevision = "seoul-east-neighborhood-packages.r2",
            SpatialPackageStableIds = ["neighborhood-spatial-package:region:kr:bjd:1126010100.v2"],
            AdministrativeAreaStableIds = [AdministrativeDongDioramaPolicy.FirstAdministrativeAreaStableId],
            LegalAreaStableIds = ["region:kr:bjd:1126010100"],
            Layers =
            [
                new RegionExperienceLayerReference
                {
                    LayerStableId = "region-layer:sagajeong:geography.r1",
                    LayerKindCode = RegionExperienceLayerKinds.Geography,
                    AvailabilityCode = RegionExperienceLayerAvailabilityCodes.Ready,
                    Endpoints =
                    [
                        new RegionExperienceLayerEndpoint
                        {
                            PurposeCode = "Manifest",
                            HttpMethod = "GET",
                            RouteTemplate = AdministrativeDongDioramaRoutes.Manifest
                        },
                        new RegionExperienceLayerEndpoint
                        {
                            PurposeCode = "Tile",
                            HttpMethod = "GET",
                            RouteTemplate = AdministrativeDongDioramaRoutes.Tile
                        }
                    ]
                }
            ]
        };

    private static AdministrativeDongDioramaManifest ReadyAdministrativeDong()
        => new()
        {
            AdministrativeAreaStableId = AdministrativeDongDioramaPolicy.FirstAdministrativeAreaStableId,
            ProjectionHashSha256 = "admin-projection-hash",
            ReadinessCode = AdministrativeDongDioramaReadinessCodes.PrivateReviewOnly,
            CoordinateFrame = new AdministrativeDongDioramaCoordinateFrame
            {
                Method = "WGS84-ECEF-ENU-at-zero-altitude",
                OriginLatitude = 37.5806971d,
                OriginLongitude = 127.0884106d,
                WorldOffsetX = 550d,
                WorldOffsetZ = 8d,
                MetersPerUnit = 1d
            },
            Bounds = new AdministrativeDongDioramaBounds
            {
                MinX = -1000d,
                MinZ = -1000d,
                MaxX = 3000d,
                MaxZ = 3000d
            },
            Boundary =
            [
                Point(-1000d, -1000d), Point(3000d, -1000d),
                Point(3000d, 3000d), Point(-1000d, 3000d)
            ],
            Tiles = Enumerable.Range(-1, 3)
                .SelectMany(x => Enumerable.Range(-1, 3).Select(z =>
                    new AdministrativeDongDioramaTileSummary
                    {
                        TileStableId = $"tile:{x}:{z}",
                        TileIndexX = x,
                        TileIndexZ = z,
                        TileHashSha256 = $"tile-hash:{x}:{z}",
                        BuildingCount = 10,
                        RoadSegmentCount = 20
                    }))
                .Append(new AdministrativeDongDioramaTileSummary
                {
                    TileStableId = "tile:outside",
                    TileIndexX = 2,
                    TileIndexZ = 0,
                    TileHashSha256 = "tile-outside-hash",
                    BuildingCount = 1,
                    RoadSegmentCount = 1
                })
                .ToArray(),
            Sources =
            [
                new AdministrativeDongDioramaSourceAttribution
                {
                    SourceId = "spatial-source",
                    DatasetId = "spatial-dataset",
                    SourceRevision = "spatial-r1",
                    ContentHashSha256 = "spatial-hash",
                    LicenseCode = "review-required",
                    LimitationCode = "private-review"
                }
            ]
        };

    private static Dictionary<string, AdministrativeDongDioramaTile> ReadyTiles(
        AdministrativeDongDioramaManifest manifest)
    {
        var center = 행정동경계GeoJsonReader.ProjectWgs84(
            37.580912d,
            127.088502d,
            manifest.CoordinateFrame);
        var window = new AdministrativeDongDioramaBounds
        {
            MinX = center.X - 500d,
            MinZ = center.Z - 500d,
            MaxX = center.X + 500d,
            MaxZ = center.Z + 500d
        };
        return manifest.Tiles.ToDictionary(summary => summary.TileStableId, summary =>
        {
            var bounds = TileBounds(summary, manifest.CoordinateFrame);
            var overlapMinX = Math.Max(bounds.MinX, window.MinX);
            var overlapMinZ = Math.Max(bounds.MinZ, window.MinZ);
            var overlapMaxX = Math.Min(bounds.MaxX, window.MaxX);
            var overlapMaxZ = Math.Min(bounds.MaxZ, window.MaxZ);
            var intersects = overlapMaxX > overlapMinX && overlapMaxZ > overlapMinZ;
            var x = intersects ? (overlapMinX + overlapMaxX) / 2d : (bounds.MinX + bounds.MaxX) / 2d;
            var z = intersects ? (overlapMinZ + overlapMaxZ) / 2d : (bounds.MinZ + bounds.MaxZ) / 2d;
            return new AdministrativeDongDioramaTile
            {
                AdministrativeAreaStableId = manifest.AdministrativeAreaStableId,
                ProjectionHashSha256 = manifest.ProjectionHashSha256,
                TileStableId = summary.TileStableId,
                TileHashSha256 = summary.TileHashSha256,
                TileIndexX = summary.TileIndexX,
                TileIndexZ = summary.TileIndexZ,
                Bounds = bounds,
                Buildings = Enumerable.Range(0, summary.BuildingCount)
                    .Select(index => new AdministrativeDongDioramaBuilding
                    {
                        BuildingStableId = summary.TileStableId + $":building:{index}",
                        Footprint =
                        [
                            Point(x - 0.1d, z - 0.1d), Point(x + 0.1d, z - 0.1d),
                            Point(x + 0.1d, z + 0.1d), Point(x - 0.1d, z + 0.1d)
                        ]
                    })
                    .ToArray(),
                Roads = Enumerable.Range(0, summary.RoadSegmentCount)
                    .Select(index => new AdministrativeDongDioramaRoadSegment
                    {
                        RoadStableId = summary.TileStableId + $":road:{index}",
                        From = Point(x - 0.1d, z),
                        To = Point(x + 0.1d, z)
                    })
                    .ToArray()
            };
        }, StringComparer.Ordinal);
    }

    private static AdministrativeDongDioramaBounds TileBounds(
        AdministrativeDongDioramaTileSummary summary,
        AdministrativeDongDioramaCoordinateFrame frame)
    {
        var minX = frame.WorldOffsetX + summary.TileIndexX * AdministrativeDongDioramaPolicy.TileSizeMeters;
        var minZ = frame.WorldOffsetZ + summary.TileIndexZ * AdministrativeDongDioramaPolicy.TileSizeMeters;
        return new AdministrativeDongDioramaBounds
        {
            MinX = minX,
            MinZ = minZ,
            MaxX = minX + AdministrativeDongDioramaPolicy.TileSizeMeters,
            MaxZ = minZ + AdministrativeDongDioramaPolicy.TileSizeMeters
        };
    }

    private static AdministrativeDongDioramaPoint Point(double x, double z)
        => new() { X = x, Z = z };

    private static void MoveGeometry(AdministrativeDongDioramaTile tile, double targetX, double targetZ)
    {
        var footprint = tile.Buildings[0].Footprint;
        var centerX = footprint.Average(point => point.X);
        var centerZ = footprint.Average(point => point.Z);
        var deltaX = targetX - centerX;
        var deltaZ = targetZ - centerZ;
        foreach (var building in tile.Buildings)
        foreach (var point in building.Footprint)
        {
            point.X += deltaX;
            point.Z += deltaZ;
        }
        foreach (var road in tile.Roads)
        {
            road.From.X += deltaX;
            road.From.Z += deltaZ;
            road.To.X += deltaX;
            road.To.Z += deltaZ;
        }
    }

    private static AdministrativeDongDioramaManifest InvalidAdministrativeDong(
        Action<AdministrativeDongDioramaManifest> mutation)
    {
        var value = ReadyAdministrativeDong();
        mutation(value);
        return value;
    }

    private sealed class FakeCatalog(IReadOnlyList<역세권디오라마Definition> all) : I역세권디오라마Catalog
    {
        public IReadOnlyList<역세권디오라마Definition> All { get; } = all;
    }

    private sealed class FakeRegionUseCase(RegionExperiencePackageManifest? manifest = null)
        : I지역ExperiencePackage조회UseCase
    {
        public int ManifestCallCount { get; private set; }

        public Task<RegionExperiencePackageCatalogResponse> CatalogAsync(CancellationToken cancellationToken)
            => Task.FromResult(new RegionExperiencePackageCatalogResponse());

        public Task<RegionExperiencePackageManifest?> ManifestAsync(
            string regionStableId,
            CancellationToken cancellationToken)
        {
            ManifestCallCount++;
            return Task.FromResult(manifest);
        }
    }

    private sealed class FakeAdministrativeDongUseCase : I행정동디오라마조회UseCase
    {
        private readonly AdministrativeDongDioramaManifest? manifest;
        private readonly IReadOnlyDictionary<string, AdministrativeDongDioramaTile> tiles;

        public FakeAdministrativeDongUseCase(
            AdministrativeDongDioramaManifest? manifest = null,
            IReadOnlyDictionary<string, AdministrativeDongDioramaTile>? tiles = null)
        {
            this.manifest = manifest;
            this.tiles = tiles ?? (CanBuildReadyTiles(manifest)
                ? ReadyTiles(manifest!)
                : new Dictionary<string, AdministrativeDongDioramaTile>(StringComparer.Ordinal));
        }

        public int ManifestCallCount { get; private set; }
        public int TileCallCount { get; private set; }

        private static bool CanBuildReadyTiles(AdministrativeDongDioramaManifest? value)
            => value?.Tiles is not null
               && value.Tiles.All(summary => summary is not null
                                             && !string.IsNullOrWhiteSpace(summary.TileStableId)
                                             && summary.BuildingCount >= 0
                                             && summary.RoadSegmentCount >= 0)
               && value.Tiles.GroupBy(summary => summary.TileStableId, StringComparer.Ordinal)
                   .All(group => group.Count() == 1);

        public Task<AdministrativeDongDioramaManifest?> ManifestAsync(
            string administrativeAreaStableId,
            CancellationToken cancellationToken)
        {
            ManifestCallCount++;
            return Task.FromResult(manifest);
        }

        public Task<AdministrativeDongDioramaTile?> TileAsync(
            string administrativeAreaStableId,
            string tileStableId,
            CancellationToken cancellationToken)
        {
            TileCallCount++;
            return Task.FromResult(tiles.TryGetValue(tileStableId, out var tile) ? tile : null);
        }

        public Task<AdministrativeDongDisplayOverlayResponse?> DisplayOverlaysAsync(
            string administrativeAreaStableId,
            DateTime asOfUtc,
            CancellationToken cancellationToken)
            => Task.FromResult<AdministrativeDongDisplayOverlayResponse?>(null);
    }
}
