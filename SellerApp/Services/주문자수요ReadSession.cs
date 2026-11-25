using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Orderer;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace SellerApp.Services;

/// <summary>수요 허브의 응답과 현재 순서의 부분 반영, 보완 조회 수명을 소유한다.</summary>
internal sealed class 주문자수요ReadSession : IAsyncDisposable
{
    private readonly CancellationTokenSource monitorCancellation = new();
    private long generation;
    private bool disposed;

    public 개별주문관점페이지응답 IndividualOrders { get; private set; } = new();
    public 공동주문관점페이지응답 GroupOrders { get; private set; } = new();
    public IReadOnlyList<공동구매자동집단응답> ClusteredDemands { get; private set; } = [];
    public 판매자수출원장목록응답 ExportLedgers { get; private set; } = new();
    public string? ErrorMessage { get; private set; }
    public string? ClusteredDemandUnavailableMessage { get; private set; }
    public string? ExportUnavailableMessage { get; private set; }
    public bool Loading { get; private set; }

    public async Task LoadAsync(
        Func<string, Task<개별주문관점페이지응답>> readIndividual,
        Func<string, Task<공동주문관점페이지응답>> readGroup,
        Func<Task<IReadOnlyList<공동구매자동집단응답>>> readClustered,
        Func<string, Task<판매자수출원장목록응답>> readExport,
        Func<string> search)
    {
        if (disposed) return;
        var request = Interlocked.Increment(ref generation);
        Loading = true;
        ErrorMessage = null;
        ClusteredDemandUnavailableMessage = null;
        ExportUnavailableMessage = null;
        try
        {
            var query = search();
            var individualTask = readIndividual(query);
            _ = ObserveAsync(individualTask);
            if (!Current(request)) return;
            var groupRead = readGroup(query);
            _ = ObserveAsync(groupRead);

            var individual = await individualTask;
            if (!Current(request)) return;
            IndividualOrders = individual;
            var group = await groupRead;
            if (!Current(request)) return;
            GroupOrders = group;
            try
            {
                var clustered = await readClustered();
                if (!Current(request)) return;
                ClusteredDemands = clustered;
            }
            catch (SsalddelApiException exception) when (exception.StatusCode is 403 or 404)
            {
                if (!Current(request)) return;
                ClusteredDemands = [];
                ClusteredDemandUnavailableMessage =
                    "집단 수요는 1.0 같이 주문·주문자 집단화 기능이 활성화된 환경에서 확인할 수 있습니다.";
            }
            try
            {
                var export = await readExport(query);
                if (!Current(request)) return;
                ExportLedgers = export;
            }
            catch (SsalddelApiException exception) when (exception.StatusCode is 403 or 404)
            {
                if (!Current(request)) return;
                ExportLedgers = new();
                ExportUnavailableMessage = "수출 판매는 1.5 무역 준비 기능과 판매자 권한이 활성화된 환경에서 확인할 수 있습니다.";
            }
        }
        catch (Exception exception)
        {
            if (Current(request)) ErrorMessage = exception.Message;
        }
        finally
        {
            if (Current(request)) Loading = false;
        }
    }

    private bool Current(long request) => !disposed && request == Volatile.Read(ref generation);

    private static async Task ObserveAsync(Task task)
    {
        // 다른 조회가 먼저 실패하거나 교체돼도 이미 시작한 병렬 조회의 실패는 관측합니다.
        try { await task; }
        catch (Exception) { }
    }

    public Task MonitorAsync(Func<Func<Task>, Task> invokeAsync, Func<Task> refresh)
        => disposed ? Task.CompletedTask : RunMonitorAsync(monitorCancellation.Token, invokeAsync, refresh);

    private static async Task RunMonitorAsync(
        CancellationToken cancellationToken,
        Func<Func<Task>, Task> invokeAsync,
        Func<Task> refresh)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await invokeAsync(refresh);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    public ValueTask DisposeAsync()
    {
        if (disposed) return ValueTask.CompletedTask;
        disposed = true;
        Interlocked.Increment(ref generation);
        Loading = false;
        monitorCancellation.Cancel();
        monitorCancellation.Dispose();
        return ValueTask.CompletedTask;
    }
}
