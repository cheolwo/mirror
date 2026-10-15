using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SkiaSharp;
using Ssalddel.Application.Settlement;
using Ssalddel.Application.Warehouse;
using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Services.Storage;
using 살뜰.Services.Options;
using 살뜰.도메인.정산;
using 살뜰.도메인.창고;
using C = Ssalddel.Contracts.Common.Settlement.NeighborhoodSettlementCodes;
using static Ssalddel.Tests.Application.Warehouse.DistributedTestFixture;

namespace Ssalddel.Tests.Application.Warehouse;

/// <summary>기존 원장과 사진 결속을 사용하는 격리 SQLite 회귀입니다. 실제 반출·지급·기기 실행 증거가 아닙니다.</summary>
public sealed class DistributedStockReleaseR76Tests
{
    private static readonly DistributedActor AssignedDriver = DistributedTestFixture.Driver;
    private static readonly DistributedActor GoodsOwner = new("goods-owner-a");
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task 실제양측만동의하며_수락한배송묶음없는지정기사에게도최소반출업무가보인다()
    {
        await using var f = await Create(); var (site, lot) = await Setup(f);
        var releaseId = await Propose(f, site, lot);
        var before = await f.Use(AssignedDriver).WorkspaceAsync(AssignedDriver, "driver", default);
        var waiting = Assert.Single(before.StockReleases);
        Assert.Empty(before.Sites); Assert.Empty(before.Lots); Assert.Empty(before.Waves);
        Assert.Null(waiting.LocationBarcode); Assert.Null(waiting.PickupInstructions); Assert.Empty(waiting.AllowedActions);
        foreach (var outsider in new[] { SellerA, new DistributedActor("manager"), new DistributedActor("admin", IsAdmin: true) })
            await Error("DistributedStockReleasePartyRequired", () => Consent(f, site, releaseId, outsider));
        await Consent(f, site, releaseId, Operator);
        Assert.Empty(Assert.Single((await f.Use(AssignedDriver).WorkspaceAsync(AssignedDriver, "driver", default)).StockReleases).AllowedActions);
        await Consent(f, site, releaseId, GoodsOwner);
        var ready = await f.Use(AssignedDriver).WorkspaceAsync(AssignedDriver, "driver", default);
        var assigned = Assert.Single(ready.StockReleases);
        Assert.Equal("Confirmed", assigned.StatusCode); Assert.Contains("handover-stock-release", assigned.AllowedActions);
        Assert.Equal(site.Locations.Single().Barcode, assigned.LocationBarcode); Assert.Equal("합성 출입 안내", assigned.PickupInstructions);
        Assert.Empty(ready.Sites); Assert.Empty((await f.Use(CustomerA).WorkspaceAsync(CustomerA, "orderer", default)).StockReleases);
        Assert.Empty((await f.Use(SellerA).WorkspaceAsync(SellerA, "shipper", default)).StockReleases);
        Assert.Empty((await f.Use(new("other-driver", true)).WorkspaceAsync(new("other-driver", true), "driver", default)).StockReleases);
    }

