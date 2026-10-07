using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Ssalddel.Infrastructure.HealthChecks;
using Ssalddel.Infrastructure.Persistence.AgriculturalFisheries;
using Ssalddel.Infrastructure.Persistence.PublicData;
using Ssalddel.Infrastructure.Persistence.TraditionalMarkets;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Options;

namespace Ssalddel.Tests.Infrastructure.HealthChecks;

public sealed class PersistenceReadinessPublicDataTests
{
    [Theory]
    [InlineData(false, false, HealthStatus.Healthy)]
    [InlineData(true, false, HealthStatus.Unhealthy)]
    [InlineData(false, true, HealthStatus.Unhealthy)]
    public async Task PublicDataDatabaseParticipatesInReadinessAlongsideTheOtherThreeContexts(
        bool unreachable, bool pendingMigration, HealthStatus expected)
    {
        var services = new ServiceCollection();
        // Isolate readiness from production MySQL DDL; connectivity still uses
        // SQLite, and the public-data pending case uses a real EF migration.
        var fixtureAssembly = typeof(PublicDataPendingFixtureMigration).Assembly.GetName().Name!;
        services.AddScoped(_ => new SsalddelContext(new DbContextOptionsBuilder<SsalddelContext>()
            .UseSqlite("Data Source=:memory:").ReplaceService<IMigrationsAssembly, EmptyMigrationsAssembly>().Options,
            new PassThroughEncryption()));
        services.AddScoped(_ => new TraditionalMarketDbContext(new DbContextOptionsBuilder<TraditionalMarketDbContext>()
            .UseSqlite("Data Source=:memory:").ReplaceService<IMigrationsAssembly, EmptyMigrationsAssembly>().Options));
        services.AddScoped(_ => new AgriculturalFisheriesDbContext(new DbContextOptionsBuilder<AgriculturalFisheriesDbContext>()
            .UseSqlite("Data Source=:memory:").ReplaceService<IMigrationsAssembly, EmptyMigrationsAssembly>().Options));
        var unavailableFile = Path.Combine(Path.GetTempPath(), "missing-readiness-" + Guid.NewGuid().ToString("N"), "db.sqlite");
        var publicOptions = new DbContextOptionsBuilder<PublicDataIngestionDbContext>()
            .UseSqlite(unreachable ? "Data Source=" + unavailableFile : "Data Source=:memory:",
                sql => sql.MigrationsAssembly(fixtureAssembly));
        if (!pendingMigration) publicOptions.ReplaceService<IMigrationsAssembly, EmptyMigrationsAssembly>();
        services.AddScoped(_ => new PublicDataIngestionDbContext(publicOptions.Options));
        await using var provider = services.BuildServiceProvider();
        var mongo = DispatchProxy.Create<IMongoClient, MongoClientProxy>();
        var check = new PersistenceReadinessHealthCheck(provider.GetRequiredService<IServiceScopeFactory>(), provider,
            mongo, Options.Create(new MongoDbOptions { Database = "readiness-fixture" }),
            Options.Create(new TransientStateOptions { Provider = TransientStateProvider.Memory }));

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(expected, result.Status);
        foreach (var context in new[] { "mysql-main", "mysql-traditional-markets", "mysql-agricultural-fisheries" })
            Assert.Equal(0, result.Data[context + "PendingMigrations"]);
        Assert.Equal("readiness-fixture", result.Data["mongoDatabase"]);
        if (unreachable) Assert.Contains("mysql-public-data-ingestion", result.Description);
        else Assert.Equal(pendingMigration ? 1 : 0, result.Data["mysql-public-data-ingestionPendingMigrations"]);
        if (pendingMigration) Assert.Contains("mysql-public-data-ingestion-migrations", result.Description);
    }

    public class MongoClientProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            targetMethod?.Name == nameof(IMongoClient.GetDatabase)
                ? DispatchProxy.Create<IMongoDatabase, MongoDatabaseProxy>()
                : throw new NotSupportedException(targetMethod?.Name);
    }

    public class MongoDatabaseProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            targetMethod?.Name == nameof(IMongoDatabase.RunCommandAsync)
                ? Task.FromResult(new BsonDocument("ok", 1))
                : throw new NotSupportedException(targetMethod?.Name);
    }

    private sealed class PassThroughEncryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }

    public sealed class EmptyMigrationsAssembly : IMigrationsAssembly
    {
        public IReadOnlyDictionary<string, TypeInfo> Migrations { get; } = new Dictionary<string, TypeInfo>();
        public ModelSnapshot? ModelSnapshot => null;
        public Assembly Assembly => typeof(EmptyMigrationsAssembly).Assembly;
        public string? FindMigrationId(string nameOrId) => null;
        public Migration CreateMigration(TypeInfo migrationClass, string activeProvider) => throw new NotSupportedException();
    }
}

[DbContext(typeof(PublicDataIngestionDbContext))]
[Migration("209901010001_PublicDataReadinessFixture")]
public sealed class PublicDataPendingFixtureMigration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) { }
    protected override void Down(MigrationBuilder migrationBuilder) { }
}
