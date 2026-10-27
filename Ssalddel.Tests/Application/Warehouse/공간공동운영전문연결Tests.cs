using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Application.Mart;
using Ssalddel.Application.Warehouse;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Contracts.Mart;
using Ssalddel.Services.Community;
using 살뜰.Services.Options;
using 살뜰.도메인.창고;
using static Ssalddel.Tests.Application.Warehouse.DistributedTestFixture;

namespace Ssalddel.Tests.Application.Warehouse;

/// <summary>격리 SQLite의 기존 입고·분산 예약·원장 연결을 사용합니다. 실제 계약·실물·단말·송금의 증거는 아닙니다.</summary>
public sealed class 공간공동운영전문연결Tests
{
    private static DistributedActor TestDriver => DistributedTestFixture.Driver;

    [Fact]
    public async Task 실제입고후_양측조건동의만으로는예약하지않고_판매자명시인계에서기존예약과원장이한번연결된다()
    {
        await using var f = await Setup.Create();
        var candidates = await f.Use(CustomerA.UserId).후보Async(f.Core.Row.LedgerId, "cycle-a");
        Assert.Single(candidates.Items);
        await f.ProposeAndConsent();
        Assert.Empty(await f.F.Db.Set<분산재고예약Record>().ToArrayAsync());
        var command = f.Command("handoff");
        var result = await f.Use(SellerA.UserId).변경Async(f.Core.Row.LedgerId, f.RequestId, command);
        Assert.True(result.IsSuccess); Assert.Equal("confirmed", result.Value.StatusCode);
        Assert.True(result.Value.InventoryReserved); Assert.False(result.Value.PaymentConfirmed);
        Assert.Equal("Connected", result.Value.OrderLedgerConnectionStatusCode);
        Assert.Equal(1, await f.F.Db.Set<분산재고예약Record>().CountAsync());
        Assert.Equal(1, await f.F.Db.출고예정.CountAsync());
        var stock = await f.F.Db.입고상품.AsNoTracking().SingleAsync(s => s.Id == f.Lot.InboundItemId);
        Assert.Equal(4, stock.가용수량); Assert.Equal(1, stock.예약수량);
        Assert.Equal(마트주문요청상태코드.확정됨, (await f.F.Db.마트주문요청.AsNoTracking().SingleAsync()).상태코드);
        var replay = await f.Use(SellerA.UserId).변경Async(f.Core.Row.LedgerId, f.RequestId, command);
        Assert.True(replay.Value.Replayed); Assert.Equal(1, await f.F.Db.Set<분산재고예약Record>().CountAsync());
        Assert.NotNull(await f.Use(SellerA.UserId).요청결과Async(f.Core.Row.LedgerId, command.ClientRequestId));
        Assert.Null(await f.Use(CustomerA.UserId).요청결과Async(f.Core.Row.LedgerId, command.ClientRequestId));
    }

