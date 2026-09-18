using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.IO;
using System.Runtime.Serialization;
using Ssalddel.Simulation.Contracts;

namespace Ssalddel.Simulation.Domain
{
    public static partial class SimulationSaveReplayCloner
    {
        public static SimulationSessionSavePackage ClonePackage(SimulationSessionSavePackage source)
            => new SimulationSessionSavePackage
            {
                TickRuleRevision = source.TickRuleRevision,
                SchemaVersion = source.SchemaVersion,
                SaveStableId = source.SaveStableId,
                SessionStableId = source.SessionStableId,
                SavedWorldTick = source.SavedWorldTick,
                SavedWorldRevision = source.SavedWorldRevision,
                ReplayHashAlgorithmCode = source.ReplayHashAlgorithmCode,
                ReplayHash = source.ReplayHash,
                WorldAssetPlacementBaseSchemaVersion =
                    source.WorldAssetPlacementBaseSchemaVersion,
                ActorEquipmentBaseSchemaVersion =
                    source.ActorEquipmentBaseSchemaVersion,
                ActionManifestationBaseSchemaVersion =
                    source.ActionManifestationBaseSchemaVersion,
                FocusMeditationBaseSchemaVersion =
                    source.FocusMeditationBaseSchemaVersion,
                LearningFocusBaseSchemaVersion =
                    source.LearningFocusBaseSchemaVersion,
                HexagramCampaignBaseSchemaVersion =
                    source.HexagramCampaignBaseSchemaVersion,
                SeasonalOperationsCampaignBaseSchemaVersion =
                    source.SeasonalOperationsCampaignBaseSchemaVersion,
                SessionCreateRequest = CloneCreateRequest(source.SessionCreateRequest),
                Snapshot = 경영SimulationSessionAggregate.Clone(source.Snapshot),
                WorldInventory = 경영SimulationSessionAggregate.CloneWorldInventory(
                    source.WorldInventory),
                SurvivalTarot = 경영SimulationSessionAggregate.CloneSurvivalTarotState(
                    source.SurvivalTarot),
                CommandLog = source.CommandLog.Select(CloneCommand).ToArray(),
                Battles = source.Battles.Select(
                    SimulationBattleInstanceState.CloneSaveRecord).ToArray(),
                LhWorld = CloneLhWorld(source.LhWorld),
                RealityContext = source.RealityContext == null ? null
                    : 경영SimulationSessionAggregate.CloneRealityContext(source.RealityContext),
                WorldInteractionManifestations =
                    (source.WorldInteractionManifestations
                        ?? Array.Empty<SimulationWorldInteractionManifestationRecord>())
                    .Select(경영SimulationSessionAggregate
                        .CloneWorldInteractionManifestation).ToArray(),
                SpatialComposition = source.SpatialComposition == null
                    ? null : SimulationSpatialCompositionSnapshots.Clone(
                        source.SpatialComposition),
                SpatialCompositionHandle = source.SpatialCompositionHandle == null
                    ? null : new SpatialCompositionGraphHandle
                    {
                        SchemaVersion = source.SpatialCompositionHandle.SchemaVersion,
                        AreaCode = source.SpatialCompositionHandle.AreaCode,
                        AreaSetStableId = source.SpatialCompositionHandle.AreaSetStableId,
                        RuleCatalogRevision = source.SpatialCompositionHandle
                            .RuleCatalogRevision,
                        RuleCatalogHashSha256 = source.SpatialCompositionHandle
                            .RuleCatalogHashSha256,
                        SourceWorldRevision = source.SpatialCompositionHandle
                            .SourceWorldRevision,
                        GraphHashSha256 = source.SpatialCompositionHandle
                            .GraphHashSha256,
                    },
                WorldAssetPlacement = CloneWorldAssetPlacementState(
                    source.WorldAssetPlacement),
                ActorEquipment = source.ActorEquipment == null ? null
                    : 경영SimulationSessionAggregate.CloneActorEquipmentState(
                        source.ActorEquipment),
                ActionManifestationLedger = CloneActionManifestationLedger(
                    source.ActionManifestationLedger),
                PlayerDomainProfile = ClonePlayerDomainProfile(
                    source.PlayerDomainProfile),
                PlayerDomainProfiles = (source.PlayerDomainProfiles
                    ?? Array.Empty<Simulation플레이어분야ProfileSnapshot>())
                    .Select(value => ClonePlayerDomainProfile(value)!)
                    .ToArray(),
                LearningFocus = 경영SimulationSessionAggregate
                    .CloneLearningFocusStateOrNull(source.LearningFocus),
                HexagramCampaign = 경영SimulationSessionAggregate
                    .CloneHexagramCampaignState(source.HexagramCampaign),
                SeasonalOperationsCampaign = 경영SimulationSessionAggregate
                    .CloneSeasonalOperationsCampaignState(
                        source.SeasonalOperationsCampaign),
            };

