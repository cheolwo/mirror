using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Ssalddel.Interior.Domain;
using Ssalddel.Simulation.Application;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;
using Ssalddel.Simulation.Infrastructure;
using Ssalddel.WorkflowRules.Contracts;

namespace Ssalddel.Simulation.Tests;

[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
    "Simulation·Unity 계약과 결정성 및 회귀 증거를 검증한다.",
    Boundary = "자동 시험 통과와 실제 Play Mode·Game View·E 승격 증거를 구분한다.",
    SubmoduleKey = Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceSubmoduleKeys.E3로컬원격동등성)]
public sealed class SimulationWorldInteractionFarmSupplyTests
{
    private const string Player = "actor:wi-farm:player";
    private const string FarmFacility = "facility:wi-farm:daegwallyeong";
    private const string MarketFacility = "facility:wi-farm:market";
    private const string CultivationUnit = "cultivation-unit:wi-farm:potato-1";
    private const string PreparationSoil = "soil:wi-farm:preparation-1";
    private const string FarmFence = "defense:wi-farm:fence-1";

    [Theory]
    [InlineData("LocalProcess", true)]
    [InlineData("RemoteHost", true)]
    [InlineData("LocalProcess", false)]
    [InlineData("RemoteHost", false)]
    public void 독립Farm_두주기는_기존Lot과_체력경계를보존하며_SaveReplay된다(
        string 권위위치, bool 처음부터경작된밭)
    {
        var 요청 = CreateRequest();
        요청.Settlement = null;
        요청.NpcWorkforce = null;
        요청.FarmSurvival!.CultivationUnits = Array.Empty<Simulation재배단위Snapshot>();
        if (처음부터경작된밭)
            요청.FarmSurvival.SoilTiles[0].StateCode = SimulationFarmSurvivalCodes.Tilled;
        요청.SpatialWorld!.Definitions = 요청.SpatialWorld.Definitions
            .Where(x => x.FacilityStableId == FarmFacility).ToArray();
        var 저장소 = new InMemory경영SimulationSessionStore();
        var 세션 = 저장소.CreateOrGet(요청);
        var 추적 = new InMemorySimulationPlayableLoopEngineTraceSink();
        var 서비스 = new SimulationFarmSurvivalService(저장소,
            worldInteractionPipeline: new 세계상호작용실행Pipeline(추적),
            authorityLocationCode: 권위위치);
        var 이전Lot = string.Empty;

        void 실행(string 명령, string 대상, string 행위, string 공간, string wi)
        {
            var 전 = 세션.CreateSavePackage(new SimulationSessionSaveRequest
            { SaveStableId = "save:farm:preview", ExpectedRevision = 세션.Revision });
            var 미리보기 = 서비스.PreviewWork(세션.SessionStableId,
                Preview(세션.Revision, 대상, 행위, 공간));
            Assert.True(미리보기.CanConfirm, string.Join(",", 미리보기.BlockingReasonCodes));
            Assert.Equal(전.ReplayHash, 세션.CreateSavePackage(new SimulationSessionSaveRequest
            { SaveStableId = 전.SaveStableId, ExpectedRevision = 세션.Revision }).ReplayHash);
            var 확정요청 = Confirm(명령, 세션.Revision, 대상, 행위, 공간);
            var 확정 = 서비스.ConfirmWork(세션.SessionStableId, 확정요청);
            서비스.ConfirmWork(세션.SessionStableId, 확정요청);
            Assert.Equal(확정.WorldRevision, 세션.Revision);
            var 작업중 = 세션.CreateSavePackage(new SimulationSessionSaveRequest
            { SaveStableId = "save:farm:working:" + 명령, ExpectedRevision = 세션.Revision });
            var 시작기록 = Assert.Single(작업중.ActionManifestationLedger!.TailRecords,
                x => x.CommandId == 명령);
            Assert.DoesNotContain(작업중.PlayerDomainProfile!.기여기록들,
                x => x.SourceActionRecordStableId == 시작기록.행위기록StableId);
            var 복원 = SimulationSessionReplay.Restore(작업중);
            Assert.Equal(작업중.ReplayHash, 복원.CreateSavePackage(new SimulationSessionSaveRequest
            { SaveStableId = 작업중.SaveStableId, ExpectedRevision = 복원.Revision }).ReplayHash);
            세션.Advance(Tick(명령 + ":tick", 세션.Revision));
            복원.Advance(Tick(명령 + ":tick", 복원.Revision));
            var 완료 = 세션.CreateSavePackage(new SimulationSessionSaveRequest
            { SaveStableId = "save:farm:complete:" + 명령, ExpectedRevision = 세션.Revision });
            서비스.ConfirmWork(세션.SessionStableId, 확정요청);
            Assert.Equal(완료.ReplayHash, 세션.CreateSavePackage(new SimulationSessionSaveRequest
            { SaveStableId = 완료.SaveStableId, ExpectedRevision = 세션.Revision }).ReplayHash);
            Assert.Equal(완료.ReplayHash, 복원.CreateSavePackage(new SimulationSessionSaveRequest
            { SaveStableId = 완료.SaveStableId, ExpectedRevision = 복원.Revision }).ReplayHash);
            var 완료기록 = Assert.Single(완료.ActionManifestationLedger!.TailRecords,
                x => x.CommandId == 명령 + ":completed");
            Assert.Equal(wi, 완료기록.WorldInteractionId);
            Assert.Contains(시작기록.행위기록StableId, 완료기록.SourceReferenceIds);
            Assert.Equal(세션.Revision, 완료기록.AfterWorldRevision);
            var 성장 = Assert.Single(완료.PlayerDomainProfile!.기여기록들,
                x => x.SourceActionRecordStableId == 완료기록.행위기록StableId);
            Assert.True(성장.현장숙련도증가량 > 0);
            var 발현 = Assert.Single(완료.WorldInteractionManifestations,
                x => x.OriginCommandId == 명령);
            Assert.Equal(wi, 발현.WorldInteractionId);
            Assert.Equal(SimulationWorldInteractionMaturityStateCodes.Manifested, 발현.StateCode);
            Assert.NotEmpty(발현.ResultStateCodes);
            var 기록 = 추적.Snapshot("playable-loop:farm-crop-cycle.v1", wi, 명령);
            Assert.NotEmpty(기록);
            Assert.All(기록, x => Assert.Equal(권위위치, x.AuthorityLocationCode));
            var 완료복원 = SimulationSessionReplay.Restore(완료);
            Assert.Equal(완료.ReplayHash, 완료복원.CreateSavePackage(new SimulationSessionSaveRequest
            { SaveStableId = 완료.SaveStableId, ExpectedRevision = 완료복원.Revision }).ReplayHash);
        }

        if (!처음부터경작된밭)
            실행("till", PreparationSoil, SimulationFarmSurvivalCodes.Tilling,
                PyeongchangSimulation공간StableIds.대관령Farm밭갈이공간, "WI-FARM-01");
        for (var 주기 = 1; 주기 <= 2; 주기++)
        {
            실행("sow:" + 주기, PreparationSoil, SimulationFarmSurvivalCodes.Sowing,
                PyeongchangSimulation공간StableIds.대관령Farm파종공간, "WI-FARM-02");
            var 재배 = Assert.Single(세션.GetFarmSurvivalState().CultivationUnits,
                x => x.StateCode == Simulation재배단위상태Codes.Growing);
            실행("care:" + 주기, 재배.CultivationUnitStableId, SimulationFarmSurvivalCodes.CropCare,
                PyeongchangSimulation공간StableIds.대관령Farm재배관리공간, "WI-FARM-03");
            if (주기 == 2 && !처음부터경작된밭)
            {
                // 7작업×15는 기존 체력100을 초과한다. 새 회복 규칙을 만들지 않는다.
                var 전 = 세션.CreateSavePackage(new SimulationSessionSaveRequest
                { SaveStableId = "save:farm:stamina", ExpectedRevision = 세션.Revision });
                var 거부 = 서비스.PreviewWork(세션.SessionStableId, Preview(세션.Revision,
                    재배.CultivationUnitStableId, SimulationFarmSurvivalCodes.Harvesting,
                    PyeongchangSimulation공간StableIds.대관령Farm수확공간));
                Assert.Contains("SimulationFarmActorStaminaInsufficient", 거부.BlockingReasonCodes);
                Assert.Throws<SimulationConflictException>(() => 서비스.ConfirmWork(
                    세션.SessionStableId, Confirm("harvest:2", 세션.Revision,
                        재배.CultivationUnitStableId, SimulationFarmSurvivalCodes.Harvesting,
                        PyeongchangSimulation공간StableIds.대관령Farm수확공간)));
                Assert.Equal(전.ReplayHash, 세션.CreateSavePackage(new SimulationSessionSaveRequest
                { SaveStableId = 전.SaveStableId, ExpectedRevision = 세션.Revision }).ReplayHash);
                Assert.Equal(이전Lot, Assert.Single(세션.GetFarmSurvivalState().HarvestLots)
                    .HarvestLotStableId);
                break;
            }
            실행("harvest:" + 주기, 재배.CultivationUnitStableId, SimulationFarmSurvivalCodes.Harvesting,
                PyeongchangSimulation공간StableIds.대관령Farm수확공간, "WI-FARM-04");
            var 상태 = 세션.GetFarmSurvivalState();
            Assert.Equal(주기, 상태.HarvestLots.Length);
            Assert.All(상태.HarvestLots, x => Assert.Equal(300m, x.Quantity));
            if (주기 == 1) 이전Lot = Assert.Single(상태.HarvestLots).HarvestLotStableId;
            else Assert.Contains(상태.HarvestLots, x => x.HarvestLotStableId == 이전Lot);
        }
    }

    [Fact]
    public void WI_FARM_01_02_03은_밭갈이_파종_재배관리를_예약과상태전이로완료한다()
    {
        var session = new 경영SimulationSessionAggregate(CreateRequest());
        var before = session.Snapshot();
        var tillingPreview = session.PreviewFarmWork(Preview(before.Revision,
            PreparationSoil, SimulationFarmSurvivalCodes.Tilling,
            PyeongchangSimulation공간StableIds.대관령Farm밭갈이공간));

        Assert.True(tillingPreview.CanConfirm);
        Assert.Equal(before.Revision, session.Snapshot().Revision);
        Assert.Contains(Simulation공간능력Codes.TillingWorkArea,
            tillingPreview.SpatialInteraction!.RequiredCapabilityCodes);

        var tilled = RunWork(session, "prepare-till", PreparationSoil,
            SimulationFarmSurvivalCodes.Tilling,
            PyeongchangSimulation공간StableIds.대관령Farm밭갈이공간);
        Assert.Equal(SimulationFarmSurvivalCodes.Tilled,
            tilled.FarmSurvival!.SoilTiles.Single(value =>
                value.SoilTileStableId == PreparationSoil).StateCode);

        var sowingPreview = session.PreviewFarmWork(Preview(tilled.Revision,
            PreparationSoil, SimulationFarmSurvivalCodes.Sowing,
            PyeongchangSimulation공간StableIds.대관령Farm파종공간));
        Assert.True(sowingPreview.CanConfirm);
        Assert.Equal(1m, sowingPreview.SeedCost);
        var sowingConfirmed = session.ConfirmFarmWork(Confirm(
            "command:wi-farm:prepare-sow", tilled.Revision, PreparationSoil,
            SimulationFarmSurvivalCodes.Sowing,
            PyeongchangSimulation공간StableIds.대관령Farm파종공간));
        Assert.Equal(1m, sowingConfirmed.ReservedSeedUnits);
        Assert.Equal(1m, sowingConfirmed.SeedUnits);
        var sown = session.Advance(Tick("command:wi-farm:prepare-sow:tick",
            sowingConfirmed.WorldRevision));
        var cultivation = sown.FarmSurvival!.CultivationUnits.Single(value =>
            value.TileStableId == PreparationSoil);
        Assert.Equal(Simulation재배단위상태Codes.Growing, cultivation.StateCode);
        Assert.Equal(0m, sown.FarmSurvival.ReservedSeedUnits);

        var carePreview = session.PreviewFarmWork(Preview(sown.Revision,
            cultivation.CultivationUnitStableId, SimulationFarmSurvivalCodes.CropCare,
            PyeongchangSimulation공간StableIds.대관령Farm재배관리공간));
        Assert.True(carePreview.CanConfirm);
        Assert.Equal(1m, carePreview.WaterCost);
        var cared = RunWork(session, "prepare-care", cultivation.CultivationUnitStableId,
            SimulationFarmSurvivalCodes.CropCare,
            PyeongchangSimulation공간StableIds.대관령Farm재배관리공간);
        Assert.Equal(Simulation재배단위상태Codes.HarvestReady,
            cared.FarmSurvival!.CultivationUnits.Single(value =>
                value.CultivationUnitStableId == cultivation.CultivationUnitStableId).StateCode);
        Assert.Equal(0m, cared.FarmSurvival.ReservedWaterUnits);
        Assert.All(cared.Tasks.Where(value => value.ActionCode ==
                SimulationFarmSurvivalCodes.Tilling
                || value.ActionCode == SimulationFarmSurvivalCodes.Sowing
                || value.ActionCode == SimulationFarmSurvivalCodes.CropCare),
            value => Assert.Equal(SimulationTaskStateCodes.Completed, value.StateCode));
    }

