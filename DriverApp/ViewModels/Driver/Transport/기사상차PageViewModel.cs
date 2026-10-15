using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DriverApp.Models.Driver.Samples;
using DriverApp.Services;
using DriverApp.ViewModels.Driver;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace DriverApp.ViewModels.Driver.Transport;

public sealed partial class 기사상차PageViewModel : 기사PageViewModelBase
{
    private readonly 기사운송상세조회Service _detail;
    private Task<bool>? _detailLoad;
    private bool _disposed;
    private long _sessionRevision;
    private readonly NavigationManager _navigation;
    private long? _initializedTransportId;
    private CancellationTokenSource? _arrivalCancellation;
    private CancellationTokenSource? _completionCancellation;
    private bool _completionOutcomePending;
    private 기사운송샘플항목? _pendingConfirmationTransport;

    public 기사상차PageViewModel(
        기사운송상세조회Service detail,
        기사상차완료ViewModel 상차완료,
        IDriverTransportExceptionService exceptionService,
        NavigationManager navigation,
        IDriverCargoPhotoRecoveryStore? recoveryStore = null)
    {
        _detail = detail;
        _sessionRevision = detail.SessionRevision;
        _detail.인증변경 += OnAuthenticationChanged;
        _navigation = navigation;
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
            ToExceptionCode));
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
    public bool 완료처리중 => _completionCancellation is not null || 상차완료.처리중;
    public bool 완료결과확인필요 => _completionOutcomePending;
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
        => !_disposed && !로그인필요 && !_completionOutcomePending && 운송 is not null
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
            if (상차완료확인) return "상차 완료를 확인했습니다. 다음 운송 업무로 이동할 수 있습니다.";
            if (_completionOutcomePending) return "상차 완료 결과를 먼저 다시 확인해 주세요. 같은 완료 요청을 반복하지 않습니다.";
            if (운송?.현재단계 != "상차지도착")
            {
                return "상차지 도착을 서버에 기록한 뒤 상차 확인을 진행해 주세요.";
            }

            if (!현장.완료 && !사진.사진있음)
            {
                return 인수증.필요
                    ? 인수증.서명필수
                        ? "현장 확인 3가지, 인수증 증빙, 상차 완료 사진을 모두 완료해야 합니다."
                        : "현장 확인 3가지, 인수증 증빙 또는 생략 사유, 상차 완료 사진을 모두 완료해야 합니다."
                    : "현장 확인 3가지를 체크하고 상차 완료 사진을 촬영해야 합니다.";
            }

            if (!현장.완료)
            {
                return "현장 확인 체크를 모두 완료해야 합니다.";
            }

            if (!화물.체크목록완료)
            {
                return "LCL/FCL 상차 체크 항목을 모두 확인해야 합니다.";
            }

            if (!화물.화물상차완료)
            {
                return "상차 대상 화물의 바코드 조회와 상차 확인을 완료해야 합니다.";
            }

            if (!인수증완료)
            {
                return 인수증.서명필수
                    ? "이 거래는 서명된 문서 사진 또는 직접 서명 입력이 필요합니다."
                    : "인수증 증빙 방식을 완료하거나 현장 합의에 따른 서명 생략 사유를 남겨야 합니다.";
            }

            return "상차 완료 사진을 먼저 촬영해 주세요.";
        }
    }

    public void Initialize(long transportId)
    {
        if (_initializedTransportId == transportId)
        {
            return;
        }

        _initializedTransportId = transportId;
        운송Id = transportId;
        도착작업취소();
        완료작업취소();
        _completionOutcomePending = false;
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
            try
            {
                var request = 사진.완료요청생성(
                    DriverTransportCompletionPhotoKind.Pickup, transportId,
                    인수증.증빙생성(사진.사진있음));
                await 상차완료.처리Command.ExecuteAsync(request);
                if (!현재완료작업(transportId, sessionRevision, cancellation)) return;
                if (상차완료.결과 is { } result) 사진.업로드결과반영(result);
            }
            catch (Exception)
            {
                // 응답이 끊겨도 미저장으로 단정하지 않고 같은 운송을 다시 확인합니다.
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
            if (상차완료확인)
                _navigation.NavigateTo(운송!.현재단계 == "인수완료"
                    ? DriverRoutes.CurrentTransport : DriverRoutes.TransportDropoff(transportId));
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
        var detail = await _detail.조회Async(transportId, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (_disposed || transportId != 운송Id)
        {
            return;
        }
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
        if (!preserveConfirmation && !(_completionOutcomePending && detail is null))
        {
            화물.운송설정(detail);
            인수증.운송설정(detail);
            현장.초기화();
        }
        if (detail is not null)
        {
            _completionOutcomePending = false;
            _pendingConfirmationTransport = null;
        }
        if (detail is not null)
        {
            사진.복구설정(_detail.OwnerId, DriverTransportCompletionPhotoKind.Pickup, transportId, _detail.SessionRevision);
            if (상차완료확인) await 사진.완료확인정리Async(cancellationToken);
            else if (detail.현재단계 == "상차지도착") await 사진.복원Async(cancellationToken);
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
            _completionOutcomePending = false;
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
            도착작업취소();
            완료작업취소();
            사진.초기화();
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
