using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using 살뜰.Data;

/// <summary>이전 r114 DB에 없는 현행 migration의 nullable 열만 추가한다. 행·기존 열·migration 이력은 변경하지 않는다.</summary>
internal static class R114NullableSchema
{
    private static readonly (string Table, string Column, string Type)[] Columns =
    [
        ("용달기사", "현재실차참조", "varchar(1000)"),
        ("배달기사", "현재실차참조", "varchar(1000)"),
        ("용달기사", "실차적재제원Json", "longtext"),
        ("용달기사", "실차적재판본", "bigint"),
        ("운송실행투영", "recommendation_decision_evidence_json", "longtext"),
        ("shipper_requests", "cargo_input_volume_cbm", "decimal(65,30)"),
        ("shipper_requests", "cargo_volume_evidence_code", "varchar(80)"),
        ("shipper_requests", "cargo_loading_calculation_revision", "varchar(80)"),
        ("shipper_requests", "cargo_floor_rotation_allowed", "tinyint(1)"),
        ("shipper_requests", "cargo_stacking_allowed", "tinyint(1)"),
        ("shipper_requests", "pickup_handling_minutes", "decimal(65,30)"),
        ("shipper_requests", "dropoff_handling_minutes", "decimal(65,30)")
    ];

    internal static async Task<IReadOnlyList<string>> PrepareAsync(CargoJourneySettings settings, SsalddelContext db, CancellationToken ct = default)
    {
        if (!settings.CargoPrimaryAI) return [];
        settings.ValidateSqlConnection(db.Database.GetDbConnection().ConnectionString);
        await using var sql = new MySqlConnection(db.Database.GetDbConnection().ConnectionString);
        await sql.OpenAsync(ct);
        await using var acquire = new MySqlCommand("SELECT GET_LOCK('r114_primary_nullable_schema_v1', 10)", sql);
        if (Convert.ToInt32(await acquire.ExecuteScalarAsync(ct)) != 1) throw new InvalidOperationException("R114SchemaLockUnavailable");
        try
        {
            var before = new Dictionary<string, long>(StringComparer.Ordinal);
            foreach (var table in Columns.Select(x => x.Table).Distinct(StringComparer.Ordinal))
            {
                await using var count = new MySqlCommand($"SELECT COUNT(*) FROM `{table}`", sql);
                before[table] = Convert.ToInt64(await count.ExecuteScalarAsync(ct));
            }
            var added = new List<string>();
            foreach (var column in Columns)
            {
                await using var read = new MySqlCommand("SELECT COLUMN_TYPE, IS_NULLABLE FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=@db AND TABLE_NAME=@table AND COLUMN_NAME=@column", sql);
                read.Parameters.AddWithValue("@db", CargoJourneySettings.DatabaseName);
                read.Parameters.AddWithValue("@table", column.Table);
                read.Parameters.AddWithValue("@column", column.Column);
                bool exists;
                await using (var reader = await read.ExecuteReaderAsync(ct))
                {
                    exists = await reader.ReadAsync(ct);
                    if (exists && (!string.Equals(reader.GetString(0), column.Type, StringComparison.OrdinalIgnoreCase) || reader.GetString(1) != "YES"))
                        throw new InvalidOperationException("R114SchemaColumnDefinitionMismatch");
                }
                if (exists) continue;
                // 식별자와 SQL 형식은 위 고정 migration 목록만 사용한다. 외부 입력은 보간하지 않는다.
                await using var add = new MySqlCommand($"ALTER TABLE `{column.Table}` ADD COLUMN `{column.Column}` {column.Type} NULL", sql);
                await add.ExecuteNonQueryAsync(ct);
                added.Add(column.Table + "." + column.Column);
            }
            var after = new Dictionary<string, long>(StringComparer.Ordinal);
            foreach (var table in before.Keys)
            {
                await using var count = new MySqlCommand($"SELECT COUNT(*) FROM `{table}`", sql);
                after[table] = Convert.ToInt64(await count.ExecuteScalarAsync(ct));
                if (after[table] != before[table]) throw new InvalidOperationException("R114SchemaRowCountChanged");
            }
            var report = new { schema = "r114-nullable-schema-additions.v1", atUtc = DateTime.UtcNow,
                database = CargoJourneySettings.DatabaseName, nullableOnly = true, addedColumns = added,
                rowsBefore = before, rowsAfter = after, existingRowsPreserved = true, migrationsApplied = false,
                sourceMigrations = new[] { "20261019100000", "20261019150000", "20261019160000", "20261019190000" } };
            var bytes = JsonSerializer.SerializeToUtf8Bytes(report, new JsonSerializerOptions { WriteIndented = true });
            var directory = Path.Combine(settings.ArtifactRoot, ".private", "schema-additions");
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, CargoAiSelector.Hash(bytes) + ".json");
            await using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            { await stream.WriteAsync(bytes, ct); stream.Flush(true); }
            return added;
        }
        finally
        {
            await using var release = new MySqlCommand("SELECT RELEASE_LOCK('r114_primary_nullable_schema_v1')", sql);
            await release.ExecuteScalarAsync(CancellationToken.None);
        }
    }
}
