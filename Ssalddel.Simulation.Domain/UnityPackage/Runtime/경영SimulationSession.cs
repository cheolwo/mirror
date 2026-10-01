using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Simulation.Contracts;

namespace Ssalddel.Simulation.Domain
{
    public sealed class SimulationContractException : InvalidOperationException
    {
        public SimulationContractException(string errorCode)
            : base(errorCode)
        {
            ErrorCode = errorCode;
        }

        public string ErrorCode { get; }
    }

    public sealed class SimulationNotFoundException : InvalidOperationException
    {
        public SimulationNotFoundException(string errorCode)
            : base(errorCode)
        {
            ErrorCode = errorCode;
        }

        public string ErrorCode { get; }
    }

    public sealed class SimulationConflictException : InvalidOperationException
    {
        public SimulationConflictException(string errorCode)
            : base(errorCode)
        {
            ErrorCode = errorCode;
        }

        public string ErrorCode { get; }
    }

    [SsalddelCodeMetadata(
        SsalddelCodeFeatureKeys.SimulationSessionLifecycle,
        SsalddelCodeLayer.Domain,
        "결정적 세션 상태와 개정·Tick 상태 전이를 소유한다.",
        StepKey = "domain.session-aggregate",
        DependsOnStepKeys = new string[] { "application.session-lifecycle" },
        ExecutionStage = SsalddelCodeExecutionStage.Tick,
        Effects = SsalddelCodeEffect.StateMutation,
        ReadsFrom = SsalddelCodeDataScope.SimulationState,
        WritesTo = SsalddelCodeDataScope.SimulationState,
        FlowOrder = 40,
        Boundary = "Aggregate 상태 전이는 Simulation 전용이며 운영 계약·재고·결제를 변경하지 않는다.")]
    [SsalddelEvidenceResponsibility(
        SsalddelEvidenceStage.E1,
        "Simulation Session의 식별자·판본·시간과 핵심 불변 경계를 소유한다.",
        Boundary = "Aggregate 계약은 실행 위치와 상위 E 증거를 분리한다.",
        SubmoduleKey = SsalddelEvidenceSubmoduleKeys.E1세션권위계약)]
    public sealed partial class 경영SimulationSessionAggregate
    {
        private readonly object gate = new object();
        private string tickRuleRevision = SimulationTickRuleRevisions.SingleStep;

        internal void RestoreTickRuleRevision(string revision)
        {
            if (revision != string.Empty && revision != SimulationTickRuleRevisions.SingleStep)
                throw new SimulationContractException("SimulationTickRuleRevisionUnsupported");
            tickRuleRevision = revision;
        }
        private readonly Dictionary<string, 적용된TickCommand> appliedCommands =
            new Dictionary<string, 적용된TickCommand>(StringComparer.Ordinal);

        public 경영SimulationSessionAggregate(경영SimulationSession생성Request request)
            : this(request, null)
        {
        }

