using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.Warehouse;
using Ssalddel.Contracts.Common.Warehouse;
using 살뜰.도메인.창고;
using static Ssalddel.Tests.Application.Warehouse.DistributedTestFixture;

namespace Ssalddel.Tests.Application.Warehouse;

public sealed class DistributedStockResolutionTests
{
    private static readonly DistributedActor AssignedWorker = DistributedTestFixture.Driver;
    private static readonly DistributedActor GoodsOwner = new("goods-owner-a");

    [Theory]
    [InlineData(DistributedStockResolutionKinds.DamagedDisposal, "불량재고폐기")]
    [InlineData(DistributedStockResolutionKinds.AvailableShortage, "가용재고소실정정")]
    [InlineData(DistributedStockResolutionKinds.DamagedShortage, "불량재고소실정정")]
    public async Task 양측합의와사진근거로_해당재고만정리하며_원입고와완료근거를보존한다(string kind, string historyKind)
    {
        await using var f = await Create(); var (site, lot) = await Setup(f, kind);
        var id = await Ready(f, site, lot, kind);
        var request = await Evidence(f, site, id, 2);
        var response = await f.Use(AssignedWorker).StockReleaseHandoverAsync(AssignedWorker, site.SiteId, request, default);
        var replay = await f.Use(AssignedWorker).StockReleaseHandoverAsync(AssignedWorker, site.SiteId, request, default);

        Assert.True(replay.Replayed);
        Assert.Equal(response.Revision, replay.Revision);
        var stock = await f.Db.입고상품.AsNoTracking().SingleAsync(x => x.Id == lot.InboundItemId);
        Assert.Equal(5, stock.입고수량);
        Assert.Equal(3, DistributedStockResolutionKinds.UsesDamagedStock(kind) ? stock.불량수량 : stock.가용수량);
        Assert.Equal(0, DistributedStockResolutionKinds.UsesDamagedStock(kind) ? stock.가용수량 : stock.불량수량);
        Assert.Equal(0, stock.예약수량);
        Assert.Equal(5, (await f.Db.Set<분산재고LotRecord>().AsNoTracking().SingleAsync(x => x.LotId == lot.LotId)).ReceivedQuantity);
        var history = Assert.Single(await f.Db.재고이력.AsNoTracking().Where(x => x.이력유형 == historyKind).ToArrayAsync());
        Assert.Equal(-2, history.변경수량); Assert.Equal(3, history.변경후수량);
        var receipt = await f.Db.Set<분산물류CommandReceipt>().AsNoTracking().SingleAsync(x => x.RequestId == request.RequestId && x.ActorUserId == AssignedWorker.UserId);
        Assert.Contains("StockResolution", receipt.CompletionLinesJson);
        Assert.Contains(request.PhotoEvidenceIds.Single(), receipt.CompletionLinesJson);
        Assert.Equal("PartiallyReleased", replay.StatusCode);
    }

    [Fact]
    public async Task 불량정리도_소유자양측동의와지정담당자와사진과실사근거를모두요구한다()
    {
        await using var f = await Create(); var (site, lot) = await Setup(f, DistributedStockResolutionKinds.DamagedDisposal);
        var id = await Propose(f, site, lot, DistributedStockResolutionKinds.DamagedDisposal);
        var request = await Request(f, site, id, 1);
        await Error("DistributedStockReleaseConsentRequired", () => f.Use(AssignedWorker).AuthorizeEvidenceAsync(AssignedWorker, Work(site, request), default));
        await Consent(f, site, id, Operator); await Consent(f, site, id, GoodsOwner);
        request = await Request(f, site, id, 1);
        await Error("DistributedStockReleaseAssigneeRequired", () => f.Use(Operator).StockReleaseHandoverAsync(Operator, site.SiteId, request, default));
        request.ResolutionEvidenceReference = null;
        await Error("DistributedInputInvalid", () => f.Use(AssignedWorker).AuthorizeEvidenceAsync(AssignedWorker, Work(site, request), default));
        request.ResolutionEvidenceReference = "합성 폐기 확인 기록";
        await Error("DistributedPhotoEvidenceRequired", () => f.Use(AssignedWorker).StockReleaseHandoverAsync(AssignedWorker, site.SiteId, request, default));
        Assert.Equal(5, (await f.Db.입고상품.AsNoTracking().SingleAsync(x => x.Id == lot.InboundItemId)).불량수량);
    }

