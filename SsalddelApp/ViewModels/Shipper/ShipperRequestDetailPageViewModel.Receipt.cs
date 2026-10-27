using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Ui.Common.Areas.App.ViewModels;
using Ssalddel.Ui.Common.Areas.App.Services;
using System.Security.Cryptography;
using System.Text;

namespace SsalddelApp.ViewModels.Shipper;

public sealed partial class ShipperRequestDetailPageViewModel
{
    private bool _receiptMode;
    private CancellationTokenSource? _receiptCts;
    private string? _pendingReceiptNumber;
    private readonly IWorkflowRecoveryStore? _receiptRecovery;
    private readonly IWorkflowRecoveryContext? _receiptContext;
    private WorkflowRecoveryRecord? _pendingReceiptRecord;
    private WorkflowRecoverySnapshot? _receiptIdentity;
    private bool _receiptProtectionReady;

    public string ReceiptNumber { get; private set; } = string.Empty;
    public string ReceiptMemo { get; private set; } = string.Empty;
    public bool ReceiptAcceptanceConfirmed { get; private set; }
    public bool ReceiptSubmitting { get; private set; }
    public bool ReceiptChecking { get; private set; }
    public bool ReceiptNeedsResultCheck => _pendingReceiptNumber is not null || _pendingReceiptRecord is not null;
    public bool ReceiptInputLocked => _disposed || State.IsBusy || ReceiptSubmitting || ReceiptChecking
        || ReceiptNeedsResultCheck || !CanPrepareReceipt || _receiptContext is not null
            && (!_receiptProtectionReady || _receiptIdentity is null || !_receiptContext.IsCurrent(_receiptIdentity));
    public bool CanRegisterReceipt => _receiptMode && _authSession.IsLoggedIn && !ReceiptInputLocked
        && ReceiptAcceptanceConfirmed && !string.IsNullOrWhiteSpace(ReceiptNumber);

    public bool CanPrepareReceipt => _source is { 인수증등록일시: null }
        && string.IsNullOrWhiteSpace(_source.인수증번호)
        && _source.운송상태 == "인수완료"
        && _source.정산상태 is "후불승인완료" or "인수증대기" or "인수증등록완료"
            or "청구대기" or "입금대기" or "입금확인완료" or "정산완료";

    public string ReceiptReadinessMessage => _receiptContext is not null && _receiptProtectionReady
        && (_receiptIdentity is null || !_receiptContext.IsCurrent(_receiptIdentity))
        ? "계정 또는 연결 서버가 변경되었습니다. 같은 의뢰를 다시 조회하고 내용을 확인해 주세요."
        : _source is null ? "의뢰를 다시 조회해 주세요."
        : _source.인수증등록일시 is not null || !string.IsNullOrWhiteSpace(_source.인수증번호)
            ? "등록된 인수증을 확인해 주세요. 입금·정산 상태는 별도로 확인합니다."
        : _source.운송상태 != "인수완료" ? "기사의 운송 완료를 확인한 뒤 인수증을 등록할 수 있습니다."
        : !CanPrepareReceipt ? "현재 정산 조건에서는 인수증을 등록할 수 없습니다. 정산 상태를 확인해 주세요."
        : "수령·검수한 내용을 확인하고 인수증 번호를 등록해 주세요. 입금·정산 완료와는 별개입니다.";

