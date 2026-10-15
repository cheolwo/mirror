using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.Settlement;
using Ssalddel.Contracts.Common.Settlement;
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
        Assert.Equal(["photo:source-photo"], source.StorageEvidenceReferences);
        Assert.Null(source.HistoricalSaleTotalKrw); Assert.Null(source.FrozenDriverFareKrw);
        Assert.Equal([C.StoredGoods], source.AllowedActivityKinds);
        Assert.Equal(NeighborhoodSettlementWorkflowCodes.InboundStorage, source.Workflow!.Kind);
        Assert.Equal("PartiallyReceived", source.Workflow.StateCode);
        Assert.False(source.Workflow.CompletionConfirmed);
        Assert.True(source.Workflow.PaymentReleaseReady);
        Assert.Equal([f.LotId], source.Workflow.InventoryLotIds);
        var lot = await f.Data.Db.Set<분산재고LotRecord>().SingleAsync();
        lot.ReceivedQuantity++; lot.Revision++; await f.Data.Db.SaveChangesAsync();
        Assert.Equal(source.SourceDigest, (await f.Resolver.ResolveAsync(C.DistributedLot, f.LotId, default)).SourceDigest);
        Assert.Empty((await f.Resolver.ResolveAsync(C.DistributedLot, f.LotId, default)).StorageIntervals);
        lot.OwnerUserId = "changed-owner"; await f.Data.Db.SaveChangesAsync();
        Assert.NotEqual(source.SourceDigest, (await f.Resolver.ResolveAsync(C.DistributedLot, f.LotId, default)).SourceDigest);
    }

    [Fact]
    public async Task 입고는전량근거가맞아야완료이며출고기사가추가돼도입고당사자판본과작업은독립이다()
    {
        await using var f = await NeighborhoodSettlementSourceFixture.Create();
        var lot = await f.Data.Db.Set<분산재고LotRecord>().SingleAsync(x => x.LotId == f.LotId);
        lot.ExpectedQuantity = lot.ReceivedQuantity; await f.Data.Db.SaveChangesAsync();
        var inbound = await f.Resolver.ResolveAsync(C.DistributedLot, f.LotId, default);
        Assert.Equal("Received", inbound.Workflow!.StateCode); Assert.True(inbound.Workflow.CompletionConfirmed);
        Assert.True(inbound.Workflow.PaymentReleaseReady);

        await f.AddExactBatch();
        var afterOutbound = await f.Resolver.ResolveAsync(C.DistributedLot, f.LotId, default);
        Assert.Equal(inbound.SourceDigest, afterOutbound.SourceDigest);
        Assert.Equal(inbound.SourceVersion, afterOutbound.SourceVersion);
        Assert.All(afterOutbound.CompletedWork, work => Assert.Equal(C.Inbound, work.Kind));
        Assert.DoesNotContain(afterOutbound.Participants, party => party.Roles.Contains("Picker") || party.Roles.Contains("Driver"));
        Assert.DoesNotContain(afterOutbound.CompletionEvidenceReferences, evidence => evidence == "photo:batch-pick-photo");
        Assert.Contains("photo:batch-pick-photo", afterOutbound.StorageEvidenceReferences);
        Assert.Equal(f.Data.Clock.Now.AddHours(-6), Assert.Single(afterOutbound.StorageIntervals).EndUtc);

        var photo = await f.Data.Db.Set<분산사진증빙Record>().SingleAsync(x => x.EvidenceId == "source-photo");
        photo.BoundAtUtc = null; await f.Data.Db.SaveChangesAsync();
        var withoutProof = await f.Resolver.ResolveAsync(C.DistributedLot, f.LotId, default);
        Assert.Equal("Received", withoutProof.Workflow!.StateCode);
        Assert.False(withoutProof.Workflow.CompletionConfirmed); Assert.False(withoutProof.Workflow.PaymentReleaseReady);
        Assert.Equal("NeighborhoodSettlementInboundWorkEvidenceRequired", withoutProof.Workflow.WaitingReasonCode);
    }

    [Fact]
    public async Task 업로드사진만있거나다른Lot사진이면완료작업과보관시작을인증하지않는다()
    {
        await using var f = await NeighborhoodSettlementSourceFixture.Create();
        var photo = await f.Data.Db.Set<분산사진증빙Record>().SingleAsync();
        photo.BoundAtUtc = null; photo.StatusCode = "Uploaded"; await f.Data.Db.SaveChangesAsync();
        var source = await f.Resolver.ResolveAsync(C.DistributedLot, f.LotId, default);
        Assert.Empty(source.CompletedWork); Assert.Null(source.StorageStartedAtUtc);
        Assert.False(source.Workflow!.PaymentReleaseReady); Assert.False(source.Workflow.CompletionConfirmed);
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
        Assert.All(source.CompletedWork, work => Assert.Equal(C.Inbound, work.Kind));
        Assert.DoesNotContain(source.CompletionEvidenceReferences, evidence => evidence is "photo:storage-pick" or "photo:storage-return");
        Assert.Contains("photo:source-photo", source.StorageEvidenceReferences);
        Assert.Contains("photo:storage-pick", source.StorageEvidenceReferences);
        Assert.Contains("photo:storage-return", source.StorageEvidenceReferences);
        Assert.Equal(source.StorageEvidenceReferences.Count, source.StorageEvidenceReferences.Distinct(StringComparer.Ordinal).Count());
        var photo = await f.Data.Db.Set<분산사진증빙Record>().SingleAsync(x => x.EvidenceId == "storage-return");
        photo.BoundAtUtc = null; await f.Data.Db.SaveChangesAsync();
        Assert.Empty((await f.Resolver.ResolveAsync(C.DistributedLot, f.LotId, default)).StorageIntervals);
    }

    [Fact]
    public async Task 주문피킹은실제재고이력ID를쓰고입고정산과분리하며판매수량과기사운임은해당주문원본만사용한다()
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
        Assert.DoesNotContain(lot.CompletedWork, x => x.Kind == C.Picking);
        var pickHistoryId = (await f.Data.Db.재고이력.SingleAsync(x => x.메모 == "photos:order-pick")).Id;
        Assert.Equal(pickHistoryId.ToString(System.Globalization.CultureInfo.InvariantCulture),
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
    [InlineData("duplicate-history")]
    [InlineData("wrong-actor")]
    [InlineData("wrong-total")]
    [InlineData("unknown-schema")]
    public async Task 신규묶음행의중복이력수행자수량판본이손상되면과거규칙으로복원하지않는다(string damage)
    {
        await using var f = await NeighborhoodSettlementSourceFixture.Create();
        await f.AddExactBatch(damage);
        Assert.Equal("NeighborhoodSettlementSourceEvidenceInvalid", (await Assert.ThrowsAsync<NeighborhoodSettlementException>(
            () => f.Resolver.ResolveAsync(C.DistributedOrder, "batch-order-a", default))).Code);
    }

    [Fact]
    public async Task 같은Lot묶음피킹은각주문의실제이력과수량에만귀속하고입고정산에는중복노출하지않는다()
    {
        await using var f = await NeighborhoodSettlementSourceFixture.Create();
        var receipt = await f.AddExactBatch();
        Assert.Null(receipt.ReservationId);
        var lot = await f.Resolver.ResolveAsync(C.DistributedLot, f.LotId, default);
        foreach (var suffix in new[] { "a", "b" })
        {
            var source = await f.Resolver.ResolveAsync(C.DistributedOrder, "batch-order-" + suffix, default);
            var pick = Assert.Single(source.CompletedWork.Where(x => x.Kind == C.Picking));
            Assert.Equal(suffix == "a" ? 1 : 2, pick.Quantity); Assert.Equal(7, pick.OriginRevision);
            Assert.Equal("batch-order-" + suffix, pick.RelatedOrderId);
            Assert.Equal(DistributedTestFixture.Operator.UserId, pick.ActorUserId);
            var actual = await f.Data.Db.재고이력.SingleAsync(x => x.Id == long.Parse(pick.OriginId, System.Globalization.CultureInfo.InvariantCulture));
            Assert.Equal(-pick.Quantity, actual.변경수량); Assert.Equal(pick.ActorUserId, actual.처리UserId);
            Assert.Equal(NeighborhoodSettlementWorkflowCodes.OutboundDelivery, source.Workflow!.Kind);
            Assert.Equal([f.LotId], source.Workflow.InventoryLotIds);
        }
        Assert.DoesNotContain(lot.CompletedWork, x => x.Kind == C.Picking);
        Assert.Equal(3, Assert.Single(lot.CompletedWork).Quantity);
    }

    [Fact]
    public async Task 같은Lot의다른입고자와다른주문피킹자는출고약정의권한과동의판본을바꾸지않는다()
    {
        await using var f = await NeighborhoodSettlementSourceFixture.Create();
        await f.AddExactBatch();
        var source = await f.Resolver.ResolveAsync(C.DistributedOrder, "batch-order-a", default);
        Assert.DoesNotContain(source.Participants, party => party.Roles.Contains("Receiver"));

        await f.AddWork("Receive", "unrelated-inbound", 1, f.Data.Clock.Now.AddHours(-5), "source-plan", actorUserId: "unrelated-receiver");
        await f.AddWork("Pick", "unrelated-pick", 1, f.Data.Clock.Now.AddHours(-4), "unrelated-wave",
            "unrelated-reservation", "unrelated-order", "unrelated-picker");
        var afterOtherWork = await f.Resolver.ResolveAsync(C.DistributedOrder, "batch-order-a", default);
        Assert.Equal(source.SourceDigest, afterOtherWork.SourceDigest);
        Assert.Equal(source.SourceVersion, afterOtherWork.SourceVersion);
        Assert.DoesNotContain(afterOtherWork.Participants, party => party.UserId is "unrelated-receiver" or "unrelated-picker"
            || party.Roles.Contains("Receiver"));
        var picker = Assert.Single(afterOtherWork.Participants.Where(party => party.Roles.Contains("Picker")));
        Assert.Equal(DistributedTestFixture.Operator.UserId, picker.UserId);
        Assert.Empty(afterOtherWork.StorageEvidenceReferences);
        Assert.All(afterOtherWork.CompletedWork.Where(work => work.Kind == C.Picking), work => Assert.Equal("batch-order-a", work.RelatedOrderId));
    }

    [Fact]
    public async Task 신규묶음사진이그주문을포함하지않으면해당행을인증하지않는다()
    {
        await using var f = await NeighborhoodSettlementSourceFixture.Create();
        await f.AddExactBatch("wrong-photo-order");
        var source = await f.Resolver.ResolveAsync(C.DistributedOrder, "batch-order-a", default);
        Assert.DoesNotContain(source.CompletedWork, x => x.Kind == C.Picking);
    }

    [Fact]
    public async Task 전달행전수량과운송완료및고객수령을별도로대조하고기존배송원점을유지한다()
    {
        await using var f = await NeighborhoodSettlementSourceFixture.Create();
        await f.AddExactBatch(); await f.AddExactDelivery();
        var source = await f.Resolver.ResolveAsync(C.DistributedOrder, "batch-order-b", default);
        var delivery = Assert.Single(source.CompletedWork.Where(x => x.Kind == C.Delivery));
        Assert.Equal("CargoTransport", delivery.OriginKind); Assert.Equal("batch-cargo-b", delivery.OriginId);
        Assert.Equal(1, delivery.Quantity); Assert.Equal(8, delivery.OriginRevision); Assert.Equal("batch-order-b", delivery.RelatedOrderId);
        Assert.Null(source.RewardOutcome);
        Assert.Equal(NeighborhoodSettlementWorkflowCodes.OutboundDelivery, source.Workflow!.Kind);
        Assert.Equal("DeliveredAwaitingReceipt", source.Workflow.StateCode);
        Assert.False(source.Workflow.CompletionConfirmed); Assert.False(source.Workflow.PaymentReleaseReady);
        Assert.Equal("NeighborhoodSettlementCustomerReceiptRequired", source.Workflow.WaitingReasonCode);
        var agreementDigest = source.SourceDigest;
        var order = await f.Data.Db.Set<분산배송주문Record>().SingleAsync(x => x.OrderId == "batch-order-b");
        order.CustomerAcknowledged = true; await f.Data.Db.SaveChangesAsync();
        source = await f.Resolver.ResolveAsync(C.DistributedOrder, "batch-order-b", default);
        Assert.Equal(2, source.RewardOutcome!.ConfirmedQuantity);
        Assert.Contains("distributed-order-received:batch-order-b", source.RewardOutcome.EvidenceReferences);
        Assert.Equal("cargo:batch-cargo-b", source.CanonicalDeliveryOrigin);
        Assert.Equal("Received", source.Workflow!.StateCode); Assert.True(source.Workflow.CompletionConfirmed);
        Assert.True(source.Workflow.PaymentReleaseReady); Assert.Null(source.Workflow.WaitingReasonCode);
        Assert.Equal(agreementDigest, source.SourceDigest);
        var reservation = await f.Data.Db.Set<분산재고예약Record>().SingleAsync(x => x.OrderId == order.OrderId);
        reservation.ReturnedQuantity = 1; await f.Data.Db.SaveChangesAsync();
        source = await f.Resolver.ResolveAsync(C.DistributedOrder, "batch-order-b", default);
        Assert.Null(source.RewardOutcome); Assert.DoesNotContain(source.CompletedWork, x => x.Kind == C.Delivery);
        Assert.False(source.Workflow!.CompletionConfirmed); Assert.False(source.Workflow.PaymentReleaseReady);
    }

    [Fact]
    public async Task 일부신규전달행만있는주문은구형전달수량을추정해성과기여를채우지않는다()
    {
        await using var f = await NeighborhoodSettlementSourceFixture.Create();
        await f.AddExactBatch(); await f.AddExactDelivery(partial: true);
        var source = await f.Resolver.ResolveAsync(C.DistributedOrder, "batch-order-b", default);
        var delivery = Assert.Single(source.CompletedWork.Where(x => x.Kind == C.Delivery));
        Assert.Equal(0, delivery.OriginRevision); Assert.Null(delivery.RelatedOrderId);
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
        string? reservationId = null, string? orderId = null, string? actorUserId = null)
    {
        var lot = await Data.Db.Set<분산재고LotRecord>().SingleAsync(x => x.LotId == LotId); var requestId = Guid.NewGuid();
        var actor = actorUserId ?? DistributedTestFixture.Operator.UserId;
        Data.Db.Add(new 재고이력 { 입고상품Id = lot.InboundItemId, 원인유형 = "분산물류실물영수증",
            이력유형 = kind == "Return" ? "실물반환" : kind == "Receive" ? "분할입고" : "기사직접피킹", 변경수량 = kind is "Return" or "Receive" ? quantity : -quantity,
            처리UserId = actor, 처리일시 = at, 메모 = "photos:" + photoId });
        Data.Db.Add(new 분산물류CommandReceipt { ActorUserId = actor, RequestId = requestId,
            Kind = kind + "Batch", AggregateId = aggregateId, ReservationId = reservationId, Quantity = quantity, RecordedAtUtc = at });
        Data.Db.Add(new 분산사진증빙Record { EvidenceId = photoId, ActorUserId = actor,
            UploadRequestId = Guid.NewGuid(), CompletionRequestId = requestId, Kind = kind, AggregateId = aggregateId, StatusCode = "Bound",
            CreatedAtUtc = at, BoundAtUtc = at, PrivateDetailsJson = JsonSerializer.Serialize(new {
                Binding = new DistributedEvidenceBinding { ActorUserId = actor, RequestId = requestId,
                    Kind = kind, AggregateId = aggregateId, LotIds = [LotId], OrderIds = orderId is null ? [] : [orderId] },
                Container = "private-fixture", ObjectName = "fixture.jpg" }, new JsonSerializerOptions(JsonSerializerDefaults.Web)) });
        await Data.Db.SaveChangesAsync();
    }
    public async Task<분산물류CommandReceipt> AddExactBatch(string damage = "")
    {
        var lot = await Data.Db.Set<분산재고LotRecord>().SingleAsync(x => x.LotId == LotId);
        Data.Db.Add(new 분산배송WaveRecord { WaveId = "batch-wave", DriverId = DistributedTestFixture.Operator.UserId, StatusCode = "Completed" });
        var history = new List<재고이력>(); var at = Data.Clock.Now.AddHours(-6);
        foreach (var suffix in new[] { "a", "b" })
        {
            var quantity = suffix == "a" ? 1 : 2;
            Data.Db.Add(new 분산배송주문Record { OrderId = "batch-order-" + suffix, WaveId = "batch-wave", CargoRequestId = "batch-cargo-" + suffix,
                CustomerUserId = "batch-buyer", SellerUserId = "seller-a", StatusCode = "Delivered" });
            Data.Db.Add(new 분산재고예약Record { ReservationId = "batch-reservation-" + suffix, OrderId = "batch-order-" + suffix,
                WaveId = "batch-wave", LotId = LotId, OutboundPlanId = quantity, Quantity = quantity, PickedQuantity = quantity, DeliveredQuantity = quantity });
            Data.Db.Add(new 화주운송의뢰 { 의뢰Id = "batch-cargo-" + suffix, 화주Id = "seller-a", 주문자UserId = "batch-buyer" });
            Data.Db.Add(new 운송원장 { 의뢰Id = "batch-cargo-" + suffix, 운송번호 = "batch-transport-" + suffix,
                확정기사Id = DistributedTestFixture.Operator.UserId, 상태 = "인수완료", 도착 = Data.Clock.Now });
            var row = new 재고이력 { 입고상품Id = lot.InboundItemId, 원인유형 = "분산물류실물영수증", 이력유형 = "기사직접피킹",
                변경수량 = -quantity, 처리UserId = DistributedTestFixture.Operator.UserId, 처리일시 = at, 메모 = "photos:batch-pick-photo" };
            history.Add(row); Data.Db.Add(row);
        }
        await Data.Db.SaveChangesAsync();
        var lines = history.Select((row, index) => new 분산물류완료행 { LineKey = "batch-reservation-" + (index == 0 ? "a" : "b"),
            WorkKind = "Pick", ActorUserId = DistributedTestFixture.Operator.UserId, LotId = LotId, InboundItemId = lot.InboundItemId,
            InventoryHistoryId = row.Id, ReservationId = "batch-reservation-" + (index == 0 ? "a" : "b"),
            OrderId = "batch-order-" + (index == 0 ? "a" : "b"), Quantity = index == 0 ? 1 : 2, PhotoEvidenceIds = ["batch-pick-photo"] }).ToArray();
        if (damage == "duplicate-history") lines[1] = lines[1] with { InventoryHistoryId = lines[0].InventoryHistoryId };
        if (damage == "wrong-actor") lines[0] = lines[0] with { ActorUserId = "unrelated-actor" };
        if (damage == "wrong-total") lines[0] = lines[0] with { Quantity = 2 };
        var receipt = new 분산물류CommandReceipt { ActorUserId = DistributedTestFixture.Operator.UserId, RequestId = Guid.NewGuid(),
            Kind = "PickBatch", AggregateId = "batch-wave", Revision = 7, Quantity = 3, RecordedAtUtc = at,
            CompletionLinesJson = JsonSerializer.Serialize(new 분산물류완료근거(damage == "unknown-schema" ? 2 : 1, lines), new JsonSerializerOptions(JsonSerializerDefaults.Web)) };
        Data.Db.Add(receipt);
        Data.Db.Add(BatchPhoto(receipt, "batch-pick-photo", "Pick", damage == "wrong-photo-order" ? ["another-order"] : ["batch-order-a", "batch-order-b"]));
        await Data.Db.SaveChangesAsync(); Data.Db.ChangeTracker.Clear(); return receipt;
    }
    public async Task AddExactDelivery(bool partial = false)
    {
        var lot = await Data.Db.Set<분산재고LotRecord>().SingleAsync(x => x.LotId == LotId);
        var lines = new[] { "a", "b" }.Select(suffix => new 분산물류완료행 { LineKey = "batch-reservation-" + suffix,
            WorkKind = "Deliver", ActorUserId = DistributedTestFixture.Operator.UserId, LotId = LotId, InboundItemId = lot.InboundItemId,
            ReservationId = "batch-reservation-" + suffix, OrderId = "batch-order-" + suffix,
            Quantity = suffix == "a" || partial ? 1 : 2, PhotoEvidenceIds = ["batch-deliver-photo"] }).ToArray();
        var receipt = new 분산물류CommandReceipt { ActorUserId = DistributedTestFixture.Operator.UserId, RequestId = Guid.NewGuid(),
            Kind = "DeliverBatch", AggregateId = "batch-wave", Revision = 8, Quantity = lines.Sum(x => x.Quantity), RecordedAtUtc = Data.Clock.Now,
            CompletionLinesJson = JsonSerializer.Serialize(new 분산물류완료근거(1, lines), new JsonSerializerOptions(JsonSerializerDefaults.Web)) };
        Data.Db.Add(receipt); Data.Db.Add(BatchPhoto(receipt, "batch-deliver-photo", "Deliver", ["batch-order-a", "batch-order-b"]));
        await Data.Db.SaveChangesAsync(); Data.Db.ChangeTracker.Clear();
    }
    private 분산사진증빙Record BatchPhoto(분산물류CommandReceipt receipt, string id, string kind, IReadOnlyList<string> orderIds)
        => new() { EvidenceId = id, ActorUserId = receipt.ActorUserId, UploadRequestId = Guid.NewGuid(), CompletionRequestId = receipt.RequestId,
            Kind = kind, AggregateId = receipt.AggregateId, StatusCode = "Bound", CreatedAtUtc = receipt.RecordedAtUtc, BoundAtUtc = receipt.RecordedAtUtc,
            PrivateDetailsJson = JsonSerializer.Serialize(new { Binding = new DistributedEvidenceBinding { ActorUserId = receipt.ActorUserId,
                RequestId = receipt.RequestId, Kind = kind, AggregateId = receipt.AggregateId, LotIds = new[] { LotId }, OrderIds = orderIds },
                Container = "private-fixture", ObjectName = "fixture.jpg" }, new JsonSerializerOptions(JsonSerializerDefaults.Web)) };
    public ValueTask DisposeAsync() => Data.DisposeAsync();
}
