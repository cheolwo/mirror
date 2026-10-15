using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.Settlement;
using 살뜰.도메인.창고;
using C = Ssalddel.Contracts.Common.Settlement.NeighborhoodSettlementCodes;

namespace Ssalddel.Tests.Application.Settlement;

public sealed class NeighborhoodSettlementStorageQuantityResolverTests
{
    [Fact]
    public async Task 순차피킹후에도최초피킹전수량과현재수량은별도구간으로보존한다()
    {
        await using var f = await NeighborhoodSettlementSourceFixture.Create();
        var before = await f.Resolver.ResolveAsync(C.DistributedLot, f.LotId, default);
        var firstPick = f.Data.Clock.Now.AddHours(-12); var secondPick = f.Data.Clock.Now.AddHours(-6);
        await Reservation(f, picked: 2);
        await f.AddWork("Pick", "quantity-pick-1", 1, firstPick);
        await f.AddWork("Pick", "quantity-pick-2", 1, secondPick);

        var after = await f.Resolver.ResolveAsync(C.DistributedLot, f.LotId, default);
        Assert.Equal(new NeighborhoodSettlementStorageInterval(f.Data.Clock.Now.AddDays(-1), null, 1), Assert.Single(after.StorageIntervals));
        Assert.Equal(new[]
        {
            new NeighborhoodSettlementStorageQuantityInterval(f.Data.Clock.Now.AddDays(-1), firstPick, 3),
            new NeighborhoodSettlementStorageQuantityInterval(firstPick, secondPick, 2),
            new NeighborhoodSettlementStorageQuantityInterval(secondPick, null, 1)
        }, after.StorageQuantityIntervals);
        Assert.Equal(before.SourceVersion, after.SourceVersion); Assert.Equal(before.SourceDigest, after.SourceDigest);
        Assert.All(after.CompletedWork, x => Assert.Equal(C.Inbound, x.Kind));
        Assert.Equal(3, Assert.Single(after.CompletedWork).Quantity);
        Assert.DoesNotContain(after.CompletionEvidenceReferences, x => x.StartsWith("photo:quantity-pick-", StringComparison.Ordinal));
        Assert.Contains("photo:quantity-pick-1", after.StorageEvidenceReferences);
        Assert.Contains("photo:quantity-pick-2", after.StorageEvidenceReferences);
    }

    [Fact]
    public async Task 동일시각피킹은길이영인중간수량구간없이이력순서대로검증한다()
    {
        await using var f = await NeighborhoodSettlementSourceFixture.Create();
        var pickedAt = f.Data.Clock.Now.AddHours(-12);
        await Reservation(f, picked: 2);
        await f.AddWork("Pick", "quantity-same-time-1", 1, pickedAt);
        await f.AddWork("Pick", "quantity-same-time-2", 1, pickedAt);

        var source = await f.Resolver.ResolveAsync(C.DistributedLot, f.LotId, default);
        Assert.Equal(new[]
        {
            new NeighborhoodSettlementStorageQuantityInterval(f.Data.Clock.Now.AddDays(-1), pickedAt, 3),
            new NeighborhoodSettlementStorageQuantityInterval(pickedAt, null, 1)
        }, source.StorageQuantityIntervals);
        Assert.All(source.StorageQuantityIntervals, x => Assert.True(x.EndUtc is null || x.EndUtc > x.StartUtc));
        Assert.Equal(1, Assert.Single(source.StorageIntervals).MinimumQuantity);
    }

    [Fact]
    public async Task 전량피킹과실물반환사이공백에는수량보관구간을만들지않는다()
    {
        await using var f = await NeighborhoodSettlementSourceFixture.Create();
        var pickedAt = f.Data.Clock.Now.AddHours(-12); var returnedAt = f.Data.Clock.Now.AddHours(-6);
        await Reservation(f, picked: 3, returned: 1);
        await f.AddWork("Pick", "quantity-gap-pick", 3, pickedAt);
        await f.AddWork("Return", "quantity-gap-return", 1, returnedAt);

        var source = await f.Resolver.ResolveAsync(C.DistributedLot, f.LotId, default);
        Assert.Equal(new[]
        {
            new NeighborhoodSettlementStorageQuantityInterval(f.Data.Clock.Now.AddDays(-1), pickedAt, 3),
            new NeighborhoodSettlementStorageQuantityInterval(returnedAt, null, 1)
        }, source.StorageQuantityIntervals);
        Assert.Equal(new[]
        {
            new NeighborhoodSettlementStorageInterval(f.Data.Clock.Now.AddDays(-1), pickedAt, 3),
            new NeighborhoodSettlementStorageInterval(returnedAt, null, 1)
        }, source.StorageIntervals);
    }