    [Fact]
    public async Task WI_FARM_01_02_03은_Scenario대체없이_Graph근거공간에서완료한다()
    {
        var graphSpatialWorld = await SimulationWorld상호작용GraphTests
            .CreateP1GraphSpatialWorldAsync();
        var graphDefinition = Assert.Single(graphSpatialWorld.Definitions);
        var request = CreateRequest(graphSpatialWorld);
        request.FarmSurvival!.FarmBuildingStableId = graphDefinition.FacilityStableId;
        request.Settlement!.Facilities.Single(value => value.FacilityStableId == FarmFacility)
            .FacilityStableId = graphDefinition.FacilityStableId;
        var session = new 경영SimulationSessionAggregate(request);
        var spatialStableId = graphDefinition.SpatialStableId;

        var current = RunWork(session, "graph-till", PreparationSoil,
            SimulationFarmSurvivalCodes.Tilling, spatialStableId);
        var sowingConfirmed = session.ConfirmFarmWork(Confirm(
            "command:wi-farm:graph-sow", current.Revision, PreparationSoil,
            SimulationFarmSurvivalCodes.Sowing, spatialStableId));
        current = session.Advance(Tick("command:wi-farm:graph-sow:tick",
            sowingConfirmed.WorldRevision));
        var cultivation = current.FarmSurvival!.CultivationUnits.Single(value =>
            value.TileStableId == PreparationSoil);
        current = RunWork(session, "graph-care", cultivation.CultivationUnitStableId,
            SimulationFarmSurvivalCodes.CropCare, spatialStableId);

        Assert.Equal(Simulation재배단위상태Codes.HarvestReady,
            current.FarmSurvival!.CultivationUnits.Single(value =>
                value.CultivationUnitStableId == cultivation.CultivationUnitStableId).StateCode);
        var spatialDefinition = Assert.Single(current.SpatialDefinitions);
        Assert.Equal(Simulation공간근거종류Codes.LandscapeGraph,
            spatialDefinition.EvidenceKindCode);
        Assert.Equal("farm:700-1145", spatialDefinition.LandscapeNodeStableId);
        Assert.All(current.Tasks.Where(value => value.ActionCode ==
                SimulationFarmSurvivalCodes.Tilling
                || value.ActionCode == SimulationFarmSurvivalCodes.Sowing
                || value.ActionCode == SimulationFarmSurvivalCodes.CropCare),
            value => Assert.Equal(SimulationTaskStateCodes.Completed, value.StateCode));
        var saved = session.CreateSavePackage(new SimulationSessionSaveRequest
        {
            SaveStableId = "save:wi-farm:graph-spatial",
            ExpectedRevision = current.Revision,
        });
        var restored = SimulationSessionReplay.Restore(saved);
        var restoredSave = restored.CreateSavePackage(new SimulationSessionSaveRequest
        {
            SaveStableId = saved.SaveStableId,
            ExpectedRevision = restored.Revision,
        });
        Assert.Equal(saved.ReplayHash, restoredSave.ReplayHash);
        Assert.Equal(Simulation공간근거종류Codes.LandscapeGraph,
            Assert.Single(restored.Snapshot().SpatialDefinitions).EvidenceKindCode);
    }

    [Fact]
    public void Farm_Hub_공간모판은_13개_WI_300kg공급선을_SaveReplay까지다시실행한다()
    {
        var spatialWorld = SimulationWorldInteractionSpatialSeedbedTestFixture
            .CreateSpatialWorld();
        var session = new 경영SimulationSessionAggregate(CreateRequest(spatialWorld));

        var tilled = RunWork(session, "seedbed-till", PreparationSoil,
            SimulationFarmSurvivalCodes.Tilling,
            SimulationWorldInteractionSpatialSeedbedTestFixture.ProductionPlot);
        var sown = RunWork(session, "seedbed-sow", PreparationSoil,
            SimulationFarmSurvivalCodes.Sowing,
            SimulationWorldInteractionSpatialSeedbedTestFixture.ProductionPlot);
        var growing = sown.FarmSurvival!.CultivationUnits.Single(value =>
            value.TileStableId == PreparationSoil);
        var cared = RunWork(session, "seedbed-care", growing.CultivationUnitStableId,
            SimulationFarmSurvivalCodes.CropCare,
            SimulationWorldInteractionSpatialSeedbedTestFixture.ProductionPlot);
        Assert.Equal(Simulation재배단위상태Codes.HarvestReady,
            cared.FarmSurvival!.CultivationUnits.Single(value =>
                value.CultivationUnitStableId == growing.CultivationUnitStableId).StateCode);

        var harvested = RunWork(session, "seedbed-harvest", CultivationUnit,
            SimulationFarmSurvivalCodes.Harvesting,
            SimulationWorldInteractionSpatialSeedbedTestFixture.ProductionPlot);
        var harvestLot = Assert.Single(harvested.FarmSurvival!.HarvestLots);
        RunWork(session, "seedbed-collect", harvestLot.HarvestLotStableId,
            SimulationFarmSurvivalCodes.HarvestCollection,
            SimulationWorldInteractionSpatialSeedbedTestFixture.CollectionArea);
        RunHubChoice(session, "seedbed");
        var packed = RunWork(session, "seedbed-pack", harvestLot.HarvestLotStableId,
            SimulationFarmSurvivalCodes.OutboundPacking,
            SimulationWorldInteractionSpatialSeedbedTestFixture.PackingArea);
        var packageLot = Assert.Single(packed.FarmSurvival!.PackageLots);

        var freightRequest = Freight(packageLot);
        freightRequest.Movement.PreferredOriginSpatialStableId =
            SimulationWorldInteractionSpatialSeedbedTestFixture.LoadingArea;
        freightRequest.Movement.PreferredRouteSpatialStableId =
            SimulationWorldInteractionSpatialSeedbedTestFixture.FarmHubCorridor;
        freightRequest.Movement.PreferredDestinationSpatialStableId =
            SimulationWorldInteractionSpatialSeedbedTestFixture.HubUnloading;
        var freightPreview = session.PreviewFreightTransport(freightRequest);
        Assert.Empty(freightPreview.BlockReasonCodes);
        Assert.Equal(packed.Revision, session.Snapshot().Revision);
        var dispatched = session.ConfirmFreightTransport(
            new SimulationFreightTransportConfirmRequest
            {
                CommandId = "command:wi-seedbed:farm-hub",
                ExpectedRevision = packed.Revision,
                Freight = freightRequest,
            });
        var departed = session.Advance(Tick("command:wi-seedbed:depart",
            dispatched.Revision));
        var inTransit = session.Advance(Tick("command:wi-seedbed:route",
            departed.Revision));
        var arrived = session.Advance(Tick("command:wi-seedbed:arrive",
            inTransit.Revision));
        var freight = Assert.Single(arrived.FreightTransports);
        Assert.Equal(화물운송상태코드.하차지도착, freight.StateCode);

        var receipt = new SimulationFreightReceiptPreviewRequest
        {
            TransportRequestStableId = freight.TransportRequestStableId,
            TransportRevision = freight.Revision,
            ActorStableId = PyeongchangSimulationNpcStableIds.진부입고검수담당,
            PreferredSpatialStableId =
                SimulationWorldInteractionSpatialSeedbedTestFixture.HubInspection,
            ReceiptDurationTicks = 1,
            SourceStableIds = new[] { packageLot.CargoStableId },
        };
        Assert.Empty(session.PreviewFreightReceipt(receipt).Decision.BlockReasonCodes);
        var receiptScheduled = session.ConfirmFreightReceipt(
            new SimulationFreightReceiptConfirmRequest
            {
                CommandId = "command:wi-seedbed:receipt",
                ExpectedRevision = arrived.Revision,
                Receipt = receipt,
            });
        var receiptCompleted = AdvanceTicks(session, receiptScheduled, 3,
            "command:wi-seedbed:receipt-tick");
        var inventory = Assert.Single(receiptCompleted.NpcFacilityInventories);
        Assert.Equal(SimulationNpcInventoryStateCodes.StorageEligible, inventory.StateCode);

        var putAway = new SimulationWarehousePutAwayPreviewRequest
        {
            InventoryStableId = inventory.InventoryStableId,
            InventoryRevision = inventory.Revision,
            ActorStableId = PyeongchangSimulationNpcStableIds.진부적재담당,
            PreferredSpatialStableId =
                SimulationWorldInteractionSpatialSeedbedTestFixture.HubStorage,
            PutAwayDurationTicks = 2,
            SourceStableIds = new[] { inventory.InventoryStableId },
        };
        Assert.Empty(session.PreviewWarehousePutAway(putAway).Decision.BlockReasonCodes);
        var putAwayScheduled = session.ConfirmWarehousePutAway(
            new SimulationWarehousePutAwayConfirmRequest
            {
                CommandId = "command:wi-seedbed:put-away",
                ExpectedRevision = receiptCompleted.Revision,
                PutAway = putAway,
            });
        var completed = AdvanceTicks(session, putAwayScheduled, 3,
            "command:wi-seedbed:put-away-tick");

        Assert.Equal(300m, completed.SpatialRuntimeStates.Single(value =>
                value.SpatialStableId ==
                    SimulationWorldInteractionSpatialSeedbedTestFixture.HubStorage)
            .OccupiedCapacities.Single(value =>
                value.CapacityCode == Simulation공간용량Codes.StorageCapacity).Quantity);
        Assert.All(completed.SpatialDefinitions, definition =>
        {
            Assert.Equal(Simulation공간근거종류Codes.Scenario,
                definition.EvidenceKindCode);
            Assert.Contains(definition.SourceStableIds, value =>
                value.StartsWith("wi-spatial-seedbed:", StringComparison.Ordinal));
        });

        var saved = session.CreateSavePackage(new SimulationSessionSaveRequest
        {
            SaveStableId = "save:wi-seedbed:farm-hub",
            ExpectedRevision = completed.Revision,
        });
        var restored = SimulationSessionReplay.Restore(saved);
        var restoredSave = restored.CreateSavePackage(new SimulationSessionSaveRequest
        {
            SaveStableId = saved.SaveStableId,
            ExpectedRevision = restored.Revision,
        });
        Assert.Equal(saved.ReplayHash, restoredSave.ReplayHash);
        Assert.Equal(300m, restored.Snapshot().SpatialRuntimeStates.Single(value =>
                value.SpatialStableId ==
                    SimulationWorldInteractionSpatialSeedbedTestFixture.HubStorage)
            .OccupiedCapacities.Single(value =>
                value.CapacityCode == Simulation공간용량Codes.StorageCapacity).Quantity);
    }

    [Fact]
    public void WI_WORLD_04_시설수리는_수리공간과자재를예약하고_완료후내구도를갱신한다()
    {
        var session = new 경영SimulationSessionAggregate(CreateRequest());
        var before = session.Snapshot();
        var preview = session.PreviewFarmWork(Preview(before.Revision, FarmFence,
            SimulationFarmSurvivalCodes.FenceRepair,
            PyeongchangSimulation공간StableIds.대관령Farm수리공간));

        Assert.True(preview.CanConfirm);
        Assert.Equal(1m, preview.MaterialCost);
        Assert.Contains(Simulation공간능력Codes.RepairWorkArea,
            preview.SpatialInteraction!.RequiredCapabilityCodes);
        Assert.Equal(before.Revision, session.Snapshot().Revision);

        session.ConfirmFarmWork(Confirm(
            "command:wi-world:facility-repair", before.Revision, FarmFence,
            SimulationFarmSurvivalCodes.FenceRepair,
            PyeongchangSimulation공간StableIds.대관령Farm수리공간));
        var confirmed = session.Snapshot();
        Assert.Equal(Simulation공간예약상태Codes.Reserved,
            Assert.Single(confirmed.SpatialReservations).StatusCode);

        var repaired = session.Advance(Tick("command:wi-world:facility-repair:tick",
            confirmed.Revision));
        Assert.Equal(85m, repaired.FarmSurvival!.Defenses.Single(value =>
            value.DefenseStableId == FarmFence).Durability);
        Assert.Equal(Simulation공간예약상태Codes.Released,
            Assert.Single(repaired.SpatialReservations).StatusCode);
        Assert.Contains(repaired.Effects, value =>
            value.EffectTypeCode == "FacilityRepaired");
    }

