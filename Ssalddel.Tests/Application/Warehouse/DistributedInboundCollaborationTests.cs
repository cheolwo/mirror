using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.Warehouse;
using Ssalddel.Contracts.Common.Warehouse;
using 살뜰.도메인.창고;
using static Ssalddel.Tests.Application.Warehouse.DistributedTestFixture;

namespace Ssalddel.Tests.Application.Warehouse;

/// <summary>공급·공간 협의의 실제 SQLite 명령, 양측 정본 조회와 사진/재고 경계를 검증합니다.</summary>
public sealed class DistributedInboundCollaborationTests
{
    private static readonly DistributedActor GoodsOwner = new("goods-owner-a");
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task 공간조건은소유자만설정하고합의전초안은남기되승인전사진입고는차단한다()
    {
        await using var f = await Create(); var site = (await f.Sites())[0];
        await Error("DistributedSpaceOwnerRequired", () => f.Use(SellerA).SiteAcceptancePolicyAsync(SellerA, site.SiteId,
            new() { RequestId = Guid.NewGuid(), ExpectedRevision = site.Revision, Policy = Policy() }, default));
        await SetPolicy(f, site); var plan = await Plan(f, site);
        var request = Receive(site, plan, 1);
        await Error("DistributedInboundApprovalRequired", () => f.Use(Operator).ReceiveAsync(Operator, plan.AggregateId, request, default));
        var line = await Line(f, SellerA, plan.AggregateId);
        Assert.Equal("Draft", line.InboundCollaboration!.StatusCode); Assert.Contains("propose-inbound", line.AllowedActions!);
        Assert.DoesNotContain("receive", line.AllowedActions!); Assert.Equal(0, await f.Db.입고상품.SumAsync(x => x.가용수량));
    }

    [Fact]
    public async Task 위탁판매자는제안만하며메모와한쪽동의는예약이아니고양측동의뒤사진부분입고된다()
    {
        await using var f = await Create(); var site = (await f.Sites())[0]; await SetPolicy(f, site); var plan = await Plan(f, site);
        plan = await Propose(f, site, plan); var version = (await Line(f, SellerA, plan.AggregateId)).InboundCollaboration!.TermsVersion;
        plan = await f.Use(SellerA).InboundNoteAsync(SellerA, plan.AggregateId, "first", new() { RequestId = Guid.NewGuid(), ExpectedRevision = plan.Revision, TermsVersion = version, Note = "도착 시간 확인 부탁드립니다" }, default);
        Assert.False((await Line(f, GoodsOwner, plan.AggregateId)).InboundCollaboration!.GoodsOwnerConsented);
        await Error("DistributedInboundConsentPartyRequired", () => Consent(f, SellerA, plan, version));
        plan = await Consent(f, GoodsOwner, plan, version);
        await Error("DistributedInboundApprovalRequired", () => f.Use(Operator).ReceiveAsync(Operator, plan.AggregateId, Receive(site, plan, 2), default));
        plan = await Consent(f, Operator, plan, version); Assert.Equal("Confirmed", plan.StatusCode);
        var request = await f.Proof(Operator, plan.AggregateId, Receive(site, plan, 2));
        var result = await f.Use(Operator).ReceiveAsync(Operator, plan.AggregateId, request, default);
        Assert.Equal("PartiallyReceived", result.StatusCode); Assert.True((await f.Use(Operator).ReceiveAsync(Operator, plan.AggregateId, request, default)).Replayed);
        var lot = await f.Db.Set<분산재고LotRecord>().AsNoTracking().SingleAsync(); Assert.Equal(2, lot.ReceivedQuantity);
        Assert.Contains("termsVersion", lot.StorageAgreementSnapshotJson); Assert.Equal(2, await f.Db.입고상품.SumAsync(x => x.가용수량));
        await Error("DistributedInboundPhysicalStockRequiresReview", () => f.Use(Operator).InboundDecisionAsync(Operator, plan.AggregateId, "first",
            new() { RequestId = Guid.NewGuid(), ExpectedRevision = result.Revision, TermsVersion = version, Action = "Cancel", Reason = "수령 뒤 취소 시도" }, default));
    }

