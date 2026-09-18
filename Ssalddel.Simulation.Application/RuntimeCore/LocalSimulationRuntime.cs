using System;
using System.Threading;
using System.Threading.Tasks;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Simulation.Application;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;

namespace Ssalddel.Simulation.Application
{
    /// <summary>
    /// ASP.NET이나 HTTP 없이 공통 Application과 Session Aggregate를 실행하는 Solo 권위다.
    /// 세션 명령을 직렬화하여 실시간 진행, WorldTick과 저장이 서로 겹치지 않게 한다.
    /// </summary>
    [SsalddelEvidenceResponsibility(
        SsalddelEvidenceStage.E2,
        "Solo LocalProcess에서 공통 Simulation Core의 WI와 Session 명령을 실행한다.",
        Boundary = "Unity GameObject가 아니라 Local Runtime이 권위 상태를 변경한다.",
        SubmoduleKey = SsalddelEvidenceSubmoduleKeys.E2로컬권위Adapter)]
    public sealed class LocalSimulationRuntime : ISimulationRuntimeModules,
        ISimulationSessionRuntime, ISimulationNatureSurvivalRuntime,
        ISimulationSessionGameplayRuntime, ISimulationWorldInteractionRuntime,
        ISimulationBattleRuntime, ISimulationActorEquipmentRuntime,
        ISimulationPlayerKnowledgeRuntime, ISimulation방문자체류Runtime,
        ISimulationPlayerLearningFocusRuntime, ISimulationPlayerIdeaMapRuntime,
        ISimulationHexagramCampaignRuntime, ISimulation절기운영CampaignRuntime,
        ISimulationNpcPolicyRuntime, ISimulationFoodOrderRuntime,
        IDisposable
    {
        private readonly SemaphoreSlim commandGate = new SemaphoreSlim(1, 1);
        private readonly ISimulationSessionSaveStore saveStore;
        private readonly ISimulationLocalSaveSlotStore slotStore;
        private readonly I경영SimulationSessionStore sessionStore;
        private readonly 경영SimulationSession생명주기Service lifecycle;
        private readonly SimulationNatureSurvivalService nature;
        private readonly SimulationFarmSurvivalService farm;
        private readonly SimulationRegionalIncidentService regionalIncidents;
        private readonly InMemorySimulationTeamObservationPolicyStore battlePolicies;
        private readonly SimulationBattleInstanceService battles;
        private readonly Simulation플레이어지식Service playerKnowledge;
        private readonly Simulation공동체방문자체류Service communityVisitors;
        private readonly SimulationPlayerLearningFocusService learningFocus;
        private readonly SimulationPlayerIdeaMapService ideaMap;
        private readonly SimulationHexagramCampaignService hexagramCampaigns;
        private readonly Simulation절기운영CampaignService
            seasonalOperationsCampaigns;
        private readonly ISimulationPlayableLoopEngineTraceSink engineTraceSink;

        public LocalSimulationRuntime(
            I경영SimulationSessionStore sessionStore,
            ISimulationSessionSaveStore sessionSaveStore,
            ISimulationLocalSaveSlotStore localSaveSlotStore,
            ISimulationBattleWorldReconciler? battleReconciler = null,
            SimulationBattleInstanceService? battleService = null,
            ISimulationPlayableLoopEngineTraceSink? playableLoopEngineTraceSink = null,
            Simulation플레이어지식Service? playerKnowledgeService = null,
            Simulation공동체방문자체류Service? visitorStayService = null,
            ISimulationHexagramCampaignAttemptStore? campaignAttemptStore = null)
        {
            this.sessionStore = sessionStore
                ?? throw new ArgumentNullException(nameof(sessionStore));
            saveStore = sessionSaveStore
                ?? throw new ArgumentNullException(nameof(sessionSaveStore));
            slotStore = localSaveSlotStore
                ?? throw new ArgumentNullException(nameof(localSaveSlotStore));
            engineTraceSink = playableLoopEngineTraceSink
                ?? new InMemorySimulationPlayableLoopEngineTraceSink();

            battlePolicies = new InMemorySimulationTeamObservationPolicyStore();
            battles = battleService ?? new SimulationBattleInstanceService(
                sessionStore, battlePolicies, new LocalSimulationBattleInstanceStore());
            playerKnowledge = playerKnowledgeService
                ?? new Simulation플레이어지식Service(
                    new InMemorySimulation플레이어지식Store());
            communityVisitors = visitorStayService
                ?? new Simulation공동체방문자체류Service(
                    new InMemorySimulation공동체방문자체류Store());
            learningFocus = new SimulationPlayerLearningFocusService(sessionStore);
            ideaMap = new SimulationPlayerIdeaMapService(sessionStore);
            var sessions = new 경영SimulationSessionAccessor(sessionStore);
            var attempts = campaignAttemptStore
                ?? new InMemorySimulationHexagramCampaignAttemptStore();
            lifecycle = new 경영SimulationSession생명주기Service(
                sessions, saveStore, battleReconciler ?? battles, attempts);
            hexagramCampaigns = new SimulationHexagramCampaignService(
                sessions, lifecycle, attempts);
            seasonalOperationsCampaigns =
                new Simulation절기운영CampaignService(sessions);
            var worldInteractionPipeline = new 세계상호작용실행Pipeline(
                engineTraceSink);
            nature = new SimulationNatureSurvivalService(sessionStore,
                worldInteractionPipeline,
                authorityLocationCode:
                    SimulationAuthorityLocation.LocalProcess.ToString());
            farm = new SimulationFarmSurvivalService(sessionStore,
                worldInteractionPipeline: worldInteractionPipeline,
                authorityLocationCode:
                    SimulationAuthorityLocation.LocalProcess.ToString());
            regionalIncidents = new SimulationRegionalIncidentService(sessions,
                worldInteractionPipeline);
        }

        public SimulationRuntimeDescriptor Descriptor { get; } = new()
        {
            AuthorityLocation = SimulationAuthorityLocation.LocalProcess,
            Purpose = SimulationRuntimePurpose.Playable,
            RuntimeStableId = "simulation-runtime:local-process",
            RequiresNetwork = false,
        };

        public ISimulationSessionRuntime Sessions => this;
        public ISimulationNatureSurvivalRuntime Nature => this;
        public ISimulationSessionGameplayRuntime Gameplay => this;
        public ISimulationWorldInteractionRuntime WorldInteractions => this;
        public ISimulationTurnRuntime Turns => this;
        public ISimulationFarmChoiceRuntime FarmChoices => this;
        public ISimulationLogisticsRuntime Logistics => this;
        public ISimulationFarmWorldInteractionRuntime FarmWorldInteractions => this;
        public ISimulationNatureWorldInteractionRuntime NatureWorldInteractions => this;
        public ISimulationBattleRuntime Battles => this;
        public ISimulationActorEquipmentRuntime ActorEquipment => this;
        public ISimulationPlayerKnowledgeRuntime PlayerKnowledge => this;
        public ISimulation방문자체류Runtime CommunityVisitors => this;
        public ISimulationPlayerLearningFocusRuntime LearningFocus => this;
        public ISimulationPlayerIdeaMapRuntime IdeaMap => this;
        public ISimulationHexagramCampaignRuntime HexagramCampaigns => this;
        public ISimulation절기운영CampaignRuntime SeasonalOperationsCampaigns
            => this;

        public ValueTask<Simulation절기운영CampaignStateSnapshot>
            GetSeasonalOperationsCampaignAsync(string sessionStableId,
                CancellationToken cancellationToken = default)
            => ExecuteAsync(() => seasonalOperationsCampaigns.Get(
                sessionStableId), cancellationToken);

        public ValueTask<Simulation절기운영CampaignStateSnapshot>
            BeginSeasonalOperationsCampaignAsync(string sessionStableId,
                Simulation절기운영CampaignStartRequest request,
                CancellationToken cancellationToken = default)
            => ExecuteAsync(() => seasonalOperationsCampaigns.Begin(
                sessionStableId, request), cancellationToken);

        public ValueTask<Simulation절기운영CampaignAdvancePreviewSnapshot>
            PreviewSeasonalOperationsCampaignAdvanceAsync(
                string sessionStableId,
                Simulation절기운영CampaignAdvancePreviewRequest request,
                CancellationToken cancellationToken = default)
            => ExecuteAsync(() => seasonalOperationsCampaigns.PreviewAdvance(
                sessionStableId, request), cancellationToken);

        public ValueTask<Simulation절기운영CampaignStateSnapshot>
            ConfirmSeasonalOperationsCampaignAdvanceAsync(
                string sessionStableId,
                Simulation절기운영CampaignAdvanceConfirmRequest request,
                CancellationToken cancellationToken = default)
            => ExecuteAsync(() => seasonalOperationsCampaigns.ConfirmAdvance(
                sessionStableId, request), cancellationToken);

        public ValueTask<Simulation플레이어이데아맵ProjectionSnapshot>
            GetPlayerIdeaMapAsync(string sessionStableId,
                string playerStableId,
                CancellationToken cancellationToken = default)
            => ExecuteAsync(() => ideaMap.Get(sessionStableId,
                playerStableId), cancellationToken);

        public ValueTask<SimulationHexagramCampaignStateSnapshot>
            GetHexagramCampaignAsync(string sessionStableId,
                CancellationToken cancellationToken = default)
            => ExecuteAsync(() => hexagramCampaigns.Get(sessionStableId),
                cancellationToken);

        public ValueTask<SimulationHexagramCampaignStateSnapshot>
            EnterHexagramCampaignAsync(string sessionStableId,
                SimulationHexagramCampaignEnterRequest request,
                CancellationToken cancellationToken = default)
            => ExecuteAsync(() => hexagramCampaigns.Enter(sessionStableId,
                request), cancellationToken);

        public ValueTask<SimulationHexagramCampaignStateSnapshot>
            CompleteHexagramLineAsync(string sessionStableId,
                SimulationHexagramCampaignLineCompleteRequest request,
                CancellationToken cancellationToken = default)
            => ExecuteAsync(() => hexagramCampaigns.CompleteLine(
                sessionStableId, request), cancellationToken);

        public ValueTask<SimulationHexagramCampaignStateSnapshot>
            RecordHexagramSetbackAsync(string sessionStableId,
                SimulationHexagramCampaignSetbackRequest request,
                CancellationToken cancellationToken = default)
            => ExecuteAsync(() => hexagramCampaigns.RecordSetback(
                sessionStableId, request), cancellationToken);

        public ValueTask<SimulationHexagramCampaignStateSnapshot>
            FailHexagramCampaignAsync(string sessionStableId,
                SimulationHexagramCampaignFailureRequest request,
                CancellationToken cancellationToken = default)
            => ExecuteAsync(() => hexagramCampaigns.Fail(sessionStableId,
                request), cancellationToken);

        public ValueTask<SimulationHexagramCampaignStateSnapshot>
            CompleteHexagramCampaignAsync(string sessionStableId,
                SimulationHexagramCampaignCompleteRequest request,
                CancellationToken cancellationToken = default)
            => ExecuteAsync(() => hexagramCampaigns.Complete(sessionStableId,
                request), cancellationToken);

        public ValueTask<Simulation학습중점ProjectionSnapshot>
            GetLearningFocusAsync(string sessionStableId,
                string playerStableId,
                CancellationToken cancellationToken = default)
            => ExecuteAsync(() => learningFocus.Get(sessionStableId,
                playerStableId), cancellationToken);

        public ValueTask<Simulation학습중점PreviewSnapshot>
            PreviewLearningFocusAsync(string sessionStableId,
                Simulation학습중점ChangeRequest request,
                CancellationToken cancellationToken = default)
            => ExecuteAsync(() => learningFocus.Preview(sessionStableId,
                request), cancellationToken);

        public ValueTask<Simulation학습중점StateSnapshot>
            ConfirmLearningFocusAsync(string sessionStableId,
                Simulation학습중점ChangeRequest request,
                CancellationToken cancellationToken = default)
            => ExecuteAsync(() => learningFocus.Confirm(sessionStableId,
                request), cancellationToken);

        public ValueTask<Simulation공동체방문자체류LedgerSnapshot> GetVisitorsAsync(
            string ledgerStableId, CancellationToken cancellationToken = default)
            => ExecuteAsync(() => communityVisitors.Get(ledgerStableId), cancellationToken);

        public ValueTask<Simulation공동체방문자체류PreviewSnapshot> PreviewVisitorStayAsync(
            string ledgerStableId, Simulation공동체방문자체류PreviewRequest request,
            CancellationToken cancellationToken = default)
            => ExecuteAsync(() => communityVisitors.Preview(ledgerStableId, request),
                cancellationToken);

        public ValueTask<Simulation공동체방문자체류ConfirmResult> ConfirmVisitorStayAsync(
            string ledgerStableId, Simulation공동체방문자체류ConfirmRequest request,
            CancellationToken cancellationToken = default)
            => ExecuteAsync(() => communityVisitors.Confirm(ledgerStableId, request),
                cancellationToken);

        public ValueTask<Simulation플레이어지식LedgerSnapshot>
            GetPlayerKnowledgeAsync(string ledgerStableId,
                CancellationToken cancellationToken = default)
            => ExecuteAsync(() => playerKnowledge.Get(ledgerStableId),
                cancellationToken);

        public ValueTask<Simulation지식습득PreviewSnapshot>
            PreviewPlayerKnowledgeAsync(string ledgerStableId,
                Simulation지식습득PreviewRequest request,
                CancellationToken cancellationToken = default)
            => ExecuteAsync(() => playerKnowledge.Preview(ledgerStableId, request),
                cancellationToken);

        public ValueTask<Simulation지식습득ConfirmResult>
            ConfirmPlayerKnowledgeAsync(string ledgerStableId,
                Simulation지식습득ConfirmRequest request,
                CancellationToken cancellationToken = default)
            => ExecuteAsync(() => playerKnowledge.Confirm(ledgerStableId, request),
                cancellationToken);

        public ValueTask<SimulationActorEquipmentStateSnapshot>
            GetActorEquipmentAsync(string sessionStableId,
                CancellationToken cancellationToken = default)
            => ExecuteAsync(() => RequireSession(sessionStableId)
                .GetActorEquipmentState(), cancellationToken);

        public ValueTask<SimulationActorItemAcquirePreviewSnapshot>
            PreviewActorItemAcquireAsync(string sessionStableId,
                SimulationActorItemAcquirePreviewRequest request,
                CancellationToken cancellationToken = default)
            => ExecuteAsync(() => RequireSession(sessionStableId)
                .PreviewActorItemAcquire(request), cancellationToken);

        public ValueTask<SimulationActorEquipmentStateSnapshot>
            ConfirmActorItemAcquireAsync(string sessionStableId,
                SimulationActorItemAcquireConfirmRequest request,
                CancellationToken cancellationToken = default)
            => ExecuteAsync(() => RequireSession(sessionStableId)
                .ConfirmActorItemAcquire(request), cancellationToken);

        public ValueTask<SimulationActorEquipmentChangePreviewSnapshot>
            PreviewActorEquipmentChangeAsync(string sessionStableId,
                SimulationActorEquipmentChangePreviewRequest request,
                CancellationToken cancellationToken = default)
            => ExecuteAsync(() => RequireSession(sessionStableId)
                .PreviewActorEquipmentChange(request), cancellationToken);

        public ValueTask<SimulationActorEquipmentStateSnapshot>
            ConfirmActorEquipmentChangeAsync(string sessionStableId,
                SimulationActorEquipmentChangeConfirmRequest request,
                CancellationToken cancellationToken = default)
            => ExecuteAsync(() => RequireSession(sessionStableId)
                .ConfirmActorEquipmentChange(request), cancellationToken);

        public SimulationPlayableLoopEngineTraceEntry[]
            GetPlayableLoopEngineTrace(string playableLoopStableId,
                string worldInteractionId, string commandId)
            => engineTraceSink.Snapshot(playableLoopStableId,
                worldInteractionId, commandId);

        public ValueTask<경영SimulationSessionSnapshot> CreateAsync(
            경영SimulationSession생성Request request,
            CancellationToken cancellationToken = default)
            => ExecuteAsync(() => lifecycle.Create(request), cancellationToken);

        ValueTask<경영SimulationSessionSnapshot> ISimulationSessionRuntime.GetAsync(
            string sessionStableId,
            CancellationToken cancellationToken)
            => ExecuteAsync(() => lifecycle.Get(sessionStableId), cancellationToken);

        public ValueTask<경영SimulationSessionSnapshot> GetPolicySessionAsync(
            string sessionStableId, CancellationToken cancellationToken = default)
            => ExecuteAsync(() => lifecycle.Get(sessionStableId), cancellationToken);

        public ValueTask<Simulation음식배달PreviewSnapshot> PreviewFoodDeliveryAsync(
            string sessionStableId, Simulation음식배달PreviewRequest request,
            CancellationToken token = default)
            => ExecuteAsync(() => RequireSession(sessionStableId).PreviewFoodDelivery(request), token);

        public ValueTask<경영SimulationSessionSnapshot> ConfirmRestaurantResponseAsync(
            string sessionStableId, Simulation음식점응답Request request,
            CancellationToken token = default)
            => ExecuteAsync(() => RequireSession(sessionStableId).ConfirmRestaurantResponse(request), token);

        public ValueTask<경영SimulationSessionSnapshot> ConfirmFoodDeliveryAsync(
            string sessionStableId, Simulation음식배달ConfirmRequest request,
            CancellationToken token = default)
            => ExecuteAsync(() => RequireSession(sessionStableId).ConfirmFoodDelivery(request), token);

        public ValueTask<SimulationDecisionPreviewSnapshot> PreviewFoodDeliveryReceiptAsync(
            string sessionStableId, Simulation음식배달수령PreviewRequest request,
            CancellationToken token = default)
            => ExecuteAsync(() => RequireSession(sessionStableId).PreviewFoodDeliveryReceipt(request), token);

        public ValueTask<경영SimulationSessionSnapshot> ConfirmFoodDeliveryReceiptAsync(
            string sessionStableId, Simulation음식배달수령ConfirmRequest request,
            CancellationToken token = default)
            => ExecuteAsync(() => RequireSession(sessionStableId).ConfirmFoodDeliveryReceipt(request), token);

        public ValueTask<경영SimulationSessionSnapshot> UpdateNpcPolicyAsync(
            string sessionStableId, SimulationNpcPolicyChangeRequest request,
            CancellationToken cancellationToken = default)
            => ExecuteAsync(() => RequireSession(sessionStableId).UpdateNpcPolicy(request), cancellationToken);

        public ValueTask<경영SimulationSessionSnapshot> AdvanceWorldTickAsync(
            string sessionStableId,
            경영SimulationTick진행Request request,
            CancellationToken cancellationToken = default)
            => ExecuteAsync(() => lifecycle.Advance(sessionStableId, request),
                cancellationToken);

        public ValueTask<SimulationNpcRoutineWorkProjection[]>
            GetNpcRoutineWorkAsync(string sessionStableId, string areaCode,
                CancellationToken cancellationToken = default)
            => ExecuteAsync(() => RequireSession(sessionStableId)
                .GetNpcRoutineWork(areaCode), cancellationToken);

        public ValueTask<SimulationSpatialCompositionStateSnapshot>
            GetSpatialCompositionAsync(string sessionStableId, string areaCode,
                CancellationToken cancellationToken = default)
            => ExecuteAsync(() => RequireSession(sessionStableId)
                .GetSpatialComposition(areaCode), cancellationToken);

        ValueTask<SimulationNatureSurvivalStateSnapshot>
            ISimulationNatureSurvivalRuntime.GetAsync(
            string sessionStableId,
            CancellationToken cancellationToken)
            => ExecuteAsync(() => nature.Get(sessionStableId), cancellationToken);

        public ValueTask<Simulation영역건물발전Snapshot>
            GetBuildingProgressionAsync(string sessionStableId, string areaCode,
                CancellationToken cancellationToken = default)
            => ExecuteAsync(() => nature.GetBuildingProgression(
                sessionStableId, areaCode), cancellationToken);

        /// <summary>Local 명령 gate를 한 번 획득하여 상태/완료 기록의 서로 다른 조회 시점을 방지한다.</summary>
        public ValueTask<SimulationNature표현관측Snapshot> GetNature표현관측Async(
            string sessionStableId, CancellationToken cancellationToken = default)
            => ExecuteAsync(() => RequireSession(sessionStableId)
                .GetNature표현관측Snapshot(), cancellationToken);

        public ValueTask<Simulation플레이어기회Snapshot[]>
            GetPlayerOpportunitiesAsync(string sessionStableId,
                CancellationToken cancellationToken = default)
            => ExecuteAsync(() => nature.GetPlayerOpportunities(sessionStableId),
                cancellationToken);

        public ValueTask<Simulation영역수요Snapshot[]>
            GetAreaNeedsAsync(string sessionStableId,
                CancellationToken cancellationToken = default)
            => ExecuteAsync(() => nature.GetAreaNeeds(sessionStableId),
                cancellationToken);

        public ValueTask<SimulationNatureSurvivalActionPreviewSnapshot> PreviewAsync(
            string sessionStableId,
            SimulationNatureSurvivalActionPreviewRequest request,
            CancellationToken cancellationToken = default)
            => ExecuteAsync(() => nature.Preview(sessionStableId, request),
                cancellationToken);

        public ValueTask<경영SimulationSessionSnapshot> ConfirmAsync(
            string sessionStableId,
            SimulationNatureSurvivalCommandRequest request,
            CancellationToken cancellationToken = default)
            => ExecuteAsync(() => nature.Confirm(sessionStableId, request),
                cancellationToken);

        public ValueTask<경영SimulationSessionSnapshot> AdvanceRealtimeAsync(
            string sessionStableId,
            SimulationNatureSurvivalClockAdvanceRequest request,
            CancellationToken cancellationToken = default)
            => ExecuteAsync(() => nature.AdvanceClock(sessionStableId, request),
                cancellationToken);

        public ValueTask<Simulation집중판정ChallengeSnapshot>
            SubmitFocusTimingAsync(string sessionStableId,
                Simulation집중판정AttemptRequest request,
                CancellationToken cancellationToken = default)
            => ExecuteAsync(() => nature.SubmitFocusTiming(
                sessionStableId, request), cancellationToken);

        public ValueTask<SimulationTurnClosingContextSnapshot>
            GetTurnClosingContextAsync(string sessionStableId,
                CancellationToken cancellationToken = default)
            => ExecuteAsync(() => RequireSession(sessionStableId)
                .GetTurnClosingContext(), cancellationToken);

        public ValueTask<SimulationTownNpcLifeStateSnapshot>
            GetTownNpcLifeStateAsync(string sessionStableId,
                CancellationToken cancellationToken = default)
            => ExecuteAsync(() => RequireSession(sessionStableId)
                .GetTownNpcLifeState(), cancellationToken);

        public ValueTask<SimulationTurnClosingPreviewSnapshot>
            PreviewTurnClosingAsync(string sessionStableId,
                SimulationTurnClosingPreviewRequest request,
                CancellationToken cancellationToken = default)
            => ExecuteAsync(() => RequireSession(sessionStableId)
                .PreviewTurnClosing(request), cancellationToken);

        public ValueTask<경영SimulationSessionSnapshot> ConfirmTurnClosingAsync(
            string sessionStableId, SimulationTurnClosingConfirmRequest request,
            CancellationToken cancellationToken = default)
            => ExecuteAsync(() => RequireSession(sessionStableId)
                .ConfirmTurnClosing(request), cancellationToken);

        public ValueTask<Simulation타로객체반응PreviewSnapshot>
            PreviewTarotObjectReactionAsync(string sessionStableId,
                Simulation타로객체반응PreviewRequest request,
                CancellationToken cancellationToken = default)
            => ExecuteAsync(() => RequireSession(sessionStableId)
                .Preview타로객체반응(request), cancellationToken);

        public ValueTask<SimulationFarmChoiceContextSnapshot>
            GetFarmChoiceContextAsync(string sessionStableId,
                CancellationToken cancellationToken = default)
            => ExecuteAsync(() => RequireSession(sessionStableId)
                .GetFarmChoiceContext(), cancellationToken);

        public ValueTask<SimulationFarmChoicePreviewSnapshot>
            PreviewFarmChoiceAsync(string sessionStableId,
                SimulationFarmChoicePreviewRequest request,
                CancellationToken cancellationToken = default)
            => ExecuteAsync(() => RequireSession(sessionStableId)
                .PreviewFarmChoice(request), cancellationToken);

        public ValueTask<경영SimulationSessionSnapshot> ConfirmFarmChoiceAsync(
            string sessionStableId, SimulationFarmChoiceConfirmRequest request,
            CancellationToken cancellationToken = default)
            => ExecuteAsync(() => RequireSession(sessionStableId)
                .ConfirmFarmChoice(request), cancellationToken);

        public ValueTask<SimulationLogisticsMovementPreviewSnapshot>
            PreviewLogisticsMovementAsync(string sessionStableId,
                SimulationLogisticsMovementPreviewRequest request,
                CancellationToken cancellationToken = default)
            => ExecuteAsync(() => RequireSession(sessionStableId)
                .PreviewLogisticsMovement(request), cancellationToken);

        public ValueTask<경영SimulationSessionSnapshot>
            ConfirmLogisticsMovementAsync(string sessionStableId,
                SimulationLogisticsMovementConfirmRequest request,
                CancellationToken cancellationToken = default)
            => ExecuteAsync(() => RequireSession(sessionStableId)
                .ConfirmLogisticsMovement(request), cancellationToken);

        public ValueTask<SimulationFreightDispatchPreviewSnapshot>
            PreviewFreightDispatchAsync(string sessionStableId,
                SimulationFreightDispatchPreviewRequest request,
                CancellationToken cancellationToken = default)
            => ExecuteAsync(() => RequireSession(sessionStableId)
                .PreviewFreightDispatch(request), cancellationToken);

        public ValueTask<경영SimulationSessionSnapshot>
            ConfirmFreightDispatchAsync(string sessionStableId,
                SimulationFreightDispatchConfirmRequest request,
                CancellationToken cancellationToken = default)
            => ExecuteAsync(() => RequireSession(sessionStableId)
                .ConfirmFreightDispatch(request), cancellationToken);

        public ValueTask<SimulationFarmWorkPreviewSnapshot> PreviewFarmWorkAsync(
            string sessionStableId, SimulationFarmWorkPreviewRequest request,
            CancellationToken cancellationToken = default)
            => ExecuteAsync(() => farm.PreviewWork(sessionStableId, request),
                cancellationToken);

        public ValueTask<SimulationFarmConstructionPlacementPreviewSnapshot>
            PreviewFarmConstructionPlacementAsync(string sessionStableId,
                SimulationFarmConstructionPlacementPreviewRequest request,
                CancellationToken cancellationToken = default)
            => ExecuteAsync(() => RequireSession(sessionStableId)
                .PreviewFarmConstructionPlacement(request), cancellationToken);

        public ValueTask<경영SimulationSessionSnapshot>
            ConfirmFarmConstructionPlacementAsync(string sessionStableId,
                SimulationFarmConstructionPlacementConfirmRequest request,
                CancellationToken cancellationToken = default)
            => ExecuteAsync(() => RequireSession(sessionStableId)
                .ConfirmFarmConstructionPlacement(request), cancellationToken);

        public ValueTask<SimulationFarmSurvivalStateSnapshot> ConfirmFarmWorkAsync(
            string sessionStableId, SimulationFarmWorkConfirmRequest request,
            CancellationToken cancellationToken = default)
            => ExecuteAsync(() => farm.ConfirmWork(sessionStableId, request),
                cancellationToken);

        public ValueTask<SimulationNatureThreatObservationPreviewSnapshot>
            PreviewNatureThreatObservationAsync(string sessionStableId,
                SimulationNatureThreatObservationPreviewRequest request,
                CancellationToken cancellationToken = default)
            => ExecuteAsync(() => regionalIncidents.PreviewThreatObservation(
                sessionStableId, request), cancellationToken);

        public ValueTask<경영SimulationSessionSnapshot>
            ConfirmNatureThreatObservationAsync(string sessionStableId,
                SimulationNatureThreatObservationConfirmRequest request,
                CancellationToken cancellationToken = default)
            => ExecuteAsync(() => regionalIncidents.ConfirmThreatObservation(
                sessionStableId, request), cancellationToken);

        public ValueTask<SimulationNatureEmergencyRetreatPreviewSnapshot>
            PreviewNatureEmergencyRetreatAsync(string sessionStableId,
                SimulationNatureEmergencyRetreatPreviewRequest request,
                CancellationToken cancellationToken = default)
            => ExecuteAsync(() => regionalIncidents.PreviewEmergencyRetreat(
                sessionStableId, request), cancellationToken);

        public ValueTask<경영SimulationSessionSnapshot>
            ConfirmNatureEmergencyRetreatAsync(string sessionStableId,
                SimulationNatureEmergencyRetreatConfirmRequest request,
                CancellationToken cancellationToken = default)
            => ExecuteAsync(() => regionalIncidents.ConfirmEmergencyRetreat(
                sessionStableId, request), cancellationToken);

        public ValueTask<SimulationNatureRestorationPreviewSnapshot>
            PreviewNatureRestorationAsync(string sessionStableId,
                SimulationNatureRestorationPreviewRequest request,
                CancellationToken cancellationToken = default)
            => ExecuteAsync(() => regionalIncidents.PreviewRestoration(
                sessionStableId, request), cancellationToken);

        public ValueTask<경영SimulationSessionSnapshot>
            ConfirmNatureRestorationAsync(string sessionStableId,
                SimulationNatureRestorationConfirmRequest request,
                CancellationToken cancellationToken = default)
            => ExecuteAsync(() => regionalIncidents.ConfirmRestoration(
                sessionStableId, request), cancellationToken);

        public ValueTask<SimulationNaturePartyRecoveryPreviewSnapshot>
            PreviewNaturePartyRecoveryAsync(string sessionStableId,
                SimulationNaturePartyRecoveryPreviewRequest request,
                CancellationToken cancellationToken = default)
            => ExecuteAsync(() => regionalIncidents.PreviewPartyRecovery(
                sessionStableId, request), cancellationToken);

        public ValueTask<경영SimulationSessionSnapshot>
            ConfirmNaturePartyRecoveryAsync(string sessionStableId,
                SimulationNaturePartyRecoveryConfirmRequest request,
                CancellationToken cancellationToken = default)
            => ExecuteAsync(() => regionalIncidents.ConfirmPartyRecovery(
                sessionStableId, request), cancellationToken);

        public ValueTask<SimulationBattleCreatePreviewSnapshot> PreviewBattleAsync(
            string sessionStableId, SimulationBattleCreatePreviewRequest request,
            CancellationToken cancellationToken = default)
            => ExecuteAsync(() =>
            {
                EnsureLocalBattlePolicy(sessionStableId,
                    request.RequestingActorStableId);
                return battles.PreviewCreate(sessionStableId, request);
            }, cancellationToken);

        public ValueTask<SimulationBattleInstanceSnapshot> ConfirmBattleAsync(
            string sessionStableId, SimulationBattleCreateConfirmRequest request,
            CancellationToken cancellationToken = default)
            => ExecuteAsync(() =>
            {
                EnsureLocalBattlePolicy(sessionStableId,
                    request.RequestingActorStableId);
                return battles.ConfirmCreate(sessionStableId, request);
            }, cancellationToken);

        public ValueTask<SimulationBattleInstanceSnapshot>
            ConfirmBattleControlModeAsync(string sessionStableId,
                string battleStableId,
                SimulationLocalCombatControlModeConfirmRequest request,
                CancellationToken cancellationToken = default)
            => ExecuteAsync(() =>
            {
                EnsureLocalBattlePolicy(sessionStableId,
                    request.RequestingActorStableId);
                return battles.ConfirmLocalControlMode(sessionStableId,
                    battleStableId, request);
            }, cancellationToken);

        public ValueTask<SimulationBattleInstanceSnapshot> ConfirmBattleActionAsync(
            string sessionStableId, string battleStableId,
            SimulationLocalCombatActionConfirmRequest request,
            CancellationToken cancellationToken = default)
            => ExecuteAsync(() =>
            {
                EnsureLocalBattlePolicy(sessionStableId,
                    request.RequestingActorStableId);
                return battles.ConfirmLocalAction(sessionStableId,
                    battleStableId, request);
            }, cancellationToken);

        public ValueTask<SimulationBattleInstanceSnapshot>
            ConfirmObserverInterventionAsync(string sessionStableId,
                string battleStableId,
                SimulationLocalCombatObserverInterventionConfirmRequest request,
                CancellationToken cancellationToken = default)
            => ExecuteAsync(() =>
            {
                EnsureLocalBattlePolicy(sessionStableId,
                    request.RequestingActorStableId);
                return battles.ConfirmObserverIntervention(sessionStableId,
                    battleStableId, request);
            }, cancellationToken);

        public ValueTask<SimulationBattleInstanceSnapshot> AdvanceBattleAsync(
            string sessionStableId, string battleStableId,
            SimulationBattleAdvanceRequest request,
            CancellationToken cancellationToken = default)
            => ExecuteAsync(() => battles.Advance(sessionStableId,
                battleStableId, request), cancellationToken);

        /// <summary>같은 직렬화 관문에서 읽은 보고 사본. HTTP/DB 저장이나 추가 업무 명령은 실행하지 않는다.</summary>
        public ValueTask<로컬생활보고> GetLocalLifeReportAsync(string sessionStableId, 로컬생활시작묶음 bundle,
            CancellationToken cancellationToken = default)
            => ExecuteAsync(() => {
                var session = RequireSession(sessionStableId);
                return 로컬생활보고.Create(session.CreateSavePackage(new SimulationSessionSaveRequest {
                    SaveStableId = "offline-report:" + sessionStableId, ExpectedRevision = session.Snapshot().Revision }), bundle);
            }, cancellationToken);

        public ValueTask<SimulationLocalSaveSlotResult> SaveSlotAsync(
            string sessionStableId,
            SimulationLocalSaveSlotRequest request,
            CancellationToken cancellationToken = default)
            => ExecuteAsync(() =>
            {
                if (request == null) throw new ArgumentNullException(nameof(request));
                var slotStableId = RequireSlot(request.SlotStableId);
                var saveStableId = "simulation-save:local:"
                    + slotStableId + ":" + Guid.NewGuid().ToString("N");
                var package = lifecycle.Save(sessionStableId,
                    new SimulationSessionSaveRequest
                    {
                        SaveStableId = saveStableId,
                        ExpectedRevision = request.ExpectedRevision,
                        LhWorldState = request.LhWorldState,
                        WorldAssetPlacementState =
                            request.WorldAssetPlacementState,
                    });
                slotStore.Write(slotStableId, package);
                return new SimulationLocalSaveSlotResult
                {
                    SlotStableId = slotStableId,
                    SaveStableId = package.SaveStableId,
                    ReplayHash = package.ReplayHash,
                    SavedWorldTick = package.SavedWorldTick,
                    SavedWorldRevision = package.SavedWorldRevision,
                };
            }, cancellationToken);

        public ValueTask<SimulationLocalLoadSlotResult> LoadSlotAsync(
            string slotStableId,
            CancellationToken cancellationToken = default)
            => LoadOrVerifyAsync(slotStableId, true, cancellationToken);

        public ValueTask<SimulationLocalLoadSlotResult> VerifySlotAsync(
            string slotStableId,
            CancellationToken cancellationToken = default)
            => LoadOrVerifyAsync(slotStableId, false, cancellationToken);

        private ValueTask<SimulationLocalLoadSlotResult> LoadOrVerifyAsync(
            string slotStableId,
            bool activate,
            CancellationToken cancellationToken)
            => ExecuteAsync(() =>
            {
                var slot = slotStore.Read(RequireSlot(slotStableId));
                saveStore.SaveOrGet(slot.Package);
                var request = new SimulationSessionRestoreRequest
                {
                    SaveStableId = slot.Package.SaveStableId,
                };
                var restored = activate
                    ? lifecycle.Restore(request)
                    : lifecycle.VerifyReplay(request);
                return new SimulationLocalLoadSlotResult
                {
                    SlotStableId = slot.SlotStableId,
                    RecoveredFromBackup = slot.RecoveredFromBackup,
                    Restore = restored,
                };
            }, cancellationToken);

        private async ValueTask<T> ExecuteAsync<T>(Func<T> action,
            CancellationToken cancellationToken)
        {
            await commandGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                return action();
            }
            finally
            {
                commandGate.Release();
            }
        }

