using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DriverApp.Services;
using MudBlazor;

namespace DriverApp.ViewModels.Driver.Transport;

public sealed partial class 기사운송예외신고ViewModel : ObservableObject
{
    private readonly IDriverTransportExceptionService _exceptionService;
    private readonly 기사운송작업상태ViewModel _상태;
    private readonly string _단계;
    private readonly string _기본유형;
    private readonly Func<string, string> _코드변환;
    private long _운송Id;

    public 기사운송예외신고ViewModel(
        IDriverTransportExceptionService exceptionService,
        기사운송작업상태ViewModel 상태,
        string 단계,
        IReadOnlyList<string> 유형목록,
        string 기본유형,
        Func<string, string> 코드변환)
    {
        _exceptionService = exceptionService;
        _상태 = 상태;
        _단계 = 단계;
        _기본유형 = 기본유형;
        _코드변환 = 코드변환;
        this.유형목록 = 유형목록;
        유형 = 기본유형;
    }

    public IReadOnlyList<string> 유형목록 { get; }

    [ObservableProperty]
    public partial bool 열림 { get; set; }

    [ObservableProperty]
    public partial string 유형 { get; set; }

    [ObservableProperty]
    public partial string? 메모 { get; set; }

    [ObservableProperty]
    public partial bool 신고중 { get; private set; }

    public void 운송설정(long transportId)
    {
        _운송Id = transportId;
        열림 = false;
        유형 = _기본유형;
        메모 = null;
        신고중 = false;
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    public async Task 신고Async()
    {
        var memo = string.IsNullOrWhiteSpace(메모) ? "메모 없음" : 메모.Trim();
        try
        {
            신고중 = true;
            _상태.설정($"{_단계} 문제를 서버에 신고하는 중입니다.", Severity.Info);

            var result = await _exceptionService.ReportAsync(new DriverTransportExceptionReport(
                TransportId: _운송Id,
                Stage: _단계,
                ExceptionCode: _코드변환(유형),
                Reason: 유형,
                Memo: memo,
                RequestAdminReview: true));

            _상태.설정(
                result.Reported
                    ? $"{result.Message} {유형} · {memo}"
                    : result.Message,
                result.Reported ? Severity.Warning : Severity.Error);
        }
        catch (Exception ex)
        {
            _상태.설정($"{_단계} 문제 신고 중 오류가 발생했습니다. 다시 시도해 주세요. {ex.Message}", Severity.Error);
        }
        finally
        {
            신고중 = false;
        }
    }
}
