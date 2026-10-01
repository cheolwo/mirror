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

public sealed class CommunityPostTranslationModelTests
{
    [Fact]
    public void Model_MapsOriginalLanguageAndTranslationCache()
    {
        using var context = CreateContext();
        var post = context.Model.FindEntityType(typeof(PlatformCommunityPost));
        var translation = context.Model.FindEntityType(typeof(PlatformCommunityPostTranslation));

        Assert.Equal(16, post?.FindProperty(nameof(PlatformCommunityPost.OriginalLanguageCode))?.GetMaxLength());
        Assert.Equal("platform_community_post_translations", translation?.GetTableName());
        Assert.Contains(translation!.GetIndexes(), index =>
            index.IsUnique
            && index.GetDatabaseName() == "UX_community_post_translation_content");
    }

    [Fact]
    public void MigrationAndSnapshot_ContainTranslationCache()
    {
        using var context = CreateContext();
        const string migrationId = "20260717180000_AddCommunityPostTranslations";
        Assert.Contains(migrationId, context.Database.GetMigrations());

        var migrationsAssembly = context.GetService<IMigrationsAssembly>();
        var migration = migrationsAssembly.CreateMigration(
            migrationsAssembly.Migrations[migrationId],
            context.Database.ProviderName!);
        var table = Assert.Single(migration.UpOperations.OfType<CreateTableOperation>());
        Assert.Equal("platform_community_post_translations", table.Name);
        var column = Assert.Single(migration.UpOperations.OfType<AddColumnOperation>());
        Assert.Equal(nameof(PlatformCommunityPost.OriginalLanguageCode), column.Name);
        Assert.Equal("platform_community_posts", column.Table);

        var snapshotType = typeof(CommunityPostTranslationService).Assembly
            .GetType("Hongdal.Migrations.HongdalContextModelSnapshot");
        var snapshot = Assert.IsAssignableFrom<ModelSnapshot>(
            Activator.CreateInstance(snapshotType!, nonPublic: true));
        Assert.Equal(
            "platform_community_post_translations",
            snapshot.Model.FindEntityType(typeof(PlatformCommunityPostTranslation).FullName!)?.GetTableName());
        Assert.Equal(
            16,
            snapshot.Model.FindEntityType(typeof(PlatformCommunityPost).FullName!)
                ?.FindProperty(nameof(PlatformCommunityPost.OriginalLanguageCode))
                ?.GetMaxLength());
    }

    private static HongdalContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<HongdalContext>()
            .UseMySql(
                "Server=localhost;Database=hongdal_translation_model_test;User=root;Password=test;",
                new MySqlServerVersion(new Version(8, 4, 0)),
                options => options.MigrationsAssembly(typeof(CommunityPostTranslationService).Assembly.GetName().Name))
            .Options;
        return new HongdalContext(options, new DummyPersonalDataEncryptionService());
    }

    private sealed class DummyPersonalDataEncryptionService : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
