using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MongoDB.Driver;
using 살뜰.Services.Options;
using 살뜰.Services.Dispatch.Common;
using 살뜰.도메인.배차;

namespace Ssalddel.Extensions;

public static partial class ServiceCollectionExtensions
{
    public static IServiceCollection AddSsalddelPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
                               ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("ConnectionStrings:DefaultConnection configuration is required.");
        }

        services.AddDbContext<SsalddelContext>(options =>
            options.UseMySql(
                connectionString,
                new MySqlServerVersion(new Version(8, 4, 0)),
                mysqlOptions =>
                {
                    mysqlOptions.MigrationsAssembly("Ssalddel");
                    mysqlOptions.EnableRetryOnFailure();
                }));

        services.AddScoped<I운영배차활동원장Store, Ef운영배차활동원장Store>();
        services.AddScoped<I운영배차공통UseCase, 운영배차공통UseCase>();
        services.AddScoped<I운영배차제안표시Service, 운영배차제안표시Service>();
        services.AddSingleton<운영배차수신상태Policy>();
        services.AddSingleton<운영배차단기지표Calculator>();
        services.TryAddSingleton(TimeProvider.System);

        services.AddTraditionalMarketModule(connectionString);
        services.AddAgriculturalFisheriesPersistence(connectionString);
        services.AddPublicDataIngestionPersistence(connectionString);
        services.AddDbContext<Ssalddel.Infrastructure.Persistence.WorldProjection.개체시각대응DbContext>(options =>
            options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 4, 0))));
        services.AddOptions<Ssalddel.Services.Content.개체시각자산Options>()
            .Bind(configuration.GetSection(Ssalddel.Services.Content.개체시각자산Options.Section));
        services.AddSsalddelTransientState(configuration);

        var mongoOptions = configuration.GetSection(MongoDbOptions.SectionName).Get<MongoDbOptions>() ?? new MongoDbOptions();
        var mongoConnectionString = string.IsNullOrWhiteSpace(mongoOptions.ConnectionString)
            ? Environment.GetEnvironmentVariable("MongoDb__ConnectionString")
            : mongoOptions.ConnectionString;
        if (string.IsNullOrWhiteSpace(mongoConnectionString))
        {
            throw new InvalidOperationException("MongoDb:ConnectionString configuration is required.");
        }

        if (string.IsNullOrWhiteSpace(mongoOptions.Database))
        {
            throw new InvalidOperationException("MongoDb:Database configuration is required.");
        }

        var mongoSettings = MongoClientSettings.FromConnectionString(mongoConnectionString);
        mongoSettings.ApplicationName ??= "Ssalddel";
        services.AddSingleton<IMongoClient>(_ => new MongoClient(mongoSettings));

        return services;
    }
}