    public async Task InitializeReceiptAsync(string? requestId, CancellationToken cancellationToken = default)
    {
        WorkflowRecoverySnapshot? expected = null;
        if (_receiptContext is not null)
        {
            try
            {
                await _authSession.RestoreAsync(cancellationToken);
                expected = _receiptContext.Capture();
                if (_receiptIdentity is not null && _receiptIdentity != expected) ClearPrivateState();
            }
            catch (Exception)
            {
                if (!_disposed)
                {
                    ClearPrivateState();
                    SetStatus("로그인과 연결 서버를 확인한 뒤 같은 의뢰를 다시 조회해 주세요.", ShipperRequestDetailMessageTone.Warning);
                    NotifyStateChanged();
                }
                return;
            }
        }
        _receiptMode = true;
        State.SourceBoundaryMessage = "인수증은 서버에 저장된 같은 의뢰의 기록을 확인합니다.";
        await InitializeAsync(requestId, cancellationToken: cancellationToken);
        if (_receiptContext is not null && (expected is null || !_receiptContext.IsCurrent(expected)))
        {
            if (!_disposed)
            {
                ClearPrivateState();
                SetStatus("조회 중 계정 또는 연결 서버가 변경되었습니다. 같은 의뢰를 다시 조회해 주세요.", ShipperRequestDetailMessageTone.Warning);
                NotifyStateChanged();
            }
            return;
        }
        if (_receiptContext is null || _disposed || _source is null || !_authSession.IsLoggedIn
            || _source.의뢰Id != requestId?.Trim() || ReceiptSubmitting || ReceiptChecking) return;
        var id = _source.의뢰Id;
        var loadRevision = Interlocked.Read(ref _loadRevision);
        try
        {
            _receiptProtectionReady = false;
            var identity = expected!;
            var store = _receiptRecovery ?? throw new InvalidOperationException("등록 요청 보호 저장소가 없습니다.");
            var scope = ReceiptRecoveryScope(identity, id);
            var record = await store.LoadAsync(identity.Owner, scope, cancellationToken);
            if (!ReceiptRecoveryCurrent(identity, id) || loadRevision != Interlocked.Read(ref _loadRevision)) return;
            ValidateReceiptRecovery(record, identity, scope, id);
            if (record is null && _pendingReceiptRecord is not null)
                throw new InvalidDataException("이전 등록 요청의 보호 기록을 확인하지 못했습니다.");
            _receiptIdentity = identity;
            _pendingReceiptRecord = record;
            _receiptProtectionReady = true;
            if (record is not null) await ReconcileProtectedReceiptAsync(identity, id, cancellationToken);
        }
        catch (Exception)
        {
            if (!_disposed && _receiptMode && _source?.의뢰Id == id && loadRevision == Interlocked.Read(ref _loadRevision))
            {
                _receiptProtectionReady = false;
                SetStatus("등록 요청 보호 상태를 확인하지 못했습니다. 같은 의뢰를 다시 조회해 주세요.", ShipperRequestDetailMessageTone.Warning);
            }
        }
        finally { NotifyStateChanged(); }
    }

    public void SetReceiptNumber(string value)
    {
        if (ReceiptInputLocked) return;
        ReceiptNumber = value;
        ReceiptAcceptanceConfirmed = false;
        NotifyStateChanged();
    }

    public void SetReceiptMemo(string value)
    {
        if (ReceiptInputLocked) return;
        ReceiptMemo = value;
        ReceiptAcceptanceConfirmed = false;
        NotifyStateChanged();
    }

    public void SetReceiptAcceptanceConfirmed(bool value)
    {
        if (ReceiptInputLocked) return;
        ReceiptAcceptanceConfirmed = value;
        NotifyStateChanged();
    }

    public async Task RegisterReceiptAsync(CancellationToken cancellationToken = default)
    {
        if (!CanRegisterReceipt || _source is null) return;
        var requestId = _source.의뢰Id;
        var sessionRevision = _authSession.SessionRevision;
        var operation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _receiptCts = operation;
        var receiptNumber = ReceiptNumber.Trim();
        var payload = new 화주운송의뢰인수증등록요청
        {
            인수증번호 = receiptNumber,
            등록메모 = string.IsNullOrWhiteSpace(ReceiptMemo) ? null : ReceiptMemo.Trim()
        };
        ReceiptAcceptanceConfirmed = false;
        ReceiptSubmitting = true;
        State.IsBusy = true;
        Interlocked.Increment(ref _loadRevision);
        _queryLifetime.CancelPending();
        SetStatus("인수증을 등록하는 중입니다.", ShipperRequestDetailMessageTone.Info);
        NotifyStateChanged();
        string? failure = null;
        try
        {
            try
            {
            if (_receiptContext is not null)
            {
                var identity = _receiptIdentity;
                if (identity is null || !_receiptContext.IsCurrent(identity))
                    throw new OperationCanceledException("조회한 의뢰의 연결 서버가 변경되었습니다.");
                var store = _receiptRecovery ?? throw new InvalidOperationException();
                var scope = ReceiptRecoveryScope(identity, requestId);
                var old = await store.LoadAsync(identity.Owner, scope, operation.Token);
                if (!IsCurrentReceipt(requestId, sessionRevision, operation)) return;
                ValidateReceiptRecovery(old, identity, scope, requestId);
                if (old is not null)
                {
                    _pendingReceiptRecord = old;
                    SetStatus("이전 인수증 등록 결과부터 확인해 주세요.", ShipperRequestDetailMessageTone.Warning);
                    return;
                }
                var record = new WorkflowRecoveryRecord(identity.Owner, scope, Guid.NewGuid(), null, DateTime.UtcNow, requestId);
                await store.SaveAsync(record, operation.Token);
                if (!IsCurrentReceipt(requestId, sessionRevision, operation)) return;
                _pendingReceiptRecord = record;
                var saved = await store.LoadAsync(identity.Owner, scope, operation.Token);
                if (!IsCurrentReceipt(requestId, sessionRevision, operation)) return;
                ValidateReceiptRecovery(saved, identity, scope, requestId);
                if (saved?.RequestId != record.RequestId) throw new InvalidDataException();
            }
            _pendingReceiptNumber = receiptNumber;
            // 응답 유실·거부도 먼저 같은 의뢰의 정본을 확인하며 POST를 재전송하지 않습니다.
            await _operations.RegisterReceiptAsync(requestId, payload, operation.Token);
            }
            catch (Exception ex)
            {
                failure = _receiptContext is null && ex is not OperationCanceledException
                    ? ex.Message : "등록 응답을 확인하지 못했습니다.";
                if (_receiptContext is not null && _pendingReceiptRecord is null)
                {
                    _receiptProtectionReady = false;
                    if (!_disposed && _authSession.SessionRevision == sessionRevision && State.LookupRequestId == requestId)
                        SetStatus("등록 요청을 보호하지 못해 전송하지 않았습니다. 같은 의뢰를 다시 조회해 주세요.", ShipperRequestDetailMessageTone.Warning);
                    return;
                }
            }
            if (!IsCurrentReceipt(requestId, sessionRevision, operation)) return;
            ReceiptSubmitting = false;
            if (operation.IsCancellationRequested)
                SetStatus("등록 결과가 미확인입니다. 결과 확인을 눌러 같은 의뢰를 조회해 주세요.", ShipperRequestDetailMessageTone.Warning);
            else
                await ConfirmReceiptResultCoreAsync(requestId, sessionRevision, operation, failure);
        }
        finally
        {
            FinishReceiptOperation(requestId, sessionRevision, operation);
        }
    }

