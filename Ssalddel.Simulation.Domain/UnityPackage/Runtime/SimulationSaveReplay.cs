using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Simulation.Contracts;

namespace Ssalddel.Simulation.Domain
{
    [SsalddelCodeMetadata(
        SsalddelCodeFeatureKeys.SimulationSaveReplay,
        SsalddelCodeLayer.Domain,
        "세션 Snapshot과 Command log를 봉인한 저장 자료로 만든다.",
        StepKey = "domain.save-package",
        DependsOnStepKeys = new string[] { "application.save-replay" },
        ExecutionStage = SsalddelCodeExecutionStage.Persistence,
        ReadsFrom = SsalddelCodeDataScope.SimulationState,
        FlowOrder = 40,
        Boundary = "재생 hash와 schema가 일치하는 Simulation 자료만 만들며 운영 원장을 직렬화하지 않는다.")]
    public sealed partial class 경영SimulationSessionAggregate
    {
        private readonly List<SimulationCommandLogEntrySnapshot> commandLog =
            new List<SimulationCommandLogEntrySnapshot>();

        public SimulationSessionSavePackage CreateSavePackage(SimulationSessionSaveRequest request)
        {
            ValidateSaveRequest(request);
            lock (gate)
            {
                if (request.ExpectedRevision != Revision)
                    throw new SimulationConflictException("SimulationExpectedRevisionMismatch");

                var lhWorld = request.LhWorldState ?? lhWorldState;
                var worldAssetPlacement = request.WorldAssetPlacementState
                    ?? worldAssetPlacementState;
                var actionManifestationLedger = request.ActionManifestationLedger
                    ?? actionManifestationLedgerState;
                var playerDomainProfile = request.PlayerDomainProfile
                    ?? playerDomainProfileState;
                var playerDomainProfiles = request.PlayerDomainProfiles?.Length > 0
                    ? request.PlayerDomainProfiles
                    : GetPlayerDomainProfiles();
                if ((actionManifestationLedger == null) !=
                    (playerDomainProfile == null))
                    throw new SimulationContractException(
                        "SimulationActionManifestationSaveStateIncomplete");
                var hasWI음양주체분류 = commandLog.Any(value =>
                        string.Equals(value.WorldInteractionInvocation?.PayloadCode,
                            "WorldInteractionInvocation.v2",
                            StringComparison.Ordinal))
                    || npcRoutineExecutions.Values.Any(value =>
                        string.Equals(value.음양주체분류?.판정RuleRevision,
                            SimulationWI음양주체사분면Rules.RuleRevision,
                            StringComparison.Ordinal));
                var package = new SimulationSessionSavePackage
                {
                    TickRuleRevision = tickRuleRevision,
                    SchemaVersion = HasWorldAtmosphere
                        ? SimulationSaveSchemaVersions.V25
                        : IsNatureR5
                        ? SimulationSaveSchemaVersions.V24
                        : hasWI음양주체분류
                        ? SimulationSaveSchemaVersions.V23
                        : HasSpatialCompositionState
                        || HasInteriorPlanHandles
                        ? SimulationSaveSchemaVersions.V22
                        : IsNpcRoutineControlEnabled
                        ? SimulationSaveSchemaVersions.V21
                        : IsNatureR4
                        ? SimulationSaveSchemaVersions.V20
                        : IsNatureR3
                        ? SimulationSaveSchemaVersions.V19
                        : IsNatureR2
                        ? SimulationSaveSchemaVersions.V18
                        : UsesContextualArcana || HasTownNpcLife
                        ? SimulationSaveSchemaVersions.V16
                        : worldInteractionManifestations.Count > 0
                        ? SimulationSaveSchemaVersions.V15
                        : regionalDevelopmentSchemaEnabled
                        && HasRegionalDevelopmentState()
                        ? SimulationSaveSchemaVersions.V14
                        : natureSurvivalCreationState != null
                        ? SimulationSaveSchemaVersions.V13
                        : !regionalCausalitySchemaEnabled
                        && (regionalIncidents.Count > 0
                            || appliedRegionalIncidentResponseCommands.Count > 0)
                        ? SimulationSaveSchemaVersions.V4
                        : coopConstructionProjects.Count > 0
                        ? SimulationSaveSchemaVersions.V12
                        : string.Equals(hostedSessionModeCode,
                        SimulationHostedWorldCodes.HostedMultiplayer,
                        StringComparison.Ordinal)
                        ? SimulationSaveSchemaVersions.V11
                        : areaAccessConfigured
                        ? SimulationSaveSchemaVersions.V10
                        : natureMindCreationState != null
                        ? SimulationSaveSchemaVersions.V9
                        : realityContext != null
                        ? SimulationSaveSchemaVersions.V8
                        : regionalCausalitySchemaEnabled
                        ? SimulationSaveSchemaVersions.V7
                        : regionalIncidents.Count > 0
                          || appliedRegionalIncidentResponseCommands.Count > 0
                        ? SimulationSaveSchemaVersions.V4
                        : lhWorld != null
                        ? SimulationSaveSchemaVersions.V3
                        : spatialWorldCreationState == null
                            ? SimulationSaveSchemaVersions.V1
                            : SimulationSaveSchemaVersions.V2,
                    SaveStableId = request.SaveStableId.Trim(),
                    SessionStableId = SessionStableId,
                    SavedWorldTick = CurrentTick,
                    SavedWorldRevision = Revision,
                    ReplayHashAlgorithmCode = SimulationReplayHashAlgorithmCodes.Sha256,
                    SessionCreateRequest = CreateSessionRequest(),
                    Snapshot = CreateSnapshot(),
                    WorldInventory = CreateWorldInventorySnapshot(),
                    SurvivalTarot = CreateSurvivalTarotStateSnapshot(),
                    CommandLog = commandLog.Select(SimulationSaveReplayCloner.CloneCommand).ToArray(),
                    LhWorld = SimulationSaveReplayCloner.CloneLhWorld(lhWorld),
                    RealityContext = realityContext == null ? null
                        : CloneRealityContext(realityContext),
                    WorldInteractionManifestations = worldInteractionManifestations
                        .Select(CloneWorldInteractionManifestation).ToArray(),
                    SpatialComposition = spatialCompositionState == null
                        ? null : SimulationSpatialCompositionSnapshots.Clone(
                            spatialCompositionState),
                    SpatialCompositionHandle = spatialCompositionState == null
                        ? null : SimulationSpatialCompositionSnapshots
                            .CreateHandle(spatialCompositionState),
                    WorldAssetPlacement = SimulationSaveReplayCloner
                        .CloneWorldAssetPlacementState(worldAssetPlacement),
                    ActorEquipment = actorEquipmentLegacyBridge ? null
                        : CreateActorEquipmentStateSnapshot(),
                    ActionManifestationLedger = SimulationSaveReplayCloner
                        .CloneActionManifestationLedger(actionManifestationLedger),
                    PlayerDomainProfile = SimulationSaveReplayCloner
                        .ClonePlayerDomainProfile(playerDomainProfile),
                    PlayerDomainProfiles = playerDomainProfiles.Select(value =>
                        SimulationSaveReplayCloner.ClonePlayerDomainProfile(
                            value)!).ToArray(),
                    LearningFocus = CloneLearningFocusStateOrNull(
                        CreateLearningFocusStateSnapshotOrNull()),
                    HexagramCampaign = CloneHexagramCampaignState(
                        hexagramCampaignState),
                    SeasonalOperationsCampaign =
                        CloneSeasonalOperationsCampaignState(
                            seasonalOperationsCampaignState),
                    FranchiseSupply = CloneFranchiseSupplyState(
                        franchiseSupplyState),
                };
                if (worldAssetPlacement != null)
                {
                    package.WorldAssetPlacementBaseSchemaVersion =
                        package.SchemaVersion;
                    package.SchemaVersion = SimulationSaveSchemaVersions.V26;
                }
                if (package.ActorEquipment?.IsEnabled == true)
                {
                    package.ActorEquipmentBaseSchemaVersion = package.SchemaVersion;
                    package.SchemaVersion = SimulationSaveSchemaVersions.V27;
                }
                if (package.ActionManifestationLedger != null
                    && package.PlayerDomainProfile != null)
                {
                    // 행위 원장과 분야 진척은 같은 권위 결과 계보로 봉인한다.
                    Simulation행위발현Ledger.Restore(
                        package.ActionManifestationLedger);
                    Simulation플레이어분야Engine.Restore(
                        package.PlayerDomainProfile);
                    package.ActionManifestationBaseSchemaVersion =
                        package.SchemaVersion;
                    package.SchemaVersion = SimulationSaveSchemaVersions.V28;
                }
                if (package.PlayerDomainProfile?.명상기여기록들?.Length > 0
                    && string.Equals(package.PlayerDomainProfile.SchemaCode,
                        Simulation플레이어분야SchemaCodes.분야Profile,
                        StringComparison.Ordinal))
                {
                    package.FocusMeditationBaseSchemaVersion =
                        package.SchemaVersion;
                    package.SchemaVersion = SimulationSaveSchemaVersions.V29;
                }
                if (package.LearningFocus != null)
                {
                    package.LearningFocusBaseSchemaVersion = package.SchemaVersion;
                    package.SchemaVersion = SimulationSaveSchemaVersions.V30;
                }
                if (package.HexagramCampaign != null)
                {
                    package.HexagramCampaignBaseSchemaVersion =
                        package.SchemaVersion;
                    package.SchemaVersion = SimulationSaveSchemaVersions.V31;
                }
                if (package.SeasonalOperationsCampaign != null)
                {
                    package.SeasonalOperationsCampaignBaseSchemaVersion =
                        package.SchemaVersion;
                    package.SchemaVersion = SimulationSaveSchemaVersions.V32;
                }
                if (package.FranchiseSupply != null)
                {
                    package.FranchiseSupplyBaseSchemaVersion =
                        package.SchemaVersion;
                    package.SchemaVersion = SimulationSaveSchemaVersions.V33;
                }
                package.ReplayHash = SimulationReplayHasher.Calculate(package);
                return SimulationSaveReplayCloner.ClonePackage(package);
            }
        }

