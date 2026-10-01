using Hongdal.Services.Content;
using Hongdal.Services.External.Apify;
using Microsoft.Extensions.Options;
using 홍달.Services.Options;

namespace Hongdal.Extensions;

public static partial class ServiceCollectionExtensions
{
    public static IServiceCollection AddApifyAmazonProductResearch(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddApifyPlatform(configuration);
        services.Configure<ApifyAmazonOptions>(
            configuration.GetSection(ApifyAmazonOptions.SectionName));

        var apifySection = configuration.GetSection(ApifyOptions.SectionName);
        var legacy = configuration
            .GetSection(ApifyAmazonOptions.SectionName)
            .Get<ApifyAmazonOptions>() ?? new ApifyAmazonOptions();
        services.PostConfigure<ApifyOptions>(options =>
        {
            if (apifySection[nameof(ApifyOptions.Enabled)] is null)
            {
                options.Enabled = legacy.Enabled;
            }

            if (apifySection[nameof(ApifyOptions.ApiToken)] is null)
            {
                options.ApiToken = legacy.ApiToken;
            }

            if (apifySection[nameof(ApifyOptions.BaseUrl)] is null)
            {
                options.BaseUrl = legacy.BaseUrl;
            }

            if (apifySection[nameof(ApifyOptions.TimeoutSeconds)] is null)
            {
                options.TimeoutSeconds = legacy.TimeoutSeconds;
            }

            options.AllowedActorIds = (options.AllowedActorIds ?? [])
                .Append(legacy.ActorId)
                .Where(actorId => !string.IsNullOrWhiteSpace(actorId))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        });

        services.AddScoped<IApifyAmazonProductClient, ApifyAmazonProductClient>();
        services.AddScoped<IAmazon상품참고자료Service, Amazon상품참고자료Service>();
        return services;
    }
}
