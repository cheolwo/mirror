namespace Hongdal.Extensions;

public static partial class ServiceCollectionExtensions
{
    public static IServiceCollection AddHongdalDomainServices(this IServiceCollection services)
    {
        services.AddHongdalFoundationDomainServices();
        services.AddHongdalCollectiveProcurementDomainServices();
        services.AddHongdalCommunityDomainServices();
        services.AddHongdalOperationsDomainServices();

        return services;
    }
}
