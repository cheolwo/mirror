using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ssalddel.Simulation.Application;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Hosting;
using Ssalddel.WorkflowRules.Contracts;

namespace Ssalddel.Simulation.Tests;

[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
    "Simulation·Unity 계약과 결정성 및 회귀 증거를 검증한다.",
    Boundary = "자동 시험 통과와 실제 Play Mode·Game View·E 승격 증거를 구분한다.")]
public sealed class SimulationRuntimeRenderingPipelineTests
{
    [Fact]
    public void 운송중화물은_차량이동_경로강조_흙길먼지의도로표현된다()
    {
        var request = Request(Simulation공간표면Codes.흙길, PcCapability());

        var snapshot = Service().Create(request);

        Assert.Equal(4, snapshot.Intents.Length);
        Assert.Contains(snapshot.Intents, item =>
            item.IntentCode == Simulation렌더링의도Codes.화물운송중
            && item.TargetStableId == "cargo:potato-300kg");
        Assert.Contains(snapshot.Instructions, item =>
            item.InstructionCode == Simulation렌더링지시Codes.차량경로이동
            && item.AdapterCode == Simulation렌더링AdapterCodes.Animation
            && item.Enabled);
        Assert.Contains(snapshot.Instructions, item =>
            item.InstructionCode == Simulation렌더링지시Codes.경로발광강조
            && item.AdapterCode == Simulation렌더링AdapterCodes.UrpMaterialPropertyBlock
            && item.ProfileKey == "urp.route-flow.emission.v1"
            && item.Enabled);
        Assert.Contains(snapshot.Instructions, item =>
            item.InstructionCode == Simulation렌더링지시Codes.차량흙길먼지
            && item.ProfileKey == "particle.vehicle.dirt-road.pc.v1"
            && item.Enabled);
        Assert.All(snapshot.Intents, item => Assert.True(item.PresentationOnly));
        Assert.All(snapshot.Instructions, item => Assert.True(item.PresentationOnly));
        Assert.True(snapshot.PresentationOnly);
    }

    [Fact]
    public void 포장도로에서는_먼지를꾸며내지않고_비활성Fallback을남긴다()
    {
        var snapshot = Service().Create(Request(
            Simulation공간표면Codes.포장도로,
            PcCapability()));

        var dust = Assert.Single(snapshot.Instructions, item =>
            item.InstructionCode == Simulation렌더링지시Codes.차량흙길먼지);
        Assert.False(dust.Enabled);
        Assert.Equal(
            Simulation렌더링FallbackCodes.흙길근거없어생략,
            dust.FallbackCode);
    }

    [Fact]
    public void 모바일Capability는_경로를단순강조하고_Particle을생략한다()
    {
        var capability = PcCapability();
        capability.ProfileStableId = "render-capability:mobile-low";
        capability.ProfileRevision = "mobile-low.v1";
        capability.TargetPlatformCode = "Mobile";
        capability.QualityTierCode = "Mobile-Low";
        capability.SupportsDepthTexture = false;
        capability.SupportsParticle = false;
        capability.ParticleBudget = 0;

        var snapshot = Service().Create(Request(Simulation공간표면Codes.흙길, capability));

        var route = Assert.Single(snapshot.Instructions, item =>
            item.InstructionCode == Simulation렌더링지시Codes.경로발광강조);
        Assert.True(route.Enabled);
        Assert.Equal("urp.route-flow.simple-color.v1", route.ProfileKey);
        Assert.Equal(
            Simulation렌더링FallbackCodes.DepthTexture미지원단순강조,
            route.FallbackCode);
        var dust = Assert.Single(snapshot.Instructions, item =>
            item.InstructionCode == Simulation렌더링지시Codes.차량흙길먼지);
        Assert.False(dust.Enabled);
        Assert.Equal(
            Simulation렌더링FallbackCodes.Particle미지원으로생략,
            dust.FallbackCode);
    }

    [Fact]
    public void 화물과물류이동상태가함께운송중이아니면_렌더링의도를만들지않는다()
    {
        var request = Request(Simulation공간표면Codes.흙길, PcCapability());
        request.Session.LogisticsMovements[0].StateCode =
            SimulationLogisticsMovementStateCodes.ArrivedAtDestination;

        var snapshot = Service().Create(request);

        Assert.Empty(snapshot.Intents);
        Assert.Empty(snapshot.Instructions);
    }

    [Fact]
    public void 같은상태와규칙과Capability는_동일한표현Hash를만든다()
    {
        var request = Request(Simulation공간표면Codes.흙길, PcCapability());

        var first = Service().Create(request);
        var second = Service().Create(request);

        Assert.Equal(first.PresentationHashSha256, second.PresentationHashSha256);
        Assert.Equal(first.Intents.Select(item => item.IntentStableId),
            second.Intents.Select(item => item.IntentStableId));
    }

