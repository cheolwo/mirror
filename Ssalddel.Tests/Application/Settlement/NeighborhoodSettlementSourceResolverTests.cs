using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.Settlement;
using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Contracts.Food;
using Ssalddel.Tests.Application.Warehouse;
using 살뜰.도메인.기사;
using 살뜰.도메인.음식;
using 살뜰.도메인.운송;
using 살뜰.도메인.창고;
using 살뜰.도메인.화주;
using C = Ssalddel.Contracts.Common.Settlement.NeighborhoodSettlementCodes;

namespace Ssalddel.Tests.Application.Settlement;

public sealed class NeighborhoodSettlementSourceResolverTests
{
    [Fact]
    public async Task 실제입고처리자수량시각을사진결속영수증과대조하고금액은임의로생성하지않는다()
    {
        await using var f = await NeighborhoodSettlementSourceFixture.Create();
        var source = await f.Resolver.ResolveAsync(C.DistributedLot, f.LotId, default);
        var work = Assert.Single(source.CompletedWork);
        Assert.Equal(C.Inbound, work.Kind); Assert.Equal(3, work.Quantity); Assert.Equal(DistributedTestFixture.Operator.UserId, work.ActorUserId);
        Assert.Equal(f.Data.Clock.Now.AddDays(-1), source.StorageStartedAtUtc);
        Assert.Equal(3, Assert.Single(source.StorageIntervals).MinimumQuantity);
        Assert.Equal("goods-owner-a", work.GoodsOwnerUserId);
        Assert.Contains("photo:source-photo", work.EvidenceReferences);
        Assert.Null(source.HistoricalSaleTotalKrw); Assert.Null(source.FrozenDriverFareKrw);
        Assert.Equal([C.StoredGoods], source.AllowedActivityKinds);
        var lot = await f.Data.Db.Set<분산재고LotRecord>().SingleAsync();
        lot.ReceivedQuantity++; lot.Revision++; await f.Data.Db.SaveChangesAsync();
        Assert.Equal(source.SourceDigest, (await f.Resolver.ResolveAsync(C.DistributedLot, f.LotId, default)).SourceDigest);
        Assert.Empty((await f.Resolver.ResolveAsync(C.DistributedLot, f.LotId, default)).StorageIntervals);
        lot.OwnerUserId = "changed-owner"; await f.Data.Db.SaveChangesAsync();
        Assert.NotEqual(source.SourceDigest, (await f.Resolver.ResolveAsync(C.DistributedLot, f.LotId, default)).SourceDigest);
    }

    [Fact]
    public async Task 업로드사진만있거나다른Lot사진이면완료작업과보관시작을인증하지않는다()
    {
        await using var f = await NeighborhoodSettlementSourceFixture.Create();
        var photo = await f.Data.Db.Set<분산사진증빙Record>().SingleAsync();
        photo.BoundAtUtc = null; photo.StatusCode = "Uploaded"; await f.Data.Db.SaveChangesAsync();
        var source = await f.Resolver.ResolveAsync(C.DistributedLot, f.LotId, default);
        Assert.Empty(source.CompletedWork); Assert.Null(source.StorageStartedAtUtc);
        photo.BoundAtUtc = f.Data.Clock.Now; photo.StatusCode = "Bound";
        photo.PrivateDetailsJson = NeighborhoodSettlementSourceFixture.PhotoJson(photo.CompletionRequestId, "another-lot");
        await f.Data.Db.SaveChangesAsync();
        Assert.Empty((await f.Resolver.ResolveAsync(C.DistributedLot, f.LotId, default)).CompletedWork);
    }

