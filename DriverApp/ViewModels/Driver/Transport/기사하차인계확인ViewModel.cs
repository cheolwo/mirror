using CommunityToolkit.Mvvm.ComponentModel;

namespace DriverApp.ViewModels.Driver.Transport;

public sealed partial class 기사하차인계확인ViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(완료))]
    public partial bool 하차지확인 { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(완료))]
    public partial bool 수령자확인 { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(완료))]
    public partial bool 결제증빙확인 { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(완료))]
    public partial string 결제증빙방식 { get; set; }

    public 기사하차인계확인ViewModel()
    {
        결제증빙방식 = "인수증";
    }

    public bool 완료
        => 하차지확인
           && 수령자확인
           && 결제증빙확인
           && !string.IsNullOrWhiteSpace(결제증빙방식);

    public void 초기화()
    {
        하차지확인 = false;
        수령자확인 = false;
        결제증빙확인 = false;
        결제증빙방식 = "인수증";
    }
}
