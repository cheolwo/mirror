using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Ssalddel.Contracts.Common.Fulfillment;
using Ssalddel.Domain.Community;
using Ssalddel.Migrations;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;

namespace Ssalddel.Tests.Infrastructure;

public sealed class AutoFulfillmentMigrationTests
{
    [Fact]
    public async Task r24데이터를_실제격리SQLite에서확장해_ID보존과원천별현재수행키를검증한다()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new SsalddelContext(new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(connection).Options, new Encryption());
        await db.Database.ExecuteSqlRawAsync("CREATE TABLE shipper_requests (Id INTEGER PRIMARY KEY);");
        await db.Database.ExecuteSqlRawAsync("CREATE TABLE 음식주문 (id INTEGER PRIMARY KEY);");
        await Apply(db, new AddNeighborhoodFulfillment());
        await db.Database.ExecuteSqlRawAsync("INSERT INTO neighborhood_fulfillment_quotes (QuoteId,ActorId,InputDigest,RequestId,ProviderId,CostKrw,WeightKg,DistanceKm,EstimatedCompletionUtc,ExpiresAtUtc,IsSimulation) VALUES ('old-quote','owner','digest','same-source','mock-local',3000,1,2,'2026-10-07 04:00:00','2026-10-07 04:30:00',1);");
        await db.Database.ExecuteSqlRawAsync("INSERT INTO neighborhood_fulfillment_attempts (AttemptId,RequestId,ClientRequestId,SelectionDigest,QuoteId,ProviderId,StateCode,CostKrw,IsSimulation,SharingAgreed,CreatedAtUtc,UpdatedAtUtc,EstimatedCompletionUtc,Revision,HistoryJson) VALUES ('old-attempt','same-source','aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa','digest','old-quote','mock-local','Accepted',3000,1,1,'2026-10-07 04:00:00','2026-10-07 04:00:00','2026-10-07 04:30:00',2,'[]');");
        await db.Database.ExecuteSqlRawAsync("INSERT INTO neighborhood_fulfillment (RequestId,ActorId,AttemptId,Revision,UpdatedAtUtc) VALUES ('same-source','owner','old-attempt',2,'2026-10-07 04:00:00');");
        await Apply(db, new AddAutomaticFulfillmentRouting());
        var current = await db.NeighborhoodFulfillments.AsNoTracking().SingleAsync();
        Assert.Equal(FulfillmentSourceKinds.NeighborhoodDelivery, current.SourceKind);
        Assert.Equal("old-attempt", current.AttemptId);
        Assert.Equal(2, current.Revision);
        Assert.False(current.IsAutomatic);
        Assert.Equal("old-quote", (await db.NeighborhoodFulfillmentQuotes.AsNoTracking().SingleAsync()).QuoteId);
        Assert.Null((await db.NeighborhoodFulfillmentAttempts.AsNoTracking().SingleAsync()).ConsentRevision);
        db.NeighborhoodFulfillments.Add(new NeighborhoodFulfillmentRecord { SourceKind = FulfillmentSourceKinds.FoodOrder,
            RequestId = current.RequestId, ActorId = "food-owner", AttemptId = "food-attempt", Revision = 1, IsAutomatic = true });
        await db.SaveChangesAsync();
        Assert.Equal(2, await db.NeighborhoodFulfillments.CountAsync());
        Assert.Equal("old-attempt", (await db.NeighborhoodFulfillments.AsNoTracking().SingleAsync(x => x.SourceKind == FulfillmentSourceKinds.NeighborhoodDelivery)).AttemptId);
    }

    [Fact]
    public void MySQL전환SQL은_기존세표를지우지않고_지원설정키를음식점Id로보존한다()
    {
        using var db = MySqlContext();
        var migration = new AddAutomaticFulfillmentRouting();
        var sql = string.Join("\n", db.GetService<IMigrationsSqlGenerator>().Generate(migration.UpOperations, db.GetService<IDesignTimeModel>().Model).Select(x => x.CommandText));
        Assert.DoesNotContain("DROP TABLE `neighborhood_fulfillment", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("DEFAULT 'NeighborhoodDelivery'", sql);
        Assert.DoesNotContain("AUTO_INCREMENT", sql);
        Assert.All(migration.UpOperations.OfType<CreateIndexOperation>(), index => Assert.InRange(index.Name.Length, 1, 64));
        Assert.Equal(ValueGenerated.Never, db.Model.FindEntityType(typeof(RestaurantFulfillmentSupportRecord))!.FindProperty(nameof(RestaurantFulfillmentSupportRecord.RestaurantId))!.ValueGenerated);
    }

    [Fact]
    public void r25테이블의_snapshot과현재EF모델이같아서_후속중복migration을만들지않는다()
    {
        using var db = MySqlContext();
        var snapshot = db.GetService<IMigrationsAssembly>().ModelSnapshot!;
        var oldModel = db.GetService<IModelRuntimeInitializer>().Initialize(snapshot.Model, designTime: true);
        var current = db.GetService<IDesignTimeModel>().Model;
        var differences = db.GetService<IMigrationsModelDiffer>().GetDifferences(oldModel.GetRelationalModel(), current.GetRelationalModel());
        var relevant = differences.Where(operation =>
        {
            var table = operation.GetType().GetProperty("Table")?.GetValue(operation) as string
                ?? (operation as TableOperation)?.Name;
            return table?.StartsWith("fulfillment_", StringComparison.Ordinal) == true
                || table?.StartsWith("neighborhood_fulfillment", StringComparison.Ordinal) == true
                || table == "restaurant_fulfillment_supports"
                || operation is ColumnOperation column && (column.Name == "simulation_fulfillment_routing" || column.Name == "자동처리처수행");
        }).ToArray();
        Assert.True(relevant.Length == 0, string.Join("\n", relevant.Select(x => x.GetType().Name + ":" + x.GetType().GetProperty("Name")?.GetValue(x))));
    }

    private static SsalddelContext MySqlContext() => new(new DbContextOptionsBuilder<SsalddelContext>()
        .UseMySql("Server=127.0.0.1;Database=fixture_not_opened;User ID=fixture;Password=fixture;", new MySqlServerVersion(new Version(8, 4, 0)),
            options => options.MigrationsAssembly("Ssalddel")).Options, new Encryption());
    private static async Task Apply(SsalddelContext db, Migration migration)
    {
        // 실제 연결은 :memory: SQLite뿐입니다. 위 MySQL SQL 생성은 연결을 열지 않습니다.
        foreach (var command in db.GetService<IMigrationsSqlGenerator>().Generate(migration.UpOperations, db.GetService<IDesignTimeModel>().Model))
            await db.Database.ExecuteSqlRawAsync(command.CommandText);
    }
    private sealed class Encryption : IPersonalDataEncryptionService
    { public string? Protect(string? value) => value; public string? Unprotect(string? value) => value; }
}
