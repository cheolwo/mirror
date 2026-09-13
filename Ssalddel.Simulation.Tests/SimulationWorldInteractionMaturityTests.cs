using System.Text.Json;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Simulation.Application;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;

namespace Ssalddel.Simulation.Tests;

[SsalddelEvidenceResponsibility(
    SsalddelEvidenceStage.E3,
    "WI 발생원과 E4 실행 문맥·E5 세계 발현 판정의 회귀를 검증한다.",
    Boundary = "결정적 계약 시험이며 실제 E5·E7 실행 증거를 대신하지 않는다.",
    SubmoduleKey = SsalddelEvidenceSubmoduleKeys.E3계약회귀)]
public sealed class SimulationWorldInteractionMaturityTests
{
    private readonly SimulationWorldInteractionMaturityService service = new();

    [Fact]
    public void E1_E2_E3은_공간실행을포함한_하위Module의책임을보존한다()
    {
        var definitions = SsalddelEvidenceSubmoduleDefinitionCatalog.All;

        // 개수만 갱신하지 않고 Q338에서 추가한 공간 실행을 포함해 각 책임을 고정한다.
        var 기대책임 = new[]
        {
            (SsalddelEvidenceSubmoduleKeys.E1세션권위계약, SsalddelEvidenceStage.E1),
            (SsalddelEvidenceSubmoduleKeys.E1세계상호작용계약, SsalddelEvidenceStage.E1),
            (SsalddelEvidenceSubmoduleKeys.E1공간계약, SsalddelEvidenceStage.E1),
            (SsalddelEvidenceSubmoduleKeys.E1저장재생계약, SsalddelEvidenceStage.E1),
            (SsalddelEvidenceSubmoduleKeys.E1전투위협계약, SsalddelEvidenceStage.E1),
            (SsalddelEvidenceSubmoduleKeys.E2세션실행, SsalddelEvidenceStage.E2),
            (SsalddelEvidenceSubmoduleKeys.E2세계상호작용실행, SsalddelEvidenceStage.E2),
            (SsalddelEvidenceSubmoduleKeys.E2공간실행, SsalddelEvidenceStage.E2),
            (SsalddelEvidenceSubmoduleKeys.E2로컬권위Adapter, SsalddelEvidenceStage.E2),
            (SsalddelEvidenceSubmoduleKeys.E2원격HostAdapter, SsalddelEvidenceStage.E2),
            (SsalddelEvidenceSubmoduleKeys.E2Unity권위Client, SsalddelEvidenceStage.E2),
            (SsalddelEvidenceSubmoduleKeys.E3계약회귀, SsalddelEvidenceStage.E3),
            (SsalddelEvidenceSubmoduleKeys.E3결정성검증, SsalddelEvidenceStage.E3),
            (SsalddelEvidenceSubmoduleKeys.E3저장재생검증, SsalddelEvidenceStage.E3),
            (SsalddelEvidenceSubmoduleKeys.E3로컬원격동등성, SsalddelEvidenceStage.E3),
            (SsalddelEvidenceSubmoduleKeys.E3Unity소비자회귀, SsalddelEvidenceStage.E3),
        };
        Assert.Equal(기대책임.OrderBy(value => value.Item1, StringComparer.Ordinal),
            definitions.Select(value => (value.SubmoduleKey, value.EvidenceStage))
                .OrderBy(value => value.SubmoduleKey, StringComparer.Ordinal));
        Assert.Equal(definitions.Count, definitions.Select(value =>
            value.SubmoduleKey).Distinct(StringComparer.Ordinal).Count());
        Assert.Contains(definitions, value => value.TechnicalName ==
            "E1세계상호작용계약Module");
        Assert.Contains(definitions, value => value.TechnicalName ==
            "E2로컬권위AdapterModule");
        Assert.Contains(definitions, value => value.TechnicalName ==
            "E3저장재생검증Module");

        var aggregate = Assert.Single(
            SsalddelEvidenceResponsibilityReader.Read(
                typeof(경영SimulationSessionAggregate)),
            value => value.Role ==
                SsalddelEvidenceResponsibilityRole.Primary &&
                value.ComponentMethod is null);
        Assert.Equal(SsalddelEvidenceSubmoduleKeys.E1세션권위계약,
            aggregate.SubmoduleKey);
        var localRuntime = Assert.Single(
            SsalddelEvidenceResponsibilityReader.Read(
                typeof(LocalSimulationRuntime)),
            value => value.Role ==
                SsalddelEvidenceResponsibilityRole.Primary &&
                value.ComponentMethod is null);
        Assert.Equal(SsalddelEvidenceSubmoduleKeys.E2로컬권위Adapter,
            localRuntime.SubmoduleKey);
        var 공간실행책임 = Assert.Single(
            SsalddelEvidenceResponsibilityReader.Read(
                typeof(SimulationLhAssetPlanLifecycleService)),
            value => value.Role == SsalddelEvidenceResponsibilityRole.Primary
                && value.ComponentMethod is null);
        Assert.Equal(SsalddelEvidenceStage.E2, 공간실행책임.EvidenceStage);
        Assert.Equal(SsalddelEvidenceSubmoduleKeys.E2공간실행,
            공간실행책임.SubmoduleKey);
    }