    [Theory]
    [InlineData("agricultural", false, "GoodsCategoryDenied")]
    [InlineData("industrial", true, "FoodNotAllowed")]
    [InlineData("unknown", false, "GoodsCategoryUnknown")]
    public async Task 공급자업종과무관하게실제물품조건을검사하고조건불명확또는거부물품은승인되지않는다(string category, bool food, string code)
    {
        await using var f = await Create(); var site = (await f.Sites())[0]; await SetPolicy(f, site); var plan = await Plan(f, site);
        var terms = await Terms(f, site); terms.SupplierTypeCode = "factory"; terms.Goods.CategoryCodes = [category]; terms.Goods.IsFood = food;
        plan = await Propose(f, site, plan, terms);
        await Error(code, () => Consent(f, GoodsOwner, plan, 1));
        Assert.Contains(code, (await Line(f, SellerA, plan.AggregateId)).WaitingReasonCodes!);
    }

    [Fact]
    public async Task 수정제안은동의를초기화하며이전판본동의와과거사진을재사용하지못한다()
    {
        await using var f = await Create(); var site = (await f.Sites())[0]; await SetPolicy(f, site); var plan = await Plan(f, site);
        plan = await Propose(f, site, plan); plan = await Consent(f, GoodsOwner, plan, 1); plan = await Consent(f, Operator, plan, 1);
        var oldPhoto = await f.Proof(Operator, plan.AggregateId, Receive(site, plan, 1));
        var terms = await Terms(f, site); terms.ArrivalEndUtc = f.Clock.Now.AddHours(3); plan = await Propose(f, site, plan, terms, Operator);
        var state = (await Line(f, GoodsOwner, plan.AggregateId)).InboundCollaboration!;
        Assert.False(state.GoodsOwnerConsented); Assert.False(state.SpaceOwnerConsented); Assert.NotNull(state.ReservedTerms); Assert.Equal(2, state.TermsVersion);
        await Error("DistributedInboundTermsVersionMismatch", () => Consent(f, GoodsOwner, plan, 1));
        plan = await Consent(f, GoodsOwner, plan, 2); plan = await Consent(f, Operator, plan, 2);
        oldPhoto.ExpectedRevision = plan.Revision;
        await Error("DistributedPhotoEvidenceBindingMismatch", () => f.Use(Operator).ReceiveAsync(Operator, plan.AggregateId, oldPhoto, default));
    }

    [Theory]
    [InlineData("Goods")]
    [InlineData("CategoryCodes")]
    [InlineData("SupplierTypeCode")]
    public async Task 명시적인null제안은400으로거절하고협의와계획을변경하지않는다(string field)
    {
        await using var f = await Create(); var site = (await f.Sites())[0]; await SetPolicy(f, site); var plan = await Plan(f, site);
        plan = await Propose(f, site, plan); var terms = await Terms(f, site);
        if (field == "Goods") terms.Goods = null!;
        else if (field == "CategoryCodes") terms.Goods.CategoryCodes = null!;
        else terms.SupplierTypeCode = null!;
        var request = JsonSerializer.Deserialize<DistributedInboundProposalRequest>(JsonSerializer.Serialize(new DistributedInboundProposalRequest
            { RequestId = Guid.NewGuid(), ExpectedRevision = plan.Revision, Terms = terms }, Json), Json)!;
        var before = await f.Db.Set<분산재고LotRecord>().AsNoTracking().SingleAsync(); var beforeSite = await SiteView(f, site);
        var count = await f.Db.Set<분산물류CommandReceipt>().CountAsync(x => x.Kind == "InboundProposal");
        var error = await Assert.ThrowsAsync<DistributedFulfillmentException>(() => f.Use(SellerA).InboundProposalAsync(SellerA, plan.AggregateId, "first", request, default));
        Assert.Equal("DistributedInboundTermsInvalid", error.Code); Assert.Equal(400, error.Status);
        var after = await f.Db.Set<분산재고LotRecord>().AsNoTracking().SingleAsync();
        Assert.Equal(before.InboundCollaborationJson, after.InboundCollaborationJson); Assert.Equal(before.Revision, after.Revision);
        Assert.Equal(plan.Revision, (await f.Db.Set<분산입고계획Record>().AsNoTracking().SingleAsync()).Revision);
        Assert.Equal(beforeSite.Revision, (await SiteView(f, site)).Revision);
        Assert.Equal(count, await f.Db.Set<분산물류CommandReceipt>().CountAsync(x => x.Kind == "InboundProposal"));
        Assert.NotNull((await Line(f, GoodsOwner, plan.AggregateId)).InboundCollaboration!.Terms.Goods.CategoryCodes);
    }

