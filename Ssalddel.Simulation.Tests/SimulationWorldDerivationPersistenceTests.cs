using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ssalddel.Infrastructure.Persistence.PublicData;
using Ssalddel.Domain.PublicData.Korea;
using Ssalddel.Simulation.Application;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;
using Ssalddel.Simulation.Persistence;
using Ssalddel.Simulation.Hosting;

namespace Ssalddel.Simulation.Tests;

[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
    "Simulation·Unity 계약과 결정성 및 회귀 증거를 검증한다.",
    Boundary = "자동 시험 통과와 실제 Play Mode·Game View·E 승격 증거를 구분한다.")]
public sealed class SimulationWorldDerivationPersistenceTests
{
    [Fact]
    public async Task 팀역할과수집보상과전투규칙정의는_파생Db업무규칙대장에저장한다()
    {
        await using var db = CreateDb();
        await db.Database.EnsureCreatedAsync();
        var catalog = PyeongchangSimulationWorld업무규칙CatalogFactory.Create(
            "world-build:test:team-role-card",
            new string('a', 64),
            "area-set:sim:pyeongchang:farm-hub-town.v1");
        var store = new SimulationWorld업무규칙집결Store(db);

        var result = await store.저장Async(catalog, CancellationToken.None);

        Assert.True(result.Inserted);
        Assert.Equal(23, result.RuleCount);
        Assert.Equal(3, await db.BusinessSimulationRules.CountAsync(value =>
            value.DomainCode == SimulationWorld업무규칙영역Codes.팀역할));
        Assert.Contains(await db.BusinessSimulationRules.ToListAsync(), value =>
            value.StableId == PyeongchangSimulationWorldStableIds.팀역할Card장착규칙
            && value.InputContractKey == nameof(SimulationTeamRoleCardEquipRequest));
        Assert.Equal(4, await db.BusinessSimulationRules.CountAsync(value =>
            value.DomainCode == SimulationWorld업무규칙영역Codes.수집보상));
        Assert.Contains(await db.BusinessSimulationRules.ToListAsync(), value =>
            value.StableId == PyeongchangSimulationWorldStableIds.수집Card양도규칙
            && value.InputContractKey == nameof(SimulationCollectibleCardTransferRequest));
        Assert.Equal(5, await db.BusinessSimulationRules.CountAsync(value =>
            value.DomainCode == SimulationWorld업무규칙영역Codes.전투));
        Assert.Contains(await db.BusinessSimulationRules.ToListAsync(), value =>
            value.StableId == PyeongchangSimulationWorldStableIds.전투반응판정규칙
            && value.InputContractKey == nameof(SimulationCombatReactionConfirmRequest));
        Assert.Contains(await db.BusinessSimulationRules.ToListAsync(), value =>
            value.StableId == PyeongchangSimulationWorldStableIds.전술명령확정규칙
            && value.InputContractKey == nameof(SimulationTacticalOrderConfirmRequest));
    }

