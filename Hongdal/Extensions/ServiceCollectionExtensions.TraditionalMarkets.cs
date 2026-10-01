using Hongdal.Infrastructure.Persistence.TraditionalMarkets;
using Hongdal.Services.TraditionalMarkets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using 홍달.Services.External.PublicData;
using 홍달.Services.Options;

namespace Hongdal.Extensions;

public static partial class ServiceCollectionExtensions
{
    private static IServiceCollection AddTraditionalMarketModule(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContext<TraditionalMarketDbContext>(options =>
            options.UseMySql(
                connectionString,
                new MySqlServerVersion(new Version(8, 4, 0)),
                mysqlOptions =>
                {
                    mysqlOptions.MigrationsAssembly("Hongdal.Infrastructure");
                    mysqlOptions.MigrationsHistoryTable("__EFMigrationsHistory_TraditionalMarkets");
                    mysqlOptions.EnableRetryOnFailure();
                }));

        services.AddHttpClient<ITraditionalMarketPublicDataClient, TraditionalMarketPublicDataClient>(
            (serviceProvider, client) =>
            {
                var options = serviceProvider.GetRequiredService<IOptions<PublicDataOptions>>().Value;
                var baseUrl = options.TraditionalMarket.BaseUrl.TrimEnd('/') + "/";
                client.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
                client.Timeout = TimeSpan.FromSeconds(Math.Max(5, options.TimeoutSeconds));
            });
        services.AddScoped<ITraditionalMarketCatalogService, TraditionalMarketCatalogService>();
        services.AddScoped<ITraditionalMarketLogisticsHubService, TraditionalMarketLogisticsHubService>();
        services.AddScoped<I전통시장생활권협의Service, 전통시장생활권협의Service>();
        return services;
    }
}
