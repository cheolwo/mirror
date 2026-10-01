using Hongdal.Contracts.Common.Versioning;

namespace Hongdal.Contracts.Common.Community;

public sealed class CommunityLedgerTemplateResponse
{
    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Category { get; set; } = "생활 원장";
    public string WorkflowTag { get; set; } = "생활 요청 원장";
    public string TargetOperatingSystemCode { get; set; } = CommunityLedgerOperatingSystemCodes.CommunityTrust;
    public string TargetOperatingSystemName { get; set; } = "커뮤니티 신뢰 OS";
    public string OperatingSystemRoleCode { get; set; } = CommunityLedgerOperatingSystemRoleCodes.Scheduler;
    public string OperatingSystemRoleSummary { get; set; } = "OS는 원장 상태와 구성 규칙을 기준으로 API/엔진 호출 순서, 큐, 재시도, 후속 원장 생성을 조율합니다.";
    public string Summary { get; set; } = string.Empty;
    public bool IsCommunityOpportunityTemplate { get; set; }
    public bool IsInternalAggregationTemplate { get; set; }
    public string 원함확인질문 { get; set; } = "무엇을 원하나요?";
    public string 원함확인설명 { get; set; } = "원장은 사용자가 원하는 일을 바로 실행하기 전에, 그 원함을 참여자와 시스템이 함께 이해할 수 있는 업무 모양으로 정리하는 단계입니다.";
    public IReadOnlyList<string> 원함확인질문목록 { get; set; } =
    [
        "무엇을 하고 싶거나 해결하고 싶은가요?",
        "누가 함께 확인하거나 도와야 하나요?",
        "언제까지, 어디에서, 어떤 조건으로 진행되면 좋나요?"
    ];
    public IReadOnlyList<string> 홍달지원범위안내목록 { get; set; } =
    [
        "원함을 원장 블록으로 나누고 필요한 참여자, 장소, 상태, 증빙, 정산 표시를 정리합니다.",
        "구성 규칙을 기준으로 어떤 화면과 행동을 먼저 열 수 있는지 알려줍니다.",
        "원장이 충분히 구체화되면 적절한 OS, 엔진, API handoff 후보를 보여줍니다."
    ];
    public IReadOnlyList<string> 사용자확인책임안내목록 { get; set; } =
    [
        "실제 조건, 장소, 시간, 상대방 확인은 사용자가 직접 입력하고 서로 확인해야 합니다.",
        "사진, 메모, 결제 표시는 필요한 경우 선택적으로 남기되, 플랫폼이 사실관계를 자동 보증하지는 않습니다.",
        "분쟁, 신고, 애매한 책임 경계가 있으면 원장을 보류하거나 사람 검토를 거쳐야 합니다."
    ];
    public IReadOnlyList<string> EngineHints { get; set; } = [];
    public IReadOnlyList<string> SchedulingHints { get; set; } =
    [
        "구성 규칙 충족 여부 확인",
        "실제 처리 API/엔진 호출 순서 결정",
        "Mongo 원장과 RDB 투영 링크 갱신"
    ];
    public IReadOnlyList<string> UiSectionHints { get; set; } = [];
    public IReadOnlyList<string> ActionHints { get; set; } = [];
    public IReadOnlyList<CommunityLedgerBlockResponse> LedgerBlocks { get; set; } = [];
    public IReadOnlyList<CommunityLedgerBlockRelationResponse> BlockRelations { get; set; } = [];
    public IReadOnlyList<CommunityLedgerCompositionRuleResponse> CompositionRules { get; set; } = [];
    public IReadOnlyList<CommunityLedgerProcessingSurfaceResponse> ProcessingSurfaces { get; set; } = [];
    public CommunityLedgerPersistencePolicyResponse PersistencePolicy { get; set; } = CommunityLedgerPersistencePolicyResponse.MongoDefault();
    public string BestLedgerPatternTitle { get; set; } = string.Empty;
    public string BestLedgerPatternSummary { get; set; } = string.Empty;
    public IReadOnlyList<string> CommunityDiscussionPrompts { get; set; } = [];
    public IReadOnlyList<CommunityLedgerRoleTemplateResponse> Roles { get; set; } = [];
    public CommunityLedgerParticipationPolicyResponse ParticipationPolicy { get; set; } = CommunityLedgerParticipationPolicyResponse.OpenByDefault();
    public IReadOnlyList<string> SuggestedStates { get; set; } = [];
}

