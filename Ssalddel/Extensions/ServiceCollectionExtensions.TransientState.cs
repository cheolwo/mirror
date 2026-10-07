using Ssalddel.Infrastructure.Storage.Memory;
using Ssalddel.Services.Security;
using StackExchange.Redis;
using 살뜰.Infrastructure.Storage.Local;
using 살뜰.Infrastructure.Storage.Redis;
using 살뜰.Services.Options;
using 살뜰.Services.Dispatch.Common;
using 살뜰.Services.Dispatch.Continuity;
using 살뜰.Services.Storage.Local;
using 살뜰.Data;

namespace Ssalddel.Extensions;

public static partial class ServiceCollectionExtensions
{
    private static IServiceCollection AddSsalddelTransientState(
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
        services.AddSingleton<InMemoryDriverPushTokenStore>();
        services.AddScoped<IDriverPushTokenStore>(provider => new AppScopedDriverPushTokenStore(
            provider.GetRequiredService<SsalddelContext>(), provider.GetRequiredService<InMemoryDriverPushTokenStore>()));
        services.AddSingleton<I사용자PushTokenStore, InMemory사용자PushTokenStore>();
        services.AddSingleton<IDriverRecommendationPushStateStore, InMemoryDriverRecommendationPushStateStore>();
        services.AddSingleton<IDriverCallScopeStore, InMemoryDriverCallScopeStore>();
        services.AddSingleton<IDriverNotificationSettingsStore, InMemoryDriverNotificationSettingsStore>();
        services.AddSingleton<IIsmsPTransportKeyStatusStore, InMemoryIsmsPTransportKeyStatusStore>();
        services.AddSingleton<I운영배차판정ProjectionStore, InMemory운영배차판정ProjectionStore>();
        services.AddSingleton<I화물연속배차ProjectionStore, InMemory화물연속배차ProjectionStore>();
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
        redisConfiguration.ClientName ??= "Ssalddel";

        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConfiguration));
        services.AddSingleton<IDriverLocationStore, DriverLocationStore>();
        services.AddSingleton<IDriverWorkQueueStore, RedisDriverWorkQueueStore>();
        services.AddSingleton<I국내화물운송기사상태Store, Redis국내화물운송기사상태Store>();
        services.AddSingleton<IDriverRejectedRequestStore, RedisDriverRejectedRequestStore>();
        services.AddSingleton<RedisDriverPushTokenStore>();
        services.AddScoped<IDriverPushTokenStore>(provider => new AppScopedDriverPushTokenStore(
            provider.GetRequiredService<SsalddelContext>(), provider.GetRequiredService<RedisDriverPushTokenStore>()));
        services.AddSingleton<I사용자PushTokenStore, Redis사용자PushTokenStore>();
        services.AddSingleton<IDriverRecommendationPushStateStore, RedisDriverRecommendationPushStateStore>();
        services.AddSingleton<IDriverCallScopeStore, RedisDriverCallScopeStore>();
        services.AddSingleton<IDriverNotificationSettingsStore, RedisDriverNotificationSettingsStore>();
        services.AddSingleton<IIsmsPTransportKeyStatusStore, RedisIsmsPTransportKeyStatusStore>();
        services.AddSingleton<I운영배차판정ProjectionStore, Redis운영배차판정ProjectionStore>();
        services.AddSingleton<I화물연속배차ProjectionStore, Redis화물연속배차ProjectionStore>();
        return services;
    }
}
