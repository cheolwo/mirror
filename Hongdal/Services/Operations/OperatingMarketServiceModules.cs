using Hongdal.Contracts.Common.Operations;

namespace Hongdal.Services.Operations;

public interface IOperatingMarketServiceModule
{
    string MarketCode { get; }

    void AddServices(IServiceCollection services, IConfiguration configuration);
}

public sealed class KoreaOperatingMarketServiceModule : IOperatingMarketServiceModule
{
    public string MarketCode => OperatingMarketCodes.Korea;

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddScoped<IOperatingMarketAddressLookupAdapter, KoreaRoadAddressLookupAdapter>();
        services.AddScoped<IThirdPartyLogisticsProviderDirectoryService,
            UnavailableThirdPartyLogisticsProviderDirectoryService>();
        services.AddScoped<IThirdPartyLogisticsProviderOutreachPreparationService,
            UnavailableThirdPartyLogisticsProviderOutreachPreparationService>();
        services.AddScoped<IOperatingMarketFreightWorkflowPolicy>(serviceProvider =>
            new KoreaOperatingMarketFreightWorkflowPolicy(
                serviceProvider.GetRequiredService<
                    IOperatingMarketFreightServiceProviderRegistry>(),
                serviceProvider.GetRequiredService<TimeProvider>()));
    }
}

public sealed class UnitedStatesOperatingMarketServiceModule : IOperatingMarketServiceModule
{
    public string MarketCode => OperatingMarketCodes.UnitedStates;

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<UnitedStatesAddressOptions>(
            configuration.GetSection(UnitedStatesAddressOptions.SectionName));
        services.AddHttpClient<IUnitedStatesAddressGeocoder, UnitedStatesCensusAddressGeocoder>(
            (serviceProvider, client) =>
            {
                var options = serviceProvider
                    .GetRequiredService<Microsoft.Extensions.Options.IOptions<UnitedStatesAddressOptions>>()
                    .Value
                    .CensusGeocoder;
                if (Uri.TryCreate(
                        $"{options.BaseUrl.TrimEnd('/')}/",
                        UriKind.Absolute,
                        out var baseAddress))
                {
                    client.BaseAddress = baseAddress;
                }

                client.Timeout = TimeSpan.FromSeconds(Math.Max(3, options.TimeoutSeconds));
            });
        services.AddSingleton<IUnitedStatesDeliveryScopePlanner,
            UnitedStatesDeliveryScopePlanner>();
        services.AddScoped<IUnitedStatesDeliveryScopeService,
            UnitedStatesDeliveryScopeService>();
        services.AddSingleton<IThirdPartyLogisticsProviderDirectoryService,
            UnitedStatesThirdPartyLogisticsProviderDirectoryService>();
        services.AddSingleton<IThirdPartyLogisticsProviderOutreachPreparationService,
            UnitedStatesThirdPartyLogisticsProviderOutreachPreparationService>();

        services.AddScoped<IOperatingMarketAddressLookupAdapter,
            UnitedStatesAddressLookupAdapter>();
        services.AddScoped<IOperatingMarketFreightWorkflowPolicy>(serviceProvider =>
            new UnitedStatesOperatingMarketFreightWorkflowPolicy(
                serviceProvider.GetRequiredService<
                    IOperatingMarketFreightServiceProviderRegistry>(),
                serviceProvider.GetRequiredService<TimeProvider>()));
    }
}
