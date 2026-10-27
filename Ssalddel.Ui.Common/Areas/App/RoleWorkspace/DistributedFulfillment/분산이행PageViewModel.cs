using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Completion;

namespace Ssalddel.Ui.Common.Areas.App.RoleWorkspace.DistributedFulfillment;

/// <summary>한 역할의 조회·명시 처리·같은 요청 재확인을 소유합니다. 권한과 순회 순서는 서버가 결정합니다.</summary>
public sealed partial class 분산이행PageViewModel : IDisposable
{
    private readonly 분산이행Client client;
    private CancellationTokenSource lifetime = new();
    private RoleWorkspaceIdentity? owner;
    private long generation;
    private bool disposed;
    private 분산명령영수증Session? pending;
    public 분산이행PageViewModel(분산이행Client client, string screen, I업무사진촬영Adapter? capture = null, IWorkflowRecoveryStore? recoveryStore = null)
    {
        if (!분산이행Navigation.AllowsScreen(client.RoleKey, screen))
            throw new ArgumentException("활동 역할의 업무 화면을 확인해 주세요.");
        this.client = client; Screen = screen; this.recoveryStore = recoveryStore ?? new VolatileWorkflowRecoveryStore();
        Photo = new(capture ?? new 미지원업무사진촬영Adapter()); client.Transport.Changed += AuthenticationChanged;
    }
    public event Action? Changed;
    public WorkflowCompletionFeedback CompletionFeedback { get; } = new();
    public string RoleKey => client.RoleKey;
    public string Screen { get; }
    public bool IsBusy { get; private set; }
    public bool RequiresLogin { get; private set; }
    public bool IsLoaded { get; private set; }
    public string? Error { get; private set; }
    public string? Message { get; private set; }
    public string? ValidationError { get; private set; }
    public DistributedWorkspaceDto? Workspace { get; private set; }
    public DistributedWaveDto? SelectedWave { get; private set; }
    public string? SelectedId { get; private set; }
    public DistributedSiteDto? SelectedSite => Workspace?.Sites.SingleOrDefault(value => value.SiteId == SelectedId);
    public DistributedInboundDto? SelectedInbound => Workspace?.InboundPlans.SingleOrDefault(value => value.PlanId == SelectedId);
    public DistributedStorageAgreementDto? SelectedAgreement => Workspace?.StorageAgreements.SingleOrDefault(value => value.AgreementId == SelectedId);
    public bool HasPendingCommand => pending is not null;
    public Guid? RequestId => pending?.Body.RequestId ?? expiredRecoveryId;
    public bool CanEdit => IsLoaded && !IsBusy && !RequiresLogin && !HasPendingCommand && !RecoveryExpired && !Photo.HasUnresolvedUpload && !Photo.Restored && Error is null;
    public string? CurrentUserId => owner?.OwnerId;
    private 분산역할명령Coordinator Commands => new(RoleKey, CanEdit, IsLoaded, Workspace, SelectedWave);
    public DistributedStopDto? CurrentStop => Commands.CurrentStop;
    public IReadOnlyList<DistributedReservationDto> CurrentPickLines => Commands.CurrentPickLines;
    public IReadOnlyList<DistributedOrderDto> CurrentDeliveryOrders => Commands.CurrentDeliveryOrders;
    public bool Allows(string action, IReadOnlyList<string>? actions = null)
        => Commands.Allows(action, actions);
    public bool CanPick(DistributedReservationDto line) => Commands.CanPick(line);
    public bool CanDeliver(DistributedOrderDto order) => Commands.CanDeliver(order);
    public bool CanManageWaves => Commands.CanManageWaves;

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

