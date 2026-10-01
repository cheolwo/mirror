using Hongdal.Client.Infrastructure;
using HongdalApp.Options;
using HongdalApp.Services.Commerce.Coupang;
using HongdalApp.Services.Commerce.Naver;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HongdalApp.Services;

internal static class ShipperOptionsModule
{
    internal static IServiceCollection AddShipperOptionsModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<ClientDataModeOptions>(configuration.GetSection(ClientDataModeOptions.SectionName));
        services.Configure<ClientDataModeOptions>(ApplyClientDataModeEnvironmentOverrides);
        services.Configure<ShipperSmokeOptions>(configuration.GetSection(ShipperSmokeOptions.SectionName));
        services.Configure<ShipperSmokeOptions>(ApplyShipperSmokeEnvironmentOverrides);
        services.Configure<CoupangWingOptions>(configuration.GetSection(CoupangWingOptions.SectionName));
        services.Configure<NaverCommerceOptions>(configuration.GetSection(NaverCommerceOptions.SectionName));
        return services;
    }

    private static void ApplyClientDataModeEnvironmentOverrides(ClientDataModeOptions options)
    {
        ApplyBooleanEnvironmentOverride(
            "ClientDataMode__AllowSampleFallback",
            value => options.AllowSampleFallback = value);
        ApplyBooleanEnvironmentOverride(
            "ClientDataMode__AllowDevelopmentSnapshotFallback",
            value => options.AllowDevelopmentSnapshotFallback = value);
        ApplyBooleanEnvironmentOverride(
            "ClientDataMode__RequireServerLedgerForV1Smoke",
            value => options.RequireServerLedgerForV1Smoke = value);
    }

    private static void ApplyShipperSmokeEnvironmentOverrides(ShipperSmokeOptions options)
    {
        var startPath = Environment.GetEnvironmentVariable("ShipperSmoke__StartPath");
        if (!string.IsNullOrWhiteSpace(startPath))
        {
            options.StartPath = startPath;
        }
    }

    private static void ApplyBooleanEnvironmentOverride(string name, Action<bool> apply)
    {
        var rawValue = Environment.GetEnvironmentVariable(name);
        if (bool.TryParse(rawValue, out var value))
        {
            apply(value);
        }
    }
}
