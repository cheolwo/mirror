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
using 살뜰.Infrastructure.Security;
using 살뜰.도메인.정산;

namespace Ssalddel.Tests.Infrastructure;

public sealed class NeighborhoodInventoryCostAttributionMigrationTests : IDisposable
{
    private const string TableName = "NeighborhoodInventoryCostAttributions";
    private readonly ServiceProvider sqliteServices = new ServiceCollection().AddEntityFrameworkSqlite().BuildServiceProvider();

    public void Dispose() => sqliteServices.Dispose();

    [Fact]
    public void MySQL원가귀속모델과snapshot은보호상세유일키불변필드를같이유지한다()
    {
        using var db = new SsalddelContext(new DbContextOptionsBuilder<SsalddelContext>()
            .UseMySql("Server=127.0.0.1;Database=not_opened;User ID=fixture;Password=fixture;",
                new MySqlServerVersion(new Version(8, 4, 0)), o => o.MigrationsAssembly("Ssalddel")).Options, new Encryption());
        var snapshot = db.GetService<IModelRuntimeInitializer>().Initialize(db.GetService<IMigrationsAssembly>().ModelSnapshot!.Model, designTime: true);
        var model = db.GetService<IDesignTimeModel>().Model;
        var differences = db.GetService<IMigrationsModelDiffer>()
            .GetDifferences(snapshot.GetRelationalModel(), model.GetRelationalModel()).Where(x => Table(x) == TableName).ToArray();
        Assert.True(differences.Length == 0, string.Join("\n", differences.Select(x => x.GetType().Name + ":" + Table(x))));
        var entity = model.FindEntityType(typeof(NeighborhoodInventoryCostAttributionRecord))!;
        Assert.Equal(TableName, entity.GetTableName());
        Assert.Equal(["AttributionId"], entity.FindPrimaryKey()!.Properties.Select(x => x.Name).ToArray());
        foreach (var name in new[] { "AttributionId", "SourceEntryId", "SourceAgreementId", "OrderId", "AgreementId" })
            Assert.Equal(120, entity.FindProperty(name)!.GetMaxLength());
        Assert.Equal(64, entity.FindProperty("CalculationDigest")!.GetMaxLength());
        Assert.Equal("longtext", entity.FindProperty("DetailsJson")!.GetColumnType());
        Assert.NotNull(entity.FindProperty("DetailsJson")!.GetValueConverter());
        Assert.All(entity.GetProperties(), x => Assert.Equal(PropertySaveBehavior.Throw, x.GetAfterSaveBehavior()));
        Assert.Contains(entity.GetIndexes(), x => x.IsUnique && x.Properties.Select(p => p.Name).SequenceEqual(["SourceEntryId", "OrderId"]));
        Assert.Contains(entity.GetIndexes(), x => x.Properties.Select(p => p.Name).SequenceEqual(["SourceAgreementId"]));
        Assert.DoesNotContain(entity.GetProperties(), x => x.Name is "AmountKrw" or "Quantity" or "PayerUserId" or "PayeeUserId");
    }

