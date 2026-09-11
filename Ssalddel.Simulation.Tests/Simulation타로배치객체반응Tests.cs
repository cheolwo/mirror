using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Ssalddel.Simulation.Application;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;
using Ssalddel.Simulation.Infrastructure;

namespace Ssalddel.Simulation.Tests;

[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
    "Simulation·Unity 계약과 결정성 및 회귀 증거를 검증한다.",
    Boundary = "자동 시험 통과와 실제 Play Mode·Game View·E 승격 증거를 구분한다.")]
public sealed class Simulation타로배치객체반응Tests
{
    [Fact]
    public void 객체Catalog는_O6검증된일곱배치객체만가진다()
    {
        var states = Simulation타로배치객체반응계산기.CreateEmptyO6ObjectStates();

        Assert.Equal(7, states.Length);
        Assert.Equal(7, states.Select(value => value.ObjectStableId).Distinct().Count());
        Assert.All(states, value =>
        {
            Assert.StartsWith("seedbed-object:", value.ObjectStableId);
            Assert.StartsWith("scene-placement:simulation-world-shell.",
                value.PlacementStableId);
            Assert.False(value.HasRelevantState);
        });
    }

    [Theory]
    [InlineData("tarot:major.empress", 2)]
    [InlineData("tarot:major.chariot", 4)]
    [InlineData("tarot:major.justice", 3)]
    [InlineData("tarot:major.temperance", 3)]
    public void 첫네장은_기획된O6객체관계만반환한다(string cardStableId, int count)
    {
        var calculator = new Simulation타로배치객체반응계산기();
        var states = Simulation타로배치객체반응계산기.CreateEmptyO6ObjectStates();
        var reaction = calculator.Calculate(Offer(cardStableId), states);

        Assert.Equal(count, reaction.ObjectReactions.Length);
        Assert.Empty(reaction.HighlightObjectStableIds);
        Assert.All(reaction.ObjectReactions, value =>
        {
            Assert.False(value.CanHighlightInWorld);
            Assert.Equal(Simulation타로객체반응상태Codes.StateUnavailable,
                value.ReactionStateCode);
            Assert.Equal(
                new[] { "SimulationTarotRelevantObjectStateUnavailable" },
                value.BlockReasonCodes);
        });
    }

    [Fact]
    public void 현재상태가있는객체만강조하고_정적관계만으로강조하지않는다()
    {
        var context = Context(withReadyHarvest: true);
        var turn = context.SessionService.GetTurnClosingContext(
            context.Session.SessionStableId);

        var preview = context.ReactionService.Preview(
            context.Session.SessionStableId,
            new Simulation타로객체반응PreviewRequest
            {
                ExpectedRevision = context.Session.Revision,
                DrawStableId = turn.TarotDraw.DrawStableId,
            });
        var unchanged = context.SessionService.Get(context.Session.SessionStableId);

        Assert.True(preview.IsCandidateOnly);
        Assert.True(preview.DoesNotMutateSession);
        Assert.Equal(context.Session.Revision, unchanged.Revision);
        Assert.Empty(unchanged.FreightTransports);
        Assert.Contains(SimulationO6배치객체StableIds.HarvestBox,
            preview.HighlightObjectStableIds);
        Assert.Contains(SimulationO6배치객체StableIds.FarmCrate,
            preview.HighlightObjectStableIds);
        Assert.Contains(SimulationO6배치객체StableIds.Market,
            preview.HighlightObjectStableIds);
        Assert.DoesNotContain(SimulationO6배치객체StableIds.DeliveryTruck,
            preview.HighlightObjectStableIds);
        Assert.DoesNotContain(SimulationO6배치객체StableIds.CargoPallet,
            preview.HighlightObjectStableIds);
        Assert.DoesNotContain(SimulationO6배치객체StableIds.HubGate,
            preview.HighlightObjectStableIds);
        Assert.DoesNotContain(SimulationO6배치객체StableIds.GroupCart,
            preview.HighlightObjectStableIds);
    }

    [Fact]
    public void 관련원장이없는Session은_세장을반환하지만강조객체를만들지않는다()
    {
        var context = Context(withReadyHarvest: false);
        var turn = context.SessionService.GetTurnClosingContext(
            context.Session.SessionStableId);

        var preview = context.ReactionService.Preview(
            context.Session.SessionStableId,
            new Simulation타로객체반응PreviewRequest
            {
                ExpectedRevision = context.Session.Revision,
                DrawStableId = turn.TarotDraw.DrawStableId,
            });

        Assert.Equal(3, preview.CardReactions.Length);
        Assert.Empty(preview.HighlightObjectStableIds);
        Assert.All(preview.CardReactions.SelectMany(value => value.ObjectReactions),
            value => Assert.False(value.CanHighlightInWorld));
    }

