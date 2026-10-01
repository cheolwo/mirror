using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Ssalddel.Simulation.Contracts;

namespace Ssalddel.Simulation.Domain
{
    public sealed partial class 경영SimulationSessionAggregate
    {
        private SimulationIntegratedWorldInitialStateRequest? integratedWorldCreationState;
        private readonly Dictionary<string, SimulationFacilityDefinitionRequest> integratedFacilityDefinitions =
            new(StringComparer.Ordinal);
        private readonly Dictionary<string, SimulationRuntimeFacilitySnapshot> integratedFacilities =
            new(StringComparer.Ordinal);
        private readonly Dictionary<string, SimulationIntegratedActorSnapshot> integratedActors =
            new(StringComparer.Ordinal);
        private readonly Dictionary<string, SimulationIntegratedLotSnapshot> integratedLots =
            new(StringComparer.Ordinal);
        private readonly Dictionary<string, SimulationManufacturingRecipeRequest> integratedRecipes =
            new(StringComparer.Ordinal);
        private readonly Dictionary<string, SimulationFacilityBlueprintRequest> integratedBlueprints =
            new(StringComparer.Ordinal);
        private readonly Dictionary<string, SimulationFacilityRestrictionSnapshot> integratedRestrictions =
            new(StringComparer.Ordinal);
        private readonly Dictionary<string, SimulationManufacturingJobSnapshot> integratedManufacturingJobs =
            new(StringComparer.Ordinal);
        private readonly Dictionary<string, SimulationConstructionProjectSnapshot> integratedConstructionProjects =
            new(StringComparer.Ordinal);
        private readonly Dictionary<string, SimulationFormationSnapshot> integratedFormations =
            new(StringComparer.Ordinal);
        private readonly Dictionary<string, SimulationActorCommitmentSnapshot> integratedCommitments =
            new(StringComparer.Ordinal);
        private readonly Dictionary<string, SimulationActorInjurySnapshot> integratedInjuries =
            new(StringComparer.Ordinal);
        private readonly Dictionary<string, SimulationIntegratedReservationSnapshot> integratedReservations =
            new(StringComparer.Ordinal);
        private readonly Dictionary<string, SimulationWorldEffectSnapshot> integratedWorldEffects =
            new(StringComparer.Ordinal);
        private readonly Dictionary<string, SimulationPendingWorldEffectEntrySnapshot> integratedPendingEffects =
            new(StringComparer.Ordinal);
        private readonly Dictionary<string, SimulationAppliedWorldEffectReceiptSnapshot> integratedAppliedEffects =
            new(StringComparer.Ordinal);
        private readonly Dictionary<string, SimulationFacilityRepairJobSnapshot> integratedRepairJobs =
            new(StringComparer.Ordinal);
        private readonly Dictionary<string, SimulationIntegratedCargoMovementSnapshot> integratedCargoMovements =
            new(StringComparer.Ordinal);
        private readonly Dictionary<string, AppliedIntegratedWorldCommand> appliedIntegratedWorldCommands =
            new(StringComparer.Ordinal);

        private void InitializeIntegratedWorld(SimulationIntegratedWorldInitialStateRequest? request)
        {
            if (request == null) return;
            RequireStableId(request.ScenarioRevision, "SimulationIntegratedWorldScenarioRevisionInvalid");
            RequireStableId(request.ScenarioHashSha256, "SimulationIntegratedWorldScenarioHashInvalid");
            integratedWorldCreationState = CloneIntegratedWorldInitialState(request);
            InitializeConstructionPlacementZones(request.ConstructionPlacementZones);

            foreach (var definition in request.FacilityDefinitions.OrderBy(value =>
                         value.FacilityDefinitionStableId, StringComparer.Ordinal))
            {
                RequireStableId(definition.FacilityDefinitionStableId,
                    "SimulationFacilityDefinitionStableIdInvalid");
                RequireStableId(definition.Revision, "SimulationFacilityDefinitionRevisionInvalid");
                RequireStableId(definition.HashSha256, "SimulationFacilityDefinitionHashInvalid");
                ValidateFacilityCapacities(definition.Capacities);
                if (!integratedFacilityDefinitions.TryAdd(definition.FacilityDefinitionStableId,
                        CloneFacilityDefinition(definition)))
                    throw new SimulationContractException("SimulationFacilityDefinitionDuplicate");
            }

            foreach (var seed in request.FacilitySeeds.OrderBy(value => value.FacilityStableId,
                         StringComparer.Ordinal))
            {
                RequireStableId(seed.FacilityStableId, "SimulationRuntimeFacilityStableIdInvalid");
                if (!integratedFacilityDefinitions.TryGetValue(seed.FacilityDefinitionStableId,
                        out var definition))
                    throw new SimulationContractException("SimulationFacilityDefinitionNotFound");
                if (!integratedFacilities.TryAdd(seed.FacilityStableId,
                        CreateRuntimeFacility(seed.FacilityStableId, definition,
                            seed.PlacementH1StableId, seed.AccessConnectorStableIds,
                            SimulationFacilityLifecycleCodes.Operational)))
                    throw new SimulationContractException("SimulationRuntimeFacilityDuplicate");
            }

            foreach (var actor in request.Actors.OrderBy(value => value.ActorStableId,
                         StringComparer.Ordinal))
            {
                RequireStableId(actor.ActorStableId, "SimulationIntegratedActorStableIdInvalid");
                if (!integratedActors.TryAdd(actor.ActorStableId, new SimulationIntegratedActorSnapshot
                    {
                        ActorStableId = actor.ActorStableId.Trim(),
                        EligibilityRank = actor.EligibilityRank,
                        FarmLaborEligible = actor.FarmLaborEligible,
                    }))
                    throw new SimulationContractException("SimulationIntegratedActorDuplicate");
            }

            foreach (var lot in request.Lots.OrderBy(value => value.LotStableId,
                         StringComparer.Ordinal))
            {
                ValidateLotSeed(lot);
                if (!integratedLots.TryAdd(lot.LotStableId, new SimulationIntegratedLotSnapshot
                    {
                        LotStableId = lot.LotStableId.Trim(),
                        ItemCode = lot.ItemCode.Trim(),
                        Quantity = lot.Quantity,
                        UnitCode = lot.UnitCode.Trim(),
                        FacilityStableId = lot.FacilityStableId.Trim(),
                        SourceStableId = "scenario:" + request.ScenarioRevision.Trim(),
                    }))
                    throw new SimulationContractException("SimulationIntegratedLotDuplicate");
            }

            foreach (var recipe in request.ManufacturingRecipes.OrderBy(value =>
                         value.RecipeStableId, StringComparer.Ordinal))
            {
                ValidateRecipe(recipe);
                if (!integratedRecipes.TryAdd(recipe.RecipeStableId, CloneRecipe(recipe)))
                    throw new SimulationContractException("SimulationManufacturingRecipeDuplicate");
            }

            foreach (var blueprint in request.FacilityBlueprints.OrderBy(value =>
                         value.BlueprintStableId, StringComparer.Ordinal))
            {
                ValidateBlueprint(blueprint);
                if (!integratedFacilityDefinitions.TryGetValue(
                        blueprint.FacilityDefinitionStableId, out var definition))
                    throw new SimulationContractException(
                        "SimulationFacilityBlueprintDefinitionNotFound");
                ValidateSettlementFacilityProjection(blueprint, definition);
                if (!integratedBlueprints.TryAdd(blueprint.BlueprintStableId,
                        CloneBlueprint(blueprint)))
                    throw new SimulationContractException("SimulationFacilityBlueprintDuplicate");
            }
        }

        public SimulationIntegratedWorldPreviewSnapshot PreviewIntegratedWorldCommand(
            SimulationIntegratedWorldCommandRequest request)
        {
            ValidateIntegratedCommand(request);
            lock (gate)
            {
                return BuildIntegratedWorldPreview(request);
            }
        }

        public 경영SimulationSessionSnapshot ConfirmIntegratedWorldCommand(
            SimulationIntegratedWorldCommandRequest request)
        {
            ValidateIntegratedCommand(request);
            lock (gate)
            {
                var fingerprint = BuildIntegratedCommandFingerprint(request);
                if (appliedIntegratedWorldCommands.TryGetValue(request.CommandId, out var applied))
                {
                    if (!string.Equals(applied.Fingerprint, fingerprint, StringComparison.Ordinal))
                        throw new SimulationConflictException("SimulationCommandPayloadConflict");
                    return Clone(applied.Snapshot);
                }
                if (HasDifferentKindCommand(request.CommandId))
                    throw new SimulationConflictException(
                        "SimulationCommandKindConflict");
                if (request.ExpectedRevision != Revision)
                    throw new SimulationConflictException("SimulationExpectedRevisionMismatch");

                var preview = BuildIntegratedWorldPreview(request);
                if (!preview.CanConfirm)
                    throw new SimulationConflictException(preview.BlockingReasonCodes[0]);

                ApplyIntegratedWorldCommand(request, preview);
                Revision++;
                AppendIntegratedWorldCommand(request);
                var snapshot = CreateSnapshot();
                appliedIntegratedWorldCommands.Add(request.CommandId,
                    new AppliedIntegratedWorldCommand(fingerprint, Clone(snapshot)));
                RecordAppliedConstructionPlacement(request, snapshot);
                return snapshot;
            }
        }

        public 경영SimulationSessionSnapshot QueueFacilityBattleDamage(
            string battleStableId,
            string facilityStableId,
            string severityCode)
        {
            RequireStableId(battleStableId, "SimulationBattleStableIdInvalid");
            RequireStableId(facilityStableId, "SimulationRuntimeFacilityStableIdInvalid");
            RequireStableId(severityCode, "SimulationFacilityDamageSeverityInvalid");
            lock (gate)
            {
                if (!integratedFacilities.ContainsKey(facilityStableId.Trim()))
                    throw new SimulationNotFoundException("SimulationRuntimeFacilityNotFound");
                var effectId = "world-effect:damage:" + battleStableId.Trim() + ":" +
                               facilityStableId.Trim();
                if (integratedWorldEffects.ContainsKey(effectId)) return CreateSnapshot();
                integratedWorldEffects.Add(effectId, new SimulationWorldEffectSnapshot
                {
                    EffectStableId = effectId,
                    EffectCode = SimulationIntegratedWorldEffectCodes.FacilityBattleDamage,
                    SourceStableId = battleStableId.Trim(),
                    TargetStableId = facilityStableId.Trim(),
                    PayloadCanonical = severityCode.Trim(),
                });
                integratedPendingEffects.Add(effectId,
                    new SimulationPendingWorldEffectEntrySnapshot
                    {
                        EffectStableId = effectId,
                        EarliestWorldTick = CurrentTick + 1,
                    });
                Revision++;
                AppendIntegratedWorldEffectEnqueued(battleStableId.Trim(),
                    facilityStableId.Trim(), severityCode.Trim());
                return CreateSnapshot();
            }
        }

        public SimulationBattleRelevantRuntimeProjectionSnapshot
            CreateBattleRelevantRuntimeProjection(string encounterScopeStableId,
                IEnumerable<string> facilityStableIds, IEnumerable<string> formationStableIds)
        {
            RequireStableId(encounterScopeStableId, "SimulationBattleEncounterScopeInvalid");
            if (facilityStableIds == null || formationStableIds == null)
                throw new ArgumentNullException(nameof(facilityStableIds));
            lock (gate)
            {
                var facilities = facilityStableIds.Distinct(StringComparer.Ordinal)
                    .Where(integratedFacilities.ContainsKey)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .Select(value => CreateRuntimeFacilitySnapshot(integratedFacilities[value]))
                    .ToArray();
                var formations = formationStableIds.Distinct(StringComparer.Ordinal)
                    .Where(integratedFormations.ContainsKey)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .Select(value => CloneFormation(integratedFormations[value])).ToArray();
                var actors = formations.SelectMany(value => value.MemberActorStableIds)
                    .Distinct(StringComparer.Ordinal).Where(IsActorBattleAvailable)
                    .OrderBy(value => value, StringComparer.Ordinal).ToArray();
                var projection = new SimulationBattleRelevantRuntimeProjectionSnapshot
                {
                    EncounterScopeStableId = encounterScopeStableId.Trim(),
                    Facilities = facilities,
                    Formations = formations,
                    BattleAvailableActorStableIds = actors,
                };
                projection.BattleRelevantOverlayHashSha256 = HashIntegrated(
                    CanonicalBattleProjection(projection));
                return projection;
            }
        }