    [Fact]
    public void WI_FARM_04_05_06은_300kg수확부터_Cargo포장까지_같은계보로완료한다()
    {
        var session = new 경영SimulationSessionAggregate(CreateRequest());
        var before = session.Snapshot();

        var harvestPreview = session.PreviewFarmWork(Preview(
            before.Revision, CultivationUnit, SimulationFarmSurvivalCodes.Harvesting,
            PyeongchangSimulation공간StableIds.대관령Farm수확공간));

        Assert.True(harvestPreview.CanConfirm);
        Assert.Equal(300m, harvestPreview.ProjectedQuantity);
        Assert.Equal("KGM", harvestPreview.ProjectedQuantityUnitCode);
        Assert.Equal(PyeongchangSimulation공간StableIds.대관령Farm수확공간,
            harvestPreview.SpatialInteraction!.SelectedSpatialStableId);
        Assert.Equal(before.Revision, session.Snapshot().Revision);
        Assert.Empty(session.Snapshot().SpatialReservations);

        var harvestConfirmed = session.ConfirmFarmWork(Confirm(
            "command:wi-farm:harvest", before.Revision, CultivationUnit,
            SimulationFarmSurvivalCodes.Harvesting,
            PyeongchangSimulation공간StableIds.대관령Farm수확공간));
        Assert.Equal(Simulation공간예약상태Codes.Reserved,
            Assert.Single(session.Snapshot().SpatialReservations).StatusCode);

        var harvested = session.Advance(Tick("command:wi-farm:harvest:tick",
            harvestConfirmed.WorldRevision));
        var harvestLot = Assert.Single(harvested.FarmSurvival!.HarvestLots);
        Assert.Equal(300m, harvestLot.Quantity);
        Assert.Equal(Simulation수확Lot상태Codes.HarvestedAtField, harvestLot.StateCode);
        Assert.Equal(Simulation재배단위상태Codes.Harvested,
            Assert.Single(harvested.FarmSurvival.CultivationUnits).StateCode);

        var collectionConfirmed = session.ConfirmFarmWork(Confirm(
            "command:wi-farm:collect", harvested.Revision, harvestLot.HarvestLotStableId,
            SimulationFarmSurvivalCodes.HarvestCollection,
            PyeongchangSimulation공간StableIds.대관령Farm집하공간));
        var collected = session.Advance(Tick("command:wi-farm:collect:tick",
            collectionConfirmed.WorldRevision));
        Assert.Equal(Simulation수확Lot상태Codes.CollectedAtYard,
            Assert.Single(collected.FarmSurvival!.HarvestLots).StateCode);

        var choiceApplied = RunHubChoice(session, "wi-farm");
        var packingConfirmed = session.ConfirmFarmWork(Confirm(
            "command:wi-farm:pack", choiceApplied.Revision,
            harvestLot.HarvestLotStableId,
            SimulationFarmSurvivalCodes.OutboundPacking,
            PyeongchangSimulation공간StableIds.대관령Farm포장공간));
        var packed = session.Advance(Tick("command:wi-farm:pack:tick",
            packingConfirmed.WorldRevision));

        var finalHarvestLot = Assert.Single(packed.FarmSurvival!.HarvestLots);
        var packageLot = Assert.Single(packed.FarmSurvival!.PackageLots);
        Assert.Equal(Simulation수확Lot상태Codes.PackedForShipment,
            finalHarvestLot.StateCode);
        Assert.Equal(Simulation포장Lot상태Codes.PreparedForShipment,
            packageLot.StateCode);
        Assert.Equal(finalHarvestLot.HarvestLotStableId, packageLot.HarvestLotStableId);
        Assert.Equal(300m, packageLot.Quantity);
        Assert.Equal("allocation:harvest-lot:" + finalHarvestLot.HarvestLotStableId,
            packageLot.SourceAllocationStableId);
        Assert.StartsWith("cargo:", packageLot.CargoStableId, StringComparison.Ordinal);
        Assert.All(packed.Tasks.Where(value => value.TaskTypeCode == "FarmSupplyWork"),
            value => Assert.Equal(SimulationTaskStateCodes.Completed, value.StateCode));
        Assert.All(packed.SpatialReservations,
            value => Assert.Equal(Simulation공간예약상태Codes.Released, value.StatusCode));
    }

    [Fact]
    public void WI_FARM_04는_공통Pipeline에서_Tick결과와V28행위증거까지완료한다()
    {
        var store = new InMemory경영SimulationSessionStore();
        var session = store.CreateOrGet(CreateRequest());
        var service = new SimulationFarmSurvivalService(store);
        var commandId = "command:wi-farm:pipeline-harvest";

        service.ConfirmWork(session.SessionStableId, Confirm(
            commandId, session.Snapshot().Revision, CultivationUnit,
            SimulationFarmSurvivalCodes.Harvesting,
            PyeongchangSimulation공간StableIds.대관령Farm수확공간));
        var confirmed = session.Snapshot();
        var beforeTick = session.CreateSavePackage(
            new SimulationSessionSaveRequest
            {
                SaveStableId = "save:wi-farm:pipeline-before-tick",
                ExpectedRevision = confirmed.Revision,
            });
        Assert.Equal(SimulationSaveSchemaVersions.V28,
            beforeTick.SchemaVersion);
        var invocation = Assert.Single(beforeTick.CommandLog,
            value => value.WorldInteractionInvocation != null)
            .WorldInteractionInvocation!;
        Assert.Equal(SimulationWI사분면Codes.YangPlayer,
            invocation.음양주체분류.사분면Code);
        Assert.Equal("++", invocation.음양주체분류.사분면기호);
        Assert.Equal(
            SimulationWorldInteractionMaturityStateCodes.ManifestationPartial,
            Assert.Single(beforeTick.WorldInteractionManifestations).StateCode);

        var completed = session.Advance(Tick(
            commandId + ":tick", confirmed.Revision));
        var saved = session.CreateSavePackage(new SimulationSessionSaveRequest
        {
            SaveStableId = "save:wi-farm:pipeline-completed",
            ExpectedRevision = completed.Revision,
        });
        var manifestation = Assert.Single(saved.WorldInteractionManifestations);
        Assert.Equal("WI-FARM-04", manifestation.WorldInteractionId);
        Assert.Equal(
            SimulationWorldInteractionMaturityStateCodes.Manifested,
            manifestation.StateCode);
        Assert.Empty(manifestation.MissingEvidenceCodes);
        Assert.NotEmpty(manifestation.ResultStateCodes);
        Assert.Contains(manifestation.TaskOrEffectReferenceIds, value =>
            value.EndsWith(":production-effect", StringComparison.Ordinal));

        var restored = SimulationSessionReplay.Restore(saved);
        var restoredSave = restored.CreateSavePackage(
            new SimulationSessionSaveRequest
            {
                SaveStableId = saved.SaveStableId,
                ExpectedRevision = restored.Revision,
            });
        Assert.Equal(saved.ReplayHash, restoredSave.ReplayHash);
        Assert.Equal(SimulationWorldInteractionMaturityStateCodes.Manifested,
            Assert.Single(restoredSave.WorldInteractionManifestations).StateCode);

        var tampered = JsonSerializer.Deserialize<SimulationSessionSavePackage>(
            JsonSerializer.Serialize(saved))!;
        Assert.Single(tampered.CommandLog,
                value => value.WorldInteractionInvocation != null)
            .WorldInteractionInvocation!.음양주체분류.사분면기호 = "--";
        // hash 변조와 계약 변조를 구분해, 여기서는 잘못된 WI 분류 계약을
        // 직접 검증한다.
        tampered.ReplayHash = SimulationReplayHasher.Calculate(tampered);
        var error = Assert.Throws<SimulationContractException>(() =>
            SimulationSessionReplay.Restore(tampered));
        Assert.Equal("SimulationSavePackageInvalid", error.Message);
    }

    [Fact]
    public void Hub출고루틴은_NPC가_결정적으로_WI03_04_05를_완료한다()
    {
        var session = new 경영SimulationSessionAggregate(
            CreateHubNpcRoutineRequest());
        var putAwayCompleted = session.Snapshot();
        var inventory = Assert.Single(putAwayCompleted.NpcFacilityInventories);

        var candidate = Assert.Single(session.GetNpcRoutineWork("Hub"));
        Assert.Equal("WI-HUB-03", candidate.WorldInteractionId);
        Assert.Equal(SimulationNpcActionPhaseCodes.Candidate, candidate.PhaseCode);
        Assert.Equal(SimulationWorldInteractionOriginCodes.OperationsDerived,
            candidate.OriginCode);
        Assert.Equal(SimulationWorldInteractionControlPolicyCodes.NpcRoutine,
            candidate.ControlPolicyCode);

        var direct = Assert.Throws<SimulationConflictException>(() =>
            session.ConfirmSupplyChainWork(new SimulationSupplyChainWorkConfirmRequest
            {
                CommandId = "command:wi-hub:direct-forbidden",
                ExpectedRevision = putAwayCompleted.Revision,
                Work = SupplyChainWork(inventory,
                    SimulationSupplyChainActionCodes.WarehouseOutboundFlow,
                    PyeongchangSimulation공간StableIds.진부Hub피킹공간, 2),
            }));
        Assert.Equal("SimulationNpcRoutineDirectControlForbidden", direct.ErrorCode);

        var scheduled = session.Advance(Tick(
            "command:npc-routine:hub-outbound:start", putAwayCompleted.Revision));
        Assert.Equal(SimulationNpcInventoryStateCodes.OutboundRequested,
            Assert.Single(scheduled.NpcFacilityInventories).StateCode);
        var assignment = scheduled.NpcTaskAssignments.Single(value =>
            value.ActionCode == SimulationNpcActionCodes.WarehouseOutboundFlow);
        Assert.Equal(PyeongchangSimulationNpcStableIds.진부출고준비담당,
            assignment.ActorStableId);
        var started = Assert.Single(scheduled.NpcRoutineExecutions);
        Assert.Equal("WI-HUB-03", started.WorldInteractionId);
        Assert.Equal(SimulationWorldInteractionTriggerSourceCodes.NpcDriven,
            started.TriggerSourceCode);

        var picked = session.Advance(Tick(
            "command:npc-routine:hub-outbound:pick", scheduled.Revision));
        Assert.Equal(SimulationNpcInventoryStateCodes.Picked,
            Assert.Single(picked.NpcFacilityInventories).StateCode);
        var completed = session.Advance(Tick(
            "command:npc-routine:hub-outbound:ready", picked.Revision));
        Assert.Equal(SimulationNpcInventoryStateCodes.OutboundReady,
            Assert.Single(completed.NpcFacilityInventories).StateCode);
        Assert.Equal(new[] { "WI-HUB-03", "WI-HUB-04", "WI-HUB-05" },
            completed.NpcRoutineExecutions.Select(value =>
                value.WorldInteractionId).ToArray());
        Assert.Equal(SimulationWorldInteractionTriggerSourceCodes.WorldDerived,
            completed.NpcRoutineExecutions.Single(value =>
                value.WorldInteractionId == "WI-HUB-05").TriggerSourceCode);
        Assert.All(completed.NpcRoutineExecutions.Where(value =>
            value.WorldInteractionId is "WI-HUB-04" or "WI-HUB-05"), value =>
            Assert.Equal(SimulationWI사분면Codes.NotApplicable,
                value.음양주체분류.사분면Code));
        Assert.DoesNotContain(completed.Tasks, value =>
            value.ActionCode == SimulationNpcActionCodes.FreightTransport);
    }