        public static Simulation행위기록LedgerSnapshot?
            CloneActionManifestationLedger(
                Simulation행위기록LedgerSnapshot? source)
            => CloneDataContract(source);

        public static Simulation플레이어분야ProfileSnapshot?
            ClonePlayerDomainProfile(
                Simulation플레이어분야ProfileSnapshot? source)
            => CloneDataContract(source);

        private static T? CloneDataContract<T>(T? source) where T : class
        {
            if (source == null) return null;
            var serializer = new DataContractSerializer(typeof(T));
            using var stream = new MemoryStream();
            serializer.WriteObject(stream, source);
            stream.Position = 0;
            return (T?)serializer.ReadObject(stream);
        }

        public static SimulationWorldAssetPlacementStateSnapshot?
            CloneWorldAssetPlacementState(
                SimulationWorldAssetPlacementStateSnapshot? source)
        {
            if (source == null) return null;
            var serializer = new DataContractSerializer(
                typeof(SimulationWorldAssetPlacementStateSnapshot));
            using var stream = new MemoryStream();
            serializer.WriteObject(stream, source);
            stream.Position = 0;
            return (SimulationWorldAssetPlacementStateSnapshot?)
                serializer.ReadObject(stream);
        }

        public static SimulationLhWorldStateSnapshot? CloneLhWorld(
            SimulationLhWorldStateSnapshot? source)
            => source == null
                ? null
                : new SimulationLhWorldStateSnapshot
                {
                    WorldSeed = source.WorldSeed,
                    GeneratorVersion = source.GeneratorVersion,
                    AreaSetStableId = source.AreaSetStableId,
                    AreaSetRevision = source.AreaSetRevision,
                    AreaSetBoundaryHashSha256 = source.AreaSetBoundaryHashSha256,
                    WorldLayoutStableId = source.WorldLayoutStableId,
                    WorldLayoutRevision = source.WorldLayoutRevision,
                    WorldLayoutHashSha256 = source.WorldLayoutHashSha256,
                    PlacementAuthorityCode = source.PlacementAuthorityCode,
                    WorldGroundingStateCode = source.WorldGroundingStateCode,
                    GroundingEvidenceHashSha256 = source.GroundingEvidenceHashSha256,
                    LastL3CellKey = source.LastL3CellKey,
                    Deltas = source.Deltas.Select(value =>
                        new SimulationLhWorldDeltaSnapshot
                        {
                            GeneratedStableId = value.GeneratedStableId,
                            DeltaKindCode = value.DeltaKindCode,
                            StateCode = value.StateCode,
                            AppliedWorldRevision = value.AppliedWorldRevision,
                            Tombstone = value.Tombstone,
                        }).ToArray(),
                };

        public static 경영SimulationSession생성Request CloneCreateRequest(
            경영SimulationSession생성Request source)
            => new 경영SimulationSession생성Request
            {
                ClientRequestId = source.ClientRequestId,
                ScenarioStableId = source.ScenarioStableId,
                ScenarioDataRevision = source.ScenarioDataRevision,
                ScenarioSeed = source.ScenarioSeed,
                RuleRevision = source.RuleRevision,
                RealityContextProfileStableId = source.RealityContextProfileStableId,
                NpcRoutineControlRevision = source.NpcRoutineControlRevision,
                SpatialCompositionRuleRevision =
                    source.SpatialCompositionRuleRevision,
                TarotOrientationPolicyCode = source.TarotOrientationPolicyCode,
                TownNpcLifeProfileStableId = source.TownNpcLifeProfileStableId,
                DurationTicks = source.DurationTicks,
                NeighborhoodDayEnabled = source.NeighborhoodDayEnabled,
                LocalLife = source.LocalLife?.Copy(),
                WorldContext = new SimulationWorldContext생성Request
                {
                    FactionStableId = source.WorldContext.FactionStableId,
                    TerritoryStableId = source.WorldContext.TerritoryStableId,
                    SettlementStableId = source.WorldContext.SettlementStableId,
                    GameDateStartsOn = source.WorldContext.GameDateStartsOn,
                },
                Settlement = 경영SimulationSessionAggregate.CloneSettlementRequest(source.Settlement),
                NpcWorkforce = 경영SimulationSessionAggregate.CloneNpcWorkforceInitialState(
                    source.NpcWorkforce),
                SpatialWorld = 경영SimulationSessionAggregate.CloneSimulationSpatialInitialState(
                    source.SpatialWorld),
                WorldInventory = 경영SimulationSessionAggregate.CloneWorldInventoryInitialState(
                    source.WorldInventory),
                ActorEquipment = 경영SimulationSessionAggregate
                    .CloneActorEquipmentInitialState(source.ActorEquipment),
                SurvivalTarot = 경영SimulationSessionAggregate.CloneSurvivalTarotInitialState(
                    source.SurvivalTarot),
                FarmSurvival = 경영SimulationSessionAggregate.CloneFarmSurvivalInitialState(
                    source.FarmSurvival),
                TeamRoleCards = 경영SimulationSessionAggregate
                    .CloneTeamRoleCardInitialStateOrNull(source.TeamRoleCards),
                LearningFocus = 경영SimulationSessionAggregate
                    .CloneLearningFocusInitialStateOrNull(source.LearningFocus),
                IntegratedWorld = 경영SimulationSessionAggregate
                    .CloneIntegratedWorldInitialState(source.IntegratedWorld),
                NatureMind = 경영SimulationSessionAggregate
                    .CloneNatureMindInitialState(source.NatureMind),
                NatureSurvival = 경영SimulationSessionAggregate
                    .CloneNatureSurvivalInitialState(source.NatureSurvival),
                Atmosphere = 경영SimulationSessionAggregate
                    .CloneWorldAtmosphereInitialState(source.Atmosphere),
                InteriorPlanHandles = 경영SimulationSessionAggregate
                    .CloneInteriorPlanHandles(source.InteriorPlanHandles),
            };