        public SimulationBattleRelevantRuntimeProjectionSnapshot
            CreateBattleRelevantRuntimeProjectionForArea(string encounterScopeStableId,
                string areaRoleCode)
        {
            RequireStableId(areaRoleCode, "SimulationBattleAreaRoleInvalid");
            lock (gate)
            {
                var facilities = integratedFacilities.Values.Where(value =>
                        value.PlacementH1StableId.Contains(areaRoleCode,
                            StringComparison.OrdinalIgnoreCase))
                    .Select(value => value.FacilityStableId).ToArray();
                var facilitySet = new HashSet<string>(facilities, StringComparer.Ordinal);
                var formations = integratedFormations.Values.Where(value =>
                        value.GarrisonFacilityStableId.Length > 0
                        && facilitySet.Contains(value.GarrisonFacilityStableId))
                    .Select(value => value.FormationStableId).ToArray();
                return CreateBattleRelevantRuntimeProjection(encounterScopeStableId,
                    facilities, formations);
            }
        }

        public 경영SimulationSessionSnapshot LockFormationForBattle(
            string formationStableId, string battleStableId, long expectedRevision)
        {
            lock (gate)
            {
                if (expectedRevision != Revision)
                    throw new SimulationConflictException("SimulationExpectedRevisionMismatch");
                if (!integratedFormations.TryGetValue(formationStableId, out var formation)
                    || formation.StateCode != SimulationFormationStateCodes.Ready)
                    throw new SimulationConflictException("SimulationFormationNotReady");
                foreach (var actorId in formation.MemberActorStableIds)
                {
                    if (!IsActorBattleAvailable(actorId))
                        throw new SimulationConflictException("SimulationFormationActorUnavailable");
                    AddCommitment(actorId, SimulationActorCommitmentCodes.BattleLock,
                        battleStableId);
                }
                formation.StateCode = SimulationFormationStateCodes.BattleLocked;
                Revision++;
                return CreateSnapshot();
            }
        }

        public 경영SimulationSessionSnapshot ReleaseFormationFromBattle(
            string formationStableId, string battleStableId,
            IEnumerable<string> injuredActorStableIds)
        {
            lock (gate)
            {
                if (!integratedFormations.TryGetValue(formationStableId, out var formation)
                    || formation.StateCode != SimulationFormationStateCodes.BattleLocked)
                    throw new SimulationConflictException("SimulationFormationNotBattleLocked");
                var injured = new HashSet<string>(injuredActorStableIds ?? Array.Empty<string>(),
                    StringComparer.Ordinal);
                foreach (var actorId in formation.MemberActorStableIds)
                {
                    ReleaseCommitment(actorId, SimulationActorCommitmentCodes.BattleLock,
                        battleStableId);
                    if (!injured.Contains(actorId)) continue;
                    var effectId = "world-effect:injury:" + battleStableId.Trim() + ":" + actorId;
                    integratedWorldEffects.TryAdd(effectId, new SimulationWorldEffectSnapshot
                    {
                        EffectStableId = effectId,
                        EffectCode = SimulationIntegratedWorldEffectCodes.ActorInjury,
                        SourceStableId = battleStableId.Trim(),
                        TargetStableId = actorId,
                        PayloadCanonical = "Active",
                    });
                    integratedInjuries.TryAdd("injury:" + battleStableId.Trim() + ":" + actorId,
                        new SimulationActorInjurySnapshot
                        {
                            InjuryStableId = "injury:" + battleStableId.Trim() + ":" + actorId,
                            ActorStableId = actorId,
                            SourceEffectStableId = effectId,
                            Active = true,
                        });
                }
                formation.StateCode = SimulationFormationStateCodes.Ready;
                Revision++;
                return CreateSnapshot();
            }
        }

        private SimulationIntegratedWorldPreviewSnapshot BuildIntegratedWorldPreview(
            SimulationIntegratedWorldCommandRequest request)
        {
            var blocks = new List<string>();
            var actors = Array.Empty<string>();
            var facilityId = string.Empty;
            var lots = Array.Empty<string>();
            switch (request.ActionCode)
            {
                case SimulationIntegratedWorldActionCodes.ManufacturingOrder:
                    PreviewManufacturing(request.Manufacturing!, blocks, out actors,
                        out facilityId, out lots);
                    break;
                case SimulationIntegratedWorldActionCodes.ConstructionOrder:
                    PreviewConstruction(request.Construction!, blocks, out actors, out lots);
                    break;
                case SimulationIntegratedWorldActionCodes.Recruitment:
                    actors = SelectAvailableActors(request.Recruitment!.ActorCount, true);
                    if (actors.Length != request.Recruitment.ActorCount)
                        blocks.Add("SimulationRecruitmentActorInsufficient");
                    facilityId = SelectFacility(string.Empty,
                        SimulationIntegratedCapabilityCodes.Recruitment);
                    if (facilityId.Length == 0)
                        blocks.Add("SimulationRecruitmentFacilityUnavailable");
                    break;
                case SimulationIntegratedWorldActionCodes.Training:
                    PreviewTraining(request.Training!, blocks, out facilityId);
                    break;
                case SimulationIntegratedWorldActionCodes.FormationDeployment:
                    PreviewDeployment(request.FormationDeployment!, blocks);
                    facilityId = request.FormationDeployment!.GarrisonFacilityStableId;
                    break;
                case SimulationIntegratedWorldActionCodes.FacilityRepair:
                    PreviewRepair(request.FacilityRepair!, blocks, out actors, out lots);
                    facilityId = request.FacilityRepair!.FacilityStableId;
                    break;
                case SimulationIntegratedWorldActionCodes.PotatoPackaging:
                    PreviewPackaging(request.PotatoPackaging!, blocks, out actors, out lots);
                    break;
                case SimulationIntegratedWorldActionCodes.CargoTransfer:
                    PreviewCargoTransfer(request.CargoTransfer!, blocks, out actors, out lots);
                    facilityId = request.CargoTransfer!.TargetFacilityStableId;
                    break;
                default:
                    blocks.Add("SimulationIntegratedWorldActionUnsupported");
                    break;
            }
            var result = new SimulationIntegratedWorldPreviewSnapshot
            {
                ActionCode = request.ActionCode,
                SourceWorldRevision = Revision,
                CanConfirm = blocks.Count == 0,
                BlockingReasonCodes = blocks.Distinct(StringComparer.Ordinal).ToArray(),
                SelectedActorStableIds = actors,
                SelectedFacilityStableId = facilityId,
                ReservedLotStableIds = lots,
            };
            result.PreviewHashSha256 = HashIntegrated(string.Join("|", result.ActionCode,
                result.SourceWorldRevision, string.Join(",", actors), facilityId,
                string.Join(",", lots), string.Join(",", result.BlockingReasonCodes)));
            return result;
        }

        private void PreviewManufacturing(SimulationManufacturingOrderPayload payload,
            List<string> blocks, out string[] actors, out string facilityId, out string[] lots)
        {
            actors = SelectAvailableActors(1, false);
            if (actors.Length == 0) blocks.Add("SimulationManufacturingActorUnavailable");
            facilityId = SelectFacility(payload.PreferredManufacturingFacilityStableId,
                SimulationIntegratedCapabilityCodes.ManufacturingWorkArea);
            if (facilityId.Length == 0) blocks.Add("SimulationManufacturingFacilityUnavailable");
            if (!integratedRecipes.TryGetValue(payload.RecipeStableId, out var recipe))
            {
                blocks.Add("SimulationManufacturingRecipeNotFound");
                lots = Array.Empty<string>();
                return;
            }
            lots = SelectLots(recipe.Inputs, facilityId, blocks).ToArray();
        }

        private void PreviewConstruction(SimulationConstructionOrderPayload payload,
            List<string> blocks, out string[] actors, out string[] lots)
        {
            if (integratedFacilities.Values.Any(value =>
                    value.PlacementH1StableId == payload.BuildSiteH1StableId)
                || integratedConstructionProjects.Values.Any(value =>
                    value.BuildSiteH1StableId == payload.BuildSiteH1StableId
                    && value.StateCode != SimulationConstructionProjectStateCodes.Cancelled))
                blocks.Add("SimulationConstructionBuildSiteOccupied");
            if (!integratedBlueprints.TryGetValue(payload.BlueprintStableId, out var blueprint))
            {
                blocks.Add("SimulationFacilityBlueprintNotFound");
                actors = Array.Empty<string>();
                lots = Array.Empty<string>();
                return;
            }
            actors = IsContinuousPlacementKind(blueprint.PlacementKindCode)
                ? SelectContinuousPlacementActors()
                : SelectAvailableActors(1, false);
            if (actors.Length == 0) blocks.Add("SimulationConstructionActorUnavailable");
            AppendDynamicConstructionPlacementBlocks(payload, blueprint, blocks);
            AppendRegionalDevelopmentPlacementBlocks(payload, blocks);
            lots = SelectLots(blueprint.Materials, string.Empty, blocks).ToArray();
        }

        private void PreviewTraining(SimulationTrainingPayload payload, List<string> blocks,
            out string facilityId)
        {
            facilityId = SelectFacility(string.Empty, SimulationIntegratedCapabilityCodes.Training);
            if (facilityId.Length == 0) blocks.Add("SimulationTrainingFacilityUnavailable");
            if (!integratedFormations.TryGetValue(payload.FormationStableId, out var formation)
                || formation.StateCode != SimulationFormationStateCodes.Recruited)
                blocks.Add("SimulationFormationNotRecruited");
            else if (formation.MemberActorStableIds.Any(IsActorInjured))
                blocks.Add("SimulationFormationActorUnavailable");
        }

        private void PreviewDeployment(SimulationFormationDeploymentPayload payload,
            List<string> blocks)
        {
            if (!integratedFormations.TryGetValue(payload.FormationStableId, out var formation)
                || formation.StateCode != SimulationFormationStateCodes.Trained)
                blocks.Add("SimulationFormationNotTrained");
            if (!FacilityHasActiveCapability(payload.GarrisonFacilityStableId,
                    SimulationIntegratedCapabilityCodes.Garrison))
                blocks.Add("SimulationGarrisonFacilityUnavailable");
        }

        private void PreviewRepair(SimulationFacilityRepairPayload payload, List<string> blocks,
            out string[] actors, out string[] lots)
        {
            actors = SelectAvailableActors(1, false);
            if (actors.Length == 0) blocks.Add("SimulationFacilityRepairActorUnavailable");
            if (!integratedFacilities.TryGetValue(payload.FacilityStableId, out var facility)
                || facility.LifecycleCode != SimulationFacilityLifecycleCodes.Operational)
                blocks.Add("SimulationRuntimeFacilityNotFound");
            if (!integratedRestrictions.Values.Any(value =>
                    value.FacilityStableId == payload.FacilityStableId
                    && value.ResolvedByEffectStableId.Length == 0
                    && integratedWorldEffects.TryGetValue(value.SourceEffectStableId, out var source)
                    && source.EffectCode == SimulationIntegratedWorldEffectCodes.FacilityBattleDamage))
                blocks.Add("SimulationFacilityDamageRestrictionUnavailable");
            lots = SelectLots(new[] { new SimulationIntegratedItemRequirement
            {
                ItemCode = SimulationIntegratedItemCodes.FacilityComponent,
                Quantity = 1m,
                UnitCode = "unit",
            } }, string.Empty, blocks).ToArray();
        }

        private void PreviewPackaging(SimulationPotatoPackagingPayload payload,
            List<string> blocks, out string[] actors, out string[] lots)
        {
            actors = SelectAvailableActors(1, false);
            if (actors.Length == 0) blocks.Add("SimulationPackagingActorUnavailable");
            if (payload.PackagingPolicyCode != "IntegratedWorldPotatoPackagingPolicy.v2")
                blocks.Add("SimulationPackagingPolicyUnsupported");
            if (!LotAvailable(payload.PotatoLotStableId, SimulationIntegratedItemCodes.HarvestPotato,
                    payload.PotatoQuantity)) blocks.Add("SimulationPotatoLotInsufficient");
            if (!LotAvailable(payload.TransportBoxLotStableId,
                    SimulationIntegratedItemCodes.TransportBox, payload.BoxQuantity))
                blocks.Add("SimulationTransportBoxLotInsufficient");
            lots = new[] { payload.PotatoLotStableId, payload.TransportBoxLotStableId };
        }

