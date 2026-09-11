using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ssalddel.Simulation.Application;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;

namespace Ssalddel.Simulation.Tests;

[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
    "Simulation·Unity 계약과 결정성 및 회귀 증거를 검증한다.",
    Boundary = "자동 시험 통과와 실제 Play Mode·Game View·E 승격 증거를 구분한다.")]
public sealed class SimulationRegionalIncidentTests
{
    [Fact]
    public void 지역발전원장골격은_Farm세H1과잠긴Nature연결을제공하고_기존상태를변경하지않는다()
    {
        var session = new 경영SimulationSessionAggregate(CreateRequest());
        var before = session.Snapshot();

        var development = before.RegionalDevelopment;

        Assert.Equal(0, development.Revision);
        Assert.Empty(development.Opportunities);
        Assert.Empty(development.RouteSafeties);
        var farm = Assert.Single(development.Areas);
        Assert.Equal(SimulationRegionalIncidentCodes.Farm, farm.AreaCode);
        Assert.Equal(SimulationRegionalDevelopmentCodes.FarmIncidentContainmentH2,
            farm.TargetH2StableId);
        Assert.Equal(SimulationRegionalDevelopmentCodes.NotStarted, farm.StateCode);
        Assert.Equal(new[]
        {
            SimulationRegionalDevelopmentCodes.FarmExposureInspectionH1,
            SimulationRegionalDevelopmentCodes.FarmIncidentQuarantineH1,
            SimulationRegionalDevelopmentCodes.FarmWeatherProtectionH1,
        }, farm.RequiredH1StableIds);
        Assert.Empty(farm.OperationalH1StableIds);
        var connector = Assert.Single(development.Connectors);
        Assert.Equal(SimulationRegionalDevelopmentCodes.NatureFarmSafetyConnector,
            connector.ConnectorStableId);
        Assert.Equal(SimulationRegionalDevelopmentCodes.Locked, connector.StateCode);
        Assert.Equal(0, session.Revision);
    }

    [Fact]
    public void 기존V4지역사건저장은_새인과점수를적용하지않고그대로재생한다()
    {
        var session = new 경영SimulationSessionAggregate(CreateRequest());
        session.UseLegacyRegionalCausalityRules();
        var harvested = HarvestAndCreateIncident(session);
        var incident = Assert.Single(harvested.RegionalIncidents);
        session.ConfirmRegionalIncidentResponse(incident.EventStableId,
            new SimulationRegionalIncidentResponseConfirmRequest
            {
                CommandId = "command:test:legacy-v4-unsafe",
                ExpectedRevision = session.Revision,
                ActorStableId = "actor:test:manager",
                ChoiceStableId = SimulationRegionalIncidentCodes.FarmLeaveExposed,
            });
        var package = session.CreateSavePackage(new SimulationSessionSaveRequest
        {
            SaveStableId = "save:test:legacy-regional-v4",
            ExpectedRevision = session.Revision,
        });

        var restored = SimulationSessionReplay.Restore(package);
        var replayed = restored.CreateSavePackage(new SimulationSessionSaveRequest
        {
            SaveStableId = package.SaveStableId,
            ExpectedRevision = restored.Revision,
        });

        Assert.Equal(SimulationSaveSchemaVersions.V4, package.SchemaVersion);
        Assert.Equal(package.ReplayHash, replayed.ReplayHash);
        Assert.Equal(0, restored.Snapshot().RegionalCausality.Revision);
        Assert.Equal(4, restored.Snapshot().NatureThreat.Routes.Single(value =>
            value.NatureRouteCode == SimulationRegionalIncidentCodes.NatureToFarm)
            .EffectivePressure);
    }

    [Fact]
    public void 경로압력은_원인심각도두배와전체삼분의일을합산하고_상한을적용한다()
    {
        var routes = SimulationNatureThreatPressurePolicy.Evaluate(
        [
            Incident("incident:farm", SimulationRegionalIncidentCodes.NatureToFarm, 2),
            Incident("incident:town", SimulationRegionalIncidentCodes.NatureToTown, 3),
            Incident("incident:hub", SimulationRegionalIncidentCodes.NatureToCityHub, 4),
        ]);

        var farm = routes.Single(value => value.NatureRouteCode ==
            SimulationRegionalIncidentCodes.NatureToFarm);
        var town = routes.Single(value => value.NatureRouteCode ==
            SimulationRegionalIncidentCodes.NatureToTown);
        var hub = routes.Single(value => value.NatureRouteCode ==
            SimulationRegionalIncidentCodes.NatureToCityHub);

        Assert.Equal(7, farm.EffectivePressure);
        Assert.Equal(SimulationRegionalIncidentCodes.Threatened,
            farm.PressureLevelCode);
        Assert.Equal(9, town.EffectivePressure);
        Assert.Equal(11, hub.EffectivePressure);
        Assert.Equal(3, SimulationNatureThreatPressurePolicy.ThreatUnitCount(
            farm.EffectivePressure));
    }

    [Fact]
    public void 안전하지않은Farm선택은_자연권조우를만들고_같은명령재전송과저장재생은동일하다()
    {
        var session = new 경영SimulationSessionAggregate(CreateRequest());
        var before = HarvestAndCreateIncident(session);
        var incident = Assert.Single(before.RegionalIncidents);

        var preview = session.PreviewRegionalIncidentResponse(incident.EventStableId,
            new SimulationRegionalIncidentResponsePreviewRequest
            {
                ExpectedRevision = before.Revision,
                ActorStableId = "actor:test:manager",
                ChoiceStableId = SimulationRegionalIncidentCodes.FarmLeaveExposed,
            });
        Assert.True(preview.CanConfirm);
        Assert.Equal(2, preview.ProjectedThreatSeverityDelta);
        Assert.Equal(before.Revision, session.Revision);

        var request = new SimulationRegionalIncidentResponseConfirmRequest
        {
            CommandId = "command:test:farm-exposed",
            ExpectedRevision = before.Revision,
            ActorStableId = "actor:test:manager",
            ChoiceStableId = SimulationRegionalIncidentCodes.FarmLeaveExposed,
        };
        var confirmed = session.ConfirmRegionalIncidentResponse(
            incident.EventStableId, request);
        var retried = session.ConfirmRegionalIncidentResponse(
            incident.EventStableId, request);

        Assert.Equal(confirmed.Revision, retried.Revision);
        Assert.Equal(SimulationRegionalIncidentCodes.AdverseOutcome,
            Assert.Single(confirmed.RegionalIncidents).StateCode);
        var route = confirmed.NatureThreat.Routes.Single(value =>
            value.NatureRouteCode == SimulationRegionalIncidentCodes.NatureToFarm);
        Assert.Equal(4, route.IncidentPressure);
        Assert.Equal(2, route.ThreatScoreModifier);
        Assert.Equal(6, route.EffectivePressure);
        Assert.Equal(2, confirmed.RegionalCausality.ThreatScore);
        Assert.Equal(0, confirmed.RegionalCausality.RecoveryScore);
        Assert.Equal(SimulationRegionalIncidentCodes.ThreatOutcome,
            confirmed.RegionalCausality.OutcomeCode);
        var encounter = Assert.Single(confirmed.NatureThreat.Encounters);
        Assert.Equal(2, encounter.ThreatUnitCount);
        Assert.Equal(SimulationRegionalIncidentCodes.Active, encounter.StateCode);

        var afterVictory = session.ApplyNatureEncounterVictory(
            "battle:test:farm-pressure", encounter.EncounterStableId);
        Assert.Equal(2, Assert.Single(afterVictory.RegionalIncidents).RemainingSeverity);
        Assert.Equal(SimulationRegionalIncidentCodes.Resolved,
            Assert.Single(afterVictory.NatureThreat.Encounters).StateCode);
        var opportunity = Assert.Single(afterVictory.RegionalDevelopment.Opportunities);
        Assert.Equal(incident.IncidentStableId, opportunity.SourceIncidentStableId);
        Assert.Equal(SimulationRegionalDevelopmentCodes.Available,
            opportunity.StateCode);
        var safety = Assert.Single(afterVictory.RegionalDevelopment.RouteSafeties);
        Assert.Equal(afterVictory.CurrentTick + 1, safety.SecuredUntilWorldTick);

        var duplicate = session.ApplyNatureEncounterVictory(
            "battle:test:farm-pressure", encounter.EncounterStableId);
        Assert.Equal(afterVictory.Revision, duplicate.Revision);
        Assert.Single(duplicate.RegionalDevelopment.Opportunities);

        var package = session.CreateSavePackage(new SimulationSessionSaveRequest
        {
            SaveStableId = "save:test:regional-incident",
            ExpectedRevision = session.Revision,
        });
        Assert.Equal(SimulationSaveSchemaVersions.V14, package.SchemaVersion);
        var restored = SimulationSessionReplay.Restore(package);
        var restoredPackage = restored.CreateSavePackage(new SimulationSessionSaveRequest
        {
            SaveStableId = package.SaveStableId,
            ExpectedRevision = restored.Revision,
        });
        Assert.Equal(package.ReplayHash, restoredPackage.ReplayHash);
        Assert.Equal(encounter.EncounterStableId,
            Assert.Single(restored.Snapshot().NatureThreat.Encounters).EncounterStableId);
        Assert.Equal(SimulationRegionalIncidentCodes.Resolved,
            Assert.Single(restored.Snapshot().NatureThreat.Encounters).StateCode);
        Assert.Single(restored.Snapshot().RegionalDevelopment.Opportunities);
    }

