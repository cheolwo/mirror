using Hongdal.Services.External.Apify.YouTube;
using Hongdal.Services.External.YouTube;
using Microsoft.Extensions.DependencyInjection.Extensions;
using 홍달.Services.Options;

namespace Hongdal.Extensions;

public static partial class ServiceCollectionExtensions
{
    public static IServiceCollection AddApifyYouTubeTranscriptResearch(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddApifyPlatform(configuration);
        services.Configure<ApifyYouTubeTranscriptOptions>(
            configuration.GetSection(ApifyYouTubeTranscriptOptions.SectionName));

        var transcriptOptions = configuration
            .GetSection(ApifyYouTubeTranscriptOptions.SectionName)
            .Get<ApifyYouTubeTranscriptOptions>()
            ?? new ApifyYouTubeTranscriptOptions();
        services.PostConfigure<ApifyOptions>(options =>
        {
            options.AllowedActorIds = (options.AllowedActorIds ?? [])
                .Append(transcriptOptions.ActorId)
                .Where(actorId => !string.IsNullOrWhiteSpace(actorId))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        });

        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IYouTubeTranscriptSource, ApifyYouTubeTranscriptSource>();
        return services;
    }
}