    [Fact]
    public async Task 사진과실수량으로부분반출한뒤재전송해도_재고와기존입고근거와보수원본을중복변경하지않는다()
    {
        await using var f = await Create(); var (site, lot) = await SetupWithStoredInboundPhoto(f);
        await ExistingAccrual(f, lot);
        var moneyBefore = await Money(f); var lotBefore = JsonSerializer.Serialize(await Lot(f, lot.LotId), Json);
        var photosBefore = JsonSerializer.Serialize(await f.Db.Set<분산사진증빙Record>().AsNoTracking().OrderBy(x => x.EvidenceId).ToArrayAsync(), Json);
        var inboundBefore = JsonSerializer.Serialize(await f.Db.Set<분산물류CommandReceipt>().AsNoTracking().Where(x => x.Kind == "Receive" || x.Kind == "ReceiveBatch").OrderBy(x => x.RequestId).ToArrayAsync(), Json);
        var stockBefore = await f.Db.입고상품.AsNoTracking().SingleAsync(x => x.Id == lot.InboundItemId);
        var releaseId = await Ready(f, site, lot);
        var request = await Proof(f, site, releaseId, 2);
        var result = await f.Use(AssignedDriver).StockReleaseHandoverAsync(AssignedDriver, site.SiteId, request, default);
        Assert.Equal("PartiallyReleased", result.StatusCode);
        var replay = await f.Use(AssignedDriver).StockReleaseHandoverAsync(AssignedDriver, site.SiteId, request, default);
        Assert.True(replay.Replayed); Assert.Equal(result.Revision, replay.Revision);
        await using var fresh = f.IndependentContext();
        var stock = await fresh.입고상품.AsNoTracking().SingleAsync(x => x.Id == lot.InboundItemId);
        Assert.Equal(stockBefore.가용수량 - 2, stock.가용수량); Assert.Equal(stockBefore.입고수량, stock.입고수량);
        Assert.Equal(lotBefore, JsonSerializer.Serialize(await Lot(f, lot.LotId), Json));
        Assert.Equal(inboundBefore, JsonSerializer.Serialize(await f.Db.Set<분산물류CommandReceipt>().AsNoTracking().Where(x => x.Kind == "Receive" || x.Kind == "ReceiveBatch").OrderBy(x => x.RequestId).ToArrayAsync(), Json));
        Assert.Equal(moneyBefore, await Money(f));
        Assert.Equal(photosBefore, JsonSerializer.Serialize(await f.Db.Set<분산사진증빙Record>().AsNoTracking().OrderBy(x => x.EvidenceId).ToArrayAsync(), Json));
        var receipt = Assert.Single(await f.Db.Set<분산물류CommandReceipt>().AsNoTracking().Where(x => x.Kind == "StockReleaseHandover").ToArrayAsync());
        var line = Assert.Single(JsonSerializer.Deserialize<분산물류완료근거>(receipt.CompletionLinesJson!, Json)!.Lines);
        Assert.Equal(2, line.Quantity); Assert.Equal("StockRelease", line.WorkKind); Assert.Equal(request.PhotoEvidenceIds, line.PhotoEvidenceIds);
        var second = await Proof(f, site, releaseId, 3);
        Assert.Equal("Completed", (await f.Use(AssignedDriver).StockReleaseHandoverAsync(AssignedDriver, site.SiteId, second, default)).StatusCode);
        Assert.Equal(0, (await f.Db.입고상품.AsNoTracking().SingleAsync(x => x.Id == lot.InboundItemId)).가용수량);
        Assert.Equal(moneyBefore, await Money(f));
    }

    [Fact]
    public async Task 저장사진으로확인한반출은_해당Lot보관기간만미확인으로보류하고_기입고보수와다른Lot은보존한다()
    {
        await using var f = await Create(); var (site, lot) = await SetupWithStoredInboundPhoto(f, revokeAfter: false);
        var (_, otherLot) = await SetupWithStoredInboundPhoto(f, site);
        await ExistingAccrual(f, lot); var moneyBefore = await Money(f);
        var resolver = new NeighborhoodSettlementSourceResolver(f.Db, f.Clock);
        var before = await resolver.ResolveAsync(C.DistributedLot, lot.LotId, default);
        var otherBefore = await resolver.ResolveAsync(C.DistributedLot, otherLot.LotId, default);
        Assert.NotEmpty(before.StorageIntervals); Assert.NotEmpty(before.StorageQuantityIntervals);
        Assert.NotEmpty(otherBefore.StorageIntervals); Assert.NotEmpty(otherBefore.StorageQuantityIntervals);
        var id = await Ready(f, site, lot); var request = await Request(f, site, id, 2);
        var photos = StoredPhotoService(f);
        request.PhotoEvidenceIds = [await UploadStoredPhoto(f, AssignedDriver, Work(site, request), photos)];
        await f.Use(AssignedDriver, binder: photos).StockReleaseHandoverAsync(AssignedDriver, site.SiteId, request, default);
        var after = await resolver.ResolveAsync(C.DistributedLot, lot.LotId, default);
        var otherAfter = await resolver.ResolveAsync(C.DistributedLot, otherLot.LotId, default);
        Assert.Empty(after.StorageIntervals); Assert.Empty(after.StorageQuantityIntervals);
        Assert.Equal(JsonSerializer.Serialize(before.CompletedWork, Json), JsonSerializer.Serialize(after.CompletedWork, Json));
        Assert.Equal(before.ActualCompletedUnits, after.ActualCompletedUnits);
        Assert.Equal(moneyBefore, await Money(f));
        Assert.Equal(JsonSerializer.Serialize(otherBefore.StorageIntervals, Json), JsonSerializer.Serialize(otherAfter.StorageIntervals, Json));
        Assert.Equal(JsonSerializer.Serialize(otherBefore.StorageQuantityIntervals, Json), JsonSerializer.Serialize(otherAfter.StorageQuantityIntervals, Json));
        Assert.Equal(JsonSerializer.Serialize(otherBefore.CompletedWork, Json), JsonSerializer.Serialize(otherAfter.CompletedWork, Json));
        var stored = await f.Db.Set<분산사진증빙Record>().AsNoTracking().SingleAsync(x => x.EvidenceId == request.PhotoEvidenceIds[0]);
        Assert.NotNull(stored.BoundAtUtc); Assert.Equal("StockRelease", stored.Kind);
        Assert.Equal(3, (await f.Db.입고상품.AsNoTracking().SingleAsync(x => x.Id == lot.InboundItemId)).가용수량);
        Assert.Equal(5, (await f.Db.입고상품.AsNoTracking().SingleAsync(x => x.Id == otherLot.InboundItemId)).가용수량);
    }

