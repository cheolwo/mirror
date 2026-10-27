using System.Security.Cryptography;
using System.Text;
using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace Ssalddel.WebApp.ViewModels;

public sealed partial class ShipperRequestDetailPageViewModel
{
    private readonly IWorkflowRecoveryStore _receiptRecovery;
    private readonly IWorkflowRecoveryContext? _receiptContext;
    private bool _receiptMode;
    private bool _receiptProtectionReady;
    private long _receiptRevision;
    private CancellationTokenSource? _receiptOperation;
    private WorkflowRecoveryRecord? _pendingReceipt;
    private string? _pendingReceiptNumber;

    public event Action? StateChanged;
    public string ReceiptNumber { get; private set; } = string.Empty;
    public string ReceiptMemo { get; private set; } = string.Empty;
    public bool ReceiptAcceptanceConfirmed { get; private set; }
    public bool ReceiptSubmitting { get; private set; }
    public bool ReceiptChecking { get; private set; }
    public bool ReceiptNeedsResultCheck => _pendingReceipt is not null;
    public bool CanPrepareReceipt => State.Request is { ReceiptRegisteredAt: null } item
        && string.IsNullOrWhiteSpace(item.ReceiptNumber) && item.TransportStatus == "인수완료"
        && item.SettlementStatus is "후불승인완료" or "인수증대기" or "인수증등록완료"
            or "청구대기" or "입금대기" or "입금확인완료" or "정산완료";
    public bool ReceiptInputLocked => _disposed || !_receiptMode || !_receiptProtectionReady
        || !_authSession.IsLoggedIn || State.IsWorkflowEnabled != true || State.IsBusy
        || ReceiptSubmitting || ReceiptChecking || ReceiptNeedsResultCheck || !CanPrepareReceipt;
    public bool CanRegisterReceipt => !ReceiptInputLocked && ReceiptAcceptanceConfirmed
        && !string.IsNullOrWhiteSpace(ReceiptNumber);
    public string ReceiptReadinessMessage => ReceiptNeedsResultCheck
        ? "등록 결과가 미확인입니다. 같은 의뢰의 결과를 확인해 주세요. 다시 전송하지 않습니다."
        : State.Request is null ? "의뢰를 다시 조회해 주세요."
        : State.Request.ReceiptRegisteredAt is not null || !string.IsNullOrWhiteSpace(State.Request.ReceiptNumber)
            ? "등록된 인수증을 확인해 주세요. 입금·정산 상태는 별도로 확인합니다."
        : State.Request.TransportStatus != "인수완료" ? "기사의 운송 완료를 확인한 뒤 인수증을 등록할 수 있습니다."
        : !CanPrepareReceipt ? "현재 정산 조건에서는 인수증을 등록할 수 없습니다. 정산 상태를 확인해 주세요."
        : !_receiptProtectionReady ? "등록 요청 보호 상태를 확인한 뒤 진행해 주세요."
        : "수령·검수한 내용을 확인하고 인수증 번호를 등록해 주세요. 입금·정산 완료와는 별개입니다.";

    public async Task InitializeReceiptAsync(string? requestId, CancellationToken cancellationToken = default)
    {
        if (_disposed || ((ReceiptSubmitting || ReceiptChecking)
            && string.Equals(State.LookupRequestId, NormalizeRequestId(requestId), StringComparison.Ordinal))) return;
        var id = NormalizeRequestId(requestId);
        await LoadAsync(requestId, cancellationToken: cancellationToken, receiptContext: true);
        if (_disposed || !_receiptMode || id is null || State.Request?.RequestId != id
            || State.LookupRequestId != id || !_authSession.IsLoggedIn || State.IsWorkflowEnabled != true) return;
        var revision = _receiptRevision;
        var session = CurrentSession();
        try
        {
            var identity = _receiptContext?.Capture()
                ?? throw new InvalidOperationException("등록 요청 보호 상태를 확인하지 못했습니다.");
            var scope = ReceiptScope(identity, id);
            _receiptProtectionReady = false;
            var record = await _receiptRecovery.LoadAsync(identity.Owner, scope, cancellationToken);
            if (!ReceiptCurrent(id, revision, session, identity)) return;
            ValidateReceiptRecord(record, identity, scope, id);
            if (record is null && _pendingReceipt is not null)
                throw new InvalidDataException("이전 등록 요청의 확인 기록을 찾지 못했습니다.");
            _pendingReceipt = record;
            _receiptProtectionReady = true;
            if (record is not null)
                await ReconcileReceiptAsync(id, revision, session, identity, cancellationToken);
        }
        catch (Exception)
        {
            if (!cancellationToken.IsCancellationRequested && !_disposed && _receiptMode
                && revision == _receiptRevision && session == CurrentSession())
            {
                _receiptProtectionReady = false;
                SetStatus("등록 요청 보호 상태를 확인하지 못했습니다. 같은 의뢰를 다시 조회해 주세요.", ShipperRequestDetailMessageTone.Warning);
            }
        }
        finally { StateChanged?.Invoke(); }
    }

