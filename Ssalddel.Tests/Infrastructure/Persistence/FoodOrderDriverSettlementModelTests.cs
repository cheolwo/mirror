using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Ssalddel.Application.Admin.Food;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.도메인.음식;

namespace Ssalddel.Tests.Infrastructure.Persistence;

public sealed class FoodOrderDriverSettlementModelTests
{
    [Fact]
    public void 정산과모의증빙의원장관계_멱등고유키_동시성판본이Migration과Snapshot에보존된다()
    {
        using var context = new SsalddelContext(new DbContextOptionsBuilder<SsalddelContext>()
            .UseMySql("Server=localhost;Database=food_settlement_model_test;User=root;Password=test;",
                new MySqlServerVersion(new Version(8, 4, 0)),
                options => options.MigrationsAssembly(typeof(음식주문기사정산UseCase).Assembly.GetName().Name))
            .Options, new Encryption());
        var assembly = context.GetService<IMigrationsAssembly>();
        var migration = assembly.CreateMigration(assembly.Migrations["20261003103000_AddFoodOrderDriverSettlement"], context.Database.ProviderName!);
        var tables = migration.UpOperations.OfType<CreateTableOperation>().ToArray();
        Assert.Equal(2, tables.Length);
        var settlement = tables.Single(x => x.Name == "음식주문기사정산");
        Assert.Contains(settlement.ForeignKeys, x => x.PrincipalTable == "음식주문" && x.PrincipalColumns.SequenceEqual(["id"]));
        Assert.Contains(settlement.ForeignKeys, x => x.PrincipalTable == "음식배달시도" && x.PrincipalColumns.SequenceEqual(["id"]));
        Assert.Contains(settlement.ForeignKeys, x => x.PrincipalTable == "운송실행투영" && x.PrincipalColumns.SequenceEqual(["id"]));
        var snapshot = Assert.IsAssignableFrom<ModelSnapshot>(Activator.CreateInstance(
            typeof(음식주문기사정산UseCase).Assembly.GetType("Ssalddel.Migrations.SsalddelContextModelSnapshot")!, nonPublic: true));
        foreach (var type in new[] { typeof(음식주문기사정산), typeof(음식주문기사지급검증) })
        {
            var runtime = context.Model.FindEntityType(type)!;
            var saved = snapshot.Model.FindEntityType(type.FullName!)!;
            Assert.Equal(runtime.GetTableName(), saved.GetTableName());
            Assert.Equal(runtime.GetProperties().Select(x => x.Name).Order(), saved.GetProperties().Select(x => x.Name).Order());
            foreach (var property in runtime.GetProperties())
            {
                var snap = saved.FindProperty(property.Name)!;
                Assert.Equal(property.IsNullable, snap.IsNullable);
                Assert.Equal(property.IsConcurrencyToken, snap.IsConcurrencyToken);
                Assert.Equal(property.GetMaxLength(), snap.GetMaxLength());
                Assert.Equal(property.GetColumnType(), snap.GetColumnType());
            }
            Assert.Equal(runtime.GetForeignKeys().Count(), saved.GetForeignKeys().Count());
            Assert.Equal(runtime.GetIndexes().Count(), saved.GetIndexes().Count());
        }
        Assert.Contains(migration.UpOperations.OfType<CreateIndexOperation>(), x => x.Table == "음식주문기사정산" && x.Columns.SequenceEqual(["주문번호"]) && x.IsUnique);
        Assert.Contains(migration.UpOperations.OfType<CreateIndexOperation>(), x => x.Table == "음식주문기사지급검증" && x.Columns.SequenceEqual(["멱등키"]) && x.IsUnique);
    }
    private sealed class Encryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