    [Fact]
    public async Task 미완료주문으로회차를시작하고_기존피킹전달수령을거친뒤에만회차를마감한다()
    {
        await using var f = await Setup.Create(); await f.ProposeAndConsent();
        var confirmed = await f.Use(SellerA.UserId).변경Async(f.Core.Row.LedgerId, f.RequestId, f.Command("handoff"));
        Assert.Equal("confirmed", confirmed.Value.StatusCode);
        var agreement = await f.F.Db.Set<분산보관합의Record>().AsNoTracking().SingleAsync();
        f.Core.Row.Cycles[0].References = [new() { KindCode = "DistributedSite", ReferenceId = f.Site.SiteId },
            new() { KindCode = "StorageAgreement", ReferenceId = agreement.AgreementId, ParentId = f.Site.SiteId },
            new() { KindCode = "InboundPlan", ReferenceId = f.Lot.PlanId, ParentId = f.Site.SiteId },
            new() { KindCode = "DistributedWave", ReferenceId = f.Terms.WaveId, ParentId = f.Site.SiteId },
            new() { KindCode = "DistributedOrder", ReferenceId = confirmed.Value.DistributedOrderId!, ParentId = f.Terms.WaveId }];
        var community = new 공간공동운영UseCase(f.Core, new Current(Operator.UserId), Setup.Mode,
            new Official생활교류공개지역Source(), f.F.Clock, f.Bridge());
        var initial = await community.상세Async(f.Core.Row.LedgerId);
        Assert.True(initial!.Readiness.CanStart); Assert.False(initial.Readiness.CanComplete);
        var started = await community.변경Async(f.Core.Row.LedgerId, new() { ClientRequestId = Guid.NewGuid(), ExpectedRevision = f.Core.Row.Revision,
            Action = 공간공동운영명령Codes.StartCycle, CycleId = "cycle-a" });
        Assert.True(started.IsSuccess); Assert.Equal("running", started.Value.Cycles[0].StatusCode);
        Assert.True((await community.변경Async(f.Core.Row.LedgerId, new() { ClientRequestId = Guid.NewGuid(), ExpectedRevision = f.Core.Row.Revision,
            Action = 공간공동운영명령Codes.CompleteCycle, CycleId = "cycle-a" })).IsFailed);
        var waveRow = await f.F.Db.Set<분산배송WaveRecord>().AsNoTracking().SingleAsync();
        var closed = await f.F.Use(Operator).CloseAsync(Operator, waveRow.WaveId, new() { RequestId = Guid.NewGuid(), ExpectedRevision = waveRow.Revision }, default);
        f.F.RefreshLocation();
        var planned = await f.F.Use(Operator).PlanAsync(Operator, waveRow.WaveId, new() { RequestId = Guid.NewGuid(), ExpectedRevision = closed.Revision, DriverId = TestDriver.UserId }, default);
        await f.F.Use(TestDriver).AcceptAsync(TestDriver, waveRow.WaveId, new() { RequestId = Guid.NewGuid(), ExpectedRevision = planned.Revision }, default);
        for (var step = 0; step < 4; step++)
        {
            var ws = await f.F.Use(TestDriver).WorkspaceAsync(TestDriver, "driver", default); var wave = Assert.Single(ws.Waves);
            if (wave.StatusCode == "Completed") break;
            var stop = wave.Stops.Single(s => s.StopId == wave.CurrentStopId); f.F.Clock.Now = stop.ExpectedArrivalUtc; f.F.RefreshLocation();
            if (stop.Kind == "Pickup")
            {
                var reservation = Assert.Single(wave.Reservations);
                var request = new DistributedPickRequest { RequestId = Guid.NewGuid(), ExpectedRevision = wave.Revision, StopId = stop.StopId,
                    ReservationId = reservation.ReservationId, LocationBarcode = f.Site.Locations.Single().Barcode,
                    OwnerUserId = reservation.OwnerUserId, Sku = reservation.Sku, Quantity = reservation.Quantity };
                await f.F.Proof(TestDriver, wave.WaveId, request); await f.F.Use(TestDriver).PickAsync(TestDriver, wave.WaveId, request, default);
            }
            else await f.F.Use(TestDriver).DeliverAsync(TestDriver, wave.WaveId, await f.F.Proof(TestDriver, wave.WaveId,
                new DistributedDeliveryRequest { RequestId = Guid.NewGuid(), ExpectedRevision = wave.Revision, StopId = stop.StopId,
                    OrderId = confirmed.Value.DistributedOrderId!, EvidenceReference = "synthetic-receipt-proof" }), default);
        }
        Assert.False((await community.상세Async(f.Core.Row.LedgerId))!.Readiness.CanComplete);
        var beforeReceipt = await f.F.Use(CustomerA).WaveAsync(CustomerA, f.Terms.WaveId, "orderer", default);
        await f.F.Use(CustomerA).AcknowledgeAsync(CustomerA, f.Terms.WaveId, new() { RequestId = Guid.NewGuid(), ExpectedRevision = beforeReceipt.Revision,
            OrderId = confirmed.Value.DistributedOrderId!, EvidenceReference = "synthetic-customer-confirmation" }, default);
        Assert.True((await community.상세Async(f.Core.Row.LedgerId))!.Readiness.CanComplete);
        var complete = await community.변경Async(f.Core.Row.LedgerId, new() { ClientRequestId = Guid.NewGuid(), ExpectedRevision = f.Core.Row.Revision,
            Action = 공간공동운영명령Codes.CompleteCycle, CycleId = "cycle-a", ResultNote = "합성 회차 한 번의 수령 확인" });
        Assert.True(complete.IsSuccess); Assert.True(complete.Value.Cycles[0].SourceCompletionVerified);
        Assert.Equal("결제대기", (await f.F.Db.화주운송의뢰.AsNoTracking().SingleAsync()).결제상태);
    }