    [Fact]
    public async Task 다른공급자의확정예약까지공간전체용량에포함하며협의수정중에도기존용량은유지한다()
    {
        await using var f = await Create(); var site = (await f.Sites())[0]; await SetPolicy(f, site, 4);
        var first = await Plan(f, site, 3); first = await Propose(f, site, first, await Terms(f, site, 3));
        first = await Consent(f, GoodsOwner, first, 1); first = await Consent(f, Operator, first, 1);
        first = await Propose(f, site, first, await Terms(f, site, 1)); // 새안이 작아도 앞서 승인한 3칸 유지
        var second = await Plan(f, site, 2, SellerB); second = await Propose(f, site, second, await Terms(f, site, 2, SellerB), SellerB);
        await Error("CapacityExceeded", () => Consent(f, SellerB, second, 1));
        first = await Consent(f, GoodsOwner, first, 2); first = await Consent(f, Operator, first, 2);
        second = await Consent(f, SellerB, second, 1); second = await Consent(f, Operator, second, 1);
        Assert.Equal("Confirmed", second.StatusCode); Assert.Equal(0, await f.Db.입고상품.SumAsync(x => x.입고수량));
    }

    [Fact]
    public async Task 거절과미수령취소는감사기록을남기고원천수량을새계획에서재사용하지만옛계획은다시입고하지못한다()
    {
        await using var f = await Create(); var site = (await f.Sites())[0]; await SetPolicy(f, site);
        var plan = await Plan(f, site, 10);
        var closed = await f.Use(Operator).InboundDecisionAsync(Operator, plan.AggregateId, "first", new() { RequestId = Guid.NewGuid(), ExpectedRevision = plan.Revision, TermsVersion = 0, Action = "Reject", Reason = "수용하지 않는 품목" }, default);
        Assert.Equal("Rejected", closed.StatusCode);
        await Error("DistributedInboundClosed", () => f.Use(Operator).ReceiveAsync(Operator, plan.AggregateId, Receive(site, closed, 1), default));
        var replacement = await Plan(f, site, 10); Assert.NotEqual(plan.AggregateId, replacement.AggregateId);
        var records = await f.Db.Set<분산재고LotRecord>().AsNoTracking().ToArrayAsync(); Assert.Equal(2, records.Length); Assert.All(records, x => Assert.Equal(0, x.ReceivedQuantity));
    }

    [Fact]
    public async Task 공간정책변경은기존승인을무효화하고양측새판본동의없이는재확정하지못한다()
    {
        await using var f = await Create(); var site = (await f.Sites())[0]; await SetPolicy(f, site); var plan = await Plan(f, site);
        plan = await Propose(f, site, plan); plan = await Consent(f, GoodsOwner, plan, 1); plan = await Consent(f, Operator, plan, 1);
        await SetPolicy(f, site, 12);
        await Error("DistributedInboundPolicyChanged", () => f.Use(Operator).ReceiveAsync(Operator, plan.AggregateId, Receive(site, plan, 1), default));
        await Error("DistributedInboundPolicyChanged", () => Consent(f, Operator, plan, 1));
        plan = await Propose(f, site, plan); plan = await Consent(f, Operator, plan, 2);
        Assert.Equal("AwaitingOtherParty", plan.StatusCode); Assert.False((await Line(f, GoodsOwner, plan.AggregateId)).InboundCollaboration!.GoodsOwnerConsented);
        plan = await Consent(f, GoodsOwner, plan, 2); Assert.Equal("Confirmed", plan.StatusCode);
    }

