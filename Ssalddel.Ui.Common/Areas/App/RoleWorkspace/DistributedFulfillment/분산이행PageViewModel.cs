using System.Text.Json;
using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Ui.Common.Areas.App.RoleWorkspace.DistributedFulfillment;

/// <summary>한 역할의 조회·명시 처리·같은 요청 재확인을 소유합니다. 권한과 순회 순서는 서버가 결정합니다.</summary>
public sealed class 분산이행PageViewModel : IDisposable
{
    private readonly 분산이행Client client;
    private CancellationTokenSource lifetime = new();
    private RoleWorkspaceIdentity? owner;
    private long generation;
    private bool disposed;
    private PendingCommand? pending;
    private PhotoOperation? photoOperation;
    private DistributedRootOrderCandidatesDto? rootOrderCandidates;
    private long rootOrderGeneration;
    public 분산이행PageViewModel(분산이행Client client, string screen, I업무사진촬영Adapter? capture = null)
    {
        if (!분산이행Navigation.AllowsScreen(client.RoleKey, screen))
            throw new ArgumentException("활동 역할의 업무 화면을 확인해 주세요.");
        this.client = client; Screen = screen; Photo = new(capture ?? new 미지원업무사진촬영Adapter()); client.Transport.Changed += AuthenticationChanged;
    }
    public event Action? Changed;
    public string RoleKey => client.RoleKey;
    public string Screen { get; }
    public bool IsBusy { get; private set; }
    public bool RequiresLogin { get; private set; }
    public bool IsLoaded { get; private set; }
    public string? Error { get; private set; }
    public string? Message { get; private set; }
    public string? ValidationError { get; private set; }
    public 분산사진업무Session Photo { get; }
    public bool HasPendingPhoto => Photo.UploadStarted && Photo.Work is not null && !HasPendingCommand;
    public bool PhotoInputsLocked => Photo.UploadStarted || HasPendingCommand || IsBusy;
    public DistributedWorkspaceDto? Workspace { get; private set; }
    public DistributedWaveDto? SelectedWave { get; private set; }
    public string? SelectedId { get; private set; }
    public DistributedSiteDto? SelectedSite => Workspace?.Sites.SingleOrDefault(value => value.SiteId == SelectedId);
    public DistributedInboundDto? SelectedInbound => Workspace?.InboundPlans.SingleOrDefault(value => value.PlanId == SelectedId);
    public DistributedStorageAgreementDto? SelectedAgreement => Workspace?.StorageAgreements.SingleOrDefault(value => value.AgreementId == SelectedId);
    public bool HasPendingCommand => pending is not null;
    public Guid? RequestId => pending?.Body.RequestId;
    public bool CanEdit => IsLoaded && !IsBusy && !RequiresLogin && !HasPendingCommand && !Photo.HasUnresolvedUpload && Error is null;
    public bool IsLoadingRootOrders { get; private set; }
    public string? RootOrdersError { get; private set; }
    public string? SelectedRootOrderId { get; private set; }
    public IReadOnlyList<DistributedRootOrderCandidateDto> RootOrderCandidates => rootOrderCandidates?.Candidates ?? [];
    public bool RootOrdersLoaded => owner == client.Transport.Identity(RoleKey) && rootOrderCandidates is { } snapshot && snapshot.CurrentUserId == CurrentUserId
        && snapshot.WaveId == SelectedWave?.WaveId && snapshot.WaveRevision == SelectedWave.Revision;
    public bool HasSelectedRootOrder => RootOrdersLoaded && SelectedRootOrderId is { } id
        && RootOrderCandidates.Any(candidate => candidate.RootOrderLedgerId == id);

