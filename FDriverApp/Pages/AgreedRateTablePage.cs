using FDriverApp.Components;
using FDriverApp.Services;
using Microsoft.AspNetCore.Components.WebView.Maui;

namespace FDriverApp.Pages;

public sealed class AgreedRateTablePage : ContentPage
{
    public const string RouteName = "food-agreed-rates";
    private BlazorWebView? _view;
    public AgreedRateTablePage() { Title = "합의 요금표"; Shell.SetNavBarIsVisible(this, false); }
    protected override void OnAppearing()
    {
        base.OnAppearing();
        _view = new() { HostPage = "wwwroot/index.html", StartPath = "/agreed-rates" };
        _view.RootComponents.Add(new() { Selector = "#app", ComponentType = typeof(FDriverSupportRoutes) });
        Content = _view;
    }
    protected override void OnDisappearing()
    { _view?.Handler?.DisconnectHandler(); _view = null; Content = null; base.OnDisappearing(); }
    protected override bool OnBackButtonPressed() { _ = ReturnAsync(); return true; }
    internal static Task ReturnAsync() => Shell.Current is { } shell
        ? shell.GoToAsync(shell.Navigation.NavigationStack.Count > 1 ? ".." : "//" + FDriverWorkspaceNavigator.WorkspaceRoute + "?focus=profile")
        : Task.CompletedTask;
}