    [Fact]
    public async Task 현재계약은변경제안만으로중단하지않고새계약판본확정뒤옛입고승인은보류한다()
    {
        await using var f = await Create(); var site = (await f.Sites())[0]; await SetPolicy(f, site); var plan = await Plan(f, site);
        plan = await Propose(f, site, plan); plan = await Consent(f, GoodsOwner, plan, 1); plan = await Consent(f, Operator, plan, 1);
        var a = Assert.Single((await f.Use(GoodsOwner).WorkspaceAsync(GoodsOwner, "shipper", default)).StorageAgreements);
        var change = new DistributedStorageAgreementRequest { RequestId = Guid.NewGuid(), ExpectedRevision = a.Revision, LocationId = a.LocationId, GoodsOwnerUserId = a.GoodsOwnerUserId, Terms = a.Terms };
        change.Terms.PriceOrFreeTerms = "새 보관료 협의";
        var response = await f.Use(GoodsOwner).AgreementTermsAsync(GoodsOwner, a.AgreementId, change, default);
        await f.Proof(Operator, plan.AggregateId, Receive(site, plan, 1));
        response = await f.Use(Operator).AgreementConsentAsync(Operator, a.AgreementId, new() { RequestId = Guid.NewGuid(), ExpectedRevision = response.Revision, TermsVersion = 2, Granted = true }, default);
        response = await f.Use(GoodsOwner).AgreementConsentAsync(GoodsOwner, a.AgreementId, new() { RequestId = Guid.NewGuid(), ExpectedRevision = response.Revision, TermsVersion = 2, Granted = true }, default);
        await Error("DistributedInboundAgreementVersionMismatch", () => f.Use(Operator).ReceiveAsync(Operator, plan.AggregateId, Receive(site, plan, 1), default));
        var state = (await Line(f, GoodsOwner, plan.AggregateId)).InboundCollaboration!;
        Assert.Equal(1, state.Terms.AgreementTermsVersion); Assert.Contains("DistributedInboundAgreementVersionMismatch", (await Line(f, GoodsOwner, plan.AggregateId)).WaitingReasonCodes!);
    }

    [Fact]
    public async Task 거절된계약변경판본은다른내용의새제안에서재사용하지않는다()
    {
        await using var f = await Create(); var site = (await f.Sites())[0]; await f.EnsureAgreement(site, GoodsOwner, "same-sku");
        var a = Assert.Single((await f.Use(GoodsOwner).WorkspaceAsync(GoodsOwner, "shipper", default)).StorageAgreements);
        var change = new DistributedStorageAgreementRequest { RequestId = Guid.NewGuid(), ExpectedRevision = a.Revision, LocationId = a.LocationId, GoodsOwnerUserId = a.GoodsOwnerUserId, Terms = a.Terms };
        change.Terms.PriceOrFreeTerms = "거절된 변경안";
        var response = await f.Use(GoodsOwner).AgreementTermsAsync(GoodsOwner, a.AgreementId, change, default);
        response = await f.Use(Operator).AgreementConsentAsync(Operator, a.AgreementId, new() { RequestId = Guid.NewGuid(), ExpectedRevision = response.Revision, TermsVersion = 2, Granted = false }, default);
        change.RequestId = Guid.NewGuid(); change.ExpectedRevision = response.Revision; change.Terms.PriceOrFreeTerms = "새 조건";
        response = await f.Use(GoodsOwner).AgreementTermsAsync(GoodsOwner, a.AgreementId, change, default);
        var current = Assert.Single((await f.Use(GoodsOwner).WorkspaceAsync(GoodsOwner, "shipper", default)).StorageAgreements);
        Assert.Equal(1, current.TermsVersion); Assert.Equal(3, current.PendingTermsVersion);
        await Error("DistributedAgreementTermsVersionMismatch", () => f.Use(Operator).AgreementConsentAsync(Operator, a.AgreementId, new() { RequestId = Guid.NewGuid(), ExpectedRevision = response.Revision, TermsVersion = 2, Granted = true }, default));
        response = await f.Use(Operator).AgreementConsentAsync(Operator, a.AgreementId, new() { RequestId = Guid.NewGuid(), ExpectedRevision = response.Revision, TermsVersion = 1, Granted = false }, default);
        change.RequestId = Guid.NewGuid(); change.ExpectedRevision = response.Revision; change.Terms.PriceOrFreeTerms = "철회 후 새 초안";
        response = await f.Use(GoodsOwner).AgreementTermsAsync(GoodsOwner, a.AgreementId, change, default);
        current = Assert.Single((await f.Use(GoodsOwner).WorkspaceAsync(GoodsOwner, "shipper", default)).StorageAgreements);
        Assert.Equal(4, current.TermsVersion); Assert.Null(current.PendingTermsVersion);
        await Error("DistributedAgreementTermsVersionMismatch", () => f.Use(Operator).AgreementConsentAsync(Operator, a.AgreementId, new() { RequestId = Guid.NewGuid(), ExpectedRevision = response.Revision, TermsVersion = 3, Granted = true }, default));
    }

