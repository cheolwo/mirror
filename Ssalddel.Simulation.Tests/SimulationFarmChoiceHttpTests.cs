using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;

namespace Ssalddel.Simulation.Tests;

[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
    "Simulation·Unity 계약과 결정성 및 회귀 증거를 검증한다.",
    Boundary = "자동 시험 통과와 실제 Play Mode·Game View·E 승격 증거를 구분한다.")]
public sealed class SimulationFarmChoiceHttpTests
{
    private const string Player = "actor:sim.farm-choice-http.farmer-1";
    private const string CultivationUnit =
        "cultivation-unit:sim.farm-choice-http.potato-ready-1";

    [Fact]
    public async Task 실제수확부터선택적용재조회까지_HTTP권위파이프라인으로이어진다()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var created = await Post<경영SimulationSessionSnapshot>(client,
            "/api/simulation/v1/sessions", CreateRequest(), HttpStatusCode.Created);
        var sessionRoute = "/api/simulation/v1/sessions/"
            + Uri.EscapeDataString(created.SessionStableId);
        var farmRoute = sessionRoute + "/farm-survival";

        var harvestPreview = await Post<SimulationFarmWorkPreviewSnapshot>(client,
            farmRoute + "/work/preview",
            WorkPreview(created.Revision, CultivationUnit,
                SimulationFarmSurvivalCodes.Harvesting,
                PyeongchangSimulation공간StableIds.대관령Farm수확공간));
        Assert.True(harvestPreview.CanConfirm);
        Assert.True(harvestPreview.ProjectedQuantity > 0m);
        Assert.Equal("KGM", harvestPreview.ProjectedQuantityUnitCode);

        var harvestConfirmed = await Post<SimulationFarmSurvivalStateSnapshot>(client,
            farmRoute + "/work/confirm",
            WorkConfirm("command:farm-choice-http:harvest", created.Revision,
                CultivationUnit, SimulationFarmSurvivalCodes.Harvesting,
                PyeongchangSimulation공간StableIds.대관령Farm수확공간));
        var harvested = await Tick(client, sessionRoute,
            "command:farm-choice-http:harvest:tick", harvestConfirmed.WorldRevision);
        var atField = Assert.Single(harvested.FarmSurvival!.HarvestLots);
        Assert.Equal(harvestPreview.ProjectedLotStableId, atField.HarvestLotStableId);
        Assert.Equal(harvestPreview.ProjectedQuantity, atField.Quantity);
        Assert.Equal(harvestPreview.ProjectedQuantityUnitCode, atField.UnitCode);
        Assert.Equal(Simulation수확Lot상태Codes.HarvestedAtField, atField.StateCode);
        Assert.Equal(CultivationUnit, atField.CultivationUnitStableId);
        Assert.NotEmpty(atField.CausedByTaskStableId);
        Assert.NotEmpty(atField.SourceStableIds);

        var collectionPreview = await Post<SimulationFarmWorkPreviewSnapshot>(client,
            farmRoute + "/work/preview",
            WorkPreview(harvested.Revision, atField.HarvestLotStableId,
                SimulationFarmSurvivalCodes.HarvestCollection,
                PyeongchangSimulation공간StableIds.대관령Farm집하공간));
        Assert.True(collectionPreview.CanConfirm);

        var collectionConfirmed =
            await Post<SimulationFarmSurvivalStateSnapshot>(client,
                farmRoute + "/work/confirm",
                WorkConfirm("command:farm-choice-http:collect", harvested.Revision,
                    atField.HarvestLotStableId,
                    SimulationFarmSurvivalCodes.HarvestCollection,
                    PyeongchangSimulation공간StableIds.대관령Farm집하공간));
        var collected = await Tick(client, sessionRoute,
            "command:farm-choice-http:collect:tick",
            collectionConfirmed.WorldRevision);
        var atYard = Assert.Single(collected.FarmSurvival!.HarvestLots);
        Assert.Equal(atField.HarvestLotStableId, atYard.HarvestLotStableId);
        Assert.Equal(atField.Revision + 1, atYard.Revision);
        Assert.Equal(atField.Quantity, atYard.Quantity);
        Assert.Equal(atField.UnitCode, atYard.UnitCode);
        Assert.Equal(atField.SourceStableIds, atYard.SourceStableIds);
        Assert.Equal(Simulation수확Lot상태Codes.CollectedAtYard, atYard.StateCode);