    [Fact]
    public void 오래된Revision과현재가아닌Draw는_객체반응계산전에차단한다()
    {
        var context = Context(withReadyHarvest: false);
        var turn = context.SessionService.GetTurnClosingContext(
            context.Session.SessionStableId);

        var stale = Assert.Throws<SimulationConflictException>(() =>
            context.ReactionService.Preview(
                context.Session.SessionStableId,
                new Simulation타로객체반응PreviewRequest
                {
                    ExpectedRevision = context.Session.Revision + 1,
                    DrawStableId = turn.TarotDraw.DrawStableId,
                }));
        var wrongDraw = Assert.Throws<SimulationConflictException>(() =>
            context.ReactionService.Preview(
                context.Session.SessionStableId,
                new Simulation타로객체반응PreviewRequest
                {
                    ExpectedRevision = context.Session.Revision,
                    DrawStableId = "tarot-draw:turn-1:forged",
                }));

        Assert.Equal("SimulationExpectedRevisionMismatch", stale.ErrorCode);
        Assert.Equal("SimulationTarotDrawUnavailable", wrongDraw.ErrorCode);
    }

    [Fact]
    public async Task 실제Http경계는_서비스를조립하고없는Session을404로반환한다()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/simulation/v1/sessions/simulation-session:missing/"
                + "tarot-object-reaction-previews",
            new Simulation타로객체반응PreviewRequest
            {
                DrawStableId = "tarot-draw:turn-1:missing",
            });
        var error = await response.Content.ReadFromJsonAsync<SimulationErrorResponse>();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("SimulationSessionNotFound", error!.ErrorCode);
    }

    private static Simulation타로CardOfferSnapshot Offer(string cardStableId)
    {
        var copy = new Simulation타로카드뽑기().CreateStarterDeck()
            .First(value => value.Card.CardStableId == cardStableId);
        return new Simulation타로CardOfferSnapshot
        {
            OfferStableId = "tarot-draw:test:offer-1",
            OfferSlotNumber = 1,
            CardCopyStableId = copy.CardCopyStableId,
            OrientationCode = Simulation타로카드방향Codes.Upright,
            Card = copy.Card,
        };
    }

    private static TestContext Context(bool withReadyHarvest)
    {
        var store = new InMemory경영SimulationSessionStore();
        var sessionService = new 경영SimulationSessionService(
            store,
            new InMemorySimulationSessionSaveStore());
        var created = sessionService.Create(CreateRequest(withReadyHarvest));
        var current = created;
        if (withReadyHarvest)
        {
            var impact = sessionService.ConfirmHarvestDispositionImpact(
                created.SessionStableId,
                new SimulationHarvestDispositionImpactConfirmRequest
                {
                    CommandId = "command:harvest.tarot-object-source",
                    ExpectedRevision = created.Revision,
                    Impact = HarvestImpact(),
                });
            current = sessionService.Advance(
                created.SessionStableId,
                new 경영SimulationTick진행Request
                {
                    CommandId = "command:tick.tarot-object-ready",
                    ExpectedRevision = impact.Revision,
                    TickCount = 2,
                });
        }
        return new TestContext(
            sessionService,
            new Simulation타로객체반응PreviewService(store),
            current);
    }

    private static 경영SimulationSession생성Request CreateRequest(bool withMarketSupply)
        => new()
        {
            ClientRequestId = Guid.NewGuid(),
            ScenarioStableId = "scenario:tarot-object-reaction-1",
            ScenarioDataRevision = "scenario-data:tarot-object-r1",
            ScenarioSeed = FindSeedWithEmpressAndChariot(),
            RuleRevision = "simulation-rule:tarot-object-r1",
            DurationTicks = 14,
            WorldContext = new SimulationWorldContext생성Request
            {
                FactionStableId = "faction:sim.tarot-object-1",
                TerritoryStableId = "territory:sim.tarot-object-1",
                SettlementStableId = "settlement:sim.tarot-object-1",
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
                MarketSupplyByProduct = withMarketSupply
                    ? new[]
                    {
                        new SimulationMarketSupplyRequest
                        {
                            ProductStableId = "product:potato",
                            Quantity = 50m,
                            UnitCode = "KGM",
                            SourceStableIds = new[] { "source:fixture.market-supply-1" },
                        },
                    }
                    : Array.Empty<SimulationMarketSupplyRequest>(),
                SourceStableIds = new[] { "source:fixture.settlement-1" },
            },
        };

    private static SimulationHarvestDispositionImpactPreviewRequest HarvestImpact()
        => new()
        {
            DispositionDecisionStableId = "decision:harvest.tarot-object-source",
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
        };

    private static int FindSeedWithEmpressAndChariot()
    {
        var draw = new Simulation타로카드뽑기();
        return Enumerable.Range(1, 10_000).First(seed =>
        {
            var cards = draw.Draw(seed, 3, Array.Empty<string>()).Offers
                .Select(value => value.Card.CardStableId).ToArray();
            return cards.Contains("tarot:major.empress")
                && cards.Contains("tarot:major.chariot");
        });
    }

    private static SimulationSettlementDistrictRequest District(string id, string type)
        => new()
        {
            DistrictStableId = id,
            DistrictTypeCode = type,
            SourceStableIds = new[] { "source:fixture.settlement-1" },
        };

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

    private sealed record TestContext(
        경영SimulationSessionService SessionService,
        Simulation타로객체반응PreviewService ReactionService,
        경영SimulationSessionSnapshot Session);
}