    [Fact]
    public void 파생Db의_물리테이블과열이름은_한국어로정의한다()
    {
        using var db = CreateDb();

        AssertColumns<SimulationWorld파생RunEntity>(db, "시뮬레이션월드_파생실행",
            "식별번호", "스키마버전", "파생실행고유식별자", "영역묶음고유식별자",
            "생성조리법개정번호", "관계규칙개정번호", "시각자산대장개정번호", "배치시드",
            "입력지문SHA256", "출력해시SHA256", "생성시각UTC", "저장시각UTC");
        AssertColumns<SimulationWorld원본계보Entity>(db, "시뮬레이션월드_원본계보",
            "식별번호", "파생실행식별번호", "원본계보고유식별자", "원본DB코드",
            "자료코드", "원본개정번호", "원본SHA256", "자료기준시각UTC");
        AssertColumns<SimulationWorld파생NodeEntity>(db, "시뮬레이션월드_파생노드",
            "식별번호", "파생실행식별번호", "노드고유식별자", "노드종류코드",
            "원본계보고유식별자", "원본레코드고유식별자", "근거종류코드", "행정구역코드", "타일키",
            "영역고유식별자", "표시이름", "대표군코드", "대표원본건수", "대표순위");
        AssertColumns<SimulationWorld파생RelationEntity>(db, "시뮬레이션월드_파생관계",
            "식별번호", "파생실행식별번호", "관계고유식별자", "시작노드고유식별자",
            "관계코드", "도착노드고유식별자", "근거종류코드", "원본계보고유식별자", "신뢰도");
        AssertColumns<SimulationWorld건물배치Entity>(db, "시뮬레이션월드_건물배치계획",
            "식별번호", "파생실행식별번호", "건물배치고유식별자", "영역노드고유식별자",
            "건물노드고유식별자", "배치근거코드", "근거종류코드", "건물분류코드",
            "시각Family코드", "표현층수", "건물바닥면적제곱미터", "높이미터",
            "위치X", "위치Y", "위치Z", "Y축회전", "표현전용여부");
        AssertColumns<SimulationWorld그래픽표현Entity>(db, "시뮬레이션월드_그래픽표현계획",
            "식별번호", "파생실행식별번호", "그래픽표현고유식별자", "대상노드고유식별자",
            "표현범위코드", "질감세트키", "재질변형키", "색조팔레트키", "배경Profile키",
            "조명Profile키", "시간대Profile키", "그림자정책코드", "그림자투사여부",
            "그림자수신여부", "접지그림자강도", "그림자거리미터", "주변광차폐강도",
            "세부표현단계코드", "품질단계코드", "표현전용여부");
        AssertColumns<SimulationWorldUnity공간변환Entity>(db, "시뮬레이션월드_Unity공간변환Profile",
            "식별번호", "파생실행식별번호", "공간변환고유식별자", "영역묶음고유식별자",
            "원본좌표계코드", "좌표축변환코드", "Unity원점동쪽좌표미터", "Unity원점북쪽좌표미터",
            "기준표고미터", "수평축척률", "높이과장률", "Unity단위당미터", "변환규칙개정번호",
            "변환상태코드", "변환ProfileSHA256");
        AssertColumns<SimulationWorldUnity타일ManifestEntity>(db, "시뮬레이션월드_Unity타일Manifest",
            "식별번호", "파생실행식별번호", "타일Manifest고유식별자", "공간변환고유식별자",
            "타일키", "타일단계", "타일크기미터", "여유영역미터", "최소동쪽좌표미터",
            "최소북쪽좌표미터", "최대동쪽좌표미터", "최대북쪽좌표미터", "입력지문SHA256",
            "ManifestSHA256", "생성상태코드");
        AssertColumns<SimulationWorldUnity산출물Entity>(db, "시뮬레이션월드_Unity산출물",
            "식별번호", "파생실행식별번호", "산출물고유식별자", "타일Manifest고유식별자",
            "산출물종류코드", "세부표현단계코드", "산출물보관객체키", "산출물SHA256",
            "원본개정번호", "원본SHA256", "원본기준일", "수평좌표계코드", "높이기준코드",
            "원본해상도미터", "NoData값", "산출물형식코드", "산출물바이트길이", "표본너비", "표본높이",
            "정점수", "삼각형수", "재질슬롯수", "예상DrawCall수", "경계정점SHA256", "생성상태코드");
        AssertColumns<SimulationWorld시각배치Entity>(db, "시뮬레이션월드_시각배치계획",
            "식별번호", "파생실행식별번호", "시각배치고유식별자", "대상노드고유식별자",
            "시각키", "세부표현단계코드", "위치X", "위치Y", "위치Z", "Y축회전",
            "균일축척", "표현전용여부");
        AssertColumns<SimulationWorldSynty경관RunEntity>(db, "시뮬레이션월드_Synty경관실행",
            "식별번호", "스키마버전", "시각실행고유식별자", "작업고유식별자",
            "공간실행고유식별자", "공간출력SHA256", "영역묶음고유식별자", "작업범위종류코드",
            "작업범위고유식별자", "경관규칙개정번호", "Synty구성대장개정번호",
            "URP표현대장개정번호", "배치시드", "대상플랫폼코드", "품질단계코드",
            "입력지문SHA256", "출력해시SHA256", "생성시각UTC", "저장시각UTC", "작업상태코드");
        AssertColumns<SimulationWorldSynty그래픽표현Entity>(db, "시뮬레이션월드_Synty그래픽표현계획",
            "식별번호", "Synty경관실행식별번호", "그래픽표현고유식별자", "대상노드고유식별자",
            "표현범위코드", "질감세트키", "재질변형키", "색조팔레트키", "배경Profile키",
            "조명Profile키", "시간대Profile키", "그림자정책코드", "그림자투사여부",
            "그림자수신여부", "접지그림자강도", "그림자거리미터", "주변광차폐강도",
            "세부표현단계코드", "품질단계코드", "표현전용여부");
        AssertColumns<SimulationWorldSynty시각배치Entity>(db, "시뮬레이션월드_Synty시각배치계획",
            "식별번호", "Synty경관실행식별번호", "시각배치고유식별자", "대상노드고유식별자",
            "시각키", "세부표현단계코드", "위치X", "위치Y", "위치Z", "Y축회전",
            "균일축척", "표현전용여부");
        AssertColumns<SimulationWorldSynty배치거부Entity>(db, "시뮬레이션월드_Synty배치거부",
            "식별번호", "Synty경관실행식별번호", "배치거부고유식별자", "대상노드고유식별자",
            "거부사유코드", "거부상세");
    }

    [Fact]
    public async Task 파생원장은_원본계보_관계_VisualKey계획을_하나의실행본으로저장한다()
    {
        await using var db = CreateDb();
        await db.Database.EnsureCreatedAsync();
        var store = new SimulationWorld파생원장Store(db);
        var ledger = Fixture();

        var result = await store.저장Async(ledger, CancellationToken.None);

        Assert.True(result.Inserted);
        Assert.Equal(1, result.SourceCount);
        Assert.Equal(3, result.NodeCount);
        Assert.Equal(2, result.RelationCount);
        Assert.Equal(1, result.BuildingPlacementCount);
        Assert.Equal(1, result.GraphicsPlanCount);
        Assert.Equal(1, result.VisualPlacementCount);
        Assert.Equal(1, await db.Runs.CountAsync());
        Assert.Equal(1, await db.Sources.CountAsync());
        Assert.Equal(3, await db.Nodes.CountAsync());
        Assert.Equal(2, await db.Relations.CountAsync());
        var buildingPlacement = await db.BuildingPlacements.SingleAsync();
        Assert.Equal(SimulationWorld건물배치근거Codes.관측대표점, buildingPlacement.PlacementBasisCode);
        Assert.Equal(2, buildingPlacement.FloorCount);
        var graphicsPlan = await db.GraphicsPlans.SingleAsync();
        Assert.Equal("building.rural.warm-earth.v1", graphicsPlan.TextureSetKey);
        Assert.Equal(SimulationWorld그림자정책Codes.혼합, graphicsPlan.ShadowPolicyCode);
        var visual = await db.VisualPlacements.SingleAsync();
        Assert.Equal("logistics.station.lowpoly.v1", visual.VisualKey);
        Assert.True(visual.PresentationOnly);
    }