        private void AppendTickCommand(경영SimulationTick진행Request request)
            => commandLog.Add(new SimulationCommandLogEntrySnapshot
            {
                Sequence = commandLog.Count + 1L,
                CommandTypeCode = SimulationCommandTypeCodes.TickAdvance,
                AppliedWorldTick = CurrentTick,
                ResultingWorldRevision = Revision,
                TickRequest = SimulationSaveReplayCloner.CloneTickRequest(request),
            });

        private void AppendNatureSurvivalActionCommand(
            SimulationNatureSurvivalCommandRequest request)
            => commandLog.Add(new SimulationCommandLogEntrySnapshot
            {
                Sequence = commandLog.Count + 1L,
                CommandTypeCode = SimulationCommandTypeCodes.NatureSurvivalActionConfirm,
                AppliedWorldTick = CurrentTick,
                ResultingWorldRevision = Revision,
                NatureSurvivalActionRequest =
                    SimulationSaveReplayCloner.CloneNatureSurvivalActionRequest(request),
            });

        private void AppendNatureSurvivalClockCommand(
            SimulationNatureSurvivalClockAdvanceRequest request)
            => commandLog.Add(new SimulationCommandLogEntrySnapshot
            {
                Sequence = commandLog.Count + 1L,
                CommandTypeCode = SimulationCommandTypeCodes.NatureSurvivalClockAdvance,
                AppliedWorldTick = CurrentTick,
                ResultingWorldRevision = Revision,
                NatureSurvivalClockAdvanceRequest =
                    SimulationSaveReplayCloner.CloneNatureSurvivalClockRequest(request),
            });