        public static SimulationCommandLogEntrySnapshot CloneCommand(
            SimulationCommandLogEntrySnapshot source)
            => new SimulationCommandLogEntrySnapshot
            {
                Sequence = source.Sequence,
                CommandTypeCode = source.CommandTypeCode,
                AppliedWorldTick = source.AppliedWorldTick,
                ResultingWorldRevision = source.ResultingWorldRevision,
                TickRequest = source.TickRequest == null ? null : CloneTickRequest(source.TickRequest),
                DecisionConfirmRequest = source.DecisionConfirmRequest == null
                    ? null
                    : CloneConfirmRequest(source.DecisionConfirmRequest),
                HarvestDispositionImpactConfirmRequest = source.HarvestDispositionImpactConfirmRequest == null
                    ? null
                    : CloneHarvestDispositionImpactConfirmRequest(
                        source.HarvestDispositionImpactConfirmRequest),
                LogisticsMovementConfirmRequest = source.LogisticsMovementConfirmRequest == null
                    ? null
                    : CloneLogisticsMovementConfirmRequest(source.LogisticsMovementConfirmRequest),
                TurnClosingConfirmRequest = source.TurnClosingConfirmRequest == null
                    ? null
                    : CloneTurnClosingConfirmRequest(source.TurnClosingConfirmRequest),
                NpcPolicyChangeRequest = source.NpcPolicyChangeRequest == null
                    ? null
                    : CloneNpcPolicyChangeRequest(source.NpcPolicyChangeRequest),
                WorldItemAcquisitionConfirmRequest =
                    source.WorldItemAcquisitionConfirmRequest == null
                        ? null
                        : CloneWorldItemAcquisitionConfirmRequest(
                            source.WorldItemAcquisitionConfirmRequest),
                SurvivalTarotResponseConfirmRequest =
                    source.SurvivalTarotResponseConfirmRequest == null
                        ? null
                        : CloneSurvivalTarotResponseConfirmRequest(
                            source.SurvivalTarotResponseConfirmRequest),
                SurvivalTarotResolutionConfirmRequest =
                    source.SurvivalTarotResolutionConfirmRequest == null
                        ? null
                        : CloneSurvivalTarotResolutionConfirmRequest(
                            source.SurvivalTarotResolutionConfirmRequest),
                FarmWorkConfirmRequest = source.FarmWorkConfirmRequest == null
                    ? null
                    : CloneFarmWorkConfirmRequest(source.FarmWorkConfirmRequest),
                FarmWorkPlanConfirmRequest = source.FarmWorkPlanConfirmRequest == null
                    ? null
                    : CloneFarmWorkPlanConfirmRequest(source.FarmWorkPlanConfirmRequest),
                ThreatResponseConfirmRequest = source.ThreatResponseConfirmRequest == null
                    ? null
                    : CloneThreatResponseConfirmRequest(
                        source.ThreatResponseConfirmRequest),
                CombatPerspectiveConfirmRequest =
                    source.CombatPerspectiveConfirmRequest == null ? null
                        : CloneCombatPerspectiveConfirmRequest(
                            source.CombatPerspectiveConfirmRequest),
                CombatBeatStartRequest = source.CombatBeatStartRequest == null ? null
                    : CloneCombatBeatStartRequest(source.CombatBeatStartRequest),
                CombatReactionConfirmRequest =
                    source.CombatReactionConfirmRequest == null ? null
                        : CloneCombatReactionConfirmRequest(
                            source.CombatReactionConfirmRequest),
                TacticalOrderConfirmRequest =
                    source.TacticalOrderConfirmRequest == null ? null
                        : CloneTacticalOrderConfirmRequest(
                            source.TacticalOrderConfirmRequest),
                TeamRoleCardEquipRequest = source.TeamRoleCardEquipRequest == null
                    ? null : CloneTeamRoleCardEquipRequest(
                        source.TeamRoleCardEquipRequest),
                CombatCardLoadoutSetRequest = source.CombatCardLoadoutSetRequest == null
                    ? null : CloneCombatCardLoadoutSetRequest(
                        source.CombatCardLoadoutSetRequest),
                TeamActivityStartRequest = source.TeamActivityStartRequest == null
                    ? null : CloneTeamActivityStartRequest(
                        source.TeamActivityStartRequest),
                TeamActivityEndRequest = source.TeamActivityEndRequest == null
                    ? null : CloneTeamActivityEndRequest(
                        source.TeamActivityEndRequest),
                TileTraversalConfirmRequest = source.TileTraversalConfirmRequest == null
                    ? null : 경영SimulationSessionAggregate.CloneTileTraversalRequest(
                        source.TileTraversalConfirmRequest),
                CollectibleCardDrawRequest = source.CollectibleCardDrawRequest == null
                    ? null : 경영SimulationSessionAggregate.CloneCollectibleCardDrawRequest(
                        source.CollectibleCardDrawRequest),
                CollectibleCardTransferRequest = source.CollectibleCardTransferRequest == null
                    ? null : 경영SimulationSessionAggregate.CloneCollectibleCardTransferRequest(
                        source.CollectibleCardTransferRequest),
                TaskCancelRequest = source.TaskCancelRequest == null
                    ? null : CloneTaskCancelRequest(source.TaskCancelRequest),
                TaskStableId = source.TaskStableId,
                WorldEventStableId = source.WorldEventStableId,
                RegionalIncidentResponseConfirmRequest =
                    source.RegionalIncidentResponseConfirmRequest == null ? null
                        : CloneRegionalIncidentResponseConfirmRequest(
                            source.RegionalIncidentResponseConfirmRequest),
                NatureEncounterVictoryRequest = source.NatureEncounterVictoryRequest == null
                    ? null : new SimulationNatureEncounterVictoryRequest
                    {
                        BattleStableId = source.NatureEncounterVictoryRequest.BattleStableId,
                        EncounterStableId = source.NatureEncounterVictoryRequest.EncounterStableId,
                    },
                IntegratedWorldConfirmRequest = source.IntegratedWorldConfirmRequest == null
                    ? null : 경영SimulationSessionAggregate.CloneIntegratedWorldCommand(
                        source.IntegratedWorldConfirmRequest),
                FacilityDamageQueueRequest = source.FacilityDamageQueueRequest == null ? null
                    : new SimulationFacilityDamageQueueRequest
                    {
                        BattleStableId = source.FacilityDamageQueueRequest.BattleStableId,
                        FacilityStableId = source.FacilityDamageQueueRequest.FacilityStableId,
                        SeverityCode = source.FacilityDamageQueueRequest.SeverityCode,
                    },
                NatureSurvivalActionRequest = source.NatureSurvivalActionRequest == null
                    ? null : CloneNatureSurvivalActionRequest(
                        source.NatureSurvivalActionRequest),
                NatureSurvivalClockAdvanceRequest =
                    source.NatureSurvivalClockAdvanceRequest == null ? null
                        : CloneNatureSurvivalClockRequest(
                            source.NatureSurvivalClockAdvanceRequest),
                NatureFocusTimingAttemptRequest =
                    source.NatureFocusTimingAttemptRequest == null ? null
                        : CloneFocusTimingAttemptRequest(
                            source.NatureFocusTimingAttemptRequest),
                ActorItemAcquireConfirmRequest =
                    source.ActorItemAcquireConfirmRequest == null ? null
                        : CloneActorItemAcquireConfirmRequest(
                            source.ActorItemAcquireConfirmRequest),
                ActorEquipmentChangeConfirmRequest =
                    source.ActorEquipmentChangeConfirmRequest == null ? null
                        : CloneActorEquipmentChangeConfirmRequest(
                            source.ActorEquipmentChangeConfirmRequest),
                HexagramCampaignState = 경영SimulationSessionAggregate
                    .CloneHexagramCampaignState(source.HexagramCampaignState),
                SeasonalOperationsCampaignCommandId =
                    source.SeasonalOperationsCampaignCommandId,
                SeasonalOperationsCampaignCommandSignature =
                    source.SeasonalOperationsCampaignCommandSignature,
                SeasonalOperationsCampaignState =
                    경영SimulationSessionAggregate
                        .CloneSeasonalOperationsCampaignState(
                            source.SeasonalOperationsCampaignState),
                WorldInteractionInvocation = source.WorldInteractionInvocation == null
                    ? null
                    : 경영SimulationSessionAggregate.CloneWorldInteractionInvocation(
                        source.WorldInteractionInvocation),
            };

