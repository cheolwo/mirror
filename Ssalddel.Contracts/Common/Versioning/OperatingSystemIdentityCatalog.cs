namespace Ssalddel.Contracts.Common.Versioning;

/// <summary>
/// Persistent operating-system identifiers shared by API metadata, ledgers, and clients.
/// Existing values with the OS suffix remain canonical so stored ledgers stay compatible.
/// </summary>
public static class OperatingSystemIds
{
    public const string ShipperTransportManagement = "ShipperTransportManagementOS";
    public const string DomesticCargoTransport = "DomesticCargoTransportOS";
    public const string WarehouseCommerceFulfillment = "WarehouseCommerceFulfillmentOS";
    public const string GroupPurchaseDemand = "GroupPurchaseDemandOS";
    public const string GroupPurchaseImport = "GroupPurchaseImportOS";
    public const string FoodDelivery = "FoodDeliveryOS";
    public const string SsalddelMartUrbanLogistics = "SsalddelMartUrbanLogisticsOS";
    public const string CommunityTrust = "CommunityTrustOS";
    public const string PlatformOperations = "PlatformOperationsOS";
    public const string EducationFieldExperience = "EducationFieldExperienceOS";

    public static IReadOnlyList<string> All { get; } =
    [
        ShipperTransportManagement,
        DomesticCargoTransport,
        WarehouseCommerceFulfillment,
        GroupPurchaseDemand,
        GroupPurchaseImport,
        FoodDelivery,
        SsalddelMartUrbanLogistics,
        CommunityTrust,
        PlatformOperations,
        EducationFieldExperience
    ];

    private static readonly IReadOnlyDictionary<string, string> CanonicalByAlias =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [ShipperTransportManagement] = ShipperTransportManagement,
            ["ShipperTransportManagement"] = ShipperTransportManagement,
            [DomesticCargoTransport] = DomesticCargoTransport,
            ["DomesticCargoTransport"] = DomesticCargoTransport,
            [WarehouseCommerceFulfillment] = WarehouseCommerceFulfillment,
            ["WarehouseCommerceFulfillment"] = WarehouseCommerceFulfillment,
            [GroupPurchaseDemand] = GroupPurchaseDemand,
            ["GroupPurchaseDemand"] = GroupPurchaseDemand,
            [GroupPurchaseImport] = GroupPurchaseImport,
            ["GroupPurchaseImport"] = GroupPurchaseImport,
            [FoodDelivery] = FoodDelivery,
            ["FoodDelivery"] = FoodDelivery,
            [SsalddelMartUrbanLogistics] = SsalddelMartUrbanLogistics,
            ["SsalddelMartUrbanLogistics"] = SsalddelMartUrbanLogistics,
            [CommunityTrust] = CommunityTrust,
            ["CommunityTrust"] = CommunityTrust,
            [PlatformOperations] = PlatformOperations,
            ["PlatformOperations"] = PlatformOperations,
            [EducationFieldExperience] = EducationFieldExperience,
            ["EducationFieldExperience"] = EducationFieldExperience
        };

    public static bool TryNormalize(string? value, out string operatingSystemId)
    {
        if (!string.IsNullOrWhiteSpace(value)
            && CanonicalByAlias.TryGetValue(value.Trim(), out var canonical))
        {
            operatingSystemId = canonical;
            return true;
        }

        operatingSystemId = string.Empty;
        return false;
    }

    public static string Normalize(string value)
        => TryNormalize(value, out var operatingSystemId)
            ? operatingSystemId
            : throw new ArgumentException($"Unknown operating system identifier: {value}", nameof(value));

    public static IReadOnlyList<string> GetAliases(string operatingSystemId)
    {
        var canonical = Normalize(operatingSystemId);
        return CanonicalByAlias
            .Where(pair => string.Equals(pair.Value, canonical, StringComparison.Ordinal))
            .Select(pair => pair.Key)
            .OrderBy(alias => alias, StringComparer.Ordinal)
            .ToArray();
    }
}

public static class EngineFamilyIds
{
    public const string TransportRequestDispatch = "TransportRequestDispatchEngine";
    public const string OutboundBatch = "OutboundBatchEngine";
    public const string PickingBatch = "PickingBatchEngine";
    public const string GroupPurchaseClustering = "GroupPurchaseClusteringEngine";
    public const string CommunitySignal = "CommunitySignalEngine";
    public const string WorkflowPolicy = "WorkflowPolicyEngine";
}

public static class EngineImplementationIds
{
    public const string CargoYongdalDispatch = "CargoYongdalDispatchEngine";
    public const string FoodDeliveryDispatch = "FoodDeliveryDispatchEngine";
    public const string OutboundBatch = EngineFamilyIds.OutboundBatch;
    public const string PickingBatch = EngineFamilyIds.PickingBatch;
    public const string GroupPurchaseClustering = EngineFamilyIds.GroupPurchaseClustering;
}

public static class RuntimeCapabilityStatuses
{
    public const string Active = "Active";
    public const string Declared = "Declared";
}

