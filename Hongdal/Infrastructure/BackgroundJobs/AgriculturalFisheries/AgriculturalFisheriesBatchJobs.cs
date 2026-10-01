using Microsoft.Extensions.Options;
using Quartz;
using 홍달.Services.Options;

namespace Hongdal.Infrastructure.BackgroundJobs.AgriculturalFisheries;

[DisallowConcurrentExecution]
public sealed class KamisDailyPriceCollectionJob : IJob
{
    private readonly AgriculturalFisheriesBatchRunner _runner;
    private readonly AgriculturalFisheriesBatchOptions _options;
    private readonly ILogger<KamisDailyPriceCollectionJob> _logger;

    public KamisDailyPriceCollectionJob(
        AgriculturalFisheriesBatchRunner runner,
        IOptions<AgriculturalFisheriesBatchOptions> options,
        ILogger<KamisDailyPriceCollectionJob> logger)
    {
        _runner = runner;
        _options = options.Value;
        _logger = logger;
    }

    public Task Execute(IJobExecutionContext context)
        => AgriculturalFisheriesBatchJobExecution.RunAsync(
            "KamisDailyPriceCollection",
            _runner.RunKamisDailyAsync,
            context,
            _options,
            _logger);
}

[DisallowConcurrentExecution]
public sealed class KamisMonthlyPriceCollectionJob : IJob
{
    private readonly AgriculturalFisheriesBatchRunner _runner;
    private readonly AgriculturalFisheriesBatchOptions _options;
    private readonly ILogger<KamisMonthlyPriceCollectionJob> _logger;

    public KamisMonthlyPriceCollectionJob(
        AgriculturalFisheriesBatchRunner runner,
        IOptions<AgriculturalFisheriesBatchOptions> options,
        ILogger<KamisMonthlyPriceCollectionJob> logger)
    {
        _runner = runner;
        _options = options.Value;
        _logger = logger;
    }

    public Task Execute(IJobExecutionContext context)
        => AgriculturalFisheriesBatchJobExecution.RunAsync(
            "KamisMonthlyPriceCollection",
            _runner.RunKamisMonthlyAsync,
            context,
            _options,
            _logger);
}

[DisallowConcurrentExecution]
public sealed class UsdaMonthlyPriceCollectionJob : IJob
{
    private readonly AgriculturalFisheriesBatchRunner _runner;
    private readonly AgriculturalFisheriesBatchOptions _options;
    private readonly ILogger<UsdaMonthlyPriceCollectionJob> _logger;

    public UsdaMonthlyPriceCollectionJob(
        AgriculturalFisheriesBatchRunner runner,
        IOptions<AgriculturalFisheriesBatchOptions> options,
        ILogger<UsdaMonthlyPriceCollectionJob> logger)
    {
        _runner = runner;
        _options = options.Value;
        _logger = logger;
    }

    public Task Execute(IJobExecutionContext context)
        => AgriculturalFisheriesBatchJobExecution.RunAsync(
            "UsdaMonthlyPriceCollection",
            _runner.RunUsdaMonthlyAsync,
            context,
            _options,
            _logger);
}

internal static class AgriculturalFisheriesBatchJobExecution
{
    internal static async Task RunAsync(
        string jobName,
        Func<CancellationToken, Task> action,
        IJobExecutionContext context,
        AgriculturalFisheriesBatchOptions options,
        ILogger logger)
    {
        try
        {
            await action(context.CancellationToken);
        }
        catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var retryLimit = Math.Clamp(options.ImmediateRetryCount, 0, 3);
            var shouldRetry = ShouldRetry(context.RefireCount, retryLimit);
            logger.LogError(
                exception,
                "Action={Action} RefireCount={RefireCount} RetryLimit={RetryLimit} WillRetry={WillRetry}",
                jobName,
                context.RefireCount,
                retryLimit,
                shouldRetry);
            throw new JobExecutionException(exception, shouldRetry);
        }
    }

    internal static bool ShouldRetry(int refireCount, int retryLimit)
        => refireCount < Math.Clamp(retryLimit, 0, 3);
}