    [Fact]
    public async Task 한쪽동의_미확인수량_다른상품_과거조건판본은실제예약을만들지않는다()
    {
        await using var f = await Setup.Create();
        var proposed = await f.Use(CustomerA.UserId).변경Async(f.Core.Row.LedgerId, f.RequestId, f.Command("propose", f.Terms));
        Assert.True(proposed.IsSuccess);
        Assert.True((await f.Use(CustomerA.UserId).변경Async(f.Core.Row.LedgerId, f.RequestId, f.Command("agree"))).IsSuccess);
        var tooEarly = await f.Use(SellerA.UserId).변경Async(f.Core.Row.LedgerId, f.RequestId, f.Command("handoff"));
        Assert.True(tooEarly.IsFailed);
        var wrongTerms = Clone(f.Terms); wrongTerms.Quantity = 2;
        var changed = await f.Use(CustomerA.UserId).변경Async(f.Core.Row.LedgerId, f.RequestId, f.Command("propose", wrongTerms));
        Assert.True(changed.IsFailed);
        var stale = f.Command("agree"); stale.ExpectedTermsVersion = 0;
        Assert.True((await f.Use(SellerA.UserId).변경Async(f.Core.Row.LedgerId, f.RequestId, stale)).IsFailed);
        Assert.Empty(await f.F.Db.Set<분산재고예약Record>().ToArrayAsync());
    }

    [Fact]
    public async Task 원장생성미확정은보류하고_명시재개만같은기존명령으로복구한다()
    {
        await using var f = await Setup.Create(); await f.ProposeAndConsent();
        f.Ledgers.FailRootCreates = true;
        var command = f.Command("handoff");
        var pending = await f.Use(SellerA.UserId).변경Async(f.Core.Row.LedgerId, f.RequestId, command);
        Assert.True(pending.IsSuccess); Assert.Equal("handoff-pending", pending.Value.StatusCode);
        Assert.Empty(await f.F.Db.Set<분산재고예약Record>().ToArrayAsync());
        Assert.Equal(마트주문요청상태코드.제출됨, (await f.F.Db.마트주문요청.AsNoTracking().SingleAsync()).상태코드);
        var writes = f.Ledgers.WriteCount;
        await f.Use(SellerA.UserId).조회Async(f.Core.Row.LedgerId, f.RequestId);
        Assert.Equal(writes, f.Ledgers.WriteCount);
        f.Ledgers.FailRootCreates = false;
        var recovered = await f.Use(SellerA.UserId).변경Async(f.Core.Row.LedgerId, f.RequestId, f.Command("resume"));
        Assert.True(recovered.IsSuccess); Assert.Equal("confirmed", recovered.Value.StatusCode);
        Assert.Single(await f.F.Db.Set<분산재고예약Record>().ToArrayAsync());
    }

