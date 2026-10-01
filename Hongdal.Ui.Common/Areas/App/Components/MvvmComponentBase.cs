using System.ComponentModel;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Hongdal.Ui.Common.Areas.App.Components;

/// <summary>
/// Razor Component에 ViewModel을 주입하고 속성 변경을 자동으로 다시 그리는 공통 기반 클래스입니다.
/// </summary>
public abstract class MvvmComponentBase<TViewModel> : ComponentBase, IDisposable
    where TViewModel : class, INotifyPropertyChanged
{
    private bool _subscribed;
    private bool _disposed;
    private TViewModel? _viewModel;

    [Inject]
    protected IServiceProvider Services { get; set; } = default!;

    protected TViewModel ViewModel
        => _viewModel ??= Services.GetRequiredService<TViewModel>();

    protected override void OnInitialized()
    {
        base.OnInitialized();
        ViewModel.PropertyChanged += ViewModel속성변경;
        _subscribed = true;
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposing || _disposed)
        {
            return;
        }

        _disposed = true;
        구독해제();
        if (_viewModel is IDisposable disposable)
        {
            disposable.Dispose();
        }

        _viewModel = null;
    }

    private void 구독해제()
    {
        if (!_subscribed)
        {
            return;
        }

        ViewModel.PropertyChanged -= ViewModel속성변경;
        _subscribed = false;
    }

    private void ViewModel속성변경(object? sender, PropertyChangedEventArgs e)
        => _ = InvokeAsync(StateHasChanged);
}