        public static SimulationNatureSurvivalCommandRequest
            CloneNatureSurvivalActionRequest(SimulationNatureSurvivalCommandRequest source)
            => new SimulationNatureSurvivalCommandRequest
            {
                CommandId = source.CommandId,
                ExpectedRevision = source.ExpectedRevision,
                PlayerStableId = source.PlayerStableId,
                ActionCode = source.ActionCode,
                TargetStableId = source.TargetStableId,
                ChoiceCode = source.ChoiceCode,
                LocalX = source.LocalX,
                LocalZ = source.LocalZ,
                YawDegrees = source.YawDegrees,
                AuthoritativeRewardBonusQuantity =
                    source.AuthoritativeRewardBonusQuantity,
            };

        public static SimulationNatureSurvivalClockAdvanceRequest
            CloneNatureSurvivalClockRequest(
                SimulationNatureSurvivalClockAdvanceRequest source)
            => new SimulationNatureSurvivalClockAdvanceRequest
            {
                CommandId = source.CommandId,
                ExpectedRevision = source.ExpectedRevision,
                ElapsedRealtimeSeconds = source.ElapsedRealtimeSeconds,
                WorkInputHeld = source.WorkInputHeld,
                PauseReasonCode = source.PauseReasonCode,
            };

        public static Simulation집중판정AttemptRequest
            CloneFocusTimingAttemptRequest(
                Simulation집중판정AttemptRequest source)
            => new Simulation집중판정AttemptRequest
            {
                CommandId = source.CommandId,
                ChallengeStableId = source.ChallengeStableId,
                ExpectedWorldRevision = source.ExpectedWorldRevision,
                ExpectedChallengeRevision = source.ExpectedChallengeRevision,
                InputOffsetMillis = source.InputOffsetMillis,
            };