    [Fact]
    public void 표현Pipeline은_Simulation상태와개정번호를변경하지않는다()
    {
        var request = Request(Simulation공간표면Codes.흙길, PcCapability());
        var revision = request.Session.Revision;
        var worldRevision = request.Session.WorldContext.WorldRevision;
        var worldTick = request.Session.WorldContext.WorldTick;
        var freightState = request.Session.FreightTransports[0].StateCode;
        var movementState = request.Session.LogisticsMovements[0].StateCode;

        Service().Create(request);

        Assert.Equal(revision, request.Session.Revision);
        Assert.Equal(worldRevision, request.Session.WorldContext.WorldRevision);
        Assert.Equal(worldTick, request.Session.WorldContext.WorldTick);
        Assert.Equal(freightState, request.Session.FreightTransports[0].StateCode);
        Assert.Equal(movementState, request.Session.LogisticsMovements[0].StateCode);
    }

    [Fact]
    public void 같은대상과Channel에서는_높은우선순위만선택하고_억제근거를남긴다()
    {
        var policy = new Simulation렌더링의도합성Policy();
        var lower = Intent("intent:low", 10);
        var higher = Intent("intent:high", 20);

        var result = policy.Compose(new[] { lower, higher }, 12, 7);

        Assert.Equal("intent:high", Assert.Single(result.Selected).IntentStableId);
        var suppressed = Assert.Single(result.Suppressed);
        Assert.Equal("intent:low", suppressed.SuppressedIntentStableId);
        Assert.Equal("intent:high", suppressed.WinningIntentStableId);
        Assert.Equal("LowerPriorityInChannel", suppressed.ReasonCode);
    }

    [Fact]
    public void 같은우선순위충돌은_고유식별자순으로결정되어재현가능하다()
    {
        var policy = new Simulation렌더링의도합성Policy();

        var result = policy.Compose(
            new[] { Intent("intent:z", 20), Intent("intent:a", 20) },
            12,
            7);

        Assert.Equal("intent:a", Assert.Single(result.Selected).IntentStableId);
        Assert.Equal("StableIdTieBreak", Assert.Single(result.Suppressed).ReasonCode);
    }

    [Fact]
    public void 만료된기간의도와이전Session개정의도는_합성에서제외한다()
    {
        var expired = Intent("intent:expired", 20);
        expired.LifetimeCode = Simulation렌더링수명Codes.기간;
        expired.ExpiresAtWorldTick = 6;
        var stale = Intent("intent:stale", 30);
        stale.SessionRevision = 11;

        var result = new Simulation렌더링의도합성Policy().Compose(
            new[] { expired, stale },
            12,
            7);

        Assert.Empty(result.Selected);
        Assert.Empty(result.Suppressed);
    }

    [Fact]
    public void 확인한일회성의도는_재조회해도다시표현하지않는다()
    {
        var oneShot = Intent("intent:cargo-arrived:sequence-1", 80);
        oneShot.LifetimeCode = Simulation렌더링수명Codes.일회;
        oneShot.OccurrenceSequence = 1;

        var first = new Simulation렌더링의도합성Policy().Compose(
            new[] { oneShot },
            12,
            7);
        var replay = new Simulation렌더링의도합성Policy().Compose(
            new[] { oneShot },
            12,
            7,
            new[] { oneShot.IntentStableId });

        Assert.Single(first.Selected);
        Assert.Empty(replay.Selected);
    }

    [Fact]
    public void 운영상태와_공간Hash불일치는_Runtime표현요청에서거부한다()
    {
        var operational = Request(Simulation공간표면Codes.흙길, PcCapability());
        operational.Session.IsOperationalState = true;
        var mismatched = Request(Simulation공간표면Codes.흙길, PcCapability());
        mismatched.RouteContexts[0].SpatialOutputHashSha256 = Hash('e');

        var operationalError = Assert.Throws<InvalidOperationException>(() =>
            Service().Create(operational));
        var mismatchError = Assert.Throws<InvalidOperationException>(() =>
            Service().Create(mismatched));

        Assert.Contains("운영 상태", operationalError.Message);
        Assert.Contains("공간 실행본", mismatchError.Message);
    }

