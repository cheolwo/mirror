using Ssalddel.Contracts.Shipper.Payment;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Ui.Common.Areas.App.ViewModels;

/// <summary>같은 의뢰의 선결제 확인과 결과 조회만 소유합니다. 미확정 결제를 자동 재전송하지 않습니다.</summary>
public sealed class ShipperPrepaymentViewModel : IDisposable
{
    private const string Role = RoleWorkspaceCatalog.Shipper;
    private readonly IRoleWorkspaceApi api;
    private readonly IRoleWorkspaceAccess access;
    private readonly IWorkflowRecoveryStore recovery;
    private readonly IWorkflowRecoveryContext context;
    private WorkflowRecoveryRecord? pending;
    private WorkflowRecoverySnapshot? owner;
    private long generation;
    private bool disposed;
    public ShipperPrepaymentViewModel(IRoleWorkspaceApi api, IRoleWorkspaceAccess access,
        IWorkflowRecoveryStore recovery, IWorkflowRecoveryContext context)
    {
        this.api = api; this.access = access; this.recovery = recovery; this.context = context;
        access.Changed += SessionChanged;
    }
    public event Action? Changed;
    public 화주선결제조회응답? State { get; private set; }
    public string RequestId { get; private set; } = string.Empty;
    public string? Message { get; private set; }
    public Api작업오류? Error { get; private set; }
    public bool IsBusy { get; private set; }
    public bool NeedsResultCheck => pending is not null || State?.ResultCheckRequired == true;
    public bool CanPay => !IsBusy && !NeedsResultCheck && State is { Enabled: true, CanPay: true, SimulationAvailable: true, Amount: > 0 };
    public string? PendingRequestId => pending?.Target;
    private static string Scope(WorkflowRecoverySnapshot identity) => identity.Scope("distributed.shipper.prepayment");

