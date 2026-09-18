using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Ssalddel.Simulation.Contracts;

namespace Ssalddel.Simulation.Domain
{
    internal static partial class SimulationReplayHasher
    {
        public static string Calculate(SimulationSessionSavePackage package)
        {
            if (string.Equals(package.SchemaVersion,
                    SimulationSaveSchemaVersions.V32,
                    StringComparison.Ordinal))
            {
                var basePackage = SimulationSaveReplayCloner.ClonePackage(package);
                basePackage.SchemaVersion =
                    package.SeasonalOperationsCampaignBaseSchemaVersion;
                basePackage.SeasonalOperationsCampaignBaseSchemaVersion =
                    string.Empty;
                basePackage.SeasonalOperationsCampaign = null;
                basePackage.Snapshot.SeasonalOperationsCampaign = null;
                basePackage.ReplayHash = string.Empty;
                var baseReplayHash = Calculate(basePackage);
                var seasonalCanonical = string.Join("|",
                    SimulationSaveSchemaVersions.V32,
                    package.SeasonalOperationsCampaignBaseSchemaVersion,
                    baseReplayHash,
                    경영SimulationSessionAggregate
                        .BuildSeasonalOperationsCampaignStatePayloadKey(
                            package.SeasonalOperationsCampaign));
                using var sha = SHA256.Create();
                return BitConverter.ToString(sha.ComputeHash(
                        Encoding.UTF8.GetBytes(seasonalCanonical)))
                    .Replace("-", string.Empty).ToLowerInvariant();
            }
            if (string.Equals(package.SchemaVersion,
                    SimulationSaveSchemaVersions.V31,
                    StringComparison.Ordinal))
            {
                var basePackage = SimulationSaveReplayCloner.ClonePackage(package);
                basePackage.SchemaVersion =
                    package.HexagramCampaignBaseSchemaVersion;
                basePackage.HexagramCampaignBaseSchemaVersion = string.Empty;
                basePackage.HexagramCampaign = null;
                basePackage.ReplayHash = string.Empty;
                var baseReplayHash = Calculate(basePackage);
                var state = package.HexagramCampaign;
                var campaignCanonical = string.Join("|", new[]
                {
                    SimulationSaveSchemaVersions.V31,
                    package.HexagramCampaignBaseSchemaVersion,
                    baseReplayHash,
                    state?.RuleRevision ?? string.Empty,
                    state?.CampaignStateCode ?? string.Empty,
                    state?.HexagramStableId ?? string.Empty,
                    state?.CurrentLineOrdinal.ToString(
                        CultureInfo.InvariantCulture) ?? string.Empty,
                    state?.AttemptOrdinal.ToString(
                        CultureInfo.InvariantCulture) ?? string.Empty,
                    state?.AttemptVariationSeed.ToString(
                        CultureInfo.InvariantCulture) ?? string.Empty,
                    state?.EntrySaveStableId ?? string.Empty,
                    state?.EntryWorldRevision.ToString(
                        CultureInfo.InvariantCulture) ?? string.Empty,
                    string.Join(",", state?.TemporaryWorldInteractionIds
                        ?? Array.Empty<string>()),
                    string.Join(",", state?
                        .PermanentlyUnlockedWorldInteractionIds
                        ?? Array.Empty<string>()),
                    string.Join(",", (state?.Events
                        ?? Array.Empty<SimulationHexagramCampaignEventSnapshot>())
                        .Select(value => string.Join("~", value.EventCode,
                            value.ReasonCode, value.AttemptOrdinal,
                            value.LineOrdinal, value.WorldTick,
                            value.WorldRevision))),
                });
                // 기존 6단계 저장의 해시는 유지하고 새 단계 수는 무결성에 포함한다.
                if (state != null && state.StoryStageCount != 6)
                    campaignCanonical += "|StoryStageCount="
                        + state.StoryStageCount.ToString(CultureInfo.InvariantCulture);
                using var sha = SHA256.Create();
                return BitConverter.ToString(sha.ComputeHash(
                        Encoding.UTF8.GetBytes(campaignCanonical)))
                    .Replace("-", string.Empty).ToLowerInvariant();
            }
            if (string.Equals(package.SchemaVersion,
                    SimulationSaveSchemaVersions.V30,
                    StringComparison.Ordinal))
            {
                var basePackage = SimulationSaveReplayCloner.ClonePackage(package);
                basePackage.SchemaVersion = package.LearningFocusBaseSchemaVersion;
                basePackage.LearningFocusBaseSchemaVersion = string.Empty;
                basePackage.LearningFocus = null;
                basePackage.SessionCreateRequest.LearningFocus = null;
                basePackage.Snapshot.LearningFocus = null;
                basePackage.ReplayHash = string.Empty;
                var baseReplayHash = Calculate(basePackage);
                var v30Canonical = string.Join("|", new[]
                {
                    SimulationSaveSchemaVersions.V30,
                    package.LearningFocusBaseSchemaVersion,
                    baseReplayHash,
                    경영SimulationSessionAggregate
                        .BuildLearningFocusInitialPayloadKey(
                            package.SessionCreateRequest.LearningFocus),
                    package.LearningFocus?.StateHashSha256 ?? string.Empty,
                    package.Snapshot.LearningFocus?.StateHashSha256
                        ?? string.Empty,
                });
                using var v30Sha = SHA256.Create();
                return BitConverter.ToString(v30Sha.ComputeHash(
                        Encoding.UTF8.GetBytes(v30Canonical)))
                    .Replace("-", string.Empty).ToLowerInvariant();
            }
            if (string.Equals(package.SchemaVersion,
                    SimulationSaveSchemaVersions.V29,
                    StringComparison.Ordinal))
            {
                var basePackage = SimulationSaveReplayCloner.ClonePackage(package);
                basePackage.SchemaVersion = package.FocusMeditationBaseSchemaVersion;
                basePackage.FocusMeditationBaseSchemaVersion = string.Empty;
                basePackage.ReplayHash = string.Empty;
                var baseReplayHash = Calculate(basePackage);
                var v29Canonical = string.Join("|", new[]
                {
                    SimulationSaveSchemaVersions.V29,
                    package.FocusMeditationBaseSchemaVersion,
                    baseReplayHash,
                    package.PlayerDomainProfile?.StateHashSha256 ?? string.Empty,
                    package.PlayerDomainProfiles == null
                    || package.PlayerDomainProfiles.Length <= 1
                        ? string.Empty
                        : string.Join(",", package.PlayerDomainProfiles
                            .OrderBy(value => value.PlayerStableId,
                                StringComparer.Ordinal)
                            .Select(value => value.StateHashSha256)),
                    Simulation집중판정Codes.MeditationRuleRevision,
                    경영SimulationSessionAggregate.CalculateNatureFocusStateHash(
                        package.Snapshot.NatureSurvival),
                });
                using var sha = SHA256.Create();
                return BitConverter.ToString(sha.ComputeHash(
                        Encoding.UTF8.GetBytes(v29Canonical)))
                    .Replace("-", string.Empty).ToLowerInvariant();
            }
            if (string.Equals(package.SchemaVersion,
                    SimulationSaveSchemaVersions.V28,
                    StringComparison.Ordinal))
            {
                var basePackage = SimulationSaveReplayCloner.ClonePackage(package);
                basePackage.SchemaVersion =
                    package.ActionManifestationBaseSchemaVersion;
                basePackage.ActionManifestationBaseSchemaVersion = string.Empty;
                basePackage.ActionManifestationLedger = null;
                basePackage.PlayerDomainProfile = null;
                basePackage.ReplayHash = string.Empty;
                var baseReplayHash = Calculate(basePackage);
                var v28Canonical = string.Join("|", new[]
                {
                    SimulationSaveSchemaVersions.V28,
                    package.ActionManifestationBaseSchemaVersion,
                    baseReplayHash,
                    package.ActionManifestationLedger?.StateHashSha256
                        ?? string.Empty,
                    package.PlayerDomainProfile?.StateHashSha256
                        ?? string.Empty,
                });
                using var sha = SHA256.Create();
                return BitConverter.ToString(sha.ComputeHash(
                        Encoding.UTF8.GetBytes(v28Canonical)))
                    .Replace("-", string.Empty).ToLowerInvariant();
            }
            if (string.Equals(package.SchemaVersion,
                    SimulationSaveSchemaVersions.V27,
                    StringComparison.Ordinal))
            {
                var basePackage = SimulationSaveReplayCloner.ClonePackage(
                    package);
                basePackage.SchemaVersion =
                    package.ActorEquipmentBaseSchemaVersion;
                basePackage.ActorEquipmentBaseSchemaVersion = string.Empty;
                basePackage.ActorEquipment = null;
                basePackage.ReplayHash = string.Empty;
                var baseReplayHash = Calculate(basePackage);
                var v27Canonical = string.Join("|", new[]
                {
                    SimulationSaveSchemaVersions.V27,
                    package.ActorEquipmentBaseSchemaVersion,
                    baseReplayHash,
                    package.ActorEquipment?.StateHashSha256 ?? string.Empty,
                });
                using var v27Sha = SHA256.Create();
                return BitConverter.ToString(v27Sha.ComputeHash(
                        Encoding.UTF8.GetBytes(v27Canonical)))
                    .Replace("-", string.Empty).ToLowerInvariant();
            }
            if (string.Equals(package.SchemaVersion,
                    SimulationSaveSchemaVersions.V26,
                    StringComparison.Ordinal))
            {
                var basePackage = SimulationSaveReplayCloner.ClonePackage(
                    package);
                basePackage.SchemaVersion =
                    package.WorldAssetPlacementBaseSchemaVersion;
                basePackage.WorldAssetPlacementBaseSchemaVersion =
                    string.Empty;
                basePackage.WorldAssetPlacement = null;
                basePackage.ReplayHash = string.Empty;
                var baseReplayHash = Calculate(basePackage);
                var v26Canonical = string.Join("|", new[]
                {
                    SimulationSaveSchemaVersions.V26,
                    package.WorldAssetPlacementBaseSchemaVersion,
                    baseReplayHash,
                    package.WorldAssetPlacement?.StateHashSha256
                        ?? string.Empty,
                });
                using var v26Sha = SHA256.Create();
                return BitConverter.ToString(v26Sha.ComputeHash(
                        Encoding.UTF8.GetBytes(v26Canonical)))
                    .Replace("-", string.Empty).ToLowerInvariant();
            }
            var canonical = new StringBuilder();
            Add(canonical, package.SchemaVersion);
            if (!string.IsNullOrEmpty(package.TickRuleRevision))
            {
                Add(canonical, "tick-rule-revision");
                Add(canonical, package.TickRuleRevision);
            }
            var isV25 = string.Equals(package.SchemaVersion,
                SimulationSaveSchemaVersions.V25, StringComparison.Ordinal);
            var isV24 = string.Equals(package.SchemaVersion,
                    SimulationSaveSchemaVersions.V24, StringComparison.Ordinal)
                || isV25;
            var isV23 = string.Equals(package.SchemaVersion,
                    SimulationSaveSchemaVersions.V23, StringComparison.Ordinal)
                || isV24;
            var isV22 = string.Equals(package.SchemaVersion,
                    SimulationSaveSchemaVersions.V22, StringComparison.Ordinal)
                || isV23;
            var isV21 = string.Equals(package.SchemaVersion,
                    SimulationSaveSchemaVersions.V21, StringComparison.Ordinal)
                || isV22;
            var isV20 = string.Equals(package.SchemaVersion,
                    SimulationSaveSchemaVersions.V20, StringComparison.Ordinal)
                || isV21;
            var isV19 = string.Equals(package.SchemaVersion,
                    SimulationSaveSchemaVersions.V19, StringComparison.Ordinal)
                || isV20;
            var isV18 = string.Equals(package.SchemaVersion,
                    SimulationSaveSchemaVersions.V18, StringComparison.Ordinal)
                || isV19;
            var isV17 = string.Equals(package.SchemaVersion,
                    SimulationSaveSchemaVersions.V17, StringComparison.Ordinal)
                || isV18;
            var isV16 = string.Equals(package.SchemaVersion,
                    SimulationSaveSchemaVersions.V16, StringComparison.Ordinal)
                || isV17;
            var isV15OrLater = string.Equals(package.SchemaVersion,
                    SimulationSaveSchemaVersions.V15, StringComparison.Ordinal)
                || isV16;
            var includesRealityContext = string.Equals(package.SchemaVersion,
                SimulationSaveSchemaVersions.V8, StringComparison.Ordinal)
                || string.Equals(package.SchemaVersion,
                    SimulationSaveSchemaVersions.V9, StringComparison.Ordinal)
                || string.Equals(package.SchemaVersion,
                    SimulationSaveSchemaVersions.V10, StringComparison.Ordinal)
                || string.Equals(package.SchemaVersion,
                    SimulationSaveSchemaVersions.V11, StringComparison.Ordinal)
                || string.Equals(package.SchemaVersion,
                    SimulationSaveSchemaVersions.V12, StringComparison.Ordinal)
                || string.Equals(package.SchemaVersion,
                    SimulationSaveSchemaVersions.V13, StringComparison.Ordinal)
                || string.Equals(package.SchemaVersion,
                    SimulationSaveSchemaVersions.V14, StringComparison.Ordinal)
                || isV15OrLater;
            AddCreateRequest(canonical, package.SessionCreateRequest,
                includesRealityContext, isV16, isV21, isV22);
            var includesTarotJourneyRoot = string.Equals(package.SchemaVersion,
                SimulationSaveSchemaVersions.V7, StringComparison.Ordinal)
                || string.Equals(package.SchemaVersion,
                    SimulationSaveSchemaVersions.V8, StringComparison.Ordinal)
                || string.Equals(package.SchemaVersion,
                    SimulationSaveSchemaVersions.V9, StringComparison.Ordinal)
                || string.Equals(package.SchemaVersion,
                    SimulationSaveSchemaVersions.V10, StringComparison.Ordinal)
                || string.Equals(package.SchemaVersion,
                    SimulationSaveSchemaVersions.V11, StringComparison.Ordinal)
                || string.Equals(package.SchemaVersion,
                    SimulationSaveSchemaVersions.V12, StringComparison.Ordinal)
                || string.Equals(package.SchemaVersion,
                    SimulationSaveSchemaVersions.V13, StringComparison.Ordinal)
                || string.Equals(package.SchemaVersion,
                    SimulationSaveSchemaVersions.V14, StringComparison.Ordinal)
                || isV15OrLater;
            AddSnapshot(canonical, package.Snapshot, includesTarotJourneyRoot,
                isV16, isV21, isV22);
            if (isV25)
            {
                AddWorldAtmosphereInitialState(canonical,
                    package.SessionCreateRequest.Atmosphere!);
                AddWorldAtmosphereState(canonical, package.Snapshot.Atmosphere);
            }
            if (string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V5,
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
                || string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V13,
                    StringComparison.Ordinal)
                || string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V14,
                    StringComparison.Ordinal)
                || isV15OrLater)
                AddRegionalCausality(canonical, package.Snapshot.RegionalCausality);
            if (string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V6,
                    StringComparison.Ordinal)
                || (string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V7,
                        StringComparison.Ordinal)
                    && package.SessionCreateRequest.IntegratedWorld != null)
                || (string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V8,
                        StringComparison.Ordinal)
                    && package.SessionCreateRequest.IntegratedWorld != null)
                || (string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V9,
                        StringComparison.Ordinal)
                    && package.SessionCreateRequest.IntegratedWorld != null)
                || (string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V10,
                        StringComparison.Ordinal)
                    && package.SessionCreateRequest.IntegratedWorld != null)
                || (string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V11,
                        StringComparison.Ordinal)
                    && package.SessionCreateRequest.IntegratedWorld != null)
                || (string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V12,
                        StringComparison.Ordinal)
                    && package.SessionCreateRequest.IntegratedWorld != null)
                || (string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V13,
                        StringComparison.Ordinal)
                    && package.SessionCreateRequest.IntegratedWorld != null)
                || (string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V14,
                        StringComparison.Ordinal)
                    && package.SessionCreateRequest.IntegratedWorld != null)
                || (isV15OrLater && package.SessionCreateRequest.IntegratedWorld != null))
            {
                AddIntegratedWorldInitialState(canonical,
                    package.SessionCreateRequest.IntegratedWorld!);
                AddIntegratedWorldSnapshot(canonical, package.Snapshot.IntegratedWorld);
            }
            if ((string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V8,
                     StringComparison.Ordinal)
                 || string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V9,
                     StringComparison.Ordinal)
                 || string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V10,
                     StringComparison.Ordinal)
                 || string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V11,
                     StringComparison.Ordinal)
                 || string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V12,
                     StringComparison.Ordinal)
                 || string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V13,
                     StringComparison.Ordinal)
                 || string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V14,
                     StringComparison.Ordinal)
                 || isV15OrLater) && package.RealityContext != null)
                AddRealityContext(canonical, package.RealityContext);
            if (string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V9,
                    StringComparison.Ordinal)
                || string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V10,
                    StringComparison.Ordinal)
                || string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V11,
                    StringComparison.Ordinal)
                || string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V12,
                    StringComparison.Ordinal)
                || string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V13,
                    StringComparison.Ordinal)
                || string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V14,
                    StringComparison.Ordinal)
                || isV15OrLater)
                AddNatureMind(canonical, package.Snapshot.NatureMind);
            if (string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V10,
                    StringComparison.Ordinal)
                || string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V11,
                    StringComparison.Ordinal)
                || string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V12,
                    StringComparison.Ordinal)
                || string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V13,
                    StringComparison.Ordinal)
                || string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V14,
                    StringComparison.Ordinal)
                || isV15OrLater)
                AddAreaAccess(canonical, package.Snapshot.AreaAccess);
            if (string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V11,
                    StringComparison.Ordinal)
                || string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V12,
                    StringComparison.Ordinal)
                || string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V13,
                    StringComparison.Ordinal)
                || string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V14,
                    StringComparison.Ordinal)
                || isV15OrLater)
                AddHostedWorld(canonical, package.Snapshot.HostedWorld);
            if (string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V12,
                    StringComparison.Ordinal)
                || string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V13,
                    StringComparison.Ordinal)
                || string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V14,
                    StringComparison.Ordinal)
                || isV15OrLater)
                AddCoopConstruction(canonical, package.Snapshot.CoopConstruction);
            if (string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V13,
                    StringComparison.Ordinal)
                || (string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V14,
                        StringComparison.Ordinal)
                    && package.SessionCreateRequest.NatureSurvival != null)
                || (isV15OrLater && package.SessionCreateRequest.NatureSurvival != null))
            {
                AddNatureSurvivalInitialState(canonical,
                    package.SessionCreateRequest.NatureSurvival!, isV19);
                AddNatureSurvivalState(canonical, package.Snapshot.NatureSurvival,
                    isV17, isV19, isV20, isV24);
            }
            if (string.Equals(package.SchemaVersion, SimulationSaveSchemaVersions.V14,
                    StringComparison.Ordinal) || isV15OrLater)
                AddRegionalDevelopment(canonical, package.Snapshot.RegionalDevelopment);
            if (package.SessionCreateRequest.WorldInventory != null)
                AddWorldInventory(canonical, package.WorldInventory);
            if (package.SessionCreateRequest.SurvivalTarot != null)
                AddSurvivalTarot(canonical, package.SurvivalTarot);
            if (package.LhWorld != null)
            {
                Add(canonical, package.LhWorld.WorldSeed);
                Add(canonical, package.LhWorld.GeneratorVersion);
                Add(canonical, package.LhWorld.AreaSetStableId);
                Add(canonical, package.LhWorld.AreaSetRevision);
                Add(canonical, package.LhWorld.AreaSetBoundaryHashSha256);
                if (!string.IsNullOrWhiteSpace(package.LhWorld.WorldLayoutStableId))
                {
                    Add(canonical, package.LhWorld.WorldLayoutStableId);
                    Add(canonical, package.LhWorld.WorldLayoutRevision);
                    Add(canonical, package.LhWorld.WorldLayoutHashSha256);
                    Add(canonical, package.LhWorld.PlacementAuthorityCode);
                    Add(canonical, package.LhWorld.WorldGroundingStateCode);
                    Add(canonical, package.LhWorld.GroundingEvidenceHashSha256);
                }
                Add(canonical, package.LhWorld.LastL3CellKey);
                Add(canonical, package.LhWorld.Deltas.Length);
                foreach (var delta in package.LhWorld.Deltas.OrderBy(
                             value => value.GeneratedStableId, StringComparer.Ordinal))
                {
                    Add(canonical, delta.GeneratedStableId);
                    Add(canonical, delta.DeltaKindCode);
                    Add(canonical, delta.StateCode);
                    Add(canonical, delta.AppliedWorldRevision);
                    Add(canonical, delta.Tombstone);
                }
            }
            if (package.Battles.Length > 0)
            {
                Add(canonical, package.Battles.Length);
                foreach (var battle in package.Battles.OrderBy(value =>
                    value.State.BattleStableId, StringComparer.Ordinal))
                {
                    Add(canonical, battle.State.BattleStableId);
                    Add(canonical, battle.IntegrityHashSha256);
                }
            }
            Add(canonical, package.CommandLog.Length);
            foreach (var entry in package.CommandLog.OrderBy(value => value.Sequence))
            {
                Add(canonical, entry.Sequence);
                Add(canonical, entry.CommandTypeCode);
                Add(canonical, entry.AppliedWorldTick);
                Add(canonical, entry.ResultingWorldRevision);
                if (isV15OrLater)
                    AddWorldInteractionInvocation(canonical,
                        entry.WorldInteractionInvocation);
                if (entry.TickRequest != null)
                {
                    Add(canonical, entry.TickRequest.CommandId);
                    Add(canonical, entry.TickRequest.ExpectedRevision);
                    Add(canonical, entry.TickRequest.TickCount);
                }
                if (entry.IntegratedWorldConfirmRequest != null)
                    AddIntegratedWorldCommand(canonical,
                        entry.IntegratedWorldConfirmRequest);
                if (entry.FacilityDamageQueueRequest != null)
                {
                    Add(canonical, entry.FacilityDamageQueueRequest.BattleStableId);
                    Add(canonical, entry.FacilityDamageQueueRequest.FacilityStableId);
                    Add(canonical, entry.FacilityDamageQueueRequest.SeverityCode);
                }
                if (entry.NatureSurvivalActionRequest != null)
                {
                    var request = entry.NatureSurvivalActionRequest;
                    Add(canonical, request.CommandId);
                    Add(canonical, request.ExpectedRevision);
                    Add(canonical, request.PlayerStableId);
                    Add(canonical, request.ActionCode);
                    Add(canonical, request.TargetStableId);
                    Add(canonical, request.ChoiceCode);
                    Add(canonical, request.LocalX);
                    Add(canonical, request.LocalZ);
                    Add(canonical, request.YawDegrees);
                    if (isV18)
                        Add(canonical,
                            request.AuthoritativeRewardBonusQuantity);
                }
                if (entry.NatureSurvivalClockAdvanceRequest != null)
                {
                    var request = entry.NatureSurvivalClockAdvanceRequest;
                    Add(canonical, request.CommandId);
                    Add(canonical, request.ExpectedRevision);
                    Add(canonical, request.ElapsedRealtimeSeconds);
                    Add(canonical, request.WorkInputHeld);
                    Add(canonical, request.PauseReasonCode);
                }
                if (entry.ActorItemAcquireConfirmRequest != null)
                {
                    var request = entry.ActorItemAcquireConfirmRequest;
                    Add(canonical, request.CommandId);
                    Add(canonical, request.ExpectedEquipmentRevision);
                    Add(canonical, request.ActorStableId);
                    Add(canonical, request.ItemInstanceStableId);
                    Add(canonical, request.SpecializationWorldInteractionId);
                }
                if (entry.ActorEquipmentChangeConfirmRequest != null)
                {
                    var request = entry.ActorEquipmentChangeConfirmRequest;
                    Add(canonical, request.CommandId);
                    Add(canonical, request.ExpectedEquipmentRevision);
                    Add(canonical, request.ActorStableId);
                    Add(canonical, request.OperationCode);
                    Add(canonical, request.ItemInstanceStableId);
                    Add(canonical, request.SlotCode);
                    Add(canonical, request.SwapItemInstanceStableId);
                    Add(canonical, request.SpecializationWorldInteractionId);
                }
                if (entry.HexagramCampaignState != null)
                {
                    var state = entry.HexagramCampaignState;
                    Add(canonical, state.RuleRevision);
                    Add(canonical, state.CampaignStateCode);
                    Add(canonical, state.HexagramStableId);
                    Add(canonical, state.CurrentLineOrdinal);
                    if (state.StoryStageCount != 6)
                    {
                        Add(canonical, "StoryStageCount");
                        Add(canonical, state.StoryStageCount);
                    }
                    Add(canonical, state.AttemptOrdinal);
                    Add(canonical, state.AttemptVariationSeed);
                    Add(canonical, state.EntrySaveStableId);
                    Add(canonical, state.EntryWorldRevision);
                    Add(canonical, string.Join(",",
                        state.TemporaryWorldInteractionIds));
                    Add(canonical, string.Join(",",
                        state.PermanentlyUnlockedWorldInteractionIds));
                    Add(canonical, string.Join(",", state.Events.Select(value =>
                        string.Join("~", value.EventCode, value.ReasonCode,
                            value.AttemptOrdinal, value.LineOrdinal,
                            value.WorldTick, value.WorldRevision))));
                }
                if (entry.SeasonalOperationsCampaignState != null)
                {
                    Add(canonical,
                        entry.SeasonalOperationsCampaignCommandId);
                    Add(canonical,
                        entry.SeasonalOperationsCampaignCommandSignature);
                    Add(canonical, 경영SimulationSessionAggregate
                        .BuildSeasonalOperationsCampaignStatePayloadKey(
                            entry.SeasonalOperationsCampaignState));
                }
                if (entry.DecisionConfirmRequest != null)
                {
                    Add(canonical, entry.DecisionConfirmRequest.CommandId);
                    Add(canonical, entry.DecisionConfirmRequest.ExpectedRevision);
                    Add(canonical, 경영SimulationSessionAggregate.BuildDecisionPayloadKey(
                        entry.DecisionConfirmRequest.Preview));
                }
                if (entry.RegionalIncidentResponseConfirmRequest != null)
                {
                    var request = entry.RegionalIncidentResponseConfirmRequest;
                    Add(canonical, entry.WorldEventStableId ?? string.Empty);
                    Add(canonical, request.CommandId);
                    Add(canonical, request.ExpectedRevision);
                    Add(canonical, request.ActorStableId);
                    Add(canonical, request.ChoiceStableId);
                }
                if (entry.NatureEncounterVictoryRequest != null)
                {
                    Add(canonical, entry.NatureEncounterVictoryRequest.BattleStableId);
                    Add(canonical, entry.NatureEncounterVictoryRequest.EncounterStableId);
                }
                if (entry.HarvestDispositionImpactConfirmRequest != null)
                {
                    Add(canonical, entry.HarvestDispositionImpactConfirmRequest.CommandId);
                    Add(canonical, entry.HarvestDispositionImpactConfirmRequest.ExpectedRevision);
                    Add(canonical, 경영SimulationSessionAggregate.BuildHarvestDispositionImpactPayloadKey(
                        entry.HarvestDispositionImpactConfirmRequest.Impact));
                }
                if (entry.LogisticsMovementConfirmRequest != null)
                {
                    Add(canonical, entry.LogisticsMovementConfirmRequest.CommandId);
                    Add(canonical, entry.LogisticsMovementConfirmRequest.ExpectedRevision);
                    Add(canonical, 경영SimulationSessionAggregate.BuildLogisticsMovementPayloadKey(
                        entry.LogisticsMovementConfirmRequest.Movement));
                }
                if (entry.TurnClosingConfirmRequest != null)
                {
                    Add(canonical, entry.TurnClosingConfirmRequest.CommandId);
                    Add(canonical, entry.TurnClosingConfirmRequest.ExpectedRevision);
                    Add(canonical, isV16
                        ? 경영SimulationSessionAggregate.BuildTurnClosingPayloadKey(
                            entry.TurnClosingConfirmRequest.Preview)
                        : 경영SimulationSessionAggregate.BuildLegacyTurnClosingPayloadKey(
                            entry.TurnClosingConfirmRequest.Preview));
                }
                if (entry.NpcPolicyChangeRequest != null)
                {
                    Add(canonical, entry.NpcPolicyChangeRequest.CommandId);
                    Add(canonical, entry.NpcPolicyChangeRequest.ExpectedRevision);
                    Add(canonical, 경영SimulationSessionAggregate.BuildNpcPolicyPayloadKey(
                        entry.NpcPolicyChangeRequest));
                }
                if (entry.WorldItemAcquisitionConfirmRequest != null)
                {
                    var request = entry.WorldItemAcquisitionConfirmRequest;
                    Add(canonical, request.CommandId);
                    Add(canonical, request.ExpectedRevision);
                    Add(canonical, request.PlayerStableId);
                    Add(canonical, request.BuildingStableId);
                    Add(canonical, request.ContainerStableId);
                    Add(canonical, request.ItemStackStableId);
                    Add(canonical, request.Quantity);
                }
                if (entry.SurvivalTarotResponseConfirmRequest != null)
                {
                    var request = entry.SurvivalTarotResponseConfirmRequest;
                    Add(canonical, request.CommandId);
                    Add(canonical, request.ExpectedRevision);
                    Add(canonical, 경영SimulationSessionAggregate
                        .BuildSurvivalTarotResponsePayloadKey(request));
                }
                if (entry.SurvivalTarotResolutionConfirmRequest != null)
                {
                    var request = entry.SurvivalTarotResolutionConfirmRequest;
                    Add(canonical, request.CommandId);
                    Add(canonical, request.ExpectedRevision);
                    Add(canonical, 경영SimulationSessionAggregate
                        .BuildSurvivalTarotResolutionPayloadKey(request));
                }
                if (entry.FarmWorkConfirmRequest != null)
                {
                    var request = entry.FarmWorkConfirmRequest;
                    Add(canonical, request.CommandId);
                    Add(canonical, request.ExpectedRevision);
                    Add(canonical, 경영SimulationSessionAggregate
                        .BuildFarmWorkPayloadKey(request));
                }
                if (entry.FarmWorkPlanConfirmRequest != null)
                {
                    var request = entry.FarmWorkPlanConfirmRequest;
                    Add(canonical, request.CommandId);
                    Add(canonical, request.ExpectedRevision);
                    Add(canonical, 경영SimulationSessionAggregate
                        .BuildFarmWorkPlanPayloadKey(request));
                }
                if (entry.ThreatResponseConfirmRequest != null)
                {
                    var request = entry.ThreatResponseConfirmRequest;
                    Add(canonical, request.CommandId);
                    Add(canonical, request.ExpectedRevision);
                    Add(canonical, 경영SimulationSessionAggregate
                        .BuildThreatResponsePayloadKey(request));
                }
                if (entry.CombatPerspectiveConfirmRequest != null)
                {
                    var request = entry.CombatPerspectiveConfirmRequest;
                    Add(canonical, request.CommandId);
                    Add(canonical, request.ExpectedRevision);
                    Add(canonical, 경영SimulationSessionAggregate
                        .BuildCombatPerspectivePayloadKey(request));
                }
                if (entry.CombatBeatStartRequest != null)
                {
                    var request = entry.CombatBeatStartRequest;
                    Add(canonical, request.CommandId);
                    Add(canonical, request.ExpectedRevision);
                    Add(canonical, 경영SimulationSessionAggregate
                        .BuildCombatBeatPayloadKey(request));
                }
                if (entry.CombatReactionConfirmRequest != null)
                {
                    var request = entry.CombatReactionConfirmRequest;
                    Add(canonical, request.CommandId);
                    Add(canonical, request.ExpectedRevision);
                    Add(canonical, 경영SimulationSessionAggregate
                        .BuildCombatReactionPayloadKey(request));
                }
                if (entry.TacticalOrderConfirmRequest != null)
                {
                    var request = entry.TacticalOrderConfirmRequest;
                    Add(canonical, request.CommandId);
                    Add(canonical, request.ExpectedRevision);
                    Add(canonical, 경영SimulationSessionAggregate
                        .BuildTacticalOrderPayloadKey(request));
                }
                if (entry.TeamRoleCardEquipRequest != null)
                {
                    var request = entry.TeamRoleCardEquipRequest;
                    Add(canonical, request.ClientRequestId.ToString("N"));
                    Add(canonical, request.ExpectedRevision);
                    Add(canonical, request.ExpectedTeamPolicyRevision);
                    Add(canonical, request.RequestingActorStableId);
                    Add(canonical, request.TargetActorStableId);
                    Add(canonical, request.CardCopyStableId);
                    Add(canonical, request.SlotCode);
                }
                if (entry.CombatCardLoadoutSetRequest != null)
                {
                    var request = entry.CombatCardLoadoutSetRequest;
                    Add(canonical, request.ClientRequestId.ToString("N"));
                    Add(canonical, request.ExpectedRevision);
                    Add(canonical, request.ExpectedTeamPolicyRevision);
                    Add(canonical, request.RequestingActorStableId);
                    Add(canonical, request.TargetActorStableId);
                    Add(canonical, request.CombatControlModeCode);
                    foreach (var slot in request.Slots.OrderBy(value =>
                                 value.SlotCode, StringComparer.Ordinal))
                    {
                        Add(canonical, slot.SlotCode);
                        Add(canonical, slot.CardCopyStableId);
                    }
                }
                if (entry.TeamActivityStartRequest != null)
                {
                    var request = entry.TeamActivityStartRequest;
                    Add(canonical, request.ClientRequestId.ToString("N"));
                    Add(canonical, request.ExpectedRevision);
                    Add(canonical, request.ExpectedTeamPolicyRevision);
                    Add(canonical, request.ActorStableId);
                    Add(canonical, request.CardCopyStableId);
                    Add(canonical, request.ActivityRoleCode);
                    Add(canonical, request.ActivityStableId);
                    Add(canonical, request.LocationStableId);
                }
                if (entry.TeamActivityEndRequest != null)
                {
                    var request = entry.TeamActivityEndRequest;
                    Add(canonical, request.ClientRequestId.ToString("N"));
                    Add(canonical, request.ExpectedRevision);
                    Add(canonical, request.ActorStableId);
                    Add(canonical, request.ActivityStableId);
                }
                if (entry.TileTraversalConfirmRequest != null)
                {
                    var request = entry.TileTraversalConfirmRequest;
                    Add(canonical, request.CommandId);
                    Add(canonical, 경영SimulationSessionAggregate
                        .BuildTileTraversalPayloadKey(request));
                }
                if (entry.CollectibleCardDrawRequest != null)
                {
                    var request = entry.CollectibleCardDrawRequest;
                    Add(canonical, request.CommandId);
                    Add(canonical, 경영SimulationSessionAggregate
                        .BuildCollectibleCardDrawPayloadKey(request));
                }
                if (entry.CollectibleCardTransferRequest != null)
                {
                    var request = entry.CollectibleCardTransferRequest;
                    Add(canonical, request.CommandId);
                    Add(canonical, 경영SimulationSessionAggregate
                        .BuildCollectibleCardTransferPayloadKey(request));
                }
                if (entry.TaskCancelRequest != null)
                {
                    Add(canonical, entry.TaskStableId ?? string.Empty);
                    Add(canonical, 경영SimulationSessionAggregate.BuildTaskCancelPayloadKey(
                        entry.TaskStableId ?? string.Empty,
                        entry.TaskCancelRequest));
                }
            }
            if (isV15OrLater)
            {
                Add(canonical, package.WorldInteractionManifestations.Length);
                foreach (var manifestation in package.WorldInteractionManifestations
                             .OrderBy(value => value.OriginCommandId,
                                 StringComparer.Ordinal))
                    AddWorldInteractionManifestation(canonical, manifestation);
            }
            if (isV23)
                AddWI음양주체분류(canonical, package);
            if (isV22)
            {
                var hasComposition = package.SpatialComposition != null
                    && package.SpatialCompositionHandle != null;
                if (hasComposition)
                {
                    var composition = package.SpatialComposition!;
                    var handle = package.SpatialCompositionHandle!;
                    Add(canonical,
                        package.SessionCreateRequest.SpatialCompositionRuleRevision);
                    Add(canonical, composition.SchemaVersion);
                    Add(canonical, composition.AreaCode);
                    Add(canonical, composition.AreaSetStableId);
                    Add(canonical, composition.PlacementControlRevision);
                    Add(canonical, composition.RuleCatalogRevision);
                    Add(canonical, composition.RuleCatalogHashSha256);
                    Add(canonical, composition.WorldTick);
                    Add(canonical, composition.WorldRevision);
                    Add(canonical, composition.GraphHashSha256);
                    Add(canonical, handle.SchemaVersion);
                    Add(canonical, handle.AreaCode);
                    Add(canonical, handle.AreaSetStableId);
                    Add(canonical, handle.RuleCatalogRevision);
                    Add(canonical, handle.RuleCatalogHashSha256);
                    Add(canonical, handle.SourceWorldRevision);
                    Add(canonical, handle.GraphHashSha256);
                }
                else
                {
                    Add(canonical, "InteriorPlanHandlesOnlyV1");
                }
            }
            using (var sha = SHA256.Create())
            {
                // 선택 필드 없는 기존 저장 해시는 보존한다.
                var local = package.SessionCreateRequest.LocalLife;
                if(local != null) {
                    Add(canonical,"OfflineLifeR1");Add(canonical,local.Revision);Add(canonical,local.SourceBundleSha256);
                    foreach(var a in local.Actors.OrderBy(x=>x.SlotKey,StringComparer.Ordinal)) {
                        Add(canonical,a.SlotKey);Add(canonical,a.ActorStableId);Add(canonical,a.DisplayName);Add(canonical,a.Role);Add(canonical,a.VisualKey); }
                    foreach(var f in local.Facilities.OrderBy(x=>x.SlotKey,StringComparer.Ordinal)) {
                        Add(canonical,f.SlotKey);Add(canonical,f.FacilityStableId);Add(canonical,f.DisplayName); }
                }
                var bytes = Encoding.UTF8.GetBytes(canonical.ToString());
                return BitConverter.ToString(sha.ComputeHash(bytes))
                    .Replace("-", string.Empty)
                    .ToLowerInvariant();
            }
        }

        private static void AddCreateRequest(
            StringBuilder target,
            경영SimulationSession생성Request request,
            bool includesRealityContext,
            bool includesV16,
            bool includesV21,
            bool includesV22)
        {
            Add(target, request.ClientRequestId.ToString("N"));
            Add(target, request.ScenarioStableId);
            Add(target, request.ScenarioDataRevision);
            Add(target, request.ScenarioSeed);
            Add(target, request.RuleRevision);
            if (includesV21)
                Add(target, request.NpcRoutineControlRevision);
            if (includesV22 && (request.InteriorPlanHandles?.Length ?? 0) > 0)
                AddInteriorPlanHandles(target, request.InteriorPlanHandles);
            if (includesRealityContext)
                Add(target, request.RealityContextProfileStableId);
            if (includesV16)
            {
                Add(target, request.TarotOrientationPolicyCode);
                Add(target, request.TownNpcLifeProfileStableId);
            }
            Add(target, request.DurationTicks);
            Add(target, request.WorldContext.FactionStableId);
            Add(target, request.WorldContext.TerritoryStableId);
            Add(target, request.WorldContext.SettlementStableId);
            Add(target, request.WorldContext.GameDateStartsOn.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
            Add(target, 경영SimulationSessionAggregate.BuildSettlementPayloadKey(request.Settlement));
            if (request.NpcWorkforce != null)
                Add(target, 경영SimulationSessionAggregate.BuildNpcWorkforcePayloadKey(
                    request.NpcWorkforce, includesV21));
            if (request.SpatialWorld != null)
                Add(target, 경영SimulationSessionAggregate.BuildSimulationSpatialPayloadKey(
                    request.SpatialWorld));
            if (request.WorldInventory != null)
                Add(target, 경영SimulationSessionAggregate.BuildWorldInventoryPayloadKey(
                    request.WorldInventory));
            if (request.SurvivalTarot != null)
                Add(target, 경영SimulationSessionAggregate.BuildSurvivalTarotPayloadKey(
                    request.SurvivalTarot));
            if (request.FarmSurvival != null)
                Add(target, 경영SimulationSessionAggregate.BuildFarmSurvivalPayloadKey(
                    request.FarmSurvival));
            if (request.TeamRoleCards != null)
                Add(target, 경영SimulationSessionAggregate.BuildTeamRoleCardPayloadKey(
                    request.TeamRoleCards));
            if (request.NatureMind != null)
                Add(target, 경영SimulationSessionAggregate.BuildNatureMindInitialPayloadKey(
                    request.NatureMind));
        }

        private static void AddInteriorPlanHandles(
            StringBuilder target,
            SimulationInteriorPlanHandleSnapshot[]? handles)
        {
            var ordered = (handles
                    ?? Array.Empty<SimulationInteriorPlanHandleSnapshot>())
                .OrderBy(value => value.BuildingPlacementStableId,
                    StringComparer.Ordinal)
                .ToArray();
            Add(target, ordered.Length);
            foreach (var handle in ordered)
            {
                Add(target, handle.SchemaVersion);
                Add(target, handle.BuildingPlacementStableId);
                Add(target, handle.H1StableId);
                Add(target, handle.InteriorDefinitionRevision);
                Add(target, handle.ReferenceCatalogRevision);
                Add(target, handle.ReferenceCatalogHashSha256);
                Add(target, handle.PlacementControlRuleRevision);
                Add(target, handle.VisualMetricCatalogRevision);
                Add(target, handle.VisualMetricCatalogHashSha256);
                Add(target, handle.AdjustmentRevision);
                Add(target, handle.InteriorPlacementPlanHashSha256);
            }
        }

        private static void AddWorldInteractionInvocation(StringBuilder target,
            SimulationWorldInteractionInvocationRecord? value)
        {
            Add(target, value != null);
            if (value == null) return;
            Add(target, value.PayloadCode);
            Add(target, value.WorldInteractionId);
            Add(target, value.TriggerSourceCode);
            Add(target, value.CommandId);
            Add(target, value.InitiatorStableId);
            Add(target, value.ActorStableId);
            Add(target, value.TargetStableId);
            AddStrings(target, value.SourceReferenceIds);
            Add(target, value.TimeReferenceId);
            Add(target, value.SpatialEvidenceStateCode);
            AddStrings(target, value.SpatialEvidenceReferenceIds);
        }

        private static void AddWI음양주체분류(StringBuilder target,
            SimulationSessionSavePackage package)
        {
            Add(target, "WorldInteractionPolarityQuadrantsV1");
            var invocations = package.CommandLog
                .Where(value => value.WorldInteractionInvocation != null)
                .OrderBy(value => value.Sequence).ToArray();
            Add(target, invocations.Length);
            foreach (var entry in invocations)
            {
                Add(target, entry.Sequence);
                Add(target, entry.WorldInteractionInvocation!.WorldInteractionId);
                AddWI음양주체분류(target,
                    entry.WorldInteractionInvocation.음양주체분류);
            }

            var executions = (package.Snapshot.NpcRoutineExecutions
                    ?? Array.Empty<SimulationNpcRoutineExecutionSnapshot>())
                .OrderBy(value => value.ExecutionStableId,
                    StringComparer.Ordinal).ToArray();
            Add(target, executions.Length);
            foreach (var execution in executions)
            {
                Add(target, execution.ExecutionStableId);
                Add(target, execution.ActorStableId);
                AddWI음양주체분류(target, execution.음양주체분류);
            }
        }

        private static void AddWI음양주체분류(StringBuilder target,
            SimulationWI음양주체분류Snapshot? value)
        {
            value ??= new SimulationWI음양주체분류Snapshot();
            Add(target, value.PayloadCode);
            Add(target, value.음양Code);
            Add(target, value.수행주체Code);
            Add(target, value.사분면Code);
            Add(target, value.사분면기호);
            Add(target, value.판정방식Code);
            Add(target, value.판정RuleRevision);
            Add(target, value.판정근거StableId);
        }

        private static void AddWorldInteractionManifestation(StringBuilder target,
            SimulationWorldInteractionManifestationRecord value)
        {
            Add(target, value.PayloadCode);
            Add(target, value.WorldInteractionId);
            Add(target, value.OriginCommandId);
            Add(target, value.BeforeWorldRevision);
            Add(target, value.AfterWorldRevision);
            Add(target, value.StateCode);
            AddStrings(target, value.TaskOrEffectReferenceIds);
            AddStrings(target, value.ResultStateCodes);
            AddStrings(target, value.SuccessorOrReturnCodes);
            Add(target, value.SpatialEvidenceStateCode);
            AddStrings(target, value.SpatialEvidenceReferenceIds);
            AddStrings(target, value.MissingEvidenceCodes);
        }

        private static void AddNatureMind(StringBuilder target,
            SimulationNatureMindStateSnapshot value)
        {
            Add(target, "NatureMindExtensionV1");
            Add(target, value.RuleRevision);
            Add(target, value.Balances.Length);
            foreach (var balance in value.Balances.OrderBy(item =>
                         item.PlayerStableId, StringComparer.Ordinal))
            {
                Add(target, balance.PlayerStableId);
                Add(target, balance.RecoveryOutput);
                Add(target, balance.ThreatOutput);
                Add(target, balance.RecoveryShare);
                Add(target, balance.ThreatShare);
                Add(target, balance.InterpretationStrength);
                Add(target, balance.InterpretationBandCode);
                Add(target, balance.Revision);
                Add(target, balance.BalanceHashSha256);
            }
            Add(target, value.Effects.Length);
            foreach (var effect in value.Effects.OrderBy(item =>
                         item.EffectStableId, StringComparer.Ordinal))
            {
                Add(target, effect.EffectStableId);
                Add(target, effect.PlayerStableId);
                Add(target, effect.SourceCode);
                Add(target, effect.SourceStableId);
                Add(target, effect.AxisCode);
                Add(target, effect.Magnitude);
                Add(target, effect.AppliedWorldTick);
                Add(target, effect.RuleRevision);
            }
            Add(target, value.Periods.Length);
            foreach (var period in value.Periods.OrderBy(item =>
                         item.PlayerStableId, StringComparer.Ordinal))
            {
                Add(target, period.PlayerStableId);
                Add(target, period.PeriodStateCode);
                Add(target, period.PeriodInstanceStableId);
                Add(target, period.SourceBalanceRevision);
                Add(target, period.SourceBalanceHashSha256);
                Add(target, period.EnteredAtWorldTick);
                Add(target, period.EnterReasonCode);
                Add(target, period.ExitThresholdPolicyRevision);
                Add(target, period.Revision);
                Add(target, period.PeriodStateHashSha256);
                Add(target, period.BaseRecoveryWorkDurationTicks);
                Add(target, period.EffectiveRecoveryWorkDurationTicks);
                Add(target, period.WorkDurationModifierTicks);
                AddStrings(target, period.CandidateStableIds);
            }
            Add(target, value.PeriodHistory.Length);
            foreach (var history in value.PeriodHistory.OrderBy(item =>
                         item.PeriodInstanceStableId, StringComparer.Ordinal))
            {
                Add(target, history.PlayerStableId);
                Add(target, history.PeriodInstanceStableId);
                Add(target, history.StateCode);
                Add(target, history.EnterTick);
                Add(target, history.ExitTick?.ToString(
                    CultureInfo.InvariantCulture) ?? string.Empty);
                AddStrings(target, history.MajorOutcomeRefs);
            }
            Add(target, value.PeriodTransitionEffects.Length);
            foreach (var effect in value.PeriodTransitionEffects.OrderBy(item =>
                         item.EffectStableId, StringComparer.Ordinal))
            {
                Add(target, effect.EffectStableId);
                Add(target, effect.EffectTypeCode);
                Add(target, effect.PlayerStableId);
                Add(target, effect.PeriodInstanceStableId);
                Add(target, effect.StateCode);
                Add(target, effect.AppliedWorldTick);
                Add(target, effect.SourceBalanceHashSha256);
            }
        }

        private static void AddAreaAccess(StringBuilder target,
            SimulationPlayerAreaAccessStateSnapshot value)
        {
            Add(target, "PlayerAreaAccessExtensionV1");
            Add(target, value.RuleRevision);
            Add(target, value.WorldRevision);
            Add(target, value.WorldTick);
            Add(target, value.CurrentAreaSetStableId);
            Add(target, value.MutatesStaticHDefinitions);
            Add(target, value.AccessEntries.Length);
            foreach (var entry in value.AccessEntries.OrderBy(item =>
                         item.AreaSetStableId, StringComparer.Ordinal))
            {
                Add(target, entry.PlayerStableId);
                Add(target, entry.AreaSetStableId);
                Add(target, entry.AccessLevelCode);
                Add(target, entry.AccessStateCode);
                AddStrings(target, entry.GrantedByEvidenceIds);
                Add(target, entry.GrantedAtWorldRevision);
                Add(target, entry.RevocationPolicyCode);
                Add(target, entry.SourceHDefinitionHashSha256);
                AddStrings(target, entry.AvailableWorldInteractionIds);
                Add(target, entry.Revision);
                Add(target, entry.AccessHashSha256);
            }
        }

        private static void AddHostedWorld(StringBuilder target,
            SimulationHostedWorldStateSnapshot value)
        {
            Add(target, "HostedWorldExtensionV1");
            Add(target, value.WorldRevision);
            Add(target, value.WorldTick);
            Add(target, value.HostedSessionStableId);
            Add(target, value.WorldStableId);
            Add(target, value.OwnerPlayerStableId);
            Add(target, value.SessionModeCode);
            Add(target, value.JoinPolicyCode);
            Add(target, value.DefaultGuestPermissionProfileCode);
            Add(target, value.Participants.Length);
            foreach (var participant in value.Participants.OrderBy(item =>
                         item.PlayerStableId, StringComparer.Ordinal))
            {
                Add(target, participant.PlayerStableId);
                Add(target, participant.ParticipantStateCode);
                Add(target, participant.CurrentAreaSetStableId);
                Add(target, participant.JoinedAtWorldRevision);
            }
            Add(target, value.PermissionGrants.Length);
            foreach (var grant in value.PermissionGrants.OrderBy(item =>
                         item.TargetPlayerStableId, StringComparer.Ordinal).ThenBy(item =>
                         item.ScopeStableId, StringComparer.Ordinal).ThenBy(item =>
                         item.CapabilityCode, StringComparer.Ordinal))
            {
                Add(target, grant.TargetPlayerStableId);
                Add(target, grant.ScopeStableId);
                Add(target, grant.CapabilityCode);
                Add(target, grant.GrantStateCode);
                Add(target, grant.ActionRiskPolicyCode);
                Add(target, grant.GrantedByPlayerStableId);
                Add(target, grant.Revision);
                Add(target, grant.GrantHashSha256);
            }
            Add(target, value.AuditTrail.Length);
            foreach (var audit in value.AuditTrail.OrderBy(item =>
                         item.EffectStableId, StringComparer.Ordinal))
            {
                Add(target, audit.EffectStableId);
                Add(target, audit.EffectTypeCode);
                Add(target, audit.ChangedByPlayerStableId);
                Add(target, audit.TargetPlayerStableId);
                Add(target, audit.ScopeStableId);
                Add(target, audit.CapabilityCode);
                Add(target, audit.AppliedWorldTick);
            }
            Add(target, value.PermissionRevision);
            Add(target, value.CreatedAtWorldRevision);
            Add(target, value.HostLossBlocksMutation);
            Add(target, value.EscPausesWorld);
            Add(target, value.SessionHashSha256);
            Add(target, value.SimulationOnly);
            Add(target, value.IsOperationalState);
        }

        private static void AddCoopConstruction(StringBuilder target,
            SimulationCoopConstructionStateSnapshot value)
        {
            Add(target, "CoopConstructionExtensionV1");
            Add(target, value.RuleRevision);
            Add(target, value.ProtectionRuleRevision);
            Add(target, value.WorldRevision);
            Add(target, value.WorldTick);
            Add(target, value.Projects.Length);
            foreach (var project in value.Projects.OrderBy(item =>
                         item.ProjectStableId, StringComparer.Ordinal))
            {
                Add(target, project.ProjectStableId);
                Add(target, project.BlueprintStableId);
                Add(target, project.BuildSiteH1StableId);
                Add(target, project.TargetFacilityStableId);
                Add(target, project.StageCode);
                Add(target, project.RequiredMaterialQuantity);
                Add(target, project.ContributedMaterialQuantity);
                Add(target, project.ProgressValue);
                Add(target, project.UnitCode);
                Add(target, project.Revision);
                Add(target, project.CompletedWorldTick ?? -1);
                AddStrings(target, project.OpenedCapabilityCodes);
                AddStrings(target, project.OpenedWorldInteractionIds);
                Add(target, project.ProjectHashSha256);
            }
            Add(target, value.Contributions.Length);
            foreach (var contribution in value.Contributions.OrderBy(item =>
                         item.ContributionStableId, StringComparer.Ordinal))
            {
                Add(target, contribution.ContributionStableId);
                Add(target, contribution.ProjectStableId);
                Add(target, contribution.PlayerStableId);
                Add(target, contribution.SourceLotStableId);
                Add(target, contribution.SourceLotRevisionBefore);
                Add(target, contribution.MaterialQuantity);
                Add(target, contribution.EffectiveWork);
                Add(target, contribution.UnitCode);
                Add(target, contribution.StateCode);
                Add(target, contribution.AppliedWorldTick);
            }
            Add(target, value.SourceLots.Length);
            foreach (var lot in value.SourceLots.OrderBy(item => item.LotStableId,
                         StringComparer.Ordinal))
            {
                Add(target, lot.LotStableId);
                Add(target, lot.Revision);
                Add(target, lot.ReservedQuantity);
                Add(target, lot.RemainingQuantity);
                Add(target, lot.UnitCode);
            }
            Add(target, value.ProtectionCheckpoints.Length);
            foreach (var checkpoint in value.ProtectionCheckpoints.OrderBy(item =>
                         item.CheckpointStableId, StringComparer.Ordinal))
            {
                Add(target, checkpoint.CheckpointStableId);
                Add(target, checkpoint.CheckpointKindCode);
                Add(target, checkpoint.WorldStableId);
                AddStrings(target, checkpoint.TargetStableIds);
                Add(target, checkpoint.BeforeWorldRevision);
                Add(target, checkpoint.SpatialStateHashSha256);
                AddStrings(target, checkpoint.RelatedResourceRefs);
                AddStrings(target, checkpoint.RelatedConnectorRefs);
                Add(target, checkpoint.CreatedByActionRequestId);
                Add(target, checkpoint.HistoricalEffectsDeleted);
            }
            Add(target, value.RestoreEffects.Length);
            foreach (var effect in value.RestoreEffects.OrderBy(item =>
                         item.EffectStableId, StringComparer.Ordinal))
            {
                Add(target, effect.EffectStableId);
                Add(target, effect.CheckpointStableId);
                Add(target, effect.TargetStableId);
                Add(target, effect.EffectTypeCode);
                Add(target, effect.AppliedWorldTick);
                Add(target, effect.DeletesHistoricalEffects);
                Add(target, effect.DuplicatesResources);
            }
            Add(target, value.UsesCompensatingEffects);
            Add(target, value.MutatesStaticHDefinitions);
            Add(target, value.StateHashSha256);
            Add(target, value.SimulationOnly);
            Add(target, value.IsOperationalState);
        }

        private static void AddRealityContext(StringBuilder target,
            SimulationRealityContextSnapshot value)
        {
            Add(target, "RealityContextExtensionV1");
            Add(target, value.SchemaVersion);
            Add(target, value.ContextSnapshotStableId);
            Add(target, value.ProfileStableId);
            Add(target, value.ProfileRevision);
            Add(target, value.SignalRuleRevision);
            Add(target, value.AreaSetStableId);
            Add(target, value.FrozenAtUtc.ToUniversalTime().ToString("O",
                CultureInfo.InvariantCulture));
            Add(target, value.AvailabilityCode);
            Add(target, value.InputHashSha256);
            Add(target, value.ChangesSimulationRules);
            Add(target, value.MovesSpatialDefinitions);
            Add(target, value.CreatesIncidentOrEffect);
            Add(target, value.SourceEvidence.Length);
            foreach (var source in value.SourceEvidence.OrderBy(item =>
                         item.SourceEvidenceStableId, StringComparer.Ordinal))
            {
                Add(target, source.SourceEvidenceStableId);
                Add(target, source.SourceName);
                Add(target, source.DatasetCode);
                Add(target, source.AvailabilityCode);
                Add(target, source.QualityCode);
                Add(target, source.FreshnessCode);
                Add(target, source.ObservedAtUtc?.ToUniversalTime().ToString("O",
                    CultureInfo.InvariantCulture) ?? string.Empty);
                Add(target, source.RetrievedAtUtc?.ToUniversalTime().ToString("O",
                    CultureInfo.InvariantCulture) ?? string.Empty);
                Add(target, source.SpatialPrecisionCode);
                AddStrings(target, source.UnitCodes);
                Add(target, source.SourceHashSha256);
                Add(target, source.LicenseCode);
                Add(target, source.SourceHref);
                AddStrings(target, source.LimitationCodes);
            }
            Add(target, value.SemanticSignals.Length);
            foreach (var signal in value.SemanticSignals.OrderBy(item =>
                         item.SignalStableId, StringComparer.Ordinal))
            {
                Add(target, signal.SignalStableId);
                Add(target, signal.SignalCode);
                Add(target, signal.SignalRuleRevision);
                AddStrings(target, signal.H3StableIds);
                AddStrings(target, signal.AdvisoryCodes);
                AddStrings(target, signal.SourceEvidenceStableIds);
            }
        }

        private static void AddWorldInventory(
            StringBuilder target,
            SimulationWorldInventorySnapshot value)
        {
            Add(target, "WorldInventoryExtensionV1");
            Add(target, value.SessionStableId);
            Add(target, value.WorldRevision);
            Add(target, value.WorldTick);
            Add(target, value.RuleRevision);
            Add(target, value.Buildings.Length);
            foreach (var item in value.Buildings)
            {
                Add(target, item.BuildingStableId);
                Add(target, item.TileKey);
                Add(target, item.RegionStableId);
                Add(target, item.BuildingEvidenceKindCode);
                Add(target, item.SourceRecordStableId);
                Add(target, item.InteriorSpaceStableId);
                Add(target, item.InteriorEvidenceKindCode);
            }
            Add(target, value.Containers.Length);
            foreach (var item in value.Containers)
            {
                Add(target, item.ContainerStableId);
                Add(target, item.BuildingStableId);
                Add(target, item.InteriorSpaceStableId);
                Add(target, item.AccessPolicyCode);
                Add(target, item.CapacityUnits);
                AddStrings(target, item.ManagerPlayerStableIds);
                Add(target, item.EvidenceKindCode);
            }
            Add(target, value.ContainerItemStacks.Length);
            foreach (var item in value.ContainerItemStacks)
            {
                Add(target, item.ItemStackStableId);
                Add(target, item.ContainerStableId);
                Add(target, item.ItemCode);
                Add(target, item.KoreanName);
                Add(target, item.Quantity);
                Add(target, item.UnitCode);
                Add(target, item.BuildingItemRelationStableId);
                Add(target, item.EvidenceKindCode);
            }
            Add(target, value.Players.Length);
            foreach (var player in value.Players)
            {
                Add(target, player.PlayerStableId);
                Add(target, player.CurrentBuildingStableId);
                Add(target, player.InventoryCapacityUnits);
                AddStrings(target, player.ManagedContainerStableIds);
                Add(target, player.Items.Length);
                foreach (var item in player.Items)
                {
                    Add(target, item.ItemCode);
                    Add(target, item.KoreanName);
                    Add(target, item.Quantity);
                    Add(target, item.UnitCode);
                }
            }
            Add(target, value.Transfers.Length);
            foreach (var item in value.Transfers)
            {
                Add(target, item.TransferStableId);
                Add(target, item.CommandId);
                Add(target, item.PlayerStableId);
                Add(target, item.BuildingStableId);
                Add(target, item.SourceContainerStableId);
                Add(target, item.SourceItemStackStableId);
                Add(target, item.ItemCode);
                Add(target, item.Quantity);
                Add(target, item.UnitCode);
                Add(target, item.AppliedWorldTick);
                Add(target, item.AppliedWorldRevision);
                Add(target, item.EvidenceKindCode);
                Add(target, item.SimulationOnly);
            }
            Add(target, value.SimulationOnly);
            Add(target, value.IsOperationalState);
        }

        private static void AddSurvivalTarot(
            StringBuilder target,
            SimulationSurvivalTarotStateSnapshot value)
        {
            Add(target, "SurvivalTarotExtensionV1");
            Add(target, value.SessionStableId);
            Add(target, value.RuleRevision);
            Add(target, value.WorldTick);
            Add(target, value.WorldRevision);
            Add(target, value.PeriodicIntervalTicks);
            Add(target, value.FoodCrisisThresholdPersonDays);
            Add(target, value.CurrentFoodReservePersonDays);
            if (value.FarmScopeConfigured)
            {
                Add(target, "FarmExitExtensionV1");
                Add(target, value.FarmExitThresholdPersonDays);
                Add(target, value.CurrentFarmFoodReservePersonDays);
                Add(target, value.RequiresExternalExpedition);
            }
            Add(target, value.CalendarRuleCode);
            Add(target, value.PendingOpportunity == null);
            if (value.PendingOpportunity != null)
                AddSurvivalTarotOpportunity(target, value.PendingOpportunity,
                    value.FarmScopeConfigured);
            Add(target, value.OpportunityHistory.Length);
            foreach (var opportunity in value.OpportunityHistory)
                AddSurvivalTarotOpportunity(target, opportunity,
                    value.FarmScopeConfigured);
            Add(target, value.ActiveModifierLines.Length);
            foreach (var line in value.ActiveModifierLines)
                AddTarotModifierLine(target, line);
            Add(target, value.SimulationOnly);
            Add(target, value.IsOperationalState);
        }

        private static void AddSurvivalTarotOpportunity(
            StringBuilder target,
            SimulationSurvivalTarotOpportunitySnapshot value,
            bool farmScopeConfigured)
        {
            Add(target, value.OpportunityStableId);
            Add(target, value.TriggerCode);
            Add(target, value.StatusCode);
            Add(target, value.TriggeredWorldTick);
            Add(target, value.FoodReservePersonDays);
            if (farmScopeConfigured)
            {
                Add(target, value.FarmFoodReservePersonDays);
                Add(target, value.RequiresExternalExpedition);
            }
            Add(target, value.SafeBuildingStableId);
            AddStrings(target, value.ParticipantPlayerStableIds);
            Add(target, value.Draw.DrawStableId);
            Add(target, value.Draw.DeckStableId);
            Add(target, value.Draw.DeckRevision);
            Add(target, value.Draw.DrawRuleRevision);
            Add(target, value.Draw.TurnNumber);
            Add(target, value.Draw.TurnHistoryHash);
            Add(target, value.Draw.Offers.Length);
            foreach (var offer in value.Draw.Offers)
            {
                Add(target, offer.OfferStableId);
                Add(target, offer.OfferSlotNumber);
                Add(target, offer.CardCopyStableId);
                Add(target, offer.OrientationCode);
                AddTurnCard(target, offer.Card);
            }
            Add(target, value.Responses.Length);
            foreach (var response in value.Responses)
            {
                Add(target, response.PlayerStableId);
                Add(target, response.OfferStableId);
                Add(target, response.RespondedWorldTick);
                Add(target, response.RespondedWorldRevision);
            }
            Add(target, value.SelectedOfferStableId);
            Add(target, value.ResolvedWorldTick?.ToString(CultureInfo.InvariantCulture)
                ?? string.Empty);
            Add(target, value.ResolvedWorldRevision?.ToString(CultureInfo.InvariantCulture)
                ?? string.Empty);
            Add(target, value.ModifierLines.Length);
            foreach (var line in value.ModifierLines)
                AddTarotModifierLine(target, line);
        }

        private static void AddTarotModifierLine(
            StringBuilder target,
            Simulation타로규칙보정선Snapshot value)
        {
            Add(target, value.ModifierLineStableId);
            Add(target, value.UpperRuleStableId);
            Add(target, value.UpperRuleRevision);
            Add(target, value.SourceCardStableId);
            Add(target, value.SourceCardRevision);
            Add(target, value.CardOrientationCode);
            Add(target, value.ResponseStableId);
            Add(target, value.TargetConnectionPointStableId);
            Add(target, value.TargetRuleDomainCode);
            Add(target, value.CompatibleLowerRuleStableId);
            Add(target, value.CompatibleLowerRuleRevision);
            Add(target, value.CalculationKindCode);
            Add(target, value.ModifierValue);
            Add(target, value.ModifierUnitCode);
            Add(target, value.MeaningCode);
            Add(target, value.ActiveFromTurnNumber);
            Add(target, value.ActiveThroughTurnNumber);
            Add(target, value.SourceTurnClosingStableId);
            AddStrings(target, value.SourceStableIds);
        }

        private static void AddSnapshot(StringBuilder target,
            경영SimulationSessionSnapshot value, bool includesTarotJourneyRoot,
            bool includesV16, bool includesV21, bool includesV22)
        {
            Add(target, value.SessionStableId);
            Add(target, value.ClientRequestId.ToString("N"));
            Add(target, value.ScenarioStableId);
            Add(target, value.ScenarioDataRevision);
            Add(target, value.ScenarioSeed);
            Add(target, value.RuleRevision);
            if (includesV21)
                Add(target, value.NpcRoutineControlRevision);
            if (includesV22 && (value.InteriorPlanHandles?.Length ?? 0) > 0)
                AddInteriorPlanHandles(target, value.InteriorPlanHandles);
            Add(target, value.CurrentTick);
            Add(target, value.DurationTicks);
            Add(target, value.Revision);
            Add(target, value.IsCompleted);
            Add(target, value.ModeCode);
            Add(target, value.IsOperationalState);
            Add(target, value.WorldContext.FactionStableId);
            Add(target, value.WorldContext.TerritoryStableId);
            Add(target, value.WorldContext.SettlementStableId);
            Add(target, value.WorldContext.WorldTick);
            Add(target, value.WorldContext.WorldRevision);
            Add(target, value.WorldContext.GameDateStartsOn.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
            Add(target, value.WorldContext.GameDate.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
            Add(target, value.WorldContext.CalendarRuleCode);
            Add(target, value.Decisions.Length);
            foreach (var decision in value.Decisions)
                AddDecision(target, decision);
            Add(target, value.Tasks.Length);
            foreach (var task in value.Tasks)
                AddTask(target, task);
            Add(target, value.Effects.Length);
            foreach (var effect in value.Effects)
                AddEffect(target, effect);
            Add(target, value.LogisticsMovements.Length);
            foreach (var movement in value.LogisticsMovements)
                AddLogisticsMovement(target, movement);
            Add(target, value.FreightTransports.Length);
            foreach (var freight in value.FreightTransports)
                AddFreightTransport(target, freight);
            Add(target, value.GroupOrders.Length);
            foreach (var groupOrder in value.GroupOrders)
                AddGroupOrder(target, groupOrder);
            Add(target, value.FoodDeliveries.Length);
            if (value.SyntheticCourier != null)
            {
                var courier = value.SyntheticCourier;
                Add(target, "synthetic-delivery.r1");
                Add(target, courier.ActorStableId); Add(target, courier.Stage); Add(target, courier.OrderStableId);
                Add(target, courier.Batch); Add(target, courier.Distance.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
                Add(target, courier.X.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
                Add(target, courier.Z.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
                Add(target, courier.VehicleX.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
                Add(target, courier.VehicleZ.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
                Add(target, courier.Carrying);
            }
            foreach (var foodDelivery in value.FoodDeliveries)
                AddFoodDelivery(target, foodDelivery);
            if (value.WaitingFleet != null)
            {
                AddNeighborhoodLife(target,value.WaitingFleet.NeighborhoodLife);
                if (value.WaitingFleet.OrderFlow != null)
                {
                    Add(target, "synthetic-order-flow.r4");
                    foreach (var person in value.WaitingFleet.OrderFlow.Orderers)
                    { Add(target, person.Residence); Add(target, person.NextTick); Add(target, person.Sequence); }
                    foreach (var entry in value.WaitingFleet.OrderFlow.Entries)
                    {
                        Add(target, entry.OrderId); Add(target, entry.SourceKind); Add(target, entry.SubmittedTick);
                        Add(target, entry.ResponseTick ?? -1); Add(target, entry.ResponseCode); Add(target, entry.QueuedTick ?? -1);
                        Add(target, entry.DriverId); Add(target, entry.State); Add(target, entry.WaitReason);
                    }
                }
                if (value.WaitingFleet.Mart != null)
                {
                    var mart = value.WaitingFleet.Mart;
                    Add(target, "synthetic-mart.r3");
                    foreach (var quantity in mart.Stock) Add(target, quantity);
                    foreach (var quantity in mart.Reserved) Add(target, quantity);
                    Add(target, mart.WorkerStage); Add(target, mart.WorkerOrderId);
                    Add(target, mart.X.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
                    Add(target, mart.Z.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
                    Add(target, mart.Shelf); Add(target, mart.WorkTicks);
                    foreach (var order in mart.Orders)
                    {
                        Add(target, order.OrderId); Add(target, order.DestinationId); Add(target, order.Stage);
                        Add(target, order.WaitReason); Add(target, order.AcceptedTick); Add(target, order.PickedUnits);
                        Add(target, order.PackedTick ?? -1); Add(target, order.ReadyTick ?? -1);
                        Add(target, order.PickedUpTick ?? -1); Add(target, order.DeliveredTick ?? -1);
                        Add(target, order.ReceivedTick ?? -1);
                    }
                }
                Add(target, "synthetic-delivery.r2"); Add(target, value.WaitingFleet.Batch);
                Add(target, value.WaitingFleet.TrafficOwner); Add(target, value.WaitingFleet.Decision);
                foreach (var item in value.WaitingFleet.Drivers)
                {
                    var driver = item.Courier;
                    Add(target, driver.ActorStableId); Add(target, driver.Stage); Add(target, driver.OrderStableId);
                    Add(target, driver.Distance.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
                    Add(target, driver.X.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
                    Add(target, driver.Z.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
                    Add(target, driver.VehicleX.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
                    Add(target, driver.VehicleZ.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
                    Add(target, driver.Carrying); Add(target, item.Slot); Add(target, item.ReturnSlot);
                    Add(target, item.AssignedTick); Add(target, item.PendingOrderId); Add(target, item.WaitReason);
                }
            }
            Add(target, value.MarketConsumptions.Length);
            foreach (var consumption in value.MarketConsumptions)
                AddMarketConsumption(target, consumption);
            Add(target, value.IndividualOrders.Length);
            foreach (var order in value.IndividualOrders)
                AddIndividualOrder(target, order);
            Add(target, value.StockReservations.Length);
            foreach (var reservation in value.StockReservations)
                AddStockReservation(target, reservation);
            Add(target, value.ExportPreparations.Length);
            foreach (var preparation in value.ExportPreparations)
                Add수출준비(target, preparation);
            Add(target, value.ExportCargoPreparations.Length);
            foreach (var preparation in value.ExportCargoPreparations)
                Add수출Cargo준비(target, preparation);
            Add(target, value.ExportCargoHandoffs.Length);
            foreach (var handoff in value.ExportCargoHandoffs)
                Add수출Cargo인계(target, handoff);
            Add(target, value.ExportPortReceipts.Length);
            foreach (var receipt in value.ExportPortReceipts)
                Add수출항만인수(target, receipt);
            Add(target, value.ExportReadinessReviews.Length);
            foreach (var review in value.ExportReadinessReviews)
                Add수출준비성검토(target, review);
            Add(target, value.ExportShipmentPlans.Length);
            foreach (var plan in value.ExportShipmentPlans)
                Add수출선적계획(target, plan);
            Add(target, value.ExportShipmentExecutions.Length);
            foreach (var execution in value.ExportShipmentExecutions)
                Add수출선적실행(target, execution);
            if (value.TurnClosings.Length > 0 || value.ActiveTurnCardEffects.Length > 0)
            {
                Add(target, "TurnClosingExtensionV1");
                Add(target, value.TurnClosings.Length);
                foreach (var closing in value.TurnClosings)
                {
                    Add(target, closing.TurnClosingStableId);
                    Add(target, closing.ClosedTurnNumber);
                    Add(target, closing.ClosedGameDate.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
                    Add(target, closing.ResultingWorldTick);
                    Add(target, closing.ResultingRevision);
                    Add(target, closing.SelectedCards.Length);
                    foreach (var card in closing.SelectedCards)
                        AddTurnCard(target, card);
                    if (includesV16)
                    {
                        Add(target, closing.DeactivatedActiveMajorArcana);
                        var direction = closing.MajorArcanaDirectionDecision;
                        Add(target, direction != null);
                        if (direction != null)
                        {
                            Add(target, direction.DirectionCode);
                            Add(target, direction.RecoveryShareMicro);
                            Add(target, direction.RecoveryOutput);
                            Add(target, direction.ThreatOutput);
                            Add(target, direction.ContextPlayerStableId);
                            Add(target, direction.EvidenceRevision);
                            Add(target, direction.EvidenceHashSha256);
                            Add(target, direction.RuleRevision);
                            Add(target, direction.DecidedAtWorldRevision);
                            Add(target, direction.DecidedAtWorldTick);
                        }
                    }
                }
                Add(target, value.ActiveTurnCardEffects.Length);
                foreach (var effect in value.ActiveTurnCardEffects)
                {
                    Add(target, effect.CardStableId);
                    Add(target, effect.CardRevision);
                    Add(target, effect.CardKindCode);
                    Add(target, effect.CardCopyStableId);
                    Add(target, effect.OfferStableId);
                    Add(target, effect.OrientationCode);
                    Add(target, effect.EffectCode);
                    Add(target, effect.TargetStatCode);
                    Add(target, effect.StatDelta);
                    Add(target, effect.ActiveTurnNumber);
                    Add(target, effect.SourceTurnClosingStableId);
                    Add(target, effect.SourceStableId);
                    Add(target, effect.RegionKey);
                    Add(target, effect.CalendarRevision);
                    Add(target, effect.EffectRuleRevision);
                    Add(target, effect.SourceUrl);
                    Add(target, effect.EvidenceCheckedAtUtc?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture) ?? string.Empty);
                }
            }
            if (value.TarotContext != null
                && ((includesTarotJourneyRoot
                        && !string.IsNullOrWhiteSpace(
                            value.TarotContext.FrameSet.JourneyRoot.CardStableId))
                    || value.TarotContext.FrameSet.ActiveFrames.Length > 0
                    || value.TarotContext.Proposals.Length > 0
                    || value.TarotContext.IncidentEvaluations.Length > 0
                    || (includesV16
                        && value.TarotContext.MajorArcanaActivations.Length > 0)))
            {
                Add(target, includesV16 ? "TarotContextExtensionV3"
                    : includesTarotJourneyRoot
                        ? "TarotContextExtensionV2" : "TarotContextExtensionV1");
                Add(target, includesV16
                    ? 경영SimulationSessionAggregate
                        .BuildTarotContextStateV3PayloadKey(value.TarotContext)
                    : includesTarotJourneyRoot
                    ? 경영SimulationSessionAggregate
                        .BuildTarotContextStatePayloadKey(value.TarotContext)
                    : 경영SimulationSessionAggregate
                        .BuildLegacyTarotContextStatePayloadKey(value.TarotContext));
                Add(target, includesV16
                    ? 경영SimulationSessionAggregate
                        .BuildTarotContextStateV3Hash(value.TarotContext)
                    : includesTarotJourneyRoot
                    ? value.TarotContext.ContextStateHashSha256
                    : 경영SimulationSessionAggregate
                        .BuildLegacyTarotContextStateHash(value.TarotContext));
            }
            if (includesV16)
            {
                Add(target, "TownNpcLifeExtensionV1");
                Add(target, 경영SimulationSessionAggregate
                    .BuildTownNpcLifePayloadKey(value.TownNpcLife));
                Add(target, value.TownNpcLife.StateHashSha256);
            }
            AddNpcWorkforceSnapshot(target, value, includesV21);
            AddSimulationSpatialSnapshot(target, value);
            AddSettlement(target, value.Settlement);
            if (value.FarmSurvival != null)
                Add(target, 경영SimulationSessionAggregate
                    .BuildFarmSurvivalStatePayloadKey(value.FarmSurvival));
            if (value.TeamRoleCards != null)
                Add(target, 경영SimulationSessionAggregate
                    .BuildTeamRoleCardStatePayloadKey(value.TeamRoleCards));
            if (value.Exploration != null)
                Add(target, 경영SimulationSessionAggregate
                    .BuildWorldExplorationStatePayloadKey(value.Exploration));
            if (value.CollectibleCardRewards != null)
                Add(target, 경영SimulationSessionAggregate
                    .BuildCollectibleCardRewardStatePayloadKey(
                        value.CollectibleCardRewards));
            if (value.RegionalIncidents.Length > 0
                || value.NatureThreat.Encounters.Length > 0)
            {
                Add(target, "RegionalIncidentExtensionV1");
                Add(target, value.RegionalIncidents.Length);
                foreach (var incident in value.RegionalIncidents.OrderBy(
                    item => item.IncidentStableId, StringComparer.Ordinal))
                {
                    Add(target, incident.IncidentStableId);
                    Add(target, incident.EventStableId);
                    Add(target, incident.IncidentRevision);
                    Add(target, incident.SourceInstanceStableId);
                    Add(target, incident.NatureRouteCode);
                    Add(target, incident.IncidentTypeCode);
                    Add(target, incident.StateCode);
                    Add(target, incident.OutcomeCode);
                    Add(target, incident.Severity);
                    Add(target, incident.RemainingSeverity);
                    Add(target, incident.OccurredWorldTick);
                    Add(target, incident.DeadlineWorldTick);
                    Add(target, incident.SourceTargetStableId);
                    Add(target, incident.FacilityStableId);
                    Add(target, incident.SelectedChoiceStableId);
                    AddStrings(target, incident.RequiredWorldInteractionIds);
                    AddStrings(target, incident.RequiredActionCodes);
                    AddStrings(target, incident.CompletedActionCodes);
                    AddStrings(target, incident.SourceStableIds);
                }
                Add(target, value.NatureThreat.Routes.Length);
                foreach (var route in value.NatureThreat.Routes.OrderBy(
                    item => item.NatureRouteCode, StringComparer.Ordinal))
                {
                    Add(target, route.NatureRouteCode);
                    Add(target, route.RootRemainingSeverity);
                    Add(target, route.GlobalSpilloverPressure);
                    Add(target, route.EffectivePressure);
                    Add(target, route.PressureLevelCode);
                    AddStrings(target, route.SourceIncidentStableIds);
                }
                Add(target, value.NatureThreat.Encounters.Length);
                foreach (var encounter in value.NatureThreat.Encounters.OrderBy(
                    item => item.EncounterStableId, StringComparer.Ordinal))
                {
                    Add(target, encounter.EncounterStableId);
                    Add(target, encounter.EncounterRevision);
                    Add(target, encounter.NatureRouteCode);
                    Add(target, encounter.StateCode);
                    Add(target, encounter.RiskBandCode);
                    Add(target, encounter.ThreatUnitCount);
                    Add(target, encounter.OccurredWorldTick);
                    Add(target, encounter.ResolvedWorldTick ?? -1);
                    AddStrings(target, encounter.SourceIncidentStableIds);
                    Add(target, encounter.PresentationKey);
                }
            }
        }

        private static void AddNpcWorkforceSnapshot(
            StringBuilder target,
            경영SimulationSessionSnapshot value,
            bool includesV21)
        {
            if (value.NpcOrganizations.Length == 0
                && value.NpcActors.Length == 0
                && value.NpcCapabilityGrants.Length == 0
                && value.NpcWorkPolicies.Length == 0
                && value.NpcTaskAssignments.Length == 0
                && value.NpcWorkRecords.Length == 0
                && value.NpcActionProjections.Length == 0
                && value.NpcFacilityInventories.Length == 0)
                return;
            Add(target, "NpcWorkforceExtensionV1");
            Add(target, value.NpcOrganizations.Length);
            foreach (var organization in value.NpcOrganizations)
            {
                Add(target, organization.OrganizationStableId);
                Add(target, organization.DisplayName);
                AddStrings(target, organization.FacilityStableIds);
                AddStrings(target, organization.AllowedCapabilityCodes);
                AddStrings(target, organization.SourceStableIds);
            }
            Add(target, value.NpcActors.Length);
            foreach (var actor in value.NpcActors)
            {
                Add(target, actor.ActorStableId);
                Add(target, actor.OrganizationStableId);
                Add(target, actor.DisplayName);
                Add(target, actor.HomeFacilityStableId);
                Add(target, actor.ReferenceRoleCode);
                Add(target, actor.MaximumConcurrentTasks);
                AddStrings(target, actor.AssignableCapabilityCodes);
                Add(target, actor.Skills.Length);
                foreach (var skill in actor.Skills)
                {
                    Add(target, skill.CapabilityCode);
                    Add(target, skill.Score);
                }
                AddStrings(target, actor.SourceStableIds);
            }
            Add(target, value.NpcCapabilityGrants.Length);
            foreach (var grant in value.NpcCapabilityGrants)
            {
                Add(target, grant.GrantStableId);
                Add(target, grant.OrganizationStableId);
                Add(target, grant.ActorStableId);
                Add(target, grant.FacilityStableId);
                Add(target, grant.CapabilityCode);
                Add(target, grant.GrantedByActorStableId);
                Add(target, grant.GrantKindCode);
                Add(target, grant.CanDelegate);
                Add(target, grant.Active);
                Add(target, grant.GrantedTick);
                Add(target, grant.Revision);
                AddStrings(target, grant.SourceStableIds);
            }
            Add(target, value.NpcWorkPolicies.Length);
            foreach (var policy in value.NpcWorkPolicies)
            {
                if (policy.CookingSlots.HasValue)
                {
                    Add(target, "restaurant-slots.v1");
                    Add(target, policy.CookingSlots.Value);
                }
                Add(target, policy.PolicyStableId);
                Add(target, policy.OrganizationStableId);
                Add(target, policy.FacilityStableId);
                Add(target, policy.ActionCode);
                Add(target, policy.RequiredCapabilityCode);
                Add(target, policy.AutomationEnabled);
                Add(target, policy.Priority);
                Add(target, policy.PreferredActorStableId);
                Add(target, policy.AutoDelegationEnabled);
                Add(target, policy.AutoDelegationBacklogThreshold);
                Add(target, policy.TravelDurationTicks);
                Add(target, policy.WorkDurationTicks);
                Add(target, policy.InteractionPointKey);
                Add(target, policy.ActionVisualKey);
                Add(target, policy.Revision);
                AddStrings(target, policy.SourceStableIds);
            }
            Add(target, value.NpcTaskAssignments.Length);
            foreach (var assignment in value.NpcTaskAssignments)
            {
                Add(target, assignment.AssignmentStableId);
                Add(target, assignment.TaskStableId);
                Add(target, assignment.PolicyStableId);
                Add(target, assignment.OrganizationStableId);
                Add(target, assignment.FacilityStableId);
                Add(target, assignment.ActorStableId);
                Add(target, assignment.ActionCode);
                Add(target, assignment.RequiredCapabilityCode);
                Add(target, assignment.PhaseCode);
                Add(target, assignment.AssignedTick);
                Add(target, assignment.PhaseStartedTick);
                Add(target, assignment.TravelDurationTicks);
                Add(target, assignment.WorkDurationTicks);
                Add(target, assignment.CompletedTick?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
                AddStrings(target, assignment.BlockReasonCodes);
                Add(target, assignment.Revision);
            }
            Add(target, value.NpcWorkRecords.Length);
            foreach (var record in value.NpcWorkRecords)
            {
                Add(target, record.WorkRecordStableId);
                Add(target, record.AssignmentStableId);
                Add(target, record.TaskStableId);
                Add(target, record.ActorStableId);
                Add(target, record.ActionCode);
                Add(target, record.FacilityStableId);
                Add(target, record.StartedTick);
                Add(target, record.CompletedTick);
                AddStrings(target, record.ResultCodes);
                AddStrings(target, record.SourceStableIds);
            }
            Add(target, value.NpcActionProjections.Length);
            foreach (var projection in value.NpcActionProjections)
            {
                Add(target, projection.ProjectionStableId);
                Add(target, projection.ActorStableId);
                Add(target, projection.TaskStableId);
                Add(target, projection.FacilityStableId);
                Add(target, projection.InteractionPointKey);
                Add(target, projection.ActionVisualKey);
                Add(target, projection.PhaseCode);
                Add(target, projection.ProgressRate);
                AddStrings(target, projection.BlockReasonCodes);
                Add(target, projection.Revision);
                Add(target, projection.WorldTick);
                Add(target, projection.PresentationOnly);
            }
            Add(target, value.NpcFacilityInventories.Length);
            foreach (var inventory in value.NpcFacilityInventories)
            {
                Add(target, inventory.InventoryStableId);
                Add(target, inventory.LotStableId);
                Add(target, inventory.FacilityStableId);
                if (!string.IsNullOrWhiteSpace(inventory.ProductStableId))
                    Add(target, inventory.ProductStableId);
                Add(target, inventory.StateCode);
                Add(target, inventory.Quantity);
                Add(target, inventory.UnitCode);
                Add(target, inventory.SourceTaskStableId);
                Add(target, inventory.UpdatedTick);
                Add(target, inventory.Revision);
                AddStrings(target, inventory.SourceStableIds);
            }
            if (includesV21)
            {
                Add(target, value.NpcRoutineExecutions.Length);
                foreach (var execution in value.NpcRoutineExecutions.OrderBy(
                             item => item.ExecutionStableId, StringComparer.Ordinal))
                {
                    Add(target, execution.ExecutionStableId);
                    Add(target, execution.AreaCode);
                    Add(target, execution.WorldInteractionId);
                    Add(target, execution.ParentExecutionStableId);
                    Add(target, execution.PolicyStableId);
                    Add(target, execution.TaskStableId);
                    Add(target, execution.InventoryStableId);
                    Add(target, execution.OriginCode);
                    Add(target, execution.ControlPolicyCode);
                    Add(target, execution.TriggerSourceCode);
                    Add(target, execution.RecordedWorldTick);
                    Add(target, execution.Revision);
                }
            }
        }

        private static void AddRegionalCausality(
            StringBuilder target,
            SimulationRegionalCausalityStateSnapshot value)
        {
            Add(target, "RegionalCausalityExtensionV1");
            Add(target, value.Revision);
            Add(target, value.ThreatScore);
            Add(target, value.RecoveryScore);
            Add(target, value.NetPressureModifier);
            Add(target, value.OutcomeCode);
            Add(target, value.LastChangedWorldTick);
            Add(target, value.Changes.Length);
            foreach (var change in value.Changes.OrderBy(
                         item => item.ChangeStableId, StringComparer.Ordinal))
            {
                Add(target, change.ChangeStableId);
                Add(target, change.SourceCode);
                Add(target, change.ThreatDelta);
                Add(target, change.RecoveryDelta);
                Add(target, change.AppliedWorldTick);
                Add(target, change.SourceStableId);
                Add(target, change.NatureRouteCode);
            }
        }

        private static void AddRegionalDevelopment(
            StringBuilder target,
            SimulationRegionalDevelopmentStateSnapshot value)
        {
            Add(target, "RegionalDevelopmentExtensionV1");
            Add(target, value.Revision);
            Add(target, value.RuleRevision);
            Add(target, value.Opportunities.Length);
            foreach (var opportunity in value.Opportunities.OrderBy(
                         item => item.OpportunityStableId, StringComparer.Ordinal))
            {
                Add(target, opportunity.OpportunityStableId);
                Add(target, opportunity.OpportunityRevision);
                Add(target, opportunity.SourceIncidentStableId);
                Add(target, opportunity.SourceEncounterStableId);
                Add(target, opportunity.SourceBattleStableId);
                Add(target, opportunity.NatureRouteCode);
                Add(target, opportunity.TargetAreaCode);
                Add(target, opportunity.StateCode);
                Add(target, opportunity.EarnedWorldTick);
                Add(target, opportunity.ReservedWorldTick.HasValue
                    ? opportunity.ReservedWorldTick.Value.ToString(CultureInfo.InvariantCulture)
                    : string.Empty);
                Add(target, opportunity.ConsumedWorldTick.HasValue
                    ? opportunity.ConsumedWorldTick.Value.ToString(CultureInfo.InvariantCulture)
                    : string.Empty);
                Add(target, opportunity.ReservedProjectStableId);
                Add(target, opportunity.OperationalFacilityStableId);
            }
            Add(target, value.RouteSafeties.Length);
            foreach (var safety in value.RouteSafeties.OrderBy(
                         item => item.NatureRouteCode, StringComparer.Ordinal))
            {
                Add(target, safety.NatureRouteCode);
                Add(target, safety.SecuredFromWorldTick);
                Add(target, safety.SecuredUntilWorldTick);
                Add(target, safety.SourceEncounterStableId);
                Add(target, safety.SourceBattleStableId);
            }
            Add(target, value.Areas.Length);
            foreach (var area in value.Areas.OrderBy(
                         item => item.AreaCode, StringComparer.Ordinal))
            {
                Add(target, area.AreaCode);
                Add(target, area.TargetH2StableId);
                Add(target, area.StateCode);
                AddStrings(target, area.RequiredH1StableIds);
                AddStrings(target, area.OperationalH1StableIds);
            }
            Add(target, value.Connectors.Length);
            foreach (var connector in value.Connectors.OrderBy(
                         item => item.ConnectorStableId, StringComparer.Ordinal))
            {
                Add(target, connector.ConnectorStableId);
                Add(target, connector.FromAreaCode);
                Add(target, connector.ToAreaCode);
                Add(target, connector.StateCode);
                Add(target, connector.RequiredAreaCode);
            }
        }

        private static void AddSimulationSpatialSnapshot(
            StringBuilder target,
            경영SimulationSessionSnapshot value)
        {
            if (value.SpatialDefinitions.Length == 0
                && value.SpatialRuntimeStates.Length == 0
                && value.SpatialReservations.Length == 0)
                return;
            Add(target, "SimulationSpatialInteractionExtensionV1");
            Add(target, value.SpatialDefinitions.Length);
            foreach (var definition in value.SpatialDefinitions)
            {
                Add(target, definition.SpatialStableId);
                Add(target, definition.FacilityStableId);
                Add(target, definition.AreaStableId);
                Add(target, definition.AreaSetStableId);
                Add(target, definition.LandscapeGraphStableId);
                Add(target, definition.LandscapeNodeStableId);
                Add(target, definition.EvidenceKindCode);
                Add(target, definition.AccessStateCode);
                AddStrings(target, definition.CapabilityCodes);
                AddCapacities(target, definition.BaseCapacities);
                Add(target, definition.DefinitionRevision);
                Add(target, definition.DefinitionHashSha256);
                AddStrings(target, definition.SourceStableIds);
            }
            Add(target, value.SpatialRuntimeStates.Length);
            foreach (var runtime in value.SpatialRuntimeStates)
            {
                Add(target, runtime.SpatialStableId);
                Add(target, runtime.AccessStateCode);
                AddCapacities(target, runtime.OccupiedCapacities);
                AddCapacities(target, runtime.ReservedCapacities);
                AddStrings(target, runtime.ActiveTaskStableIds);
                Add(target, runtime.Revision);
            }
            Add(target, value.SpatialReservations.Length);
            var hasSpatialRoles = value.SpatialReservations.Any(reservation =>
                !string.IsNullOrWhiteSpace(reservation.RoleCode));
            if (hasSpatialRoles) Add(target, "SimulationSpatialReservationRolesV2");
            foreach (var reservation in value.SpatialReservations)
            {
                Add(target, reservation.ReservationStableId);
                Add(target, reservation.SpatialStableId);
                Add(target, reservation.TaskStableId);
                if (hasSpatialRoles) Add(target, reservation.RoleCode);
                Add(target, reservation.ReservationKindCode);
                Add(target, reservation.Quantity);
                Add(target, reservation.UnitCode);
                Add(target, reservation.StatusCode);
                Add(target, reservation.ReservedAtTick);
                Add(target, reservation.ConsumedAtTick ?? -1);
                Add(target, reservation.ReleasedAtTick ?? -1);
                Add(target, reservation.CreatedRevision);
                Add(target, reservation.FinalizedRevision ?? -1);
            }
        }

        private static void AddCapacities(
            StringBuilder target,
            Simulation공간용량Snapshot[] capacities)
        {
            Add(target, capacities.Length);
            foreach (var capacity in capacities)
            {
                Add(target, capacity.CapacityCode);
                Add(target, capacity.Quantity);
                Add(target, capacity.UnitCode);
            }
        }

        private static void AddTurnCard(StringBuilder target, SimulationTurnCardSnapshot card)
        {
            Add(target, card.CardStableId);
            Add(target, card.CardRevision);
            Add(target, card.CardKindCode);
            Add(target, card.CardCopyStableId);
            Add(target, card.OfferStableId);
            Add(target, card.OrientationCode);
            Add(target, card.Title);
            Add(target, card.Summary);
            Add(target, card.EffectTimingCode);
            Add(target, card.EffectCode);
            Add(target, card.TargetStatCode);
            Add(target, card.StatDelta);
            Add(target, card.SourceStableId);
            Add(target, card.RegionKey);
            Add(target, card.AvailableFromGameDate?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture) ?? string.Empty);
            Add(target, card.AvailableThroughGameDate?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture) ?? string.Empty);
            Add(target, card.CalendarRevision);
            Add(target, card.EffectRuleRevision);
            Add(target, card.SourceUrl);
            Add(target, card.EvidenceCheckedAtUtc?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture) ?? string.Empty);
        }

        private static void Add수출준비(
            StringBuilder target,
            Simulation수출준비Snapshot value)
        {
            Add(target, value.PreparationStableId);
            Add(target, value.RootPreparationStableId);
            Add(target, value.PreviousPreparationStableId ?? string.Empty);
            Add(target, value.AttemptNumber);
            Add(target, value.IsReworkAttempt);
            Add(target, value.StateCode);
            Add(target, value.Revision);
            Add(target, value.SourceAllocationStableId);
            Add(target, value.HarvestLotStableId);
            Add(target, value.ProductStableId);
            Add(target, value.Quantity);
            Add(target, value.UnitCode);
            Add(target, value.PackingFacilityStableId);
            Add(target, value.HandoffFacilityStableId);
            Add(target, value.PackageLotCandidateStableId);
            Add(target, value.HandoffCandidateStableId);
            Add(target, value.InspectionOutcomeCode);
            Add(target, value.FailureReasonCode ?? string.Empty);
            Add(target, value.DecisionStableId);
            Add(target, value.TaskStableId);
            Add(target, value.PackagingTicks);
            Add(target, value.InspectionTicks);
            Add(target, value.ReservedTick);
            Add(target, value.PackagedTick ?? -1);
            Add(target, value.InspectedTick ?? -1);
            Add(target, value.HandoffCandidateReadyTick ?? -1);
            Add(target, value.CanRetry);
            Add(target, value.CargoPreparationStableId ?? string.Empty);
            Add(target, value.CargoStableId ?? string.Empty);
            AddStrings(target, value.SourceStableIds);
        }

        private static void Add수출Cargo준비(
            StringBuilder target,
            Simulation수출Cargo준비Snapshot value)
        {
            Add(target, value.CargoPreparationStableId);
            Add(target, value.StateCode);
            Add(target, value.Revision);
            Add(target, value.SourceExportPreparationStableId);
            Add(target, value.RootExportPreparationStableId);
            Add(target, value.ExportPreparationAttemptNumber);
            Add(target, value.SourceAllocationStableId);
            Add(target, value.HarvestLotStableId);
            Add(target, value.PackageLotStableId);
            Add(target, value.ProductStableId);
            Add(target, value.Quantity);
            Add(target, value.UnitCode);
            Add(target, value.CargoStableId);
            Add(target, value.CargoRevision);
            Add(target, value.RouteStableId);
            Add(target, value.OriginFacilityStableId);
            Add(target, value.DestinationFacilityStableId);
            Add(target, value.DecisionStableId);
            Add(target, value.TaskStableId);
            Add(target, value.RequiredPreparationTicks);
            Add(target, value.ScheduledTick);
            Add(target, value.ReadyForHandoffTick ?? -1);
            Add(target, value.HandoffStableId ?? string.Empty);
            Add(target, value.HandoffCompletedTick ?? -1);
            AddStrings(target, value.BoundaryCodes);
            AddStrings(target, value.SourceStableIds);
        }

        private static void Add수출Cargo인계(
            StringBuilder target,
            Simulation수출Cargo인계Snapshot value)
        {
            Add(target, value.HandoffStableId);
            Add(target, value.StateCode);
            Add(target, value.Revision);
            Add(target, value.SourceCargoPreparationStableId);
            Add(target, value.SourceExportPreparationStableId);
            Add(target, value.SourceAllocationStableId);
            Add(target, value.HarvestLotStableId);
            Add(target, value.PackageLotStableId);
            Add(target, value.ProductStableId);
            Add(target, value.CargoStableId);
            Add(target, value.Quantity);
            Add(target, value.UnitCode);
            Add(target, value.ReceivingFacilityStableId);
            Add(target, value.DecisionStableId);
            Add(target, value.TaskStableId);
            Add(target, value.RequiredHandoffTicks);
            Add(target, value.ScheduledTick);
            Add(target, value.CompletedTick ?? -1);
            Add(target, value.LogisticsMovementCargoStableId ?? string.Empty);
            Add(target, value.LogisticsMovementTaskStableId ?? string.Empty);
            AddStrings(target, value.BoundaryCodes);
            AddStrings(target, value.SourceStableIds);
        }

        private static void Add수출항만인수(
            StringBuilder target,
            Simulation수출항만인수Snapshot value)
        {
            Add(target, value.ReceiptStableId);
            Add(target, value.StateCode);
            Add(target, value.Revision);
            Add(target, value.CargoStableId);
            Add(target, value.SourceExportCargoHandoffStableId);
            Add(target, value.SourceAllocationStableId);
            Add(target, value.HarvestLotStableId);
            Add(target, value.PackageLotStableId);
            Add(target, value.ProductStableId);
            Add(target, value.Quantity);
            Add(target, value.UnitCode);
            Add(target, value.ReceivingFacilityStableId);
            Add(target, value.DecisionStableId);
            Add(target, value.TaskStableId);
            Add(target, value.RequiredReceivingTicks);
            Add(target, value.ScheduledTick);
            Add(target, value.CompletedTick ?? -1);
            AddStrings(target, value.BoundaryCodes);
            AddStrings(target, value.SourceStableIds);
        }

        private static void Add수출선적실행(
            StringBuilder target,
            Simulation수출선적실행Snapshot value)
        {
            Add(target, value.ExecutionStableId);
            Add(target, value.StateCode);
            Add(target, value.Revision);
            Add(target, value.OutcomeCode);
            Add(target, value.OutcomeRoll ?? -1);
            Add(target, value.SourceShipmentPlanStableId);
            Add(target, value.SourceReadinessReviewStableId);
            Add(target, value.SourcePortReceiptStableId);
            Add(target, value.CargoStableId);
            Add(target, value.SourceAllocationStableId);
            Add(target, value.HarvestLotStableId);
            Add(target, value.PackageLotStableId);
            Add(target, value.ProductStableId);
            Add(target, value.Quantity);
            Add(target, value.DeliveredQuantity);
            Add(target, value.LostQuantity);
            Add(target, value.UnitCode);
            Add(target, value.DestinationCountryCode);
            Add(target, value.DestinationMarketStableId);
            Add(target, value.TransportModeCode);
            Add(target, value.ExecutionFacilityStableId);
            Add(target, value.EstimatedTransitTicks);
            Add(target, value.RiskScore);
            Add(target, value.SuccessProbabilityPercent);
            Add(target, value.ExpectedGrossRevenue);
            Add(target, value.ExpectedTotalCost);
            Add(target, value.PreviouslyRecognizedProjectedRevenue);
            Add(target, value.SuccessTreasuryDeltaCandidate);
            Add(target, value.LossTreasuryDeltaCandidate);
            Add(target, value.RequiredLossCapacityReservation);
            Add(target, value.AppliedTreasuryDelta ?? 0m);
            Add(target, value.TreasuryBeforeApplication ?? 0m);
            Add(target, value.TreasuryAfterApplication ?? 0m);
            Add(target, value.CurrencyCode);
            Add(target, value.DecisionStableId);
            Add(target, value.TaskStableId);
            Add(target, value.ScheduledTick);
            Add(target, value.DepartedTick ?? -1);
            Add(target, value.CompletedTick ?? -1);
            AddStrings(target, value.BoundaryCodes);
            AddStrings(target, value.SourceStableIds);
        }

        private static void Add수출준비성검토(
            StringBuilder target,
            Simulation수출준비성검토Snapshot value)
        {
            Add(target, value.ReviewStableId);
            Add(target, value.StateCode);
            Add(target, value.Revision);
            Add(target, value.SourcePortReceiptStableId);
            Add(target, value.ParentReviewStableId ?? string.Empty);
            Add(target, value.AttemptNumber);
            Add(target, value.CargoStableId);
            Add(target, value.SourceExportCargoHandoffStableId);
            Add(target, value.SourceAllocationStableId);
            Add(target, value.HarvestLotStableId);
            Add(target, value.PackageLotStableId);
            Add(target, value.ProductStableId);
            Add(target, value.Quantity);
            Add(target, value.UnitCode);
            Add(target, value.ReviewingFacilityStableId);
            Add(target, value.DocumentsPrepared);
            Add(target, value.InspectionPreparationReady);
            Add(target, value.OutcomeCode);
            AddStrings(target, value.MissingRequirementCodes);
            Add(target, value.DecisionStableId);
            Add(target, value.TaskStableId);
            Add(target, value.RequiredReviewTicks);
            Add(target, value.ScheduledTick);
            Add(target, value.CompletedTick ?? -1);
            AddStrings(target, value.BoundaryCodes);
            AddStrings(target, value.SourceStableIds);
        }

        private static void Add수출선적계획(
            StringBuilder target,
            Simulation수출선적계획Snapshot value)
        {
            Add(target, value.PlanStableId);
            Add(target, value.StateCode);
            Add(target, value.Revision);
            Add(target, value.SourceReadinessReviewStableId);
            Add(target, value.SourcePortReceiptStableId);
            Add(target, value.CargoStableId);
            Add(target, value.SourceAllocationStableId);
            Add(target, value.HarvestLotStableId);
            Add(target, value.PackageLotStableId);
            Add(target, value.ProductStableId);
            Add(target, value.Quantity);
            Add(target, value.UnitCode);
            Add(target, value.DestinationCountryCode);
            Add(target, value.DestinationMarketStableId);
            Add(target, value.TransportModeCode);
            Add(target, value.PlanningFacilityStableId);
            Add(target, value.ExpectedGrossRevenue);
            Add(target, value.ExpectedInternationalLogisticsCost);
            Add(target, value.ExpectedHandlingCost);
            Add(target, value.ExpectedOtherCost);
            Add(target, value.ExpectedTotalCost);
            Add(target, value.ExpectedNetRevenue);
            Add(target, value.CurrencyCode);
            Add(target, value.EstimatedTransitTicks);
            Add(target, value.RiskScore);
            Add(target, value.RiskLevelCode);
            Add(target, value.DecisionStableId);
            Add(target, value.TaskStableId);
            Add(target, value.RequiredPlanningTicks);
            Add(target, value.ScheduledTick);
            Add(target, value.CompletedTick ?? -1);
            Add(target, value.ExecutionStableId ?? string.Empty);
            Add(target, value.ExecutionCompletedTick ?? -1);
            AddStrings(target, value.BoundaryCodes);
            AddStrings(target, value.SourceStableIds);
        }

        private static void AddIndividualOrder(
            StringBuilder target,
            SimulationIndividualOrderSnapshot value)
        {
            Add(target, value.OrderStableId);
            Add(target, value.StateCode);
            Add(target, value.Revision);
            Add(target, value.ActorStableId);
            Add(target, value.ProductStableId);
            Add(target, value.MarketFacilityStableId);
            Add(target, value.OrderedQuantity);
            Add(target, value.FulfilledQuantity);
            Add(target, value.UnitCode);
            Add(target, value.TotalPrice);
            Add(target, value.CurrencyCode);
            Add(target, value.RequiredLabor);
            Add(target, value.DecisionStableId);
            Add(target, value.TaskStableId);
            Add(target, value.CancellationTaskStableId ?? string.Empty);
            Add(target, value.ReservedTick);
            Add(target, value.ReadyForPickupTick.HasValue
                ? value.ReadyForPickupTick.Value.ToString(CultureInfo.InvariantCulture) : string.Empty);
            if (value.PickedTick.HasValue || value.PackedTick.HasValue
                || value.PickupTaskStableId != null || value.FulfilledTick.HasValue)
            {
                Add(target, "IndividualOrderLifecycleV2");
                Add(target, value.ConfirmedTick);
                Add(target, value.StockReservedTick);
                Add(target, value.PickedTick?.ToString(CultureInfo.InvariantCulture)
                    ?? string.Empty);
                Add(target, value.PackedTick?.ToString(CultureInfo.InvariantCulture)
                    ?? string.Empty);
                Add(target, value.PickupDecisionStableId ?? string.Empty);
                Add(target, value.PickupTaskStableId ?? string.Empty);
                Add(target, value.FulfilledTick?.ToString(CultureInfo.InvariantCulture)
                    ?? string.Empty);
            }
            Add(target, value.ConsumptionDecisionStableId ?? string.Empty);
            Add(target, value.ConsumptionTaskStableId ?? string.Empty);
            Add(target, value.ConsumedTick.HasValue
                ? value.ConsumedTick.Value.ToString(CultureInfo.InvariantCulture) : string.Empty);
            Add(target, value.CancelledTick.HasValue
                ? value.CancelledTick.Value.ToString(CultureInfo.InvariantCulture) : string.Empty);
            AddStrings(target, value.SourceStableIds);
        }

        private static void AddStockReservation(
            StringBuilder target,
            SimulationStockReservationSnapshot value)
        {
            Add(target, value.ReservationStableId);
            Add(target, value.OrderStableId);
            Add(target, value.MarketFacilityStableId);
            Add(target, value.ProductStableId);
            Add(target, value.Quantity);
            Add(target, value.UnitCode);
            Add(target, value.StateCode);
            Add(target, value.ReservedTick);
            Add(target, value.ConsumedTick.HasValue
                ? value.ConsumedTick.Value.ToString(CultureInfo.InvariantCulture) : string.Empty);
            Add(target, value.ReleasedTick.HasValue
                ? value.ReleasedTick.Value.ToString(CultureInfo.InvariantCulture) : string.Empty);
            AddStrings(target, value.SourceStableIds);
        }

        private static void AddLogisticsMovement(
            StringBuilder target,
            SimulationLogisticsMovementSnapshot value)
        {
            Add(target, value.CargoStableId);
            Add(target, value.CargoRevision);
            Add(target, value.SourceExportCargoHandoffStableId ?? string.Empty);
            Add(target, value.StateCode);
            Add(target, value.Revision);
            Add(target, value.SourceAllocationStableId);
            Add(target, value.HarvestLotStableId);
            Add(target, value.PackageLotStableId);
            Add(target, value.ProductStableId);
            Add(target, value.Quantity);
            Add(target, value.ReservedQuantity);
            Add(target, value.UnitCode);
            Add(target, value.RouteStableId);
            Add(target, value.OriginFacilityStableId);
            Add(target, value.DestinationFacilityStableId);
            Add(target, value.DecisionStableId);
            Add(target, value.TaskStableId);
            Add(target, value.RequiredRouteTicks);
            Add(target, value.CompletedRouteTicks);
            Add(target, value.ReservedTick);
            Add(target, value.DepartedTick ?? -1);
            Add(target, value.ArrivedTick ?? -1);
            Add(target, value.DestinationStockCandidateStableId);
            Add(target, value.DestinationReceiptStableId ?? string.Empty);
            Add(target, value.DestinationReceiptCompletedTick ?? -1);
            AddStrings(target, value.SourceStableIds);
        }

        private static void AddFreightTransport(
            StringBuilder target,
            SimulationFreightTransportSnapshot value)
        {
            Add(target, value.TransportRequestStableId);
            Add(target, value.DispatchOfferStableId);
            Add(target, value.RequestStateCode);
            Add(target, value.DispatchStateCode);
            Add(target, value.StateCode);
            Add(target, value.Revision);
            Add(target, value.CargoStableId);
            Add(target, value.CarrierCandidateStableId);
            Add(target, value.VehicleStableId);
            Add(target, value.VehicleCapacity);
            Add(target, value.VehicleCapacityUnitCode);
            Add(target, value.Quantity);
            Add(target, value.UnitCode);
            Add(target, value.LogisticsTaskStableId);
            Add(target, value.ReceiptDecisionStableId ?? string.Empty);
            Add(target, value.ReceiptTaskStableId ?? string.Empty);
            Add(target, value.RequestedTick);
            Add(target, value.DispatchedTick ?? -1);
            Add(target, value.PickedUpTick ?? -1);
            Add(target, value.ArrivedAtDropoffTick ?? -1);
            Add(target, value.ReceivedTick ?? -1);
            Add(target, value.RuleRevision);
            AddFreightDispatchDecision(target, value.DispatchDecision);
            AddStrings(target, value.ExcludedOperationalEffectCodes);
            AddStrings(target, value.SourceStableIds);
            Add(target, value.StateHistory.Length);
            foreach (var transition in value.StateHistory)
            {
                Add(target, transition.FromStateCode);
                Add(target, transition.ToStateCode);
                Add(target, transition.WorldTick);
                Add(target, transition.CauseStableId);
                Add(target, transition.RuleRevision);
            }
        }

        private static void AddFreightDispatchDecision(
            StringBuilder target,
            SimulationFreightDispatchDecisionSnapshot? value)
        {
            Add(target, value == null ? 0 : 1);
            if (value == null) return;
            Add(target, value.DispatchOfferStableId);
            Add(target, value.TransportRequestStableId);
            Add(target, value.RecommendedCarrierCandidateStableId ?? string.Empty);
            Add(target, value.SelectedCarrierCandidateStableId ?? string.Empty);
            Add(target, value.SelectedVehicleStableId ?? string.Empty);
            Add(target, value.RuleRevision);
            AddStrings(target, value.SourceStableIds);
            Add(target, value.CandidateEvaluations.Length);
            foreach (var candidate in value.CandidateEvaluations)
            {
                Add(target, candidate.CarrierCandidateStableId);
                Add(target, candidate.VehicleStableId);
                Add(target, candidate.IsEligible ? 1 : 0);
                Add(target, candidate.IsRecommended ? 1 : 0);
                Add(target, candidate.IsSelected ? 1 : 0);
                Add(target, candidate.Rank);
                Add(target, candidate.PickupDistanceKm ?? decimal.MinValue);
                Add(target, candidate.VehicleCapacity);
                Add(target, candidate.VehicleCapacityUnitCode);
                Add(target, candidate.Reason);
                AddStrings(target, candidate.BlockReasonCodes);
                Add(target, candidate.Score.ScheduleScore);
                Add(target, candidate.Score.ProfitScore);
                Add(target, candidate.Score.DelayScore);
                Add(target, candidate.Score.DistanceScore);
                Add(target, candidate.Score.RecommendationTypeScore);
                Add(target, candidate.Score.CargoSensitivityScore);
                Add(target, candidate.Score.ReturnBurdenScore);
                Add(target, candidate.Score.BaseScore);
                Add(target, candidate.Score.DriverWaitingScore);
                Add(target, candidate.Score.TotalScore);
            }
        }

        private static void AddGroupOrder(StringBuilder target, Simulation같이주문Snapshot value)
        {
            Add(target, value.GroupOrderStableId);
            Add(target, value.ProductStableId);
            Add(target, value.DeliveryScopeStableId);
            Add(target, value.AggregationFacilityStableId);
            Add(target, value.StateCode);
            Add(target, value.Revision);
            Add(target, value.ParticipantCount);
            Add(target, value.TotalQuantity);
            Add(target, value.UnitCode);
            Add(target, value.TargetParticipantCount);
            Add(target, value.TargetQuantity);
            Add(target, value.DecisionStableId);
            Add(target, value.TaskStableId);
            Add(target, value.CreatedTick);
            Add(target, value.FinalizedTick ?? -1);
            Add(target, value.RuleRevision);
            AddStrings(target, value.ExcludedOperationalEffectCodes);
            AddStrings(target, value.SourceStableIds);
            Add(target, value.Intents.Length);
            foreach (var intent in value.Intents)
            {
                Add(target, intent.IntentStableId);
                Add(target, intent.ParticipantStableId);
                Add(target, intent.Quantity);
                Add(target, intent.UnitCode);
                Add(target, intent.ExplicitParticipationConsent);
                AddStrings(target, intent.SourceStableIds);
            }
        }

        private static void AddFoodDelivery(
            StringBuilder target,
            Simulation음식배달Snapshot value)
        {
            if (!string.IsNullOrEmpty(value.RestaurantResponseDecisionStableId) || !string.IsNullOrEmpty(value.RejectionReasonCode))
            {
                Add(target, "restaurant-response.v1");
                Add(target, value.RestaurantResponseDecisionStableId);
                Add(target, value.RejectionReasonCode);
            }
            Add(target, value.FoodOrderStableId);
            Add(target, value.MenuItemStableId);
            Add(target, value.RestaurantFacilityStableId);
            Add(target, value.DestinationFacilityStableId);
            Add(target, value.DeliveryScopeStableId);
            Add(target, value.OrdererStableId);
            Add(target, value.StateCode);
            Add(target, value.Revision);
            Add(target, value.Quantity);
            Add(target, value.UnitCode);
            Add(target, value.PreparationDurationTicks);
            Add(target, value.DeliveryDurationTicks);
            Add(target, value.TotalDurationTicks);
            Add(target, value.DecisionStableId);
            Add(target, value.TaskStableId);
            Add(target, value.ReceiptDecisionStableId ?? string.Empty);
            Add(target, value.ReceiptTaskStableId ?? string.Empty);
            Add(target, value.AcceptedTick);
            Add(target, value.CookingStartedTick ?? -1);
            Add(target, value.ReadyForPickupTick ?? -1);
            Add(target, value.DispatchCandidateTick ?? -1);
            Add(target, value.PickedUpTick ?? -1);
            Add(target, value.DeliveredTick ?? -1);
            Add(target, value.ReceivedTick ?? -1);
            Add(target, value.RuleRevision);
            AddStrings(target, value.ExcludedOperationalEffectCodes);
            AddStrings(target, value.SourceStableIds);
            Add(target, value.StateHistory.Length);
            foreach (var transition in value.StateHistory)
            {
                Add(target, transition.FromStateCode);
                Add(target, transition.ToStateCode);
                Add(target, transition.WorldTick);
                Add(target, transition.CauseStableId);
                Add(target, transition.RuleRevision);
            }
        }

        private static void AddMarketConsumption(
            StringBuilder target,
            Simulation시장소비Snapshot value)
        {
            Add(target, value.ConsumptionStableId);
            Add(target, value.OrderStableId);
            Add(target, value.ReservationStableId);
            Add(target, value.ActorStableId);
            Add(target, value.ProductStableId);
            Add(target, value.MarketFacilityStableId);
            Add(target, value.Quantity);
            Add(target, value.UnitCode);
            Add(target, value.StateCode);
            Add(target, value.Revision);
            Add(target, value.DecisionStableId);
            Add(target, value.TaskStableId);
            Add(target, value.ScheduledTick);
            Add(target, value.ConsumedTick ?? -1);
            Add(target, value.MarketSupplyAfterOrderFulfillment);
            Add(target, value.MarketSupplyObservedAtConsumption.HasValue
                ? value.MarketSupplyObservedAtConsumption.Value.ToString(CultureInfo.InvariantCulture)
                : string.Empty);
            Add(target, value.AdditionalMarketSupplyDeductionApplied);
            AddStrings(target, value.SourceStableIds);
        }

        private static void AddDecision(StringBuilder target, SimulationDecisionSnapshot value)
        {
            Add(target, value.DecisionStableId);
            Add(target, value.DecisionTypeCode);
            Add(target, value.StateCode);
            Add(target, value.Revision);
            Add(target, value.SessionStableId);
            Add(target, value.FactionStableId);
            Add(target, value.TerritoryStableId);
            Add(target, value.SettlementStableId);
            Add(target, value.ActorStableId);
            AddStrings(target, value.TargetStableIds);
            Add(target, value.CreatedTick);
            Add(target, value.ConfirmedTick.HasValue ? value.ConfirmedTick.Value.ToString(CultureInfo.InvariantCulture) : string.Empty);
            AddValues(target, value.ExpectedCosts);
            AddValues(target, value.ExpectedEffects);
            AddStrings(target, value.Uncertainties);
            AddStrings(target, value.BlockReasonCodes);
            AddStrings(target, value.SourceStableIds);
        }

        private static void AddTask(StringBuilder target, SimulationTaskSnapshot value)
        {
            Add(target, value.TaskStableId);
            Add(target, value.TaskTypeCode);
            Add(target, value.StateCode);
            Add(target, value.Revision);
            Add(target, value.CausedByDecisionStableId);
            Add(target, value.FacilityStableId);
            if (!string.IsNullOrWhiteSpace(value.ActionCode)
                || !string.IsNullOrWhiteSpace(value.AssignedActorStableId))
            {
                Add(target, "NpcTaskBindingV1");
                Add(target, value.ActionCode);
                Add(target, value.AssignedActorStableId);
            }
            if (!string.IsNullOrWhiteSpace(value.SelectedSpatialStableId))
            {
                Add(target, "SimulationSpatialTaskBindingV1");
                Add(target, value.SelectedSpatialStableId);
                Add(target, value.SpatialDefinitionRevision);
                Add(target, value.SpatialDefinitionHashSha256);
            }
            if (value.SpatialRoleBindings.Length > 0)
            {
                Add(target, "SimulationSpatialTaskBindingsV2");
                Add(target, value.SpatialRoleBindings.Length);
                foreach (var binding in value.SpatialRoleBindings)
                {
                    Add(target, binding.RoleCode);
                    Add(target, binding.PreferredSpatialStableId);
                    Add(target, binding.SelectedSpatialStableId);
                    Add(target, binding.DefinitionRevision);
                    Add(target, binding.DefinitionHashSha256);
                    Add(target, binding.EvidenceKindCode);
                    AddStrings(target, binding.RequiredCapabilityCodes);
                    AddCapacities(target, binding.RequiredCapacities);
                    AddStrings(target, binding.BlockReasonCodes);
                }
            }
            Add(target, value.AssignedCapacity);
            Add(target, value.AssignedCapacityUnitCode);
            Add(target, value.ScheduledStartTick);
            Add(target, value.ExpectedEndTick);
            Add(target, value.ActualEndTick.HasValue ? value.ActualEndTick.Value.ToString(CultureInfo.InvariantCulture) : string.Empty);
            AddStrings(target, value.InputLotStableIds);
            AddStrings(target, value.OutputCandidateCodes);
            AddStrings(target, value.BlockReasonCodes);
            AddStrings(target, value.SourceStableIds);
        }

        private static void AddEffect(StringBuilder target, SimulationEffectRecord value)
        {
            Add(target, value.EffectStableId);
            Add(target, value.EffectTypeCode);
            Add(target, value.StateCode);
            Add(target, value.Revision);
            Add(target, value.AppliedTick.HasValue ? value.AppliedTick.Value.ToString(CultureInfo.InvariantCulture) : string.Empty);
            Add(target, value.CausedByDecisionStableId);
            Add(target, value.CausedByTaskStableId);
            Add(target, value.TargetLedgerStableId);
            Add(target, value.BeforeValue);
            Add(target, value.Delta);
            Add(target, value.AfterValue);
            Add(target, value.UnitCode);
            AddStrings(target, value.SourceStableIds);
        }

        private static void AddSettlement(
            StringBuilder target,
            SimulationSettlementEconomySnapshot? value)
        {
            Add(target, value == null ? "none" : "present");
            if (value == null) return;
            Add(target, value.SettlementStableId);
            Add(target, value.WorldTick);
            Add(target, value.Revision);
            Add(target, value.RuleRevision);
            Add(target, value.TreasuryBalance);
            Add(target, value.TreasuryReserved);
            Add(target, value.TreasuryAvailable);
            Add(target, value.CurrencyCode);
            Add(target, value.LaborCapacityTotal);
            Add(target, value.LaborReserved);
            Add(target, value.LaborAvailable);
            Add(target, value.StorageCapacity);
            Add(target, value.StorageOccupied);
            Add(target, value.StorageReserved);
            Add(target, value.StorageAvailable);
            Add(target, value.StorageUnitCode);
            Add(target, value.PopulationCount);
            Add(target, value.PopulationFoodDemandPerTick);
            Add(target, value.GarrisonCount);
            Add(target, value.GarrisonFoodDemandPerTick);
            Add(target, value.FoodReserveEquivalent);
            Add(target, value.FoodDemandPerTick);
            Add(target, value.FoodSecurityDays);
            Add(target, value.FoodEquivalentUnitCode);
            Add(target, value.FoodEquivalentRuleRevision);
            Add(target, value.FoodSecurityFormulaCode);
            Add(target, value.Districts.Length);
            foreach (var district in value.Districts)
            {
                Add(target, district.DistrictStableId);
                Add(target, district.DistrictTypeCode);
                AddStrings(target, district.SourceStableIds);
            }
            Add(target, value.Facilities.Length);
            foreach (var facility in value.Facilities)
            {
                Add(target, facility.FacilityStableId);
                Add(target, facility.FacilityTypeCode);
                Add(target, facility.DistrictStableId);
                AddStrings(target, facility.SourceStableIds);
            }
            Add(target, value.MarketSupplyByProduct.Length);
            foreach (var supply in value.MarketSupplyByProduct)
            {
                Add(target, supply.ProductStableId);
                Add(target, supply.Quantity);
                Add(target, supply.UnitCode);
                AddStrings(target, supply.SourceStableIds);
            }
            Add(target, value.ResidentConsumptionByProduct.Length);
            foreach (var consumption in value.ResidentConsumptionByProduct)
            {
                Add(target, consumption.ProductStableId);
                Add(target, consumption.Quantity);
                Add(target, consumption.UnitCode);
                Add(target, consumption.ConsumptionCount);
                AddStrings(target, consumption.SourceStableIds);
            }
            Add(target, value.ReserveStockLots.Length);
            foreach (var lot in value.ReserveStockLots)
            {
                Add(target, lot.StockLotStableId);
                Add(target, lot.ProductStableId);
                Add(target, lot.StorageFacilityStableId);
                Add(target, lot.Quantity);
                Add(target, lot.OutboundReservedQuantity);
                Add(target, lot.AvailableQuantity);
                Add(target, lot.UnitCode);
                Add(target, lot.FoodEquivalentQuantity);
                Add(target, lot.OutboundReservedFoodEquivalentQuantity);
                Add(target, lot.AvailableFoodEquivalentQuantity);
                AddStrings(target, lot.SourceStableIds);
            }
            Add(target, value.HarvestLotAllocations.Length);
            foreach (var allocation in value.HarvestLotAllocations)
            {
                Add(target, allocation.AllocationStableId);
                Add(target, allocation.HarvestLotStableId);
                Add(target, allocation.HarvestLotRevision);
                Add(target, allocation.ProductStableId);
                Add(target, allocation.Quantity);
                Add(target, allocation.UnitCode);
                Add(target, allocation.ChoiceCode);
                Add(target, allocation.NextWorkflowCode);
                Add(target, allocation.DecisionStableId);
                Add(target, allocation.TaskStableId);
                Add(target, allocation.FacilityStableId);
                Add(target, allocation.RequiredLabor);
                Add(target, allocation.TreasuryCost);
                Add(target, allocation.ProjectedRevenue.HasValue
                    ? allocation.ProjectedRevenue.Value.ToString(CultureInfo.InvariantCulture) : string.Empty);
                Add(target, allocation.StateCode);
                Add(target, allocation.ReservedTick);
                Add(target, allocation.AppliedTick.HasValue
                    ? allocation.AppliedTick.Value.ToString(CultureInfo.InvariantCulture) : string.Empty);
                Add(target, allocation.ReserveStockLotStableId ?? string.Empty);
                Add(target, allocation.StoredQuantity);
                Add(target, allocation.FoodEquivalentQuantity);
                Add(target, allocation.OutboundReservedQuantity);
                Add(target, allocation.AvailableQuantity);
                AddStrings(target, allocation.SourceStableIds);
            }
            AddStrings(target, value.ActiveTaskStableIds);
            AddStrings(target, value.SourceStableIds);
        }

        private static void AddValues(StringBuilder target, SimulationValueProjection[] values)
        {
            Add(target, values.Length);
            foreach (var value in values)
            {
                Add(target, value.ValueTypeCode);
                Add(target, value.TargetLedgerStableId);
                Add(target, value.BeforeValue);
                Add(target, value.Delta);
                Add(target, value.AfterValue);
                Add(target, value.UnitCode);
                AddStrings(target, value.SourceStableIds);
            }
        }

        private static void AddStrings(StringBuilder target, string[] values)
        {
            Add(target, values.Length);
            foreach (var value in values)
                Add(target, value);
        }

        private static void Add(StringBuilder target, object value)
        {
            var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
            target.Append(text.Length.ToString(CultureInfo.InvariantCulture));
            target.Append(':');
            target.Append(text);
            target.Append('|');
        }
    }
}
