using CommunityToolkit.Mvvm.ComponentModel;
using Ssalddel.Contracts.Common.Commerce;
using Ssalddel.Ui.Common.Areas.App.Services.Commerce;

namespace Ssalddel.Ui.Common.Areas.App.ViewModels.Commerce;

public sealed class 통신판매안내ViewModel(I통신판매보호Client client) : ObservableObject, IDisposable
{
    private readonly CancellationTokenSource _lifetime = new();
    private bool _disposed;
    public 통신판매안내Response? Value { get; private set; }
    public bool IsLoading { get; private set; }
    public string? Error { get; private set; }
    public async Task LoadAsync()
    {
        if (_disposed || IsLoading) return;
        IsLoading = true; Error = null; OnPropertyChanged(string.Empty);
        try { var value = await client.안내Async(_lifetime.Token) ?? throw new InvalidOperationException(); if (!_disposed) Value = value; }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch { if (!_disposed) { Value = null; Error = "안내를 불러오지 못했습니다. 다시 확인해 주세요."; } }
        finally { if (!_disposed) { IsLoading = false; OnPropertyChanged(string.Empty); } }
    }
    public void Dispose() { if (_disposed) return; _disposed = true; _lifetime.Cancel(); _lifetime.Dispose(); }
}
