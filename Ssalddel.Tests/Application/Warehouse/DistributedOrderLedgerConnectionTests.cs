using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Ssalddel.Application.Warehouse;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Services.Community;
using 살뜰.도메인.창고;
using 살뜰.Services.Options;
using static Ssalddel.Tests.Application.Warehouse.DistributedTestFixture;

namespace Ssalddel.Tests.Application.Warehouse;

/// <summary>실제 분산 SQL 업무와 기존 원장 동기화/포함 서비스를 사용합니다. Mongo 경계만 격리 CAS 저장소입니다.</summary>
public sealed class DistributedOrderLedgerConnectionTests
{
    [Fact]
    public async Task 원주문연결실패후_같은접수를재실행해도_재고를다시예약하지않고_연결만복구한다()
    {
        await using var f = await Setup.Create(); f.Store.FailRootWrites = 1;
        var result = await f.Add();
        Assert.Equal("Pending", result.OrderLedgerConnectionStatusCode);
        Assert.Equal(1, await f.F.Db.Set<분산배송주문Record>().CountAsync());
        Assert.Equal(1, await f.F.Db.Set<분산재고예약Record>().CountAsync());
        Assert.Equal(1, (await f.F.Db.입고상품.AsNoTracking().SingleAsync(x => x.Id == f.Lot.InboundItemId)).예약수량);
        var beforeRead = f.Store.WriteCount;
        var pending = Assert.Single((await f.Use(CustomerA).WaveAsync(CustomerA, f.Wave.AggregateId, "orderer", default)).Orders);
        Assert.Equal("Pending", pending.OrderLedgerConnectionStatusCode); Assert.Equal(beforeRead, f.Store.WriteCount);
        var replay = await f.Add();
        Assert.True(replay.Replayed); Assert.Equal(result.Revision, replay.Revision); Assert.Equal("Connected", replay.OrderLedgerConnectionStatusCode);
        Assert.Equal(1, await f.F.Db.Set<분산재고예약Record>().CountAsync()); Assert.Equal(1, await f.F.Db.출고예정.CountAsync());
        var order = await f.F.Db.Set<분산배송주문Record>().SingleAsync();
        var root = (await f.Store.원장조회Async(f.RootId))!;
        Assert.Equal(2, root.포함원장목록.Count); Assert.All(root.포함원장목록, x => Assert.True(x.필수여부));
        Assert.Contains(root.포함원장목록, x => x.원장Id == "warehouse-outbound:" + order.OrderId);
        Assert.Contains(root.포함원장목록, x => x.원장Id == "transport:cargo-a");
        var beforeRevision = root.Revision;
        await f.Add(); Assert.Equal(beforeRevision, (await f.Store.원장조회Async(f.RootId))!.Revision);
        Assert.Equal(f.RootId, (await f.Adapter.FindForChildAsync(CustomerA.UserId, f.RootId, "transport:cargo-a", default))!.RootOrderLedgerId);
        Assert.Null(await f.Adapter.FindForChildAsync(CustomerB.UserId, f.RootId, "transport:cargo-a", default));
        Assert.Null(await f.Adapter.FindForChildAsync(CustomerA.UserId, f.RootId, "transport:foreign", default));
        Assert.Null(Assert.Single((await f.Use(Operator).WaveAsync(Operator, f.Wave.AggregateId, "warehouse", default)).Orders).RootOrderLedgerId);
    }

