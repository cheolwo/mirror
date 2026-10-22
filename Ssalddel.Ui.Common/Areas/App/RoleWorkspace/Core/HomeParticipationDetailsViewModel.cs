using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using Ssalddel.Contracts.Common.Home;

namespace Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;

/// <summary>공개 집계 페이지의 조회·취소·페이지 상태만 소유합니다.</summary>
public sealed class HomeParticipationDetailsViewModel(IHomeParticipationDetailsClient client,
    ILogger<HomeParticipationDetailsViewModel> logger) : ObservableObject, IDisposable
{
    private HomeParticipationDetailsResponse? _response;
    private bool _isLoading;
    private bool _hasError;
    private int _page = 1;
    private int _generation;
    private bool _disposed;
    private CancellationTokenSource? _request;
    public HomeParticipationDetailsResponse? Response { get => _response; private set => SetProperty(ref _response, value); }
    public bool IsLoading { get => _isLoading; private set => SetProperty(ref _isLoading, value); }
    public bool HasError { get => _hasError; private set => SetProperty(ref _hasError, value); }
    public int Page { get => _page; private set => SetProperty(ref _page, value); }

    public async Task LoadAsync(int page)
    {
        if (_disposed) return;
        if (page is < 1 or > 10000) throw new ArgumentOutOfRangeException(nameof(page));
        var generation = Interlocked.Increment(ref _generation);
        _request?.Cancel();
        _request?.Dispose();
        var request = _request = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        Page = page; Response = null; HasError = false; IsLoading = true;
        try
        {
            var response = await client.LoadAsync(page, request.Token);
            if (!_disposed && generation == _generation) Response = response;
        }
        catch (OperationCanceledException) when (_disposed || generation != _generation) { }
        catch (Exception error)
        {
            if (!_disposed && generation == _generation)
            {
                logger.LogWarning(error, "공개 참여 세부 집계 조회 실패");
                HasError = true;
            }
        }
        finally { if (!_disposed && generation == _generation) IsLoading = false; }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Interlocked.Increment(ref _generation);
        _request?.Cancel(); _request?.Dispose(); _request = null;
    }
}