    public async Task ConfirmReceiptResultAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed || !_receiptMode || !_authSession.IsLoggedIn || _source is null
            || ReceiptSubmitting || ReceiptChecking || State.IsBusy || !ReceiptNeedsResultCheck) return;
        var requestId = _source.의뢰Id;
        var revision = _authSession.SessionRevision;
        var operation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _receiptCts = operation;
        Interlocked.Increment(ref _loadRevision);
        _queryLifetime.CancelPending();
        try { await ConfirmReceiptResultCoreAsync(requestId, revision, operation); }
        finally { FinishReceiptOperation(requestId, revision, operation); }
    }

    private async Task ConfirmReceiptResultCoreAsync(string requestId, long revision,
        CancellationTokenSource operation, string? submissionFailure = null)
    {
        ReceiptChecking = true;
        State.IsBusy = true;
        SetStatus("같은 의뢰의 인수증 등록 결과를 확인하는 중입니다.", ShipperRequestDetailMessageTone.Info);
        NotifyStateChanged();
        try
        {
            var result = await _operations.GetRequestForReceiptAsync(requestId, operation.Token);
            if (!IsCurrentReceipt(requestId, revision, operation) || operation.IsCancellationRequested) return;
            if (result is null || !string.Equals(result.의뢰Id, requestId, StringComparison.Ordinal))
                throw new InvalidOperationException("선택한 의뢰의 등록 결과를 확인하지 못했습니다.");
            _source = result;
            State.Request = Map(result);
            if (_receiptContext is not null && _receiptIdentity is not null)
                await ReconcileProtectedReceiptAsync(_receiptIdentity, requestId, operation.Token);
            else ApplyReceiptLookup(submissionFailure);
        }
        catch (Exception ex)
        {
            if (IsCurrentReceipt(requestId, revision, operation))
                SetStatus($"등록 결과가 미확인입니다. 결과 확인 후 다시 진행해 주세요. {ex.Message}", ShipperRequestDetailMessageTone.Warning);
        }
    }

    private void ApplyReceiptLookup(string? submissionFailure = null)
    {
        if (_pendingReceiptNumber is null || _source is null) return;
        if (_source.인수증등록일시 is not null && _source.인수증번호 == _pendingReceiptNumber)
        {
            _pendingReceiptNumber = null;
            ReceiptNumber = ReceiptMemo = string.Empty;
            SetStatus("인수증 등록 완료 · 입금·정산은 별도 확인", ShipperRequestDetailMessageTone.Success);
        }
        else if (_source.인수증등록일시 is not null || !string.IsNullOrWhiteSpace(_source.인수증번호))
        {
            _pendingReceiptNumber = null;
            SetStatus("다른 인수증 기록이 있습니다. 기존 번호와 등록 시각을 확인해 주세요.", ShipperRequestDetailMessageTone.Warning);
        }
        else
        {
            _pendingReceiptNumber = null;
            ReceiptAcceptanceConfirmed = false;
            SetStatus(string.IsNullOrWhiteSpace(submissionFailure)
                ? "아직 인수증이 등록되지 않았습니다. 내용을 확인한 뒤 직접 다시 등록해 주세요."
                : $"인수증 미등록을 확인했습니다. {submissionFailure}", ShipperRequestDetailMessageTone.Warning);
        }
    }

    private bool IsCurrentReceipt(string requestId, long revision, CancellationTokenSource operation)
        => !_disposed && _authSession.IsLoggedIn && _authSession.SessionRevision == revision
            && string.Equals(State.LookupRequestId, requestId, StringComparison.Ordinal)
            && ReferenceEquals(_receiptCts, operation)
            && (_receiptContext is null || _receiptIdentity is not null && _receiptContext.IsCurrent(_receiptIdentity));

    private async Task ReconcileProtectedReceiptAsync(WorkflowRecoverySnapshot identity, string id, CancellationToken ct)
    {
        if (_pendingReceiptRecord is null || _source is null || !ReceiptRecoveryCurrent(identity, id)) return;
        if (_source.인수증등록일시 is null || string.IsNullOrWhiteSpace(_source.인수증번호))
        {
            SetStatus("등록 결과가 아직 확인되지 않았습니다. 같은 의뢰의 결과를 다시 확인해 주세요. 다시 전송하지 않습니다.", ShipperRequestDetailMessageTone.Warning);
            return;
        }
        var originalKnown = _pendingReceiptNumber is not null;
        var same = originalKnown && _source.인수증번호 == _pendingReceiptNumber;
        await _receiptRecovery!.RemoveAsync(identity.Owner, _pendingReceiptRecord.Scope, _pendingReceiptRecord.RequestId, ct);
        if (!ReceiptRecoveryCurrent(identity, id)) return;
        _pendingReceiptRecord = null;
        _pendingReceiptNumber = null;
        ReceiptNumber = ReceiptMemo = string.Empty;
        ReceiptAcceptanceConfirmed = false;
        SetStatus(!originalKnown ? "등록된 인수증 있음 · 이전 요청과 일치 여부 미확인. 기존 번호와 등록 시각을 확인해 주세요."
            : same ? "인수증 등록 확인 · 입금·정산은 별도 확인"
            : "다른 인수증 기록이 있습니다. 기존 번호와 등록 시각을 확인해 주세요.",
            same ? ShipperRequestDetailMessageTone.Success : ShipperRequestDetailMessageTone.Warning);
    }

    private bool ReceiptRecoveryCurrent(WorkflowRecoverySnapshot identity, string id)
        => !_disposed && _receiptMode && _authSession.IsLoggedIn && _receiptContext?.IsCurrent(identity) == true
            && _source?.의뢰Id == id && State.LookupRequestId == id;

    private static string ReceiptRecoveryScope(WorkflowRecoverySnapshot identity, string id)
        => identity.Scope("shipper.receipt." + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(id))).ToLowerInvariant()[..32]);

    private static void ValidateReceiptRecovery(WorkflowRecoveryRecord? record, WorkflowRecoverySnapshot identity, string scope, string id)
    {
        if (record is null) return;
        WorkflowRecoveryPolicy.Validate(record);
        if (record.Owner != identity.Owner || record.Scope != scope || record.Target != id || record.PayloadJson is not null)
            throw new InvalidDataException("이전 등록 요청이 현재 의뢰와 다릅니다.");
    }

    private void FinishReceiptOperation(string requestId, long revision, CancellationTokenSource operation)
    {
        if (!_disposed && _authSession.IsLoggedIn && _authSession.SessionRevision == revision
            && State.LookupRequestId == requestId && ReferenceEquals(_receiptCts, operation))
        {
            ReceiptSubmitting = ReceiptChecking = false;
            State.IsBusy = false;
            if (_receiptContext is not null && (_receiptIdentity is null || !_receiptContext.IsCurrent(_receiptIdentity)))
                _receiptProtectionReady = false;
            NotifyStateChanged();
        }
        if (ReferenceEquals(_receiptCts, operation)) _receiptCts = null;
        operation.Dispose();
    }

    private void ClearReceiptState()
    {
        _receiptCts?.Cancel();
        _receiptCts = null;
        _pendingReceiptNumber = null;
        _pendingReceiptRecord = null;
        _receiptIdentity = null;
        _receiptProtectionReady = false;
        ReceiptNumber = ReceiptMemo = string.Empty;
        ReceiptAcceptanceConfirmed = ReceiptSubmitting = ReceiptChecking = false;
    }
}
