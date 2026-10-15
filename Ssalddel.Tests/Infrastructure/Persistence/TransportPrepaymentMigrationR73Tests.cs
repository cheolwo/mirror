using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Ssalddel.Migrations;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.도메인.기사;

namespace Ssalddel.Tests.Infrastructure.Persistence;

public sealed class TransportPrepaymentMigrationR73Tests
{
    [Fact]
    public void 기존참여테이블을재생성하지않고정산두테이블과결제근거열만추가한다()
    {
        var migration = new AddTransportPrepaymentPayoutBatchR73();
        Assert.Equal(new[] { "기사운송대금정산배치", "기사운송대금정산배치행" },
            migration.UpOperations.OfType<CreateTableOperation>().Select(x => x.Name).Order());
        var columns = migration.UpOperations.OfType<AddColumnOperation>().ToArray();
        Assert.Equal(new[] { "deposit_webhook_verification_hash", "preparation_evidence_json" }, columns.Select(x => x.Name).Order());
        Assert.All(columns, x => { Assert.Equal("결제", x.Table); Assert.True(x.IsNullable); });
        Assert.All(migration.UpOperations, x => Assert.True(x is CreateTableOperation or CreateIndexOperation or AddColumnOperation));
        Assert.All(migration.DownOperations, x => Assert.True(x is DropTableOperation or DropColumnOperation));
        Assert.Equal(2, migration.DownOperations.OfType<DropTableOperation>().Count());
        Assert.Equal(2, migration.DownOperations.OfType<DropColumnOperation>().Count());
        Assert.DoesNotContain(migration.DownOperations.OfType<DropTableOperation>(), x => x.Name == "driver_work_participation");
    }

    [Fact]
    public void MySql명세행의중복채무제약과판본예정일을현재모델SnapshotMigration에서보존한다()
    {
        using var db = new SsalddelContext(new DbContextOptionsBuilder<SsalddelContext>()
            .UseMySql("Server=127.0.0.1;Port=1;Database=r73_model_only;User=unused;Password=unused;",
                new MySqlServerVersion(new Version(8, 4, 0))).Options, new Encryption());
        var migration = new AddTransportPrepaymentPayoutBatchR73 { ActiveProvider = db.Database.ProviderName! };
        var snapshot = Assert.IsAssignableFrom<ModelSnapshot>(Activator.CreateInstance(
            typeof(AddTransportPrepaymentPayoutBatchR73).Assembly.GetType("Ssalddel.Migrations.SsalddelContextModelSnapshot")!, nonPublic: true));
        foreach (var type in new[] { typeof(기사운송대금정산배치), typeof(기사운송대금정산배치행) })
        {
            var runtime = db.Model.FindEntityType(type)!;
            var saved = snapshot.Model.FindEntityType(type.FullName!)!;
            var table = migration.UpOperations.OfType<CreateTableOperation>().Single(x => x.Name == runtime.GetTableName());
            Assert.Equal(runtime.GetProperties().Select(x => x.Name).Order(), saved.GetProperties().Select(x => x.Name).Order());
            Assert.Equal(runtime.GetProperties().Select(x => x.GetColumnName()).Order(), table.Columns.Select(x => x.Name).Order());
            Assert.Equal(runtime.GetForeignKeys().Count(), table.ForeignKeys.Count);
            Assert.Equal(runtime.FindProperty("Revision")?.IsConcurrencyToken, saved.FindProperty("Revision")?.IsConcurrencyToken);
        }
        Assert.Contains(migration.UpOperations.OfType<CreateIndexOperation>(),
            x => x.Table == "기사운송대금정산배치행" && x.IsUnique && x.Columns.SequenceEqual(new[] { "payout_request_id" }));
        var sql = string.Join('\n', db.GetService<IMigrationsSqlGenerator>().Generate(migration.UpOperations).Select(x => x.CommandText));
        Assert.DoesNotContain("DROP ", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UPDATE ", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CREATE TABLE `driver_work_participation`", sql, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class Encryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
