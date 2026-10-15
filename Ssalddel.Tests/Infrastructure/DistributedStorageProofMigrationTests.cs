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

public sealed class DistributedStorageProofMigrationTests
{
    [Fact]
    public void MySQL사진보관합의모델snapshot과새migration이정합하며과거완료를승격하지않는다()
    {
        using var db = new SsalddelContext(new DbContextOptionsBuilder<SsalddelContext>().UseMySql("Server=127.0.0.1;Database=not_opened;User ID=fixture;Password=fixture;", new MySqlServerVersion(new Version(8, 4, 0)), o => o.MigrationsAssembly("Ssalddel")).Options, new Encryption());
        var stored = db.GetService<IModelRuntimeInitializer>().Initialize(db.GetService<IMigrationsAssembly>().ModelSnapshot!.Model, designTime: true);
        var model = db.GetService<IDesignTimeModel>().Model; var tables = 분산물류Model.Tables.Concat(분산사진증빙Model.Tables).ToArray();
        var diff = db.GetService<IMigrationsModelDiffer>().GetDifferences(stored.GetRelationalModel(), model.GetRelationalModel()).Where(x => tables.Contains(Table(x))).ToArray();
        Assert.True(diff.Length == 0, string.Join("\n", diff.Select(x => x.GetType().Name + ":" + Table(x) + ":" + x.GetType().GetProperty("Name")?.GetValue(x))));
        var migration = new AddDistributedStorageProof(); Assert.Equal(3, migration.UpOperations.OfType<CreateTableOperation>().Count());
        Assert.Equal(2, migration.UpOperations.OfType<AddColumnOperation>().Count());
        var sql = string.Join("\n", db.GetService<IMigrationsSqlGenerator>().Generate(migration.UpOperations, model).Select(x => x.CommandText));
        Assert.Contains("distributed_photo_evidence", sql); Assert.Contains("distributed_storage_agreements", sql); Assert.DoesNotContain("UPDATE ", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Throws<NotSupportedException>(() => migration.DownOperations);
        var proof = model.FindEntityType(typeof(분산사진증빙Record))!;
        Assert.Equal("distributed_photo_evidence", proof.GetTableName());
        Assert.Equal(["EvidenceId"], proof.FindPrimaryKey()!.Properties.Select(x => x.Name).ToArray());
        Assert.True(proof.FindProperty("Revision")!.IsConcurrencyToken);
        Assert.Contains(proof.GetIndexes(), x => x.IsUnique && x.Properties.Select(p => p.Name).SequenceEqual(["ActorUserId", "UploadRequestId"]));
        var hold = model.FindEntityType(typeof(분산사진보존정지Record))!;
        Assert.Equal("distributed_photo_holds", hold.GetTableName());
        Assert.Equal(["CaseId"], hold.FindPrimaryKey()!.Properties.Select(x => x.Name).ToArray());
        Assert.Equal(100, hold.FindProperty("CaseId")!.GetMaxLength());
        Assert.NotNull(proof.FindProperty("PrivateDetailsJson")!.GetValueConverter());
        Assert.NotNull(hold.FindProperty("LegalBasisJson")!.GetValueConverter());
    }
    [Fact]
    public async Task SQLite이전분산완료영수증을남겨두고사진과합의를별도원장으로추가한다()
    {
        await using var c = new SqliteConnection("Data Source=:memory:"); await c.OpenAsync();
        await using var db = new SsalddelContext(new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(c).Options, new Encryption());
        var generator = db.GetService<IMigrationsSqlGenerator>(); var model = db.GetService<IDesignTimeModel>().Model;
        foreach (var command in generator.Generate(new AddDistributedFulfillment().UpOperations, model)) await db.Database.ExecuteSqlRawAsync(command.CommandText);
        foreach (var command in generator.Generate(new AddDistributedCompletionLineEvidence().UpOperations, model)) await db.Database.ExecuteSqlRawAsync(command.CommandText);
        var requestId = Guid.NewGuid(); db.Add(new 분산물류CommandReceipt { ActorUserId = "legacy-driver", RequestId = requestId, InputDigest = "legacy-digest", Kind = "Deliver", AggregateId = "legacy-wave", StatusCode = "Completed", Revision = 2, RecordedAtUtc = DateTime.UtcNow }); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        foreach (var command in generator.Generate(new AddDistributedStorageProof().UpOperations, model)) await db.Database.ExecuteSqlRawAsync(command.CommandText);
        Assert.Equal(requestId, (await db.Set<분산물류CommandReceipt>().SingleAsync()).RequestId); Assert.Empty(await db.Set<분산사진증빙Record>().ToArrayAsync());
        db.Add(new 분산사진증빙Record { EvidenceId = "fixture-proof", ActorUserId = "actor", UploadRequestId = Guid.NewGuid(), CompletionRequestId = Guid.NewGuid(), Kind = "Receive", BindingDigest = "fixture-binding", CreatedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear(); Assert.Equal("fixture-proof", (await db.Set<분산사진증빙Record>().SingleAsync()).EvidenceId);
        var actualCaseId = "support:" + new string('a', 64); Assert.Equal(72, actualCaseId.Length);
        db.Add(new 분산사진보존정지Record { CaseId = actualCaseId, SourceKind = "distributed-inbound", SourceId = "fixture-lot", Active = true });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear(); Assert.Equal(actualCaseId, (await db.Set<분산사진보존정지Record>().SingleAsync()).CaseId);
        Assert.Throws<NotSupportedException>(() => new AddDistributedStorageProof().DownOperations);
    }
    private static string? Table(MigrationOperation operation) => operation.GetType().GetProperty("Table")?.GetValue(operation) as string ?? (operation as TableOperation)?.Name;
    private sealed class Encryption : IPersonalDataEncryptionService { public string? Protect(string? value) => value; public string? Unprotect(string? value) => value; }
}
