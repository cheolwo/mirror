using Hongdal.Services.Content;
using Microsoft.Extensions.DependencyInjection.Extensions;
using 홍달.Services.Options;

namespace Hongdal.Extensions;

public static partial class ServiceCollectionExtensions
{
    public static IServiceCollection AddYouTubeSocialContextWorkspace(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<AmazonAssociatesOptions>(
            configuration.GetSection(AmazonAssociatesOptions.SectionName));
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IYouTubeSocialContextVideoSource, YouTubeMonitoringSocialContextVideoSource>();
        services.AddScoped<IYouTubeSocialContextPostComposer, YouTubeSocialContextPostComposer>();
        services.AddScoped<IAmazonAssociatesLinkBuilder, AmazonAssociatesLinkBuilder>();
        services.AddScoped<IYouTubeSocialContextResearchService, YouTubeSocialContextResearchService>();
        services.AddScoped<IYouTubeSocialContextWorkspaceStore, MongoYouTubeSocialContextWorkspaceStore>();
        services.AddScoped<IYouTubeSocialContextWorkspaceService, YouTubeSocialContextWorkspaceService>();
        return services;
    }
}
