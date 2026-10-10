using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Ssalddel.Migrations;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;

namespace Ssalddel.Tests.Infrastructure.Persistence;

public sealed class DriverWorkStartMigrationTests
{
    [Fact]
    public async Task Additive_migration_applies_to_legacy_rows_without_rewriting_or_claiming_active_slots()
    {
        SQLitePCL.Batteries_V2.Init();
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = new SsalddelContext(new DbContextOptionsBuilder<SsalddelContext>()
            .UseSqlite(connection).Options, new Encryption());
        await context.Database.ExecuteSqlRawAsync("CREATE TABLE driver_shifts (id INTEGER PRIMARY KEY, legacy_marker TEXT NOT NULL)");
        await context.Database.ExecuteSqlRawAsync("INSERT INTO driver_shifts (id, legacy_marker) VALUES (1, 'original-1'), (2, 'original-2')");
        var migration = new AddDriverWorkStartReliabilityR50 { ActiveProvider = context.Database.ProviderName! };
        var commands = context.GetService<IMigrationsSqlGenerator>().Generate(migration.UpOperations);
        foreach (var command in commands) await context.Database.ExecuteSqlRawAsync(command.CommandText);
        await using var select = connection.CreateCommand();
        select.CommandText = "SELECT id, legacy_marker, active_driver_id, work_start_request_key, stopped_at_utc FROM driver_shifts ORDER BY id";
        await using (var reader = await select.ExecuteReaderAsync())
        {
            for (var id = 1; id <= 2; id++)
            {
                Assert.True(await reader.ReadAsync());
                Assert.Equal(id, reader.GetInt32(0));
                Assert.Equal($"original-{id}", reader.GetString(1));
                Assert.True(reader.IsDBNull(2)); Assert.True(reader.IsDBNull(3)); Assert.True(reader.IsDBNull(4));
            }
            Assert.False(await reader.ReadAsync());
        }
        await context.Database.ExecuteSqlRawAsync("UPDATE driver_shifts SET active_driver_id = 'driver-1' WHERE id = 1");
        await Assert.ThrowsAsync<SqliteException>(() => context.Database.ExecuteSqlRawAsync(
            "UPDATE driver_shifts SET active_driver_id = 'driver-1' WHERE id = 2"));
        await context.Database.ExecuteSqlRawAsync("INSERT INTO driver_work_start_followups (ShiftId, DriverId, Status, AttemptCount, CreatedAtUtc) VALUES (1, 'driver-1', 'Pending', 0, '2026-10-09T00:00:00Z')");
        Assert.Equal(1L, await ScalarAsync(connection, "SELECT COUNT(*) FROM driver_work_start_followups"));
    }

    [Fact]
    public void MySql_migration_keeps_legacy_rows_and_only_adds_nullable_columns_unique_slots_and_followup_table()
    {
        using var context = new SsalddelContext(new DbContextOptionsBuilder<SsalddelContext>()
            .UseMySql("Server=localhost;Database=local_migration_shape;User=test;Password=test;",
                new MySqlServerVersion(new Version(8, 4, 0))).Options, new Encryption());
        var migration = new AddDriverWorkStartReliabilityR50 { ActiveProvider = context.Database.ProviderName! };
        Assert.Equal(6, migration.UpOperations.OfType<AddColumnOperation>().Count());
        Assert.All(migration.UpOperations.OfType<AddColumnOperation>(), operation => Assert.True(operation.IsNullable));
        Assert.Empty(migration.UpOperations.OfType<SqlOperation>());
        var table = Assert.Single(migration.UpOperations.OfType<CreateTableOperation>());
        Assert.Equal("driver_work_start_followups", table.Name);
        Assert.Equal("driver_shifts", Assert.Single(table.ForeignKeys).PrincipalTable);
        Assert.Equal(2, migration.UpOperations.OfType<CreateIndexOperation>().Count(operation => operation.IsUnique));
        var sql = string.Join('\n', context.GetService<IMigrationsSqlGenerator>().Generate(migration.UpOperations).Select(command => command.CommandText));
        Assert.DoesNotContain("UPDATE ", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DROP ", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CREATE UNIQUE INDEX", sql, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<long> ScalarAsync(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand(); command.CommandText = sql;
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    private sealed class Encryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
