using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.DependencyInjection;
using Ssalddel.Migrations;
using 살뜰.Data;
using 살뜰.Infrastructure.Persistence.Configurations.Settlement;
using 살뜰.Infrastructure.Security;
using 살뜰.도메인.정산;
using 살뜰.도메인.창고;

namespace Ssalddel.Tests.Infrastructure;

public sealed class NeighborhoodSettlementPersistenceTests : IDisposable
{
    // 시험마다 전용 provider를 소유·해제해 보호 모델을 격리하고 전역 EF 캐시에 누적하지 않는다.
    private readonly ServiceProvider _sqliteServices = new ServiceCollection().AddEntityFrameworkSqlite().BuildServiceProvider();

    public void Dispose() => _sqliteServices.Dispose();

    [Fact]
    public async Task 마트완료근거migration은격리SQLite에nullable열하나만추가한다()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync(); await using var db = Db(connection);
        await db.Database.ExecuteSqlRawAsync("CREATE TABLE 피킹포장작업 (id INTEGER PRIMARY KEY)");
        var migration = new AddMartActivityCompletionEvidence();
        var operation = Assert.Single(migration.UpOperations);
        var column = Assert.IsType<AddColumnOperation>(operation);
        Assert.Equal("피킹포장작업", column.Table); Assert.Equal("completion_evidence_json", column.Name); Assert.True(column.IsNullable);
        foreach (var command in db.GetService<IMigrationsSqlGenerator>().Generate(migration.UpOperations, db.GetService<IDesignTimeModel>().Model))
            await db.Database.ExecuteSqlRawAsync(command.CommandText);
        await db.Database.ExecuteSqlRawAsync("INSERT INTO 피킹포장작업 (id, completion_evidence_json) VALUES (1, NULL)");
        using var query = connection.CreateCommand(); query.CommandText = "SELECT completion_evidence_json IS NULL FROM 피킹포장작업 WHERE id=1";
        Assert.Equal(1L, await query.ExecuteScalarAsync());
    }

    [Fact]
    public async Task 마트완료근거는보호해서저장하고실제MySQL모델과snapshot의열동시성이일치한다()
    {
        using (var mysql = new SsalddelContext(new DbContextOptionsBuilder<SsalddelContext>().UseMySql("Server=127.0.0.1;Database=not_opened;User ID=fixture;Password=fixture;",
            new MySqlServerVersion(new Version(8, 4, 0)), options => options.MigrationsAssembly("Ssalddel")).Options, new Encryption()))
        {
            var snapshot = mysql.GetService<IModelRuntimeInitializer>().Initialize(mysql.GetService<IMigrationsAssembly>().ModelSnapshot!.Model, designTime: true);
            var model = mysql.GetService<IDesignTimeModel>().Model;
            Assert.Empty(mysql.GetService<IMigrationsModelDiffer>().GetDifferences(snapshot.GetRelationalModel(), model.GetRelationalModel()).Where(operation => Table(operation) == "피킹포장작업"));
            var entity = model.FindEntityType(typeof(피킹포장작업))!;
            Assert.NotNull(entity.FindProperty(nameof(피킹포장작업.완료근거Json))!.GetValueConverter());
            Assert.True(entity.FindProperty(nameof(피킹포장작업.상태))!.IsConcurrencyToken);
        }
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync(); await using var db = Db(connection);
        await db.Database.EnsureCreatedAsync();
        db.피킹포장작업.Add(new() { 작업Key = "private-work", 완료근거Json = "{\"ActorUserId\":\"actual-worker-private\",\"Quantity\":2}" });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        using var query = connection.CreateCommand(); query.CommandText = "SELECT completion_evidence_json FROM 피킹포장작업 WHERE 작업_key='private-work'";
        var raw = Assert.IsType<string>(await query.ExecuteScalarAsync());
        Assert.StartsWith("protected:", raw); Assert.DoesNotContain("actual-worker-private", raw);
        Assert.Contains("actual-worker-private", (await db.피킹포장작업.SingleAsync()).완료근거Json);
    }

    [Fact]
    public void 실제MySQL모델과snapshot은5테이블과보호변환동시성멱등키배송원점유일성을같이유지한다()
    {
        using var db = new SsalddelContext(new DbContextOptionsBuilder<SsalddelContext>().UseMySql("Server=127.0.0.1;Database=not_opened;User ID=fixture;Password=fixture;", new MySqlServerVersion(new Version(8, 4, 0)), o => o.MigrationsAssembly("Ssalddel")).Options, new Encryption());
        var snapshot = db.GetService<IModelRuntimeInitializer>().Initialize(db.GetService<IMigrationsAssembly>().ModelSnapshot!.Model, designTime: true);
        var model = db.GetService<IDesignTimeModel>().Model;
        var diff = db.GetService<IMigrationsModelDiffer>().GetDifferences(snapshot.GetRelationalModel(), model.GetRelationalModel()).Where(x => NeighborhoodSettlementModel.Tables.Contains(Table(x))).ToArray();
        Assert.True(diff.Length == 0, string.Join("\n", diff.Select(x => x.GetType().Name + ":" + Table(x) + ":" + x.GetType().GetProperty("Name")?.GetValue(x))));
        var entities = model.GetEntityTypes().Where(x => NeighborhoodSettlementModel.Tables.Contains(x.GetTableName())).ToArray(); Assert.Equal(6, entities.Length);
        Assert.All(entities, e => { Assert.NotNull(e.FindPrimaryKey()); Assert.All(e.GetProperties().Where(x => x.Name.EndsWith("Json", StringComparison.Ordinal)), p => Assert.NotNull(p.GetValueConverter())); });
        var agreement = model.FindEntityType(typeof(NeighborhoodSettlementAgreementRecord))!; Assert.True(agreement.FindProperty("Revision")!.IsConcurrencyToken);
        var entry = model.FindEntityType(typeof(NeighborhoodSettlementEntryRecord))!;
        Assert.Contains(entry.GetIndexes(), x => x.IsUnique && x.Properties.Select(p => p.Name).SequenceEqual(["AccrualKey"]));
        Assert.Contains(entry.GetIndexes(), x => x.IsUnique && x.Properties.Select(p => p.Name).SequenceEqual(["CanonicalDeliveryOrigin"]));
        Assert.True(entry.FindProperty("Revision")!.IsConcurrencyToken); Assert.All(entry.GetForeignKeys(), x => Assert.Equal(DeleteBehavior.Restrict, x.DeleteBehavior));
        var receipt = model.FindEntityType(typeof(NeighborhoodSettlementCommandReceiptRecord))!;
        Assert.Equal(["ActorUserId", "RequestId"], receipt.FindPrimaryKey()!.Properties.Select(x => x.Name).ToArray());
        var version = model.FindEntityType(typeof(NeighborhoodSettlementAgreementVersionRecord))!;
        Assert.Equal(PropertySaveBehavior.Throw, version.FindProperty("TermsJson")!.GetAfterSaveBehavior());
        Assert.Equal(PropertySaveBehavior.Throw, version.FindProperty("SourceSnapshotJson")!.GetAfterSaveBehavior());
        Assert.DoesNotContain(entities.SelectMany(x => x.GetProperties()), x => x.Name is "AmountKrw" or "UnitPriceKrw" or "PayerUserId" or "PayeeUserId");
    }

    [Fact]
    public async Task 새migration은격리SQLite에만5테이블을추가하며약정금액과입금배분원문은평문으로저장하지않는다()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync(); await using var db = Db(connection);
        var migration = new AddNeighborhoodSettlement(); Assert.Equal(5, migration.UpOperations.OfType<CreateTableOperation>().Count()); Assert.All(migration.UpOperations, x => Assert.True(x is CreateTableOperation or CreateIndexOperation));
        foreach (var command in db.GetService<IMigrationsSqlGenerator>().Generate(migration.UpOperations, db.GetService<IDesignTimeModel>().Model)) await db.Database.ExecuteSqlRawAsync(command.CommandText);
        AddAgreement(db); db.Add(new NeighborhoodSettlementPaymentRecord { PaymentId = "payment-a", AgreementId = "agreement-a", DetailsJson = "{\"amountKrw\":1234.56,\"payerUserId\":\"buyer-private\",\"allocations\":[{\"entryId\":\"entry-a\",\"amountKrw\":500}]}" });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        foreach (var pair in new[] { ("neighborhood_settlement_agreements", "TermsJson"), ("neighborhood_settlement_versions", "TermsJson"), ("neighborhood_settlement_payments", "DetailsJson") })
        {
            using var command = connection.CreateCommand(); command.CommandText = $"SELECT {pair.Item2} FROM {pair.Item1}"; var raw = (string)(await command.ExecuteScalarAsync())!;
            Assert.StartsWith("protected:", raw); Assert.DoesNotContain("1234.56", raw); Assert.DoesNotContain("buyer-private", raw); Assert.DoesNotContain("goods-owner-private", raw);
        }
        Assert.Contains("1234.56", (await db.Set<NeighborhoodSettlementAgreementRecord>().SingleAsync()).TermsJson);
        Assert.Contains("allocations", (await db.Set<NeighborhoodSettlementPaymentRecord>().SingleAsync()).DetailsJson);
        Assert.Throws<NotSupportedException>(() => migration.DownOperations);
    }

    [Fact]
    public async Task 같은업무항목과같은기존배송원점은다른입력명으로중복발생시키지못한다()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync(); await using var db = Db(connection); await db.Database.EnsureCreatedAsync(); AddAgreement(db);
        db.Add(Entry("first", "origin-a", "cargo:existing-delivery")); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        db.Add(Entry("changed-line", "origin-a", null)); await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear();
        db.Add(Entry("other-source", "origin-b", "cargo:existing-delivery")); await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear();
        Assert.Single(await db.Set<NeighborhoodSettlementEntryRecord>().ToArrayAsync());
        db.Add(Entry("independent-storage", "origin-c", null)); await db.SaveChangesAsync(); Assert.Equal(2, await db.Set<NeighborhoodSettlementEntryRecord>().CountAsync());
    }

    [Fact]
    public async Task 같은사용자완료요청은유일하고다른사용자는같은요청번호로별도영수증을남길수있다()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync(); await using var db = Db(connection); await db.Database.EnsureCreatedAsync(); AddAgreement(db);
        var request = Guid.NewGuid(); db.Add(Receipt("first-actor", request)); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        db.Add(Receipt("first-actor", request)); await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear();
        db.Add(Receipt("second-actor", request)); await db.SaveChangesAsync(); Assert.Equal(2, await db.Set<NeighborhoodSettlementCommandReceiptRecord>().CountAsync());
    }

    [Fact]
    public async Task 확정했던판본조건을덮어쓰지않고새판본을추가하며정산이력의부모를삭제하지못한다()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync(); await using var db = Db(connection); await db.Database.EnsureCreatedAsync(); AddAgreement(db); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var original = await db.Set<NeighborhoodSettlementAgreementVersionRecord>().SingleAsync(); var originalTerms = original.TermsJson; original.TermsJson = "{\"changed\":true}";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear();
        Assert.Equal(originalTerms, (await db.Set<NeighborhoodSettlementAgreementVersionRecord>().AsNoTracking().SingleAsync()).TermsJson);
        db.Add(new NeighborhoodSettlementAgreementVersionRecord { AgreementId = "agreement-a", TermsVersion = 2, TermsJson = "{\"amountKrw\":4321.56}" }); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        Assert.Equal(2, await db.Set<NeighborhoodSettlementAgreementVersionRecord>().CountAsync());
        var agreement = await db.Set<NeighborhoodSettlementAgreementRecord>().SingleAsync(); db.Remove(agreement); await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    // 실제 Context를 유지하면서 다른 시험의 항등 보호기를 캡처한 모델 캐시가 섞이지 않게 합니다.
    private SsalddelContext Db(SqliteConnection connection) => new(new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(connection).UseInternalServiceProvider(_sqliteServices).Options, new Encryption());
    private static void AddAgreement(SsalddelContext db)
    {
        const string details = "{\"amountKrw\":1234.56,\"goodsOwnerUserId\":\"goods-owner-private\"}";
        db.Add(new NeighborhoodSettlementAgreementRecord { AgreementId = "agreement-a", SourceKind = "DistributedLot", SourceId = "lot-a", CreatorUserId = "creator", TermsJson = details });
        db.Add(new NeighborhoodSettlementAgreementVersionRecord { AgreementId = "agreement-a", TermsVersion = 1, TermsJson = details });
    }
    private static NeighborhoodSettlementEntryRecord Entry(string id, string key, string? delivery) => new() { EntryId = id, AgreementId = "agreement-a", TermsVersion = 1, LineKey = id, AccrualKey = key, CanonicalDeliveryOrigin = delivery, DetailsJson = "{\"amountKrw\":1000}" };
    private static NeighborhoodSettlementCommandReceiptRecord Receipt(string actor, Guid request) => new() { ActorUserId = actor, RequestId = request, AgreementId = "agreement-a", InputDigest = "same-input", Kind = "Accrue" };
    private static string? Table(MigrationOperation operation) => operation.GetType().GetProperty("Table")?.GetValue(operation) as string ?? (operation as TableOperation)?.Name;
    private sealed class Encryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value is null ? null : "protected:" + Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
        public string? Unprotect(string? value) => value is null ? null : Encoding.UTF8.GetString(Convert.FromBase64String(value["protected:".Length..]));
    }
}
