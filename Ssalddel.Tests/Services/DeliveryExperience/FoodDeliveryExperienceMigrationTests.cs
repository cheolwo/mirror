using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.도메인.음식;

namespace Ssalddel.Tests.Services.DeliveryExperience;

public sealed class FoodDeliveryExperienceMigrationTests
{
    [Fact]
    public void MySql_additive_migration_and_snapshot_match_without_opening_database()
    {
        using var db = new SsalddelContext(new DbContextOptionsBuilder<SsalddelContext>()
            .UseMySql("Server=127.0.0.1;Database=not_opened;User ID=fixture;Password=fixture;",
                new MySqlServerVersion(new Version(8, 4, 0)), o => o.MigrationsAssembly("Ssalddel"))
            .Options, new FixtureEncryption());
        var current = db.GetService<IDesignTimeModel>().Model;
        var stored = db.GetService<IModelRuntimeInitializer>().Initialize(
            db.GetService<IMigrationsAssembly>().ModelSnapshot!.Model, designTime: true);
        string[] tables = ["음식배달경험공유설정", "음식배달경험완료Outbox", "음식배달경험Fact", "음식배달전달지역Snapshot", "음식배달시도"];
        var relevant = db.GetService<IMigrationsModelDiffer>().GetDifferences(stored.GetRelationalModel(), current.GetRelationalModel())
            .Where(x => tables.Contains(x.GetType().GetProperty("Table")?.GetValue(x) as string
                ?? x.GetType().GetProperty("Name")?.GetValue(x) as string ?? "", StringComparer.Ordinal)).ToArray();
        Assert.True(relevant.Length == 0, string.Join(",", relevant.Select(x => x.GetType().Name)));
        var operations = new Ssalddel.Migrations.AddFoodDeliveryExperienceR47().UpOperations;
        Assert.Equal(4, operations.OfType<CreateTableOperation>().Count());
        var column = Assert.Single(operations.OfType<AddColumnOperation>());
        Assert.Equal("음식배달시도", column.Table);
        Assert.Equal("experience_execution_mode_code", column.Name);
        Assert.True(column.IsNullable); // 유산 기록의 실행모드를 임의로 채우지 않습니다.
        Assert.All(operations, x => Assert.True(x is CreateTableOperation or CreateIndexOperation or AddColumnOperation));
        var sql = string.Join("\n", db.GetService<IMigrationsSqlGenerator>().Generate(operations, current).Select(x => x.CommandText));
        Assert.All(tables.Take(4), name => Assert.Contains(name, sql));
        Assert.DoesNotContain("UPDATE ", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DROP ", sql, StringComparison.OrdinalIgnoreCase);
        Assert.True(current.FindEntityType(typeof(음식배달경험공유설정))!.FindProperty(nameof(음식배달경험공유설정.Revision))!.IsConcurrencyToken);
        Assert.Contains(current.FindEntityType(typeof(음식배달경험완료Outbox))!.GetIndexes(),
            x => x.IsUnique && x.Properties.Select(p => p.Name).SequenceEqual(new[] { "기사Id", "주문번호", "완료종류Code", "실행모드Code" }));
        Assert.Contains(current.FindEntityType(typeof(음식배달경험Fact))!.GetIndexes(),
            x => x.IsUnique && x.Properties.Select(p => p.Name).SequenceEqual(new[] { "완료OutboxId", "장소종류Code" }));
    }

    private sealed class FixtureEncryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
