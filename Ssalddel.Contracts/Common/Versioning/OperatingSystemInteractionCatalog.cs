namespace Ssalddel.Contracts.Common.Versioning;

/// <summary>
/// Stable identifiers for explicit work handoffs between independently authoritative operating systems.
/// This catalog describes allowed relationships; it does not execute or accept a handoff.
/// </summary>
public static class OperatingSystemInteractionIds
{
    public const string ShipperRequestToCargo = "shipper-request-to-cargo";
    public const string CargoCompletionToShipperAcceptance = "cargo-completion-to-shipper-acceptance";
    public const string WarehouseOutboundToCargo = "warehouse-outbound-to-cargo";
    public const string MartLastMileToFoodDelivery = "mart-last-mile-to-food-delivery";
}

public static class OperatingSystemInteractionContractCodes
{
    public const string ShipperTransportRequestToDomesticCargo = "ShipperTransportRequestToDomesticCargo";
    public const string DomesticCargoCompletionToShipperAcceptance = "DomesticCargoCompletionToShipperAcceptance";
    public const string WarehouseOutboundToCargoTransport = "WarehouseOutboundToCargoTransport";
    public const string SsalddelMartLastMileToFoodDelivery = "SsalddelMartLastMileToFoodDelivery";
}

public static class OperatingSystemInteractionContractRevisions
{
    public const string ShipperTransportRequestToDomesticCargo = "shipper-transport-request-cargo-handoff.v1";
    public const string DomesticCargoCompletionToShipperAcceptance = "domestic-cargo-completion-shipper-acceptance.v1";
    public const string WarehouseOutboundToCargoTransport = "warehouse-outbound-cargo-handoff.v1";
    public const string SsalddelMartLastMileToFoodDelivery = "ssalddel-mart-last-mile-handoff.v1";
}

public static class OperatingSystemInteractionModes
{
    public const string ChildWork = "ChildWork";
    public const string ResponsibilityTransfer = "ResponsibilityTransfer";
    public const string ReturnForAcceptance = "ReturnForAcceptance";

    public static IReadOnlyList<string> All { get; } =
    [
        ChildWork,
        ResponsibilityTransfer,
        ReturnForAcceptance
    ];
}

public static class OperatingSystemInteractionCardinalities
{
    public const string OneToOne = "1:1";
    public const string OneToMany = "1:N";
    public const string ManyToOne = "N:1";

    public static IReadOnlyList<string> All { get; } = [OneToOne, OneToMany, ManyToOne];
}

public static class OperatingSystemInteractionLifecycleBindingStatuses
{
    public const string Complete = "Complete";
    public const string SourceLifecyclePending = "SourceLifecyclePending";
}

public sealed record OperatingSystemInteractionDefinition(
    string InteractionId,
    string ContractCode,
    string ContractRevision,
    string SourceOperatingSystemId,
    string? SourceLifecycleStageId,
    string TargetOperatingSystemId,
    string? TargetLifecycleStageId,
    string Mode,
    string Cardinality,
    string LifecycleBindingStatus,
    string? ReturnInteractionId,
    string Responsibility);

public sealed record OperatingSystemLifecycleCoverageDefinition(
    string OperatingSystemId,
    bool HasLifecycle,
    int DefinedStageCount);

public static class OperatingSystemOrderSegments
{
    public const string BeforeOrder = "BeforeOrder";
    public const string OrderCommitment = "OrderCommitment";
    public const string Fulfillment = "Fulfillment";
    public const string ResultReturn = "ResultReturn";
}

public static class OperatingSystemCurrentStructureStatuses
{
    public const string VerifiedFromLifecycle = "VerifiedFromLifecycle";
    public const string LifecyclePending = "LifecyclePending";
}

public sealed record OperatingSystemCurrentStructureDefinition(
    string OperatingSystemId,
    bool HasLifecycle,
    int DefinedStageCount,
    IReadOnlyList<string> OrderSegments,
    string Status);

/// <summary>
/// Read-only design catalog for the OS-to-OS handoffs already implemented by the server.
/// Runtime handoff state remains authoritative in the operating-system handoff ledger.
/// </summary>
public static class OperatingSystemInteractionCatalog
{
    public const string CatalogRevision = "operating-system-interaction-catalog.v1";

