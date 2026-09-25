using Ssalddel.Ui.Common.Areas.App.Services;

namespace OrdererApp;

public partial class App : Application
{
    private readonly 역할앱생명주기State _lifecycle;

    public App(역할앱생명주기State lifecycle)
    {
        InitializeComponent();
        _lifecycle = lifecycle;
        _lifecycle.초기화(DeviceInfo.Current.Platform.ToString(), IsConnected());
        Connectivity.Current.ConnectivityChanged += HandleConnectivityChanged;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(new MainPage()) { Title = "살뜰 주문" };
        AttachLifecycle(window);
        return window;
    }

    private void AttachLifecycle(Window window)
    {
        window.Created += (_, _) => _lifecycle.전환(역할앱생명주기단계.시작중);
        window.Activated += (_, _) => _lifecycle.전환(역할앱생명주기단계.활성);
        window.Deactivated += (_, _) => _lifecycle.전환(역할앱생명주기단계.일시정지);
        window.Stopped += (_, _) => _lifecycle.전환(역할앱생명주기단계.일시정지);
        window.Resumed += async (_, _) => await _lifecycle.재개표시후활성Async();
        window.Destroying += (_, _) =>
        {
            _lifecycle.전환(역할앱생명주기단계.종료중);
            Connectivity.Current.ConnectivityChanged -= HandleConnectivityChanged;
        };
    }

    private void HandleConnectivityChanged(object? sender, ConnectivityChangedEventArgs args)
        => _lifecycle.연결상태변경(IsConnected(args.NetworkAccess));

    private static bool IsConnected()
        => IsConnected(Connectivity.Current.NetworkAccess);

    private static bool IsConnected(NetworkAccess access)
        => access is not NetworkAccess.None and not NetworkAccess.Unknown;
}
