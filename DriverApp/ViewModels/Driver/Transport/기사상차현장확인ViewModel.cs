using CommunityToolkit.Mvvm.ComponentModel;

namespace DriverApp.ViewModels.Driver.Transport;

public sealed partial class 기사상차현장확인ViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(완료))]
    public partial bool 상차지확인 { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(완료))]
    public partial bool 화물상태확인 { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(완료))]
    public partial bool 화주연락확인 { get; set; }

    public bool 완료 => 상차지확인 && 화물상태확인 && 화주연락확인;

    public void 초기화()
    {
        상차지확인 = false;
        화물상태확인 = false;
        화주연락확인 = false;
    }
}