    private static readonly IReadOnlyList<OperatingSystemInteractionDefinition> Items =
    [
        new(
            OperatingSystemInteractionIds.ShipperRequestToCargo,
            OperatingSystemInteractionContractCodes.ShipperTransportRequestToDomesticCargo,
            OperatingSystemInteractionContractRevisions.ShipperTransportRequestToDomesticCargo,
            OperatingSystemIds.ShipperTransportManagement,
            OperatingSystemLifecycleStageIds.ShipperTransportHandoff,
            OperatingSystemIds.DomesticCargoTransport,
            OperatingSystemLifecycleStageIds.CargoRequest,
            OperatingSystemInteractionModes.ResponsibilityTransfer,
            OperatingSystemInteractionCardinalities.OneToOne,
            OperatingSystemInteractionLifecycleBindingStatuses.Complete,
            OperatingSystemInteractionIds.CargoCompletionToShipperAcceptance,
            "확정된 화주 운송의뢰의 실행 책임을 화물운송 OS에 넘기되 배차나 기사 상태를 직접 변경하지 않습니다."),
        new(
            OperatingSystemInteractionIds.CargoCompletionToShipperAcceptance,
            OperatingSystemInteractionContractCodes.DomesticCargoCompletionToShipperAcceptance,
            OperatingSystemInteractionContractRevisions.DomesticCargoCompletionToShipperAcceptance,
            OperatingSystemIds.DomesticCargoTransport,
            OperatingSystemLifecycleStageIds.CargoEvidenceSettlement,
            OperatingSystemIds.ShipperTransportManagement,
            OperatingSystemLifecycleStageIds.ShipperDeliveryAcceptance,
            OperatingSystemInteractionModes.ReturnForAcceptance,
            OperatingSystemInteractionCardinalities.OneToOne,
            OperatingSystemInteractionLifecycleBindingStatuses.Complete,
            null,
            "하차 증빙이 있는 운송 완료 결과를 화주 인수·검수로 반환하며 정산 완료를 자동 확정하지 않습니다."),
        new(
            OperatingSystemInteractionIds.WarehouseOutboundToCargo,
            OperatingSystemInteractionContractCodes.WarehouseOutboundToCargoTransport,
            OperatingSystemInteractionContractRevisions.WarehouseOutboundToCargoTransport,
            OperatingSystemIds.WarehouseCommerceFulfillment,
            null,
            OperatingSystemIds.DomesticCargoTransport,
            OperatingSystemLifecycleStageIds.CargoRequest,
            OperatingSystemInteractionModes.ChildWork,
            OperatingSystemInteractionCardinalities.OneToOne,
            OperatingSystemInteractionLifecycleBindingStatuses.SourceLifecyclePending,
            null,
            "완료된 창고 출고를 이미 존재하는 화물 운송의뢰에 결속하며 새 운송 의뢰나 배차를 확정하지 않습니다."),
        new(
            OperatingSystemInteractionIds.MartLastMileToFoodDelivery,
            OperatingSystemInteractionContractCodes.SsalddelMartLastMileToFoodDelivery,
            OperatingSystemInteractionContractRevisions.SsalddelMartLastMileToFoodDelivery,
            OperatingSystemIds.SsalddelMartUrbanLogistics,
            OperatingSystemLifecycleStageIds.MartLastMileHandoff,
            OperatingSystemIds.FoodDelivery,
            OperatingSystemLifecycleStageIds.FoodDispatch,
            OperatingSystemInteractionModes.ChildWork,
            OperatingSystemInteractionCardinalities.OneToOne,
            OperatingSystemInteractionLifecycleBindingStatuses.Complete,
            null,
            "마트 주문 전체가 아니라 라스트마일 자식 업무만 음식배달 OS에 인계합니다.")
    ];

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> VerifiedOrderSegments =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            [OperatingSystemIds.ShipperTransportManagement] =
            [
                OperatingSystemOrderSegments.OrderCommitment,
                OperatingSystemOrderSegments.Fulfillment,
                OperatingSystemOrderSegments.ResultReturn
            ],
            [OperatingSystemIds.DomesticCargoTransport] =
            [
                OperatingSystemOrderSegments.Fulfillment,
                OperatingSystemOrderSegments.ResultReturn
            ],
            [OperatingSystemIds.FoodDelivery] =
            [
                OperatingSystemOrderSegments.OrderCommitment,
                OperatingSystemOrderSegments.Fulfillment,
                OperatingSystemOrderSegments.ResultReturn
            ],
            [OperatingSystemIds.SsalddelMartUrbanLogistics] =
            [
                OperatingSystemOrderSegments.BeforeOrder,
                OperatingSystemOrderSegments.OrderCommitment,
                OperatingSystemOrderSegments.Fulfillment,
                OperatingSystemOrderSegments.ResultReturn
            ]
        };

    static OperatingSystemInteractionCatalog()
    {
        ValidateDefinitions();
    }

    public static IReadOnlyList<OperatingSystemInteractionDefinition> GetAll() => Items;

    public static OperatingSystemInteractionDefinition Get(string interactionId)
        => Items.SingleOrDefault(item => string.Equals(item.InteractionId, interactionId, StringComparison.Ordinal))
           ?? throw new ArgumentException($"Unknown operating-system interaction: {interactionId}", nameof(interactionId));

    public static IReadOnlyList<OperatingSystemInteractionDefinition> GetBySourceOperatingSystem(string operatingSystemId)
    {
        var canonical = OperatingSystemIds.Normalize(operatingSystemId);
        return Items.Where(item => item.SourceOperatingSystemId == canonical).ToArray();
    }

    public static IReadOnlyList<OperatingSystemInteractionDefinition> GetByTargetOperatingSystem(string operatingSystemId)
    {
        var canonical = OperatingSystemIds.Normalize(operatingSystemId);
        return Items.Where(item => item.TargetOperatingSystemId == canonical).ToArray();
    }

    public static IReadOnlyList<OperatingSystemLifecycleCoverageDefinition> GetLifecycleCoverage()
        => OperatingSystemIds.All
            .Select(operatingSystemId => OperatingSystemLifecycleCatalog.TryGet(operatingSystemId, out var lifecycle)
                ? new OperatingSystemLifecycleCoverageDefinition(operatingSystemId, true, lifecycle.Stages.Count)
                : new OperatingSystemLifecycleCoverageDefinition(operatingSystemId, false, 0))
            .ToArray();

    /// <summary>
    /// 모든 안정 OS 식별자의 현행 대조표입니다. 실제 생명주기가 없는 OS는 단계나 주문 구간을
    /// 추측하지 않고 LifecyclePending으로 남깁니다.
    /// </summary>
    public static IReadOnlyList<OperatingSystemCurrentStructureDefinition> GetCurrentStructure()
        => OperatingSystemIds.All
            .Select(operatingSystemId =>
            {
                var hasLifecycle = OperatingSystemLifecycleCatalog.TryGet(operatingSystemId, out var lifecycle);
                return new OperatingSystemCurrentStructureDefinition(
                    operatingSystemId,
                    hasLifecycle,
                    hasLifecycle ? lifecycle.Stages.Count : 0,
                    VerifiedOrderSegments.TryGetValue(operatingSystemId, out var segments) ? segments : [],
                    hasLifecycle
                        ? OperatingSystemCurrentStructureStatuses.VerifiedFromLifecycle
                        : OperatingSystemCurrentStructureStatuses.LifecyclePending);
            })
            .ToArray();

    private static void ValidateDefinitions()
    {
        if (Items.Select(item => item.InteractionId).Distinct(StringComparer.Ordinal).Count() != Items.Count)
        {
            throw new InvalidOperationException("Operating-system interaction IDs must be unique.");
        }

        if (Items.Select(item => item.ContractCode).Distinct(StringComparer.Ordinal).Count() != Items.Count)
        {
            throw new InvalidOperationException("Operating-system interaction contract codes must be unique.");
        }

        foreach (var item in Items)
        {
            _ = OperatingSystemIds.Normalize(item.SourceOperatingSystemId);
            _ = OperatingSystemIds.Normalize(item.TargetOperatingSystemId);

            if (item.SourceOperatingSystemId == item.TargetOperatingSystemId)
            {
                throw new InvalidOperationException($"An OS interaction cannot target itself: {item.InteractionId}");
            }
            if (!OperatingSystemInteractionModes.All.Contains(item.Mode, StringComparer.Ordinal))
            {
                throw new InvalidOperationException($"Unknown OS interaction mode: {item.InteractionId}/{item.Mode}");
            }
            if (!OperatingSystemInteractionCardinalities.All.Contains(item.Cardinality, StringComparer.Ordinal))
            {
                throw new InvalidOperationException($"Unknown OS interaction cardinality: {item.InteractionId}/{item.Cardinality}");
            }

            ValidateLifecycleStage(item.SourceOperatingSystemId, item.SourceLifecycleStageId, item.InteractionId, "source");
            ValidateLifecycleStage(item.TargetOperatingSystemId, item.TargetLifecycleStageId, item.InteractionId, "target");

            var bindingComplete = item.SourceLifecycleStageId is not null && item.TargetLifecycleStageId is not null;
            if (bindingComplete != (item.LifecycleBindingStatus == OperatingSystemInteractionLifecycleBindingStatuses.Complete))
            {
                throw new InvalidOperationException($"Lifecycle binding status does not match stage bindings: {item.InteractionId}");
            }

            if (item.ReturnInteractionId is not null
                && Items.All(candidate => candidate.InteractionId != item.ReturnInteractionId))
            {
                throw new InvalidOperationException($"Unknown return interaction: {item.InteractionId}/{item.ReturnInteractionId}");
            }
        }
    }

    private static void ValidateLifecycleStage(
        string operatingSystemId,
        string? lifecycleStageId,
        string interactionId,
        string side)
    {
        if (lifecycleStageId is null)
        {
            return;
        }

        if (!OperatingSystemLifecycleCatalog.TryGet(operatingSystemId, out var lifecycle)
            || lifecycle.Stages.All(stage => stage.StageId != lifecycleStageId))
        {
            throw new InvalidOperationException(
                $"Unknown {side} lifecycle stage in OS interaction: {interactionId}/{lifecycleStageId}");
        }
    }
}
