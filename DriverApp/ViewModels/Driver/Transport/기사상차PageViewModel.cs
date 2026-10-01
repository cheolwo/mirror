using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DriverApp.Models.Driver.Samples;
using DriverApp.Services;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace DriverApp.ViewModels.Driver.Transport;

public sealed partial class 기사상차PageViewModel : ObservableObject
{
    private readonly IDriverSampleDataService _samples;
    private readonly NavigationManager _navigation;
    private long? _initializedTransportId;

    public 기사상차PageViewModel(
        IDriverSampleDataService samples,
        기사상차완료ViewModel 상차완료,
        IDriverTransportExceptionService exceptionService,
        NavigationManager navigation)
    {
        _samples = samples;
        _navigation = navigation;
        this.상차완료 = 상차완료;

        상태 = new 기사운송작업상태ViewModel();
        화물 = new 기사상차화물ViewModel(상태);
        현장 = new 기사상차현장확인ViewModel();
        인수증 = new 기사상차인수증ViewModel();
        사진 = new 기사운송사진ViewModel("상차", "상차 완료 사진", 상태);
        예외 = new 기사운송예외신고ViewModel(
            exceptionService,
            상태,
            "상차",
            ["수량 부족", "다른 화물 혼입", "바코드 훼손", "문서 번호 불일치", "파손 의심"],
            "수량 부족",
            ToExceptionCode);

        하위ViewModel관찰(상태, 화물, 현장, 인수증, 사진, 예외, 상차완료);
    }

    [ObservableProperty]
    public partial long 운송Id { get; private set; }

    [ObservableProperty]
    public partial 기사운송샘플항목? 운송 { get; private set; }

    public 기사운송작업상태ViewModel 상태 { get; }
    public 기사상차화물ViewModel 화물 { get; }
    public 기사상차현장확인ViewModel 현장 { get; }
    public 기사상차인수증ViewModel 인수증 { get; }
    public 기사운송사진ViewModel 사진 { get; }
    public 기사운송예외신고ViewModel 예외 { get; }
    public 기사상차완료ViewModel 상차완료 { get; }
    public bool 완료처리중 => 상차완료.처리중;

    public bool 인수증완료 => 인수증.완료(사진.사진있음);

    public bool 완료가능
        => !사진.촬영중
           && 화물.체크목록완료
           && 화물.화물상차완료
           && 현장.완료
           && 인수증완료
           && 사진.사진있음;

    public string 완료안내
    {
        get
        {
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
        운송 = _samples.운송조회(transportId);
        상태.초기화();
        화물.운송설정(운송);
        현장.초기화();
        인수증.운송설정(운송);
        사진.초기화();
        예외.운송설정(transportId);
        상차완료.초기화();
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
            상태.설정("상차 완료를 처리하는 중입니다.", Severity.Info);

            var request = 사진.완료요청생성(
                DriverTransportCompletionPhotoKind.Pickup,
                운송Id,
                인수증.증빙생성(사진.사진있음));
            await 상차완료.처리Command.ExecuteAsync(request);
            var result = 상차완료.결과
                         ?? throw new InvalidOperationException("상차 완료 API 처리 결과가 없습니다.");

            사진.업로드결과반영(result);
            var objectName = string.IsNullOrWhiteSpace(result.ObjectName) ? string.Empty : $" 저장 경로: {result.ObjectName}";
            상태.설정($"{result.Message}{objectName}", result.CompletionRecorded ? Severity.Success : Severity.Warning);

            if (result.CompletionRecorded)
            {
                _navigation.NavigateTo(DriverRoutes.TransportDropoff(운송Id));
            }
        }
        catch (Exception ex)
        {
            상태.설정($"상차 완료 처리에 실패했습니다. 다시 시도해 주세요. {ex.Message}", Severity.Error);
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
            "수량 부족" => "Pickup.QuantityShortage",
            "다른 화물 혼입" => "Pickup.MixedCargo",
            "바코드 훼손" => "Pickup.BarcodeDamaged",
            "문서 번호 불일치" => "Pickup.DocumentMismatch",
            "파손 의심" => "Pickup.DamageSuspected",
            _ => "Pickup.FieldIssue"
        };
}
