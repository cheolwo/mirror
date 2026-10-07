using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Quartz;
using Ssalddel.Extensions;
using Ssalddel.Services.LogisticsProcessing.SalesOrders;
using 살뜰.Infrastructure.BackgroundJobs.DispatchQueue;
using 살뜰.Infrastructure.BackgroundJobs.FoodDeliveryDispatch;
using 살뜰.Services.Options;

namespace Ssalddel.Tests.Infrastructure.BackgroundJobs;

public sealed class FoodCargoDispatchRegistrationTests
{
    [Fact]
    public void 음식설정과화물설정은_각자구역에서읽는다()
    {
        var configuration = Configuration(new()
        {
            ["DispatchQueueJobs:큐스캔주기초"] = "71",
            ["DispatchQueueJobs:처리배치크기"] = "3",
            ["FoodDeliveryDispatchJobs:큐스캔주기초"] = "13",
            ["FoodDeliveryDispatchJobs:처리배치크기"] = "7",
            ["FoodDeliveryDispatchJobs:후보재탐색간격초"] = "11"
        });
        using var provider = new ServiceCollection().AddSsalddelOptions(configuration).BuildServiceProvider();

        var food = provider.GetRequiredService<IOptions<음식배달배차배치작업Options>>().Value;
        var cargo = provider.GetRequiredService<IOptions<배차큐배치작업Options>>().Value;
        Assert.Equal(13, food.큐스캔주기초);
        Assert.Equal(7, food.처리배치크기);
        Assert.Equal(11, food.후보재탐색간격초);
        Assert.Equal(71, cargo.큐스캔주기초);
        Assert.Equal(3, cargo.처리배치크기);
        Assert.Equal(food.큐스캔주기초, configuration.GetFoodDeliveryDispatchJobOptions().큐스캔주기초);
    }

    [Theory]
    [InlineData(null, 43)]
    [InlineData("17", 17)]
    public void 과거음식재탐색설정만_호환하고_새음식설정을우선한다(string? newValue, int expected)
    {
        var values = new Dictionary<string, string?>
        {
            ["DispatchQueue:음식배달후보재탐색간격초"] = "43",
            ["DispatchQueueJobs:큐스캔주기초"] = "99",
            ["DispatchQueueJobs:처리배치크기"] = "2"
        };
        if (newValue is not null) values["FoodDeliveryDispatchJobs:후보재탐색간격초"] = newValue;
        var configuration = Configuration(values);
        using var provider = new ServiceCollection().AddSsalddelOptions(configuration).BuildServiceProvider();
        var runtime = provider.GetRequiredService<IOptions<음식배달배차배치작업Options>>().Value;
        var registration = configuration.GetFoodDeliveryDispatchJobOptions();
        Assert.Equal(expected, runtime.후보재탐색간격초);
        Assert.Equal(expected, registration.후보재탐색간격초);
        Assert.Equal(30, runtime.큐스캔주기초);
        Assert.Equal(100, runtime.처리배치크기);
    }

    [Theory]
    [InlineData(SsalddelExecutionMode.Operational, true)]
    [InlineData(SsalddelExecutionMode.Simulation, false)]
    public async Task 두업무는_별도Quartz작업과주기로등록하고_Simulation에는등록하지않는다(
        SsalddelExecutionMode mode, bool registered)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        // Quartz의 프로세스 공용 logging provider가 이전 시험의 폐기된 factory를 보관하지 않게 한다.
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSsalddelBackgroundJobs(
            new 배차큐배치작업Options { 큐스캔주기초 = 61, 추천만료정리주기초 = 62, 알림발송주기초 = 63 },
            new SalesChannelOrderSyncOptions(), new YouTubeOptions(), new HongikHakdangCardOptions(),
            new AgriculturalFisheriesBatchOptions(), new CommunityEditorialBatchOptions(),
            new SsalddelExecutionOptions { Mode = mode },
            new 음식배달배차배치작업Options { 큐스캔주기초 = 11, 추천만료정리주기초 = 12, 알림발송주기초 = 13 });
        // 시작하지 않는 독립 scheduler에서 등록만 조회한다. 실제 배치나 외부 발송은 실행하지 않는다.
        services.AddQuartz(q => q.SchedulerName = "FoodCargoRegistration-" + Guid.NewGuid().ToString("N"));
        await using var provider = services.BuildServiceProvider();
        var scheduler = await provider.GetRequiredService<ISchedulerFactory>().GetScheduler();
        try
        {
            await AssertJob<음식배달배차큐스캔Job>(scheduler, "FoodDeliveryDispatchQueueScan", 11, registered);
            await AssertJob<음식배달추천만료정리Job>(scheduler, "FoodDeliveryRecommendationExpire", 12, registered);
            await AssertJob<음식배달추천알림발송Job>(scheduler, "FoodDeliveryRecommendationPush", 13, registered);
            await AssertJob<배차큐스캔Job>(scheduler, "DispatchQueueScan", 61, registered);
            await AssertJob<추천만료정리Job>(scheduler, "DispatchRecommendationExpire", 62, registered);
            await AssertJob<배차추천알림발송Job>(scheduler, "DispatchRecommendationPush", 63, registered);
        }
        finally
        {
            await scheduler.Shutdown();
        }
    }

    private static async Task AssertJob<T>(IScheduler scheduler, string name, int intervalSeconds, bool registered)
    {
        var detail = await scheduler.GetJobDetail(new JobKey(name));
        if (!registered)
        {
            Assert.Null(detail);
            Assert.Empty(await scheduler.GetTriggersOfJob(new JobKey(name)));
            return;
        }
        Assert.NotNull(detail);
        Assert.Equal(typeof(T), detail.JobType);
        var trigger = Assert.IsAssignableFrom<ISimpleTrigger>(Assert.Single(await scheduler.GetTriggersOfJob(detail.Key)));
        Assert.Equal(name + "-trigger", trigger.Key.Name);
        Assert.Equal(TimeSpan.FromSeconds(intervalSeconds), trigger.RepeatInterval);
    }

    private static IConfiguration Configuration(Dictionary<string, string?> values)
        => new ConfigurationBuilder().AddInMemoryCollection(values).Build();
}