    [Fact]
    public async Task 실제SQL예약후하위원장연결실패도_조회와중복명령은재실행하지않고_명시재개만연결을복구한다()
    {
        await using var f = await Setup.Create(); await f.ProposeAndConsent();
        f.Ledgers.FailChildWrites = true;
        var command = f.Command("handoff");
        var pending = await f.Use(SellerA.UserId).변경Async(f.Core.Row.LedgerId, f.RequestId, command);
        Assert.True(pending.IsSuccess); Assert.Equal("confirmed", pending.Value.StatusCode);
        Assert.True(pending.Value.InventoryReserved); Assert.Equal("Pending", pending.Value.OrderLedgerConnectionStatusCode);
        Assert.Single(await f.F.Db.Set<분산재고예약Record>().ToArrayAsync());
        var before = f.Ledgers.WriteCount;
        await f.Use(SellerA.UserId).조회Async(f.Core.Row.LedgerId, f.RequestId);
        var replay = await f.Use(SellerA.UserId).변경Async(f.Core.Row.LedgerId, f.RequestId, command);
        Assert.True(replay.Value.Replayed); Assert.Equal("Pending", replay.Value.OrderLedgerConnectionStatusCode);
        Assert.Equal(before, f.Ledgers.WriteCount);
        f.Ledgers.FailChildWrites = false;
        var recovered = await f.Use(SellerA.UserId).변경Async(f.Core.Row.LedgerId, f.RequestId, f.Command("resume"));
        Assert.True(recovered.IsSuccess); Assert.Equal("Connected", recovered.Value.OrderLedgerConnectionStatusCode);
        Assert.Single(await f.F.Db.Set<분산재고예약Record>().ToArrayAsync());
        Assert.Single(await f.F.Db.Set<분산물류CommandReceipt>().Where(r => r.Kind == "AddOrder").ToArrayAsync());
    }

    [Theory]
    [InlineData(마트주문요청상태코드.확정확인중)]
    [InlineData(마트주문요청상태코드.확정됨)]
    public async Task 기존비구속요청API는확정잠금뒤수량과철회를거절한다(string state)
    {
        await using var f = await Setup.Create();
        await f.F.Db.마트주문요청.Where(q => q.Id == f.RequestId).ExecuteUpdateAsync(s => s.SetProperty(q => q.상태코드, state));
        var writer = new 마트주문요청작성UseCase(f.F.Db, new Current(CustomerA.UserId));
        Assert.True((await writer.수량변경Async(f.RequestId, new() { 수량 = 2, 기대상태코드 = state,
            비구속주문요청확인 = true, 안내버전 = 마트주문요청안내.현재버전 }, default)).IsFailed);
        Assert.True((await writer.철회Async(f.RequestId, new() { 기대상태코드 = state }, default)).IsFailed);
        Assert.Equal(state, (await f.F.Db.마트주문요청.AsNoTracking().SingleAsync()).상태코드);
    }

    [Fact]
    public async Task 최신전문판본은연결당시판본보다커져도조회되며_행동문구만으로완료되지않는다()
    {
        await using var f = await Setup.Create();
        var bridge = f.Bridge();
        var result = await bridge.ValidateAsync(Operator.UserId, [new() { KindCode = "DistributedSite", ReferenceId = f.Site.SiteId, ExpectedRevision = 0 },
            new() { KindCode = "InboundPlan", ReferenceId = f.Lot.PlanId, ParentId = f.Site.SiteId, ExpectedRevision = 1 }], default);
        Assert.All(result, r => Assert.True(r.Verified)); Assert.All(result, r => Assert.True(r.Completed));
        var wave = await f.F.Db.Set<분산배송WaveRecord>().SingleAsync(); wave.StatusCode = "Completed"; await f.F.Db.SaveChangesAsync();
        var noPhysicalOrder = Assert.Single(await bridge.ValidateAsync(Operator.UserId,
            [new() { KindCode = "DistributedWave", ReferenceId = wave.WaveId, ParentId = f.Site.SiteId }], default));
        Assert.True(noPhysicalOrder.Verified); Assert.False(noPhysicalOrder.Completed);
        Assert.All(await bridge.ValidateAsync("foreign", [new() { KindCode = "DistributedSite", ReferenceId = f.Site.SiteId }], default), r => Assert.False(r.Verified));
    }

    [Fact]
    public async Task 후보는현재본인거점과직접소유한비구속요청으로한정하고_조회는쓰지않는다()
    {
        await using var f = await Setup.Create();
        var before = f.Core.Row.Revision; var ledgerWrites = f.Ledgers.WriteCount;
        Assert.Empty((await f.Use(SellerA.UserId).후보Async(f.Core.Row.LedgerId, "cycle-a")).Items);
        var ownCandidates = await f.Use(CustomerA.UserId).후보Async(f.Core.Row.LedgerId, "cycle-a");
        Assert.Single(ownCandidates.Items); Assert.Equal(f.Core.Row.LedgerId, ownCandidates.LedgerId); Assert.Equal(before, ownCandidates.Revision);
        var refs = new[] { new 공간공동운영전문참조Dto { KindCode = "DistributedSite", ReferenceId = f.Site.SiteId } };
        var candidates = await f.Bridge().CandidatesAsync(Operator.UserId, refs);
        Assert.Contains(candidates, c => c.Reference.KindCode == "InboundPlan" && c.Reference.ReferenceId == f.Lot.PlanId);
        Assert.DoesNotContain(candidates, c => c.Reference.KindCode == "DistributedSite" && c.Reference.ReferenceId != f.Site.SiteId);
        Assert.Equal(before, f.Core.Row.Revision); Assert.Equal(ledgerWrites, f.Ledgers.WriteCount);
    }

