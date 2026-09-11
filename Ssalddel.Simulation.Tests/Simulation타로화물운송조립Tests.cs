using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Ssalddel.Simulation.Application;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;
using Ssalddel.Simulation.Infrastructure;
using Ssalddel.Simulation.Hosting.Controllers;

namespace Ssalddel.Simulation.Tests;

[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
    "Simulation·Unity 계약과 결정성 및 회귀 증거를 검증한다.",
    Boundary = "자동 시험 통과와 실제 Play Mode·Game View·E 승격 증거를 구분한다.")]
public sealed class Simulation타로화물운송조립Tests
{
    private const string TarotChariot = "tarot:major.chariot";

    [Fact]
    public void 일반타로전차는_기존학당카드와별도로턴선택지에게시된다()
    {
        var context = Context();

        var turnContext = context.SessionService
            .GetTurnClosingContext(context.Session.SessionStableId)
            ;
        var offer = turnContext.TarotDraw.Offers.First(
            value => value.Card.CardStableId == TarotChariot);
        var card = offer.Card;

        Assert.Equal(SimulationTurnCardKindCodes.Tarot, card.CardKindCode);
        Assert.Equal(SimulationTurnCardEffectCodes.ChariotFastTransport, card.EffectCode);
        Assert.Equal("tarot-card:r1", card.CardRevision);
        Assert.Contains(turnContext.AvailableCards, value =>
            value.CardStableId == "learning:hongik.chariot.integrated-progress");
    }

    [Fact]
    public void 전차를선택해턴을마감하면_다음턴화물운송Preview에상하위규칙을조립한다()
    {
        var context = Context();
        var closed = CloseWithChariot(context);

        var preview = context.TarotService.Preview(
            closed.SessionStableId,
            Request(closed.Revision, context.SourceAllocationStableId));

        Assert.Equal(closed.Revision, preview.BaseRevision);
        Assert.Equal(4, preview.ActiveTurnNumber);
        Assert.True(preview.IsCandidateOnly);
        Assert.True(preview.DoesNotApplyResourceLedgers);
        Assert.Equal("tarot-baseline-policy:freight.v1", preview.BaselinePolicyStableId);
        Assert.Equal(TarotChariot, preview.ActiveTarotCard!.CardStableId);
        Assert.Equal(300m, preview.LowerRulePreview.LogisticsMovement.Quantity);
        Assert.Empty(preview.BlockReasonCodes);
        var tarot = Assert.IsType<Simulation타로운송보정PreviewSnapshot>(
            preview.TarotRulePreview);
        AssertMetric(tarot, Simulation타로운송지표Codes.DurationTicks, 3m, 2m);
        AssertMetric(tarot, Simulation타로운송지표Codes.ThroughputCapacity, 300m, 360m);
        AssertMetric(tarot, Simulation타로운송지표Codes.FuelConsumption, 30m, 33m);
        AssertMetric(tarot, Simulation타로운송지표Codes.LaborConsumption, 3m, 3.3m);
        AssertMetric(tarot, Simulation타로운송지표Codes.RiskPercentPoint, 6m, 11m);
        Assert.Contains(
            preview.ActiveTarotCard.SourceTurnClosingStableId,
            tarot.SourceStableIds);
        Assert.Empty(context.SessionService.Get(closed.SessionStableId).FreightTransports);
    }

    [Theory]
    [InlineData(Simulation전차운송대응StableIds.FastTransport, 2, 360, 33, 3.3, 11)]
    [InlineData(Simulation전차운송대응StableIds.SafeTransport, 4, 300, 31.5, 3.6, 2)]
    [InlineData(Simulation전차운송대응StableIds.ConsolidatedTransport, 4, 300, 25.5, 2.7, 5)]
    public void 세가지전차대응은_같은하위운송기준에서서로다른후보를계산한다(
        string responseStableId,
        decimal duration,
        decimal throughput,
        decimal fuel,
        decimal labor,
        decimal risk)
    {
        var context = Context();
        var closed = CloseWithChariot(context);
        var request = Request(closed.Revision, context.SourceAllocationStableId);
        request.ResponseStableId = responseStableId;

        var preview = context.TarotService.Preview(closed.SessionStableId, request);
        var tarot = Assert.IsType<Simulation타로운송보정PreviewSnapshot>(
            preview.TarotRulePreview);

        Assert.Equal(responseStableId, tarot.ResponseStableId);
        AssertMetric(tarot, Simulation타로운송지표Codes.DurationTicks, 3m, duration);
        AssertMetric(tarot, Simulation타로운송지표Codes.ThroughputCapacity, 300m, throughput);
        AssertMetric(tarot, Simulation타로운송지표Codes.FuelConsumption, 30m, fuel);
        AssertMetric(tarot, Simulation타로운송지표Codes.LaborConsumption, 3m, labor);
        AssertMetric(tarot, Simulation타로운송지표Codes.RiskPercentPoint, 6m, risk);
        Assert.Contains(responseStableId, preview.PreviewStableId);
        Assert.Empty(context.SessionService.Get(closed.SessionStableId).FreightTransports);
    }