    [Fact]
    public void 공통E단계Module은_E1부터E10까지_현재증거주체역할을노출한다()
    {
        var expected = new[]
        {
            (SsalddelEvidenceStage.E1, "E1핵심계약Module", typeof(IE1핵심계약Module)),
            (SsalddelEvidenceStage.E2, "E2실행경계Module", typeof(IE2실행경계Module)),
            (SsalddelEvidenceStage.E3, "E3회귀증거Module", typeof(IE3회귀증거Module)),
            (SsalddelEvidenceStage.E4, "E4실행문맥결속Module", typeof(IE4실행문맥결속Module)),
            (SsalddelEvidenceStage.E5, "E5세계발현Module", typeof(IE5세계발현Module)),
            (SsalddelEvidenceStage.E6, "E6세계정제Module", typeof(IE6세계정제Module)),
            (SsalddelEvidenceStage.E7, "E7플레이경험폐루프Module", typeof(IE7플레이경험폐루프Module)),
            (SsalddelEvidenceStage.E8, "E8개별폐루프안정Module", typeof(IE8개별폐루프안정Module)),
            (SsalddelEvidenceStage.E9, "E9영역조화사람승인Module", typeof(IE9영역조화사람승인Module)),
            (SsalddelEvidenceStage.E10, "E10제한운영검증Module", typeof(IE10제한운영검증Module)),
        };

        Assert.Equal(expected.Select(item => (item.Item1, item.Item2)),
            SsalddelEvidenceStageDefinitionCatalog.All.Select(item =>
                (item.EvidenceStage, item.TechnicalName)));
        Assert.All(expected, item =>
            Assert.Contains(typeof(IE단계Module), item.Item3.GetInterfaces()));

        Assert.Contains(typeof(IE단계Module),
            typeof(IE8영역폐루프조화Module).GetInterfaces());
        Assert.Contains(typeof(IE단계Module),
            typeof(IE9사람통합플레이개선Module).GetInterfaces());
        Assert.Contains(typeof(IE단계Module),
            typeof(IE8생활연속성Module).GetInterfaces());
        Assert.Contains(typeof(IE단계Module),
            typeof(IE9변화봉투Module).GetInterfaces());
        Assert.Equal(new[] { "NPC 생활세계 폐루프", "변화 적응형 세계" },
            SsalddelLegacyEvidenceStageDefinitionCatalog.E8AndE9
                .Select(value => value.KoreanName));
    }

