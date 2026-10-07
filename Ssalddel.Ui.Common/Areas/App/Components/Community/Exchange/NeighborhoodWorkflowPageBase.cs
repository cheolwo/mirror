using System.ComponentModel;
using Microsoft.AspNetCore.Components;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace Ssalddel.Ui.Common.Areas.App.Components.Community.Exchange;

public abstract class NeighborhoodWorkflowPageBase<TModel> : ComponentBase, IDisposable where TModel : NeighborhoodWorkflowViewModel
{
    [Inject] protected TModel Model { get; set; } = null!;
    [Inject] protected NavigationManager Navigation { get; set; } = null!;
    protected bool PageDisposed { get; private set; }
    protected string Href(string route) => Navigation.ToAbsoluteUri(route.TrimStart('/')).ToString();
    protected override void OnInitialized() => Model.PropertyChanged += Changed;
    private void Changed(object? sender, PropertyChangedEventArgs args) => _ = RenderAsync();
    private async Task RenderAsync()
    {
        if (PageDisposed) return;
        try { await InvokeAsync(() => { if (!PageDisposed) StateHasChanged(); }); }
        catch (InvalidOperationException) when (PageDisposed) { }
    }
    public Task NotifyAuthenticationChangedAsync()
    {
        if (PageDisposed) return Task.CompletedTask;
        Model.ResetAuthentication(); return RenderAsync();
    }
    public void Dispose()
    {
        if (PageDisposed) return;
        PageDisposed = true; Model.PropertyChanged -= Changed;
        // 페이지 DTO는 비우되 같은 계정의 별도 workflow session과 요청 번호는 유지합니다.
        Model.ResetAuthentication(); Model.Dispose();
    }
}