    [Fact]
    public async Task 새migration은기존정산을그대로두고보호된원가귀속테이블만추가한다()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = Db(connection);
        var generator = db.GetService<IMigrationsSqlGenerator>(); var model = db.GetService<IDesignTimeModel>().Model;
        foreach (var command in generator.Generate(new AddNeighborhoodSettlement().UpOperations, model))
            await db.Database.ExecuteSqlRawAsync(command.CommandText);
        AddOriginalCost(db); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var migration = new AddNeighborhoodInventoryCostAttribution();
        Assert.Single(migration.UpOperations.OfType<CreateTableOperation>());
        Assert.Equal(2, migration.UpOperations.OfType<CreateIndexOperation>().Count());
        Assert.All(migration.UpOperations, x => Assert.True(x is CreateTableOperation or CreateIndexOperation));
        Assert.All(migration.UpOperations, x => Assert.Equal(TableName, Table(x)));
        foreach (var command in generator.Generate(migration.UpOperations, model))
            await db.Database.ExecuteSqlRawAsync(command.CommandText);
        Assert.Equal("{\"amountKrw\":101,\"quantity\":3}", (await db.Set<NeighborhoodSettlementEntryRecord>().SingleAsync()).DetailsJson);
        Assert.Empty(await db.Set<NeighborhoodInventoryCostAttributionRecord>().ToArrayAsync());
        db.Add(Attribution("cost-a", "entry-a", "order-a")); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        using var raw = connection.CreateCommand(); raw.CommandText = $"SELECT DetailsJson FROM {TableName}";
        var protectedDetails = (string)(await raw.ExecuteScalarAsync())!;
        Assert.StartsWith("protected:", protectedDetails);
        Assert.DoesNotContain("amountKrw", protectedDetails); Assert.DoesNotContain("private-basis", protectedDetails);
        Assert.Equal("{\"amountKrw\":34,\"quantity\":1,\"basisReference\":\"private-basis\"}",
            (await db.Set<NeighborhoodInventoryCostAttributionRecord>().SingleAsync()).DetailsJson);
        Assert.Single(await db.Set<NeighborhoodSettlementEntryRecord>().ToArrayAsync());
        Assert.Throws<NotSupportedException>(() => migration.DownOperations);
    }

    [Fact]
    public async Task 원천비용과주문이같으면약정이나귀속번호를바꾸어도중복기록하지못한다()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = Db(connection); await db.Database.EnsureCreatedAsync();
        db.Add(Attribution("cost-a", "entry-a", "order-a")); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var duplicate = Attribution("changed-id", "entry-a", "order-a"); duplicate.AgreementId = "changed-agreement";
        db.Add(duplicate); await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear();
        Assert.Single(await db.Set<NeighborhoodInventoryCostAttributionRecord>().ToArrayAsync());
        db.Add(Attribution("cost-b", "entry-a", "order-b"));
        db.Add(Attribution("cost-c", "entry-b", "order-a")); await db.SaveChangesAsync();
        Assert.Equal(3, await db.Set<NeighborhoodInventoryCostAttributionRecord>().CountAsync());
    }

    [Theory]
    [InlineData("SourceEntryId")]
    [InlineData("SourceAgreementId")]
    [InlineData("OrderId")]
    [InlineData("AgreementId")]
    [InlineData("CalculationDigest")]
    [InlineData("DetailsJson")]
    [InlineData("RecordedAtUtc")]
    public async Task 저장된원가귀속근거는덮어쓰지못한다(string propertyName)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = Db(connection); await db.Database.EnsureCreatedAsync();
        db.Add(Attribution("cost-a", "entry-a", "order-a")); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var saved = await db.Set<NeighborhoodInventoryCostAttributionRecord>().SingleAsync();
        var property = db.Entry(saved).Property(propertyName); var original = property.CurrentValue;
        property.CurrentValue = propertyName == "RecordedAtUtc" ? saved.RecordedAtUtc.AddMinutes(1) : "changed-private-value";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear();
        var unchanged = await db.Set<NeighborhoodInventoryCostAttributionRecord>().SingleAsync();
        Assert.Equal(original, db.Entry(unchanged).Property(propertyName).CurrentValue);
    }

    private SsalddelContext Db(SqliteConnection connection) => new(new DbContextOptionsBuilder<SsalddelContext>()
        .UseSqlite(connection).UseInternalServiceProvider(sqliteServices).Options, new Encryption());
    private static void AddOriginalCost(SsalddelContext db)
    {
        db.Add(new NeighborhoodSettlementAgreementRecord { AgreementId = "source-agreement", SourceKind = "DistributedLot", SourceId = "lot-a" });
        db.Add(new NeighborhoodSettlementAgreementVersionRecord { AgreementId = "source-agreement", TermsVersion = 1 });
        db.Add(new NeighborhoodSettlementEntryRecord { EntryId = "entry-a", AgreementId = "source-agreement", TermsVersion = 1,
            LineKey = "inbound", Kind = "Inbound", OriginKind = "InventoryHistory", OriginId = "11", AccrualKey = "original-cost",
            StatusCode = "Confirmed", DetailsJson = "{\"amountKrw\":101,\"quantity\":3}" });
    }
    private static NeighborhoodInventoryCostAttributionRecord Attribution(string id, string source, string order) => new()
    {
        AttributionId = id, SourceEntryId = source, SourceAgreementId = "source-agreement", OrderId = order,
        AgreementId = "outbound-agreement", CalculationDigest = "calculation-a",
        DetailsJson = "{\"amountKrw\":34,\"quantity\":1,\"basisReference\":\"private-basis\"}",
        RecordedAtUtc = new DateTime(2026, 10, 12, 0, 0, 0, DateTimeKind.Utc)
    };
    private static string? Table(MigrationOperation operation) => operation.GetType().GetProperty("Table")?.GetValue(operation) as string
        ?? (operation as TableOperation)?.Name;
    private sealed class Encryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value is null ? null : "protected:" + Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
        public string? Unprotect(string? value) => value is null ? null : Encoding.UTF8.GetString(Convert.FromBase64String(value["protected:".Length..]));
    }
}