public sealed class CommunityLedgerBlockResponse
{
    public string Code { get; set; } = string.Empty;
    public string BlockType { get; set; } = CommunityLedgerBlockTypes.Generic;
    public string DisplayName { get; set; } = string.Empty;
    public string UiSectionHint { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public IReadOnlyList<string> DataHints { get; set; } = [];
    public IReadOnlyList<string> ActionHints { get; set; } = [];
    public IReadOnlyList<string> CompositionRuleCodes { get; set; } = [];
    public bool RequiredForAiJudgment { get; set; }
    public bool OpensApiHandoff { get; set; }
}

public sealed class CommunityLedgerBlockRelationResponse
{
    public string FromBlockCode { get; set; } = string.Empty;
    public string ToBlockCode { get; set; } = string.Empty;
    public string RelationType { get; set; } = CommunityLedgerRelationTypes.Flow;
    public string Cardinality { get; set; } = CommunityLedgerRelationCardinality.OneToOne;
    public bool Required { get; set; }
    public string CompositionRuleCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public sealed class CommunityLedgerImplementationModuleResponse
{
    public int Priority { get; set; }
    public string ModuleCode { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string LedgerTemplateKey { get; set; } = string.Empty;
    public string TargetOperatingSystemCode { get; set; } = CommunityLedgerOperatingSystemCodes.CommunityTrust;
    public string TargetOperatingSystemName { get; set; } = "커뮤니티 신뢰 OS";
    public string Summary { get; set; } = string.Empty;
    public IReadOnlyList<string> IncludedLedgerNames { get; set; } = [];
    public IReadOnlyList<string> PrimaryBlockHints { get; set; } = [];
}

public sealed class CommunityLedgerRelationResponse
{
    public string FromModuleCode { get; set; } = string.Empty;
    public string ToModuleCode { get; set; } = string.Empty;
    public string FromLedgerTemplateKey { get; set; } = string.Empty;
    public string ToLedgerTemplateKey { get; set; } = string.Empty;
    public string RelationType { get; set; } = CommunityLedgerRelationTypes.Handoff;
    public string Cardinality { get; set; } = CommunityLedgerRelationCardinality.OneToOne;
    public bool Required { get; set; }
    public string Trigger { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public sealed class CommunityLedgerCompositionRuleResponse
{
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public IReadOnlyList<string> RequiredLedgerTemplateKeys { get; set; } = [];
    public IReadOnlyList<string> RequiredUiSectionHints { get; set; } = [];
    public IReadOnlyList<string> GatedActionHints { get; set; } = [];
}

public sealed class CommunityLedgerProcessingSurfaceResponse
{
    public string HandoffMode { get; set; } = CommunityLedgerHandoffModes.HttpApi;
    public string ApiEndpointKey { get; set; } = string.Empty;
    public string ControllerName { get; set; } = string.Empty;
    public string ActionName { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public string RoutePattern { get; set; } = string.Empty;
    public string ServiceHint { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public bool IsExistingSurface { get; set; }
}

public sealed class CommunityLedgerPersistencePolicyResponse
{
    public string PrimaryStoreKind { get; set; } = CommunityLedgerPrimaryStoreKinds.MongoDocument;
    public string PrimaryStoreName { get; set; } = "community_ledgers";
    public string FlexibleAttributeStrategy { get; set; } = "원장별 속성, 단계, 참여자, 증빙, 외부 참조는 MongoDB 문서에 유연하게 보관합니다.";
    public string RelationalProjectionPolicy { get; set; } = "관계형 DB에는 확정된 업무 엔티티와 조회 인덱스만 투영하고, Mongo 원장 id를 역참조 키로 남깁니다.";
    public IReadOnlyList<CommunityLedgerRelationalProjectionTargetResponse> RelationalProjectionTargets { get; set; } = [];

    public static CommunityLedgerPersistencePolicyResponse MongoDefault()
        => new();
}

public sealed class CommunityLedgerRelationalProjectionTargetResponse
{
    public string TargetName { get; set; } = string.Empty;
    public string EntityHint { get; set; } = string.Empty;
    public string LinkFieldHint { get; set; } = "CommunityLedgerId";
    public string ProjectionTiming { get; set; } = string.Empty;
}

public sealed class CommunityLedgerRoleTemplateResponse
{
    public string RoleName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public IReadOnlyList<string> Permissions { get; set; } = [];
}

public sealed class CommunityLedgerFlowAnalysisRequest
{
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public IReadOnlyList<string> UiSectionHints { get; set; } = [];
    public IReadOnlyList<string> ActionHints { get; set; } = [];
    public IReadOnlyList<string> StateHints { get; set; } = [];
    public IReadOnlyDictionary<string, string> Attributes { get; set; } = new Dictionary<string, string>();
}

public sealed class CommunityLedgerFlowAnalysisResponse
{
    public CommunityLedgerFlowCandidateResponse PrimaryCandidate { get; set; } = new();
    public IReadOnlyList<CommunityLedgerFlowCandidateResponse> Candidates { get; set; } = [];
    public bool RequiresHumanReview { get; set; }
    public string ReviewReason { get; set; } = string.Empty;
}

public sealed class CommunityLedgerFlowCandidateResponse
{
    public string TemplateKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string TargetOperatingSystemCode { get; set; } = string.Empty;
    public string TargetOperatingSystemName { get; set; } = string.Empty;
    public string RelationCode { get; set; } = CommunityLedgerFlowRelationCodes.LooseCommunityRequest;
    public int MatchScore { get; set; }
    public IReadOnlyList<string> EngineHints { get; set; } = [];
    public IReadOnlyList<string> MatchedSignals { get; set; } = [];
    public IReadOnlyList<string> MissingRequiredSignals { get; set; } = [];
    public IReadOnlyList<string> RelatedCompositionRuleCodes { get; set; } = [];
    public IReadOnlyList<string> RelatedLedgerBlockCodes { get; set; } = [];
    public IReadOnlyList<string> RelatedProcessingSurfaceHints { get; set; } = [];
    public string Reason { get; set; } = string.Empty;
}

public sealed class CommunityLedgerParticipationPolicyResponse
{
    public string DefaultParticipationMode { get; set; } = CommunityLedgerParticipationModes.OpenRoleParticipation;
    public string RoleLabelPolicy { get; set; } = "역할은 고정 권한이 아니라 현재 참여 맥락을 나타내는 라벨입니다.";
    public string IdentityDisplayPolicy { get; set; } = "커뮤니티와 원장 참여의 기본 표시 이름은 실명이 아니라 닉네임, 활동명, 익명 라벨입니다. 본인 인증은 사용자가 원할 때 붙이는 선택적 신뢰 신호이며 공개 표시 이름을 실명으로 강제하지 않습니다.";
    public string PermissionInterpretation { get; set; } = "템플릿 권한 코드는 기본 행동 힌트이며, 신고나 분쟁 제한이 없으면 사용자는 여러 역할의 행동을 수행할 수 있습니다.";
    public string RestrictionPolicy { get; set; } = "신고, 분쟁, 반복 악용 신호, 운영자 검토가 있을 때만 특정 사용자와 행동 범위로 좁혀 제한합니다.";
    public CommunityLedgerExperiencePolicyResponse ExperiencePolicy { get; set; } = CommunityLedgerExperiencePolicyResponse.Default();
    public IReadOnlyList<string> RestrictionTriggers { get; set; } =
    [
        "신고 접수",
        "분쟁 상태",
        "반복 악용 신호",
        "운영자 검토"
    ];
    public IReadOnlyList<string> RestrictableActionCodes { get; set; } =
    [
        CommunityLedgerPermissionCodes.ChangeState,
        CommunityLedgerPermissionCodes.AttachEvidence,
        CommunityLedgerPermissionCodes.MarkPayment,
        CommunityLedgerPermissionCodes.ConfirmCompletion,
        CommunityLedgerPermissionCodes.InviteParticipant,
        CommunityLedgerPermissionCodes.CloseLedger
    ];

    public static CommunityLedgerParticipationPolicyResponse OpenByDefault()
        => new();
}

public sealed class CommunityLedgerExperiencePolicyResponse
{
    public int InitialLevel { get; set; } = 1;
    public string InitialLevelSummary { get; set; } = "가입 시 1레벨로 시작합니다.";
    public string LevelBasis { get; set; } = "레벨은 고정 역할 권한이 아니라 커뮤니티 안에서 쌓인 참여 경험과 신뢰 신호입니다.";
    public string ExperienceAccumulationPolicy { get; set; } = "글 작성, 댓글, 원장 참여, 상태 확인, 증빙 첨부, 완료 확인처럼 커뮤니티와 원장 진행에 도움이 되는 행동을 경험치로 기록합니다.";
    public string RestrictionInteractionPolicy { get; set; } = "신고, 분쟁, 반복 악용 신호가 있으면 경험치 반영을 보류하거나 제외하고, 운영자 검토 뒤 제한 또는 회복을 결정합니다.";
    public IReadOnlyList<CommunityLedgerLevelTierResponse> LevelTiers { get; set; } = [];
    public IReadOnlyList<CommunityLedgerExperienceEventResponse> ExperienceEvents { get; set; } = [];

    public static CommunityLedgerExperiencePolicyResponse Default()
        => new()
        {
            LevelTiers =
            [
                new()
                {
                    Level = 1,
                    RequiredExperience = 0,
                    Label = "가입 구성원",
                    ParticipationScope = "커뮤니티 글, 댓글, 원장 초안, 기본 상태 확인에 참여합니다."
                },
                new()
                {
                    Level = 2,
                    RequiredExperience = 100,
                    Label = "활동 구성원",
                    ParticipationScope = "반복 원장 참여와 간단한 베스트 원장 패턴 제안을 더 잘 드러냅니다."
                },
                new()
                {
                    Level = 3,
                    RequiredExperience = 300,
                    Label = "신뢰 구성원",
                    ParticipationScope = "완료율과 낮은 분쟁 신호를 바탕으로 원장 협업 신뢰도를 표시합니다."
                },
                new()
                {
                    Level = 4,
                    RequiredExperience = 700,
                    Label = "운영 협력 구성원",
                    ParticipationScope = "신고 검토 보조, 원장 패턴 정리, 커뮤니티 운영 협력 신호로 활용합니다."
                }
            ],
            ExperienceEvents =
            [
                new()
                {
                    EventCode = CommunityLedgerExperienceEventCodes.CommunityPostCreated,
                    DisplayName = "커뮤니티 글 작성",
                    BaseExperience = 5,
                    AuditSource = "커뮤니티 게시글"
                },
                new()
                {
                    EventCode = CommunityLedgerExperienceEventCodes.CommunityCommentCreated,
                    DisplayName = "댓글 참여",
                    BaseExperience = 2,
                    AuditSource = "커뮤니티 댓글"
                },
                new()
                {
                    EventCode = CommunityLedgerExperienceEventCodes.LedgerDraftCreated,
                    DisplayName = "원장 초안 작성",
                    BaseExperience = 10,
                    AuditSource = "커뮤니티 원장"
                },
                new()
                {
                    EventCode = CommunityLedgerExperienceEventCodes.LedgerStateConfirmed,
                    DisplayName = "원장 상태 확인",
                    BaseExperience = 8,
                    AuditSource = "원장 상태 로그"
                },
                new()
                {
                    EventCode = CommunityLedgerExperienceEventCodes.WorkStateChanged,
                    DisplayName = "업무 상태 변경",
                    BaseExperience = 5,
                    AuditSource = "상태 변경 이벤트"
                },
                new()
                {
                    EventCode = CommunityLedgerExperienceEventCodes.TransportPickupArrived,
                    DisplayName = "운송 상차지 도착",
                    BaseExperience = 10,
                    AuditSource = "운송 상차지 도착 이벤트"
                },
                new()
                {
                    EventCode = CommunityLedgerExperienceEventCodes.TransportPickupCompleted,
                    DisplayName = "운송 상차 완료",
                    BaseExperience = 20,
                    AuditSource = "운송 상차 완료 이벤트"
                },
                new()
                {
                    EventCode = CommunityLedgerExperienceEventCodes.TransportDropoffArrived,
                    DisplayName = "운송 하차지 도착",
                    BaseExperience = 10,
                    AuditSource = "운송 하차지 도착 이벤트"
                },
                new()
                {
                    EventCode = CommunityLedgerExperienceEventCodes.TransportDropoffCompleted,
                    DisplayName = "운송 하차 완료",
                    BaseExperience = 30,
                    AuditSource = "운송 하차 완료 이벤트"
                },
                new()
                {
                    EventCode = CommunityLedgerExperienceEventCodes.TransportIssueReported,
                    DisplayName = "운송 문제 신고",
                    BaseExperience = 4,
                    AuditSource = "운송 문제 신고 이벤트"
                },
                new()
                {
                    EventCode = CommunityLedgerExperienceEventCodes.FoodOrderAccepted,
                    DisplayName = "음식 주문 수락",
                    BaseExperience = 12,
                    AuditSource = "음식점 주문 수락 이벤트"
                },
                new()
                {
                    EventCode = CommunityLedgerExperienceEventCodes.WarehouseInboundCompleted,
                    DisplayName = "창고 입고 완료",
                    BaseExperience = 20,
                    AuditSource = "창고 입고 완료 이벤트"
                },
                new()
                {
                    EventCode = CommunityLedgerExperienceEventCodes.WarehouseInboundInspected,
                    DisplayName = "창고 입고 검수",
                    BaseExperience = 12,
                    AuditSource = "창고 입고 검수 이벤트"
                },
                new()
                {
                    EventCode = CommunityLedgerExperienceEventCodes.WarehousePutAwayCompleted,
                    DisplayName = "창고 적재 위치 배정",
                    BaseExperience = 8,
                    AuditSource = "창고 적재 위치 배정 이벤트"
                },
                new()
                {
                    EventCode = CommunityLedgerExperienceEventCodes.WarehousePickingCompleted,
                    DisplayName = "창고 피킹 완료",
                    BaseExperience = 12,
                    AuditSource = "창고 피킹 완료 이벤트"
                },
                new()
                {
                    EventCode = CommunityLedgerExperienceEventCodes.WarehouseInventoryPacked,
                    DisplayName = "창고 포장 완료",
                    BaseExperience = 14,
                    AuditSource = "창고 포장 이벤트"
                },
                new()
                {
                    EventCode = CommunityLedgerExperienceEventCodes.WarehouseReconsignmentCreated,
                    DisplayName = "창고 재위탁 운송 생성",
                    BaseExperience = 18,
                    AuditSource = "창고 재위탁 운송 생성 이벤트"
                },
                new()
                {
                    EventCode = CommunityLedgerExperienceEventCodes.EvidenceAttached,
                    DisplayName = "선택 증빙 첨부",
                    BaseExperience = 6,
                    AuditSource = "원장 증빙"
                },
                new()
                {
                    EventCode = CommunityLedgerExperienceEventCodes.CompletionConfirmed,
                    DisplayName = "완료 확인",
                    BaseExperience = 15,
                    AuditSource = "완료 확인 로그"
                },
                new()
                {
                    EventCode = CommunityLedgerExperienceEventCodes.HelpfulReportAccepted,
                    DisplayName = "유효 신고 채택",
                    BaseExperience = 12,
                    AuditSource = "신고/운영 검토"
                }
            ]
        };
}

public sealed class CommunityLedgerLevelTierResponse
{
    public int Level { get; set; }
    public int RequiredExperience { get; set; }
    public string Label { get; set; } = string.Empty;
    public string ParticipationScope { get; set; } = string.Empty;
}

public sealed class CommunityLedgerExperienceEventResponse
{
    public string EventCode { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int BaseExperience { get; set; }
    public string AuditSource { get; set; } = string.Empty;
}

public static class CommunityLedgerTemplateKeys
{
    public const string Order = "order";
    public const string GroupOrder = "group-order";
    public const string CargoTransport = "cargo-transport";
    public const string FoodOrder = "food-order";
    public const string FoodDelivery = "food-delivery";
    public const string HongdalMart = "hongdal-mart";
    public const string WarehouseOutbound = "warehouse-outbound";
    public const string WarehouseInbound = "warehouse-inbound";
    public const string LocalSale = "local-sale";
    public const string GroupPurchase = "group-purchase";
    public const string GroupImport = "group-import";
    public const string MeatImportReadiness = "meat-import-readiness";
    public const string Errand = "errand";
}

public static class CommunityLedgerImplementationModuleCodes
{
    public const string CommunityConversation = "community-conversation";
    public const string WishLedgerAssessment = "wish-ledger-assessment";
    public const string GroupOrderAggregation = "group-order-aggregation";
    public const string OrderRoot = "order-root";
    public const string CargoTransport = "cargo-transport";
    public const string TransportProgress = "transport-progress";
    public const string WarehouseOutbound = "warehouse-outbound";
    public const string PickingPacking = "picking-packing";
    public const string WarehouseInbound = "warehouse-inbound";
    public const string FoodOrder = "food-order";
    public const string FoodDelivery = "food-delivery";
    public const string HongdalMartOrder = "hongdal-mart-order";
    public const string HongdalMartDelivery = "hongdal-mart-delivery";
    public const string GroupPurchaseDemand = "group-purchase-demand";
    public const string GroupPurchaseImportDecision = "group-purchase-import-decision";
    public const string GroupPurchaseShipmentCustoms = "group-purchase-shipment-customs";
    public const string GroupPurchaseDistribution = "group-purchase-distribution";
    public const string SettlementMark = "settlement-mark";
    public const string ReportDispute = "report-dispute";
}

public static class CommunityLedgerRelationTypes
{
    public const string Flow = "Flow";
    public const string Contains = "Contains";
    public const string Requires = "Requires";
    public const string Handoff = "Handoff";
    public const string Reference = "Reference";
}

public static class 주문원장포함역할
{
    public const string 주문집계 = "주문집계";
    public const string 개별주문 = "개별주문";
    public const string 판매 = "판매";
    public const string 창고입고 = "창고입고";
    public const string 창고출고 = "창고출고";
    public const string 배송 = "배송";
    public const string 운송 = "운송";

    public static IReadOnlySet<string> All { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        주문집계,
        개별주문,
        판매,
        창고입고,
        창고출고,
        배송,
        운송
    };
}

public static class 공동수입원장관계역할
{
    public const string 원천공동구매 = "원천공동구매";
    public const string 국제운송 = "국제운송";
    public const string 국내운송 = "국내운송";
    public const string 물류거점입고 = "물류거점입고";
    public const string 물류거점출고 = "물류거점출고";

    public static IReadOnlySet<string> All { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        원천공동구매,
        국제운송,
        국내운송,
        물류거점입고,
        물류거점출고
    };
}

public static class CommunityLedgerRelationCardinality
{
    public const string OneToOne = "1:1";
    public const string OneToMany = "1:N";
    public const string ManyToOne = "N:1";
    public const string ManyToMany = "N:M";
}

public static class CommunityLedgerPermissionCodes
{
    public const string ChangeState = "ChangeState";
    public const string AttachEvidence = "AttachEvidence";
    public const string MarkPayment = "MarkPayment";
    public const string ConfirmCompletion = "ConfirmCompletion";
    public const string InviteParticipant = "InviteParticipant";
    public const string CloseLedger = "CloseLedger";
}

public static class CommunityLedgerParticipationModes
{
    public const string OpenRoleParticipation = "OpenRoleParticipation";
}

public static class CommunityLedgerExperienceEventCodes
{
    public const string CommunityPostCreated = "CommunityPostCreated";
    public const string CommunityCommentCreated = "CommunityCommentCreated";
    public const string LedgerDraftCreated = "LedgerDraftCreated";
    public const string LedgerStateConfirmed = "LedgerStateConfirmed";
    public const string WorkStateChanged = "WorkStateChanged";
    public const string TransportPickupArrived = "TransportPickupArrived";
    public const string TransportPickupCompleted = "TransportPickupCompleted";
    public const string TransportDropoffArrived = "TransportDropoffArrived";
    public const string TransportDropoffCompleted = "TransportDropoffCompleted";
    public const string TransportIssueReported = "TransportIssueReported";
    public const string FoodOrderAccepted = "FoodOrderAccepted";
    public const string WarehouseInboundCompleted = "WarehouseInboundCompleted";
    public const string WarehouseInboundInspected = "WarehouseInboundInspected";
    public const string WarehousePutAwayCompleted = "WarehousePutAwayCompleted";
    public const string WarehousePickingCompleted = "WarehousePickingCompleted";
    public const string WarehouseInventoryPacked = "WarehouseInventoryPacked";
    public const string WarehouseReconsignmentCreated = "WarehouseReconsignmentCreated";
    public const string EvidenceAttached = "EvidenceAttached";
    public const string CompletionConfirmed = "CompletionConfirmed";
    public const string HelpfulReportAccepted = "HelpfulReportAccepted";
}

public static class CommunityLedgerOperatingSystemCodes
{
    public const string CommunityTrust = OperatingSystemIds.CommunityTrust;
    public const string DomesticCargoTransport = OperatingSystemIds.DomesticCargoTransport;
    public const string FoodDelivery = OperatingSystemIds.FoodDelivery;
    public const string HongdalMartUrbanLogistics = OperatingSystemIds.HongdalMartUrbanLogistics;
    public const string WarehouseCommerceFulfillment = OperatingSystemIds.WarehouseCommerceFulfillment;
    public const string GroupPurchaseImport = OperatingSystemIds.GroupPurchaseImport;
}

public static class CommunityLedgerOperatingSystemRoleCodes
{
    public const string Scheduler = "Scheduler";
}

public static class CommunityLedgerEngineHints
{
    public const string CommunityActivitySignal = "커뮤니티 활동 신호 엔진";
    public const string TransportDispatch = "운송 의뢰 배차 엔진";
    public const string FoodDeliveryDispatch = "음식 배달 배차 엔진";
    public const string OutboundBatch = "출고 배치 엔진";
    public const string PickingBatch = "피킹 배치 엔진";
    public const string Grouping = "집단화 엔진";
    public const string ImportCustoms = "수입 통관 엔진";
}

public static class CommunityLedgerCompositionRuleCodes
{
    public const string OrderBeforeFulfillment = "OrderBeforeFulfillment";
    public const string GroupPurchaseAgreementBeforeGroupOrder = "GroupPurchaseAgreementBeforeGroupOrder";
    public const string GroupOrderRequiresIndividualOrders = "GroupOrderRequiresIndividualOrders";
    public const string GroupPurchaseRequiresIndividualOrders = "GroupPurchaseRequiresIndividualOrders";
    public const string TransportRequestBeforePickupDropoff = "TransportRequestBeforePickupDropoff";
    public const string FoodOrderBeforeDelivery = "FoodOrderBeforeDelivery";
    public const string MartOrderBeforePickingPacking = "MartOrderBeforePickingPacking";
    public const string MartPackedBeforeDeliveryPickup = "MartPackedBeforeDeliveryPickup";
    public const string InboundOrStockBeforeOutbound = "InboundOrStockBeforeOutbound";
    public const string OutboundBeforeHandoffTransport = "OutboundBeforeHandoffTransport";
    public const string SaleItemBeforeReservationSettlement = "SaleItemBeforeReservationSettlement";
    public const string RecruitmentBeforePurchaseDistribution = "RecruitmentBeforePurchaseDistribution";
    public const string GroupPurchaseDemandBeforeImportDecision = "GroupPurchaseDemandBeforeImportDecision";
    public const string GroupPurchaseImportDecisionBeforeShipment = "GroupPurchaseImportDecisionBeforeShipment";
    public const string GroupPurchaseCustomsBeforeDomesticDistribution = "GroupPurchaseCustomsBeforeDomesticDistribution";
    public const string CommunityDiscussionBeforeMeatImportReadiness = "CommunityDiscussionBeforeMeatImportReadiness";
    public const string RequestAndParticipantBeforeProgress = "RequestAndParticipantBeforeProgress";
}

public static class CommunityLedgerHandoffModes
{
    public const string HttpApi = "HttpApi";
    public const string InternalService = "InternalService";
    public const string PlannedApi = "PlannedApi";
}

public static class CommunityLedgerPrimaryStoreKinds
{
    public const string MongoDocument = "MongoDocument";
    public const string RelationalProjection = "RelationalProjection";
}

public static class CommunityLedgerFlowRelationCodes
{
    public const string StrongFlowMatch = "StrongFlowMatch";
    public const string PartialFlowMatch = "PartialFlowMatch";
    public const string LooseCommunityRequest = "LooseCommunityRequest";
    public const string Ambiguous = "Ambiguous";
}

public static class CommunityLedgerBlockTypes
{
    public const string Generic = "Generic";
    public const string Participant = "Participant";
    public const string Place = "Place";
    public const string Item = "Item";
    public const string Order = "Order";
    public const string Inventory = "Inventory";
    public const string Quantity = "Quantity";
    public const string Decision = "Decision";
    public const string Time = "Time";
    public const string State = "State";
    public const string Evidence = "Evidence";
    public const string Settlement = "Settlement";
    public const string Handoff = "Handoff";
}