    public void ClearRootOrderCandidates()
    {
        ++rootOrderGeneration; rootOrderCandidates = null; SelectedRootOrderId = null;
        RootOrdersError = null; IsLoadingRootOrders = false;
    }
    public void SelectRootOrder(string? id)
    {
        SelectedRootOrderId = RootOrdersLoaded && RootOrderCandidates.Any(candidate => candidate.RootOrderLedgerId == id) ? id : null;
        Changed?.Invoke();
    }
    public async Task LoadRootOrderCandidatesAsync(string cargoId)
    {
        if (RoleKey != "shipper" || Screen != "orders" || !CanEdit || SelectedWave is not { } wave
            || !Allows("add-order", wave.AllowedActions) || Workspace?.CargoCandidates.Any(cargo => cargo.CargoRequestId == cargoId) != true) return;
        ClearRootOrderCandidates(); var mine = rootOrderGeneration; var ct = lifetime.Token;
        IsLoadingRootOrders = true; Changed?.Invoke();
        bool Current() => !disposed && mine == rootOrderGeneration && !ct.IsCancellationRequested
            && owner == client.Transport.Identity(RoleKey) && SelectedWave?.WaveId == wave.WaveId && SelectedWave.Revision == wave.Revision;
        try
        {
            var snapshot = await client.RootOrderCandidatesAsync(wave.WaveId, cargoId, ct);
            if (!Current()) return;
            if (snapshot.CurrentUserId != CurrentUserId || snapshot.WaveId != wave.WaveId || snapshot.WaveRevision != wave.Revision
                || snapshot.CargoRequestId != cargoId || snapshot.Candidates is null
                || snapshot.Candidates.Count > 200 || snapshot.Candidates.Select(candidate => candidate.RootOrderLedgerId).Distinct().Count() != snapshot.Candidates.Count
                || snapshot.Candidates.Any(candidate => CleanId(candidate.RootOrderLedgerId) is null || candidate.Revision < 0
                    || string.IsNullOrWhiteSpace(candidate.Label) || candidate.StatusCode is not ("초안" or "진행중")))
                throw new InvalidOperationException("원주문의 현재 당사자와 선택 조건을 확인하지 못했습니다.");
            rootOrderCandidates = snapshot;
        }
        catch (Exception) when (!Current()) { }
        catch (Exception ex) when (Current())
        {
            var status = ex switch { RoleWorkspaceAccessException role => role.StatusCode, SsalddelApiException api => api.StatusCode, _ => 0 };
            if (status == 401)
            {
                AuthenticationChanged(); RequiresLogin = true; Error = "로그인한 뒤 업무를 다시 확인해 주세요.";
                Changed?.Invoke(); return;
            }
            RootOrdersError = status == 409 ? "주문이나 배송 조건이 바뀌었습니다. 최신 업무를 다시 조회한 뒤 원주문을 확인해 주세요."
                : "연결할 원주문을 확인하지 못했습니다. 다시 조회해 주세요.";
        }
        finally { if (Current()) { IsLoadingRootOrders = false; Changed?.Invoke(); } }
    }
    public string? CurrentUserId => owner?.OwnerId;
    public DistributedStopDto? CurrentStop => SelectedWave?.Stops.SingleOrDefault(value => value.StopId == SelectedWave.CurrentStopId);
    public IReadOnlyList<DistributedReservationDto> CurrentPickLines => SelectedWave is { } wave && CurrentStop is { Kind: DistributedFulfillmentCodes.Pickup } stop
        ? wave.Reservations.Where(value => value.SiteId == stop.SiteId && stop.OrderIds.Contains(value.OrderId)).ToArray() : [];
    public IReadOnlyList<DistributedOrderDto> CurrentDeliveryOrders => SelectedWave is { } wave && CurrentStop is { Kind: DistributedFulfillmentCodes.Delivery } stop
        ? wave.Orders.Where(value => stop.OrderIds.Contains(value.OrderId)).ToArray() : [];
    public bool Allows(string action, IReadOnlyList<string>? actions = null)
        => CanEdit && (actions ?? Workspace?.AllowedActions ?? []).Contains(action, StringComparer.Ordinal);
    public bool CanPick(DistributedReservationDto line)
        => RoleKey == "cargo-driver" && Allows("pick", line.AllowedActions) && SelectedWave is { } wave
           && Allows("pick", wave.AllowedActions) && CurrentPickLines.Any(value => value.ReservationId == line.ReservationId);
    public bool CanDeliver(DistributedOrderDto order)
        => RoleKey == "cargo-driver" && SelectedWave is { } wave && Allows("deliver", wave.AllowedActions)
           && wave.Reservations.Any(line => line.OrderId == order.OrderId)
           && wave.Reservations.Where(line => line.OrderId == order.OrderId).All(line => line.PickedQuantity == line.Quantity && line.ReturnedQuantity == 0 && line.ReleasedQuantity == 0)
           && CurrentDeliveryOrders.Any(value => value.OrderId == order.OrderId);
    public bool CanAcknowledge(DistributedOrderDto order)
        => RoleKey == "orderer" && order.IsCurrentCustomer && Allows("acknowledge", order.AllowedActions);
    public bool CanCancel(DistributedOrderDto order)
        => (RoleKey is "shipper" or "orderer" or "warehouse" or "operator")
           && (RoleKey != "orderer" || order.IsCurrentCustomer) && Allows("cancel", order.AllowedActions);
    public bool CanRetryOrderLedger(DistributedOrderDto order)
        => (RoleKey is "orderer" or "shipper") && (RoleKey != "orderer" || order.IsCurrentCustomer)
           && order.OrderLedgerConnectionStatusCode == "Pending" && !string.IsNullOrWhiteSpace(order.RootOrderLedgerId)
           && order.RootOrderLedgerRevision.HasValue && Allows("retry-order-ledger", order.AllowedActions);
    public Task RetryOrderLedgerAsync(string orderId)
    {
        var order = SelectedWave?.Orders.SingleOrDefault(x => x.OrderId == orderId);
        if (order is null || !CanRetryOrderLedger(order)) return Task.CompletedTask;
        return ExecuteAsync("retry-order-ledger", $"waves/{분산이행Client.Segment(SelectedWave!.WaveId)}/orders/{분산이행Client.Segment(orderId)}/ledger",
            new DistributedOrderLedgerLinkRequest { ExpectedRevision = SelectedWave.Revision,
                RootOrderLedgerId = order.RootOrderLedgerId!, ExpectedRootRevision = order.RootOrderLedgerRevision!.Value },
            SelectedWave.WaveId, order.AllowedActions, true);
    }
    public bool CanManageWaves => (RoleKey is "warehouse" or "operator") && IsLoaded
        && ((Workspace?.AllowedActions.Contains("create-wave") ?? false)
            || (Workspace?.Waves.Any(wave => wave.AllowedActions.Any(action => action is "close" or "plan")) ?? false));

