using Hongdal.Domain.Content;
using Hongdal.Domain.Notifications;
using Hongdal.Services.Content;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using 홍달.Data;
using 홍달.Infrastructure.Security;

namespace Hongdal.Tests.Infrastructure.Persistence;

public sealed class HongikHakdangCardDeliveryModelTests
{
    [Fact]
    public void RuntimeModel_contains_card_delivery_and_mobile_installation_tables()
    {
        using var context = CreateContext();

        AssertEntityTable<HongikHakdangCardImageVariant>(context, "hongik_hakdang_card_image_variants");
        AssertEntityTable<HongikHakdangCardDeliveryPreference>(context, "hongik_hakdang_card_delivery_preferences");
        AssertEntityTable<HongikHakdangDailyCardSelection>(context, "hongik_hakdang_daily_card_selections");
        AssertEntityTable<HongikHakdangCardDeliveryOutbox>(context, "hongik_hakdang_card_delivery_outbox");
        AssertEntityTable<HongdalMobilePushInstallation>(context, "hongdal_mobile_push_installations");

        var variant = context.Model.FindEntityType(typeof(HongikHakdangCardImageVariant))!;
        Assert.Contains(variant.GetIndexes(), index =>
            index.IsUnique
            && index.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(HongikHakdangCardImageVariant.CardId), nameof(HongikHakdangCardImageVariant.VariantKind)]));

        var selection = context.Model.FindEntityType(typeof(HongikHakdangDailyCardSelection))!;
        Assert.Contains(selection.GetIndexes(), index =>
            index.IsUnique
            && index.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(HongikHakdangDailyCardSelection.SelectionDate), nameof(HongikHakdangDailyCardSelection.TimeZoneId)]));

        var installation = context.Model.FindEntityType(typeof(HongdalMobilePushInstallation))!;
        Assert.Equal(4096, installation.FindProperty(nameof(HongdalMobilePushInstallation.PushToken))!.GetMaxLength());

        var card = context.Model.FindEntityType(typeof(HongikHakdangCard))!;
        var collection = context.Model.FindEntityType(typeof(HongikHakdangCardCollection))!;
        Assert.NotNull(card.FindProperty(nameof(HongikHakdangCard.IsAdminEnabled)));
        Assert.NotNull(collection.FindProperty(nameof(HongikHakdangCardCollection.IsAdminEnabled)));
    }

    [Fact]
    public void MigrationSnapshot_contains_the_same_card_delivery_tables()
    {
        var assembly = typeof(HongikHakdangCardDeliveryService).Assembly;
        var snapshotType = assembly.GetType("Hongdal.Migrations.HongdalContextModelSnapshot");
        Assert.NotNull(snapshotType);
        var snapshot = Assert.IsAssignableFrom<ModelSnapshot>(
            Activator.CreateInstance(snapshotType!, nonPublic: true));

        Assert.Equal(
            "hongik_hakdang_card_image_variants",
            snapshot.Model.FindEntityType(typeof(HongikHakdangCardImageVariant).FullName!)?.GetTableName());
        Assert.Equal(
            "hongik_hakdang_card_delivery_preferences",
            snapshot.Model.FindEntityType(typeof(HongikHakdangCardDeliveryPreference).FullName!)?.GetTableName());
        Assert.Equal(
            "hongik_hakdang_daily_card_selections",
            snapshot.Model.FindEntityType(typeof(HongikHakdangDailyCardSelection).FullName!)?.GetTableName());
        Assert.Equal(
            "hongik_hakdang_card_delivery_outbox",
            snapshot.Model.FindEntityType(typeof(HongikHakdangCardDeliveryOutbox).FullName!)?.GetTableName());
        Assert.Equal(
            "hongdal_mobile_push_installations",
            snapshot.Model.FindEntityType(typeof(HongdalMobilePushInstallation).FullName!)?.GetTableName());
        Assert.NotNull(
            snapshot.Model.FindEntityType(typeof(HongikHakdangCard).FullName!)
                ?.FindProperty(nameof(HongikHakdangCard.IsAdminEnabled)));
        Assert.NotNull(
            snapshot.Model.FindEntityType(typeof(HongikHakdangCardCollection).FullName!)
                ?.FindProperty(nameof(HongikHakdangCardCollection.IsAdminEnabled)));
    }

    private static void AssertEntityTable<TEntity>(HongdalContext context, string tableName)
        where TEntity : class
        => Assert.Equal(tableName, context.Model.FindEntityType(typeof(TEntity))?.GetTableName());

    private static HongdalContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<HongdalContext>()
            .UseMySql(
                "Server=localhost;Database=hongdal_model_test;User=root;Password=test;",
                new MySqlServerVersion(new Version(8, 4, 0)))
            .Options;

        return new HongdalContext(options, new DummyPersonalDataEncryptionService());
    }

    private sealed class DummyPersonalDataEncryptionService : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;

        public string? Unprotect(string? value) => value;
    }
}