        public static SimulationActorItemAcquireConfirmRequest
            CloneActorItemAcquireConfirmRequest(
                SimulationActorItemAcquireConfirmRequest source)
            => new SimulationActorItemAcquireConfirmRequest
            {
                CommandId = source.CommandId,
                ExpectedEquipmentRevision = source.ExpectedEquipmentRevision,
                ActorStableId = source.ActorStableId,
                ItemInstanceStableId = source.ItemInstanceStableId,
                SpecializationWorldInteractionId =
                    source.SpecializationWorldInteractionId,
            };

        public static SimulationActorEquipmentChangeConfirmRequest
            CloneActorEquipmentChangeConfirmRequest(
                SimulationActorEquipmentChangeConfirmRequest source)
            => new SimulationActorEquipmentChangeConfirmRequest
            {
                CommandId = source.CommandId,
                ExpectedEquipmentRevision = source.ExpectedEquipmentRevision,
                ActorStableId = source.ActorStableId,
                OperationCode = source.OperationCode,
                ItemInstanceStableId = source.ItemInstanceStableId,
                SlotCode = source.SlotCode,
                SwapItemInstanceStableId = source.SwapItemInstanceStableId,
                SpecializationWorldInteractionId =
                    source.SpecializationWorldInteractionId,
            };

        public static SimulationRegionalIncidentResponseConfirmRequest
            CloneRegionalIncidentResponseConfirmRequest(
                SimulationRegionalIncidentResponseConfirmRequest source)
            => new SimulationRegionalIncidentResponseConfirmRequest
            {
                CommandId = source.CommandId,
                ExpectedRevision = source.ExpectedRevision,
                ActorStableId = source.ActorStableId,
                ChoiceStableId = source.ChoiceStableId,
            };

        public static SimulationTaskCancelRequest CloneTaskCancelRequest(
            SimulationTaskCancelRequest source)
            => new SimulationTaskCancelRequest
            {
                CommandId = source.CommandId,
                ExpectedRevision = source.ExpectedRevision,
                ReasonCode = source.ReasonCode,
            };

        public static SimulationFarmWorkConfirmRequest CloneFarmWorkConfirmRequest(
            SimulationFarmWorkConfirmRequest source)
            => new SimulationFarmWorkConfirmRequest
            {
                CommandId = source.CommandId,
                ExpectedRevision = source.ExpectedRevision,
                ActorStableId = source.ActorStableId,
                TargetStableId = source.TargetStableId,
                ActionCode = source.ActionCode,
                AssignmentKindCode = source.AssignmentKindCode,
                PreferredSpatialStableId = source.PreferredSpatialStableId,
            };

        public static SimulationFarmWorkPlanConfirmRequest CloneFarmWorkPlanConfirmRequest(
            SimulationFarmWorkPlanConfirmRequest source)
            => new SimulationFarmWorkPlanConfirmRequest
            {
                CommandId = source.CommandId,
                ExpectedRevision = source.ExpectedRevision,
                Items = source.Items.Select(CloneFarmWorkPlanItemRequest).ToArray(),
            };

        private static SimulationFarmWorkPlanItemRequest CloneFarmWorkPlanItemRequest(
            SimulationFarmWorkPlanItemRequest source)
            => new SimulationFarmWorkPlanItemRequest
            {
                PlanItemStableId = source.PlanItemStableId,
                Priority = source.Priority,
                ActorStableId = source.ActorStableId,
                TargetStableId = source.TargetStableId,
                ActionCode = source.ActionCode,
                AssignmentKindCode = source.AssignmentKindCode,
                PreferredSpatialStableId = source.PreferredSpatialStableId,
            };

