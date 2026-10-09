using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Ssalddel.Services.Commerce;

namespace Ssalddel.Extensions;

public static partial class ServiceCollectionExtensions
{
    public static IServiceCollection AddNeighborhoodTaskContractPolicy(this IServiceCollection services, IConfiguration configuration)
    {
        // 옵션 배열을 두 번 Bind하면 항목이 누적되므로 설정 등록도 한 번만 수행한다.
        if (services.Any(descriptor => descriptor.ServiceType == typeof(NeighborhoodTaskContractPolicyRegistration)))
            return services;

        services.AddOptions<NeighborhoodTaskContractOptions>()
            .Bind(configuration.GetSection(NeighborhoodTaskContractOptions.SectionName))
            .ValidateOnStart();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<NeighborhoodTaskContractOptions>, NeighborhoodTaskContractOptionsValidator>());
        services.TryAddSingleton<NeighborhoodTaskContractPolicyService>();
        services.AddSingleton(new NeighborhoodTaskContractPolicyRegistration());
        return services;
    }

    private sealed class NeighborhoodTaskContractPolicyRegistration { }
}