    [Fact]
    public async Task 실보관량의단위근거가없으면빈공간으로가정하지않고기존재고등록우회도차단한다()
    {
        await using var f = await Create(); var sites = await f.Sites(); await f.Stock(sites); var site = sites[0]; await SetPolicy(f, site);
        var old = (await f.Use(Operator).WorkspaceAsync(Operator, "warehouse", default)).Lots.First(x => x.SiteId == site.SiteId);
        var currentSite = await SiteView(f, site);
        await Error("DistributedInboundApprovalRequired", () => f.Use(Operator).ImportLotAsync(Operator, site.SiteId, new() { RequestId = Guid.NewGuid(), ExpectedRevision = currentSite.Revision, ActualLocationConfirmed = true }, default));
        var target = await Plan(f, site, 1, SellerA, newSource: true); target = await Propose(f, site, target, await Terms(f, site, 1, SellerA, ownerId: SellerA.UserId), SellerA);
        await Error("ExistingCapacityUnknown", () => Consent(f, SellerA, target, 1));
        Assert.Equal(20, await f.Db.입고상품.SumAsync(x => x.입고수량));
    }

    [Fact]
    public async Task 현장관리자는상품과도착조건만보고당사자대화나공급대금을열람하거나승인하지못한다()
    {
        await using var f = await Create(); var site = (await f.Sites())[0]; await SetPolicy(f, site); var plan = await Plan(f, site); plan = await Propose(f, site, plan);
        plan = await f.Use(SellerA).InboundNoteAsync(SellerA, plan.AggregateId, "first", new() { RequestId = Guid.NewGuid(), ExpectedRevision = plan.Revision, TermsVersion = 1, Note = "private supplier note" }, default);
        var manager = new DistributedActor("receiver-manager"); var record = await f.Db.Set<분산거점Record>().SingleAsync(x => x.SiteId == site.SiteId); record.ManagerUserId = manager.UserId; await f.Db.SaveChangesAsync();
        var line = Assert.Single(Assert.Single((await f.Use(manager).WorkspaceAsync(manager, "warehouse", default)).InboundPlans).Lines);
        Assert.Empty(line.InboundCollaboration!.History); Assert.Empty(line.InboundCollaboration.Terms.SupplyPriceOrFreeTerms); Assert.DoesNotContain("consent-inbound", line.AllowedActions!);
        await Error("DistributedInboundPartyRequired", () => f.Use(manager).InboundNoteAsync(manager, plan.AggregateId, "first", new() { RequestId = Guid.NewGuid(), ExpectedRevision = plan.Revision, TermsVersion = 1, Note = "unauthorized" }, default));
        Assert.Empty((await f.Use(DistributedTestFixture.Driver).WorkspaceAsync(DistributedTestFixture.Driver, "driver", default)).InboundPlans);
        Assert.Empty((await f.Use(CustomerA).WorkspaceAsync(CustomerA, "orderer", default)).InboundPlans);
    }

    [Fact]
    public async Task 첫동의뒤정책이바뀌면두번째동의가이전정책동의를재사용하지않는다()
    {
        await using var f = await Create(); var site = (await f.Sites())[0]; await SetPolicy(f, site); var plan = await Plan(f, site);
        plan = await Propose(f, site, plan); plan = await Consent(f, GoodsOwner, plan, 1); await SetPolicy(f, site, 30);
        await Error("DistributedInboundPolicyChanged", () => Consent(f, Operator, plan, 1));
        plan = await Propose(f, site, plan); Assert.False((await Line(f, GoodsOwner, plan.AggregateId)).InboundCollaboration!.GoodsOwnerConsented);
    }

    [Fact]
    public async Task 부분입고뒤정책변경은실물특성을보존한채남은수량만새판본재협의할수있다()
    {
        await using var f = await Create(); var site = (await f.Sites())[0]; await SetPolicy(f, site, 5); var plan = await Plan(f, site);
        plan = await Propose(f, site, plan); plan = await Consent(f, GoodsOwner, plan, 1); plan = await Consent(f, Operator, plan, 1);
        plan = await f.Use(Operator).ReceiveAsync(Operator, plan.AggregateId, await f.Proof(Operator, plan.AggregateId, Receive(site, plan, 2)), default);
        var original = (await f.Db.Set<분산재고LotRecord>().AsNoTracking().SingleAsync()).StorageAgreementSnapshotJson;
        await SetPolicy(f, site, 5); var terms = await Terms(f, site);
        terms.Goods.CapacityPerItem = 2;
        await Error("DistributedInboundReceivedGoodsImmutable", () => Propose(f, site, plan, terms));
        terms.Goods.CapacityPerItem = 1; plan = await Propose(f, site, plan, terms);
        plan = await Consent(f, GoodsOwner, plan, 2); plan = await Consent(f, Operator, plan, 2);
        // 현재 실보관 2개 + 남은 3개만 합산하므로 5칸에서 재승인됩니다.
        plan = await f.Use(Operator).ReceiveAsync(Operator, plan.AggregateId, await f.Proof(Operator, plan.AggregateId, Receive(site, plan, 3)), default);
        var lot = await f.Db.Set<분산재고LotRecord>().AsNoTracking().SingleAsync(); Assert.Equal(5, lot.ReceivedQuantity); Assert.Equal("Received", plan.StatusCode);
        Assert.Contains(JsonSerializer.Deserialize<JsonElement>(original)[0].GetRawText(), lot.StorageAgreementSnapshotJson);
        await Error("DistributedInboundAlreadyReceived", () => Propose(f, site, plan, terms));
        Assert.DoesNotContain("propose-inbound", (await Line(f, GoodsOwner, plan.AggregateId)).AllowedActions!);
    }

