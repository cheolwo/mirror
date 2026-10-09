using Microsoft.Extensions.DependencyInjection.Extensions;
using Ssalddel.Application.Warehouse;
namespace Ssalddel.Extensions;

public static class DistributedFulfillmentRegistration
{
    public static IServiceCollection AddDistributedFulfillment(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<DistributedFulfillmentOptions>(configuration.GetSection(DistributedFulfillmentOptions.SectionName));
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddScoped<IDistributedBuildingCandidateProvider, EmptyDistributedBuildingCandidateProvider>();
        services.AddScoped<I분산배송경로Planner, 분산배송경로Planner>(); services.AddScoped<I분산화물연결Service, 분산화물연결Service>();
        services.AddScoped<DistributedPhotoEvidenceService>();
        services.AddScoped<IDistributedPhotoEvidenceBinder>(s => s.GetRequiredService<DistributedPhotoEvidenceService>());
        services.TryAddScoped<I분산사진보존연결, DistributedPhotoHoldService>();
        services.AddHostedService<DistributedPhotoRetentionWorker>();
        services.AddScoped<I분산주문원장연결Adapter, 분산주문원장연결Adapter>();
        services.AddScoped<IDistributedFulfillmentUseCase, 분산물류UseCase>();
        services.AddNeighborhoodSettlement(configuration);
        return services;
    }
}