        var context = await Get<SimulationFarmChoiceContextSnapshot>(client,
            sessionRoute + "/farm-choice-context");
        Assert.Equal(SimulationFarmChoicePlayableCodes.AwaitingChoice,
            context.SituationStateCode);
        Assert.Equal(atYard.HarvestLotStableId, context.HarvestLotStableId);
        Assert.Equal(atYard.ProductStableId, context.ProductStableId);
        Assert.Equal(collected.Revision, context.WorldRevision);
        Assert.Equal(3, context.Candidates.Length);
        var lotFact = Assert.Single(context.Facts,
            fact => fact.FactCode == "HarvestLotReady");
        Assert.Equal(atYard.HarvestLotStableId, lotFact.TargetStableId);
        Assert.Contains(atYard.HarvestLotStableId, lotFact.SourceStableIds);
        Assert.All(atYard.SourceStableIds,
            source => Assert.Contains(source, lotFact.SourceStableIds));

        var previewRequest = new SimulationFarmChoicePreviewRequest
        {
            ExpectedRevision = context.WorldRevision,
            ChoiceStableId =
                SimulationFarmChoicePlayableCodes.ReserveStorageChoice,
        };
        AssertChoiceRequestShape(previewRequest, "expectedRevision", "choiceStableId");
        var choicePreview = await Post<SimulationFarmChoicePreviewSnapshot>(client,
            sessionRoute + "/farm-choice-previews", previewRequest);
        Assert.True(choicePreview.IsCandidateOnly);
        Assert.True(choicePreview.RequiresExplicitConfirm);
        Assert.Equal(atYard.HarvestLotStableId,
            choicePreview.Impact.HarvestLotStableId);
        Assert.Equal(atYard.ProductStableId, choicePreview.Impact.ProductStableId);
        Assert.Equal(atYard.Quantity, choicePreview.Impact.Quantity);
        Assert.Equal(atYard.UnitCode, choicePreview.Impact.SourceUnitCode);

        var confirmRequest = new SimulationFarmChoiceConfirmRequest
        {
            CommandId = "command:farm-choice-http:reserve-storage",
            ExpectedRevision = choicePreview.BaseRevision,
            ChoiceStableId = choicePreview.ChoiceStableId,
        };
        AssertChoiceRequestShape(confirmRequest,
            "commandId", "expectedRevision", "choiceStableId");
        var choiceConfirmed = await Post<경영SimulationSessionSnapshot>(client,
            sessionRoute + "/farm-choices/confirm", confirmRequest);
        var reservedAllocation = Assert.Single(
            choiceConfirmed.Settlement!.HarvestLotAllocations);
        AssertLotLineage(reservedAllocation, atYard);
        Assert.Equal(SimulationHarvestLotAllocationStateCodes.Reserved,
            reservedAllocation.StateCode);

        var applied = await Tick(client, sessionRoute,
            "command:farm-choice-http:reserve-storage:tick",
            choiceConfirmed.Revision, choicePreview.Impact.DurationTicks);
        var appliedAllocation = Assert.Single(
            applied.Settlement!.HarvestLotAllocations);
        AssertLotLineage(appliedAllocation, atYard);
        Assert.Equal(SimulationHarvestLotAllocationStateCodes.Applied,
            appliedAllocation.StateCode);
        Assert.Equal(SimulationHarvestDispositionChoiceCodes.ReserveStorage,
            appliedAllocation.ChoiceCode);

        var refreshed = await Get<SimulationFarmChoiceContextSnapshot>(client,
            sessionRoute + "/farm-choice-context");
        Assert.Equal(applied.Revision, refreshed.WorldRevision);
        Assert.Equal(applied.CurrentTick, refreshed.WorldTick);
        Assert.Equal(SimulationFarmChoicePlayableCodes.ChoiceConfirmed,
            refreshed.SituationStateCode);
        Assert.Equal(confirmRequest.ChoiceStableId,
            refreshed.AppliedChoiceStableId);
        Assert.Equal(atYard.HarvestLotStableId, refreshed.HarvestLotStableId);

