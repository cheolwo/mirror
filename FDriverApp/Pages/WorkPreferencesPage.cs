using FDriverApp.Components;
using FDriverApp.Services;
using Microsoft.AspNetCore.Components.WebView.Maui;

namespace FDriverApp.Pages;

/// <summary>기존 내 정보에서 같은 공통 업무 선택 화면을 열고, 떠날 때 화면 수명을 종료합니다.</summary>
public sealed class WorkPreferencesPage : ContentPage
{
    public const string RouteName = "food-work-preferences";
    private BlazorWebView? view;
    public WorkPreferencesPage() { Title = "받을 업무·적재 설정"; Shell.SetNavBarIsVisible(this, false); }
    protected override void OnAppearing()
    {
        base.OnAppearing();
        view = new() { HostPage = "wwwroot/index.html", StartPath = "/work-preferences" };
        view.RootComponents.Add(new() { Selector = "#app", ComponentType = typeof(FDriverSupportRoutes) });
        Content = view;
    }
    protected override void OnDisappearing()
    { view?.Handler?.DisconnectHandler(); view = null; Content = null; base.OnDisappearing(); }
    protected override bool OnBackButtonPressed() { _ = ReturnAsync(); return true; }
    internal static Task ReturnAsync() => Shell.Current is { } shell
        ? shell.GoToAsync(shell.Navigation.NavigationStack.Count > 1 ? ".." : "//" + FDriverWorkspaceNavigator.WorkspaceRoute + "?focus=profile")
        : Task.CompletedTask;
}