    public void SetReceiptNumber(string value)
    {
        if (ReceiptInputLocked) return;
        ReceiptNumber = value; ReceiptAcceptanceConfirmed = false; StateChanged?.Invoke();
    }
    public void SetReceiptMemo(string value)
    {
        if (ReceiptInputLocked) return;
        ReceiptMemo = value; ReceiptAcceptanceConfirmed = false; StateChanged?.Invoke();
    }
    public void SetReceiptAcceptanceConfirmed(bool value)
    {
        if (ReceiptInputLocked) return;
        ReceiptAcceptanceConfirmed = value; StateChanged?.Invoke();
    }

    public async Task RegisterReceiptAsync(CancellationToken cancellationToken = default)
    {
        if (!CanRegisterReceipt || State.Request is null || _receiptContext is null) return;
        var id = State.Request.RequestId;
        var revision = _receiptRevision;
        var session = CurrentSession();
        WorkflowRecoverySnapshot identity;
        try { identity = _receiptContext.Capture(); }
        catch (Exception exception) when (exception is InvalidOperationException or InvalidDataException or ArgumentException)
        {
            _receiptProtectionReady = false;
            SetStatus("등록 요청 보호 상태를 확인하지 못했습니다. 같은 의뢰를 다시 조회해 주세요.", ShipperRequestDetailMessageTone.Warning);
            StateChanged?.Invoke();
            return;
        }
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _receiptOperation = operation;
        ReceiptSubmitting = true; State.IsBusy = true; ReceiptAcceptanceConfirmed = false;
        State.QueryError = null;
        SetStatus("인수증을 등록하는 중입니다.", ShipperRequestDetailMessageTone.Info);
        var number = ReceiptNumber.Trim();
        var memo = string.IsNullOrWhiteSpace(ReceiptMemo) ? null : ReceiptMemo.Trim();
        StateChanged?.Invoke();
        try
        {
            var scope = ReceiptScope(identity, id);
            var existing = await _receiptRecovery.LoadAsync(identity.Owner, scope, operation.Token);
            if (!ReceiptCurrent(id, revision, session, identity)) return;
            ValidateReceiptRecord(existing, identity, scope, id);
            if (existing is not null)
            {
                _pendingReceipt = existing;
                SetStatus("이전 인수증 등록 결과부터 확인해 주세요.", ShipperRequestDetailMessageTone.Warning);
                return;
            }
            var record = new WorkflowRecoveryRecord(identity.Owner, scope, Guid.NewGuid(), null, DateTime.UtcNow, id);
            await _receiptRecovery.SaveAsync(record, operation.Token);
            if (!ReceiptCurrent(id, revision, session, identity)) return;
            _pendingReceipt = record; _pendingReceiptNumber = number;
            var saved = await _receiptRecovery.LoadAsync(identity.Owner, scope, operation.Token);
            if (!ReceiptCurrent(id, revision, session, identity)) return;
            ValidateReceiptRecord(saved, identity, scope, id);
            if (saved?.RequestId != record.RequestId) throw new InvalidDataException("등록 요청 보호 기록이 일치하지 않습니다.");
            try
            {
                await _settlementService.인수증단일등록Async(id, number, memo,
                    session.UserId!, session.AccessToken!, operation.Token);
            }
            catch (Exception)
            {
                // 응답 실패는 미적용의 증거가 아닙니다. GET으로 등록 사실이 확인될 때까지 보호합니다.
            }
            if (!ReceiptCurrent(id, revision, session, identity)) return;
            ReceiptSubmitting = false; ReceiptChecking = true;
            if (operation.IsCancellationRequested)
                SetStatus("등록 결과가 미확인입니다. 결과 확인 후 다시 진행해 주세요.", ShipperRequestDetailMessageTone.Warning);
            else
            {
                var source = await _settlementService.의뢰조회Async(id, operation.Token);
                if (!ReceiptCurrent(id, revision, session, identity)) return;
                if (source.의뢰Id != id) throw new InvalidDataException("다른 의뢰의 등록 결과입니다.");
                State.Request = ShipperRequestDetailSnapshot.FromContract(source);
                await ReconcileReceiptAsync(id, revision, session, identity, operation.Token);
            }
        }
        catch (Exception)
        {
            if (ReceiptCurrent(id, revision, session, identity))
                SetStatus(ReceiptNeedsResultCheck ? "등록 결과가 미확인입니다. 결과 확인 후 다시 진행해 주세요."
                    : "등록 요청을 시작하지 못했습니다. 같은 의뢰를 다시 조회해 주세요.", ShipperRequestDetailMessageTone.Warning);
        }
        finally
        {
            OnSessionChanged();
            if (ReceiptCurrent(id, revision, session, identity))
            {
                ReceiptSubmitting = ReceiptChecking = false; State.IsBusy = false; StateChanged?.Invoke();
            }
            if (ReferenceEquals(_receiptOperation, operation)) _receiptOperation = null;
        }
    }

