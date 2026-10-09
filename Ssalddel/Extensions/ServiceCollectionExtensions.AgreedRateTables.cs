using Ssalddel.Application.Pricing;

namespace Ssalddel.Extensions;

public static partial class ServiceCollectionExtensions
{
    public static IServiceCollection AddSsalddelAgreedRateTables(this IServiceCollection services)
    {
        services.AddScoped<IAgreedRateTableUseCase, AgreedRateTableUseCase>();
        return services;
    }
}
