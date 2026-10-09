using Microsoft.Extensions.DependencyInjection.Extensions;
using Ssalddel.Application.Settlement;

namespace Ssalddel.Extensions;

public static class NeighborhoodSettlementRegistration
{
    public static IServiceCollection AddNeighborhoodSettlement(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<NeighborhoodSettlementOptions>(configuration.GetSection(NeighborhoodSettlementOptions.SectionName));
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddScoped<INeighborhoodSettlementSourceResolver, NeighborhoodSettlementSourceResolver>();
        services.TryAddScoped<INeighborhoodSettlementUseCase, NeighborhoodSettlementUseCase>();
        return services;
    }
}
