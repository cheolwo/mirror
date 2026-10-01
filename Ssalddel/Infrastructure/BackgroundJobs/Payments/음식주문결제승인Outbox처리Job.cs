using Quartz;
using Microsoft.Extensions.Options;
using 살뜰.Infrastructure.BackgroundJobs.DispatchQueue;
using 살뜰.Services.Payments;

namespace 살뜰.Infrastructure.BackgroundJobs.Payments;

[DisallowConcurrentExecution]
public sealed class 음식주문결제승인Outbox처리Job(
    음식주문결제승인OutboxService service,
    IOptions<배차큐배치작업Options> options) : IJob
{
    public async Task Execute(IJobExecutionContext context)
        => await service.대기승인반영Async(options.Value.처리배치크기, context.CancellationToken);
}