    [Fact]
    public async Task 부분수령사진수량으로예정을합의축소하면추가작업없이계획과입고상태만정합한다()
    {
        await using var f = await Create(); var site = (await f.Sites())[0]; await SetPolicy(f, site, 5); var plan = await Plan(f, site);
        plan = await Propose(f, site, plan); plan = await Consent(f, GoodsOwner, plan, 1); plan = await Consent(f, Operator, plan, 1);
        plan = await f.Use(Operator).ReceiveAsync(Operator, plan.AggregateId, await f.Proof(Operator, plan.AggregateId, Receive(site, plan, 2)), default);
        var count = await f.Db.Set<분산물류CommandReceipt>().CountAsync(x => x.Kind == "Receive");
        plan = await Propose(f, site, plan, await Terms(f, site, 2)); plan = await Consent(f, GoodsOwner, plan, 2); plan = await Consent(f, Operator, plan, 2);
        var lot = await f.Db.Set<분산재고LotRecord>().AsNoTracking().SingleAsync(); var target = await f.Db.입고요청.AsNoTracking().SingleAsync(x => x.Id == lot.TargetInboundId);
        Assert.Equal("Received", (await f.Db.Set<분산입고계획Record>().AsNoTracking().SingleAsync()).StatusCode);
        Assert.Equal("입고완료", target.상태); Assert.Equal(2, target.예정수량); Assert.NotNull(target.입고완료일시);
        Assert.Equal(2, lot.ReceivedQuantity); Assert.Equal(count, await f.Db.Set<분산물류CommandReceipt>().CountAsync(x => x.Kind == "Receive"));
        var reopening = await Terms(f, site, 5);
        await Error("DistributedInboundAlreadyReceived", () => Propose(f, site, plan, reopening));
    }

    [Fact]
    public async Task 실제물품소유자를공급자로선택할수있지만무관한제삼자는허용하지않는다()
    {
        await using var f = await Create(); var site = (await f.Sites())[0]; await SetPolicy(f, site); var plan = await Plan(f, site);
        var terms = await Terms(f, site); terms.SupplierUserId = "foreign-supplier";
        await Error("DistributedInboundTermsInvalid", () => Propose(f, site, plan, terms));
        terms.SupplierUserId = GoodsOwner.UserId; plan = await Propose(f, site, plan, terms);
        plan = await Consent(f, GoodsOwner, plan, 1); plan = await Consent(f, Operator, plan, 1);
        var confirmed = (await Line(f, GoodsOwner, plan.AggregateId)).InboundCollaboration!;
        Assert.Equal(GoodsOwner.UserId, confirmed.Terms.SupplierUserId); Assert.Equal(SellerA.UserId, confirmed.Terms.SellerUserId);
    }