        private void AppendNatureFocusTimingCommand(
            Simulation집중판정AttemptRequest request)
            => commandLog.Add(new SimulationCommandLogEntrySnapshot
            {
                Sequence = commandLog.Count + 1L,
                CommandTypeCode =
                    SimulationCommandTypeCodes.NatureFocusTimingAttempt,
                AppliedWorldTick = CurrentTick,
                ResultingWorldRevision = Revision,
                NatureFocusTimingAttemptRequest = SimulationSaveReplayCloner
                    .CloneFocusTimingAttemptRequest(request),
            });

        private void AppendActorItemAcquireCommand(
            SimulationActorItemAcquireConfirmRequest request)
            => commandLog.Add(new SimulationCommandLogEntrySnapshot
            {
                Sequence = commandLog.Count + 1L,
                CommandTypeCode = SimulationCommandTypeCodes.ActorItemAcquireConfirm,
                AppliedWorldTick = CurrentTick,
                ResultingWorldRevision = Revision,
                ActorItemAcquireConfirmRequest =
                    SimulationSaveReplayCloner.CloneActorItemAcquireConfirmRequest(
                        request),
            });

        private void AppendActorEquipmentChangeCommand(
            SimulationActorEquipmentChangeConfirmRequest request)
            => commandLog.Add(new SimulationCommandLogEntrySnapshot
            {
                Sequence = commandLog.Count + 1L,
                CommandTypeCode =
                    SimulationCommandTypeCodes.ActorEquipmentChangeConfirm,
                AppliedWorldTick = CurrentTick,
                ResultingWorldRevision = Revision,
                ActorEquipmentChangeConfirmRequest =
                    SimulationSaveReplayCloner
                        .CloneActorEquipmentChangeConfirmRequest(request),
            });

