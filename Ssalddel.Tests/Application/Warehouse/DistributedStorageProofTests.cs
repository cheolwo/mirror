using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.Warehouse;
using Ssalddel.Contracts.Common.Warehouse;
using 살뜰.도메인.창고;
using static Ssalddel.Tests.Application.Warehouse.DistributedTestFixture;

namespace Ssalddel.Tests.Application.Warehouse;

public sealed class DistributedStorageProofTests
{
    [Fact]
    public async Task 합의전입고계획은가능하지만서로같은조건판본에동의해야실제입고된다()
    {
        await using var f = await Create(); var sites = await f.Sites(); var site = sites[0]; var plan = await Plan(f, site);
        var request = Receive(f, site, plan, 2);
        await Error("DistributedStorageAgreementRequired", () => f.Use(Operator).ReceiveBatchAsync(Operator, plan.AggregateId, request, default));
        var draft = await f.Use(SellerA).AgreementAsync(SellerA, site.SiteId, Agreement(f, site), default);
        await Error("DistributedAgreementPartyRequired", () => f.Use(SellerA).AgreementConsentAsync(SellerA, draft.AggregateId, new() { RequestId = Guid.NewGuid(), ExpectedRevision = draft.Revision, TermsVersion = 1, Granted = true }, default));
        draft = await f.Use(Operator).AgreementConsentAsync(Operator, draft.AggregateId, new() { RequestId = Guid.NewGuid(), ExpectedRevision = draft.Revision, TermsVersion = 1, Granted = true }, default);
        await f.Export("warehouse", Operator, "agreement-pending");
        await Error("DistributedStorageAgreementRequired", () => f.Use(Operator).ReceiveBatchAsync(Operator, plan.AggregateId, request, default));
        var owner = new DistributedActor("goods-owner-a");
        await f.Use(owner).AgreementConsentAsync(owner, draft.AggregateId, new() { RequestId = Guid.NewGuid(), ExpectedRevision = draft.Revision, TermsVersion = 1, Granted = true }, default);
        await f.Proof(Operator, plan.AggregateId, request);
        await f.Use(Operator).ReceiveBatchAsync(Operator, plan.AggregateId, request, default);
        await f.Export("warehouse", Operator, "agreement-active");
        var driverScope = await f.Use(DistributedTestFixture.Driver).WorkspaceAsync(DistributedTestFixture.Driver, "driver", default);
        Assert.Empty(driverScope.StorageAgreements); Assert.Empty(driverScope.GoodsOwnerCandidates);
        var customerScope = await f.Use(owner).WorkspaceAsync(owner, "orderer", default);
        Assert.Empty(customerScope.StorageAgreements); Assert.Empty(customerScope.GoodsOwnerCandidates);
        var lot = await f.Db.Set<분산재고LotRecord>().AsNoTracking().SingleAsync(); Assert.Equal(2, lot.ReceivedQuantity);
        Assert.Contains("\"termsVersion\":1", lot.StorageAgreementSnapshotJson); Assert.Contains("goods-owner-a", lot.StorageAgreementSnapshotJson);
        var agreement = Assert.Single((await f.Use(owner).WorkspaceAsync(owner, "shipper", default)).StorageAgreements);
        var terms = Agreement(f, site); terms.ExpectedRevision = agreement.Revision; terms.Terms.PriceOrFreeTerms = "new mutually reviewed price";
        var changed = await f.Use(owner).AgreementTermsAsync(owner, agreement.AgreementId, terms, default);
        request = Receive(f, site, new(plan.RequestId, plan.AggregateId, 2, "PartiallyReceived", false), 1);
        await Error("DistributedStorageAgreementRequired", () => f.Use(Operator).ReceiveBatchAsync(Operator, plan.AggregateId, request, default));
        await Error("DistributedAgreementTermsVersionMismatch", () => f.Use(Operator).AgreementConsentAsync(Operator, agreement.AgreementId, new() { RequestId = Guid.NewGuid(), ExpectedRevision = changed.Revision, TermsVersion = 1, Granted = true }, default));
        Assert.Equal(lot.StorageAgreementSnapshotJson, (await f.Db.Set<분산재고LotRecord>().AsNoTracking().SingleAsync()).StorageAgreementSnapshotJson);
        Assert.DoesNotContain("new mutually", lot.StorageAgreementSnapshotJson);
    }
    [Theory]
    [InlineData("missing")]
    [InlineData("foreign")]
    [InlineData("changed-quantity")]
    [InlineData("changed-request")]
    public async Task 사진없음외인사진다른수량과다른완료요청사진은수량을전혀바꾸지않는다(string field)
    {
        await using var f = await Create(); var site = (await f.Sites())[0]; var plan = await Plan(f, site);
        await f.EnsureAgreement(site, new("goods-owner-a"), "same-sku"); var request = Receive(f, site, plan, 2);
        if (field != "missing") await f.Proof(Operator, plan.AggregateId, request);
        var expected = "DistributedPhotoEvidenceRequired";
        if (field == "foreign")
        {
            var manager = new DistributedActor("test-manager"); var record = await f.Db.Set<분산거점Record>().SingleAsync(x => x.SiteId == site.SiteId); record.ManagerUserId = manager.UserId; await f.Db.SaveChangesAsync();
            var altered = Receive(f, site, plan, 2); altered.RequestId = request.RequestId; await f.Proof(manager, plan.AggregateId, altered);
            request.PhotoEvidenceIds = altered.PhotoEvidenceIds; expected = "DistributedPhotoEvidenceActorMismatch";
        }
        if (field == "changed-quantity") { request.Lines[0].Quantity = 3; expected = "DistributedPhotoEvidenceBindingMismatch"; }
        if (field == "changed-request") { request.RequestId = Guid.NewGuid(); expected = "DistributedPhotoEvidenceBindingMismatch"; }
        await Error(expected, () => f.Use(Operator).ReceiveBatchAsync(Operator, plan.AggregateId, request, default));
        Assert.Equal(0, (await f.Db.Set<분산재고LotRecord>().AsNoTracking().SingleAsync()).ReceivedQuantity);
        Assert.Equal(0, await f.Db.입고상품.SumAsync(x => x.가용수량)); Assert.Empty(await f.Db.Set<분산물류CommandReceipt>().Where(x => x.Kind == "ReceiveBatch").ToArrayAsync());
    }
    [Fact]
    public async Task 같은적재위치묶음부분입고는한번반영하고마지막줄재고충돌시전체롤백된다()
    {
        await using var f = await Create(); var site = (await f.Sites())[0]; await f.EnsureAgreement(site, new("goods-owner-a"), "same-sku");
        var plan = await f.Use(SellerA).InboundAsync(SellerA, new() { RequestId = Guid.NewGuid(), SourceReference = "two lines",
            Lines = new[] { "a", "b" }.Select(key => new DistributedInboundLineRequest { LineKey = key, SourceInboundId = 101, SiteId = site.SiteId, LocationId = site.Locations.Single().LocationId, Sku = "same-sku", ProductName = "stock", LotCode = key, ExpectedQuantity = 5 }).ToArray() }, default);
        var request = new DistributedInboundReceiptBatchRequest { RequestId = Guid.NewGuid(), ExpectedRevision = plan.Revision, LocationBarcode = site.Locations.Single().Barcode,
            Lines = [new() { LineKey = "a", Sku = "same-sku", Quantity = 2 }, new() { LineKey = "b", Sku = "same-sku", Quantity = 3 }] };
        await f.Proof(Operator, plan.AggregateId, request);
        var second = await f.Db.Set<분산재고LotRecord>().AsNoTracking().SingleAsync(x => x.LineKey == "b");
        await f.Db.입고상품.Where(x => x.Id == second.InboundItemId).ExecuteUpdateAsync(s => s.SetProperty(x => x.소유자UserId, "changed-owner"));
        await Error("DistributedInventoryConflict", () => f.Use(Operator).ReceiveBatchAsync(Operator, plan.AggregateId, request, default));
        Assert.All(await f.Db.Set<분산재고LotRecord>().AsNoTracking().ToArrayAsync(), x => Assert.Equal(0, x.ReceivedQuantity)); Assert.Equal(0, await f.Db.입고상품.SumAsync(x => x.가용수량));
        await f.Db.입고상품.Where(x => x.Id == second.InboundItemId).ExecuteUpdateAsync(s => s.SetProperty(x => x.소유자UserId, "goods-owner-a"));
        var ack = await f.Use(Operator).ReceiveBatchAsync(Operator, plan.AggregateId, request, default);
        Assert.True((await f.Use(Operator).ReceiveBatchAsync(Operator, plan.AggregateId, request, default)).Replayed);
        Assert.Equal(2, ack.Revision); Assert.Equal(5, await f.Db.Set<분산재고LotRecord>().SumAsync(x => x.ReceivedQuantity));
        Assert.Equal(5, (await f.Use(Operator).ReceiptAsync(Operator, request.RequestId, default)).Quantity);
    }
    [Fact]
    public async Task 철회만료는새예약을막고사진으로이미피킹한물품을반환할수있다()
    {
        await using var f = await Create(); var wave = await f.Round(quantityPerLot: 2); var workspace = await f.Use(DistributedTestFixture.Driver).WorkspaceAsync(DistributedTestFixture.Driver, "driver", default);
        var stop = wave.Stops.First(); f.Clock.Now = stop.ExpectedArrivalUtc; var item = wave.Reservations.First(x => x.SiteId == stop.SiteId);
        var site = workspace.Sites.Single(x => x.SiteId == item.SiteId); var location = site.Locations.Single(x => x.LocationId == item.LocationId);
        var pick = new DistributedPickBatchRequest { RequestId = Guid.NewGuid(), ExpectedRevision = wave.Revision, StopId = stop.StopId, LocationBarcode = location.Barcode,
            Lines = [new() { ReservationId = item.ReservationId, OwnerUserId = item.OwnerUserId, Sku = item.Sku, Quantity = 1 }] };
        await f.Proof(DistributedTestFixture.Driver, wave.WaveId, pick); var picked = await f.Use(DistributedTestFixture.Driver).PickBatchAsync(DistributedTestFixture.Driver, wave.WaveId, pick, default);
        await f.Export("driver", DistributedTestFixture.Driver, "partial-pick");
        var agreement = await f.Db.Set<분산보관합의Record>().AsNoTracking().SingleAsync(x => x.LocationId == item.LocationId && x.GoodsOwnerUserId == item.OwnerUserId);
        await f.Use(Operator).AgreementConsentAsync(Operator, agreement.AgreementId, new() { RequestId = Guid.NewGuid(), ExpectedRevision = agreement.Revision, TermsVersion = 1, Granted = false }, default);
        var cancel = await f.Use(Operator).CancelAsync(Operator, wave.WaveId, new() { RequestId = Guid.NewGuid(), ExpectedRevision = picked.Revision, OrderId = item.OrderId, Reason = "test physical return" }, default);
        await f.Export("warehouse", Operator, "return-required");
        var returned = new DistributedReturnBatchRequest { RequestId = Guid.NewGuid(), ExpectedRevision = cancel.Revision, LocationBarcode = location.Barcode, Lines = [new() { ReservationId = item.ReservationId, Quantity = 1 }] };
        await f.Proof(Operator, wave.WaveId, returned); await f.Use(Operator).ReturnBatchAsync(Operator, wave.WaveId, returned, default);
        await f.Export("warehouse", Operator, "return-completed");
        Assert.Equal(1, (await f.Db.Set<분산재고예약Record>().AsNoTracking().SingleAsync(x => x.ReservationId == item.ReservationId)).ReturnedQuantity);
        Assert.Equal("Revoked", (await f.Db.Set<분산보관합의Record>().AsNoTracking().SingleAsync(x => x.AgreementId == agreement.AgreementId)).StatusCode);
    }
    private static DistributedStorageAgreementRequest Agreement(DistributedTestFixture f, DistributedSiteDto site) => new() { RequestId = Guid.NewGuid(), LocationId = site.Locations.Single().LocationId, GoodsOwnerUserId = "goods-owner-a",
        Terms = new() { AllowedSkus = ["same-sku"], MaximumStoredQuantity = 10, StartsAtUtc = f.Clock.Now.AddHours(-1), EndsAtUtc = f.Clock.Now.AddDays(1), AccessHours = "agreed hours", PriceOrFreeTerms = "free", CustodianAndCare = "named operator", LossDamageAndReturnTerms = "return remaining stock" } };
    private static Task<DistributedCommandResponse> Plan(DistributedTestFixture f, DistributedSiteDto site) => f.Use(SellerA).InboundAsync(SellerA, new() { RequestId = Guid.NewGuid(), SourceReference = "test declared scope",
        Lines = [new() { LineKey = "first", SourceInboundId = 101, SiteId = site.SiteId, LocationId = site.Locations.Single().LocationId, Sku = "same-sku", ProductName = "stock", LotCode = "first", ExpectedQuantity = 5 }] }, default);
    private static DistributedInboundReceiptBatchRequest Receive(DistributedTestFixture f, DistributedSiteDto site, DistributedCommandResponse plan, int quantity) => new() { RequestId = Guid.NewGuid(), ExpectedRevision = plan.Revision,
        LocationBarcode = site.Locations.Single().Barcode, Lines = [new() { LineKey = "first", Sku = "same-sku", Quantity = quantity }] };
    private static async Task Error(string code, Func<Task> action) => Assert.Equal(code, (await Assert.ThrowsAsync<DistributedFulfillmentException>(action)).Code);
}