public static class OperatingSystemLifecycleStageIds
{
    public const string ShipperPartyContext = "shipper.party-context";
    public const string ShipperCargoDefinition = "shipper.cargo-definition";
    public const string ShipperTermsQuote = "shipper.terms-quote";
    public const string ShipperRequestCommitment = "shipper.request-commitment";
    public const string ShipperTransportHandoff = "shipper.transport-handoff";
    public const string ShipperProgressChange = "shipper.progress-change";
    public const string ShipperDeliveryAcceptance = "shipper.delivery-acceptance";
    public const string ShipperSettlementRecovery = "shipper.settlement-recovery";

    public const string CargoRequest = "cargo.request";
    public const string CargoTermsAgreement = "cargo.terms-agreement";
    public const string CargoDispatch = "cargo.dispatch";
    public const string CargoPickup = "cargo.pickup";
    public const string CargoTransport = "cargo.transport";
    public const string CargoDropoff = "cargo.dropoff";
    public const string CargoEvidenceSettlement = "cargo.evidence-settlement";
    public const string CargoInterruptionRecovery = "cargo.interruption-recovery";

    public const string FoodOrder = "food.order";
    public const string FoodRestaurantResponse = "food.restaurant-response";
    public const string FoodCooking = "food.cooking";
    public const string FoodDispatch = "food.dispatch";
    public const string FoodPickup = "food.pickup";
    public const string FoodDelivery = "food.delivery";
    public const string FoodCancellationCompensation = "food.cancellation-compensation";
    public const string FoodInterruptionRecovery = "food.interruption-recovery";

    public const string WarehouseInboundPlan = "warehouse.inbound-plan";
    public const string WarehouseReceiving = "warehouse.receiving";
    public const string WarehouseInspection = "warehouse.inspection";
    public const string WarehousePutAwayInventory = "warehouse.put-away-inventory";
    public const string WarehouseOutboundAllocation = "warehouse.outbound-allocation";
    public const string WarehousePicking = "warehouse.picking";
    public const string WarehousePacking = "warehouse.packing";
    public const string WarehouseOutboundHandoff = "warehouse.outbound-handoff";
    public const string WarehouseExceptionRecovery = "warehouse.exception-recovery";

    public const string MartSupplyAgreement = "mart.supply-agreement";
    public const string MartReplenishmentOrder = "mart.replenishment-order";
    public const string MartInboundReceiving = "mart.inbound-receiving";
    public const string MartPutawayInventory = "mart.putaway-inventory";
    public const string MartCustomerOrderAllocation = "mart.customer-order-allocation";
    public const string MartPickingPacking = "mart.picking-packing";
    public const string MartLastMileHandoff = "mart.last-mile-handoff";
    public const string MartCompletionRecovery = "mart.completion-recovery";

    public const string DemandIntent = "demand.intent";
    public const string DemandClusteringReview = "demand.clustering-review";
    public const string DemandHandoffApproval = "demand.handoff-approval";
    public const string DemandDownstreamLink = "demand.downstream-link";

    public const string ImportReadinessLedger = "import.readiness-ledger";
    public const string ImportEvidenceReview = "import.evidence-review";
    public const string ImportForwarderHandoff = "import.forwarder-handoff";
    public const string ImportQualifiedReview = "import.qualified-review";
    public const string ImportShipmentTracking = "import.shipment-tracking";

    public const string CommunityDiscoveryParticipation = "community.discovery-participation";
    public const string CommunityAgreementConsent = "community.agreement-consent";
    public const string CommunityWorkCompletion = "community.work-completion";
    public const string CommunityRelationshipDisclosure = "community.relationship-disclosure";

    public const string PlatformLedgerReview = "platform.ledger-review";
    public const string PlatformEconomicsEvaluation = "platform.economics-evaluation";
    public const string PlatformFollowupRecovery = "platform.followup-recovery";

    public const string EducationActivityPlan = "education.activity-plan";
    public const string EducationActivityVerification = "education.activity-verification";
    public const string EducationGuardianApproval = "education.guardian-approval";
    public const string EducationSchoolSubmission = "education.school-submission";
    public const string EducationSchoolDecision = "education.school-decision";
}

/// <summary>
/// Sequence는 책임을 설명하는 표시 순서이며 실행 선후 DAG나 필수 절차 목록이 아닙니다.
/// 단계의 등록은 상태 전이·권한·외부 실행 완료를 뜻하지 않으며 실제 조건은 해당 UseCase가 검증합니다.
/// 선택·병행·재진입 가능한 업무는 각 업무의 현재 절차와 동의 조건을 따릅니다.
/// </summary>
public sealed record OperatingSystemLifecycleStageDefinition(
    string StageId,
    int Sequence,
    string Name,
    string Responsibility);

public sealed record OperatingSystemLifecycleDefinition(
    string OperatingSystemId,
    string Name,
    IReadOnlyList<OperatingSystemLifecycleStageDefinition> Stages);