        private void PreviewCargoTransfer(SimulationCargoTransferPayload payload,
            List<string> blocks, out string[] actors, out string[] lots)
        {
            actors = SelectAvailableActors(1, false);
            if (actors.Length == 0) blocks.Add("SimulationCargoTransferActorUnavailable");
            if (!integratedFacilities.ContainsKey(payload.TargetFacilityStableId))
                blocks.Add("SimulationCargoTransferTargetFacilityNotFound");
            if (!integratedLots.ContainsKey(payload.SourceLotStableId)
                || payload.Quantity <= 0m
                || AvailableLotQuantity(payload.SourceLotStableId) < payload.Quantity)
                blocks.Add("SimulationCargoTransferLotInsufficient");
            if (payload.TransportTicks <= 0) blocks.Add("SimulationCargoTransferTicksInvalid");
            lots = new[] { payload.SourceLotStableId };
        }

        private void ApplyIntegratedWorldCommand(SimulationIntegratedWorldCommandRequest request,
            SimulationIntegratedWorldPreviewSnapshot preview)
        {
            switch (request.ActionCode)
            {
                case SimulationIntegratedWorldActionCodes.ManufacturingOrder:
                    StartManufacturing(request, preview);
                    break;
                case SimulationIntegratedWorldActionCodes.ConstructionOrder:
                    StartConstruction(request, preview);
                    break;
                case SimulationIntegratedWorldActionCodes.Recruitment:
                    StartRecruitment(request, preview);
                    break;
                case SimulationIntegratedWorldActionCodes.Training:
                    StartTraining(request);
                    break;
                case SimulationIntegratedWorldActionCodes.FormationDeployment:
                    DeployFormation(request);
                    break;
                case SimulationIntegratedWorldActionCodes.FacilityRepair:
                    StartFacilityRepair(request, preview);
                    break;
                case SimulationIntegratedWorldActionCodes.PotatoPackaging:
                    StartPotatoPackaging(request, preview);
                    break;
                case SimulationIntegratedWorldActionCodes.CargoTransfer:
                    StartCargoTransfer(request, preview);
                    break;
            }
        }

        private void StartManufacturing(SimulationIntegratedWorldCommandRequest request,
            SimulationIntegratedWorldPreviewSnapshot preview)
        {
            var recipe = integratedRecipes[request.Manufacturing!.RecipeStableId];
            var jobId = "manufacturing-job:" + request.CommandId.Trim();
            var job = new SimulationManufacturingJobSnapshot
            {
                ManufacturingJobStableId = jobId,
                RecipeStableId = recipe.RecipeStableId,
                RecipeRevision = recipe.Revision,
                RecipeHashSha256 = recipe.HashSha256,
                StateCode = SimulationManufacturingJobStateCodes.Reserved,
                ProcessingStartsAtTick = CurrentTick + 1,
                ProcessingCompletesAtTick = CurrentTick + recipe.ProcessingTicks,
                ResolvedInputRequirements = CloneRequirements(recipe.Inputs),
                ResolvedOutputSpecification = CloneRequirements(recipe.Outputs),
                ReservedInputLotStableIds = preview.ReservedLotStableIds.ToArray(),
                ActorStableId = preview.SelectedActorStableIds[0],
                FacilityStableId = preview.SelectedFacilityStableId,
            };
            integratedManufacturingJobs.Add(jobId, job);
            ReserveRequirements(jobId, recipe.Inputs, preview.ReservedLotStableIds);
            AddCommitment(job.ActorStableId, SimulationActorCommitmentCodes.SimulationTask, jobId);
        }

        private void StartConstruction(SimulationIntegratedWorldCommandRequest request,
            SimulationIntegratedWorldPreviewSnapshot preview)
        {
            var payload = request.Construction!;
            var blueprint = integratedBlueprints[payload.BlueprintStableId];
            var projectId = "construction-project:" + request.CommandId.Trim();
            var facilityId = "facility:player-built:" + request.CommandId.Trim();
            var actorStableId = preview.SelectedActorStableIds[0];
            var startsAtTick = CurrentTick + 1;
            if (IsContinuousPlacementKind(blueprint.PlacementKindCode))
            {
                var queuedCompletion = integratedConstructionProjects.Values
                    .Where(value => value.ActorStableId == actorStableId
                                    && value.StateCode !=
                                    SimulationConstructionProjectStateCodes.Completed
                                    && value.StateCode !=
                                    SimulationConstructionProjectStateCodes.Cancelled)
                    .Select(value => value.ConstructionCompletesAtTick)
                    .DefaultIfEmpty(CurrentTick)
                    .Max();
                startsAtTick = Math.Max(startsAtTick, queuedCompletion + 1);
            }
            integratedConstructionProjects.Add(projectId, new SimulationConstructionProjectSnapshot
            {
                ConstructionProjectStableId = projectId,
                BlueprintStableId = blueprint.BlueprintStableId,
                BlueprintRevision = blueprint.Revision,
                BlueprintHashSha256 = blueprint.HashSha256,
                StateCode = SimulationConstructionProjectStateCodes.Planned,
                TargetFacilityStableId = facilityId,
                BuildSiteH1StableId = payload.BuildSiteH1StableId,
                PlacementProposalStableId = payload.PlacementProposalStableId,
                PlacementPreviewHashSha256 = payload.PlacementPreviewHashSha256,
                PlacementZoneStableId = payload.PlacementZoneStableId,
                TargetH2StableId = payload.TargetH2StableId,
                PlacementKindCode = payload.PlacementKindCode,
                LocalXCentimeters = payload.LocalXCentimeters,
                LocalZCentimeters = payload.LocalZCentimeters,
                RotationQuarterTurns = payload.RotationQuarterTurns,
                PlacementProfileRevision = payload.PlacementProfileRevision,
                FenceChainStableId = payload.FenceChainStableId,
                DevelopmentOpportunityStableId =
                    payload.DevelopmentOpportunityStableId,
                ConstructionStartsAtTick = startsAtTick,
                ConstructionCompletesAtTick = startsAtTick
                    + blueprint.ConstructionTicks - 1,
                ResolvedMaterialRequirements = CloneRequirements(blueprint.Materials),
                ReservedMaterialLotStableIds = preview.ReservedLotStableIds.ToArray(),
                ActorStableId = actorStableId,
            });
            ReserveRegionalDevelopmentOpportunityForConstruction(payload, projectId);
            var definition = integratedFacilityDefinitions[blueprint.FacilityDefinitionStableId];
            var facility = CreateRuntimeFacility(facilityId, definition,
                payload.BuildSiteH1StableId, Array.Empty<string>(),
                SimulationFacilityLifecycleCodes.Planned,
                blueprint.SettlementFacilityTypeCode,
                blueprint.SettlementDistrictStableId);
            ApplyDynamicPlacement(facility, payload);
            integratedFacilities.Add(facilityId, facility);
            ReserveRequirements(projectId, blueprint.Materials, preview.ReservedLotStableIds);
            AddReservation(projectId, payload.BuildSiteH1StableId, "BuildSite", 1m);
            AddCommitment(actorStableId,
                SimulationActorCommitmentCodes.SimulationTask, projectId);
        }

        private string[] SelectContinuousPlacementActors()
        {
            var queuedActor = integratedConstructionProjects.Values
                .Where(value => value.StateCode !=
                                SimulationConstructionProjectStateCodes.Completed
                                && value.StateCode !=
                                SimulationConstructionProjectStateCodes.Cancelled
                                && IsContinuousPlacementKind(value.PlacementKindCode))
                .OrderBy(value => value.ConstructionStartsAtTick)
                .ThenBy(value => value.ConstructionProjectStableId,
                    StringComparer.Ordinal)
                .Select(value => value.ActorStableId)
                .FirstOrDefault(value => integratedActors.ContainsKey(value)
                                         && !IsActorInjured(value));
            return string.IsNullOrWhiteSpace(queuedActor)
                ? SelectAvailableActors(1, false)
                : new[] { queuedActor };
        }

        private static bool IsContinuousPlacementKind(string kind)
            => kind == SimulationConstructionPlacementKindCodes.FenceSegment
               || kind == SimulationConstructionPlacementKindCodes.FenceCorner
               || kind == SimulationConstructionPlacementKindCodes.Tree;

        private void StartRecruitment(SimulationIntegratedWorldCommandRequest request,
            SimulationIntegratedWorldPreviewSnapshot preview)
        {
            var formationId = "formation:" + request.CommandId.Trim();
            integratedFormations.Add(formationId, new SimulationFormationSnapshot
            {
                FormationStableId = formationId,
                StateCode = SimulationFormationStateCodes.Recruiting,
                MemberActorStableIds = preview.SelectedActorStableIds.ToArray(),
                StateCompletesAtTick = CurrentTick + 1,
            });
            foreach (var actorId in preview.SelectedActorStableIds)
                AddCommitment(actorId, SimulationActorCommitmentCodes.FormationDuty, formationId);
        }

        private void StartTraining(SimulationIntegratedWorldCommandRequest request)
        {
            var payload = request.Training!;
            var formation = integratedFormations[payload.FormationStableId];
            foreach (var actorId in formation.MemberActorStableIds)
                AddCommitment(actorId, SimulationActorCommitmentCodes.Training,
                    formation.FormationStableId);
            formation.StateCode = SimulationFormationStateCodes.Training;
            formation.StateCompletesAtTick = CurrentTick + payload.TrainingTicks;
        }

        private void DeployFormation(SimulationIntegratedWorldCommandRequest request)
        {
            var payload = request.FormationDeployment!;
            var formation = integratedFormations[payload.FormationStableId];
            formation.GarrisonFacilityStableId = payload.GarrisonFacilityStableId;
            formation.StateCode = SimulationFormationStateCodes.Ready;
            formation.StateCompletesAtTick = null;
        }

        private void StartFacilityRepair(SimulationIntegratedWorldCommandRequest request,
            SimulationIntegratedWorldPreviewSnapshot preview)
        {
            var payload = request.FacilityRepair!;
            var jobId = "facility-repair-job:" + request.CommandId.Trim();
            var targets = integratedRestrictions.Values.Where(value =>
                    value.FacilityStableId == payload.FacilityStableId
                    && value.ResolvedByEffectStableId.Length == 0
                    && integratedWorldEffects.TryGetValue(value.SourceEffectStableId, out var source)
                    && source.EffectCode == SimulationIntegratedWorldEffectCodes.FacilityBattleDamage)
                .OrderBy(value => value.RestrictionStableId, StringComparer.Ordinal)
                .Select(value => value.RestrictionStableId).ToArray();
            integratedRepairJobs.Add(jobId, new SimulationFacilityRepairJobSnapshot
            {
                RepairJobStableId = jobId,
                FacilityStableId = payload.FacilityStableId,
                ActorStableId = preview.SelectedActorStableIds[0],
                TargetRestrictionStableIds = targets,
                ReservedMaterialLotStableIds = preview.ReservedLotStableIds.ToArray(),
                CompletesAtTick = CurrentTick + payload.RepairTicks,
            });
            ReserveRequirements(jobId, new[] { new SimulationIntegratedItemRequirement
            {
                ItemCode = SimulationIntegratedItemCodes.FacilityComponent,
                Quantity = 1m,
                UnitCode = "unit",
            } }, preview.ReservedLotStableIds);
            AddCommitment(preview.SelectedActorStableIds[0],
                SimulationActorCommitmentCodes.SimulationTask, jobId);
            integratedFacilities[payload.FacilityStableId].MaintenanceCode =
                SimulationFacilityMaintenanceCodes.Repairing;
        }