        public 경영SimulationSessionAggregate(경영SimulationSession생성Request request,
            SimulationRealityContextSnapshot? frozenRealityContext)
        {
            ValidateCreate(request);
            localLife = ValidateLocalLife(request);
            SessionStableId = "simulation-session:" + request.ClientRequestId.ToString("N");
            ClientRequestId = request.ClientRequestId;
            ScenarioStableId = request.ScenarioStableId.Trim();
            ScenarioDataRevision = request.ScenarioDataRevision.Trim();
            neighborhoodDayEnabled = request.NeighborhoodDayEnabled;
            if (neighborhoodDayEnabled && !NeighborhoodLifeEnabled)
                throw new SimulationContractException("NeighborhoodDayProfileRequired");
            if (WaitingFleetEnabled) waitingFleet = CreateWaitingFleet();
            ScenarioSeed = request.ScenarioSeed;
            RuleRevision = request.RuleRevision.Trim();
            RealityContextProfileStableId = (request.RealityContextProfileStableId
                ?? string.Empty).Trim();
            NpcRoutineControlRevision = (request.NpcRoutineControlRevision
                ?? string.Empty).Trim();
            SpatialCompositionRuleRevision =
                (request.SpatialCompositionRuleRevision ?? string.Empty).Trim();
            DurationTicks = request.DurationTicks;
            FactionStableId = request.WorldContext.FactionStableId.Trim();
            TerritoryStableId = request.WorldContext.TerritoryStableId.Trim();
            SettlementStableId = request.WorldContext.SettlementStableId.Trim();
            GameDateStartsOn = request.WorldContext.GameDateStartsOn;
            InitializeSettlement(request.Settlement);
            InitializeNpcWorkforce(request.NpcWorkforce);
            InitializeSimulationSpatialWorld(request.SpatialWorld);
            InitializeWorldInventory(request.WorldInventory);
            InitializeSurvivalTarot(request.SurvivalTarot);
            InitializeFarmSurvival(request.FarmSurvival);
            InitializeTeamRoleCards(request.TeamRoleCards);
            InitializeLearningFocus(request.LearningFocus);
            InitializeCollectibleCardRewards();
            InitializeIntegratedWorld(request.IntegratedWorld);
            InitializeNatureMind(request.NatureMind);
            InitializeArcanaTownLife(request.TarotOrientationPolicyCode,
                request.TownNpcLifeProfileStableId);
            InitializeNatureSurvival(request.NatureSurvival);
            InitializeActorEquipment(request.ActorEquipment);
            InitializeWorldAtmosphere(request.Atmosphere);
            InitializeAreaAccess(request.FarmSurvival != null);
            InitializeHostedWorld();
            InitializeCoopConstruction();
            InitializeRealityContext(frozenRealityContext);
            InitializeInteriorPlanHandles(request.InteriorPlanHandles);
            InitializeSpatialComposition();
        }

        public string SessionStableId { get; }
        public Guid ClientRequestId { get; }
        public string ScenarioStableId { get; }
        public string ScenarioDataRevision { get; }
        public int ScenarioSeed { get; }
        public string RuleRevision { get; }
        public string RealityContextProfileStableId { get; }
        public string NpcRoutineControlRevision { get; }
        public string SpatialCompositionRuleRevision { get; }
        public int CurrentTick { get; private set; }
        public int DurationTicks { get; }
        public string FactionStableId { get; }
        public string TerritoryStableId { get; }
        public string SettlementStableId { get; }
        public DateTimeOffset GameDateStartsOn { get; }
        public long Revision { get; private set; }

        public 경영SimulationSessionSnapshot Snapshot()
        {
            lock (gate)
            {
                return CreateSnapshot();
            }
        }

        public 경영SimulationSessionSnapshot Advance(경영SimulationTick진행Request request)
        {
            ValidateAdvance(request);
            lock (gate)
            {
                if (HasAppliedDecisionCommand(request.CommandId)
                    || HasAppliedNpcPolicyCommand(request.CommandId)
                    || HasAppliedFarmSurvivalCommand(request.CommandId)
                    || HasAppliedCollectibleCardCommand(request.CommandId)
                    || HasAppliedNatureSurvivalCommand(request.CommandId)
                    || HasAppliedActorEquipmentCommand(request.CommandId))
                    throw new SimulationConflictException("SimulationCommandKindConflict");
                if (appliedCommands.TryGetValue(request.CommandId, out var applied))
                {
                    if (applied.TickCount != request.TickCount)
                        throw new SimulationConflictException("SimulationCommandPayloadConflict");
                    return Clone(applied.Snapshot);
                }

                if (request.ExpectedRevision != Revision)
                    throw new SimulationConflictException("SimulationExpectedRevisionMismatch");
                if (CurrentTick + request.TickCount > DurationTicks)
                    throw new SimulationConflictException("SimulationDurationExceeded");

                worldStateAdvanceInProgress = true;
                try
                {
                    if (tickRuleRevision == SimulationTickRuleRevisions.SingleStep)
                    {
                        for (var step = 0; step < request.TickCount; step++) AdvanceWorldState(1);
                    }
                    else AdvanceWorldState(request.TickCount);
                }
                finally
                {
                    worldStateAdvanceInProgress = false;
                }
                Revision++;
                AppendTickCommand(request);
                var snapshot = CreateSnapshot();
                appliedCommands.Add(
                    request.CommandId,
                    new 적용된TickCommand(request.TickCount, Clone(snapshot)));
                return snapshot;
            }
        }

