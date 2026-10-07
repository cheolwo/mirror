using Ssalddel.Contracts.Common.Inventory;
using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Cargo;

/// <summary>기존 검수·출고 전문 화면을 현재 창고 역할 세션에 결속합니다.</summary>
public sealed class WarehouseRoleJsonApiBridge : ISsalddelJsonApiClient, IDisposable
{
    private const string Root = "api/v1/warehouse-operations/";
    private readonly IRoleWorkspaceApi _api;
    private readonly IRoleWorkspaceAccess _access;
    private readonly RoleWorkspaceIdentity _owner;
    private readonly Action<int> _denied;
    private readonly CancellationTokenSource _session = new();
    private readonly string _kind;
    private readonly long _id;
    private 출고예정검토상세응답? _viewedPlan;
    private bool _disposed;

    public WarehouseRoleJsonApiBridge(IRoleWorkspaceApi api, IRoleWorkspaceAccess access, string kind, long id, Action<int> denied)
    {
        _api = api; _access = access; _kind = kind; _id = id; _denied = denied;
        _owner = access.GetIdentity(RoleWorkspaceCatalog.Warehouse);
    }

    public Task<T?> GetAsync<T>(string path, string operationName, bool allowNotFound = true, CancellationToken cancellationToken = default)
        => RunAsync<T>(async ct =>
        {
            RequirePath(path, write: false);
            var result = await _api.GetAsync<T>(RoleWorkspaceCatalog.Warehouse, path, ct);
            RequireCurrent(ct);
            if (result is 입고검수대상상세응답 inspection && inspection.InboundItemId != _id
                || result is 출고예정검토상세응답 plan && plan.OutboundPlanId != _id)
                throw new InvalidOperationException("선택한 창고 원장과 조회 결과가 일치하지 않습니다.");
            if (result is 출고예정검토상세응답 viewed) _viewedPlan = viewed;
            return result;
        }, cancellationToken);

    public Task<T?> SendAsync<T>(HttpMethod method, string path, string operationName, bool allowNotFound = false, CancellationToken cancellationToken = default)
        => method == HttpMethod.Get ? GetAsync<T>(path, operationName, allowNotFound, cancellationToken)
            : throw new NotSupportedException("이 업무 화면은 입력이 없는 변경을 지원하지 않습니다.");

    public Task<T?> SendAsync<TRequest, T>(HttpMethod method, string path, TRequest request, string operationName,
        bool allowNotFound = false, CancellationToken cancellationToken = default)
        => RunAsync<T>(async ct =>
        {
            if (method != HttpMethod.Post) throw new NotSupportedException("이 업무 화면은 명시한 저장만 지원합니다.");
            RequirePath(path, write: true);
            object? result;
            if (request is 입고검수요청 inspection)
            {
                var current = await ReadInspectionAsync(ct);
                if (!current.CanInspect) throw new InvalidOperationException("검수 가능 상태를 다시 확인해 주세요.");
                result = await _api.PostAsync<T>(RoleWorkspaceCatalog.Warehouse, path, request, ct);
                RequireCurrent(ct);
                if (result is not 창고작업결과응답 saved || saved.입고상품Id != _id || saved.창고Id != current.WarehouseId
                    || saved.처리일시 <= DateTime.MinValue)
                    throw new InvalidOperationException("저장한 검수 원장을 확인하지 못했습니다.");
                var fresh = await ReadInspectionAsync(ct);
                if (fresh.CanInspect || fresh.InspectedAtUtc is not { } stamp || stamp <= DateTime.MinValue
                    || fresh.InboundId != current.InboundId || fresh.WarehouseId != current.WarehouseId
                    || fresh.ReceivedQuantity != inspection.검수수량 || fresh.DefectiveQuantity != inspection.불량수량)
                    throw new InvalidOperationException("검수 이후의 같은 재고 상태를 확인하지 못했습니다.");
            }
            else if (request is 재고운송의뢰생성요청 draft)
            {
                var current = await ReadPlanAsync(ct);
                if (!current.CanStartTransportRequestDraft || !string.IsNullOrWhiteSpace(current.TransportRequestId)
                    || draft.출고예정Id != _id || current.InboundItemId != draft.입고상품Id || current.Quantity != draft.요청수량)
                    throw new InvalidOperationException("검토한 출고예정과 운송의뢰 입력을 다시 확인해 주세요.");
                result = await _api.PostAsync<T>(RoleWorkspaceCatalog.Warehouse, path, request, ct);
                RequireCurrent(ct);
                if (result is not 화주운송의뢰응답 created || string.IsNullOrWhiteSpace(created.의뢰Id))
                    throw new InvalidOperationException("운송의뢰 저장 결과를 확인하지 못했습니다.");
                var fresh = await ReadPlanAsync(ct);
                if (fresh.InboundItemId != current.InboundItemId || fresh.TransportRequestId != created.의뢰Id)
                    throw new InvalidOperationException("출고예정에 연결된 운송의뢰를 확인하지 못했습니다.");
            }
            else if (request is 출고운송인계완료요청 handoff)
            {
                var viewed = _viewedPlan;
                var current = await ReadPlanAsync(ct);
                if (viewed is null || !current.CanCompleteHandoff || !handoff.DriverIdentityConfirmed
                    || !handoff.VehicleConfirmed || !handoff.CargoReleasedConfirmed
                    || current.AssignedDriverId != viewed.AssignedDriverId
                    || current.AssignedDriverVehicle != viewed.AssignedDriverVehicle
                    || current.TransportRequestId != viewed.TransportRequestId)
                    throw new InvalidOperationException("기사·차량과 인계 가능 상태를 다시 확인해 주세요.");
                result = await _api.PostAsync<T>(RoleWorkspaceCatalog.Warehouse, path, request, ct);
                RequireCurrent(ct);
                if (result is not 출고운송인계완료응답 saved || saved.OutboundPlanId != _id
                    || saved.TransportRequestId != current.TransportRequestId || saved.HandoffCompletedAtUtc <= DateTime.MinValue)
                    throw new InvalidOperationException("기사 인계 결과를 확인하지 못했습니다.");
                var fresh = await ReadPlanAsync(ct);
                if (fresh.HandoffCompletedAtUtc is not { } stamp || stamp <= DateTime.MinValue || fresh.CanCompleteHandoff
                    || fresh.TransportRequestId != current.TransportRequestId || fresh.InboundItemId != current.InboundItemId)
                    throw new InvalidOperationException("인계 이후의 출고 상태를 확인하지 못했습니다.");
            }
            else throw new NotSupportedException("지원하지 않는 창고 입력입니다.");
            RequireCurrent(ct);
            return (T?)result;
        }, cancellationToken);

