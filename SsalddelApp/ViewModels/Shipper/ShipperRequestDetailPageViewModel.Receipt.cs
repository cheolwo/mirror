using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace SsalddelApp.ViewModels.Shipper;

public sealed partial class ShipperRequestDetailPageViewModel
{
    private bool _receiptMode;
    private CancellationTokenSource? _receiptCts;
    private string? _pendingReceiptNumber;

    public string ReceiptNumber { get; private set; } = string.Empty;
    public string ReceiptMemo { get; private set; } = string.Empty;
    public bool ReceiptAcceptanceConfirmed { get; private set; }
    public bool ReceiptSubmitting { get; private set; }
    public bool ReceiptChecking { get; private set; }
    public bool ReceiptNeedsResultCheck => _pendingReceiptNumber is not null;
    public bool ReceiptInputLocked => _disposed || State.IsBusy || ReceiptSubmitting || ReceiptChecking
        || ReceiptNeedsResultCheck || !CanPrepareReceipt;
    public bool CanRegisterReceipt => _receiptMode && _authSession.IsLoggedIn && !ReceiptInputLocked
        && ReceiptAcceptanceConfirmed && !string.IsNullOrWhiteSpace(ReceiptNumber);

    public bool CanPrepareReceipt => _source is { 인수증등록일시: null }
        && string.IsNullOrWhiteSpace(_source.인수증번호)
        && _source.운송상태 == "인수완료"
        && _source.정산상태 is "후불승인완료" or "인수증대기" or "인수증등록완료"
            or "청구대기" or "입금대기" or "입금확인완료" or "정산완료";

    public string ReceiptReadinessMessage => _source is null ? "의뢰를 다시 조회해 주세요."
        : _source.인수증등록일시 is not null || !string.IsNullOrWhiteSpace(_source.인수증번호)
            ? "등록된 인수증을 확인해 주세요. 입금·정산 상태는 별도로 확인합니다."
        : _source.운송상태 != "인수완료" ? "기사의 운송 완료를 확인한 뒤 인수증을 등록할 수 있습니다."
        : !CanPrepareReceipt ? "현재 정산 조건에서는 인수증을 등록할 수 없습니다. 정산 상태를 확인해 주세요."
        : "수령·검수한 내용을 확인하고 인수증 번호를 등록해 주세요. 입금·정산 완료와는 별개입니다.";

    public Task InitializeReceiptAsync(string? requestId, CancellationToken cancellationToken = default)
    {
        _receiptMode = true;
        State.SourceBoundaryMessage = "인수증은 서버에 저장된 같은 의뢰의 기록을 확인합니다.";
        return InitializeAsync(requestId, cancellationToken: cancellationToken);
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
        _pendingReceiptNumber = ReceiptNumber.Trim();
        var payload = new 화주운송의뢰인수증등록요청
        {
            인수증번호 = _pendingReceiptNumber,
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
            // 응답 유실·거부도 먼저 같은 의뢰의 정본을 확인하며 POST를 재전송하지 않습니다.
            await _operations.RegisterReceiptAsync(requestId, payload, operation.Token);
        }
        catch (Exception ex)
        {
            failure = ex is OperationCanceledException ? "등록 응답을 확인하지 못했습니다." : ex.Message;
        }
        try
        {
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
            ApplyReceiptLookup(submissionFailure);
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
            SetStatus("인수증 등록을 확인했습니다. 입금·정산 상태는 별도로 확인해 주세요.", ShipperRequestDetailMessageTone.Success);
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
            && ReferenceEquals(_receiptCts, operation);

    private void FinishReceiptOperation(string requestId, long revision, CancellationTokenSource operation)
    {
        if (IsCurrentReceipt(requestId, revision, operation))
        {
            ReceiptSubmitting = ReceiptChecking = false;
            State.IsBusy = false;
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
        ReceiptNumber = ReceiptMemo = string.Empty;
        ReceiptAcceptanceConfirmed = ReceiptSubmitting = ReceiptChecking = false;
    }
}