    [Fact]
    public void 기한전원인Wi를완료하면_압력이생기지않고_기한초과때만압력이생긴다()
    {
        var contained = new 경영SimulationSessionAggregate(CreateRequest());
        contained.RegisterFarmHarvestExposureIncident(
            "harvest:test:contained", "facility:test:farm", 0);
        var incident = Assert.Single(contained.Snapshot().RegionalIncidents);
        contained.ConfirmRegionalIncidentResponse(incident.EventStableId,
            new SimulationRegionalIncidentResponseConfirmRequest
            {
                CommandId = "command:test:farm-safe",
                ExpectedRevision = contained.Revision,
                ActorStableId = "actor:test:manager",
                ChoiceStableId = SimulationRegionalIncidentCodes.FarmCollectAndPack,
            });
        contained.ObserveRegionalIncidentAction(
            SimulationFarmSurvivalCodes.HarvestCollection,
            incident.SourceTargetStableId, 1);
        contained.ObserveRegionalIncidentAction(
            SimulationFarmSurvivalCodes.OutboundPacking,
            incident.SourceTargetStableId, 2);
        var containedState = contained.Snapshot();
        Assert.Equal(SimulationRegionalIncidentCodes.Contained,
            Assert.Single(containedState.RegionalIncidents).OutcomeCode);
        Assert.All(containedState.NatureThreat.Routes,
            value => Assert.Equal(0, value.EffectivePressure));

        var missed = new 경영SimulationSessionAggregate(CreateRequest());
        missed.RegisterFarmHarvestExposureIncident(
            "harvest:test:missed", "facility:test:farm", 0);
        var advanced = missed.Advance(new 경영SimulationTick진행Request
        {
            CommandId = "command:test:deadline",
            ExpectedRevision = 0,
            TickCount = 3,
        });
        Assert.Equal(SimulationRegionalIncidentCodes.DeadlineMissed,
            Assert.Single(advanced.RegionalIncidents).OutcomeCode);
        Assert.Equal(6, advanced.NatureThreat.Routes.Single(value =>
            value.NatureRouteCode == SimulationRegionalIncidentCodes.NatureToFarm)
            .EffectivePressure);
        Assert.Equal(2, advanced.RegionalCausality.ThreatScore);
    }

    [Fact]
    public async Task 세계사건Api는_서버규칙으로선택을미리보고확정한다()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var createResponse = await client.PostAsJsonAsync(
            "/api/simulation/v1/sessions", CreateRequest());
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content
            .ReadFromJsonAsync<경영SimulationSessionSnapshot>();
        var sessionRoute = "/api/simulation/v1/sessions/"
            + Uri.EscapeDataString(created!.SessionStableId);

        var harvestResponse = await client.PostAsJsonAsync(
            sessionRoute + "/farm-survival/work/confirm",
            new SimulationFarmWorkConfirmRequest
            {
                CommandId = "command:http:harvest",
                ExpectedRevision = created.Revision,
                ActorStableId = "actor:test:farmer",
                TargetStableId = "cultivation:test:potato",
                ActionCode = SimulationFarmSurvivalCodes.Harvesting,
                AssignmentKindCode = SimulationFarmSurvivalCodes.PlayerDirect,
            });
        Assert.Equal(HttpStatusCode.OK, harvestResponse.StatusCode);
        var harvest = await harvestResponse.Content
            .ReadFromJsonAsync<SimulationFarmSurvivalStateSnapshot>();
        var tickResponse = await client.PostAsJsonAsync(sessionRoute + "/ticks",
            new 경영SimulationTick진행Request
            {
                CommandId = "command:http:harvest-tick",
                ExpectedRevision = harvest!.WorldRevision,
                TickCount = 1,
            });
        Assert.Equal(HttpStatusCode.OK, tickResponse.StatusCode);
        var ticked = await tickResponse.Content
            .ReadFromJsonAsync<경영SimulationSessionSnapshot>();
        var incident = Assert.Single(ticked!.RegionalIncidents);
        var eventRoute = sessionRoute + "/world-events/"
            + Uri.EscapeDataString(incident.EventStableId);

