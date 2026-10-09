using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Ssalddel.Domain.Pricing;
using Ssalddel.Migrations;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;

namespace Ssalddel.Tests.Infrastructure;

public sealed class AgreedRateTableMigrationTests
{
    [Fact]
    public void MySQL모델과snapshot의합의테이블및운임근거가일치하여중복migration을만들지않는다()
    {
        using var db = MySqlContext();
        var snapshot = db.GetService<IMigrationsAssembly>().ModelSnapshot!;
        var storedModel = db.GetService<IModelRuntimeInitializer>().Initialize(snapshot.Model, designTime: true);
        var current = db.GetService<IDesignTimeModel>().Model;
        var relevant = db.GetService<IMigrationsModelDiffer>().GetDifferences(storedModel.GetRelationalModel(), current.GetRelationalModel())
            .Where(x => Table(x) is "agreed_rate_tables" or "agreed_rate_table_receipts" or "agreed_rate_table_selections" or "운임구성")
            .ToArray();
        Assert.True(relevant.Length == 0, string.Join("\n", relevant.Select(x => $"{x.GetType().Name}:{Table(x)}:{x.GetType().GetProperty("Name")?.GetValue(x)}")));
        var agreement = current.FindEntityType(typeof(AgreedRateTableRecord))!;
        Assert.True(agreement.FindProperty(nameof(AgreedRateTableRecord.Revision))!.IsConcurrencyToken);
        Assert.Equal(["AgreementId"], agreement.FindPrimaryKey()!.Properties.Select(x => x.Name));
        Assert.Equal(["PartyKind", "PartyId", "RequestId"], current.FindEntityType(typeof(AgreedRateTableCommandReceipt))!.FindPrimaryKey()!.Properties.Select(x => x.Name));
        var selection = current.FindEntityType(typeof(AgreedRateTableSelectionRecord))!;
        Assert.Equal(["BusinessKind", "WorkId"], selection.FindPrimaryKey()!.Properties.Select(x => x.Name));
        Assert.Equal(18, selection.FindProperty(nameof(AgreedRateTableSelectionRecord.AgreedAmountKrw))!.GetPrecision());
        Assert.Equal(2, selection.FindProperty(nameof(AgreedRateTableSelectionRecord.AgreedAmountKrw))!.GetScale());
        Assert.Equal(3, selection.FindProperty(nameof(AgreedRateTableSelectionRecord.DistanceKm))!.GetScale());
        Assert.True(current.FindEntityType(typeof(살뜰.도메인.운송.운임구성))!.FindProperty("합의요금표SnapshotJson")!.IsNullable);
    }