    [Fact]
    public void 활성전차가없으면_기존화물운송Preview만반환하고수치를보정하지않는다()
    {
        var context = Context();

        var preview = context.TarotService.Preview(
            context.Session.SessionStableId,
            Request(context.Session.Revision, context.SourceAllocationStableId));

        Assert.Null(preview.ActiveTarotCard);
        Assert.Null(preview.TarotRulePreview);
        Assert.Equal(3, preview.LowerRulePreview.LogisticsMovement.RequiredRouteTicks);
        Assert.Equal(300m, preview.LowerRulePreview.LogisticsMovement.Quantity);
    }

    [Fact]
    public void 요청은카드와보정수치를받지않고_서버가활성카드와기준Policy를결정한다()
    {
        var propertyNames = typeof(Simulation타로화물운송PreviewRequest)
            .GetProperties()
            .Select(value => value.Name)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(new[] { "ExpectedRevision", "Freight", "ResponseStableId" }, propertyNames);
    }

    [Fact]
    public void 등록되지않은전차대응은_활성카드여부와무관하게요청경계에서차단한다()
    {
        var context = Context();
        var request = Request(context.Session.Revision, context.SourceAllocationStableId);
        request.ResponseStableId = "tarot-response:chariot.unknown";

        var error = Assert.Throws<SimulationContractException>(() =>
            context.TarotService.Preview(context.Session.SessionStableId, request));

        Assert.Equal("SimulationTarotTransportResponseInvalid", error.ErrorCode);
    }

    [Fact]
    public void 하위운송규칙이차단되면_활성전차가있어도보정하지않는다()
    {
        var context = Context();
        var closed = CloseWithChariot(context);
        var request = Request(closed.Revision, context.SourceAllocationStableId);
        request.Freight.Transport.VehicleCapacity = 250m;

        var preview = context.TarotService.Preview(closed.SessionStableId, request);

        Assert.Contains("FreightVehicleCapacityExceeded", preview.BlockReasonCodes);
        Assert.NotNull(preview.ActiveTarotCard);
        Assert.Null(preview.TarotRulePreview);
        Assert.Empty(context.SessionService.Get(closed.SessionStableId).FreightTransports);
    }

    [Fact]
    public void 오래된Revision은_타로와하위규칙계산전에차단한다()
    {
        var context = Context();
        var closed = CloseWithChariot(context);

        var error = Assert.Throws<SimulationConflictException>(() =>
            context.TarotService.Preview(
                closed.SessionStableId,
                Request(0, context.SourceAllocationStableId)));

        Assert.Equal("SimulationExpectedRevisionMismatch", error.ErrorCode);
    }

    [Fact]
    public void Controller는_타로화물운송Preview전용Http경계를노출한다()
    {
        var context = Context();
        var closed = CloseWithChariot(context);
        var controller = new Simulation타로화물운송Controller(context.TarotService);

        var response = Assert.IsType<OkObjectResult>(
            controller.Preview(
                closed.SessionStableId,
                Request(closed.Revision, context.SourceAllocationStableId)).Result);

        Assert.IsType<Simulation타로화물운송통합PreviewSnapshot>(response.Value);
    }

