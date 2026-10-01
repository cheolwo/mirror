using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HongdalApp.Services;

public static class ShipperServiceCollectionExtensions
{
    public static IServiceCollection AddHongdalAppServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddShipperOptionsModule(configuration);
        services.AddShipperPlatformModule();
        services.AddShipperTransportModule();
        services.AddShipperWarehouseModule();
        services.AddShipperSalesModule();
        services.AddShipperCustomsModule();
        services.AddShipperExternalApiModule();
        return services;
    }
}