    [Fact]
    public async Task 사진없는완료와타담당자와틀린바코드와사진후수량변경을차단한다()
    {
        await using var f = await Create(); var (site, lot) = await Setup(f); var id = await Ready(f, site, lot);
        var raw = await Request(f, site, id, 2);
        await Error("DistributedPhotoEvidenceRequired", () => f.Use(AssignedDriver).StockReleaseHandoverAsync(AssignedDriver, site.SiteId, raw, default));
        await Error("DistributedStockReleaseAssigneeRequired", () => f.Use(Operator).StockReleaseHandoverAsync(Operator, site.SiteId, raw, default));
        var wrongBarcode = await Request(f, site, id, 2); wrongBarcode.LocationBarcode = "another-location";
        await Error("DistributedLocationMismatch", () => f.Use(AssignedDriver).AuthorizeEvidenceAsync(AssignedDriver, Work(site, wrongBarcode), default));
        var photographed = await Proof(f, site, id, 2); photographed.Quantity = 1;
        await Error("DistributedPhotoEvidenceBindingMismatch", () => f.Use(AssignedDriver).StockReleaseHandoverAsync(AssignedDriver, site.SiteId, photographed, default));
        Assert.Equal(5, (await f.Db.입고상품.AsNoTracking().SingleAsync(x => x.Id == lot.InboundItemId)).가용수량);
        Assert.Empty(await f.Db.Set<분산물류CommandReceipt>().Where(x => x.Kind == "StockReleaseHandover").ToArrayAsync());
    }

    [Fact]
    public async Task 사진뒤동의철회와다시동의는_예전사진을새판본완료에재사용하지못한다()
    {
        await using var f = await Create(); var (site, lot) = await Setup(f); var id = await Ready(f, site, lot);
        var photographed = await Proof(f, site, id, 1);
        await Consent(f, site, id, GoodsOwner, granted: false);
        await Error("DistributedRevisionConflict", () => f.Use(AssignedDriver).StockReleaseHandoverAsync(AssignedDriver, site.SiteId, photographed, default));
        photographed.ExpectedRevision = await Revision(f, site);
        await Error("DistributedStockReleaseConsentRequired", () => f.Use(AssignedDriver).StockReleaseHandoverAsync(AssignedDriver, site.SiteId, photographed, default));
        await Consent(f, site, id, GoodsOwner); photographed.ExpectedRevision = await Revision(f, site);
        await Error("DistributedPhotoEvidenceBindingMismatch", () => f.Use(AssignedDriver).StockReleaseHandoverAsync(AssignedDriver, site.SiteId, photographed, default));
    }

    [Theory]
    [InlineData("stock")]
    [InlineData("picked")]
    [InlineData("ledger")]
    public async Task 예약또는미전달피킹보관량이있으면_새반출과사진완료와공간종료를차단한다(string kind)
    {
        await using var f = await Create(); var (site, lot) = await Setup(f); var id = await Ready(f, site, lot);
        var request = await Proof(f, site, id, 1);
        if (kind != "stock")
        {
            f.Db.Add(new 분산배송WaveRecord { WaveId = "existing-wave", RoundId = "existing-round", CreatorUserId = Operator.UserId, StatusCode = "Accepted" });
            f.Db.Add(new 분산배송주문Record { OrderId = "existing-order", WaveId = "existing-wave", CargoRequestId = "existing-cargo", SellerUserId = SellerA.UserId, CustomerUserId = CustomerA.UserId });
            f.Db.Add(new 분산재고예약Record { ReservationId = "existing-reservation", WaveId = "existing-wave", OrderId = "existing-order", LotId = lot.LotId, Quantity = 1, PickedQuantity = kind == "picked" ? 1 : 0 });
        }
        else { var stock = await f.Db.입고상품.SingleAsync(x => x.Id == lot.InboundItemId); stock.예약수량 = 1; }
        await f.Db.SaveChangesAsync(); f.Db.ChangeTracker.Clear();
        var code = kind == "picked" ? "DistributedStockReleasePickedCustodyRemaining" : kind == "ledger" ? "DistributedStockReleaseReservationRemaining" : "DistributedStockReleaseReservedOrInsufficient";
        await Error(code, () => f.Use(AssignedDriver).StockReleaseHandoverAsync(AssignedDriver, site.SiteId, request, default));
        await Consent(f, site, id, GoodsOwner, granted: false, cancel: true);
        await Error(code, () => Propose(f, site, lot));
        await Error(kind == "stock" ? "DistributedSiteStockRemaining" : code, () => Close(f, site));
    }

