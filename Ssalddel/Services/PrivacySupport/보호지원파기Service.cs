using Microsoft.Extensions.Options;

namespace Ssalddel.Services.PrivacySupport;

public sealed class 보호지원파기Service(I보호지원Store store, IOptions<보호지원Options> options, TimeProvider clock)
{
    // 내부 보수 기준입니다. 모든 법정 분쟁 기록의 기산점이 폐쇄일이라고 확정하는 상수는 아닙니다.
    public const string DisputeRetentionPolicyVersion = "internal-closed-dispute-three-years.r1";

    public async Task<int> 실행Async(CancellationToken ct = default)
    {
        var policy = options.Value;
        if (!policy.ClosedDisputePurgeEnabled || !policy.ClosedDisputeRetentionPolicyConfirmed
            || policy.ClosedDisputeRetentionPolicyVersion != DisputeRetentionPolicyVersion) return 0;
        var now = clock.GetUtcNow().UtcDateTime;
        var deleted = 0;
        foreach (var row in await store.파기후보Async(now, 100, ct))
        {
            if (row.Kind != "transaction-dispute" || row.StatusCode != "closed" || row.SourceHoldActive || row.RetentionIntent is not null
                || row.RetentionPolicyVersion != DisputeRetentionPolicyVersion || row.PurgeAfterAtUtc is null || row.PurgeAfterAtUtc > now) continue;
            if (row.PurgeStateCode != "claimed")
            {
                var expected = row.Revision++;
                row.PurgeStateCode = "claimed";
                if (!await store.교체Async(row, expected, ct)) continue;
            }
            if (await store.파기Async(row, now, ct)) deleted++;
        }
        return deleted;
    }
}

public sealed class 보호지원파기Worker(IServiceScopeFactory scopes, IOptions<보호지원Options> options,
    ILogger<보호지원파기Worker> logger, 살뜰.Services.Options.ISsalddelExecutionModePolicy executionMode) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (executionMode.IsOperational && options.Value.ClosedDisputePurgeEnabled)
            {
                try
                {
                    using var scope = scopes.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<보호지원파기Service>().실행Async(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception ex) { logger.LogError("지원 사건 파기를 완료하지 못했습니다. 오류 종류: {ErrorType}", ex.GetType().Name); }
            }
            try { await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}