    [Fact]
    public async Task 실제Http경계는_서비스를조립하고없는세션을404로반환한다()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/simulation/v1/sessions/simulation-session:missing/"
                + "tarot-freight-transport-previews",
            new Simulation타로화물운송PreviewRequest());
        var error = await response.Content.ReadFromJsonAsync<SimulationErrorResponse>();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("SimulationSessionNotFound", error!.ErrorCode);
    }

    private static 경영SimulationSessionSnapshot CloseWithChariot(TestContext context)
    {
        var offer = context.SessionService
                .GetTurnClosingContext(context.Session.SessionStableId)
                .TarotDraw.Offers.First(value =>
                    value.Card.CardStableId == TarotChariot
                    && value.OrientationCode == Simulation타로카드방향Codes.Upright);
        var preview = new SimulationTurnClosingPreviewRequest
        {
            ExpectedRevision = context.Session.Revision,
            SelectedTarotCard = new Simulation타로CardSelectionRequest
            {
                OfferStableId = offer.OfferStableId,
                CardStableId = offer.Card.CardStableId,
                OrientationCode = offer.OrientationCode,
            },
        };
        return context.SessionService.ConfirmTurnClosing(
            context.Session.SessionStableId,
            new SimulationTurnClosingConfirmRequest
            {
                CommandId = "command:turn.close-with-tarot-chariot",
                ExpectedRevision = context.Session.Revision,
                Preview = preview,
            });
    }

    private static void AssertMetric(
        Simulation타로운송보정PreviewSnapshot preview,
        string metricCode,
        decimal baseValue,
        decimal finalValue)
    {
        var metric = Assert.Single(preview.Metrics, value => value.MetricCode == metricCode);
        Assert.Equal(baseValue, metric.BaseValue);
        Assert.Equal(finalValue, metric.FinalValue);
    }

    private static Simulation타로화물운송PreviewRequest Request(
        long revision,
        string sourceAllocationStableId)
        => new()
        {
            ExpectedRevision = revision,
            Freight = new SimulationFreightTransportPreviewRequest
            {
                Transport = new SimulationFreightTransportBindingRequest
                {
                    TransportRequestStableId = "freight-transport:sim.potato-1",
                    DispatchOfferStableId = "dispatch-offer:sim.potato-1",
                    CarrierCandidateStableId = "carrier-candidate:sim.coop-1",
                    VehicleStableId = "vehicle:sim.truck-1",
                    VehicleCapacity = 400m,
                    VehicleCapacityUnitCode = "KGM",
                },
                Movement = new SimulationLogisticsMovementPreviewRequest
                {
                    CargoStableId = "cargo:sim.potato-1",
                    CargoRevision = 1,
                    SourceAllocationStableId = sourceAllocationStableId,
                    HarvestLotStableId = "harvest-lot:potato-1",
                    PackageLotStableId = "package-lot:potato-1",
                    ProductStableId = "product:potato",
                    Quantity = 300m,
                    UnitCode = "KGM",
                    RouteStableId = "route:sim.farm-hub-1",
                    OriginFacilityStableId = "facility:sim.farm-packing-1",
                    DestinationFacilityStableId = "facility:sim.regional-hub-1",
                    ActorStableId = "actor:sim.farmer-1",
                    RequiredRouteTicks = 3,
                    SourceStableIds = new[]
                    {
                        "harvest-lot:potato-1",
                        "package-lot:potato-1",
                        "source:fixture.freight-1",
                    },
                },
            },
        };

    private static TestContext Context()
    {
        var store = new InMemory경영SimulationSessionStore();
        var sessionService = new 경영SimulationSessionService(
            store,
            new InMemorySimulationSessionSaveStore());
        var created = sessionService.Create(new 경영SimulationSession생성Request
        {
            ClientRequestId = Guid.NewGuid(),
            ScenarioStableId = "scenario:tarot-freight-transport-1",
            ScenarioDataRevision = "scenario-data:r1",
            ScenarioSeed = FindChariotSeed(),
            RuleRevision = "rule:r1",
            DurationTicks = 14,
            WorldContext = new SimulationWorldContext생성Request
            {
                FactionStableId = "faction:sim.farmers-1",
                TerritoryStableId = "territory:sim.farm-region-1",
                SettlementStableId = "settlement:sim.farm-town-1",
                GameDateStartsOn = new DateTimeOffset(
                    2026, 4, 1, 0, 0, 0, TimeSpan.Zero),
            },
            Settlement = new SimulationSettlementInitialStateRequest
            {
                TreasuryBalance = 1_000_000m,
                CurrencyCode = "KRW",
                LaborCapacityTotal = 100m,
                StorageCapacity = 2_000m,
                StorageUnitCode = "KGM",
                PopulationCount = 100,
                PopulationFoodDemandPerTick = 100m,
                FoodEquivalentUnitCode = "KGM",
                FoodEquivalentRuleRevision = "food-equivalent:r1",
                Districts = new[]
                {
                    District("district:sim.farm-1", "Farm"),
                    District("district:sim.logistics-1", "Logistics"),
                    District("district:sim.market-1", "Market"),
                    District("district:sim.storage-1", "Storage"),
                },
                Facilities = new[]
                {
                    Facility("facility:sim.farm-packing-1", "FarmPacking",
                        "district:sim.farm-1"),
                    Facility("facility:sim.regional-hub-1", "LogisticsHub",
                        "district:sim.logistics-1"),
                    Facility("facility:sim.market-1",
                        SimulationSettlementFacilityTypeCodes.Market,
                        "district:sim.market-1"),
                    Facility("facility:sim.storage-1",
                        SimulationSettlementFacilityTypeCodes.Storage,
                        "district:sim.storage-1"),
                },
                SourceStableIds = new[] { "source:fixture.settlement-1" },
            },
        });
        var impact = sessionService.ConfirmHarvestDispositionImpact(
            created.SessionStableId,
            new SimulationHarvestDispositionImpactConfirmRequest
            {
                CommandId = "command:harvest.tarot-freight-source",
                ExpectedRevision = created.Revision,
                Impact = new SimulationHarvestDispositionImpactPreviewRequest
                {
                    DispositionDecisionStableId = "decision:harvest.tarot-freight-source",
                    DispositionDecisionRevision = 1,
                    HarvestLotStableId = "harvest-lot:potato-1",
                    HarvestLotRevision = 1,
                    ProductStableId = "product:potato",
                    Quantity = 300m,
                    UnitCode = "KGM",
                    ChoiceCode = SimulationHarvestDispositionChoiceCodes.CooperativeShipment,
                    NextWorkflowCode =
                        SimulationHarvestDispositionWorkflowCodes.CooperativeIntakeCandidate,
                    ActorStableId = "actor:sim.farmer-1",
                    SourceStableIds = new[]
                    {
                        "harvest-lot:potato-1",
                        "source:fixture.harvest-1",
                    },
                },
            });
        var session = sessionService.Advance(
            created.SessionStableId,
            new 경영SimulationTick진행Request
            {
                CommandId = "command:tick.tarot-freight-ready",
                ExpectedRevision = impact.Revision,
                TickCount = 2,
            });
        return new TestContext(
            sessionService,
            new Simulation타로화물운송PreviewService(store),
            session,
            session.Settlement!.HarvestLotAllocations.Single().AllocationStableId);
    }

    private static WebApplicationFactory<Program> CreateFactory()
        => new SimulationWebApplicationFactory()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureAppConfiguration((_, configuration) =>
                {
                    configuration.AddInMemoryCollection(
                        new Dictionary<string, string?>
                        {
                            ["SsalddelExecution:Mode"] = "Operational",
                            ["SsalddelSimulation:AllowUnauthenticatedTesting"] = "true",
                            ["SimulationSharedPublicData:Enabled"] = "false",
                        });
                });
            });

    private static SimulationSettlementDistrictRequest District(string id, string type)
        => new()
        {
            DistrictStableId = id,
            DistrictTypeCode = type,
            SourceStableIds = new[] { "source:fixture.settlement-1" },
        };

    private static int FindChariotSeed()
    {
        var draw = new Simulation타로카드뽑기();
        return Enumerable.Range(1, 10_000).First(seed =>
            draw.Draw(seed, 3, Array.Empty<string>()).Offers.Any(value =>
                value.Card.CardStableId == TarotChariot
                && value.OrientationCode == Simulation타로카드방향Codes.Upright));
    }

    private static SimulationSettlementFacilityRequest Facility(
        string id,
        string type,
        string district)
        => new()
        {
            FacilityStableId = id,
            FacilityTypeCode = type,
            DistrictStableId = district,
            SourceStableIds = new[] { "source:fixture.settlement-1" },
        };

    private sealed record TestContext(
        경영SimulationSessionService SessionService,
        Simulation타로화물운송PreviewService TarotService,
        경영SimulationSessionSnapshot Session,
        string SourceAllocationStableId);
}