    public Task AcceptAsync(IReadOnlyCollection<string> acknowledgedCodes, bool confirmed)
    {
        var wave = SelectedWave;
        if (!confirmed || RoleKey != "cargo-driver" || wave is null || !Allows("accept", wave.AllowedActions)) return Task.CompletedTask;
        var required = wave.AcceptanceWarnings.Select(warning => warning.Code).ToHashSet(StringComparer.Ordinal);
        if (wave.AcceptanceWarnings.Any(warning => string.IsNullOrWhiteSpace(warning.Code) || string.IsNullOrWhiteSpace(warning.Message))
            || !required.SetEquals(acknowledgedCodes))
            return InvalidInputAsync("서버가 표시한 수락 조건을 각각 확인해 주세요. 조회하지 않은 조건은 승인할 수 없습니다.");
        return ExecuteAsync("accept", $"waves/{분산이행Client.Segment(wave.WaveId)}/accept",
            new DistributedRoundAcceptRequest { ExpectedRevision = wave.Revision, AcknowledgedWarningCodes = acknowledgedCodes.Distinct(StringComparer.Ordinal).ToArray() },
            wave.WaveId, wave.AllowedActions, true);
    }

    public Task AddOrderAsync(string cargoId, string reference, IReadOnlyList<DistributedOrderLineRequest> lines, bool confirmed, bool connectRootOrder = false)
    {
        var wave = SelectedWave;
        if (!confirmed || RoleKey != "shipper" || wave is null || !Allows("add-order", wave.AllowedActions)) return Task.CompletedTask;
        var root = connectRootOrder && HasSelectedRootOrder && rootOrderCandidates?.CargoRequestId == cargoId
            ? RootOrderCandidates.Single(candidate => candidate.RootOrderLedgerId == SelectedRootOrderId) : null;
        if (connectRootOrder && (IsLoadingRootOrders || root is null))
            return InvalidInputAsync("현재 화물 의뢰의 원주문을 다시 조회하고 목록에서 선택해 주세요.");
        if (string.IsNullOrWhiteSpace(reference) || Workspace?.CargoCandidates.Any(cargo => cargo.CargoRequestId == cargoId) != true
            || lines.Count == 0 || lines.Any(line => line.Quantity < 1) || lines.Any(line => line.ExistingOutboundPlanId.HasValue) && lines.Any(line => !line.ExistingOutboundPlanId.HasValue))
            return InvalidInputAsync("조회한 내 의뢰와 상품을 선택하고 새 예약 또는 기존 예약 편입 중 한 방식으로 확인해 주세요.");
        if (lines.All(line => line.ExistingOutboundPlanId.HasValue))
        {
            if (lines.Select(line => line.ExistingOutboundPlanId).Distinct().Count() != lines.Count || lines.Any(line =>
                Workspace.ExistingOutboundCandidates.SingleOrDefault(candidate => candidate.ExistingOutboundPlanId == line.ExistingOutboundPlanId) is not { } candidate
                || candidate.CargoRequestId != cargoId || candidate.LotId != line.LotId || candidate.Quantity != line.Quantity || candidate.SellerUserId != CurrentUserId))
                return InvalidInputAsync("같은 내 화물 의뢰의 조회한 기존 예약을 원래 수량 그대로 선택해 주세요.");
        }
        else if (lines.GroupBy(line => line.LotId).Any(group =>
            Workspace.Lots.SingleOrDefault(lot => lot.LotId == group.Key) is not { } lot || lot.SellerUserId != CurrentUserId || group.Sum(line => (long)line.Quantity) > lot.AvailableQuantity))
            return InvalidInputAsync("조회한 본인 판매 재고의 가용 수량 안에서 새 예약을 입력해 주세요.");
        return ExecuteAsync("add-order", $"waves/{분산이행Client.Segment(wave.WaveId)}/orders", new DistributedOrderRequest
            { ExpectedRevision = wave.Revision, OrderReference = reference.Trim(), CargoRequestId = cargoId, Lines = lines.ToArray(),
                RootOrderLedgerId = root?.RootOrderLedgerId, ExpectedRootRevision = root?.Revision }, wave.WaveId, wave.AllowedActions, true);
    }