        private void AdvanceWorldState(int tickCount)
        {
            var previousTick = CurrentTick;
            CurrentTick += tickCount;
            AdvanceNeighborhoodLife();
            AdvanceLearningFocus(previousTick, CurrentTick);
            EvaluateNpcRoutineWork();
            AdvanceNpcWorkforce(CurrentTick);
            AdvanceRestaurantAndDecisionWork();
            AdvanceSyntheticDelivery();
            ExpireActiveTurnCardEffects();
            ExpireTarotContext();
            AdvanceTownNpcLife(previousTick, CurrentTick);
            AdvanceFarmSurvival(previousTick, CurrentTick);
            AdvanceRegionalIncidents(CurrentTick);
            AdvanceIntegratedWorld(CurrentTick);
            RefreshAllAreaAccessEvidence();
            EvaluateSurvivalTarotOpportunity();
            EvaluateSpatialComposition();
        }

        public void EnsureSameCreationRequest(경영SimulationSession생성Request request,
            SimulationRealityContextSnapshot? frozenRealityContext = null)
        {
            ValidateCreate(request);
            if (ClientRequestId != request.ClientRequestId
                || !LocalLifeEqual(localLife, request.LocalLife)
                || !string.Equals(ScenarioStableId, request.ScenarioStableId.Trim(), StringComparison.Ordinal)
                || !string.Equals(ScenarioDataRevision, request.ScenarioDataRevision.Trim(), StringComparison.Ordinal)
                || ScenarioSeed != request.ScenarioSeed
                || !string.Equals(RuleRevision, request.RuleRevision.Trim(), StringComparison.Ordinal)
                || !string.Equals(RealityContextProfileStableId,
                    (request.RealityContextProfileStableId ?? string.Empty).Trim(),
                    StringComparison.Ordinal)
                || !string.Equals(NpcRoutineControlRevision,
                    (request.NpcRoutineControlRevision ?? string.Empty).Trim(),
                    StringComparison.Ordinal)
                || !string.Equals(SpatialCompositionRuleRevision,
                    (request.SpatialCompositionRuleRevision
                     ?? string.Empty).Trim(), StringComparison.Ordinal)
                || !InteriorPlanHandlesEqual(interiorPlanHandles,
                    request.InteriorPlanHandles)
                || !string.Equals(tarotOrientationPolicyCode,
                    string.IsNullOrWhiteSpace(request.TarotOrientationPolicyCode)
                        ? Simulation타로방향결정정책Codes.SeededHash
                        : request.TarotOrientationPolicyCode.Trim(),
                    StringComparison.Ordinal)
                || !string.Equals(townNpcLifeProfileStableId,
                    (request.TownNpcLifeProfileStableId ?? string.Empty).Trim(),
                    StringComparison.Ordinal)
                || !string.Equals(realityContext?.InputHashSha256,
                    frozenRealityContext?.InputHashSha256, StringComparison.Ordinal)
                || DurationTicks != request.DurationTicks
                || !string.Equals(FactionStableId, request.WorldContext.FactionStableId.Trim(), StringComparison.Ordinal)
                || !string.Equals(TerritoryStableId, request.WorldContext.TerritoryStableId.Trim(), StringComparison.Ordinal)
                || !string.Equals(SettlementStableId, request.WorldContext.SettlementStableId.Trim(), StringComparison.Ordinal)
                || GameDateStartsOn != request.WorldContext.GameDateStartsOn
                || !string.Equals(
                    SettlementPayloadKey,
                    BuildSettlementPayloadKey(request.Settlement),
                    StringComparison.Ordinal)
                || !string.Equals(
                    BuildNpcWorkforcePayloadKey(npcWorkforceCreationState,
                        IsNpcRoutineControlEnabled),
                    BuildNpcWorkforcePayloadKey(request.NpcWorkforce,
                        IsNpcRoutineControlEnabled),
                    StringComparison.Ordinal)
                || !string.Equals(
                    BuildSimulationSpatialPayloadKey(spatialWorldCreationState),
                    BuildSimulationSpatialPayloadKey(request.SpatialWorld),
                    StringComparison.Ordinal)
                || !string.Equals(
                    worldInventoryInitialPayloadKey,
                    BuildWorldInventoryPayloadKey(request.WorldInventory),
                    StringComparison.Ordinal)
                || !string.Equals(
                    survivalTarotInitialPayloadKey,
                    BuildSurvivalTarotPayloadKey(request.SurvivalTarot),
                    StringComparison.Ordinal)
                || !string.Equals(
                    farmSurvivalInitialPayloadKey,
                    BuildFarmSurvivalPayloadKey(request.FarmSurvival),
                    StringComparison.Ordinal)
                || !string.Equals(
                    teamRoleCardInitialPayloadKey,
                    BuildTeamRoleCardPayloadKey(request.TeamRoleCards),
                    StringComparison.Ordinal)
                || !string.Equals(
                    learningFocusInitialPayloadKey,
                    BuildLearningFocusInitialPayloadKey(request.LearningFocus),
                    StringComparison.Ordinal)
                || !string.Equals(
                    BuildIntegratedWorldInitialFingerprint(integratedWorldCreationState),
                    BuildIntegratedWorldInitialFingerprint(request.IntegratedWorld),
                    StringComparison.Ordinal)
                || !string.Equals(natureMindInitialPayloadKey,
                    BuildNatureMindInitialPayloadKey(request.NatureMind),
                    StringComparison.Ordinal)
                || !string.Equals(natureSurvivalInitialPayloadKey,
                    BuildNatureSurvivalInitialPayloadKey(request.NatureSurvival),
                    StringComparison.Ordinal)
                || !string.Equals(actorEquipmentInitialPayloadKey,
                    BuildActorEquipmentInitialPayloadKey(request.ActorEquipment),
                    StringComparison.Ordinal)
                || !string.Equals(
                    BuildWorldAtmosphereInitialPayloadKey(atmosphereCreationState),
                    BuildWorldAtmosphereInitialPayloadKey(request.Atmosphere),
                    StringComparison.Ordinal))
            {
                throw new SimulationConflictException("SimulationCreateRequestPayloadConflict");
            }
        }