    [Theory]
    [InlineData("WrongCustomer", "DistributedRootOrderLedgerPartyMismatch")]
    [InlineData("MissingSeller", "DistributedRootOrderLedgerPartyMismatch")]
    [InlineData("StaleRevision", "DistributedRootOrderLedgerRevisionConflict")]
    [InlineData("FoodRoot", "DistributedRootOrderLedgerKindInvalid")]
    [InlineData("ClosedRoot", "DistributedRootOrderLedgerClosed")]
    [InlineData("CompletedRoot", "DistributedRootOrderLedgerClosed")]
    [InlineData("WithdrawnCreator", "DistributedRootOrderLedgerPartyMismatch")]
    [InlineData("WithdrawnSeller", "DistributedRootOrderLedgerPartyMismatch")]
    public async Task 실제당사자와원주문판본이맞지않으면_SQL예약전거절한다(string change, string code)
    {
        await using var f = await Setup.Create();
        Assert.Contains((await f.Use(SellerA).RootOrderCandidatesAsync(SellerA, f.Wave.AggregateId, "cargo-a", "shipper", default)).Candidates,
            x => x.RootOrderLedgerId == f.RootId);
        var root = (await f.Store.원장조회Async(f.RootId))!;
        if (change == "WrongCustomer") { root.생성자UserId = CustomerB.UserId; root.참여자목록 = [Party(CustomerB, "주문자"), Party(SellerA, "판매자")]; }
        if (change == "MissingSeller") root.참여자목록 = [Party(CustomerA, "주문자")];
        if (change == "StaleRevision") root.Revision++;
        if (change == "FoodRoot") root.원장템플릿Key = CommunityLedgerTemplateKeys.FoodOrder;
        if (change == "ClosedRoot") root.상태 = 커뮤니티원장상태.닫힘;
        if (change == "CompletedRoot") root.상태 = 커뮤니티원장상태.완료;
        if (change == "WithdrawnCreator") root.참여자목록 = [new() { UserId = CustomerA.UserId, RoleLabel = "주문자", ParticipationState = "철회" }, Party(SellerA, "판매자")];
        if (change == "WithdrawnSeller") root.참여자목록 = [Party(CustomerA, "주문자"), new() { UserId = SellerA.UserId, RoleLabel = "판매자", ParticipationState = "철회" }];
        f.Store.Put(root);
        var error = await Assert.ThrowsAsync<DistributedFulfillmentException>(() => f.Add()); Assert.Equal(code, error.Code);
        Assert.Empty(await f.F.Db.Set<분산배송주문Record>().ToArrayAsync()); Assert.Empty(await f.F.Db.출고예정.ToArrayAsync());
        Assert.Equal(0, f.Store.WriteCount);
    }

    [Fact]
    public async Task 원주문후보는_선택한화물의현재판매자와고객의_열린원장만조회한다()
    {
        await using var f = await Setup.Create();
        var allowed = Root("order:allowed"); allowed.제목 = "이번 배송의 실제 주문"; allowed.상태 = 커뮤니티원장상태.진행중; f.Store.Put(allowed);
        var closed = Root("order:closed"); closed.상태 = 커뮤니티원장상태.닫힘; f.Store.Put(closed);
        var completed = Root("order:completed"); completed.상태 = 커뮤니티원장상태.완료; f.Store.Put(completed);
        var foreign = Root("order:foreign"); foreign.생성자UserId = CustomerB.UserId; foreign.참여자목록 = [Party(CustomerB, "주문자"), Party(SellerB, "판매자")]; f.Store.Put(foreign);
        var withdrawn = Root("order:withdrawn"); withdrawn.참여자목록 = [Party(CustomerA, "주문자"), new() { UserId = SellerA.UserId, RoleLabel = "판매자", ParticipationState = "철회" }]; f.Store.Put(withdrawn);
        var wrongKind = Root("order:food"); wrongKind.원장템플릿Key = CommunityLedgerTemplateKeys.FoodOrder; f.Store.Put(wrongKind);
        var result = await f.Use(SellerA).RootOrderCandidatesAsync(SellerA, f.Wave.AggregateId, "cargo-a", "shipper", default);
        Assert.Equal(SellerA.UserId, result.CurrentUserId); Assert.Equal("cargo-a", result.CargoRequestId); Assert.Equal(f.Wave.Revision, result.WaveRevision);
        Assert.Equal(new[] { f.RootId, allowed.원장Id }.Order(), result.Candidates.Select(x => x.RootOrderLedgerId).Order());
        Assert.Equal(allowed.Revision, result.Candidates.Single(x => x.RootOrderLedgerId == allowed.원장Id).Revision);
        Assert.Equal(SellerA.UserId, f.Store.LastQuery!.접근UserId); Assert.Equal("platform", f.Store.LastQuery.커뮤니티Id);
        Assert.Equal(0, f.Store.WriteCount); Assert.Empty(await f.F.Db.Set<분산재고예약Record>().ToArrayAsync());
    }