    public Task ConfirmReceiptResultAsync(CancellationToken cancellationToken = default)
        => ReceiptNeedsResultCheck && !ReceiptSubmitting && !ReceiptChecking
            ? InitializeReceiptAsync(State.LookupRequestId, cancellationToken) : Task.CompletedTask;

    private async Task ReconcileReceiptAsync(string id, long revision, SessionIdentity session,
        WorkflowRecoverySnapshot identity, CancellationToken ct)
    {
        if (_pendingReceipt is null || State.Request is not { } item) return;
        if (item.ReceiptRegisteredAt is null || string.IsNullOrWhiteSpace(item.ReceiptNumber))
        {
            SetStatus("등록 결과가 아직 확인되지 않았습니다. 결과 확인을 눌러 조회해 주세요. 다시 전송하지 않습니다.", ShipperRequestDetailMessageTone.Warning);
            return;
        }
        var originalNumberKnown = _pendingReceiptNumber is not null;
        var sameNumber = originalNumberKnown && item.ReceiptNumber == _pendingReceiptNumber;
        await _receiptRecovery.RemoveAsync(identity.Owner, _pendingReceipt.Scope, _pendingReceipt.RequestId, ct);
        if (!ReceiptCurrent(id, revision, session, identity)) return;
        _pendingReceipt = null; _pendingReceiptNumber = null;
        ReceiptNumber = ReceiptMemo = string.Empty; ReceiptAcceptanceConfirmed = false;
        SetStatus(!originalNumberKnown ? "등록된 인수증 있음 · 이전 요청과 일치 여부 미확인. 기존 번호와 등록 시각을 확인해 주세요."
            : sameNumber ? "인수증 등록 확인 · 입금·정산은 별도 확인"
            : "다른 인수증 기록이 있습니다. 기존 번호와 등록 시각을 확인해 주세요.",
            sameNumber ? ShipperRequestDetailMessageTone.Success : ShipperRequestDetailMessageTone.Warning);
    }

    private static string ReceiptScope(WorkflowRecoverySnapshot identity, string id)
        => identity.Scope("web.shipper.receipt." + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(id))).ToLowerInvariant()[..32]);
    private static void ValidateReceiptRecord(WorkflowRecoveryRecord? record, WorkflowRecoverySnapshot identity, string scope, string id)
    {
        if (record is null) return;
        WorkflowRecoveryPolicy.Validate(record);
        if (record.Owner != identity.Owner || record.Scope != scope || record.Target != id || record.PayloadJson is not null)
            throw new InvalidDataException("이전 인수증 등록 요청이 현재 의뢰와 다릅니다.");
    }
    private bool ReceiptCurrent(string id, long revision, SessionIdentity session, WorkflowRecoverySnapshot identity)
        => !_disposed && _receiptMode && revision == _receiptRevision && session == CurrentSession()
            && _authSession.IsLoggedIn && State.LookupRequestId == id && State.Request?.RequestId == id
            && _receiptContext?.IsCurrent(identity) == true;
    public void LeaveReceipt()
    {
        ClearReceiptState(); StateChanged?.Invoke();
    }
    private void ClearReceiptState()
    {
        if (ReceiptSubmitting || ReceiptChecking) State.IsBusy = false;
        ++_receiptRevision;
        _receiptMode = false; _receiptProtectionReady = false;
        _receiptOperation?.Cancel(); _receiptOperation = null;
        _pendingReceipt = null; _pendingReceiptNumber = null;
        ReceiptNumber = ReceiptMemo = string.Empty;
        ReceiptAcceptanceConfirmed = ReceiptSubmitting = ReceiptChecking = false;
    }
}