        private 경영SimulationSessionSnapshot CreateSnapshot()
            => new 경영SimulationSessionSnapshot
            {
                SessionStableId = SessionStableId,
                ClientRequestId = ClientRequestId,
                ScenarioStableId = ScenarioStableId,
                ScenarioDataRevision = ScenarioDataRevision,
                ScenarioSeed = ScenarioSeed,
                RuleRevision = RuleRevision,
                NpcRoutineControlRevision = NpcRoutineControlRevision,
                InteriorPlanHandles = CreateInteriorPlanHandleSnapshots(),
                CurrentTick = CurrentTick,
                DurationTicks = DurationTicks,
                Revision = Revision,
                IsCompleted = CurrentTick == DurationTicks,
                ModeCode = SimulationModeCodes.Simulation,
                IsOperationalState = false,
                WorldContext = new SimulationWorldContextSnapshot
                {
                    FactionStableId = FactionStableId,
                    TerritoryStableId = TerritoryStableId,
                    SettlementStableId = SettlementStableId,
                    WorldTick = CurrentTick,
                    WorldRevision = Revision,
                    GameDateStartsOn = GameDateStartsOn,
                    GameDate = GameDateStartsOn.AddDays(CurrentTick),
                    CalendarRuleCode = "OneTickOneDay",
                },
                Decisions = CreateDecisionSnapshots(),
                Tasks = CreateTaskSnapshots(),
                Effects = CreateEffectSnapshots(),
                LogisticsMovements = CreateLogisticsMovementSnapshots(),
                FreightTransports = CreateFreightTransportSnapshots(),
                GroupOrders = CreateGroupOrderSnapshots(),
                FoodDeliveries = CreateFoodDeliverySnapshots(),
                SyntheticCourier = syntheticCourier?.Copy(),
                WaitingFleet = waitingFleet?.Copy(),
                MarketConsumptions = CreateMarketConsumptionSnapshots(),
                IndividualOrders = CreateIndividualOrderSnapshots(),
                StockReservations = CreateStockReservationSnapshots(),
                ExportPreparations = Create수출준비Snapshots(),
                ExportCargoPreparations = Create수출Cargo준비Snapshots(),
                ExportCargoHandoffs = Create수출Cargo인계Snapshots(),
                ExportPortReceipts = Create수출항만인수Snapshots(),
                ExportReadinessReviews = Create수출준비성검토Snapshots(),
                ExportShipmentPlans = Create수출선적계획Snapshots(),
                ExportShipmentExecutions = Create수출선적실행Snapshots(),
                TurnClosings = CreateTurnClosingSnapshots(),
                ActiveTurnCardEffects = CreateActiveTurnCardEffectSnapshots(),
                TarotContext = CreateTarotContextSnapshot(),
                TownNpcLife = CreateTownNpcLifeStateSnapshot(),
                NpcOrganizations = CreateNpcOrganizationSnapshots(),
                NpcActors = CreateNpcActorSnapshots(),
                NpcCapabilityGrants = CreateNpcCapabilityGrantSnapshots(),
                NpcWorkPolicies = CreateNpcWorkPolicySnapshots(),
                NpcTaskAssignments = CreateNpcTaskAssignmentSnapshots(),
                NpcWorkRecords = CreateNpcWorkRecordSnapshots(),
                NpcActionProjections = CreateNpcActionProjections(),
                NpcFacilityInventories = CreateNpcFacilityInventorySnapshots(),
                NpcRoutineExecutions = CreateNpcRoutineExecutionSnapshots(),
                SpatialDefinitions = CreateSimulationSpatialDefinitionSnapshots(),
                SpatialRuntimeStates = CreateSimulationSpatialRuntimeSnapshots(),
                SpatialReservations = CreateSimulationSpatialReservationSnapshots(),
                Settlement = CreateSettlementSnapshot(),
                FarmSurvival = farmSurvivalCreationState == null
                    ? null : CreateFarmSurvivalStateSnapshot(),
                TeamRoleCards = CreateTeamRoleCardStateSnapshotOrNull(),
                LearningFocus = CreateLearningFocusStateSnapshotOrNull(),
                Exploration = CreateWorldExplorationStateSnapshotOrNull(),
                CollectibleCardRewards = CreateCollectibleCardRewardStateSnapshotOrNull(),
                RegionalIncidents = CreateRegionalIncidentSnapshots(),
                NatureThreat = CreateNatureThreatStateSnapshot(),
                RegionalCausality = CreateRegionalCausalitySnapshot(),
                RegionalDevelopment = CreateRegionalDevelopmentStateSnapshot(),
                NatureMind = CreateNatureMindStateSnapshot(),
                AreaAccess = CreateAreaAccessStateSnapshot(),
                HostedWorld = CreateHostedWorldSnapshot(),
                CoopConstruction = CreateCoopConstructionStateSnapshot(),
                IntegratedWorld = CreateIntegratedWorldSnapshot(),
                NatureSurvival = CreateNatureSurvivalStateSnapshot(),
                ActorEquipment = CreateActorEquipmentStateSnapshot(),
                Atmosphere = CreateWorldAtmosphereStateSnapshot(),
                HexagramCampaign = CloneHexagramCampaignState(
                    hexagramCampaignState),
                SeasonalOperationsCampaign =
                    CloneSeasonalOperationsCampaignState(
                        seasonalOperationsCampaignState),
                FranchiseSupply = CloneFranchiseSupplyState(
                    franchiseSupplyState),
            };

