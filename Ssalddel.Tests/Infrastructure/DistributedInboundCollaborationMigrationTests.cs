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
using 살뜰.Infrastructure.Persistence.Configurations.Warehouse;
using 살뜰.Infrastructure.Security;
using 살뜰.도메인.창고;

namespace Ssalddel.Tests.Infrastructure;

public sealed class DistributedInboundCollaborationMigrationTests : IDisposable
{
    private readonly ServiceProvider sqliteServices = new ServiceCollection().AddEntityFrameworkSqlite().BuildServiceProvider();
    public void Dispose() => sqliteServices.Dispose();

    [Fact]
    public void 현재모델과스냅샷이일치하고협의확장은기존업무수량을변경하지않는다()
    {
        using var db = new SsalddelContext(new DbContextOptionsBuilder<SsalddelContext>()
            .UseMySql("Server=127.0.0.1;Database=fixture_not_opened;User ID=fixture;Password=fixture;",
                new MySqlServerVersion(new Version(8, 4, 0)), o => o.MigrationsAssembly("Ssalddel")).Options, new Encryption());
        var snapshot = db.GetService<IModelRuntimeInitializer>().Initialize(db.GetService<IMigrationsAssembly>().ModelSnapshot!.Model, designTime: true);
        var model = db.GetService<IDesignTimeModel>().Model;
        var differences = db.GetService<IMigrationsModelDiffer>().GetDifferences(snapshot.GetRelationalModel(), model.GetRelationalModel())
            .Where(x => 분산물류Model.Tables.Contains(Table(x))).ToArray();
        Assert.True(differences.Length == 0, string.Join("\n", differences.Select(x => x.GetType().Name + ":" + Table(x))));
        var migration = new AddDistributedInboundCollaboration();
        Assert.Equal(7, migration.UpOperations.Count);
        Assert.All(migration.UpOperations, x => Assert.IsType<AddColumnOperation>(x));
        var sql = string.Join("\n", db.GetService<IMigrationsSqlGenerator>().Generate(migration.UpOperations, model).Select(x => x.CommandText));
        Assert.DoesNotContain("UPDATE ", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DROP ", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Throws<NotSupportedException>(() => migration.DownOperations);
        foreach (var pair in new[]
        {
            (typeof(분산거점Record), "AcceptancePolicyJson"),
            (typeof(분산재고LotRecord), "InboundCollaborationJson"),
            (typeof(분산보관합의Record), "PendingTermsJson")
        })
        {
            var property = model.FindEntityType(pair.Item1)!.FindProperty(pair.Item2)!;
            Assert.True(property.IsNullable);
            Assert.NotNull(property.GetValueConverter());
        }
    }

    [Fact]
    public async Task 이전공간과요청영수증은보존하며새조건없는공간을임의로활성화하지않는다()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new SsalddelContext(new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(connection).UseInternalServiceProvider(sqliteServices).Options, new Encryption());
        var model = db.GetService<IDesignTimeModel>().Model;
        var generator = db.GetService<IMigrationsSqlGenerator>();
        foreach (var migration in new Migration[] { new AddDistributedFulfillment(), new AddDistributedCompletionLineEvidence(), new AddDistributedStorageProof() })
            foreach (var command in generator.Generate(migration.UpOperations, model))
                await db.Database.ExecuteSqlRawAsync(command.CommandText);
        using var insert = connection.CreateCommand();
        insert.CommandText = """
            INSERT INTO distributed_sites
            (SiteId,WarehouseId,PlaceId,PublicCandidateRef,PublicLabel,OwnerUserId,ManagerUserId,PrivateDetailsJson,
             SpaceConfirmed,PickupPointConfirmed,OwnerConsented,ManagerConsented,ConsentEvidenceJson,StatusCode,Revision)
            VALUES ('legacy-site',1,'warehouse:1','','기존 합성 공간','owner','manager','{}',1,1,1,1,'[]','Active',7);
            """;
        await insert.ExecuteNonQueryAsync();
        var request = Guid.NewGuid();
        db.Add(new 분산물류CommandReceipt { ActorUserId = "owner", RequestId = request, InputDigest = "preserved-digest",
            Kind = "Consent", AggregateId = "legacy-site", Revision = 7, StatusCode = "Active", RecordedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        foreach (var command in generator.Generate(new AddDistributedInboundCollaboration().UpOperations, model))
            await db.Database.ExecuteSqlRawAsync(command.CommandText);
        var site = await db.Set<분산거점Record>().SingleAsync();
        Assert.Equal(7, site.Revision);
        Assert.Equal("Active", site.StatusCode);
        Assert.Null(site.AcceptancePolicyJson);
        Assert.Equal(0, site.AcceptancePolicyRevision);
        Assert.Equal(request, (await db.Set<분산물류CommandReceipt>().SingleAsync()).RequestId);
        Assert.Equal("preserved-digest", (await db.Set<분산물류CommandReceipt>().SingleAsync()).InputDigest);
    }

    [Fact]
    public async Task 수용조건과비공개협의및미확정계약내용은암호화되어다시읽힌다()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new SsalddelContext(new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(connection).UseInternalServiceProvider(sqliteServices).Options, new Encryption());
        await db.Database.EnsureCreatedAsync();
        db.Add(new 분산거점Record { SiteId = "private-site", WarehouseId = 1, AcceptancePolicyJson = "정책 합성 비공개 문구", AcceptancePolicyRevision = 1 });
        db.Add(new 분산적재위치Record { SiteId = "private-site", LocationId = "private-location", Barcode = "fixture-barcode" });
        db.Add(new 분산입고계획Record { PlanId = "private-plan" });
        db.Add(new 분산재고LotRecord { LotId = "private-lot", PlanId = "private-plan", LocationId = "private-location", SiteId = "private-site", InboundItemId = 1,
            LineKey = "line", InboundCollaborationJson = "협의 합성 비공개 문구" });
        db.Add(new 분산보관합의Record { AgreementId = "private-agreement", SiteId = "private-site", LocationId = "private-location",
            GoodsOwnerUserId = "owner", PendingTermsJson = "변경안 합성 비공개 문구", PendingTermsVersion = 2 });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        foreach (var source in new[]
        {
            ("distributed_sites", "AcceptancePolicyJson"),
            ("distributed_lots", "InboundCollaborationJson"),
            ("distributed_storage_agreements", "PendingTermsJson")
        })
        {
            using var command = connection.CreateCommand();
            command.CommandText = $"SELECT {source.Item2} FROM {source.Item1}";
            var raw = Assert.IsType<string>(await command.ExecuteScalarAsync());
            Assert.StartsWith("enc:", raw);
            Assert.DoesNotContain("비공개", raw);
        }
        Assert.Equal("정책 합성 비공개 문구", (await db.Set<분산거점Record>().SingleAsync()).AcceptancePolicyJson);
        Assert.Equal("협의 합성 비공개 문구", (await db.Set<분산재고LotRecord>().SingleAsync()).InboundCollaborationJson);
        Assert.Equal("변경안 합성 비공개 문구", (await db.Set<분산보관합의Record>().SingleAsync()).PendingTermsJson);
    }

    private static string? Table(MigrationOperation operation) => operation.GetType().GetProperty("Table")?.GetValue(operation) as string ?? (operation as TableOperation)?.Name;
    private sealed class Encryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value is null ? null : "enc:" + Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
        public string? Unprotect(string? value) => value?.StartsWith("enc:", StringComparison.Ordinal) == true
            ? Encoding.UTF8.GetString(Convert.FromBase64String(value[4..])) : value;
    }
}
