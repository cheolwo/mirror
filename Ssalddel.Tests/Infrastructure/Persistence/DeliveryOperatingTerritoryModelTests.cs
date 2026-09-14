using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.도메인.배달권;

namespace Ssalddel.Tests.Infrastructure.Persistence;

public sealed class DeliveryOperatingTerritoryModelTests
{
    [Fact]
    public void 현행_행정동_유일성과_이력_FK_Restrict를_구성한다()
    {
        using var db = CreateContext();
        var membership = db.Model.FindEntityType(typeof(배달운영권역행정동Membership));

        Assert.NotNull(membership);
        Assert.Equal("delivery_operating_territory_admin_dongs", membership.GetTableName());
        var activeIndex = Assert.Single(
            membership.GetIndexes(),
            index => index.Properties.Select(x => x.Name)
                .SequenceEqual([nameof(배달운영권역행정동Membership.현행행정동유일성Key)]));
        Assert.True(activeIndex.IsUnique);
        var historyIndex = Assert.Single(
            membership.GetIndexes(),
            index => index.Properties.Select(x => x.Name).SequenceEqual(
                [
                    nameof(배달운영권역행정동Membership.배달운영권역Id),
                    nameof(배달운영권역행정동Membership.행정동고유식별자)
                ]));
        Assert.True(historyIndex.IsUnique);
        Assert.Equal(DeleteBehavior.Restrict, Assert.Single(membership.GetForeignKeys()).DeleteBehavior);
    }

    [Fact]
    public void 권역_revision과_멱등수신증_Outbox에_경쟁변경_구성을_둔다()
    {
        using var db = CreateContext();
        var territory = db.Model.FindEntityType(typeof(배달운영권역));
        var receipt = db.Model.FindEntityType(typeof(배달운영권역CommandReceipt));
        var outbox = db.Model.FindEntityType(typeof(배달운영권역변경Outbox));

        Assert.NotNull(territory);
        Assert.NotNull(receipt);
        Assert.NotNull(outbox);
        Assert.True(territory.FindProperty(nameof(배달운영권역.Revision))!.IsConcurrencyToken);
        Assert.True(Assert.Single(
            receipt.GetIndexes(),
            index => index.Properties.Single().Name == nameof(배달운영권역CommandReceipt.ClientRequestId)).IsUnique);
        Assert.Equal(
            "json",
            receipt.FindProperty(nameof(배달운영권역CommandReceipt.ResultJson))!
                .FindAnnotation(RelationalAnnotationNames.ColumnType)!.Value);
        Assert.Equal(
            160,
            receipt.FindProperty(nameof(배달운영권역CommandReceipt.ActorUserStableId))!.GetMaxLength());
        Assert.Equal(
            160,
            outbox.FindProperty(nameof(배달운영권역변경Outbox.ActorUserStableId))!.GetMaxLength());
        Assert.True(Assert.Single(
            outbox.GetIndexes(),
            index => index.Properties.Select(x => x.Name).SequenceEqual(
                [
                    nameof(배달운영권역변경Outbox.AggregateStableId),
                    nameof(배달운영권역변경Outbox.AggregateRevision)
                ])).IsUnique);
    }

    private static SsalddelContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<SsalddelContext>()
            .UseInMemoryDatabase($"delivery-territory-model-{Guid.NewGuid():N}")
            .Options;
        return new SsalddelContext(options, new DummyEncryption());
    }

    private sealed class DummyEncryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