    [Fact]
    public void E단계Handle은_증거모델과주체를함께요구한다()
    {
        var unit = new SsalddelEvidenceStageHandle(
            SsalddelEvidenceModelRevisions.Current,
            SsalddelEvidenceStage.E7,
            SsalddelEvidenceSubjectKind.PlayableUnit);
        var stability = new SsalddelEvidenceStageHandle(
            SsalddelEvidenceModelRevisions.Current,
            SsalddelEvidenceStage.E8,
            SsalddelEvidenceSubjectKind.PlayableUnitStabilityCampaign);

        Assert.Equal(SsalddelEvidenceStage.E7, unit.EvidenceStage);
        Assert.Equal(SsalddelEvidenceSubjectKind.PlayableUnitStabilityCampaign,
            stability.SubjectKind);
        Assert.Throws<ArgumentException>(() => new SsalddelEvidenceStageHandle(
            "", SsalddelEvidenceStage.E8,
            SsalddelEvidenceSubjectKind.PlayableUnitStabilityCampaign));
        Assert.Throws<ArgumentException>(() => new SsalddelEvidenceStageHandle(
            SsalddelEvidenceModelRevisions.Current,
            SsalddelEvidenceStage.E8,
            SsalddelEvidenceSubjectKind.PlayableUnit));
        Assert.Throws<ArgumentException>(() => new SsalddelEvidenceStageHandle(
            SsalddelEvidenceModelRevisions.Current,
            SsalddelEvidenceStage.E9,
            SsalddelEvidenceSubjectKind.HumanPlaytestCampaign));

        var harmony = new SsalddelEvidenceStageHandle(
            SsalddelEvidenceModelRevisions.Current,
            SsalddelEvidenceStage.E9,
            SsalddelEvidenceSubjectKind.AreaHarmonySet);
        var operation = new SsalddelEvidenceStageHandle(
            SsalddelEvidenceModelRevisions.Current,
            SsalddelEvidenceStage.E10,
            SsalddelEvidenceSubjectKind.LimitedOperationWindow);
        Assert.Equal(SsalddelEvidenceSubjectKind.AreaHarmonySet,
            harmony.SubjectKind);
        Assert.Equal(SsalddelEvidenceSubjectKind.LimitedOperationWindow,
            operation.SubjectKind);
    }

    [Fact]
    public void E4는_발생원과실행문맥과필수공간이모두결속되면완료된다()
    {
        var result = service.ReviewE4(new()
        {
            Definition = SpatialHarvestDefinition(),
            BoundTriggerSourceCodes = new[]
            {
                SimulationWorldInteractionTriggerSourceCodes.PlayerDriven,
                SimulationWorldInteractionTriggerSourceCodes.NpcDriven,
            },
            BoundContextCodes = new[]
            {
                SimulationWorldInteractionContextCodes.Initiator,
                SimulationWorldInteractionContextCodes.Actor,
                SimulationWorldInteractionContextCodes.Target,
                SimulationWorldInteractionContextCodes.DataResource,
                SimulationWorldInteractionContextCodes.Time,
                SimulationWorldInteractionContextCodes.Spatial,
            },
            SpatialEvidenceStateCode =
                SimulationWorldInteractionSpatialEvidenceCodes.Bound,
        });

        Assert.Equal(SimulationWorldInteractionMaturityStateCodes.ContextBound,
            result.StateCode);
        Assert.Equal("익은 농작물 수확", result.WorldInteractionName);
        Assert.Equal("농장 생산 · 익은 농작물 수확 (WI-FARM-04)",
            result.WorldInteractionDisplayName);
        Assert.Equal("ActorIntent", result.ResponsibilityKindCode);
        Assert.Equal("HarvestLotCreated", result.PrimaryOutcomeCode);
        Assert.Equal("AtomicBundle",
            result.SingleResponsibilityAssessmentCode);
        Assert.Equal(SimulationWI음양Codes.Yang,
            result.음양분류Code);
        Assert.Equal("Fixed", result.음양판정방식Code);
        Assert.Empty(result.MissingContextCodes);
    }

    [Fact]
    public void E4는_비공간WI에_H결속을강제하지않는다()
    {
        var result = service.ReviewE4(new()
        {
            Definition = NonSpatialWorldDefinition(),
            BoundTriggerSourceCodes = new[]
            {
                SimulationWorldInteractionTriggerSourceCodes.WorldDerived,
            },
            BoundContextCodes = new[]
            {
                SimulationWorldInteractionContextCodes.Initiator,
                SimulationWorldInteractionContextCodes.DataResource,
                SimulationWorldInteractionContextCodes.Time,
            },
            SpatialEvidenceStateCode =
                SimulationWorldInteractionSpatialEvidenceCodes.NotApplicable,
        });

        Assert.Equal(SimulationWorldInteractionMaturityStateCodes.ContextBound,
            result.StateCode);
        Assert.Equal(SimulationWorldInteractionSpatialEvidenceCodes.NotApplicable,
            result.SpatialEvidenceStateCode);
    }