        internal static 경영SimulationSessionSnapshot Clone(경영SimulationSessionSnapshot source)
            => new 경영SimulationSessionSnapshot
            {
                SessionStableId = source.SessionStableId,
                ClientRequestId = source.ClientRequestId,
                ScenarioStableId = source.ScenarioStableId,
                ScenarioDataRevision = source.ScenarioDataRevision,
                ScenarioSeed = source.ScenarioSeed,
                RuleRevision = source.RuleRevision,
                NpcRoutineControlRevision = source.NpcRoutineControlRevision,
                InteriorPlanHandles = CloneInteriorPlanHandles(
                    source.InteriorPlanHandles),
                CurrentTick = source.CurrentTick,
                DurationTicks = source.DurationTicks,
                Revision = source.Revision,
                IsCompleted = source.IsCompleted,
                ModeCode = source.ModeCode,
                IsOperationalState = source.IsOperationalState,
                WorldContext = new SimulationWorldContextSnapshot
                {
                    FactionStableId = source.WorldContext.FactionStableId,
                    TerritoryStableId = source.WorldContext.TerritoryStableId,
                    SettlementStableId = source.WorldContext.SettlementStableId,
                    WorldTick = source.WorldContext.WorldTick,
                    WorldRevision = source.WorldContext.WorldRevision,
                    GameDateStartsOn = source.WorldContext.GameDateStartsOn,
                    GameDate = source.WorldContext.GameDate,
                    CalendarRuleCode = source.WorldContext.CalendarRuleCode,
                },
                Decisions = CloneDecisions(source.Decisions),
                Tasks = CloneTasks(source.Tasks),
                Effects = CloneEffects(source.Effects),
                LogisticsMovements = source.LogisticsMovements.Select(CloneLogisticsMovement).ToArray(),
                FreightTransports = source.FreightTransports.Select(CloneFreightTransport).ToArray(),
                GroupOrders = source.GroupOrders.Select(CloneGroupOrder).ToArray(),
                FoodDeliveries = source.FoodDeliveries.Select(CloneFoodDelivery).ToArray(),
                SyntheticCourier = source.SyntheticCourier?.Copy(),
                WaitingFleet = source.WaitingFleet?.Copy(),
                MarketConsumptions = source.MarketConsumptions.Select(CloneMarketConsumption).ToArray(),
                IndividualOrders = source.IndividualOrders.Select(CloneIndividualOrder).ToArray(),
                StockReservations = source.StockReservations.Select(CloneStockReservation).ToArray(),
                ExportPreparations = source.ExportPreparations.Select(Clone수출준비).ToArray(),
                ExportCargoPreparations = source.ExportCargoPreparations
                    .Select(Clone수출Cargo준비).ToArray(),
                ExportCargoHandoffs = source.ExportCargoHandoffs
                    .Select(Clone수출Cargo인계).ToArray(),
                ExportPortReceipts = source.ExportPortReceipts
                    .Select(Clone수출항만인수).ToArray(),
                ExportReadinessReviews = source.ExportReadinessReviews
                    .Select(Clone수출준비성검토).ToArray(),
                ExportShipmentPlans = source.ExportShipmentPlans
                    .Select(Clone수출선적계획).ToArray(),
                ExportShipmentExecutions = source.ExportShipmentExecutions
                    .Select(Clone수출선적실행).ToArray(),
                TurnClosings = source.TurnClosings.Select(CloneTurnClosing).ToArray(),
                ActiveTurnCardEffects = source.ActiveTurnCardEffects
                    .Select(CloneActiveTurnCardEffect).ToArray(),
                TarotContext = CloneTarotContext(source.TarotContext),
                TownNpcLife = CloneTownNpcLifeState(source.TownNpcLife),
                NpcOrganizations = source.NpcOrganizations.Select(CloneNpcOrganization).ToArray(),
                NpcActors = source.NpcActors.Select(CloneNpcActor).ToArray(),
                NpcCapabilityGrants = source.NpcCapabilityGrants.Select(CloneNpcCapabilityGrant).ToArray(),
                NpcWorkPolicies = source.NpcWorkPolicies.Select(CloneNpcWorkPolicy).ToArray(),
                NpcTaskAssignments = source.NpcTaskAssignments.Select(CloneNpcTaskAssignment).ToArray(),
                NpcWorkRecords = source.NpcWorkRecords.Select(CloneNpcWorkRecord).ToArray(),
                NpcActionProjections = source.NpcActionProjections.Select(CloneNpcActionProjection).ToArray(),
                NpcFacilityInventories = source.NpcFacilityInventories.Select(CloneNpcFacilityInventory).ToArray(),
                NpcRoutineExecutions = (source.NpcRoutineExecutions
                        ?? Array.Empty<SimulationNpcRoutineExecutionSnapshot>())
                    .Select(CloneNpcRoutineExecution).ToArray(),
                SpatialDefinitions = source.SpatialDefinitions.Select(CloneSpatialDefinition).ToArray(),
                SpatialRuntimeStates = source.SpatialRuntimeStates.Select(CloneSpatialRuntime).ToArray(),
                SpatialReservations = source.SpatialReservations.Select(CloneSpatialReservation).ToArray(),
                Settlement = CloneSettlementSnapshot(source.Settlement),
                FarmSurvival = CloneFarmSurvivalStateOrNull(source.FarmSurvival),
                TeamRoleCards = CloneTeamRoleCardStateOrNull(source.TeamRoleCards),
                LearningFocus = CloneLearningFocusStateOrNull(source.LearningFocus),
                Exploration = CloneWorldExplorationStateOrNull(source.Exploration),
                CollectibleCardRewards = CloneCollectibleCardRewardStateOrNull(
                    source.CollectibleCardRewards),
                RegionalIncidents = source.RegionalIncidents
                    .Select(CloneRegionalIncident).ToArray(),
                NatureThreat = new SimulationNatureThreatStateSnapshot
                {
                    Routes = source.NatureThreat.Routes.Select(value =>
                        new SimulationNatureThreatRouteSnapshot
                        {
                            NatureRouteCode = value.NatureRouteCode,
                            RootRemainingSeverity = value.RootRemainingSeverity,
                            GlobalSpilloverPressure = value.GlobalSpilloverPressure,
                            IncidentPressure = value.IncidentPressure,
                            ThreatScoreModifier = value.ThreatScoreModifier,
                            RecoveryScoreModifier = value.RecoveryScoreModifier,
                            EffectivePressure = value.EffectivePressure,
                            PressureLevelCode = value.PressureLevelCode,
                            SourceIncidentStableIds = value.SourceIncidentStableIds.ToArray(),
                        }).ToArray(),
                    Encounters = source.NatureThreat.Encounters
                        .Select(CloneNatureEncounter).ToArray(),
                    SimulationOnly = source.NatureThreat.SimulationOnly,
                    IsOperationalState = source.NatureThreat.IsOperationalState,
                },
                RegionalCausality = CloneRegionalCausalityState(
                    source.RegionalCausality),
                RegionalDevelopment = CloneRegionalDevelopmentState(
                    source.RegionalDevelopment),
                NatureMind = CloneNatureMindState(source.NatureMind),
                AreaAccess = CloneAreaAccessState(source.AreaAccess),
                HostedWorld = CloneHostedWorldState(source.HostedWorld),
                CoopConstruction = CloneCoopConstructionState(source.CoopConstruction),
                IntegratedWorld = CloneIntegratedWorldSnapshot(source.IntegratedWorld),
                NatureSurvival = CloneNatureSurvivalState(source.NatureSurvival),
                ActorEquipment = source.ActorEquipment == null
                    ? new SimulationActorEquipmentStateSnapshot()
                    : CloneActorEquipmentState(source.ActorEquipment),
                Atmosphere = CloneWorldAtmosphereState(source.Atmosphere
                    ?? new SimulationAtmosphereStateSnapshot()),
                HexagramCampaign = CloneHexagramCampaignState(
                    source.HexagramCampaign),
                SeasonalOperationsCampaign =
                    CloneSeasonalOperationsCampaignState(
                        source.SeasonalOperationsCampaign),
                FranchiseSupply = CloneFranchiseSupplyState(
                    source.FranchiseSupply),
            };