        private static string RequireSlot(string slotStableId)
        {
            if (string.IsNullOrWhiteSpace(slotStableId))
                throw new SimulationContractException("SimulationLocalSaveSlotInvalid");
            return slotStableId.Trim();
        }

        private 경영SimulationSessionAggregate RequireSession(string sessionStableId)
        {
            if (string.IsNullOrWhiteSpace(sessionStableId))
                throw new SimulationContractException("SimulationSessionStableIdMissing");
            return sessionStore.Find(sessionStableId.Trim())
                ?? throw new SimulationNotFoundException("SimulationSessionNotFound");
        }

        private void EnsureLocalBattlePolicy(string sessionStableId,
            string actorStableId)
        {
            if (string.IsNullOrWhiteSpace(sessionStableId)
                || string.IsNullOrWhiteSpace(actorStableId))
                throw new SimulationContractException(
                    "SimulationBattleActorInvalid");
            battlePolicies.Replace(new SimulationTeamObservationPolicySnapshot
            {
                SessionStableId = sessionStableId.Trim(),
                TeamStableId = "team:local-player:" + actorStableId.Trim(),
                Revision = RequireSession(sessionStableId).Revision,
                MembersCanObserve = true,
                MemberActorStableIds = new[] { actorStableId.Trim() },
                AllowedViewModeCodes = new[]
                {
                    "FirstPerson", "TacticalThirdPerson", "ObserverOperation",
                },
                SimulationOnly = true,
                IsOperationalState = false,
            });
        }

        public void Dispose() => commandGate.Dispose();
    }
}
