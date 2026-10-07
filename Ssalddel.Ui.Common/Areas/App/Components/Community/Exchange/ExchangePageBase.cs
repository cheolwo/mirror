using System.ComponentModel;
using Microsoft.AspNetCore.Components;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace Ssalddel.Ui.Common.Areas.App.Components.Community.Exchange;

public abstract class ExchangePageBase : ComponentBase, IDisposable
{
    [Inject] protected NeighborhoodExchangeViewModel Model { get; set; } = null!;
    [Inject] protected NavigationManager Navigation { get; set; } = null!;
    protected string Href(string route) => Navigation.ToAbsoluteUri(route.TrimStart('/')).ToString();
    protected override void OnInitialized() => Model.PropertyChanged += Changed;
    private void Changed(object? sender, PropertyChangedEventArgs args) => _ = InvokeAsync(StateHasChanged);
    public void Dispose() { Model.PropertyChanged -= Changed; Model.Dispose(); }
}
