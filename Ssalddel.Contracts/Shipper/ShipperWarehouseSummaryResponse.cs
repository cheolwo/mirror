namespace Ssalddel.Contracts.Shipper;

/// <summary>인증된 화주 본인의 입고·재고 건수만 제공합니다. 창고 작업자 권한이나 원자료를 제공하지 않습니다.</summary>
public sealed record ShipperWarehouseSummaryResponse
{
    public int PendingInboundCount { get; init; }
    public int ActionableInventoryCount { get; init; }
}
