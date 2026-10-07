using FDriverApp.Pages;
using FDriverApp.PageModels;
using Microsoft.Extensions.DependencyInjection;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace FDriverApp
{
    public partial class App : Application
    {
        private readonly MainPage _mainPage;
        private readonly AppShell _shell;
        private readonly 역할앱생명주기State _lifecycle;

        public App(IServiceProvider services, 역할앱생명주기State lifecycle)
        {
            InitializeComponent();
            _mainPage = services.GetRequiredService<MainPage>();
            _shell = services.GetRequiredService<AppShell>();
            _lifecycle = lifecycle;
            _lifecycle.초기화(DeviceInfo.Current.Platform.ToString(), IsConnected());
            Connectivity.Current.ConnectivityChanged += HandleConnectivityChanged;
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            var window = new Window(_shell) { Title = "살뜰 배달" };
            AttachLifecycle(window);
            return window;
        }

        private void AttachLifecycle(Window window)
        {
            window.Created += (_, _) => _lifecycle.전환(역할앱생명주기단계.시작중);
            window.Activated += async (_, _) =>
            {
                _lifecycle.전환(역할앱생명주기단계.활성);
                await RefreshFoodWorkspaceAsync();
            };
            window.Deactivated += (_, _) => PauseFoodWorkspace();
            window.Stopped += (_, _) => PauseFoodWorkspace();
            window.Resumed += async (_, _) =>
            {
                await _lifecycle.재개표시후활성Async();
                await RefreshFoodWorkspaceAsync();
            };
            window.Destroying += (_, _) =>
            {
                _lifecycle.전환(역할앱생명주기단계.종료중);
                PauseFoodWorkspace();
                Connectivity.Current.ConnectivityChanged -= HandleConnectivityChanged;
            };
        }

        private async Task RefreshFoodWorkspaceAsync()
        {
            var work = _mainPage.BindingContext is MainPageModel model
                ? model.ResumeFoodWorkspaceAsync() : Task.CompletedTask;
            var page = Task.CompletedTask;
            if (_shell.CurrentPage?.BindingContext is FDriverCompletedDeliveryDetailPageModel detail)
                page = detail.ResumeAsync();
            else if (_shell.CurrentPage?.BindingContext is FDriverCompletedDeliveryListPageModel list)
                page = list.ResumeAsync();
            await Task.WhenAll(work, page);
        }

        private void PauseFoodWorkspace()
        {
            _lifecycle.전환(역할앱생명주기단계.일시정지);
            if (_mainPage.BindingContext is MainPageModel model)
                model.PauseFoodWorkspace();
            if (_shell.CurrentPage?.BindingContext is FDriverCompletedDeliveryDetailPageModel detail)
                detail.Pause();
            else if (_shell.CurrentPage?.BindingContext is FDriverCompletedDeliveryListPageModel list)
                list.Pause();
        }

        private void HandleConnectivityChanged(object? sender, ConnectivityChangedEventArgs args)
            => _lifecycle.연결상태변경(IsConnected(args.NetworkAccess));

        private static bool IsConnected()
            => IsConnected(Connectivity.Current.NetworkAccess);

        private static bool IsConnected(NetworkAccess access)
            => access is not NetworkAccess.None and not NetworkAccess.Unknown;
    }
}