    public async Task LoadAsync(string requestId, CancellationToken ct = default)
    {
        if (disposed) return;
        var version = ++generation;
        RequestId = requestId.Trim(); State = null; Message = null; Error = null; IsBusy = true; Changed?.Invoke();
        try
        {
            await access.EnsureInitializedAsync(Role, ct);
            if (disposed || version != generation) return;
            owner = context.Capture();
            var identity = owner;
            var id = RequestId;
            var record = await recovery.LoadAsync(identity.Owner, Scope(identity), ct);
            if (!Current(version, identity)) return;
            if (record is null && pending is not null && pending.Owner == identity.Owner && pending.Scope == Scope(identity))
                throw new InvalidDataException("이전 결제 확인 기록을 찾지 못했습니다.");
            if (record is not null && (record.Owner != identity.Owner || record.Scope != Scope(identity)
                || record.RequestId == Guid.Empty || string.IsNullOrWhiteSpace(record.Target) || record.PayloadJson is not null))
                throw new InvalidDataException("이전 결제 확인 기록이 일치하지 않습니다.");
            pending = record;
            var result = await ReadAsync(id, ct);
            if (!Current(version, identity)) return;
            State = result;
            if (pending?.Target == id && result.PaymentConfirmed)
            {
                await recovery.RemoveAsync(identity.Owner, Scope(identity), pending.RequestId, ct);
                if (!Current(version, identity)) return;
                pending = null;
            }
            Message = NeedsResultCheck ? "결제 결과 확인 중 · 다시 결제하지 마세요."
                : result.PaymentConfirmed ? result.SimulationPayment ? "시험 결제 확인 · 실제 결제 없음" : "결제 확인"
                : !result.Enabled ? "결제 연결 준비 중 · 접수 준비가 저장되었습니다."
                : !result.SimulationAvailable ? "온라인 결제 연결 준비 중 · 접수 준비가 저장되었습니다."
                : result.Amount <= 0 ? "결제 금액 확인 필요 · 결과를 다시 확인해 주세요."
                : !result.CanPay ? "현재 의뢰는 결제할 수 없습니다. 상태를 다시 확인해 주세요."
                : "결제 후 배차 접수";
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            if (!disposed && version == generation)
            {
                Error = Api작업오류.변환(ex);
                Message = "결제 상태를 확인하지 못했습니다. 다시 확인해 주세요.";
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        finally
        {
            // 인증 만료는 Changed 이벤트 없이 발생할 수 있습니다. 보호 기록은 남기고 화면 잠김만 정리합니다.
            SessionChanged();
            if (!disposed && version == generation) { IsBusy = false; Changed?.Invoke(); }
        }
    }

    public async Task ConfirmSimulationAsync(CancellationToken ct = default)
    {
        if (!CanPay || owner is null || State is null) return;
        var identity = owner; var version = generation; var id = RequestId; var amount = State.Amount;
        if (!context.IsCurrent(identity)) return;
        IsBusy = true; Message = "결제 확인 중"; Error = null; Changed?.Invoke();
        try
        {
            var old = await recovery.LoadAsync(identity.Owner, Scope(identity), ct);
            if (!Current(version, identity)) return;
            if (old is not null) { pending = old; Message = "이전 결제 결과부터 확인해 주세요."; return; }
            var record = new WorkflowRecoveryRecord(identity.Owner, Scope(identity), Guid.NewGuid(), null, DateTime.UtcNow, id);
            await recovery.SaveAsync(record, ct);
            if (!Current(version, identity)) return;
            pending = record;
            var stored = await recovery.LoadAsync(identity.Owner, Scope(identity), ct);
            if (!Current(version, identity)) return;
            if (stored?.RequestId != record.RequestId || stored.Target != id)
                throw new InvalidDataException("결제 요청 보호 기록을 확인하지 못했습니다.");
            try
            {
                await api.PostAsync<페이크결제승인응답>(Role, "api/v1/payments/fake/confirm", new 페이크결제승인요청
                {
                    의뢰Id = id, Amount = amount, 결제수단 = "카드", IdempotencyKey = "shipper-prepay-" + record.RequestId.ToString("N")
                }, ct);
            }
            catch (RoleWorkspaceAccessException ex) when (ex.StatusCode is 400 or 403 or 404 or 409 or 422)
            {
                // POST 자체가 명시적으로 거절된 경우만 정리합니다. 승인 뒤 GET 실패는 이 경로에 들어오지 않습니다.
                if (!Current(version, identity)) return;
                await recovery.RemoveAsync(identity.Owner, Scope(identity), record.RequestId, ct);
                if (!Current(version, identity)) return;
                pending = null; State = null; Message = "결제 조건이 변경되었습니다. 상태를 다시 확인해 주세요.";
                return;
            }
            if (!Current(version, identity)) return;
            var result = await ReadAsync(id, ct);
            if (!Current(version, identity)) return;
            State = result;
            if (result.PaymentConfirmed)
            {
                await recovery.RemoveAsync(identity.Owner, Scope(identity), record.RequestId, ct);
                if (!Current(version, identity)) return;
                pending = null; Message = "시험 결제 확인 · 실제 결제 없음";
            }
            else Message = "결제 결과 확인 중 · 다시 결제하지 마세요.";
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            if (Current(version, identity))
            {
                Error = Api작업오류.변환(ex);
                Message = pending is null ? "결제를 시작하지 못했습니다. 상태를 다시 확인해 주세요."
                    : "결제 결과 확인 중 · 다시 결제하지 마세요.";
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        finally
        {
            SessionChanged();
            if (Current(version, identity)) { IsBusy = false; Changed?.Invoke(); }
        }
    }

    private async Task<화주선결제조회응답> ReadAsync(string id, CancellationToken ct)
    {
        var result = await api.GetAsync<화주선결제조회응답>(Role,
            "api/v1/payments/requests/" + Uri.EscapeDataString(id) + "/prepayment", ct);
        if (result.의뢰Id != id || result.Amount < 0) throw new InvalidDataException("다른 의뢰의 결제 응답입니다.");
        return result;
    }
    private bool Current(long version, WorkflowRecoverySnapshot identity)
        => !disposed && version == generation && context.IsCurrent(identity);
    private void SessionChanged()
    {
        if (owner is not null && !context.IsCurrent(owner))
        {
            ++generation; owner = null; State = null; pending = null; IsBusy = false; Error = null;
            Message = "로그인 정보를 확인한 뒤 결제 상태를 다시 확인해 주세요."; Changed?.Invoke();
        }
    }
    public void Dispose() { disposed = true; ++generation; access.Changed -= SessionChanged; }
}