    public Task CancelAsync(string orderId, string reason, bool confirmed)
    {
        var order = SelectedWave?.Orders.SingleOrDefault(value => value.OrderId == orderId);
        if (!confirmed || order is null || !CanCancel(order)) return Task.CompletedTask;
        if (string.IsNullOrWhiteSpace(reason)) return InvalidInputAsync("선택한 주문의 취소 사유를 입력해 주세요.");
        return ExecuteAsync("cancel", $"waves/{분산이행Client.Segment(SelectedWave!.WaveId)}/cancel",
            new DistributedCancelRequest { ExpectedRevision = SelectedWave.Revision, OrderId = order.OrderId, Reason = reason.Trim() },
            SelectedWave.WaveId, order.AllowedActions, true);
    }

    public Task LoadAsync(string? selectedId = null)
    {
        if (HasPendingCommand || HasPendingPhoto || IsBusy) return Task.CompletedTask;
        if (!ValidSelection(selectedId ?? SelectedId)) return Task.CompletedTask;
        SelectedId = CleanId(selectedId ?? SelectedId); Message = null;
        return RunAsync(RefreshAsync);
    }
    public Task ChooseAsync(string id)
    {
        if (HasPendingCommand || HasPendingPhoto || IsBusy) return Task.CompletedTask;
        if (!ValidSelection(id)) return Task.CompletedTask;
        ClearPhotos(); ClearRootOrderCandidates(); SelectedId = CleanId(id); SelectedWave = null; Message = null;
        return RunAsync(RefreshAsync);
    }
    private bool ValidSelection(string? id)
    {
        if (id is null || RoleWorkspaceNavigation.StableId(id) is not null) return true;
        Workspace = null; SelectedWave = null; IsLoaded = false; Error = "업무 대상의 번호를 확인해 주세요."; Changed?.Invoke(); return false;
    }
    private async Task RefreshAsync(CancellationToken ct)
    {
        ClearRootOrderCandidates();
        var workspace = await client.WorkspaceAsync(ct); RequireCurrent(ct); ValidateWorkspace(workspace);
        DistributedWaveDto? wave = null;
        if ((Screen is "round" or "waves" or "orders" or "returns") && SelectedId is { } id)
        {
            if (!workspace.Waves.Any(value => value.WaveId == id))
                throw new InvalidOperationException("현재 역할에서 선택한 배송 묶음을 확인할 수 없습니다.");
            wave = await client.WaveAsync(id, ct); RequireCurrent(ct); ValidateWave(wave, id);
            if (wave.Revision < workspace.Waves.Single(value => value.WaveId == id).Revision)
                throw new InvalidOperationException("선택한 배송 묶음의 최신 판본을 다시 확인해 주세요.");
        }
        Workspace = workspace; SelectedWave = wave; IsLoaded = true;
    }
    private void ValidateWorkspace(DistributedWorkspaceDto value)
    {
        if (value.Profile != DistributedFulfillmentCodes.Profile || !value.IsSimulation || value.CurrentUserId != owner?.OwnerId
            || value.Sites.Select(item => item.SiteId).Distinct().Count() != value.Sites.Count
            || value.Waves.Select(item => item.WaveId).Distinct().Count() != value.Waves.Count
            || value.InboundPlans.Select(item => item.PlanId).Distinct().Count() != value.InboundPlans.Count
            || value.Sites.Any(item => CleanId(item.SiteId) is null || item.Revision < 1)
            || value.InboundPlans.Any(item => CleanId(item.PlanId) is null || item.Revision < 1))
            throw new InvalidOperationException("조회한 업무의 사용자·대상·판본을 확인하지 못했습니다.");
        if (RoleKey == "orderer" && (value.Lots.Count > 0 || value.InboundPlans.Count > 0 || value.Sites.Count > 0 || value.ExistingOutboundCandidates.Count > 0 || value.StorageAgreements.Count > 0 || value.GoodsOwnerCandidates.Count > 0))
            throw new InvalidOperationException("본인 주문 이외의 보관 정보를 확인했습니다. 다시 조회해 주세요.");
        if (RoleKey == "shipper" && (value.Lots.Any(lot => lot.SellerUserId != owner?.OwnerId && lot.OwnerUserId != owner?.OwnerId)
                || value.InboundSourceCandidates.Any(source => source.SellerUserId != owner?.OwnerId)
                || value.ExistingStockCandidates.Any(stock => stock.SellerUserId != owner?.OwnerId && stock.OwnerUserId != owner?.OwnerId)
                || value.ExistingOutboundCandidates.Any(candidate => candidate.SellerUserId != owner?.OwnerId)))
            throw new InvalidOperationException("본인 판매 권한의 재고 범위를 확인하지 못했습니다.");
        foreach (var wave in value.Waves) ValidateWave(wave, wave.WaveId);
    }
    private void ValidateWave(DistributedWaveDto wave, string id)
    {
        if (wave.WaveId != id || CleanId(id) is null || wave.Revision < 1
            || wave.Stops.Select(value => value.StopId).Distinct().Count() != wave.Stops.Count
            || wave.Stops.Select(value => value.Sequence).Distinct().Count() != wave.Stops.Count
            || !wave.Stops.Select(value => value.Sequence).SequenceEqual(wave.Stops.OrderBy(value => value.Sequence).Select(value => value.Sequence))
            || wave.CurrentStopId is { } current && !wave.Stops.Any(value => value.StopId == current))
            throw new InvalidOperationException("배송 묶음의 대상·판본·방문 순서를 확인하지 못했습니다.");
        if (RoleKey == "orderer" && (wave.Orders.Any(order => !order.IsCurrentCustomer) || wave.Reservations.Count > 0
                || wave.DriverId is not null || wave.Stops.Any(stop => stop.Address is not null || stop.Latitude is not null || stop.Longitude is not null)))
            throw new InvalidOperationException("본인 주문의 공개 범위를 확인하지 못했습니다.");
    }