public static class OperatingSystemLifecycleCatalog
{
    private static readonly IReadOnlyDictionary<string, OperatingSystemLifecycleDefinition> Items =
        new Dictionary<string, OperatingSystemLifecycleDefinition>(StringComparer.Ordinal)
        {
            [OperatingSystemIds.ShipperTransportManagement] = new(
                OperatingSystemIds.ShipperTransportManagement,
                "화주 운송관리 OS",
                [
                    new(OperatingSystemLifecycleStageIds.ShipperPartyContext, 10, "화주 당사자 확정", "이번 운송에서 물건을 맡기고 비용을 부담할 당사자와 권한을 확인합니다."),
                    new(OperatingSystemLifecycleStageIds.ShipperCargoDefinition, 20, "화물 정의", "품목·수량·중량·부피·포장·온도와 차량 요구 조건을 기록합니다."),
                    new(OperatingSystemLifecycleStageIds.ShipperTermsQuote, 30, "운송 조건·운임 검토", "상하차 위치·시간창·기준운임·추가 비용과 지급 조건을 검토합니다."),
                    new(OperatingSystemLifecycleStageIds.ShipperRequestCommitment, 40, "운송의뢰 확정", "검증된 운송 조건과 정산 조건을 판본화하여 화주 의뢰로 확정합니다."),
                    new(OperatingSystemLifecycleStageIds.ShipperTransportHandoff, 50, "화물운송 인계", "확정된 의뢰의 실행 책임을 화물운송 OS에 명시적으로 인계합니다."),
                    new(OperatingSystemLifecycleStageIds.ShipperProgressChange, 60, "진행·조건 변경", "운송 진행을 조회하고 핵심 조건 변경은 새 판본과 재동의 대상으로 분리합니다."),
                    new(OperatingSystemLifecycleStageIds.ShipperDeliveryAcceptance, 70, "인수·검수", "인수증·수량 부족·파손과 반품·재위탁 여부를 확인합니다."),
                    new(OperatingSystemLifecycleStageIds.ShipperSettlementRecovery, 80, "정산·비정상 운송 처리·업무 회복", "운임 지급·정산과 수량 부족·파손·인수 보류 같은 비정상 운송의 검토·해결·재처리를 관리합니다.")
                ]),
            [OperatingSystemIds.DomesticCargoTransport] = new(
                OperatingSystemIds.DomesticCargoTransport,
                "화물운송 OS",
                [
                    new(OperatingSystemLifecycleStageIds.CargoRequest, 10, "운송 의뢰", "화주의 의뢰와 최초 운송 조건을 기록합니다."),
                    new(OperatingSystemLifecycleStageIds.CargoTermsAgreement, 20, "조건 합의", "기사 수락과 핵심 조건 변경 재동의를 관리합니다."),
                    new(OperatingSystemLifecycleStageIds.CargoDispatch, 30, "배차", "후보·제안·예약·수락·거절을 조율합니다."),
                    new(OperatingSystemLifecycleStageIds.CargoPickup, 40, "상차", "상차 준비·도착·적재와 출발 가능 상태를 관리합니다."),
                    new(OperatingSystemLifecycleStageIds.CargoTransport, 50, "운송", "경유와 운송 약속, 다음 콜 연속성을 관리합니다."),
                    new(OperatingSystemLifecycleStageIds.CargoDropoff, 60, "하차", "도착·하차·인수 결과를 관리합니다."),
                    new(OperatingSystemLifecycleStageIds.CargoEvidenceSettlement, 70, "증빙·정산", "완료 증빙과 이동·대기 보전, 정산 후보를 관리합니다."),
                    new(OperatingSystemLifecycleStageIds.CargoInterruptionRecovery, 80, "비정상 운송 처리·업무 회복", "사고·고장·지연·수량 부족·파손의 검토와 안전한 재개·재배차·종료를 조율합니다.")
                ]),
            [OperatingSystemIds.FoodDelivery] = new(
                OperatingSystemIds.FoodDelivery,
                "음식배달 OS",
                [
                    new(OperatingSystemLifecycleStageIds.FoodOrder, 10, "음식 주문", "주문자의 주문과 주문 원장을 관리합니다."),
                    new(OperatingSystemLifecycleStageIds.FoodRestaurantResponse, 20, "음식점 응답", "음식점 수락·거절과 조리시간 선택을 관리합니다."),
                    new(OperatingSystemLifecycleStageIds.FoodCooking, 30, "조리", "조리 진행과 픽업 준비 예정·완료를 관리합니다."),
                    new(OperatingSystemLifecycleStageIds.FoodDispatch, 40, "배차", "기사 후보·제안·수락·거절과 균형을 조율합니다."),
                    new(OperatingSystemLifecycleStageIds.FoodPickup, 50, "픽업", "가게 도착·현장 대기·픽업을 관리합니다."),
                    new(OperatingSystemLifecycleStageIds.FoodDelivery, 60, "전달", "고객 전달과 수령 완료를 관리합니다."),
                    new(OperatingSystemLifecycleStageIds.FoodCancellationCompensation, 70, "취소·보상", "취소·환불·음식점 보상과 사고 손실 대응을 관리합니다."),
                    new(OperatingSystemLifecycleStageIds.FoodInterruptionRecovery, 80, "중단·회복", "조리 지연·사고·재조리·재배차와 운영자 검토를 조율합니다.")
                ]),
            [OperatingSystemIds.WarehouseCommerceFulfillment] = new(
                OperatingSystemIds.WarehouseCommerceFulfillment,
                "창고·커머스 이행 OS",
                [
                    new(OperatingSystemLifecycleStageIds.WarehouseInboundPlan, 10, "입고 예정", "확정된 입고 요청과 운송 인계 근거를 기록하며 실제 도착이나 재고 반영을 뜻하지 않습니다."),
                    new(OperatingSystemLifecycleStageIds.WarehouseReceiving, 20, "도착·수령", "입고 바코드와 도착·수령을 기록하고 검수 대기 재고 후보를 만듭니다."),
                    new(OperatingSystemLifecycleStageIds.WarehouseInspection, 30, "검수", "예정·수령 수량과 상태를 대조하고 가용·불량 수량을 명시적으로 판정합니다."),
                    new(OperatingSystemLifecycleStageIds.WarehousePutAwayInventory, 40, "적재·재고", "검수된 상품에 보관 위치를 배정하고 가용·예약 재고에 결속합니다."),
                    new(OperatingSystemLifecycleStageIds.WarehouseOutboundAllocation, 50, "출고 할당", "확정된 주문 수량을 재고·출고 예정·피킹 작업에 결속하며 미할당 수량을 숨기지 않습니다."),
                    new(OperatingSystemLifecycleStageIds.WarehousePicking, 60, "피킹", "대기·진행중·완료 상태에서 위치·상품·수량 확인을 거쳐 출고 대상을 집품합니다."),
                    new(OperatingSystemLifecycleStageIds.WarehousePacking, 70, "포장", "확인된 재고나 피킹 결과를 포장하고 출고 묶음과 준비 상태를 기록합니다."),
                    new(OperatingSystemLifecycleStageIds.WarehouseOutboundHandoff, 80, "출고·운송 인계 준비", "출고 결과와 기존 운송 의뢰 참조를 결속합니다. 실제 화물 OS 책임 인계나 배차 완료를 자동 확정하지 않습니다."),
                    new(OperatingSystemLifecycleStageIds.WarehouseExceptionRecovery, 90, "수량 이상 보류·재검수 회복", "수량 불일치 단위를 보호 보류하고 재검수·재계수 뒤 승인된 수량으로 검수 단계에 재진입시킵니다.")
                ]),
            [OperatingSystemIds.SsalddelMartUrbanLogistics] = new(
                OperatingSystemIds.SsalddelMartUrbanLogistics,
                "살뜰마트 도심물류 OS",
                [
                    new(OperatingSystemLifecycleStageIds.MartSupplyAgreement, 10, "공급계약 이용", "플랫폼 공급계약 중 각 마트가 이용할 계약과 품목 범위를 등록합니다."),
                    new(OperatingSystemLifecycleStageIds.MartReplenishmentOrder, 20, "점포별 입고 발주", "마트별 필요 수량과 납기 요청을 기록하며 공급자의 수락 수량을 보존합니다."),
                    new(OperatingSystemLifecycleStageIds.MartInboundReceiving, 30, "입고·검수", "도착 수량과 상태를 검수하고 확인된 재고 후보를 만듭니다."),
                    new(OperatingSystemLifecycleStageIds.MartPutawayInventory, 40, "적재·재고", "검수된 상품을 적재 위치와 가용 재고에 결속합니다."),
                    new(OperatingSystemLifecycleStageIds.MartCustomerOrderAllocation, 50, "고객 주문·할당", "확정된 고객 주문을 점포 재고와 출고 작업에 결속합니다."),
                    new(OperatingSystemLifecycleStageIds.MartPickingPacking, 60, "피킹·포장", "적재 위치에 근거한 피킹과 포장, 픽업 준비 시각을 관리합니다."),
                    new(OperatingSystemLifecycleStageIds.MartLastMileHandoff, 70, "라스트마일 인계", "마트 주문과 분리된 배송 자식 업무를 음식배달 OS에 명시적으로 인계합니다."),
                    new(OperatingSystemLifecycleStageIds.MartCompletionRecovery, 80, "완료·회복", "배송 증거를 주문에 반영하고 품절·지연·재배차·취소 복구를 관리합니다.")
                ]),
            [OperatingSystemIds.GroupPurchaseDemand] = new(
                OperatingSystemIds.GroupPurchaseDemand,
                "공동구매 수요·모집 OS",
                [
                    new(OperatingSystemLifecycleStageIds.DemandIntent, 10, "수요 등록·철회", "본인의 비구속 구매 의사와 수량·수령 조건을 등록하고 허용된 상태에서 변경·철회합니다."),
                    new(OperatingSystemLifecycleStageIds.DemandClusteringReview, 20, "집단화·모집 점검", "수요 집단을 조율하고 목표 충족·모집 마감·장기 정체와 검토 대기 상태를 점검합니다."),
                    new(OperatingSystemLifecycleStageIds.DemandHandoffApproval, 30, "사람의 인계 승인", "운영자가 확인 가능한 집단과 모집 조건을 검토하여 수입 준비 인계를 승인합니다. 주문·결제나 수입 실행을 자동 확정하지 않습니다."),
                    new(OperatingSystemLifecycleStageIds.DemandDownstreamLink, 40, "후속 준비 원장 결속", "승인된 집단에 명시적으로 작성된 수입 준비 원장 참조를 결속하고 중복 연결을 검증합니다.")
                ]),
            [OperatingSystemIds.GroupPurchaseImport] = new(
                OperatingSystemIds.GroupPurchaseImport,
                "같이 주문 수입 OS",
                [
                    new(OperatingSystemLifecycleStageIds.ImportReadinessLedger, 10, "수입 준비 원장", "승인된 수요 집단과 기존 준비 원장의 판본·참여 조건을 결속합니다. 계약·결제·신고·운송 실행을 확정하지 않습니다."),
                    new(OperatingSystemLifecycleStageIds.ImportEvidenceReview, 20, "근거 점검·갱신", "견적·분류·규제·책임 근거와 유효기간을 점검하고 준비 상태와 보완 작업을 기록합니다."),
                    new(OperatingSystemLifecycleStageIds.ImportForwarderHandoff, 30, "사람의 포워더 전달·회신 기록", "사람이 선택한 전달 범위와 포워더 회신을 기록하며 개인정보 제공은 명시적 동의와 철회 가능한 근거를 확인합니다."),
                    new(OperatingSystemLifecycleStageIds.ImportQualifiedReview, 40, "전문검토 인계", "사람이 정한 검토자·범위·접수 근거를 검증하여 전문검토 인계를 기록합니다. 전문 판단이나 자동 신고를 대신하지 않습니다."),
                    new(OperatingSystemLifecycleStageIds.ImportShipmentTracking, 50, "선적·통관 추적", "해외 선적 사건과 통관 조회 결과·예외를 추적 원장에 기록합니다. 실제 통관 허가나 국내 운송 완료를 확정하지 않습니다.")
                ]),
            [OperatingSystemIds.CommunityTrust] = new(
                OperatingSystemIds.CommunityTrust,
                "커뮤니티·신뢰 OS",
                [
                    new(OperatingSystemLifecycleStageIds.CommunityDiscoveryParticipation, 10, "탐색·관심·비구속 참가", "글과 협업 기회를 탐색하고 관심·투표·비구속 참가를 기록합니다. 구매나 유료 계약을 확정하지 않습니다."),
                    new(OperatingSystemLifecycleStageIds.CommunityAgreementConsent, 20, "협업 조건·동의·가원장", "명시적 승격 의사와 협업 조건을 원장에 결속하고 현재 조건에 대한 당사자 동의와 공개·전달 동의를 구분합니다."),
                    new(OperatingSystemLifecycleStageIds.CommunityWorkCompletion, 30, "협업 수행·완료 확인", "생활 협업·보관·전달의 허용된 수행과 당사자 확인을 기록하며 선택한 전달 방식과 실제 완료 권위를 검증합니다."),
                    new(OperatingSystemLifecycleStageIds.CommunityRelationshipDisclosure, 40, "관계·공개 동의·신뢰 신호", "선택한 친구 관계 응답과 본인의 연락처 공개 동의를 기록하고 완료 이력의 개인정보와 공개 범위를 관리합니다.")
                ]),
            [OperatingSystemIds.PlatformOperations] = new(
                OperatingSystemIds.PlatformOperations,
                "플랫폼 운영 OS",
                [
                    new(OperatingSystemLifecycleStageIds.PlatformLedgerReview, 10, "운영 재무 사건·대사 조회", "운영 재무 사건·잔액·대사·현금흐름과 경제성 원장을 읽어 상태를 검토합니다. 조회가 지급·전표 반영·세금 신고를 수행하지 않습니다."),
                    new(OperatingSystemLifecycleStageIds.PlatformEconomicsEvaluation, 20, "경영 시뮬레이션 검토", "운영 경제성의 가정과 시뮬레이션 후보를 평가합니다. 실제 정산이나 운영 조건을 자동 변경하지 않습니다."),
                    new(OperatingSystemLifecycleStageIds.PlatformFollowupRecovery, 30, "후속 처리 복구", "허용된 원장 동기화와 OS 인계 Outbox의 실패·대기 상태를 조회하고 조건을 충족한 후속 작업을 재시도합니다. 원 업무 완료를 되돌리지 않습니다.")
                ]),
            [OperatingSystemIds.EducationFieldExperience] = new(
                OperatingSystemIds.EducationFieldExperience,
                "현장 체험 교육 OS",
                [
                    new(OperatingSystemLifecycleStageIds.EducationActivityPlan, 10, "비공개 활동 계획", "학생·학교·기간·장소와 담당자를 결속한 비공개 현장 체험 계획 원장을 작성합니다."),
                    new(OperatingSystemLifecycleStageIds.EducationActivityVerification, 20, "활동 기록·지도자 확인", "활동 근거를 기록하고 지도자가 배정된 경우 해당 지도자의 확인을 받습니다."),
                    new(OperatingSystemLifecycleStageIds.EducationGuardianApproval, 30, "보호자 확인", "해당 학생의 보호자 권한을 확인하여 활동 계획과 기록의 확인 결과를 보존합니다."),
                    new(OperatingSystemLifecycleStageIds.EducationSchoolSubmission, 40, "학교 제출·전송 재시도", "활동·보호자·조건부 지도자 확인을 검증하고 선택한 제출 방식과 접수·전송·재시도 상태를 기록합니다."),
                    new(OperatingSystemLifecycleStageIds.EducationSchoolDecision, 50, "교육기관 결정 기록", "해당 학교의 권한 있는 주체가 출석 인정·미인정 결정을 기록합니다. 플랫폼이 교육기관의 판단을 대신하지 않습니다.")
                ])
        };