        private void StartPotatoPackaging(SimulationIntegratedWorldCommandRequest request,
            SimulationIntegratedWorldPreviewSnapshot preview)
        {
            var payload = request.PotatoPackaging!;
            var jobId = "packaging-job:" + request.CommandId.Trim();
            var potato = integratedLots[payload.PotatoLotStableId];
            integratedManufacturingJobs.Add(jobId, new SimulationManufacturingJobSnapshot
            {
                ManufacturingJobStableId = jobId,
                RecipeStableId = payload.PackagingPolicyCode,
                RecipeRevision = "v2",
                RecipeHashSha256 = HashIntegrated(payload.PackagingPolicyCode),
                StateCode = SimulationManufacturingJobStateCodes.Reserved,
                ProcessingStartsAtTick = CurrentTick + 1,
                ProcessingCompletesAtTick = CurrentTick + 1,
                ResolvedInputRequirements = new[]
                {
                    new SimulationIntegratedItemRequirement { ItemCode = potato.ItemCode,
                        Quantity = payload.PotatoQuantity, UnitCode = potato.UnitCode },
                    new SimulationIntegratedItemRequirement { ItemCode = SimulationIntegratedItemCodes.TransportBox,
                        Quantity = payload.BoxQuantity, UnitCode = "unit" },
                },
                ResolvedOutputSpecification = new[]
                {
                    new SimulationIntegratedItemRequirement { ItemCode = SimulationIntegratedItemCodes.PackagedPotato,
                        Quantity = payload.PotatoQuantity, UnitCode = potato.UnitCode },
                },
                ReservedInputLotStableIds = preview.ReservedLotStableIds.ToArray(),
                ActorStableId = preview.SelectedActorStableIds[0],
                FacilityStableId = potato.FacilityStableId,
            });
            ReserveExactLot(jobId, payload.PotatoLotStableId, payload.PotatoQuantity);
            ReserveExactLot(jobId, payload.TransportBoxLotStableId, payload.BoxQuantity);
            AddCommitment(preview.SelectedActorStableIds[0],
                SimulationActorCommitmentCodes.SimulationTask, jobId);
        }

        private void StartCargoTransfer(SimulationIntegratedWorldCommandRequest request,
            SimulationIntegratedWorldPreviewSnapshot preview)
        {
            var payload = request.CargoTransfer!;
            var movementId = "cargo-movement:" + request.CommandId.Trim();
            integratedCargoMovements.Add(movementId, new SimulationIntegratedCargoMovementSnapshot
            {
                MovementStableId = movementId,
                SourceLotStableId = payload.SourceLotStableId,
                TargetFacilityStableId = payload.TargetFacilityStableId,
                Quantity = payload.Quantity,
                ActorStableId = preview.SelectedActorStableIds[0],
                CompletesAtTick = CurrentTick + payload.TransportTicks,
                OutputLotStableId = movementId + ":output:00",
            });
            ReserveExactLot(movementId, payload.SourceLotStableId, payload.Quantity);
            AddCommitment(preview.SelectedActorStableIds[0],
                SimulationActorCommitmentCodes.SimulationTask, movementId);
        }

        private void AdvanceIntegratedWorld(int currentTick)
        {
            foreach (var job in integratedManufacturingJobs.Values.OrderBy(value =>
                         value.ManufacturingJobStableId, StringComparer.Ordinal))
            {
                if (job.StateCode == SimulationManufacturingJobStateCodes.Reserved
                    && currentTick >= job.ProcessingStartsAtTick)
                    job.StateCode = SimulationManufacturingJobStateCodes.Processing;
                if (job.StateCode == SimulationManufacturingJobStateCodes.Processing
                    && currentTick >= job.ProcessingCompletesAtTick)
                {
                    ConsumeReservations(job.ManufacturingJobStableId);
                    job.ConsumedInputLotStableIds = job.ReservedInputLotStableIds.ToArray();
                    job.StateCode = SimulationManufacturingJobStateCodes.AwaitingInspection;
                }
                else if (job.StateCode == SimulationManufacturingJobStateCodes.AwaitingInspection
                         && currentTick > job.ProcessingCompletesAtTick)
                {
                    var outputs = new List<string>();
                    for (var index = 0; index < job.ResolvedOutputSpecification.Length; index++)
                    {
                        var output = job.ResolvedOutputSpecification[index];
                        var lotId = job.ManufacturingJobStableId + ":output:" + index
                            .ToString("D2", CultureInfo.InvariantCulture);
                        if (!integratedLots.ContainsKey(lotId))
                            integratedLots.Add(lotId, new SimulationIntegratedLotSnapshot
                            {
                                LotStableId = lotId,
                                ItemCode = output.ItemCode,
                                Quantity = output.Quantity,
                                UnitCode = output.UnitCode,
                                FacilityStableId = job.FacilityStableId,
                                SourceStableId = job.ManufacturingJobStableId,
                            });
                        outputs.Add(lotId);
                    }
                    job.OutputLotStableIds = outputs.ToArray();
                    job.StateCode = SimulationManufacturingJobStateCodes.Completed;
                    ReleaseCommitment(job.ActorStableId,
                        SimulationActorCommitmentCodes.SimulationTask,
                        job.ManufacturingJobStableId);
                }
            }

            foreach (var project in integratedConstructionProjects.Values.OrderBy(value =>
                         value.ConstructionProjectStableId, StringComparer.Ordinal))
            {
                if (project.StateCode == SimulationConstructionProjectStateCodes.Planned
                    && currentTick >= project.ConstructionStartsAtTick)
                {
                    ConsumeReservations(project.ConstructionProjectStableId);
                    project.ConsumedMaterialLotStableIds = project.ReservedMaterialLotStableIds.ToArray();
                    project.StateCode = SimulationConstructionProjectStateCodes.Building;
                    integratedFacilities[project.TargetFacilityStableId].LifecycleCode =
                        SimulationFacilityLifecycleCodes.UnderConstruction;
                }
                if (project.StateCode == SimulationConstructionProjectStateCodes.Building
                    && currentTick >= project.ConstructionCompletesAtTick)
                {
                    project.StateCode = SimulationConstructionProjectStateCodes.Completed;
                    integratedFacilities[project.TargetFacilityStableId].LifecycleCode =
                        SimulationFacilityLifecycleCodes.Operational;
                    CompleteRegionalDevelopmentConstruction(project);
                    ReleaseCommitment(project.ActorStableId,
                        SimulationActorCommitmentCodes.SimulationTask,
                        project.ConstructionProjectStableId);
                    FinalizeBuildSiteReservation(project.ConstructionProjectStableId,
                        project.TargetFacilityStableId);
                }
            }

            foreach (var formation in integratedFormations.Values.OrderBy(value =>
                         value.FormationStableId, StringComparer.Ordinal))
            {
                if (!formation.StateCompletesAtTick.HasValue
                    || currentTick < formation.StateCompletesAtTick.Value) continue;
                if (formation.StateCode == SimulationFormationStateCodes.Recruiting)
                {
                    formation.StateCode = SimulationFormationStateCodes.Recruited;
                    formation.StateCompletesAtTick = null;
                }
                else if (formation.StateCode == SimulationFormationStateCodes.Training)
                {
                    foreach (var actorId in formation.MemberActorStableIds)
                        ReleaseCommitment(actorId, SimulationActorCommitmentCodes.Training,
                            formation.FormationStableId);
                    formation.StateCode = SimulationFormationStateCodes.Trained;
                    formation.StateCompletesAtTick = null;
                }
            }

            ApplyPendingIntegratedWorldEffects(currentTick);
            CompleteIntegratedRepairJobs(currentTick);
            CompleteIntegratedCargoMovements(currentTick);
        }

        private void CompleteIntegratedCargoMovements(int currentTick)
        {
            foreach (var movement in integratedCargoMovements.Values.Where(value =>
                         value.StateCode == "InTransit" && value.CompletesAtTick <= currentTick)
                     .OrderBy(value => value.MovementStableId, StringComparer.Ordinal))
            {
                var source = integratedLots[movement.SourceLotStableId];
                ConsumeReservations(movement.MovementStableId);
                integratedLots.TryAdd(movement.OutputLotStableId,
                    new SimulationIntegratedLotSnapshot
                    {
                        LotStableId = movement.OutputLotStableId,
                        ItemCode = source.ItemCode,
                        Quantity = movement.Quantity,
                        UnitCode = source.UnitCode,
                        FacilityStableId = movement.TargetFacilityStableId,
                        SourceStableId = movement.MovementStableId,
                    });
                movement.StateCode = "Completed";
                ReleaseCommitment(movement.ActorStableId,
                    SimulationActorCommitmentCodes.SimulationTask,
                    movement.MovementStableId);
            }
        }

        private void ApplyPendingIntegratedWorldEffects(int currentTick)
        {
            foreach (var pending in integratedPendingEffects.Values.Where(value =>
                         value.EarliestWorldTick <= currentTick)
                     .OrderBy(value => value.EffectStableId, StringComparer.Ordinal).ToArray())
            {
                if (integratedAppliedEffects.ContainsKey(pending.EffectStableId))
                {
                    integratedPendingEffects.Remove(pending.EffectStableId);
                    continue;
                }
                var effect = integratedWorldEffects[pending.EffectStableId];
                if (effect.EffectCode == SimulationIntegratedWorldEffectCodes.FacilityBattleDamage)
                    ApplyFacilityDamage(effect);
                integratedAppliedEffects.Add(effect.EffectStableId,
                    new SimulationAppliedWorldEffectReceiptSnapshot
                    {
                        EffectStableId = effect.EffectStableId,
                        AppliedWorldTick = currentTick,
                        AppliedWorldRevision = Revision,
                    });
                integratedPendingEffects.Remove(effect.EffectStableId);
            }
        }

        private void ApplyFacilityDamage(SimulationWorldEffectSnapshot effect)
        {
            var facility = integratedFacilities[effect.TargetStableId];
            facility.IntegrityCode = effect.PayloadCanonical == "Destroyed"
                ? SimulationFacilityIntegrityCodes.Disabled
                : SimulationFacilityIntegrityCodes.Damaged;
            AddRestriction(effect, SimulationIntegratedCapabilityCodes.CargoAccessible,
                SimulationFacilityCapabilityStateCodes.Restricted, 0);
            AddRestriction(effect, SimulationIntegratedCapabilityCodes.LoadingWorkArea,
                SimulationFacilityCapabilityStateCodes.Suspended, 0);
        }

        private void CompleteIntegratedRepairJobs(int currentTick)
        {
            foreach (var job in integratedRepairJobs.Values.Where(value =>
                         value.StateCode == "Repairing" && value.CompletesAtTick <= currentTick)
                     .OrderBy(value => value.RepairJobStableId, StringComparer.Ordinal))
            {
                ConsumeReservations(job.RepairJobStableId);
                var effectId = "world-effect:repair:" + job.RepairJobStableId;
                integratedWorldEffects.TryAdd(effectId, new SimulationWorldEffectSnapshot
                {
                    EffectStableId = effectId,
                    EffectCode = SimulationIntegratedWorldEffectCodes.FacilityRepair,
                    SourceStableId = job.RepairJobStableId,
                    TargetStableId = job.FacilityStableId,
                    PayloadCanonical = string.Join(",", job.TargetRestrictionStableIds),
                });
                foreach (var restrictionId in job.TargetRestrictionStableIds)
                    if (integratedRestrictions.TryGetValue(restrictionId, out var restriction)
                        && restriction.ResolvedByEffectStableId.Length == 0)
                        restriction.ResolvedByEffectStableId = effectId;
                var facility = integratedFacilities[job.FacilityStableId];
                var remainingDamage = integratedRestrictions.Values.Any(value =>
                    value.FacilityStableId == job.FacilityStableId
                    && value.ResolvedByEffectStableId.Length == 0
                    && integratedWorldEffects[value.SourceEffectStableId].EffectCode ==
                    SimulationIntegratedWorldEffectCodes.FacilityBattleDamage);
                facility.IntegrityCode = remainingDamage
                    ? SimulationFacilityIntegrityCodes.Damaged
                    : SimulationFacilityIntegrityCodes.Intact;
                facility.MaintenanceCode = SimulationFacilityMaintenanceCodes.None;
                job.StateCode = "Completed";
                ReleaseCommitment(job.ActorStableId,
                    SimulationActorCommitmentCodes.SimulationTask, job.RepairJobStableId);
            }
        }

        private void AddRestriction(SimulationWorldEffectSnapshot effect, string capability,
            string level, int ordinal)
        {
            var id = "restriction:" + effect.EffectStableId + ":" + capability + ":" + ordinal;
            integratedRestrictions.TryAdd(id, new SimulationFacilityRestrictionSnapshot
            {
                RestrictionStableId = id,
                SourceEffectStableId = effect.EffectStableId,
                FacilityStableId = effect.TargetStableId,
                CapabilityCode = capability,
                RestrictionLevelCode = level,
            });
        }

