using Ssalddel.Contracts.Common.Community;

namespace Ssalddel.WebApp.Services;

// 공개 snapshot의 조회·갱신 수명만 소유합니다. 선택과 URL은 페이지가 적용합니다.
internal sealed class 커뮤니티지도SnapshotSession(
    Func<string, CancellationToken, Task<커뮤니티세계지도SnapshotDto>> read,
    Func<string> currentDataset,
    Func<Action, Task> dispatch,
    Action<커뮤니티세계지도SnapshotDto, bool> applySelection,
    Action changed,
    Func<TimeSpan, CancellationToken, Task>? delay = null) : IAsyncDisposable
{
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private CancellationTokenSource? _pollingCts;
    private Task? _pollingTask;

    public 커뮤니티세계지도SnapshotDto? AppliedSnapshot { get; private set; }
    public DateTimeOffset? LastCheckedAt { get; private set; }
    public string? ConnectionMessage { get; private set; }

    public void ResetForDataset()
    {
        AppliedSnapshot = null;
        ConnectionMessage = null;
    }

    public void StartPolling(Func<CancellationToken, Task> refreshPendingLedgers)
    {
        _pollingCts = new CancellationTokenSource();
        _pollingTask = PollMapSnapshotsAsync(refreshPendingLedgers, _pollingCts.Token);
    }

    private async Task PollMapSnapshotsAsync(
        Func<CancellationToken, Task> refreshPendingLedgers,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await RefreshAsync(applyImmediately: false, cancellationToken);
            await refreshPendingLedgers(cancellationToken);
            try
            {
                await (delay?.Invoke(TimeSpan.FromSeconds(30), cancellationToken)
                       ?? Task.Delay(TimeSpan.FromSeconds(30), cancellationToken));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    public async Task RefreshAsync(bool applyImmediately, CancellationToken cancellationToken)
    {
        var requestedDataset = currentDataset();
        try
        {
            await _refreshGate.WaitAsync(cancellationToken);
            try
            {
                var snapshot = await read(requestedDataset, cancellationToken);
                if (!string.Equals(requestedDataset, currentDataset(), StringComparison.Ordinal))
                {
                    return;
                }

                await dispatch(() =>
                {
                    LastCheckedAt = DateTimeOffset.Now;
                    ConnectionMessage = null;
                    if (applyImmediately || AppliedSnapshot is null)
                    {
                        ApplySnapshot(snapshot);
                    }
                    else if (!string.Equals(AppliedSnapshot.Revision, snapshot.Revision, StringComparison.Ordinal))
                    {
                        ApplySnapshot(snapshot, preserveViewport: true);
                    }

                    changed();
                });
            }
            finally
            {
                _refreshGate.Release();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            await dispatch(() =>
            {
                ConnectionMessage = AppliedSnapshot is null
                    ? "서버 snapshot을 가져오지 못해 현재 내장 공개 카탈로그를 표시합니다."
                    : "서버 snapshot을 다시 확인하지 못해 마지막으로 연결된 자료를 유지합니다.";
                changed();
            });
        }
    }

    private void ApplySnapshot(커뮤니티세계지도SnapshotDto snapshot, bool preserveViewport = false)
    {
        AppliedSnapshot = snapshot;
        applySelection(snapshot, preserveViewport);
    }

    public async ValueTask DisposeAsync()
    {
        if (_pollingCts is not null)
        {
            await _pollingCts.CancelAsync();
            if (_pollingTask is not null)
            {
                try
                {
                    await _pollingTask;
                }
                catch (OperationCanceledException)
                {
                }
            }

            _pollingCts.Dispose();
        }
    }
}
