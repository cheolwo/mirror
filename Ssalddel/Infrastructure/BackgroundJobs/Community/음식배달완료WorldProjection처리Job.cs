using Quartz;
using Ssalddel.Services.Food;
using 살뜰.Infrastructure.BackgroundJobs.DispatchQueue;

namespace Ssalddel.Infrastructure.BackgroundJobs.Community;

[DisallowConcurrentExecution]
public sealed class 음식배달완료WorldProjection처리Job(
    I음식배달완료WorldProjectionService projectionService,
    Microsoft.Extensions.Options.IOptions<배차큐배치작업Options> options,
    ILogger<음식배달완료WorldProjection처리Job> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var processed = await projectionService.대기항목처리Async(
            options.Value.처리배치크기,
            context.CancellationToken);
        logger.LogDebug(
            "음식 배달 완료 World 상태 사본 처리 완료. 처리={ProcessedCount}",
            processed);
    }
}