    [Fact]
    public async Task 사진촬영뒤수량이나실사근거변경은_같은사진으로정리할수없다()
    {
        await using var f = await Create(); var (site, lot) = await Setup(f, DistributedStockResolutionKinds.DamagedShortage);
        var id = await Ready(f, site, lot, DistributedStockResolutionKinds.DamagedShortage);
        var request = await Evidence(f, site, id, 2);
        request.ResolutionEvidenceReference = "촬영 후 다른 실사 기록";
        await Error("DistributedPhotoEvidenceBindingMismatch", () => f.Use(AssignedWorker).StockReleaseHandoverAsync(AssignedWorker, site.SiteId, request, default));
        Assert.Equal(5, (await f.Db.입고상품.AsNoTracking().SingleAsync(x => x.Id == lot.InboundItemId)).불량수량);
    }

    [Fact]
    public async Task 불량만남은재고도후보에표시되고_전량정리뒤별도로공간을종료한다()
    {
        await using var f = await Create(); var (site, lot) = await Setup(f, DistributedStockResolutionKinds.DamagedDisposal);
        var candidate = Assert.Single((await f.Use(Operator).WorkspaceAsync(Operator, "warehouse", default)).StockReleaseCandidates);
        Assert.Equal(0, candidate.AvailableQuantity); Assert.Equal(5, candidate.DamagedQuantity);
        var id = await Ready(f, site, lot, DistributedStockResolutionKinds.DamagedDisposal);
        await f.Use(AssignedWorker).StockReleaseHandoverAsync(AssignedWorker, site.SiteId, await Evidence(f, site, id, 5), default);
        var result = await f.Use(Operator).CloseSiteAsync(Operator, site.SiteId, new()
        { RequestId = Guid.NewGuid(), ExpectedRevision = await Revision(f, site), NoRemainingCustodyConfirmed = true, EvidenceReference = "합성 현장 잔량 확인" }, default);
        Assert.Equal("Closed", result.StatusCode);
    }

    [Theory]
    [InlineData(DistributedStockResolutionKinds.DamagedDisposal)]
    [InlineData(DistributedStockResolutionKinds.AvailableShortage)]
    public async Task 예약재고나기사보관을_소실정정으로우회해없앨수없다(string kind)
    {
        await using var f = await Create(); var (site, lot) = await Setup(f, kind);
        var stock = await f.Db.입고상품.SingleAsync(x => x.Id == lot.InboundItemId); stock.예약수량 = 1;
        await f.Db.SaveChangesAsync(); f.Db.ChangeTracker.Clear();
        await Error("DistributedStockReleaseReservedOrInsufficient", () => Propose(f, site, lot, kind));
    }