    [Fact]
    public async Task 기존공간상세와알수없는루트봉투계획이력필드는_동의반출철회뒤에도보존한다()
    {
        await using var f = await Create(); var (site, lot) = await Setup(f); var id = await Propose(f, site, lot);
        var record = await f.Db.Set<분산거점Record>().SingleAsync(x => x.SiteId == site.SiteId);
        var root = JsonNode.Parse(record.PrivateDetailsJson)!.AsObject(); root["futureRoot"] = new JsonObject { ["keep"] = "root" };
        var envelope = root["stockRelease"]!.AsObject(); envelope["futureEnvelope"] = "envelope";
        var plan = envelope["plans"]![0]!.AsObject(); plan["futurePlan"] = new JsonArray("plan", 7);
        plan["history"]!.AsArray()[0]!.AsObject()["futureHistory"] = "history";
        record.PrivateDetailsJson = root.ToJsonString(Json); await f.Db.SaveChangesAsync(); f.Db.ChangeTracker.Clear();
        await Consent(f, site, id, Operator); await Consent(f, site, id, GoodsOwner);
        await f.Use(AssignedDriver).StockReleaseHandoverAsync(AssignedDriver, site.SiteId, await Proof(f, site, id, 1), default);
        await f.Use(Operator).ConsentAsync(Operator, site.SiteId, new() { RequestId = Guid.NewGuid(), ExpectedRevision = await Revision(f, site), ConsentKind = "Owner", Granted = false, EvidenceReference = "철회 근거 재확인" }, default);
        await f.Use(Operator).WorkspaceAsync(Operator, "warehouse", default);
        var after = JsonNode.Parse((await Site(f, site)).PrivateDetailsJson)!;
        Assert.Equal("root", after["futureRoot"]!["keep"]!.GetValue<string>());
        Assert.Equal("envelope", after["stockRelease"]!["futureEnvelope"]!.GetValue<string>());
        Assert.Equal("[\"plan\",7]", after["stockRelease"]!["plans"]![0]!["futurePlan"]!.ToJsonString());
        Assert.Equal("history", after["stockRelease"]!["plans"]![0]!["history"]![0]!["futureHistory"]!.GetValue<string>());
        Assert.Equal("합성 출입 안내", after["pickupInstructions"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("\"1\"")]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("2")]
    public async Task 오염되거나지원하지않는판본은_빈반출성공대신known503으로보류한다(string schema)
    {
        await using var f = await Create(); var site = (await f.Sites())[0];
        var record = await f.Db.Set<분산거점Record>().SingleAsync(x => x.SiteId == site.SiteId);
        var root = JsonNode.Parse(record.PrivateDetailsJson)!.AsObject();
        root["stockRelease"] = new JsonObject { ["schemaVersion"] = JsonNode.Parse(schema), ["plans"] = new JsonArray() };
        record.PrivateDetailsJson = root.ToJsonString(Json); await f.Db.SaveChangesAsync(); f.Db.ChangeTracker.Clear();
        var error = await Assert.ThrowsAsync<DistributedFulfillmentException>(() => f.Use(Operator).WorkspaceAsync(Operator, "warehouse", default));
        Assert.Equal("DistributedStockReleaseMetadataInvalid", error.Code); Assert.Equal(503, error.Status);
    }

    [Fact]
    public async Task 계획배열의null행도_원본을보존하고known503으로보류한다()
    {
        await using var f = await Create(); var site = (await f.Sites())[0];
        var record = await f.Db.Set<분산거점Record>().SingleAsync(x => x.SiteId == site.SiteId);
        var root = JsonNode.Parse(record.PrivateDetailsJson)!.AsObject();
        root["stockRelease"] = JsonNode.Parse("{\"schemaVersion\":1,\"plans\":[null]}");
        record.PrivateDetailsJson = root.ToJsonString(Json); await f.Db.SaveChangesAsync(); f.Db.ChangeTracker.Clear();
        var before = (await Site(f, site)).PrivateDetailsJson;
        var error = await Assert.ThrowsAsync<DistributedFulfillmentException>(() => f.Use(Operator).WorkspaceAsync(Operator, "warehouse", default));
        Assert.Equal("DistributedStockReleaseMetadataInvalid", error.Code); Assert.Equal(503, error.Status);
        Assert.Equal(before, (await Site(f, site)).PrivateDetailsJson);
    }

    [Fact]
    public async Task 이백개계획다음제안은추가저장전에차단하여기존기록을읽을수있게유지한다()
    {
        await using var f = await Create(); var (site, lot) = await Setup(f); await Propose(f, site, lot);
        var record = await f.Db.Set<분산거점Record>().SingleAsync(x => x.SiteId == site.SiteId);
        var root = JsonNode.Parse(record.PrivateDetailsJson)!.AsObject(); var plans = root["stockRelease"]!["plans"]!.AsArray(); var template = plans[0]!.DeepClone(); plans.Clear();
        for (var i = 0; i < 200; i++) { var item = template.DeepClone(); item["releaseId"] = "historical-" + i; item["statusCode"] = "Cancelled"; plans.Add(item); }
        record.PrivateDetailsJson = root.ToJsonString(Json); await f.Db.SaveChangesAsync(); f.Db.ChangeTracker.Clear();
        var before = (await Site(f, site)).PrivateDetailsJson;
        await Error("DistributedStockReleaseHistoryLimitReached", () => Propose(f, site, lot));
        Assert.Equal(before, (await Site(f, site)).PrivateDetailsJson);
        Assert.Equal(200, (await f.Use(Operator).WorkspaceAsync(Operator, "warehouse", default)).StockReleases.Count);
    }

    [Fact]
    public async Task 이력이상한에도기존합의의실물인계와철회취소는_옛이력을지우지않고마무리한다()
    {
        await using var f = await Create(); var (site, lot) = await Setup(f); var id = await Ready(f, site, lot);
        var record = await f.Db.Set<분산거점Record>().SingleAsync(x => x.SiteId == site.SiteId);
        var root = JsonNode.Parse(record.PrivateDetailsJson)!.AsObject(); var history = root["stockRelease"]!["plans"]![0]!["history"]!.AsArray(); history.Clear();
        for (var i = 0; i < 500; i++) history.Add(new JsonObject { ["action"] = "Historical", ["keep"] = i });
        var original = history.ToJsonString(); record.PrivateDetailsJson = root.ToJsonString(Json);
        await f.Db.SaveChangesAsync(); f.Db.ChangeTracker.Clear();
        await f.Use(AssignedDriver).StockReleaseHandoverAsync(AssignedDriver, site.SiteId, await Proof(f, site, id, 1), default);
        await Consent(f, site, id, GoodsOwner, granted: false);
        await Error("DistributedStockReleaseHistoryLimitReached", () => Consent(f, site, id, GoodsOwner));
        await Consent(f, site, id, GoodsOwner, granted: false, cancel: true);
        var after = JsonNode.Parse((await Site(f, site)).PrivateDetailsJson)!["stockRelease"]!["plans"]![0]!;
        var afterHistory = after["history"]!.AsArray(); Assert.Equal(503, afterHistory.Count);
        var preserved = new JsonArray(afterHistory.Take(500).Select(x => x!.DeepClone()).ToArray()); Assert.Equal(original, preserved.ToJsonString());
        Assert.Equal("Cancelled", after["statusCode"]!.GetValue<string>()); Assert.Equal(1, after["releasedQuantity"]!.GetValue<int>());
        Assert.Equal(4, (await f.Db.입고상품.AsNoTracking().SingleAsync(x => x.Id == lot.InboundItemId)).가용수량);
    }

    [Fact]
    public async Task 전체실물반출뒤공간소유자가별도로종료하며_종료후재동의나새사진입고를허용하지않는다()
    {
        await using var f = await Create(); var (site, _) = await Setup(f);
        await Error("DistributedSiteStockRemaining", () => Close(f, site));
        foreach (var lot in await f.Db.Set<분산재고LotRecord>().AsNoTracking().Where(x => x.SiteId == site.SiteId).ToArrayAsync())
        { var id = await Ready(f, site, lot); await f.Use(AssignedDriver).StockReleaseHandoverAsync(AssignedDriver, site.SiteId, await Proof(f, site, id, lot.ReceivedQuantity), default); }
        await Error("DistributedSpaceOwnerRequired", () => Close(f, site, GoodsOwner));
        var response = await Close(f, site); Assert.Equal("Closed", response.StatusCode);
        Assert.Empty((await f.Use(Operator).WorkspaceAsync(Operator, "warehouse", default)).Sites.Single(x => x.SiteId == site.SiteId).AllowedActions);
        await Error("DistributedSiteClosed", () => f.Use(Operator).ConsentAsync(Operator, site.SiteId, new() { RequestId = Guid.NewGuid(), ExpectedRevision = response.Revision, ConsentKind = "Owner", Granted = true, EvidenceReference = "재동의 시도" }, default));
        Assert.Equal("Closed", (await Site(f, site)).StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 미연결불량재고나미수령입고가있으면_종료를보류하고원래입고수량은변경하지않는다(bool pendingInbound)
    {
        await using var f = await Create(); var site = (await f.Sites())[0];
        if (pendingInbound)
        {
            await f.EnsureAgreement(site, GoodsOwner, "same-sku");
            await f.Use(SellerA).InboundAsync(SellerA, new() { RequestId = Guid.NewGuid(), SourceReference = "미수령 계획", Lines = [new() { LineKey = "not-received", SourceInboundId = 101, SiteId = site.SiteId, LocationId = site.Locations.Single().LocationId, Sku = "same-sku", ProductName = "미수령 상품", LotCode = "pending", ExpectedQuantity = 2 }] }, default);
        }
        else f.Db.입고상품.Add(new() { 창고Id = site.WarehouseId, 입고요청Id = 101, 소유자UserId = GoodsOwner.UserId, 판매자UserId = SellerA.UserId, SKU = "unknown-stock", 상품명 = "미연결 불량 물품", 불량수량 = 1 });
        await f.Db.SaveChangesAsync(); f.Db.ChangeTracker.Clear(); await Revoke(f, site);
        await Error(pendingInbound ? "DistributedSiteInboundPending" : "DistributedSiteStockRemaining", () => Close(f, site));
        Assert.NotEqual("Closed", (await Site(f, site)).StatusCode);
        if (pendingInbound) { var lot = Assert.Single(await f.Db.Set<분산재고LotRecord>().AsNoTracking().ToArrayAsync()); Assert.Equal(2, lot.ExpectedQuantity); Assert.Equal(0, lot.ReceivedQuantity); }
    }

    private static async Task<(DistributedSiteDto Site, 분산재고LotRecord Lot)> Setup(DistributedTestFixture f)
    { var sites = await f.Sites(); await f.Stock(sites); var site = sites[0]; await Revoke(f, site); return (site, await f.Db.Set<분산재고LotRecord>().AsNoTracking().SingleAsync(x => x.SiteId == site.SiteId && x.OwnerUserId == GoodsOwner.UserId)); }
    private static async Task<(DistributedSiteDto Site, 분산재고LotRecord Lot)> SetupWithStoredInboundPhoto(DistributedTestFixture f,
        DistributedSiteDto? existingSite = null, bool revokeAfter = true)
    {
        // 금전 원천은 메모리 binder만으로 만들지 않습니다. 실제 저장·결속된 격리 사진 원장을 준비합니다.
        var site = existingSite ?? (await f.Sites())[0]; var location = site.Locations.Single();
        await f.EnsureAgreement(site, GoodsOwner, "same-sku");
        var plan = await f.Use(SellerA).InboundAsync(SellerA, new()
        {
            RequestId = Guid.NewGuid(), SourceReference = "보수 원본 보존용 격리 입고",
            Lines = [new() { LineKey = "inbound-a", SourceInboundId = 101, SiteId = site.SiteId, LocationId = location.LocationId,
                Sku = "same-sku", ProductName = "합성 공산품", LotCode = "real-evidence-fixture", ExpectedQuantity = 5 }]
        }, default);
        var photos = StoredPhotoService(f);
        var receipt = new DistributedInboundReceiptBatchRequest { RequestId = Guid.NewGuid(), ExpectedRevision = plan.Revision,
            LocationBarcode = location.Barcode, Lines = [new() { LineKey = "inbound-a", Sku = "same-sku", Quantity = 5 }] };
        var work = new DistributedEvidenceWorkRequest { Kind = "Receive", AggregateId = plan.AggregateId, Receive = receipt };
        receipt.PhotoEvidenceIds = [await UploadStoredPhoto(f, Operator, work, photos)];
        await f.Use(Operator, binder: photos).ReceiveBatchAsync(Operator, plan.AggregateId, receipt, default);
        var lot = await f.Db.Set<분산재고LotRecord>().AsNoTracking().SingleAsync(x => x.PlanId == plan.AggregateId);
        if (revokeAfter) await Revoke(f, site); return (site, lot);
    }
    private static DistributedPhotoEvidenceService StoredPhotoService(DistributedTestFixture f)
        => new(f.Db, new DevelopmentLocalStorageService(new PhotoEnvironment(), new HttpContextAccessor()),
            new SsalddelExecutionModePolicy(Options.Create(new SsalddelExecutionOptions { Mode = SsalddelExecutionMode.Simulation })),
            Options.Create(new DistributedFulfillmentOptions { Enabled = true, Profile = DistributedFulfillmentCodes.Profile }), f.Clock);
    private static async Task<string> UploadStoredPhoto(DistributedTestFixture f, DistributedActor actor,
        DistributedEvidenceWorkRequest work, DistributedPhotoEvidenceService photos)
    {
        using var bitmap = new SKBitmap(16, 16); bitmap.Erase(SKColors.ForestGreen);
        using var image = SKImage.FromBitmap(bitmap); using var bytes = image.Encode(SKEncodedImageFormat.Jpeg, 85);
        var binding = await f.Use(actor).AuthorizeEvidenceAsync(actor, work, default);
        return (await photos.UploadAsync(actor, binding, new() { UploadRequestId = Guid.NewGuid(), Work = work,
            ContentType = "image/jpeg", ImageBase64 = Convert.ToBase64String(bytes.ToArray()) }, default)).EvidenceId;
    }
    private static Task<분산거점Record> Site(DistributedTestFixture f, DistributedSiteDto site) => f.Db.Set<분산거점Record>().AsNoTracking().SingleAsync(x => x.SiteId == site.SiteId);
    private static Task<분산재고LotRecord> Lot(DistributedTestFixture f, string id) => f.Db.Set<분산재고LotRecord>().AsNoTracking().SingleAsync(x => x.LotId == id);
    private static async Task<long> Revision(DistributedTestFixture f, DistributedSiteDto site) => (await Site(f, site)).Revision;
    private static async Task Revoke(DistributedTestFixture f, DistributedSiteDto site) => await f.Use(Operator).ConsentAsync(Operator, site.SiteId, new() { RequestId = Guid.NewGuid(), ExpectedRevision = await Revision(f, site), ConsentKind = "Owner", Granted = false, EvidenceReference = "합성 공간 철회" }, default);
    private static async Task<string> Propose(DistributedTestFixture f, DistributedSiteDto site, 분산재고LotRecord lot)
    { var id = Guid.NewGuid(); await f.Use(Operator).StockReleaseProposalAsync(Operator, site.SiteId, new() { RequestId = id, ExpectedRevision = await Revision(f, site), LotId = lot.LotId, AssignedUserId = AssignedDriver.UserId, Quantity = lot.ReceivedQuantity, HandoverTerms = "소유자가 지정한 담당자에게 현장 인계", Reason = "공간 사용 종료 협의" }, default); return "release-" + id.ToString("N"); }
    private static async Task<string> Ready(DistributedTestFixture f, DistributedSiteDto site, 분산재고LotRecord lot)
    { var id = await Propose(f, site, lot); await Consent(f, site, id, Operator); await Consent(f, site, id, new(lot.OwnerUserId)); return id; }
    private static async Task<DistributedCommandResponse> Consent(DistributedTestFixture f, DistributedSiteDto site, string id, DistributedActor actor, bool granted = true, bool cancel = false)
        => await f.Use(actor).StockReleaseConsentAsync(actor, site.SiteId, new() { RequestId = Guid.NewGuid(), ExpectedRevision = await Revision(f, site), ReleaseId = id, Granted = granted, CancelRemaining = cancel }, default);
    private static async Task<DistributedStockReleaseHandoverRequest> Request(DistributedTestFixture f, DistributedSiteDto site, string id, int quantity)
        => new() { RequestId = Guid.NewGuid(), ExpectedRevision = await Revision(f, site), ReleaseId = id, Quantity = quantity, LocationBarcode = site.Locations.Single().Barcode };
    private static DistributedEvidenceWorkRequest Work(DistributedSiteDto site, DistributedStockReleaseHandoverRequest request) => new() { Kind = "StockRelease", AggregateId = site.SiteId, StockRelease = request };
    private static async Task<DistributedStockReleaseHandoverRequest> Proof(DistributedTestFixture f, DistributedSiteDto site, string id, int quantity)
    { var request = await Request(f, site, id, quantity); request.PhotoEvidenceIds = f.Photos.Register(await f.Use(AssignedDriver).AuthorizeEvidenceAsync(AssignedDriver, Work(site, request), default)); return request; }
    private static async Task<DistributedCommandResponse> Close(DistributedTestFixture f, DistributedSiteDto site, DistributedActor? actor = null)
    { actor ??= Operator; return await f.Use(actor).CloseSiteAsync(actor, site.SiteId, new() { RequestId = Guid.NewGuid(), ExpectedRevision = await Revision(f, site), NoRemainingCustodyConfirmed = true, EvidenceReference = "합성 현장 잔량 확인" }, default); }
    private static async Task Error(string code, Func<Task> action) => Assert.Equal(code, (await Assert.ThrowsAsync<DistributedFulfillmentException>(action)).Code);

    private static async Task ExistingAccrual(DistributedTestFixture f, 분산재고LotRecord lot)
    {
        var resolver = new NeighborhoodSettlementSourceResolver(f.Db, f.Clock);
        var source = await resolver.ResolveAsync(C.DistributedLot, lot.LotId, default); var work = Assert.Single(source.CompletedWork, x => x.Kind == C.Inbound);
        var execution = new SsalddelExecutionModePolicy(Options.Create(new SsalddelExecutionOptions { Mode = SsalddelExecutionMode.Simulation }));
        var service = new NeighborhoodSettlementUseCase(f.Db, resolver, execution, Options.Create(new NeighborhoodSettlementOptions { Enabled = true, Profile = NeighborhoodSettlementOptions.RequiredProfile }), f.Clock);
        var terms = await service.CreateAsync(GoodsOwner.UserId, new() { RequestId = Guid.NewGuid(), SourceKind = C.DistributedLot, SourceId = lot.LotId, ActivityKind = C.StoredGoods,
            Lines = [new() { LineKey = "inbound", Kind = C.Inbound, Billing = C.PerUnit, Quantity = 5, UnitPriceKrw = 100, PayerUserId = GoodsOwner.UserId, PayeeUserId = Operator.UserId, BeneficiaryRole = "Receiver" }] }, default);
        foreach (var actor in new[] { GoodsOwner, Operator }) terms = await service.ConsentAsync(terms.AgreementId, actor.UserId, new() { RequestId = Guid.NewGuid(), ExpectedRevision = terms.Revision, TermsVersion = 1, Granted = true }, default);
        await service.AccrueAsync(terms.AgreementId, GoodsOwner.UserId, new() { RequestId = Guid.NewGuid(), ExpectedRevision = terms.Revision, OriginKind = work.OriginKind, OriginId = work.OriginId, Lines = [new() { LineKey = "inbound", Quantity = 5 }] }, default);
        var entry = await f.Db.Set<NeighborhoodSettlementEntryRecord>().AsNoTracking().SingleAsync();
        // 기수행 보수와 별개인 기존 지급 보고·주문별 원가 귀속 원본도 그대로 남아야 합니다.
        f.Db.Add(new NeighborhoodSettlementPaymentRecord { PaymentId = "existing-report", AgreementId = terms.AgreementId, DetailsJson = "{\"sentinel\":\"reported-payment\"}", ReportedAtUtc = f.Clock.Now });
        f.Db.Add(new NeighborhoodInventoryCostAttributionRecord { AttributionId = "existing-attribution", SourceEntryId = entry.EntryId, SourceAgreementId = terms.AgreementId, AgreementId = "existing-order-agreement", OrderId = "existing-order", CalculationDigest = "existing-digest", DetailsJson = "{\"sentinel\":\"original-cost\"}", RecordedAtUtc = f.Clock.Now });
        await f.Db.SaveChangesAsync(); f.Db.ChangeTracker.Clear();
    }
    private static async Task<string> Money(DistributedTestFixture f) => JsonSerializer.Serialize(new
    {
        Agreements = await f.Db.Set<NeighborhoodSettlementAgreementRecord>().AsNoTracking().OrderBy(x => x.AgreementId).ToArrayAsync(),
        Versions = await f.Db.Set<NeighborhoodSettlementAgreementVersionRecord>().AsNoTracking().OrderBy(x => x.AgreementId).ThenBy(x => x.TermsVersion).ToArrayAsync(),
        Entries = await f.Db.Set<NeighborhoodSettlementEntryRecord>().AsNoTracking().OrderBy(x => x.EntryId).ToArrayAsync(),
        Payments = await f.Db.Set<NeighborhoodSettlementPaymentRecord>().AsNoTracking().OrderBy(x => x.PaymentId).ToArrayAsync(),
        Receipts = await f.Db.Set<NeighborhoodSettlementCommandReceiptRecord>().AsNoTracking().OrderBy(x => x.RequestId).ToArrayAsync(),
        Attributions = await f.Db.Set<NeighborhoodInventoryCostAttributionRecord>().AsNoTracking().OrderBy(x => x.AttributionId).ToArrayAsync()
    }, Json);
    private sealed class PhotoEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Ssalddel.Tests";
        public string ContentRootPath { get; set; } = Path.GetFullPath(Path.Combine("artifacts", "local", "stock-release-r76", Guid.NewGuid().ToString("N")));
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
