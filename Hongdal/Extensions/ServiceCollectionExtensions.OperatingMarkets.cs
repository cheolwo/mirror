using Hongdal.Application.Operations;
using Hongdal.Contracts.Common.Operations;
using Hongdal.Services.Operations;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Hongdal.Extensions;

public static partial class ServiceCollectionExtensions
{
    public static IServiceCollection AddHongdalOperatingMarketServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var deploymentOptions = configuration
            .GetSection(OperatingMarketDeploymentOptions.SectionName)
            .Get<OperatingMarketDeploymentOptions>()
            ?? new OperatingMarketDeploymentOptions();
        var configuredMarketCode = deploymentOptions.MarketCode;
        configuredMarketCode = string.IsNullOrWhiteSpace(configuredMarketCode)
            ? OperatingMarketCodes.Korea
            : configuredMarketCode;

        if (!OperatingMarketCodes.TryNormalize(configuredMarketCode, out var marketCode))
        {
            throw new InvalidOperationException(
                $"{OperatingMarketDeploymentOptions.SectionName}:MarketCode must be KR or US.");
        }

        deploymentOptions.FreightServiceProvider ??=
            new OperatingMarketFreightServiceProviderOptions();
        if (string.IsNullOrWhiteSpace(
                deploymentOptions.FreightServiceProvider.ParticipantId))
        {
            deploymentOptions.FreightServiceProvider.ParticipantId =
                deploymentOptions.VerifiedLicensedBrokerPartnerId;
        }

        var deployment = new OperatingMarketDeployment(
            marketCode,
            deploymentOptions.FreightServiceProvider.ParticipantId,
            deploymentOptions.TimeZoneId);
        var freightServiceProviderRegistry =
            new DeploymentOperatingMarketFreightServiceProviderRegistry(
                marketCode,
                deploymentOptions.FreightServiceProvider);
        IOperatingMarketServiceModule module = marketCode switch
        {
            OperatingMarketCodes.Korea => new KoreaOperatingMarketServiceModule(),
            OperatingMarketCodes.UnitedStates => new UnitedStatesOperatingMarketServiceModule(),
            _ => throw new InvalidOperationException(
                $"No service module exists for operating market {marketCode}.")
        };

        services.AddSingleton<IOperatingMarketDeployment>(deployment);
        services.AddSingleton<IOperatingMarketFreightServiceProviderRegistry>(
            freightServiceProviderRegistry);
        services.AddSingleton<IOperatingMarketServiceModule>(module);
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IOperatingMarketContextAccessor,
            DeploymentOperatingMarketContextAccessor>();
        services.AddScoped<IOperatingMarketAddressLookupService,
            OperatingMarketAddressLookupService>();
        services.AddScoped<IOperatingMarketRuntimeProfileService,
            OperatingMarketRuntimeProfileService>();
        module.AddServices(services, configuration);

        return services;
    }
}