    [Fact]
    public async Task 의뢰와연결되지않은운임과모의지급은확정기사운임및실제입금으로승격하지않는다()
    {
        await using var f = await NeighborhoodSettlementSourceFixture.Create();
        f.Data.Db.Add(new 화주운송의뢰 { 의뢰Id = "cargo-source", 화주Id = "seller-a", 주문자UserId = "buyer-a" });
        var transport = new 운송원장 { 의뢰Id = "cargo-source", 운송번호 = "transport-source", 확정기사Id = "driver-a",
            상태 = "인수완료", 도착 = f.Data.Clock.Now, 기사지급예정액 = 5000 };
        f.Data.Db.Add(transport); f.Data.Db.Add(new 운임구성 { 의뢰Id = "cargo-source", 기사지급예정운임 = 4999 });
        await f.Data.Db.SaveChangesAsync();
        f.Data.Db.Add(new 기사운송대금지급요청 { 운송Id = transport.Id, 의뢰Id = "cargo-source", 기사Id = "driver-a",
            상태코드 = 기사지급요청상태코드.Simulation검증완료, 승인일시Utc = f.Data.Clock.Now });
        await f.Data.Db.SaveChangesAsync();
        var source = await f.Resolver.ResolveAsync(C.CargoRequest, "cargo-source", default);
        Assert.Null(source.FrozenDriverFareKrw); Assert.Null(source.HistoricalSaleTotalKrw);
        Assert.Equal("cargo:cargo-source", source.CanonicalDeliveryOrigin);
        Assert.Equal("SimulationVerified", source.OriginalExternalPaymentStatus);
        Assert.Equal(C.Delivery, Assert.Single(source.CompletedWork).Kind);
        var fare = await f.Data.Db.Set<운임구성>().SingleAsync();
        var request = await f.Data.Db.Set<화주운송의뢰>().SingleAsync(); request.운임구성Id = fare.Id; await f.Data.Db.SaveChangesAsync();
        Assert.Equal(4999, (await f.Resolver.ResolveAsync(C.CargoRequest, "cargo-source", default)).FrozenDriverFareKrw);
        Assert.Single(await f.Data.Db.Set<기사운송대금지급요청>().ToArrayAsync());
    }

    [Fact]
    public async Task 음식점역할이나다른음식점클레임만으로소유자가되지않고완료근거없이는작업을인증하지않는다()
    {
        await using var f = await NeighborhoodSettlementSourceFixture.Create();
        f.Data.Db.Users.AddRange(new 살뜰.Data.ApplicationUser { Id = "restaurant-user" }, new 살뜰.Data.ApplicationUser { Id = "other-user" });
        f.Data.Db.Roles.Add(new Microsoft.AspNetCore.Identity.IdentityRole { Id = "restaurant-role", Name = "음식점", NormalizedName = "음식점" });
        f.Data.Db.UserRoles.AddRange(new() { UserId = "restaurant-user", RoleId = "restaurant-role" }, new() { UserId = "other-user", RoleId = "restaurant-role" });
        f.Data.Db.UserClaims.AddRange(new() { UserId = "restaurant-user", ClaimType = 음식점접근ClaimTypes.음식점Id, ClaimValue = "7" },
            new() { UserId = "other-user", ClaimType = 음식점접근ClaimTypes.음식점Id, ClaimValue = "8" });
        f.Data.Db.Add(new 음식주문 { 주문번호 = "food-source", 음식점Id = 7, 주문자UserId = "buyer-a", 총주문금액 = 15000 });
        await f.Data.Db.SaveChangesAsync();
        var source = await f.Resolver.ResolveAsync(C.FoodOrder, "food-source", default);
        Assert.Equal(15000, source.HistoricalSaleTotalKrw); Assert.Equal([C.PreparedFood], source.AllowedActivityKinds);
        Assert.Contains(source.Participants, x => x.UserId == "restaurant-user" && x.Roles.Contains("Seller"));
        Assert.DoesNotContain(source.Participants, x => x.UserId == "other-user" || x.Roles.Contains("Owner"));
        Assert.Empty(source.CompletedWork); Assert.Null(source.FrozenDriverFareKrw);
    }

    [Fact]
    public async Task 전량피킹뒤보관기간은종료되고실물반환전공백을보관으로계산하지않는다()
    {
        await using var f = await NeighborhoodSettlementSourceFixture.Create();
        var pickedAt = f.Data.Clock.Now.AddHours(-12); var returnedAt = f.Data.Clock.Now.AddHours(-6);
        f.Data.Db.Add(new 분산배송WaveRecord { WaveId = "storage-wave" });
        f.Data.Db.Add(new 분산배송주문Record { OrderId = "storage-order", WaveId = "storage-wave" });
        f.Data.Db.Add(new 분산재고예약Record { ReservationId = "storage-reservation", LotId = f.LotId,
            WaveId = "storage-wave", OrderId = "storage-order", Quantity = 3, PickedQuantity = 3, ReturnedQuantity = 1 });
        await f.AddWork("Pick", "storage-pick", 3, pickedAt);
        await f.AddWork("Return", "storage-return", 1, returnedAt);
        var source = await f.Resolver.ResolveAsync(C.DistributedLot, f.LotId, default);
        Assert.Equal(2, source.StorageIntervals.Count);
        Assert.Equal(new NeighborhoodSettlementStorageInterval(f.Data.Clock.Now.AddDays(-1), pickedAt, 3), source.StorageIntervals[0]);
        Assert.Equal(new NeighborhoodSettlementStorageInterval(returnedAt, null, 1), source.StorageIntervals[1]);
        Assert.Equal(1, Assert.Single(source.CompletedWork.Where(x => x.Kind == "ReturnToStorage")).Quantity);
        var photo = await f.Data.Db.Set<분산사진증빙Record>().SingleAsync(x => x.EvidenceId == "storage-return");
        photo.BoundAtUtc = null; await f.Data.Db.SaveChangesAsync();
        Assert.Empty((await f.Resolver.ResolveAsync(C.DistributedLot, f.LotId, default)).StorageIntervals);
    }

