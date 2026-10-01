using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hongdal.Ui.Common.Areas.App.Services;

public static class HongdalUiCommonServiceCollectionExtensions
{
    public static IServiceCollection AddHongdalCommunityWritingServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddHongdalUiCoreModule();
        services.AddCommunityWritingUiModule();
        return services;
    }

    public static IServiceCollection AddHongdalCommunityWritingServices<TAccessTokenProvider>(
        this IServiceCollection services)
        where TAccessTokenProvider : class, IHongdalAccessTokenProvider
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<IHongdalAccessTokenProvider>(provider =>
            provider.GetRequiredService<TAccessTokenProvider>());

        return services.AddHongdalCommunityWritingServices();
    }

    public static IServiceCollection AddHongdalUiCommonAppServices(this IServiceCollection services)
        => AddHongdalUiCommonModules(services);

    public static IServiceCollection AddHongdalUiCommonAppServices<TAccessTokenProvider>(
        this IServiceCollection services)
        where TAccessTokenProvider : class, IHongdalAccessTokenProvider
    {
        services.TryAddScoped<IHongdalAccessTokenProvider>(provider =>
            provider.GetRequiredService<TAccessTokenProvider>());

        return AddHongdalUiCommonModules(services);
    }

    public static IServiceCollection AddHongdalApiHttpClient(
        this IServiceCollection services,
        Uri baseAddress,
        ServiceLifetime lifetime = ServiceLifetime.Scoped,
        TimeSpan? timeout = null)
        => services.AddHongdalApiHttpClient(_ => baseAddress, lifetime, timeout);

    public static IServiceCollection AddHongdalApiHttpClient(
        this IServiceCollection services,
        Func<IServiceProvider, Uri> baseAddressFactory,
        ServiceLifetime lifetime = ServiceLifetime.Scoped,
        TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(baseAddressFactory);

        if (timeout is { } value
            && value != Timeout.InfiniteTimeSpan
            && value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }

        services.TryAdd(new ServiceDescriptor(
            typeof(HttpClient),
            provider => CreateHttpClient(baseAddressFactory(provider), timeout),
            lifetime));

        return services;
    }

    private static IServiceCollection AddHongdalUiCommonModules(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddHongdalUiCoreModule();
        services.AddCommunityPlatformUiModule();
        services.AddGroupPurchaseUiModule();
        services.AddSalesUiModule();
        services.AddOrderUiModule();
        services.AddWarehouseUiModule();
        return services;
    }

    private static HttpClient CreateHttpClient(Uri baseAddress, TimeSpan? timeout)
    {
        var client = new HttpClient
        {
            BaseAddress = HongdalApiEndpoint.NormalizeBaseAddress(baseAddress)
        };

        if (timeout is { } value)
        {
            client.Timeout = value;
        }

        return client;
    }
}
