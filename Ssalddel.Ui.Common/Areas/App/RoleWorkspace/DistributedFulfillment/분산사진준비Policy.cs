using Ssalddel.Contracts.Common.Warehouse;

namespace Ssalddel.Ui.Common.Areas.App.RoleWorkspace.DistributedFulfillment;

/// <summary>촬영 전에 현재 업무와 입력의 연결만 검사합니다. 사진 원본·업로드·완료 요청의 수명은 보관하지 않습니다.</summary>
internal sealed class 분산사진준비Policy(분산역할명령Coordinator commands, string? currentUserId, DistributedInboundDto? selectedInbound)
{
    private DistributedWorkspaceDto? Workspace => commands.Workspace;
    private DistributedWaveDto? SelectedWave => commands.Wave;
    private string RoleKey => commands.RoleKey;
    internal bool IsValid(DistributedEvidenceWorkRequest work, string action)
    {
        if (분산사진업무Session.Body(work) is not { } body || body.RequestId != Guid.Empty) return false;
        if (work.Kind == "StockRelease" && action == "handover-stock-release" && work.StockRelease is { } released)
            return Workspace?.StockReleases.SingleOrDefault(x => x.ReleaseId == released.ReleaseId && x.SiteId == work.AggregateId) is { } release
                && release.SiteRevision == body.ExpectedRevision && release.AssignedUserId == currentUserId
                && release.AllowedActions.Contains(action) && !string.IsNullOrWhiteSpace(released.LocationBarcode)
                && released.LocationBarcode == release.LocationBarcode && released.Quantity > 0
                && released.Quantity <= release.AgreedQuantity - release.ReleasedQuantity;
        if (work.Kind == "Receive" && action == "receive" && work.Receive is { } receive && selectedInbound is { } inbound && work.AggregateId == inbound.PlanId && body.ExpectedRevision == inbound.Revision)
        {
            var rows = receive.Lines.Select(line => (Input: line, Row: inbound.Lines.SingleOrDefault(row => row.LineKey == line.LineKey))).ToArray();
            return rows.Length > 0 && rows.Select(item => item.Input.LineKey).Distinct().Count() == rows.Length && rows.All(item => item.Row is { } row && item.Input.Sku == row.Sku && item.Input.Quantity > 0 && item.Input.Quantity <= row.ExpectedQuantity - row.ReceivedQuantity)
                && rows.Select(item => (item.Row!.LocationId, item.Row.OwnerUserId)).Distinct().Count() == 1 && LocationBarcode(rows[0].Row!.LocationId) == receive.LocationBarcode;
        }
        if (work.Kind == "Pick" && action == "pick" && RoleKey == "cargo-driver" && work.Pick is { } pick && SelectedWave is { } wave && work.AggregateId == wave.WaveId && body.ExpectedRevision == wave.Revision && pick.StopId == commands.CurrentStop?.StopId)
        {
            var rows = pick.Lines.Select(line => (Input: line, Row: commands.CurrentPickLines.SingleOrDefault(row => row.ReservationId == line.ReservationId))).ToArray();
            return rows.Length > 0 && rows.Select(item => item.Input.ReservationId).Distinct().Count() == rows.Length && rows.All(item => item.Row is { } row && commands.CanPick(row) && item.Input.OwnerUserId == row.OwnerUserId && item.Input.Sku == row.Sku && item.Input.Quantity > 0 && item.Input.Quantity <= row.Quantity - row.PickedQuantity - row.ReleasedQuantity)
                && rows.Select(item => (item.Row!.LocationId, item.Row.OwnerUserId)).Distinct().Count() == 1 && LocationBarcode(rows[0].Row!.LocationId) == pick.LocationBarcode;
        }
        if (work.Kind == "Deliver" && action == "deliver" && RoleKey == "cargo-driver" && work.Deliver is { } delivery && SelectedWave is { } deliveryWave && work.AggregateId == deliveryWave.WaveId && body.ExpectedRevision == deliveryWave.Revision && delivery.StopId == commands.CurrentStop?.StopId)
            return ValidReservationLines(delivery.Lines, deliveryWave, line => commands.CurrentDeliveryOrders.Any(order => order.OrderId == line.OrderId && commands.CanDeliver(order)) && line.AllowedActions.Contains("deliver"));
        if (work.Kind == "Return" && action == "return" && work.Return is { } returned && SelectedWave is { } returnWave && work.AggregateId == returnWave.WaveId && body.ExpectedRevision == returnWave.Revision)
        {
            if (!ValidReservationLines(returned.Lines, returnWave, line => line.AllowedActions.Contains("return"))) return false;
            var rows = returned.Lines.Select(line => returnWave.Reservations.Single(row => row.ReservationId == line.ReservationId)).ToArray();
            return rows.Select(row => (row.LocationId, row.OwnerUserId)).Distinct().Count() == 1 && LocationBarcode(rows[0].LocationId) == returned.LocationBarcode;
        }
        if (work.Kind == "ImportLot" && action == "import-lot" && work.ImportLot is { } import && Workspace?.Sites.SingleOrDefault(site => site.SiteId == work.AggregateId) is { } importSite && body.ExpectedRevision == importSite.Revision)
            return import.ActualLocationConfirmed && !string.IsNullOrWhiteSpace(import.LotCode) && importSite.Locations.Any(location => location.LocationId == import.LocationId && location.Barcode == import.LocationBarcode)
                && Workspace.ExistingStockCandidates.Any(stock => stock.InboundItemId == import.InboundItemId && stock.SiteId == importSite.SiteId && (RoleKey != "shipper" || stock.SellerUserId == currentUserId));
        return false;
    }
    private static bool ValidReservationLines(IReadOnlyList<DistributedDeliveryLineRequest> lines, DistributedWaveDto wave, Func<DistributedReservationDto, bool> allowed)
        => lines.Count > 0 && lines.Select(line => line.ReservationId).Distinct().Count() == lines.Count && lines.All(line => wave.Reservations.SingleOrDefault(row => row.ReservationId == line.ReservationId) is { } row && allowed(row) && line.Quantity > 0 && line.Quantity <= row.PickedQuantity - row.DeliveredQuantity - row.ReturnedQuantity);
    private string? LocationBarcode(string id) => Workspace?.Sites.SelectMany(site => site.Locations).SingleOrDefault(location => location.LocationId == id)?.Barcode;
}
