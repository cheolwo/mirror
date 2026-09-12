namespace Ssalddel.Contracts.Common.Operations;

public static class OrderWorkNetworkProjectionContract
{
    public const string Revision = "order-work-network-projection.v1";
    public const string AuthorityBoundary = "ReadOnlyProjection";
}

public static class OrderWorkNetworkRootTypeCodes
{
    public const string TransportServiceOrder = "TransportServiceOrder";
}

public static class OrderWorkNetworkNodeTypeCodes
{
    public const string OrderRoot = "OrderRoot";
    public const string ChildExecution = "ChildExecution";
    public const string Result = "Result";
    public const string Acceptance = "Acceptance";
}

public static class OrderWorkNetworkPendingCodes
{
    public const string CargoHandoffPending = "CargoHandoffPending";
    public const string CargoExecutionLedgerPending = "CargoExecutionLedgerPending";
    public const string CompletionReturnPending = "CompletionReturnPending";
    public const string ShipperAcceptancePending = "ShipperAcceptancePending";
}

/// <summary>
/// 서로 다른 업무 원장을 합치지 않고 한 주문의 OS별 실행과 결과 반환만 읽어 주는 관점별 조회 결과입니다.
/// </summary>
public sealed class OrderWorkNetworkProjection
{
    public string ContractRevision { get; init; } = OrderWorkNetworkProjectionContract.Revision;

    public string AuthorityBoundary { get; init; } = OrderWorkNetworkProjectionContract.AuthorityBoundary;

    public bool ContainsPersonalData { get; init; }

    public string RootTypeCode { get; init; } = string.Empty;

    public string RootWorkStableId { get; init; } = string.Empty;

    /// <summary>모든 자식 업무와 결과 반환을 같은 주문 맥락으로 묶는 비식별 안정 키입니다.</summary>
    public string CorrelationStableId { get; init; } = string.Empty;

    public string RootOperatingSystemId { get; init; } = string.Empty;

    public bool RoundTripCompleted { get; init; }

    public IReadOnlyList<OrderWorkNetworkNodeDto> Nodes { get; init; } = [];

    public IReadOnlyList<OrderWorkNetworkInteractionDto> Interactions { get; init; } = [];

    public IReadOnlyList<string> PendingCodes { get; init; } = [];

    public DateTime GeneratedAtUtc { get; init; }
}

public sealed class OrderWorkNetworkNodeDto
{
    public string WorkStableId { get; init; } = string.Empty;

    public string NodeTypeCode { get; init; } = string.Empty;

    public string OperatingSystemId { get; init; } = string.Empty;

    public string LifecycleStageId { get; init; } = string.Empty;

    public string StateCode { get; init; } = string.Empty;

    public long SourceRevision { get; init; }

    public string SourceRevisionBasis { get; init; } = string.Empty;
}

public sealed class OrderWorkNetworkInteractionDto
{
    public string InteractionId { get; init; } = string.Empty;

    public string ContractCode { get; init; } = string.Empty;

    public string ContractRevision { get; init; } = string.Empty;

    public string SourceWorkStableId { get; init; } = string.Empty;

    public string TargetWorkStableId { get; init; } = string.Empty;

    public string HandoffStableId { get; init; } = string.Empty;

    public long HandoffRevision { get; init; }

    public string HandoffStateCode { get; init; } = string.Empty;

    public string CurrentResponsibleOperatingSystemId { get; init; } = string.Empty;
}
