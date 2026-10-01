using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Ssalddel.Simulation.Contracts;

namespace Ssalddel.Simulation.Domain
{
    public static partial class SimulationSessionReplay
    {
        public static 경영SimulationSessionAggregate Restore(SimulationSessionSavePackage package)
        {
            if (package == null) throw new ArgumentNullException(nameof(package));
            if (string.Equals(package.SchemaVersion,
                    SimulationSaveSchemaVersions.V33,
                    StringComparison.Ordinal))
            {
                ValidatePackage(package);
                var basePackage = SimulationSaveReplayCloner.ClonePackage(package);
                basePackage.SchemaVersion =
                    package.FranchiseSupplyBaseSchemaVersion;
                basePackage.FranchiseSupplyBaseSchemaVersion = string.Empty;
                basePackage.FranchiseSupply = null;
                basePackage.Snapshot.FranchiseSupply = null;
                basePackage.ReplayHash = SimulationReplayHasher.Calculate(
                    basePackage);
                var restored = Restore(basePackage);
                if (!string.Equals(
                        경영SimulationSessionAggregate
                            .BuildFranchiseSupplyStatePayloadKey(
                                restored.GetFranchiseSupplyState()),
                        경영SimulationSessionAggregate
                            .BuildFranchiseSupplyStatePayloadKey(
                                package.FranchiseSupply),
                        StringComparison.Ordinal))
                    throw new SimulationConflictException(
                        "FranchiseSupplyReplayStateMismatch");
                return restored;
            }
            if (string.Equals(package.SchemaVersion,
                    SimulationSaveSchemaVersions.V32,
                    StringComparison.Ordinal))
            {
                ValidatePackage(package);
                var basePackage = SimulationSaveReplayCloner.ClonePackage(package);
                basePackage.SchemaVersion =
                    package.SeasonalOperationsCampaignBaseSchemaVersion;
                basePackage.SeasonalOperationsCampaignBaseSchemaVersion =
                    string.Empty;
                basePackage.SeasonalOperationsCampaign = null;
                basePackage.Snapshot.SeasonalOperationsCampaign = null;
                basePackage.ReplayHash = SimulationReplayHasher.Calculate(basePackage);
                var restored = Restore(basePackage);
                if (!string.Equals(
                        경영SimulationSessionAggregate
                            .BuildSeasonalOperationsCampaignStatePayloadKey(
                                restored.GetSeasonalOperationsCampaignState()),
                        경영SimulationSessionAggregate
                            .BuildSeasonalOperationsCampaignStatePayloadKey(
                                package.SeasonalOperationsCampaign),
                        StringComparison.Ordinal))
                    throw new SimulationConflictException(
                        "SeasonalOperationsCampaignReplayStateMismatch");
                return restored;
            }
            if (string.Equals(package.SchemaVersion,
                    SimulationSaveSchemaVersions.V31,
                    StringComparison.Ordinal))
            {
                ValidatePackage(package);
                var basePackage = SimulationSaveReplayCloner.ClonePackage(package);
                basePackage.SchemaVersion =
                    package.HexagramCampaignBaseSchemaVersion;
                basePackage.HexagramCampaignBaseSchemaVersion = string.Empty;
                basePackage.HexagramCampaign = null;
                basePackage.ReplayHash = SimulationReplayHasher.Calculate(basePackage);
                var restored = Restore(basePackage);
                restored.RestoreHexagramCampaignState(package.HexagramCampaign);
                return restored;
            }
            if (string.Equals(package.SchemaVersion,
                    SimulationSaveSchemaVersions.V30,
                    StringComparison.Ordinal))
            {
                ValidatePackage(package);
                var initial = package.SessionCreateRequest.LearningFocus;
                var learning = package.LearningFocus;
                var basePackage = SimulationSaveReplayCloner.ClonePackage(package);
                basePackage.SchemaVersion = package.LearningFocusBaseSchemaVersion;
                basePackage.LearningFocusBaseSchemaVersion = string.Empty;
                basePackage.LearningFocus = null;
                basePackage.SessionCreateRequest.LearningFocus = null;
                basePackage.Snapshot.LearningFocus = null;
                basePackage.ReplayHash = SimulationReplayHasher.Calculate(basePackage);
                var restored = Restore(basePackage);
                restored.RestoreLearningFocusState(initial, learning);
                return restored;
            }
            if (string.Equals(package.SchemaVersion,
                    SimulationSaveSchemaVersions.V29,
                    StringComparison.Ordinal))
            {
                ValidatePackage(package);
                var basePackage = SimulationSaveReplayCloner.ClonePackage(package);
                basePackage.SchemaVersion = package.FocusMeditationBaseSchemaVersion;
                basePackage.FocusMeditationBaseSchemaVersion = string.Empty;
                basePackage.ReplayHash = SimulationReplayHasher.Calculate(basePackage);
                var restored = Restore(basePackage);
                restored.RestoreNatureFocusState(package.Snapshot.NatureSurvival);
                restored.RestoreAdditionalPlayerDomainProfiles(
                    package.PlayerDomainProfiles);
                return restored;
            }
            if (string.Equals(package.SchemaVersion,
                    SimulationSaveSchemaVersions.V28,
                    StringComparison.Ordinal))
            {
                ValidatePackage(package);
                var basePackage = SimulationSaveReplayCloner.ClonePackage(package);
                basePackage.SchemaVersion =
                    package.ActionManifestationBaseSchemaVersion;
                basePackage.ActionManifestationBaseSchemaVersion = string.Empty;
                basePackage.ActionManifestationLedger = null;
                basePackage.PlayerDomainProfile = null;
                basePackage.ReplayHash = SimulationReplayHasher.Calculate(basePackage);
                var restoredAggregate = Restore(basePackage);
                restoredAggregate.RestoreActionManifestationAndPlayerDomainState(
                    package.ActionManifestationLedger,
                    package.PlayerDomainProfile);
                return restoredAggregate;
            }
            if (string.Equals(package.SchemaVersion,
                    SimulationSaveSchemaVersions.V27,
                    StringComparison.Ordinal))
            {
                ValidatePackage(package);
                var basePackage = SimulationSaveReplayCloner.ClonePackage(package);
                basePackage.SchemaVersion =
                    package.ActorEquipmentBaseSchemaVersion;
                basePackage.ActorEquipmentBaseSchemaVersion = string.Empty;
                basePackage.ActorEquipment = null;
                basePackage.ReplayHash = SimulationReplayHasher.Calculate(basePackage);
                var restoredAggregate = Restore(basePackage);
                restoredAggregate.RestoreActorEquipmentState(
                    package.ActorEquipment);
                return restoredAggregate;
            }
            if (string.Equals(package.SchemaVersion,
                    SimulationSaveSchemaVersions.V26,
                    StringComparison.Ordinal))
            {
                ValidatePackage(package);
                var legacy = SimulationSaveReplayCloner.ClonePackage(package);
                legacy.SchemaVersion =
                    package.WorldAssetPlacementBaseSchemaVersion;
                legacy.WorldAssetPlacementBaseSchemaVersion = string.Empty;
                legacy.WorldAssetPlacement = null;
                legacy.ReplayHash = SimulationReplayHasher.Calculate(legacy);
                var restoredAggregate = Restore(legacy);
                restoredAggregate.RestoreWorldAssetPlacementState(
                    package.WorldAssetPlacement);
                return restoredAggregate;
            }
            ValidatePackage(package);
            var aggregate = new 경영SimulationSessionAggregate(
                SimulationSaveReplayCloner.CloneCreateRequest(package.SessionCreateRequest),
                package.RealityContext == null ? null
                    : 경영SimulationSessionAggregate.CloneRealityContext(
                        package.RealityContext));
            aggregate.RestoreTickRuleRevision(package.TickRuleRevision);
            aggregate.ReplayingSyntheticRecords = true;
            if (!string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V5,
                    StringComparison.Ordinal)
                && !string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V6,
                    StringComparison.Ordinal)
                && !string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V7,
                    StringComparison.Ordinal)
                && !string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V8,
                    StringComparison.Ordinal)
                && !string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V9,
                    StringComparison.Ordinal)
                && !string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V10,
                    StringComparison.Ordinal)
                && !string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V11,
                    StringComparison.Ordinal)
                && !string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V12,
                    StringComparison.Ordinal)
                && !string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V13,
                    StringComparison.Ordinal)
                && !string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V14,
                    StringComparison.Ordinal)
                && !string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V15,
                    StringComparison.Ordinal)
                && !string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V16,
                    StringComparison.Ordinal)
                && !string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V17,
                    StringComparison.Ordinal)
                && !string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V18,
                    StringComparison.Ordinal)
                && !string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V19,
                    StringComparison.Ordinal)
                && !string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V20,
                    StringComparison.Ordinal)
                && !string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V21,
                    StringComparison.Ordinal)
                && !string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V22,
                    StringComparison.Ordinal)
                && !string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V23,
                    StringComparison.Ordinal)
                && !string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V24,
                    StringComparison.Ordinal)
                && !string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V25,
                    StringComparison.Ordinal)
                && !string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V26,
                    StringComparison.Ordinal)
                && !string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V27,
                    StringComparison.Ordinal)
                && !string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V28,
                    StringComparison.Ordinal)
                && !string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V29,
                    StringComparison.Ordinal)
                && !string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V30,
                    StringComparison.Ordinal)
                && !string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V31,
                    StringComparison.Ordinal))
                aggregate.UseLegacyRegionalCausalityRules();
            if (!string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V14,
                    StringComparison.Ordinal)
                && !string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V15,
                    StringComparison.Ordinal)
                && !string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V16,
                    StringComparison.Ordinal)
                && !string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V17,
                    StringComparison.Ordinal)
                && !string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V18,
                    StringComparison.Ordinal)
                && !string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V19,
                    StringComparison.Ordinal)
                && !string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V20,
                    StringComparison.Ordinal)
                && !string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V21,
                    StringComparison.Ordinal)
                && !string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V22,
                    StringComparison.Ordinal)
                && !string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V23,
                    StringComparison.Ordinal)
                && !string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V24,
                    StringComparison.Ordinal)
                && !string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V25,
                    StringComparison.Ordinal))
                aggregate.UseLegacyRegionalDevelopmentRules();
            if (!string.Equals(aggregate.SessionStableId, package.SessionStableId, StringComparison.Ordinal))
                throw new SimulationConflictException("SimulationSaveSessionIdentityMismatch");

            for (var index = 0; index < package.CommandLog.Length; index++)
            {
                var entry = package.CommandLog[index];
                if (entry.Sequence != index + 1L)
                    throw new SimulationConflictException("SimulationCommandLogSequenceInvalid");
                EnsureSingleCommandPayload(entry);

                if (entry.CommandTypeCode == SimulationCommandTypeCodes.DecisionConfirm)
                {
                    if (entry.DecisionConfirmRequest == null || entry.TickRequest != null
                        || entry.HarvestDispositionImpactConfirmRequest != null
                        || entry.LogisticsMovementConfirmRequest != null
                        || entry.TurnClosingConfirmRequest != null
                        || entry.NpcPolicyChangeRequest != null)
                        throw new SimulationConflictException("SimulationCommandLogPayloadInvalid");
                    aggregate.ReplayDecision(
                        SimulationSaveReplayCloner.CloneConfirmRequest(entry.DecisionConfirmRequest));
                }
                else if (entry.CommandTypeCode == SimulationCommandTypeCodes.HarvestDispositionImpactConfirm)
                {
                    if (entry.HarvestDispositionImpactConfirmRequest == null
                        || entry.TickRequest != null || entry.DecisionConfirmRequest != null
                        || entry.LogisticsMovementConfirmRequest != null
                        || entry.TurnClosingConfirmRequest != null
                        || entry.NpcPolicyChangeRequest != null)
                        throw new SimulationConflictException("SimulationCommandLogPayloadInvalid");
                    aggregate.ConfirmHarvestDispositionImpact(
                        SimulationSaveReplayCloner.CloneHarvestDispositionImpactConfirmRequest(
                            entry.HarvestDispositionImpactConfirmRequest));
                }
                else if (entry.CommandTypeCode == SimulationCommandTypeCodes.LogisticsMovementConfirm)
                {
                    if (entry.LogisticsMovementConfirmRequest == null
                        || entry.TickRequest != null || entry.DecisionConfirmRequest != null
                        || entry.HarvestDispositionImpactConfirmRequest != null
                        || entry.TurnClosingConfirmRequest != null
                        || entry.NpcPolicyChangeRequest != null)
                        throw new SimulationConflictException("SimulationCommandLogPayloadInvalid");
                    aggregate.ConfirmLogisticsMovement(
                        SimulationSaveReplayCloner.CloneLogisticsMovementConfirmRequest(
                            entry.LogisticsMovementConfirmRequest));
                }
                else if (entry.CommandTypeCode == SimulationCommandTypeCodes.TurnClosingConfirm)
                {
                    if (entry.TurnClosingConfirmRequest == null || entry.TickRequest != null
                        || entry.DecisionConfirmRequest != null
                        || entry.HarvestDispositionImpactConfirmRequest != null
                        || entry.LogisticsMovementConfirmRequest != null
                        || entry.NpcPolicyChangeRequest != null)
                        throw new SimulationConflictException("SimulationCommandLogPayloadInvalid");
                    aggregate.ConfirmTurnClosing(
                        SimulationSaveReplayCloner.CloneTurnClosingConfirmRequest(
                            entry.TurnClosingConfirmRequest));
                }
                else if (entry.CommandTypeCode == SimulationCommandTypeCodes.NpcPolicyChange)
                {
                    if (entry.NpcPolicyChangeRequest == null || entry.TickRequest != null
                        || entry.DecisionConfirmRequest != null
                        || entry.HarvestDispositionImpactConfirmRequest != null
                        || entry.LogisticsMovementConfirmRequest != null
                        || entry.TurnClosingConfirmRequest != null)
                        throw new SimulationConflictException("SimulationCommandLogPayloadInvalid");
                    aggregate.UpdateNpcPolicy(
                        SimulationSaveReplayCloner.CloneNpcPolicyChangeRequest(
                            entry.NpcPolicyChangeRequest));
                }
                else if (entry.CommandTypeCode
                    == SimulationCommandTypeCodes.WorldItemAcquisitionConfirm)
                {
                    aggregate.ConfirmWorldItemAcquisition(
                        SimulationSaveReplayCloner.CloneWorldItemAcquisitionConfirmRequest(
                            entry.WorldItemAcquisitionConfirmRequest!));
                }
                else if (entry.CommandTypeCode
                    == SimulationCommandTypeCodes.SurvivalTarotResponseConfirm)
                {
                    aggregate.ConfirmSurvivalTarotResponse(
                        SimulationSaveReplayCloner.CloneSurvivalTarotResponseConfirmRequest(
                            entry.SurvivalTarotResponseConfirmRequest!));
                }
                else if (entry.CommandTypeCode
                    == SimulationCommandTypeCodes.SurvivalTarotResolutionConfirm)
                {
                    aggregate.ConfirmSurvivalTarotResolution(
                        SimulationSaveReplayCloner.CloneSurvivalTarotResolutionConfirmRequest(
                            entry.SurvivalTarotResolutionConfirmRequest!));
                }
                else if (entry.CommandTypeCode
                    == SimulationCommandTypeCodes.FarmWorkConfirm)
                {
                    aggregate.ConfirmFarmWork(
                        SimulationSaveReplayCloner.CloneFarmWorkConfirmRequest(
                            entry.FarmWorkConfirmRequest!));
                }
                else if (entry.CommandTypeCode
                    == SimulationCommandTypeCodes.FarmWorkPlanConfirm)
                {
                    aggregate.ConfirmFarmWorkPlan(
                        SimulationSaveReplayCloner.CloneFarmWorkPlanConfirmRequest(
                            entry.FarmWorkPlanConfirmRequest!));
                }
                else if (entry.CommandTypeCode
                    == SimulationCommandTypeCodes.ThreatResponseConfirm)
                {
                    aggregate.ConfirmThreatResponse(
                        SimulationSaveReplayCloner.CloneThreatResponseConfirmRequest(
                            entry.ThreatResponseConfirmRequest!));
                }
                else if (entry.CommandTypeCode
                    == SimulationCommandTypeCodes.CombatPerspectiveConfirm)
                {
                    aggregate.ConfirmCombatPerspective(
                        SimulationSaveReplayCloner.CloneCombatPerspectiveConfirmRequest(
                            entry.CombatPerspectiveConfirmRequest!));
                }
                else if (entry.CommandTypeCode
                    == SimulationCommandTypeCodes.CombatBeatStart)
                {
                    aggregate.StartCombatBeat(
                        SimulationSaveReplayCloner.CloneCombatBeatStartRequest(
                            entry.CombatBeatStartRequest!));
                }
                else if (entry.CommandTypeCode
                    == SimulationCommandTypeCodes.CombatReactionConfirm)
                {
                    aggregate.ConfirmCombatReaction(
                        SimulationSaveReplayCloner.CloneCombatReactionConfirmRequest(
                            entry.CombatReactionConfirmRequest!));
                }
                else if (entry.CommandTypeCode
                    == SimulationCommandTypeCodes.TacticalOrderConfirm)
                {
                    aggregate.ConfirmTacticalOrder(
                        SimulationSaveReplayCloner.CloneTacticalOrderConfirmRequest(
                            entry.TacticalOrderConfirmRequest!));
                }
                else if (entry.CommandTypeCode
                    == SimulationCommandTypeCodes.TeamRoleCardEquip)
                {
                    aggregate.EquipTeamRoleCard(
                        SimulationSaveReplayCloner.CloneTeamRoleCardEquipRequest(
                            entry.TeamRoleCardEquipRequest!));
                }
                else if (entry.CommandTypeCode
                    == SimulationCommandTypeCodes.TeamActivityStart)
                {
                    aggregate.StartTeamActivity(
                        SimulationSaveReplayCloner.CloneTeamActivityStartRequest(
                            entry.TeamActivityStartRequest!));
                }
                else if (entry.CommandTypeCode
                    == SimulationCommandTypeCodes.CombatCardLoadoutSet)
                {
                    aggregate.SetTeamCombatCardLoadout(
                        SimulationSaveReplayCloner.CloneCombatCardLoadoutSetRequest(
                            entry.CombatCardLoadoutSetRequest!));
                }
                else if (entry.CommandTypeCode
                    == SimulationCommandTypeCodes.TeamActivityEnd)
                {
                    aggregate.EndTeamActivity(
                        SimulationSaveReplayCloner.CloneTeamActivityEndRequest(
                            entry.TeamActivityEndRequest!));
                }
                else if (entry.CommandTypeCode
                    == SimulationCommandTypeCodes.TileTraversalConfirm)
                {
                    aggregate.ConfirmTileTraversal(
                        경영SimulationSessionAggregate.CloneTileTraversalRequest(
                            entry.TileTraversalConfirmRequest!));
                }
                else if (entry.CommandTypeCode
                    == SimulationCommandTypeCodes.CollectibleCardDraw)
                {
                    aggregate.DrawCollectibleCard(
                        경영SimulationSessionAggregate.CloneCollectibleCardDrawRequest(
                            entry.CollectibleCardDrawRequest!));
                }
                else if (entry.CommandTypeCode
                    == SimulationCommandTypeCodes.CollectibleCardTransfer)
                {
                    aggregate.TransferCollectibleCard(
                        경영SimulationSessionAggregate.CloneCollectibleCardTransferRequest(
                        entry.CollectibleCardTransferRequest!));
                }
                else if (entry.CommandTypeCode == SimulationCommandTypeCodes.TaskCancel)
                {
                    aggregate.CancelTask(
                        entry.TaskStableId!,
                        SimulationSaveReplayCloner.CloneTaskCancelRequest(
                            entry.TaskCancelRequest!));
                }
                else if (entry.CommandTypeCode ==
                    SimulationCommandTypeCodes.RegionalIncidentResponseConfirm)
                {
                    aggregate.ConfirmRegionalIncidentResponse(
                        entry.WorldEventStableId!,
                        SimulationSaveReplayCloner.CloneRegionalIncidentResponseConfirmRequest(
                            entry.RegionalIncidentResponseConfirmRequest!));
                }
                else if (entry.CommandTypeCode ==
                    SimulationCommandTypeCodes.NatureEncounterVictory)
                {
                    aggregate.ApplyNatureEncounterVictory(
                        entry.NatureEncounterVictoryRequest!.BattleStableId,
                        entry.NatureEncounterVictoryRequest.EncounterStableId);
                }
                else if (entry.CommandTypeCode ==
                    SimulationCommandTypeCodes.IntegratedWorldConfirm)
                {
                    aggregate.ConfirmIntegratedWorldCommand(
                        경영SimulationSessionAggregate.CloneIntegratedWorldCommand(
                            entry.IntegratedWorldConfirmRequest!));
                }
                else if (entry.CommandTypeCode ==
                    SimulationCommandTypeCodes.IntegratedWorldEffectEnqueued)
                {
                    aggregate.QueueFacilityBattleDamage(
                        entry.FacilityDamageQueueRequest!.BattleStableId,
                        entry.FacilityDamageQueueRequest.FacilityStableId,
                        entry.FacilityDamageQueueRequest.SeverityCode);
                }
                else if (entry.CommandTypeCode ==
                    SimulationCommandTypeCodes.NatureSurvivalActionConfirm)
                {
                    aggregate.ConfirmNatureSurvivalAction(
                        SimulationSaveReplayCloner.CloneNatureSurvivalActionRequest(
                            entry.NatureSurvivalActionRequest!));
                }
                else if (entry.CommandTypeCode ==
                    SimulationCommandTypeCodes.NatureSurvivalClockAdvance)
                {
                    aggregate.AdvanceNatureSurvivalClock(
                        SimulationSaveReplayCloner.CloneNatureSurvivalClockRequest(
                            entry.NatureSurvivalClockAdvanceRequest!));
                }
                else if (entry.CommandTypeCode ==
                    SimulationCommandTypeCodes.NatureFocusTimingAttempt)
                {
                    aggregate.SubmitNatureFocusTiming(
                        SimulationSaveReplayCloner.CloneFocusTimingAttemptRequest(
                            entry.NatureFocusTimingAttemptRequest!));
                }
                else if (entry.CommandTypeCode ==
                    SimulationCommandTypeCodes.ActorItemAcquireConfirm)
                {
                    aggregate.ConfirmActorItemAcquire(
                        SimulationSaveReplayCloner
                            .CloneActorItemAcquireConfirmRequest(
                                entry.ActorItemAcquireConfirmRequest!));
                }
                else if (entry.CommandTypeCode ==
                    SimulationCommandTypeCodes.ActorEquipmentChangeConfirm)
                {
                    aggregate.ConfirmActorEquipmentChange(
                        SimulationSaveReplayCloner
                            .CloneActorEquipmentChangeConfirmRequest(
                                entry.ActorEquipmentChangeConfirmRequest!));
                }
                else if (entry.CommandTypeCode == SimulationCommandTypeCodes
                    .HexagramCampaignStateTransition)
                {
                    aggregate.ReplayHexagramCampaignTransition(
                        entry.HexagramCampaignState!);
                }
                else if (entry.CommandTypeCode == SimulationCommandTypeCodes
                    .SeasonalOperationsCampaignStateTransition)
                {
                    aggregate.ReplaySeasonalOperationsCampaignTransition(
                        entry.SeasonalOperationsCampaignCommandId,
                        entry.SeasonalOperationsCampaignCommandSignature,
                        entry.SeasonalOperationsCampaignState!);
                }
                else if (entry.CommandTypeCode == SimulationCommandTypeCodes
                    .FranchiseSupplyChainStateTransition)
                {
                    aggregate.ReplayFranchiseSupplyTransition(
                        entry.FranchiseSupplyClientRequestId,
                        entry.FranchiseSupplyCommandSignature,
                        entry.FranchiseSupplyState!);
                }
                else if (entry.CommandTypeCode == SimulationCommandTypeCodes.TickAdvance)
                {
                    if (entry.TickRequest == null || entry.DecisionConfirmRequest != null
                        || entry.HarvestDispositionImpactConfirmRequest != null
                        || entry.LogisticsMovementConfirmRequest != null
                        || entry.TurnClosingConfirmRequest != null
                        || entry.NpcPolicyChangeRequest != null)
                        throw new SimulationConflictException("SimulationCommandLogPayloadInvalid");
                    aggregate.Advance(
                        SimulationSaveReplayCloner.CloneTickRequest(entry.TickRequest));
                }
                else
                {
                    throw new SimulationConflictException("SimulationCommandTypeUnsupported");
                }

                var current = aggregate.Snapshot();
                if (current.CurrentTick != entry.AppliedWorldTick
                    || current.Revision != entry.ResultingWorldRevision)
                {
                    throw new SimulationConflictException("SimulationCommandReplayResultMismatch");
                }
            }

            aggregate.RestoreLhWorldState(package.LhWorld);
            aggregate.RestoreWorldInteractionEvidence(package);
            if ((string.Equals(package.SchemaVersion,
                     SimulationSaveSchemaVersions.V22, StringComparison.Ordinal)
                 || string.Equals(package.SchemaVersion,
                     SimulationSaveSchemaVersions.V23, StringComparison.Ordinal)
                 || string.Equals(package.SchemaVersion,
                     SimulationSaveSchemaVersions.V24, StringComparison.Ordinal)
                 || string.Equals(package.SchemaVersion,
                     SimulationSaveSchemaVersions.V25, StringComparison.Ordinal))
                && package.SpatialComposition != null)
                aggregate.RestoreSpatialCompositionState(
                    package.SpatialComposition);
            var replayed = aggregate.CreateSavePackage(new SimulationSessionSaveRequest
            {
                SaveStableId = package.SaveStableId,
                ExpectedRevision = aggregate.Revision,
            });
            if (!string.Equals(package.SchemaVersion,
                    SimulationSaveSchemaVersions.V33,
                    StringComparison.Ordinal)
                && string.Equals(replayed.SchemaVersion,
                    SimulationSaveSchemaVersions.V33,
                    StringComparison.Ordinal))
            {
                replayed.SchemaVersion =
                    replayed.FranchiseSupplyBaseSchemaVersion;
                replayed.FranchiseSupplyBaseSchemaVersion = string.Empty;
                replayed.FranchiseSupply = null;
                replayed.Snapshot.FranchiseSupply = null;
                replayed.ReplayHash = SimulationReplayHasher.Calculate(replayed);
            }
            if (!string.Equals(package.SchemaVersion,
                    SimulationSaveSchemaVersions.V32,
                    StringComparison.Ordinal)
                && string.Equals(replayed.SchemaVersion,
                    SimulationSaveSchemaVersions.V32,
                    StringComparison.Ordinal))
            {
                replayed.SchemaVersion =
                    replayed.SeasonalOperationsCampaignBaseSchemaVersion;
                replayed.SeasonalOperationsCampaignBaseSchemaVersion =
                    string.Empty;
                replayed.SeasonalOperationsCampaign = null;
                replayed.Snapshot.SeasonalOperationsCampaign = null;
                replayed.ReplayHash = SimulationReplayHasher.Calculate(replayed);
            }
            if (!string.Equals(package.SchemaVersion,
                    SimulationSaveSchemaVersions.V31,
                    StringComparison.Ordinal)
                && string.Equals(replayed.SchemaVersion,
                    SimulationSaveSchemaVersions.V31,
                    StringComparison.Ordinal))
            {
                replayed.SchemaVersion =
                    replayed.HexagramCampaignBaseSchemaVersion;
                replayed.HexagramCampaignBaseSchemaVersion = string.Empty;
                replayed.HexagramCampaign = null;
                replayed.ReplayHash = SimulationReplayHasher.Calculate(replayed);
            }
            if (!string.Equals(package.SchemaVersion,
                    SimulationSaveSchemaVersions.V27,
                    StringComparison.Ordinal)
                && string.Equals(replayed.SchemaVersion,
                    SimulationSaveSchemaVersions.V27,
                    StringComparison.Ordinal))
            {
                replayed.SchemaVersion = replayed.ActorEquipmentBaseSchemaVersion;
                replayed.ActorEquipmentBaseSchemaVersion = string.Empty;
                replayed.ActorEquipment = null;
                replayed.ReplayHash = SimulationReplayHasher.Calculate(replayed);
            }
            if (string.Equals(package.SchemaVersion,
                    SimulationSaveSchemaVersions.V17, StringComparison.Ordinal)
                && !string.Equals(replayed.SchemaVersion,
                    SimulationSaveSchemaVersions.V17, StringComparison.Ordinal))
            {
                replayed.SchemaVersion = SimulationSaveSchemaVersions.V17;
                replayed.ReplayHash = SimulationReplayHasher.Calculate(replayed);
            }
            if (string.Equals(package.SchemaVersion,
                    SimulationSaveSchemaVersions.V21, StringComparison.Ordinal)
                && (string.Equals(replayed.SchemaVersion,
                        SimulationSaveSchemaVersions.V22, StringComparison.Ordinal)
                    || string.Equals(replayed.SchemaVersion,
                        SimulationSaveSchemaVersions.V23, StringComparison.Ordinal)
                    || string.Equals(replayed.SchemaVersion,
                        SimulationSaveSchemaVersions.V24, StringComparison.Ordinal)
                    || string.Equals(replayed.SchemaVersion,
                        SimulationSaveSchemaVersions.V25, StringComparison.Ordinal)))
            {
                replayed.SchemaVersion = SimulationSaveSchemaVersions.V21;
                replayed.SpatialComposition = null;
                replayed.SpatialCompositionHandle = null;
                replayed.ReplayHash = SimulationReplayHasher.Calculate(replayed);
            }
            if (string.Equals(package.SchemaVersion,
                    SimulationSaveSchemaVersions.V22, StringComparison.Ordinal)
                && (string.Equals(replayed.SchemaVersion,
                        SimulationSaveSchemaVersions.V23, StringComparison.Ordinal)
                    || string.Equals(replayed.SchemaVersion,
                        SimulationSaveSchemaVersions.V24, StringComparison.Ordinal)
                    || string.Equals(replayed.SchemaVersion,
                        SimulationSaveSchemaVersions.V25, StringComparison.Ordinal)))
            {
                replayed.SchemaVersion = SimulationSaveSchemaVersions.V22;
                replayed.ReplayHash = SimulationReplayHasher.Calculate(replayed);
            }
            replayed = SimulationBattleSaveReplay.AttachToPackage(
                replayed,
                package.Battles);
            if (!string.Equals(replayed.ReplayHash, package.ReplayHash, StringComparison.Ordinal))
                throw new SimulationConflictException("SimulationReplayHashMismatch");
            if (replayed.SavedWorldTick != package.SavedWorldTick
                || replayed.SavedWorldRevision != package.SavedWorldRevision)
            {
                throw new SimulationConflictException("SimulationSavePositionMismatch");
            }

            aggregate.ReplayingSyntheticRecords = false;
            return aggregate;
        }

        private static void ValidatePackage(SimulationSessionSavePackage package)
        {
            if (package == null) throw new ArgumentNullException(nameof(package));
            if (package.TickRuleRevision != string.Empty && package.TickRuleRevision != SimulationTickRuleRevisions.SingleStep)
                throw new SimulationContractException("SimulationTickRuleRevisionUnsupported");
            if (string.Equals(package.SchemaVersion,
                    SimulationSaveSchemaVersions.V33,
                    StringComparison.Ordinal))
            {
                if (string.IsNullOrWhiteSpace(
                        package.FranchiseSupplyBaseSchemaVersion)
                    || string.Equals(package.FranchiseSupplyBaseSchemaVersion,
                        SimulationSaveSchemaVersions.V33,
                        StringComparison.Ordinal)
                    || package.FranchiseSupply == null
                    || package.Snapshot.FranchiseSupply == null
                    || !string.Equals(
                        경영SimulationSessionAggregate
                            .BuildFranchiseSupplyStatePayloadKey(
                                package.FranchiseSupply),
                        경영SimulationSessionAggregate
                            .BuildFranchiseSupplyStatePayloadKey(
                                package.Snapshot.FranchiseSupply),
                        StringComparison.Ordinal))
                    throw new SimulationContractException(
                        "FranchiseSupplySaveStateInvalid");
                경영SimulationSessionAggregate.ValidateFranchiseSupplyState(
                    package.FranchiseSupply);
                if (!string.Equals(package.ReplayHash,
                        SimulationReplayHasher.Calculate(package),
                        StringComparison.Ordinal))
                    throw new SimulationConflictException(
                        "SimulationReplayHashMismatch");
                var basePackage = SimulationSaveReplayCloner.ClonePackage(package);
                basePackage.SchemaVersion =
                    package.FranchiseSupplyBaseSchemaVersion;
                basePackage.FranchiseSupplyBaseSchemaVersion = string.Empty;
                basePackage.FranchiseSupply = null;
                basePackage.Snapshot.FranchiseSupply = null;
                basePackage.ReplayHash = SimulationReplayHasher.Calculate(
                    basePackage);
                ValidatePackage(basePackage);
                return;
            }
            if (string.Equals(package.SchemaVersion,
                    SimulationSaveSchemaVersions.V32,
                    StringComparison.Ordinal))
            {
                if (string.IsNullOrWhiteSpace(
                        package.SeasonalOperationsCampaignBaseSchemaVersion)
                    || string.Equals(
                        package.SeasonalOperationsCampaignBaseSchemaVersion,
                        SimulationSaveSchemaVersions.V32,
                        StringComparison.Ordinal)
                    || package.SeasonalOperationsCampaign == null
                    || package.Snapshot.SeasonalOperationsCampaign == null
                    || !string.Equals(
                        경영SimulationSessionAggregate
                            .BuildSeasonalOperationsCampaignStatePayloadKey(
                                package.SeasonalOperationsCampaign),
                        경영SimulationSessionAggregate
                            .BuildSeasonalOperationsCampaignStatePayloadKey(
                                package.Snapshot.SeasonalOperationsCampaign),
                        StringComparison.Ordinal))
                    throw new SimulationContractException(
                        "SeasonalOperationsCampaignSaveStateInvalid");
                if (!string.Equals(package.ReplayHash,
                        SimulationReplayHasher.Calculate(package),
                        StringComparison.Ordinal))
                    throw new SimulationConflictException(
                        "SimulationReplayHashMismatch");
                var basePackage = SimulationSaveReplayCloner.ClonePackage(package);
                basePackage.SchemaVersion =
                    package.SeasonalOperationsCampaignBaseSchemaVersion;
                basePackage.SeasonalOperationsCampaignBaseSchemaVersion =
                    string.Empty;
                basePackage.SeasonalOperationsCampaign = null;
                basePackage.Snapshot.SeasonalOperationsCampaign = null;
                basePackage.ReplayHash = SimulationReplayHasher.Calculate(
                    basePackage);
                ValidatePackage(basePackage);
                return;
            }
            if (string.Equals(package.SchemaVersion,
                    SimulationSaveSchemaVersions.V30,
                    StringComparison.Ordinal))
            {
                if (string.IsNullOrWhiteSpace(
                        package.LearningFocusBaseSchemaVersion)
                    || string.Equals(package.LearningFocusBaseSchemaVersion,
                        SimulationSaveSchemaVersions.V30,
                        StringComparison.Ordinal)
                    || package.SessionCreateRequest.LearningFocus == null
                    || package.LearningFocus == null
                    || package.Snapshot.LearningFocus == null
                    || !string.Equals(package.LearningFocus.StateHashSha256,
                        package.Snapshot.LearningFocus.StateHashSha256,
                        StringComparison.Ordinal))
                    throw new SimulationContractException(
                        "SimulationLearningFocusSaveStateInvalid");
                Simulation학습중점State.Restore(package.LearningFocus);
                if (!string.Equals(package.ReplayHash,
                        SimulationReplayHasher.Calculate(package),
                        StringComparison.Ordinal))
                    throw new SimulationConflictException(
                        "SimulationReplayHashMismatch");
                var basePackage = SimulationSaveReplayCloner.ClonePackage(package);
                basePackage.SchemaVersion =
                    package.LearningFocusBaseSchemaVersion;
                basePackage.LearningFocusBaseSchemaVersion = string.Empty;
                basePackage.LearningFocus = null;
                basePackage.SessionCreateRequest.LearningFocus = null;
                basePackage.Snapshot.LearningFocus = null;
                basePackage.ReplayHash = SimulationReplayHasher.Calculate(
                    basePackage);
                ValidatePackage(basePackage);
                return;
            }
            if (string.Equals(package.SchemaVersion,
                    SimulationSaveSchemaVersions.V29,
                    StringComparison.Ordinal))
            {
                if (!string.Equals(package.FocusMeditationBaseSchemaVersion,
                        SimulationSaveSchemaVersions.V28,
                        StringComparison.Ordinal)
                    || package.PlayerDomainProfile == null
                    || !string.Equals(package.PlayerDomainProfile.SchemaCode,
                        Simulation플레이어분야SchemaCodes.분야Profile,
                        StringComparison.Ordinal))
                    throw new SimulationContractException(
                        "SimulationFocusMeditationSaveStateInvalid");
                Simulation플레이어분야Engine.Restore(
                    package.PlayerDomainProfile);
                if (!string.Equals(package.ReplayHash,
                        SimulationReplayHasher.Calculate(package),
                        StringComparison.Ordinal))
                    throw new SimulationConflictException(
                        "SimulationReplayHashMismatch");
                var basePackage = SimulationSaveReplayCloner.ClonePackage(package);
                basePackage.SchemaVersion = package.FocusMeditationBaseSchemaVersion;
                basePackage.FocusMeditationBaseSchemaVersion = string.Empty;
                basePackage.ReplayHash = SimulationReplayHasher.Calculate(basePackage);
                ValidatePackage(basePackage);
                return;
            }
            if (string.Equals(package.SchemaVersion,
                    SimulationSaveSchemaVersions.V28,
                    StringComparison.Ordinal))
            {
                if (string.IsNullOrWhiteSpace(
                        package.ActionManifestationBaseSchemaVersion)
                    || string.Equals(package.ActionManifestationBaseSchemaVersion,
                        SimulationSaveSchemaVersions.V28,
                        StringComparison.Ordinal)
                    || package.ActionManifestationLedger == null
                    || package.PlayerDomainProfile == null)
                    throw new SimulationContractException(
                        "SimulationActionManifestationSaveStateInvalid");
                Simulation행위발현Ledger.Restore(
                    package.ActionManifestationLedger);
                Simulation플레이어분야Engine.Restore(
                    package.PlayerDomainProfile);
                if (!string.Equals(package.ReplayHash,
                        SimulationReplayHasher.Calculate(package),
                        StringComparison.Ordinal))
                    throw new SimulationConflictException(
                        "SimulationReplayHashMismatch");
                var basePackage = SimulationSaveReplayCloner.ClonePackage(package);
                basePackage.SchemaVersion =
                    package.ActionManifestationBaseSchemaVersion;
                basePackage.ActionManifestationBaseSchemaVersion = string.Empty;
                basePackage.ActionManifestationLedger = null;
                basePackage.PlayerDomainProfile = null;
                basePackage.ReplayHash = SimulationReplayHasher.Calculate(basePackage);
                ValidatePackage(basePackage);
                return;
            }
            if (string.Equals(package.SchemaVersion,
                    SimulationSaveSchemaVersions.V27,
                    StringComparison.Ordinal))
            {
                if (string.IsNullOrWhiteSpace(
                        package.ActorEquipmentBaseSchemaVersion)
                    || string.Equals(package.ActorEquipmentBaseSchemaVersion,
                        SimulationSaveSchemaVersions.V27,
                        StringComparison.Ordinal)
                    || package.ActorEquipment == null
                    || !package.ActorEquipment.IsEnabled
                    || !string.Equals(package.ActorEquipment.StateHashSha256,
                        경영SimulationSessionAggregate
                            .CalculateActorEquipmentStateHash(
                                package.ActorEquipment),
                        StringComparison.Ordinal))
                    throw new SimulationContractException(
                        "SimulationActorEquipmentStateInvalid");
                if (!string.Equals(package.ReplayHash,
                        SimulationReplayHasher.Calculate(package),
                        StringComparison.Ordinal))
                    throw new SimulationConflictException(
                        "SimulationReplayHashMismatch");
                var basePackage = SimulationSaveReplayCloner.ClonePackage(package);
                basePackage.SchemaVersion =
                    package.ActorEquipmentBaseSchemaVersion;
                basePackage.ActorEquipmentBaseSchemaVersion = string.Empty;
                basePackage.ActorEquipment = null;
                basePackage.ReplayHash = SimulationReplayHasher.Calculate(basePackage);
                ValidatePackage(basePackage);
                return;
            }
            if (string.Equals(package.SchemaVersion,
                    SimulationSaveSchemaVersions.V26,
                    StringComparison.Ordinal))
            {
                if (string.IsNullOrWhiteSpace(
                        package.WorldAssetPlacementBaseSchemaVersion)
                    || string.Equals(
                        package.WorldAssetPlacementBaseSchemaVersion,
                        SimulationSaveSchemaVersions.V26,
                        StringComparison.Ordinal))
                    throw new SimulationContractException(
                        "SimulationWorldAssetPlacementBaseSchemaInvalid");
                SimulationWorldAssetPlacementSaveReplay.Validate(
                    package.WorldAssetPlacement,
                    package.SavedWorldRevision);
                if (!string.Equals(package.ReplayHash,
                        SimulationReplayHasher.Calculate(package),
                        StringComparison.Ordinal))
                    throw new SimulationConflictException(
                        "SimulationReplayHashMismatch");
                var legacy = SimulationSaveReplayCloner.ClonePackage(package);
                legacy.SchemaVersion =
                    package.WorldAssetPlacementBaseSchemaVersion;
                legacy.WorldAssetPlacementBaseSchemaVersion = string.Empty;
                legacy.WorldAssetPlacement = null;
                legacy.ReplayHash = SimulationReplayHasher.Calculate(legacy);
                ValidatePackage(legacy);
                return;
            }
            if (!string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V1, StringComparison.Ordinal)
                && !string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V2,
                    StringComparison.Ordinal)
                && !string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V3,
                    StringComparison.Ordinal)
                && !string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V4,
                    StringComparison.Ordinal)
                && !string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V5,
                    StringComparison.Ordinal)
                && !string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V6,
                    StringComparison.Ordinal)
                && !string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V7,
                    StringComparison.Ordinal)
                && !string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V8,
                    StringComparison.Ordinal)
                && !string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V9,
                    StringComparison.Ordinal)
                && !string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V10,
                    StringComparison.Ordinal)
                && !string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V11,
                    StringComparison.Ordinal)
                && !string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V12,
                    StringComparison.Ordinal)
                && !string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V13,
                    StringComparison.Ordinal)
                && !string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V14,
                    StringComparison.Ordinal)
                && !string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V15,
                    StringComparison.Ordinal)
                && !string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V16,
                    StringComparison.Ordinal)
                && !string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V17,
                    StringComparison.Ordinal)
                && !string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V18,
                    StringComparison.Ordinal)
                && !string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V19,
                    StringComparison.Ordinal)
                && !string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V20,
                    StringComparison.Ordinal)
                && !string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V21,
                    StringComparison.Ordinal)
                && !string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V22,
                    StringComparison.Ordinal)
                && !string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V23,
                    StringComparison.Ordinal)
                && !string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V24,
                    StringComparison.Ordinal)
                && !string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V25,
                    StringComparison.Ordinal)
                && !string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V26,
                    StringComparison.Ordinal)
                && !string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V27,
                    StringComparison.Ordinal)
                && !string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V28,
                    StringComparison.Ordinal)
                && !string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V29,
                    StringComparison.Ordinal)
                && !string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V30,
                    StringComparison.Ordinal)
                && !string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V31,
                    StringComparison.Ordinal)
                && !string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V32,
                    StringComparison.Ordinal)
                && !string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V33,
                    StringComparison.Ordinal))
                throw new SimulationContractException("SimulationSaveSchemaUnsupported");
            if (string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V3,
                    StringComparison.Ordinal) && package.LhWorld == null)
                throw new SimulationContractException("SimulationLhWorldStateMissing");
            if (!string.Equals(
                package.ReplayHashAlgorithmCode,
                SimulationReplayHashAlgorithmCodes.Sha256,
                StringComparison.Ordinal))
            {
                throw new SimulationContractException("SimulationReplayHashAlgorithmUnsupported");
            }
            if (string.IsNullOrWhiteSpace(package.SaveStableId)
                || string.IsNullOrWhiteSpace(package.SessionStableId)
                || string.IsNullOrWhiteSpace(package.ReplayHash)
                || package.SessionCreateRequest == null
                || package.SessionCreateRequest.WorldContext == null
                || package.Snapshot == null
                || package.WorldInventory == null
                || package.SurvivalTarot == null
                || package.Snapshot.WorldContext == null
                || package.Snapshot.Decisions == null
                || package.Snapshot.Tasks == null
                || package.Snapshot.Effects == null
                || package.Snapshot.LogisticsMovements == null
                || package.Snapshot.FreightTransports == null
                || package.Snapshot.GroupOrders == null
                || package.Snapshot.FoodDeliveries == null
                || package.Snapshot.MarketConsumptions == null
                || package.Snapshot.IndividualOrders == null
                || package.Snapshot.StockReservations == null
                || package.Snapshot.ExportPreparations == null
                || package.Snapshot.ExportCargoPreparations == null
                || package.Snapshot.ExportCargoHandoffs == null
                || package.Snapshot.ExportPortReceipts == null
                || package.Snapshot.ExportReadinessReviews == null
                || package.Snapshot.ExportShipmentPlans == null
                || package.Snapshot.ExportShipmentExecutions == null
                || package.Snapshot.NpcOrganizations == null
                || package.Snapshot.NpcActors == null
                || package.Snapshot.NpcCapabilityGrants == null
                || package.Snapshot.NpcWorkPolicies == null
                || package.Snapshot.NpcTaskAssignments == null
                || package.Snapshot.NpcWorkRecords == null
                || package.Snapshot.NpcActionProjections == null
                || package.Snapshot.NpcFacilityInventories == null
                || ((string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V5,
                        StringComparison.Ordinal)
                    || string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V6,
                        StringComparison.Ordinal)
                    || string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V7,
                        StringComparison.Ordinal)
                    || string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V8,
                        StringComparison.Ordinal)
                    || string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V9,
                        StringComparison.Ordinal)
                    || string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V10,
                        StringComparison.Ordinal)
                    || string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V11,
                        StringComparison.Ordinal)
                    || string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V12,
                        StringComparison.Ordinal)
                    ) && package.Snapshot.RegionalCausality == null)
                || (string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V6,
                        StringComparison.Ordinal)
                    && (package.SessionCreateRequest.IntegratedWorld == null
                        || package.Snapshot.IntegratedWorld == null))
                || (string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V7,
                        StringComparison.Ordinal)
                    && package.SessionCreateRequest.IntegratedWorld != null
                    && package.Snapshot.IntegratedWorld == null)
                || (string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V8,
                        StringComparison.Ordinal)
                    && package.SessionCreateRequest.IntegratedWorld != null
                    && package.Snapshot.IntegratedWorld == null)
                || ((string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V9,
                         StringComparison.Ordinal)
                     || string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V10,
                         StringComparison.Ordinal)
                     || string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V11,
                         StringComparison.Ordinal)
                     || string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V12,
                         StringComparison.Ordinal))
                    && package.SessionCreateRequest.IntegratedWorld != null
                    && package.Snapshot.IntegratedWorld == null)
                || (string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V8,
                        StringComparison.Ordinal)
                    && (package.RealityContext == null
                        || string.IsNullOrWhiteSpace(
                            package.SessionCreateRequest.RealityContextProfileStableId)))
                || ((string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V9,
                         StringComparison.Ordinal)
                     || string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V10,
                         StringComparison.Ordinal)
                     || string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V11,
                         StringComparison.Ordinal)
                     || string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V12,
                         StringComparison.Ordinal))
                    && package.Snapshot.NatureMind == null)
                || ((string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V10,
                         StringComparison.Ordinal)
                     || string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V11,
                         StringComparison.Ordinal)
                     || string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V12,
                         StringComparison.Ordinal))
                    && package.Snapshot.AreaAccess == null)
                || ((string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V11,
                         StringComparison.Ordinal)
                     || string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V12,
                         StringComparison.Ordinal))
                    && package.Snapshot.HostedWorld == null)
                || (string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V12,
                        StringComparison.Ordinal)
                    && package.Snapshot.CoopConstruction == null)
                || (string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V13,
                        StringComparison.Ordinal)
                    && (package.SessionCreateRequest.NatureSurvival == null
                        || package.Snapshot.NatureSurvival == null
                        || !package.Snapshot.NatureSurvival.IsEnabled))
                || (string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V14,
                        StringComparison.Ordinal)
                    && (package.Snapshot.RegionalDevelopment == null
                        || package.Snapshot.RegionalDevelopment.Opportunities == null
                        || package.Snapshot.RegionalDevelopment.RouteSafeties == null
                        || package.Snapshot.RegionalDevelopment.Areas == null
                        || package.Snapshot.RegionalDevelopment.Connectors == null))
                || (string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V15,
                        StringComparison.Ordinal)
                    && (package.Snapshot.RegionalDevelopment == null
                        || package.Snapshot.RegionalDevelopment.Opportunities == null
                        || package.Snapshot.RegionalDevelopment.RouteSafeties == null
                        || package.Snapshot.RegionalDevelopment.Areas == null
                        || package.Snapshot.RegionalDevelopment.Connectors == null
                        || package.WorldInteractionManifestations == null))
                || (string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V16,
                        StringComparison.Ordinal)
                    && (package.Snapshot.RegionalDevelopment == null
                        || package.Snapshot.RegionalDevelopment.Opportunities == null
                        || package.Snapshot.RegionalDevelopment.RouteSafeties == null
                        || package.Snapshot.RegionalDevelopment.Areas == null
                        || package.Snapshot.RegionalDevelopment.Connectors == null
                        || package.WorldInteractionManifestations == null
                        || package.Snapshot.TarotContext == null
                        || package.Snapshot.TarotContext.MajorArcanaActivations == null
                        || package.Snapshot.TarotContext.OrientationInheritances == null
                        || package.Snapshot.TownNpcLife == null
                        || package.Snapshot.TownNpcLife.Items == null
                        || package.Snapshot.TownNpcLife.Npcs == null
                        || package.Snapshot.TownNpcLife.Goals == null
                        || package.Snapshot.TownNpcLife.Orders == null))
                || ((string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V17,
                        StringComparison.Ordinal)
                    || string.Equals(package.SchemaVersion,
                        SimulationSaveSchemaVersions.V18, StringComparison.Ordinal)
                    || string.Equals(package.SchemaVersion,
                        SimulationSaveSchemaVersions.V19, StringComparison.Ordinal)
                     || string.Equals(package.SchemaVersion,
                        SimulationSaveSchemaVersions.V20, StringComparison.Ordinal)
                     || string.Equals(package.SchemaVersion,
                        SimulationSaveSchemaVersions.V24, StringComparison.Ordinal)
                     || string.Equals(package.SchemaVersion,
                        SimulationSaveSchemaVersions.V25, StringComparison.Ordinal))
                    && (package.SessionCreateRequest.NatureSurvival == null
                        || !SimulationNatureSurvivalCodes.IsR2(package.SessionCreateRequest
                            .NatureSurvival.ProfileRevision)
                        || package.Snapshot.NatureSurvival == null
                        || !package.Snapshot.NatureSurvival.IsEnabled))
                || ((string.Equals(package.SchemaVersion,
                         SimulationSaveSchemaVersions.V19, StringComparison.Ordinal)
                      || string.Equals(package.SchemaVersion,
                          SimulationSaveSchemaVersions.V20, StringComparison.Ordinal)
                      || string.Equals(package.SchemaVersion,
                          SimulationSaveSchemaVersions.V24, StringComparison.Ordinal)
                      || string.Equals(package.SchemaVersion,
                          SimulationSaveSchemaVersions.V25, StringComparison.Ordinal))
                    && (package.SessionCreateRequest.NatureSurvival == null
                        || !SimulationNatureSurvivalCodes.IsR3(
                            package.SessionCreateRequest.NatureSurvival.ProfileRevision)
                        || package.SessionCreateRequest.NatureSurvival
                            .BuildingProgressionCatalog == null
                        || package.Snapshot.NatureSurvival.BuildingProgression == null))
                || ((string.Equals(package.SchemaVersion,
                         SimulationSaveSchemaVersions.V20, StringComparison.Ordinal)
                     || string.Equals(package.SchemaVersion,
                         SimulationSaveSchemaVersions.V24, StringComparison.Ordinal)
                     || string.Equals(package.SchemaVersion,
                         SimulationSaveSchemaVersions.V25, StringComparison.Ordinal))
                    && (package.SessionCreateRequest.NatureSurvival == null
                        || !SimulationNatureSurvivalCodes.IsR4(
                            package.SessionCreateRequest.NatureSurvival.ProfileRevision)))
                || ((string.Equals(package.SchemaVersion,
                         SimulationSaveSchemaVersions.V24, StringComparison.Ordinal)
                     || string.Equals(package.SchemaVersion,
                         SimulationSaveSchemaVersions.V25, StringComparison.Ordinal))
                    && (package.SessionCreateRequest.NatureSurvival == null
                        || !SimulationNatureSurvivalCodes.IsR5(
                            package.SessionCreateRequest.NatureSurvival.ProfileRevision)
                        || package.Snapshot.NatureSurvival == null
                        || package.Snapshot.NatureSurvival.DroppedTimber == null))
                || ((string.Equals(package.SchemaVersion,
                         SimulationSaveSchemaVersions.V21, StringComparison.Ordinal)
                     || ((string.Equals(package.SchemaVersion,
                              SimulationSaveSchemaVersions.V22,
                              StringComparison.Ordinal)
                          || string.Equals(package.SchemaVersion,
                              SimulationSaveSchemaVersions.V23,
                              StringComparison.Ordinal)
                          || string.Equals(package.SchemaVersion,
                              SimulationSaveSchemaVersions.V24,
                              StringComparison.Ordinal)
                          || string.Equals(package.SchemaVersion,
                              SimulationSaveSchemaVersions.V25,
                              StringComparison.Ordinal))
                        && (!string.IsNullOrWhiteSpace(package.SessionCreateRequest
                                .NpcRoutineControlRevision)
                            || (package.Snapshot.NpcRoutineExecutions?.Length
                                ?? 0) > 0)))
                    && (string.IsNullOrWhiteSpace(package.SessionCreateRequest
                            .NpcRoutineControlRevision)
                        || !SimulationNpcRoutineControlRevisionCodes.IsKnown(
                            package.SessionCreateRequest
                                .NpcRoutineControlRevision)
                        || !string.Equals(package.Snapshot.NpcRoutineControlRevision,
                            package.SessionCreateRequest.NpcRoutineControlRevision,
                            StringComparison.Ordinal)
                        || package.Snapshot.NpcRoutineExecutions == null
                        || package.SessionCreateRequest.NatureSurvival != null
                        && !SimulationNatureSurvivalCodes.IsR2(
                            package.SessionCreateRequest.NatureSurvival.ProfileRevision)))
                || (string.Equals(package.SchemaVersion,
                        SimulationSaveSchemaVersions.V22, StringComparison.Ordinal)
                    && ((package.SpatialComposition == null)
                            != (package.SpatialCompositionHandle == null)
                        || package.SpatialComposition == null
                            && (package.SessionCreateRequest.InteriorPlanHandles
                                ?.Length ?? 0) == 0
                        || package.SpatialComposition != null
                            && (!string.Equals(package.SessionCreateRequest
                                    .SpatialCompositionRuleRevision,
                                    SimulationSpatialCompositionCodes.RuleRevision,
                                    StringComparison.Ordinal)
                                || !string.Equals(package.SpatialComposition
                                        .GraphHashSha256,
                                    package.SpatialCompositionHandle!
                                        .GraphHashSha256,
                                    StringComparison.Ordinal)
                                || !string.Equals(package.SpatialComposition
                                        .GraphHashSha256,
                                    SimulationSpatialCompositionEngine
                                        .ComputeGraphHash(
                                            package.SpatialComposition),
                                    StringComparison.Ordinal))
                        || !경영SimulationSessionAggregate
                            .InteriorPlanHandlesEqual(
                                package.SessionCreateRequest.InteriorPlanHandles,
                                package.Snapshot.InteriorPlanHandles)))
                || ((string.Equals(package.SchemaVersion,
                         SimulationSaveSchemaVersions.V23, StringComparison.Ordinal)
                     || string.Equals(package.SchemaVersion,
                         SimulationSaveSchemaVersions.V24, StringComparison.Ordinal)
                     || string.Equals(package.SchemaVersion,
                         SimulationSaveSchemaVersions.V25, StringComparison.Ordinal))
                    && ((package.SpatialComposition == null)
                            != (package.SpatialCompositionHandle == null)
                        || package.SpatialComposition != null
                            && (!string.Equals(package.SessionCreateRequest
                                    .SpatialCompositionRuleRevision,
                                    SimulationSpatialCompositionCodes.RuleRevision,
                                    StringComparison.Ordinal)
                                || !string.Equals(package.SpatialComposition
                                        .GraphHashSha256,
                                    package.SpatialCompositionHandle!
                                        .GraphHashSha256,
                                    StringComparison.Ordinal)
                                || !string.Equals(package.SpatialComposition
                                        .GraphHashSha256,
                                    SimulationSpatialCompositionEngine
                                        .ComputeGraphHash(
                                            package.SpatialComposition),
                                    StringComparison.Ordinal))
                        || !경영SimulationSessionAggregate
                            .InteriorPlanHandlesEqual(
                                package.SessionCreateRequest.InteriorPlanHandles,
                                package.Snapshot.InteriorPlanHandles)))
                || package.CommandLog == null
                || package.Battles == null)
            {
                throw new SimulationContractException("SimulationSavePackageInvalid");
            }
            if (string.Equals(package.SchemaVersion,
                    SimulationSaveSchemaVersions.V22, StringComparison.Ordinal)
                || string.Equals(package.SchemaVersion,
                    SimulationSaveSchemaVersions.V23, StringComparison.Ordinal)
                || string.Equals(package.SchemaVersion,
                    SimulationSaveSchemaVersions.V24, StringComparison.Ordinal)
                || string.Equals(package.SchemaVersion,
                    SimulationSaveSchemaVersions.V25, StringComparison.Ordinal))
            {
                경영SimulationSessionAggregate.ValidateInteriorPlanHandles(
                    package.SessionCreateRequest.InteriorPlanHandles
                    ?? Array.Empty<SimulationInteriorPlanHandleSnapshot>());
                경영SimulationSessionAggregate.ValidateInteriorPlanHandles(
                    package.Snapshot.InteriorPlanHandles
                    ?? Array.Empty<SimulationInteriorPlanHandleSnapshot>());
            }
            else if ((package.SessionCreateRequest.InteriorPlanHandles?.Length ?? 0) > 0
                || (package.Snapshot.InteriorPlanHandles?.Length ?? 0) > 0)
            {
                throw new SimulationContractException(
                    "SimulationInteriorPlanHandlesRequireV22");
            }
            if (string.Equals(package.SchemaVersion,
                    SimulationSaveSchemaVersions.V23, StringComparison.Ordinal)
                || string.Equals(package.SchemaVersion,
                    SimulationSaveSchemaVersions.V24, StringComparison.Ordinal)
                || string.Equals(package.SchemaVersion,
                    SimulationSaveSchemaVersions.V25, StringComparison.Ordinal))
                ValidateWI음양주체분류(package);
            if (string.Equals(package.SchemaVersion,
                    SimulationSaveSchemaVersions.V25, StringComparison.Ordinal))
                ValidateWorldAtmosphere(package);
            if (package.Snapshot.Settlement != null
                && (package.Snapshot.Settlement.Districts == null
                    || package.Snapshot.Settlement.Facilities == null
                    || package.Snapshot.Settlement.MarketSupplyByProduct == null
                    || package.Snapshot.Settlement.ResidentConsumptionByProduct == null
                    || package.Snapshot.Settlement.ReserveStockLots == null
                    || package.Snapshot.Settlement.HarvestLotAllocations == null
                    || package.Snapshot.Settlement.ActiveTaskStableIds == null
                    || package.Snapshot.Settlement.SourceStableIds == null))
            {
                throw new SimulationContractException("SimulationSavePackageInvalid");
            }
            if (package.Snapshot.FarmSurvival != null
                && (package.Snapshot.FarmSurvival.Actors == null
                    || package.Snapshot.FarmSurvival.SoilTiles == null
                    || package.Snapshot.FarmSurvival.Defenses == null
                    || package.Snapshot.FarmSurvival.WorkOrders == null
                    || package.Snapshot.FarmSurvival.Encounters == null
                    || package.Snapshot.FarmSurvival.DayReports == null
                    || package.Snapshot.FarmSurvival.Combat == null
                    || package.Snapshot.FarmSurvival.Combat.Perspectives == null
                    || package.Snapshot.FarmSurvival.Combat.Beats == null
                    || package.Snapshot.FarmSurvival.Combat.Reactions == null
                    || package.Snapshot.FarmSurvival.Combat.Tactical == null
                    || package.Snapshot.FarmSurvival.Combat.Tactical.Fronts == null
                    || package.Snapshot.FarmSurvival.Combat.Tactical.Squads == null
                    || package.Snapshot.FarmSurvival.Combat.Tactical.Opportunities == null
                    || package.Snapshot.FarmSurvival.Combat.Tactical.OrderWindows == null
                    || package.Snapshot.FarmSurvival.Combat.Tactical.Orders == null
                    || package.Snapshot.FarmSurvival.Combat.Tactical.Resolutions == null))
            {
                throw new SimulationContractException("SimulationSavePackageInvalid");
            }
            if (package.Snapshot.TeamRoleCards != null
                && (package.Snapshot.TeamRoleCards.MemberActorStableIds == null
                    || package.Snapshot.TeamRoleCards.Cards == null
                    || package.Snapshot.TeamRoleCards.ActiveActivities == null
                    || package.Snapshot.TeamRoleCards.MemberRoles == null))
            {
                throw new SimulationContractException("SimulationSavePackageInvalid");
            }
            if (package.Snapshot.Exploration != null
                && (package.Snapshot.Exploration.ActorTilePositions == null
                    || package.Snapshot.Exploration.RevealedL2TileKeys == null
                    || package.Snapshot.Exploration.RevealedL1AreaKeys == null
                    || package.Snapshot.Exploration.DiscoveryEvents == null))
                throw new SimulationContractException("SimulationSavePackageInvalid");
            if (package.Snapshot.CollectibleCardRewards != null
                && (package.Snapshot.CollectibleCardRewards.ProbabilityProfile == null
                    || package.Snapshot.CollectibleCardRewards.Definitions == null
                    || package.Snapshot.CollectibleCardRewards.DrawOpportunities == null
                    || package.Snapshot.CollectibleCardRewards.Cards == null
                    || package.Snapshot.CollectibleCardRewards.PityStates == null
                    || package.Snapshot.CollectibleCardRewards.Evaluations == null
                    || package.Snapshot.CollectibleCardRewards.Transfers == null))
                throw new SimulationContractException("SimulationSavePackageInvalid");

            for (var index = 0; index < package.CommandLog.Length; index++)
            {
                var entry = package.CommandLog[index];
                if (entry == null || entry.Sequence != index + 1L)
                    throw new SimulationConflictException("SimulationCommandLogSequenceInvalid");
                EnsureSingleCommandPayload(entry);
                if (entry.CommandTypeCode == SimulationCommandTypeCodes.DecisionConfirm)
                {
                    if (entry.DecisionConfirmRequest == null || entry.TickRequest != null
                        || entry.HarvestDispositionImpactConfirmRequest != null
                        || entry.LogisticsMovementConfirmRequest != null
                        || entry.TurnClosingConfirmRequest != null
                        || entry.NpcPolicyChangeRequest != null)
                        throw new SimulationConflictException("SimulationCommandLogPayloadInvalid");
                    경영SimulationSessionAggregate.ValidateDecisionConfirm(
                        entry.DecisionConfirmRequest);
                }
                else if (entry.CommandTypeCode == SimulationCommandTypeCodes.HarvestDispositionImpactConfirm)
                {
                    if (entry.HarvestDispositionImpactConfirmRequest == null
                        || entry.TickRequest != null || entry.DecisionConfirmRequest != null
                        || entry.LogisticsMovementConfirmRequest != null
                        || entry.TurnClosingConfirmRequest != null
                        || entry.NpcPolicyChangeRequest != null)
                        throw new SimulationConflictException("SimulationCommandLogPayloadInvalid");
                    경영SimulationSessionAggregate.ValidateHarvestDispositionImpactConfirmRequestForReplay(
                        entry.HarvestDispositionImpactConfirmRequest);
                }
                else if (entry.CommandTypeCode == SimulationCommandTypeCodes.LogisticsMovementConfirm)
                {
                    if (entry.LogisticsMovementConfirmRequest == null
                        || entry.TickRequest != null || entry.DecisionConfirmRequest != null
                        || entry.HarvestDispositionImpactConfirmRequest != null
                        || entry.TurnClosingConfirmRequest != null
                        || entry.NpcPolicyChangeRequest != null)
                        throw new SimulationConflictException("SimulationCommandLogPayloadInvalid");
                    경영SimulationSessionAggregate.ValidateLogisticsMovementConfirmRequestForReplay(
                        entry.LogisticsMovementConfirmRequest);
                }
                else if (entry.CommandTypeCode == SimulationCommandTypeCodes.TurnClosingConfirm)
                {
                    if (entry.TurnClosingConfirmRequest == null || entry.TickRequest != null
                        || entry.DecisionConfirmRequest != null
                        || entry.HarvestDispositionImpactConfirmRequest != null
                        || entry.LogisticsMovementConfirmRequest != null
                        || entry.NpcPolicyChangeRequest != null)
                        throw new SimulationConflictException("SimulationCommandLogPayloadInvalid");
                    경영SimulationSessionAggregate.ValidateTurnClosingConfirmRequest(
                        entry.TurnClosingConfirmRequest);
                }
                else if (entry.CommandTypeCode == SimulationCommandTypeCodes.NpcPolicyChange)
                {
                    if (entry.NpcPolicyChangeRequest == null || entry.TickRequest != null
                        || entry.DecisionConfirmRequest != null
                        || entry.HarvestDispositionImpactConfirmRequest != null
                        || entry.LogisticsMovementConfirmRequest != null
                        || entry.TurnClosingConfirmRequest != null)
                        throw new SimulationConflictException("SimulationCommandLogPayloadInvalid");
                    경영SimulationSessionAggregate.ValidateNpcPolicyChangeRequest(
                        entry.NpcPolicyChangeRequest);
                }
                else if (entry.CommandTypeCode
                    == SimulationCommandTypeCodes.WorldItemAcquisitionConfirm)
                {
                    경영SimulationSessionAggregate.ValidateWorldItemAcquisitionConfirmRequest(
                        entry.WorldItemAcquisitionConfirmRequest!);
                }
                else if (entry.CommandTypeCode
                    == SimulationCommandTypeCodes.SurvivalTarotResponseConfirm)
                {
                    경영SimulationSessionAggregate.ValidateSurvivalTarotResponseRequest(
                        entry.SurvivalTarotResponseConfirmRequest!);
                }
                else if (entry.CommandTypeCode
                    == SimulationCommandTypeCodes.SurvivalTarotResolutionConfirm)
                {
                    경영SimulationSessionAggregate.ValidateSurvivalTarotResolutionRequest(
                        entry.SurvivalTarotResolutionConfirmRequest!);
                }
                else if (entry.CommandTypeCode
                    == SimulationCommandTypeCodes.FarmWorkConfirm)
                {
                    경영SimulationSessionAggregate.ValidateFarmWorkConfirmRequest(
                        entry.FarmWorkConfirmRequest!);
                }
                else if (entry.CommandTypeCode
                    == SimulationCommandTypeCodes.FarmWorkPlanConfirm)
                {
                    경영SimulationSessionAggregate.ValidateFarmWorkPlanConfirmRequest(
                        entry.FarmWorkPlanConfirmRequest!);
                }
                else if (entry.CommandTypeCode
                    == SimulationCommandTypeCodes.ThreatResponseConfirm)
                {
                    경영SimulationSessionAggregate.ValidateThreatResponseRequest(
                        entry.ThreatResponseConfirmRequest!);
                }
                else if (entry.CommandTypeCode
                    == SimulationCommandTypeCodes.CombatPerspectiveConfirm)
                {
                    경영SimulationSessionAggregate.ValidateCombatPerspectiveRequest(
                        entry.CombatPerspectiveConfirmRequest!);
                }
                else if (entry.CommandTypeCode
                    == SimulationCommandTypeCodes.CombatBeatStart)
                {
                    경영SimulationSessionAggregate.ValidateCombatBeatStartRequest(
                        entry.CombatBeatStartRequest!);
                }
                else if (entry.CommandTypeCode
                    == SimulationCommandTypeCodes.CombatReactionConfirm)
                {
                    경영SimulationSessionAggregate.ValidateCombatReactionRequest(
                        entry.CombatReactionConfirmRequest!);
                }
                else if (entry.CommandTypeCode
                    == SimulationCommandTypeCodes.TacticalOrderConfirm)
                {
                    경영SimulationSessionAggregate.ValidateTacticalOrderConfirmRequest(
                        entry.TacticalOrderConfirmRequest!);
                }
                else if (entry.CommandTypeCode
                    == SimulationCommandTypeCodes.TeamRoleCardEquip)
                {
                    SimulationTeamRoleCardState.ValidateEquip(
                        entry.TeamRoleCardEquipRequest!);
                }
                else if (entry.CommandTypeCode
                    == SimulationCommandTypeCodes.TeamActivityStart)
                {
                    SimulationTeamRoleCardState.ValidateStart(
                        entry.TeamActivityStartRequest!);
                }
                else if (entry.CommandTypeCode
                    == SimulationCommandTypeCodes.CombatCardLoadoutSet)
                {
                    SimulationTeamRoleCardState.ValidateCombatLoadout(
                        entry.CombatCardLoadoutSetRequest!);
                }
                else if (entry.CommandTypeCode
                    == SimulationCommandTypeCodes.TeamActivityEnd)
                {
                    SimulationTeamRoleCardState.ValidateEnd(
                        entry.TeamActivityEndRequest!);
                }
                else if (entry.CommandTypeCode
                    == SimulationCommandTypeCodes.TileTraversalConfirm)
                {
                    경영SimulationSessionAggregate.ValidateTileTraversalRequest(
                        entry.TileTraversalConfirmRequest!);
                }
                else if (entry.CommandTypeCode
                    == SimulationCommandTypeCodes.CollectibleCardDraw)
                {
                    경영SimulationSessionAggregate.ValidateCollectibleCardDrawRequest(
                        entry.CollectibleCardDrawRequest!);
                }
                else if (entry.CommandTypeCode
                    == SimulationCommandTypeCodes.CollectibleCardTransfer)
                {
                    경영SimulationSessionAggregate.ValidateCollectibleCardTransferRequest(
                        entry.CollectibleCardTransferRequest!);
                }
                else if (entry.CommandTypeCode == SimulationCommandTypeCodes.TaskCancel)
                {
                    if (string.IsNullOrWhiteSpace(entry.TaskStableId))
                        throw new SimulationConflictException("SimulationCommandLogPayloadInvalid");
                    경영SimulationSessionAggregate.ValidateTaskCancel(
                        entry.TaskStableId,
                        entry.TaskCancelRequest!);
                }
                else if (entry.CommandTypeCode ==
                    SimulationCommandTypeCodes.RegionalIncidentResponseConfirm)
                {
                    if (string.IsNullOrWhiteSpace(entry.WorldEventStableId))
                        throw new SimulationConflictException("SimulationCommandLogPayloadInvalid");
                    경영SimulationSessionAggregate.ValidateRegionalIncidentConfirmRequest(
                        entry.WorldEventStableId!,
                        entry.RegionalIncidentResponseConfirmRequest!);
                }
                else if (entry.CommandTypeCode ==
                    SimulationCommandTypeCodes.NatureEncounterVictory)
                {
                    if (entry.NatureEncounterVictoryRequest == null
                        || string.IsNullOrWhiteSpace(
                            entry.NatureEncounterVictoryRequest.BattleStableId)
                        || string.IsNullOrWhiteSpace(
                            entry.NatureEncounterVictoryRequest.EncounterStableId))
                        throw new SimulationConflictException("SimulationCommandLogPayloadInvalid");
                }
                else if (entry.CommandTypeCode ==
                    SimulationCommandTypeCodes.IntegratedWorldConfirm)
                {
                    if (entry.IntegratedWorldConfirmRequest == null)
                        throw new SimulationConflictException("SimulationCommandLogPayloadInvalid");
                }
                else if (entry.CommandTypeCode ==
                    SimulationCommandTypeCodes.IntegratedWorldEffectEnqueued)
                {
                    if (entry.FacilityDamageQueueRequest == null)
                        throw new SimulationConflictException("SimulationCommandLogPayloadInvalid");
                }
                else if (entry.CommandTypeCode ==
                    SimulationCommandTypeCodes.NatureSurvivalActionConfirm)
                {
                    경영SimulationSessionAggregate.ValidateNatureSurvivalCommandRequest(
                        entry.NatureSurvivalActionRequest!);
                }
                else if (entry.CommandTypeCode ==
                    SimulationCommandTypeCodes.NatureSurvivalClockAdvance)
                {
                    경영SimulationSessionAggregate.ValidateNatureSurvivalClockRequest(
                        entry.NatureSurvivalClockAdvanceRequest!);
                }
                else if (entry.CommandTypeCode ==
                    SimulationCommandTypeCodes.NatureFocusTimingAttempt)
                {
                    var request = entry.NatureFocusTimingAttemptRequest;
                    if (request == null
                        || string.IsNullOrWhiteSpace(request.CommandId)
                        || string.IsNullOrWhiteSpace(request.ChallengeStableId)
                        || request.ExpectedWorldRevision < 0
                        || request.ExpectedChallengeRevision < 0
                        || request.InputOffsetMillis < 0)
                        throw new SimulationConflictException(
                            "SimulationCommandLogPayloadInvalid");
                }
                else if (entry.CommandTypeCode ==
                    SimulationCommandTypeCodes.ActorItemAcquireConfirm)
                {
                    경영SimulationSessionAggregate
                        .ValidateActorItemAcquireConfirmRequest(
                            entry.ActorItemAcquireConfirmRequest!);
                }
                else if (entry.CommandTypeCode ==
                    SimulationCommandTypeCodes.ActorEquipmentChangeConfirm)
                {
                    경영SimulationSessionAggregate
                        .ValidateActorEquipmentChangeConfirmRequest(
                            entry.ActorEquipmentChangeConfirmRequest!);
                }
                else if (entry.CommandTypeCode == SimulationCommandTypeCodes
                    .HexagramCampaignStateTransition)
                {
                    if (entry.HexagramCampaignState == null)
                        throw new SimulationConflictException(
                            "SimulationCommandLogPayloadInvalid");
                }
                else if (entry.CommandTypeCode == SimulationCommandTypeCodes
                    .SeasonalOperationsCampaignStateTransition)
                {
                    if (entry.SeasonalOperationsCampaignState == null
                        || string.IsNullOrWhiteSpace(
                            entry.SeasonalOperationsCampaignCommandId)
                        || string.IsNullOrWhiteSpace(
                            entry.SeasonalOperationsCampaignCommandSignature))
                        throw new SimulationConflictException(
                            "SimulationCommandLogPayloadInvalid");
                }
                else if (entry.CommandTypeCode == SimulationCommandTypeCodes
                    .FranchiseSupplyChainStateTransition)
                {
                    if (entry.FranchiseSupplyState == null
                        || string.IsNullOrWhiteSpace(
                            entry.FranchiseSupplyClientRequestId)
                        || string.IsNullOrWhiteSpace(
                            entry.FranchiseSupplyCommandSignature))
                        throw new SimulationConflictException(
                            "SimulationCommandLogPayloadInvalid");
                    경영SimulationSessionAggregate.ValidateFranchiseSupplyState(
                        entry.FranchiseSupplyState);
                }
                else if (entry.CommandTypeCode == SimulationCommandTypeCodes.TickAdvance)
                {
                    if (entry.TickRequest == null || entry.DecisionConfirmRequest != null
                        || entry.HarvestDispositionImpactConfirmRequest != null
                        || entry.LogisticsMovementConfirmRequest != null
                        || entry.TurnClosingConfirmRequest != null
                        || entry.NpcPolicyChangeRequest != null)
                        throw new SimulationConflictException("SimulationCommandLogPayloadInvalid");
                    경영SimulationSessionAggregate.ValidateAdvance(entry.TickRequest);
                }
                else
                {
                    throw new SimulationConflictException("SimulationCommandTypeUnsupported");
                }
            }

            SimulationBattleSaveReplay.ValidatePackage(package);

            경영SimulationSessionAggregate.ValidateCreate(package.SessionCreateRequest);
            string packageHash;
            try
            {
                packageHash = SimulationReplayHasher.Calculate(package);
            }
            catch (Exception error) when (
                error is NullReferenceException
                || error is ArgumentException
                || error is InvalidOperationException)
            {
                throw new SimulationContractException("SimulationSavePackageInvalid");
            }
            if (!string.Equals(packageHash, package.ReplayHash, StringComparison.Ordinal))
                throw new SimulationConflictException("SimulationReplayHashMismatch");
        }

        private static void ValidateWI음양주체분류(
            SimulationSessionSavePackage package)
        {
            if (package.WorldInteractionManifestations == null)
                throw new SimulationContractException(
                    "SimulationSavePackageInvalid");

            foreach (var entry in package.CommandLog.Where(value =>
                         value.WorldInteractionInvocation != null))
            {
                var invocation = entry.WorldInteractionInvocation!;
                var classification = invocation.음양주체분류;
                if (!string.Equals(invocation.PayloadCode,
                        "WorldInteractionInvocation.v2",
                        StringComparison.Ordinal))
                {
                    if (!string.IsNullOrWhiteSpace(
                            classification?.판정RuleRevision))
                        throw new SimulationContractException(
                            "SimulationSavePackageInvalid");
                    continue;
                }

                if (!SimulationWI음양주체사분면Rules.Matches(
                        invocation.WorldInteractionId,
                        invocation.TriggerSourceCode,
                        classification)
                    || SimulationWI수행주체Codes.IsActor(
                            classification.수행주체Code)
                        && string.IsNullOrWhiteSpace(invocation.ActorStableId))
                    throw new SimulationContractException(
                        "SimulationSavePackageInvalid");
            }

            foreach (var execution in package.Snapshot.NpcRoutineExecutions
                         ?? Array.Empty<SimulationNpcRoutineExecutionSnapshot>())
            {
                var classification = execution.음양주체분류;
                if (string.IsNullOrWhiteSpace(classification?.판정RuleRevision))
                    continue;
                if (!SimulationWI음양주체사분면Rules.Matches(
                        execution.WorldInteractionId,
                        execution.TriggerSourceCode,
                        classification)
                    || SimulationWI수행주체Codes.IsActor(
                            classification.수행주체Code)
                        && string.IsNullOrWhiteSpace(execution.ActorStableId))
                    throw new SimulationContractException(
                        "SimulationSavePackageInvalid");
            }
        }

        private static void EnsureSingleCommandPayload(SimulationCommandLogEntrySnapshot entry)
        {
            var payloadCount = 0;
            if (entry.TickRequest != null) payloadCount++;
            if (entry.DecisionConfirmRequest != null) payloadCount++;
            if (entry.HarvestDispositionImpactConfirmRequest != null) payloadCount++;
            if (entry.LogisticsMovementConfirmRequest != null) payloadCount++;
            if (entry.TurnClosingConfirmRequest != null) payloadCount++;
            if (entry.NpcPolicyChangeRequest != null) payloadCount++;
            if (entry.WorldItemAcquisitionConfirmRequest != null) payloadCount++;
            if (entry.SurvivalTarotResponseConfirmRequest != null) payloadCount++;
            if (entry.SurvivalTarotResolutionConfirmRequest != null) payloadCount++;
            if (entry.FarmWorkConfirmRequest != null) payloadCount++;
            if (entry.FarmWorkPlanConfirmRequest != null) payloadCount++;
            if (entry.ThreatResponseConfirmRequest != null) payloadCount++;
            if (entry.CombatPerspectiveConfirmRequest != null) payloadCount++;
            if (entry.CombatBeatStartRequest != null) payloadCount++;
            if (entry.CombatReactionConfirmRequest != null) payloadCount++;
            if (entry.TacticalOrderConfirmRequest != null) payloadCount++;
            if (entry.TeamRoleCardEquipRequest != null) payloadCount++;
            if (entry.CombatCardLoadoutSetRequest != null) payloadCount++;
            if (entry.TeamActivityStartRequest != null) payloadCount++;
            if (entry.TeamActivityEndRequest != null) payloadCount++;
            if (entry.TileTraversalConfirmRequest != null) payloadCount++;
            if (entry.CollectibleCardDrawRequest != null) payloadCount++;
            if (entry.CollectibleCardTransferRequest != null) payloadCount++;
            if (entry.TaskCancelRequest != null) payloadCount++;
            if (entry.RegionalIncidentResponseConfirmRequest != null) payloadCount++;
            if (entry.NatureEncounterVictoryRequest != null) payloadCount++;
            if (entry.IntegratedWorldConfirmRequest != null) payloadCount++;
            if (entry.FacilityDamageQueueRequest != null) payloadCount++;
            if (entry.NatureSurvivalActionRequest != null) payloadCount++;
            if (entry.NatureSurvivalClockAdvanceRequest != null) payloadCount++;
            if (entry.NatureFocusTimingAttemptRequest != null) payloadCount++;
            if (entry.ActorItemAcquireConfirmRequest != null) payloadCount++;
            if (entry.ActorEquipmentChangeConfirmRequest != null) payloadCount++;
            if (entry.HexagramCampaignState != null) payloadCount++;
            if (entry.SeasonalOperationsCampaignState != null) payloadCount++;
            if (entry.FranchiseSupplyState != null) payloadCount++;
            if (payloadCount != 1)
                throw new SimulationConflictException("SimulationCommandLogPayloadInvalid");
        }
    }
}
