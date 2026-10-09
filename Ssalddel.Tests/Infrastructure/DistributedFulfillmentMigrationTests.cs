using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Ssalddel.Migrations;
using 살뜰.Data;
using 살뜰.Infrastructure.Persistence.Configurations.Warehouse;
using 살뜰.Infrastructure.Security;
using 살뜰.도메인.창고;

namespace Ssalddel.Tests.Infrastructure;

public sealed class DistributedFulfillmentMigrationTests
{
    [Fact]
    public void MySQL실제모델과snapshot에분산8테이블이동일하고판본과유일예약키를유지한다()
    {
        using var db = MySql(); var stored = db.GetService<IModelRuntimeInitializer>().Initialize(db.GetService<IMigrationsAssembly>().ModelSnapshot!.Model, designTime: true);
        var model = db.GetService<IDesignTimeModel>().Model;
        var diff = db.GetService<IMigrationsModelDiffer>().GetDifferences(stored.GetRelationalModel(), model.GetRelationalModel())
            .Where(x => 분산물류Model.Tables.Contains(Table(x))).ToArray();
        Assert.True(diff.Length == 0, string.Join("\n", diff.Select(x => x.GetType().Name + ":" + Table(x) + ":" + x.GetType().GetProperty("Name")?.GetValue(x))));
        Assert.True(model.FindEntityType(typeof(분산배송WaveRecord))!.FindProperty("Revision")!.IsConcurrencyToken);
        Assert.Equal(["ActorUserId", "RequestId"], model.FindEntityType(typeof(분산물류CommandReceipt))!.FindPrimaryKey()!.Properties.Select(x => x.Name));
        Assert.Contains(model.FindEntityType(typeof(분산재고예약Record))!.GetIndexes(), x => x.IsUnique && x.Properties.Count == 1 && x.Properties[0].Name == "OutboundPlanId");
    }
    [Fact]
    public void MySQLmigration은기존재고금액변경없이8테이블과실제권한수량영수증을추가한다()
    {
        using var db = MySql(); var migration = new AddDistributedFulfillment();
        Assert.Equal(8, migration.UpOperations.OfType<CreateTableOperation>().Count());
        Assert.All(migration.UpOperations, x => Assert.True(x is CreateTableOperation or CreateIndexOperation));
        var sql = string.Join("\n", db.GetService<IMigrationsSqlGenerator>().Generate(migration.UpOperations, db.GetService<IDesignTimeModel>().Model).Select(x => x.CommandText));
        foreach (var table in 분산물류Model.Tables.Take(8)) Assert.Contains(table, sql);
        Assert.DoesNotContain("UPDATE ", sql, StringComparison.OrdinalIgnoreCase); Assert.DoesNotContain("DROP ", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SellerUserId", sql); Assert.Contains("OwnerUserId", sql); Assert.Contains("ReturnedQuantity", sql); Assert.Contains("RequestId", sql);
        Assert.Throws<NotSupportedException>(() => migration.DownOperations);
    }
    [Fact]
    public async Task SQLite이전schema에서Up를실행해기존행을보존하고동의와요청이재조회된다()
    {
        await using var c = new SqliteConnection("Data Source=:memory:"); await c.OpenAsync();
        await using var db = new SsalddelContext(new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(c).Options, new Encryption());
        await db.Database.ExecuteSqlRawAsync("CREATE TABLE preserved_stock (owner TEXT NOT NULL, quantity INTEGER NOT NULL); INSERT INTO preserved_stock VALUES ('legacy-owner',7);");
        foreach (var command in db.GetService<IMigrationsSqlGenerator>().Generate(new AddDistributedFulfillment().UpOperations, db.GetService<IDesignTimeModel>().Model))
            await db.Database.ExecuteSqlRawAsync(command.CommandText);
        var id = Guid.NewGuid(); db.Add(new 분산거점Record { SiteId = "fixture-site", WarehouseId = 1, PlaceId = "warehouse:1", OwnerUserId = "fixture-owner", ManagerUserId = "fixture-manager", Revision = 3, OwnerConsented = true, ManagerConsented = true });
        db.Add(new 분산물류CommandReceipt { ActorUserId = "fixture-owner", RequestId = id, InputDigest = "fixture-digest", Kind = "Consent", AggregateId = "fixture-site", Revision = 3, RecordedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        Assert.Equal(3, (await db.Set<분산거점Record>().SingleAsync()).Revision); Assert.Equal(id, (await db.Set<분산물류CommandReceipt>().SingleAsync()).RequestId);
        db.Add(new 분산적재위치Record { LocationId = "fixture-location", SiteId = "fixture-site", Barcode = "fixture-barcode", Label = "보존 위치" });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        await Assert.ThrowsAsync<SqliteException>(() => db.Database.ExecuteSqlRawAsync("DELETE FROM distributed_sites WHERE SiteId='fixture-site';"));
        await Assert.ThrowsAsync<SqliteException>(() => db.Database.ExecuteSqlRawAsync("INSERT INTO distributed_locations (LocationId,SiteId,Barcode,Label,Revision) VALUES ('orphan','missing','orphan-barcode','unknown',1);"));
        Assert.Throws<NotSupportedException>(() => new AddDistributedFulfillment().DownOperations);
        Assert.Equal(1, await db.Set<분산적재위치Record>().CountAsync()); Assert.Equal(id, (await db.Set<분산물류CommandReceipt>().SingleAsync()).RequestId);
        using var commandRead = c.CreateCommand(); commandRead.CommandText = "SELECT quantity FROM preserved_stock WHERE owner='legacy-owner'";
        Assert.Equal(7L, await commandRead.ExecuteScalarAsync());
    }
    private static string? Table(MigrationOperation x) => x.GetType().GetProperty("Table")?.GetValue(x) as string ?? (x as TableOperation)?.Name;
    private static SsalddelContext MySql() => new(new DbContextOptionsBuilder<SsalddelContext>()
        .UseMySql("Server=127.0.0.1;Database=fixture_not_opened;User ID=fixture;Password=fixture;", new MySqlServerVersion(new Version(8, 4, 0)), o => o.MigrationsAssembly("Ssalddel")).Options, new Encryption());
    private sealed class Encryption : IPersonalDataEncryptionService { public string? Protect(string? v) => v; public string? Unprotect(string? v) => v; }
}
