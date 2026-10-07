using System.Data.Common;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Ssalddel.Migrations;
using 살뜰.Data;
namespace NeighborhoodExchangePreview;
internal static class FlexibleMigrationProbe
{
    public static async Task VerifyAsync(SsalddelContext db, string evidence)
    {
        // Table identifiers are generated locally from a GUID, never from user input.
#pragma warning disable EF1002
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var queue = "flex_migration_queue_" + suffix; var request = "flex_migration_request_" + suffix;
        await db.Database.OpenConnectionAsync();
        try
        {
            await db.Database.ExecuteSqlRawAsync($"CREATE TEMPORARY TABLE `{queue}` (id int PRIMARY KEY, legacy_value varchar(30));");
            await db.Database.ExecuteSqlRawAsync($"CREATE TEMPORARY TABLE `{request}` (id int PRIMARY KEY, legacy_value varchar(30));");
            await db.Database.ExecuteSqlRawAsync($"INSERT INTO `{queue}` VALUES (1, 'legacy-preserved'); INSERT INTO `{request}` VALUES (1, 'legacy-preserved');");
            var operations = new AddNeighborhoodDispatchChoice().UpOperations.ToArray();
            foreach (var op in operations)
                if (op is AddColumnOperation col) col.Table = col.Table == "운송실행투영" ? queue : request;
                else if (op is CreateIndexOperation index) index.Table = request;
            var commands = db.GetService<IMigrationsSqlGenerator>().Generate(operations, db.Model);
            foreach (var command in commands) await db.Database.ExecuteSqlRawAsync(command.CommandText);
            await using var query = db.Database.GetDbConnection().CreateCommand();
            query.CommandText = $"SELECT legacy_value, neighborhood_dispatch_mode, neighborhood_dispatch_revision, neighborhood_dispatch_ready FROM `{queue}` WHERE id=1";
            await using (var reader = await query.ExecuteReaderAsync())
            {
                if (!await reader.ReadAsync() || reader.GetString(0) != "legacy-preserved" || !reader.IsDBNull(1) || reader.GetInt64(2) != 0 || !reader.GetBoolean(3))
                    throw new InvalidOperationException("Legacy migration defaults were not preserved.");
            }
            await db.Database.ExecuteSqlRawAsync($"INSERT INTO `{request}` (id, neighborhood_registration_key) VALUES (2, 'same-key')");
            var uniqueRejected = false;
            try { await db.Database.ExecuteSqlRawAsync($"INSERT INTO `{request}` (id, neighborhood_registration_key) VALUES (3, 'same-key')"); }
            catch (MySqlConnector.MySqlException ex) when (ex.Number == 1062) { uniqueRejected = true; }
            if (!uniqueRejected) throw new InvalidOperationException("Unique registration key was not enforced.");
            File.WriteAllText(Path.Combine(evidence, "migration-evidence.json"), JsonSerializer.Serialize(new {
                Provider = "MySQL 8.4", Migration = "20261006120000_AddNeighborhoodDispatchChoice", LegacyRowPreserved = true,
                LegacyModeNull = true, LegacyRevisionZero = true, LegacyReadyTrue = true, UniqueRegistrationKeyEnforced = true,
                Scope = "Exact Up operations on task-only temporary tables; entire repository migration history not applied" }, new JsonSerializerOptions { WriteIndented=true }));
        }
        finally { await db.Database.CloseConnectionAsync(); }
#pragma warning restore EF1002
    }
}