    [Fact]
    public void E4는_공간WI의공간증거누락을_부분결속으로남긴다()
    {
        var definition = SpatialHarvestDefinition();
        var result = service.ReviewE4(new()
        {
            Definition = definition,
            BoundTriggerSourceCodes = new[]
            {
                SimulationWorldInteractionTriggerSourceCodes.PlayerDriven,
            },
            BoundContextCodes = definition.RequiredContextCodes
                .Where(value => value != SimulationWorldInteractionContextCodes.Spatial)
                .ToArray(),
            SpatialEvidenceStateCode =
                SimulationWorldInteractionSpatialEvidenceCodes.RequiredMissing,
        });

        Assert.Equal(
            SimulationWorldInteractionMaturityStateCodes.ContextPartiallyBound,
            result.StateCode);
        Assert.Contains(SimulationWorldInteractionContextCodes.Spatial,
            result.MissingContextCodes);
    }

    [Fact]
    public void E5는_공간조립만으로완료되지않고_결과와후속경로를요구한다()
    {
        var result = service.ReviewE5(new()
        {
            Definition = SpatialHarvestDefinition(),
            E4StateCode = SimulationWorldInteractionMaturityStateCodes.ContextBound,
            Invocation = SimulationWorldInteractionMaturityService.FromPlayer(
                "WI-FARM-04", "player:1", "actor:player:1"),
            SpatialEvidenceStateCode =
                SimulationWorldInteractionSpatialEvidenceCodes.Bound,
        });

        Assert.Equal(
            SimulationWorldInteractionMaturityStateCodes.ManifestationPartial,
            result.StateCode);
        Assert.Contains("AuthorityTransition", result.MissingEvidenceCodes);
        Assert.Contains("SuccessorOrReturnPath", result.MissingEvidenceCodes);
    }

    [Fact]
    public void E5는_비공간WorldDerivedWI도_공간없이발현될수있다()
    {
        var result = service.ReviewE5(new()
        {
            Definition = NonSpatialWorldDefinition(),
            E4StateCode = SimulationWorldInteractionMaturityStateCodes.ContextBound,
            Invocation = SimulationWorldInteractionMaturityService.FromWorldDerived(
                "WI-WORLD-ERA-01", "rule:era-transition.r1",
                "world-state:threat:82"),
            AuthorityTransitionRecorded = true,
            TaskOrEffectRecorded = true,
            ResultStateRecorded = true,
            SuccessorOrReturnPathRecorded = true,
            SpatialEvidenceStateCode =
                SimulationWorldInteractionSpatialEvidenceCodes.NotApplicable,
        });

        Assert.Equal(SimulationWorldInteractionMaturityStateCodes.Manifested,
            result.StateCode);
        Assert.Empty(result.MissingEvidenceCodes);
    }