    [Fact]
    public async Task 같은사람이두거점을관리해도_Wave를생략한다른거점주문과마트출고는섞을수없다()
    {
        await using var f = await Setup.Create(); await f.ProposeAndConsent();
        var confirmed = await f.Use(SellerA.UserId).변경Async(f.Core.Row.LedgerId, f.RequestId, f.Command("handoff"));
        Assert.True(confirmed.IsSuccess);
        var otherSite = await f.F.Db.Set<분산거점Record>().AsNoTracking().SingleAsync(s => s.SiteId != f.Site.SiteId);
        var orderRef = new 공간공동운영전문참조Dto { KindCode = "DistributedOrder", ReferenceId = confirmed.Value.DistributedOrderId! };
        Assert.True(Assert.Single(await f.Bridge().ValidateAsync(Operator.UserId, [orderRef], default)).Verified);
        var mixed = await f.Bridge().ValidateAsync(Operator.UserId,
            [new() { KindCode = "DistributedSite", ReferenceId = otherSite.SiteId }, orderRef], default);
        Assert.True(mixed[0].Verified); Assert.False(mixed[1].Verified); Assert.Equal("lineage-mismatch", mixed[1].StatusCode);

        // 마트 통제 표본: 수령 완료를 꾸미지 않고 실제 출고-재고 참조만 가진 미완료 주문입니다.
        var outbound = await f.F.Db.출고예정.AsNoTracking().SingleAsync();
        var mart = new 살뜰.도메인.마트.마트주문 { 주문참조번호 = outbound.주문참조번호,
            주문자UserId = CustomerA.UserId, 판매자UserId = SellerA.UserId,
            상품목록 = [new() { 출고예정Id = outbound.Id, 상품명 = outbound.상품명, SKU = outbound.SKU, 수량 = outbound.수량 }] };
        f.F.Db.마트주문.Add(mart); await f.F.Db.SaveChangesAsync();
        var martRef = new 공간공동운영전문참조Dto { KindCode = "MartOrder", ReferenceId = mart.Id.ToString() };
        var same = await f.Bridge().ValidateAsync(CustomerA.UserId,
            [new() { KindCode = "DistributedSite", ReferenceId = f.Site.SiteId }, martRef], default);
        Assert.True(same[1].Verified); Assert.False(same[1].Completed);
        var other = await f.Bridge().ValidateAsync(CustomerA.UserId,
            [new() { KindCode = "DistributedSite", ReferenceId = otherSite.SiteId }, martRef], default);
        Assert.False(other[1].Verified); Assert.Equal("lineage-mismatch", other[1].StatusCode);
    }

