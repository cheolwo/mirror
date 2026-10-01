using System.ComponentModel;
using Microsoft.AspNetCore.Components;

namespace DriverApp.Components;

public abstract class MvvmComponentBase<TViewModel> : ComponentBase, IDisposable
    where TViewModel : class, INotifyPropertyChanged
{
    [Inject]
    protected TViewModel ViewModel { get; set; } = default!;

    protected override void OnInitialized()
    {
        base.OnInitialized();
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    public void Dispose()
    {
        ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        if (ViewModel is IDisposable disposable)
        {
            disposable.Dispose();
        }

        GC.SuppressFinalize(this);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        => _ = InvokeAsync(StateHasChanged);
}
