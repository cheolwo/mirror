using Ssalddel.Contracts.Common.Warehouse;

namespace Ssalddel.Ui.Common.Areas.App.RoleWorkspace.DistributedFulfillment;

/// <summary>서버가 허용한 행동을 현재 역할·방문 문맥에 한정합니다. 명령을 전송하거나 업무 상태를 변경하지 않습니다.</summary>
internal sealed class 분산역할명령Coordinator(string roleKey, bool canEdit, bool isLoaded,
    DistributedWorkspaceDto? workspace, DistributedWaveDto? wave)
{
    internal string RoleKey => roleKey;
    internal DistributedWorkspaceDto? Workspace => workspace;
    internal DistributedWaveDto? Wave => wave;
    internal DistributedStopDto? CurrentStop => wave?.Stops.SingleOrDefault(value => value.StopId == wave.CurrentStopId);
    internal IReadOnlyList<DistributedReservationDto> CurrentPickLines => wave is { } selected && CurrentStop is { Kind: DistributedFulfillmentCodes.Pickup } stop
        ? selected.Reservations.Where(value => value.SiteId == stop.SiteId && stop.OrderIds.Contains(value.OrderId)).ToArray() : [];
    internal IReadOnlyList<DistributedOrderDto> CurrentDeliveryOrders => wave is { } selected && CurrentStop is { Kind: DistributedFulfillmentCodes.Delivery } stop
        ? selected.Orders.Where(value => stop.OrderIds.Contains(value.OrderId)).ToArray() : [];
    internal bool Allows(string action, IReadOnlyList<string>? actions = null)
        => canEdit && (actions ?? workspace?.AllowedActions ?? []).Contains(action, StringComparer.Ordinal);
    internal bool CanPick(DistributedReservationDto line)
        => roleKey == "cargo-driver" && Allows("pick", line.AllowedActions) && wave is { } selected
           && Allows("pick", selected.AllowedActions) && CurrentPickLines.Any(value => value.ReservationId == line.ReservationId);
    internal bool CanDeliver(DistributedOrderDto order)
        => roleKey == "cargo-driver" && wave is { } selected && Allows("deliver", selected.AllowedActions)
           && selected.Reservations.Any(line => line.OrderId == order.OrderId)
           && selected.Reservations.Where(line => line.OrderId == order.OrderId).All(line => line.PickedQuantity == line.Quantity && line.ReturnedQuantity == 0 && line.ReleasedQuantity == 0)
           && CurrentDeliveryOrders.Any(value => value.OrderId == order.OrderId);
    internal bool CanManageWaves => (roleKey is "warehouse" or "operator") && isLoaded
        && ((workspace?.AllowedActions.Contains("create-wave") ?? false)
            || (workspace?.Waves.Any(value => value.AllowedActions.Any(action => action is "close" or "plan")) ?? false));
    internal bool CanExecute(string action)
    {
        if ((action is "create-wave" or "close" or "plan") && !CanManageWaves) return false;
        if ((action is "pick" or "deliver" or "accept") && roleKey != "cargo-driver") return false;
        if (action == "acknowledge" && roleKey != "orderer") return false;
        return true;
    }
}