    public Task LoadAsync(string? selectedId = null)
    {
        if (HasPendingCommand || HasPendingPhoto || IsBusy) return Task.CompletedTask;
        if (!ValidSelection(selectedId ?? SelectedId)) return Task.CompletedTask;
        if (selectedId is not null && selectedId != SelectedId) CompletionFeedback.Dismiss();
        SelectedId = CleanId(selectedId ?? SelectedId); Message = null;
        return RunAsync(async ct => { await RestoreRecoveryAsync(ct); RequireCurrent(ct); await RefreshAsync(ct); });
    }
    public Task ChooseAsync(string id)
    {
        if (HasPendingCommand || HasPendingPhoto || IsBusy) return Task.CompletedTask;
        if (!ValidSelection(id)) return Task.CompletedTask;
        CompletionFeedback.Dismiss();
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
        if (action is "receive" or "pick" or "deliver" or "return" or "import-lot" or "handover-stock-release")
            return InvalidInputAsync("완료 사진을 촬영하고 사진 보내기 절차로 완료해 주세요.");
        if (!Commands.CanExecute(action)) return Task.CompletedTask;
        if (body.RequestId == Guid.Empty) body.RequestId = Guid.NewGuid();
        pending = 분산명령영수증Session.Freeze(action, suffix, body, aggregateId, quantity, reservationId);
        Message = ValidationError = null;
        return RunAsync(SendPendingAsync);
    }
    public Task InvalidInputAsync(string message)
    { ValidationError = message; Changed?.Invoke(); return Task.CompletedTask; }
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
    public Task RetryPendingAsync() => HasPendingCommand ? RunAsync(SendPendingAsync) : Task.CompletedTask;
    private async Task SendPendingAsync(CancellationToken ct)
    {
        var command = pending ?? throw new InvalidOperationException("확인할 요청이 없습니다.");
        CompletionFeedback.Dismiss();
        if (command.Acknowledgement is null && command.Sent)
        {
            try
            {
                var saved = await client.ReceiptAsync(command.Body.RequestId, ct); RequireCurrent(ct);
                command.RecoverAcknowledgement(saved);
            }
            catch (RoleWorkspaceAccessException ex) when (ex.StatusCode == 404) { }
            catch (SsalddelApiException ex) when (ex.StatusCode == 404) { }
        }
        if (command.Acknowledgement is null)
        {
            command.MarkSent();
            await PersistRecoveryAsync(ct); RequireCurrent(ct);
            var acknowledgement = await client.SendAsync(command.Suffix, command.Body, ct); RequireCurrent(ct);
            command.AcceptAcknowledgement(acknowledgement);
            await PersistRecoveryAsync(ct); RequireCurrent(ct);
        }
        var receipt = await client.ReceiptAsync(command.Body.RequestId, ct); RequireCurrent(ct);
        command.ValidateReceipt(receipt);
        await RefreshAsync(ct); RequireCurrent(ct);
        var revision = FindRevision(receipt.AggregateId);
        if (revision is null || revision < receipt.Revision)
            throw new InvalidOperationException("처리 후 같은 업무의 최신 상태를 확인하지 못했습니다.");
        await recoveryStore.RemoveAsync(owner!.OwnerId!, RecoveryScope, command.Body.RequestId, ct); RequireCurrent(ct);
        pending = null; recoveryCreatedAtUtc = null;
        if (Photo.CompletionRequestId == command.Body.RequestId) { Photo.Clear(); photoOperation = null; }
        RecordCompletion(command.Action, receipt);
        Message = command.Action == "retry-order-ledger"
            ? SelectedWave?.Orders.FirstOrDefault(x => command.Suffix.EndsWith($"/orders/{분산이행Client.Segment(x.OrderId)}/ledger", StringComparison.Ordinal))?.OrderLedgerConnectionStatusCode switch
            {
                "Connected" => "주문과 배송 업무의 연결을 확인했습니다.",
                "Blocked" => "배송 업무는 저장되어 있습니다. 원주문 연결의 현재 권한과 조건을 확인해 주세요.",
                _ => "배송 업무는 저장되어 있습니다. 원주문 연결은 아직 확인 중입니다."
            }
            : "저장 근거와 최신 업무 상태를 다시 확인했습니다.";
    }
    private void RecordCompletion(string action, DistributedReceiptDto receipt)
    {
        // 정확한 요청 영수증과 같은 업무의 정본 판본을 확인한 뒤에만 표시합니다.
        if (RoleKey != "cargo-driver" || action is not ("pick" or "pick-batch" or "deliver" or "deliver-batch")) return;
        var delivered = action is "deliver" or "deliver-batch";
        CompletionFeedback.Record(new($"distributed:{receipt.RequestId:N}:{receipt.Revision}",
            delivered ? "전달을 완료했습니다" : "피킹을 완료했습니다",
            delivered ? "연습 환경의 이번 전달 기록을 확인했습니다." : "연습 환경에서 선택한 물품과 수량의 피킹 기록을 확인했습니다.",
            delivered ? "남은 업무를 확인해 주세요. 수령 확인·정산·입금은 별도입니다." : "남은 피킹과 다음 방문지를 확인해 주세요.",
            delivered, delivered ? WorkflowCompletionVisuals.CargoIconPath : WorkflowCompletionVisuals.PickingIconPath,
            delivered ? "#DC2626" : "#0F766E"));
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
           ?? Workspace?.StockReleases.FirstOrDefault(value => value.SiteId == id)?.SiteRevision
           ?? Workspace?.StockReleaseCandidates.FirstOrDefault(value => value.SiteId == id)?.SiteRevision
           ?? Workspace?.InboundPlans.FirstOrDefault(value => value.PlanId == id)?.Revision
           ?? Workspace?.Waves.FirstOrDefault(value => value.WaveId == id)?.Revision
           ?? Workspace?.StorageAgreements.FirstOrDefault(value => value.AgreementId == id)?.Revision;
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
                if (status is >= 400 and < 500 && status != 408 && pending is { Acknowledgement: null } rejected)
                {
                    try
                    {
                        await recoveryStore.RemoveAsync(owner!.OwnerId!, RecoveryScope, rejected.Body.RequestId, ct);
                        RequireCurrent(ct); pending = null; recoveryCreatedAtUtc = null;
                    }
                    catch (Exception) { /* 삭제 실패·계정 전환에서는 원 요청을 보존하고 새 요청을 만들지 않습니다. */ }
                    if (disposed || mine != generation || ct.IsCancellationRequested) return;
                    if (client.Transport.Identity(RoleKey) != owner) { AuthenticationChanged(); return; }
                }
                Workspace = null; SelectedWave = null; IsLoaded = false;
                var code = ex switch { RoleWorkspaceAccessException role => role.ErrorCode, SsalddelApiException api => api.ErrorCode, _ => null };
                Error = 분산이행표시.InboundReason(code) ?? (status == 409 ? "업무 상태가 변경되었습니다. 최신 내용을 조회한 뒤 다시 확인해 주세요."
                    : HasPendingCommand ? "요청 결과를 아직 확인하지 못했습니다. 같은 요청의 결과를 확인해 주세요."
                    : "업무를 확인하지 못했습니다. 연결과 입력을 확인한 뒤 다시 시도해 주세요.");
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
    { CompletionFeedback.Clear(); ClearRootOrderCandidates(); Workspace = null; SelectedWave = null; SelectedId = null; pending = null; Photo.Clear(); photoOperation = null; IsLoaded = false; Error = Message = ValidationError = null;
      recoveryLoaded = false; recoveryCreatedAtUtc = null; expiredRecoveryId = null; }
    private static string? CleanId(string? id) => id is null ? null : RoleWorkspaceNavigation.StableId(id)
        ?? throw new InvalidOperationException("업무 대상의 번호를 확인해 주세요.");
    public void Dispose()
    { if (disposed) return; disposed = true; ++generation; lifetime.Cancel(); lifetime.Dispose(); client.Transport.Changed -= AuthenticationChanged; Clear(); }
}