        private void AddCommitment(string actorId, string code, string sourceId)
        {
            if (!integratedActors.ContainsKey(actorId))
                throw new SimulationNotFoundException("SimulationIntegratedActorNotFound");
            if (!CommitmentCompatible(actorId, code, sourceId))
                throw new SimulationConflictException("SimulationActorCommitmentConflict");
            var id = "actor-commitment:" + actorId + ":" + code + ":" + sourceId;
            if (!integratedCommitments.TryAdd(id, new SimulationActorCommitmentSnapshot
                {
                    CommitmentStableId = id,
                    ActorStableId = actorId,
                    CommitmentCode = code,
                    SourceStableId = sourceId,
                    Active = true,
                }))
                throw new SimulationConflictException("SimulationActorCommitmentDuplicate");
        }

        private bool CommitmentCompatible(string actorId, string requested,
            string sourceId)
        {
            var activeCommitments = integratedCommitments.Values.Where(value =>
                value.Active && value.ActorStableId == actorId).ToArray();
            var active = activeCommitments.Select(value => value.CommitmentCode).ToArray();
            if (requested == SimulationActorCommitmentCodes.FormationDuty)
                return !active.Contains(SimulationActorCommitmentCodes.FormationDuty,
                    StringComparer.Ordinal);
            if (requested == SimulationActorCommitmentCodes.Training)
                return !active.Contains(SimulationActorCommitmentCodes.SimulationTask,
                           StringComparer.Ordinal)
                       && !active.Contains(SimulationActorCommitmentCodes.BattleLock,
                           StringComparer.Ordinal);
            if (requested == SimulationActorCommitmentCodes.BattleLock)
                return !active.Contains(SimulationActorCommitmentCodes.SimulationTask,
                           StringComparer.Ordinal)
                       && !active.Contains(SimulationActorCommitmentCodes.Training,
                           StringComparer.Ordinal);
            if (requested == SimulationActorCommitmentCodes.SimulationTask
                && IsContinuousConstructionProject(sourceId))
                return activeCommitments.All(value =>
                    value.CommitmentCode == SimulationActorCommitmentCodes.FormationDuty
                    || value.CommitmentCode ==
                    SimulationActorCommitmentCodes.SimulationTask
                    && IsContinuousConstructionProject(value.SourceStableId));
            return active.All(value => value == SimulationActorCommitmentCodes.FormationDuty);
        }

        private bool IsContinuousConstructionProject(string sourceId)
            => integratedConstructionProjects.TryGetValue(sourceId, out var project)
               && IsContinuousPlacementKind(project.PlacementKindCode);

        private void ReleaseCommitment(string actorId, string code, string sourceId)
        {
            foreach (var commitment in integratedCommitments.Values.Where(value => value.Active
                         && value.ActorStableId == actorId && value.CommitmentCode == code
                         && value.SourceStableId == sourceId))
                commitment.Active = false;
        }

        private string[] SelectAvailableActors(int count, bool farmOnly)
            => integratedActors.Values.Where(value => (!farmOnly || value.FarmLaborEligible)
                    && !IsActorInjured(value.ActorStableId)
                    && !integratedCommitments.Values.Any(commitment => commitment.Active
                        && commitment.ActorStableId == value.ActorStableId))
                .OrderBy(value => value.EligibilityRank)
                .ThenBy(value => value.ActorStableId, StringComparer.Ordinal)
                .Take(count).Select(value => value.ActorStableId).ToArray();

        private bool IsActorInjured(string actorId) => integratedInjuries.Values.Any(value =>
            value.ActorStableId == actorId && value.Active);

        private bool IsActorBattleAvailable(string actorId)
            => !IsActorInjured(actorId) && !integratedCommitments.Values.Any(value => value.Active
                && value.ActorStableId == actorId
                && value.CommitmentCode != SimulationActorCommitmentCodes.FormationDuty);

        private string SelectFacility(string preferred, string capability)
        {
            if (!string.IsNullOrWhiteSpace(preferred))
                return FacilityHasActiveCapability(preferred, capability) ? preferred.Trim() : string.Empty;
            return integratedFacilities.Values.OrderBy(value => value.FacilityStableId,
                    StringComparer.Ordinal).FirstOrDefault(value =>
                    FacilityHasActiveCapability(value.FacilityStableId, capability))
                ?.FacilityStableId ?? string.Empty;
        }

        private bool FacilityHasActiveCapability(string facilityId, string capability)
            => integratedFacilities.TryGetValue(facilityId, out var facility)
               && EffectiveCapabilityState(facility, capability) ==
               SimulationFacilityCapabilityStateCodes.Active;

        private string EffectiveCapabilityState(SimulationRuntimeFacilitySnapshot facility,
            string capability)
        {
            if (!facility.DefinedCapabilityCodes.Contains(capability, StringComparer.Ordinal))
                return SimulationFacilityCapabilityStateCodes.Suspended;
            if (facility.LifecycleCode != SimulationFacilityLifecycleCodes.Operational)
                return SimulationFacilityCapabilityStateCodes.Suspended;
            if (facility.IntegrityCode == SimulationFacilityIntegrityCodes.Disabled)
                return SimulationFacilityCapabilityStateCodes.Suspended;
            var levels = integratedRestrictions.Values.Where(value =>
                    value.FacilityStableId == facility.FacilityStableId
                    && value.CapabilityCode == capability
                    && value.ResolvedByEffectStableId.Length == 0)
                .Select(value => value.RestrictionLevelCode).ToArray();
            if (levels.Contains(SimulationFacilityCapabilityStateCodes.Suspended,
                    StringComparer.Ordinal)) return SimulationFacilityCapabilityStateCodes.Suspended;
            if (levels.Contains(SimulationFacilityCapabilityStateCodes.Restricted,
                    StringComparer.Ordinal)) return SimulationFacilityCapabilityStateCodes.Restricted;
            return SimulationFacilityCapabilityStateCodes.Active;
        }

        private List<string> SelectLots(IEnumerable<SimulationIntegratedItemRequirement> requirements,
            string facilityId, List<string> blocks)
        {
            var selected = new List<string>();
            foreach (var requirement in requirements)
            {
                var remaining = requirement.Quantity;
                foreach (var lot in integratedLots.Values.Where(value =>
                             value.ItemCode == requirement.ItemCode
                             && (facilityId.Length == 0 || value.FacilityStableId == facilityId))
                         .OrderBy(value => value.LotStableId, StringComparer.Ordinal))
                {
                    var available = AvailableLotQuantity(lot.LotStableId);
                    if (available <= 0m) continue;
                    selected.Add(lot.LotStableId);
                    remaining -= Math.Min(remaining, available);
                    if (remaining <= 0m) break;
                }
                if (remaining > 0m) blocks.Add("SimulationIntegratedInventoryInsufficient");
            }
            return selected.Distinct(StringComparer.Ordinal).ToList();
        }

        private void ReserveRequirements(string owner,
            IEnumerable<SimulationIntegratedItemRequirement> requirements,
            IEnumerable<string> selectedLots)
        {
            var candidates = selectedLots.Select(value => integratedLots[value]).ToArray();
            foreach (var requirement in requirements)
            {
                var remaining = requirement.Quantity;
                foreach (var lot in candidates.Where(value => value.ItemCode == requirement.ItemCode)
                             .OrderBy(value => value.LotStableId, StringComparer.Ordinal))
                {
                    var quantity = Math.Min(remaining, AvailableLotQuantity(lot.LotStableId));
                    if (quantity <= 0m) continue;
                    ReserveExactLot(owner, lot.LotStableId, quantity);
                    remaining -= quantity;
                    if (remaining <= 0m) break;
                }
                if (remaining > 0m) throw new SimulationConflictException(
                    "SimulationIntegratedInventoryInsufficient");
            }
        }

        private void ReserveExactLot(string owner, string lotId, decimal quantity)
        {
            if (AvailableLotQuantity(lotId) < quantity)
                throw new SimulationConflictException("SimulationIntegratedInventoryInsufficient");
            AddReservation(owner, lotId, "Lot", quantity);
        }

        private void AddReservation(string owner, string target, string kind, decimal quantity)
        {
            var ordinal = integratedReservations.Values.Count(value => value.OwnerStableId == owner);
            var id = "integrated-reservation:" + owner + ":" + ordinal
                .ToString("D3", CultureInfo.InvariantCulture);
            integratedReservations.Add(id, new SimulationIntegratedReservationSnapshot
            {
                ReservationStableId = id,
                OwnerStableId = owner,
                TargetStableId = target,
                ReservationKindCode = kind,
                Quantity = quantity,
            });
        }

        private void ConsumeReservations(string owner)
        {
            foreach (var reservation in integratedReservations.Values.Where(value =>
                         value.OwnerStableId == owner && value.StateCode == "Reserved")
                     .OrderBy(value => value.ReservationStableId, StringComparer.Ordinal))
            {
                if (reservation.ReservationKindCode == "Lot")
                {
                    var lot = integratedLots[reservation.TargetStableId];
                    lot.Quantity -= reservation.Quantity;
                    if (lot.Quantity < 0m)
                        throw new SimulationConflictException("SimulationIntegratedInventoryNegative");
                    reservation.StateCode = "Consumed";
                }
            }
        }

        private void FinalizeBuildSiteReservation(string owner, string facilityId)
        {
            foreach (var reservation in integratedReservations.Values.Where(value =>
                         value.OwnerStableId == owner && value.ReservationKindCode == "BuildSite"
                         && value.StateCode == "Reserved"))
            {
                reservation.StateCode = "Consumed";
                reservation.OwnerStableId = facilityId;
                reservation.ReservationKindCode = "FacilityOccupancy";
            }
        }

        private decimal AvailableLotQuantity(string lotId)
        {
            if (!integratedLots.TryGetValue(lotId, out var lot)) return 0m;
            var reserved = integratedReservations.Values.Where(value =>
                value.TargetStableId == lotId && value.StateCode == "Reserved")
                .Sum(value => value.Quantity);
            return lot.Quantity - reserved;
        }

        private bool LotAvailable(string lotId, string itemCode, decimal quantity)
            => integratedLots.TryGetValue(lotId, out var lot) && lot.ItemCode == itemCode
               && quantity > 0m && AvailableLotQuantity(lotId) >= quantity;

        private SimulationIntegratedWorldSnapshot CreateIntegratedWorldSnapshot()
            => new()
            {
                ScenarioRevision = integratedWorldCreationState?.ScenarioRevision ?? string.Empty,
                ScenarioHashSha256 = integratedWorldCreationState?.ScenarioHashSha256 ?? string.Empty,
                Facilities = integratedFacilities.Values.OrderBy(value => value.FacilityStableId,
                    StringComparer.Ordinal).Select(CreateRuntimeFacilitySnapshot).ToArray(),
                FacilityRestrictions = integratedRestrictions.Values.OrderBy(value =>
                    value.RestrictionStableId, StringComparer.Ordinal).Select(CloneRestriction).ToArray(),
                ManufacturingJobs = integratedManufacturingJobs.Values.OrderBy(value =>
                    value.ManufacturingJobStableId, StringComparer.Ordinal).Select(CloneManufacturingJob).ToArray(),
                ConstructionProjects = integratedConstructionProjects.Values.OrderBy(value =>
                    value.ConstructionProjectStableId, StringComparer.Ordinal).Select(CloneConstructionProject).ToArray(),
                Actors = integratedActors.Values.OrderBy(value => value.ActorStableId,
                    StringComparer.Ordinal).Select(CloneIntegratedActor).ToArray(),
                Formations = integratedFormations.Values.OrderBy(value => value.FormationStableId,
                    StringComparer.Ordinal).Select(CloneFormation).ToArray(),
                ActorCommitments = integratedCommitments.Values.OrderBy(value =>
                    value.CommitmentStableId, StringComparer.Ordinal).Select(CloneCommitment).ToArray(),
                ActorInjuries = integratedInjuries.Values.OrderBy(value => value.InjuryStableId,
                    StringComparer.Ordinal).Select(CloneInjury).ToArray(),
                Lots = integratedLots.Values.OrderBy(value => value.LotStableId,
                    StringComparer.Ordinal).Select(CloneLot).ToArray(),
                Reservations = integratedReservations.Values.OrderBy(value =>
                    value.ReservationStableId, StringComparer.Ordinal).Select(CloneIntegratedReservation).ToArray(),
                WorldEffects = integratedWorldEffects.Values.OrderBy(value => value.EffectStableId,
                    StringComparer.Ordinal).Select(CloneWorldEffect).ToArray(),
                PendingWorldEffects = integratedPendingEffects.Values.OrderBy(value =>
                    value.EffectStableId, StringComparer.Ordinal).Select(ClonePendingEffect).ToArray(),
                AppliedWorldEffectReceipts = integratedAppliedEffects.Values.OrderBy(value =>
                    value.EffectStableId, StringComparer.Ordinal).Select(CloneAppliedEffect).ToArray(),
                RepairJobs = integratedRepairJobs.Values.OrderBy(value => value.RepairJobStableId,
                    StringComparer.Ordinal).Select(CloneRepairJob).ToArray(),
                CargoMovements = integratedCargoMovements.Values.OrderBy(value => value.MovementStableId,
                    StringComparer.Ordinal).Select(CloneCargoMovement).ToArray(),
            };