        public static SimulationThreatResponseConfirmRequest
            CloneThreatResponseConfirmRequest(
                SimulationThreatResponseConfirmRequest source)
            => new SimulationThreatResponseConfirmRequest
            {
                CommandId = source.CommandId,
                ExpectedRevision = source.ExpectedRevision,
                EncounterStableId = source.EncounterStableId,
                ActorStableId = source.ActorStableId,
                ChoiceStableId = source.ChoiceStableId,
            };

        public static SimulationCombatPerspectiveConfirmRequest
            CloneCombatPerspectiveConfirmRequest(
                SimulationCombatPerspectiveConfirmRequest source)
            => new SimulationCombatPerspectiveConfirmRequest
            {
                CommandId = source.CommandId,
                ExpectedRevision = source.ExpectedRevision,
                ActorStableId = source.ActorStableId,
                PerspectiveCode = source.PerspectiveCode,
            };

        public static SimulationCombatBeatStartRequest CloneCombatBeatStartRequest(
            SimulationCombatBeatStartRequest source)
            => new SimulationCombatBeatStartRequest
            {
                CommandId = source.CommandId,
                ExpectedRevision = source.ExpectedRevision,
                EncounterStableId = source.EncounterStableId,
                ActorStableId = source.ActorStableId,
            };

        public static SimulationCombatReactionConfirmRequest
            CloneCombatReactionConfirmRequest(
                SimulationCombatReactionConfirmRequest source)
            => new SimulationCombatReactionConfirmRequest
            {
                CommandId = source.CommandId,
                ExpectedRevision = source.ExpectedRevision,
                BeatStableId = source.BeatStableId,
                ActorStableId = source.ActorStableId,
                ReactionActionCode = source.ReactionActionCode,
                ReactionOffsetMs = source.ReactionOffsetMs,
            };

        public static SimulationTacticalOrderConfirmRequest
            CloneTacticalOrderConfirmRequest(
                SimulationTacticalOrderConfirmRequest source)
            => new SimulationTacticalOrderConfirmRequest
            {
                CommandId = source.CommandId,
                ExpectedRevision = source.ExpectedRevision,
                OrderWindowStableId = source.OrderWindowStableId,
                FrontStableId = source.FrontStableId,
                ActorStableId = source.ActorStableId,
                OrderCode = source.OrderCode,
                OpportunityStableId = source.OpportunityStableId,
            };

        public static SimulationTeamRoleCardEquipRequest
            CloneTeamRoleCardEquipRequest(SimulationTeamRoleCardEquipRequest source)
            => new SimulationTeamRoleCardEquipRequest
            {
                ClientRequestId = source.ClientRequestId,
                ExpectedRevision = source.ExpectedRevision,
                ExpectedTeamPolicyRevision = source.ExpectedTeamPolicyRevision,
                RequestingActorStableId = source.RequestingActorStableId,
                TargetActorStableId = source.TargetActorStableId,
                CardCopyStableId = source.CardCopyStableId,
                SlotCode = source.SlotCode,
            };

        public static SimulationCombatCardLoadoutSetRequest
            CloneCombatCardLoadoutSetRequest(
                SimulationCombatCardLoadoutSetRequest source)
            => new SimulationCombatCardLoadoutSetRequest
            {
                ClientRequestId = source.ClientRequestId,
                ExpectedRevision = source.ExpectedRevision,
                ExpectedTeamPolicyRevision = source.ExpectedTeamPolicyRevision,
                RequestingActorStableId = source.RequestingActorStableId,
                TargetActorStableId = source.TargetActorStableId,
                CombatControlModeCode = source.CombatControlModeCode,
                Slots = source.Slots.Select(value =>
                    new SimulationCombatCardLoadoutSlotSnapshot
                    {
                        SlotCode = value.SlotCode,
                        CardCopyStableId = value.CardCopyStableId,
                    }).ToArray(),
            };

        public static SimulationTeamActivityStartRequest
            CloneTeamActivityStartRequest(SimulationTeamActivityStartRequest source)
            => new SimulationTeamActivityStartRequest
            {
                ClientRequestId = source.ClientRequestId,
                ExpectedRevision = source.ExpectedRevision,
                ExpectedTeamPolicyRevision = source.ExpectedTeamPolicyRevision,
                ActorStableId = source.ActorStableId,
                CardCopyStableId = source.CardCopyStableId,
                ActivityRoleCode = source.ActivityRoleCode,
                ActivityStableId = source.ActivityStableId,
                LocationStableId = source.LocationStableId,
            };

        public static SimulationTeamActivityEndRequest
            CloneTeamActivityEndRequest(SimulationTeamActivityEndRequest source)
            => new SimulationTeamActivityEndRequest
            {
                ClientRequestId = source.ClientRequestId,
                ExpectedRevision = source.ExpectedRevision,
                ActorStableId = source.ActorStableId,
                ActivityStableId = source.ActivityStableId,
            };