        private void AppendIntegratedWorldCommand(SimulationIntegratedWorldCommandRequest request)
            => commandLog.Add(new SimulationCommandLogEntrySnapshot
            {
                Sequence = commandLog.Count + 1L,
                CommandTypeCode = SimulationCommandTypeCodes.IntegratedWorldConfirm,
                AppliedWorldTick = CurrentTick,
                ResultingWorldRevision = Revision,
                IntegratedWorldConfirmRequest = CloneIntegratedWorldCommand(request),
            });

        private void AppendIntegratedWorldEffectEnqueued(string battleStableId,
            string facilityStableId, string severityCode)
            => commandLog.Add(new SimulationCommandLogEntrySnapshot
            {
                Sequence = commandLog.Count + 1L,
                CommandTypeCode = SimulationCommandTypeCodes.IntegratedWorldEffectEnqueued,
                AppliedWorldTick = CurrentTick,
                ResultingWorldRevision = Revision,
                FacilityDamageQueueRequest = new SimulationFacilityDamageQueueRequest
                {
                    BattleStableId = battleStableId,
                    FacilityStableId = facilityStableId,
                    SeverityCode = severityCode,
                },
            });

        private void AppendDecisionConfirmCommand(SimulationDecisionConfirmRequest request)
            => commandLog.Add(new SimulationCommandLogEntrySnapshot
            {
                Sequence = commandLog.Count + 1L,
                CommandTypeCode = SimulationCommandTypeCodes.DecisionConfirm,
                AppliedWorldTick = CurrentTick,
                ResultingWorldRevision = Revision,
                DecisionConfirmRequest = SimulationSaveReplayCloner.CloneConfirmRequest(request),
            });

        private void AppendTaskCancelCommand(
            string taskStableId,
            SimulationTaskCancelRequest request)
            => commandLog.Add(new SimulationCommandLogEntrySnapshot
            {
                Sequence = commandLog.Count + 1L,
                CommandTypeCode = SimulationCommandTypeCodes.TaskCancel,
                AppliedWorldTick = CurrentTick,
                ResultingWorldRevision = Revision,
                TaskCancelRequest = SimulationSaveReplayCloner.CloneTaskCancelRequest(request),
                TaskStableId = taskStableId.Trim(),
            });