    public Task ExecuteAsync(string action, string suffix, DistributedCommandRequest body,
        string? aggregateId = null, IReadOnlyList<string>? allowedActions = null, bool confirmed = false,
        int? quantity = null, string? reservationId = null)
    {
        if (!confirmed || !Allows(action, allowedActions)) return Task.CompletedTask;
        if (action is "receive" or "pick" or "deliver" or "return" or "import-lot")
            return InvalidInputAsync("완료 사진을 촬영하고 사진 보내기 절차로 완료해 주세요.");
        if ((action is "create-wave" or "close" or "plan") && !CanManageWaves) return Task.CompletedTask;
        if ((action is "pick" or "deliver" or "accept") && RoleKey != "cargo-driver") return Task.CompletedTask;
        if (action == "acknowledge" && RoleKey != "orderer") return Task.CompletedTask;
        if (body.RequestId == Guid.Empty) body.RequestId = Guid.NewGuid();
        var snapshot = (DistributedCommandRequest?)JsonSerializer.Deserialize(JsonSerializer.Serialize(body, body.GetType()), body.GetType())
            ?? throw new InvalidOperationException("입력 내용을 확인하지 못했습니다.");
        pending = new(action, suffix, snapshot, aggregateId, quantity, reservationId);
        Message = ValidationError = null;
        return RunAsync(SendPendingAsync);
    }
    public Task InvalidInputAsync(string message)
    { ValidationError = message; Changed?.Invoke(); return Task.CompletedTask; }
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
    public Task RetryPhotoAsync() => HasPendingPhoto ? RunAsync(UploadAndCompleteAsync) : Task.CompletedTask;
    public Task CancelPhotoWorkAsync()
    {
        if (IsBusy || HasPendingCommand) return Task.CompletedTask;
        Photo.Clear(); photoOperation = null;
        return LoadAsync(SelectedId);
    }
    private async Task UploadAndCompleteAsync(CancellationToken ct)
    {
        var operation = photoOperation ?? throw new InvalidOperationException("확인할 사진 업무가 없습니다.");
        await Photo.UploadAsync(client, ct, () => RequireCurrent(ct)); RequireCurrent(ct);
        var body = Photo.CompletionBody();
        var snapshot = (DistributedCommandRequest)JsonSerializer.Deserialize(JsonSerializer.Serialize(body, body.GetType()), body.GetType())!;
        pending = new(operation.Action == "import-lot" ? operation.Action : operation.Action + "-batch", operation.Suffix, snapshot,
            Photo.Work!.AggregateId, operation.Quantity, null);
        await SendPendingAsync(ct);
    }
    public Task PickAsync(string reservationId, string locationBarcode, string sku, int quantity, string evidence, bool confirmed)
    {
        var line = CurrentPickLines.SingleOrDefault(item => item.ReservationId == reservationId);
        var location = Workspace?.Sites.SelectMany(site => site.Locations).SingleOrDefault(item => item.LocationId == line?.LocationId);
        if (!confirmed || line is null || !CanPick(line)) return Task.CompletedTask;
        if (location is null || location.Barcode != locationBarcode.Trim() || line.Sku != sku.Trim()
            || quantity < 1 || quantity > line.Quantity - line.PickedQuantity - line.ReleasedQuantity || string.IsNullOrWhiteSpace(evidence))
            return InvalidInputAsync("현재 방문지의 적재 위치, 상품, 남은 수량과 확인 근거를 순서대로 확인해 주세요.");
        return ExecuteAsync("pick", $"waves/{분산이행Client.Segment(SelectedWave!.WaveId)}/picks",
            new DistributedPickRequest { ExpectedRevision = SelectedWave.Revision, StopId = CurrentStop!.StopId,
                ReservationId = line.ReservationId, LocationBarcode = locationBarcode.Trim(), OwnerUserId = line.OwnerUserId,
                Sku = line.Sku, Quantity = quantity, EvidenceReference = evidence.Trim() },
            SelectedWave.WaveId, line.AllowedActions, true, quantity, line.ReservationId);
    }
    public Task DeliverAsync(string orderId, string evidence, bool confirmed)
    {
        var order = CurrentDeliveryOrders.SingleOrDefault(item => item.OrderId == orderId);
        if (!confirmed || order is null || !CanDeliver(order)) return Task.CompletedTask;
        if (string.IsNullOrWhiteSpace(evidence)) return InvalidInputAsync("실제 전달을 확인한 근거를 입력해 주세요.");
        return ExecuteAsync("deliver", $"waves/{분산이행Client.Segment(SelectedWave!.WaveId)}/deliveries",
            new DistributedDeliveryRequest { ExpectedRevision = SelectedWave.Revision, StopId = CurrentStop!.StopId,
                OrderId = order.OrderId, EvidenceReference = evidence.Trim() }, SelectedWave.WaveId, SelectedWave.AllowedActions, true);
    }
    public Task AcknowledgeAsync(string orderId, string evidence, bool confirmed)
    {
        var order = SelectedWave?.Orders.SingleOrDefault(item => item.OrderId == orderId);
        if (!confirmed || order is null || !CanAcknowledge(order)) return Task.CompletedTask;
        if (string.IsNullOrWhiteSpace(evidence)) return InvalidInputAsync("본인이 물품을 수령한 확인 근거를 입력해 주세요.");
        return ExecuteAsync("acknowledge", $"waves/{분산이행Client.Segment(SelectedWave!.WaveId)}/acknowledgements",
            new DistributedAcknowledgementRequest { ExpectedRevision = SelectedWave.Revision, OrderId = order.OrderId,
                EvidenceReference = evidence.Trim() }, SelectedWave.WaveId, order.AllowedActions, true);
    }
    public Task RetryPendingAsync() => HasPendingCommand ? RunAsync(SendPendingAsync) : Task.CompletedTask;
    private async Task SendPendingAsync(CancellationToken ct)
    {
        var command = pending ?? throw new InvalidOperationException("확인할 요청이 없습니다.");
        if (command.Acknowledgement is null && command.Sent)
        {
            try
            {
                var saved = await client.ReceiptAsync(command.Body.RequestId, ct); RequireCurrent(ct);
                command.Acknowledgement = new(saved.RequestId, saved.AggregateId, saved.Revision, "Recorded", true);
            }
            catch (RoleWorkspaceAccessException ex) when (ex.StatusCode == 404) { }
            catch (SsalddelApiException ex) when (ex.StatusCode == 404) { }
        }
        if (command.Acknowledgement is null)
        {
            command.Sent = true;
            var acknowledgement = await client.SendAsync(command.Suffix, command.Body, ct); RequireCurrent(ct);
            if (acknowledgement is null || acknowledgement.RequestId != command.Body.RequestId
                || string.IsNullOrWhiteSpace(acknowledgement.AggregateId) || string.IsNullOrWhiteSpace(acknowledgement.StatusCode)
                || command.AggregateId is { } expected && acknowledgement.AggregateId != expected
                || acknowledgement.Revision <= command.Body.ExpectedRevision)
                throw new InvalidOperationException("저장 응답의 요청·대상·판본을 확인하지 못했습니다. 같은 요청을 다시 확인해 주세요.");
            command.Acknowledgement = acknowledgement;
        }
        var receipt = await client.ReceiptAsync(command.Body.RequestId, ct); RequireCurrent(ct);
        var acknowledgementResult = command.Acknowledgement;
        if (receipt.RequestId != command.Body.RequestId || receipt.AggregateId != acknowledgementResult.AggregateId
            || receipt.Revision != acknowledgementResult.Revision || receipt.Kind != ReceiptKind(command.Action)
            || command.Quantity is { } quantity && receipt.Quantity != quantity
            || command.ReservationId is { } reservation && receipt.ReservationId != reservation)
            throw new InvalidOperationException("같은 요청의 저장 근거를 확인하지 못했습니다.");
        await RefreshAsync(ct); RequireCurrent(ct);
        var revision = FindRevision(receipt.AggregateId);
        if (revision is null || revision < receipt.Revision)
            throw new InvalidOperationException("처리 후 같은 업무의 최신 상태를 확인하지 못했습니다.");
        pending = null;
        if (Photo.CompletionRequestId == command.Body.RequestId) { Photo.Clear(); photoOperation = null; }
        Message = command.Action == "retry-order-ledger"
            ? SelectedWave?.Orders.FirstOrDefault(x => command.Suffix.EndsWith($"/orders/{분산이행Client.Segment(x.OrderId)}/ledger", StringComparison.Ordinal))?.OrderLedgerConnectionStatusCode switch
            {
                "Connected" => "주문과 배송 업무의 연결을 확인했습니다.",
                "Blocked" => "배송 업무는 저장되어 있습니다. 원주문 연결의 현재 권한과 조건을 확인해 주세요.",
                _ => "배송 업무는 저장되어 있습니다. 원주문 연결은 아직 확인 중입니다."
            }
            : "저장 근거와 최신 업무 상태를 다시 확인했습니다.";
    }
    public Task RecoverReceiptAsync(Guid requestId)
    {
        if (requestId == Guid.Empty || HasPendingCommand || IsBusy) return Task.CompletedTask;
        return RunAsync(async ct =>
        {
            var receipt = await client.ReceiptAsync(requestId, ct); RequireCurrent(ct);
            if (receipt.RequestId != requestId || receipt.Revision < 1) throw new InvalidOperationException("요청의 저장 근거를 확인하지 못했습니다.");
            await RefreshAsync(ct); RequireCurrent(ct);
            if (FindRevision(receipt.AggregateId) is not { } revision || revision < receipt.Revision)
                throw new InvalidOperationException("같은 요청의 최신 업무 상태를 확인하지 못했습니다.");
            Message = "이전에 보낸 요청의 저장 근거와 최신 상태를 확인했습니다.";
        });
    }
    private long? FindRevision(string id)
        => Workspace?.Sites.FirstOrDefault(value => value.SiteId == id)?.Revision
           ?? Workspace?.InboundPlans.FirstOrDefault(value => value.PlanId == id)?.Revision
           ?? Workspace?.Waves.FirstOrDefault(value => value.WaveId == id)?.Revision
           ?? Workspace?.StorageAgreements.FirstOrDefault(value => value.AgreementId == id)?.Revision;
    private static string ReceiptKind(string action) => action switch
    { "create-site" => "CreateSite", "consent-owner" or "consent-manager" or "revoke-owner" or "revoke-manager" => "Consent",
      "create-location" => "CreateLocation", "import-lot" => "ImportLot", "plan-inbound" => "PlanInbound", "receive" => "Receive",
      "create-wave" => "CreateWave", "add-order" => "AddOrder", "close" => "Close", "plan" => "Plan", "accept" => "Accept",
      "retry-order-ledger" => "LinkOrderLedger",
      "pick" => "Pick", "deliver" => "Deliver", "cancel" => "Cancel", "return" => "Return", "acknowledge" => "Acknowledge",
      "receive-batch" => "ReceiveBatch", "pick-batch" => "PickBatch", "deliver-batch" => "DeliverBatch", "return-batch" => "ReturnBatch",
      "create-agreement" => "CreateAgreement", "change-terms" => "AgreementTerms", "consent" or "revoke" => "AgreementConsent", _ => "" };
    private async Task RunAsync(Func<CancellationToken, Task> operation)
    {
        if (disposed || IsBusy) return;
        var mine = ++generation; var ct = lifetime.Token; IsBusy = true; Error = ValidationError = Message = null; Changed?.Invoke();
        try
        {
            await client.Transport.InitializeAsync(RoleKey, ct);
            if (mine != generation || disposed || ct.IsCancellationRequested) return;
            var identity = client.Transport.Identity(RoleKey);
            if (owner is not null && owner != identity)
            { Clear(); lifetime.Cancel(); lifetime.Dispose(); lifetime = new(); owner = identity; RequiresLogin = !identity.IsAuthenticated; return; }
            owner = identity;
            if (!identity.IsAuthenticated || string.IsNullOrWhiteSpace(identity.OwnerId))
            { Clear(); RequiresLogin = true; return; }
            RequiresLogin = false; await operation(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested || mine != generation || disposed) { }
        catch (Exception ex) when (!disposed && mine == generation)
        {
            if (client.Transport.Identity(RoleKey) != owner) { AuthenticationChanged(); return; }
            var status = ex switch { RoleWorkspaceAccessException role => role.StatusCode, SsalddelApiException api => api.StatusCode, _ => 0 };
            if (status is 401 or 403)
            { Clear(); RequiresLogin = status == 401; Error = status == 401 ? "로그인한 뒤 업무를 다시 확인해 주세요." : "이 업무를 처리할 권한이 없습니다."; }
            else
            {
                if (status is >= 400 and < 500 && status != 408 && pending?.Acknowledgement is null) pending = null;
                Workspace = null; SelectedWave = null; IsLoaded = false;
                Error = status == 409 ? "업무 상태가 변경되었습니다. 최신 내용을 조회한 뒤 다시 확인해 주세요."
                    : HasPendingCommand ? "요청 결과를 아직 확인하지 못했습니다. 같은 요청의 결과를 확인해 주세요."
                    : "업무를 확인하지 못했습니다. 연결과 입력을 확인한 뒤 다시 시도해 주세요.";
            }
        }
        finally { if (!disposed && mine == generation) { IsBusy = false; Changed?.Invoke(); } }
    }
    private void RequireCurrent(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (disposed || owner != client.Transport.Identity(RoleKey)) throw new OperationCanceledException("로그인 상태가 변경되었습니다.", ct);
    }
    public void VerifyAuthentication()
    { if (owner is not null && owner != client.Transport.Identity(RoleKey)) AuthenticationChanged(); }
    private void AuthenticationChanged()
    {
        if (disposed) return;
        ++generation; lifetime.Cancel(); lifetime.Dispose(); lifetime = new();
        Clear(); owner = null; RequiresLogin = !client.Transport.Identity(RoleKey).IsAuthenticated; IsBusy = false; Changed?.Invoke();
    }
    private void Clear()
    { ClearRootOrderCandidates(); Workspace = null; SelectedWave = null; SelectedId = null; pending = null; Photo.Clear(); photoOperation = null; IsLoaded = false; Error = Message = ValidationError = null; }
    private static string? CleanId(string? id) => id is null ? null : RoleWorkspaceNavigation.StableId(id)
        ?? throw new InvalidOperationException("업무 대상의 번호를 확인해 주세요.");
    public void Dispose()
    { if (disposed) return; disposed = true; ++generation; lifetime.Cancel(); lifetime.Dispose(); client.Transport.Changed -= AuthenticationChanged; Clear(); }
    private sealed record PendingCommand(string Action, string Suffix, DistributedCommandRequest Body,
        string? AggregateId, int? Quantity, string? ReservationId)
    { public DistributedCommandResponse? Acknowledgement { get; set; } public bool Sent { get; set; } }
    private sealed record PhotoOperation(string Action, string Suffix, int? Quantity);
}
