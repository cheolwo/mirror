using CommunityToolkit.Mvvm.ComponentModel;
using Ssalddel.Contracts.Common.Commerce;
using Ssalddel.Ui.Common.Areas.App.Services.Commerce;
namespace Ssalddel.Ui.Common.Areas.App.ViewModels.Commerce;
public sealed class 판매자공개정보ViewModel(I통신판매보호Client client) : ObservableObject, IDisposable
{
    private readonly CancellationTokenSource _lifetime = new();
    private long _generation;
    private bool _disposed;
    public 판매자공개정보Response? Value { get; private set; }
    public string? Error { get; private set; }
    public bool IsLoading { get; private set; }
    public async Task LoadAsync(string? sellerId, long? restaurantId, long? postId = null, string? collaborationId = null)
    {
        if (_disposed) return;
        var generation = ++_generation; Value = null; Error = null; IsLoading = true; OnPropertyChanged(string.Empty);
        try
        {
            var value = postId is > 0 || !string.IsNullOrWhiteSpace(collaborationId)
                ? await client.공개거래판매자Async(postId, collaborationId, _lifetime.Token)
                : await client.공개판매자Async(sellerId, restaurantId, _lifetime.Token);
            if (!_disposed && generation == _generation) Value = value;
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch { if (!_disposed && generation == _generation) Error = "판매자 정보를 확인하지 못했습니다. 다시 확인해 주세요."; }
        finally { if (!_disposed && generation == _generation) { IsLoading = false; OnPropertyChanged(string.Empty); } }
    }
    public void Dispose() { if (_disposed) return; _disposed=true; ++_generation; _lifetime.Cancel(); _lifetime.Dispose(); }
}
