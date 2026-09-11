using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.Simulation.Application;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;
using Ssalddel.Simulation.Infrastructure;
using Ssalddel.Simulation.Hosting.Controllers;
using Xunit;

namespace Ssalddel.Simulation.Tests;

[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
    "Simulation·Unity 계약과 결정성 및 회귀 증거를 검증한다.",
    Boundary = "자동 시험 통과와 실제 Play Mode·Game View·E 승격 증거를 구분한다.")]
public sealed class SimulationIntegratedWorldTests
{
    [Fact]
    public void HTTP_Controller는_통합생활세계_Preview를_상태변경없이_반환한다()
    {
        var store = new InMemory경영SimulationSessionStore();
        var session = store.CreateOrGet(CreateRequest());
        var controller = new SimulationIntegratedWorldController(
            new 경영Simulation통합생활세계Service(
                new 경영SimulationSessionAccessor(store)));
        var before = session.Snapshot();

        var action = controller.Preview(before.SessionStableId,
            Manufacturing("http-preview-box", before.Revision,
                "recipe:transport-box"));

        var ok = Assert.IsType<OkObjectResult>(action.Result);
        var preview = Assert.IsType<SimulationIntegratedWorldPreviewSnapshot>(ok.Value);
        Assert.True(preview.CanConfirm);
        Assert.Equal(before.Revision, session.Snapshot().Revision);
    }

    [Fact]
    public void Preview는_상태를_바꾸지_않고_Hub_제조를_판정한다()
    {
        var session = CreateSession();
        var before = session.Snapshot();
        var request = Manufacturing("preview-box", before.Revision, "recipe:transport-box");

        var preview = session.PreviewIntegratedWorldCommand(request);
        var after = session.Snapshot();

        Assert.True(preview.CanConfirm);
        Assert.Equal("facility:hub:workshop", preview.SelectedFacilityStableId);
        Assert.Single(preview.SelectedActorStableIds);
        Assert.Equal(before.Revision, after.Revision);
        Assert.Empty(after.IntegratedWorld.ManufacturingJobs);
        Assert.Equal(before.IntegratedWorld.Lots.Select(value => value.Quantity),
            after.IntegratedWorld.Lots.Select(value => value.Quantity));
    }