    [Fact]
    public void Hub전체루틴은_감자300KGM을_검수부터_출고준비까지_NPC가완료한다()
    {
        var session = new 경영SimulationSessionAggregate(
            CreateHubNpcFullRoutineRequest());
        var current = session.Snapshot();
        var states = new List<string>
        {
            Assert.Single(current.NpcFacilityInventories).StateCode,
        };
        Assert.Equal("WI-001",
            Assert.Single(session.GetNpcRoutineWork("Hub")).WorldInteractionId);

        for (var tick = 1; tick <= 15; tick++)
        {
            current = session.Advance(Tick(
                "command:npc-routine:hub-full:" + tick, current.Revision));
            states.Add(Assert.Single(current.NpcFacilityInventories).StateCode);
            if (states[^1] == SimulationNpcInventoryStateCodes.OutboundReady)
                break;
        }

        Assert.Equal(SimulationNpcInventoryStateCodes.OutboundReady,
            states[^1]);
        Assert.Contains(SimulationNpcInventoryStateCodes.PendingInspection,
            states);
        Assert.Contains(SimulationNpcInventoryStateCodes.StorageEligible,
            states);
        Assert.Contains(SimulationNpcInventoryStateCodes.PutAwayCompleted,
            states);
        Assert.Contains(SimulationNpcInventoryStateCodes.OutboundRequested,
            states);
        Assert.Contains(SimulationNpcInventoryStateCodes.Picked, states);
        Assert.Equal(300m,
            Assert.Single(current.NpcFacilityInventories).Quantity);
        Assert.Equal("KGM",
            Assert.Single(current.NpcFacilityInventories).UnitCode);
        Assert.Equal(new[]
            {
                "WI-001", "WI-002", "WI-HUB-03", "WI-HUB-04", "WI-HUB-05",
            }, current.NpcRoutineExecutions.Select(value =>
                value.WorldInteractionId).ToArray());
        Assert.Equal(3, current.NpcWorkRecords.Count(value =>
            value.ActionCode is
                SimulationNpcActionCodes.WarehouseInboundInspection
                or SimulationNpcActionCodes.WarehouseStorageMove
                or SimulationNpcActionCodes.WarehouseOutboundFlow));
        Assert.DoesNotContain(current.Tasks, value =>
            value.ActionCode == SimulationNpcActionCodes.FreightTransport);
    }

    [Fact]
    public void Hub정밀배치상태사본은_v22에서_동결되고_같은hash로재생된다()
    {
        var plan = new DeterministicInteriorLayoutEngine().Generate(
            HubWarehouseInteriorGrammar.CreateRequest());
        var request = CreateHubNpcFullRoutineRequest();
        request.InteriorPlanHandles = new[]
        {
            new SimulationInteriorPlanHandleSnapshot
            {
                SchemaVersion = plan.SchemaVersion,
                BuildingPlacementStableId = plan.BuildingPlacementStableId,
                H1StableId = plan.H1StableId,
                InteriorDefinitionRevision = plan.InteriorDefinitionRevision,
                ReferenceCatalogRevision = plan.ReferenceCatalogRevision,
                ReferenceCatalogHashSha256 =
                    plan.ReferenceCatalogHashSha256,
                PlacementControlRuleRevision =
                    plan.PlacementControlRuleRevision,
                VisualMetricCatalogRevision =
                    plan.VisualMetricCatalogRevision,
                VisualMetricCatalogHashSha256 =
                    plan.VisualMetricCatalogHashSha256,
                AdjustmentRevision = plan.AdjustmentRevision,
                InteriorPlacementPlanHashSha256 =
                    plan.InteriorPlacementPlanHashSha256,
            },
        };
        var expectedPlanHash = plan.InteriorPlacementPlanHashSha256;
        var session = new 경영SimulationSessionAggregate(request);

        request.InteriorPlanHandles[0].InteriorPlacementPlanHashSha256 =
            new string('f', 64);
        var retryConflict = Assert.Throws<SimulationConflictException>(() =>
            session.EnsureSameCreationRequest(request));
        Assert.Equal("SimulationCreateRequestPayloadConflict",
            retryConflict.ErrorCode);
        var current = session.Snapshot();
        for (var tick = 1; tick <= 15; tick++)
        {
            current = session.Advance(Tick(
                "command:npc-routine:hub-save-v22:" + tick,
                current.Revision));
            if (Assert.Single(current.NpcFacilityInventories).StateCode
                == SimulationNpcInventoryStateCodes.OutboundReady)
                break;
        }

        var saved = session.CreateSavePackage(new SimulationSessionSaveRequest
        {
            SaveStableId = "save:npc-routine:hub-precision-placement",
            ExpectedRevision = current.Revision,
        });
        Assert.Equal(SimulationSaveSchemaVersions.V23, saved.SchemaVersion);
        Assert.Null(saved.SpatialComposition);
        Assert.Equal(expectedPlanHash, Assert.Single(
            saved.SessionCreateRequest.InteriorPlanHandles)
            .InteriorPlacementPlanHashSha256);
        Assert.Equal(expectedPlanHash, Assert.Single(
            saved.Snapshot.InteriorPlanHandles)
            .InteriorPlacementPlanHashSha256);

        var restored = SimulationSessionReplay.Restore(saved);
        var replayed = restored.CreateSavePackage(
            new SimulationSessionSaveRequest
            {
                SaveStableId = saved.SaveStableId,
                ExpectedRevision = restored.Revision,
            });
        Assert.Equal(saved.ReplayHash, replayed.ReplayHash);
        Assert.Equal(expectedPlanHash, Assert.Single(
            replayed.Snapshot.InteriorPlanHandles)
            .InteriorPlacementPlanHashSha256);
        Assert.Equal(SimulationNpcInventoryStateCodes.OutboundReady,
            Assert.Single(replayed.Snapshot.NpcFacilityInventories).StateCode);
    }

    [Fact]
    public void Hub전체루틴의_적재는_플레이어직접확정을거부한다()
    {
        var session = new 경영SimulationSessionAggregate(
            CreateHubNpcFullRoutineRequest());
        var current = session.Snapshot();
        while (Assert.Single(current.NpcFacilityInventories).StateCode
               != SimulationNpcInventoryStateCodes.StorageEligible)
        {
            current = session.Advance(Tick(
                "command:npc-routine:hub-to-storage:" + current.CurrentTick,
                current.Revision));
        }
        var inventory = Assert.Single(current.NpcFacilityInventories);

        var error = Assert.Throws<SimulationConflictException>(() =>
            session.ConfirmWarehousePutAway(
                new SimulationWarehousePutAwayConfirmRequest
                {
                    CommandId = "command:hub-put-away:player-forbidden",
                    ExpectedRevision = current.Revision,
                    PutAway = new SimulationWarehousePutAwayPreviewRequest
                    {
                        InventoryStableId = inventory.InventoryStableId,
                        InventoryRevision = inventory.Revision,
                        ActorStableId = Player,
                        PreferredSpatialStableId =
                            PyeongchangSimulation공간StableIds.진부Hub창고공간,
                        PutAwayDurationTicks = 2,
                        SourceStableIds = new[] { "source:test.player-direct" },
                    },
                }));
        Assert.Equal("SimulationNpcRoutineDirectControlForbidden",
            error.ErrorCode);
    }

    [Fact]
    public void Hub_NPC루틴_v23은_계보와_음양사분면_hash를_저장재생한다()
    {
        var session = new 경영SimulationSessionAggregate(
            CreateHubNpcRoutineRequest());
        var current = session.Snapshot();
        current = session.Advance(Tick(
            "command:npc-routine-save:start", current.Revision));
        current = session.Advance(Tick(
            "command:npc-routine-save:pick", current.Revision));
        current = session.Advance(Tick(
            "command:npc-routine-save:ready", current.Revision));

        var saved = session.CreateSavePackage(new SimulationSessionSaveRequest
        {
            SaveStableId = "save:npc-routine:hub-outbound",
            ExpectedRevision = current.Revision,
        });
        Assert.Equal(SimulationSaveSchemaVersions.V23, saved.SchemaVersion);
        Assert.Contains(saved.Snapshot.NpcRoutineExecutions, value =>
            value.음양주체분류.사분면Code ==
            SimulationWI사분면Codes.YinNpc
            && value.음양주체분류.사분면기호 == "--");

        var restored = SimulationSessionReplay.Restore(saved);
        var replayed = restored.CreateSavePackage(new SimulationSessionSaveRequest
        {
            SaveStableId = saved.SaveStableId,
            ExpectedRevision = restored.Revision,
        });
        Assert.Equal(saved.ReplayHash, replayed.ReplayHash);
        Assert.Equal(saved.Snapshot.NpcRoutineExecutions.Select(value =>
                value.ExecutionStableId),
            replayed.Snapshot.NpcRoutineExecutions.Select(value =>
                value.ExecutionStableId));

        var legacyV21 = SimulationSaveReplayCloner.ClonePackage(saved);
        legacyV21.SchemaVersion = SimulationSaveSchemaVersions.V21;
        legacyV21.ReplayHash = SimulationReplayHasher.Calculate(legacyV21);
        var restoredV21 = SimulationSessionReplay.Restore(legacyV21);
        Assert.Equal(legacyV21.SavedWorldRevision, restoredV21.Revision);

        var legacyV22 = SimulationSaveReplayCloner.ClonePackage(saved);
        legacyV22.SchemaVersion = SimulationSaveSchemaVersions.V22;
        var v22HashBefore = SimulationReplayHasher.Calculate(legacyV22);
        legacyV22.Snapshot.NpcRoutineExecutions[0]
            .음양주체분류.사분면기호 = "tampered-but-ignored-by-v22";
        Assert.Equal(v22HashBefore,
            SimulationReplayHasher.Calculate(legacyV22));
    }

    [Fact]
    public void Hub_NPC루틴은_정책중지와_담당자부재를_차단상태로보존한다()
    {
        var policyOffSession = new 경영SimulationSessionAggregate(
            CreateHubNpcRoutineRequest());
        var putAway = policyOffSession.Snapshot();
        var policyOff = policyOffSession.UpdateNpcPolicy(
            new SimulationNpcPolicyChangeRequest
            {
                CommandId = "command:npc-routine:policy-off",
                ExpectedRevision = putAway.Revision,
                PolicyStableId = PyeongchangSimulationNpcStableIds.진부출고준비정책,
                AutomationEnabled = false,
                Priority = 80,
                PreferredActorStableId =
                    PyeongchangSimulationNpcStableIds.진부출고준비담당,
                AutoDelegationEnabled = false,
            });
        var stopped = Assert.Single(policyOffSession.GetNpcRoutineWork("Hub"));
        Assert.Equal(SimulationNpcActionPhaseCodes.Blocked, stopped.PhaseCode);
        Assert.Contains("SimulationNpcAutomationDisabled", stopped.BlockReasonCodes);
        var afterStoppedTick = policyOffSession.Advance(Tick(
            "command:npc-routine:policy-off:tick", policyOff.Revision));
        Assert.DoesNotContain(afterStoppedTick.Tasks, value =>
            value.ActionCode == SimulationNpcActionCodes.WarehouseOutboundFlow);

        var noActorRequest = CreateHubNpcRoutineRequest();
        var workforce = noActorRequest.NpcWorkforce!;
        workforce.Actors = workforce.Actors.Where(value => value.ActorStableId !=
            PyeongchangSimulationNpcStableIds.진부출고준비담당).ToArray();
        workforce.CapabilityGrants = workforce.CapabilityGrants.Where(value =>
            value.ActorStableId != PyeongchangSimulationNpcStableIds
                .진부출고준비담당).ToArray();
        workforce.Policies.Single(value => value.PolicyStableId ==
            PyeongchangSimulationNpcStableIds.진부출고준비정책)
            .PreferredActorStableId = string.Empty;
        var noActorSession = new 경영SimulationSessionAggregate(noActorRequest);
        var ready = noActorSession.Snapshot();
        noActorSession.Advance(Tick("command:npc-routine:no-actor:tick",
            ready.Revision));

        var blocked = Assert.Single(noActorSession.GetNpcRoutineWork("Hub"));
        Assert.Equal(SimulationNpcActionPhaseCodes.Blocked, blocked.PhaseCode);
        Assert.Contains("SimulationNpcEligibleActorMissing",
            blocked.BlockReasonCodes);
        Assert.NotEmpty(blocked.TaskStableId);
    }

    [Fact]
    public void Hub_NPC루틴는_완료전_플레이어예외취소를_허용한다()
    {
        var session = new 경영SimulationSessionAggregate(
            CreateHubNpcRoutineRequest());
        var ready = session.Snapshot();
        var scheduled = session.Advance(Tick(
            "command:npc-routine:cancel:start", ready.Revision));
        var task = scheduled.Tasks.Single(value =>
            value.ActionCode == SimulationNpcActionCodes.WarehouseOutboundFlow);
        var projection = Assert.Single(session.GetNpcRoutineWork("Hub"));
        Assert.Contains(
            SimulationNpcRoutinePlayerInterventionCodes.CancelBeforeCompletion,
            projection.AllowedPlayerInterventionCodes);

        var cancelled = session.CancelTask(task.TaskStableId,
            new SimulationTaskCancelRequest
            {
                CommandId = "command:npc-routine:cancel",
                ExpectedRevision = scheduled.Revision,
                ReasonCode = "PlayerExceptionIntervention",
            });
        Assert.Equal(SimulationTaskStateCodes.Cancelled,
            cancelled.Tasks.Single(value => value.TaskStableId ==
                task.TaskStableId).StateCode);
        Assert.Equal(SimulationNpcInventoryStateCodes.PutAwayCompleted,
            Assert.Single(cancelled.NpcFacilityInventories).StateCode);
    }