        public static SimulationWorldItemAcquisitionConfirmRequest
            CloneWorldItemAcquisitionConfirmRequest(
                SimulationWorldItemAcquisitionConfirmRequest source)
            => new SimulationWorldItemAcquisitionConfirmRequest
            {
                CommandId = source.CommandId,
                ExpectedRevision = source.ExpectedRevision,
                PlayerStableId = source.PlayerStableId,
                BuildingStableId = source.BuildingStableId,
                ContainerStableId = source.ContainerStableId,
                ItemStackStableId = source.ItemStackStableId,
                Quantity = source.Quantity,
            };

        public static SimulationSurvivalTarotResponseConfirmRequest
            CloneSurvivalTarotResponseConfirmRequest(
                SimulationSurvivalTarotResponseConfirmRequest source)
            => new SimulationSurvivalTarotResponseConfirmRequest
            {
                CommandId = source.CommandId,
                ExpectedRevision = source.ExpectedRevision,
                OpportunityStableId = source.OpportunityStableId,
                PlayerStableId = source.PlayerStableId,
                OfferStableId = source.OfferStableId,
            };

        public static SimulationSurvivalTarotResolutionConfirmRequest
            CloneSurvivalTarotResolutionConfirmRequest(
                SimulationSurvivalTarotResolutionConfirmRequest source)
            => new SimulationSurvivalTarotResolutionConfirmRequest
            {
                CommandId = source.CommandId,
                ExpectedRevision = source.ExpectedRevision,
                OpportunityStableId = source.OpportunityStableId,
                PlayerStableId = source.PlayerStableId,
                OfferStableId = source.OfferStableId,
            };

        public static SimulationNpcPolicyChangeRequest CloneNpcPolicyChangeRequest(
            SimulationNpcPolicyChangeRequest source)
            => new SimulationNpcPolicyChangeRequest
            {
                CookingSlots = source.CookingSlots,
                CookingDurationTicks = source.CookingDurationTicks,
                ObserverWorkingTicks = source.ObserverWorkingTicks,
                ObserverRestTicks = source.ObserverRestTicks,
                ObserverRestockThreshold = source.ObserverRestockThreshold,
                CommandId = source.CommandId,
                ExpectedRevision = source.ExpectedRevision,
                PolicyStableId = source.PolicyStableId,
                AutomationEnabled = source.AutomationEnabled,
                Priority = source.Priority,
                PreferredActorStableId = source.PreferredActorStableId,
                AutoDelegationEnabled = source.AutoDelegationEnabled,
            };

        public static SimulationTurnClosingConfirmRequest CloneTurnClosingConfirmRequest(
            SimulationTurnClosingConfirmRequest source)
            => new SimulationTurnClosingConfirmRequest
            {
                CommandId = source.CommandId,
                ExpectedRevision = source.ExpectedRevision,
                Preview = new SimulationTurnClosingPreviewRequest
                {
                    ExpectedRevision = source.Preview.ExpectedRevision,
                    SelectedCardStableIds = source.Preview.SelectedCardStableIds.ToArray(),
                    SelectedTarotCard = source.Preview.SelectedTarotCard == null
                        ? null
                        : new Simulation타로CardSelectionRequest
                        {
                            OfferStableId = source.Preview.SelectedTarotCard.OfferStableId,
                            CardStableId = source.Preview.SelectedTarotCard.CardStableId,
                            OrientationCode = source.Preview.SelectedTarotCard.OrientationCode,
                        },
                    DeactivateActiveMajorArcana =
                        source.Preview.DeactivateActiveMajorArcana,
                },
            };

        public static 경영SimulationTick진행Request CloneTickRequest(
            경영SimulationTick진행Request source)
            => new 경영SimulationTick진행Request
            {
                CommandId = source.CommandId,
                ExpectedRevision = source.ExpectedRevision,
                TickCount = source.TickCount,
            };

        public static SimulationDecisionConfirmRequest CloneConfirmRequest(
            SimulationDecisionConfirmRequest source)
            => new SimulationDecisionConfirmRequest
            {
                CommandId = source.CommandId,
                ExpectedRevision = source.ExpectedRevision,
                Preview = ClonePreviewRequest(source.Preview),
            };

        public static SimulationHarvestDispositionImpactConfirmRequest
            CloneHarvestDispositionImpactConfirmRequest(
                SimulationHarvestDispositionImpactConfirmRequest source)
            => new SimulationHarvestDispositionImpactConfirmRequest
            {
                CommandId = source.CommandId,
                ExpectedRevision = source.ExpectedRevision,
                Impact = new SimulationHarvestDispositionImpactPreviewRequest
                {
                    DispositionDecisionStableId = source.Impact.DispositionDecisionStableId,
                    DispositionDecisionRevision = source.Impact.DispositionDecisionRevision,
                    HarvestLotStableId = source.Impact.HarvestLotStableId,
                    HarvestLotRevision = source.Impact.HarvestLotRevision,
                    ProductStableId = source.Impact.ProductStableId,
                    Quantity = source.Impact.Quantity,
                    UnitCode = source.Impact.UnitCode,
                    ChoiceCode = source.Impact.ChoiceCode,
                    NextWorkflowCode = source.Impact.NextWorkflowCode,
                    ActorStableId = source.Impact.ActorStableId,
                    SourceStableIds = source.Impact.SourceStableIds.ToArray(),
                },
            };