    public static IReadOnlyList<OperatingSystemLifecycleDefinition> GetAll()
        => Items.Values.OrderBy(item => item.OperatingSystemId, StringComparer.Ordinal).ToArray();

    public static bool TryGet(string operatingSystemId, out OperatingSystemLifecycleDefinition definition)
    {
        definition = null!;
        return OperatingSystemIds.TryNormalize(operatingSystemId, out var canonical)
               && Items.TryGetValue(canonical, out definition!);
    }

    public static OperatingSystemLifecycleDefinition Get(string operatingSystemId)
        => TryGet(operatingSystemId, out var definition)
            ? definition
            : throw new ArgumentException(
                $"Lifecycle is not defined for operating system: {operatingSystemId}",
                nameof(operatingSystemId));
}

public static class WarehouseLifecycleValidationCaseCodes
{
    public const string Normal = "warehouse-normal";
    public const string QuantityMismatchRecovery = "warehouse-quantity-mismatch-recovery";
}

/// <summary>
/// 창고 공통 생명주기를 결정적 표본으로 검증할 때 사용하는 읽기 전용 경로입니다.
/// 운영 상태를 전이하거나 OS 간 인계를 완료하는 권위를 갖지 않습니다.
/// </summary>
public sealed record WarehouseLifecycleValidationCaseDefinition(
    string CaseCode,
    string OperatingSystemId,
    IReadOnlyList<string> StageIds,
    string DirectResultStageId,
    string? FailureDetectedAtStageId,
    string? RecoveryStageId,
    string? ResumeAtStageId);