    [Fact]
    public async Task 같은입력과결과를재저장하면_중복행을만들지않는다()
    {
        await using var db = CreateDb();
        await db.Database.EnsureCreatedAsync();
        var store = new SimulationWorld파생원장Store(db);
        var ledger = Fixture();

        await store.저장Async(ledger, CancellationToken.None);
        db.ChangeTracker.Clear();
        var second = await store.저장Async(ledger, CancellationToken.None);

        Assert.False(second.Inserted);
        Assert.Equal(1, await db.Runs.CountAsync());
        Assert.Equal(3, await db.Nodes.CountAsync());
    }

    [Fact]
    public async Task 같은실행식별자에다른Visual계획은_충돌로거부한다()
    {
        await using var db = CreateDb();
        await db.Database.EnsureCreatedAsync();
        var store = new SimulationWorld파생원장Store(db);
        var ledger = Fixture();
        await store.저장Async(ledger, CancellationToken.None);
        db.ChangeTracker.Clear();
        ledger.VisualPlacements[0].VisualKey = "town.house.lowpoly.v2";

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.저장Async(ledger, CancellationToken.None));

        Assert.Equal(SimulationWorld파생원장Store.ConflictCode, error.Message);
    }

    [Fact]
    public void VisualKey에Prefab경로를넣으면_원장검증이거부한다()
    {
        var ledger = Fixture();
        ledger.VisualPlacements[0].VisualKey = "Assets/Synty/Prefab.prefab";

        var error = Assert.Throws<InvalidOperationException>(() =>
            SimulationWorld파생원장Validator.Validate(ledger));

        Assert.StartsWith(SimulationWorld파생원장Validator.InvalidCode, error.Message);
    }

    [Fact]
    public void 원본레코드식별자는_원본계보없이단독저장할수없다()
    {
        var ledger = Fixture();
        ledger.Nodes[1].SourceStableId = null;

        var error = Assert.Throws<InvalidOperationException>(() =>
            SimulationWorld파생원장Validator.Validate(ledger));

        Assert.Contains("원본 레코드 식별자가 있으면 원본 계보 참조도 필요합니다", error.Message);
    }

    [Fact]
    public void 통계대표건물은_관측도형건물로표시할수없다()
    {
        var ledger = Fixture();
        ledger.BuildingPlacements[0].PlacementBasisCode = "ObservedOrMaybeSynthetic";

        var error = Assert.Throws<InvalidOperationException>(() =>
            SimulationWorld파생원장Validator.Validate(ledger));

        Assert.Contains("지원하지 않는 건물 배치 근거입니다", error.Message);
    }

    [Fact]
    public void 그래픽표현키에는_자산파일경로를저장할수없다()
    {
        var ledger = Fixture();
        ledger.GraphicsPlans[0].TextureSetKey = "Assets/Synty/texture.png";

        var error = Assert.Throws<InvalidOperationException>(() =>
            SimulationWorld파생원장Validator.Validate(ledger));

        Assert.Contains("자산 파일 경로를 저장할 수 없습니다", error.Message);
    }

    [Fact]
    public void 그래픽효과강도는_정규화범위를벗어날수없다()
    {
        var ledger = Fixture();
        ledger.GraphicsPlans[0].ContactShadowStrength = 1.5m;

        var error = Assert.Throws<InvalidOperationException>(() =>
            SimulationWorld파생원장Validator.Validate(ledger));

        Assert.Contains("접지 그림자 강도는 0~1이어야 합니다", error.Message);
    }

    [Fact]
    public void Unity변환가능Profile은_원점과기준표고가필요하다()
    {
        var ledger = Fixture();
        ledger.UnityTransformProfiles = new[]
        {
            new SimulationWorldUnity공간변환Profile
            {
                StableId = "unity-transform:test",
                AreaSetStableId = ledger.AreaSetStableId,
                SourceCrsCode = "EPSG:5186",
                AxisMappingCode = "EastingToX-NorthingToZ-ElevationToY",
                HorizontalScale = 1m,
                VerticalExaggeration = 1m,
                MetersPerUnityUnit = 1m,
                RuleRevision = "transform-v1",
                StatusCode = SimulationWorldUnity변환상태Codes.변환가능,
                ProfileHashSha256 = Hash('d'),
            },
        };

        var error = Assert.Throws<InvalidOperationException>(() =>
            SimulationWorld파생원장Validator.Validate(ledger));

        Assert.Contains("Unity 원점 좌표가 필요합니다", error.Message);
    }

    [Fact]
    public void 완료Unity산출물은_보관객체키와Hash가필요하다()
    {
        var ledger = Fixture();
        ledger.UnityTransformProfiles = new[]
        {
            new SimulationWorldUnity공간변환Profile
            {
                StableId = "unity-transform:test",
                AreaSetStableId = ledger.AreaSetStableId,
                SourceCrsCode = "EPSG:5186",
                AxisMappingCode = "EastingToX-NorthingToZ-ElevationToY",
                OriginEastingMeters = 1m,
                OriginNorthingMeters = 1m,
                ReferenceElevationMeters = 1m,
                HorizontalScale = 1m,
                VerticalExaggeration = 1m,
                MetersPerUnityUnit = 1m,
                RuleRevision = "transform-v1",
                StatusCode = SimulationWorldUnity변환상태Codes.변환가능,
                ProfileHashSha256 = Hash('d'),
            },
        };
        ledger.UnityTileManifests = new[]
        {
            new SimulationWorldUnity타일Manifest
            {
                StableId = "unity-tile:test",
                TransformProfileStableId = "unity-transform:test",
                TileKey = "kr5186:l2:1:1",
                Level = 2,
                SizeMeters = 500m,
                HaloMeters = 60m,
                MinEastingMeters = 500m,
                MinNorthingMeters = 500m,
                MaxEastingMeters = 1000m,
                MaxNorthingMeters = 1000m,
                InputFingerprintSha256 = Hash('e'),
                ManifestHashSha256 = Hash('f'),
                StatusCode = SimulationWorldUnity변환상태Codes.변환가능,
            },
        };
        ledger.UnityArtifacts = new[]
        {
            new SimulationWorldUnity산출물
            {
                StableId = "unity-artifact:test",
                TileManifestStableId = "unity-tile:test",
                ArtifactKindCode = "TerrainMesh",
                LodCode = "L2",
                StatusCode = SimulationWorldUnity산출물상태Codes.완료,
            },
        };

        var error = Assert.Throws<InvalidOperationException>(() =>
            SimulationWorld파생원장Validator.Validate(ledger));

        Assert.Contains("완료 Unity 산출물 저장 객체 키", error.Message);
    }

    [Fact]
    public async Task 운영공공데이터Context는_Simulation연결에서도_저장을거부한다()
    {
        var options = new DbContextOptionsBuilder<PublicDataIngestionDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .AddInterceptors(new SimulationSharedPublicDataReadOnlySaveChangesInterceptor())
            .Options;
        await using var context = new PublicDataIngestionDbContext(options);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            context.SaveChangesAsync());

        Assert.Equal(SimulationSharedPublicDataReadOnlySaveChangesInterceptor.ErrorCode, error.Message);
    }

    [Fact]
    public void 파생Db가활성화됐는데연결문자열이없으면_서버구성을거부한다()
    {
        var configuration = Configuration(new Dictionary<string, string?>
        {
            ["SimulationSharedPublicData:Enabled"] = "false",
            ["SimulationWorldDerivationDatabase:Enabled"] = "true",
            ["SimulationWorldDerivationDatabase:ConnectionStringName"] = "SimulationWorldDerived",
        });

        var error = Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddSsalddelSimulationModule(configuration));

        Assert.Equal(
            SsalddelSimulationHostingServiceCollectionExtensions.WorldDerivationConnectionStringMissingErrorCode,
            error.Message);
    }

    [Fact]
    public void 파생Db연결이명시되면_별도DbContext와Store를등록한다()
    {
        var configuration = Configuration(new Dictionary<string, string?>
        {
            ["SimulationSharedPublicData:Enabled"] = "false",
            ["SimulationWorldDerivationDatabase:Enabled"] = "true",
            ["SimulationWorldDerivationDatabase:ConnectionStringName"] = "SimulationWorldDerived",
            ["ConnectionStrings:SimulationWorldDerived"] =
                "Server=localhost;Database=simulation_world;User=test;Password=test;",
        });
        var services = new ServiceCollection();

        services.AddSsalddelSimulationModule(configuration);

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(ISimulationWorld파생원장Store)
            && descriptor.ImplementationType == typeof(SimulationWorld파생원장Store));
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(ISimulationWorld지역ProjectionReader)
            && descriptor.ImplementationType == typeof(SimulationWorld지역ProjectionReader));
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(ISimulationWorld공간실행Reader)
            && descriptor.ImplementationType == typeof(SimulationWorld공간실행Reader));
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(ISimulationWorldSynty경관Store)
            && descriptor.ImplementationType == typeof(SimulationWorldSynty경관Store));
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(ISimulationWorldSynty경관Planner)
            && descriptor.ImplementationType == typeof(SimulationWorld기본Synty경관Planner));
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(SimulationWorldSynty경관JobShell));
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(DbContextOptions<SimulationWorld파생DbContext>));
    }

    [Fact]
    public async Task 평창군공간Pipeline은_원본이없으면_자료부족만저장하고_그래픽을만들지않는다()
    {
        await using var publicDb = CreatePublicDataDb();
        await using var derivedDb = CreateDb();
        await publicDb.Database.EnsureCreatedAsync();
        await derivedDb.Database.EnsureCreatedAsync();
        var pipeline = new 평창군공간파생Pipeline(
            publicDb,
            new SimulationWorld파생원장Store(derivedDb));

        var result = await pipeline.실행Async(CancellationToken.None);

        Assert.Equal(평창군공간파생Pipeline.자료부족, result.상태코드);
        Assert.Equal(0, result.건축물수);
        Assert.Equal(1, await derivedDb.Nodes.CountAsync(item => item.NodeKindCode == "DataGap"));
        Assert.Equal(1, await derivedDb.Nodes.CountAsync(item =>
            item.NodeKindCode == "LandscapeCompletionArea"
            && item.StableId == PyeongchangSimulationWorldStableIds.대관령Farm경관완결영역));
        Assert.Equal(1, await derivedDb.Relations.CountAsync(item =>
            item.RelationCode == "ContainsLandscapeCompletionArea"
            && item.ToNodeStableId == PyeongchangSimulationWorldStableIds.대관령Farm경관완결영역));
        Assert.Equal(0, await derivedDb.GraphicsPlans.CountAsync());
        Assert.Null((await derivedDb.Runs.SingleAsync()).VisualCatalogRevision);
        Assert.Equal(1, result.Unity공간변환Profile수);
        Assert.Equal(1, await derivedDb.UnityTransformProfiles.CountAsync(item =>
            item.StatusCode == SimulationWorldUnity변환상태Codes.타일Manifest대기));
        Assert.Equal(0, result.Unity타일Manifest수);
        Assert.Equal(0, result.Unity산출물수);
        Assert.Equal(0, await derivedDb.BuildingPlacements.CountAsync());
    }

    [Fact]
    public async Task 평창군Pipeline은_타일Manifest를_공간변환원장과함께저장하고_재실행한다()
    {
        await using var publicDb = CreatePublicDataDb();
        await using var derivedDb = CreateDb();
        await publicDb.Database.EnsureCreatedAsync();
        await derivedDb.Database.EnsureCreatedAsync();
        var pipeline = new 평창군공간파생Pipeline(
            publicDb,
            new SimulationWorld파생원장Store(derivedDb));
        var path = Path.Combine(Path.GetTempPath(), $"pyeongchang-tile-manifest-{Guid.NewGuid():N}.json");
        var fingerprint = new string('a', 64);
        var rasterHash = new string('b', 64);
        await File.WriteAllTextAsync(path, $$"""
            {
              "schemaVersion": "pyeongchang-spatial-tile-manifest.v1",
              "generatedAt": "2026-08-13T00:00:00+00:00",
              "source": { "sha256": "{{rasterHash}}" },
              "tiles": [
                {
                  "tileKey": "kr5186:l2:400:1100",
                  "level": 2,
                  "sizeMeters": 500,
                  "haloMeters": 60,
                  "coreBounds": {
                    "minEasting": 200000,
                    "minNorthing": 550000,
                    "maxEasting": 200500,
                    "maxNorthing": 550500
                  },
                  "fingerprint": "{{fingerprint}}"
                }
              ]
            }
            """);

        try
        {
            var first = await pipeline.실행Async(path, CancellationToken.None);
            derivedDb.ChangeTracker.Clear();
            var second = await pipeline.실행Async(path, CancellationToken.None);

            Assert.True(first.새실행본저장여부);
            Assert.False(second.새실행본저장여부);
            Assert.Equal(first.파생실행고유식별자, second.파생실행고유식별자);
            Assert.Equal(1, first.Unity타일Manifest수);
            Assert.Equal(1, await derivedDb.UnityTileManifests.CountAsync());
            var transform = await derivedDb.UnityTransformProfiles.SingleAsync();
            Assert.Equal(SimulationWorldUnity변환상태Codes.자료부족, transform.StatusCode);
            Assert.Equal(200000m, transform.OriginEastingMeters);
            Assert.Null(transform.ReferenceElevationMeters);
            Assert.Equal(3, await derivedDb.Sources.CountAsync());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task 평창군Pipeline은_중앙L2공간산출물과_물리표고기준을_파생Db에저장한다()
    {
        await using var publicDb = CreatePublicDataDb();
        await using var derivedDb = CreateDb();
        await publicDb.Database.EnsureCreatedAsync();
        await derivedDb.Database.EnsureCreatedAsync();
        var pipeline = new 평창군공간파생Pipeline(
            publicDb,
            new SimulationWorld파생원장Store(derivedDb));
        var suffix = Guid.NewGuid().ToString("N");
        var tilePath = Path.Combine(Path.GetTempPath(), $"pyeongchang-tile-{suffix}.json");
        var artifactPath = Path.Combine(Path.GetTempPath(), $"pyeongchang-artifact-{suffix}.json");
        await File.WriteAllTextAsync(tilePath, $$"""
            {
              "schemaVersion": "pyeongchang-spatial-tile-manifest.v1",
              "generatedAt": "2026-08-13T00:00:00+00:00",
              "source": { "sha256": "{{new string('b', 64)}}" },
              "tiles": [{
                "tileKey": "kr5186:l2:700:1145", "level": 2,
                "sizeMeters": 500, "haloMeters": 60,
                "coreBounds": { "minEasting": 350000, "minNorthing": 572500,
                  "maxEasting": 350500, "maxNorthing": 573000 },
                "fingerprint": "{{new string('a', 64)}}"
              }]
            }
            """);
        await File.WriteAllTextAsync(artifactPath, $$"""
            {
              "schemaVersion": "ssalddel-spatial-layer-artifacts.v1",
              "ruleRevision": "daegwallyeong-l2-physical-spatial.r1",
              "tileKey": "kr5186:l2:700:1145",
              "coordinateReferenceSystem": "EPSG:5186",
              "sampleSpacingMeters": 10,
              "coreBounds": { "minEasting": 350000, "minNorthing": 572500 },
              "statistics": { "minimumPhysicalElevationMeters": 905.4617 },
              "sources": {
                "elevation": { "sourceRevision": "Copernicus-DEM-GLO30-N37E128",
                  "sha256": "{{new string('c', 64)}}", "horizontalCrs": "EPSG:5186",
                  "verticalDatum": "Unverified", "resolutionMeters": 30,
                  "noDataValue": -32767, "sourceReferenceDate": null },
                "landCover": { "sourceRevision": "ESA-WorldCover-2021-v200-N36E126",
                  "sha256": "{{new string('d', 64)}}", "horizontalCrs": "EPSG:5186",
                  "resolutionMeters": 10, "noDataValue": 0, "sourceReferenceDate": "2021" }
              },
              "artifacts": {
                "elevation": { "relativePath": "generated/elevation.bin", "sha256": "{{new string('e', 64)}}",
                  "formatCode": "height-f32-v1", "byteLength": 15876, "width": 63, "height": 63 },
                "landCover": { "relativePath": "generated/land-cover.bin", "sha256": "{{new string('f', 64)}}",
                  "formatCode": "landcover-u8-v1", "byteLength": 3844, "width": 62, "height": 62 },
                "placementMask": { "relativePath": "generated/placement-mask.bin", "sha256": "{{new string('1', 64)}}",
                  "formatCode": "placement-mask-u8-v1", "byteLength": 3844, "width": 62, "height": 62 }
              }
            }
            """);

        try
        {
            var result = await pipeline.실행Async(tilePath, artifactPath, CancellationToken.None);

            Assert.Equal(3, result.Unity산출물수);
            Assert.Equal(3, await derivedDb.UnityArtifacts.CountAsync());
            var elevation = await derivedDb.UnityArtifacts.SingleAsync(
                item => item.ArtifactKindCode == "elevation");
            Assert.Equal("height-f32-v1", elevation.ArtifactFormatCode);
            Assert.Equal("Unverified", elevation.VerticalDatumCode);
            Assert.Equal(63, elevation.SampleWidth);
            var reader = new SimulationWorldTileArtifactReader(derivedDb);
            Assert.True(reader.TryRead("kr5186:l2:700:1145", "elevation", out var snapshot));
            Assert.Equal(elevation.ArtifactHashSha256, snapshot.ArtifactHashSha256);
            Assert.Equal("EPSG:5186", snapshot.HorizontalCrsCode);
            var transform = await derivedDb.UnityTransformProfiles.SingleAsync();
            Assert.Equal(SimulationWorldUnity변환상태Codes.변환가능, transform.StatusCode);
            Assert.Equal(905.4617m, transform.ReferenceElevationMeters);
            Assert.Equal(350000m, transform.OriginEastingMeters);
        }
        finally
        {
            File.Delete(tilePath);
            File.Delete(artifactPath);
        }
    }

    [Fact]
    public async Task 평창군Pipeline은_건물과공개사업장을_원본레코드관계로저장한다()
    {
        await using var publicDb = CreatePublicDataDb();
        await using var derivedDb = CreateDb();
        await publicDb.Database.EnsureCreatedAsync();
        await derivedDb.Database.EnsureCreatedAsync();
        var buildingId = Guid.NewGuid();
        var businessId = Guid.NewGuid();
        publicDb.BuildingRegisterTitles.Add(new 건축물대장표제부Record
        {
            Id = buildingId,
            RegisterManagementPk = "pyeongchang-building-1",
            RegisterKindCode = "title",
            SigunguCode = "51760",
            LegalDongCode = "36000",
            BuildingName = "진부 물류 건물",
            SourceRevision = "building-2026-08",
            EvidenceSnapshotId = 1,
            ObservedAtUtc = DateTimeOffset.Parse("2026-08-13T00:00:00Z"),
        });
        var business = new 공개인허가사업장Record
        {
            Id = businessId,
            SourceId = "localdata",
            SourceDatasetId = "licensed-business",
            OpenServiceId = "service-1",
            ManagementNumber = "management-1",
            BusinessName = "진부 공개 상호",
            SourceRevision = "business-2026-08",
            SourceHashSha256 = Hash('c'),
            ObservedAtUtc = DateTimeOffset.Parse("2026-08-13T00:00:00Z"),
        };
        publicDb.공개인허가사업장Records.Add(business);
        publicDb.공개사업장건축물Assignments.Add(new 공개사업장건축물Assignment
        {
            Id = Guid.NewGuid(),
            BusinessRecordId = businessId,
            BusinessRecord = business,
            BuildingRecordId = buildingId,
            AssignmentStatusCode = 공개사업장연결상태Codes.연결됨,
            AssignmentMethodCode = 공개사업장연결방법Codes.정확한정규화도로명주소,
            ConfidenceCode = "DerivedHigh",
            CandidateBuildingCount = 1,
            RuleRevision = "match-v1",
            EvaluatedAtUtc = DateTimeOffset.Parse("2026-08-13T00:00:00Z"),
        });
        await publicDb.SaveChangesAsync();
        var pipeline = new 평창군공간파생Pipeline(
            publicDb,
            new SimulationWorld파생원장Store(derivedDb));

        var result = await pipeline.실행Async(CancellationToken.None);

        Assert.Equal(평창군공간파생Pipeline.완료, result.상태코드);
        Assert.Equal(1, result.건축물수);
        Assert.Equal(1, result.공개사업장수);
        Assert.Equal(1, await derivedDb.Relations.CountAsync(item =>
            item.RelationCode == "HostsPublicLicensedBusiness"));
        Assert.Equal(2, await derivedDb.Nodes.CountAsync(item => item.SourceRecordStableId != null));
        Assert.Equal(0, await derivedDb.BuildingPlacements.CountAsync());
    }

    [Fact]
    public async Task 공간Pipeline과_SyntyJobShell은_독립실행본과Hash를저장한다()
    {
        await using var publicDb = CreatePublicDataDb();
        await using var derivedDb = CreateDb();
        await publicDb.Database.EnsureCreatedAsync();
        await derivedDb.Database.EnsureCreatedAsync();
        var spatialPipeline = new 평창군공간파생Pipeline(
            publicDb,
            new SimulationWorld파생원장Store(derivedDb));

        var spatial = await spatialPipeline.실행Async(CancellationToken.None);

        var spatialRun = await derivedDb.Runs.SingleAsync();
        Assert.Equal(2, spatialRun.SchemaVersion);
        Assert.Null(spatialRun.VisualCatalogRevision);
        Assert.Equal(0, await derivedDb.GraphicsPlans.CountAsync());
        Assert.Equal(0, await derivedDb.VisualPlacements.CountAsync());
        var shell = new SimulationWorldSynty경관JobShell(
            new SimulationWorld공간실행Reader(derivedDb),
            new SimulationWorld기본Synty경관Planner(),
            new SimulationWorldSynty경관Store(derivedDb));
        var request = SyntyRequest(spatial);

        var first = await shell.실행Async(request, CancellationToken.None);
        derivedDb.ChangeTracker.Clear();
        var second = await shell.실행Async(request, CancellationToken.None);

        Assert.True(first.Inserted);
        Assert.False(second.Inserted);
        Assert.Equal(first.VisualBuildStableId, second.VisualBuildStableId);
        Assert.Equal(SimulationWorldSynty작업상태Codes.일부완료, first.StatusCode);
        Assert.Equal(3, first.GraphicsPlanCount);
        Assert.Equal(0, first.VisualPlacementCount);
        Assert.Equal(3, first.RejectionCount);
        Assert.Equal(1, await derivedDb.Runs.CountAsync());
        Assert.Equal(1, await derivedDb.SyntyLandscapeRuns.CountAsync());
        Assert.Equal(3, await derivedDb.SyntyGraphicsPlans.CountAsync());
        Assert.Equal(3, await derivedDb.SyntyRejections.CountAsync());
        Assert.Equal(spatial.출력해시SHA256, (await derivedDb.Runs.SingleAsync()).OutputHashSha256);
    }

    [Fact]
    public async Task Synty대장개정은_공간실행을재생성하지않고_새시각실행만만든다()
    {
        await using var publicDb = CreatePublicDataDb();
        await using var derivedDb = CreateDb();
        await publicDb.Database.EnsureCreatedAsync();
        await derivedDb.Database.EnsureCreatedAsync();
        var spatialPipeline = new 평창군공간파생Pipeline(
            publicDb,
            new SimulationWorld파생원장Store(derivedDb));
        var spatial = await spatialPipeline.실행Async(CancellationToken.None);
        var shell = new SimulationWorldSynty경관JobShell(
            new SimulationWorld공간실행Reader(derivedDb),
            new SimulationWorld기본Synty경관Planner(),
            new SimulationWorldSynty경관Store(derivedDb));
        var firstRequest = SyntyRequest(spatial);
        await shell.실행Async(firstRequest, CancellationToken.None);
        derivedDb.ChangeTracker.Clear();
        var secondRequest = SyntyRequest(spatial);
        secondRequest.JobStableId = "synty-job:test:pyeongchang-v2";
        secondRequest.VisualCatalogRevision = "synty-world-catalog.v2";

        var second = await shell.실행Async(secondRequest, CancellationToken.None);

        Assert.True(second.Inserted);
        Assert.Equal(1, await derivedDb.Runs.CountAsync());
        Assert.Equal(2, await derivedDb.SyntyLandscapeRuns.CountAsync());
    }

    [Fact]
    public async Task SyntyJob은_요청한공간Hash가저장본과다르면거부한다()
    {
        await using var publicDb = CreatePublicDataDb();
        await using var derivedDb = CreateDb();
        await publicDb.Database.EnsureCreatedAsync();
        await derivedDb.Database.EnsureCreatedAsync();
        var spatialPipeline = new 평창군공간파생Pipeline(
            publicDb,
            new SimulationWorld파생원장Store(derivedDb));
        var spatial = await spatialPipeline.실행Async(CancellationToken.None);
        var shell = new SimulationWorldSynty경관JobShell(
            new SimulationWorld공간실행Reader(derivedDb),
            new SimulationWorld기본Synty경관Planner(),
            new SimulationWorldSynty경관Store(derivedDb));
        var request = SyntyRequest(spatial);
        request.SpatialOutputHashSha256 = Hash('f');

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            shell.실행Async(request, CancellationToken.None));

        Assert.Equal(SimulationWorldSynty경관JobShell.SpatialOutputMismatchCode, error.Message);
        Assert.Equal(0, await derivedDb.SyntyLandscapeRuns.CountAsync());
    }

    private static SimulationWorld파생DbContext CreateDb() => new(
        new DbContextOptionsBuilder<SimulationWorld파생DbContext>()
            .UseInMemoryDatabase("simulation-world-derivation-" + Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(warnings => warnings.Ignore(
                InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    private static PublicDataIngestionDbContext CreatePublicDataDb() => new(
        new DbContextOptionsBuilder<PublicDataIngestionDbContext>()
            .UseInMemoryDatabase("simulation-public-data-" + Guid.NewGuid().ToString("N"))
            .Options);

    private static void AssertColumns<TEntity>(
        SimulationWorld파생DbContext db,
        string expectedTable,
        params string[] expectedColumns)
    {
        var entity = db.Model.FindEntityType(typeof(TEntity));
        Assert.NotNull(entity);
        Assert.Equal(expectedTable, entity.GetTableName());
        var table = StoreObjectIdentifier.Table(expectedTable, entity.GetSchema());
        var actualColumns = entity.GetProperties()
            .Select(property => property.GetColumnName(table))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(
            expectedColumns.OrderBy(name => name, StringComparer.Ordinal),
            actualColumns);
    }

    private static SimulationWorld파생원장 Fixture() => new()
    {
        SchemaVersion = 1,
        BuildStableId = "world-build:pyeongchang-v1",
        AreaSetStableId = "pyeongchang-farm-hub-town-v1",
        RecipeRevision = "world-recipe.r1",
        RuleRevision = "world-relation.r1",
        VisualCatalogRevision = "synty-catalog.r1",
        Seed = 51760,
        InputFingerprintSha256 = Hash('a'),
        GeneratedAtUtc = DateTimeOffset.Parse("2026-08-13T00:00:00Z"),
        Sources = new[]
        {
            new SimulationWorld원본계보
            {
                SourceStableId = "source:public-data:building-v1",
                SourceDatabaseCode = "SharedPublicData",
                DatasetCode = "building-register",
                SourceRevision = "building-v1",
                SourceHashSha256 = Hash('b'),
            },
        },
        Nodes = new[]
        {
            new SimulationWorld파생Node
            {
                StableId = "area:jinbu-hub",
                NodeKindCode = "Area",
                EvidenceKindCode = SimulationWorld근거종류Codes.시나리오,
                RegionCode = "5176036000",
            },
            new SimulationWorld파생Node
            {
                StableId = "building:hub-001",
                NodeKindCode = "Building",
                SourceStableId = "source:public-data:building-v1",
                SourceRecordStableId = "building-record:hub-001",
                EvidenceKindCode = SimulationWorld근거종류Codes.파생,
                AreaStableId = "area:jinbu-hub",
            },
            new SimulationWorld파생Node
            {
                StableId = "business:hub-market-001",
                NodeKindCode = "PublicLicensedBusiness",
                SourceStableId = "source:public-data:building-v1",
                SourceRecordStableId = "licensed-business:hub-market-001",
                EvidenceKindCode = SimulationWorld근거종류Codes.관측,
                AreaStableId = "area:jinbu-hub",
                DisplayName = "진부 공개상호",
            },
        },
        Relations = new[]
        {
            new SimulationWorld파생Relation
            {
                StableId = "relation:hub-contains-building-001",
                FromNodeStableId = "area:jinbu-hub",
                RelationCode = "Contains",
                ToNodeStableId = "building:hub-001",
                EvidenceKindCode = SimulationWorld근거종류Codes.파생,
                SourceStableId = "source:public-data:building-v1",
                Confidence = 0.95m,
            },
            new SimulationWorld파생Relation
            {
                StableId = "relation:building-hosts-business-001",
                FromNodeStableId = "building:hub-001",
                RelationCode = "HostsPublicLicensedBusiness",
                ToNodeStableId = "business:hub-market-001",
                EvidenceKindCode = SimulationWorld근거종류Codes.파생,
                SourceStableId = "source:public-data:building-v1",
                Confidence = 0.9m,
            },
        },
        BuildingPlacements = new[]
        {
            new SimulationWorld건물배치계획
            {
                StableId = "building-placement:hub-001",
                AreaNodeStableId = "area:jinbu-hub",
                BuildingNodeStableId = "building:hub-001",
                PlacementBasisCode = SimulationWorld건물배치근거Codes.관측대표점,
                EvidenceKindCode = SimulationWorld근거종류Codes.파생,
                BuildingCategoryCode = "logistics",
                VisualFamilyCode = "logistics.station",
                FloorCount = 2,
                FootprintAreaSquareMeters = 480m,
                HeightMeters = 9m,
                PositionX = 10m,
                PositionY = 2m,
                PositionZ = 30m,
                RotationY = 90m,
                PresentationOnly = true,
            },
        },
        GraphicsPlans = new[]
        {
            new SimulationWorld그래픽표현계획
            {
                StableId = "graphics:hub-building-001",
                TargetNodeStableId = "building:hub-001",
                PresentationScopeCode = "BuildingExterior",
                TextureSetKey = "building.rural.warm-earth.v1",
                MaterialVariantKey = "building.hub.concrete-orange.v1",
                ColorPaletteKey = "palette.hub.concrete-orange.v1",
                BackgroundProfileKey = "background.jinbu.forest-buffer.v1",
                LightingProfileKey = "lighting.pyeongchang.day.v1",
                TimeOfDayProfileKey = "timeofday.shared.day.v1",
                ShadowPolicyCode = SimulationWorld그림자정책Codes.혼합,
                CastShadows = true,
                ReceiveShadows = true,
                ContactShadowStrength = 0.65m,
                ShadowDistanceMeters = 120m,
                AmbientOcclusionStrength = 0.35m,
                LodCode = "L2",
                QualityTierCode = "PC-High",
                PresentationOnly = true,
            },
        },
        VisualPlacements = new[]
        {
            new SimulationWorld시각배치계획
            {
                StableId = "visual:hub-station-001",
                TargetNodeStableId = "building:hub-001",
                VisualKey = "logistics.station.lowpoly.v1",
                LodCode = "L2",
                PositionX = 10m,
                PositionY = 2m,
                PositionZ = 30m,
                RotationY = 90m,
                UniformScale = 1m,
                PresentationOnly = true,
            },
        },
    };

    private static SimulationWorldSynty경관Job요청 SyntyRequest(
        평창군공간파생PipelineResult spatial) => new()
        {
            JobStableId = "synty-job:test:pyeongchang-v1",
            SpatialBuildStableId = spatial.파생실행고유식별자,
            SpatialOutputHashSha256 = spatial.출력해시SHA256,
            AreaSetStableId = "area-set:sim:pyeongchang:farm-hub-town.v1",
            ScopeKindCode = SimulationWorldSynty범위Codes.영역묶음,
            ScopeStableId = "area-set:sim:pyeongchang:farm-hub-town.v1",
            LandscapeRuleRevision = "pyeongchang-synty-landscape.v1",
            VisualCatalogRevision = "synty-world-catalog.v1",
            UrpProfileCatalogRevision = "urp-world-profile.v1",
            Seed = 51760,
            TargetPlatformCode = SimulationWorldSynty대상플랫폼Codes.PC,
            QualityTierCode = "PC-High",
        };

    private static string Hash(char value) => new(value, 64);

    private static IConfiguration Configuration(
        IReadOnlyDictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();
}
