using Hongdal.Extensions;
using Hongdal.Infrastructure.Storage.Memory;
using Hongdal.Services.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using 홍달.Services.Storage.Local;

namespace Hongdal.Tests.Infrastructure.Storage;

public sealed class TransientStateRegistrationTests
{
    [Fact]
    public void MemoryProvider_DoesNotRequireRedisConnection()
    {
        var configuration = BuildConfiguration("Memory");
        var services = new ServiceCollection();
        services.AddHongdalOptions(configuration);
        services.AddHongdalPersistence(configuration);

        using var provider = services.BuildServiceProvider();

        Assert.IsType<InMemoryDriverLocationStore>(provider.GetRequiredService<IDriverLocationStore>());
        Assert.IsType<InMemoryDriverWorkQueueStore>(provider.GetRequiredService<IDriverWorkQueueStore>());
        Assert.IsType<InMemoryIsmsPTransportKeyStatusStore>(provider.GetRequiredService<IIsmsPTransportKeyStatusStore>());
        Assert.Null(provider.GetService<IConnectionMultiplexer>());
    }

    [Fact]
    public void RedisProvider_RequiresRedisConnectionString()
    {
        var configuration = BuildConfiguration("Redis");
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddHongdalPersistence(configuration));

        Assert.Contains("Redis:ConnectionString", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MemoryProvider_PreservesQueueOrderingAndRejectionIndexes()
    {
        var queue = new InMemoryDriverWorkQueueStore();
        await queue.UpsertAsync(new DriverWorkQueueEntry(
            "driver-later",
            2,
            new DateTime(2026, 7, 17, 2, 0, 0, DateTimeKind.Utc),
            "Manual",
            "Seoul",
            null));
        await queue.UpsertAsync(new DriverWorkQueueEntry(
            "driver-first",
            1,
            new DateTime(2026, 7, 17, 1, 0, 0, DateTimeKind.Utc),
            "Manual",
            "Busan",
            null));

        var snapshot = await queue.SnapshotAsync();

        Assert.Equal(["driver-first", "driver-later"], snapshot.Select(item => item.DriverId));

        var rejections = new InMemoryDriverRejectedRequestStore();
        await rejections.RejectAsync("driver-first", "request-1");

        Assert.True(await rejections.IsRejectedAsync("driver-first", "request-1"));
        Assert.Contains("request-1", await rejections.GetRejectedRequestIdsAsync("driver-first"));
        Assert.Contains("driver-first", await rejections.GetRejectedDriverIdsAsync("request-1"));
    }

    private static IConfiguration BuildConfiguration(string provider)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "server=127.0.0.1;port=13306;database=hongdal_test;user=hongdal;password=test;",
                ["TransientState:Provider"] = provider,
                ["MongoDb:ConnectionString"] = "mongodb://127.0.0.1:27017",
                ["MongoDb:Database"] = "hongdal_test"
            })
            .Build();
}
