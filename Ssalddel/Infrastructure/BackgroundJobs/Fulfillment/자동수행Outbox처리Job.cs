using Quartz;
using Ssalddel.Services.Fulfillment;

namespace Ssalddel.Infrastructure.BackgroundJobs.Fulfillment;

/// <summary>승인된 자동 수행 요청의 Outbox만 처리합니다. 기본 비활성화이며 Coordinator에서 Simulation을 재검증합니다.</summary>
[DisallowConcurrentExecution]
public sealed class 자동수행Outbox처리Job(I자동수행Coordinator coordinator, ILogger<자동수행Outbox처리Job> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        if (!coordinator.Enabled) return;
        var processed = await coordinator.대기처리Async(100, context.CancellationToken);
        if (processed > 0) logger.LogInformation("자동 수행 Outbox {ProcessedCount}건을 처리했습니다.", processed);
    }
}