    [Fact]
    public void MySQL생성SQL과실제migration스크립트는세테이블과nullable근거만추가하고기존금액을수정하지않는다()
    {
        using var db = MySqlContext();
        var migration = new AddAgreedRateTables();
        Assert.Equal(6, migration.UpOperations.Count);
        Assert.Equal(3, migration.UpOperations.OfType<CreateTableOperation>().Count());
        Assert.Equal(2, migration.UpOperations.OfType<CreateIndexOperation>().Count());
        var column = Assert.Single(migration.UpOperations.OfType<AddColumnOperation>());
        Assert.Equal("운임구성", column.Table); Assert.Equal("agreed_rate_table_snapshot_json", column.Name);
        Assert.True(column.IsNullable); Assert.Null(column.DefaultValue); Assert.Null(column.DefaultValueSql);
        var sql = string.Join("\n", db.GetService<IMigrationsSqlGenerator>().Generate(migration.UpOperations, db.GetService<IDesignTimeModel>().Model).Select(x => x.CommandText));
        var script = db.GetService<IMigrator>().GenerateScript("20261007180000_AddNeighborhoodPackageSize", "20261008120000_AddAgreedRateTables");
        foreach (var generated in new[] { sql, script })
        {
            Assert.Contains("agreed_rate_tables", generated); Assert.Contains("agreed_rate_table_receipts", generated);
            Assert.Contains("agreed_rate_table_selections", generated); Assert.Contains("agreed_rate_table_snapshot_json", generated);
            Assert.DoesNotContain("UPDATE ", generated, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("DROP ", generated, StringComparison.OrdinalIgnoreCase);
        }
        Assert.DoesNotContain("INSERT ", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Throws<NotSupportedException>(() => migration.DownOperations);
    }

    [Fact]
    public async Task 실제격리SQLite의기존운임행과금액은Up후보존되고새합의판본과요청은왕복저장된다()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new SsalddelContext(new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(connection).Options, new Encryption());
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE 운임구성 (
                id INTEGER PRIMARY KEY, request_id TEXT NOT NULL, 기본운임 TEXT NOT NULL, 거리운임 TEXT NOT NULL,
                할증 TEXT NOT NULL, 대기료 TEXT NOT NULL, 수작업비 TEXT NOT NULL, 최종운임 TEXT NOT NULL,
                driver_expected_payout TEXT NULL, expected_distance_km TEXT NULL, per_km_rate TEXT NULL,
                minimum_fare TEXT NULL, distance_basis TEXT NULL, rate_source TEXT NULL, created_at TEXT NOT NULL, updated_at TEXT NOT NULL);
            INSERT INTO 운임구성 (id,request_id,기본운임,거리운임,할증,대기료,수작업비,최종운임,driver_expected_payout,
                expected_distance_km,per_km_rate,minimum_fare,distance_basis,rate_source,created_at,updated_at)
            VALUES (7,'legacy-work','13000','2000','0','0','0','15000','15000','15.5','129.03','15000','입력거리','original-fixture',
                '2026-10-07 06:00:00','2026-10-07 06:00:00');
            """);
        foreach (var command in db.GetService<IMigrationsSqlGenerator>().Generate(new AddAgreedRateTables().UpOperations, db.GetService<IDesignTimeModel>().Model))
            await db.Database.ExecuteSqlRawAsync(command.CommandText);
        var original = await db.운임구성.SingleAsync();
        Assert.Equal(7, original.Id); Assert.Equal("legacy-work", original.의뢰Id);
        Assert.Equal(13000m, original.기본운임); Assert.Equal(2000m, original.거리운임);
        Assert.Equal(15000m, original.최종운임); Assert.Equal(15000m, original.기사지급예정운임);
        Assert.Equal(15.5m, original.예상거리Km); Assert.Equal("original-fixture", original.단가출처);
        Assert.Null(original.합의요금표SnapshotJson);
        var now = new DateTime(2026, 10, 8, 3, 30, 0, DateTimeKind.Utc); var requestId = Guid.NewGuid();
        db.Add(new AgreedRateTableRecord
        {
            AgreementId = "fixture-agreement", BusinessKind = "CargoTransport", VehicleCode = "다마스",
            ProposerKind = "Shipper", ProposerId = "fixture-owner", CounterpartyKind = "CargoDriver", CounterpartyId = "fixture-driver",
            TermsJson = "fixture-terms", TermsDigest = "fixture-digest", StatusCode = "Accepted", Revision = 2,
            EffectiveFromUtc = now.AddHours(-1), EffectiveUntilUtc = now.AddHours(1), AcceptedAtUtc = now, HistoryJson = "[]"
        });
        db.Add(new AgreedRateTableCommandReceipt { PartyKind = "Shipper", PartyId = "fixture-owner", RequestId = requestId,
            InputDigest = "fixture-command-digest", ResultJson = "fixture-result", RecordedAtUtc = now });
        db.Add(new AgreedRateTableSelectionRecord { BusinessKind = "CargoTransport", WorkId = "fixture-new-work", AgreementId = "fixture-agreement",
            AgreementRevision = 2, SelectionRequestId = requestId, SnapshotJson = "fixture-snapshot", SelectingPartyKind = "Shipper",
            SelectingPartyId = "fixture-owner", SelectedAtUtc = now, AgreedAmountKrw = 13100m, DistanceKm = 15.5m,
            DistanceBasisCode = "RouteEstimate", SynchronizationStatusCode = "Pending" });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var stored = await db.Set<AgreedRateTableRecord>().SingleAsync();
        Assert.Equal(2, stored.Revision); Assert.Equal("fixture-terms", stored.TermsJson); Assert.Equal(now, stored.AcceptedAtUtc);
        Assert.Equal(DateTimeKind.Utc, stored.EffectiveFromUtc.Kind); Assert.Equal(DateTimeKind.Utc, stored.AcceptedAtUtc!.Value.Kind);
        Assert.Equal(requestId, (await db.Set<AgreedRateTableCommandReceipt>().SingleAsync()).RequestId);
        var selection = await db.Set<AgreedRateTableSelectionRecord>().SingleAsync();
        Assert.Equal(requestId, selection.SelectionRequestId); Assert.Equal(13100m, selection.AgreedAmountKrw);
        Assert.Equal("fixture-snapshot", selection.SnapshotJson); Assert.Equal(DateTimeKind.Utc, selection.SelectedAtUtc.Kind);
        db.ChangeTracker.Clear();
        db.Add(new AgreedRateTableSelectionRecord { BusinessKind = "CargoTransport", WorkId = "fixture-new-work" });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear();
        var unchanged = await db.운임구성.SingleAsync();
        Assert.Equal(15000m, unchanged.최종운임); Assert.Equal(15000m, unchanged.기사지급예정운임);
        Assert.Null(unchanged.합의요금표SnapshotJson); Assert.Single(await db.Set<AgreedRateTableSelectionRecord>().ToArrayAsync());
    }

    private static string? Table(MigrationOperation operation)
        => operation.GetType().GetProperty("Table")?.GetValue(operation) as string ?? (operation as TableOperation)?.Name;
    private static SsalddelContext MySqlContext() => new(new DbContextOptionsBuilder<SsalddelContext>()
        .UseMySql("Server=127.0.0.1;Database=fixture_not_opened;User ID=fixture;Password=fixture;", new MySqlServerVersion(new Version(8, 4, 0)),
            options => options.MigrationsAssembly("Ssalddel")).Options, new Encryption());
    private sealed class Encryption : IPersonalDataEncryptionService
    { public string? Protect(string? value) => value; public string? Unprotect(string? value) => value; }
}
