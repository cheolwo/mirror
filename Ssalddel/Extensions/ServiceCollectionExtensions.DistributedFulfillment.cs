using Microsoft.Extensions.DependencyInjection.Extensions;
using Ssalddel.Application.Warehouse;
namespace Ssalddel.Extensions;

public static class DistributedFulfillmentRegistration
{
    public static IServiceCollection AddDistributedFulfillment(this IServiceCollection services, IConfiguration configuration)
    {
        services.Add분산물류기반Services(configuration);
        services.Add분산사진증빙Services();
        services.AddHostedService<DistributedPhotoRetentionWorker>();
        services.Add분산물류ApplicationServices();
        services.AddNeighborhoodSettlement(configuration);
        return services;
    }

    private static void Add분산물류기반Services(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<DistributedFulfillmentOptions>(configuration.GetSection(DistributedFulfillmentOptions.SectionName));
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddScoped<IDistributedBuildingCandidateProvider, EmptyDistributedBuildingCandidateProvider>();
        services.AddScoped<I분산배송경로Planner, 분산배송경로Planner>(); services.AddScoped<I분산화물연결Service, 분산화물연결Service>();
    }

    private static void Add분산사진증빙Services(this IServiceCollection services)
    {
        services.AddScoped<DistributedPhotoEvidenceService>();
        services.AddScoped<IDistributedPhotoEvidenceBinder>(s => s.GetRequiredService<DistributedPhotoEvidenceService>());
        services.TryAddScoped<I분산사진보존연결, DistributedPhotoHoldService>();
    }

    private static void Add분산물류ApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<I분산주문원장연결Adapter, 분산주문원장연결Adapter>();
        services.AddScoped<IDistributedFulfillmentUseCase, 분산물류UseCase>();
    }
}
