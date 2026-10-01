using Hongdal.Services.External.Apify;
using Microsoft.Extensions.Options;
using 홍달.Services.Options;

namespace Hongdal.Extensions;

public static partial class ServiceCollectionExtensions
{
    public static IServiceCollection AddApifyPlatform(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        if (services.Any(descriptor =>
                descriptor.ServiceType == typeof(ApifyPlatformRegistrationMarker)))
        {
            return services;
        }

        services.AddSingleton<ApifyPlatformRegistrationMarker>();
        services.Configure<ApifyOptions>(configuration.GetSection(ApifyOptions.SectionName));
        services.AddHttpClient<IApifyActorGateway, ApifyActorGateway>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<ApifyOptions>>().Value;
            client.BaseAddress = new Uri($"{options.BaseUrl.TrimEnd('/')}/");
            client.Timeout = TimeSpan.FromSeconds(Math.Clamp(options.TimeoutSeconds, 30, 300));
        });
        return services;
    }

    private sealed class ApifyPlatformRegistrationMarker
    {
    }
}