    [Fact]
    public void 구형정상반출요청은_추가필드없이같은JSON계약을유지한다()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        Assert.DoesNotContain("resolutionKind", JsonSerializer.Serialize(new DistributedStockReleaseProposalRequest(), options));
        Assert.DoesNotContain("resolutionEvidenceReference", JsonSerializer.Serialize(new DistributedStockReleaseHandoverRequest(), options));
    }

    private static async Task<(DistributedSiteDto Site, 분산재고LotRecord Lot)> Setup(DistributedTestFixture f, string kind)
    {
        var sites = await f.Sites(); await f.Stock(sites); var site = sites[0];
        var lot = await f.Db.Set<분산재고LotRecord>().AsNoTracking().SingleAsync(x => x.SiteId == site.SiteId && x.OwnerUserId == GoodsOwner.UserId);
        // 테스트의 검수 완료 시점: 이 공간에는 선택한 5개만 남았다고 명시한다. 운영 정정 명령이 아니다.
        foreach (var stock in await f.Db.입고상품.Where(x => x.창고Id == site.WarehouseId).ToArrayAsync())
        { stock.가용수량 = stock.Id == lot.InboundItemId && !DistributedStockResolutionKinds.UsesDamagedStock(kind) ? 5 : 0; stock.불량수량 = stock.Id == lot.InboundItemId && DistributedStockResolutionKinds.UsesDamagedStock(kind) ? 5 : 0; }
        await f.Db.SaveChangesAsync(); f.Db.ChangeTracker.Clear();
        await f.Use(Operator).ConsentAsync(Operator, site.SiteId, new() { RequestId = Guid.NewGuid(), ExpectedRevision = await Revision(f, site), ConsentKind = "Owner", Granted = false, EvidenceReference = "합성 공간 철회" }, default);
        return (site, lot);
    }
    private static async Task<long> Revision(DistributedTestFixture f, DistributedSiteDto site)
        => (await f.Db.Set<분산거점Record>().AsNoTracking().SingleAsync(x => x.SiteId == site.SiteId)).Revision;
    private static async Task<string> Propose(DistributedTestFixture f, DistributedSiteDto site, 분산재고LotRecord lot, string kind)
    {
        var request = new DistributedStockReleaseProposalRequest { RequestId = Guid.NewGuid(), ExpectedRevision = await Revision(f, site), LotId = lot.LotId,
            AssignedUserId = AssignedWorker.UserId, Quantity = 5, ResolutionKind = kind, HandoverTerms = "합성 현장 실사·처리 결과 확인", Reason = "합성 재고 정리 합의" };
        await f.Use(Operator).StockReleaseProposalAsync(Operator, site.SiteId, request, default); return "release-" + request.RequestId.ToString("N");
    }
    private static Task<DistributedCommandResponse> Consent(DistributedTestFixture f, DistributedSiteDto site, string id, DistributedActor actor)
        => ConsentCore(f, site, id, actor);
    private static async Task<DistributedCommandResponse> ConsentCore(DistributedTestFixture f, DistributedSiteDto site, string id, DistributedActor actor)
        => await f.Use(actor).StockReleaseConsentAsync(actor, site.SiteId, new() { RequestId = Guid.NewGuid(), ExpectedRevision = await Revision(f, site), ReleaseId = id, Granted = true }, default);
    private static async Task<string> Ready(DistributedTestFixture f, DistributedSiteDto site, 분산재고LotRecord lot, string kind)
    { var id = await Propose(f, site, lot, kind); await Consent(f, site, id, Operator); await Consent(f, site, id, GoodsOwner); return id; }
    private static async Task<DistributedStockReleaseHandoverRequest> Request(DistributedTestFixture f, DistributedSiteDto site, string id, int quantity)
        => new() { RequestId = Guid.NewGuid(), ExpectedRevision = await Revision(f, site), ReleaseId = id, LocationBarcode = site.Locations.Single().Barcode, Quantity = quantity, ResolutionEvidenceReference = "합성 실사·폐기 확인 기록" };
    private static DistributedEvidenceWorkRequest Work(DistributedSiteDto site, DistributedStockReleaseHandoverRequest request)
        => new() { Kind = "StockRelease", AggregateId = site.SiteId, StockRelease = request };
    private static async Task<DistributedStockReleaseHandoverRequest> Evidence(DistributedTestFixture f, DistributedSiteDto site, string id, int quantity)
    { var request = await Request(f, site, id, quantity); request.PhotoEvidenceIds = f.Photos.Register(await f.Use(AssignedWorker).AuthorizeEvidenceAsync(AssignedWorker, Work(site, request), default)); return request; }
    private static async Task Error(string code, Func<Task> action) => Assert.Equal(code, (await Assert.ThrowsAsync<DistributedFulfillmentException>(action)).Code);
}