    [Fact]
    public void 감자300kg은_Hub출고와마트진열까지_공간예약과상태계보를보존한다()
    {
        var session = new 경영SimulationSessionAggregate(CreateRequest());
        var harvested = RunWork(session, "chain-harvest", CultivationUnit,
            SimulationFarmSurvivalCodes.Harvesting,
            PyeongchangSimulation공간StableIds.대관령Farm수확공간);
        var harvestLotId = Assert.Single(harvested.FarmSurvival!.HarvestLots)
            .HarvestLotStableId;
        RunWork(session, "chain-collect", harvestLotId,
            SimulationFarmSurvivalCodes.HarvestCollection,
            PyeongchangSimulation공간StableIds.대관령Farm집하공간);
        RunHubChoice(session, "chain");
        var packed = RunWork(session, "chain-pack", harvestLotId,
            SimulationFarmSurvivalCodes.OutboundPacking,
            PyeongchangSimulation공간StableIds.대관령Farm포장공간);
        var packageLot = Assert.Single(packed.FarmSurvival!.PackageLots);

        var freightRequest = Freight(packageLot);
        var freightPreview = session.PreviewFreightTransport(freightRequest);
        Assert.Empty(freightPreview.BlockReasonCodes);
        Assert.Equal(3, freightPreview.LogisticsMovement.CommonDecisionPreview
            .SpatialInteraction!.RoleBindings.Length);
        Assert.Equal(packed.Revision, session.Snapshot().Revision);

        var freightConfirm = new SimulationFreightTransportConfirmRequest
        {
            CommandId = "command:wi-log:farm-hub",
            ExpectedRevision = packed.Revision,
            Freight = freightRequest,
        };
        var dispatched = session.ConfirmFreightTransport(freightConfirm);
        var dispatchedRetry = session.ConfirmFreightTransport(freightConfirm);
        Assert.Equal(dispatched.Revision, dispatchedRetry.Revision);
        var movementTask = dispatched.Tasks.Single(value =>
            value.TaskTypeCode == "CargoRouteMovement");
        Assert.Equal(3, movementTask.SpatialRoleBindings.Length);
        var movementReservations = dispatched.SpatialReservations.Where(value =>
            value.TaskStableId == movementTask.TaskStableId).ToArray();
        Assert.Equal(2, movementReservations.Length);
        Assert.Contains(movementReservations, value => value.RoleCode ==
            Simulation공간역할Codes.OriginLoading);
        Assert.Contains(movementReservations, value => value.RoleCode ==
            Simulation공간역할Codes.DestinationUnloading);

        var departed = session.Advance(Tick("command:wi-log:depart", dispatched.Revision));
        Assert.Equal(SimulationLogisticsMovementStateCodes.InTransit,
            Assert.Single(departed.LogisticsMovements).StateCode);
        Assert.Equal(Simulation공간예약상태Codes.Released,
            departed.SpatialReservations.Single(value => value.TaskStableId ==
                movementTask.TaskStableId && value.RoleCode ==
                Simulation공간역할Codes.OriginLoading).StatusCode);
        Assert.Equal(Simulation공간예약상태Codes.Reserved,
            departed.SpatialReservations.Single(value => value.TaskStableId ==
                movementTask.TaskStableId && value.RoleCode ==
                Simulation공간역할Codes.DestinationUnloading).StatusCode);

        var inTransit = session.Advance(Tick("command:wi-log:route", departed.Revision));
        var arrived = session.Advance(Tick("command:wi-log:arrive", inTransit.Revision));
        var freight = Assert.Single(arrived.FreightTransports);
        Assert.Equal(화물운송상태코드.하차지도착, freight.StateCode);
        Assert.All(arrived.SpatialReservations.Where(value => value.TaskStableId ==
            movementTask.TaskStableId), value =>
            Assert.Equal(Simulation공간예약상태Codes.Released, value.StatusCode));

        var receipt = new SimulationFreightReceiptPreviewRequest
        {
            TransportRequestStableId = freight.TransportRequestStableId,
            TransportRevision = freight.Revision,
            ActorStableId = PyeongchangSimulationNpcStableIds.진부입고검수담당,
            PreferredSpatialStableId = PyeongchangSimulation공간StableIds.진부Hub검수공간,
            ReceiptDurationTicks = 1,
            SourceStableIds = new[] { packageLot.CargoStableId },
        };
        var receiptPreview = session.PreviewFreightReceipt(receipt);
        Assert.Empty(receiptPreview.Decision.BlockReasonCodes);
        Assert.Equal(PyeongchangSimulation공간StableIds.진부Hub검수공간,
            receiptPreview.SpatialInteraction!.SelectedSpatialStableId);
        var receiptScheduled = session.ConfirmFreightReceipt(
            new SimulationFreightReceiptConfirmRequest
            {
                CommandId = "command:wi-hub:receipt",
                ExpectedRevision = arrived.Revision,
                Receipt = receipt,
            });
        var receiptCompleted = AdvanceTicks(session, receiptScheduled, 3,
            "command:wi-hub:receipt-tick");
        var inventory = Assert.Single(receiptCompleted.NpcFacilityInventories);
        Assert.Equal(SimulationNpcInventoryStateCodes.StorageEligible, inventory.StateCode);
        Assert.Equal(300m, inventory.Quantity);

        var putAway = new SimulationWarehousePutAwayPreviewRequest
        {
            InventoryStableId = inventory.InventoryStableId,
            InventoryRevision = inventory.Revision,
            ActorStableId = PyeongchangSimulationNpcStableIds.진부적재담당,
            PreferredSpatialStableId = PyeongchangSimulation공간StableIds.진부Hub창고공간,
            PutAwayDurationTicks = 2,
            SourceStableIds = new[] { inventory.InventoryStableId },
        };
        var putAwayPreview = session.PreviewWarehousePutAway(putAway);
        Assert.Empty(putAwayPreview.Decision.BlockReasonCodes);
        Assert.Equal(PyeongchangSimulation공간StableIds.진부Hub창고공간,
            putAwayPreview.SpatialInteraction!.SelectedSpatialStableId);
        var putAwayScheduled = session.ConfirmWarehousePutAway(
            new SimulationWarehousePutAwayConfirmRequest
            {
                CommandId = "command:wi-hub:put-away",
                ExpectedRevision = receiptCompleted.Revision,
                PutAway = putAway,
            });
        var completed = AdvanceTicks(session, putAwayScheduled, 3,
            "command:wi-hub:put-away-tick");

        Assert.Equal(SimulationNpcInventoryStateCodes.PutAwayCompleted,
            Assert.Single(completed.NpcFacilityInventories).StateCode);
        var warehouse = completed.SpatialRuntimeStates.Single(value =>
            value.SpatialStableId == PyeongchangSimulation공간StableIds.진부Hub창고공간);
        Assert.Equal(300m, warehouse.OccupiedCapacities.Single(value =>
            value.CapacityCode == Simulation공간용량Codes.StorageCapacity).Quantity);
        Assert.Equal(0m, warehouse.ReservedCapacities.Single(value =>
            value.CapacityCode == Simulation공간용량Codes.StorageCapacity).Quantity);

        inventory = completed.NpcFacilityInventories.Single(value =>
            value.InventoryStableId == inventory.InventoryStableId);
        var outbound = SupplyChainWork(inventory,
            SimulationSupplyChainActionCodes.WarehouseOutboundFlow,
            PyeongchangSimulation공간StableIds.진부Hub피킹공간, 2);
        var outboundPreview = session.PreviewSupplyChainWork(outbound);
        Assert.Empty(outboundPreview.Decision.BlockReasonCodes);
        Assert.Equal(completed.Revision, session.Snapshot().Revision);
        var outboundScheduled = session.ConfirmSupplyChainWork(
            new SimulationSupplyChainWorkConfirmRequest
            {
                CommandId = "command:wi-hub:outbound",
                ExpectedRevision = completed.Revision,
                Work = outbound,
            });
        Assert.Equal(SimulationNpcInventoryStateCodes.OutboundRequested,
            outboundScheduled.NpcFacilityInventories.Single(value =>
                value.InventoryStableId == inventory.InventoryStableId).StateCode);
        var picked = session.Advance(Tick("command:wi-hub:picking-tick",
            outboundScheduled.Revision));
        Assert.Equal(SimulationNpcInventoryStateCodes.Picked,
            picked.NpcFacilityInventories.Single(value =>
                value.InventoryStableId == inventory.InventoryStableId).StateCode);
        var outboundReady = session.Advance(Tick("command:wi-hub:outbound-ready-tick",
            picked.Revision));
        var hubInventory = outboundReady.NpcFacilityInventories.Single(value =>
            value.InventoryStableId == inventory.InventoryStableId);
        Assert.Equal(SimulationNpcInventoryStateCodes.OutboundReady,
            hubInventory.StateCode);
        var outboundAllocation = outboundReady.Settlement!.HarvestLotAllocations
            .Single(value => value.AllocationStableId ==
                "allocation:warehouse-outbound:" + inventory.InventoryStableId);
        Assert.Equal(300m, outboundAllocation.AvailableQuantity);

        var marketFreightRequest = HubMarketFreight(hubInventory, outboundAllocation);
        Assert.Empty(session.PreviewFreightTransport(marketFreightRequest).BlockReasonCodes);
        var marketDispatched = session.ConfirmFreightTransport(
            new SimulationFreightTransportConfirmRequest
            {
                CommandId = "command:wi-market:transport",
                ExpectedRevision = outboundReady.Revision,
                Freight = marketFreightRequest,
            });
        var marketArrived = AdvanceTicks(session, marketDispatched, 3,
            "command:wi-market:transport-tick");
        var marketFreight = marketArrived.FreightTransports.Single(value =>
            value.TransportRequestStableId ==
                marketFreightRequest.Transport.TransportRequestStableId);
        Assert.Equal(화물운송상태코드.하차지도착, marketFreight.StateCode);

        var marketReceipt = new SimulationFreightReceiptPreviewRequest
        {
            TransportRequestStableId = marketFreight.TransportRequestStableId,
            TransportRevision = marketFreight.Revision,
            ActorStableId = PyeongchangSimulationNpcStableIds.진부입고검수담당,
            PreferredSpatialStableId =
                PyeongchangSimulation공간StableIds.평창Town마트하차공간,
            ReceiptDurationTicks = 1,
            SourceStableIds = new[] { marketFreight.CargoStableId },
        };
        Assert.Empty(session.PreviewFreightReceipt(marketReceipt).Decision.BlockReasonCodes);
        var marketReceiptScheduled = session.ConfirmFreightReceipt(
            new SimulationFreightReceiptConfirmRequest
            {
                CommandId = "command:wi-market:receipt",
                ExpectedRevision = marketArrived.Revision,
                Receipt = marketReceipt,
            });
        var marketReceived = session.Advance(Tick("command:wi-market:receipt-tick",
            marketReceiptScheduled.Revision));
        var marketInventory = marketReceived.NpcFacilityInventories.Single(value =>
            value.FacilityStableId == MarketFacility);
        Assert.Equal(SimulationNpcInventoryStateCodes.MarketReceived,
            marketInventory.StateCode);
        Assert.Equal("product:potato", marketInventory.ProductStableId);

        var inspected = RunSupplyChainWork(session, marketInventory,
            SimulationSupplyChainActionCodes.MarketInspection,
            PyeongchangSimulation공간StableIds.평창Town마트검수공간,
            "market-inspection");
        marketInventory = inspected.NpcFacilityInventories.Single(value =>
            value.FacilityStableId == MarketFacility);
        Assert.Equal(SimulationNpcInventoryStateCodes.MarketStorageEligible,
            marketInventory.StateCode);
        var backroom = RunSupplyChainWork(session, marketInventory,
            SimulationSupplyChainActionCodes.MarketBackroomPutAway,
            PyeongchangSimulation공간StableIds.평창Town마트후방공간,
            "market-backroom");
        marketInventory = backroom.NpcFacilityInventories.Single(value =>
            value.FacilityStableId == MarketFacility);
        Assert.Equal(SimulationNpcInventoryStateCodes.MarketBackroomStored,
            marketInventory.StateCode);
        var displayed = RunSupplyChainWork(session, marketInventory,
            SimulationSupplyChainActionCodes.MarketDisplayReplenishment,
            PyeongchangSimulation공간StableIds.평창Town마트진열공간,
            "market-display");
        Assert.Equal(SimulationNpcInventoryStateCodes.Displayed,
            displayed.NpcFacilityInventories.Single(value =>
                value.FacilityStableId == MarketFacility).StateCode);

        var saved = session.CreateSavePackage(new SimulationSessionSaveRequest
        {
            SaveStableId = "save:wi:farm-hub-chain",
            ExpectedRevision = displayed.Revision,
        });
        var restored = SimulationSessionReplay.Restore(saved);
        var restoredSave = restored.CreateSavePackage(new SimulationSessionSaveRequest
        {
            SaveStableId = saved.SaveStableId,
            ExpectedRevision = restored.Revision,
        });
        Assert.Equal(saved.ReplayHash, restoredSave.ReplayHash);
        Assert.Equal(300m, restored.Snapshot().SpatialRuntimeStates.Single(value =>
            value.SpatialStableId == PyeongchangSimulation공간StableIds.진부Hub창고공간)
            .OccupiedCapacities.Single(value => value.CapacityCode ==
                Simulation공간용량Codes.StorageCapacity).Quantity);
    }

