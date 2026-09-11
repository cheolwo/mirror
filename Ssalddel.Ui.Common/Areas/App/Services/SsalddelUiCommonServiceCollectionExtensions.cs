using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Ssalddel.Ui.Common.Areas.App.Services;

public static class SsalddelUiCommonServiceCollectionExtensions
{
    public static IServiceCollection AddSsalddelCommunityWritingServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSsalddelUiCoreModule();
        services.AddCommunityWritingUiModule();
        return services;
    }

    public static IServiceCollection AddSsalddelCommunityWritingServices<TAccessTokenProvider>(
        this IServiceCollection services)
        where TAccessTokenProvider : class, ISsalddelAccessTokenProvider
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<ISsalddelAccessTokenProvider>(provider =>
            provider.GetRequiredService<TAccessTokenProvider>());

        return services.AddSsalddelCommunityWritingServices();
    }

    public static IServiceCollection AddSsalddelUiCommonAppServices(this IServiceCollection services)
        => AddSsalddelUiCommonModules(services);

    public static IServiceCollection AddSsalddelUiCommonAppServices<TAccessTokenProvider>(
        this IServiceCollection services)
        where TAccessTokenProvider : class, ISsalddelAccessTokenProvider
    {
        services.TryAddScoped<ISsalddelAccessTokenProvider>(provider =>
            provider.GetRequiredService<TAccessTokenProvider>());

        return AddSsalddelUiCommonModules(services);
    }

    public static IServiceCollection AddSsalddelApiHttpClient(
        this IServiceCollection services,
        Uri baseAddress,
        ServiceLifetime lifetime = ServiceLifetime.Scoped,
        TimeSpan? timeout = null)
        => services.AddSsalddelOperationalApiHttpClient(
            _ => baseAddress,
            lifetime,
            timeout);

    public static IServiceCollection AddSsalddelApiHttpClient(
        this IServiceCollection services,
        Func<IServiceProvider, Uri> baseAddressFactory,
        ServiceLifetime lifetime = ServiceLifetime.Scoped,
        TimeSpan? timeout = null)
        => services.AddSsalddelOperationalApiHttpClient(
            baseAddressFactory,
            lifetime,
            timeout);

    public static IServiceCollection AddSsalddelOperationalApiHttpClient(
        this IServiceCollection services,
        Uri baseAddress,
        ServiceLifetime lifetime = ServiceLifetime.Scoped,
        TimeSpan? timeout = null)
        => services.AddSsalddelOperationalApiHttpClient(
            _ => baseAddress,
            lifetime,
            timeout);

    public static IServiceCollection AddSsalddelOperationalApiHttpClient(
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

        var hasExistingBareHttpClient = services.Any(descriptor =>
            descriptor.ServiceType == typeof(HttpClient));

        services.AddHttpClient(
            SsalddelHttpClientNames.OperationalApi,
            (provider, client) => ConfigureHttpClient(
                client,
                baseAddressFactory(provider),
                timeout));

        // 기존 ViewModel과 역할별 API Client가 단계적으로 이름 있는 Client로
        // 이관되는 동안 bare HttpClient는 운영 API만 가리키는 호환 조립으로 둔다.
        if (!hasExistingBareHttpClient)
        {
            // AddHttpClient가 추가하는 이름 없는 기본 HttpClient를 제거하고,
            // 기존 소비자가 운영 주소만 받도록 호환 등록을 명시한다.
            services.RemoveAll<HttpClient>();
            services.Add(new ServiceDescriptor(
                typeof(HttpClient),
                provider => provider
                    .GetRequiredService<IHttpClientFactory>()
                    .CreateClient(SsalddelHttpClientNames.OperationalApi),
                lifetime));
        }

        services.TryAddScoped<
            ISsalddelOperationalServerCapabilityClient,
            SsalddelOperationalServerCapabilityClient>();

        return services;
    }

    private static IServiceCollection AddSsalddelUiCommonModules(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSsalddelUiCoreModule();
        services.AddCommunityPlatformUiModule();
        services.AddGroupPurchaseUiModule();
        services.AddSalesUiModule();
        services.AddOrderUiModule();
        services.AddWarehouseUiModule();
        services.AddCustomsUiModule();
        services.AddFoodDiscoveryUiModule();
        services.AddMartDiscoveryUiModule();
        services.AddHumanResourcesUiModule();
        return services;
    }

    private static void ConfigureHttpClient(
        HttpClient client,
        Uri baseAddress,
        TimeSpan? timeout)
    {
        client.BaseAddress =
            SsalddelServerEndpoint.NormalizeBaseAddress(baseAddress);

        if (timeout is { } value)
        {
            client.Timeout = value;
        }
    }
}
