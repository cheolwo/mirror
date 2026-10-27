using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DriverApp.Models.Driver.Samples;
using DriverApp.Services;
using DriverApp.ViewModels.Driver;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using Ssalddel.Ui.Common.Areas.App.ViewModels;
using Ssalddel.Contracts.Common.Workflow;
using Ssalddel.Contracts.Common.Versioning;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Completion;

namespace DriverApp.ViewModels.Driver.Transport;

public sealed partial class 기사상차PageViewModel : 기사PageViewModelBase
{
    private readonly 기사운송상세조회Service _detail;
    private Task<bool>? _detailLoad;
    private bool _disposed;
    private long _sessionRevision;
    private long? _initializedTransportId;
    private CancellationTokenSource? _arrivalCancellation;
    private CancellationTokenSource? _completionCancellation;
    private bool _completionOutcomePending;
    private 기사운송샘플항목? _pendingConfirmationTransport;
    private string? _완료실패상태메시지;
    private bool _명시적완료미확정;
    private readonly DriverCargoCompletionRecoverySession _완료복구;
    private bool _완료명령미전송확인;
    private bool _completionFeedbackRequested;

    public 기사상차PageViewModel(
        기사운송상세조회Service detail,
        기사상차완료ViewModel 상차완료,
        IDriverTransportExceptionService exceptionService,
        NavigationManager navigation,
        IDriverCargoPhotoRecoveryStore? recoveryStore = null,
        IDriverCargoCompletionRecovery? completionRecovery = null,
        IDriverCargoExceptionRecovery? exceptionRecovery = null)
    {
        _detail = detail;
        _완료복구 = new(completionRecovery, DriverTransportCompletionPhotoKind.Pickup);
        _sessionRevision = detail.SessionRevision;
        _detail.인증변경 += OnAuthenticationChanged;
        작업상태 = 하위ViewModel등록(new 기사운송작업상태ViewModel());
        화물 = 하위ViewModel등록(new 기사상차화물ViewModel(작업상태));
        현장 = 하위ViewModel등록(new 기사상차현장확인ViewModel());
        인수증 = 하위ViewModel등록(new 기사상차인수증ViewModel());
        사진 = 하위ViewModel등록(new 기사운송사진ViewModel("상차", "상차 완료 사진", 작업상태, recoveryStore));
        예외 = 하위ViewModel등록(new 기사운송예외신고ViewModel(
            exceptionService,
            작업상태,
            "상차",
            ["수량 부족", "다른 화물 혼입", "바코드 훼손", "문서 번호 불일치", "파손 의심"],
            "수량 부족",
            ToExceptionCode,
            () => !_disposed && _detail.로그인됨 && 운송?.Id == 운송Id,
            exceptionRecovery));
        this.상차완료 = 하위ViewModel등록(상차완료);
    }

    [ObservableProperty]
    public partial long 운송Id { get; private set; }

    [ObservableProperty]
    public partial 기사운송샘플항목? 운송 { get; private set; }