    [Fact]
    public void 제조_건설_편성_피해_복구가_하나의_Session에서_닫힌다()
    {
        var session = CreateSession();

        Confirm(session, Manufacturing("mfg-box", session.Revision,
            "recipe:transport-box"));
        Advance(session, "tick-box-processing");
        Advance(session, "tick-box-inspection");

        Confirm(session, Manufacturing("mfg-component", session.Revision,
            "recipe:facility-component"));
        Advance(session, "tick-component-processing");
        Advance(session, "tick-component-inspection");

        Confirm(session, Cargo("move-component", session.Revision,
            "manufacturing-job:mfg-component:output:00", "facility:farm:warehouse", 3m));
        Advance(session, "tick-move-component");
        Confirm(session, Cargo("move-box", session.Revision,
            "manufacturing-job:mfg-box:output:00", "facility:farm:warehouse", 5m));
        Advance(session, "tick-move-box");

        Confirm(session, new SimulationIntegratedWorldCommandRequest
        {
            ActionCode = SimulationIntegratedWorldActionCodes.ConstructionOrder,
            CommandId = "build-barracks",
            ExpectedRevision = session.Revision,
            Construction = new SimulationConstructionOrderPayload
            {
                BlueprintStableId = "blueprint:barracks",
                BuildSiteH1StableId = "h1:Farm:barracks-build-site",
            },
        });
        var planned = session.Snapshot().IntegratedWorld;
        Assert.Equal(SimulationFacilityLifecycleCodes.Planned,
            planned.Facilities.Single(value =>
                value.FacilityStableId == "facility:player-built:build-barracks").LifecycleCode);
        Advance(session, "tick-barracks-start");
        Advance(session, "tick-barracks-complete");

        Confirm(session, new SimulationIntegratedWorldCommandRequest
        {
            ActionCode = SimulationIntegratedWorldActionCodes.PotatoPackaging,
            CommandId = "package-potato",
            ExpectedRevision = session.Revision,
            PotatoPackaging = new SimulationPotatoPackagingPayload
            {
                PotatoLotStableId = "lot:farm:harvest-potato",
                TransportBoxLotStableId = "cargo-movement:move-box:output:00",
                PotatoQuantity = 100m,
                BoxQuantity = 1m,
            },
        });
        Advance(session, "tick-package-processing");
        Advance(session, "tick-package-inspection");

        Confirm(session, new SimulationIntegratedWorldCommandRequest
        {
            ActionCode = SimulationIntegratedWorldActionCodes.Recruitment,
            CommandId = "recruit-militia",
            ExpectedRevision = session.Revision,
            Recruitment = new SimulationRecruitmentPayload
            {
                RecruitmentPolicyStableId = "recruitment-policy:farm-militia.r1",
                ActorCount = 8,
            },
        });
        Advance(session, "tick-recruitment");
        Confirm(session, new SimulationIntegratedWorldCommandRequest
        {
            ActionCode = SimulationIntegratedWorldActionCodes.Training,
            CommandId = "train-militia",
            ExpectedRevision = session.Revision,
            Training = new SimulationTrainingPayload
            {
                FormationStableId = "formation:recruit-militia",
                TrainingTicks = 1,
            },
        });
        Advance(session, "tick-training");
        Confirm(session, new SimulationIntegratedWorldCommandRequest
        {
            ActionCode = SimulationIntegratedWorldActionCodes.FormationDeployment,
            CommandId = "deploy-militia",
            ExpectedRevision = session.Revision,
            FormationDeployment = new SimulationFormationDeploymentPayload
            {
                FormationStableId = "formation:recruit-militia",
                GarrisonFacilityStableId = "facility:player-built:build-barracks",
            },
        });

        var projection = session.CreateBattleRelevantRuntimeProjectionForArea(
            "encounter:farm-gate", "Farm");
        Assert.Contains(projection.Facilities, value =>
            value.FacilityStableId == "facility:player-built:build-barracks");
        Assert.Contains(projection.Formations, value =>
            value.FormationStableId == "formation:recruit-militia");
        Assert.Equal(8, projection.BattleAvailableActorStableIds.Length);

        session.QueueFacilityBattleDamage("battle:farm-gate:001",
            "facility:farm:warehouse", SimulationBattlefieldDerivationCodes.Moderate);
        Advance(session, "tick-apply-damage");
        var damaged = session.Snapshot().IntegratedWorld;
        var warehouse = damaged.Facilities.Single(value =>
            value.FacilityStableId == "facility:farm:warehouse");
        Assert.Equal(SimulationFacilityIntegrityCodes.Damaged, warehouse.IntegrityCode);
        Assert.Equal(SimulationFacilityCapabilityStateCodes.Suspended,
            warehouse.EffectiveCapabilities.Single(value =>
                value.CapabilityCode == SimulationIntegratedCapabilityCodes.LoadingWorkArea)
                .StateCode);
        Assert.Equal(200m, damaged.Lots.Single(value =>
            value.LotStableId == "lot:farm:harvest-potato").Quantity);
        Assert.Equal(100m, damaged.Lots.Single(value =>
            value.LotStableId == "packaging-job:package-potato:output:00").Quantity);

        Confirm(session, new SimulationIntegratedWorldCommandRequest
        {
            ActionCode = SimulationIntegratedWorldActionCodes.FacilityRepair,
            CommandId = "repair-warehouse",
            ExpectedRevision = session.Revision,
            FacilityRepair = new SimulationFacilityRepairPayload
            {
                FacilityStableId = "facility:farm:warehouse",
                RepairTicks = 1,
            },
        });
        Advance(session, "tick-repair");

        var restored = session.Snapshot().IntegratedWorld;
        warehouse = restored.Facilities.Single(value =>
            value.FacilityStableId == "facility:farm:warehouse");
        Assert.Equal(SimulationFacilityIntegrityCodes.Intact, warehouse.IntegrityCode);
        Assert.Equal(SimulationFacilityCapabilityStateCodes.Active,
            warehouse.EffectiveCapabilities.Single(value =>
                value.CapabilityCode == SimulationIntegratedCapabilityCodes.LoadingWorkArea)
                .StateCode);
        Assert.All(restored.FacilityRestrictions,
            value => Assert.NotEmpty(value.ResolvedByEffectStableId));
        Assert.Equal(8, restored.ActorCommitments.Count(value => value.Active
            && value.CommitmentCode == SimulationActorCommitmentCodes.FormationDuty));
        Assert.Equal(4, restored.Actors.Count(value => value.FarmLaborEligible
            && !restored.ActorCommitments.Any(commitment => commitment.Active
                && commitment.ActorStableId == value.ActorStableId)));
        Assert.Equal(4m, restored.Lots.Single(value =>
            value.LotStableId == "cargo-movement:move-box:output:00").Quantity);

        var save = session.CreateSavePackage(new SimulationSessionSaveRequest
        {
            SaveStableId = "save:h5-integrated-world",
            ExpectedRevision = session.Revision,
        });
        Assert.Equal(SimulationSaveSchemaVersions.V7, save.SchemaVersion);
        var replayed = SimulationSessionReplay.Restore(save).Snapshot();
        Assert.Equal(session.Revision, replayed.Revision);
        Assert.Equal(restored.Facilities.Select(value =>
                (value.FacilityStableId, value.LifecycleCode, value.IntegrityCode)),
            replayed.IntegratedWorld.Facilities.Select(value =>
                (value.FacilityStableId, value.LifecycleCode, value.IntegrityCode)));
        Assert.Equal(restored.Lots.Select(value => (value.LotStableId, value.Quantity)),
            replayed.IntegratedWorld.Lots.Select(value => (value.LotStableId, value.Quantity)));
    }