        public static SimulationLogisticsMovementConfirmRequest
            CloneLogisticsMovementConfirmRequest(
                SimulationLogisticsMovementConfirmRequest source)
            => new SimulationLogisticsMovementConfirmRequest
            {
                CommandId = source.CommandId,
                ExpectedRevision = source.ExpectedRevision,
                Movement = new SimulationLogisticsMovementPreviewRequest
                {
                    CargoStableId = source.Movement.CargoStableId,
                    CargoRevision = source.Movement.CargoRevision,
                    SourceExportCargoHandoffStableId =
                        source.Movement.SourceExportCargoHandoffStableId,
                    SourceAllocationStableId = source.Movement.SourceAllocationStableId,
                    HarvestLotStableId = source.Movement.HarvestLotStableId,
                    PackageLotStableId = source.Movement.PackageLotStableId,
                    ProductStableId = source.Movement.ProductStableId,
                    Quantity = source.Movement.Quantity,
                    UnitCode = source.Movement.UnitCode,
                    RouteStableId = source.Movement.RouteStableId,
                    OriginFacilityStableId = source.Movement.OriginFacilityStableId,
                    DestinationFacilityStableId = source.Movement.DestinationFacilityStableId,
                    ActorStableId = source.Movement.ActorStableId,
                    PreferredOriginSpatialStableId = source.Movement.PreferredOriginSpatialStableId,
                    PreferredRouteSpatialStableId = source.Movement.PreferredRouteSpatialStableId,
                    PreferredDestinationSpatialStableId = source.Movement.PreferredDestinationSpatialStableId,
                    RequiredRouteTicks = source.Movement.RequiredRouteTicks,
                    FreightTransport = 경영SimulationSessionAggregate.CloneFreightTransportBinding(
                        source.Movement.FreightTransport),
                    SourceStableIds = source.Movement.SourceStableIds.ToArray(),
                },
            };

        private static SimulationDecisionPreviewRequest ClonePreviewRequest(
            SimulationDecisionPreviewRequest source)
            => new SimulationDecisionPreviewRequest
            {
                DecisionStableId = source.DecisionStableId,
                DecisionTypeCode = source.DecisionTypeCode,
                ActorStableId = source.ActorStableId,
                TargetStableIds = source.TargetStableIds.ToArray(),
                ExpectedCosts = source.ExpectedCosts.Select(CloneValue).ToArray(),
                ExpectedEffects = source.ExpectedEffects.Select(CloneValue).ToArray(),
                Uncertainties = source.Uncertainties.ToArray(),
                BlockReasonCodes = source.BlockReasonCodes.ToArray(),
                SourceStableIds = source.SourceStableIds.ToArray(),
                Task = new SimulationTaskPlanRequest
                {
                    TaskStableId = source.Task.TaskStableId,
                    TaskTypeCode = source.Task.TaskTypeCode,
                    FacilityStableId = source.Task.FacilityStableId,
                    ActionCode = source.Task.ActionCode,
                    AssignedActorStableId = source.Task.AssignedActorStableId,
                    PreferredSpatialStableId = source.Task.PreferredSpatialStableId,
                    PreferredOriginSpatialStableId = source.Task.PreferredOriginSpatialStableId,
                    PreferredRouteSpatialStableId = source.Task.PreferredRouteSpatialStableId,
                    PreferredDestinationSpatialStableId = source.Task.PreferredDestinationSpatialStableId,
                    RouteStableId = source.Task.RouteStableId,
                    DestinationFacilityStableId = source.Task.DestinationFacilityStableId,
                    AssignedCapacity = source.Task.AssignedCapacity,
                    AssignedCapacityUnitCode = source.Task.AssignedCapacityUnitCode,
                    DurationTicks = source.Task.DurationTicks,
                    InputLotStableIds = source.Task.InputLotStableIds.ToArray(),
                    OutputCandidateCodes = source.Task.OutputCandidateCodes.ToArray(),
                    SourceStableIds = source.Task.SourceStableIds.ToArray(),
                },
            };

        private static SimulationValueProjection CloneValue(SimulationValueProjection source)
            => new SimulationValueProjection
            {
                ValueTypeCode = source.ValueTypeCode,
                TargetLedgerStableId = source.TargetLedgerStableId,
                BeforeValue = source.BeforeValue,
                Delta = source.Delta,
                AfterValue = source.AfterValue,
                UnitCode = source.UnitCode,
                SourceStableIds = source.SourceStableIds.ToArray(),
            };
    }
}