        private void AppendRegionalIncidentResponseConfirmCommand(
            string eventStableId,
            SimulationRegionalIncidentResponseConfirmRequest request)
            => commandLog.Add(new SimulationCommandLogEntrySnapshot
            {
                Sequence = commandLog.Count + 1L,
                CommandTypeCode = SimulationCommandTypeCodes.RegionalIncidentResponseConfirm,
                AppliedWorldTick = CurrentTick,
                ResultingWorldRevision = Revision,
                WorldEventStableId = eventStableId.Trim(),
                RegionalIncidentResponseConfirmRequest =
                    SimulationSaveReplayCloner.CloneRegionalIncidentResponseConfirmRequest(request),
            });

        private void AppendNatureEncounterVictoryCommand(string battleStableId,
            string encounterStableId)
            => commandLog.Add(new SimulationCommandLogEntrySnapshot
            {
                Sequence = commandLog.Count + 1L,
                CommandTypeCode = SimulationCommandTypeCodes.NatureEncounterVictory,
                AppliedWorldTick = CurrentTick,
                ResultingWorldRevision = Revision,
                NatureEncounterVictoryRequest = new SimulationNatureEncounterVictoryRequest
                {
                    BattleStableId = battleStableId.Trim(),
                    EncounterStableId = encounterStableId.Trim(),
                },
            });

        private void AppendHarvestDispositionImpactConfirmCommand(
            SimulationHarvestDispositionImpactConfirmRequest request)
            => commandLog.Add(new SimulationCommandLogEntrySnapshot
            {
                Sequence = commandLog.Count + 1L,
                CommandTypeCode = SimulationCommandTypeCodes.HarvestDispositionImpactConfirm,
                AppliedWorldTick = CurrentTick,
                ResultingWorldRevision = Revision,
                HarvestDispositionImpactConfirmRequest =
                    SimulationSaveReplayCloner.CloneHarvestDispositionImpactConfirmRequest(request),
            });

        private void AppendLogisticsMovementConfirmCommand(
            SimulationLogisticsMovementConfirmRequest request)
            => commandLog.Add(new SimulationCommandLogEntrySnapshot
            {
                Sequence = commandLog.Count + 1L,
                CommandTypeCode = SimulationCommandTypeCodes.LogisticsMovementConfirm,
                AppliedWorldTick = CurrentTick,
                ResultingWorldRevision = Revision,
                LogisticsMovementConfirmRequest =
                    SimulationSaveReplayCloner.CloneLogisticsMovementConfirmRequest(request),
            });

        private void AppendTurnClosingCommand(SimulationTurnClosingConfirmRequest request)
            => commandLog.Add(new SimulationCommandLogEntrySnapshot
            {
                Sequence = commandLog.Count + 1L,
                CommandTypeCode = SimulationCommandTypeCodes.TurnClosingConfirm,
                AppliedWorldTick = CurrentTick,
                ResultingWorldRevision = Revision,
                TurnClosingConfirmRequest =
                    SimulationSaveReplayCloner.CloneTurnClosingConfirmRequest(request),
            });

        private void AppendNpcPolicyCommand(SimulationNpcPolicyChangeRequest request)
            => commandLog.Add(new SimulationCommandLogEntrySnapshot
            {
                Sequence = commandLog.Count + 1L,
                CommandTypeCode = SimulationCommandTypeCodes.NpcPolicyChange,
                AppliedWorldTick = CurrentTick,
                ResultingWorldRevision = Revision,
                NpcPolicyChangeRequest = SimulationSaveReplayCloner.CloneNpcPolicyChangeRequest(request),
            });

        private void AppendWorldItemAcquisitionCommand(
            SimulationWorldItemAcquisitionConfirmRequest request)
            => commandLog.Add(new SimulationCommandLogEntrySnapshot
            {
                Sequence = commandLog.Count + 1L,
                CommandTypeCode = SimulationCommandTypeCodes.WorldItemAcquisitionConfirm,
                AppliedWorldTick = CurrentTick,
                ResultingWorldRevision = Revision,
                WorldItemAcquisitionConfirmRequest =
                    SimulationSaveReplayCloner.CloneWorldItemAcquisitionConfirmRequest(request),
            });