    [Fact]
    public void WI_134개는_발생원과별개로_원천과조작정책을_분류한다()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(
            SimulationWorldInteractionSpatialSeedbedTestFixture.WorldInteractionCatalog));
        var items = document.RootElement.GetProperty("items")
            .EnumerateArray().ToArray();

        Assert.Equal(134, items.Length);
        Assert.All(items, item =>
        {
            Assert.Contains(item.GetProperty("originCode").GetString(),
                SimulationWorldInteractionOriginCodes.All);
            Assert.Contains(item.GetProperty("controlPolicyCode").GetString(),
                SimulationWorldInteractionControlPolicyCodes.All);
        });
        Assert.All(items.Where(item =>
            (item.GetProperty("groupCode").GetString()
                is "HUB" or "MARKET" or "ORDER" or "LOG" or "CITY")
            && item.GetProperty("kind").GetString() == "Command"
            && item.GetProperty("originCode").GetString() != "SimulationNative"), item =>
            Assert.Equal(SimulationWorldInteractionControlPolicyCodes.NpcRoutine,
                item.GetProperty("controlPolicyCode").GetString()));
        Assert.All(items.Where(item => item.GetProperty("id").GetString()
            is "WI-FARM-01" or "WI-FARM-02" or "WI-FARM-03" or "WI-FARM-04" or "WI-FARM-05" or "WI-FARM-06"), item => Assert.Equal(
                SimulationWorldInteractionControlPolicyCodes.PlayerOrNpc,
                item.GetProperty("controlPolicyCode").GetString()));
        foreach (var id in new[] { "WI-HUB-DEMAND-ALLOCATE", "WI-TOWN-STOCK-REPLENISH", "WI-FARM-FIELD-BOUNDARY-CONFIRM" })
        {
            var 항목 = Assert.Single(items, 값 => 값.GetProperty("id").GetString() == id);
            Assert.Equal("PlayerDirect", 항목.GetProperty("controlPolicyCode").GetString());
        }
    }

    [Fact]
    public void WI_134개는_절차단계대신_한국어기능명과단일책임을노출한다()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(
            SimulationWorldInteractionSpatialSeedbedTestFixture.WorldInteractionCatalog));
        var root = document.RootElement;
        var items = root.GetProperty("items").EnumerateArray().ToArray();

        Assert.Equal(134, Simulation세계상호작용이름Catalog.All.Count);
        Assert.Equal(134, items.Length);
        Assert.Equal(items.Select(item => item.GetProperty("id").GetString()).OrderBy(id => id, StringComparer.Ordinal),
            Simulation세계상호작용이름Catalog.All.Select(item => item.WorldInteractionId).OrderBy(id => id, StringComparer.Ordinal));
        foreach (var item in items)
        {
            var id = item.GetProperty("id").GetString()!;
            var title = item.GetProperty("title").GetString()!;
            var groupCode = item.GetProperty("groupCode").GetString()!;
            var groupName = root.GetProperty("groupDisplayNames")
                .GetProperty(groupCode).GetString()!;
            var sequence = item.GetProperty("sequence").GetInt32();
            var definition = Assert.IsType<Simulation세계상호작용이름Definition>(
                Simulation세계상호작용이름Catalog.Find(id));

            Assert.Matches("[가-힣]", title);
            Assert.Equal(title, definition.한국어기능명);
            Assert.Equal(groupName, definition.한국어작업군명);
            Assert.Equal(sequence, definition.대장순번);
            Assert.False(string.IsNullOrWhiteSpace(definition.책임종류코드));
            Assert.False(string.IsNullOrWhiteSpace(definition.주요결과코드));
            Assert.False(string.IsNullOrWhiteSpace(definition.단일책임판정코드));
            Assert.Equal($"{groupName} · {title} ({id})",
                definition.한국어표시명);
        }

        Assert.Equal("LegacyCompositeMigrationRequired",
            Simulation세계상호작용이름Catalog.Find("WI-NATURE-11")!
                .단일책임판정코드);
        Assert.Equal("ProceduralStepMigrationRequired",
            Simulation세계상호작용이름Catalog.Find("WI-LOG-03")!
                .단일책임판정코드);
        Assert.Equal("ActorResponsibilityMigrationRequired",
            Simulation세계상호작용이름Catalog.Find("WI-HUB-04")!
                .단일책임판정코드);

        Assert.Equal("알 수 없는 세계 상호작용 (WI-UNKNOWN-01)",
            Simulation세계상호작용이름Catalog
                .한국어표시명("WI-UNKNOWN-01"));
    }

    [Theory]
    [InlineData("WI-CITY-RESTAURANT-ACCEPT", "OperationsDerived", "NpcRoutine")]
    [InlineData("WI-CITY-STATION-DEFENSE-ASSIGN", "SimulationNative", "PlayerDirect")]
    [InlineData("WI-CITY-RESTAURANT-COOK", "OperationsDerived", "NpcRoutine")]
    [InlineData("WI-NATURE-HANS-BOUNDARY-PATROL", "SimulationNative", "PlayerOrNpc")]
    [InlineData("WI-WORLD-BOUNDED-MANAGEMENT-GRANT", "SimulationNative", "PlayerDirect")]
    [InlineData("WI-REFLECT-HANS-FARM-CRISIS-DEBRIEF", "SimulationNative", "PlayerDirect")]
    public void 추가등록WI는_고유식별자와_원천_조작정책을_유지한다(string id, string origin, string control)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(
            SimulationWorldInteractionSpatialSeedbedTestFixture.WorldInteractionCatalog));
        var item = Assert.Single(document.RootElement.GetProperty("items").EnumerateArray(),
            value => value.GetProperty("id").GetString() == id);
        Assert.Equal(origin, item.GetProperty("originCode").GetString());
        Assert.Equal(control, item.GetProperty("controlPolicyCode").GetString());
        Assert.NotNull(Simulation세계상호작용이름Catalog.Find(id));
    }

    [Fact]
    public void 허용되지않은발생원은_실행인스턴스에서거부된다()
    {
        var invocation = SimulationWorldInteractionMaturityService.FromWorldDerived(
            "WI-FARM-04", "rule:forged-origin.r1", "world-state:1");

        var error = Assert.Throws<InvalidOperationException>(() => service.ReviewE5(new()
        {
            Definition = SpatialHarvestDefinition(),
            E4StateCode = SimulationWorldInteractionMaturityStateCodes.ContextBound,
            Invocation = invocation,
            SpatialEvidenceStateCode =
                SimulationWorldInteractionSpatialEvidenceCodes.Bound,
        }));

        Assert.Equal("WorldInteractionTriggerSourceNotAllowed", error.Message);
    }

    [Fact]
    public void 발생원기록은_JSON왕복에서보존된다()
    {
        var source = SimulationWorldInteractionMaturityService.FromNpc(
            "WI-FARM-04", "npc:worker:1", "actor:npc:worker:1");

        var restored = JsonSerializer.Deserialize<
            SimulationWorldInteractionInvocationRecord>(
                JsonSerializer.Serialize(source));

        Assert.NotNull(restored);
        Assert.Equal(source.TriggerSourceCode, restored!.TriggerSourceCode);
        Assert.Equal(source.InitiatorStableId, restored.InitiatorStableId);
        Assert.Equal(source.ActorStableId, restored.ActorStableId);
    }

    [Fact]
    public void 같은양WI도_실제수행주체에따라_플레이어와NPC사분면이갈린다()
    {
        var player = SimulationWorldInteractionMaturityService.FromPlayer(
            "WI-FARM-04", "player:1", "actor:player:1");
        var npc = SimulationWorldInteractionMaturityService.FromNpc(
            "WI-FARM-04", "npc:worker:1", "actor:npc:worker:1");

        Assert.Equal(SimulationWI사분면Codes.YangPlayer,
            player.음양주체분류.사분면Code);
        Assert.Equal("++", player.음양주체분류.사분면기호);
        Assert.Equal(SimulationWI사분면Codes.YangNpc,
            npc.음양주체분류.사분면Code);
        Assert.Equal("+-", npc.음양주체분류.사분면기호);
    }

    [Fact]
    public void 발생원이플레이어여도_NPC가수행하면_두번째부호는NPC다()
    {
        var delegated = SimulationWorldInteractionMaturityService
            .FromPlayerDelegatedToNpc(
                "WI-ORDER-01", "player:manager:1", "npc:clerk:1");

        Assert.Equal(
            SimulationWorldInteractionTriggerSourceCodes.PlayerDriven,
            delegated.TriggerSourceCode);
        Assert.Equal(SimulationWI수행주체Codes.NpcActor,
            delegated.음양주체분류.수행주체Code);
        Assert.Equal(SimulationWI사분면Codes.YinNpc,
            delegated.음양주체분류.사분면Code);
        Assert.Equal("--", delegated.음양주체분류.사분면기호);
    }

    [Fact]
    public void Actor전환대상도_신뢰NpcActor가있으면_발생원과무관하게NPC부호다()
    {
        var delegated = SimulationWorldInteractionMaturityService
            .FromPlayerDelegatedToNpc(
                "WI-HUB-04", "player:manager:1", "npc:picker:1");

        Assert.Equal(
            SimulationWorldInteractionTriggerSourceCodes.PlayerDriven,
            delegated.TriggerSourceCode);
        Assert.Equal(SimulationWI사분면Codes.YangNpc,
            delegated.음양주체분류.사분면Code);
        Assert.Equal("+-", delegated.음양주체분류.사분면기호);
    }

    [Fact]
    public void 음WI의플레이어실행과_자동전이는_사분면포함여부가갈린다()
    {
        var player = SimulationWorldInteractionMaturityService.FromPlayer(
            "WI-NATURE-15", "player:1", "actor:player:1");
        var automatic = SimulationWorldInteractionMaturityService
            .FromWorldDerived("WI-CITY-02", "rule:city-allocation.r1");

        Assert.Equal(SimulationWI사분면Codes.YinPlayer,
            player.음양주체분류.사분면Code);
        Assert.Equal("-+", player.음양주체분류.사분면기호);
        Assert.Equal(SimulationWI사분면Codes.NotApplicable,
            automatic.음양주체분류.사분면Code);
        Assert.Empty(automatic.음양주체분류.사분면기호);
    }

    [Fact]
    public void 문맥형WI는_승인된PlayableLoop에서만_음양을확정한다()
    {
        var approved = SimulationWorldInteractionMaturityService.FromPlayer(
            "WI-NATURE-03", "player:1", "actor:player:1",
            "playable-loop:nature-twilight-return.v1");
        var missing = SimulationWorldInteractionMaturityService.FromPlayer(
            "WI-NATURE-03", "player:1", "actor:player:1");

        Assert.Equal(SimulationWI사분면Codes.YangPlayer,
            approved.음양주체분류.사분면Code);
        Assert.Equal("++", approved.음양주체분류.사분면기호);
        Assert.Equal(SimulationWI사분면Codes.Unclassified,
            missing.음양주체분류.사분면Code);
    }

    [Fact]
    public void 실행중음양사분면Snapshot변조는_E5검토에서거부된다()
    {
        var invocation = SimulationWorldInteractionMaturityService.FromPlayer(
            "WI-NATURE-03", "player:1", "actor:player:1",
            "playable-loop:nature-twilight-return.v1");
        invocation.음양주체분류.사분면Code =
            SimulationWI사분면Codes.YinPlayer;

        var error = Assert.Throws<InvalidOperationException>(() =>
            service.ReviewE5(new SimulationWorldInteractionE5ManifestationReviewRequest
            {
                Definition = new SimulationWorldInteractionDefinitionContext
                {
                    WorldInteractionId = "WI-NATURE-03",
                    AllowedTriggerSourceCodes = new[]
                    {
                        SimulationWorldInteractionTriggerSourceCodes.PlayerDriven,
                    },
                    RequiredContextCodes = Array.Empty<string>(),
                    SpatialApplicabilityCode =
                        SimulationWorldInteractionSpatialEvidenceCodes.Required,
                },
                E4StateCode =
                    SimulationWorldInteractionMaturityStateCodes.ContextBound,
                Invocation = invocation,
                SpatialEvidenceStateCode =
                    SimulationWorldInteractionSpatialEvidenceCodes.Bound,
            }));

        Assert.Equal("WorldInteractionPolaritySnapshotInvalid", error.Message);
    }

    [Fact]
    public void 공개Confirm요청은_발생원선택필드를노출하지않는다()
    {
        Assert.Null(typeof(SimulationNatureSurvivalCommandRequest)
            .GetProperty("TriggerSourceCode"));
        Assert.Null(typeof(SimulationFarmWorkConfirmRequest)
            .GetProperty("TriggerSourceCode"));
        Assert.Null(typeof(SimulationRegionalIncidentResponseConfirmRequest)
            .GetProperty("TriggerSourceCode"));
    }

    private static SimulationWorldInteractionDefinitionContext SpatialHarvestDefinition()
        => new()
        {
            WorldInteractionId = "WI-FARM-04",
            AllowedTriggerSourceCodes = new[]
            {
                SimulationWorldInteractionTriggerSourceCodes.PlayerDriven,
                SimulationWorldInteractionTriggerSourceCodes.NpcDriven,
            },
            RequiredContextCodes = new[]
            {
                SimulationWorldInteractionContextCodes.Initiator,
                SimulationWorldInteractionContextCodes.Actor,
                SimulationWorldInteractionContextCodes.Target,
                SimulationWorldInteractionContextCodes.DataResource,
                SimulationWorldInteractionContextCodes.Time,
                SimulationWorldInteractionContextCodes.Spatial,
            },
            SpatialApplicabilityCode =
                SimulationWorldInteractionSpatialEvidenceCodes.Required,
        };

    private static SimulationWorldInteractionDefinitionContext NonSpatialWorldDefinition()
        => new()
        {
            WorldInteractionId = "WI-WORLD-ERA-01",
            AllowedTriggerSourceCodes = new[]
            {
                SimulationWorldInteractionTriggerSourceCodes.WorldDerived,
            },
            RequiredContextCodes = new[]
            {
                SimulationWorldInteractionContextCodes.Initiator,
                SimulationWorldInteractionContextCodes.DataResource,
                SimulationWorldInteractionContextCodes.Time,
            },
            SpatialApplicabilityCode =
                SimulationWorldInteractionSpatialEvidenceCodes.NotApplicable,
        };
}
