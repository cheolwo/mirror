using Hongdal.Infrastructure.Persistence.AgriculturalFisheries;
using Hongdal.Infrastructure.Persistence.TraditionalMarkets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using StackExchange.Redis;
using 홍달.Data;
using 홍달.Services.Options;

namespace Hongdal.Infrastructure.HealthChecks;

public sealed class PersistenceReadinessHealthCheck : IHealthCheck
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConnectionMultiplexer? _redis;
    private readonly TransientStateProvider _transientStateProvider;
    private readonly IMongoClient _mongo;
    private readonly MongoDbOptions _mongoOptions;

    public PersistenceReadinessHealthCheck(
        IServiceScopeFactory scopeFactory,
        IServiceProvider serviceProvider,
        IMongoClient mongo,
        IOptions<MongoDbOptions> mongoOptions,
        IOptions<TransientStateOptions> transientStateOptions)
    {
        _scopeFactory = scopeFactory;
        _transientStateProvider = transientStateOptions.Value.Provider;
        _redis = _transientStateProvider == TransientStateProvider.Redis
            ? serviceProvider.GetRequiredService<IConnectionMultiplexer>()
            : null;
        _mongo = mongo;
        _mongoOptions = mongoOptions.Value;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var failures = new List<string>();
        var data = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        Exception? firstException = null;

        await using (var scope = _scopeFactory.CreateAsyncScope())
        {
            await CheckDbContextAsync(
                "mysql-main",
                scope.ServiceProvider.GetRequiredService<HongdalContext>(),
                failures,
                data,
                CaptureException,
                cancellationToken);
            await CheckDbContextAsync(
                "mysql-traditional-markets",
                scope.ServiceProvider.GetRequiredService<TraditionalMarketDbContext>(),
                failures,
                data,
                CaptureException,
                cancellationToken);
            await CheckDbContextAsync(
                "mysql-agricultural-fisheries",
                scope.ServiceProvider.GetRequiredService<AgriculturalFisheriesDbContext>(),
                failures,
                data,
                CaptureException,
                cancellationToken);
        }

        data["transientStateProvider"] = _transientStateProvider.ToString();
        if (_redis is not null)
        {
            try
            {
                var latency = await _redis.GetDatabase().PingAsync().WaitAsync(cancellationToken);
                data["redisLatencyMs"] = Math.Round(latency.TotalMilliseconds, 2);
            }
            catch (Exception exception)
            {
                failures.Add("redis");
                CaptureException(exception);
            }
        }

        try
        {
            if (string.IsNullOrWhiteSpace(_mongoOptions.Database))
            {
                throw new InvalidOperationException("MongoDb:Database configuration is required.");
            }

            var database = _mongo.GetDatabase(_mongoOptions.Database);
            await database.RunCommandAsync<BsonDocument>(
                new BsonDocument("ping", 1),
                cancellationToken: cancellationToken);
            data["mongoDatabase"] = _mongoOptions.Database;
        }
        catch (Exception exception)
        {
            failures.Add("mongo");
            CaptureException(exception);
        }

        return failures.Count == 0
            ? HealthCheckResult.Healthy(
                $"MySQL, MongoDB and {_transientStateProvider} transient state are ready.",
                data)
            : HealthCheckResult.Unhealthy(
                $"Persistence dependencies are not ready: {string.Join(", ", failures)}.",
                firstException,
                data);

        void CaptureException(Exception exception)
        {
            firstException ??= exception;
        }
    }

    private static async Task CheckDbContextAsync(
        string name,
        DbContext dbContext,
        ICollection<string> failures,
        IDictionary<string, object> data,
        Action<Exception> captureException,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!await dbContext.Database.CanConnectAsync(cancellationToken))
            {
                failures.Add(name);
                return;
            }

            var pendingMigrations = (await dbContext.Database
                    .GetPendingMigrationsAsync(cancellationToken))
                .ToArray();
            data[$"{name}PendingMigrations"] = pendingMigrations.Length;
            if (pendingMigrations.Length > 0)
            {
                failures.Add($"{name}-migrations");
            }
        }
        catch (Exception exception)
        {
            failures.Add(name);
            captureException(exception);
        }
    }
}
