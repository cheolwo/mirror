using System;
using Ssalddel.Contracts.Common.Metadata;

namespace Ssalddel.Simulation.Contracts
{
    public static class SimulationTickRuleRevisions
    {
        // 빈 값은 기존 합산 Tick 재생 규칙이다. 과거 저장을 자동 승격하지 않는다.
        public const string SingleStep = "world-tick-step.r1";
    }
    public static class SimulationSaveSchemaVersions
    {
        public const string V1 = "simulation-save.v1";
        public const string V2 = "simulation-save.v2";
        public const string V3 = "simulation-save.v3";
        public const string V4 = "simulation-save.v4";
        public const string V5 = "simulation-save.v5";
        public const string V6 = "simulation-save.v6";
        public const string V7 = "simulation-save.v7";
        public const string V8 = "simulation-save.v8";
        public const string V9 = "simulation-save.v9";
        public const string V10 = "simulation-save.v10";
        public const string V11 = "simulation-save.v11";
        public const string V12 = "simulation-save.v12";
        public const string V13 = "simulation-save.v13";
        public const string V14 = "simulation-save.v14";
        public const string V15 = "simulation-save.v15";
        public const string V16 = "simulation-save.v16";
        public const string V17 = "simulation-save.v17";
        public const string V18 = "simulation-save.v18";
        public const string V19 = "simulation-save.v19";
        public const string V20 = "simulation-save.v20";
        public const string V21 = "simulation-save.v21";
        public const string V22 = "simulation-save.v22";
        public const string V23 = "simulation-save.v23";
        public const string V24 = "simulation-save.v24";
        public const string V25 = "simulation-save.v25";
        public const string V26 = "simulation-save.v26";
        public const string V27 = "simulation-save.v27";
        public const string V28 = "simulation-save.v28";
        public const string V29 = "simulation-save.v29";
        public const string V30 = "simulation-save.v30";
        public const string V31 = "simulation-save.v31";
        public const string V32 = "simulation-save.v32";
        public const string V33 = "simulation-save.v33";
    }

    public static class SimulationReplayHashAlgorithmCodes
    {
        public const string Sha256 = "SHA-256";
    }

    public static class SimulationCommandTypeCodes
    {
        public const string DecisionConfirm = "DecisionConfirm";
        public const string HarvestDispositionImpactConfirm = "HarvestDispositionImpactConfirm";
        public const string LogisticsMovementConfirm = "LogisticsMovementConfirm";
        public const string TurnClosingConfirm = "TurnClosingConfirm";
        public const string NpcPolicyChange = "NpcPolicyChange";
        public const string WorldItemAcquisitionConfirm = "WorldItemAcquisitionConfirm";
        public const string SurvivalTarotResponseConfirm = "SurvivalTarotResponseConfirm";
        public const string SurvivalTarotResolutionConfirm = "SurvivalTarotResolutionConfirm";
        public const string FarmWorkConfirm = "FarmWorkConfirm";
        public const string FarmWorkPlanConfirm = "FarmWorkPlanConfirm";
        public const string ThreatResponseConfirm = "ThreatResponseConfirm";
        public const string CombatPerspectiveConfirm = "CombatPerspectiveConfirm";
        public const string CombatBeatStart = "CombatBeatStart";
        public const string CombatReactionConfirm = "CombatReactionConfirm";
        public const string TacticalOrderConfirm = "TacticalOrderConfirm";
        public const string TeamRoleCardEquip = "TeamRoleCardEquip";
        public const string CombatCardLoadoutSet = "CombatCardLoadoutSet";
        public const string TeamActivityStart = "TeamActivityStart";
        public const string TeamActivityEnd = "TeamActivityEnd";
        public const string TileTraversalConfirm = "TileTraversalConfirm";
        public const string CollectibleCardDraw = "CollectibleCardDraw";
        public const string CollectibleCardTransfer = "CollectibleCardTransfer";
        public const string TickAdvance = "TickAdvance";
        public const string TaskCancel = "TaskCancel";
        public const string RegionalIncidentResponseConfirm = "RegionalIncidentResponseConfirm";
        public const string NatureEncounterVictory = "NatureEncounterVictory";
        public const string IntegratedWorldConfirm = "IntegratedWorldConfirm";
        public const string IntegratedWorldEffectEnqueued = "IntegratedWorldEffectEnqueued";
        public const string NatureSurvivalActionConfirm = "NatureSurvivalActionConfirm";
        public const string NatureSurvivalClockAdvance = "NatureSurvivalClockAdvance";
        public const string NatureFocusTimingAttempt = "NatureFocusTimingAttempt";
        public const string ActorItemAcquireConfirm = "ActorItemAcquireConfirm";
        public const string ActorEquipmentChangeConfirm = "ActorEquipmentChangeConfirm";
        public const string HexagramCampaignStateTransition =
            "HexagramCampaignStateTransition";
        public const string SeasonalOperationsCampaignStateTransition =
            "SeasonalOperationsCampaignStateTransition";
        public const string FranchiseSupplyChainStateTransition =
            "FranchiseSupplyChainStateTransition";
    }

