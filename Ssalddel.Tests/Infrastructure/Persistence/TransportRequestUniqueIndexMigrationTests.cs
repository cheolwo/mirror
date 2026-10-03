using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Dispatch.Queue;

namespace Ssalddel.Tests.Infrastructure.Persistence;

public sealed class TransportRequestUniqueIndexMigrationTests
{
    [Fact]
    public void 음식배달제안계산근거는_새Nullable열만추가하고_기존할증호환열은보존한다()
    {
        using var context = CreateContext();
        const string migrationId = "20261002004600_음식배달제안계산근거";
        var assembly = context.GetService<IMigrationsAssembly>();
        var migration = assembly.CreateMigration(assembly.Migrations[migrationId], context.Database.ProviderName!);
        var additions = migration.UpOperations.OfType<AddColumnOperation>().ToArray();
        Assert.Equal(2, additions.Length);
        Assert.All(additions, column => Assert.True(column.IsNullable));
        Assert.Contains(additions, column => column.Name == "driver_offer_calculation_json" && column.ColumnType == "longtext");
        var compatibility = migration.UpOperations.OfType<SqlOperation>().ToArray();
        Assert.Equal(9, compatibility.Length);
        Assert.All(compatibility, item =>
        {
            Assert.Contains("TABLE_SCHEMA = DATABASE()", item.Sql);
            Assert.Contains("information_schema.COLUMNS", item.Sql);
            Assert.Contains("'SELECT 1'", item.Sql);
            Assert.DoesNotContain("UPDATE ", item.Sql);
            Assert.DoesNotContain("DROP ", item.Sql);
        });
        Assert.Equal(2, migration.DownOperations.Count);
        Assert.All(migration.DownOperations, operation => Assert.DoesNotContain("temporary_demand", ((DropColumnOperation)operation).Name));
    }

    [Fact]
    public void Migration_빈의뢰Id를정규화한뒤고유인덱스를만든다()
    {
        using var context = CreateContext();
        const string migrationId = "20260727112931_AddTransportRequestUniqueIndex";
        Assert.Contains(migrationId, context.Database.GetMigrations());

        var migrationsAssembly = context.GetService<IMigrationsAssembly>();
        var migration = migrationsAssembly.CreateMigration(
            migrationsAssembly.Migrations[migrationId],
            context.Database.ProviderName!);
        var operations = migration.UpOperations.ToList();
        var legacyRequestIdBackfill = Assert.Single(
            operations.OfType<SqlOperation>(),
            operation => operation.Sql.Contains(
                "legacy-unlinked-transport-",
                StringComparison.Ordinal));
        var uniqueRequestIdIndex = Assert.Single(
            operations.OfType<CreateIndexOperation>(),
            operation => operation.Name == "ux_운송실행투영_request_id"
                         && operation.IsUnique);

        Assert.Contains("TRIM(`request_id`) = ''", legacyRequestIdBackfill.Sql);
        Assert.True(
            operations.IndexOf(legacyRequestIdBackfill)
            < operations.IndexOf(uniqueRequestIdIndex));
    }

    private static SsalddelContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<SsalddelContext>()
            .UseMySql(
                "Server=localhost;Database=ssalddel_transport_migration_test;User=root;Password=test;",
                new MySqlServerVersion(new Version(8, 4, 0)),
                mysql => mysql.MigrationsAssembly(
                    typeof(운송의뢰배차대기Service).Assembly.GetName().Name))
            .Options;
        return new SsalddelContext(options, new DummyPersonalDataEncryptionService());
    }

    private sealed class DummyPersonalDataEncryptionService : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;

        public string? Unprotect(string? value) => value;
    }
}