        var previewResponse = await client.PostAsJsonAsync(
            eventRoute + "/response-previews",
            new SimulationRegionalIncidentResponsePreviewRequest
            {
                ExpectedRevision = ticked.Revision,
                ActorStableId = "actor:test:manager",
                ChoiceStableId = SimulationRegionalIncidentCodes.FarmLeaveExposed,
            });
        Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);
        var preview = await previewResponse.Content
            .ReadFromJsonAsync<SimulationRegionalIncidentResponsePreviewSnapshot>();
        Assert.True(preview!.CanConfirm);

        var confirmResponse = await client.PostAsJsonAsync(
            eventRoute + "/responses/confirm",
            new SimulationRegionalIncidentResponseConfirmRequest
            {
                CommandId = "command:http:farm-exposed",
                ExpectedRevision = ticked.Revision,
                ActorStableId = "actor:test:manager",
                ChoiceStableId = SimulationRegionalIncidentCodes.FarmLeaveExposed,
            });
        Assert.Equal(HttpStatusCode.OK, confirmResponse.StatusCode);
        var confirmed = await confirmResponse.Content
            .ReadFromJsonAsync<경영SimulationSessionSnapshot>();
        Assert.Single(confirmed!.NatureThreat.Encounters);
    }

    [Fact]
    public void 기존저장규칙의_자연권전투승리는_원인심각도감소의미를보존한다()
    {
        var session = new 경영SimulationSessionAggregate(CreateRequest());
        session.UseLegacyRegionalDevelopmentRules();
        session.RegisterHubCargoBacklogIncident(
            "cargo:test:battle", "facility:test:hub", 0);
        var incident = Assert.Single(session.Snapshot().RegionalIncidents);
        var adverse = session.ConfirmRegionalIncidentResponse(incident.EventStableId,
            new SimulationRegionalIncidentResponseConfirmRequest
            {
                CommandId = "command:test:hub-overflow-for-battle",
                ExpectedRevision = session.Revision,
                ActorStableId = "actor:test:manager",
                ChoiceStableId = SimulationRegionalIncidentCodes.HubOverflowOpenYard,
            });
        var encounter = Assert.Single(adverse.NatureThreat.Encounters);

        var first = session.ApplyNatureEncounterVictory(
            "battle:test:first", encounter.EncounterStableId);
        Assert.Equal(2, Assert.Single(first.RegionalIncidents).RemainingSeverity);
        var retried = session.ApplyNatureEncounterVictory(
            "battle:test:first", encounter.EncounterStableId);
        Assert.Equal(first.Revision, retried.Revision);

        var second = session.ApplyNatureEncounterVictory(
            "battle:test:second", encounter.EncounterStableId);
        Assert.Equal(1, Assert.Single(second.RegionalIncidents).RemainingSeverity);
        Assert.Equal(SimulationRegionalIncidentCodes.Active,
            Assert.Single(second.NatureThreat.Encounters).StateCode);
        Assert.Equal(5, second.NatureThreat.Routes.Single(value =>
            value.NatureRouteCode == SimulationRegionalIncidentCodes.NatureToCityHub)
            .EffectivePressure);

        var third = session.ApplyNatureEncounterVictory(
            "battle:test:third", encounter.EncounterStableId);
        Assert.Equal(0, Assert.Single(third.RegionalIncidents).RemainingSeverity);
        Assert.Equal(SimulationRegionalIncidentCodes.Resolved,
            Assert.Single(third.NatureThreat.Encounters).StateCode);
        Assert.Equal(3, third.RegionalCausality.RecoveryScore);
    }

    [Fact]
    public void 자연권위협관찰은_공간을예약하고_압력을바꾸지않으며_저장재생된다()
    {
        var session = new 경영SimulationSessionAggregate(CreateRequest());
        var incidentState = HarvestAndCreateIncident(session);
        var incident = Assert.Single(incidentState.RegionalIncidents);
        var adverse = session.ConfirmRegionalIncidentResponse(incident.EventStableId,
            new SimulationRegionalIncidentResponseConfirmRequest
            {
                CommandId = "command:test:nature-observation-source",
                ExpectedRevision = incidentState.Revision,
                ActorStableId = "actor:test:manager",
                ChoiceStableId = SimulationRegionalIncidentCodes.FarmLeaveExposed,
            });
        var pressureBefore = adverse.NatureThreat.Routes.Single(value =>
            value.NatureRouteCode == SimulationRegionalIncidentCodes.NatureToFarm)
            .EffectivePressure;
        var previewRequest = new SimulationNatureThreatObservationPreviewRequest
        {
            ExpectedRevision = adverse.Revision,
            DecisionStableId = "decision:test:nature-observation",
            TaskStableId = "task:test:nature-observation",
            ActorStableId = "actor:test:scout",
            NatureRouteCode = SimulationRegionalIncidentCodes.NatureToFarm,
            PreferredSpatialStableId =
                PyeongchangSimulation공간StableIds.Nature위협관찰공간,
        };

        var preview = session.PreviewNatureThreatObservation(previewRequest);

        Assert.True(preview.CanConfirm);
        Assert.Equal(pressureBefore, preview.EffectivePressure);
        Assert.Equal(adverse.Revision, session.Revision);
        Assert.Equal(PyeongchangSimulation공간StableIds.Nature위협관찰공간,
            preview.DecisionPreview.SpatialInteraction!.SelectedSpatialStableId);
        Assert.Equal(new[] { "WI-NATURE-02", "WI-NATURE-03" },
            preview.NextWorldInteractionIds);

        var confirmRequest = new SimulationNatureThreatObservationConfirmRequest
        {
            CommandId = "command:test:nature-observation",
            ExpectedRevision = adverse.Revision,
            Preview = previewRequest,
        };
        var confirmed = session.ConfirmNatureThreatObservation(confirmRequest);
        var retried = session.ConfirmNatureThreatObservation(confirmRequest);

        Assert.Equal(confirmed.Revision, retried.Revision);
        var conflictingPreview = new SimulationNatureThreatObservationPreviewRequest
        {
            ExpectedRevision = confirmRequest.ExpectedRevision,
            DecisionStableId = previewRequest.DecisionStableId,
            TaskStableId = previewRequest.TaskStableId,
            ActorStableId = previewRequest.ActorStableId,
            NatureRouteCode = SimulationRegionalIncidentCodes.NatureToTown,
            PreferredSpatialStableId = previewRequest.PreferredSpatialStableId,
        };
        var payloadConflict = Assert.Throws<SimulationConflictException>(() =>
            session.ConfirmNatureThreatObservation(
                new SimulationNatureThreatObservationConfirmRequest
                {
                    CommandId = confirmRequest.CommandId,
                    ExpectedRevision = confirmRequest.ExpectedRevision,
                    Preview = conflictingPreview,
                }));
        Assert.Equal("SimulationCommandPayloadConflict", payloadConflict.ErrorCode);
        Assert.Equal(SimulationTaskStateCodes.Scheduled,
            confirmed.Tasks.Single(value => value.TaskStableId ==
                previewRequest.TaskStableId).StateCode);
        Assert.Contains(confirmed.SpatialReservations, value =>
            value.TaskStableId == previewRequest.TaskStableId
            && value.StatusCode == Simulation공간예약상태Codes.Reserved);
        Assert.Equal(pressureBefore, confirmed.NatureThreat.Routes.Single(value =>
            value.NatureRouteCode == SimulationRegionalIncidentCodes.NatureToFarm)
            .EffectivePressure);

        var completed = session.Advance(new 경영SimulationTick진행Request
        {
            CommandId = "command:test:nature-observation-tick",
            ExpectedRevision = confirmed.Revision,
            TickCount = 1,
        });
        Assert.Equal(SimulationTaskStateCodes.Completed,
            completed.Tasks.Single(value => value.TaskStableId ==
                previewRequest.TaskStableId).StateCode);
        Assert.Equal(SimulationEffectStateCodes.Applied,
            completed.Effects.Single(value => value.EffectTypeCode ==
                SimulationNatureInteractionCodes.NatureThreatObserved).StateCode);
        Assert.Contains(completed.SpatialReservations, value =>
            value.TaskStableId == previewRequest.TaskStableId
            && value.StatusCode == Simulation공간예약상태Codes.Released);
        Assert.Equal(pressureBefore, completed.NatureThreat.Routes.Single(value =>
            value.NatureRouteCode == SimulationRegionalIncidentCodes.NatureToFarm)
            .EffectivePressure);

        var package = session.CreateSavePackage(new SimulationSessionSaveRequest
        {
            SaveStableId = "save:test:nature-observation",
            ExpectedRevision = completed.Revision,
        });
        var restored = SimulationSessionReplay.Restore(package);
        var restoredPackage = restored.CreateSavePackage(new SimulationSessionSaveRequest
        {
            SaveStableId = package.SaveStableId,
            ExpectedRevision = restored.Revision,
        });
        Assert.Equal(package.ReplayHash, restoredPackage.ReplayHash);
        Assert.Contains(restored.Snapshot().Effects, value =>
            value.EffectTypeCode == SimulationNatureInteractionCodes.NatureThreatObserved
            && value.StateCode == SimulationEffectStateCodes.Applied);
    }

    [Fact]
    public async Task 자연권위협관찰Api는_미리보기_확정_완료를왕복한다()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var createResponse = await client.PostAsJsonAsync(
            "/api/simulation/v1/sessions", CreateRequest());
        var created = await createResponse.Content
            .ReadFromJsonAsync<경영SimulationSessionSnapshot>();
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var sessionRoute = "/api/simulation/v1/sessions/"
            + Uri.EscapeDataString(created!.SessionStableId);
        var request = new SimulationNatureThreatObservationPreviewRequest
        {
            ExpectedRevision = created.Revision,
            DecisionStableId = "decision:http:nature-observation",
            TaskStableId = "task:http:nature-observation",
            ActorStableId = "actor:http:scout",
            NatureRouteCode = SimulationRegionalIncidentCodes.NatureToFarm,
            PreferredSpatialStableId =
                PyeongchangSimulation공간StableIds.Nature위협관찰공간,
        };

        var previewResponse = await client.PostAsJsonAsync(
            sessionRoute + "/world-events/nature-threat/observation-previews", request);
        var preview = await previewResponse.Content
            .ReadFromJsonAsync<SimulationNatureThreatObservationPreviewSnapshot>();
        Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);
        Assert.True(preview!.CanConfirm);

        var confirmResponse = await client.PostAsJsonAsync(
            sessionRoute + "/world-events/nature-threat/observations/confirm",
            new SimulationNatureThreatObservationConfirmRequest
            {
                CommandId = "command:http:nature-observation",
                ExpectedRevision = created.Revision,
                Preview = request,
            });
        var confirmed = await confirmResponse.Content
            .ReadFromJsonAsync<경영SimulationSessionSnapshot>();
        Assert.Equal(HttpStatusCode.OK, confirmResponse.StatusCode);
        Assert.Contains(confirmed!.SpatialReservations, value =>
            value.TaskStableId == request.TaskStableId
            && value.StatusCode == Simulation공간예약상태Codes.Reserved);

        var tickResponse = await client.PostAsJsonAsync(sessionRoute + "/ticks",
            new 경영SimulationTick진행Request
            {
                CommandId = "command:http:nature-observation-tick",
                ExpectedRevision = confirmed.Revision,
                TickCount = 1,
            });
        var completed = await tickResponse.Content
            .ReadFromJsonAsync<경영SimulationSessionSnapshot>();
        Assert.Equal(HttpStatusCode.OK, tickResponse.StatusCode);
        Assert.Contains(completed!.Effects, value =>
            value.EffectTypeCode == SimulationNatureInteractionCodes.NatureThreatObserved
            && value.StateCode == SimulationEffectStateCodes.Applied);
    }

    [Fact]
    public void 자연권긴급후퇴는_관찰후_공간을예약하고_파티를안전핵으로인계한다()
    {
        var session = new 경영SimulationSessionAggregate(CreateRequest());
        ObserveThreat(session, SimulationRegionalIncidentCodes.NatureToFarm,
            "retreat-prerequisite");
        var pressureBefore = session.Snapshot().NatureThreat.Routes.Single(value =>
            value.NatureRouteCode == SimulationRegionalIncidentCodes.NatureToFarm)
            .EffectivePressure;
        var request = new SimulationNatureEmergencyRetreatPreviewRequest
        {
            ExpectedRevision = session.Revision,
            DecisionStableId = "decision:test:nature-retreat",
            TaskStableId = "task:test:nature-retreat",
            ActorStableId = "actor:test:player-party",
            NatureRouteCode = SimulationRegionalIncidentCodes.NatureToFarm,
            PreferredSpatialStableId =
                PyeongchangSimulation공간StableIds.Nature긴급후퇴경로,
        };

        var preview = session.PreviewNatureEmergencyRetreat(request);

        Assert.True(preview.CanConfirm);
        Assert.True(preview.HasObservedThreat);
        Assert.False(preview.HasActiveEncounter);
        Assert.Equal(new[] { "WI-NATURE-04" }, preview.NextWorldInteractionIds);
        Assert.Equal(PyeongchangSimulation공간StableIds.Nature긴급후퇴경로,
            preview.DecisionPreview.SpatialInteraction!.SelectedSpatialStableId);
        var confirm = new SimulationNatureEmergencyRetreatConfirmRequest
        {
            CommandId = "command:test:nature-retreat",
            ExpectedRevision = session.Revision,
            Preview = request,
        };
        var confirmed = session.ConfirmNatureEmergencyRetreat(confirm);
        var retried = session.ConfirmNatureEmergencyRetreat(confirm);
        Assert.Equal(confirmed.Revision, retried.Revision);
        Assert.Contains(confirmed.SpatialReservations, value =>
            value.TaskStableId == request.TaskStableId
            && value.ReservationKindCode == Simulation공간용량Codes.EscapeRouteCapacity
            && value.StatusCode == Simulation공간예약상태Codes.Reserved);

        var completed = session.Advance(new 경영SimulationTick진행Request
        {
            CommandId = "command:test:nature-retreat-tick",
            ExpectedRevision = confirmed.Revision,
            TickCount = 1,
        });
        Assert.Equal(SimulationEffectStateCodes.Applied,
            completed.Effects.Single(value => value.EffectTypeCode ==
                SimulationNatureInteractionCodes.PartyRetreatedToSafeCore).StateCode);
        Assert.Contains(completed.SpatialReservations, value =>
            value.TaskStableId == request.TaskStableId
            && value.StatusCode == Simulation공간예약상태Codes.Released);
        Assert.Equal(pressureBefore, completed.NatureThreat.Routes.Single(value =>
            value.NatureRouteCode == SimulationRegionalIncidentCodes.NatureToFarm)
            .EffectivePressure);

        var package = session.CreateSavePackage(new SimulationSessionSaveRequest
        {
            SaveStableId = "save:test:nature-retreat",
            ExpectedRevision = completed.Revision,
        });
        var restored = SimulationSessionReplay.Restore(package);
        var restoredPackage = restored.CreateSavePackage(new SimulationSessionSaveRequest
        {
            SaveStableId = package.SaveStableId,
            ExpectedRevision = restored.Revision,
        });
        Assert.Equal(package.ReplayHash, restoredPackage.ReplayHash);

        var conflictRequest = new SimulationNatureEmergencyRetreatPreviewRequest
        {
            ExpectedRevision = confirm.ExpectedRevision,
            DecisionStableId = request.DecisionStableId,
            TaskStableId = request.TaskStableId,
            ActorStableId = request.ActorStableId,
            NatureRouteCode = SimulationRegionalIncidentCodes.NatureToTown,
            PreferredSpatialStableId = request.PreferredSpatialStableId,
        };
        var conflict = Assert.Throws<SimulationConflictException>(() =>
            session.ConfirmNatureEmergencyRetreat(
                new SimulationNatureEmergencyRetreatConfirmRequest
                {
                    CommandId = confirm.CommandId,
                    ExpectedRevision = confirm.ExpectedRevision,
                    Preview = conflictRequest,
                }));
        Assert.Equal("SimulationCommandPayloadConflict", conflict.ErrorCode);
    }

    [Fact]
    public void 자연권긴급후퇴는_선행관찰_공간_개정을검사하고_취소시예약을반환한다()
    {
        var session = new 경영SimulationSessionAggregate(CreateRequest());
        var request = new SimulationNatureEmergencyRetreatPreviewRequest
        {
            ExpectedRevision = session.Revision,
            DecisionStableId = "decision:test:nature-retreat-blocked",
            TaskStableId = "task:test:nature-retreat-blocked",
            ActorStableId = "actor:test:player-party",
            NatureRouteCode = SimulationRegionalIncidentCodes.NatureToFarm,
        };
        var withoutObservation = session.PreviewNatureEmergencyRetreat(request);
        Assert.False(withoutObservation.CanConfirm);
        Assert.Contains("NatureThreatObservationRequired",
            withoutObservation.BlockingReasonCodes);

        var encounterSession = new 경영SimulationSessionAggregate(CreateRequest());
        var incidentSnapshot = HarvestAndCreateIncident(encounterSession);
        var incident = Assert.Single(incidentSnapshot.RegionalIncidents);
        encounterSession.ConfirmRegionalIncidentResponse(incident.EventStableId,
            new SimulationRegionalIncidentResponseConfirmRequest
            {
                CommandId = "command:test:retreat-active-encounter",
                ExpectedRevision = incidentSnapshot.Revision,
                ActorStableId = "actor:test:manager",
                ChoiceStableId = SimulationRegionalIncidentCodes.FarmLeaveExposed,
            });
        request.ExpectedRevision = encounterSession.Revision;
        request.PreferredSpatialStableId =
            PyeongchangSimulation공간StableIds.Nature긴급후퇴경로;
        var activeEncounter = encounterSession.PreviewNatureEmergencyRetreat(request);
        Assert.True(activeEncounter.CanConfirm);
        Assert.False(activeEncounter.HasObservedThreat);
        Assert.True(activeEncounter.HasActiveEncounter);

        ObserveThreat(session, request.NatureRouteCode, "retreat-cancel");
        request.ExpectedRevision = session.Revision;
        request.PreferredSpatialStableId =
            PyeongchangSimulation공간StableIds.Nature긴급후퇴경로;
        var scheduled = session.ConfirmNatureEmergencyRetreat(
            new SimulationNatureEmergencyRetreatConfirmRequest
            {
                CommandId = "command:test:nature-retreat-cancel",
                ExpectedRevision = session.Revision,
                Preview = request,
            });
        var cancelled = session.CancelTask(request.TaskStableId,
            new SimulationTaskCancelRequest
            {
                CommandId = "command:test:nature-retreat-task-cancel",
                ExpectedRevision = scheduled.Revision,
                ReasonCode = "PlayerCancelled",
            });
        Assert.All(cancelled.SpatialReservations.Where(value =>
            value.TaskStableId == request.TaskStableId), value =>
                Assert.Equal(Simulation공간예약상태Codes.Cancelled, value.StatusCode));

        var noSpaceRequest = CreateRequest();
        noSpaceRequest.SpatialWorld!.Definitions = noSpaceRequest.SpatialWorld.Definitions
            .Where(value => value.SpatialStableId !=
                PyeongchangSimulation공간StableIds.Nature긴급후퇴경로).ToArray();
        var noSpace = new 경영SimulationSessionAggregate(noSpaceRequest);
        ObserveThreat(noSpace, SimulationRegionalIncidentCodes.NatureToFarm,
            "retreat-no-space");
        request.ExpectedRevision = noSpace.Revision;
        var missingSpace = noSpace.PreviewNatureEmergencyRetreat(request);
        Assert.False(missingSpace.CanConfirm);
        Assert.Contains(Simulation공간차단Codes.DefinitionUnavailable,
            missingSpace.BlockingReasonCodes);

        request.PreferredSpatialStableId = string.Empty;
        request.ExpectedRevision = noSpace.Revision + 1;
        var stale = noSpace.PreviewNatureEmergencyRetreat(request);
        Assert.Contains("SimulationExpectedRevisionMismatch", stale.BlockingReasonCodes);
    }

    [Fact]
    public async Task 자연권긴급후퇴Api는_관찰후_미리보기_확정_완료를왕복한다()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var createResponse = await client.PostAsJsonAsync(
            "/api/simulation/v1/sessions", CreateRequest());
        var created = await createResponse.Content
            .ReadFromJsonAsync<경영SimulationSessionSnapshot>();
        var sessionRoute = "/api/simulation/v1/sessions/"
            + Uri.EscapeDataString(created!.SessionStableId);
        var observation = new SimulationNatureThreatObservationPreviewRequest
        {
            ExpectedRevision = created.Revision,
            DecisionStableId = "decision:http:retreat-observation",
            TaskStableId = "task:http:retreat-observation",
            ActorStableId = "actor:http:scout",
            NatureRouteCode = SimulationRegionalIncidentCodes.NatureToFarm,
            PreferredSpatialStableId = PyeongchangSimulation공간StableIds.Nature위협관찰공간,
        };
        var observedResponse = await client.PostAsJsonAsync(
            sessionRoute + "/world-events/nature-threat/observations/confirm",
            new SimulationNatureThreatObservationConfirmRequest
            {
                CommandId = "command:http:retreat-observation",
                ExpectedRevision = created.Revision,
                Preview = observation,
            });
        var observed = await observedResponse.Content
            .ReadFromJsonAsync<경영SimulationSessionSnapshot>();
        var observationTick = await client.PostAsJsonAsync(sessionRoute + "/ticks",
            new 경영SimulationTick진행Request
            {
                CommandId = "command:http:retreat-observation-tick",
                ExpectedRevision = observed!.Revision,
                TickCount = 1,
            });
        var ready = await observationTick.Content
            .ReadFromJsonAsync<경영SimulationSessionSnapshot>();
        var retreat = new SimulationNatureEmergencyRetreatPreviewRequest
        {
            ExpectedRevision = ready!.Revision,
            DecisionStableId = "decision:http:nature-retreat",
            TaskStableId = "task:http:nature-retreat",
            ActorStableId = "actor:http:player-party",
            NatureRouteCode = SimulationRegionalIncidentCodes.NatureToFarm,
            PreferredSpatialStableId = PyeongchangSimulation공간StableIds.Nature긴급후퇴경로,
        };
        var previewResponse = await client.PostAsJsonAsync(
            sessionRoute + "/world-events/nature-threat/retreat-previews", retreat);
        var preview = await previewResponse.Content
            .ReadFromJsonAsync<SimulationNatureEmergencyRetreatPreviewSnapshot>();
        Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);
        Assert.True(preview!.CanConfirm);
        var confirmResponse = await client.PostAsJsonAsync(
            sessionRoute + "/world-events/nature-threat/retreats/confirm",
            new SimulationNatureEmergencyRetreatConfirmRequest
            {
                CommandId = "command:http:nature-retreat",
                ExpectedRevision = ready.Revision,
                Preview = retreat,
            });
        Assert.Equal(HttpStatusCode.OK, confirmResponse.StatusCode);
        var confirmed = await confirmResponse.Content
            .ReadFromJsonAsync<경영SimulationSessionSnapshot>();
        var tickResponse = await client.PostAsJsonAsync(sessionRoute + "/ticks",
            new 경영SimulationTick진행Request
            {
                CommandId = "command:http:nature-retreat-tick",
                ExpectedRevision = confirmed!.Revision,
                TickCount = 1,
            });
        var completed = await tickResponse.Content
            .ReadFromJsonAsync<경영SimulationSessionSnapshot>();
        Assert.Equal(HttpStatusCode.OK, tickResponse.StatusCode);
        Assert.Contains(completed!.Effects, value => value.EffectTypeCode ==
            SimulationNatureInteractionCodes.PartyRetreatedToSafeCore
            && value.StateCode == SimulationEffectStateCodes.Applied);
    }

    [Fact]
    public void 자연권복원은_관찰한원인이해결된경로의_공간과자재를사용한다()
    {
        var session = new 경영SimulationSessionAggregate(CreateRequest());
        var resolved = PrepareResolvedObservedNatureRoute(session, "restoration");
        var request = RestorationRequest(session.Revision, "restoration");
        var pressureBefore = resolved.NatureThreat.Routes.Single(value =>
            value.NatureRouteCode == request.NatureRouteCode).EffectivePressure;
        var threatBefore = resolved.RegionalCausality.ThreatScore;
        var recoveryBefore = resolved.RegionalCausality.RecoveryScore;

        var preview = session.PreviewNatureRestoration(request);

        Assert.True(preview.CanConfirm);
        Assert.Single(preview.ResolvedCauseIncidentStableIds);
        Assert.Equal(new[] { "WI-NATURE-04" }, preview.NextWorldInteractionIds);
        Assert.Equal(PyeongchangSimulation공간StableIds.Nature복원작업공간,
            preview.DecisionPreview.SpatialInteraction!.SelectedSpatialStableId);
        var confirm = new SimulationNatureRestorationConfirmRequest
        {
            CommandId = "command:test:nature-restoration",
            ExpectedRevision = session.Revision,
            Preview = request,
        };
        var confirmed = session.ConfirmNatureRestoration(confirm);
        var retried = session.ConfirmNatureRestoration(confirm);
        Assert.Equal(confirmed.Revision, retried.Revision);
        Assert.Contains(confirmed.SpatialReservations, value =>
            value.TaskStableId == request.TaskStableId
            && value.ReservationKindCode == Simulation공간용량Codes.RestorationMaterial
            && value.StatusCode == Simulation공간예약상태Codes.Reserved);

        var completed = session.Advance(new 경영SimulationTick진행Request
        {
            CommandId = "command:test:nature-restoration-tick",
            ExpectedRevision = confirmed.Revision,
            TickCount = 1,
        });
        Assert.Equal(SimulationEffectStateCodes.Applied,
            completed.Effects.Single(value => value.EffectTypeCode ==
                SimulationNatureInteractionCodes.NatureRouteRestored).StateCode);
        Assert.Contains(completed.SpatialReservations, value =>
            value.TaskStableId == request.TaskStableId
            && value.ReservationKindCode == Simulation공간용량Codes.RestorationMaterial
            && value.StatusCode == Simulation공간예약상태Codes.Consumed);
        Assert.Contains(completed.SpatialReservations, value =>
            value.TaskStableId == request.TaskStableId
            && value.ReservationKindCode == Simulation공간용량Codes.WorkArea
            && value.StatusCode == Simulation공간예약상태Codes.Released);
        Assert.Equal(pressureBefore, completed.NatureThreat.Routes.Single(value =>
            value.NatureRouteCode == request.NatureRouteCode).EffectivePressure);
        Assert.Equal(Math.Max(0, threatBefore - 1),
            completed.RegionalCausality.ThreatScore);
        Assert.Equal(recoveryBefore + 1,
            completed.RegionalCausality.RecoveryScore);
        Assert.Contains(completed.RegionalCausality.Changes, value =>
            value.SourceCode ==
            SimulationRegionalIncidentCodes.NatureRestorationCompleted);
        request.ExpectedRevision = completed.Revision;
        Assert.Contains("NatureRouteAlreadyRestored",
            session.PreviewNatureRestoration(request).BlockingReasonCodes);

        var package = session.CreateSavePackage(new SimulationSessionSaveRequest
        {
            SaveStableId = "save:test:nature-restoration",
            ExpectedRevision = completed.Revision,
        });
        var restored = SimulationSessionReplay.Restore(package);
        var restoredPackage = restored.CreateSavePackage(new SimulationSessionSaveRequest
        {
            SaveStableId = package.SaveStableId,
            ExpectedRevision = restored.Revision,
        });
        Assert.Equal(package.ReplayHash, restoredPackage.ReplayHash);
    }

    [Fact]
    public void 자연권복원은_미해결원인_공간부재_낮은개정을차단하고_취소예약을반환한다()
    {
        var unresolved = new 경영SimulationSessionAggregate(CreateRequest());
        var adverse = CreateAdverseObservedNatureRoute(unresolved, "unresolved");
        var request = RestorationRequest(unresolved.Revision, "unresolved");
        var unresolvedPreview = unresolved.PreviewNatureRestoration(request);
        Assert.False(unresolvedPreview.CanConfirm);
        Assert.Contains("NatureIncidentCauseUnresolved",
            unresolvedPreview.BlockingReasonCodes);
        Assert.True(adverse.NatureThreat.Routes.Single(value =>
            value.NatureRouteCode == request.NatureRouteCode).EffectivePressure > 0);

        var cancellable = new 경영SimulationSessionAggregate(CreateRequest());
        PrepareResolvedObservedNatureRoute(cancellable, "restoration-cancel");
        request = RestorationRequest(cancellable.Revision, "restoration-cancel");
        var scheduled = cancellable.ConfirmNatureRestoration(
            new SimulationNatureRestorationConfirmRequest
            {
                CommandId = "command:test:nature-restoration-cancel",
                ExpectedRevision = cancellable.Revision,
                Preview = request,
            });
        var cancelled = cancellable.CancelTask(request.TaskStableId,
            new SimulationTaskCancelRequest
            {
                CommandId = "command:test:nature-restoration-task-cancel",
                ExpectedRevision = scheduled.Revision,
                ReasonCode = "PlayerCancelled",
            });
        Assert.All(cancelled.SpatialReservations.Where(value =>
            value.TaskStableId == request.TaskStableId), value =>
                Assert.Equal(Simulation공간예약상태Codes.Cancelled, value.StatusCode));

        var missingRequest = CreateRequest();
        missingRequest.SpatialWorld!.Definitions = missingRequest.SpatialWorld.Definitions
            .Where(value => value.SpatialStableId !=
                PyeongchangSimulation공간StableIds.Nature복원작업공간).ToArray();
        var missing = new 경영SimulationSessionAggregate(missingRequest);
        PrepareResolvedObservedNatureRoute(missing, "restoration-missing");
        request = RestorationRequest(missing.Revision, "restoration-missing");
        var missingPreview = missing.PreviewNatureRestoration(request);
        Assert.Contains(Simulation공간차단Codes.DefinitionUnavailable,
            missingPreview.BlockingReasonCodes);
        request.ExpectedRevision = missing.Revision + 1;
        Assert.Contains("SimulationExpectedRevisionMismatch",
            missing.PreviewNatureRestoration(request).BlockingReasonCodes);
    }

    [Fact]
    public async Task 자연권복원Api는_해결된원인에서_미리보기_확정_완료를왕복한다()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var createResponse = await client.PostAsJsonAsync(
            "/api/simulation/v1/sessions", CreateRequest());
        var created = await createResponse.Content
            .ReadFromJsonAsync<경영SimulationSessionSnapshot>();
        var accessor = factory.Services.GetRequiredService<경영SimulationSessionAccessor>();
        var session = accessor.Require(created!.SessionStableId);
        PrepareResolvedObservedNatureRoute(session, "restoration-http");
        var request = RestorationRequest(session.Revision, "restoration-http");
        var sessionRoute = "/api/simulation/v1/sessions/"
            + Uri.EscapeDataString(created.SessionStableId);

        var previewResponse = await client.PostAsJsonAsync(
            sessionRoute + "/world-events/nature-threat/restoration-previews", request);
        var preview = await previewResponse.Content
            .ReadFromJsonAsync<SimulationNatureRestorationPreviewSnapshot>();
        Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);
        Assert.True(preview!.CanConfirm);
        var confirmResponse = await client.PostAsJsonAsync(
            sessionRoute + "/world-events/nature-threat/restorations/confirm",
            new SimulationNatureRestorationConfirmRequest
            {
                CommandId = "command:http:nature-restoration",
                ExpectedRevision = session.Revision,
                Preview = request,
            });
        var confirmed = await confirmResponse.Content
            .ReadFromJsonAsync<경영SimulationSessionSnapshot>();
        var tickResponse = await client.PostAsJsonAsync(sessionRoute + "/ticks",
            new 경영SimulationTick진행Request
            {
                CommandId = "command:http:nature-restoration-tick",
                ExpectedRevision = confirmed!.Revision,
                TickCount = 1,
            });
        var completed = await tickResponse.Content
            .ReadFromJsonAsync<경영SimulationSessionSnapshot>();
        Assert.Equal(HttpStatusCode.OK, tickResponse.StatusCode);
        Assert.Contains(completed!.Effects, value => value.EffectTypeCode ==
            SimulationNatureInteractionCodes.NatureRouteRestored
            && value.StateCode == SimulationEffectStateCodes.Applied);
    }

    [Fact]
    public void Nature기간은_같은Balance로_광복기강화와_암흑기회복복귀를결정한다()
    {
        var gwangbokRequest = CreateRequest();
        gwangbokRequest.NatureMind = NatureMindRequest(8m, 2m);
        var gwangbok = new 경영SimulationSessionAggregate(gwangbokRequest);
        PrepareRetreatedParty(gwangbok, "period-gwangbok");
        var gwangbokPreview = gwangbok.PreviewNaturePartyRecovery(
            PartyRecoveryRequest(gwangbok.Revision, "period-gwangbok"));
        var gwangbokPeriod = Assert.Single(gwangbok.Snapshot().NatureMind.Periods);

        Assert.Equal(SimulationNaturePeriodCodes.GwangbokPeriod,
            gwangbokPeriod.PeriodStateCode);
        Assert.Equal(1, gwangbokPreview.EffectiveDurationTicks);
        Assert.Equal(-1, gwangbokPeriod.WorkDurationModifierTicks);
        Assert.Contains(SimulationNaturePeriodCodes.GwangbokRevelationCandidate,
            gwangbokPeriod.CandidateStableIds);

        var darkAgeRequest = CreateRequest();
        darkAgeRequest.NatureMind = NatureMindRequest(2m, 8m);
        var darkAge = new 경영SimulationSessionAggregate(darkAgeRequest);
        PrepareRetreatedParty(darkAge, "period-dark-age");
        var recoveryRequest = PartyRecoveryRequest(
            darkAge.Revision, "period-dark-age");
        var darkPreview = darkAge.PreviewNaturePartyRecovery(recoveryRequest);
        var darkBefore = Assert.Single(darkAge.Snapshot().NatureMind.Periods);

        Assert.Equal(SimulationNaturePeriodCodes.DarkAgePeriod,
            darkBefore.PeriodStateCode);
        Assert.Equal(3, darkPreview.EffectiveDurationTicks);
        Assert.Equal(1, darkBefore.WorkDurationModifierTicks);
        Assert.Contains(SimulationNaturePeriodCodes.DarkAgeRecoveryWorldInteraction,
            darkBefore.CandidateStableIds);

        var confirmed = darkAge.ConfirmNaturePartyRecovery(
            new SimulationNaturePartyRecoveryConfirmRequest
            {
                CommandId = "command:test:nature-period-recovery",
                ExpectedRevision = darkAge.Revision,
                Preview = recoveryRequest,
            });
        var completed = darkAge.Advance(new 경영SimulationTick진행Request
        {
            CommandId = "command:test:nature-period-recovery-ticks",
            ExpectedRevision = confirmed.Revision,
            TickCount = darkPreview.EffectiveDurationTicks,
        });
        var ordinary = Assert.Single(completed.NatureMind.Periods);
        var history = Assert.Single(completed.NatureMind.PeriodHistory);

        Assert.Equal(SimulationNaturePeriodCodes.OrdinaryPeriod,
            ordinary.PeriodStateCode);
        Assert.Equal(SimulationNaturePeriodCodes.DarkAgePeriod,
            history.StateCode);
        Assert.NotNull(history.ExitTick);
        Assert.Contains(completed.NatureMind.PeriodTransitionEffects, value =>
            value.EffectTypeCode == SimulationNaturePeriodCodes.EnteredEffect
            && value.StateCode == SimulationNaturePeriodCodes.DarkAgePeriod);
        Assert.Contains(completed.NatureMind.PeriodTransitionEffects, value =>
            value.EffectTypeCode == SimulationNaturePeriodCodes.ExitedEffect
            && value.PeriodInstanceStableId == history.PeriodInstanceStableId);

        var package = darkAge.CreateSavePackage(new SimulationSessionSaveRequest
        {
            SaveStableId = "save:test:nature-period-dark-age",
            ExpectedRevision = completed.Revision,
        });
        var restored = SimulationSessionReplay.Restore(package);
        var restoredPackage = restored.CreateSavePackage(
            new SimulationSessionSaveRequest
            {
                SaveStableId = package.SaveStableId,
                ExpectedRevision = restored.Revision,
            });
        Assert.Equal(package.ReplayHash, restoredPackage.ReplayHash);
        Assert.Equal(ordinary.PeriodStateHashSha256,
            Assert.Single(restored.Snapshot().NatureMind.Periods)
                .PeriodStateHashSha256);
    }

    [Fact]
    public void Farm경로안전은_다음WorldTick에서끝나고_같은원인기회는한번만발급된다()
    {
        var session = new 경영SimulationSessionAggregate(CreateRequest());
        var incidentState = HarvestAndCreateIncident(session);
        var incident = Assert.Single(incidentState.RegionalIncidents);
        var adverse = session.ConfirmRegionalIncidentResponse(incident.EventStableId,
            new SimulationRegionalIncidentResponseConfirmRequest
            {
                CommandId = "command:test:farm-safety-source",
                ExpectedRevision = incidentState.Revision,
                ActorStableId = "actor:test:manager",
                ChoiceStableId = SimulationRegionalIncidentCodes.FarmLeaveExposed,
            });
        var encounter = Assert.Single(adverse.NatureThreat.Encounters);
        var secured = session.ApplyNatureEncounterVictory(
            "battle:test:farm-safety-first", encounter.EncounterStableId);

        Assert.Equal(SimulationRegionalIncidentCodes.Resolved,
            Assert.Single(secured.NatureThreat.Encounters).StateCode);
        Assert.Single(secured.RegionalDevelopment.Opportunities);

        var nextTick = session.Advance(new 경영SimulationTick진행Request
        {
            CommandId = "command:test:farm-safety-next-tick",
            ExpectedRevision = secured.Revision,
            TickCount = 1,
        });

        Assert.Equal(SimulationRegionalIncidentCodes.Active,
            Assert.Single(nextTick.NatureThreat.Encounters).StateCode);
        Assert.Single(nextTick.RegionalDevelopment.Opportunities);
        var secondVictory = session.ApplyNatureEncounterVictory(
            "battle:test:farm-safety-second", encounter.EncounterStableId);
        Assert.Single(secondVictory.RegionalDevelopment.Opportunities);
        Assert.Equal(2, Assert.Single(secondVictory.RegionalIncidents).RemainingSeverity);
    }

    [Fact]
    public void 파티회복은_후퇴후_안전생활핵공간과보급을사용하고_탐색을다시연다()
    {
        var session = new 경영SimulationSessionAggregate(CreateRequest());
        PrepareRetreatedParty(session, "party-recovery");
        var request = PartyRecoveryRequest(session.Revision, "party-recovery");
        var pressureBefore = session.Snapshot().NatureThreat.Routes.Single(value =>
            value.NatureRouteCode == request.NatureRouteCode).EffectivePressure;
        var causalityBefore = session.Snapshot().RegionalCausality;

        var preview = session.PreviewNaturePartyRecovery(request);

        Assert.True(preview.CanConfirm);
        Assert.True(preview.HasRetreatPredecessor);
        Assert.False(preview.HasRestorationPredecessor);
        Assert.Equal("Explore", preview.NextPlayerActionCode);
        Assert.Equal(PyeongchangSimulation공간StableIds.Nature안전회복야영지,
            preview.DecisionPreview.SpatialInteraction!.SelectedSpatialStableId);
        var confirm = new SimulationNaturePartyRecoveryConfirmRequest
        {
            CommandId = "command:test:nature-party-recovery",
            ExpectedRevision = session.Revision,
            Preview = request,
        };
        var confirmed = session.ConfirmNaturePartyRecovery(confirm);
        var retried = session.ConfirmNaturePartyRecovery(confirm);
        Assert.Equal(confirmed.Revision, retried.Revision);
        Assert.Contains(confirmed.SpatialReservations, value =>
            value.TaskStableId == request.TaskStableId
            && value.ReservationKindCode == Simulation공간용량Codes.RecoverySupply
            && value.StatusCode == Simulation공간예약상태Codes.Reserved);

        var completed = session.Advance(new 경영SimulationTick진행Request
        {
            CommandId = "command:test:nature-party-recovery-tick",
            ExpectedRevision = confirmed.Revision,
            TickCount = 1,
        });
        Assert.Equal(SimulationEffectStateCodes.Applied,
            completed.Effects.Single(value => value.EffectTypeCode ==
                SimulationNatureInteractionCodes.PartyRecovered).StateCode);
        Assert.Contains(completed.SpatialReservations, value =>
            value.TaskStableId == request.TaskStableId
            && value.ReservationKindCode == Simulation공간용량Codes.RecoverySupply
            && value.StatusCode == Simulation공간예약상태Codes.Consumed);
        Assert.Contains(completed.SpatialReservations, value =>
            value.TaskStableId == request.TaskStableId
            && value.ReservationKindCode == Simulation공간용량Codes.RestAreaParty
            && value.StatusCode == Simulation공간예약상태Codes.Released);
        Assert.Equal(Math.Max(0, pressureBefore - 2), completed.NatureThreat.Routes.Single(value =>
            value.NatureRouteCode == request.NatureRouteCode).EffectivePressure);
        Assert.Equal(Math.Max(0, causalityBefore.ThreatScore - 1),
            completed.RegionalCausality.ThreatScore);
        Assert.Equal(causalityBefore.RecoveryScore + 1,
            completed.RegionalCausality.RecoveryScore);
        Assert.Contains(completed.RegionalCausality.Changes, value =>
            value.SourceCode ==
            SimulationRegionalIncidentCodes.NaturePartyRecoveryCompleted);
        request.ExpectedRevision = completed.Revision;
        Assert.Contains("PartyAlreadyRecovered",
            session.PreviewNaturePartyRecovery(request).BlockingReasonCodes);

        var package = session.CreateSavePackage(new SimulationSessionSaveRequest
        {
            SaveStableId = "save:test:nature-party-recovery",
            ExpectedRevision = completed.Revision,
        });
        var restored = SimulationSessionReplay.Restore(package);
        var restoredPackage = restored.CreateSavePackage(new SimulationSessionSaveRequest
        {
            SaveStableId = package.SaveStableId,
            ExpectedRevision = restored.Revision,
        });
        Assert.Equal(package.ReplayHash, restoredPackage.ReplayHash);
    }

    [Fact]
    public void 파티회복은_복원효과도선행으로받고_미완료_공간부재_취소를검사한다()
    {
        var missingPredecessor = new 경영SimulationSessionAggregate(CreateRequest());
        var request = PartyRecoveryRequest(missingPredecessor.Revision, "no-predecessor");
        Assert.Contains("NatureRecoveryPrerequisiteMissing",
            missingPredecessor.PreviewNaturePartyRecovery(request).BlockingReasonCodes);

        var restoredRoute = new 경영SimulationSessionAggregate(CreateRequest());
        PrepareRestoredRoute(restoredRoute, "recovery-restored-route");
        request = PartyRecoveryRequest(restoredRoute.Revision, "restored-route");
        var restorationPreview = restoredRoute.PreviewNaturePartyRecovery(request);
        Assert.True(restorationPreview.CanConfirm);
        Assert.False(restorationPreview.HasRetreatPredecessor);
        Assert.True(restorationPreview.HasRestorationPredecessor);

        var cancellable = new 경영SimulationSessionAggregate(CreateRequest());
        PrepareRetreatedParty(cancellable, "recovery-cancel");
        request = PartyRecoveryRequest(cancellable.Revision, "recovery-cancel");
        var scheduled = cancellable.ConfirmNaturePartyRecovery(
            new SimulationNaturePartyRecoveryConfirmRequest
            {
                CommandId = "command:test:nature-recovery-cancel",
                ExpectedRevision = cancellable.Revision,
                Preview = request,
            });
        var cancelled = cancellable.CancelTask(request.TaskStableId,
            new SimulationTaskCancelRequest
            {
                CommandId = "command:test:nature-recovery-task-cancel",
                ExpectedRevision = scheduled.Revision,
                ReasonCode = "PlayerCancelled",
            });
        Assert.All(cancelled.SpatialReservations.Where(value =>
            value.TaskStableId == request.TaskStableId), value =>
                Assert.Equal(Simulation공간예약상태Codes.Cancelled, value.StatusCode));

        var missingSpaceRequest = CreateRequest();
        missingSpaceRequest.SpatialWorld!.Definitions = missingSpaceRequest.SpatialWorld.Definitions
            .Where(value => value.SpatialStableId !=
                PyeongchangSimulation공간StableIds.Nature안전회복야영지).ToArray();
        var missingSpace = new 경영SimulationSessionAggregate(missingSpaceRequest);
        PrepareRetreatedParty(missingSpace, "recovery-missing-space");
        request = PartyRecoveryRequest(missingSpace.Revision, "recovery-missing-space");
        Assert.Contains(Simulation공간차단Codes.DefinitionUnavailable,
            missingSpace.PreviewNaturePartyRecovery(request).BlockingReasonCodes);
        request.ExpectedRevision = missingSpace.Revision + 1;
        Assert.Contains("SimulationExpectedRevisionMismatch",
            missingSpace.PreviewNaturePartyRecovery(request).BlockingReasonCodes);
    }

    [Fact]
    public async Task 파티회복Api는_후퇴후_미리보기_확정_완료를왕복한다()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var createResponse = await client.PostAsJsonAsync(
            "/api/simulation/v1/sessions", CreateRequest());
        var created = await createResponse.Content
            .ReadFromJsonAsync<경영SimulationSessionSnapshot>();
        var session = factory.Services.GetRequiredService<경영SimulationSessionAccessor>()
            .Require(created!.SessionStableId);
        PrepareRetreatedParty(session, "recovery-http");
        var request = PartyRecoveryRequest(session.Revision, "recovery-http");
        var sessionRoute = "/api/simulation/v1/sessions/"
            + Uri.EscapeDataString(created.SessionStableId);
        var previewResponse = await client.PostAsJsonAsync(
            sessionRoute + "/world-events/nature-threat/party-recovery-previews", request);
        var preview = await previewResponse.Content
            .ReadFromJsonAsync<SimulationNaturePartyRecoveryPreviewSnapshot>();
        Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);
        Assert.True(preview!.CanConfirm);
        var confirmResponse = await client.PostAsJsonAsync(
            sessionRoute + "/world-events/nature-threat/party-recoveries/confirm",
            new SimulationNaturePartyRecoveryConfirmRequest
            {
                CommandId = "command:http:nature-party-recovery",
                ExpectedRevision = session.Revision,
                Preview = request,
            });
        var confirmed = await confirmResponse.Content
            .ReadFromJsonAsync<경영SimulationSessionSnapshot>();
        var tickResponse = await client.PostAsJsonAsync(sessionRoute + "/ticks",
            new 경영SimulationTick진행Request
            {
                CommandId = "command:http:nature-party-recovery-tick",
                ExpectedRevision = confirmed!.Revision,
                TickCount = 1,
            });
        var completed = await tickResponse.Content
            .ReadFromJsonAsync<경영SimulationSessionSnapshot>();
        Assert.Equal(HttpStatusCode.OK, tickResponse.StatusCode);
        Assert.Contains(completed!.Effects, value => value.EffectTypeCode ==
            SimulationNatureInteractionCodes.PartyRecovered
            && value.StateCode == SimulationEffectStateCodes.Applied);
    }

    [Fact]
    public void 자연권위협관찰은_낮은개정_잘못된경로_공간부족을차단한다()
    {
        var request = CreateRequest();
        request.SpatialWorld!.Definitions = request.SpatialWorld.Definitions.Where(value =>
            value.SpatialStableId != PyeongchangSimulation공간StableIds.Nature위협관찰공간)
            .ToArray();
        var session = new 경영SimulationSessionAggregate(request);
        var baseRequest = new SimulationNatureThreatObservationPreviewRequest
        {
            ExpectedRevision = session.Revision,
            DecisionStableId = "decision:test:nature-observation-blocked",
            TaskStableId = "task:test:nature-observation-blocked",
            ActorStableId = "actor:test:scout",
            NatureRouteCode = SimulationRegionalIncidentCodes.NatureToFarm,
            PreferredSpatialStableId =
                PyeongchangSimulation공간StableIds.Nature위협관찰공간,
        };

        var missingSpace = session.PreviewNatureThreatObservation(baseRequest);
        Assert.False(missingSpace.CanConfirm);
        Assert.Contains(Simulation공간차단Codes.DefinitionUnavailable,
            missingSpace.BlockingReasonCodes);

        baseRequest.NatureRouteCode = "UnknownNatureRoute";
        var missingRoute = session.PreviewNatureThreatObservation(baseRequest);
        Assert.False(missingRoute.CanConfirm);
        Assert.Contains("NatureThreatRouteUnavailable", missingRoute.BlockingReasonCodes);

        baseRequest.NatureRouteCode = SimulationRegionalIncidentCodes.NatureToFarm;
        baseRequest.ExpectedRevision = session.Revision + 1;
        var stale = session.PreviewNatureThreatObservation(baseRequest);
        Assert.False(stale.CanConfirm);
        Assert.Contains("SimulationExpectedRevisionMismatch", stale.BlockingReasonCodes);
        Assert.Equal(0, session.Revision);

        var cancellable = new 경영SimulationSessionAggregate(CreateRequest());
        var cancellablePreview = new SimulationNatureThreatObservationPreviewRequest
        {
            ExpectedRevision = cancellable.Revision,
            DecisionStableId = "decision:test:nature-observation-cancel",
            TaskStableId = "task:test:nature-observation-cancel",
            ActorStableId = "actor:test:scout",
            NatureRouteCode = SimulationRegionalIncidentCodes.NatureToFarm,
            PreferredSpatialStableId =
                PyeongchangSimulation공간StableIds.Nature위협관찰공간,
        };
        var scheduled = cancellable.ConfirmNatureThreatObservation(
            new SimulationNatureThreatObservationConfirmRequest
            {
                CommandId = "command:test:nature-observation-cancel",
                ExpectedRevision = cancellable.Revision,
                Preview = cancellablePreview,
            });
        var cancelled = cancellable.CancelTask(cancellablePreview.TaskStableId,
            new SimulationTaskCancelRequest
            {
                CommandId = "command:test:nature-observation-task-cancel",
                ExpectedRevision = scheduled.Revision,
                ReasonCode = "PlayerCancelled",
            });
        Assert.Equal(SimulationTaskStateCodes.Cancelled,
            cancelled.Tasks.Single(value => value.TaskStableId ==
                cancellablePreview.TaskStableId).StateCode);
        Assert.All(cancelled.SpatialReservations.Where(value =>
            value.TaskStableId == cancellablePreview.TaskStableId), value =>
                Assert.Equal(Simulation공간예약상태Codes.Cancelled, value.StatusCode));
    }

    private static SimulationRegionalIncidentSnapshot Incident(
        string id, string route, int remaining)
        => new()
        {
            IncidentStableId = id,
            NatureRouteCode = route,
            RemainingSeverity = remaining,
            OccurredWorldTick = 1,
        };

    private static 경영SimulationSessionSnapshot HarvestAndCreateIncident(
        경영SimulationSessionAggregate session)
    {
        session.ConfirmFarmWork(new SimulationFarmWorkConfirmRequest
        {
            CommandId = "command:test:harvest",
            ExpectedRevision = session.Revision,
            ActorStableId = "actor:test:farmer",
            TargetStableId = "cultivation:test:potato",
            ActionCode = SimulationFarmSurvivalCodes.Harvesting,
            AssignmentKindCode = SimulationFarmSurvivalCodes.PlayerDirect,
        });
        return session.Advance(new 경영SimulationTick진행Request
        {
            CommandId = "command:test:harvest-tick",
            ExpectedRevision = session.Revision,
            TickCount = 1,
        });
    }

    private static 경영SimulationSession생성Request CreateRequest()
        => new()
        {
            ClientRequestId = Guid.Parse("90c74be8-45dc-4cb2-ad25-8607ba4a347b"),
            ScenarioStableId = "scenario:test.regional-incident",
            ScenarioDataRevision = "scenario-data:test:r1",
            ScenarioSeed = 20260818,
            RuleRevision = "rule:test:regional-incident:r1",
            DurationTicks = 30,
            WorldContext = new SimulationWorldContext생성Request
            {
                FactionStableId = "faction:test:player",
                TerritoryStableId = "territory:test:pyeongchang",
                SettlementStableId = "settlement:test:world",
                GameDateStartsOn = new DateTimeOffset(
                    2026, 8, 18, 0, 0, 0, TimeSpan.Zero),
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
                FoodEquivalentRuleRevision = "food-equivalent:regional-incident.r1",
                Districts = new[]
                {
                    new SimulationSettlementDistrictRequest
                    {
                        DistrictStableId = "district:test:farm",
                        DistrictTypeCode = "Farm",
                        SourceStableIds = new[] { "source:test:regional-incident" },
                    },
                },
                Facilities = new[]
                {
                    new SimulationSettlementFacilityRequest
                    {
                        FacilityStableId = "facility:test:farm",
                        FacilityTypeCode = "FarmPacking",
                        DistrictStableId = "district:test:farm",
                        SourceStableIds = new[] { "source:test:regional-incident" },
                    },
                    new SimulationSettlementFacilityRequest
                    {
                        FacilityStableId = "facility:test:storage",
                        FacilityTypeCode = SimulationSettlementFacilityTypeCodes.Storage,
                        DistrictStableId = "district:test:farm",
                        SourceStableIds = new[] { "source:test:regional-incident" },
                    },
                    new SimulationSettlementFacilityRequest
                    {
                        FacilityStableId = "facility:test:market",
                        FacilityTypeCode = SimulationSettlementFacilityTypeCodes.Market,
                        DistrictStableId = "district:test:farm",
                        SourceStableIds = new[] { "source:test:regional-incident" },
                    },
                },
                MarketSupplyByProduct = Array.Empty<SimulationMarketSupplyRequest>(),
                SourceStableIds = new[] { "source:test:regional-incident" },
            },
            SpatialWorld = new Simulation공간세계InitialStateRequest
            {
                Definitions = PyeongchangSimulation공간상호작용Fixture.CreateFarmHubSupply(
                        "facility:test:farm", "facility:test:market").Definitions
                    .Concat(PyeongchangSimulation공간상호작용Fixture
                        .CreateNatureThreatResponse().Definitions).ToArray(),
            },
            FarmSurvival = new SimulationFarmSurvivalInitialStateRequest
            {
                RuleRevision = SimulationFarmSurvivalCodes.RuleRevision,
                RegionStableId = "region:test:farm",
                AreaStableId = "area:test:farm",
                TileKey = "kr5186:l2:700:1145",
                FarmBuildingStableId = "facility:test:farm",
                Actors =
                [
                    new SimulationFarmActorInitialStateRequest
                    {
                        ActorStableId = "actor:test:farmer",
                        ActorKindCode = SimulationFarmSurvivalCodes.Player,
                        KoreanName = "시험 농장 작업자",
                        CapabilityCodes =
                        [
                            SimulationFarmActorCapabilityCodes.FarmHarvest,
                            SimulationFarmActorCapabilityCodes.FarmCollection,
                            SimulationFarmActorCapabilityCodes.FarmPacking,
                        ],
                    },
                ],
                CultivationUnits =
                [
                    new Simulation재배단위Snapshot
                    {
                        CultivationUnitStableId = "cultivation:test:potato",
                        Revision = 1,
                        TileStableId = "soil:test:potato",
                        CultivationStableId = "crop:test:potato",
                        ProductStableId = "product:potato",
                        CropVariantStableId = "crop-variant:potato.fixture",
                        StateCode = Simulation재배단위상태Codes.HarvestReady,
                        PhysicalAreaSquareMeters = 100m,
                        EffectiveCultivationAreaRatio = 1m,
                        SourceStableIds = ["source:test:cultivation"],
                    },
                ],
                PotatoProductionRule = new Simulation감자생산RuleSnapshot
                {
                    RuleStableId = "rule:test:potato-production",
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
                    SourceStableIds = ["source:test:potato-rule"],
                    Limitations = ["시험 전용"],
                },
            },
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

    private static void ObserveThreat(경영SimulationSessionAggregate session,
        string routeCode, string suffix)
    {
        var request = new SimulationNatureThreatObservationPreviewRequest
        {
            ExpectedRevision = session.Revision,
            DecisionStableId = "decision:test:nature-observation:" + suffix,
            TaskStableId = "task:test:nature-observation:" + suffix,
            ActorStableId = "actor:test:scout",
            NatureRouteCode = routeCode,
            PreferredSpatialStableId = PyeongchangSimulation공간StableIds.Nature위협관찰공간,
        };
        var confirmed = session.ConfirmNatureThreatObservation(
            new SimulationNatureThreatObservationConfirmRequest
            {
                CommandId = "command:test:nature-observation:" + suffix,
                ExpectedRevision = session.Revision,
                Preview = request,
            });
        session.Advance(new 경영SimulationTick진행Request
        {
            CommandId = "command:test:nature-observation-tick:" + suffix,
            ExpectedRevision = confirmed.Revision,
            TickCount = 1,
        });
    }

    private static 경영SimulationSessionSnapshot CreateAdverseObservedNatureRoute(
        경영SimulationSessionAggregate session, string suffix)
    {
        var harvested = HarvestAndCreateIncident(session);
        var incident = Assert.Single(harvested.RegionalIncidents);
        var adverse = session.ConfirmRegionalIncidentResponse(incident.EventStableId,
            new SimulationRegionalIncidentResponseConfirmRequest
            {
                CommandId = "command:test:nature-adverse:" + suffix,
                ExpectedRevision = harvested.Revision,
                ActorStableId = "actor:test:manager",
                ChoiceStableId = SimulationRegionalIncidentCodes.FarmLeaveExposed,
            });
        ObserveThreat(session, SimulationRegionalIncidentCodes.NatureToFarm,
            "cause:" + suffix);
        return adverse;
    }

    private static 경영SimulationSessionSnapshot PrepareResolvedObservedNatureRoute(
        경영SimulationSessionAggregate session, string suffix)
    {
        CreateAdverseObservedNatureRoute(session, suffix);
        var harvestLotId = Assert.Single(session.Snapshot().FarmSurvival!.HarvestLots)
            .HarvestLotStableId;
        var collected = RunFarmCauseWork(session, suffix + ":collect", harvestLotId,
            SimulationFarmSurvivalCodes.HarvestCollection,
            PyeongchangSimulation공간StableIds.대관령Farm집하공간);
        ApplyHubChoice(session, suffix);
        var packed = RunFarmCauseWork(session, suffix + ":pack", harvestLotId,
            SimulationFarmSurvivalCodes.OutboundPacking,
            PyeongchangSimulation공간StableIds.대관령Farm포장공간);
        Assert.Equal(SimulationRegionalIncidentCodes.Resolved,
            Assert.Single(packed.RegionalIncidents).StateCode);
        return packed;
    }

    private static 경영SimulationSessionSnapshot RunFarmCauseWork(
        경영SimulationSessionAggregate session, string suffix, string targetStableId,
        string actionCode, string spatialStableId)
    {
        var confirmed = session.ConfirmFarmWork(new SimulationFarmWorkConfirmRequest
        {
            CommandId = "command:test:nature-cause-work:" + suffix,
            ExpectedRevision = session.Revision,
            ActorStableId = "actor:test:farmer",
            TargetStableId = targetStableId,
            ActionCode = actionCode,
            AssignmentKindCode = SimulationFarmSurvivalCodes.PlayerDirect,
            PreferredSpatialStableId = spatialStableId,
        });
        return session.Advance(new 경영SimulationTick진행Request
        {
            CommandId = "command:test:nature-cause-work-tick:" + suffix,
            ExpectedRevision = confirmed.WorldRevision,
            TickCount = 1,
        });
    }

    private static void ApplyHubChoice(
        경영SimulationSessionAggregate session,
        string suffix)
    {
        var context = session.GetFarmChoiceContext();
        var preview = session.PreviewFarmChoice(new SimulationFarmChoicePreviewRequest
        {
            ExpectedRevision = context.WorldRevision,
            ChoiceStableId = SimulationFarmChoicePlayableCodes.HubShipmentChoice,
        });
        var confirmed = session.ConfirmFarmChoice(new SimulationFarmChoiceConfirmRequest
        {
            CommandId = "command:test:nature-hub-choice:" + suffix,
            ExpectedRevision = preview.BaseRevision,
            ChoiceStableId = preview.ChoiceStableId,
        });
        session.Advance(new 경영SimulationTick진행Request
        {
            CommandId = "command:test:nature-hub-choice-tick:" + suffix,
            ExpectedRevision = confirmed.Revision,
            TickCount = preview.Impact.DurationTicks,
        });
    }

    private static SimulationNatureRestorationPreviewRequest RestorationRequest(
        long revision, string suffix)
        => new()
        {
            ExpectedRevision = revision,
            DecisionStableId = "decision:test:nature-restoration:" + suffix,
            TaskStableId = "task:test:nature-restoration:" + suffix,
            ActorStableId = "actor:test:restorer",
            NatureRouteCode = SimulationRegionalIncidentCodes.NatureToFarm,
            PreferredSpatialStableId =
                PyeongchangSimulation공간StableIds.Nature복원작업공간,
        };

    private static void PrepareRetreatedParty(경영SimulationSessionAggregate session,
        string suffix)
    {
        ObserveThreat(session, SimulationRegionalIncidentCodes.NatureToFarm,
            "retreat-prerequisite:" + suffix);
        var request = new SimulationNatureEmergencyRetreatPreviewRequest
        {
            ExpectedRevision = session.Revision,
            DecisionStableId = "decision:test:nature-retreat:" + suffix,
            TaskStableId = "task:test:nature-retreat:" + suffix,
            ActorStableId = "actor:test:player-party",
            NatureRouteCode = SimulationRegionalIncidentCodes.NatureToFarm,
            PreferredSpatialStableId =
                PyeongchangSimulation공간StableIds.Nature긴급후퇴경로,
        };
        var confirmed = session.ConfirmNatureEmergencyRetreat(
            new SimulationNatureEmergencyRetreatConfirmRequest
            {
                CommandId = "command:test:nature-retreat:" + suffix,
                ExpectedRevision = session.Revision,
                Preview = request,
            });
        session.Advance(new 경영SimulationTick진행Request
        {
            CommandId = "command:test:nature-retreat-tick:" + suffix,
            ExpectedRevision = confirmed.Revision,
            TickCount = 1,
        });
    }

    private static void PrepareRestoredRoute(경영SimulationSessionAggregate session,
        string suffix)
    {
        PrepareResolvedObservedNatureRoute(session, suffix);
        var request = RestorationRequest(session.Revision, suffix);
        var confirmed = session.ConfirmNatureRestoration(
            new SimulationNatureRestorationConfirmRequest
            {
                CommandId = "command:test:nature-restoration:" + suffix,
                ExpectedRevision = session.Revision,
                Preview = request,
            });
        session.Advance(new 경영SimulationTick진행Request
        {
            CommandId = "command:test:nature-restoration-tick:" + suffix,
            ExpectedRevision = confirmed.Revision,
            TickCount = 1,
        });
    }

    private static SimulationNaturePartyRecoveryPreviewRequest PartyRecoveryRequest(
        long revision, string suffix)
        => new()
        {
            ExpectedRevision = revision,
            DecisionStableId = "decision:test:nature-party-recovery:" + suffix,
            TaskStableId = "task:test:nature-party-recovery:" + suffix,
            ActorStableId = "actor:test:player-party",
            NatureRouteCode = SimulationRegionalIncidentCodes.NatureToFarm,
            PreferredSpatialStableId =
                PyeongchangSimulation공간StableIds.Nature안전회복야영지,
        };

    private static SimulationNatureMindInitialStateRequest NatureMindRequest(
        decimal recovery, decimal threat) => new()
        {
            Players = new[]
            {
                new SimulationNatureMindPlayerInitialStateRequest
                {
                    PlayerStableId = "actor:test:player-party",
                    RecoveryBaseOutput = recovery,
                    ThreatBaseOutput = threat,
                },
            },
        };
}
