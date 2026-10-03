using Microsoft.AspNetCore.Components;
using OrdererApp.Components.Layout;
using Ssalddel.Ui.Common.Areas.App.Models.Auth;

namespace OrdererApp.Components.Pages;

public partial class GroupPurchaseProducts
{
    private bool _authenticationMode;
    private bool _disposed;
    private OrdererPageDisplayContext.PageLease? _displayLease;

    [CascadingParameter]
    public OrdererPageDisplayContext? DisplayContext { get; set; }

    private void SetAuthenticationMode(bool authenticationMode)
    {
        if (_disposed || _displayLease is { IsCurrent: false }) return;
        _authenticationMode = authenticationMode;
        _displayLease?.SetAuthenticationMode(authenticationMode);
    }

    private void OpenAuthentication() => SetAuthenticationMode(true);
    private void ReturnToCatalog()
    {
        if (!ViewModel.Authentication.처리중) SetAuthenticationMode(false);
    }

    private async Task LoginAsync(공통로그인요청 request)
    {
        if (_disposed || !_authenticationMode) return;
        await ViewModel.LoginAsync(request);
        if (!_disposed && ViewModel.Authentication.로그인됨 && !ViewModel.Authentication.오류발생)
            SetAuthenticationMode(false);
    }

    protected override void OnParametersSet()
    {
        if (_disposed || DisplayContext is null) return;
        if (_displayLease?.IsFor(Navigation.Uri) != true)
        {
            _displayLease?.Dispose();
            _displayLease = DisplayContext.Acquire(_authenticationMode, Navigation.Uri);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;
            _displayLease?.Dispose();
        }
        base.Dispose(disposing);
    }

    protected override Task OnInitializedAsync()
        => ViewModel.초기화Async();
}
