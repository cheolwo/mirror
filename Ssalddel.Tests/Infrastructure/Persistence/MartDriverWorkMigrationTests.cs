using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Ssalddel.Migrations;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.도메인.마트;

namespace Ssalddel.Tests.Infrastructure.Persistence;

public sealed class MartDriverWorkMigrationTests
{
    [Fact]
    public void MySql_네가지추가테이블과Snapshot이현재모델의멱등키_판본_열을보존한다()
    {
        using var context = new SsalddelContext(new DbContextOptionsBuilder<SsalddelContext>()
            .UseMySql("Server=localhost;Database=mart_driver_model_test;User=test;Password=test;",
                new MySqlServerVersion(new Version(8, 4, 0))).Options, new Encryption());
        var migration = new AddMartDriverWorkR58 { ActiveProvider = context.Database.ProviderName! };
        var tables = migration.UpOperations.OfType<CreateTableOperation>().ToArray();
        Assert.Equal(4, tables.Length);
        Assert.All(migration.UpOperations, x => Assert.True(x is CreateTableOperation or CreateIndexOperation));
        var snapshot = Assert.IsAssignableFrom<ModelSnapshot>(Activator.CreateInstance(
            typeof(AddMartDriverWorkR58).Assembly.GetType("Ssalddel.Migrations.SsalddelContextModelSnapshot")!, nonPublic: true));
        foreach (var type in new[] { typeof(마트배송시도), typeof(마트포장전표), typeof(마트기사요청영수증), typeof(마트기사후속Outbox) })
        {
            var runtime = context.Model.FindEntityType(type)!;
            var saved = snapshot.Model.FindEntityType(type.FullName!)!;
            var table = tables.Single(x => x.Name == runtime.GetTableName());
            Assert.Equal(runtime.GetTableName(), saved.GetTableName());
            Assert.Equal(runtime.GetProperties().Select(x => x.Name).Order(), saved.GetProperties().Select(x => x.Name).Order());
            Assert.Equal(runtime.FindPrimaryKey()!.Properties.Select(x => x.Name), saved.FindPrimaryKey()!.Properties.Select(x => x.Name));
            Assert.Equal(runtime.FindPrimaryKey()!.Properties.Select(x => x.Name), table.PrimaryKey!.Columns);
            foreach (var property in runtime.GetProperties())
            {
                var snap = saved.FindProperty(property.Name)!;
                var column = table.Columns.Single(x => x.Name == property.Name);
                Assert.Equal(property.IsNullable, snap.IsNullable);
                Assert.Equal(property.IsNullable, column.IsNullable);
                Assert.Equal(property.IsConcurrencyToken, snap.IsConcurrencyToken);
                Assert.Equal(property.GetMaxLength(), snap.GetMaxLength());
                Assert.Equal(property.GetMaxLength(), column.MaxLength);
                Assert.Equal(property.GetColumnType(), snap.GetColumnType());
                Assert.Equal(property.GetColumnType(), column.ColumnType);
            }
            Assert.Equal(runtime.GetIndexes().Count(), saved.GetIndexes().Count());
            foreach (var index in runtime.GetIndexes())
            {
                var names = index.Properties.Select(x => x.Name).ToArray();
                Assert.Contains(saved.GetIndexes(), x => x.IsUnique == index.IsUnique && x.Properties.Select(p => p.Name).SequenceEqual(names));
                Assert.Contains(migration.UpOperations.OfType<CreateIndexOperation>(), x => x.Table == table.Name && x.IsUnique == index.IsUnique && x.Columns.SequenceEqual(names));
            }
        }
        var sql = string.Join('\n', context.GetService<IMigrationsSqlGenerator>().Generate(migration.UpOperations).Select(x => x.CommandText));
        Assert.DoesNotContain("UPDATE ", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DROP ", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CREATE UNIQUE INDEX", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(tables.Select(x => x.Name).Order(), migration.DownOperations.OfType<DropTableOperation>().Select(x => x.Name).Order());
    }

    private sealed class Encryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