        private SimulationRuntimeFacilitySnapshot CreateRuntimeFacilitySnapshot(
            SimulationRuntimeFacilitySnapshot value)
        {
            var clone = CloneRuntimeFacility(value);
            clone.EffectiveCapabilities = clone.DefinedCapabilityCodes.OrderBy(code => code,
                    StringComparer.Ordinal).Select(code => new SimulationEffectiveFacilityCapabilitySnapshot
                {
                    CapabilityCode = code,
                    StateCode = EffectiveCapabilityState(value, code),
                    SourceRestrictionStableIds = integratedRestrictions.Values.Where(restriction =>
                            restriction.FacilityStableId == value.FacilityStableId
                            && restriction.CapabilityCode == code
                            && restriction.ResolvedByEffectStableId.Length == 0)
                        .OrderBy(restriction => restriction.RestrictionStableId,
                            StringComparer.Ordinal)
                        .Select(restriction => restriction.RestrictionStableId).ToArray(),
                }).ToArray();
            return clone;
        }

        private static void ValidateIntegratedCommand(SimulationIntegratedWorldCommandRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            RequireStableId(request.ActionCode, "SimulationIntegratedWorldActionCodeInvalid");
            RequireStableId(request.CommandId, "SimulationCommandIdInvalid");
            if (request.ExpectedRevision < 0)
                throw new SimulationContractException("SimulationExpectedRevisionInvalid");
            var payloadCount = new object?[] { request.Manufacturing, request.Construction,
                request.Recruitment, request.Training, request.FormationDeployment,
                request.FacilityRepair, request.PotatoPackaging,
                request.CargoTransfer }.Count(value => value != null);
            if (payloadCount != 1) throw new SimulationContractException(
                "SimulationIntegratedWorldCommandPayloadInvalid");
        }

        internal static string BuildIntegratedCommandFingerprint(
            SimulationIntegratedWorldCommandRequest request)
        {
            var construction = request.Construction;
            if (construction == null || string.IsNullOrWhiteSpace(
                    construction.PlacementProposalStableId))
                return HashIntegrated(string.Join("|", request.ActionCode,
                    request.Manufacturing?.RecipeStableId,
                    request.Manufacturing?.PreferredManufacturingFacilityStableId,
                    construction?.BlueprintStableId, construction?.BuildSiteH1StableId,
                    request.Recruitment?.RecruitmentPolicyStableId, request.Recruitment?.ActorCount,
                    request.Training?.FormationStableId, request.Training?.TrainingTicks,
                    request.FormationDeployment?.FormationStableId,
                    request.FormationDeployment?.GarrisonFacilityStableId,
                    request.FacilityRepair?.FacilityStableId, request.FacilityRepair?.RepairTicks,
                    request.PotatoPackaging?.PotatoLotStableId,
                    request.PotatoPackaging?.TransportBoxLotStableId,
                    request.PotatoPackaging?.PotatoQuantity,
                    request.PotatoPackaging?.BoxQuantity,
                    request.PotatoPackaging?.PackagingPolicyCode,
                    request.CargoTransfer?.SourceLotStableId,
                    request.CargoTransfer?.TargetFacilityStableId,
                    request.CargoTransfer?.Quantity,
                    request.CargoTransfer?.TransportTicks));

            var canonical = string.Join("|", request.ActionCode,
                request.Manufacturing?.RecipeStableId,
                request.Manufacturing?.PreferredManufacturingFacilityStableId,
                construction.BlueprintStableId, construction.BuildSiteH1StableId,
                construction.PlacementProposalStableId,
                construction.PlacementPreviewHashSha256,
                construction.PlacementZoneStableId,
                construction.TargetH2StableId,
                construction.PlacementKindCode,
                construction.LocalXCentimeters,
                construction.LocalZCentimeters,
                construction.RotationQuarterTurns,
                construction.AccessConnectorStableId,
                construction.FenceChainStableId,
                construction.PlacementProfileRevision,
                request.Recruitment?.RecruitmentPolicyStableId, request.Recruitment?.ActorCount,
                request.Training?.FormationStableId, request.Training?.TrainingTicks,
                request.FormationDeployment?.FormationStableId,
                request.FormationDeployment?.GarrisonFacilityStableId,
                request.FacilityRepair?.FacilityStableId, request.FacilityRepair?.RepairTicks,
                request.PotatoPackaging?.PotatoLotStableId,
                request.PotatoPackaging?.TransportBoxLotStableId,
                request.PotatoPackaging?.PotatoQuantity,
                request.PotatoPackaging?.BoxQuantity,
                request.PotatoPackaging?.PackagingPolicyCode,
                request.CargoTransfer?.SourceLotStableId,
                request.CargoTransfer?.TargetFacilityStableId,
                request.CargoTransfer?.Quantity,
                request.CargoTransfer?.TransportTicks);
            if (!string.IsNullOrWhiteSpace(construction.DevelopmentOpportunityStableId))
                canonical += "|" + construction.DevelopmentOpportunityStableId;
            return HashIntegrated(canonical);
        }

        internal static string BuildIntegratedWorldInitialFingerprint(
            SimulationIntegratedWorldInitialStateRequest? value)
        {
            if (value == null) return string.Empty;
            var hasPlacementProfile = value.ConstructionPlacementZones.Length > 0 ||
                                      value.FacilityBlueprints.Any(item =>
                                          !string.IsNullOrWhiteSpace(item.PlacementKindCode));
            var definitions = string.Join(";", value.FacilityDefinitions.OrderBy(item =>
                item.FacilityDefinitionStableId, StringComparer.Ordinal).Select(item =>
                string.Join(",", item.FacilityDefinitionStableId, item.Revision,
                    item.HashSha256, item.FacilityTypeCode,
                    string.Join("+", item.CapabilityCodes.OrderBy(code => code,
                        StringComparer.Ordinal)),
                    string.Join("+", item.Capacities.OrderBy(capacity =>
                        capacity.CapacityCode, StringComparer.Ordinal).Select(capacity =>
                        string.Join("=", capacity.CapacityCode, capacity.Quantity,
                            capacity.UnitCode))))));
            var blueprints = string.Join(";", value.FacilityBlueprints.OrderBy(item =>
                item.BlueprintStableId, StringComparer.Ordinal).Select(item =>
                hasPlacementProfile
                    ? string.Join(",", item.BlueprintStableId, item.Revision,
                        item.HashSha256, item.FacilityDefinitionStableId,
                        item.SettlementFacilityTypeCode,
                        item.SettlementDistrictStableId, item.ConstructionTicks,
                        item.PlacementKindCode, item.FootprintWidthCentimeters,
                        item.FootprintDepthCentimeters, item.ClearanceCentimeters,
                        item.MaxSlopeMilliDegrees, item.RequiresRoadAccess,
                        string.Join("+", item.AllowedPlacementZoneTypeCodes
                            .OrderBy(code => code, StringComparer.Ordinal)))
                    : string.Join(",", item.BlueprintStableId, item.Revision,
                        item.HashSha256, item.FacilityDefinitionStableId,
                        item.SettlementFacilityTypeCode,
                        item.SettlementDistrictStableId, item.ConstructionTicks)));
            if (!hasPlacementProfile)
                return HashIntegrated(string.Join("|", value.ScenarioRevision,
                    value.ScenarioHashSha256, definitions, blueprints,
                    value.FacilitySeeds.Length, value.Actors.Length, value.Lots.Length,
                    value.ManufacturingRecipes.Length));

            var zones = string.Join(";", value.ConstructionPlacementZones.OrderBy(item =>
                item.PlacementZoneStableId, StringComparer.Ordinal).Select(item =>
                string.Join(",", item.PlacementZoneStableId, item.TargetH2StableId,
                    item.ZoneTypeCode, item.PlacementProfileRevision,
                    item.MinXCentimeters, item.MaxXCentimeters,
                    item.MinZCentimeters, item.MaxZCentimeters,
                    item.TerrainSlopeMilliDegrees, item.FenceChainStableId,
                    item.FenceStartXCentimeters, item.FenceStartZCentimeters,
                    string.Join("+", item.RoadAccessConnectorStableIds
                        .OrderBy(code => code, StringComparer.Ordinal)))));
            return HashIntegrated(string.Join("|", value.ScenarioRevision,
                value.ScenarioHashSha256, definitions, blueprints, zones,
                value.FacilitySeeds.Length, value.Actors.Length, value.Lots.Length,
                value.ManufacturingRecipes.Length));
        }

        internal static SimulationIntegratedWorldCommandRequest CloneIntegratedWorldCommand(
            SimulationIntegratedWorldCommandRequest value) => new()
        {
            ActionCode = value.ActionCode,
            CommandId = value.CommandId,
            ExpectedRevision = value.ExpectedRevision,
            Manufacturing = value.Manufacturing == null ? null : new SimulationManufacturingOrderPayload
            {
                RecipeStableId = value.Manufacturing.RecipeStableId,
                PreferredManufacturingFacilityStableId = value.Manufacturing.PreferredManufacturingFacilityStableId,
            },
            Construction = value.Construction == null ? null : new SimulationConstructionOrderPayload
            {
                BlueprintStableId = value.Construction.BlueprintStableId,
                BuildSiteH1StableId = value.Construction.BuildSiteH1StableId,
                PlacementProposalStableId = value.Construction.PlacementProposalStableId,
                PlacementPreviewHashSha256 = value.Construction.PlacementPreviewHashSha256,
                PlacementZoneStableId = value.Construction.PlacementZoneStableId,
                TargetH2StableId = value.Construction.TargetH2StableId,
                PlacementKindCode = value.Construction.PlacementKindCode,
                LocalXCentimeters = value.Construction.LocalXCentimeters,
                LocalZCentimeters = value.Construction.LocalZCentimeters,
                RotationQuarterTurns = value.Construction.RotationQuarterTurns,
                AccessConnectorStableId = value.Construction.AccessConnectorStableId,
                FenceChainStableId = value.Construction.FenceChainStableId,
                PlacementProfileRevision = value.Construction.PlacementProfileRevision,
                DevelopmentOpportunityStableId =
                    value.Construction.DevelopmentOpportunityStableId,
            },
            Recruitment = value.Recruitment == null ? null : new SimulationRecruitmentPayload
            {
                RecruitmentPolicyStableId = value.Recruitment.RecruitmentPolicyStableId,
                ActorCount = value.Recruitment.ActorCount,
            },
            Training = value.Training == null ? null : new SimulationTrainingPayload
            {
                FormationStableId = value.Training.FormationStableId,
                TrainingTicks = value.Training.TrainingTicks,
            },
            FormationDeployment = value.FormationDeployment == null ? null
                : new SimulationFormationDeploymentPayload
                {
                    FormationStableId = value.FormationDeployment.FormationStableId,
                    GarrisonFacilityStableId = value.FormationDeployment.GarrisonFacilityStableId,
                },
            FacilityRepair = value.FacilityRepair == null ? null : new SimulationFacilityRepairPayload
            {
                FacilityStableId = value.FacilityRepair.FacilityStableId,
                RepairTicks = value.FacilityRepair.RepairTicks,
            },
            PotatoPackaging = value.PotatoPackaging == null ? null : new SimulationPotatoPackagingPayload
            {
                PotatoLotStableId = value.PotatoPackaging.PotatoLotStableId,
                TransportBoxLotStableId = value.PotatoPackaging.TransportBoxLotStableId,
                PotatoQuantity = value.PotatoPackaging.PotatoQuantity,
                BoxQuantity = value.PotatoPackaging.BoxQuantity,
                PackagingPolicyCode = value.PotatoPackaging.PackagingPolicyCode,
            },
            CargoTransfer = value.CargoTransfer == null ? null : new SimulationCargoTransferPayload
            {
                SourceLotStableId = value.CargoTransfer.SourceLotStableId,
                TargetFacilityStableId = value.CargoTransfer.TargetFacilityStableId,
                Quantity = value.CargoTransfer.Quantity,
                TransportTicks = value.CargoTransfer.TransportTicks,
            },
        };