        var finalFarmState = await Get<SimulationFarmSurvivalStateSnapshot>(client,
            farmRoute);
        var finalLot = Assert.Single(finalFarmState.HarvestLots);
        Assert.Equal(atYard.HarvestLotStableId, finalLot.HarvestLotStableId);
        Assert.Equal(atYard.Revision, finalLot.Revision);
        Assert.Equal(atYard.Quantity, finalLot.Quantity);
        Assert.Equal(atYard.SourceStableIds, finalLot.SourceStableIds);
    }

    private static void AssertLotLineage(
        SimulationHarvestLotAllocationSnapshot allocation,
        Simulation수확LotSnapshot harvestLot)
    {
        Assert.Equal(harvestLot.HarvestLotStableId,
            allocation.HarvestLotStableId);
        Assert.Equal(harvestLot.Revision, allocation.HarvestLotRevision);
        Assert.Equal(harvestLot.ProductStableId, allocation.ProductStableId);
        Assert.Equal(harvestLot.Quantity, allocation.Quantity);
        Assert.Equal(harvestLot.UnitCode, allocation.UnitCode);
        Assert.Contains(harvestLot.HarvestLotStableId,
            allocation.SourceStableIds);
        Assert.All(harvestLot.SourceStableIds,
            source => Assert.Contains(source, allocation.SourceStableIds));
    }

    private static void AssertChoiceRequestShape<T>(
        T request,
        params string[] expectedPropertyNames)
    {
        var json = JsonSerializer.SerializeToElement(request,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var actualPropertyNames = json.EnumerateObject()
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expectedPropertyNames.OrderBy(name => name, StringComparer.Ordinal),
            actualPropertyNames);
        Assert.DoesNotContain(actualPropertyNames,
            name => name.Contains("quantity", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(actualPropertyNames,
            name => name.Contains("harvestLot", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(actualPropertyNames,
            name => name.Contains("unit", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("impact", actualPropertyNames);
    }

    private static SimulationFarmWorkPreviewRequest WorkPreview(
        long revision,
        string targetStableId,
        string actionCode,
        string preferredSpatialStableId)
        => new()
        {
            ExpectedRevision = revision,
            ActorStableId = Player,
            TargetStableId = targetStableId,
            ActionCode = actionCode,
            AssignmentKindCode = SimulationFarmSurvivalCodes.PlayerDirect,
            PreferredSpatialStableId = preferredSpatialStableId,
        };

    private static SimulationFarmWorkConfirmRequest WorkConfirm(
        string commandId,
        long revision,
        string targetStableId,
        string actionCode,
        string preferredSpatialStableId)
        => new()
        {
            CommandId = commandId,
            ExpectedRevision = revision,
            ActorStableId = Player,
            TargetStableId = targetStableId,
            ActionCode = actionCode,
            AssignmentKindCode = SimulationFarmSurvivalCodes.PlayerDirect,
            PreferredSpatialStableId = preferredSpatialStableId,
        };

    private static async Task<경영SimulationSessionSnapshot> Tick(
        HttpClient client,
        string sessionRoute,
        string commandId,
        long revision,
        int count = 1)
        => await Post<경영SimulationSessionSnapshot>(client,
            sessionRoute + "/ticks",
            new 경영SimulationTick진행Request
            {
                CommandId = commandId,
                ExpectedRevision = revision,
                TickCount = count,
            });

    private static async Task<T> Post<T>(
        HttpClient client,
        string route,
        object request,
        HttpStatusCode expectedStatus = HttpStatusCode.OK)
    {
        using var response = await client.PostAsJsonAsync(route, request);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == expectedStatus,
            $"POST {route} expected {expectedStatus} but received "
            + $"{response.StatusCode}: {body}");
        return Assert.IsType<T>(JsonSerializer.Deserialize<T>(body,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }

    private static async Task<T> Get<T>(HttpClient client, string route)
    {
        using var response = await client.GetAsync(route);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode,
            $"GET {route} failed with {response.StatusCode}: {body}");
        return Assert.IsType<T>(JsonSerializer.Deserialize<T>(body,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)));
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

    private static 경영SimulationSession생성Request CreateRequest() => new()
    {
        ClientRequestId = Guid.NewGuid(),
        ScenarioStableId = "scenario:test.farm-choice-http",
        ScenarioDataRevision = "scenario-data:farm-choice-http.r1",
        ScenarioSeed = 20260821,
        RuleRevision = "rule:farm-choice-http.r1",
        DurationTicks = 28,
        WorldContext = new SimulationWorldContext생성Request
        {
            FactionStableId = "faction:sim.farm-choice-http",
            TerritoryStableId = "territory:sim.farm-choice-http",
            SettlementStableId = "settlement:sim.farm-choice-http",
            GameDateStartsOn = new DateTimeOffset(
                2026, 4, 1, 0, 0, 0, TimeSpan.Zero),
        },
        Settlement = new SimulationSettlementInitialStateRequest
        {
            TreasuryBalance = 1_000_000m,
            CurrencyCode = "KRW",
            LaborCapacityTotal = 100m,
            LaborReserved = 25m,
            StorageCapacity = 2000m,
            StorageOccupied = 1200m,
            StorageUnitCode = "KGM",
            PopulationCount = 100,
            PopulationFoodDemandPerTick = 100m,
            GarrisonCount = 20,
            GarrisonFoodDemandPerTick = 20m,
            FoodEquivalentUnitCode = "FoodEquivalentUnit",
            FoodEquivalentRuleRevision = "food-equivalent:fixture-r1",
            Districts =
            [
                new SimulationSettlementDistrictRequest
                {
                    DistrictStableId = "district:sim.farm-choice-http.farm",
                    DistrictTypeCode = "FarmDistrict",
                    SourceStableIds = Sources(),
                },
                new SimulationSettlementDistrictRequest
                {
                    DistrictStableId = "district:sim.farm-choice-http.central",
                    DistrictTypeCode = "CentralDistrict",
                    SourceStableIds = Sources(),
                },
            ],
            Facilities =
            [
                new SimulationSettlementFacilityRequest
                {
                    FacilityStableId = "facility:sim.farm-choice-http.storage",
                    FacilityTypeCode = SimulationSettlementFacilityTypeCodes.Storage,
                    DistrictStableId = "district:sim.farm-choice-http.farm",
                    SourceStableIds = Sources(),
                },
                new SimulationSettlementFacilityRequest
                {
                    FacilityStableId = "facility:sim.farm-choice-http.market",
                    FacilityTypeCode = SimulationSettlementFacilityTypeCodes.Market,
                    DistrictStableId = "district:sim.farm-choice-http.central",
                    SourceStableIds = Sources(),
                },
            ],
            MarketSupplyByProduct =
            [
                new SimulationMarketSupplyRequest
                {
                    ProductStableId = SimulationFarmChoicePlayableCodes.ProductStableId,
                    Quantity = 300m,
                    UnitCode = "KGM",
                    SourceStableIds = Sources(),
                },
            ],
            ReserveStockLots =
            [
                new SimulationReserveStockLotRequest
                {
                    StockLotStableId = "stock-lot:sim.farm-choice-http.potato-1",
                    ProductStableId = SimulationFarmChoicePlayableCodes.ProductStableId,
                    StorageFacilityStableId =
                        "facility:sim.farm-choice-http.storage",
                    Quantity = 1200m,
                    UnitCode = "KGM",
                    FoodEquivalentQuantity = 1200m,
                    SourceStableIds = Sources(),
                },
            ],
            SourceStableIds = Sources(),
        },
        SpatialWorld = PyeongchangSimulation공간상호작용Fixture.CreateFarmHubSupply(
            "facility:sim.farm-choice-http",
            "facility:sim.farm-choice-http.market"),
        FarmSurvival = new SimulationFarmSurvivalInitialStateRequest
        {
            RuleRevision = SimulationFarmSurvivalCodes.ScenicSeasonRuleRevision,
            RegionStableId = "region:legal-dong:5176031000",
            AreaStableId = "area:pyeongchang:daegwallyeong-farm",
            TileKey = "kr5186:l2:700:1145",
            FarmBuildingStableId = "facility:sim.farm-choice-http",
            SupplyUnits = 8m,
            RepairMaterialUnits = 4m,
            SeedUnits = 2m,
            WaterUnits = 2m,
            Actors =
            [
                new SimulationFarmActorInitialStateRequest
                {
                    ActorStableId = Player,
                    ActorKindCode = SimulationFarmSurvivalCodes.Player,
                    KoreanName = "감자 농장 작업자",
                    CapabilityCodes =
                    [
                        SimulationFarmActorCapabilityCodes.FarmHarvest,
                        SimulationFarmActorCapabilityCodes.FarmCollection,
                    ],
                },
            ],
            CultivationUnits =
            [
                new Simulation재배단위Snapshot
                {
                    CultivationUnitStableId = CultivationUnit,
                    Revision = 1,
                    TileStableId = "soil-tile:sim.farm-choice-http.potato-ready-1",
                    CultivationStableId =
                        "cultivation:sim.farm-choice-http.potato-ready-1",
                    ProductStableId =
                        SimulationFarmChoicePlayableCodes.ProductStableId,
                    CropVariantStableId = "crop-variant:potato.fixture",
                    StateCode = Simulation재배단위상태Codes.HarvestReady,
                    PhysicalAreaSquareMeters = 100m,
                    EffectiveCultivationAreaRatio = 1m,
                    SourceStableIds = Sources(),
                },
            ],
            PotatoProductionRule = new Simulation감자생산RuleSnapshot
            {
                RuleStableId = "rule:potato-production.farm-choice-http.v1",
                RuleRevision = 1,
                SourceTypeCode = Simulation생산규칙SourceTypeCodes.Fixture,
                ProductStableId = SimulationFarmChoicePlayableCodes.ProductStableId,
                CropVariantStableId = "crop-variant:potato.fixture",
                BaseYieldKilogramsPerSquareMeter = 3m,
                MinimumEnvironmentFactor = 0.5m,
                MaximumEnvironmentFactor = 1m,
                MinimumInputFactor = 0.8m,
                MaximumInputFactor = 1.2m,
                MinimumFacilityFactor = 0.8m,
                MaximumFacilityFactor = 1.2m,
                MinimumLossFactor = 0.1m,
                MaximumLossFactor = 1m,
                SourceStableIds = Sources(),
                Limitations = ["Simulation fixture only"],
            },
        },
    };

    private static string[] Sources()
        => ["source:scenario-farm-choice-http-r1"];
}
