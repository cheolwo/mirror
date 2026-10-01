using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DriverApp.Models.Driver.Samples;
using DriverApp.Services;
using MudBlazor;

namespace DriverApp.ViewModels.Driver.Transport;

public sealed partial class 기사하차PageViewModel : ObservableObject
{
    private readonly IDriverSampleDataService _samples;
    private long? _initializedTransportId;

    public 기사하차PageViewModel(
        IDriverSampleDataService samples,
        기사하차완료ViewModel 하차완료,
        IDriverTransportExceptionService exceptionService)
    {
        _samples = samples;
        this.하차완료 = 하차완료;

        상태 = new 기사운송작업상태ViewModel();
        화물 = new 기사하차화물ViewModel(상태);
        인계 = new 기사하차인계확인ViewModel();
        사진 = new 기사운송사진ViewModel("하차", "하차 완료 사진", 상태);
        예외 = new 기사운송예외신고ViewModel(
            exceptionService,
            상태,
            "하차",
            ["수량 부족", "다른 화물", "바코드 훼손", "수령자 부재", "파손 의심"],
            "수량 부족",
            ToExceptionCode);

        하위ViewModel관찰(상태, 화물, 인계, 사진, 예외, 하차완료);
    }

    [ObservableProperty]
    public partial long 운송Id { get; private set; }

    [ObservableProperty]
    public partial 기사운송샘플항목? 운송 { get; private set; }

    public 기사운송작업상태ViewModel 상태 { get; }
    public 기사하차화물ViewModel 화물 { get; }
    public 기사하차인계확인ViewModel 인계 { get; }
    public 기사운송사진ViewModel 사진 { get; }
    public 기사운송예외신고ViewModel 예외 { get; }
    public 기사하차완료ViewModel 하차완료 { get; }
    public bool 완료처리중 => 하차완료.처리중;

    public bool 완료가능
        => !사진.촬영중
           && 화물.화물하차완료
           && 인계.완료
           && 사진.사진있음;

    public string 완료안내
    {
        get
        {
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
        운송 = _samples.운송조회(transportId);
        상태.초기화();
        화물.운송설정(운송);
        인계.초기화();
        사진.초기화();
        예외.운송설정(transportId);
        하차완료.초기화();
        OnPropertyChanged(string.Empty);
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    public async Task 완료Async()
    {
        if (!완료가능)
        {
            상태.설정(완료안내, Severity.Warning);
            return;
        }

        try
        {
            상태.설정($"{인계.결제증빙방식} 확인 후 하차 완료를 처리하는 중입니다.", Severity.Info);

            var request = 사진.완료요청생성(DriverTransportCompletionPhotoKind.Dropoff, 운송Id);
            await 하차완료.처리Command.ExecuteAsync(request);
            var result = 하차완료.결과
                         ?? throw new InvalidOperationException("하차 완료 API 처리 결과가 없습니다.");

            사진.업로드결과반영(result);
            var objectName = string.IsNullOrWhiteSpace(result.ObjectName) ? string.Empty : $" 저장 경로: {result.ObjectName}";
            상태.설정($"{result.Message}{objectName}", result.CompletionRecorded ? Severity.Success : Severity.Warning);
        }
        catch (Exception ex)
        {
            상태.설정($"하차 완료 처리에 실패했습니다. 다시 시도해 주세요. {ex.Message}", Severity.Error);
        }
    }

    private void 하위ViewModel관찰(params INotifyPropertyChanged[] children)
    {
        foreach (var child in children)
        {
            child.PropertyChanged += 하위ViewModel변경;
        }
    }

    private void 하위ViewModel변경(object? sender, PropertyChangedEventArgs e)
        => OnPropertyChanged(string.Empty);

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
