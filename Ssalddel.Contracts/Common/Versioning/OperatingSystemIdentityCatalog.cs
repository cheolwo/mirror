namespace Ssalddel.Contracts.Common.Versioning;

/// <summary>
/// Persistent operating-system identifiers shared by API metadata, ledgers, and clients.
/// Existing values with the OS suffix remain canonical so stored ledgers stay compatible.
/// </summary>
public static class OperatingSystemIds
{
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
                    new(OperatingSystemLifecycleStageIds.CargoInterruptionRecovery, 80, "중단·회복", "사고·고장·지연·재배차와 이의 제기를 조율합니다.")
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