    [Fact]
    public async System.Threading.Tasks.Task NPC루틴조회는_Hosted_HTTP와_같은계약을사용한다()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var request = CreateRequest(npcRoutineControlRevision:
            SimulationNpcRoutineControlRevisionCodes.R1);
        request.ClientRequestId = Guid.Parse(
            "E760D074-88B1-4B31-94F3-A6080CE16822");
        var created = await Post<경영SimulationSessionSnapshot>(client,
            "/api/simulation/v1/sessions", request, HttpStatusCode.Created);

        var projection = await Get<SimulationNpcRoutineWorkProjection[]>(client,
            $"/api/simulation/v1/sessions/{created.SessionStableId}"
            + "/npc-routine-work?areaCode=Hub");

        Assert.Empty(projection);
        Assert.Equal(SimulationNpcRoutineControlRevisionCodes.R1,
            created.NpcRoutineControlRevision);
        Assert.False(created.IsOperationalState);
    }

    [Fact]
    public async System.Threading.Tasks.Task SoloLocalProcess도_같은_NPC루틴_Core와조회계약을사용한다()
    {
        using var runtime = new LocalSimulationRuntime(
            new InMemory경영SimulationSessionStore(),
            new InMemorySimulationSessionSaveStore(),
            new 사용하지않는NpcRoutineLocalSaveSlotStore());
        var created = await runtime.Sessions.CreateAsync(
            CreateHubNpcRoutineRequest());

        var before = await runtime.Sessions.GetNpcRoutineWorkAsync(
            created.SessionStableId, "Hub");
        var advanced = await runtime.Sessions.AdvanceWorldTickAsync(
            created.SessionStableId,
            Tick("command:local:npc-routine:start", created.Revision));
        var after = await runtime.Sessions.GetNpcRoutineWorkAsync(
            created.SessionStableId, "Hub");

        var hostedStore = new InMemory경영SimulationSessionStore();
        var hosted = new 경영SimulationSessionService(hostedStore,
            new InMemorySimulationSessionSaveStore());
        var hostedCreated = hosted.Create(CreateHubNpcRoutineRequest());
        hosted.Advance(hostedCreated.SessionStableId,
            Tick("command:hosted:npc-routine:start",
                hostedCreated.Revision));
        var hostedAfter = hosted.GetNpcRoutineWork(
            hostedCreated.SessionStableId, "Hub");

        Assert.Equal(SimulationAuthorityLocation.LocalProcess,
            runtime.Descriptor.AuthorityLocation);
        Assert.False(runtime.Descriptor.RequiresNetwork);
        Assert.Equal(SimulationNpcActionPhaseCodes.Candidate,
            Assert.Single(before).PhaseCode);
        Assert.Equal(SimulationNpcInventoryStateCodes.OutboundRequested,
            Assert.Single(advanced.NpcFacilityInventories).StateCode);
        var localProjection = Assert.Single(after);
        var hostedProjection = Assert.Single(hostedAfter);
        Assert.Equal("WI-HUB-03", localProjection.WorldInteractionId);
        Assert.Equal(localProjection.음양주체분류.사분면Code,
            hostedProjection.음양주체분류.사분면Code);
        Assert.Equal(localProjection.음양주체분류.사분면기호,
            hostedProjection.음양주체분류.사분면기호);
        Assert.Equal(SimulationWI사분면Codes.YinNpc,
            localProjection.음양주체분류.사분면Code);
        Assert.Equal("--", localProjection.음양주체분류.사분면기호);
    }

    [Fact]
    public async System.Threading.Tasks.Task 감자생산부터_Hub보관까지_시험HTTP경계를왕복한다()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var current = await Post<경영SimulationSessionSnapshot>(client,
            "/api/simulation/v1/sessions", CreateRequest(), HttpStatusCode.Created);

        current = await RunFarmWork(client, current, "http-harvest", CultivationUnit,
            SimulationFarmSurvivalCodes.Harvesting,
            PyeongchangSimulation공간StableIds.대관령Farm수확공간);
        var harvestLotId = Assert.Single(current.FarmSurvival!.HarvestLots)
            .HarvestLotStableId;
        current = await RunFarmWork(client, current, "http-collect", harvestLotId,
            SimulationFarmSurvivalCodes.HarvestCollection,
            PyeongchangSimulation공간StableIds.대관령Farm집하공간);
        current = await RunHubChoiceHttp(client, current, "http");
        current = await RunFarmWork(client, current, "http-pack", harvestLotId,
            SimulationFarmSurvivalCodes.OutboundPacking,
            PyeongchangSimulation공간StableIds.대관령Farm포장공간);
        var packageLot = Assert.Single(current.FarmSurvival!.PackageLots);

        var freight = Freight(packageLot);
        var freightPreview = await Post<SimulationFreightTransportPreviewSnapshot>(client,
            $"/api/simulation/v1/sessions/{current.SessionStableId}/freight-transport-previews",
            freight);
        Assert.Empty(freightPreview.BlockReasonCodes);
        Assert.Equal(3, freightPreview.LogisticsMovement.CommonDecisionPreview
            .SpatialInteraction!.RoleBindings.Length);
        current = await Post<경영SimulationSessionSnapshot>(client,
            $"/api/simulation/v1/sessions/{current.SessionStableId}/freight-transports/confirm",
            new SimulationFreightTransportConfirmRequest
            {
                CommandId = "command:wi-http:freight",
                ExpectedRevision = current.Revision,
                Freight = freight,
            });
        current = await TickHttp(client, current, "command:wi-http:freight-tick", 3);
        var arrivedFreight = Assert.Single(current.FreightTransports);

        var receipt = new SimulationFreightReceiptPreviewRequest
        {
            TransportRequestStableId = arrivedFreight.TransportRequestStableId,
            TransportRevision = arrivedFreight.Revision,
            ActorStableId = PyeongchangSimulationNpcStableIds.진부입고검수담당,
            PreferredSpatialStableId = PyeongchangSimulation공간StableIds.진부Hub검수공간,
            ReceiptDurationTicks = 1,
            SourceStableIds = new[] { packageLot.CargoStableId },
        };
        var receiptPreview = await Post<SimulationDecisionPreviewSnapshot>(client,
            $"/api/simulation/v1/sessions/{current.SessionStableId}/freight-receipt-previews",
            receipt);
        Assert.Empty(receiptPreview.Decision.BlockReasonCodes);
        current = await Post<경영SimulationSessionSnapshot>(client,
            $"/api/simulation/v1/sessions/{current.SessionStableId}/freight-receipts/confirm",
            new SimulationFreightReceiptConfirmRequest
            {
                CommandId = "command:wi-http:receipt",
                ExpectedRevision = current.Revision,
                Receipt = receipt,
            });
        current = await TickHttp(client, current, "command:wi-http:receipt-tick", 3);
        var inventory = Assert.Single(current.NpcFacilityInventories);

        var putAway = new SimulationWarehousePutAwayPreviewRequest
        {
            InventoryStableId = inventory.InventoryStableId,
            InventoryRevision = inventory.Revision,
            ActorStableId = PyeongchangSimulationNpcStableIds.진부적재담당,
            PreferredSpatialStableId = PyeongchangSimulation공간StableIds.진부Hub창고공간,
            PutAwayDurationTicks = 2,
            SourceStableIds = new[] { inventory.InventoryStableId },
        };
        var putAwayPreview = await Post<SimulationDecisionPreviewSnapshot>(client,
            $"/api/simulation/v1/sessions/{current.SessionStableId}/warehouse-put-away-previews",
            putAway);
        Assert.Empty(putAwayPreview.Decision.BlockReasonCodes);
        current = await Post<경영SimulationSessionSnapshot>(client,
            $"/api/simulation/v1/sessions/{current.SessionStableId}/warehouse-put-aways/confirm",
            new SimulationWarehousePutAwayConfirmRequest
            {
                CommandId = "command:wi-http:put-away",
                ExpectedRevision = current.Revision,
                PutAway = putAway,
            });
        current = await TickHttp(client, current, "command:wi-http:put-away-tick", 3);

        Assert.Equal(SimulationNpcInventoryStateCodes.PutAwayCompleted,
            Assert.Single(current.NpcFacilityInventories).StateCode);
        Assert.Equal(300m, current.SpatialRuntimeStates.Single(value =>
            value.SpatialStableId == PyeongchangSimulation공간StableIds.진부Hub창고공간)
            .OccupiedCapacities.Single(value => value.CapacityCode ==
                Simulation공간용량Codes.StorageCapacity).Quantity);

        inventory = current.NpcFacilityInventories.Single(value =>
            value.InventoryStableId == inventory.InventoryStableId);
        var outbound = SupplyChainWork(inventory,
            SimulationSupplyChainActionCodes.WarehouseOutboundFlow,
            PyeongchangSimulation공간StableIds.진부Hub피킹공간, 2);
        var outboundPreview = await Post<SimulationDecisionPreviewSnapshot>(client,
            $"/api/simulation/v1/sessions/{current.SessionStableId}/supply-chain-work-previews",
            outbound);
        Assert.Empty(outboundPreview.Decision.BlockReasonCodes);
        current = await Post<경영SimulationSessionSnapshot>(client,
            $"/api/simulation/v1/sessions/{current.SessionStableId}/supply-chain-works/confirm",
            new SimulationSupplyChainWorkConfirmRequest
            {
                CommandId = "command:wi-http:hub-outbound",
                ExpectedRevision = current.Revision,
                Work = outbound,
            });
        current = await TickHttp(client, current,
            "command:wi-http:hub-outbound-tick", 2);
        Assert.Equal(SimulationNpcInventoryStateCodes.OutboundReady,
            current.NpcFacilityInventories.Single(value =>
                value.InventoryStableId == inventory.InventoryStableId).StateCode);

        var save = await Post<SimulationSessionSavePackage>(client,
            $"/api/simulation/v1/sessions/{current.SessionStableId}/saves",
            new SimulationSessionSaveRequest
            {
                SaveStableId = "save:wi:http:farm-hub",
                ExpectedRevision = current.Revision,
            });
        Assert.False(string.IsNullOrWhiteSpace(save.ReplayHash));
        Assert.Equal(current.Revision, save.Snapshot.Revision);
        Assert.Equal(SimulationSaveSchemaVersions.V28, save.SchemaVersion);
        Assert.Contains(save.WorldInteractionManifestations, value =>
            value.WorldInteractionId == "WI-FARM-04");
        Assert.Contains(save.WorldInteractionManifestations, value =>
            value.WorldInteractionId == "WI-FARM-05");
        Assert.Contains(save.WorldInteractionManifestations, value =>
            value.WorldInteractionId == "WI-FARM-06");
    }

    [Fact]
    public void 수확Preview는_행위자공간능력과_선호공간을_임의대체하지않는다()
    {
        var request = CreateRequest();
        request.FarmSurvival!.Actors[0].CapabilityCodes = Array.Empty<string>();
        var missingActorCapability = new 경영SimulationSessionAggregate(request);

        var actorBlocked = missingActorCapability.PreviewFarmWork(Preview(
            0, CultivationUnit, SimulationFarmSurvivalCodes.Harvesting,
            PyeongchangSimulation공간StableIds.대관령Farm수확공간));

        Assert.False(actorBlocked.CanConfirm);
        Assert.Contains("SimulationFarmActorCapabilityMissing",
            actorBlocked.BlockingReasonCodes);

        var session = new 경영SimulationSessionAggregate(CreateRequest());
        var wrongPreferred = session.PreviewFarmWork(Preview(
            0, CultivationUnit, SimulationFarmSurvivalCodes.Harvesting,
            PyeongchangSimulation공간StableIds.진부Hub검수공간));

        Assert.False(wrongPreferred.CanConfirm);
        Assert.Contains(Simulation공간차단Codes.DefinitionUnavailable,
            wrongPreferred.BlockingReasonCodes);
        Assert.Equal(string.Empty,
            wrongPreferred.SpatialInteraction!.SelectedSpatialStableId);
    }

    [Fact]
    public void 수확작업취소는_자기공간예약과행위자배정만_반환한다()
    {
        var session = new 경영SimulationSessionAggregate(CreateRequest());
        var confirmed = session.ConfirmFarmWork(Confirm(
            "command:wi-farm:harvest-cancel", 0, CultivationUnit,
            SimulationFarmSurvivalCodes.Harvesting,
            PyeongchangSimulation공간StableIds.대관령Farm수확공간));
        var task = confirmed.WorkOrders.Single().WorkOrderStableId;

        var cancelled = session.CancelTask(task, new SimulationTaskCancelRequest
        {
            CommandId = "command:wi-farm:harvest-cancel:cancel",
            ExpectedRevision = confirmed.WorldRevision,
            ReasonCode = "PlayerChangedPlan",
        });

        Assert.Equal(SimulationTaskStateCodes.Cancelled,
            cancelled.Tasks.Single(value => value.TaskStableId == task).StateCode);
        Assert.Equal(SimulationTaskStateCodes.Cancelled,
            cancelled.FarmSurvival!.WorkOrders.Single().StatusCode);
        Assert.Equal(string.Empty, cancelled.FarmSurvival.Actors.Single().ActiveWorkOrderStableId);
        Assert.Equal(Simulation공간예약상태Codes.Cancelled,
            Assert.Single(cancelled.SpatialReservations).StatusCode);
        Assert.Equal(0m, cancelled.SpatialRuntimeStates.Single(value =>
            value.SpatialStableId == PyeongchangSimulation공간StableIds.대관령Farm수확공간)
            .ReservedCapacities.Single().Quantity);
    }

    [Fact]
    public void 수확확정은_명령멱등성과_예상개정충돌을보존한다()
    {
        var session = new 경영SimulationSessionAggregate(CreateRequest());
        var request = Confirm("command:wi-farm:idempotent", 0, CultivationUnit,
            SimulationFarmSurvivalCodes.Harvesting,
            PyeongchangSimulation공간StableIds.대관령Farm수확공간);

        var first = session.ConfirmFarmWork(request);
        var retried = session.ConfirmFarmWork(request);
        Assert.Equal(first.WorldRevision, retried.WorldRevision);
        Assert.Single(session.Snapshot().Tasks);
        Assert.Single(session.Snapshot().SpatialReservations);

        var payloadConflict = Assert.Throws<SimulationConflictException>(() =>
            session.ConfirmFarmWork(Confirm("command:wi-farm:idempotent", 0,
                CultivationUnit, SimulationFarmSurvivalCodes.Harvesting,
                PyeongchangSimulation공간StableIds.대관령Farm집하공간)));
        Assert.Equal("SimulationCommandPayloadConflict", payloadConflict.ErrorCode);

        var revisionConflict = Assert.Throws<SimulationConflictException>(() =>
            session.ConfirmFarmWork(Confirm("command:wi-farm:stale", 0,
                CultivationUnit, SimulationFarmSurvivalCodes.Harvesting,
                PyeongchangSimulation공간StableIds.대관령Farm수확공간)));
        Assert.Equal("SimulationExpectedRevisionMismatch", revisionConflict.ErrorCode);
    }

    [Fact]
    public void 수확집하포장_SaveReplay는_같은상태와hash를재현한다()
    {
        var session = new 경영SimulationSessionAggregate(CreateRequest());
        var harvest = RunWork(session, "save-harvest", CultivationUnit,
            SimulationFarmSurvivalCodes.Harvesting,
            PyeongchangSimulation공간StableIds.대관령Farm수확공간);
        var harvestLotId = Assert.Single(harvest.FarmSurvival!.HarvestLots).HarvestLotStableId;
        RunWork(session, "save-collect", harvestLotId,
            SimulationFarmSurvivalCodes.HarvestCollection,
            PyeongchangSimulation공간StableIds.대관령Farm집하공간);
        RunHubChoice(session, "save");
        var completed = RunWork(session, "save-pack", harvestLotId,
            SimulationFarmSurvivalCodes.OutboundPacking,
            PyeongchangSimulation공간StableIds.대관령Farm포장공간);
        var saved = session.CreateSavePackage(new SimulationSessionSaveRequest
        {
            SaveStableId = "save:wi-farm:supply",
            ExpectedRevision = completed.Revision,
        });

        var restored = SimulationSessionReplay.Restore(saved);
        var restoredSave = restored.CreateSavePackage(new SimulationSessionSaveRequest
        {
            SaveStableId = saved.SaveStableId,
            ExpectedRevision = restored.Revision,
        });

        Assert.Equal(saved.ReplayHash, restoredSave.ReplayHash);
        Assert.Equal(300m,
            Assert.Single(restored.Snapshot().FarmSurvival!.PackageLots).Quantity);
    }

    private static 경영SimulationSessionSnapshot RunWork(
        경영SimulationSessionAggregate session,
        string commandSuffix,
        string targetStableId,
        string actionCode,
        string spatialStableId)
    {
        var current = session.Snapshot();
        var confirmed = session.ConfirmFarmWork(Confirm(
            "command:wi-farm:" + commandSuffix, current.Revision, targetStableId,
            actionCode, spatialStableId));
        return session.Advance(Tick("command:wi-farm:" + commandSuffix + ":tick",
            confirmed.WorldRevision));
    }

    private static 경영SimulationSessionSnapshot RunHubChoice(
        경영SimulationSessionAggregate session,
        string commandSuffix)
    {
        var context = session.GetFarmChoiceContext();
        Assert.Equal(SimulationFarmChoicePlayableCodes.AwaitingChoice,
            context.SituationStateCode);
        var preview = session.PreviewFarmChoice(new SimulationFarmChoicePreviewRequest
        {
            ExpectedRevision = context.WorldRevision,
            ChoiceStableId = SimulationFarmChoicePlayableCodes.HubShipmentChoice,
        });
        var confirmed = session.ConfirmFarmChoice(new SimulationFarmChoiceConfirmRequest
        {
            CommandId = "command:wi-farm:choice:" + commandSuffix,
            ExpectedRevision = preview.BaseRevision,
            ChoiceStableId = preview.ChoiceStableId,
        });
        return session.Advance(new 경영SimulationTick진행Request
        {
            CommandId = "command:wi-farm:choice:" + commandSuffix + ":tick",
            ExpectedRevision = confirmed.Revision,
            TickCount = preview.Impact.DurationTicks,
        });
    }

    private static 경영SimulationSessionSnapshot AdvanceTicks(
        경영SimulationSessionAggregate session,
        경영SimulationSessionSnapshot current,
        int count,
        string commandPrefix)
    {
        for (var index = 1; index <= count; index++)
        {
            current = session.Advance(Tick(commandPrefix + ":" + index,
                current.Revision));
        }
        return current;
    }

    private static async System.Threading.Tasks.Task<경영SimulationSessionSnapshot> RunFarmWork(
        HttpClient client,
        경영SimulationSessionSnapshot current,
        string commandSuffix,
        string targetStableId,
        string actionCode,
        string spatialStableId)
    {
        var preview = Preview(current.Revision, targetStableId, actionCode,
            spatialStableId);
        var previewResult = await Post<SimulationFarmWorkPreviewSnapshot>(client,
            $"/api/simulation/v1/sessions/{current.SessionStableId}/farm-survival/work/preview",
            preview);
        Assert.True(previewResult.CanConfirm);
        await Post<SimulationFarmSurvivalStateSnapshot>(client,
            $"/api/simulation/v1/sessions/{current.SessionStableId}/farm-survival/work/confirm",
            Confirm("command:wi-http:" + commandSuffix, current.Revision, targetStableId,
                actionCode, spatialStableId));
        current = await Get<경영SimulationSessionSnapshot>(client,
            $"/api/simulation/v1/sessions/{current.SessionStableId}");
        return await TickHttp(client, current,
            "command:wi-http:" + commandSuffix + ":tick", 1);
    }

    private static async System.Threading.Tasks.Task<경영SimulationSessionSnapshot>
        RunHubChoiceHttp(
            HttpClient client,
            경영SimulationSessionSnapshot current,
            string commandSuffix)
    {
        var route = $"/api/simulation/v1/sessions/{current.SessionStableId}";
        var context = await Get<SimulationFarmChoiceContextSnapshot>(client,
            route + "/farm-choice-context");
        var preview = await Post<SimulationFarmChoicePreviewSnapshot>(client,
            route + "/farm-choice-previews",
            new SimulationFarmChoicePreviewRequest
            {
                ExpectedRevision = context.WorldRevision,
                ChoiceStableId = SimulationFarmChoicePlayableCodes.HubShipmentChoice,
            });
        current = await Post<경영SimulationSessionSnapshot>(client,
            route + "/farm-choices/confirm",
            new SimulationFarmChoiceConfirmRequest
            {
                CommandId = "command:wi-http:choice:" + commandSuffix,
                ExpectedRevision = preview.BaseRevision,
                ChoiceStableId = preview.ChoiceStableId,
            });
        return await TickHttp(client, current,
            "command:wi-http:choice:" + commandSuffix + ":tick",
            preview.Impact.DurationTicks);
    }

    private static async System.Threading.Tasks.Task<경영SimulationSessionSnapshot> TickHttp(
        HttpClient client,
        경영SimulationSessionSnapshot current,
        string commandPrefix,
        int count)
    {
        for (var index = 1; index <= count; index++)
        {
            current = await Post<경영SimulationSessionSnapshot>(client,
                $"/api/simulation/v1/sessions/{current.SessionStableId}/ticks",
                Tick(commandPrefix + ":" + index, current.Revision));
        }
        return current;
    }

    private static async System.Threading.Tasks.Task<T> Post<T>(
        HttpClient client,
        string route,
        object request,
        HttpStatusCode expectedStatus = HttpStatusCode.OK)
    {
        using var response = await client.PostAsJsonAsync(route, request);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == expectedStatus,
            $"Expected {expectedStatus} but received {response.StatusCode}: {body}");
        var result = JsonSerializer.Deserialize<T>(body,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return Assert.IsType<T>(result);
    }

    private static async System.Threading.Tasks.Task<T> Get<T>(
        HttpClient client,
        string route)
    {
        using var response = await client.GetAsync(route);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode,
            $"GET {route} failed with {response.StatusCode}: {body}");
        var result = JsonSerializer.Deserialize<T>(body,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return Assert.IsType<T>(result);
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

    private static SimulationFreightTransportPreviewRequest Freight(
        Simulation포장LotSnapshot packageLot)
        => new()
        {
            Transport = new SimulationFreightTransportBindingRequest
            {
                TransportRequestStableId = "freight-transport:wi:farm-hub:potato-1",
                DispatchOfferStableId = "dispatch-offer:wi:farm-hub:potato-1",
                CarrierCandidateStableId = "carrier-candidate:wi:cooperative-1",
                VehicleStableId = "vehicle:wi:truck-1",
                VehicleCapacity = 400m,
                VehicleCapacityUnitCode = "KGM",
            },
            Movement = new SimulationLogisticsMovementPreviewRequest
            {
                CargoStableId = packageLot.CargoStableId,
                CargoRevision = 1,
                SourceAllocationStableId = packageLot.SourceAllocationStableId,
                HarvestLotStableId = packageLot.HarvestLotStableId,
                PackageLotStableId = packageLot.PackageLotStableId,
                ProductStableId = "product:potato",
                Quantity = packageLot.Quantity,
                UnitCode = packageLot.UnitCode,
                RouteStableId = "route:wi:farm-hub",
                OriginFacilityStableId = FarmFacility,
                DestinationFacilityStableId = PyeongchangSimulationWorldStableIds.진부Hub시설,
                ActorStableId = Player,
                PreferredOriginSpatialStableId =
                    PyeongchangSimulation공간StableIds.대관령Farm상차공간,
                PreferredRouteSpatialStableId =
                    PyeongchangSimulation공간StableIds.FarmHub운송회랑,
                PreferredDestinationSpatialStableId =
                    PyeongchangSimulation공간StableIds.진부Hub하차공간,
                RequiredRouteTicks = 3,
                SourceStableIds = new[] { packageLot.PackageLotStableId },
            },
        };

    private static SimulationSupplyChainWorkPreviewRequest SupplyChainWork(
        SimulationNpcFacilityInventorySnapshot inventory,
        string actionCode,
        string spatialStableId,
        int durationTicks = 1)
        => new()
        {
            InventoryStableId = inventory.InventoryStableId,
            InventoryRevision = inventory.Revision,
            ActionCode = actionCode,
            ActorStableId = PyeongchangSimulationNpcStableIds.진부적재담당,
            PreferredSpatialStableId = spatialStableId,
            DurationTicks = durationTicks,
            SourceStableIds = new[] { inventory.InventoryStableId },
        };

    private static 경영SimulationSessionSnapshot RunSupplyChainWork(
        경영SimulationSessionAggregate session,
        SimulationNpcFacilityInventorySnapshot inventory,
        string actionCode,
        string spatialStableId,
        string commandSuffix)
    {
        var current = session.Snapshot();
        var request = SupplyChainWork(inventory, actionCode, spatialStableId);
        Assert.Empty(session.PreviewSupplyChainWork(request).Decision.BlockReasonCodes);
        var confirmed = session.ConfirmSupplyChainWork(
            new SimulationSupplyChainWorkConfirmRequest
            {
                CommandId = "command:wi:" + commandSuffix,
                ExpectedRevision = current.Revision,
                Work = request,
            });
        return session.Advance(Tick("command:wi:" + commandSuffix + ":tick",
            confirmed.Revision));
    }

    private static SimulationFreightTransportPreviewRequest HubMarketFreight(
        SimulationNpcFacilityInventorySnapshot inventory,
        SimulationHarvestLotAllocationSnapshot allocation)
        => new()
        {
            Transport = new SimulationFreightTransportBindingRequest
            {
                TransportRequestStableId = "freight-transport:wi:hub-market:potato-1",
                DispatchOfferStableId = "dispatch-offer:wi:hub-market:potato-1",
                CarrierCandidateStableId = "carrier-candidate:wi:market-1",
                VehicleStableId = "vehicle:wi:market-truck-1",
                VehicleCapacity = 400m,
                VehicleCapacityUnitCode = inventory.UnitCode,
            },
            Movement = new SimulationLogisticsMovementPreviewRequest
            {
                CargoStableId = "cargo:wi:hub-market:potato-1",
                CargoRevision = 1,
                SourceAllocationStableId = allocation.AllocationStableId,
                HarvestLotStableId = allocation.HarvestLotStableId,
                PackageLotStableId = "package-lot:wi:hub-market:potato-1",
                ProductStableId = inventory.ProductStableId,
                Quantity = inventory.Quantity,
                UnitCode = inventory.UnitCode,
                RouteStableId = "route:wi:hub-market",
                OriginFacilityStableId = inventory.FacilityStableId,
                DestinationFacilityStableId = MarketFacility,
                ActorStableId = Player,
                PreferredOriginSpatialStableId =
                    PyeongchangSimulation공간StableIds.진부Hub출고상차공간,
                PreferredRouteSpatialStableId =
                    PyeongchangSimulation공간StableIds.HubTown운송회랑,
                PreferredDestinationSpatialStableId =
                    PyeongchangSimulation공간StableIds.평창Town마트하차공간,
                RequiredRouteTicks = 3,
                SourceStableIds = new[] { inventory.InventoryStableId },
            },
        };

    private static SimulationFarmWorkPreviewRequest Preview(
        long revision,
        string targetStableId,
        string actionCode,
        string spatialStableId)
        => new()
        {
            ExpectedRevision = revision,
            ActorStableId = Player,
            TargetStableId = targetStableId,
            ActionCode = actionCode,
            AssignmentKindCode = SimulationFarmSurvivalCodes.PlayerDirect,
            PreferredSpatialStableId = spatialStableId,
        };

    private static SimulationFarmWorkConfirmRequest Confirm(
        string commandId,
        long revision,
        string targetStableId,
        string actionCode,
        string spatialStableId)
        => new()
        {
            CommandId = commandId,
            ExpectedRevision = revision,
            ActorStableId = Player,
            TargetStableId = targetStableId,
            ActionCode = actionCode,
            AssignmentKindCode = SimulationFarmSurvivalCodes.PlayerDirect,
            PreferredSpatialStableId = spatialStableId,
        };

    private static 경영SimulationTick진행Request Tick(string commandId, long revision)
        => new()
        {
            CommandId = commandId,
            ExpectedRevision = revision,
            TickCount = 1,
        };

    private static 경영SimulationSession생성Request CreateHubNpcRoutineRequest()
    {
        var request = CreateRequest(npcRoutineControlRevision:
            SimulationNpcRoutineControlRevisionCodes.R1);
        request.NpcWorkforce =
            PyeongchangSimulationNpcWorkforceFixture.CreateHubOutboundReadyFixture();
        return request;
    }

    private static 경영SimulationSession생성Request
        CreateHubNpcFullRoutineRequest()
    {
        var request = CreateRequest(npcRoutineControlRevision:
            SimulationNpcRoutineControlRevisionCodes.R2);
        request.NpcWorkforce = PyeongchangSimulationNpcWorkforceFixture
            .CreateHubWarehouseFullLoopFixture();
        return request;
    }

    private static 경영SimulationSession생성Request CreateRequest(
        Simulation공간세계InitialStateRequest? spatialWorld = null,
        string npcRoutineControlRevision = "")
        => new()
        {
            ClientRequestId = Guid.Parse("4D73AB1E-7E22-4BE5-B638-807CB45C2AA1"),
            ScenarioStableId = "scenario:wi-farm-supply",
            ScenarioDataRevision = "fixture.r1",
            ScenarioSeed = 300,
            RuleRevision = "world-interaction.farm-supply.r1",
            NpcRoutineControlRevision = npcRoutineControlRevision,
            DurationTicks = 30,
            WorldContext = new SimulationWorldContext생성Request
            {
                FactionStableId = "faction:wi-farm",
                TerritoryStableId = "territory:pyeongchang",
                SettlementStableId = "settlement:pyeongchang",
                GameDateStartsOn = new DateTimeOffset(2026, 8, 17, 0, 0, 0,
                    TimeSpan.Zero),
            },
            Settlement = new SimulationSettlementInitialStateRequest
            {
                TreasuryBalance = 1_000_000m,
                CurrencyCode = "KRW",
                LaborCapacityTotal = 100m,
                StorageCapacity = 20_000m,
                StorageUnitCode = "KGM",
                PopulationCount = 100,
                PopulationFoodDemandPerTick = 100m,
                FoodEquivalentUnitCode = "KGM",
                FoodEquivalentRuleRevision = "food-equivalent:wi.r1",
                Districts = new[]
                {
                    new SimulationSettlementDistrictRequest
                    {
                        DistrictStableId = "district:wi-farm",
                        DistrictTypeCode = "Farm",
                        SourceStableIds = new[] { "source:scenario.wi-farm" },
                    },
                    new SimulationSettlementDistrictRequest
                    {
                        DistrictStableId = "district:wi-hub",
                        DistrictTypeCode = "Logistics",
                        SourceStableIds = new[] { "source:scenario.wi-farm" },
                    },
                },
                Facilities = new[]
                {
                    new SimulationSettlementFacilityRequest
                    {
                        FacilityStableId = FarmFacility,
                        FacilityTypeCode = "FarmPacking",
                        DistrictStableId = "district:wi-farm",
                        SourceStableIds = new[] { "source:scenario.wi-farm" },
                    },
                    new SimulationSettlementFacilityRequest
                    {
                        FacilityStableId = PyeongchangSimulationWorldStableIds.진부Hub시설,
                        FacilityTypeCode = "LogisticsHub",
                        DistrictStableId = "district:wi-hub",
                        SourceStableIds = new[] { "source:scenario.wi-farm" },
                    },
                    new SimulationSettlementFacilityRequest
                    {
                        FacilityStableId = "facility:wi-farm:storage",
                        FacilityTypeCode = SimulationSettlementFacilityTypeCodes.Storage,
                        DistrictStableId = "district:wi-hub",
                        SourceStableIds = new[] { "source:scenario.wi-farm" },
                    },
                    new SimulationSettlementFacilityRequest
                    {
                        FacilityStableId = "facility:wi-farm:market",
                        FacilityTypeCode = SimulationSettlementFacilityTypeCodes.Market,
                        DistrictStableId = "district:wi-hub",
                        SourceStableIds = new[] { "source:scenario.wi-farm" },
                    },
                },
                SourceStableIds = new[] { "source:scenario.wi-farm" },
            },
            SpatialWorld = spatialWorld
                ?? PyeongchangSimulation공간상호작용Fixture.CreateFarmHubSupply(
                    FarmFacility, MarketFacility),
            NpcWorkforce = PyeongchangSimulationNpcWorkforceFixture.Create(),
            FarmSurvival = new SimulationFarmSurvivalInitialStateRequest
            {
                RuleRevision = SimulationFarmSurvivalCodes.ScenicSeasonRuleRevision,
                RegionStableId = "region:legal-dong:5176031000",
                AreaStableId = "area:pyeongchang:daegwallyeong-farm",
                TileKey = "kr5186:l2:700:1145",
                FarmBuildingStableId = FarmFacility,
                SupplyUnits = 8m,
                RepairMaterialUnits = 4m,
                SeedUnits = 2m,
                WaterUnits = 2m,
                Actors = new[]
                {
                    new SimulationFarmActorInitialStateRequest
                    {
                        ActorStableId = Player,
                        ActorKindCode = SimulationFarmSurvivalCodes.Player,
                        KoreanName = "공급선 농장 작업자",
                        CapabilityCodes = new[]
                        {
                            SimulationFarmActorCapabilityCodes.FarmHarvest,
                            SimulationFarmActorCapabilityCodes.FarmCollection,
                            SimulationFarmActorCapabilityCodes.FarmPacking,
                            SimulationFarmActorCapabilityCodes.FarmTilling,
                            SimulationFarmActorCapabilityCodes.FarmSowing,
                            SimulationFarmActorCapabilityCodes.FarmCropCare,
                        },
                    },
                },
                SoilTiles = new[]
                {
                    new SimulationFarmSoilTileInitialStateRequest
                    {
                        SoilTileStableId = PreparationSoil,
                        GridX = 0,
                        GridY = 0,
                        StateCode = SimulationFarmSurvivalCodes.Untilled,
                        PhysicalAreaSquareMeters = 100m,
                    },
                },
                CultivationUnits = new[]
                {
                    new Simulation재배단위Snapshot
                    {
                        CultivationUnitStableId = CultivationUnit,
                        Revision = 1,
                        TileStableId = "tile:wi-farm:potato-1",
                        CultivationStableId = "cultivation:wi-farm:potato",
                        ProductStableId = "product:potato",
                        CropVariantStableId = "crop-variant:potato.fixture",
                        StateCode = Simulation재배단위상태Codes.HarvestReady,
                        PhysicalAreaSquareMeters = 100m,
                        EffectiveCultivationAreaRatio = 1m,
                        SourceStableIds = new[] { "source:scenario.cultivation-unit" },
                    },
                },
                Defenses = new[]
                {
                    new SimulationFarmDefenseInitialStateRequest
                    {
                        DefenseStableId = FarmFence,
                        DefenseKindCode = SimulationFarmSurvivalCodes.Fence,
                        Durability = 60m,
                    },
                },
                PotatoProductionRule = new Simulation감자생산RuleSnapshot
                {
                    RuleStableId = "rule:potato-production.fixture.v1",
                    RuleRevision = 1,
                    SourceTypeCode = Simulation생산규칙SourceTypeCodes.Fixture,
                    ProductStableId = "product:potato",
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
                    SourceStableIds = new[] { "source:fixture.potato-yield-rule" },
                    Limitations = new[] { "실제 생산량 또는 운영 수확량으로 사용하지 않는다." },
                },
            },
        };

    private sealed class 사용하지않는NpcRoutineLocalSaveSlotStore
        : ISimulationLocalSaveSlotStore
    {
        public void Write(string slotStableId,
            SimulationSessionSavePackage package)
            => throw new NotSupportedException();

        public SimulationLocalSaveSlotPackage Read(string slotStableId)
            => throw new NotSupportedException();
    }
}
