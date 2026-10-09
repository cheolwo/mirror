namespace Ssalddel.Extensions;

public static partial class ServiceCollectionExtensions
{
    public static IServiceCollection AddSsalddelDomainServices(this IServiceCollection services)
    {
        services.AddSsalddelHsCodeCatalog();
        services.AddSsalddelFoundationDomainServices();
        services.AddSsalddelCollectiveProcurementDomainServices();
        services.AddSsalddelCommunityDomainServices();
        services.AddSsalddelOperationsDomainServices();
        services.AddSsalddelNeighborhoodMemberDelivery();
        services.AddSsalddelRegionalCultureDomainServices();

        return services;
    }
}
