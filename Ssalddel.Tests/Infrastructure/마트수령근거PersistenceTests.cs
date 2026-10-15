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
using 살뜰.도메인.마트;

namespace Ssalddel.Tests.Infrastructure;

public sealed class 마트수령근거PersistenceTests : IDisposable
{
    private readonly ServiceProvider _sqliteServices = new ServiceCollection().AddEntityFrameworkSqlite().BuildServiceProvider();
    public void Dispose() => _sqliteServices.Dispose();

    [Fact]
    public async Task 격리migration은기존배송시도에nullable열하나만추가하고과거시도를수령으로승격하지않는다()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(); await using var db = Db(connection);
        await db.Database.ExecuteSqlRawAsync("CREATE TABLE 마트배송시도 (시도StableId TEXT PRIMARY KEY)");
        await db.Database.ExecuteSqlRawAsync("INSERT INTO 마트배송시도 (시도StableId) VALUES ('legacy-attempt')");
        var migration = new AddMartCustomerReceipt();
        var column = Assert.IsType<AddColumnOperation>(Assert.Single(migration.UpOperations));
        Assert.Equal("마트배송시도", column.Table); Assert.Equal("customer_receipt_evidence_json", column.Name);
        Assert.True(column.IsNullable);
        foreach (var command in db.GetService<IMigrationsSqlGenerator>().Generate(migration.UpOperations, db.GetService<IDesignTimeModel>().Model))
            await db.Database.ExecuteSqlRawAsync(command.CommandText);
        using var query = connection.CreateCommand();
        query.CommandText = "SELECT customer_receipt_evidence_json IS NULL FROM 마트배송시도 WHERE 시도StableId='legacy-attempt'";
        Assert.Equal(1L, await query.ExecuteScalarAsync());
        var drop = Assert.IsType<DropColumnOperation>(Assert.Single(migration.DownOperations));
        Assert.Equal(column.Table, drop.Table); Assert.Equal(column.Name, drop.Name);
    }

    [Fact]
    public async Task 수령과보류원문은보호저장하고배송시도판본으로낡은수정을막는다()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var first = Db(connection); await first.Database.EnsureCreatedAsync();
        first.Set<마트배송시도>().Add(new() { 시도StableId = "private-attempt", 배차의뢰Id = "private-order",
            주문참조번호 = "private-order", 기사Id = "private-driver", Revision = 3, 상태Code = "Delivered" });
        await first.SaveChangesAsync(); first.ChangeTracker.Clear();
        await using var second = Db(connection);
        var current = await first.Set<마트배송시도>().SingleAsync();
        var stale = await second.Set<마트배송시도>().SingleAsync();
        const string evidence = "{\"ConfirmedProof\":{\"BuyerUserId\":\"private-buyer\"},\"Hold\":{\"ReasonCode\":\"Missing\"}}";
        current.고객수령근거Json = evidence; current.Revision++;
        await first.SaveChangesAsync();
        stale.고객수령근거Json = "{\"stale\":true}"; stale.Revision++;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
        using var query = connection.CreateCommand();
        query.CommandText = "SELECT customer_receipt_evidence_json FROM 마트배송시도 WHERE 시도StableId='private-attempt'";
        var raw = Assert.IsType<string>(await query.ExecuteScalarAsync());
        Assert.StartsWith("protected:", raw); Assert.DoesNotContain("private-buyer", raw); Assert.DoesNotContain("Missing", raw);
        first.ChangeTracker.Clear();
        var preserved = await first.Set<마트배송시도>().SingleAsync();
        Assert.Equal(evidence, preserved.고객수령근거Json); Assert.Equal(4, preserved.Revision); Assert.Equal("Delivered", preserved.상태Code);
    }

    [Fact]
    public void 실제MySQL모델과snapshot의수령보호열과동시성은일치한다()
    {
        using var db = new SsalddelContext(new DbContextOptionsBuilder<SsalddelContext>().UseMySql(
            "Server=127.0.0.1;Database=not_opened;User ID=fixture;Password=fixture;", new MySqlServerVersion(new Version(8, 4, 0)),
            options => options.MigrationsAssembly("Ssalddel")).Options, new Encryption());
        var snapshot = db.GetService<IModelRuntimeInitializer>().Initialize(db.GetService<IMigrationsAssembly>().ModelSnapshot!.Model, designTime: true);
        var model = db.GetService<IDesignTimeModel>().Model;
        Assert.Empty(db.GetService<IMigrationsModelDiffer>().GetDifferences(snapshot.GetRelationalModel(), model.GetRelationalModel())
            .Where(operation => Table(operation) == "마트배송시도"));
        var entity = model.FindEntityType(typeof(마트배송시도))!;
        var property = entity.FindProperty(nameof(마트배송시도.고객수령근거Json))!;
        Assert.Equal("customer_receipt_evidence_json", property.GetColumnName());
        Assert.True(property.IsNullable); Assert.NotNull(property.GetValueConverter());
        Assert.True(entity.FindProperty(nameof(마트배송시도.Revision))!.IsConcurrencyToken);
    }

    private SsalddelContext Db(SqliteConnection connection)
        => new(new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(connection).UseInternalServiceProvider(_sqliteServices).Options, new Encryption());
    private static string? Table(MigrationOperation operation)
        => operation.GetType().GetProperty("Table")?.GetValue(operation) as string ?? (operation as TableOperation)?.Name;
    private sealed class Encryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value is null ? null : "protected:" + Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
        public string? Unprotect(string? value) => value is null ? null : Encoding.UTF8.GetString(Convert.FromBase64String(value["protected:".Length..]));
    }
}