    private static T Clone<T>(T v) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(v))!;
    private sealed class Current(string user) : ICurrentUserAccessor { public string? UserId => user; public string? Role => null; }

    private sealed class Setup(DistributedTestFixture f, CoreStore core, LedgerStore ledgers,
        DistributedSiteDto site, 분산재고LotRecord lot, 공간공동운영주문조건Dto terms) : IAsyncDisposable
    {
        public DistributedTestFixture F => f; public CoreStore Core => core; public LedgerStore Ledgers => ledgers;
        public DistributedSiteDto Site => site; public 분산재고LotRecord Lot => lot;
        public 공간공동운영주문조건Dto Terms => terms; public Guid RequestId => terms.OrderRequestId;
        private static IOptions<DistributedFulfillmentOptions> Settings => Options.Create(new DistributedFulfillmentOptions { Enabled = true, Profile = DistributedFulfillmentCodes.Profile });
        public static ISsalddelExecutionModePolicy Mode => new SsalddelExecutionModePolicy(Options.Create(new SsalddelExecutionOptions { Mode = SsalddelExecutionMode.Simulation }));
        public 공간공동운영전문업무Bridge Bridge() => new(f.Db, ledgers, Mode, Settings, f.Clock);
        public 공간공동운영주문UseCase Use(string actor)
        {
            var adapter = new 분산주문원장연결Adapter(f.Db, ledgers, new 주문원장통합UseCase(ledgers),
                new 음식마트원장Mongo동기화Service(ledgers, f.Db, NullLogger<음식마트원장Mongo동기화Service>.Instance),
                new 운송원장Mongo동기화Service(f.Db, ledgers, NullLogger<운송원장Mongo동기화Service>.Instance), Mode, Settings);
            return new(f.Db, new Current(actor), core, core, ledgers, f.Use(new(actor), orderLedgers: adapter), Mode, Settings, f.Clock);
        }
        public 공간공동운영주문명령Request Command(string action, 공간공동운영주문조건Dto? proposed = null) => new()
        { ClientRequestId = Guid.NewGuid(), ExpectedRevision = core.Row.Revision, ExpectedTermsVersion = action == "propose" ? 0 : 1,
            Action = action, ConfirmTerms = true, Terms = proposed };
        public async Task ProposeAndConsent()
        {
            Assert.True((await Use(CustomerA.UserId).변경Async(core.Row.LedgerId, RequestId, Command("propose", terms))).IsSuccess);
            Assert.True((await Use(CustomerA.UserId).변경Async(core.Row.LedgerId, RequestId, Command("agree"))).IsSuccess);
            Assert.True((await Use(SellerA.UserId).변경Async(core.Row.LedgerId, RequestId, Command("agree"))).IsSuccess);
        }
        public static async Task<Setup> Create()
        {
            var f = await DistributedTestFixture.Create(); var site = (await f.Sites())[0]; var goodsOwner = new DistributedActor("goods-owner-a");
            await f.EnsureAgreement(site, goodsOwner, "same-sku");
            var siteRow = await f.Db.Set<분산거점Record>().AsNoTracking().SingleAsync(s => s.SiteId == site.SiteId);
            await f.Use(Operator).SiteAcceptancePolicyAsync(Operator, site.SiteId, new() { RequestId = Guid.NewGuid(), ExpectedRevision = siteRow.Revision,
                Policy = new() { AllowedCategoryCodes = ["industrial"], AllowFood = false, AllowedPackagingCodes = ["boxed"], AllowedTemperatureCodes = ["ambient"],
                    CapacityUnit = "칸", MaximumCapacity = 100, MaximumStorageDays = 2, AccessHours = "합의한 시간" } }, default);
            var plan = await f.Use(SellerA).InboundAsync(SellerA, new() { RequestId = Guid.NewGuid(), SourceReference = "한 거점 합성 입고",
                Lines = [new() { LineKey = "first", SourceInboundId = 101, SiteId = site.SiteId, LocationId = site.Locations.Single().LocationId,
                    Sku = "same-sku", ProductName = "포장 상품", LotCode = "pilot-lot", ExpectedQuantity = 5 }] }, default);
            var agreement = await f.Db.Set<분산보관합의Record>().AsNoTracking().SingleAsync(a => a.SiteId == site.SiteId);
            plan = await f.Use(SellerA).InboundProposalAsync(SellerA, plan.AggregateId, "first", new() { RequestId = Guid.NewGuid(), ExpectedRevision = plan.Revision,
                Terms = new() { AgreementId = agreement.AgreementId, AgreementTermsVersion = agreement.TermsVersion, SupplierUserId = SellerA.UserId,
                    SellerUserId = SellerA.UserId, GoodsOwnerUserId = goodsOwner.UserId, SupplierTypeCode = "factory", Quantity = 5,
                    ArrivalStartUtc = f.Clock.Now.AddMinutes(-1), ArrivalEndUtc = f.Clock.Now.AddHours(2), StorageEndsAtUtc = f.Clock.Now.AddDays(1), AccessHoursConfirmed = true,
                    SupplyPriceOrFreeTerms = "명시한 공급 조건", SupplierResponsibilityTerms = "공급 책임 확인", ReturnTerms = "반환 조건 확인",
                    Goods = new() { CategoryCodes = ["industrial"], IsFood = false, IsProcessed = true, PackagingCode = "boxed", TemperatureCode = "ambient", CapacityUnit = "칸", CapacityPerItem = 1 } } }, default);
            foreach (var party in new[] { goodsOwner, Operator }) plan = await f.Use(party).InboundConsentAsync(party, plan.AggregateId, "first",
                new() { RequestId = Guid.NewGuid(), ExpectedRevision = plan.Revision, TermsVersion = 1, Granted = true }, default);
            plan = await f.Use(Operator).ReceiveAsync(Operator, plan.AggregateId, await f.Proof(Operator, plan.AggregateId,
                new DistributedInboundReceiptRequest { RequestId = Guid.NewGuid(), ExpectedRevision = plan.Revision, LineKey = "first",
                    LocationBarcode = site.Locations.Single().Barcode, Sku = "same-sku", Quantity = 5 }), default);
            var lot = await f.Db.Set<분산재고LotRecord>().AsNoTracking().SingleAsync();
            var stock = await f.Db.입고상품.AsNoTracking().SingleAsync(s => s.Id == lot.InboundItemId);
            var sale = new 살뜰.도메인.판매.판매상품 { 입고상품Id = lot.InboundItemId, 소유자UserId = SellerA.UserId, 대표상품명 = stock.상품명, 판매SKU = stock.SKU, 판매가 = 1000 };
            f.Db.판매상품.Add(sale); await f.Db.SaveChangesAsync();
            var product = new 살뜰.도메인.마트.마트공개상품 { 판매상품Id = sale.Id, 상품명 = stock.상품명, 판매단위 = "개", 판매가 = 1000,
                판매가능수량 = 5, 공개여부 = true, 판매허용여부 = true, 재고기준시각Utc = f.Clock.Now };
            f.Db.마트공개상품.Add(product); await f.Db.SaveChangesAsync();
            var writer = new 마트주문요청작성UseCase(f.Db, new Current(CustomerA.UserId));
            var request = await writer.등록Async(new() { 클라이언트요청Id = Guid.NewGuid(), 공개상품Id = product.Id, 수량 = 1,
                비구속주문요청확인 = true, 안내버전 = 마트주문요청안내.현재버전 }, default); Assert.True(request.IsSuccess);
            await f.Cargo("pilot-cargo", SellerA, CustomerA, 1);
            var wave = await f.Use(Operator).CreateWaveAsync(Operator, new() { RequestId = Guid.NewGuid(), Label = "한 거점 한 주문",
                CutoffUtc = f.Clock.Now.AddMinutes(1), PickupStartUtc = f.Clock.Now.AddMinutes(2), PickupEndUtc = f.Clock.Now.AddHours(3),
                DeliveryStartUtc = f.Clock.Now.AddMinutes(2), DeliveryEndUtc = f.Clock.Now.AddHours(4), PickupServiceMinutes = 1, DeliveryServiceMinutes = 1 }, default);
            var core = new CoreStore(new() { LedgerId = "cooperation-pilot", Revision = 1, TermsRevision = 1, ExecutionMode = "Simulation", OwnerUserId = Operator.UserId, OperatorUserId = Operator.UserId,
                Participants = [new() { UserId = CustomerA.UserId, RoleCode = 공간공동운영역할Codes.Resident, StateCode = "accepted", AgreedTermsRevision = 1 },
                    new() { UserId = SellerA.UserId, RoleCode = 공간공동운영역할Codes.Supplier, StateCode = "accepted", AgreedTermsRevision = 1 },
                    new() { UserId = Operator.UserId, RoleCode = 공간공동운영역할Codes.SpaceProvider, StateCode = "accepted", AgreedTermsRevision = 1 },
                    new() { UserId = TestDriver.UserId, RoleCode = 공간공동운영역할Codes.Driver, StateCode = "accepted", AgreedTermsRevision = 1 }],
                Cycles = [new() { CycleId = "cycle-a", Number = 1, TermsRevision = 1, References = [new() { KindCode = "DistributedSite", ReferenceId = site.SiteId },
                    new() { KindCode = "DistributedWave", ReferenceId = wave.AggregateId }] }] });
            return new(f, core, new(), site, lot, new() { OrderRequestId = request.Value.주문요청Id, PublicProductId = product.Id, Quantity = 1,
                UnitPriceKrw = 1000, CycleId = "cycle-a", SiteId = site.SiteId, LotId = lot.LotId, WaveId = wave.AggregateId,
                CargoRequestId = "pilot-cargo", DeadlineUtc = f.Clock.Now.AddHours(5) });
        }
        public ValueTask DisposeAsync() => f.DisposeAsync();
    }

    private sealed class CoreStore(공간공동운영Record initial) : I공간공동운영Store, I공간공동운영연결Query
    {
        public 공간공동운영Record Row { get; private set; } = Clone(initial);
        public Task<공간공동운영Record?> 조회Async(string id, CancellationToken ct = default) => Task.FromResult<공간공동운영Record?>(id == Row.LedgerId ? Clone(Row) : null);
        public Task<공간공동운영Record?> 조회Async(string id, string actor, CancellationToken ct = default)
            => Task.FromResult<공간공동운영Record?>(id == Row.LedgerId && (Row.OwnerUserId == actor || Row.Participants.Any(p => p.UserId == actor)) ? Clone(Row) : null);
        public Task<IReadOnlyList<공간공동운영Record>> 목록Async(int skip = 0, int take = 200, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<공간공동운영Record>>([Clone(Row)]);
        public Task<bool> 생성Async(공간공동운영Record r, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> 교체Async(공간공동운영Record r, long expected, string actor, CancellationToken ct = default)
        { if (Row.Revision != expected) return Task.FromResult(false); Row = Clone(r); return Task.FromResult(true); }
    }
    private sealed class LedgerStore : I커뮤니티원장저장소
    {
        private readonly Dictionary<string, 커뮤니티원장Dto> rows = new();
        public bool FailRootCreates { get; set; }
        public bool FailChildWrites { get; set; }
        public int WriteCount { get; private set; }
        public Task<커뮤니티원장Dto?> 원장조회Async(string id, CancellationToken ct = default) => Task.FromResult(rows.TryGetValue(id, out var r) ? Clone(r) : null);
        public Task<IReadOnlyList<커뮤니티원장Dto>> 원장목록조회Async(커뮤니티원장조회조건 q, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<커뮤니티원장Dto>>(rows.Values.Select(Clone).ToArray());
        public Task<커뮤니티원장Dto?> 원장상태변경Async(커뮤니티원장상태변경요청 r, string actor, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<커뮤니티원장Dto> 원장저장Async(커뮤니티원장저장요청 r, string actor, CancellationToken ct = default)
        {
            if (FailRootCreates && r.원장Id!.StartsWith("space-order:")) throw new IOException("synthetic-root-unavailable");
            if (FailChildWrites && !r.원장Id!.StartsWith("space-order:")) throw new IOException("synthetic-child-ledger-unavailable");
            rows.TryGetValue(r.원장Id!, out var old); if (r.기대Revision.HasValue && r.기대Revision != (old?.Revision ?? 0)) throw new InvalidOperationException("revision conflict");
            var result = new 커뮤니티원장Dto { 원장Id = r.원장Id!, Revision = (old?.Revision ?? 0) + 1, 커뮤니티Id = r.커뮤니티Id,
                원장템플릿Key = r.원장템플릿Key, 제목 = r.제목, 상태 = r.상태 ?? 커뮤니티원장상태.초안, 생성자UserId = r.생성자UserId,
                블록목록 = Clone(r.블록목록), 참여자목록 = Clone(r.참여자목록), 포함원장목록 = r.포함원장목록 ?? old?.포함원장목록 ?? [],
                외부참조 = Clone(r.외부참조), 확장속성 = Clone(r.확장속성) };
            rows[result.원장Id] = Clone(result); WriteCount++; return Task.FromResult(Clone(result));
        }
    }
}
