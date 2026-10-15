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
using 살뜰.도메인.창고;

namespace Ssalddel.Tests.Infrastructure;

public sealed class DistributedCompletionLineEvidenceTests : IDisposable
{
    private readonly ServiceProvider _services = new ServiceCollection().AddEntityFrameworkSqlite().BuildServiceProvider();
    public void Dispose() => _services.Dispose();

    [Fact]
    public void 새migration은행근거열만추가하고기존영수증을역산하거나별도원장을만들지않는다()
    {
        var migration = new AddDistributedCompletionLineEvidence();
        var column = Assert.IsType<AddColumnOperation>(Assert.Single(migration.UpOperations));
        Assert.Equal("distributed_receipts", column.Table); Assert.Equal("CompletionLinesJson", column.Name);
        Assert.Equal("longtext", column.ColumnType); Assert.True(column.IsNullable); Assert.Null(column.DefaultValue);
        Assert.Null(column.DefaultValueSql); Assert.Throws<NotSupportedException>(() => migration.DownOperations);
    }

    [Fact]
    public async Task 과거묶음영수증은요청과총수량그대로보존하고행별근거는미확인으로남는다()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = Db(connection); var generator = db.GetService<IMigrationsSqlGenerator>();
        var model = db.GetService<IDesignTimeModel>().Model;
        foreach (var sql in generator.Generate(new AddDistributedFulfillment().UpOperations, model))
            await db.Database.ExecuteSqlRawAsync(sql.CommandText);
        var requestId = Guid.NewGuid(); var now = DateTime.UtcNow;
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO distributed_receipts (ActorUserId,RequestId,InputDigest,Kind,AggregateId,ReservationId,Quantity,Revision,StatusCode,RecordedAtUtc) VALUES ({"legacy-actor"},{requestId},{"legacy-digest"},{"PickBatch"},{"legacy-wave"},NULL,{3},{7},{"Picking"},{now})");
        foreach (var sql in generator.Generate(new AddDistributedCompletionLineEvidence().UpOperations, model))
            await db.Database.ExecuteSqlRawAsync(sql.CommandText);
        var saved = await db.Set<분산물류CommandReceipt>().SingleAsync();
        Assert.Equal(requestId, saved.RequestId); Assert.Equal("legacy-digest", saved.InputDigest);
        Assert.Equal(3, saved.Quantity); Assert.Equal(7, saved.Revision); Assert.Null(saved.ReservationId);
        Assert.Null(saved.CompletionLinesJson);
    }

    [Fact]
    public async Task 실제저장은행근거를보호하고저장후변경과구형행에대한추가복원을거부한다()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = Db(connection); await db.Database.EnsureCreatedAsync();
        const string details = "{\"schemaVersion\":1,\"lines\":[{\"actorUserId\":\"private-performer\",\"lotId\":\"private-lot\",\"quantity\":2}]}";
        db.Add(new 분산물류CommandReceipt { ActorUserId = "actor", RequestId = Guid.NewGuid(), CompletionLinesJson = details });
        db.Add(new 분산물류CommandReceipt { ActorUserId = "legacy", RequestId = Guid.NewGuid() });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        using var raw = connection.CreateCommand(); raw.CommandText = "SELECT CompletionLinesJson FROM distributed_receipts WHERE ActorUserId='actor'";
        var stored = Assert.IsType<string>(await raw.ExecuteScalarAsync());
        Assert.StartsWith("protected:", stored); Assert.DoesNotContain("private-performer", stored); Assert.DoesNotContain("private-lot", stored);
        var property = db.Model.FindEntityType(typeof(분산물류CommandReceipt))!.FindProperty(nameof(분산물류CommandReceipt.CompletionLinesJson))!;
        Assert.True(property.IsNullable); Assert.NotNull(property.GetValueConverter()); Assert.Equal(PropertySaveBehavior.Throw, property.GetAfterSaveBehavior());
        var receipt = await db.Set<분산물류CommandReceipt>().SingleAsync(x => x.ActorUserId == "actor");
        Assert.Equal(details, receipt.CompletionLinesJson); receipt.CompletionLinesJson = "{\"changed\":true}";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear();
        Assert.Equal(details, (await db.Set<분산물류CommandReceipt>().AsNoTracking().SingleAsync(x => x.ActorUserId == "actor")).CompletionLinesJson);
        var legacy = await db.Set<분산물류CommandReceipt>().SingleAsync(x => x.ActorUserId == "legacy"); legacy.CompletionLinesJson = details;
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear();
        Assert.Null((await db.Set<분산물류CommandReceipt>().SingleAsync(x => x.ActorUserId == "legacy")).CompletionLinesJson);
    }

    private SsalddelContext Db(SqliteConnection connection) => new(new DbContextOptionsBuilder<SsalddelContext>()
        .UseSqlite(connection).UseInternalServiceProvider(_services).Options, new Encryption());
    private sealed class Encryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value is null ? null : "protected:" + Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
        public string? Unprotect(string? value) => value is null ? null : Encoding.UTF8.GetString(Convert.FromBase64String(value["protected:".Length..]));
    }
}
