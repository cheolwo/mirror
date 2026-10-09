using System.Text.Json;
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

public sealed class DeliveryCostSharingMigrationTests
{
    [Fact]
    public async Task 실제SQLite에서_기존정액설정과주문동의를보존하고_정책전체를왕복저장한다()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new SsalddelContext(new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(connection).Options, new Encryption());
        await db.Database.ExecuteSqlRawAsync("CREATE TABLE shipper_requests (Id INTEGER PRIMARY KEY);");
        await db.Database.ExecuteSqlRawAsync("CREATE TABLE 음식주문 (id INTEGER PRIMARY KEY);");
        await Apply(db, new AddNeighborhoodFulfillment());
        await Apply(db, new AddAutomaticFulfillmentRouting());
        await db.Database.ExecuteSqlRawAsync("INSERT INTO restaurant_fulfillment_supports (RestaurantId,Revision,FixedContributionKrw,Enabled,ClientRequestId,ActorId,UpdatedAtUtc) VALUES (101,4,2000,1,'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa','restaurant-owner','2026-10-07 06:00:00');");
        await db.Database.ExecuteSqlRawAsync("INSERT INTO fulfillment_routing_consents (SourceKind,RequestId,OwnerId,ClientRequestId,InputDigest,Revision,MaxUserChargeKrw,AllowedProvidersJson,InformationFieldsJson,NoticeVersion,ReceiptsJson,SharingAgreed,AutomaticSubmissionAgreed,DirectPaymentAgreed,Revoked,FrozenRestaurantSupportKrw,RestaurantSupportRevision,UpdatedAtUtc) VALUES ('FoodOrder','legacy-order','orderer','bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb','original-digest',2,5000,'[]','[]','original-notice','[]',1,1,0,0,1500,3,'2026-10-07 06:00:00');");
        await Apply(db, new AddDeliveryCostSharingPolicies());
        var setting = await db.RestaurantFulfillmentSupports.SingleAsync();
        var consent = await db.AutoFulfillmentConsents.SingleAsync();
        Assert.Equal(2000, setting.FixedContributionKrw);
        Assert.Equal(4, setting.Revision);
        Assert.Null(setting.CostSharingPolicyJson);
        Assert.Null(consent.FrozenCostSharingPolicyJson);
        Assert.Equal(1500, consent.FrozenRestaurantSupportKrw);
        Assert.Equal(3, consent.RestaurantSupportRevision);
        Assert.Equal(2, consent.Revision);
        var policy = new DeliveryCostSharingPolicy { Mode = "PercentageShare", RestaurantSharePercent = 50, MaximumRestaurantChargeKrw = 3000 };
        setting.CostSharingPolicyJson = JsonSerializer.Serialize(policy);
        consent.FrozenCostSharingPolicyJson = JsonSerializer.Serialize(policy);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var stored = JsonSerializer.Deserialize<DeliveryCostSharingPolicy>((await db.AutoFulfillmentConsents.SingleAsync()).FrozenCostSharingPolicyJson!)!;
        Assert.Equal("PercentageShare", stored.Mode);
        Assert.Equal(50, stored.RestaurantSharePercent);
        Assert.Equal(3000, stored.MaximumRestaurantChargeKrw);
        Assert.Equal(1500, (await db.AutoFulfillmentConsents.SingleAsync()).FrozenRestaurantSupportKrw);
    }

    [Fact]
    public void MySQL생성SQL은_nullable두열만추가하고_기존값을덮어쓰지않는다()
    {
        using var db = MySqlContext();
        var migration = new AddDeliveryCostSharingPolicies();
        Assert.Equal(2, migration.UpOperations.Count);
        Assert.All(migration.UpOperations, operation => Assert.True(Assert.IsType<AddColumnOperation>(operation).IsNullable));
        var sql = string.Join("\n", db.GetService<IMigrationsSqlGenerator>().Generate(migration.UpOperations, db.GetService<IDesignTimeModel>().Model).Select(x => x.CommandText));
        Assert.Contains("CostSharingPolicyJson", sql);
        Assert.Contains("FrozenCostSharingPolicyJson", sql);
        Assert.DoesNotContain("UPDATE ", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DROP ", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Throws<NotSupportedException>(() => migration.DownOperations);
    }

    [Fact]
    public void 정책설정과동의의_EF모델과snapshot이일치한다()
    {
        using var db = MySqlContext();
        var snapshot = db.GetService<IMigrationsAssembly>().ModelSnapshot!;
        var oldModel = db.GetService<IModelRuntimeInitializer>().Initialize(snapshot.Model, designTime: true);
        var current = db.GetService<IDesignTimeModel>().Model;
        var differences = db.GetService<IMigrationsModelDiffer>().GetDifferences(oldModel.GetRelationalModel(), current.GetRelationalModel());
        Assert.DoesNotContain(differences, operation =>
        {
            var table = operation.GetType().GetProperty("Table")?.GetValue(operation) as string ?? (operation as TableOperation)?.Name;
            return table is "restaurant_fulfillment_supports" or "fulfillment_routing_consents";
        });
        Assert.True(db.Model.FindEntityType(typeof(RestaurantFulfillmentSupportRecord))!.FindProperty("CostSharingPolicyJson")!.IsNullable);
        Assert.True(db.Model.FindEntityType(typeof(AutoFulfillmentConsentRecord))!.FindProperty("FrozenCostSharingPolicyJson")!.IsNullable);
    }

    private static SsalddelContext MySqlContext() => new(new DbContextOptionsBuilder<SsalddelContext>()
        .UseMySql("Server=127.0.0.1;Database=fixture_not_opened;User ID=fixture;Password=fixture;", new MySqlServerVersion(new Version(8, 4, 0)),
            options => options.MigrationsAssembly("Ssalddel")).Options, new Encryption());

    private static async Task Apply(SsalddelContext db, Migration migration)
    {
        foreach (var command in db.GetService<IMigrationsSqlGenerator>().Generate(migration.UpOperations, db.GetService<IDesignTimeModel>().Model))
            await db.Database.ExecuteSqlRawAsync(command.CommandText);
    }

    private sealed class Encryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
