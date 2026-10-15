using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.Warehouse;
using Ssalddel.Contracts.Common.Warehouse;
using 살뜰.도메인.창고;
using static Ssalddel.Tests.Application.Warehouse.DistributedTestFixture;

namespace Ssalddel.Tests.Application.Warehouse;

/// <summary>최종 실물·종료 사실이 같으면 명령 순서와 관계없이 같은 입고 계획 상태를 얻습니다.</summary>
public sealed class DistributedInboundCompletionR76Tests
{
    private static readonly DistributedActor GoodsOwner = new("goods-owner-a");
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData("Cancel")]
    [InlineData("Reject")]
    public async Task 수령완료와미수령행종료의역순은같은완료상태와실물근거를보존한다(string action)
    {
        var receiveFirst = await ReceiveAndCloseAsync(action, closeFirst: false, receivedQuantity: 3);
        var closeFirst = await ReceiveAndCloseAsync(action, closeFirst: true, receivedQuantity: 3);

        Assert.Equal(receiveFirst, closeFirst);
        Assert.Equal("Received", receiveFirst.PlanStatus);
        Assert.Equal("입고완료", receiveFirst.FirstTargetStatus);
        Assert.Equal("입고취소", receiveFirst.SecondTargetStatus);
        Assert.Equal(3, receiveFirst.ReceivedQuantity);
        Assert.Equal(3, receiveFirst.AvailableQuantity);
        Assert.Equal(3, receiveFirst.CompletedQuantity);
        Assert.Equal(1, receiveFirst.ReceiveReceiptCount);
        Assert.Equal(0, receiveFirst.SecondReceivedQuantity);
    }

    [Theory]
    [InlineData("Cancel")]
    [InlineData("Reject")]
    public async Task 일부실물만받은행이남으면미수령행종료의역순에도부분입고를유지한다(string action)
    {
        var receiveFirst = await ReceiveAndCloseAsync(action, closeFirst: false, receivedQuantity: 1);
        var closeFirst = await ReceiveAndCloseAsync(action, closeFirst: true, receivedQuantity: 1);

        Assert.Equal(receiveFirst, closeFirst);
        Assert.Equal("PartiallyReceived", receiveFirst.PlanStatus);
        Assert.Equal("부분입고", receiveFirst.FirstTargetStatus);
        Assert.Equal(1, receiveFirst.ReceivedQuantity);
        Assert.Equal(1, receiveFirst.CompletedQuantity);
        Assert.Equal(0, receiveFirst.ReservedQuantity);
    }

    [Theory]
    [InlineData("Cancel", "Reject")]
    [InlineData("Reject", "Cancel")]
    public async Task 전부미수령인계획의마지막행을종료하면실물근거없이취소로마감한다(string firstAction, string secondAction)
    {
        await using var f = await Create();
        var (site, plan) = await SetupAsync(f);
        plan = await CloseAsync(f, plan, "first", firstAction);
        Assert.Equal("Planned", (await SnapshotAsync(f, plan.AggregateId)).PlanStatus);
        plan = await CloseAsync(f, plan, "second", secondAction);

        var snapshot = await SnapshotAsync(f, plan.AggregateId);
        Assert.Equal("Cancelled", snapshot.PlanStatus);
        Assert.Equal(0, snapshot.ReceivedQuantity + snapshot.SecondReceivedQuantity);
        Assert.Equal(0, snapshot.AvailableQuantity + snapshot.ReservedQuantity);
        Assert.Equal(0, snapshot.ReceiveReceiptCount);
        Assert.Equal(0, snapshot.CompletedQuantity);
        var replacement = await CreatePlanAsync(f, site);
        Assert.NotEqual(plan.AggregateId, replacement.AggregateId);
        Assert.Equal("Cancelled", (await SnapshotAsync(f, plan.AggregateId)).PlanStatus);
    }

    [Fact]
    public async Task 부분수령수량합의축소와다른행취소의역순은새수령이나새작업없이같이완료된다()
    {
        var reduceFirst = await ReduceAndCloseAsync(closeFirst: false);
        var closeFirst = await ReduceAndCloseAsync(closeFirst: true);

        Assert.Equal(reduceFirst, closeFirst);
        Assert.Equal("Received", reduceFirst.PlanStatus);
        Assert.Equal(2, reduceFirst.FirstExpectedQuantity);
        Assert.Equal(2, reduceFirst.FirstTargetExpectedQuantity);
        Assert.Equal(2, reduceFirst.ReceivedQuantity);
        Assert.Equal(2, reduceFirst.CompletedQuantity);
        Assert.Equal(1, reduceFirst.ReceiveReceiptCount);
        Assert.Equal("입고완료", reduceFirst.FirstTargetStatus);
    }

    [Fact]
    public async Task 수령과마지막취소재전송은계획판본과사진수량을늘리지않고충돌은기존완료를보존한다()
    {
        await using var f = await Create();
        var (site, plan) = await SetupAsync(f);
        var receive = await f.Proof(Operator, plan.AggregateId, new DistributedInboundReceiptRequest
        {
            RequestId = Guid.NewGuid(), ExpectedRevision = plan.Revision, LineKey = "first",
            LocationBarcode = site.Locations.Single().Barcode, Sku = "same-sku", Quantity = 3
        });
        plan = await f.Use(Operator).ReceiveAsync(Operator, plan.AggregateId, receive, default);
        var receiveReplay = await f.Use(Operator).ReceiveAsync(Operator, plan.AggregateId, receive, default);
        Assert.True(receiveReplay.Replayed);
        Assert.Equal(plan.Revision, receiveReplay.Revision);

        var cancel = new DistributedInboundDecisionRequest
        {
            RequestId = Guid.NewGuid(), ExpectedRevision = plan.Revision, TermsVersion = 1,
            Action = "Cancel", Reason = "합성 미수령 잔여행 종료"
        };
        plan = await f.Use(Operator).InboundDecisionAsync(Operator, plan.AggregateId, "second", cancel, default);
        var before = await SnapshotAsync(f, plan.AggregateId);
        var cancelReplay = await f.Use(Operator).InboundDecisionAsync(Operator, plan.AggregateId, "second", cancel, default);
        Assert.True(cancelReplay.Replayed);
        Assert.Equal(plan.Revision, cancelReplay.Revision);
        Assert.Equal(before, await SnapshotAsync(f, plan.AggregateId));
        Assert.Equal("Received", before.PlanStatus);
        Assert.Equal(1, before.ReceiveReceiptCount);
        Assert.Equal(3, before.CompletedQuantity);

        await Error("DistributedRevisionConflict", () => f.Use(Operator).InboundDecisionAsync(Operator,
            plan.AggregateId, "second", new() { RequestId = Guid.NewGuid(), ExpectedRevision = cancel.ExpectedRevision,
                TermsVersion = 1, Action = "Cancel", Reason = "오래된 판본" }, default));
        var outsider = new DistributedActor("unrelated-user");
        await Error("DistributedInboundPartyRequired", () => f.Use(outsider).InboundDecisionAsync(outsider,
            plan.AggregateId, "second", new() { RequestId = Guid.NewGuid(), ExpectedRevision = plan.Revision,
                TermsVersion = 1, Action = "Cancel", Reason = "무관한 사용자" }, default));
        Assert.Equal(before, await SnapshotAsync(f, plan.AggregateId));
        Assert.Equal(plan.Revision, (await f.Db.Set<분산입고계획Record>().AsNoTracking().SingleAsync(x => x.PlanId == plan.AggregateId)).Revision);
    }

    private static async Task<Snapshot> ReceiveAndCloseAsync(string action, bool closeFirst, int receivedQuantity)
    {
        await using var f = await Create();
        var (site, plan) = await SetupAsync(f);
        if (closeFirst) plan = await CloseAsync(f, plan, "second", action);
        plan = await ReceiveAsync(f, site, plan, receivedQuantity);
        if (!closeFirst) plan = await CloseAsync(f, plan, "second", action);
        return await SnapshotAsync(f, plan.AggregateId);
    }

    private static async Task<Snapshot> ReduceAndCloseAsync(bool closeFirst)
    {
        await using var f = await Create();
        var (site, plan) = await SetupAsync(f);
        plan = await ReceiveAsync(f, site, plan, 2);
        if (closeFirst) plan = await CloseAsync(f, plan, "second", "Cancel");
        plan = await AgreeAsync(f, site, plan, "first", quantity: 2, version: 2);
        if (!closeFirst) plan = await CloseAsync(f, plan, "second", "Cancel");
        return await SnapshotAsync(f, plan.AggregateId);
    }

    private static async Task<(DistributedSiteDto Site, DistributedCommandResponse Plan)> SetupAsync(DistributedTestFixture f)
    {
        var site = (await f.Sites())[0];
        await f.Use(Operator).SiteAcceptancePolicyAsync(Operator, site.SiteId, new()
        {
            RequestId = Guid.NewGuid(), ExpectedRevision = site.Revision, Policy = new()
            {
                AllowedCategoryCodes = ["industrial"], AllowedPackagingCodes = ["boxed"],
                AllowedTemperatureCodes = ["ambient"], CapacityUnit = "칸", MaximumCapacity = 20,
                MaximumStorageDays = 2, AccessHours = "합성 입고 시간"
            }
        }, default);
        await f.EnsureAgreement(site, GoodsOwner, "same-sku");
        var plan = await CreatePlanAsync(f, site);
        plan = await AgreeAsync(f, site, plan, "first", quantity: 3, version: 1);
        plan = await AgreeAsync(f, site, plan, "second", quantity: 2, version: 1);
        return (site, plan);
    }

    private static Task<DistributedCommandResponse> CreatePlanAsync(DistributedTestFixture f, DistributedSiteDto site)
        => f.Use(SellerA).InboundAsync(SellerA, new()
        {
            RequestId = Guid.NewGuid(), SourceReference = "합성 순서 반전 입고",
            Lines = new[] { (Key: "first", Quantity: 3), (Key: "second", Quantity: 2) }.Select(x => new DistributedInboundLineRequest
            {
                LineKey = x.Key, SourceInboundId = 101, SiteId = site.SiteId, LocationId = site.Locations.Single().LocationId,
                Sku = "same-sku", ProductName = "합성 물품", LotCode = Guid.NewGuid().ToString("N"), ExpectedQuantity = x.Quantity
            }).ToArray()
        }, default);

    private static async Task<DistributedCommandResponse> AgreeAsync(DistributedTestFixture f, DistributedSiteDto site,
        DistributedCommandResponse plan, string line, int quantity, int version)
    {
        var agreement = await f.Db.Set<분산보관합의Record>().AsNoTracking().SingleAsync(x => x.SiteId == site.SiteId && x.GoodsOwnerUserId == GoodsOwner.UserId);
        plan = await f.Use(SellerA).InboundProposalAsync(SellerA, plan.AggregateId, line, new()
        {
            RequestId = Guid.NewGuid(), ExpectedRevision = plan.Revision, Terms = new()
            {
                AgreementId = agreement.AgreementId, AgreementTermsVersion = agreement.TermsVersion,
                SupplierUserId = SellerA.UserId, SellerUserId = SellerA.UserId, GoodsOwnerUserId = GoodsOwner.UserId,
                SupplierTypeCode = "factory", Quantity = quantity, ArrivalStartUtc = f.Clock.Now.AddMinutes(-1),
                ArrivalEndUtc = f.Clock.Now.AddHours(2), StorageEndsAtUtc = f.Clock.Now.AddDays(1), AccessHoursConfirmed = true,
                SupplyPriceOrFreeTerms = "합성 무상", SupplierResponsibilityTerms = "합성 공급 확인", ReturnTerms = "별도 반환 협의",
                Goods = new() { CategoryCodes = ["industrial"], IsFood = false, IsProcessed = true, PackagingCode = "boxed",
                    TemperatureCode = "ambient", CapacityUnit = "칸", CapacityPerItem = 1 }
            }
        }, default);
        foreach (var actor in new[] { GoodsOwner, Operator })
            plan = await f.Use(actor).InboundConsentAsync(actor, plan.AggregateId, line,
                new() { RequestId = Guid.NewGuid(), ExpectedRevision = plan.Revision, TermsVersion = version, Granted = true }, default);
        return plan;
    }

    private static async Task<DistributedCommandResponse> ReceiveAsync(DistributedTestFixture f, DistributedSiteDto site,
        DistributedCommandResponse plan, int quantity)
        => await f.Use(Operator).ReceiveAsync(Operator, plan.AggregateId, await f.Proof(Operator, plan.AggregateId,
            new DistributedInboundReceiptRequest { RequestId = Guid.NewGuid(), ExpectedRevision = plan.Revision,
                LineKey = "first", LocationBarcode = site.Locations.Single().Barcode, Sku = "same-sku", Quantity = quantity }), default);

    private static Task<DistributedCommandResponse> CloseAsync(DistributedTestFixture f, DistributedCommandResponse plan, string line, string action)
        => f.Use(Operator).InboundDecisionAsync(Operator, plan.AggregateId, line, new()
        {
            RequestId = Guid.NewGuid(), ExpectedRevision = plan.Revision, TermsVersion = 1, Action = action,
            Reason = "합성 미수령 잔여행 종료"
        }, default);

    private static async Task<Snapshot> SnapshotAsync(DistributedTestFixture f, string id)
    {
        var plan = await f.Db.Set<분산입고계획Record>().AsNoTracking().SingleAsync(x => x.PlanId == id);
        var lots = await f.Db.Set<분산재고LotRecord>().AsNoTracking().Where(x => x.PlanId == id).ToArrayAsync();
        var first = lots.Single(x => x.LineKey == "first"); var second = lots.Single(x => x.LineKey == "second");
        var targets = await f.Db.입고요청.AsNoTracking().Where(x => x.Id == first.TargetInboundId || x.Id == second.TargetInboundId).ToDictionaryAsync(x => x.Id);
        var stockIds = lots.Select(x => x.InboundItemId).ToArray();
        var stocks = await f.Db.입고상품.AsNoTracking().Where(x => Enumerable.Contains(stockIds, x.Id)).ToArrayAsync();
        var receipts = await f.Db.Set<분산물류CommandReceipt>().AsNoTracking().Where(x => x.AggregateId == id && x.Kind == "Receive").ToArrayAsync();
        var completed = receipts.Sum(x => JsonSerializer.Deserialize<분산물류완료근거>(x.CompletionLinesJson!, Json)!.Lines.Sum(line => line.Quantity));
        return new(plan.StatusCode, first.ExpectedQuantity, first.ReceivedQuantity, second.ReceivedQuantity,
            targets[first.TargetInboundId].상태, targets[second.TargetInboundId].상태, targets[first.TargetInboundId].예정수량,
            stocks.Sum(x => x.가용수량), stocks.Sum(x => x.예약수량), receipts.Length, completed);
    }

    private sealed record Snapshot(string PlanStatus, int FirstExpectedQuantity, int ReceivedQuantity, int SecondReceivedQuantity,
        string FirstTargetStatus, string SecondTargetStatus, int? FirstTargetExpectedQuantity, int AvailableQuantity,
        int ReservedQuantity, int ReceiveReceiptCount, int CompletedQuantity);

    private static async Task Error(string code, Func<Task> action)
        => Assert.Equal(code, (await Assert.ThrowsAsync<DistributedFulfillmentException>(action)).Code);
}
