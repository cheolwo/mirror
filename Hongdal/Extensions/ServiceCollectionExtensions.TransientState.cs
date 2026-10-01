using Hongdal.Infrastructure.Storage.Memory;
using Hongdal.Services.Security;
using StackExchange.Redis;
using 홍달.Infrastructure.Storage.Local;
using 홍달.Infrastructure.Storage.Redis;
using 홍달.Services.Options;
using 홍달.Services.Storage.Local;

namespace Hongdal.Extensions;

public static partial class ServiceCollectionExtensions
{
    private static IServiceCollection AddHongdalTransientState(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var options = configuration
            .GetSection(TransientStateOptions.SectionName)
            .Get<TransientStateOptions>() ?? new TransientStateOptions();

        return options.Provider switch
        {
            TransientStateProvider.Memory => services.AddInMemoryTransientState(),
            TransientStateProvider.Redis => services.AddRedisTransientState(configuration),
            _ => throw new InvalidOperationException(
                $"Unsupported {TransientStateOptions.SectionName}:Provider value: {options.Provider}.")
        };
    }

    private static IServiceCollection AddInMemoryTransientState(this IServiceCollection services)
    {
        services.AddSingleton<IDriverLocationStore, InMemoryDriverLocationStore>();
        services.AddSingleton<IDriverWorkQueueStore, InMemoryDriverWorkQueueStore>();
        services.AddSingleton<I국내화물운송기사상태Store, InMemory국내화물운송기사상태Store>();
        services.AddSingleton<IDriverRejectedRequestStore, InMemoryDriverRejectedRequestStore>();
        services.AddSingleton<IDriverPushTokenStore, InMemoryDriverPushTokenStore>();
        services.AddSingleton<I사용자PushTokenStore, InMemory사용자PushTokenStore>();
        services.AddSingleton<IDriverRecommendationPushStateStore, InMemoryDriverRecommendationPushStateStore>();
        services.AddSingleton<IDriverCallScopeStore, InMemoryDriverCallScopeStore>();
        services.AddSingleton<IDriverNotificationSettingsStore, InMemoryDriverNotificationSettingsStore>();
        services.AddSingleton<IIsmsPTransportKeyStatusStore, InMemoryIsmsPTransportKeyStatusStore>();
        return services;
    }

    private static IServiceCollection AddRedisTransientState(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var redisConnectionString = configuration
                                        .GetSection(RedisOptions.SectionName)
                                        .GetValue<string>(nameof(RedisOptions.ConnectionString))
                                    ?? Environment.GetEnvironmentVariable("Redis__ConnectionString");
        if (string.IsNullOrWhiteSpace(redisConnectionString))
        {
            throw new InvalidOperationException(
                "Redis:ConnectionString configuration is required when TransientState:Provider is Redis.");
        }

        var redisConfiguration = ConfigurationOptions.Parse(redisConnectionString);
        redisConfiguration.AbortOnConnectFail = false;
        redisConfiguration.ClientName ??= "Hongdal";

        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConfiguration));
        services.AddSingleton<IDriverLocationStore, DriverLocationStore>();
        services.AddSingleton<IDriverWorkQueueStore, RedisDriverWorkQueueStore>();
        services.AddSingleton<I국내화물운송기사상태Store, Redis국내화물운송기사상태Store>();
        services.AddSingleton<IDriverRejectedRequestStore, RedisDriverRejectedRequestStore>();
        services.AddSingleton<IDriverPushTokenStore, RedisDriverPushTokenStore>();
        services.AddSingleton<I사용자PushTokenStore, Redis사용자PushTokenStore>();
        services.AddSingleton<IDriverRecommendationPushStateStore, RedisDriverRecommendationPushStateStore>();
        services.AddSingleton<IDriverCallScopeStore, RedisDriverCallScopeStore>();
        services.AddSingleton<IDriverNotificationSettingsStore, RedisDriverNotificationSettingsStore>();
        services.AddSingleton<IIsmsPTransportKeyStatusStore, RedisIsmsPTransportKeyStatusStore>();
        return services;
    }
}