public static class WarehouseLifecycleValidationCatalog
{
    private static readonly IReadOnlyDictionary<string, WarehouseLifecycleValidationCaseDefinition> Items =
        new Dictionary<string, WarehouseLifecycleValidationCaseDefinition>(StringComparer.Ordinal)
        {
            [WarehouseLifecycleValidationCaseCodes.Normal] = new(
                WarehouseLifecycleValidationCaseCodes.Normal,
                OperatingSystemIds.WarehouseCommerceFulfillment,
                [
                    OperatingSystemLifecycleStageIds.WarehouseInboundPlan,
                    OperatingSystemLifecycleStageIds.WarehouseReceiving,
                    OperatingSystemLifecycleStageIds.WarehouseInspection,
                    OperatingSystemLifecycleStageIds.WarehousePutAwayInventory,
                    OperatingSystemLifecycleStageIds.WarehouseOutboundAllocation,
                    OperatingSystemLifecycleStageIds.WarehousePicking,
                    OperatingSystemLifecycleStageIds.WarehousePacking,
                    OperatingSystemLifecycleStageIds.WarehouseOutboundHandoff
                ],
                OperatingSystemLifecycleStageIds.WarehouseOutboundHandoff,
                null,
                null,
                null),
            [WarehouseLifecycleValidationCaseCodes.QuantityMismatchRecovery] = new(
                WarehouseLifecycleValidationCaseCodes.QuantityMismatchRecovery,
                OperatingSystemIds.WarehouseCommerceFulfillment,
                [
                    OperatingSystemLifecycleStageIds.WarehouseInboundPlan,
                    OperatingSystemLifecycleStageIds.WarehouseReceiving,
                    OperatingSystemLifecycleStageIds.WarehouseInspection,
                    OperatingSystemLifecycleStageIds.WarehouseExceptionRecovery,
                    OperatingSystemLifecycleStageIds.WarehouseInspection,
                    OperatingSystemLifecycleStageIds.WarehousePutAwayInventory,
                    OperatingSystemLifecycleStageIds.WarehouseOutboundAllocation,
                    OperatingSystemLifecycleStageIds.WarehousePicking,
                    OperatingSystemLifecycleStageIds.WarehousePacking,
                    OperatingSystemLifecycleStageIds.WarehouseOutboundHandoff
                ],
                OperatingSystemLifecycleStageIds.WarehouseOutboundHandoff,
                OperatingSystemLifecycleStageIds.WarehouseInspection,
                OperatingSystemLifecycleStageIds.WarehouseExceptionRecovery,
                OperatingSystemLifecycleStageIds.WarehouseInspection)
        };

