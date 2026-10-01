using Hongdal.Domain.Community;
using Hongdal.Services.Community;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using 홍달.Data;
using 홍달.Infrastructure.Security;

namespace Hongdal.Tests.Infrastructure.Persistence;

public sealed class CommunityKeywordNotificationModelTests
{
    [Fact]
    public void Model_MapsKeywordSubscriptionInboxScanAndDeliveryTables()
    {
        using var context = CreateContext();

        AssertTable<CommunityKeywordSubscription>(context, "community_keyword_subscriptions");
        AssertTable<PlatformCommunityPostKeywordScan>(context, "platform_community_post_keyword_scans");
        AssertTable<CommunityKeywordNotification>(context, "community_keyword_notifications");
        AssertTable<CommunityKeywordNotificationDelivery>(context, "community_keyword_notification_deliveries");

        var post = context.Model.FindEntityType(typeof(PlatformCommunityPost));
        Assert.Equal(450, post?.FindProperty(nameof(PlatformCommunityPost.AuthorUserId))?.GetMaxLength());

        var subscription = context.Model.FindEntityType(typeof(CommunityKeywordSubscription));
        Assert.Contains(subscription!.GetIndexes(), index =>
            index.IsUnique
            && index.Properties.Select(property => property.Name).SequenceEqual(
                [nameof(CommunityKeywordSubscription.UserId),
                 nameof(CommunityKeywordSubscription.AppKey),
                 nameof(CommunityKeywordSubscription.NormalizedKeyword)]));

        var notification = context.Model.FindEntityType(typeof(CommunityKeywordNotification));
        Assert.Contains(notification!.GetIndexes(), index =>
            index.IsUnique
            && index.Properties.Select(property => property.Name).SequenceEqual(
                [nameof(CommunityKeywordNotification.UserId), nameof(CommunityKeywordNotification.PostId)]));
    }

    [Fact]
    public void MigrationAndSnapshot_ContainTheKeywordNotificationFoundation()
    {
        using var context = CreateContext();
        const string migrationId = "20260715001000_AddCommunityKeywordNotifications";
        Assert.Contains(migrationId, context.Database.GetMigrations());

        var migrationsAssembly = context.GetService<IMigrationsAssembly>();
        var migration = migrationsAssembly.CreateMigration(
            migrationsAssembly.Migrations[migrationId],
            context.Database.ProviderName!);
        Assert.Equal(
            [
                "community_keyword_notification_deliveries",
                "community_keyword_notifications",
                "community_keyword_subscriptions",
                "platform_community_post_keyword_scans"
            ],
            migration.UpOperations
                .OfType<CreateTableOperation>()
                .Select(operation => operation.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray());
        var addedColumn = Assert.Single(migration.UpOperations.OfType<AddColumnOperation>());
        Assert.Equal("platform_community_posts", addedColumn.Table);
        Assert.Equal(nameof(PlatformCommunityPost.AuthorUserId), addedColumn.Name);

        var assembly = typeof(CommunityKeywordNotificationProcessor).Assembly;
        var snapshotType = assembly.GetType("Hongdal.Migrations.HongdalContextModelSnapshot");
        Assert.NotNull(snapshotType);
        var snapshot = Assert.IsAssignableFrom<ModelSnapshot>(
            Activator.CreateInstance(snapshotType!, nonPublic: true));

        Assert.Equal(
            "community_keyword_subscriptions",
            snapshot.Model.FindEntityType(typeof(CommunityKeywordSubscription).FullName!)?.GetTableName());
        Assert.Equal(
            "platform_community_post_keyword_scans",
            snapshot.Model.FindEntityType(typeof(PlatformCommunityPostKeywordScan).FullName!)?.GetTableName());
        Assert.Equal(
            "community_keyword_notifications",
            snapshot.Model.FindEntityType(typeof(CommunityKeywordNotification).FullName!)?.GetTableName());
        Assert.Equal(
            "community_keyword_notification_deliveries",
            snapshot.Model.FindEntityType(typeof(CommunityKeywordNotificationDelivery).FullName!)?.GetTableName());
        Assert.Equal(
            450,
            snapshot.Model.FindEntityType(typeof(PlatformCommunityPost).FullName!)
                ?.FindProperty(nameof(PlatformCommunityPost.AuthorUserId))
                ?.GetMaxLength());
    }

    [Fact]
    public void MigrationAndSnapshot_ContainOnlyThePostAuthorDisplayCountryColumns()
    {
        using var context = CreateContext();
        const string migrationId = "20260717134000_AddCommunityPostAuthorDisplayCountry";
        Assert.Contains(migrationId, context.Database.GetMigrations());

        var migrationsAssembly = context.GetService<IMigrationsAssembly>();
        var migration = migrationsAssembly.CreateMigration(
            migrationsAssembly.Migrations[migrationId],
            context.Database.ProviderName!);
        var addedColumns = migration.UpOperations
            .OfType<AddColumnOperation>()
            .OrderBy(operation => operation.Name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(3, addedColumns.Length);
        Assert.All(addedColumns, operation => Assert.Equal("platform_community_posts", operation.Table));
        Assert.Equal(
            [
                nameof(PlatformCommunityPost.AuthorDisplayCountryCode),
                nameof(PlatformCommunityPost.AuthorDisplayCountryName),
                nameof(PlatformCommunityPost.IsAuthorDisplayCountryPublic)
            ],
            addedColumns.Select(operation => operation.Name).ToArray());
        Assert.DoesNotContain(migration.UpOperations, operation => operation is CreateTableOperation);

        var post = context.Model.FindEntityType(typeof(PlatformCommunityPost));
        Assert.Equal(2, post?.FindProperty(nameof(PlatformCommunityPost.AuthorDisplayCountryCode))?.GetMaxLength());
        Assert.Equal(80, post?.FindProperty(nameof(PlatformCommunityPost.AuthorDisplayCountryName))?.GetMaxLength());

        var snapshotType = typeof(CommunityKeywordNotificationProcessor).Assembly
            .GetType("Hongdal.Migrations.HongdalContextModelSnapshot");
        var snapshot = Assert.IsAssignableFrom<ModelSnapshot>(
            Activator.CreateInstance(snapshotType!, nonPublic: true));
        var snapshotPost = snapshot.Model.FindEntityType(typeof(PlatformCommunityPost).FullName!);
        Assert.Equal(2, snapshotPost?.FindProperty(nameof(PlatformCommunityPost.AuthorDisplayCountryCode))?.GetMaxLength());
        Assert.Equal(80, snapshotPost?.FindProperty(nameof(PlatformCommunityPost.AuthorDisplayCountryName))?.GetMaxLength());
        Assert.NotNull(snapshotPost?.FindProperty(nameof(PlatformCommunityPost.IsAuthorDisplayCountryPublic)));
    }

    private static void AssertTable<TEntity>(HongdalContext context, string tableName)
        where TEntity : class
        => Assert.Equal(tableName, context.Model.FindEntityType(typeof(TEntity))?.GetTableName());

    private static HongdalContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<HongdalContext>()
            .UseMySql(
                "Server=localhost;Database=hongdal_keyword_model_test;User=root;Password=test;",
                new MySqlServerVersion(new Version(8, 4, 0)),
                options => options.MigrationsAssembly(typeof(CommunityKeywordNotificationProcessor).Assembly.GetName().Name))
            .Options;
        return new HongdalContext(options, new DummyPersonalDataEncryptionService());
    }

    private sealed class DummyPersonalDataEncryptionService : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
