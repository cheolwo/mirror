using Hongdal.Services.External.Apify.SocialMedia;
using Microsoft.Extensions.DependencyInjection.Extensions;
using 홍달.Services.Options;

namespace Hongdal.Extensions;

public static partial class ServiceCollectionExtensions
{
    public static IServiceCollection AddApifySocialMediaResearch(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddApifyPlatform(configuration);
        services.Configure<ApifySocialMediaOptions>(
            configuration.GetSection(ApifySocialMediaOptions.SectionName));
        var socialOptions = configuration
            .GetSection(ApifySocialMediaOptions.SectionName)
            .Get<ApifySocialMediaOptions>() ?? new ApifySocialMediaOptions();
        services.PostConfigure<ApifyOptions>(options =>
        {
            options.AllowedActorIds = (options.AllowedActorIds ?? [])
                .Concat(new[]
                {
                    socialOptions.Reddit.ActorId,
                    socialOptions.X.ActorId,
                    socialOptions.Instagram.ActorId,
                    socialOptions.Facebook.ActorId
                })
                .Where(actorId => !string.IsNullOrWhiteSpace(actorId))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        });

        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<ISocialMediaPublicContentSource, ApifyRedditPublicContentSource>();
        services.AddScoped<ISocialMediaPublicContentSource, ApifyXPublicContentSource>();
        services.AddScoped<ISocialMediaPublicContentSource, ApifyInstagramPublicContentSource>();
        services.AddScoped<ISocialMediaPublicContentSource, ApifyFacebookPublicContentSource>();
        return services;
    }
}
