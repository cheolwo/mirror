using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Ssalddel.Simulation.Application;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;
using Ssalddel.WorkflowRules.Contracts;
using Xunit;

namespace Ssalddel.Simulation.Tests;

[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
    "Simulation·Unity 계약과 결정성 및 회귀 증거를 검증한다.",
    Boundary = "자동 시험 통과와 실제 Play Mode·Game View·E 승격 증거를 구분한다.")]
public sealed class SimulationWorldInboundInspectionUiVerticalSliceTests
{
    [Fact]
    public async System.Threading.Tasks.Task 공간예약된검수작업은_HTTP취소뒤예약과임시재고가반환된다()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var session = await Post<경영SimulationSessionSnapshot>(
            client,
            "/api/simulation/v1/sessions",
            CreateSessionRequest(),
            HttpStatusCode.Created);
        var request = CancellationInspection();
        var preview = await Post<SimulationDecisionPreviewSnapshot>(
            client,
            $"/api/simulation/v1/sessions/{session.SessionStableId}/decision-previews",
            request);
        Assert.Equal(PyeongchangSimulation공간StableIds.진부Hub검수공간,
            preview.TaskPlan.SelectedSpatialStableId);

        session = await Post<경영SimulationSessionSnapshot>(
            client,
            $"/api/simulation/v1/sessions/{session.SessionStableId}/decisions/confirm",
            new SimulationDecisionConfirmRequest
            {
                CommandId = "command:http:spatial-cancel:confirm",
                ExpectedRevision = session.Revision,
                Preview = request,
            });
        var task = Assert.Single(session.Tasks);
        session = await Post<경영SimulationSessionSnapshot>(
            client,
            $"/api/simulation/v1/sessions/{session.SessionStableId}/tasks/{task.TaskStableId}/cancel",
            new SimulationTaskCancelRequest
            {
                CommandId = "command:http:spatial-cancel",
                ExpectedRevision = session.Revision,
                ReasonCode = "UserCancelled",
            });

