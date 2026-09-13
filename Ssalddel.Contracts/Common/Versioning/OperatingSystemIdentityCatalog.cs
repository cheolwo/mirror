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
}

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
