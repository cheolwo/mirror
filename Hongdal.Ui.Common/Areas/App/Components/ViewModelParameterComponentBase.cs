using System.ComponentModel;
using Microsoft.AspNetCore.Components;

namespace Hongdal.Ui.Common.Areas.App.Components;

/// <summary>
/// Observes a ViewModel supplied by a parent component without creating or owning another DI scope.
/// </summary>
public abstract class ViewModelParameterComponentBase<TViewModel> : ComponentBase, IDisposable
    where TViewModel : class, INotifyPropertyChanged
{
    private TViewModel? _observedViewModel;

    [Parameter, EditorRequired]
    public TViewModel ViewModel { get; set; } = default!;

    protected override void OnParametersSet()
    {
        ArgumentNullException.ThrowIfNull(ViewModel);

        if (ReferenceEquals(_observedViewModel, ViewModel))
        {
            return;
        }

        StopObserving();
        _observedViewModel = ViewModel;
        _observedViewModel.PropertyChanged += HandleViewModelPropertyChanged;
    }

    public void Dispose()
    {
        StopObserving();
        GC.SuppressFinalize(this);
    }

    private void StopObserving()
    {
        if (_observedViewModel is null)
        {
            return;
        }

        _observedViewModel.PropertyChanged -= HandleViewModelPropertyChanged;
        _observedViewModel = null;
    }

    private void HandleViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        => _ = InvokeAsync(StateHasChanged);
}