    [Fact]
    public async Task 원주문후보는_다른판매자와고객_닫힌묶음_미확인조회결과를_대체하지않는다()
    {
        await using var f = await Setup.Create();
        Assert.Equal("DistributedCargoOwnerRequired", (await Assert.ThrowsAsync<DistributedFulfillmentException>(() =>
            f.Use(SellerB).RootOrderCandidatesAsync(SellerB, f.Wave.AggregateId, "cargo-a", "shipper", default))).Code);
        Assert.Equal("DistributedRoleScopeInvalid", (await Assert.ThrowsAsync<DistributedFulfillmentException>(() =>
            f.Use(CustomerA).RootOrderCandidatesAsync(CustomerA, f.Wave.AggregateId, "cargo-a", "orderer", default))).Code);
        await f.F.Db.화주운송의뢰.Where(x => x.의뢰Id == "cargo-a").ExecuteUpdateAsync(s => s.SetProperty(x => x.주문자UserId, CustomerB.UserId));
        Assert.Empty((await f.Use(SellerA).RootOrderCandidatesAsync(SellerA, f.Wave.AggregateId, "cargo-a", "shipper", default)).Candidates);
        await f.F.Db.화주운송의뢰.Where(x => x.의뢰Id == "cargo-a").ExecuteUpdateAsync(s => s.SetProperty(x => x.주문자UserId, CustomerA.UserId));
        f.Store.FailLists = true;
        await Assert.ThrowsAsync<IOException>(() => f.Use(SellerA).RootOrderCandidatesAsync(SellerA, f.Wave.AggregateId, "cargo-a", "shipper", default));
        f.Store.FailLists = false;
        await f.F.Db.Set<분산배송WaveRecord>().Where(x => x.WaveId == f.Wave.AggregateId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.StatusCode, "Closed"));
        Assert.Equal("DistributedCutoffPassed", (await Assert.ThrowsAsync<DistributedFulfillmentException>(() =>
            f.Use(SellerA).RootOrderCandidatesAsync(SellerA, f.Wave.AggregateId, "cargo-a", "shipper", default))).Code);
        Assert.Empty(await f.F.Db.Set<분산배송주문Record>().ToArrayAsync());
    }

    [Fact]
    public async Task 연결요청은_선택한원주문판본과멱등입력을검사하고_다른원주문으로바꾸지않는다()
    {
        await using var f = await Setup.Create(); f.Request.RootOrderLedgerId = null; f.Request.ExpectedRootRevision = null;
        await f.Add(); var order = await f.F.Db.Set<분산배송주문Record>().SingleAsync();
        var revision = (await f.F.Db.Set<분산배송WaveRecord>().SingleAsync()).Revision;
        var command = new DistributedOrderLedgerLinkRequest { RequestId = Guid.NewGuid(), ExpectedRevision = revision, RootOrderLedgerId = f.RootId, ExpectedRootRevision = 1 };
        var linked = await f.Use(CustomerA).LinkOrderLedgerAsync(CustomerA, f.Wave.AggregateId, order.OrderId, command, default);
        Assert.Equal("Connected", linked.OrderLedgerConnectionStatusCode);
        Assert.True((await f.Use(CustomerA).LinkOrderLedgerAsync(CustomerA, f.Wave.AggregateId, order.OrderId, command, default)).Replayed);
        command.ExpectedRootRevision++;
        Assert.Equal("DistributedRequestContentConflict", (await Assert.ThrowsAsync<DistributedFulfillmentException>(() =>
            f.Use(CustomerA).LinkOrderLedgerAsync(CustomerA, f.Wave.AggregateId, order.OrderId, command, default))).Code);
        var secondRoot = Root("order:second"); f.Store.Put(secondRoot);
        var replacement = new DistributedOrderLedgerLinkRequest { RequestId = Guid.NewGuid(), ExpectedRevision = linked.Revision,
            RootOrderLedgerId = secondRoot.원장Id, ExpectedRootRevision = secondRoot.Revision };
        Assert.Equal("DistributedRootOrderLedgerReplacementForbidden", (await Assert.ThrowsAsync<DistributedFulfillmentException>(() =>
            f.Use(CustomerA).LinkOrderLedgerAsync(CustomerA, f.Wave.AggregateId, order.OrderId, replacement, default))).Code);
        Assert.Equal("DistributedOrderLedgerPartyRequired", (await Assert.ThrowsAsync<DistributedFulfillmentException>(() =>
            f.Use(CustomerB).LinkOrderLedgerAsync(CustomerB, f.Wave.AggregateId, order.OrderId, replacement, default))).Code);
        Assert.Equal(1, await f.F.Db.Set<분산재고예약Record>().CountAsync());
    }

    [Theory]
    [InlineData("Seller")]
    [InlineData("Customer")]
    public async Task 연결접수후_현재루트권한이철회되면_재실행과조회는차단을명시하고_새연결정보를공개하지않는다(string withdrawn)
    {
        await using var f = await Setup.Create(); f.Store.FailRootWrites = 1; await f.Add();
        var root = (await f.Store.원장조회Async(f.RootId))!;
        root.생성자UserId = withdrawn == "Customer" ? SellerA.UserId : CustomerA.UserId;
        root.참여자목록 = [new() { UserId = CustomerA.UserId, RoleLabel = "주문자", ParticipationState = withdrawn == "Customer" ? "철회" : "참여중" },
            new() { UserId = SellerA.UserId, RoleLabel = "판매자", ParticipationState = withdrawn == "Seller" ? "철회" : "참여중" }];
        root.Revision++; f.Store.Put(root); var writes = f.Store.WriteCount;
        var replay = await f.Add(); Assert.True(replay.Replayed); Assert.Equal("Blocked", replay.OrderLedgerConnectionStatusCode);
        Assert.Equal("DistributedRootOrderLedgerPartyMismatch", replay.OrderLedgerConnectionErrorCode); Assert.Equal(writes, f.Store.WriteCount);
        var read = Assert.Single((await f.Use(SellerA).WaveAsync(SellerA, f.Wave.AggregateId, "shipper", default)).Orders);
        Assert.Equal("Blocked", read.OrderLedgerConnectionStatusCode); Assert.Null(read.RootOrderLedgerId);
        Assert.DoesNotContain("retry-order-ledger", read.AllowedActions);
        Assert.Null(await f.Adapter.FindForChildAsync(SellerA.UserId, f.RootId, "transport:cargo-a", default));
        Assert.Equal(1, await f.F.Db.Set<분산재고예약Record>().CountAsync());
    }

    [Fact]
    public async Task 접수후원주문내용이변경되면_자동재시도는멈추고_현재판본명시확인으로만연결을재개한다()
    {
        await using var f = await Setup.Create(); f.Store.FailRootWrites = 1; var first = await f.Add();
        var root = (await f.Store.원장조회Async(f.RootId))!;
        root.블록목록 = [new() { BlockId = "item", BlockType = CommunityLedgerBlockTypes.Item, Data = new Dictionary<string, string> { ["조건"] = "변경된 조건" } }];
        root.Revision++; f.Store.Put(root); var writes = f.Store.WriteCount;
        var pending = await f.Add(); Assert.Equal("DistributedRootOrderLedgerChanged", pending.OrderLedgerConnectionErrorCode);
        Assert.Equal(writes, f.Store.WriteCount);
        var order = await f.F.Db.Set<분산배송주문Record>().SingleAsync();
        var resumed = await f.Use(CustomerA).LinkOrderLedgerAsync(CustomerA, f.Wave.AggregateId, order.OrderId, new()
        { RequestId = Guid.NewGuid(), ExpectedRevision = first.Revision, RootOrderLedgerId = f.RootId, ExpectedRootRevision = root.Revision }, default);
        Assert.Equal("Connected", resumed.OrderLedgerConnectionStatusCode); Assert.Equal(1, await f.F.Db.Set<분산재고예약Record>().CountAsync());
        Assert.Equal("Connected", (await f.Add()).OrderLedgerConnectionStatusCode);
    }

    [Fact]
    public async Task 표시참조가같아도_새출고식별자는분산주문별로분리되고_예약당시합의는재계획에도남는다()
    {
        await using var f = await Setup.Create(); await f.Add();
        var wave = await f.F.Db.Set<분산배송WaveRecord>().AsNoTracking().SingleAsync();
        var initial = JsonNode.Parse(wave.RouteEvidenceJson)!["reservationTerms"]!.ToJsonString();
        var lotSnapshot = (await f.F.Db.Set<분산재고LotRecord>().AsNoTracking().SingleAsync(x => x.LotId == f.Lot.LotId)).StorageAgreementSnapshotJson;
        await f.F.Cargo("cargo-b", SellerA, CustomerA, 1);
        var second = new DistributedOrderRequest { RequestId = Guid.NewGuid(), ExpectedRevision = wave.Revision, OrderReference = f.Request.OrderReference,
            CargoRequestId = "cargo-b", Lines = f.Request.Lines, RootOrderLedgerId = f.RootId, ExpectedRootRevision = (await f.Store.원장조회Async(f.RootId))!.Revision };
        var ack = await f.Use(SellerA).OrderAsync(SellerA, f.Wave.AggregateId, second, default);
        var outbounds = await f.F.Db.출고예정.AsNoTracking().ToArrayAsync();
        Assert.Equal(2, outbounds.Select(x => x.주문참조번호).Distinct().Count());
        Assert.All(outbounds, x => Assert.NotEqual(f.Request.OrderReference, x.주문참조번호));
        ack = await f.Use(Operator).CloseAsync(Operator, f.Wave.AggregateId, new() { RequestId = Guid.NewGuid(), ExpectedRevision = ack.Revision }, default);
        var beforePlan = JsonNode.Parse((await f.F.Db.Set<분산배송WaveRecord>().AsNoTracking().SingleAsync()).RouteEvidenceJson)!;
        var agreement = await f.F.Db.Set<분산보관합의Record>().AsNoTracking().SingleAsync(x => x.LocationId == f.Lot.LocationId && x.GoodsOwnerUserId == f.Lot.OwnerUserId);
        var changedTerms = JsonSerializer.Deserialize<DistributedStorageTerms>(agreement.TermsJson, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        changedTerms.PriceOrFreeTerms = "예약 뒤 합의한 새로운 조건";
        var termsAck = await f.Use(Operator).AgreementTermsAsync(Operator, agreement.AgreementId, new()
        { RequestId = Guid.NewGuid(), ExpectedRevision = agreement.Revision, LocationId = agreement.LocationId, GoodsOwnerUserId = agreement.GoodsOwnerUserId, Terms = changedTerms }, default);
        termsAck = await f.Use(Operator).AgreementConsentAsync(Operator, agreement.AgreementId, new()
        { RequestId = Guid.NewGuid(), ExpectedRevision = termsAck.Revision, TermsVersion = 2, Granted = true }, default);
        var goodsOwner = new DistributedActor(agreement.GoodsOwnerUserId);
        await f.Use(goodsOwner).AgreementConsentAsync(goodsOwner, agreement.AgreementId, new()
        { RequestId = Guid.NewGuid(), ExpectedRevision = termsAck.Revision, TermsVersion = 2, Granted = true }, default);
        f.F.RefreshLocation(); ack = await f.Use(Operator).PlanAsync(Operator, f.Wave.AggregateId,
            new() { RequestId = Guid.NewGuid(), ExpectedRevision = ack.Revision, DriverId = DistributedTestFixture.Driver.UserId }, default);
        var afterPlan = JsonNode.Parse((await f.F.Db.Set<분산배송WaveRecord>().AsNoTracking().SingleAsync()).RouteEvidenceJson)!;
        Assert.Equal(beforePlan["reservationTerms"]!.ToJsonString(), afterPlan["reservationTerms"]!.ToJsonString());
        Assert.Equal(beforePlan["orderLedgerConnections"]!.ToJsonString(), afterPlan["orderLedgerConnections"]!.ToJsonString());
        var originalReservationId = JsonNode.Parse(initial)!.AsObject().Single().Key;
        var snapshot = afterPlan["reservationTerms"]![originalReservationId]!;
        Assert.Equal(1, snapshot["termsVersion"]!.GetValue<int>()); Assert.False(string.IsNullOrWhiteSpace(snapshot["termsJson"]!.GetValue<string>()));
        Assert.Equal(lotSnapshot, (await f.F.Db.Set<분산재고LotRecord>().AsNoTracking().SingleAsync(x => x.LotId == f.Lot.LotId)).StorageAgreementSnapshotJson);
    }

    [Fact]
    public async Task 기존접수형식은_null연결필드를직렬화하지않아_기존영수증입력해시를보존한다()
    {
        await using var f = await Setup.Create(); f.Request.RootOrderLedgerId = null; f.Request.ExpectedRootRevision = null;
        var json = JsonSerializer.Serialize(f.Request, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.DoesNotContain("rootOrderLedgerId", json); Assert.DoesNotContain("expectedRootRevision", json);
        var result = await f.Add(); Assert.Null(result.OrderLedgerConnectionStatusCode);
        var oldRequest = JsonSerializer.Deserialize<DistributedOrderRequest>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.True((await f.Use(SellerA).OrderAsync(SellerA, f.Wave.AggregateId, oldRequest, default)).Replayed);
        Assert.Equal(0, f.Store.WriteCount); Assert.Equal(1, await f.F.Db.Set<분산재고예약Record>().CountAsync());
    }

    [Fact]
    public async Task 입력전연결기능미등록과실행게이트는_새재고업무를허용하지않는다()
    {
        await using var f = await Setup.Create();
        Assert.Equal("DistributedOrderLedgerAdapterUnavailable", (await Assert.ThrowsAsync<DistributedFulfillmentException>(() =>
            f.F.Use(SellerA).OrderAsync(SellerA, f.Wave.AggregateId, f.Request, default))).Code);
        var gated = await Assert.ThrowsAsync<DistributedFulfillmentException>(() => f.F.Use(SellerA, enabled: false, orderLedgers: f.Adapter)
            .OrderAsync(SellerA, f.Wave.AggregateId, f.Request, default));
        Assert.Equal(0, f.Store.WriteCount); Assert.Empty(await f.F.Db.Set<분산재고예약Record>().ToArrayAsync());
        Assert.Equal("DistributedFulfillmentDisabled", gated.Code);
    }

    [Fact]
    public async Task SQL수량확정에실패하면_Mongo연결계획과하위원장을발행하지않는다()
    {
        await using var f = await Setup.Create();
        await f.F.Db.입고상품.Where(x => x.Id == f.Lot.InboundItemId).ExecuteUpdateAsync(s => s.SetProperty(x => x.가용수량, 0));
        Assert.Equal("DistributedStockInsufficientOrChanged", (await Assert.ThrowsAsync<DistributedFulfillmentException>(() => f.Add())).Code);
        Assert.Equal(0, f.Store.WriteCount); Assert.Empty(await f.F.Db.Set<분산배송주문Record>().ToArrayAsync());
        var evidence = JsonNode.Parse((await f.F.Db.Set<분산배송WaveRecord>().AsNoTracking().SingleAsync()).RouteEvidenceJson)!;
        Assert.Null(evidence["orderLedgerConnections"]); Assert.Null(evidence["reservationTerms"]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 기존출고참조는보존하고_참조가충돌하면_과거원장을덮어쓰지않는다(bool ambiguous)
    {
        await using var f = await Setup.Create();
        var item = await f.F.Db.입고상품.SingleAsync(x => x.Id == f.Lot.InboundItemId);
        item.가용수량--; item.예약수량++;
        var site = await f.F.Db.Set<분산거점Record>().SingleAsync(x => x.SiteId == f.Lot.SiteId);
        var outbound = new 출고예정 { 주문참조번호 = "legacy-reference", 입고상품Id = item.Id, 입고요청Id = item.입고요청Id,
            출고창고Id = site.WarehouseId, 판매자UserId = SellerA.UserId, 주문자UserId = CustomerA.UserId, SKU = item.SKU,
            상품명 = item.상품명, 수량 = 1, 상태 = "준비중", 운송의뢰Id = "cargo-a" };
        f.F.Db.출고예정.Add(outbound);
        if (ambiguous) f.F.Db.출고예정.Add(new() { 주문참조번호 = "legacy-reference", 출고창고Id = site.WarehouseId,
            판매자UserId = SellerB.UserId, 주문자UserId = CustomerB.UserId, 수량 = 1, 상태 = "준비중" });
        await f.F.Db.SaveChangesAsync();
        f.Request.Lines = [new() { LotId = f.Lot.LotId, Quantity = 1, ExistingOutboundPlanId = outbound.Id }];
        var result = await f.Add();
        Assert.Equal("legacy-reference", (await f.F.Db.출고예정.AsNoTracking().SingleAsync(x => x.Id == outbound.Id)).주문참조번호);
        Assert.Equal(1, (await f.F.Db.입고상품.AsNoTracking().SingleAsync(x => x.Id == item.Id)).예약수량);
        if (ambiguous)
        {
            Assert.Equal("Pending", result.OrderLedgerConnectionStatusCode); Assert.Equal("DistributedLegacyOutboundReferenceAmbiguous", result.OrderLedgerConnectionErrorCode);
            Assert.Null(await f.Store.원장조회Async("warehouse-outbound:legacy-reference")); Assert.Equal(0, f.Store.WriteCount);
        }
        else
        {
            Assert.Equal("Connected", result.OrderLedgerConnectionStatusCode);
            Assert.Contains((await f.Store.원장조회Async(f.RootId))!.포함원장목록, x => x.원장Id == "warehouse-outbound:legacy-reference");
        }
    }

    [Fact]
    public async Task 연결된출처가다른실제고객으로바뀌면_재실행으로다른고객정보를투영하지않는다()
    {
        await using var f = await Setup.Create(); await f.Add();
        await f.F.Db.화주운송의뢰.Where(x => x.의뢰Id == "cargo-a").ExecuteUpdateAsync(s => s.SetProperty(x => x.주문자UserId, CustomerB.UserId));
        var writes = f.Store.WriteCount; var result = await f.Add();
        Assert.Equal("DistributedOrderLedgerSourceChanged", result.OrderLedgerConnectionErrorCode); Assert.Equal(writes, f.Store.WriteCount);
        Assert.Null(await f.Adapter.FindForChildAsync(CustomerB.UserId, f.RootId, "transport:cargo-a", default));
    }

    [Theory]
    [InlineData(false, "Simulation", "distributed-fulfillment-r36")]
    [InlineData(true, "Operational", "distributed-fulfillment-r36")]
    [InlineData(true, "Simulation", "other-profile")]
    public async Task 일반주문조회는_분산기능게이트가닫히면_분산SQL에접근하지않는다(bool enabled, string mode, string profile)
    {
        await using var f = await Setup.Create();
        // SQL 명령을 수행할 수 없는 context에서도 닫힌 gate는 null로 끝나야 합니다.
        await using var unavailableDb = f.F.IndependentContext(); await unavailableDb.DisposeAsync();
        var adapter = new 분산주문원장연결Adapter(unavailableDb, f.Store, new 주문원장통합UseCase(f.Store),
            new 음식마트원장Mongo동기화Service(f.Store, unavailableDb, NullLogger<음식마트원장Mongo동기화Service>.Instance),
            new 운송원장Mongo동기화Service(unavailableDb, f.Store, NullLogger<운송원장Mongo동기화Service>.Instance),
            new SsalddelExecutionModePolicy(Options.Create(new SsalddelExecutionOptions { Mode = Enum.Parse<SsalddelExecutionMode>(mode) })),
            Options.Create(new DistributedFulfillmentOptions { Enabled = enabled, Profile = profile }));
        Assert.Null(await adapter.FindForChildAsync(CustomerA.UserId, f.RootId, "transport:cargo-a", default));
    }

    [Fact]
    public async Task 다른당사자가같은GUID로다른주문을연결해도_원본주문재개대상이섞이지않는다()
    {
        await using var f = await Setup.Create(); await f.Add(); await f.F.Cargo("cargo-b", SellerA, CustomerA, 1);
        var revision = (await f.F.Db.Set<분산배송WaveRecord>().AsNoTracking().SingleAsync()).Revision;
        var second = await f.Use(SellerA).OrderAsync(SellerA, f.Wave.AggregateId, new()
        { RequestId = Guid.NewGuid(), ExpectedRevision = revision, OrderReference = "둘째 표시", CargoRequestId = "cargo-b", Lines = f.Request.Lines }, default);
        var otherOrder = await f.F.Db.Set<분산배송주문Record>().SingleAsync(x => x.CargoRequestId == "cargo-b");
        await f.Use(CustomerA).LinkOrderLedgerAsync(CustomerA, f.Wave.AggregateId, otherOrder.OrderId, new()
        { RequestId = f.Request.RequestId, ExpectedRevision = second.Revision, RootOrderLedgerId = f.RootId,
            ExpectedRootRevision = (await f.Store.원장조회Async(f.RootId))!.Revision }, default);
        var replay = await f.Add(); Assert.True(replay.Replayed); Assert.Equal("Connected", replay.OrderLedgerConnectionStatusCode);
        Assert.Equal(2, await f.F.Db.Set<분산재고예약Record>().CountAsync());
    }

    private static 커뮤니티원장참여자Dto Party(DistributedActor actor, string role) => new() { UserId = actor.UserId, RoleLabel = role };
    private static 커뮤니티원장Dto Root(string id) => new()
    {
        원장Id = id, 커뮤니티Id = "platform", 원장템플릿Key = CommunityLedgerTemplateKeys.Order, Revision = 1,
        생성자UserId = CustomerA.UserId, 참여자목록 = [Party(CustomerA, "주문자"), Party(SellerA, "판매자")],
        블록목록 = [new() { BlockId = "item", BlockType = CommunityLedgerBlockTypes.Item,
            Data = new Dictionary<string, string> { ["상품"] = "same-sku", ["수량"] = "1" } }]
    };

    private sealed class Setup(DistributedTestFixture f, MemoryStore store, 분산주문원장연결Adapter adapter,
        DistributedCommandResponse wave, DistributedLotDto lot, DistributedOrderRequest request) : IAsyncDisposable
    {
        public DistributedTestFixture F { get; } = f; public MemoryStore Store { get; } = store;
        public 분산주문원장연결Adapter Adapter { get; } = adapter; public DistributedCommandResponse Wave { get; } = wave;
        public DistributedLotDto Lot { get; } = lot; public DistributedOrderRequest Request { get; } = request;
        public string RootId => "order:customer-a";
        public 분산물류UseCase Use(DistributedActor actor) => F.Use(actor, orderLedgers: Adapter);
        public Task<DistributedCommandResponse> Add() => Use(SellerA).OrderAsync(SellerA, Wave.AggregateId, Request, default);
        public static async Task<Setup> Create()
        {
            var f = await DistributedTestFixture.Create(); var sites = await f.Sites(); var lots = await f.Stock(sites);
            var lot = lots.First(x => x.SellerUserId == SellerA.UserId); await f.Cargo("cargo-a", SellerA, CustomerA, 1);
            var wave = await f.Use(Operator).CreateWaveAsync(Operator, new() { RequestId = Guid.NewGuid(), Label = "연결 시험",
                CutoffUtc = f.Clock.Now.AddMinutes(1), PickupStartUtc = f.Clock.Now.AddMinutes(2), PickupEndUtc = f.Clock.Now.AddHours(3),
                DeliveryStartUtc = f.Clock.Now.AddMinutes(2), DeliveryEndUtc = f.Clock.Now.AddHours(4), PickupServiceMinutes = 1, DeliveryServiceMinutes = 1 }, default);
            var store = new MemoryStore(); store.Put(Root("order:customer-a"));
            var adapter = new 분산주문원장연결Adapter(f.Db, store, new 주문원장통합UseCase(store),
                new 음식마트원장Mongo동기화Service(store, f.Db, NullLogger<음식마트원장Mongo동기화Service>.Instance),
                new 운송원장Mongo동기화Service(f.Db, store, NullLogger<운송원장Mongo동기화Service>.Instance),
                new SsalddelExecutionModePolicy(Options.Create(new SsalddelExecutionOptions { Mode = SsalddelExecutionMode.Simulation })),
                Options.Create(new DistributedFulfillmentOptions { Enabled = true, Profile = DistributedFulfillmentCodes.Profile }));
            return new(f, store, adapter, wave, lot, new() { RequestId = Guid.NewGuid(), ExpectedRevision = wave.Revision,
                OrderReference = "표시 참조", CargoRequestId = "cargo-a", Lines = [new() { LotId = lot.LotId, Quantity = 1 }],
                RootOrderLedgerId = "order:customer-a", ExpectedRootRevision = 1 });
        }
        public ValueTask DisposeAsync() => F.DisposeAsync();
    }

    private sealed class MemoryStore : I커뮤니티원장저장소
    {
        private readonly Dictionary<string, 커뮤니티원장Dto> rows = new(StringComparer.Ordinal);
        public int WriteCount { get; private set; } public int FailRootWrites { get; set; }
        public bool FailLists { get; set; }
        public 커뮤니티원장조회조건? LastQuery { get; private set; }
        private static T Copy<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;
        public void Put(커뮤니티원장Dto value) => rows[value.원장Id] = Copy(value);
        public Task<커뮤니티원장Dto?> 원장조회Async(string id, CancellationToken ct = default)
            => Task.FromResult(rows.TryGetValue(id, out var row) ? Copy(row) : null);
        public Task<IReadOnlyList<커뮤니티원장Dto>> 원장목록조회Async(커뮤니티원장조회조건 query, CancellationToken ct = default)
        {
            LastQuery = query; if (FailLists) throw new IOException("격리된 원주문 조회 실패");
            return Task.FromResult<IReadOnlyList<커뮤니티원장Dto>>(rows.Values.Select(Copy).ToArray());
        }
        public Task<커뮤니티원장Dto?> 원장상태변경Async(커뮤니티원장상태변경요청 request, string actor, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<커뮤니티원장Dto> 원장저장Async(커뮤니티원장저장요청 r, string actor, CancellationToken ct = default)
        {
            WriteCount++;
            if (r.원장Id!.StartsWith("order:", StringComparison.Ordinal) && FailRootWrites > 0) { FailRootWrites--; throw new IOException("격리된 Mongo 쓰기 실패"); }
            rows.TryGetValue(r.원장Id, out var previous);
            if (r.기대Revision is { } expected && expected != (previous?.Revision ?? 0))
                throw new InvalidOperationException("원장의 현재 상태가 다른 요청에서 먼저 변경되었습니다.");
            var saved = new 커뮤니티원장Dto { 원장Id = r.원장Id, 커뮤니티Id = r.커뮤니티Id, 원장템플릿Key = r.원장템플릿Key,
                Revision = (previous?.Revision ?? 0) + 1, 제목 = r.제목, 원함 = r.원함, 상태 = r.상태 ?? 커뮤니티원장상태.초안,
                현재단계Key = r.현재단계Key, 대상OsCode = r.대상OsCode, 대상OsName = r.대상OsName, 생성자UserId = r.생성자UserId,
                생성자표시명 = r.생성자표시명 ?? "시험", 블록목록 = r.블록목록, 참여자목록 = r.참여자목록,
                포함원장목록 = r.포함원장목록 ?? previous?.포함원장목록 ?? [], 다이어그램스냅샷 = r.다이어그램스냅샷,
                외부참조 = r.외부참조, 확장속성 = r.확장속성 };
            Put(saved); return Task.FromResult(Copy(saved));
        }
    }
}