        internal static void ValidateCreate(경영SimulationSession생성Request request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (request.ClientRequestId == Guid.Empty)
                throw new SimulationContractException("SimulationClientRequestIdMissing");
            RequireStableId(request.ScenarioStableId, "SimulationScenarioStableIdInvalid");
            RequireText(request.ScenarioDataRevision, "SimulationScenarioDataRevisionMissing");
            RequireText(request.RuleRevision, "SimulationRuleRevisionMissing");
            if (!string.IsNullOrWhiteSpace(request.RealityContextProfileStableId))
                RequireStableId(request.RealityContextProfileStableId,
                    "SimulationRealityContextProfileStableIdInvalid");
            if (!SimulationNpcRoutineControlRevisionCodes.IsKnown(
                    request.NpcRoutineControlRevision ?? string.Empty))
                throw new SimulationContractException(
                    "SimulationNpcRoutineControlRevisionInvalid");
            if (!string.IsNullOrWhiteSpace(
                    request.SpatialCompositionRuleRevision)
                && !string.Equals(request.SpatialCompositionRuleRevision,
                    SimulationSpatialCompositionCodes.RuleRevision,
                    StringComparison.Ordinal))
                throw new SimulationContractException(
                    "SimulationSpatialCompositionRuleRevisionInvalid");
            if (request.DurationTicks <= 0 || request.DurationTicks >
                (가상동네생활기준.Matches(request.ScenarioStableId, request.ScenarioDataRevision) ? 가상동네생활기준.DurationTicks : 365))
                throw new SimulationContractException("SimulationDurationTicksInvalid");
            if (request.WorldContext == null)
                throw new SimulationContractException("SimulationWorldContextMissing");
            RequireStableId(request.WorldContext.FactionStableId, "SimulationFactionStableIdInvalid");
            RequireStableId(request.WorldContext.TerritoryStableId, "SimulationTerritoryStableIdInvalid");
            RequireStableId(request.WorldContext.SettlementStableId, "SimulationSettlementStableIdInvalid");
            if (request.WorldContext.GameDateStartsOn == default
                || request.WorldContext.GameDateStartsOn.Offset != TimeSpan.Zero
                || request.WorldContext.GameDateStartsOn.TimeOfDay != TimeSpan.Zero)
                throw new SimulationContractException("SimulationGameDateStartsOnInvalid");
            ValidateSettlementInitialState(request.Settlement, request.WorldContext.SettlementStableId);
            ValidateNpcWorkforceInitialState(request.NpcWorkforce);
            if ((request.NpcWorkforce?.Inventories?.Length ?? 0) > 0
                && !string.Equals(request.NpcRoutineControlRevision,
                    SimulationNpcRoutineControlRevisionCodes.R1,
                    StringComparison.Ordinal)
                && !string.Equals(request.NpcRoutineControlRevision,
                    SimulationNpcRoutineControlRevisionCodes.R2,
                    StringComparison.Ordinal))
                throw new SimulationContractException(
                    "SimulationNpcInitialInventoryRequiresRoutineControl");
            ValidateSimulationSpatialInitialState(request.SpatialWorld);
            ValidateWorldInventoryInitialState(request.WorldInventory);
            ValidateSurvivalTarotInitialState(request.SurvivalTarot);
            ValidateFarmSurvivalInitialState(request.FarmSurvival);
            ValidateTeamRoleCardInitialState(request.TeamRoleCards);
            if (request.LearningFocus != null)
                Simulation학습중점State.ValidateInitial(request.LearningFocus);
            ValidateNatureSurvivalInitialState(request.NatureSurvival);
            ValidateWorldAtmosphereInitialState(request.Atmosphere,
                request.NatureSurvival);
            ValidateInteriorPlanHandles(request.InteriorPlanHandles);
        }

        internal static void ValidateAdvance(경영SimulationTick진행Request request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            RequireStableId(request.CommandId, "SimulationCommandIdInvalid");
            if (request.ExpectedRevision < 0)
                throw new SimulationContractException("SimulationExpectedRevisionInvalid");
            if (request.TickCount <= 0 || request.TickCount > 28)
                throw new SimulationContractException("SimulationTickCountInvalid");
        }

        private static void RequireStableId(string value, string errorCode)
        {
            if (string.IsNullOrWhiteSpace(value)
                || value.Length > 160
                || value.IndexOfAny(new[] { ' ', '\t', '\r', '\n' }) >= 0)
            {
                throw new SimulationContractException(errorCode);
            }
        }

        private static void RequireText(string value, string errorCode)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new SimulationContractException(errorCode);
        }

        private sealed class 적용된TickCommand
        {
            public 적용된TickCommand(int tickCount, 경영SimulationSessionSnapshot snapshot)
            {
                TickCount = tickCount;
                Snapshot = snapshot;
            }

            public int TickCount { get; }
            public 경영SimulationSessionSnapshot Snapshot { get; }
        }
    }

}