    [Fact]
    public async Task 주문피킹은Lot와같은작업ID를쓰고판매수량과기사운임은해당주문원본만사용한다()
    {
        await using var f = await NeighborhoodSettlementSourceFixture.Create();
        var at = f.Data.Clock.Now.AddHours(-12);
        f.Data.Db.Add(new 분산배송WaveRecord { WaveId = "source-wave", DriverId = DistributedTestFixture.Operator.UserId,
            OfferedDriverFareKrw = 99999, StatusCode = "Completed" });
        f.Data.Db.Add(new 분산배송주문Record { OrderId = "source-order", WaveId = "source-wave", CargoRequestId = "order-cargo",
            CustomerUserId = "buyer-a", SellerUserId = "seller-a", CustomerAcknowledged = true, StatusCode = "Delivered" });
        f.Data.Db.Add(new 분산재고예약Record { ReservationId = "source-reservation", LotId = f.LotId,
            WaveId = "source-wave", OrderId = "source-order", Quantity = 2, PickedQuantity = 2, DeliveredQuantity = 2 });
        var fare = new 운임구성 { 의뢰Id = "order-cargo", 기사지급예정운임 = 4100 }; f.Data.Db.Add(fare);
        await f.Data.Db.SaveChangesAsync();
        f.Data.Db.Add(new 화주운송의뢰 { 의뢰Id = "order-cargo", 화주Id = "seller-a", 주문자UserId = "buyer-a", 운임구성Id = fare.Id });
        f.Data.Db.Add(new 운송원장 { 의뢰Id = "order-cargo", 운송번호 = "order-transport", 확정기사Id = DistributedTestFixture.Operator.UserId,
            상태 = "인수완료", 도착 = f.Data.Clock.Now });
        await f.AddWork("Pick", "order-pick", 2, at, "source-wave", "source-reservation", "source-order");
        var lot = await f.Resolver.ResolveAsync(C.DistributedLot, f.LotId, default);
        var order = await f.Resolver.ResolveAsync(C.DistributedOrder, "source-order", default);
        Assert.Equal(Assert.Single(lot.CompletedWork.Where(x => x.Kind == C.Picking)).OriginId,
            Assert.Single(order.CompletedWork.Where(x => x.Kind == C.Picking)).OriginId);
        var sale = Assert.Single(order.CompletedWork.Where(x => x.Kind == C.ProductSale));
        Assert.Equal(2, sale.Quantity); Assert.Equal("goods-owner-a", sale.GoodsOwnerUserId); Assert.Null(sale.OccurredAtUtc);
        Assert.Null(order.HistoricalSaleTotalKrw); Assert.Equal(4100, order.FrozenDriverFareKrw);
        Assert.Equal(order.CanonicalDeliveryOrigin, (await f.Resolver.ResolveAsync(C.CargoRequest, "order-cargo", default)).CanonicalDeliveryOrigin);
        var receipt = await f.Data.Db.Set<분산물류CommandReceipt>().SingleAsync(x => x.Kind == "PickBatch");
        receipt.ReservationId = null; await f.Data.Db.SaveChangesAsync();
        Assert.DoesNotContain((await f.Resolver.ResolveAsync(C.DistributedOrder, "source-order", default)).CompletedWork, x => x.Kind == C.Picking);
    }

    [Theory]
    [InlineData("unknown", "source", "NeighborhoodSettlementSourceInvalid")]
    [InlineData("DistributedLot", "missing", "NeighborhoodSettlementSourceNotFound")]
    public async Task 미지원및없는원본은대체데이터없이실패한다(string kind, string id, string code)
    {
        await using var f = await NeighborhoodSettlementSourceFixture.Create();
        Assert.Equal(code, (await Assert.ThrowsAsync<NeighborhoodSettlementException>(() => f.Resolver.ResolveAsync(kind, id, default))).Code);
    }
}