    [SsalddelCodeMetadata(
        SsalddelCodeFeatureKeys.SimulationSaveReplay,
        SsalddelCodeLayer.Contract,
        "세션 저장 식별자와 기대 개정을 정의한다.",
        StepKey = "contract.save-request",
        ExecutionStage = SsalddelCodeExecutionStage.Definition,
        FlowOrder = 10,
        Boundary = "저장 자료는 Simulation 상태만 포함하며 운영 원장과 공공데이터 원본을 복제하지 않는다.")]
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E1,
        "구성 요소의 핵심 계약과 불변 경계를 정의한다.",
        Boundary = "계약 정의는 실행 효과나 E 단계 달성 증거를 소유하지 않는다.",
        SubmoduleKey = Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceSubmoduleKeys.E1저장재생계약)]
    public sealed class SimulationSessionSaveRequest
    {
        public string SaveStableId { get; set; } = string.Empty;
        public long ExpectedRevision { get; set; }
        public SimulationLhWorldStateSnapshot? LhWorldState { get; set; }
        public SimulationWorldAssetPlacementStateSnapshot?
            WorldAssetPlacementState { get; set; }
        public Simulation행위기록LedgerSnapshot? ActionManifestationLedger { get; set; }
        public Simulation플레이어분야ProfileSnapshot? PlayerDomainProfile { get; set; }
        public Simulation플레이어분야ProfileSnapshot[] PlayerDomainProfiles
            { get; set; } = Array.Empty<Simulation플레이어분야ProfileSnapshot>();
    }

    public sealed class SimulationSessionRestoreRequest
    {
        public string SaveStableId { get; set; } = string.Empty;
    }

    public sealed class SimulationCommandLogEntrySnapshot
    {
        public long Sequence { get; set; }
        public string CommandTypeCode { get; set; } = string.Empty;
        public int AppliedWorldTick { get; set; }
        public long ResultingWorldRevision { get; set; }
        public 경영SimulationTick진행Request? TickRequest { get; set; }
        public SimulationDecisionConfirmRequest? DecisionConfirmRequest { get; set; }
        public SimulationHarvestDispositionImpactConfirmRequest? HarvestDispositionImpactConfirmRequest { get; set; }
        public SimulationLogisticsMovementConfirmRequest? LogisticsMovementConfirmRequest { get; set; }
        public SimulationTurnClosingConfirmRequest? TurnClosingConfirmRequest { get; set; }
        public SimulationNpcPolicyChangeRequest? NpcPolicyChangeRequest { get; set; }
        public SimulationWorldItemAcquisitionConfirmRequest? WorldItemAcquisitionConfirmRequest
            { get; set; }
        public SimulationSurvivalTarotResponseConfirmRequest? SurvivalTarotResponseConfirmRequest
            { get; set; }
        public SimulationSurvivalTarotResolutionConfirmRequest? SurvivalTarotResolutionConfirmRequest
            { get; set; }
        public SimulationFarmWorkConfirmRequest? FarmWorkConfirmRequest { get; set; }
        public SimulationFarmWorkPlanConfirmRequest? FarmWorkPlanConfirmRequest { get; set; }
        public SimulationThreatResponseConfirmRequest? ThreatResponseConfirmRequest { get; set; }
        public SimulationCombatPerspectiveConfirmRequest? CombatPerspectiveConfirmRequest
            { get; set; }
        public SimulationCombatBeatStartRequest? CombatBeatStartRequest { get; set; }
        public SimulationCombatReactionConfirmRequest? CombatReactionConfirmRequest
            { get; set; }
        public SimulationTacticalOrderConfirmRequest? TacticalOrderConfirmRequest
            { get; set; }
        public SimulationTeamRoleCardEquipRequest? TeamRoleCardEquipRequest { get; set; }
        public SimulationCombatCardLoadoutSetRequest? CombatCardLoadoutSetRequest
            { get; set; }
        public SimulationTeamActivityStartRequest? TeamActivityStartRequest { get; set; }
        public SimulationTeamActivityEndRequest? TeamActivityEndRequest { get; set; }
        public SimulationTileTraversalConfirmRequest? TileTraversalConfirmRequest { get; set; }
        public SimulationCollectibleCardDrawRequest? CollectibleCardDrawRequest { get; set; }
        public SimulationCollectibleCardTransferRequest? CollectibleCardTransferRequest { get; set; }
        public SimulationTaskCancelRequest? TaskCancelRequest { get; set; }
        public string? TaskStableId { get; set; }
        public SimulationRegionalIncidentResponseConfirmRequest?
            RegionalIncidentResponseConfirmRequest { get; set; }
        public string? WorldEventStableId { get; set; }
        public SimulationNatureEncounterVictoryRequest? NatureEncounterVictoryRequest
            { get; set; }
        public SimulationIntegratedWorldCommandRequest? IntegratedWorldConfirmRequest
            { get; set; }
        public SimulationFacilityDamageQueueRequest? FacilityDamageQueueRequest { get; set; }
        public SimulationNatureSurvivalCommandRequest? NatureSurvivalActionRequest
            { get; set; }
        public SimulationNatureSurvivalClockAdvanceRequest? NatureSurvivalClockAdvanceRequest
            { get; set; }
        public Simulation집중판정AttemptRequest? NatureFocusTimingAttemptRequest
            { get; set; }
        public SimulationActorItemAcquireConfirmRequest? ActorItemAcquireConfirmRequest
            { get; set; }
        public SimulationActorEquipmentChangeConfirmRequest?
            ActorEquipmentChangeConfirmRequest { get; set; }
        public SimulationHexagramCampaignStateSnapshot? HexagramCampaignState
            { get; set; }
        public string SeasonalOperationsCampaignCommandId { get; set; }
            = string.Empty;
        public string SeasonalOperationsCampaignCommandSignature { get; set; }
            = string.Empty;
        public Simulation절기운영CampaignStateSnapshot?
            SeasonalOperationsCampaignState { get; set; }
        public SimulationWorldInteractionInvocationRecord? WorldInteractionInvocation
            { get; set; }
        public string FranchiseSupplyClientRequestId { get; set; }
            = string.Empty;
        public string FranchiseSupplyCommandSignature { get; set; }
            = string.Empty;
        public 프랜차이즈공급망StateSnapshot? FranchiseSupplyState
            { get; set; }
    }

    public sealed class SimulationFacilityDamageQueueRequest
    {
        public string BattleStableId { get; set; } = string.Empty;
        public string FacilityStableId { get; set; } = string.Empty;
        public string SeverityCode { get; set; } = string.Empty;
    }

    public sealed class SimulationSessionSavePackage
    {
        public string TickRuleRevision { get; set; } = string.Empty;
        public string SchemaVersion { get; set; } = SimulationSaveSchemaVersions.V2;
        public string SaveStableId { get; set; } = string.Empty;
        public string SessionStableId { get; set; } = string.Empty;
        public int SavedWorldTick { get; set; }
        public long SavedWorldRevision { get; set; }
        public string ReplayHashAlgorithmCode { get; set; }
            = SimulationReplayHashAlgorithmCodes.Sha256;
        public string ReplayHash { get; set; } = string.Empty;
        public string WorldAssetPlacementBaseSchemaVersion { get; set; }
            = string.Empty;
        public string ActorEquipmentBaseSchemaVersion { get; set; }
            = string.Empty;
        public string ActionManifestationBaseSchemaVersion { get; set; }
            = string.Empty;
        public string FocusMeditationBaseSchemaVersion { get; set; }
            = string.Empty;
        public string LearningFocusBaseSchemaVersion { get; set; }
            = string.Empty;
        public string HexagramCampaignBaseSchemaVersion { get; set; }
            = string.Empty;
        public string SeasonalOperationsCampaignBaseSchemaVersion { get; set; }
            = string.Empty;
        public string FranchiseSupplyBaseSchemaVersion { get; set; }
            = string.Empty;
        public 경영SimulationSession생성Request SessionCreateRequest { get; set; }
            = new 경영SimulationSession생성Request();
        public 경영SimulationSessionSnapshot Snapshot { get; set; }
            = new 경영SimulationSessionSnapshot();
        public SimulationWorldInventorySnapshot WorldInventory { get; set; }
            = new SimulationWorldInventorySnapshot();
        public SimulationSurvivalTarotStateSnapshot SurvivalTarot { get; set; }
            = new SimulationSurvivalTarotStateSnapshot();
        public SimulationCommandLogEntrySnapshot[] CommandLog { get; set; }
            = Array.Empty<SimulationCommandLogEntrySnapshot>();
        public SimulationBattleSaveRecordSnapshot[] Battles { get; set; }
            = Array.Empty<SimulationBattleSaveRecordSnapshot>();
        public SimulationLhWorldStateSnapshot? LhWorld { get; set; }
        public SimulationRealityContextSnapshot? RealityContext { get; set; }
        public SimulationWorldInteractionManifestationRecord[] WorldInteractionManifestations
            { get; set; } = Array.Empty<SimulationWorldInteractionManifestationRecord>();
        public SimulationSpatialCompositionStateSnapshot? SpatialComposition
            { get; set; }
        public SpatialCompositionGraphHandle? SpatialCompositionHandle
            { get; set; }
        public SimulationWorldAssetPlacementStateSnapshot?
            WorldAssetPlacement { get; set; }
        public SimulationActorEquipmentStateSnapshot? ActorEquipment { get; set; }
        public Simulation행위기록LedgerSnapshot? ActionManifestationLedger { get; set; }
        public Simulation플레이어분야ProfileSnapshot? PlayerDomainProfile { get; set; }
        public Simulation플레이어분야ProfileSnapshot[] PlayerDomainProfiles
            { get; set; } = Array.Empty<Simulation플레이어분야ProfileSnapshot>();
        public Simulation학습중점StateSnapshot? LearningFocus { get; set; }
        public SimulationHexagramCampaignStateSnapshot? HexagramCampaign
            { get; set; }
        public Simulation절기운영CampaignStateSnapshot?
            SeasonalOperationsCampaign { get; set; }
        public 프랜차이즈공급망StateSnapshot? FranchiseSupply
            { get; set; }
    }

    public sealed class SimulationSessionRestoreResult
    {
        public string SaveStableId { get; set; } = string.Empty;
        public string SchemaVersion { get; set; } = string.Empty;
        public string ReplayHash { get; set; } = string.Empty;
        public int ReplayedCommandCount { get; set; }
        public int RestoredBattleCount { get; set; }
        public 경영SimulationSessionSnapshot Session { get; set; }
            = new 경영SimulationSessionSnapshot();
    }
}