        private void AppendSurvivalTarotResponseCommand(
            SimulationSurvivalTarotResponseConfirmRequest request)
            => commandLog.Add(new SimulationCommandLogEntrySnapshot
            {
                Sequence = commandLog.Count + 1L,
                CommandTypeCode = SimulationCommandTypeCodes.SurvivalTarotResponseConfirm,
                AppliedWorldTick = CurrentTick,
                ResultingWorldRevision = Revision,
                SurvivalTarotResponseConfirmRequest =
                    SimulationSaveReplayCloner.CloneSurvivalTarotResponseConfirmRequest(request),
            });

        private void AppendSurvivalTarotResolutionCommand(
            SimulationSurvivalTarotResolutionConfirmRequest request)
            => commandLog.Add(new SimulationCommandLogEntrySnapshot
            {
                Sequence = commandLog.Count + 1L,
                CommandTypeCode = SimulationCommandTypeCodes.SurvivalTarotResolutionConfirm,
                AppliedWorldTick = CurrentTick,
                ResultingWorldRevision = Revision,
                SurvivalTarotResolutionConfirmRequest =
                    SimulationSaveReplayCloner.CloneSurvivalTarotResolutionConfirmRequest(request),
            });

        private void AppendFarmWorkConfirmCommand(
            SimulationFarmWorkConfirmRequest request)
            => commandLog.Add(new SimulationCommandLogEntrySnapshot
            {
                Sequence = commandLog.Count + 1L,
                CommandTypeCode = SimulationCommandTypeCodes.FarmWorkConfirm,
                AppliedWorldTick = CurrentTick,
                ResultingWorldRevision = Revision,
                FarmWorkConfirmRequest =
                    SimulationSaveReplayCloner.CloneFarmWorkConfirmRequest(request),
            });

        private void AppendFarmWorkPlanConfirmCommand(
            SimulationFarmWorkPlanConfirmRequest request)
            => commandLog.Add(new SimulationCommandLogEntrySnapshot
            {
                Sequence = commandLog.Count + 1L,
                CommandTypeCode = SimulationCommandTypeCodes.FarmWorkPlanConfirm,
                AppliedWorldTick = CurrentTick,
                ResultingWorldRevision = Revision,
                FarmWorkPlanConfirmRequest =
                    SimulationSaveReplayCloner.CloneFarmWorkPlanConfirmRequest(request),
            });

        private void AppendThreatResponseConfirmCommand(
            SimulationThreatResponseConfirmRequest request)
            => commandLog.Add(new SimulationCommandLogEntrySnapshot
            {
                Sequence = commandLog.Count + 1L,
                CommandTypeCode = SimulationCommandTypeCodes.ThreatResponseConfirm,
                AppliedWorldTick = CurrentTick,
                ResultingWorldRevision = Revision,
                ThreatResponseConfirmRequest =
                    SimulationSaveReplayCloner.CloneThreatResponseConfirmRequest(request),
            });

        private void AppendTeamRoleCardEquipCommand(
            SimulationTeamRoleCardEquipRequest request)
            => commandLog.Add(new SimulationCommandLogEntrySnapshot
            {
                Sequence = commandLog.Count + 1L,
                CommandTypeCode = SimulationCommandTypeCodes.TeamRoleCardEquip,
                AppliedWorldTick = CurrentTick,
                ResultingWorldRevision = Revision,
                TeamRoleCardEquipRequest =
                    SimulationSaveReplayCloner.CloneTeamRoleCardEquipRequest(request),
            });

        private void AppendCombatCardLoadoutSetCommand(
            SimulationCombatCardLoadoutSetRequest request)
            => commandLog.Add(new SimulationCommandLogEntrySnapshot
            {
                Sequence = commandLog.Count + 1L,
                CommandTypeCode = SimulationCommandTypeCodes.CombatCardLoadoutSet,
                AppliedWorldTick = CurrentTick,
                ResultingWorldRevision = Revision,
                CombatCardLoadoutSetRequest =
                    SimulationSaveReplayCloner.CloneCombatCardLoadoutSetRequest(request),
            });

