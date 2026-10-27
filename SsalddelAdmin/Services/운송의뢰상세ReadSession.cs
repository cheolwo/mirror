using Ssalddel.Client.Infrastructure.Transport;

namespace SsalddelAdmin.Services;

/// <summary>상세 조회 결과와 관찰 구독·보완 조회의 수명을 소유한다.</summary>
internal sealed class 운송의뢰상세ReadSession(
    Func<string, Task<화주운송의뢰응답?>> read,
    Func<Task<bool>> readWorkflowEnabled,
    ITransportRequestLedgerObserver observer,
    Func<string> currentRequestId,
    Func<bool> isSaving,
    Func<Func<Task>, Task> invokeAsync,
    Action render,
    Action clearMessage) : IDisposable
{
    private CancellationTokenSource? pollingCts;

    public bool IsLoading { get; set; }
    public bool RequiresLogin { get; set; }
    public bool IsMemoryMode { get; set; }
    public bool IsNotFound { get; set; }
    public bool? IsTransportWorkflowEnabled { get; set; }
    public string? LoadedRequestId { get; set; }
    public string? ErrorMessage { get; set; }
    public 화주운송의뢰응답? Request { get; set; }

    public async Task LoadAsync(bool showMessage)
    {
        if (IsLoading || isSaving() || IsTransportWorkflowEnabled != true)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(currentRequestId()))
        {
            Request = null;
            IsNotFound = true;
            return;
        }

        IsLoading = true;
        IsNotFound = false;
        ErrorMessage = null;
        var previousRequest = Request;
        if (showMessage)
        {
            clearMessage();
        }

        try
        {
            Request = await read(currentRequestId());
            IsNotFound = Request is null;
        }
        catch (Exception ex)
        {
            Request = showMessage ? null : previousRequest;
            RequiresLogin = IsAuthenticationFailure(ex);
            if (ex is HttpRequestException { StatusCode: System.Net.HttpStatusCode.NotFound })
            {
                Request = null;
                IsNotFound = true;
            }
            else if (IsFeatureDisabledFailure(ex))
            {
                IsTransportWorkflowEnabled = false;
            }
            else if (!RequiresLogin && showMessage)
            {
                ErrorMessage = ResolveLoadFailureMessage(ex);
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task<bool> EnsureTransportWorkflowEnabledAsync()
    {
        if (IsMemoryMode)
        {
            IsTransportWorkflowEnabled = true;
            return true;
        }

        if (IsTransportWorkflowEnabled.HasValue)
        {
            return IsTransportWorkflowEnabled.Value;
        }

        try
        {
            IsTransportWorkflowEnabled = await readWorkflowEnabled();
            return IsTransportWorkflowEnabled.Value;
        }
        catch (Exception)
        {
            IsTransportWorkflowEnabled = null;
            ErrorMessage = "국내 운송 의뢰 상세 조회 가능 여부를 확인하지 못했습니다. 잠시 뒤 다시 시도해 주세요.";
            return false;
        }
    }

    public void EnsureObserverStarted()
    {
        if (pollingCts is not null)
        {
            return;
        }

        observer.Changed += OnLedgerChanged;
        observer.RefreshRequested += OnLedgerRefreshRequested;
        pollingCts = new CancellationTokenSource();
        _ = RunPollingAsync(pollingCts.Token);
    }

    private async Task RunPollingAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(
            TransportRequestLedgerRefreshPolicy.FallbackPollingInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await PollOnceAsync();
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    internal async Task PollOnceAsync()
    {
        if (string.IsNullOrWhiteSpace(currentRequestId())
            || IsLoading
            || isSaving()
            || RequiresLogin
            || IsTransportWorkflowEnabled != true)
        {
            return;
        }

        await invokeAsync(async () =>
        {
            await LoadAsync(showMessage: false);
            render();
        });
    }

    private void OnLedgerChanged(TransportRequestLedgerChange change)
    {
        if (!IsCurrentRequest(change.RequestId))
        {
            return;
        }

        _ = invokeAsync(async () =>
        {
            await LoadAsync(showMessage: false);
            render();
        });
    }

    private void OnLedgerRefreshRequested(TransportRequestLedgerRefreshRequest refreshRequest)
    {
        if (!IsCurrentRequest(refreshRequest.RequestId))
        {
            return;
        }

        _ = invokeAsync(async () =>
        {
            await LoadAsync(showMessage: false);
            render();
        });
    }

    private bool IsCurrentRequest(string requestId)
        => !string.IsNullOrWhiteSpace(currentRequestId())
           && string.Equals(currentRequestId().Trim(), requestId.Trim(), StringComparison.OrdinalIgnoreCase);

    public void Dispose()
    {
        observer.Changed -= OnLedgerChanged;
        observer.RefreshRequested -= OnLedgerRefreshRequested;
        pollingCts?.Cancel();
        pollingCts?.Dispose();
    }

    internal static bool IsAuthenticationFailure(Exception exception)
        => exception is HttpRequestException
           {
               StatusCode: System.Net.HttpStatusCode.Unauthorized
                   or System.Net.HttpStatusCode.Forbidden
           }
           || exception.Message.Contains("로그인이 필요", StringComparison.Ordinal);

    private static bool IsFeatureDisabledFailure(Exception exception)
        => exception.Message.Contains("FeatureDisabled", StringComparison.OrdinalIgnoreCase);

    private static string ResolveLoadFailureMessage(Exception exception)
        => exception is HttpRequestException { StatusCode: System.Net.HttpStatusCode.NotFound }
            ? "운송 의뢰 상세 API를 찾을 수 없습니다. 현재 배포 버전과 기능 상태를 확인해 주세요."
            : "운송 의뢰 상세를 불러오지 못했습니다. 잠시 뒤 다시 시도해 주세요.";
}
