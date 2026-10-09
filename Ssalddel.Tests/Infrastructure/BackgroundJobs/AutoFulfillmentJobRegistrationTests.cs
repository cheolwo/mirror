using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Quartz;
using Ssalddel.Extensions;
using Ssalddel.Infrastructure.BackgroundJobs.Fulfillment;
using Ssalddel.Services.LogisticsProcessing.SalesOrders;
using 살뜰.Infrastructure.BackgroundJobs.DispatchQueue;
using 살뜰.Services.Options;

namespace Ssalddel.Tests.Infrastructure.BackgroundJobs;

public sealed class AutoFulfillmentJobRegistrationTests
{
    [Theory]
    [InlineData(SsalddelExecutionMode.Simulation, true)]
    [InlineData(SsalddelExecutionMode.Operational, false)]
    public async Task 자동수행작업은_Simulation에서만등록되고_운영배차작업과분리한다(
        SsalddelExecutionMode mode, bool registered)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSsalddelBackgroundJobs(new 배차큐배치작업Options(),
            new SalesChannelOrderSyncOptions(), new YouTubeOptions(), new HongikHakdangCardOptions(),
            new AgriculturalFisheriesBatchOptions(), new CommunityEditorialBatchOptions(),
            new SsalddelExecutionOptions { Mode = mode });
        services.AddQuartz(q => q.SchedulerName = "AutoFulfillmentRegistration-" + Guid.NewGuid().ToString("N"));
        await using var provider = services.BuildServiceProvider();
        var scheduler = await provider.GetRequiredService<ISchedulerFactory>().GetScheduler();
        try
        {
            var key = new JobKey("AutoFulfillmentOutboxProcess");
            var job = await scheduler.GetJobDetail(key);
            if (!registered)
            {
                Assert.Null(job);
                Assert.Empty(await scheduler.GetTriggersOfJob(key));
                return;
            }
            Assert.NotNull(job);
            Assert.Equal(typeof(자동수행Outbox처리Job), job.JobType);
            var trigger = Assert.IsAssignableFrom<ISimpleTrigger>(Assert.Single(await scheduler.GetTriggersOfJob(key)));
            Assert.Equal(TimeSpan.FromSeconds(10), trigger.RepeatInterval);
            Assert.False(scheduler.IsStarted);
        }
        finally
        {
            await scheduler.Shutdown();
        }
    }
}