        private void AppendTeamActivityStartCommand(
            SimulationTeamActivityStartRequest request)
            => commandLog.Add(new SimulationCommandLogEntrySnapshot
            {
                Sequence = commandLog.Count + 1L,
                CommandTypeCode = SimulationCommandTypeCodes.TeamActivityStart,
                AppliedWorldTick = CurrentTick,
                ResultingWorldRevision = Revision,
                TeamActivityStartRequest =
                    SimulationSaveReplayCloner.CloneTeamActivityStartRequest(request),
            });

        private void AppendTeamActivityEndCommand(
            SimulationTeamActivityEndRequest request)
            => commandLog.Add(new SimulationCommandLogEntrySnapshot
            {
                Sequence = commandLog.Count + 1L,
                CommandTypeCode = SimulationCommandTypeCodes.TeamActivityEnd,
                AppliedWorldTick = CurrentTick,
                ResultingWorldRevision = Revision,
                TeamActivityEndRequest =
                    SimulationSaveReplayCloner.CloneTeamActivityEndRequest(request),
            });

        private 경영SimulationSession생성Request CreateSessionRequest()
            => new 경영SimulationSession생성Request
            {
                ClientRequestId = ClientRequestId,
                ScenarioStableId = ScenarioStableId,
                ScenarioDataRevision = ScenarioDataRevision,
                ScenarioSeed = ScenarioSeed,
                RuleRevision = RuleRevision,
                RealityContextProfileStableId = RealityContextProfileStableId,
                NpcRoutineControlRevision = NpcRoutineControlRevision,
                SpatialCompositionRuleRevision =
                    SpatialCompositionRuleRevision,
                TarotOrientationPolicyCode = tarotOrientationPolicyCode,
                TownNpcLifeProfileStableId = townNpcLifeProfileStableId,
                DurationTicks = DurationTicks,
                NeighborhoodDayEnabled = neighborhoodDayEnabled,
                LocalLife = localLife?.Copy(),
                WorldContext = new SimulationWorldContext생성Request
                {
                    FactionStableId = FactionStableId,
                    TerritoryStableId = TerritoryStableId,
                    SettlementStableId = SettlementStableId,
                    GameDateStartsOn = GameDateStartsOn,
                },
                Settlement = CloneSettlementRequest(settlementCreationState),
                NpcWorkforce = CloneNpcWorkforceInitialState(npcWorkforceCreationState),
                SpatialWorld = CloneSimulationSpatialInitialState(spatialWorldCreationState),
                WorldInventory = CloneWorldInventoryInitialState(worldInventoryCreationState),
                ActorEquipment = actorEquipmentLegacyBridge ? null
                    : CloneActorEquipmentInitialState(actorEquipmentCreationState),
                SurvivalTarot = CloneSurvivalTarotInitialState(survivalTarotCreationState),
                FarmSurvival = CloneFarmSurvivalInitialState(farmSurvivalCreationState),
                TeamRoleCards = CloneTeamRoleCardInitialStateOrNull(
                    teamRoleCardCreationState),
                LearningFocus = CloneLearningFocusInitialStateOrNull(
                    learningFocusCreationState),
                IntegratedWorld = CloneIntegratedWorldInitialState(
                    integratedWorldCreationState),
                NatureMind = CloneNatureMindInitialState(natureMindCreationState),
                NatureSurvival = CloneNatureSurvivalInitialState(
                    natureSurvivalCreationState),
                Atmosphere = CloneWorldAtmosphereInitialState(
                    atmosphereCreationState),
                InteriorPlanHandles = CreateInteriorPlanHandleSnapshots(),
            };

        private static void ValidateSaveRequest(SimulationSessionSaveRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            RequireStableId(request.SaveStableId, "SimulationSaveStableIdInvalid");
            if (request.ExpectedRevision < 0)
                throw new SimulationContractException("SimulationExpectedRevisionInvalid");
            ValidateLhWorldState(request.LhWorldState, request.ExpectedRevision);
        }
    }
}