// 금전·주소·사진은 격리 SQLite의 명시적 시험 자료이며 실제 촬영이나 입금 증거가 아닙니다.
internal sealed class NeighborhoodSettlementSourceFixture(DistributedTestFixture data) : IAsyncDisposable
{
    public DistributedTestFixture Data { get; } = data;
    public string LotId => "source-lot";
    public NeighborhoodSettlementSourceResolver Resolver => new(Data.Db, Data.Clock);
    public static async Task<NeighborhoodSettlementSourceFixture> Create()
    {
        var data = await DistributedTestFixture.Create();
        var site = (await data.Sites())[0]; var item = await data.Db.입고상품.SingleAsync();
        data.Db.Add(new 분산입고계획Record { PlanId = "source-plan", CreatorUserId = "seller-a", StatusCode = "PartiallyReceived" });
        data.Db.Add(new 분산재고LotRecord { LotId = "source-lot", PlanId = "source-plan", LineKey = "source-line", SiteId = site.SiteId,
            LocationId = site.Locations[0].LocationId, OwnerUserId = "goods-owner-a", SellerUserId = "seller-a", InboundItemId = item.Id,
            SourceInboundId = 101, TargetInboundId = 101, ExpectedQuantity = 5, ReceivedQuantity = 3 });
        var requestId = Guid.NewGuid(); var at = data.Clock.Now.AddDays(-1);
        data.Db.Add(new 재고이력 { 입고상품Id = item.Id, 원인유형 = "분산물류실물영수증", 이력유형 = "분할입고", 변경수량 = 3,
            처리UserId = DistributedTestFixture.Operator.UserId, 처리일시 = at, 메모 = "photos:source-photo" });
        data.Db.Add(new 분산물류CommandReceipt { ActorUserId = DistributedTestFixture.Operator.UserId, RequestId = requestId,
            Kind = "ReceiveBatch", AggregateId = "source-plan", Quantity = 3, RecordedAtUtc = at });
        data.Db.Add(new 분산사진증빙Record { EvidenceId = "source-photo", ActorUserId = DistributedTestFixture.Operator.UserId,
            UploadRequestId = Guid.NewGuid(), CompletionRequestId = requestId, Kind = "Receive", AggregateId = "source-plan", StatusCode = "Bound",
            CreatedAtUtc = at, BoundAtUtc = at, PrivateDetailsJson = PhotoJson(requestId, "source-lot") });
        await data.Db.SaveChangesAsync(); data.Db.ChangeTracker.Clear(); return new(data);
    }
    public static string PhotoJson(Guid requestId, string lotId) => JsonSerializer.Serialize(new
    {
        Binding = new DistributedEvidenceBinding { ActorUserId = DistributedTestFixture.Operator.UserId, RequestId = requestId,
            Kind = "Receive", AggregateId = "source-plan", LotIds = [lotId] }, Container = "private-fixture", ObjectName = "fixture.jpg"
    }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    public async Task AddWork(string kind, string photoId, int quantity, DateTime at, string aggregateId = "storage-wave",
        string? reservationId = null, string? orderId = null)
    {
        var lot = await Data.Db.Set<분산재고LotRecord>().SingleAsync(x => x.LotId == LotId); var requestId = Guid.NewGuid();
        Data.Db.Add(new 재고이력 { 입고상품Id = lot.InboundItemId, 원인유형 = "분산물류실물영수증",
            이력유형 = kind == "Return" ? "실물반환" : "기사직접피킹", 변경수량 = kind == "Return" ? quantity : -quantity,
            처리UserId = DistributedTestFixture.Operator.UserId, 처리일시 = at, 메모 = "photos:" + photoId });
        Data.Db.Add(new 분산물류CommandReceipt { ActorUserId = DistributedTestFixture.Operator.UserId, RequestId = requestId,
            Kind = kind + "Batch", AggregateId = aggregateId, ReservationId = reservationId, Quantity = quantity, RecordedAtUtc = at });
        Data.Db.Add(new 분산사진증빙Record { EvidenceId = photoId, ActorUserId = DistributedTestFixture.Operator.UserId,
            UploadRequestId = Guid.NewGuid(), CompletionRequestId = requestId, Kind = kind, AggregateId = aggregateId, StatusCode = "Bound",
            CreatedAtUtc = at, BoundAtUtc = at, PrivateDetailsJson = JsonSerializer.Serialize(new {
                Binding = new DistributedEvidenceBinding { ActorUserId = DistributedTestFixture.Operator.UserId, RequestId = requestId,
                    Kind = kind, AggregateId = aggregateId, LotIds = [LotId], OrderIds = orderId is null ? [] : [orderId] },
                Container = "private-fixture", ObjectName = "fixture.jpg" }, new JsonSerializerOptions(JsonSerializerDefaults.Web)) });
        await Data.Db.SaveChangesAsync();
    }
    public ValueTask DisposeAsync() => Data.DisposeAsync();
}
