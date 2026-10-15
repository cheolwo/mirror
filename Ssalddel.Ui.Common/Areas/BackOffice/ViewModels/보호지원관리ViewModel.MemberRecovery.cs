using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.PrivacySupport;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Ui.Common.Areas.BackOffice.ViewModels;

public sealed partial class 보호지원관리ViewModel
{
    private readonly MemberDeliveryRecoveryState _memberDeliveryRecovery = new();
    public NeighborhoodMemberRecoveryContextResponse? Recovery => _memberDeliveryRecovery.Context;
    public string RecoveryResolution { get => _memberDeliveryRecovery.Resolution; set => _memberDeliveryRecovery.Resolution = value; }
    public bool RecoveryConfirmed { get => _memberDeliveryRecovery.Confirmed; set => _memberDeliveryRecovery.Confirmed = value; }
    public bool HasRecoveryPending => _memberDeliveryRecovery.HasPending;
    public bool CanLoadRecovery => IsAuthenticated && !IsBusy && !HasPending && Detail is
        { Kind: 보호지원종류Codes.분쟁, SourceKind: 보호지원출처Codes.생활배송, StatusCode: "closed", RetentionPending: false }
        && !string.IsNullOrWhiteSpace(Detail.SourceId);
    public bool CanSubmitRecovery => CanLoadRecovery && _memberDeliveryRecovery.CanSubmit(Detail!.SourceId, Detail.CaseId);
    public bool CanRetryRecovery => IsAuthenticated && !IsBusy && _memberDeliveryRecovery.CanRetry;

    public async Task LoadRecoveryAsync()
    {
        SynchronizeOwner();
        if (!CanLoadRecovery) return;
        var requestId = Detail!.SourceId; var request = new NeighborhoodMemberRecoveryRequest { SupportCaseId = Detail.CaseId };
        var op = Begin(); IsBusy = true; Error = Notice = null; RecoveryConfirmed = false; Changed();
        try
        {
            var row = await client.배송복구조회Async(requestId, request, _operation.Token);
            if (!Current(op)) return;
            _memberDeliveryRecovery.ApplyLoadedContext(row, requestId, request.SupportCaseId);
        }
        catch (Exception ex) { if (Current(op)) { _memberDeliveryRecovery.ClearContext(); ApplyRecoveryError(ex); } }
        finally { if (Current(op)) { IsBusy = false; Changed(); } }
    }

    public async Task SubmitRecoveryAsync()
    {
        SynchronizeOwner();
        // 미확정 요청은 새 요청으로 덮지 않고 결과 조회·동일 요청 재확인으로만 이어갑니다.
        if (HasRecoveryPending || !CanSubmitRecovery) return;
        _memberDeliveryRecovery.PrepareSubmission();
        await SendRecoveryAsync();
    }

    public Task CheckRecoveryAsync() => RunRecoveryAsync(send: false);
    public Task RetryRecoveryAsync() => CanRetryRecovery ? RunRecoveryAsync(send: true) : Task.CompletedTask;
    private Task SendRecoveryAsync() => RunRecoveryAsync(send: true);

    private async Task RunRecoveryAsync(bool send)
    {
        SynchronizeOwner();
        if (!IsAuthenticated || IsBusy || _memberDeliveryRecovery.Pending is null || _memberDeliveryRecovery.RequestId is null || _disposed) return;
        var request = _memberDeliveryRecovery.Pending; var requestId = _memberDeliveryRecovery.RequestId;
        var op = Begin(); IsBusy = true; _memberDeliveryRecovery.BeginRead(); Error = Notice = null; Changed();
        try
        {
            // 최초 요청과 재확인 모두 정본을 먼저 읽고, 바뀐 판본에 이전 명령을 적용하지 않습니다.
            var row = await client.배송복구조회Async(requestId, request, _operation.Token);
            if (!Current(op)) return;
            if (ApplyRecoveryRead(row, requestId, request)) return;
            if (!send || _memberDeliveryRecovery.Acknowledged || !_memberDeliveryRecovery.ReadSucceeded) return;
            var response = await client.배송복구Async(requestId, request, _operation.Token);
            if (!Current(op)) return;
            _memberDeliveryRecovery.ApplyAcknowledgement(response, requestId, request);
            row = await client.배송복구조회Async(requestId, request, _operation.Token);
            if (Current(op)) ApplyRecoveryRead(row, requestId, request);
        }
        catch (Exception ex)
        {
            if (!Current(op)) return;
            _memberDeliveryRecovery.BeginRead(); ApplyRecoveryError(ex);
            // 불확실한 전송은 보존합니다. 현재 상태만으로 성공 처리하거나 자동 재전송하지 않습니다.
            if (ex is SsalddelApiException { StatusCode: 400 or 404 or 409 or 422 })
            { _memberDeliveryRecovery.RejectPendingRead(); }
        }
        finally { if (Current(op)) { IsBusy = false; Changed(); } }
    }

    private bool ApplyRecoveryRead(NeighborhoodMemberRecoveryContextResponse row, string requestId, NeighborhoodMemberRecoveryRequest request)
    {
        switch (_memberDeliveryRecovery.ApplyRead(row, requestId, request))
        {
            case MemberDeliveryRecoveryReadOutcome.Applied:
                Notice = "배송 복구 요청의 처리 기록을 확인했습니다. 현재 배송 상태를 확인하세요. 수령·정산·지급 완료는 별개입니다.";
                return true;
            case MemberDeliveryRecoveryReadOutcome.Changed:
                Error = "배송 상태가 변경되었습니다. 이 요청의 성공은 확인되지 않았습니다. 연결된 배송을 다시 확인해 주세요.";
                return true;
            case MemberDeliveryRecoveryReadOutcome.Blocked:
                Error = row.BlockReason ?? "현재 조건에서는 이 복구를 진행할 수 없습니다. 연결된 배송을 다시 확인해 주세요.";
                return true;
            default:
                Notice = _memberDeliveryRecovery.Acknowledged ? "요청 응답을 받았지만 처리 기록은 아직 확인되지 않았습니다. 결과를 다시 확인해 주세요."
                    : "현재 배송을 확인했습니다. 이 요청의 처리 기록은 아직 없습니다.";
                return false;
        }
    }
    private void ApplyRecoveryError(Exception ex)
    {
        if (ex is SsalddelApiException { StatusCode: 401 or 403 }) { ApplyError(ex); return; }
        Error = ex is SsalddelApiException { StatusCode: 400 or 404 or 409 or 422 }
            ? "연결된 배송과 종결 사건의 처리 조건이 바뀌었습니다. 최신 내용을 다시 확인해 주세요."
            : "배송 복구 결과를 확인하지 못했습니다. 요청 내용은 보존했으니 결과를 다시 확인해 주세요.";
    }
    private void ClearRecovery() { _memberDeliveryRecovery.Clear(); ClearFoodFollowUp(); }
}