    [Fact]
    public async Task 만료용량초과와메모제한은동의철회나미수령취소를막지않는다()
    {
        await using var f = await Create(); var site = (await f.Sites())[0]; await SetPolicy(f, site); var plan = await Plan(f, site);
        plan = await Propose(f, site, plan); plan = await Consent(f, GoodsOwner, plan, 1); plan = await Consent(f, Operator, plan, 1);
        var lot = await f.Db.Set<분산재고LotRecord>().SingleAsync(); var state = JsonSerializer.Deserialize<DistributedInboundCollaborationDto>(lot.InboundCollaborationJson!, Json)!;
        state.History = [.. state.History, .. Enumerable.Range(0, 50).Select(_ => new DistributedInboundCollaborationHistoryDto("Note", SellerA.UserId, 1, f.Clock.Now, "synthetic-note"))];
        lot.InboundCollaborationJson = JsonSerializer.Serialize(state, Json); await f.Db.SaveChangesAsync(); f.Clock.Now = f.Clock.Now.AddDays(5);
        plan = await f.Use(GoodsOwner).InboundConsentAsync(GoodsOwner, plan.AggregateId, "first", new() { RequestId = Guid.NewGuid(), ExpectedRevision = plan.Revision, TermsVersion = 1, Granted = false }, default);
        Assert.Equal("Proposed", plan.StatusCode);
        plan = await f.Use(Operator).InboundDecisionAsync(Operator, plan.AggregateId, "first", new() { RequestId = Guid.NewGuid(), ExpectedRevision = plan.Revision, TermsVersion = 1, Action = "Cancel", Reason = "출발 전 취소" }, default);
        Assert.Equal("Cancelled", plan.StatusCode);
    }

    [Fact]
    public async Task 같은요청번호를다른입고행에보내면성공재전송으로오인하지않는다()
    {
        await using var f = await Create(); var site = (await f.Sites())[0]; await SetPolicy(f, site);
        var plan = await f.Use(SellerA).InboundAsync(SellerA, new() { RequestId = Guid.NewGuid(), SourceReference = "두 행 협의",
            Lines = new[] { "a", "b" }.Select(key => new DistributedInboundLineRequest { LineKey = key, SourceInboundId = 101, SiteId = site.SiteId,
                LocationId = site.Locations.Single().LocationId, Sku = "same-sku", ProductName = "물품", LotCode = key, ExpectedQuantity = 2 }).ToArray() }, default);
        var request = new DistributedInboundNoteRequest { RequestId = Guid.NewGuid(), ExpectedRevision = plan.Revision, TermsVersion = 0, Note = "first line only" };
        await f.Use(SellerA).InboundNoteAsync(SellerA, plan.AggregateId, "a", request, default);
        Assert.True((await f.Use(SellerA).InboundNoteAsync(SellerA, plan.AggregateId, "a", request, default)).Replayed);
        await Error("DistributedRequestContentConflict", () => f.Use(SellerA).InboundNoteAsync(SellerA, plan.AggregateId, "b", request, default));
        var untouched = await f.Db.Set<분산재고LotRecord>().AsNoTracking().SingleAsync(x => x.LineKey == "b");
        Assert.Empty(JsonSerializer.Deserialize<DistributedInboundCollaborationDto>(untouched.InboundCollaborationJson!, Json)!.History);
    }

    [Fact]
    public async Task 이미확정된원천수량을넘는확대제안은최종동의때원천재검사로막는다()
    {
        await using var f = await Create(); var site = (await f.Sites())[0]; await SetPolicy(f, site, 100); var plan = await Plan(f, site, 5);
        plan = await Propose(f, site, plan, await Terms(f, site, 11)); plan = await Consent(f, GoodsOwner, plan, 1);
        await Error("DistributedInboundSourceOverallocated", () => Consent(f, Operator, plan, 1));
        Assert.Equal(5, (await f.Db.Set<분산재고LotRecord>().AsNoTracking().SingleAsync()).ExpectedQuantity);
    }

