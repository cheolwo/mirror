using Microsoft.Extensions.DependencyInjection.Extensions;
using Ssalddel.Services.Community;

namespace Ssalddel.Extensions;

public static class NeighborhoodMemberDeliveryRegistration
{
    public static IServiceCollection AddSsalddelNeighborhoodMemberDelivery(this IServiceCollection services)
    {
        services.AddOptions<NeighborhoodMemberDeliveryOptions>().BindConfiguration(NeighborhoodMemberDeliveryOptions.SectionName);
        return Register(services);
    }
    public static IServiceCollection AddSsalddelNeighborhoodMemberDelivery(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<NeighborhoodMemberDeliveryOptions>(configuration.GetSection(NeighborhoodMemberDeliveryOptions.SectionName));
        return Register(services);
    }
    private static IServiceCollection Register(IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddScoped<NeighborhoodMemberDeliveryUseCase>();
        services.TryAddScoped<NeighborhoodMemberDeliveryPushOutbox>();
        services.AddHostedService<NeighborhoodMemberDeliveryWorker>();
        return services;
    }
}
