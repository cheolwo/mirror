using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.Driver.Transport;
using Ssalddel.Application.Warehouse;
using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Controllers;
using 살뜰.도메인.창고;
using static Ssalddel.Tests.Application.Warehouse.DistributedTestFixture;

namespace Ssalddel.Tests.Application.Warehouse;

public sealed class DistributedFulfillmentUseCaseTests
{
    private static DistributedActor Driver => DistributedTestFixture.Driver;
    [Fact]
    public async Task 전역창고권한이나기존재고가없는사용자가본인공간과본인상품예정을명시등록한다()
    {
        await using var f = await Create(); var owner = new DistributedActor("first-space-owner");
        Assert.Contains("create-site", (await f.Use(owner).WorkspaceAsync(owner,"warehouse",default)).AllowedActions);
        await f.Export("warehouse", owner, "first-space");
        var created = await f.Use(owner).SiteAsync(owner,new(){RequestId=Guid.NewGuid(),PublicLabel="작은 보관공간", Floor="1층",Subspace="안쪽",PickupInstructions="합성 확인 안내",SpaceConfirmed=true,PickupPointConfirmed=true,
            NewWarehouse=new(){Name="본인 일상공간",ConfirmedRoadAddress="사용자가 확인한 합성 도로",Latitude=37,Longitude=127,AddressConfirmed=true,CoordinatesConfirmed=true}},default);
        Assert.Equal("PendingConsent",created.StatusCode); var site=(await f.Use(owner).WorkspaceAsync(owner,"warehouse",default)).Sites.Single();
        Assert.Equal("warehouse:"+site.WarehouseId,site.PlaceId); Assert.False(site.OwnerConsented); Assert.False(site.ManagerConsented);
        var warehouse=await f.Db.창고.AsNoTracking().SingleAsync(x=>x.Id==site.WarehouseId); Assert.Equal(owner.UserId,warehouse.소유자UserId);
        foreach(var kind in new[]{"Owner","Manager"}){var ack=await f.Use(owner).ConsentAsync(owner,site.SiteId,new(){RequestId=Guid.NewGuid(),ExpectedRevision=site.Revision,ConsentKind=kind,Granted=true,EvidenceReference="명시 동의"},default);site=(await f.Use(owner).WorkspaceAsync(owner,"warehouse",default)).Sites.Single();Assert.Equal(ack.Revision,site.Revision);}
        await f.Use(owner).LocationAsync(owner,site.SiteId,new(){RequestId=Guid.NewGuid(),ExpectedRevision=site.Revision,LocationLabel="작은 선반"},default);
        site=(await f.Use(owner).WorkspaceAsync(owner,"warehouse",default)).Sites.Single();
        await f.Export("shipper", owner, "first-source");
        var plan=await f.Use(owner).InboundAsync(owner,new(){RequestId=Guid.NewGuid(),SourceReference="본인 상품 예정",NewSource=new(){ProductName="본인 상품",Sku="first-sku",ExpectedQuantity=3,StockOwnerIsCurrentSellerConfirmed=true,OwnershipEvidenceReference="직접 소유 확인"},
            Lines=[new(){LineKey="first",SourceInboundId=0,SiteId=site.SiteId,LocationId=site.Locations.Single().LocationId,ProductName="본인 상품",Sku="first-sku",LotCode="first-lot",ExpectedQuantity=3}]},default);
        Assert.Equal("Planned",plan.StatusCode);Assert.Equal(0,(await f.Use(owner).WorkspaceAsync(owner,"shipper",default)).Lots.Sum(x=>x.ReceivedQuantity));
        Assert.Empty(await f.Db.창고사용자.Where(x=>x.UserId==owner.UserId).ToArrayAsync());
    }
    [Fact]
    public async Task 두거점두기업두고객의실제화물수락부분피킹전달과영수증재조회가같은원장으로완료된다()
    {
        await using var f = await Create(); var wave = await f.Round(accept: false, quantityPerLot: 2);
        await f.Export("warehouse", Operator, "planned"); await f.Export("shipper", SellerA, "planned");
        await f.Use(Driver).AcceptAsync(Driver, wave.WaveId, new() { RequestId = Guid.NewGuid(), ExpectedRevision = wave.Revision }, default);
        wave = await f.Use(Operator).WaveAsync(Operator, wave.WaveId, "warehouse", default);
        Assert.Equal("Accepted", wave.StatusCode); Assert.Equal(2, wave.Stops.Count(x => x.Kind == "Pickup"));
        Assert.Equal(Enumerable.Range(1, wave.Stops.Count), wave.Stops.Select(x => x.Sequence));
        Assert.All(await f.Db.운송원장.ToArrayAsync(), x => Assert.Equal(Driver.UserId, x.확정기사Id));
        Assert.Equal(4, await f.Db.운송의뢰상품연결.CountAsync());
        Assert.Equal(4, await f.Db.출고예정.CountAsync());
        foreach (var transport in await f.Db.운송원장.AsNoTracking().ToArrayAsync())
            await AssertGeneralCustodyBlocked(f, transport.Id);
        await f.Export("warehouse", Operator, "accepted"); await f.Export("shipper", SellerA, "accepted"); await f.Export("driver", Driver, "pickup");
        f.Publisher.DbTransaction = () => f.Db.Database.CurrentTransaction;
        var firstPartial = true;
        while (wave.StatusCode != "Completed")
        {
            var ws = await f.Use(Driver).WorkspaceAsync(Driver, "driver", default); wave = ws.Waves.Single();
            var stop = wave.Stops.Single(x => x.StopId == wave.CurrentStopId); f.Clock.Now = stop.ExpectedArrivalUtc; f.RefreshLocation();
            if (stop.Kind == "Pickup")
            {
                foreach (var reservation in wave.Reservations.Where(x => x.SiteId == stop.SiteId && stop.OrderIds.Contains(x.OrderId)))
                {
                    var barcode = ws.Sites.Single(x => x.SiteId == reservation.SiteId).Locations.Single(x => x.LocationId == reservation.LocationId).Barcode;
                    var quantity = firstPartial ? 1 : reservation.Quantity - reservation.PickedQuantity; firstPartial = false;
                    var request = new DistributedPickRequest { RequestId = Guid.NewGuid(), ExpectedRevision = wave.Revision, StopId = stop.StopId,
                        ReservationId = reservation.ReservationId, LocationBarcode = barcode, OwnerUserId = reservation.OwnerUserId, Sku = reservation.Sku,
                        Quantity = quantity, EvidenceReference = "fixture-scanned-count" };
                    await f.Proof(Driver, wave.WaveId, request);
                    var ack = await f.Use(Driver).PickAsync(Driver, wave.WaveId, request, default);
                    var replay = await f.Use(Driver).PickAsync(Driver, wave.WaveId, request, default); Assert.True(replay.Replayed); Assert.Equal(ack.Revision, replay.Revision);
                    Assert.Equal(quantity, (await f.Use(Driver).ReceiptAsync(Driver, request.RequestId, default)).Quantity);
                    wave = await f.Use(Driver).WaveAsync(Driver, wave.WaveId, "driver", default);
                    var pickedCargoId = wave.Orders.Single(x => x.OrderId == reservation.OrderId).CargoRequestId;
                    var pickedTransportId = await f.Db.운송원장.AsNoTracking().Where(x => x.의뢰Id == pickedCargoId).Select(x => x.Id).SingleAsync();
                    await AssertGeneralCustodyBlocked(f, pickedTransportId);
                    var stored = wave.Reservations.Single(x => x.ReservationId == reservation.ReservationId);
                    if (stored.PickedQuantity < stored.Quantity)
                    {
                        var second = new DistributedPickRequest { RequestId = Guid.NewGuid(), ExpectedRevision = wave.Revision, StopId = stop.StopId,
                            ReservationId = reservation.ReservationId, LocationBarcode = barcode, OwnerUserId = reservation.OwnerUserId, Sku = reservation.Sku,
                            Quantity = stored.Quantity - stored.PickedQuantity, EvidenceReference = "fixture-second-count" };
                        await f.Proof(Driver, wave.WaveId, second);
                        await f.Use(Driver).PickAsync(Driver, wave.WaveId, second, default); wave = await f.Use(Driver).WaveAsync(Driver, wave.WaveId, "driver", default);
                    }
                }
            }
            else
            {
                await f.Export("driver", Driver, "delivery");
                foreach (var orderId in stop.OrderIds)
                {
                    var cargoId = wave.Orders.Single(x => x.OrderId == orderId).CargoRequestId;
                    var transportId = await f.Db.운송원장.AsNoTracking().Where(x => x.의뢰Id == cargoId).Select(x => x.Id).SingleAsync();
                    Assert.All(wave.Reservations.Where(x => x.OrderId == orderId), x => { Assert.Equal(x.Quantity, x.PickedQuantity); Assert.Equal(0, x.DeliveredQuantity); });
                    await AssertGeneralCustodyBlocked(f, transportId);
                    await f.Use(Driver).DeliverAsync(Driver, wave.WaveId, await f.Proof(Driver, wave.WaveId, new DistributedDeliveryRequest { RequestId = Guid.NewGuid(), ExpectedRevision = wave.Revision,
                        StopId = stop.StopId, OrderId = orderId, EvidenceReference = "fixture-delivery-handover" }), default);
                    wave = await f.Use(Operator).WaveAsync(Operator, wave.WaveId, "warehouse", default);
                }
            }
        }
        f.Db.ChangeTracker.Clear();
        Assert.All(await f.Db.운송원장.ToArrayAsync(), x => Assert.Equal("인수완료", x.상태));
        Assert.All(await f.Db.화주운송의뢰.ToArrayAsync(), x => { Assert.Equal("인수완료", x.배차상태); Assert.Equal("결제대기", x.결제상태); });
        Assert.All(await f.Db.Set<분산재고예약Record>().ToArrayAsync(), x => Assert.Equal(x.Quantity, x.DeliveredQuantity));
        Assert.All(await f.Db.출고예정.ToArrayAsync(), x => Assert.Equal(출고상태.출고완료, x.상태));
        Assert.Equal(2, f.Publisher.Events.OfType<운송인수완료됨Event>().Count());
        foreach (var customer in new[] { CustomerA, CustomerB })
        {
            await f.Export("orderer", customer, "delivered");
            var customerWave = await f.Use(customer).WaveAsync(customer, wave.WaveId, "orderer", default);
            var own = Assert.Single(customerWave.Orders); Assert.True(own.IsCurrentCustomer); Assert.Empty(customerWave.Reservations);
            Assert.Null(customerWave.DriverId); Assert.All(customerWave.Stops, x => Assert.Null(x.Address));
            await f.Use(customer).AcknowledgeAsync(customer, wave.WaveId, new() { RequestId = Guid.NewGuid(), ExpectedRevision = customerWave.Revision,
                OrderId = own.OrderId, EvidenceReference = "fixture-customer-receipt" }, default);
            Assert.True(Assert.Single((await f.Use(customer).WaveAsync(customer, wave.WaveId, "orderer", default)).Orders).CustomerAcknowledged);
            await f.Export("orderer", customer, "acknowledged");
        }
    }
    private static async Task AssertGeneralCustodyBlocked(DistributedTestFixture f, long transportId)
    {
        var before = await f.Db.운송원장.AsNoTracking().SingleAsync(x => x.Id == transportId);
        var shipperBefore = await f.Db.화주운송의뢰.AsNoTracking().SingleAsync(x => x.의뢰Id == before.의뢰Id);
        var eventsBefore = await f.Db.운송이벤트.CountAsync(); var publishedBefore = f.Publisher.Events.Count;
        foreach (var target in new[] { 기사운송상태코드.상차지도착, 기사운송상태코드.상차완료, 기사운송상태코드.하차지도착, 기사운송상태코드.인수완료 })
        {
            var result = await f.GeneralTransportProgressAsync(transportId, target); Assert.True(result.IsFailed);
            Assert.Equal(409, result.Errors.Single().Metadata["StatusCode"]);
            // The existing pickup handler can stop unpicked stock before reaching the shared executor.
            if (target != 기사운송상태코드.상차완료 || (await f.Db.출고예정.AsNoTracking().Where(x => x.운송의뢰Id == before.의뢰Id).ToArrayAsync()).All(x => x.상태 == 출고상태.출고완료))
            {
                Assert.Equal("DistributedCargoCustodyRequired", result.Errors.Single().Metadata["Code"]);
                Assert.Contains("분산 순회", result.Errors.Single().Message);
                AssertGeneralCustodyHttpError(result);
            }
        }
        var after = await f.Db.운송원장.AsNoTracking().SingleAsync(x => x.Id == transportId);
        var shipperAfter = await f.Db.화주운송의뢰.AsNoTracking().SingleAsync(x => x.의뢰Id == before.의뢰Id);
        Assert.Equal(before.상태, after.상태); Assert.Equal(before.UpdatedAt, after.UpdatedAt);
        Assert.Equal(before.출발_픽업, after.출발_픽업); Assert.Equal(before.도착, after.도착); Assert.Equal(before.첨부_json, after.첨부_json);
        Assert.Equal(shipperBefore.배차상태, shipperAfter.배차상태); Assert.Equal(shipperBefore.UpdatedAt, shipperAfter.UpdatedAt);
        Assert.Equal(eventsBefore, await f.Db.운송이벤트.CountAsync()); Assert.Equal(publishedBefore, f.Publisher.Events.Count);
    }
    private sealed class CustodyResultController : ControllerBase { }
    private static void AssertGeneralCustodyHttpError(FluentResults.Result<Ssalddel.Contracts.Driver.Transport.기사운송상태변경응답> result)
    {
        var controller = new CustodyResultController { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        var response = Assert.IsType<ObjectResult>(Result응답확장.ToActionResult(controller, result));
        Assert.Equal(StatusCodes.Status409Conflict, response.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(response.Value);
        Assert.Equal(StatusCodes.Status409Conflict, problem.Status);
        Assert.Equal("DistributedCargoCustodyRequired", problem.Extensions["errorCode"]);
        Assert.Contains("분산 순회", Assert.IsType<string>(problem.Title));
    }
    [Theory]
    [InlineData(true, "상차지도착")]
    [InlineData(true, "상차완료")]
    [InlineData(true, "하차지도착")]
    [InlineData(true, "인수완료")]
    [InlineData(false, "상차지도착")]
    [InlineData(false, "상차완료")]
    [InlineData(false, "하차지도착")]
    [InlineData(false, "인수완료")]
    public async Task 분산테이블없는기존DB에서도출고완료DLOC진행은막고일반화물진행은보존한다(bool distributed, string target)
    {
        await using var f = await Create();
        foreach (var table in new[] { "distributed_receipts", "distributed_reservations", "distributed_orders", "distributed_waves", "distributed_lots", "distributed_inbound_plans", "distributed_locations", "distributed_sites" })
            await f.Db.Database.ExecuteSqlRawAsync("DROP TABLE " + table);
        await f.Cargo("legacy-cargo", SellerA, CustomerA, 1);
        var transport = await f.Db.운송원장.SingleAsync(x => x.의뢰Id == "legacy-cargo");
        transport.기사_운송자 = Driver.UserId; transport.확정기사Id = Driver.UserId;
        transport.상태 = target switch { "상차지도착" => "배차확정", "상차완료" => "상차지도착", "하차지도착" => "상차완료", _ => "하차지도착" };
        var previous = transport.상태; var inventory = await f.Db.입고상품.SingleAsync(); inventory.보관위치 = distributed ? "DLOC-fixture" : "LEGACY-RACK";
        f.Db.출고예정.Add(new() { 운송의뢰Id = "legacy-cargo", 입고상품Id = inventory.Id, 출고창고Id = inventory.창고Id, 수량 = 1, 상태 = 출고상태.출고완료 });
        await f.Db.SaveChangesAsync(); var id = transport.Id; f.Db.ChangeTracker.Clear();
        var result = await f.GeneralTransportProgressAsync(id, target);
        if (distributed)
        {
            Assert.True(result.IsFailed); Assert.Equal(409, result.Errors.Single().Metadata["StatusCode"]);
            Assert.Equal("DistributedCargoCustodyRequired", result.Errors.Single().Metadata["Code"]);
            AssertGeneralCustodyHttpError(result);
            Assert.Equal(previous, (await f.Db.운송원장.AsNoTracking().SingleAsync(x => x.Id == id)).상태);
            Assert.Equal(0, await f.Db.운송이벤트.CountAsync()); Assert.Empty(f.Publisher.Events);
        }
        else
        {
            Assert.True(result.IsSuccess); Assert.Equal(target, result.Value.상태);
            Assert.Equal(target, (await f.Db.운송원장.AsNoTracking().SingleAsync(x => x.Id == id)).상태);
            Assert.Equal(target, (await f.Db.화주운송의뢰.AsNoTracking().SingleAsync(x => x.의뢰Id == "legacy-cargo")).배차상태);
            Assert.Equal(1, await f.Db.운송이벤트.CountAsync()); Assert.Single(f.Publisher.Events);
        }
    }
    [Fact]
    public async Task 부분입고는원예정전체를받은것으로바꾸지않고소유자와대리판매자를보존한다()
    {
        await using var f = await Create(); var sites = await f.Sites();
        var plan = await f.Use(SellerA).InboundAsync(SellerA, new() { RequestId = Guid.NewGuid(), SourceReference = "split",
            Lines = sites.Select((s, i) => new DistributedInboundLineRequest { LineKey = "line-" + i, SourceInboundId = 101, SiteId = s.SiteId,
                LocationId = s.Locations.Single().LocationId, Sku = "same-sku", ProductName = "A", LotCode = "split-" + i, ExpectedQuantity = 5 }).ToArray() }, default);
        var request = new DistributedInboundReceiptRequest { RequestId = Guid.NewGuid(), ExpectedRevision = plan.Revision, LineKey = "line-0",
            LocationBarcode = sites[0].Locations.Single().Barcode, Sku = "same-sku", Quantity = 2, EvidenceReference = "fixture-partial" };
        await f.EnsureAgreement(sites[0], new("goods-owner-a"), "same-sku");
        await f.Proof(Operator, plan.AggregateId, request);
        await f.Use(Operator).ReceiveAsync(Operator, plan.AggregateId, request, default); await f.Use(Operator).ReceiveAsync(Operator, plan.AggregateId, request, default);
        var source = await f.Db.입고요청.AsNoTracking().SingleAsync(x => x.Id == 101); Assert.Equal("부분입고", source.상태); Assert.Null(source.입고완료일시);
        var lots = (await f.Use(SellerA).WorkspaceAsync(SellerA, "shipper", default)).Lots;
        Assert.Equal(2, lots.Sum(x => x.ReceivedQuantity)); Assert.All(lots, x => { Assert.Equal("goods-owner-a", x.OwnerUserId); Assert.Equal(SellerA.UserId, x.SellerUserId); });
        await Error("DistributedRequestContentConflict", () => f.Use(Operator).ReceiveAsync(Operator, plan.AggregateId,
            new() { RequestId = request.RequestId, ExpectedRevision = request.ExpectedRevision, LineKey = request.LineKey, LocationBarcode = request.LocationBarcode, Sku = request.Sku, Quantity = 3, EvidenceReference = request.EvidenceReference }, default));
    }
    [Fact]
    public async Task 일부배차실패는피킹권한을주지않고새명시수락으로복구한다()
    {
        await using var f = await Create(); f.FailCargoId = "cargo-b"; var wave = await f.Round();
        Assert.Equal("AcceptancePending", wave.StatusCode); var pending = await f.Use(Driver).WorkspaceAsync(Driver, "driver", default);
        Assert.Empty(pending.Sites); Assert.Empty(pending.Lots); Assert.Empty(pending.Waves.Single().Reservations);
        f.FailCargoId = null;
        var result = await f.Use(Driver).AcceptAsync(Driver, wave.WaveId, new() { RequestId = Guid.NewGuid(), ExpectedRevision = wave.Revision,
            AcknowledgedWarningCodes = [살뜰.Services.Dispatch.Recommendation.화물배차수락경고코드.차량주의사항] }, default);
        Assert.Equal("Accepted", result.StatusCode); Assert.Equal(2, (await f.Use(Driver).WorkspaceAsync(Driver, "driver", default)).Sites.Count);
    }
    [Theory]
    [InlineData("Expired")]
    [InlineData("AssignmentRemoved")]
    public async Task 만료되거나배정해제된기사는출입정보바코드고객주소를재조회할수없다(string mode)
    {
        await using var f = await Create(); var wave = await f.Round();
        Assert.NotEmpty((await f.Use(Driver).WorkspaceAsync(Driver, "driver", default)).Sites);
        if (mode == "Expired") f.Clock.Now = wave.DeliveryEndUtc.AddSeconds(1);
        else await f.Db.운송원장.ExecuteUpdateAsync(x => x.SetProperty(t => t.확정기사Id, "another-driver"));
        var ws = await f.Use(Driver).WorkspaceAsync(Driver, "driver", default);
        Assert.Empty(ws.Sites); Assert.Empty(ws.Lots); Assert.Empty(ws.Waves.Single().Reservations);
        Assert.DoesNotContain("pick", ws.Waves.Single().AllowedActions); Assert.All(ws.Waves.Single().Stops, x => { Assert.Null(x.Address); Assert.Null(x.Latitude); });
        if (mode == "Expired") await f.Export("driver", Driver, "expired");
    }
    [Fact]
    public async Task 취소는미피킹만해제하고실물반납이후미완료기사사실을보존하여재계획한다()
    {
        await using var f = await Create(); var wave = await f.Round(quantityPerLot: 2); var ws = await f.Use(Driver).WorkspaceAsync(Driver, "driver", default);
        var stop = wave.Stops.First(); f.Clock.Now = stop.ExpectedArrivalUtc; var reservation = wave.Reservations.First(x => x.SiteId == stop.SiteId);
        var barcode = ws.Sites.Single(x => x.SiteId == stop.SiteId).Locations.Single().Barcode;
        await f.Use(Driver).PickAsync(Driver, wave.WaveId, await f.Proof(Driver, wave.WaveId, new DistributedPickRequest { RequestId = Guid.NewGuid(), ExpectedRevision = wave.Revision, StopId = stop.StopId,
            ReservationId = reservation.ReservationId, OwnerUserId = reservation.OwnerUserId, Sku = reservation.Sku, LocationBarcode = barcode, Quantity = 1, EvidenceReference = "fixture-pick" }), default);
        wave = await f.Use(Operator).WaveAsync(Operator, wave.WaveId, "warehouse", default);
        await f.Use(Operator).CancelAsync(Operator, wave.WaveId, new() { RequestId = Guid.NewGuid(), ExpectedRevision = wave.Revision, OrderId = reservation.OrderId, Reason = "fixture-cancel" }, default);
        wave = await f.Use(Operator).WaveAsync(Operator, wave.WaveId, "warehouse", default); Assert.Equal("ReturnRequired", wave.StatusCode);
        var row = wave.Reservations.Single(x => x.ReservationId == reservation.ReservationId); Assert.Equal(1, row.PickedQuantity); Assert.Equal(1, row.ReleasedQuantity); Assert.Equal(0, row.ReturnedQuantity);
        await Error("DistributedWaveCannotPlan", () => f.Use(Operator).PlanAsync(Operator, wave.WaveId, new() { RequestId = Guid.NewGuid(), ExpectedRevision = wave.Revision, DriverId = Driver.UserId }, default));
        await f.Use(Operator).ReturnAsync(Operator, wave.WaveId, await f.Proof(Operator, wave.WaveId, new DistributedReturnRequest { RequestId = Guid.NewGuid(), ExpectedRevision = wave.Revision, ReservationId = row.ReservationId,
            LocationBarcode = barcode, Quantity = 1, EvidenceReference = "fixture-physical-return" }), default);
        wave = await f.Use(Operator).WaveAsync(Operator, wave.WaveId, "warehouse", default); Assert.Equal("NeedsReplan", wave.StatusCode); Assert.Contains("plan", wave.AllowedActions);
        var cancelledCargoId = wave.Orders.Single(x => x.OrderId == reservation.OrderId).CargoRequestId;
        Assert.Equal("취소", (await f.Db.운송원장.AsNoTracking().SingleAsync(x => x.의뢰Id == cancelledCargoId)).상태);
        f.RefreshLocation(); await f.Use(Operator).PlanAsync(Operator, wave.WaveId, new() { RequestId = Guid.NewGuid(), ExpectedRevision = wave.Revision, DriverId = Driver.UserId }, default);
        Assert.Equal(1, (await f.Db.Set<분산재고예약Record>().AsNoTracking().SingleAsync(x => x.ReservationId == row.ReservationId)).ReturnedQuantity);
    }
    [Fact]
    public async Task 부족한경로는계획저장없이실패하며다른계정의영수증도차단한다()
    {
        await using var f = await Create(); var wave = await f.Round(false); var frozen = (await f.Db.Set<분산배송WaveRecord>().AsNoTracking().SingleAsync()).RouteJson;
        f.Routes.Available = false; await Error("DistributedRouteInfeasibleOrEvidenceMissing", () => f.Use(Operator).PlanAsync(Operator, wave.WaveId,
            new() { RequestId = Guid.NewGuid(), ExpectedRevision = wave.Revision, DriverId = Driver.UserId }, default));
        Assert.Equal(frozen, (await f.Db.Set<분산배송WaveRecord>().AsNoTracking().SingleAsync()).RouteJson);
        var receipt = (await f.Use(Operator).WorkspaceAsync(Operator, "warehouse", default)).Receipts.First();
        await Error("DistributedReceiptNotFound", () => f.Use(CustomerA).ReceiptAsync(CustomerA, receipt.RequestId, default));
        await Error("DistributedFulfillmentDisabled", () => f.Use(Operator, false).WorkspaceAsync(Operator, "warehouse", default));
    }
    private static async Task Error(string code, Func<Task> call) => Assert.Equal(code, (await Assert.ThrowsAsync<DistributedFulfillmentException>(call)).Code);

    [Theory]
    [InlineData("Owner")]
    [InlineData("Barcode")]
    [InlineData("Sku")]
    [InlineData("Quantity")]
    [InlineData("Revision")]
    [InlineData("Driver")]
    public async Task 잘못된소유자위치상품수량판본기사는인수원장을전혀바꾸지않는다(string field)
    {
        await using var f = await Create(); var wave = await f.Round(); var ws = await f.Use(Driver).WorkspaceAsync(Driver,"driver",default);
        var stop = wave.Stops.First(); f.Clock.Now = stop.ExpectedArrivalUtc; var r = wave.Reservations.First(x => x.SiteId == stop.SiteId);
        var barcode = ws.Sites.Single(x => x.SiteId == stop.SiteId).Locations.Single().Barcode;
        var actor = field == "Driver" ? new DistributedActor("other-driver",true) : Driver;
        await Error(field == "Revision" ? "DistributedRevisionConflict" : field == "Driver" ? "DistributedAssignedDriverRequired" : "DistributedPickReceiptMismatch",
            () => f.Use(actor).PickAsync(actor, wave.WaveId, new() { RequestId = Guid.NewGuid(), ExpectedRevision = wave.Revision + (field == "Revision" ? 1 : 0),
                StopId = stop.StopId, ReservationId = r.ReservationId, OwnerUserId = field == "Owner" ? "different-owner" : r.OwnerUserId,
                LocationBarcode = field == "Barcode" ? "different-barcode" : barcode, Sku = field == "Sku" ? "different-sku" : r.Sku,
                Quantity = field == "Quantity" ? r.Quantity + 1 : 1, EvidenceReference = "fixture-invalid-receipt" }, default));
        Assert.All(await f.Db.Set<분산재고예약Record>().AsNoTracking().ToArrayAsync(), x => Assert.Equal(0, x.PickedQuantity));
        Assert.All(await f.Db.운송원장.AsNoTracking().ToArrayAsync(), x => Assert.Equal("확정", x.상태));
        Assert.All(ws.Lots, x => Assert.Contains(x.OwnerLabel, new[] { "A 기업 소유자", "B 기업 소유자" }));
    }
    [Theory]
    [InlineData("Operational")]
    [InlineData("WrongProfile")]
    [InlineData("Disabled")]
    public async Task 정확한모의프로필을명시하지않으면재고와배차쓰기가닫힌다(string gate)
    {
        await using var f = await Create();
        var use = f.Use(Operator, gate != "Disabled", gate == "Operational" ? 살뜰.Services.Options.SsalddelExecutionMode.Operational : 살뜰.Services.Options.SsalddelExecutionMode.Simulation,
            gate == "WrongProfile" ? "legacy-profile" : null);
        await Error("DistributedFulfillmentDisabled", () => use.WorkspaceAsync(Operator,"warehouse",default));
        Assert.Empty(await f.Db.Set<분산거점Record>().ToArrayAsync());
    }
    [Fact]
    public async Task 원천예정량초과와UTC아닌시간대는새재고나시간대를생성하지않는다()
    {
        await using var f = await Create(); var sites = await f.Sites();
        await Error("DistributedInboundSourceOverallocated", () => f.Use(SellerA).InboundAsync(SellerA, new() { RequestId = Guid.NewGuid(), SourceReference = "overflow",
            Lines = [new() { LineKey = "a", SourceInboundId = 101, SiteId = sites[0].SiteId, LocationId = sites[0].Locations.Single().LocationId,
                Sku = "same-sku", ProductName = "A", LotCode = "overflow", ExpectedQuantity = 11 }] }, default));
        await Error("DistributedWaveWindowInvalid", () => f.Use(Operator).CreateWaveAsync(Operator, new() { RequestId = Guid.NewGuid(), Label = "invalid",
            CutoffUtc = DateTime.SpecifyKind(f.Clock.Now.AddMinutes(1), DateTimeKind.Unspecified), PickupStartUtc = f.Clock.Now.AddMinutes(2),
            PickupEndUtc = f.Clock.Now.AddHours(2), DeliveryStartUtc = f.Clock.Now.AddMinutes(2), DeliveryEndUtc = f.Clock.Now.AddHours(3) }, default));
        Assert.Empty(await f.Db.Set<분산재고LotRecord>().ToArrayAsync()); Assert.Empty(await f.Db.Set<분산배송WaveRecord>().ToArrayAsync());
    }
    [Fact]
    public async Task 열린시간대둘은독립순회ID를가지며동일출고기존예약의수량은재예약하지않는다()
    {
        await using var f = await Create(); var sites = await f.Sites(); var lots = (await f.Stock(sites)).Where(x => x.SellerUserId == SellerA.UserId).ToArray();
        await f.Cargo("existing-reserved-cargo", SellerA, CustomerA, quantity: 10);
        var lines = new List<DistributedOrderLineRequest>();
        foreach(var lot in lots)
        {
            await f.Db.입고상품.Where(x => x.Id == lot.InboundItemId).ExecuteUpdateAsync(s => s.SetProperty(x => x.가용수량, 0).SetProperty(x => x.예약수량, 5));
            var outbound = new 출고예정 { 입고상품Id = lot.InboundItemId, 입고요청Id = (await f.Db.입고상품.AsNoTracking().SingleAsync(x => x.Id == lot.InboundItemId)).입고요청Id,
                출고창고Id = sites.Single(x => x.SiteId == lot.SiteId).WarehouseId, 판매자UserId = SellerA.UserId, 주문자UserId = CustomerA.UserId, 수량 = 5, 상태 = "준비중", 운송의뢰Id = "existing-reserved-cargo" };
            f.Db.Add(outbound); f.Db.운송의뢰상품연결.Add(new() { 운송의뢰Id = "existing-reserved-cargo", 입고상품Id = lot.InboundItemId, 할당수량 = 5 });
            await f.Db.SaveChangesAsync(); lines.Add(new() { LotId = lot.LotId, Quantity = 5, ExistingOutboundPlanId = outbound.Id });
        }
        var waves = new List<DistributedCommandResponse>();
        for(var i=0;i<2;i++) waves.Add(await f.Use(Operator).CreateWaveAsync(Operator,new(){ RequestId=Guid.NewGuid(), Label="독립 시간대"+i,
            CutoffUtc=f.Clock.Now.AddMinutes(1),PickupStartUtc=f.Clock.Now.AddMinutes(2),PickupEndUtc=f.Clock.Now.AddHours(2),DeliveryStartUtc=f.Clock.Now.AddMinutes(2),DeliveryEndUtc=f.Clock.Now.AddHours(3)},default));
        Assert.Equal(2, (await f.Db.Set<분산배송WaveRecord>().AsNoTracking().Select(x=>x.RoundId).ToArrayAsync()).Distinct().Count());
        var candidateWorkspace = await f.Use(SellerA).WorkspaceAsync(SellerA, "shipper", default);
        Assert.Equal(2, candidateWorkspace.ExistingOutboundCandidates.Count);
        Assert.All(candidateWorkspace.ExistingOutboundCandidates, x => { Assert.Equal(5, x.Quantity); Assert.Equal(SellerA.UserId, x.SellerUserId); Assert.Equal("goods-owner-a", x.OwnerUserId);
            Assert.Contains(candidateWorkspace.CargoCandidates, c => c.CargoRequestId == x.CargoRequestId); });
        Assert.Empty((await f.Use(SellerB).WorkspaceAsync(SellerB, "shipper", default)).ExistingOutboundCandidates);
        Assert.Empty((await f.Use(new("goods-owner-a")).WorkspaceAsync(new("goods-owner-a"), "shipper", default)).ExistingOutboundCandidates);
        await f.Cargo("foreign-import-cargo", SellerB, CustomerB, quantity: 10);
        await Error("DistributedStockSellerMismatch", () => f.Use(SellerB).OrderAsync(SellerB, waves[0].AggregateId,
            new() { RequestId = Guid.NewGuid(), ExpectedRevision = 1, CargoRequestId = "foreign-import-cargo", OrderReference = "다른 기업 예약 도용", Lines = lines }, default));
        await f.Export("shipper", SellerA, "existing-reserved");
        var request = new DistributedOrderRequest { RequestId=Guid.NewGuid(), ExpectedRevision=1,OrderReference="existing",CargoRequestId="existing-reserved-cargo",Lines=lines };
        await f.Use(SellerA).OrderAsync(SellerA,waves[0].AggregateId,request,default); await f.Use(SellerA).OrderAsync(SellerA,waves[0].AggregateId,request,default);
        Assert.All(await f.Db.입고상품.AsNoTracking().Where(x=>lots.Select(l=>l.InboundItemId).Contains(x.Id)).ToArrayAsync(),x=>{Assert.Equal(0,x.가용수량);Assert.Equal(5,x.예약수량);});
        Assert.Empty((await f.Use(SellerA).WorkspaceAsync(SellerA, "shipper", default)).ExistingOutboundCandidates);
        await Error("DistributedStorageConflict",()=>f.Use(SellerA).OrderAsync(SellerA,waves[1].AggregateId,new(){RequestId=Guid.NewGuid(),ExpectedRevision=1,OrderReference="duplicate",CargoRequestId="existing-reserved-cargo",Lines=lines},default));
        Assert.Equal(2,await f.Db.Set<분산재고예약Record>().CountAsync());
    }

    [Fact]
    public async Task 다른거점재고부족은전체예약을rollback하고같은출고예약을두번사용하지않는다()
    {
        await using var f = await Create(); var sites = await f.Sites(); var lots = (await f.Stock(sites)).Where(x => x.SellerUserId == SellerA.UserId).OrderBy(x => x.LotId).ToArray();
        await f.Cargo("cargo-atomic", SellerA, CustomerA);
        var wave = await f.Use(Operator).CreateWaveAsync(Operator, new() { RequestId = Guid.NewGuid(), Label = "원자 예약",
            CutoffUtc = f.Clock.Now.AddMinutes(1), PickupStartUtc = f.Clock.Now.AddMinutes(2), PickupEndUtc = f.Clock.Now.AddHours(2),
            DeliveryStartUtc = f.Clock.Now.AddMinutes(2), DeliveryEndUtc = f.Clock.Now.AddHours(3) }, default);
        // A competing authoritative reservation has consumed the second location before this multi-site command.
        await f.Db.입고상품.Where(x => x.Id == lots[1].InboundItemId).ExecuteUpdateAsync(s => s.SetProperty(x => x.가용수량, 0).SetProperty(x => x.예약수량, 5));
        await Error("DistributedStockInsufficientOrChanged", () => f.Use(SellerA).OrderAsync(SellerA, wave.AggregateId, new() { RequestId = Guid.NewGuid(), ExpectedRevision = 1,
            OrderReference = "atomic", CargoRequestId = "cargo-atomic", Lines = lots.Select(x => new DistributedOrderLineRequest { LotId = x.LotId, Quantity = 1 }).ToArray() }, default));
        Assert.Equal(5, (await f.Db.입고상품.AsNoTracking().SingleAsync(x => x.Id == lots[0].InboundItemId)).가용수량);
        Assert.Empty(await f.Db.출고예정.ToArrayAsync()); Assert.Empty(await f.Db.Set<분산재고예약Record>().ToArrayAsync()); Assert.Empty(await f.Db.운송의뢰상품연결.ToArrayAsync());
        Assert.Empty(await f.Db.Set<분산배송주문Record>().ToArrayAsync());
    }
    [Fact]
    public async Task 용량과고객시간창때문에피킹전달혼합이필요하면전체피킹선행을강제하지않는다()
    {
        await using var f = await Create(); var options = Microsoft.Extensions.Options.Options.Create(new DistributedFulfillmentOptions { UseExistingRouteProvider = true, MaximumRouteStops = 8 });
        var p = new 분산배송경로Planner(f.Routes, options); var now = f.Clock.Now;
        var loads = new Dictionary<string, 분산경로적재량> { ["a"] = new(600, 1, 0), ["b"] = new(600, 1, 0) };
        var stops = new 분산경로Stop[] {
            new("pickup-a", "Pickup", "site-a", ["order-a"], ["a"], "합성 A", new(37,127), now, now.AddHours(2), 1, loads),
            new("pickup-b", "Pickup", "site-b", ["order-b"], ["b"], "합성 B", new(37.04m,127), now, now.AddHours(2), 1, loads),
            new("delivery-a", "Delivery", null, ["order-a"], ["a"], "합성 고객 A", new(37.01m,127), now, now.AddMinutes(8), 1, loads),
            new("delivery-b", "Delivery", null, ["order-b"], ["b"], "합성 고객 B", new(37.05m,127), now, now.AddHours(2), 1, loads) };
        var result = await p.PlanAsync(new(37,127), now, new(1000, 10, 10), stops, stops, default);
        Assert.Equal(["pickup-a", "delivery-a", "pickup-b", "delivery-b"], result.Stops.Select(x => x.StopId));
        Assert.Contains("elapsedIncludingWaitAndServiceThenDistance", result.EvidenceJson);
    }
    [Fact]
    public async Task 철회는기존실재고보관을남기고새피킹과입고를차단한다()
    {
        await using var f = await Create(); var wave = await f.Round(); var driverBefore = await f.Use(Driver).WorkspaceAsync(Driver, "driver", default);
        var current = wave.Stops.Single(x => x.StopId == wave.CurrentStopId);
        var site = (await f.Use(Operator).WorkspaceAsync(Operator,"warehouse",default)).Sites.Single(x => x.SiteId == current.SiteId);
        await f.Use(Operator).ConsentAsync(Operator, site.SiteId, new() { RequestId = Guid.NewGuid(), ExpectedRevision = site.Revision, ConsentKind = "Owner", Granted = false, EvidenceReference = "fixture-revoked" }, default);
        var stored = await f.Db.Set<분산거점Record>().AsNoTracking().SingleAsync(x => x.SiteId == site.SiteId); Assert.Equal("RevocationPendingCustody", stored.StatusCode);
        Assert.True(await f.Db.입고상품.AnyAsync(x => x.창고Id == site.WarehouseId && x.가용수량 > 0));
        f.Clock.Now = current.ExpectedArrivalUtc;
        var reservation = wave.Reservations.First(x => x.SiteId == site.SiteId); var barcode = driverBefore.Sites.Single(x => x.SiteId == site.SiteId).Locations.Single().Barcode;
        await Error("DistributedConsentRequired", () => f.Use(Driver).PickAsync(Driver, wave.WaveId, new() { RequestId = Guid.NewGuid(), ExpectedRevision = wave.Revision,
            StopId = current.StopId, ReservationId = reservation.ReservationId, OwnerUserId = reservation.OwnerUserId, LocationBarcode = barcode, Sku = reservation.Sku, Quantity = 1, EvidenceReference = "withdrawn" }, default));
        Assert.All(await f.Db.Set<분산재고예약Record>().AsNoTracking().ToArrayAsync(), x => Assert.Equal(0, x.PickedQuantity));
    }
    [Fact]
    public async Task 독립context둘이같은잔량을읽어도두번째예약은조건부갱신으로초과예약할수없다()
    {
        await using var f = await Create(); var sites = await f.Sites(); var lot = (await f.Stock(sites)).First(x => x.SellerUserId == SellerA.UserId);
        await using var competingDb = f.IndependentContext();
        var observedA = await f.Db.입고상품.AsNoTracking().SingleAsync(x => x.Id == lot.InboundItemId);
        var observedB = await competingDb.입고상품.AsNoTracking().SingleAsync(x => x.Id == lot.InboundItemId);
        Assert.Equal(5, observedA.가용수량); Assert.Equal(observedA.가용수량, observedB.가용수량);
        var waves = new List<DistributedCommandResponse>();
        foreach (var suffix in new[] { "a", "b" })
        {
            await f.Cargo("competing-" + suffix, SellerA, CustomerA, quantity: 3);
            waves.Add(await f.Use(Operator).CreateWaveAsync(Operator, new() { RequestId = Guid.NewGuid(), Label = "경합 " + suffix,
                CutoffUtc = f.Clock.Now.AddMinutes(1), PickupStartUtc = f.Clock.Now.AddMinutes(2), PickupEndUtc = f.Clock.Now.AddHours(2),
                DeliveryStartUtc = f.Clock.Now.AddMinutes(2), DeliveryEndUtc = f.Clock.Now.AddHours(3) }, default));
        }
        var first = new DistributedOrderRequest { RequestId = Guid.NewGuid(), ExpectedRevision = 1, CargoRequestId = "competing-a", OrderReference = "경합 A", Lines = [new() { LotId = lot.LotId, Quantity = 3 }] };
        await f.Use(SellerA).OrderAsync(SellerA, waves[0].AggregateId, first, default);
        var second = new DistributedOrderRequest { RequestId = Guid.NewGuid(), ExpectedRevision = 1, CargoRequestId = "competing-b", OrderReference = "경합 B", Lines = [new() { LotId = lot.LotId, Quantity = 3 }] };
        await Error("DistributedStockInsufficientOrChanged", () => f.Use(SellerA, store: competingDb).OrderAsync(SellerA, waves[1].AggregateId, second, default));
        var actual = await competingDb.입고상품.AsNoTracking().SingleAsync(x => x.Id == lot.InboundItemId);
        Assert.Equal(2, actual.가용수량); Assert.Equal(3, actual.예약수량); Assert.Single(await competingDb.Set<분산재고예약Record>().ToArrayAsync());
        await Error("DistributedRevisionConflict", () => f.Use(SellerA, store: competingDb).OrderAsync(SellerA, waves[0].AggregateId, second, default));
    }
    [Theory]
    [InlineData("PublicLabel")]
    [InlineData("InboundLines")]
    public async Task 명시null입력은구조화된입력오류이며새원장을만들지않는다(string field)
    {
        await using var f = await Create();
        if (field == "PublicLabel") await Error("DistributedInputInvalid", () => f.Use(Operator).SiteAsync(Operator, new() { RequestId = Guid.NewGuid(), PublicLabel = null! }, default));
        else await Error("DistributedInboundLinesInvalid", () => f.Use(SellerA).InboundAsync(SellerA, new() { RequestId = Guid.NewGuid(), Lines = null! }, default));
        Assert.Empty(await f.Db.Set<분산거점Record>().ToArrayAsync()); Assert.Empty(await f.Db.Set<분산입고계획Record>().ToArrayAsync());
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 무피킹과일부수락복구에서도남은실제배정기사를자동교체하지않는다(bool partiallyAccepted)
    {
        await using var f = await Create(); f.FailCargoId = partiallyAccepted ? "cargo-b" : null;
        var wave = await f.Round();
        if (!partiallyAccepted)
        {
            await f.Use(Operator).CancelAsync(Operator, wave.WaveId, new() { RequestId = Guid.NewGuid(), ExpectedRevision = wave.Revision,
                OrderId = wave.Orders.First().OrderId, Reason = "미피킹 주문 취소" }, default);
            wave = await f.Use(Operator).WaveAsync(Operator, wave.WaveId, "warehouse", default);
        }
        else Assert.Equal("AcceptancePending", wave.StatusCode);
        var originalRevision = wave.Revision; var originalStops = wave.Stops.Select(x => x.StopId).ToArray();
        f.Db.용달기사.Add(new() { 기사Id = "other-driver", 기사명 = "다른 합성 기사", 차량 = "fixture-vehicle" });
        await f.Db.SaveChangesAsync(); f.Locations.Upsert(new("other-driver", 37, 127, 5, "대기", f.Clock.Now, f.Clock.Now));
        await Error("DistributedCargoAssignmentDriverLocked", () => f.Use(Operator).PlanAsync(Operator, wave.WaveId,
            new() { RequestId = Guid.NewGuid(), ExpectedRevision = originalRevision, DriverId = "other-driver" }, default));
        wave = await f.Use(Operator).WaveAsync(Operator, wave.WaveId, "warehouse", default);
        Assert.Equal(originalRevision, wave.Revision); Assert.Equal(originalStops, wave.Stops.Select(x => x.StopId)); Assert.Equal(Driver.UserId, wave.DriverId);
        Assert.All(await f.Db.운송원장.AsNoTracking().Where(x => x.배차큐단계 != 살뜰.도메인.공통.상태값.배차큐단계.종료 && x.확정기사Id != null).ToArrayAsync(), x => Assert.Equal(Driver.UserId, x.확정기사Id));
        f.FailCargoId = null; f.RefreshLocation();
        var planned = await f.Use(Operator).PlanAsync(Operator, wave.WaveId, new() { RequestId = Guid.NewGuid(), ExpectedRevision = wave.Revision, DriverId = Driver.UserId }, default);
        var accepted = await f.Use(Driver).AcceptAsync(Driver, wave.WaveId, new() { RequestId = Guid.NewGuid(), ExpectedRevision = planned.Revision }, default);
        Assert.Equal("Accepted", accepted.StatusCode);
    }
    [Fact]
    public async Task 접수중A와B에서A취소뒤에도마감전C주문을같은묶음으로받는다()
    {
        await using var f = await Create(); var sites = await f.Sites(); var lots = await f.Stock(sites);
        var wave = await f.Use(Operator).CreateWaveAsync(Operator, new() { RequestId = Guid.NewGuid(), Label = "접수 취소 복구",
            CutoffUtc = f.Clock.Now.AddMinutes(10), PickupStartUtc = f.Clock.Now.AddMinutes(11), PickupEndUtc = f.Clock.Now.AddHours(2),
            DeliveryStartUtc = f.Clock.Now.AddMinutes(11), DeliveryEndUtc = f.Clock.Now.AddHours(3) }, default);
        var revision = wave.Revision;
        foreach (var pair in new[] { (SellerA, CustomerA, "open-a"), (SellerB, CustomerB, "open-b") })
        {
            await f.Cargo(pair.Item3, pair.Item1, pair.Item2);
            revision = (await f.Use(pair.Item1).OrderAsync(pair.Item1, wave.AggregateId, new() { RequestId = Guid.NewGuid(), ExpectedRevision = revision,
                CargoRequestId = pair.Item3, OrderReference = pair.Item3, Lines = lots.Where(x => x.SellerUserId == pair.Item1.UserId).Select(x => new DistributedOrderLineRequest { LotId = x.LotId, Quantity = 1 }).ToArray() }, default)).Revision;
        }
        var current = await f.Use(Operator).WaveAsync(Operator, wave.AggregateId, "warehouse", default);
        var cancelled = current.Orders.Single(x => x.CargoRequestId == "open-a");
        revision = (await f.Use(SellerA).CancelAsync(SellerA, wave.AggregateId, new() { RequestId = Guid.NewGuid(), ExpectedRevision = revision, OrderId = cancelled.OrderId, Reason = "접수중 본인 취소" }, default)).Revision;
        current = await f.Use(SellerA).WaveAsync(SellerA, wave.AggregateId, "shipper", default); Assert.Equal("Open", current.StatusCode); Assert.Contains("add-order", current.AllowedActions);
        await f.Cargo("open-c", SellerA, CustomerA);
        await f.Use(SellerA).OrderAsync(SellerA, wave.AggregateId, new() { RequestId = Guid.NewGuid(), ExpectedRevision = revision, CargoRequestId = "open-c", OrderReference = "접수 C",
            Lines = lots.Where(x => x.SellerUserId == SellerA.UserId).Select(x => new DistributedOrderLineRequest { LotId = x.LotId, Quantity = 1 }).ToArray() }, default);
        current = await f.Use(Operator).WaveAsync(Operator, wave.AggregateId, "warehouse", default); Assert.Equal("Open", current.StatusCode);
        Assert.Equal(3, current.Orders.Count); Assert.Equal("Cancelled", current.Orders.Single(x => x.CargoRequestId == "open-a").StatusCode);
        Assert.All(current.Reservations.Where(x => x.OrderId == cancelled.OrderId), x => { Assert.Equal(x.Quantity, x.ReleasedQuantity); Assert.Equal(0, x.PickedQuantity); });
        Assert.All(await f.Db.입고상품.AsNoTracking().Where(x => x.입고수량 > 0).ToArrayAsync(), x => { Assert.Equal(4, x.가용수량); Assert.Equal(1, x.예약수량); });
    }
    [Fact]
    public async Task 실제차량경고코드와설명을재조회하고명시확인한새요청으로만수락한다()
    {
        await using var f = await Create(); f.RequireVehicleAdvisory = true; var wave = await f.Round(accept: false);
        // The real compatibility engine uses the cargo label in its warning text. Keep route, quantity and ownership facts unchanged.
        await f.Db.화주운송의뢰.Where(x => x.의뢰Id == "cargo-a").ExecuteUpdateAsync(s => s.SetProperty(x => x.화물종류, "기업 A 상온 물품"));
        await f.Db.화주운송의뢰.Where(x => x.의뢰Id == "cargo-b").ExecuteUpdateAsync(s => s.SetProperty(x => x.화물종류, "기업 B 상온 물품"));
        var request = new DistributedRoundAcceptRequest { RequestId = Guid.NewGuid(), ExpectedRevision = wave.Revision };
        var pending = await f.Use(Driver).AcceptAsync(Driver, wave.WaveId, request, default); Assert.Equal("AcceptancePending", pending.StatusCode);
        f.Db.ChangeTracker.Clear(); var visible = await f.Use(Driver).WaveAsync(Driver, wave.WaveId, "driver", default);
        Assert.Equal("FreightWarningAcknowledgementRequired", visible.AcceptanceErrorCode);
        Assert.NotEmpty(visible.AcceptanceWarnings); Assert.All(visible.AcceptanceWarnings, x => { Assert.Equal("FreightVehicleAdvisory", x.Code); Assert.Contains("포장 치수", x.Message); });
        Assert.All(await f.Db.운송원장.AsNoTracking().ToArrayAsync(), x => Assert.True(string.IsNullOrWhiteSpace(x.확정기사Id)));
        var privateCustomer = await f.Use(CustomerA).WaveAsync(CustomerA, wave.WaveId, "orderer", default);
        Assert.Empty(privateCustomer.AcceptanceWarnings); Assert.Null(privateCustomer.AcceptanceErrorCode); Assert.Null(privateCustomer.AcceptanceErrorMessage);
        await f.Export("driver", Driver, "warning-pending");
        await Error("DistributedRequestContentConflict", () => f.Use(Driver).AcceptAsync(Driver, wave.WaveId,
            new() { RequestId = request.RequestId, ExpectedRevision = request.ExpectedRevision, AcknowledgedWarningCodes = visible.AcceptanceWarnings.Select(x => x.Code).Distinct().ToArray() }, default));
        var firstWarning = Assert.Single(visible.AcceptanceWarnings);
        var next = await f.Use(Driver).AcceptAsync(Driver, wave.WaveId, new() { RequestId = Guid.NewGuid(), ExpectedRevision = visible.Revision,
            AcknowledgedWarningCodes = visible.AcceptanceWarnings.Select(x => x.Code).Distinct().ToArray() }, default);
        Assert.Equal("AcceptancePending", next.StatusCode);
        var newlyVisible = await f.Use(Driver).WaveAsync(Driver, wave.WaveId, "driver", default);
        var secondWarning = Assert.Single(newlyVisible.AcceptanceWarnings);
        Assert.Equal(firstWarning.Code, secondWarning.Code); Assert.NotEqual(firstWarning.CargoRequestId, secondWarning.CargoRequestId);
        Assert.NotEqual(firstWarning.Message, secondWarning.Message);
        var assignments = await f.Db.운송원장.AsNoTracking().ToArrayAsync(); Assert.Single(assignments, x => x.확정기사Id == Driver.UserId);
        Assert.True(string.IsNullOrWhiteSpace(assignments.Single(x => x.의뢰Id == secondWarning.CargoRequestId).확정기사Id));
        await f.Export("driver", Driver, "warning-next-cargo");
        var accepted = await f.Use(Driver).AcceptAsync(Driver, wave.WaveId, new() { RequestId = Guid.NewGuid(), ExpectedRevision = newlyVisible.Revision,
            AcknowledgedWarningCodes = newlyVisible.AcceptanceWarnings.Select(x => x.Code).Distinct().ToArray() }, default); Assert.Equal("Accepted", accepted.StatusCode);
        var evidence = (await f.Db.Set<분산배송WaveRecord>().AsNoTracking().SingleAsync()).RouteEvidenceJson;
        Assert.Contains("elapsedIncludingWaitAndServiceThenDistance", evidence); Assert.Contains("FreightVehicleAdvisory", evidence);
        using var recorded = System.Text.Json.JsonDocument.Parse(evidence);
        var warningText = recorded.RootElement.GetProperty("cargoAcceptanceHistory").EnumerateArray().First().GetProperty("results").EnumerateArray().First()
            .GetProperty("warnings").EnumerateArray().First().GetProperty("message").GetString();
        Assert.Contains("포장 치수", warningText);
        await f.Export("driver", Driver, "warning-accepted");
    }
}
