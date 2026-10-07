using FDriverApp.Components;
using Microsoft.AspNetCore.Components.WebView.Maui;
using Ssalddel.Contracts.Common.Commerce;

namespace FDriverApp.Pages;

/// <summary>배달 수행 화면과 분리된 공통 접수 화면입니다. 화면 이탈 시 WebView의 비공개 상태를 폐기합니다.</summary>
public sealed class ProtectionSupportPage : ContentPage, IQueryAttributable
{
    private readonly IFDriverProtectionSupportNavigator _navigator;
    private BlazorWebView? _webView;
    private string? _orderNo;
    public ProtectionSupportPage(IFDriverProtectionSupportNavigator navigator)
    {
        _navigator = navigator;
        Title = "배달 문제 접수";
        Shell.SetNavBarIsVisible(this, false);
    }
    public void ApplyQueryAttributes(IDictionary<string, object> query)
        => _orderNo = query.TryGetValue(FDriverProtectionSupportNavigator.OrderQueryKey, out var value)
            ? Convert.ToString(value) : null;
    protected override void OnAppearing()
    {
        base.OnAppearing();
        var back = new Button { Text = "배달 화면으로", MinimumHeightRequest = 48, HorizontalOptions = LayoutOptions.Start };
        back.Clicked += async (_, _) => await _navigator.BackAsync();
        _webView = new BlazorWebView { HostPage = "wwwroot/index.html", StartPath = CommerceAuthenticationRoutes.DisputeRoute(null, "food-order", _orderNo) };
        _webView.RootComponents.Add(new RootComponent { Selector = "#app", ComponentType = typeof(FDriverSupportRoutes) });
        var grid = new Grid { RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star) } };
        grid.Add(back); grid.Add(_webView, 0, 1); Content = grid;
    }
    protected override void OnDisappearing()
    {
        _webView?.Handler?.DisconnectHandler(); _webView = null; Content = null;
        base.OnDisappearing();
    }
    protected override bool OnBackButtonPressed() { _ = _navigator.BackAsync(); return true; }
}