    public 기사운송작업상태ViewModel 작업상태 { get; }
    public 기사상차화물ViewModel 화물 { get; }
    public 기사상차현장확인ViewModel 현장 { get; }
    public 기사상차인수증ViewModel 인수증 { get; }
    public 기사운송사진ViewModel 사진 { get; }
    public 기사운송예외신고ViewModel 예외 { get; }
    public 기사상차완료ViewModel 상차완료 { get; }
    public WorkflowCompletionFeedback CompletionFeedback { get; } = new();
    public string? CompletionReturnHref => 상차완료확인 && CompletionFeedback.History.Count > 0
        ? 운송!.현재단계 == "인수완료" ? DriverRoutes.CurrentTransport : DriverRoutes.TransportDropoff(운송Id)
        : null;
    public bool 완료처리중 => _completionCancellation is not null || 상차완료.처리중;
    public bool 완료결과확인필요 => _completionOutcomePending;
    [ObservableProperty]
    public partial Api작업오류? 완료실패 { get; private set; }
    public bool 완료실패상태안내인가 => 완료실패 is not null
        && string.Equals(_완료실패상태메시지, 작업상태.메시지, StringComparison.Ordinal);
    public bool 상차완료확인 => 운송?.현재단계 is "상차완료" or "운송중" or "하차지도착" or "인수완료";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(도착가능))]
    [NotifyPropertyChangedFor(nameof(완료가능))]
    public partial bool 도착처리중 { get; private set; }

    public bool 도착가능
        => !_disposed && !로그인필요 && !처리중 && !완료처리중 && !도착처리중 && !사진.촬영중
           && 운송?.현재단계 is "배차대기" or "매칭중" or "배차확정" or "확정" or "이동중";

    public bool 인수증완료 => 인수증.완료(사진.사진있음);

    public bool 완료가능
        => !_disposed && !로그인필요 && !_completionOutcomePending && _완료복구.CanSubmit && 운송 is not null
           && !처리중
           && !완료처리중
           && !도착처리중
           && 운송.현재단계 == "상차지도착"
           && !사진.촬영중
           && 화물.체크목록완료
           && 화물.화물상차완료
           && 현장.완료
           && 인수증완료
           && 사진.사진있음;

    public string 완료안내
    {
        get
        {
            if (상차완료확인) return "상차 완료";
            if (!_완료복구.CanSubmit && !_completionOutcomePending) return "이전 완료 요청 확인 필요";
            if (_completionOutcomePending) return "완료 여부 미확인 · 재요청 대신 결과 확인";
            if (운송?.현재단계 != "상차지도착")
            {
                return "상차지 도착 확인 필요";
            }

            if (!현장.완료 && !사진.사진있음)
            {
                return 인수증.필요
                    ? 인수증.서명필수
                        ? "현장 확인·인수증·상차 사진 필요"
                        : "현장 확인·인수증 또는 생략 사유·상차 사진 필요"
                    : "현장 확인·상차 사진 필요";
            }

            if (!현장.완료)
            {
                return "현장 확인 필요";
            }

            if (!화물.체크목록완료)
            {
                return "상차 항목 확인 필요";
            }

            if (!화물.화물상차완료)
            {
                return "바코드·화물 상차 확인 필요";
            }

            if (!인수증완료)
            {
                return 인수증.서명필수
                    ? "서명된 인수증 사진 또는 직접 서명 필요"
                    : "인수증 또는 합의된 서명 생략 사유 필요";
            }

            return "상차 사진 필요";
        }
    }

    public void Initialize(long transportId)
    {
        if (_initializedTransportId == transportId)
        {
            return;
        }

        _initializedTransportId = transportId;
        CompletionFeedback.Clear();
        _completionFeedbackRequested = false;
        운송Id = transportId;
        _완료복구.Reset(transportId);
        _완료명령미전송확인 = false;
        도착작업취소();
        완료작업취소();
        _completionOutcomePending = false;
        완료실패 = null;
        _완료실패상태메시지 = null;
        _명시적완료미확정 = false;
        _pendingConfirmationTransport = null;
        취소();
        운송 = null;
        작업상태.초기화();
        화물.운송설정(운송);
        현장.초기화();
        인수증.운송설정(운송);
        사진.초기화();
        예외.운송설정(transportId);
        상차완료.초기화();
        OnPropertyChanged(string.Empty);
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    public async Task 도착Async()
    {
        if (!도착가능)
        {
            return;
        }

        var transportId = 운송Id;
        var sessionRevision = _detail.SessionRevision;
        using var cancellation = new CancellationTokenSource();
        _arrivalCancellation = cancellation;
        도착처리중 = true;
        try
        {
            작업상태.설정("상차지 도착을 기록하고 있습니다.", Severity.Info);
            await _detail.상차지도착Async(transportId, cancellation.Token);
            if (!현재도착작업(transportId, sessionRevision, cancellation)) return;

            _detailLoad = 새로고침Async(cancellation.Token);
            var refreshed = await _detailLoad;
            if (!현재도착작업(transportId, sessionRevision, cancellation)) return;
            var arrived = refreshed && 운송?.현재단계 == "상차지도착";
            작업상태.설정(
                arrived
                    ? "상차지 도착을 기록하고 같은 운송을 다시 확인했습니다."
                    : $"상차지 도착 기록 후 운송을 다시 확인하지 못했습니다. 다시 불러와 주세요. {오류메시지}",
                arrived ? Severity.Success : Severity.Warning);
            if (arrived)
                CompletionFeedback.Record(new($"cargo:{transportId}:pickup-arrival", "상차지 도착을 확인했습니다",
                    "같은 운송의 도착 기록을 다시 확인했습니다.", "다음 단계: 화물과 증빙을 확인하고 상차를 진행해 주세요.",
                    false, WorkflowCompletionVisuals.CargoIconPath, "#DC2626"));
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (Exception ex)
        {
            if (현재도착작업(transportId, sessionRevision, cancellation))
                작업상태.설정($"상차지 도착 처리에 실패했습니다. 다시 시도해 주세요. {ex.Message}", Severity.Error);
        }
        finally
        {
            if (ReferenceEquals(_arrivalCancellation, cancellation))
            {
                _arrivalCancellation = null;
                도착처리중 = false;
            }
        }
    }

    private bool 현재도착작업(long transportId, long sessionRevision, CancellationTokenSource cancellation)
        => ReferenceEquals(_arrivalCancellation, cancellation)
           && !_disposed && !cancellation.IsCancellationRequested && 운송Id == transportId
           && _detail.로그인됨 && _detail.SessionRevision == sessionRevision;

    private void 도착작업취소()
    {
        var cancellation = _arrivalCancellation;
        _arrivalCancellation = null;
        cancellation?.Cancel();
        도착처리중 = false;
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    public async Task 완료Async()
    {
        if (!완료가능)
        {
            작업상태.설정(완료안내, Severity.Warning);
            return;
        }

        var transportId = 운송Id;
        var sessionRevision = _detail.SessionRevision;
        using var cancellation = new CancellationTokenSource();
        _completionCancellation = cancellation;
        _완료명령미전송확인 = false;
        완료실패 = null;
        _완료실패상태메시지 = null;
        _명시적완료미확정 = false;
        _completionOutcomePending = true;
        _pendingConfirmationTransport = 운송 is null ? null : 운송 with
        {
            상차대상화물목록 = 운송.상차대상화물목록.ToArray(),
            상차체크목록 = 운송.상차체크목록.ToArray()
        };
        OnPropertyChanged(string.Empty);
        try
        {
            작업상태.설정("상차 완료를 처리하는 중입니다.", Severity.Info);
            var commandStarted = false;
            try
            {
                var request = 사진.완료요청생성(
                    DriverTransportCompletionPhotoKind.Pickup, transportId,
                    인수증.증빙생성(사진.사진있음));
                await _완료복구.BeginAsync(cancellation.Token);
                if (!현재완료작업(transportId, sessionRevision, cancellation)) return;
                commandStarted = true;
                _completionFeedbackRequested = true;
                await 상차완료.처리Command.ExecuteAsync(request);
                if (!현재완료작업(transportId, sessionRevision, cancellation)) return;
                if (상차완료.결과 is { } result)
                {
                    사진.업로드결과반영(result);
                    _완료명령미전송확인 = result.CompletionNotSubmitted;
                    if (result.CompletionFailure is { } failure)
                        완료실패기록(failure);
                }
            }
            catch (Exception exception)
            {
                // 응답이 끊겨도 미저장으로 단정하지 않고 같은 운송을 다시 확인합니다.
                if (현재완료작업(transportId, sessionRevision, cancellation))
                {
                    _완료명령미전송확인 = !commandStarted;
                    완료실패기록(exception);
                }
            }
            if (!현재완료작업(transportId, sessionRevision, cancellation)) return;
            _detailLoad = 새로고침Async(cancellation.Token);
            var refreshed = await _detailLoad;
            if (!현재완료작업(transportId, sessionRevision, cancellation)) return;
            작업상태.설정(상차완료확인
                ? "같은 운송을 다시 조회해 상차 완료를 확인했습니다."
                : refreshed && 운송 is not null
                    ? "현재 운송 상태를 확인했습니다. 아직 완료되지 않았다면 기존 사진과 확인 내용으로 다시 요청할 수 있습니다."
                    : $"상차 완료 결과를 아직 확인하지 못했습니다. 사진과 확인 내용을 유지했으니 결과를 다시 확인해 주세요. {오류메시지}",
                상차완료확인 ? Severity.Success : Severity.Warning);
            _완료실패상태메시지 = 완료실패 is null ? null : 작업상태.메시지;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        finally
        {
            if (ReferenceEquals(_completionCancellation, cancellation))
            {
                _completionCancellation = null;
                OnPropertyChanged(string.Empty);
            }
        }
    }

    private bool 현재완료작업(long id, long revision, CancellationTokenSource cancellation)
        => ReferenceEquals(_completionCancellation, cancellation) && !_disposed
           && !cancellation.IsCancellationRequested && 운송Id == id
           && _detail.로그인됨 && _detail.SessionRevision == revision;

    private void 완료실패기록(Exception exception)
    {
        var context = 정규화조회오류(exception).WorkflowContext;
        _명시적완료미확정 = context?.유효한가() == true && context.OperatingSystemId == OperatingSystemIds.DomesticCargoTransport
            && context.StageId == OperatingSystemLifecycleStageIds.CargoPickup && context.OutcomeCode == 업무처리결과Codes.미확정;
        완료실패 = 완료오류변환(exception, OperatingSystemLifecycleStageIds.CargoPickup);
    }

    private async Task 완료복구확인Async(WorkflowRecoverySnapshot? snapshot, bool completed, bool canonicalAvailable, CancellationToken cancellationToken)
    {
        if (!_완료복구.Enabled) return;
        var targetId = 운송Id; var revision = _detail.SessionRevision;
        var context = 완료실패?.WorkflowContext;
        var rejected = canonicalAvailable && (_완료명령미전송확인 || context?.유효한가() == true
            && context.OperatingSystemId == OperatingSystemIds.DomesticCargoTransport && context.OutcomeCode == 업무처리결과Codes.미적용
            && (context.StageId == OperatingSystemLifecycleStageIds.CargoPickup
                || context.StageId == OperatingSystemLifecycleStageIds.CargoInterruptionRecovery && 완료실패?.코드 == "CargoTransportHeld"));
        try
        {
            await _완료복구.ObserveAsync(snapshot, completed, rejected, cancellationToken);
            if (_완료복구.Pending is not null)
            {
                _명시적완료미확정 = true;
                완료실패 = 복구안내("CargoCompletionResultPending", 업무처리결과Codes.미확정);
            }
            else if (_완료명령미전송확인)
            {
                _명시적완료미확정 = false;
                완료실패 = 복구안내("CargoCompletionPreparationIncomplete", 업무처리결과Codes.미적용);
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception)
        {
            if (_disposed || targetId != 운송Id || revision != _detail.SessionRevision)
                throw new OperationCanceledException("이전 운송의 복구 결과를 적용하지 않았습니다.", cancellationToken);
            _완료복구.EnsureCurrent(snapshot);
            완료실패 = 복구안내(completed ? "CargoCompletionRecoveryCleanupPending" : "CargoCompletionRecoveryUnavailable",
                completed ? 업무처리결과Codes.적용 : 업무처리결과Codes.미확정);
        }
    }

    private static Api작업오류 복구안내(string code, string outcome)
        => new(code, string.Empty, 복구가능행동: [업무복구행동Ids.상태전체재조회],
            WorkflowContext: new(OperatingSystemIds.DomesticCargoTransport, OperatingSystemLifecycleStageIds.CargoPickup, outcome));

    private void 완료작업취소()
    {
        var cancellation = _completionCancellation;
        _completionCancellation = null;
        cancellation?.Cancel();
        상차완료.처리Command.Cancel();
    }

    public bool 로그인필요 => !_detail.로그인됨;

    public async Task InitializeAsync(long transportId)
    {
        if ((도착처리중 || 완료처리중) && 운송Id == transportId) return;
        Initialize(transportId);
        // 이전 ID의 취소된 작업이 끝난 뒤 현재 ID만 시작합니다.
        if (_detailLoad is not null)
        {
            await _detailLoad;
        }
        if (_disposed || 운송Id != transportId || 처리중)
        {
            return;
        }
        _detailLoad = 새로고침Async();
        await _detailLoad;
    }

    protected override async Task 불러오기Async(
        bool 새로고침,
        CancellationToken cancellationToken)
    {
        var transportId = 운송Id;
        await _detail.인증복원Async(cancellationToken);
        if (_disposed || transportId != 운송Id) return;
        var sessionRevision = _detail.SessionRevision;
        var recoverySnapshot = _완료복구.StartQuery();
        if (_완료복구.ScopeChanged)
        {
            CompletionFeedback.Clear(); _completionFeedbackRequested = false;
            완료실패 = null; _완료실패상태메시지 = null; _명시적완료미확정 = false;
            _완료명령미전송확인 = false; _completionOutcomePending = false; _pendingConfirmationTransport = null;
            사진.초기화();
        }
        var detail = await _detail.조회Async(transportId, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (_disposed || transportId != 운송Id)
        {
            return;
        }
        _완료복구.EnsureCurrent(recoverySnapshot);
        await 완료복구확인Async(recoverySnapshot, detail?.현재단계 is "상차완료" or "운송중" or "하차지도착" or "인수완료", detail is not null, cancellationToken);
        var confirmation = _pendingConfirmationTransport ?? 운송;
        var preserveConfirmation = _completionOutcomePending && confirmation?.현재단계 == "상차지도착"
            && detail?.현재단계 == "상차지도착"
            && confirmation.의뢰Id == detail.의뢰Id && confirmation.픽업지 == detail.픽업지
            && confirmation.상차대상화물목록.SequenceEqual(detail.상차대상화물목록)
            && confirmation.상차체크목록.SequenceEqual(detail.상차체크목록)
            && confirmation.인수증필요 == detail.인수증필요 && confirmation.인수증서명필수 == detail.인수증서명필수
            && confirmation.결제방식 == detail.결제방식 && confirmation.상차담당자명 == detail.상차담당자명
            && confirmation.상차연락처 == detail.상차연락처 && confirmation.상차주의사항 == detail.상차주의사항;
        운송 = detail;
        if (detail is not null && _완료복구.Checked && (상차완료확인 || !_명시적완료미확정 && 완료실패?.WorkflowContext?.OutcomeCode == 업무처리결과Codes.미확정))
        {
            완료실패 = null;
            _완료실패상태메시지 = null;
            _명시적완료미확정 = false;
        }
        if (!preserveConfirmation && !(_completionOutcomePending && detail is null))
        {
            화물.운송설정(detail);
            인수증.운송설정(detail);
            현장.초기화();
        }
        if (detail is not null)
        {
            _completionOutcomePending = (_명시적완료미확정 || _완료복구.Pending is not null || !_완료복구.Checked) && !상차완료확인;
            if (!_completionOutcomePending) _pendingConfirmationTransport = null;
        }
        if (detail is not null)
        {
            사진.복구설정(_detail.OwnerId, DriverTransportCompletionPhotoKind.Pickup, transportId, _detail.SessionRevision);
            if (상차완료확인 && _완료복구.Checked) await 사진.완료확인정리Async(cancellationToken);
            else if (detail.현재단계 == "상차지도착") await 사진.복원Async(cancellationToken);
        }
        cancellationToken.ThrowIfCancellationRequested();
        if (_disposed || transportId != 운송Id || !_detail.로그인됨 || sessionRevision != _detail.SessionRevision) return;
        if (detail is not null) await 예외.복원Async(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (_disposed || transportId != 운송Id || !_detail.로그인됨 || sessionRevision != _detail.SessionRevision) return;
        if (_completionFeedbackRequested && 상차완료확인)
        {
            _completionFeedbackRequested = false;
            CompletionFeedback.Record(new($"cargo:{transportId}:pickup", "상차가 완료되었습니다",
                "같은 운송의 상차 완료 기록을 다시 확인했습니다.",
                detail!.현재단계 == "인수완료" ? "현재 운송 화면에서 다음 업무를 확인해 주세요." : "다음 단계: 목적지로 이동해 주세요.",
                false, WorkflowCompletionVisuals.CargoIconPath, "#DC2626"));
        }
        OnPropertyChanged(string.Empty);
    }

    private void OnAuthenticationChanged()
    {
        if (!_detail.로그인됨 || _sessionRevision != _detail.SessionRevision)
        {
            if (!_detail.로그인됨 || 운송 is not null)
            {
                도착작업취소();
                완료작업취소();
                취소();
            }
            운송 = null;
            CompletionFeedback.Clear();
            _completionFeedbackRequested = false;
            _완료복구.Reset(운송Id);
            _완료명령미전송확인 = false;
            _completionOutcomePending = false;
            완료실패 = null;
            _완료실패상태메시지 = null;
            _명시적완료미확정 = false;
            _pendingConfirmationTransport = null;
            화물.운송설정(null);
            사진.초기화();
            인수증.운송설정(null);
            현장.초기화();
            예외.운송설정(운송Id);
            작업상태.초기화();
            상차완료.초기화();
        }
        _sessionRevision = _detail.SessionRevision;
        OnPropertyChanged(string.Empty);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _disposed = true;
            CompletionFeedback.Clear();
            _completionFeedbackRequested = false;
            _완료복구.Reset(0);
            완료실패 = null;
            _완료실패상태메시지 = null;
            _명시적완료미확정 = false;
            도착작업취소();
            완료작업취소();
            사진.초기화();
            예외.Dispose();
            _detail.인증변경 -= OnAuthenticationChanged;
        }
        base.Dispose(disposing);
    }

    private static string ToExceptionCode(string issueType)
        => issueType switch
        {
            "수량 부족" => "Pickup.QuantityShortage",
            "다른 화물 혼입" => "Pickup.MixedCargo",
            "바코드 훼손" => "Pickup.BarcodeDamaged",
            "문서 번호 불일치" => "Pickup.DocumentMismatch",
            "파손 의심" => "Pickup.DamageSuspected",
            _ => "Pickup.FieldIssue"
        };
}
