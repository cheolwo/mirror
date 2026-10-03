namespace DriverApp;

public partial class MainPage : ContentPage
{
    public MainPage() : this("/")
    {
    }

    public MainPage(string startPath)
    {
        InitializeComponent();
        WorkspaceWebView.StartPath = startPath;
    }

    public async Task OpenRouteAsync(string route)
    {
        var dispatched = await WorkspaceWebView.TryDispatchAsync(services =>
            Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                .GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>(services)
                .NavigateTo(route));
        if (!dispatched)
        {
            // 아직 Blazor 범위가 만들어지지 않은 새 화면은 시작 경로로 인계합니다.
            WorkspaceWebView.StartPath = route;
        }
    }
}
