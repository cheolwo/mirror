using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DriverApp.Models.Driver.Samples;
using DriverApp.Services;
using DriverApp.ViewModels.Driver;
using MudBlazor;

namespace DriverApp.ViewModels.Driver.Transport;

public sealed partial class 기사하차PageViewModel : 기사PageViewModelBase
{
    private readonly 기사운송상세조회Service _detail;
    private Task<bool>? _detailLoad;
    private bool _disposed;
    private long _sessionRevision;
    private long? _initializedTransportId;
    private CancellationTokenSource? _arrivalCancellation;

    public 기사하차PageViewModel(
        기사운송상세조회Service detail,
        기사하차완료ViewModel 하차완료,
        IDriverTransportExceptionService exceptionService)
    {
        _detail = detail;
        _sessionRevision = detail.SessionRevision;
        _detail.인증변경 += OnAuthenticationChanged;
        작업상태 = 하위ViewModel등록(new 기사운송작업상태ViewModel());
        화물 = 하위ViewModel등록(new 기사하차화물ViewModel(작업상태));
        인계 = 하위ViewModel등록(new 기사하차인계확인ViewModel());
        사진 = 하위ViewModel등록(new 기사운송사진ViewModel("하차", "하차 완료 사진", 작업상태));
        예외 = 하위ViewModel등록(new 기사운송예외신고ViewModel(
            exceptionService,
            작업상태,
            "하차",
            ["수량 부족", "다른 화물", "바코드 훼손", "수령자 부재", "파손 의심"],
            "수량 부족",
            ToExceptionCode));
        this.하차완료 = 하위ViewModel등록(하차완료);
    }

    [ObservableProperty]
    public partial long 운송Id { get; private set; }

    [ObservableProperty]
    public partial 기사운송샘플항목? 운송 { get; private set; }

    public 기사운송작업상태ViewModel 작업상태 { get; }
    public 기사하차화물ViewModel 화물 { get; }
    public 기사하차인계확인ViewModel 인계 { get; }
    public 기사운송사진ViewModel 사진 { get; }
    public 기사운송예외신고ViewModel 예외 { get; }
    public 기사하차완료ViewModel 하차완료 { get; }
    public bool 완료처리중 => 하차완료.처리중;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(도착가능))]
    [NotifyPropertyChangedFor(nameof(완료가능))]
    public partial bool 도착처리중 { get; private set; }

    public bool 도착가능
        => !_disposed && !로그인필요 && !처리중 && !완료처리중 && !도착처리중 && !사진.촬영중
           && 운송?.현재단계 is "상차완료" or "운송중";

    public bool 완료가능
        => 운송 is not null
           && !처리중
           && !완료처리중
           && !도착처리중
           && 운송.현재단계 == "하차지도착"
           && !사진.촬영중
           && 화물.화물하차완료
           && 인계.완료
           && 사진.사진있음;

    public string 완료안내
    {
        get
        {
            if (운송?.현재단계 != "하차지도착")
            {
                return "하차지 도착을 서버에 기록한 뒤 인수 확인을 진행해 주세요.";
            }

            if (!인계.완료 && !사진.사진있음)
            {
                return "인수/결제 확인을 완료하고 하차 완료 사진을 촬영해야 합니다.";
            }

            if (!인계.완료)
            {
                return "인수자와 결제/증빙 확인을 모두 완료해야 합니다.";
            }

            if (!화물.화물하차완료)
            {
                return "세대/하차지별 화물 하차 확인을 완료해야 합니다.";
            }

            return "하차 완료 사진을 먼저 촬영해 주세요.";
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
        취소();
        운송 = null;
        작업상태.초기화();
        화물.운송설정(운송);
        인계.초기화();
        사진.초기화();
        예외.운송설정(transportId);
        하차완료.초기화();
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
            작업상태.설정("하차지 도착을 기록하고 있습니다.", Severity.Info);
            await _detail.하차지도착Async(transportId, cancellation.Token);
            if (!현재도착작업(transportId, sessionRevision, cancellation)) return;

            _detailLoad = 새로고침Async(cancellation.Token);
            var refreshed = await _detailLoad;
            if (!현재도착작업(transportId, sessionRevision, cancellation)) return;
            var arrived = refreshed && 운송?.현재단계 == "하차지도착";
            작업상태.설정(
                arrived
                    ? "하차지 도착을 기록하고 같은 운송을 다시 확인했습니다."
                    : $"하차지 도착 기록 후 운송을 다시 확인하지 못했습니다. 다시 불러와 주세요. {오류메시지}",
                arrived ? Severity.Success : Severity.Warning);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (Exception ex)
        {
            if (현재도착작업(transportId, sessionRevision, cancellation))
                작업상태.설정($"하차지 도착 처리에 실패했습니다. 다시 시도해 주세요. {ex.Message}", Severity.Error);
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
        try
        {
            작업상태.설정($"{인계.결제증빙방식} 확인 후 하차 완료를 처리하는 중입니다.", Severity.Info);

            var request = 사진.완료요청생성(DriverTransportCompletionPhotoKind.Dropoff, 운송Id);
            await 하차완료.처리Command.ExecuteAsync(request);
            if (_disposed || transportId != 운송Id || sessionRevision != _detail.SessionRevision)
            {
                return;
            }
            var result = 하차완료.결과
                         ?? throw new InvalidOperationException("하차 완료 API 처리 결과가 없습니다.");

            사진.업로드결과반영(result);
            var objectName = string.IsNullOrWhiteSpace(result.ObjectName) ? string.Empty : $" 저장 경로: {result.ObjectName}";
            작업상태.설정($"{result.Message}{objectName}", result.CompletionRecorded ? Severity.Success : Severity.Warning);
        }
        catch (Exception ex)
        {
            if (_disposed || transportId != 운송Id || sessionRevision != _detail.SessionRevision)
            {
                return;
            }
            작업상태.설정($"하차 완료 처리에 실패했습니다. 다시 시도해 주세요. {ex.Message}", Severity.Error);
        }
    }

    public bool 로그인필요 => !_detail.로그인됨;

    public async Task InitializeAsync(long transportId)
    {
        if (도착처리중 && 운송Id == transportId) return;
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
        운송 = detail;
        화물.운송설정(detail);
        인계.초기화();
        OnPropertyChanged(string.Empty);
    }

    private void OnAuthenticationChanged()
    {
        if (!_detail.로그인됨 || _sessionRevision != _detail.SessionRevision)
        {
            if (!_detail.로그인됨 || 운송 is not null)
            {
                도착작업취소();
                취소();
            }
            운송 = null;
            화물.운송설정(null);
            사진.초기화();
            인계.초기화();
            예외.운송설정(운송Id);
            작업상태.초기화();
            하차완료.초기화();
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
            _detail.인증변경 -= OnAuthenticationChanged;
        }
        base.Dispose(disposing);
    }

    private static string ToExceptionCode(string issueType)
        => issueType switch
        {
            "수량 부족" => "Dropoff.QuantityShortage",
            "다른 화물" => "Dropoff.WrongCargo",
            "바코드 훼손" => "Dropoff.BarcodeDamaged",
            "수령자 부재" => "Dropoff.RecipientAbsent",
            "파손 의심" => "Dropoff.DamageSuspected",
            _ => "Dropoff.FieldIssue"
        };
}