    [Fact]
    public void Server조립은_렌더링의도와Urp표현Pipeline을등록한다()
    {
        var services = new ServiceCollection();
        services.AddSsalddelSimulationModule(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SsalddelSimulation:AllowUnauthenticatedTesting"] = "false",
                ["SimulationSharedPublicData:Enabled"] = "false",
                ["SimulationWorldDerivationDatabase:Enabled"] = "false",
            })
            .Build());

        Assert.Contains(services, item =>
            item.ServiceType == typeof(SimulationFreight렌더링의도Projector));
        Assert.Contains(services, item =>
            item.ServiceType == typeof(Simulation렌더링의도합성Policy));
        Assert.Contains(services, item =>
            item.ServiceType == typeof(Simulation기본Urp표현Catalog));
        Assert.Contains(services, item =>
            item.ServiceType == typeof(SimulationRuntimeWorldPresentationService));
    }

    private static SimulationRuntimeWorldPresentationService Service() => new(
        new SimulationFreight렌더링의도Projector(),
        new Simulation렌더링의도합성Policy(),
        new Simulation기본Urp표현Catalog());

    private static SimulationRuntime표현요청 Request(
        string surfaceCode,
        Simulation렌더CapabilityProfile capability)
    {
        var spatialHash = Hash('a');
        return new SimulationRuntime표현요청
        {
            Session = Session(),
            SpatialBuildStableId = "world-build:pyeongchang:spatial-v3",
            SpatialOutputHashSha256 = spatialHash,
            SyntyVisualBuildStableId = "synty-visual-build:pyeongchang-pc-high-v1",
            SyntyVisualOutputHashSha256 = Hash('b'),
            Capability = capability,
            RouteContexts = new[]
            {
                new SimulationRoute렌더링Context
                {
                    RouteStableId = "route:farm-hub",
                    SurfaceCode = surfaceCode,
                    EvidenceKindCode = "Derived",
                    SpatialBuildStableId = "world-build:pyeongchang:spatial-v3",
                    SpatialOutputHashSha256 = spatialHash,
                },
            },
        };
    }

    private static 경영SimulationSessionSnapshot Session() => new()
    {
        SessionStableId = "simulation-session:render-test",
        ScenarioStableId = "scenario:pyeongchang-farm-hub",
        ScenarioDataRevision = "scenario.v1",
        ScenarioSeed = 51760,
        RuleRevision = "simulation-rule.v1",
        Revision = 12,
        CurrentTick = 7,
        DurationTicks = 28,
        ModeCode = SimulationModeCodes.Simulation,
        IsOperationalState = false,
        WorldContext = new SimulationWorldContextSnapshot
        {
            WorldRevision = 12,
            WorldTick = 7,
            SettlementStableId = "settlement:pyeongchang",
        },
        FreightTransports = new[]
        {
            new SimulationFreightTransportSnapshot
            {
                TransportRequestStableId = "freight-transport:potato-300kg",
                StateCode = 화물운송상태코드.운송중,
                Revision = 4,
                CargoStableId = "cargo:potato-300kg",
                VehicleStableId = "vehicle:synty-van-001",
                LogisticsTaskStableId = "task:logistics:potato-300kg",
            },
        },
        LogisticsMovements = new[]
        {
            new SimulationLogisticsMovementSnapshot
            {
                CargoStableId = "cargo:potato-300kg",
                StateCode = SimulationLogisticsMovementStateCodes.InTransit,
                Revision = 5,
                TaskStableId = "task:logistics:potato-300kg",
                RouteStableId = "route:farm-hub",
                OriginFacilityStableId = "facility:daegwallyeong-farm",
                DestinationFacilityStableId = "facility:jinbu-hub",
            },
        },
    };

    private static Simulation렌더CapabilityProfile PcCapability() => new()
    {
        ProfileStableId = "render-capability:pc-high",
        ProfileRevision = "pc-high.v1",
        TargetPlatformCode = "PC",
        QualityTierCode = "PC-High",
        SupportsForwardPlus = true,
        SupportsDepthTexture = true,
        SupportsOpaqueTexture = true,
        SupportsSsao = true,
        SupportsDecal = true,
        SupportsGpuInstancing = true,
        SupportsParticle = true,
        MaximumShadowedAdditionalLights = 2,
        ParticleBudget = 1000,
        ShadowCasterBudget = 250,
    };

    private static Simulation렌더링의도 Intent(string stableId, int priority) => new()
    {
        IntentStableId = stableId,
        SourceStateStableId = "source:test",
        SourceStateRevision = 12,
        SessionRevision = 12,
        IntentCode = "TestAttention",
        ChannelCode = Simulation렌더링ChannelCodes.Attention,
        ScopeCode = Simulation렌더링범위Codes.Object,
        TargetStableId = "object:test",
        Priority = priority,
        LifetimeCode = Simulation렌더링수명Codes.상태일치동안,
        EvidenceKindCode = "Derived",
        PresentationOnly = true,
    };

    private static string Hash(char value) => new(value, 64);
}