    static WarehouseLifecycleValidationCatalog()
    {
        var lifecycle = OperatingSystemLifecycleCatalog.Get(OperatingSystemIds.WarehouseCommerceFulfillment);
        var knownStageIds = lifecycle.Stages
            .Select(stage => stage.StageId)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var item in Items.Values)
        {
            if (!string.Equals(
                    item.OperatingSystemId,
                    OperatingSystemIds.WarehouseCommerceFulfillment,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Warehouse validation case references another OS: {item.CaseCode}");
            }

            if (item.StageIds.Count == 0 || item.StageIds.Any(stageId => !knownStageIds.Contains(stageId)))
            {
                throw new InvalidOperationException($"Warehouse validation case references an unknown lifecycle stage: {item.CaseCode}");
            }

            if (!string.Equals(item.StageIds[^1], item.DirectResultStageId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Warehouse validation case must end at its direct result: {item.CaseCode}");
            }

            ValidateRecoveryContract(item);
        }
    }

    public static IReadOnlyList<WarehouseLifecycleValidationCaseDefinition> GetAll()
        => Items.Values.OrderBy(item => item.CaseCode, StringComparer.Ordinal).ToArray();

    public static WarehouseLifecycleValidationCaseDefinition Get(string caseCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(caseCode);
        return Items.TryGetValue(caseCode.Trim(), out var definition)
            ? definition
            : throw new ArgumentException($"Unknown warehouse lifecycle validation case: {caseCode}", nameof(caseCode));
    }

    private static void ValidateRecoveryContract(WarehouseLifecycleValidationCaseDefinition item)
    {
        var recoveryFields = new[]
        {
            item.FailureDetectedAtStageId,
            item.RecoveryStageId,
            item.ResumeAtStageId
        };
        if (recoveryFields.All(string.IsNullOrWhiteSpace))
        {
            return;
        }

        if (recoveryFields.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidOperationException($"Warehouse recovery contract is incomplete: {item.CaseCode}");
        }

        var failureIndex = IndexOf(item.StageIds, item.FailureDetectedAtStageId!);
        var recoveryIndex = IndexOf(item.StageIds, item.RecoveryStageId!, failureIndex + 1);
        var resumeIndex = IndexOf(item.StageIds, item.ResumeAtStageId!, recoveryIndex + 1);
        if (failureIndex < 0 || recoveryIndex < 0 || resumeIndex < 0)
        {
            throw new InvalidOperationException($"Warehouse recovery contract does not re-enter after its recovery stage: {item.CaseCode}");
        }
    }

    private static int IndexOf(IReadOnlyList<string> values, string expected, int startIndex = 0)
    {
        for (var index = Math.Max(0, startIndex); index < values.Count; index++)
        {
            if (string.Equals(values[index], expected, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }
}

public static class OperatingSystemEngineRoles
{
    public const string Primary = "Primary";
    public const string ApprovedSafeFallback = "ApprovedSafeFallback";
    public const string Shadow = "Shadow";
}

public static class OperatingSystemEngineActivationStatuses
{
    public const string Disabled = "Disabled";
    public const string Shadow = "Shadow";
    public const string Active = "Active";
}

public sealed record OperatingSystemEngineCatalogEntry(
    string OperatingSystemId,
    string EngineFamilyId,
    string ImplementationId,
    string Role,
    string ActivationStatus,
    string InputContractRevision,
    string ResultContractRevision,
    string PolicyRevision,
    string? FallbackForImplementationId = null);

public static class OperatingSystemEngineCatalog
{
    public const string CatalogRevision = "operating-system-engine-catalog.v1";
    public const string DispatchInputContractRevision = "dispatch-engine-input.v1";
    public const string DispatchResultContractRevision = "dispatch-candidate-selection.v1";

    private static readonly IReadOnlyList<OperatingSystemEngineCatalogEntry> Items =
    [
        new(
            OperatingSystemIds.DomesticCargoTransport,
            EngineFamilyIds.TransportRequestDispatch,
            EngineImplementationIds.CargoYongdalDispatch,
            OperatingSystemEngineRoles.Primary,
            OperatingSystemEngineActivationStatuses.Active,
            DispatchInputContractRevision,
            DispatchResultContractRevision,
            "cargo-dispatch-policy.v1"),
        new(
            OperatingSystemIds.FoodDelivery,
            EngineFamilyIds.TransportRequestDispatch,
            EngineImplementationIds.FoodDeliveryDispatch,
            OperatingSystemEngineRoles.Primary,
            OperatingSystemEngineActivationStatuses.Active,
            DispatchInputContractRevision,
            DispatchResultContractRevision,
            "food-delivery-dispatch-policy.v1"),
        new(
            OperatingSystemIds.WarehouseCommerceFulfillment,
            EngineFamilyIds.OutboundBatch,
            EngineImplementationIds.OutboundBatch,
            OperatingSystemEngineRoles.Primary,
            OperatingSystemEngineActivationStatuses.Active,
            "outbound-batch-input.v1",
            "outbound-batch-result.v1",
            "warehouse-outbound-policy.v1"),
        new(
            OperatingSystemIds.WarehouseCommerceFulfillment,
            EngineFamilyIds.PickingBatch,
            EngineImplementationIds.PickingBatch,
            OperatingSystemEngineRoles.Primary,
            OperatingSystemEngineActivationStatuses.Active,
            "picking-batch-input.v1",
            "picking-batch-result.v1",
            "warehouse-picking-policy.v1"),
        new(
            OperatingSystemIds.GroupPurchaseDemand,
            EngineFamilyIds.GroupPurchaseClustering,
            EngineImplementationIds.GroupPurchaseClustering,
            OperatingSystemEngineRoles.Primary,
            OperatingSystemEngineActivationStatuses.Active,
            "group-purchase-clustering-input.v1",
            "group-purchase-clustering-result.v1",
            "group-purchase-clustering-policy.v1"),
        new(
            OperatingSystemIds.GroupPurchaseImport,
            EngineFamilyIds.OutboundBatch,
            EngineImplementationIds.OutboundBatch,
            OperatingSystemEngineRoles.Primary,
            OperatingSystemEngineActivationStatuses.Active,
            "outbound-batch-input.v1",
            "outbound-batch-result.v1",
            "group-import-outbound-policy.v1"),
        new(
            OperatingSystemIds.SsalddelMartUrbanLogistics,
            EngineFamilyIds.OutboundBatch,
            EngineImplementationIds.OutboundBatch,
            OperatingSystemEngineRoles.Primary,
            OperatingSystemEngineActivationStatuses.Active,
            "outbound-batch-input.v1",
            "outbound-batch-result.v1",
            "mart-outbound-policy.v1"),
        new(
            OperatingSystemIds.SsalddelMartUrbanLogistics,
            EngineFamilyIds.PickingBatch,
            EngineImplementationIds.PickingBatch,
            OperatingSystemEngineRoles.Primary,
            OperatingSystemEngineActivationStatuses.Active,
            "picking-batch-input.v1",
            "picking-batch-result.v1",
            "mart-picking-policy.v1")
    ];

    static OperatingSystemEngineCatalog()
    {
        var duplicateSlots = Items
            .Where(item => item.ActivationStatus == OperatingSystemEngineActivationStatuses.Active)
            .GroupBy(item => new { item.OperatingSystemId, item.EngineFamilyId, item.Role })
            .Where(group => group.Count() > 1)
            .Select(group => $"{group.Key.OperatingSystemId}:{group.Key.EngineFamilyId}:{group.Key.Role}")
            .ToArray();
        if (duplicateSlots.Length > 0)
        {
            throw new InvalidOperationException(
                $"Multiple active operating-system engines occupy the same role: {string.Join(',', duplicateSlots)}");
        }

        foreach (var item in Items)
        {
            if (!OperatingSystemIds.TryNormalize(item.OperatingSystemId, out _))
            {
                throw new InvalidOperationException($"Unknown operating system in engine catalog: {item.OperatingSystemId}");
            }

            if (!EngineImplementationCatalog.TryGetFamilyId(item.ImplementationId, out var familyId)
                || !string.Equals(familyId, item.EngineFamilyId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Engine implementation is not bound to the declared family: {item.ImplementationId}");
            }
        }
    }

    public static IReadOnlyList<OperatingSystemEngineCatalogEntry> GetAll() => Items;

    public static IReadOnlyList<OperatingSystemEngineCatalogEntry> GetByOperatingSystem(
        string operatingSystemId)
    {
        var canonical = OperatingSystemIds.Normalize(operatingSystemId);
        return Items
            .Where(item => string.Equals(item.OperatingSystemId, canonical, StringComparison.Ordinal))
            .ToArray();
    }

    public static IReadOnlyList<OperatingSystemEngineCatalogEntry> GetByOperatingSystemAndFamily(
        string operatingSystemId,
        string engineFamilyId)
        => GetByOperatingSystem(operatingSystemId)
            .Where(item => string.Equals(item.EngineFamilyId, engineFamilyId, StringComparison.Ordinal))
            .ToArray();

    public static bool TryGetActivePrimary(
        string operatingSystemId,
        string engineFamilyId,
        out OperatingSystemEngineCatalogEntry entry)
    {
        entry = GetByOperatingSystemAndFamily(operatingSystemId, engineFamilyId)
            .SingleOrDefault(item =>
                item.Role == OperatingSystemEngineRoles.Primary
                && item.ActivationStatus == OperatingSystemEngineActivationStatuses.Active)!;
        return entry is not null;
    }
}

public static class SchedulingPolicyImplementationCatalog
{
    private static readonly IReadOnlySet<string> ActivePolicyCodes = new HashSet<string>(StringComparer.Ordinal)
    {
        "DemandClusterBatching",
        "RecruitmentDeadlineEdf",
        "DemandRecruitmentAging"
    };

    public static bool IsActive(string policyCode)
        => ActivePolicyCodes.Contains(policyCode);
}

public sealed record EngineImplementationBinding(
    string ImplementationId,
    string EngineFamilyId);

public static class EngineImplementationCatalog
{
    private static readonly IReadOnlyDictionary<string, string> FamilyByImplementation =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [EngineImplementationIds.CargoYongdalDispatch] = EngineFamilyIds.TransportRequestDispatch,
            [EngineImplementationIds.FoodDeliveryDispatch] = EngineFamilyIds.TransportRequestDispatch,
            [EngineImplementationIds.OutboundBatch] = EngineFamilyIds.OutboundBatch,
            [EngineImplementationIds.PickingBatch] = EngineFamilyIds.PickingBatch,
            [EngineImplementationIds.GroupPurchaseClustering] = EngineFamilyIds.GroupPurchaseClustering
        };

    public static IReadOnlyList<EngineImplementationBinding> GetAll()
        => FamilyByImplementation
            .Select(pair => new EngineImplementationBinding(pair.Key, pair.Value))
            .OrderBy(binding => binding.ImplementationId, StringComparer.Ordinal)
            .ToArray();

    public static bool TryGetFamilyId(string implementationId, out string engineFamilyId)
        => FamilyByImplementation.TryGetValue(implementationId, out engineFamilyId!);
}
