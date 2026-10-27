using Quartz;
using Ssalddel.Application.Admin.Settlement;
using Ssalddel.Infrastructure.BackgroundJobs;

namespace 살뜰.Infrastructure.BackgroundJobs.Payments;

[DisallowConcurrentExecution]
public sealed class 주간정산명세준비Job(
    I배송료주간자동준비Service service,
    ISsalddelBackgroundJobActivationPolicy activationPolicy,
    ILogger<주간정산명세준비Job> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        if (!activationPolicy.Evaluate(SsalddelBackgroundWorkloadKeys.WeeklyStatementPreparation).IsEnabled)
            return;
        var result = await service.준비Async(context.CancellationToken);
        if (result.Enabled)
            logger.LogInformation("WeeklyStatementPreparation Prepared={Prepared} Held={Held} Conflicts={Conflicts} Revalidated={Revalidated} ReviewRequired={ReviewRequired}",
                result.PreparedBatches, result.HeldLines, result.Conflicts, result.RevalidatedBatches, result.ReviewRequiredLines);
    }
}