        Assert.Equal(SimulationTaskStateCodes.Cancelled, Assert.Single(session.Tasks).StateCode);
        Assert.Empty(session.NpcFacilityInventories);
        Assert.All(session.SpatialReservations, value =>
            Assert.Equal(Simulation공간예약상태Codes.Cancelled, value.StatusCode));
    }

    [Fact]
    public async System.Threading.Tasks.Task 진부면물류거점정보판은_미리보기_확정_Npc검수_완료재조회를끝까지잇는다()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var session = await Post<경영SimulationSessionSnapshot>(
            client,
            "/api/simulation/v1/sessions",
            CreateSessionRequest(),
            HttpStatusCode.Created);
        session = await Post<경영SimulationSessionSnapshot>(
            client,
            $"/api/simulation/v1/sessions/{session.SessionStableId}/harvest-disposition-impacts/confirm",
            new SimulationHarvestDispositionImpactConfirmRequest
            {
                CommandId = "command:ui-vertical:harvest",
                ExpectedRevision = session.Revision,
                Impact = HarvestImpact(),
            });
        session = await Tick(client, session, "command:ui-vertical:harvest-ready", 2);
        session = await Post<경영SimulationSessionSnapshot>(
            client,
            $"/api/simulation/v1/sessions/{session.SessionStableId}/freight-transports/confirm",
            new SimulationFreightTransportConfirmRequest
            {
                CommandId = "command:ui-vertical:freight",
                ExpectedRevision = session.Revision,
                Freight = Freight(),
            });
        session = await Tick(client, session, "command:ui-vertical:freight-1");
        session = await Tick(client, session, "command:ui-vertical:freight-2");
        session = await Tick(client, session, "command:ui-vertical:freight-3");

        var projectionRoute = $"/api/simulation/v1/sessions/{session.SessionStableId}/world-ui/surfaces/{SimulationWorldUIProjectionService.진부면물류거점입출고SurfaceStableId}";
        var ready = await Get<SimulationWorldUIProjection>(client, projectionRoute);
        var previewAction = ready.Actions.Single(value => value.ActionKindCode == "Preview");
        var confirmAction = ready.Actions.Single(value => value.ActionKindCode == "Confirm");

        Assert.Equal("진부면 물류 거점 입출고", ready.KoreanTitle);
        Assert.Equal("Ready", ready.StateCode);
        Assert.Equal(SimulationWorldUIDesignProfileCodes.FigmaMauiWarehouseV1,
            ready.DesignProfileRevision);
        Assert.Equal(SimulationWorldUI화면종류Codes.업무상세판, ready.SurfaceKindCode);
        Assert.Equal(SimulationWorldUILayoutProfileCodes.WorldSidePanel,
            ready.LayoutProfileCode);
        Assert.Equal(SimulationWorldUIStyleSemanticKeys.Warehouse,
            ready.RoleStyleSemanticKey);
        Assert.Equal("State.Ready", ready.StateStyleSemanticKey);
        Assert.Equal(업무흐름코드.창고입고, ready.WorkflowCode);
        Assert.Equal(창고입고상태코드.입고예정, ready.WorkflowStageCode);
        Assert.Equal(SimulationWorldUIExecutionModeCodes.Simulation, ready.ExecutionModeCode);
        Assert.True(previewAction.Enabled);
        Assert.True(confirmAction.Enabled);
        Assert.Equal(SimulationWorldUIStyleSemanticKeys.PreviewAction,
            previewAction.StyleSemanticKey);
        Assert.Equal(SimulationWorldUIStyleSemanticKeys.ConfirmAction,
            confirmAction.StyleSemanticKey);
        Assert.Contains(ready.InformationItems, value =>
            value.InformationKindCode == "Limitation"
            && value.StyleSemanticKey == SimulationWorldUIStyleSemanticKeys.Limitation);
        Assert.Equal("POST", previewAction.HttpMethod);
        Assert.Equal("SimulationFreightReceiptPreviewRequest", previewAction.RequestContractKey);
        Assert.Equal("SimulationFreightReceiptConfirmRequest", confirmAction.RequestContractKey);
        Assert.Equal(창고입고행동코드.검수완료, confirmAction.CanonicalActionCode);
        Assert.Equal(projectionRoute.Replace(session.SessionStableId, "{sessionStableId}"),
            confirmAction.CanonicalRequeryRouteTemplate.Replace("{surfaceStableId}", ready.SurfaceStableId));
        Assert.NotNull(confirmAction.Invocation);
        Assert.Equal(session.Revision, confirmAction.Invocation.ExpectedStateRevision);

        var receipt = Receipt(previewAction.Invocation!);
        var preview = await Post<SimulationDecisionPreviewSnapshot>(
            client,
            $"/api/simulation/v1/sessions/{session.SessionStableId}/freight-receipt-previews",
            receipt);
        Assert.Empty(preview.Decision.BlockReasonCodes);

        session = await Post<경영SimulationSessionSnapshot>(
            client,
            $"/api/simulation/v1/sessions/{session.SessionStableId}/freight-receipts/confirm",
            new SimulationFreightReceiptConfirmRequest
            {
                CommandId = "command:ui-vertical:receipt",
                ExpectedRevision = confirmAction.Invocation.ExpectedStateRevision!.Value,
                Receipt = Receipt(confirmAction.Invocation),
            });
        var working = await Get<SimulationWorldUIProjection>(client, projectionRoute);
        Assert.Equal("InProgress", working.StateCode);
        Assert.False(working.Actions.Single(value => value.ActionKindCode == "Confirm").Enabled);
        Assert.Single(session.NpcTaskAssignments);
        Assert.Equal(PyeongchangSimulationNpcStableIds.진부입고검수담당,
            Assert.Single(session.NpcTaskAssignments).ActorStableId);

        for (var index = 0; index < 4; index++)
        {
            session = await Tick(client, session, $"command:ui-vertical:receipt-{index + 1}");
            var current = await Get<SimulationWorldUIProjection>(client, projectionRoute);
            if (current.WorkflowStageCode == 창고입고상태코드.적재대기) break;
        }

        var putAwayReady = await Get<SimulationWorldUIProjection>(client, projectionRoute);
        Assert.Equal("Ready", putAwayReady.StateCode);
        Assert.Equal(창고입고상태코드.적재대기, putAwayReady.WorkflowStageCode);
        Assert.Contains(putAwayReady.InformationItems,
            value => value.InformationKindCode == "Summary"
                && value.ValueText.Contains("적재 대기 1건", StringComparison.Ordinal));
        Assert.Equal(SimulationNpcInventoryStateCodes.StorageEligible,
            Assert.Single(session.NpcFacilityInventories).StateCode);
        var putAwayPreviewAction = putAwayReady.Actions.Single(value => value.ActionKindCode == "Preview");
        var putAwayConfirmAction = putAwayReady.Actions.Single(value => value.ActionKindCode == "Confirm");
        Assert.Equal(nameof(SimulationWarehousePutAwayPreviewRequest), putAwayPreviewAction.RequestContractKey);
        Assert.Equal(nameof(SimulationWarehousePutAwayConfirmRequest), putAwayConfirmAction.RequestContractKey);
        Assert.Equal(창고입고행동코드.적재완료, putAwayConfirmAction.CanonicalActionCode);
        Assert.NotNull(putAwayConfirmAction.Invocation);

        var putAway = PutAway(putAwayPreviewAction.Invocation!);
        var putAwayPreview = await Post<SimulationDecisionPreviewSnapshot>(
            client,
            $"/api/simulation/v1/sessions/{session.SessionStableId}/warehouse-put-away-previews",
            putAway);
        Assert.Empty(putAwayPreview.Decision.BlockReasonCodes);
        session = await Post<경영SimulationSessionSnapshot>(
            client,
            $"/api/simulation/v1/sessions/{session.SessionStableId}/warehouse-put-aways/confirm",
            new SimulationWarehousePutAwayConfirmRequest
            {
                CommandId = "command:ui-vertical:put-away",
                ExpectedRevision = putAwayConfirmAction.Invocation!.ExpectedStateRevision!.Value,
                PutAway = PutAway(putAwayConfirmAction.Invocation),
            });

        for (var index = 0; index < 4; index++)
        {
            session = await Tick(client, session, $"command:ui-vertical:put-away-{index + 1}");
            var current = await Get<SimulationWorldUIProjection>(client, projectionRoute);
            if (current.StateCode == "Completed") break;
        }

        var completed = await Get<SimulationWorldUIProjection>(client, projectionRoute);
        Assert.Equal("Completed", completed.StateCode);
        Assert.Equal("완료", completed.StateKoreanLabel);
        Assert.Equal(창고입고상태코드.적재완료, completed.WorkflowStageCode);
        Assert.Contains(completed.InformationItems,
            value => value.InformationKindCode == "Summary"
                && value.ValueText.Contains("적재 완료 1건", StringComparison.Ordinal));
        Assert.Equal(SimulationNpcInventoryStateCodes.PutAwayCompleted,
            Assert.Single(session.NpcFacilityInventories).StateCode);
        Assert.Equal(2, session.NpcWorkRecords.Length);
        Assert.Contains(session.NpcWorkRecords, value =>
            value.ActionCode == SimulationNpcActionCodes.WarehouseStorageMove
            && value.ActorStableId == PyeongchangSimulationNpcStableIds.진부적재담당);
        Assert.Equal("인수완료", Assert.Single(session.FreightTransports).StateCode);
        Assert.False(session.IsOperationalState);
    }

    private static SimulationFreightReceiptPreviewRequest Receipt(
        SimulationWorldUIActionInvocation invocation)
        => new()
        {
            TransportRequestStableId = invocation.TargetStableId!,
            TransportRevision = invocation.TargetRevision!.Value,
            ActorStableId = invocation.ActorStableId!,
            ReceiptDurationTicks = invocation.DurationTicks!.Value,
            SourceStableIds = invocation.SourceStableIds.ToArray(),
        };

    private static SimulationDecisionPreviewRequest CancellationInspection()
        => new()
        {
            DecisionStableId = "decision:http:spatial-cancel",
            DecisionTypeCode = SimulationNpcActionCodes.WarehouseInboundInspection,
            ActorStableId = PyeongchangSimulationNpcStableIds.진부입고검수담당,
            TargetStableIds = new[] { "cargo:http:spatial-cancel" },
            ExpectedEffects = new[]
            {
                new SimulationValueProjection
                {
                    ValueTypeCode = "FreightReceiptQuantity",
                    TargetLedgerStableId = "cargo:http:spatial-cancel",
                    BeforeValue = 0m,
                    Delta = 100m,
                    AfterValue = 100m,
                    UnitCode = "KGM",
                    SourceStableIds = new[] { "source:fixture:http-spatial-cancel" },
                },
            },
            SourceStableIds = new[] { "source:fixture:http-spatial-cancel" },
            Task = new SimulationTaskPlanRequest
            {
                TaskStableId = "task:http:spatial-cancel",
                TaskTypeCode = "FreightReceiptConfirmation",
                FacilityStableId = PyeongchangSimulationWorldStableIds.진부Hub시설,
                ActionCode = SimulationNpcActionCodes.WarehouseInboundInspection,
                AssignedCapacity = 100m,
                AssignedCapacityUnitCode = "KGM",
                DurationTicks = 1,
                InputLotStableIds = new[] { "cargo:http:spatial-cancel" },
                OutputCandidateCodes = new[] { SimulationNpcInventoryStateCodes.StorageEligible },
                SourceStableIds = new[] { "source:fixture:http-spatial-cancel" },
            },
        };

    private static SimulationWarehousePutAwayPreviewRequest PutAway(
        SimulationWorldUIActionInvocation invocation)
        => new()
        {
            InventoryStableId = invocation.TargetStableId!,
            InventoryRevision = invocation.TargetRevision!.Value,
            ActorStableId = invocation.ActorStableId!,
            PutAwayDurationTicks = invocation.DurationTicks!.Value,
            SourceStableIds = invocation.SourceStableIds.ToArray(),
        };

    private static async System.Threading.Tasks.Task<경영SimulationSessionSnapshot> Tick(
        System.Net.Http.HttpClient client,
        경영SimulationSessionSnapshot session,
        string commandId,
        int tickCount = 1)
        => await Post<경영SimulationSessionSnapshot>(
            client,
            $"/api/simulation/v1/sessions/{session.SessionStableId}/ticks",
            new 경영SimulationTick진행Request
            {
                CommandId = commandId,
                ExpectedRevision = session.Revision,
                TickCount = tickCount,
            });

    private static async System.Threading.Tasks.Task<T> Post<T>(
        System.Net.Http.HttpClient client,
        string route,
        object body,
        HttpStatusCode expected = HttpStatusCode.OK)
    {
        using var response = await client.PostAsJsonAsync(route, body);
        Assert.True(response.StatusCode == expected,
            $"expected={expected}, actual={response.StatusCode}, body={await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private static async System.Threading.Tasks.Task<T> Get<T>(
        System.Net.Http.HttpClient client,
        string route)
    {
        using var response = await client.GetAsync(route);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private static SimulationFreightTransportPreviewRequest Freight()
        => new()
        {
            Transport = new SimulationFreightTransportBindingRequest
            {
                TransportRequestStableId = "freight-transport:sim.ui-vertical-potato-1",
                DispatchOfferStableId = "dispatch-offer:sim.ui-vertical-potato-1",
                CarrierCandidateStableId = "carrier-candidate:sim.ui-vertical-1",
                VehicleStableId = "vehicle:sim.ui-vertical-truck-1",
                VehicleCapacity = 400m,
                VehicleCapacityUnitCode = "KGM",
            },
            Movement = new SimulationLogisticsMovementPreviewRequest
            {
                CargoStableId = "cargo:sim.ui-vertical-potato-1",
                CargoRevision = 1,
                SourceAllocationStableId = "allocation:harvest-lot:harvest-lot:ui-vertical-potato-1",
                HarvestLotStableId = "harvest-lot:ui-vertical-potato-1",
                PackageLotStableId = "package-lot:ui-vertical-potato-1",
                ProductStableId = "product:potato",
                Quantity = 300m,
                UnitCode = "KGM",
                RouteStableId = "route:sim:daegwallyeong-jinbu-1",
                OriginFacilityStableId = PyeongchangSimulationWorldStableIds.대관령Farm시설,
                DestinationFacilityStableId = PyeongchangSimulationWorldStableIds.진부Hub시설,
                ActorStableId = "actor:sim:pyeongchang:shipper-1",
                RequiredRouteTicks = 3,
                SourceStableIds = new[]
                {
                    "harvest-lot:ui-vertical-potato-1",
                    "package-lot:ui-vertical-potato-1",
                    "source:fixture:ui-vertical-freight-1",
                },
            },
        };

    private static SimulationHarvestDispositionImpactPreviewRequest HarvestImpact()
        => new()
        {
            DispositionDecisionStableId = "decision:harvest.ui-vertical-source",
            DispositionDecisionRevision = 1,
            HarvestLotStableId = "harvest-lot:ui-vertical-potato-1",
            HarvestLotRevision = 1,
            ProductStableId = "product:potato",
            Quantity = 300m,
            UnitCode = "KGM",
            ChoiceCode = SimulationHarvestDispositionChoiceCodes.CooperativeShipment,
            NextWorkflowCode = SimulationHarvestDispositionWorkflowCodes.CooperativeIntakeCandidate,
            ActorStableId = "actor:sim:pyeongchang:farmer-1",
            SourceStableIds = new[]
            {
                "harvest-lot:ui-vertical-potato-1",
                "source:fixture:ui-vertical-harvest-1",
            },
        };

    private static 경영SimulationSession생성Request CreateSessionRequest()
        => new()
        {
            ClientRequestId = Guid.Parse("123a7841-a58c-491b-b195-6b96efc1f683"),
            ScenarioStableId = "scenario:pyeongchang-farm-hub-town:ui-vertical",
            ScenarioDataRevision = "scenario-data:pyeongchang:ui-vertical:r1",
            ScenarioSeed = 240814,
            RuleRevision = "simulation-world-ui-vertical:r1",
            DurationTicks = 40,
            WorldContext = new SimulationWorldContext생성Request
            {
                FactionStableId = "faction:sim:pyeongchang",
                TerritoryStableId = "territory:sim:pyeongchang",
                SettlementStableId = "settlement:sim:pyeongchang",
                GameDateStartsOn = new DateTimeOffset(2026, 8, 14, 0, 0, 0, TimeSpan.Zero),
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
                    District("district:sim:pyeongchang:farm", "Farm"),
                    District("district:sim:pyeongchang:logistics", "Logistics"),
                    District("district:sim:pyeongchang:market", "Market"),
                    District("district:sim:pyeongchang:storage", "Storage"),
                },
                Facilities = new[]
                {
                    Facility(PyeongchangSimulationWorldStableIds.대관령Farm시설, "FarmPacking", "district:sim:pyeongchang:farm"),
                    Facility(PyeongchangSimulationWorldStableIds.진부Hub시설, "LogisticsHub", "district:sim:pyeongchang:logistics"),
                    Facility("facility:sim:pyeongchang:market", SimulationSettlementFacilityTypeCodes.Market, "district:sim:pyeongchang:market"),
                    Facility("facility:sim:pyeongchang:storage", SimulationSettlementFacilityTypeCodes.Storage, "district:sim:pyeongchang:storage"),
                },
                SourceStableIds = new[] { "source:fixture:ui-vertical-settlement-1" },
            },
            NpcWorkforce = PyeongchangSimulationNpcWorkforceFixture.Create(),
            SpatialWorld = PyeongchangSimulation공간상호작용Fixture.Create(),
        };

    private static SimulationSettlementDistrictRequest District(string stableId, string typeCode)
        => new()
        {
            DistrictStableId = stableId,
            DistrictTypeCode = typeCode,
            SourceStableIds = new[] { "source:fixture:ui-vertical-settlement-1" },
        };

    private static SimulationSettlementFacilityRequest Facility(
        string stableId,
        string typeCode,
        string districtStableId)
        => new()
        {
            FacilityStableId = stableId,
            FacilityTypeCode = typeCode,
            DistrictStableId = districtStableId,
            SourceStableIds = new[] { "source:fixture:ui-vertical-settlement-1" },
        };

    private static WebApplicationFactory<Program> CreateFactory()
        => new SimulationWebApplicationFactory()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureAppConfiguration((_, configuration) =>
                {
                    configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["SsalddelExecution:Mode"] = "Operational",
                        ["SsalddelSimulation:AllowUnauthenticatedTesting"] = "true",
                        ["SimulationSharedPublicData:Enabled"] = "false",
                    });
                });
            });
}