    private async Task<입고검수대상상세응답> ReadInspectionAsync(CancellationToken ct)
    {
        var result = await _api.GetAsync<입고검수대상상세응답>(RoleWorkspaceCatalog.Warehouse,
            Root + "inventory/" + CargoWorkspaceRoutes.Number(_id) + "/inspection-target", ct);
        RequireCurrent(ct);
        if (result.InboundItemId != _id) throw new InvalidOperationException("검수 재고가 일치하지 않습니다.");
        return result;
    }
    private async Task<출고예정검토상세응답> ReadPlanAsync(CancellationToken ct)
    {
        var result = await _api.GetAsync<출고예정검토상세응답>(RoleWorkspaceCatalog.Warehouse,
            Root + "outbound-plan-reviews/" + CargoWorkspaceRoutes.Number(_id), ct);
        RequireCurrent(ct);
        if (result.OutboundPlanId != _id) throw new InvalidOperationException("출고예정이 일치하지 않습니다.");
        return result;
    }
    private void RequirePath(string path, bool write)
    {
        var id = CargoWorkspaceRoutes.Number(_id);
        var allowed = _kind == "inspection"
            ? path == Root + "inventory/" + id + (write ? "/inspect" : "/inspection-target")
            : write ? _kind == "transport-draft" && (path == Root + "inventory/reconsignment" || path == Root + "outbound-plan-reviews/" + id + "/handoff-complete")
            : path == Root + "outbound-plan-reviews/" + id || _kind == "outbound" && path.StartsWith(Root + "outbound-plan-reviews?", StringComparison.Ordinal);
        if (!allowed) throw new InvalidOperationException("선택한 창고 업무의 조회 범위를 벗어났습니다.");
    }
    private async Task<T?> RunAsync<T>(Func<CancellationToken, Task<T?>> action, CancellationToken ct)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _session.Token);
        RequireCurrent(linked.Token);
        try { return await action(linked.Token); }
        catch (RoleWorkspaceAccessException ex) when (ex.StatusCode is 401 or 403)
        { _denied(ex.StatusCode); throw new InvalidOperationException("현재 계정으로 이 업무를 확인할 수 없습니다."); }
    }
    private void RequireCurrent(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (_disposed || !_owner.IsAuthenticated || _access.GetIdentity(RoleWorkspaceCatalog.Warehouse) != _owner)
            throw new OperationCanceledException("창고 업무 계정이 변경되었습니다.", ct);
    }
    public Task SendAsync(HttpMethod method, string path, string operationName, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
    public Task SendAsync<TRequest>(HttpMethod method, string path, TRequest request, string operationName, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
    public void Dispose() { if (_disposed) return; _disposed = true; _session.Cancel(); _session.Dispose(); _viewedPlan = null; }
}
