using System.Net;
using Hongdal.Services.AgriculturalFisheries.Information;
using Hongdal.Services.AgriculturalFisheries.ImportReadiness;
using Microsoft.Extensions.Options;
using 홍달.Services.External.PublicData;
using 홍달.Services.Options;

namespace Hongdal.Extensions;

public static partial class ServiceCollectionExtensions
{
    public static IServiceCollection AddAgriculturalFisheriesInformationModule(
        this IServiceCollection services)
    {
        services.AddHttpClient<IAtDomesticFoodPriceLookupService, AtDomesticFoodPriceLookupService>(
            (serviceProvider, client) =>
            {
                var options = serviceProvider.GetRequiredService<IOptions<PublicDataOptions>>().Value;
                client.BaseAddress = new Uri(options.AtFoodPrices.BaseUrl);
                client.Timeout = TimeSpan.FromSeconds(Math.Max(5, options.TimeoutSeconds));
            });
        services
            .AddHttpClient<UsdaNassQuickStats가격공급자>(
                (serviceProvider, client) =>
                {
                    var options = serviceProvider.GetRequiredService<IOptions<PublicDataOptions>>().Value;
                    client.BaseAddress = new Uri(options.UsdaNassQuickStats.BaseUrl);
                    client.Timeout = TimeSpan.FromSeconds(Math.Max(5, options.TimeoutSeconds));
                })
            .RemoveAllLoggers();
        services.AddTransient<I미국농수산가격공급자>(serviceProvider =>
            serviceProvider.GetRequiredService<UsdaNassQuickStats가격공급자>());
        services
            .AddHttpClient<AbsConsumerPriceIndex식품가격공급자>(
                (serviceProvider, client) =>
                {
                    var options = serviceProvider.GetRequiredService<IOptions<PublicDataOptions>>().Value;
                    client.BaseAddress = new Uri(options.AbsConsumerPriceIndex.BaseUrl);
                    client.Timeout = TimeSpan.FromSeconds(Math.Max(5, options.TimeoutSeconds));
                })
            .RemoveAllLoggers();
        services.AddTransient<I호주농수산식품가격공급자>(serviceProvider =>
            serviceProvider.GetRequiredService<AbsConsumerPriceIndex식품가격공급자>());
        services
            .AddHttpClient<UsdaNassPriceArchiveService>(
                (serviceProvider, client) =>
                {
                    var options = serviceProvider.GetRequiredService<IOptions<PublicDataOptions>>().Value;
                    client.BaseAddress = new Uri(options.UsdaNassQuickStats.BaseUrl);
                    client.Timeout = TimeSpan.FromSeconds(Math.Max(30, options.TimeoutSeconds));
                })
            .RemoveAllLoggers();
        services.AddScoped<IUsdaNassPriceArchiveService>(serviceProvider =>
            serviceProvider.GetRequiredService<UsdaNassPriceArchiveService>());
        services
            .AddHttpClient<IKamisJsonClient, KamisJsonClient>(
                (serviceProvider, client) =>
                {
                    var options = serviceProvider.GetRequiredService<IOptions<PublicDataOptions>>().Value;
                    client.BaseAddress = new Uri(options.Kamis.BaseUrl);
                    client.DefaultRequestVersion = HttpVersion.Version11;
                    client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact;
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("Hongdal-KAMIS-Collector/0.0");
                    client.Timeout = TimeSpan.FromSeconds(Math.Max(90, options.TimeoutSeconds));
                })
            .RemoveAllLoggers();
        services.AddScoped<IKamisPriceArchiveService, KamisPriceArchiveService>();
        services.AddSingleton<IFoodPriceCrosswalkCatalog, FoodPriceCrosswalkCatalog>();
        services.AddScoped<IAgriculturalFisheriesInformationService, AgriculturalFisheriesInformationService>();
        services.AddScoped<I미국농수산가격조회Service, 미국농수산가격조회Service>();
        services.AddScoped<I호주농수산식품가격조회Service, 호주농수산식품가격조회Service>();
        services.AddSingleton<I미국농어업경영체정보원천Service,
            미국농어업경영체정보원천Service>();
        services.AddScoped<IMeatImportReadinessService, MeatImportReadinessService>();
        services.AddScoped<IFoodPriceComparisonService, FoodPriceComparisonService>();

        return services;
    }
}
