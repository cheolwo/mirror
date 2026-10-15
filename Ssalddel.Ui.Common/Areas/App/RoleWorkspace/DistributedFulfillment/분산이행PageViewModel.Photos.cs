using System.Text.Json;
using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Ui.Common.Areas.App.RoleWorkspace.DistributedFulfillment;

public sealed partial class 분산이행PageViewModel
{
    private PhotoOperation? photoOperation;
    public 분산사진업무Session Photo { get; }
    public bool HasPendingPhoto => (Photo.UploadStarted || Photo.Restored) && Photo.Work is not null && !HasPendingCommand;
    public bool PhotoInputsLocked => Photo.UploadStarted || Photo.Restored || HasPendingCommand || RecoveryExpired || IsBusy;
    public void ClearPhotos()
    { if (HasPendingCommand || Photo.HasUnresolvedUpload) return; Photo.Clear(); photoOperation = null; }
    public void RemovePhoto(int index)
    { if (!PhotoInputsLocked) { Photo.Remove(index); Changed?.Invoke(); } }
    public Task CapturePhotoAsync(DistributedEvidenceWorkRequest work, string title, string action, string suffix,
        IReadOnlyList<string> allowedActions, int? quantity = null)
    {
        if (!Allows(action, allowedActions) || PhotoInputsLocked) return Task.CompletedTask;
        if (!ValidPhotoWork(work, action)) { ClearPhotos(); return InvalidInputAsync("현재 업무의 위치·상품·남은 수량을 다시 확인해 주세요."); }
        if (!Photo.Bind(work, title)) return InvalidInputAsync("먼저 원래 사진 전송과 완료 요청의 결과를 확인해 주세요.");
        photoOperation = new(action, suffix, quantity);
        return RunAsync(async ct =>
        {
            try
            {
                var captured = await Photo.CaptureAsync(ct); RequireCurrent(ct);
                Message = captured ? "사진을 확인한 뒤 보내고 완료해 주세요." : "사진 촬영이 취소되었습니다.";
            }
            catch (Exception ex) when (ex is NotSupportedException or InvalidOperationException)
            { RequireCurrent(ct); ValidationError = ex.Message; }
        });
    }
    private bool ValidPhotoWork(DistributedEvidenceWorkRequest work, string action)
    {
        if (분산사진업무Session.Body(work) is not { } body || body.RequestId != Guid.Empty) return false;
        if (work.Kind == "StockRelease" && action == "handover-stock-release" && work.StockRelease is { } released)
            return Workspace?.StockReleases.SingleOrDefault(x => x.ReleaseId == released.ReleaseId && x.SiteId == work.AggregateId) is { } release
                && release.SiteRevision == body.ExpectedRevision && release.AssignedUserId == CurrentUserId
                && release.AllowedActions.Contains(action) && !string.IsNullOrWhiteSpace(released.LocationBarcode)
                && released.LocationBarcode == release.LocationBarcode && released.Quantity > 0
                && released.Quantity <= release.AgreedQuantity - release.ReleasedQuantity;
        if (work.Kind == "Receive" && action == "receive" && work.Receive is { } receive && SelectedInbound is { } inbound && work.AggregateId == inbound.PlanId && body.ExpectedRevision == inbound.Revision)
        {
            var rows = receive.Lines.Select(line => (Input: line, Row: inbound.Lines.SingleOrDefault(row => row.LineKey == line.LineKey))).ToArray();
            return rows.Length > 0 && rows.Select(item => item.Input.LineKey).Distinct().Count() == rows.Length && rows.All(item => item.Row is { } row && item.Input.Sku == row.Sku && item.Input.Quantity > 0 && item.Input.Quantity <= row.ExpectedQuantity - row.ReceivedQuantity)
                && rows.Select(item => (item.Row!.LocationId, item.Row.OwnerUserId)).Distinct().Count() == 1 && LocationBarcode(rows[0].Row!.LocationId) == receive.LocationBarcode;
        }
        if (work.Kind == "Pick" && action == "pick" && RoleKey == "cargo-driver" && work.Pick is { } pick && SelectedWave is { } wave && work.AggregateId == wave.WaveId && body.ExpectedRevision == wave.Revision && pick.StopId == CurrentStop?.StopId)
        {
            var rows = pick.Lines.Select(line => (Input: line, Row: CurrentPickLines.SingleOrDefault(row => row.ReservationId == line.ReservationId))).ToArray();
            return rows.Length > 0 && rows.Select(item => item.Input.ReservationId).Distinct().Count() == rows.Length && rows.All(item => item.Row is { } row && CanPick(row) && item.Input.OwnerUserId == row.OwnerUserId && item.Input.Sku == row.Sku && item.Input.Quantity > 0 && item.Input.Quantity <= row.Quantity - row.PickedQuantity - row.ReleasedQuantity)
                && rows.Select(item => (item.Row!.LocationId, item.Row.OwnerUserId)).Distinct().Count() == 1 && LocationBarcode(rows[0].Row!.LocationId) == pick.LocationBarcode;
        }
        if (work.Kind == "Deliver" && action == "deliver" && RoleKey == "cargo-driver" && work.Deliver is { } delivery && SelectedWave is { } deliveryWave && work.AggregateId == deliveryWave.WaveId && body.ExpectedRevision == deliveryWave.Revision && delivery.StopId == CurrentStop?.StopId)
            return ValidReservationLines(delivery.Lines, deliveryWave, line => CurrentDeliveryOrders.Any(order => order.OrderId == line.OrderId && CanDeliver(order)) && line.AllowedActions.Contains("deliver"));
        if (work.Kind == "Return" && action == "return" && work.Return is { } returned && SelectedWave is { } returnWave && work.AggregateId == returnWave.WaveId && body.ExpectedRevision == returnWave.Revision)
        {
            if (!ValidReservationLines(returned.Lines, returnWave, line => line.AllowedActions.Contains("return"))) return false;
            var rows = returned.Lines.Select(line => returnWave.Reservations.Single(row => row.ReservationId == line.ReservationId)).ToArray();
            return rows.Select(row => (row.LocationId, row.OwnerUserId)).Distinct().Count() == 1 && LocationBarcode(rows[0].LocationId) == returned.LocationBarcode;
        }
        if (work.Kind == "ImportLot" && action == "import-lot" && work.ImportLot is { } import && Workspace?.Sites.SingleOrDefault(site => site.SiteId == work.AggregateId) is { } importSite && body.ExpectedRevision == importSite.Revision)
            return import.ActualLocationConfirmed && !string.IsNullOrWhiteSpace(import.LotCode) && importSite.Locations.Any(location => location.LocationId == import.LocationId && location.Barcode == import.LocationBarcode)
                && Workspace.ExistingStockCandidates.Any(stock => stock.InboundItemId == import.InboundItemId && stock.SiteId == importSite.SiteId && (RoleKey != "shipper" || stock.SellerUserId == CurrentUserId));
        return false;
    }
    private static bool ValidReservationLines(IReadOnlyList<DistributedDeliveryLineRequest> lines, DistributedWaveDto wave, Func<DistributedReservationDto, bool> allowed)
        => lines.Count > 0 && lines.Select(line => line.ReservationId).Distinct().Count() == lines.Count && lines.All(line => wave.Reservations.SingleOrDefault(row => row.ReservationId == line.ReservationId) is { } row && allowed(row) && line.Quantity > 0 && line.Quantity <= row.PickedQuantity - row.DeliveredQuantity - row.ReturnedQuantity);
    private string? LocationBarcode(string id) => Workspace?.Sites.SelectMany(site => site.Locations).SingleOrDefault(location => location.LocationId == id)?.Barcode;
    public Task CompletePhotoAsync(bool confirmed)
    {
        if (!confirmed || !CanEdit || photoOperation is null || Photo.Work is null) return Task.CompletedTask;
        if (!Photo.HasPhotos) return InvalidInputAsync("완료 사진을 먼저 촬영하고 확인해 주세요.");
        return RunAsync(UploadAndCompleteAsync);
    }
    public Task RetryPhotoAsync() => !HasPendingPhoto ? Task.CompletedTask : RunAsync(async ct =>
    {
        if (Photo.CompletionRequestId is { } id)
        {
            var receipt = await TryReceiptAsync(id, ct); RequireCurrent(ct);
            if (receipt is not null) { await FinishRecoveredReceiptAsync(receipt, ct); return; }
        }
        if (Photo.Restored)
        {
            await Photo.RecoverUploadsAsync(client, ct, () => RequireCurrent(ct)); RequireCurrent(ct);
            if (Photo.NeedsRecapture)
            { await RefreshAsync(ct); Message = "저장되지 않은 사진의 원본은 보관하지 않았습니다. 사진 작업을 닫고 현재 업무에서 다시 촬영해 주세요."; return; }
        }
        await UploadAndCompleteAsync(ct);
    });
    public Task CancelPhotoWorkAsync()
    {
        if (IsBusy || HasPendingCommand) return Task.CompletedTask;
        return RunAsync(async ct =>
        {
            if (Photo.CompletionRequestId is { } id)
            {
                var receipt = await TryReceiptAsync(id, ct); RequireCurrent(ct);
                if (receipt is not null) { await FinishRecoveredReceiptAsync(receipt, ct); return; }
                await recoveryStore.RemoveAsync(owner!.OwnerId!, RecoveryScope, id, ct); RequireCurrent(ct);
            }
            Photo.Clear(); photoOperation = null; recoveryCreatedAtUtc = null; await RefreshAsync(ct);
            Message = "완료 기록이 없는 사진 작업을 닫았습니다. 현재 위치·수량을 확인하고 다시 촬영해 주세요.";
        });
    }
    private async Task UploadAndCompleteAsync(CancellationToken ct)
    {
        var operation = photoOperation ?? throw new InvalidOperationException("확인할 사진 업무가 없습니다.");
        await Photo.UploadAsync(client, ct, () => RequireCurrent(ct), () => PersistRecoveryAsync(ct)); RequireCurrent(ct);
        var body = Photo.CompletionBody();
        var snapshot = (DistributedCommandRequest)JsonSerializer.Deserialize(JsonSerializer.Serialize(body, body.GetType()), body.GetType())!;
        pending = new(operation.Action is "import-lot" or "handover-stock-release" ? operation.Action : operation.Action + "-batch", operation.Suffix, snapshot,
            Photo.Work!.AggregateId, operation.Quantity, null);
        await SendPendingAsync(ct);
    }
    private sealed record PhotoOperation(string Action, string Suffix, int? Quantity);
}