    private static DistributedSiteAcceptancePolicy Policy(decimal capacity = 20) => new() { AllowedCategoryCodes = ["industrial"], DeniedCategoryCodes = ["agricultural"], AllowFood = false,
        AllowedPackagingCodes = ["boxed", "sealed"], AllowedTemperatureCodes = ["ambient"], CapacityUnit = "칸", MaximumCapacity = capacity, MaximumStorageDays = 2, AccessHours = "확인한 입고 시간" };
    private static Task<DistributedSiteDto> SiteView(DistributedTestFixture f, DistributedSiteDto site) => ReadSite(f, site.SiteId);
    private static async Task<DistributedSiteDto> ReadSite(DistributedTestFixture f, string id) => (await f.Use(Operator).WorkspaceAsync(Operator, "warehouse", default)).Sites.Single(x => x.SiteId == id);
    private static async Task SetPolicy(DistributedTestFixture f, DistributedSiteDto site, decimal capacity = 20)
        => await f.Use(Operator).SiteAcceptancePolicyAsync(Operator, site.SiteId, new() { RequestId = Guid.NewGuid(), ExpectedRevision = (await SiteView(f, site)).Revision, Policy = Policy(capacity) }, default);
    private static Task<DistributedCommandResponse> Plan(DistributedTestFixture f, DistributedSiteDto site, int quantity = 5, DistributedActor? seller = null, bool newSource = false)
    {
        seller ??= SellerA;
        return f.Use(seller).InboundAsync(seller, new() { RequestId = Guid.NewGuid(), SourceReference = "합성 공급 제안",
            NewSource = newSource ? new() { ProductName = "새 원천 물품", Sku = "same-sku", ExpectedQuantity = quantity, StockOwnerIsCurrentSellerConfirmed = true, OwnershipEvidenceReference = "synthetic-owner-proof" } : null,
            Lines = [new() { LineKey = "first", SourceInboundId = newSource ? 0 : seller == SellerA ? 101 : 102, SiteId = site.SiteId, LocationId = site.Locations.Single().LocationId,
                Sku = "same-sku", ProductName = "합성 물품", LotCode = Guid.NewGuid().ToString("N"), ExpectedQuantity = quantity, StockOwnerIsCurrentSellerConfirmed = seller == SellerB, OwnershipEvidenceReference = "synthetic-owner-proof" }] }, default);
    }
    private static async Task<DistributedInboundCollaborationTerms> Terms(DistributedTestFixture f, DistributedSiteDto site, int quantity = 5, DistributedActor? seller = null, string? ownerId = null)
    {
        seller ??= SellerA; ownerId ??= seller == SellerA ? GoodsOwner.UserId : seller.UserId;
        await f.EnsureAgreement(site, new(ownerId), "same-sku");
        var agreement = (await f.Use(new(ownerId)).WorkspaceAsync(new(ownerId), "shipper", default)).StorageAgreements.Single(x => x.SiteId == site.SiteId && x.GoodsOwnerUserId == ownerId);
        return new() { AgreementId = agreement.AgreementId, AgreementTermsVersion = agreement.TermsVersion, SupplierUserId = seller.UserId, SellerUserId = seller.UserId, GoodsOwnerUserId = ownerId,
            SupplierTypeCode = "factory", Quantity = quantity, ArrivalStartUtc = f.Clock.Now.AddMinutes(-1), ArrivalEndUtc = f.Clock.Now.AddHours(2), StorageEndsAtUtc = f.Clock.Now.AddDays(1), AccessHoursConfirmed = true,
            SupplyPriceOrFreeTerms = "synthetic-free-private-price", SupplierResponsibilityTerms = "공급자가 소유 관계와 도착을 확인", ReturnTerms = "미판매 상품 별도 회수 협의",
            Goods = new() { CategoryCodes = ["industrial"], IsFood = false, IsProcessed = true, PackagingCode = "boxed", TemperatureCode = "ambient", CapacityUnit = "칸", CapacityPerItem = 1 } };
    }
    private static async Task<DistributedCommandResponse> Propose(DistributedTestFixture f, DistributedSiteDto site, DistributedCommandResponse plan, DistributedInboundCollaborationTerms? terms = null, DistributedActor? actor = null)
    { actor ??= SellerA; return await f.Use(actor).InboundProposalAsync(actor, plan.AggregateId, "first", new() { RequestId = Guid.NewGuid(), ExpectedRevision = plan.Revision, Terms = terms ?? await Terms(f, site) }, default); }
    private static Task<DistributedCommandResponse> Consent(DistributedTestFixture f, DistributedActor actor, DistributedCommandResponse plan, int version)
        => f.Use(actor).InboundConsentAsync(actor, plan.AggregateId, "first", new() { RequestId = Guid.NewGuid(), ExpectedRevision = plan.Revision, TermsVersion = version, Granted = true }, default);
    private static async Task<DistributedInboundLineDto> Line(DistributedTestFixture f, DistributedActor actor, string id)
        => (await f.Use(actor).WorkspaceAsync(actor, actor == Operator ? "warehouse" : "shipper", default)).InboundPlans.Single(x => x.PlanId == id).Lines.Single();
    private static DistributedInboundReceiptRequest Receive(DistributedSiteDto site, DistributedCommandResponse plan, int quantity)
        => new() { RequestId = Guid.NewGuid(), ExpectedRevision = plan.Revision, LineKey = "first", LocationBarcode = site.Locations.Single().Barcode, Sku = "same-sku", Quantity = quantity };
    private static async Task Error(string code, Func<Task> action) => Assert.Equal(code, (await Assert.ThrowsAsync<DistributedFulfillmentException>(action)).Code);
}
