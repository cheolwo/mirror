using CommunityToolkit.Mvvm.ComponentModel;
using MudBlazor;

namespace DriverApp.ViewModels.Driver.Transport;

public sealed partial class 기사운송작업상태ViewModel : ObservableObject
{
    [ObservableProperty]
    public partial string? 메시지 { get; private set; }

    [ObservableProperty]
    public partial Severity 심각도 { get; private set; }

    public 기사운송작업상태ViewModel()
    {
        심각도 = Severity.Info;
    }

    public void 설정(string message, Severity severity)
    {
        메시지 = message;
        심각도 = severity;
    }

    public void 초기화()
    {
        메시지 = null;
        심각도 = Severity.Info;
    }
}