        private static string CanonicalBattleProjection(
            SimulationBattleRelevantRuntimeProjectionSnapshot value)
            => string.Join("|", value.EncounterScopeStableId,
                string.Join(";", value.Facilities.Select(facility => string.Join(",",
                    facility.FacilityStableId, facility.LifecycleCode, facility.IntegrityCode,
                    facility.MaintenanceCode, string.Join("+", facility.EffectiveCapabilities
                        .Select(capability => capability.CapabilityCode + "=" + capability.StateCode))))),
                string.Join(";", value.Formations.Select(formation => string.Join(",",
                    formation.FormationStableId, formation.StateCode,
                    formation.GarrisonFacilityStableId,
                    string.Join("+", formation.MemberActorStableIds)))),
                string.Join(";", value.BattleAvailableActorStableIds));

        private static string HashIntegrated(string value)
        {
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value)))
                .Replace("-", string.Empty).ToLowerInvariant();
        }

        private static SimulationRuntimeFacilitySnapshot CreateRuntimeFacility(string id,
            SimulationFacilityDefinitionRequest definition, string placement,
            IEnumerable<string> connectors, string lifecycle,
            string settlementFacilityTypeCode = "",
            string settlementDistrictStableId = "")
            => new()
            {
                FacilityStableId = id.Trim(),
                FacilityDefinitionStableId = definition.FacilityDefinitionStableId,
                FacilityDefinitionRevision = definition.Revision,
                FacilityDefinitionHashSha256 = definition.HashSha256,
                PlacementH1StableId = placement?.Trim() ?? string.Empty,
                SettlementFacilityTypeCode = settlementFacilityTypeCode?.Trim() ?? string.Empty,
                SettlementDistrictStableId = settlementDistrictStableId?.Trim() ?? string.Empty,
                AccessConnectorStableIds = connectors?.OrderBy(value => value,
                    StringComparer.Ordinal).ToArray() ?? Array.Empty<string>(),
                LifecycleCode = lifecycle,
                IntegrityCode = SimulationFacilityIntegrityCodes.Intact,
                MaintenanceCode = SimulationFacilityMaintenanceCodes.None,
                DefinedCapabilityCodes = definition.CapabilityCodes.OrderBy(value => value,
                    StringComparer.Ordinal).ToArray(),
                DefinedCapacities = definition.Capacities
                    .OrderBy(value => value.CapacityCode, StringComparer.Ordinal)
                    .Select(value => new SimulationRuntimeFacilityCapacitySnapshot
                    {
                        CapacityCode = value.CapacityCode.Trim(),
                        Quantity = value.Quantity,
                        UnitCode = value.UnitCode.Trim(),
                    }).ToArray(),
            };

        private static void ValidateFacilityCapacities(
            IEnumerable<SimulationFacilityCapacityDefinitionRequest> capacities)
        {
            var codes = new HashSet<string>(StringComparer.Ordinal);
            foreach (var capacity in capacities ??
                     Array.Empty<SimulationFacilityCapacityDefinitionRequest>())
            {
                RequireStableId(capacity.CapacityCode,
                    "SimulationFacilityCapacityCodeInvalid");
                RequireStableId(capacity.UnitCode,
                    "SimulationFacilityCapacityUnitInvalid");
                if (capacity.Quantity <= 0m)
                    throw new SimulationContractException(
                        "SimulationFacilityCapacityQuantityInvalid");
                if (!codes.Add(capacity.CapacityCode.Trim()))
                    throw new SimulationContractException(
                        "SimulationFacilityCapacityDuplicate");
            }
        }

        private void ValidateSettlementFacilityProjection(
            SimulationFacilityBlueprintRequest blueprint,
            SimulationFacilityDefinitionRequest definition)
        {
            var hasType = !string.IsNullOrWhiteSpace(
                blueprint.SettlementFacilityTypeCode);
            var hasDistrict = !string.IsNullOrWhiteSpace(
                blueprint.SettlementDistrictStableId);
            if (hasType != hasDistrict)
                throw new SimulationContractException(
                    "SimulationSettlementFacilityProjectionInvalid");
            if (!hasType) return;
            if (settlementInitialState == null)
                throw new SimulationContractException(
                    "SimulationSettlementRequiredForFacilityProjection");
            if (!settlementInitialState.Districts.Any(value =>
                    value.DistrictStableId == blueprint.SettlementDistrictStableId.Trim()))
                throw new SimulationContractException(
                    "SimulationSettlementFacilityProjectionDistrictNotFound");
            if (blueprint.SettlementFacilityTypeCode.Trim()
                != SimulationSettlementFacilityTypeCodes.Storage) return;
            var storage = definition.Capacities.SingleOrDefault(value =>
                value.CapacityCode == Simulation공간용량Codes.StorageCapacity);
            if (storage == null)
                throw new SimulationContractException(
                    "SimulationSettlementStorageCapacityProjectionRequired");
            if (storage.UnitCode.Trim() != settlementInitialState.StorageUnitCode)
                throw new SimulationContractException(
                    "SimulationSettlementStorageCapacityProjectionUnitMismatch");
        }

        private static void ValidateLotSeed(SimulationIntegratedLotSeedRequest value)
        {
            RequireStableId(value.LotStableId, "SimulationIntegratedLotStableIdInvalid");
            RequireStableId(value.ItemCode, "SimulationIntegratedLotItemCodeInvalid");
            RequireStableId(value.UnitCode, "SimulationIntegratedLotUnitCodeInvalid");
            RequireStableId(value.FacilityStableId, "SimulationIntegratedLotFacilityInvalid");
            if (value.Quantity <= 0m) throw new SimulationContractException(
                "SimulationIntegratedLotQuantityInvalid");
        }

        private static void ValidateRecipe(SimulationManufacturingRecipeRequest value)
        {
            RequireStableId(value.RecipeStableId, "SimulationManufacturingRecipeStableIdInvalid");
            RequireStableId(value.Revision, "SimulationManufacturingRecipeRevisionInvalid");
            RequireStableId(value.HashSha256, "SimulationManufacturingRecipeHashInvalid");
            if (value.ProcessingTicks <= 0 || value.Inputs.Length == 0 || value.Outputs.Length == 0)
                throw new SimulationContractException("SimulationManufacturingRecipeInvalid");
            ValidateRequirements(value.Inputs); ValidateRequirements(value.Outputs);
        }

        private static void ValidateBlueprint(SimulationFacilityBlueprintRequest value)
        {
            RequireStableId(value.BlueprintStableId, "SimulationFacilityBlueprintStableIdInvalid");
            RequireStableId(value.Revision, "SimulationFacilityBlueprintRevisionInvalid");
            RequireStableId(value.HashSha256, "SimulationFacilityBlueprintHashInvalid");
            RequireStableId(value.FacilityDefinitionStableId,
                "SimulationFacilityBlueprintDefinitionInvalid");
            if (value.ConstructionTicks <= 0 || value.Materials.Length == 0)
                throw new SimulationContractException("SimulationFacilityBlueprintInvalid");
            if (!string.IsNullOrWhiteSpace(value.PlacementKindCode))
            {
                if (value.AllowedPlacementZoneTypeCodes.Length == 0
                    || value.FootprintWidthCentimeters <= 0
                    || value.FootprintDepthCentimeters <= 0
                    || value.ClearanceCentimeters < 0
                    || value.MaxSlopeMilliDegrees < 0)
                    throw new SimulationContractException(
                        "SimulationFacilityBlueprintPlacementProfileInvalid");
                if (value.AllowedPlacementZoneTypeCodes.Any(string.IsNullOrWhiteSpace))
                    throw new SimulationContractException(
                        "SimulationFacilityBlueprintPlacementZoneTypeInvalid");
            }
            ValidateRequirements(value.Materials);
        }

        private static void ValidateRequirements(IEnumerable<SimulationIntegratedItemRequirement> values)
        {
            foreach (var value in values)
            {
                RequireStableId(value.ItemCode, "SimulationIntegratedItemCodeInvalid");
                RequireStableId(value.UnitCode, "SimulationIntegratedItemUnitCodeInvalid");
                if (value.Quantity <= 0m) throw new SimulationContractException(
                    "SimulationIntegratedItemQuantityInvalid");
            }
        }

        private sealed class AppliedIntegratedWorldCommand
        {
            public AppliedIntegratedWorldCommand(string fingerprint,
                경영SimulationSessionSnapshot snapshot)
            {
                Fingerprint = fingerprint;
                Snapshot = snapshot;
            }

            public string Fingerprint { get; }
            public 경영SimulationSessionSnapshot Snapshot { get; }
        }

        private static SimulationIntegratedItemRequirement[] CloneRequirements(
            IEnumerable<SimulationIntegratedItemRequirement> source) => source.Select(value =>
                new SimulationIntegratedItemRequirement { ItemCode = value.ItemCode,
                    Quantity = value.Quantity, UnitCode = value.UnitCode }).ToArray();

        private static SimulationFacilityDefinitionRequest CloneFacilityDefinition(
            SimulationFacilityDefinitionRequest value) => new()
            {
                FacilityDefinitionStableId = value.FacilityDefinitionStableId,
                Revision = value.Revision, HashSha256 = value.HashSha256,
                FacilityTypeCode = value.FacilityTypeCode,
                CapabilityCodes = value.CapabilityCodes.ToArray(),
                Capacities = value.Capacities.Select(capacity =>
                    new SimulationFacilityCapacityDefinitionRequest
                    {
                        CapacityCode = capacity.CapacityCode,
                        Quantity = capacity.Quantity,
                        UnitCode = capacity.UnitCode,
                    }).ToArray(),
            };

        private static SimulationManufacturingRecipeRequest CloneRecipe(
            SimulationManufacturingRecipeRequest value) => new()
            {
                RecipeStableId = value.RecipeStableId, Revision = value.Revision,
                HashSha256 = value.HashSha256, ProcessingTicks = value.ProcessingTicks,
                Inputs = CloneRequirements(value.Inputs), Outputs = CloneRequirements(value.Outputs),
            };

        private static SimulationFacilityBlueprintRequest CloneBlueprint(
            SimulationFacilityBlueprintRequest value) => new()
            {
                BlueprintStableId = value.BlueprintStableId, Revision = value.Revision,
                HashSha256 = value.HashSha256,
                FacilityDefinitionStableId = value.FacilityDefinitionStableId,
                SettlementFacilityTypeCode = value.SettlementFacilityTypeCode,
                SettlementDistrictStableId = value.SettlementDistrictStableId,
                ConstructionTicks = value.ConstructionTicks,
                PlacementKindCode = value.PlacementKindCode,
                AllowedPlacementZoneTypeCodes = value.AllowedPlacementZoneTypeCodes.ToArray(),
                FootprintWidthCentimeters = value.FootprintWidthCentimeters,
                FootprintDepthCentimeters = value.FootprintDepthCentimeters,
                ClearanceCentimeters = value.ClearanceCentimeters,
                MaxSlopeMilliDegrees = value.MaxSlopeMilliDegrees,
                RequiresRoadAccess = value.RequiresRoadAccess,
                Materials = CloneRequirements(value.Materials),
            };

        internal static SimulationIntegratedWorldInitialStateRequest? CloneIntegratedWorldInitialState(
            SimulationIntegratedWorldInitialStateRequest? value) => value == null ? null : new()
            {
                ScenarioRevision = value.ScenarioRevision,
                ScenarioHashSha256 = value.ScenarioHashSha256,
                FacilityDefinitions = value.FacilityDefinitions.Select(CloneFacilityDefinition).ToArray(),
                FacilitySeeds = value.FacilitySeeds.Select(seed => new SimulationScenarioFacilitySeedRequest
                {
                    FacilityStableId = seed.FacilityStableId,
                    FacilityDefinitionStableId = seed.FacilityDefinitionStableId,
                    PlacementH1StableId = seed.PlacementH1StableId,
                    AccessConnectorStableIds = seed.AccessConnectorStableIds.ToArray(),
                }).ToArray(),
                Actors = value.Actors.Select(actor => new SimulationIntegratedActorSeedRequest
                {
                    ActorStableId = actor.ActorStableId, EligibilityRank = actor.EligibilityRank,
                    FarmLaborEligible = actor.FarmLaborEligible,
                }).ToArray(),
                Lots = value.Lots.Select(lot => new SimulationIntegratedLotSeedRequest
                {
                    LotStableId = lot.LotStableId, ItemCode = lot.ItemCode,
                    Quantity = lot.Quantity, UnitCode = lot.UnitCode,
                    FacilityStableId = lot.FacilityStableId,
                }).ToArray(),
                ManufacturingRecipes = value.ManufacturingRecipes.Select(CloneRecipe).ToArray(),
                FacilityBlueprints = value.FacilityBlueprints.Select(CloneBlueprint).ToArray(),
                ConstructionPlacementZones = value.ConstructionPlacementZones
                    .Select(ClonePlacementZone).ToArray(),
            };

        private static SimulationRuntimeFacilitySnapshot CloneRuntimeFacility(
            SimulationRuntimeFacilitySnapshot value) => new()
            {
                FacilityStableId = value.FacilityStableId,
                FacilityDefinitionStableId = value.FacilityDefinitionStableId,
                FacilityDefinitionRevision = value.FacilityDefinitionRevision,
                FacilityDefinitionHashSha256 = value.FacilityDefinitionHashSha256,
                PlacementH1StableId = value.PlacementH1StableId,
                PlacementZoneStableId = value.PlacementZoneStableId,
                TargetH2StableId = value.TargetH2StableId,
                PlacementKindCode = value.PlacementKindCode,
                LocalXCentimeters = value.LocalXCentimeters,
                LocalZCentimeters = value.LocalZCentimeters,
                RotationQuarterTurns = value.RotationQuarterTurns,
                PlacementProfileRevision = value.PlacementProfileRevision,
                FenceChainStableId = value.FenceChainStableId,
                SettlementFacilityTypeCode = value.SettlementFacilityTypeCode,
                SettlementDistrictStableId = value.SettlementDistrictStableId,
                AccessConnectorStableIds = value.AccessConnectorStableIds.ToArray(),
                LifecycleCode = value.LifecycleCode, IntegrityCode = value.IntegrityCode,
                MaintenanceCode = value.MaintenanceCode,
                DefinedCapabilityCodes = value.DefinedCapabilityCodes.ToArray(),
                DefinedCapacities = value.DefinedCapacities.Select(capacity =>
                    new SimulationRuntimeFacilityCapacitySnapshot
                    {
                        CapacityCode = capacity.CapacityCode,
                        Quantity = capacity.Quantity,
                        UnitCode = capacity.UnitCode,
                    }).ToArray(),
                EffectiveCapabilities = value.EffectiveCapabilities.Select(capability =>
                    new SimulationEffectiveFacilityCapabilitySnapshot
                    {
                        CapabilityCode = capability.CapabilityCode, StateCode = capability.StateCode,
                        SourceRestrictionStableIds = capability.SourceRestrictionStableIds.ToArray(),
                    }).ToArray(),
            };

        internal static SimulationIntegratedWorldSnapshot CloneIntegratedWorldSnapshot(
            SimulationIntegratedWorldSnapshot value) => new()
            {
                ScenarioRevision = value.ScenarioRevision, ScenarioHashSha256 = value.ScenarioHashSha256,
                Facilities = value.Facilities.Select(CloneRuntimeFacility).ToArray(),
                FacilityRestrictions = value.FacilityRestrictions.Select(CloneRestriction).ToArray(),
                ManufacturingJobs = value.ManufacturingJobs.Select(CloneManufacturingJob).ToArray(),
                ConstructionProjects = value.ConstructionProjects.Select(CloneConstructionProject).ToArray(),
                Actors = value.Actors.Select(CloneIntegratedActor).ToArray(),
                Formations = value.Formations.Select(CloneFormation).ToArray(),
                ActorCommitments = value.ActorCommitments.Select(CloneCommitment).ToArray(),
                ActorInjuries = value.ActorInjuries.Select(CloneInjury).ToArray(),
                Lots = value.Lots.Select(CloneLot).ToArray(),
                Reservations = value.Reservations.Select(CloneIntegratedReservation).ToArray(),
                WorldEffects = value.WorldEffects.Select(CloneWorldEffect).ToArray(),
                PendingWorldEffects = value.PendingWorldEffects.Select(ClonePendingEffect).ToArray(),
                AppliedWorldEffectReceipts = value.AppliedWorldEffectReceipts.Select(CloneAppliedEffect).ToArray(),
                RepairJobs = value.RepairJobs.Select(CloneRepairJob).ToArray(),
                CargoMovements = value.CargoMovements.Select(CloneCargoMovement).ToArray(),
            };

        private static SimulationFacilityRestrictionSnapshot CloneRestriction(SimulationFacilityRestrictionSnapshot value)
            => new() { RestrictionStableId = value.RestrictionStableId,
                SourceEffectStableId = value.SourceEffectStableId, FacilityStableId = value.FacilityStableId,
                CapabilityCode = value.CapabilityCode, RestrictionLevelCode = value.RestrictionLevelCode,
                ResolvedByEffectStableId = value.ResolvedByEffectStableId };
        private static SimulationManufacturingJobSnapshot CloneManufacturingJob(SimulationManufacturingJobSnapshot value)
            => new() { ManufacturingJobStableId = value.ManufacturingJobStableId,
                RecipeStableId = value.RecipeStableId, RecipeRevision = value.RecipeRevision,
                RecipeHashSha256 = value.RecipeHashSha256, StateCode = value.StateCode,
                ProcessingStartsAtTick = value.ProcessingStartsAtTick,
                ProcessingCompletesAtTick = value.ProcessingCompletesAtTick,
                ResolvedInputRequirements = CloneRequirements(value.ResolvedInputRequirements),
                ResolvedOutputSpecification = CloneRequirements(value.ResolvedOutputSpecification),
                ReservedInputLotStableIds = value.ReservedInputLotStableIds.ToArray(),
                ConsumedInputLotStableIds = value.ConsumedInputLotStableIds.ToArray(),
                OutputLotStableIds = value.OutputLotStableIds.ToArray(),
                ActorStableId = value.ActorStableId, FacilityStableId = value.FacilityStableId };
        private static SimulationConstructionProjectSnapshot CloneConstructionProject(SimulationConstructionProjectSnapshot value)
            => new() { ConstructionProjectStableId = value.ConstructionProjectStableId,
                BlueprintStableId = value.BlueprintStableId, BlueprintRevision = value.BlueprintRevision,
                BlueprintHashSha256 = value.BlueprintHashSha256, StateCode = value.StateCode,
                TargetFacilityStableId = value.TargetFacilityStableId, BuildSiteH1StableId = value.BuildSiteH1StableId,
                PlacementProposalStableId = value.PlacementProposalStableId,
                PlacementPreviewHashSha256 = value.PlacementPreviewHashSha256,
                PlacementZoneStableId = value.PlacementZoneStableId,
                TargetH2StableId = value.TargetH2StableId,
                PlacementKindCode = value.PlacementKindCode,
                LocalXCentimeters = value.LocalXCentimeters,
                LocalZCentimeters = value.LocalZCentimeters,
                RotationQuarterTurns = value.RotationQuarterTurns,
                PlacementProfileRevision = value.PlacementProfileRevision,
                FenceChainStableId = value.FenceChainStableId,
                DevelopmentOpportunityStableId = value.DevelopmentOpportunityStableId,
                ConstructionStartsAtTick = value.ConstructionStartsAtTick,
                ConstructionCompletesAtTick = value.ConstructionCompletesAtTick,
                ResolvedMaterialRequirements = CloneRequirements(value.ResolvedMaterialRequirements),
                ReservedMaterialLotStableIds = value.ReservedMaterialLotStableIds.ToArray(),
                ConsumedMaterialLotStableIds = value.ConsumedMaterialLotStableIds.ToArray(),
                ActorStableId = value.ActorStableId };
        private static SimulationIntegratedActorSnapshot CloneIntegratedActor(SimulationIntegratedActorSnapshot value)
            => new() { ActorStableId = value.ActorStableId, EligibilityRank = value.EligibilityRank,
                FarmLaborEligible = value.FarmLaborEligible };
        private static SimulationFormationSnapshot CloneFormation(SimulationFormationSnapshot value)
            => new() { FormationStableId = value.FormationStableId, StateCode = value.StateCode,
                MemberActorStableIds = value.MemberActorStableIds.ToArray(),
                GarrisonFacilityStableId = value.GarrisonFacilityStableId,
                StateCompletesAtTick = value.StateCompletesAtTick };
        private static SimulationActorCommitmentSnapshot CloneCommitment(SimulationActorCommitmentSnapshot value)
            => new() { CommitmentStableId = value.CommitmentStableId, ActorStableId = value.ActorStableId,
                CommitmentCode = value.CommitmentCode, SourceStableId = value.SourceStableId, Active = value.Active };
        private static SimulationActorInjurySnapshot CloneInjury(SimulationActorInjurySnapshot value)
            => new() { InjuryStableId = value.InjuryStableId, ActorStableId = value.ActorStableId,
                SourceEffectStableId = value.SourceEffectStableId, Active = value.Active };
        private static SimulationIntegratedLotSnapshot CloneLot(SimulationIntegratedLotSnapshot value)
            => new() { LotStableId = value.LotStableId, ItemCode = value.ItemCode, Quantity = value.Quantity,
                UnitCode = value.UnitCode, FacilityStableId = value.FacilityStableId, SourceStableId = value.SourceStableId };
        private static SimulationIntegratedReservationSnapshot CloneIntegratedReservation(SimulationIntegratedReservationSnapshot value)
            => new() { ReservationStableId = value.ReservationStableId, OwnerStableId = value.OwnerStableId,
                TargetStableId = value.TargetStableId, ReservationKindCode = value.ReservationKindCode,
                Quantity = value.Quantity, StateCode = value.StateCode };
        private static SimulationWorldEffectSnapshot CloneWorldEffect(SimulationWorldEffectSnapshot value)
            => new() { EffectStableId = value.EffectStableId, EffectCode = value.EffectCode,
                SourceStableId = value.SourceStableId, TargetStableId = value.TargetStableId,
                PayloadCanonical = value.PayloadCanonical };
        private static SimulationPendingWorldEffectEntrySnapshot ClonePendingEffect(SimulationPendingWorldEffectEntrySnapshot value)
            => new() { EffectStableId = value.EffectStableId, EarliestWorldTick = value.EarliestWorldTick };
        private static SimulationAppliedWorldEffectReceiptSnapshot CloneAppliedEffect(SimulationAppliedWorldEffectReceiptSnapshot value)
            => new() { EffectStableId = value.EffectStableId, AppliedWorldTick = value.AppliedWorldTick,
                AppliedWorldRevision = value.AppliedWorldRevision };
        private static SimulationFacilityRepairJobSnapshot CloneRepairJob(SimulationFacilityRepairJobSnapshot value)
            => new() { RepairJobStableId = value.RepairJobStableId, FacilityStableId = value.FacilityStableId,
                ActorStableId = value.ActorStableId, TargetRestrictionStableIds = value.TargetRestrictionStableIds.ToArray(),
                ReservedMaterialLotStableIds = value.ReservedMaterialLotStableIds.ToArray(),
                CompletesAtTick = value.CompletesAtTick, StateCode = value.StateCode };
        private static SimulationIntegratedCargoMovementSnapshot CloneCargoMovement(
            SimulationIntegratedCargoMovementSnapshot value) => new()
            {
                MovementStableId = value.MovementStableId,
                SourceLotStableId = value.SourceLotStableId,
                TargetFacilityStableId = value.TargetFacilityStableId,
                Quantity = value.Quantity,
                ActorStableId = value.ActorStableId,
                CompletesAtTick = value.CompletesAtTick,
                StateCode = value.StateCode,
                OutputLotStableId = value.OutputLotStableId,
            };
    }
}
