using Hongdal.Infrastructure.Persistence.AgriculturalFisheries;
using Microsoft.EntityFrameworkCore;

namespace Hongdal.Extensions;

public static partial class ServiceCollectionExtensions
{
    private static IServiceCollection AddAgriculturalFisheriesPersistence(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContext<AgriculturalFisheriesDbContext>(options =>
            options.UseMySql(
                connectionString,
                new MySqlServerVersion(new Version(8, 4, 0)),
                mysqlOptions =>
                {
                    mysqlOptions.MigrationsAssembly("Hongdal.Infrastructure");
                    mysqlOptions.MigrationsHistoryTable("__EFMigrationsHistory_AgriculturalFisheries");
                    mysqlOptions.EnableRetryOnFailure();
                }));
        return services;
    }
}
