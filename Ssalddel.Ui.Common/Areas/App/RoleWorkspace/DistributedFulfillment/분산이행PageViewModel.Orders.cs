using System.Text.Json;
using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Ui.Common.Areas.App.RoleWorkspace.DistributedFulfillment;

public sealed partial class 분산이행PageViewModel
{
    private DistributedRootOrderCandidatesDto? rootOrderCandidates;
    private long rootOrderGeneration;
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

    public Task AcknowledgeAsync(string orderId, string evidence, bool confirmed)
    {
        var order = SelectedWave?.Orders.SingleOrDefault(item => item.OrderId == orderId);
        if (!confirmed || order is null || !CanAcknowledge(order)) return Task.CompletedTask;
        if (string.IsNullOrWhiteSpace(evidence)) return InvalidInputAsync("본인이 물품을 수령한 확인 근거를 입력해 주세요.");
        return ExecuteAsync("acknowledge", $"waves/{분산이행Client.Segment(SelectedWave!.WaveId)}/acknowledgements",
            new DistributedAcknowledgementRequest { ExpectedRevision = SelectedWave.Revision, OrderId = order.OrderId,
                EvidenceReference = evidence.Trim() }, SelectedWave.WaveId, order.AllowedActions, true);
    }
}