    [Fact]
    public void 무관한_제조는_전투_문맥_투영_해시를_바꾸지_않는다()
    {
        var session = CreateSession();
        var before = session.CreateBattleRelevantRuntimeProjectionForArea(
            "encounter:farm-gate", "Farm");

        Confirm(session, Manufacturing("unrelated-hub-manufacturing", session.Revision,
            "recipe:transport-box"));

        var after = session.CreateBattleRelevantRuntimeProjectionForArea(
            "encounter:farm-gate", "Farm");
        Assert.Equal(before.BattleRelevantOverlayHashSha256,
            after.BattleRelevantOverlayHashSha256);
    }

    private static 경영SimulationSessionAggregate CreateSession()
        => new(CreateRequest());

    private static 경영SimulationSession생성Request CreateRequest()
        => new()
        {
            ClientRequestId = Guid.NewGuid(),
            ScenarioStableId = "scenario:h5-integrated-world",
            ScenarioDataRevision = "r1",
            ScenarioSeed = 20260820,
            RuleRevision = "h5-integrated-world.contract.v1",
            DurationTicks = 40,
            WorldContext = new SimulationWorldContext생성Request
            {
                FactionStableId = "faction:player",
                TerritoryStableId = "territory:pyeongchang",
                SettlementStableId = "settlement:pyeongchang",
                GameDateStartsOn = new DateTimeOffset(
                    2026, 8, 20, 0, 0, 0, TimeSpan.Zero),
            },
            IntegratedWorld = H5IntegratedWorldScenarioFixture.Create(),
        };

    private static SimulationIntegratedWorldCommandRequest Manufacturing(string commandId,
        long revision, string recipe) => new()
    {
        ActionCode = SimulationIntegratedWorldActionCodes.ManufacturingOrder,
        CommandId = commandId,
        ExpectedRevision = revision,
        Manufacturing = new SimulationManufacturingOrderPayload
        {
            RecipeStableId = recipe,
            PreferredManufacturingFacilityStableId = "facility:hub:workshop",
        },
    };

    private static SimulationIntegratedWorldCommandRequest Cargo(string commandId,
        long revision, string lot, string facility, decimal quantity) => new()
    {
        ActionCode = SimulationIntegratedWorldActionCodes.CargoTransfer,
        CommandId = commandId,
        ExpectedRevision = revision,
        CargoTransfer = new SimulationCargoTransferPayload
        {
            SourceLotStableId = lot,
            TargetFacilityStableId = facility,
            Quantity = quantity,
            TransportTicks = 1,
        },
    };

    private static void Confirm(경영SimulationSessionAggregate session,
        SimulationIntegratedWorldCommandRequest request)
        => session.ConfirmIntegratedWorldCommand(request);

    private static void Advance(경영SimulationSessionAggregate session, string commandId)
        => session.Advance(new 경영SimulationTick진행Request
        {
            CommandId = commandId,
            ExpectedRevision = session.Revision,
            TickCount = 1,
        });
}