    [Fact]
    public async Task 같은시각전량피킹뒤반환은새상수량으로이어지며영길이구간을노출하지않는다()
    {
        await using var f = await NeighborhoodSettlementSourceFixture.Create();
        var at = f.Data.Clock.Now.AddHours(-12);
        await Reservation(f, picked: 3, returned: 1);
        await f.AddWork("Pick", "quantity-touch-pick", 3, at);
        await f.AddWork("Return", "quantity-touch-return", 1, at);

        var source = await f.Resolver.ResolveAsync(C.DistributedLot, f.LotId, default);
        Assert.Equal(new[]
        {
            new NeighborhoodSettlementStorageQuantityInterval(f.Data.Clock.Now.AddDays(-1), at, 3),
            new NeighborhoodSettlementStorageQuantityInterval(at, null, 1)
        }, source.StorageQuantityIntervals);
    }

    [Theory]
    [InlineData("missing-receive-photo")]
    [InlineData("received-quantity-mismatch")]
    [InlineData("missing-pick-photo")]
    [InlineData("missing-return-photo")]
    [InlineData("future-pick")]
    [InlineData("negative-balance")]
    [InlineData("same-time-negative-balance")]
    public async Task 수량대조나사진근거및시간순재고가유효하지않으면두보관투영모두인증하지않는다(string damage)
    {
        await using var f = await NeighborhoodSettlementSourceFixture.Create();
        var quantity = damage is "negative-balance" or "same-time-negative-balance" ? 4 : 1;
        var returned = damage is "missing-return-photo" or "same-time-negative-balance" ? 1 : 0;
        var at = damage == "future-pick" ? f.Data.Clock.Now.AddMinutes(1) : f.Data.Clock.Now.AddHours(-12);
        await Reservation(f, quantity, returned);
        await f.AddWork("Pick", "quantity-invalid-pick", quantity, at);
        if (returned > 0)
            await f.AddWork("Return", "quantity-invalid-return", returned,
                damage == "same-time-negative-balance" ? at : f.Data.Clock.Now.AddHours(-6));

        if (damage == "received-quantity-mismatch")
        {
            var lot = await f.Data.Db.Set<분산재고LotRecord>().SingleAsync(x => x.LotId == f.LotId);
            lot.ReceivedQuantity++;
        }
        else if (damage.StartsWith("missing-", StringComparison.Ordinal))
        {
            var photoId = damage switch
            {
                "missing-receive-photo" => "source-photo",
                "missing-pick-photo" => "quantity-invalid-pick",
                _ => "quantity-invalid-return"
            };
            var photo = await f.Data.Db.Set<분산사진증빙Record>().SingleAsync(x => x.EvidenceId == photoId);
            photo.BoundAtUtc = null;
        }
        await f.Data.Db.SaveChangesAsync();

        var source = await f.Resolver.ResolveAsync(C.DistributedLot, f.LotId, default);
        Assert.Empty(source.StorageQuantityIntervals); Assert.Empty(source.StorageIntervals);
    }

    private static async Task Reservation(NeighborhoodSettlementSourceFixture f, int picked, int returned = 0)
    {
        f.Data.Db.Add(new 분산배송WaveRecord { WaveId = "storage-wave" });
        f.Data.Db.Add(new 분산배송주문Record { OrderId = "quantity-order", WaveId = "storage-wave" });
        f.Data.Db.Add(new 분산재고예약Record { ReservationId = "quantity-reservation", LotId = f.LotId,
            WaveId = "storage-wave", OrderId = "quantity-order", Quantity = picked,
            PickedQuantity = picked, ReturnedQuantity = returned });
        await f.Data.Db.SaveChangesAsync();
    }
}
