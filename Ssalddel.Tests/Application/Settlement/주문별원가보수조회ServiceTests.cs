using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ssalddel.Application.Settlement;
using Ssalddel.Application.Warehouse;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Settlement;
using Ssalddel.Services.Community;
using Ssalddel.Services.PrivacyRetention;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Options;
using 살뜰.도메인.마트;
using 살뜰.도메인.정산;
using 살뜰.도메인.창고;
using 살뜰.도메인.운송;
using C = Ssalddel.Contracts.Common.Settlement.NeighborhoodSettlementCodes;

namespace Ssalddel.Tests.Application.Settlement;

public sealed class 주문별원가보수조회ServiceTests
{
    [Fact]
    public async Task 미정단가와실제완료수량을구분하고조회는새채무나원장변경을만들지않는다()
    {
        await using var f = await Fixture.Create();
        var source = f.Mart(1, "ref-1", "picker");
        f.Agreement("a1", source, [new() { LineKey = "pick", Kind = C.Picking, Billing = C.PerUnit,
            UnitPriceKrw = null, Quantity = 3, PayerUserId = "seller", PayeeUserId = "picker", BeneficiaryRole = "Picker" }]);
        await f.Db.SaveChangesAsync();
        var read = await f.Use().GetAsync("ssalddel-mart:ref-1", "picker", default);
        var mine = Assert.Single(Assert.Single(read.Sources).MyCompletedWork);
        Assert.Equal(3, mine.ConfirmedQuantity); Assert.Equal("RateUnresolved", mine.CompensationStatusCode);
        var group = Assert.Single(read.AmountGroups); Assert.Null(group.CompleteAccruedAmountKrw);
        Assert.Equal(1, group.PendingAgreementLineCount); Assert.Null(read.PlatformProfitAmountKrw);
        Assert.False(read.IsActualBankVerified); Assert.Empty(await f.Db.Set<NeighborhoodSettlementEntryRecord>().ToArrayAsync());
        Assert.Equal(1, (await f.Db.Set<NeighborhoodSettlementAgreementRecord>().SingleAsync()).Revision);
        Assert.Equal(0, f.Ledgers.Writes);
    }

    [Fact]
    public async Task 본인작업으로최소진입해도다른주문과동료보수및전체재고를보지않는다()
    {
        await using var f = await Fixture.Create();
        var one = f.Mart(1, "ref-1", "picker"); var two = f.Mart(2, "ref-2", "other");
        f.Agreement("a1", one, [Line("pick", "picker", 300), Line("pack", "packer", 500, "Packing")]);
        f.Agreement("a2", two, [Line("other", "other", 700)]);
        f.Entry("e1", "a1", "pick", "picker", 300, one.CompletedWork[0]);
        f.Entry("e2", "a1", "pack", "packer", 500, one.CompletedWork[0], "Packing");
        f.Entry("e3", "a2", "other", "other", 700, two.CompletedWork[0]);
        await f.Db.SaveChangesAsync();
        var read = await f.Use().GetAsync("ssalddel-mart:ref-1", "picker", default);
        var source = Assert.Single(read.Sources); Assert.Equal("1", source.SourceId);
        var settlement = Assert.Single(source.Settlements); Assert.Equal("e1", Assert.Single(settlement.Entries).EntryId);
        Assert.Equal("picker", Assert.Single(settlement.Agreement.Terms.Lines).PayeeUserId);
        Assert.Equal("picker", Assert.Single(source.MyCompletedWork).ActorUserId);
        Assert.Empty(read.InventoryCosts); Assert.DoesNotContain(f.Resolver.Calls, x => x == "MartOrder:2");
        var outsider = await Assert.ThrowsAsync<NeighborhoodSettlementException>(() => f.Use().GetAsync("ssalddel-mart:ref-1", "outsider", default));
        Assert.Equal("NeighborhoodSettlementOrderNotFound", outsider.Code);
    }

    [Fact]
    public async Task 루트공개승인은하위정산의금액이나작업사진조회권이아니다()
    {
        await using var f = await Fixture.Create(); var source = f.Mart(1, "ref-1", "picker");
        f.Agreement("a1", source, [Line("pick", "picker", 300)]);
        f.Entry("e1", "a1", "pick", "picker", 300, source.CompletedWork[0]);
        f.Disclosures.Grants.Add("ssalddel-mart:ref-1"); await f.Db.SaveChangesAsync();
        var read = await f.Use().GetAsync("ssalddel-mart:ref-1", "outsider", default);
        Assert.Empty(read.Sources); Assert.Empty(read.AmountGroups); Assert.Empty(read.InventoryCosts);
        Assert.Contains("OrderSettlementSourceNotConnected", read.ReasonCodes);
    }

    [Fact]
    public async Task 삭제된자녀와틀린역할은새탐색에재사용하지않고루트파기는차단한다()
    {
        await using var f = await Fixture.Create(); f.Mart(1, "ref-1", "picker");
        var child = f.Ledgers.Rows["ssalddel-mart:ref-1"];
        var root = f.Root("root", "seller", [new() { 원장Id = child.원장Id, 원장템플릿Key = child.원장템플릿Key,
            역할 = 주문원장포함역할.판매 }]);
        await f.Db.SaveChangesAsync();
        Assert.Empty((await f.Use().GetAsync(root.원장Id, "seller", default)).Sources);
        root.포함원장목록[0].역할 = 주문원장포함역할.창고출고;
        child.확장속성 = new Dictionary<string, string> { [배송원장개인정보파기Service.Marker] = "purged" };
        var read = await f.Use().GetAsync(root.원장Id, "seller", default);
        Assert.DoesNotContain(read.Sources, x => x.SourceKind == C.DistributedOrder);
        Assert.DoesNotContain(C.DistributedOrder + ":A", f.Resolver.Calls);
        root.확장속성 = new Dictionary<string, string> { [배송원장개인정보파기Service.Marker] = "purged" };
        var error = await Assert.ThrowsAsync<NeighborhoodSettlementException>(() => f.Use().GetAsync(root.원장Id, "seller", default));
        Assert.Equal("NeighborhoodSettlementOrderNotFound", error.Code);
    }

    [Fact]
    public async Task 이백번째뒤의명시적자녀참조도끝까지읽되전체원장목록조회는하지않는다()
    {
        await using var f = await Fixture.Create(); f.Mart(1, "ref-1", "picker");
        var child = f.Ledgers.Rows["ssalddel-mart:ref-1"];
        var references = Enumerable.Range(0, 205).Select(i => new 커뮤니티포함원장참조Dto
            { 원장Id = "unrelated-" + i, 원장템플릿Key = CommunityLedgerTemplateKeys.WarehouseOutbound, 역할 = 주문원장포함역할.창고출고 }).ToList();
        references.Add(new() { 원장Id = child.원장Id, 원장템플릿Key = child.원장템플릿Key, 역할 = 주문원장포함역할.창고출고 });
        var root = f.Root("root", "seller", references); await f.Db.SaveChangesAsync();
        Assert.Equal("1", Assert.Single((await f.Use().GetAsync(root.원장Id, "picker", default)).Sources).SourceId);
        Assert.Equal(0, f.Ledgers.ListCalls);
    }

    [Fact]
    public async Task 한입고의두출고주문원가는각귀속분만읽고입고보수를다시발생시키지않는다()
    {
        await using var f = await Fixture.Create();
        f.Distributed("A", 1, 4); f.Distributed("B", 2, 3);
        var inbound = new NeighborhoodSettlementEntryDto { EntryId = "inbound-entry", AgreementId = "inbound-agreement", TermsVersion = 1,
            Kind = C.Inbound, AmountKrw = 1000, PayerUserId = "seller", PayeeUserId = "receiver", Quantity = 10 };
        f.Db.Add(new NeighborhoodSettlementEntryRecord { EntryId = inbound.EntryId, AgreementId = inbound.AgreementId,
            TermsVersion = 1, Kind = inbound.Kind, DetailsJson = Serialize(inbound), AccrualKey = "inbound-key" });
        f.Agreement(inbound.AgreementId, new() { SourceKind = C.DistributedLot, SourceId = "same-lot", SourceVersion = "1" }, []);
        f.Cost("A", 4, 400); f.Cost("B", 3, 300); await f.Db.SaveChangesAsync();
        var a = await f.Use().GetAsync("root-A", "seller", default);
        var b = await f.Use().GetAsync("root-B", "seller", default);
        Assert.Equal(400, Assert.Single(a.InventoryCosts).Attribution.AmountKrw);
        Assert.Equal(300, Assert.Single(b.InventoryCosts).Attribution.AmountKrw);
        Assert.All(a.InventoryCosts.Concat(b.InventoryCosts), x => Assert.Equal("AllocatedCostNotNewPayable", x.AccountingCode));
        Assert.Empty(a.AmountGroups); Assert.Empty(b.AmountGroups);
        Assert.Single(await f.Db.Set<NeighborhoodSettlementEntryRecord>().ToArrayAsync());
        var worker = await f.Use().GetAsync("root-A", "picker", default);
        Assert.Empty(worker.InventoryCosts); Assert.Single(worker.Sources); Assert.Null(worker.PlatformProfitAmountKrw);
    }

    [Fact]
    public async Task 원주문과동일한표시참조만있고영구연결이다르면다른주문에연결하지않는다()
    {
        await using var f = await Fixture.Create(); f.Distributed("A", 1, 4);
        var root = f.Ledgers.Rows["root-A"]; root.원장Id = "different-root";
        f.Ledgers.Rows.Remove("root-A"); f.Ledgers.Rows.Add(root.원장Id, root);
        await f.Db.SaveChangesAsync();
        var read = await f.Use().GetAsync(root.원장Id, "seller", default);
        Assert.DoesNotContain(read.Sources, x => x.SourceKind == C.DistributedOrder);
        Assert.DoesNotContain(C.DistributedOrder + ":A", f.Resolver.Calls);
    }

    [Fact]
    public async Task 조회중루트참조가바뀌면오래된연결결과대신재조회를요구한다()
    {
        await using var f = await Fixture.Create(); f.Mart(1, "ref-1", "picker"); await f.Db.SaveChangesAsync();
        f.Ledgers.ChangeRootOnSecondRead = "ssalddel-mart:ref-1";
        var error = await Assert.ThrowsAsync<NeighborhoodSettlementException>(() => f.Use().GetAsync("ssalddel-mart:ref-1", "picker", default));
        Assert.Equal("NeighborhoodSettlementOrderRequeryRequired", error.Code);
    }

    [Fact]
    public async Task 조회중공개승인이철회되면별도저장소의변경도재조회로처리한다()
    {
        await using var f = await Fixture.Create(); f.Mart(1, "ref-1", "picker"); await f.Db.SaveChangesAsync();
        f.Disclosures.Grants.Add("ssalddel-mart:ref-1"); f.Disclosures.RevokeOnSecondRead = true;
        var error = await Assert.ThrowsAsync<NeighborhoodSettlementException>(() => f.Use().GetAsync("ssalddel-mart:ref-1", "outsider", default));
        Assert.Equal("NeighborhoodSettlementOrderRequeryRequired", error.Code);
    }

    [Theory]
    [InlineData("탈퇴")]
    [InlineData("종료")]
    [InlineData("철회")]
    public async Task 비활성참여상태는루트조회진입권이아니다(string state)
    {
        await using var f = await Fixture.Create();
        var root = f.Root("root", "owner"); root.참여자목록 = [new() { UserId = "former", ParticipationState = state }];
        var error = await Assert.ThrowsAsync<NeighborhoodSettlementException>(() => f.Use().GetAsync("root", "former", default));
        Assert.Equal("NeighborhoodSettlementOrderNotFound", error.Code);
    }

    [Fact]
    public async Task 독립운송참조는본인배송보수를읽고음수정정환불은부호를보존한다()
    {
        await using var f = await Fixture.Create(); var root = f.Root("root", "buyer");
        var source = f.Cargo("cargo", "transport:cargo");
        root.포함원장목록 = [new() { 원장Id = "transport:cargo", 원장템플릿Key = CommunityLedgerTemplateKeys.CargoTransport,
            역할 = 주문원장포함역할.운송 }];
        f.Agreement("cargo-agreement", source, [Line("delivery", "driver", 4000, C.Delivery)]);
        f.Entry("delivery-entry", "cargo-agreement", "delivery", "driver", 4000, source.CompletedWork[0], C.Delivery);
        f.Entry("refund", "cargo-agreement", "refund", "driver", -100, source.CompletedWork[0], C.Adjustment);
        var adjustment = f.Db.Set<NeighborhoodSettlementEntryRecord>().Local.Single(x => x.EntryId == "refund");
        var detail = JsonSerializer.Deserialize<NeighborhoodSettlementEntryDto>(adjustment.DetailsJson, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        detail.OriginalEntryId = "delivery-entry"; adjustment.DetailsJson = Serialize(detail); adjustment.OriginalEntryId = "delivery-entry";
        var payment = new NeighborhoodSettlementPaymentDto { PaymentId = "refund-payment", AgreementId = "cargo-agreement",
            PayerUserId = "seller", PayeeUserId = "driver", AmountKrw = 100, RecipientConfirmedAmountKrw = 100,
            Allocations = [new() { EntryId = "refund", AmountKrw = 100 }] };
        f.Db.Add(new NeighborhoodSettlementPaymentRecord { PaymentId = payment.PaymentId, AgreementId = payment.AgreementId,
            DetailsJson = Serialize(payment), StatusCode = C.RecipientConfirmed }); await f.Db.SaveChangesAsync();
        var read = await f.Use().GetAsync("root", "driver", default);
        Assert.Equal(C.CargoRequest, Assert.Single(read.Sources).SourceKind);
        var refund = Assert.Single(read.AmountGroups, x => x.Kind == C.Adjustment);
        Assert.Equal(-100, refund.KnownAccruedAmountKrw); Assert.Equal(-100, refund.ReportedPaymentAmountKrw);
        Assert.Equal(-100, refund.RecipientConfirmedAmountKrw);
    }

    [Fact]
    public async Task 기존배송참고금액과내부작업통과자금은새작업보수와분리한다()
    {
        await using var f = await Fixture.Create(); var source = f.Mart(1, "ref-1", "picker");
        f.Agreement("a1", source, [new() { LineKey = "delivery", Kind = C.Delivery, AmountKrw = 4000,
            IncludedInDelivery = true, PayerUserId = "seller", PayeeUserId = "picker" }]);
        f.Entry("reference", "a1", "delivery", "picker", 4000, source.CompletedWork[0], C.Delivery);
        var entry = f.Db.Set<NeighborhoodSettlementEntryRecord>().Local.Single();
        var detail = JsonSerializer.Deserialize<NeighborhoodSettlementEntryDto>(entry.DetailsJson, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        detail.IncludedInDelivery = true; detail.StatusCode = C.Expected; entry.DetailsJson = Serialize(detail); entry.StatusCode = C.Expected;
        await f.Db.SaveChangesAsync();
        var group = Assert.Single((await f.Use().GetAsync("ssalddel-mart:ref-1", "picker", default)).AmountGroups);
        Assert.Equal("DeliveryReferenceOnly", group.AccountingCode); Assert.Equal(4000, group.ReferenceAmountKrw);
        Assert.Equal(0, group.AccruedEntryCount); Assert.Equal(0, group.KnownAccruedAmountKrw); Assert.Null(group.CompleteAccruedAmountKrw);
    }

    [Theory]
    [InlineData(C.Fixed, 300, 3, "Accrued")]
    [InlineData(C.Free, 0, 3, "Accrued")]
    [InlineData(C.PerUnit, 100, 1, "PartiallyAccrued")]
    public async Task 합의해시판본영과실제완료판본이다르더라도증빙으로연결하고청구단위와물리수량을구별한다(
        string billing, decimal amount, decimal covered, string status)
    {
        await using var f = await Fixture.Create(); var original = f.Mart(1, "ref-1", "picker");
        var source = new NeighborhoodSettlementSourceSnapshot { SourceKind = original.SourceKind, SourceId = original.SourceId,
            SourceVersion = "hashed-source-version", SourceDigest = original.SourceDigest, Participants = original.Participants,
            AllowedActivityKinds = original.AllowedActivityKinds, ObservedAtUtc = original.ObservedAtUtc,
            CompletedWork = [original.CompletedWork[0] with { OriginRevision = 15 }] };
        f.Resolver.Rows["MartOrder:1"] = source;
        f.Agreement("a1", source, [Line("pick", "picker", amount)]);
        f.Entry("e1", "a1", "pick", "picker", amount, source.CompletedWork[0]);
        var entry = f.Db.Set<NeighborhoodSettlementEntryRecord>().Local.Single();
        var detail = JsonSerializer.Deserialize<NeighborhoodSettlementEntryDto>(entry.DetailsJson, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        detail.Billing = billing; detail.Quantity = 1; detail.OriginRevision = 0;
        entry.OriginRevision = 0; entry.DetailsJson = Serialize(detail); await f.Db.SaveChangesAsync();
        var work = Assert.Single(Assert.Single((await f.Use().GetAsync("ssalddel-mart:ref-1", "picker", default)).Sources).MyCompletedWork);
        Assert.Equal(15, work.OriginRevision); Assert.Equal(3, work.ConfirmedQuantity);
        Assert.Equal(covered, work.AccruedQuantity); Assert.Equal(status, work.CompensationStatusCode);
        Assert.Equal("e1", Assert.Single(work.EntryIds));
    }

    [Fact]
    public async Task 기존기사원장참고항목은본인작업에도새보수발생으로표시하지않는다()
    {
        await using var f = await Fixture.Create(); var root = f.Root("root", "buyer");
        var source = f.Cargo("cargo", "transport:cargo");
        root.포함원장목록 = [new() { 원장Id = "transport:cargo", 원장템플릿Key = CommunityLedgerTemplateKeys.CargoTransport, 역할 = 주문원장포함역할.운송 }];
        f.Agreement("a1", source, [Line("delivery", "driver", 4000, C.Delivery)]);
        f.Entry("e1", "a1", "delivery", "driver", 4000, source.CompletedWork[0], C.Delivery);
        var row = f.Db.Set<NeighborhoodSettlementEntryRecord>().Local.Single();
        var detail = JsonSerializer.Deserialize<NeighborhoodSettlementEntryDto>(row.DetailsJson, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        detail.IncludedInDelivery = true; detail.StatusCode = C.Expected; row.DetailsJson = Serialize(detail); row.StatusCode = C.Expected;
        await f.Db.SaveChangesAsync();
        var work = Assert.Single(Assert.Single((await f.Use().GetAsync("root", "driver", default)).Sources).MyCompletedWork);
        Assert.Equal("OriginalLedgerReference", work.CompensationStatusCode); Assert.Equal(0, work.AccruedQuantity);
        Assert.Empty(work.EntryIds); Assert.Equal("e1", Assert.Single(work.ReferenceEntryIds));
    }

    [Fact]
    public async Task 지급신고와부분수령진술은저장배분순서로표시하고은행확인으로승격하지않는다()
    {
        await using var f = await Fixture.Create(); var source = f.Mart(1, "ref-1", "picker");
        f.Agreement("a1", source, [Line("pick", "picker", 300)]);
        f.Entry("e1", "a1", "pick", "picker", 300, source.CompletedWork[0]);
        var payment = new NeighborhoodSettlementPaymentDto { PaymentId = "p1", AgreementId = "a1", PayerUserId = "seller",
            PayeeUserId = "picker", AmountKrw = 300, RecipientConfirmedAmountKrw = 120,
            Allocations = [new() { EntryId = "e1", AmountKrw = 300 }], IsActualBankVerified = true };
        f.Db.Add(new NeighborhoodSettlementPaymentRecord { PaymentId = "p1", AgreementId = "a1", DetailsJson = Serialize(payment),
            StatusCode = C.PartiallyConfirmed }); await f.Db.SaveChangesAsync();
        var read = await f.Use().GetAsync("ssalddel-mart:ref-1", "picker", default);
        var group = Assert.Single(read.AmountGroups);
        Assert.Equal(300, group.ReportedPaymentAmountKrw); Assert.Equal(120, group.RecipientConfirmedAmountKrw);
        Assert.False(group.IsActualBankVerified);
        Assert.False(Assert.Single(Assert.Single(Assert.Single(read.Sources).Settlements).Payments).IsActualBankVerified);
    }

    private static NeighborhoodSettlementTermLine Line(string key, string worker, decimal amount, string kind = C.Picking)
        => new() { LineKey = key, Kind = kind, AmountKrw = amount, Quantity = 3,
            PayerUserId = "seller", PayeeUserId = worker, BeneficiaryRole = kind == "Packing" ? "Packer" : "Picker" };
    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, new JsonSerializerOptions(JsonSerializerDefaults.Web));

    private sealed class Resolver : INeighborhoodSettlementSourceResolver
    {
        public Dictionary<string, NeighborhoodSettlementSourceSnapshot> Rows { get; } = [];
        public List<string> Calls { get; } = [];
        public Task<NeighborhoodSettlementSourceSnapshot> ResolveAsync(string kind, string id, CancellationToken ct)
        { Calls.Add(kind + ":" + id); return Task.FromResult(Rows[kind + ":" + id]); }
    }
    private sealed class Ledgers : I커뮤니티원장저장소
    {
        public Dictionary<string, 커뮤니티원장Dto> Rows { get; } = [];
        public int Writes { get; private set; }
        public int ListCalls { get; private set; }
        public string? ChangeRootOnSecondRead { get; set; }
        private readonly Dictionary<string, int> _reads = [];
        public Task<커뮤니티원장Dto?> 원장조회Async(string 원장Id, CancellationToken cancellationToken = default)
        {
            _reads[원장Id] = _reads.GetValueOrDefault(원장Id) + 1;
            var row = Rows.GetValueOrDefault(원장Id);
            if (row is not null && 원장Id == ChangeRootOnSecondRead && _reads[원장Id] == 2)
                return Task.FromResult<커뮤니티원장Dto?>(new() { 원장Id = row.원장Id, Revision = row.Revision + 1 });
            return Task.FromResult(row);
        }
        public Task<IReadOnlyList<커뮤니티원장Dto>> 원장목록조회Async(커뮤니티원장조회조건 query, CancellationToken cancellationToken = default)
        { ListCalls++; throw new InvalidOperationException("전체 원장 탐색 금지"); }
        public Task<커뮤니티원장Dto> 원장저장Async(커뮤니티원장저장요청 request, string updatedBy, CancellationToken cancellationToken = default)
        { Writes++; throw new InvalidOperationException("조회 중 쓰기 금지"); }
        public Task<커뮤니티원장Dto?> 원장상태변경Async(커뮤니티원장상태변경요청 request, string updatedBy, CancellationToken cancellationToken = default)
        { Writes++; throw new InvalidOperationException("조회 중 쓰기 금지"); }
    }
    private sealed class Disclosures : I주문원장공개요청저장소
    {
        public HashSet<string> Grants { get; } = [];
        public bool RevokeOnSecondRead { get; set; }
        private int _reads;
        public Task<IReadOnlySet<string>> 승인된대상원장Ids조회Async(string 주문원장Id, string 요청자UserId,
            IEnumerable<string> 대상원장Ids, DateTimeOffset 기준시각Utc, CancellationToken cancellationToken = default)
        {
            _reads++; if (RevokeOnSecondRead && _reads == 2) Grants.Clear();
            return Task.FromResult<IReadOnlySet<string>>(Grants.Intersect(대상원장Ids).ToHashSet());
        }
        public Task<원장공개요청기록> 요청생성Async(원장공개요청기록 요청, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<원장공개요청기록?> 요청조회Async(string 요청Id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<원장공개요청기록>> 받은요청목록Async(string 승인자UserId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<원장공개요청기록?> 요청결정Async(string 요청Id, string 승인자UserId, bool 승인여부, string? 처리메모,
            DateTimeOffset 처리시각Utc, DateTimeOffset 승인만료시각Utc, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    private sealed class Encryption : IPersonalDataEncryptionService
    { public string? Protect(string? value) => value; public string? Unprotect(string? value) => value; }
    private sealed class Fixture(SqliteConnection connection, SsalddelContext db) : IAsyncDisposable
    {
        public SsalddelContext Db { get; } = db;
        public Ledgers Ledgers { get; } = new(); public Disclosures Disclosures { get; } = new(); public Resolver Resolver { get; } = new();
        private readonly DateTime _at = new(2026, 10, 13, 0, 0, 0, DateTimeKind.Utc);
        public 주문별원가보수조회Service Use()
        {
            var execution = new SsalddelExecutionModePolicy(Options.Create(new SsalddelExecutionOptions { Mode = SsalddelExecutionMode.Simulation }));
            var options = Options.Create(new NeighborhoodSettlementOptions { Enabled = true });
            var settlement = new NeighborhoodSettlementUseCase(Db, Resolver, execution, options, TimeProvider.System);
            return new(Db, Ledgers, Disclosures, Resolver, settlement, execution, options, TimeProvider.System);
        }
        public 커뮤니티원장Dto Root(string id, string creator, IReadOnlyList<커뮤니티포함원장참조Dto>? children = null)
        {
            var root = new 커뮤니티원장Dto { 원장Id = id, 생성자UserId = creator, 커뮤니티Id = "platform",
                원장템플릿Key = CommunityLedgerTemplateKeys.Order, Revision = 1, 포함원장목록 = children ?? [] };
            Ledgers.Rows[id] = root; return root;
        }
        public NeighborhoodSettlementSourceSnapshot Mart(long id, string reference, string worker)
        {
            var ledgerId = "ssalddel-mart:" + reference;
            var root = Root(ledgerId, "buyer"); root.원장템플릿Key = CommunityLedgerTemplateKeys.SsalddelMart;
            root.외부참조 = new Dictionary<string, string> { ["원천유형"] = "SsalddelMartOrder", ["원천Id"] = reference };
            root.참여자목록 = [new() { UserId = "seller", RoleLabel = "판매자" }];
            Db.Add(new 마트주문 { Id = id, 주문참조번호 = reference, 주문자UserId = "buyer", 판매자UserId = "seller",
                커뮤니티원장Id = ledgerId, 커뮤니티원장템플릿Key = CommunityLedgerTemplateKeys.SsalddelMart });
            var source = new NeighborhoodSettlementSourceSnapshot { SourceKind = "MartOrder", SourceId = id.ToString(), SourceVersion = "1",
                SourceDigest = "digest-" + id, ObservedAtUtc = _at, AllowedActivityKinds = [C.StoredGoods],
                Participants = [new("buyer", ["Buyer"]), new("seller", ["Seller"]), new(worker, ["Picker"]), new("packer", ["Packer"])],
                CompletedWork = [new(C.Picking, "MartTaskCompletion", "task-" + id, 3, _at.AddMinutes(-1), ["proof-" + id], worker, OriginRevision: 1)] };
            Resolver.Rows["MartOrder:" + id] = source; return source;
        }
        public void Agreement(string id, NeighborhoodSettlementSourceSnapshot source, IReadOnlyList<NeighborhoodSettlementTermLine> lines)
        {
            var terms = new NeighborhoodSettlementAgreementRequest { SourceKind = source.SourceKind, SourceId = source.SourceId,
                ActivityKind = C.StoredGoods, Lines = lines };
            Db.Add(new NeighborhoodSettlementAgreementRecord { AgreementId = id, SourceKind = source.SourceKind, SourceId = source.SourceId,
                CreatorUserId = "seller", ActivityKind = C.StoredGoods, TermsJson = Serialize(terms), SourceSnapshotJson = Serialize(source), StatusCode = C.Active });
            Db.Add(new NeighborhoodSettlementAgreementVersionRecord { AgreementId = id, TermsVersion = 1,
                TermsJson = Serialize(terms), SourceSnapshotJson = Serialize(source) });
        }
        public void Entry(string id, string agreement, string line, string payee, decimal amount, NeighborhoodSettlementCompletedWork work, string kind = C.Picking)
        {
            var dto = new NeighborhoodSettlementEntryDto { EntryId = id, AgreementId = agreement, LineKey = line, TermsVersion = 1,
                Kind = kind, Billing = C.Fixed, AmountKrw = amount, PayerUserId = "seller", PayeeUserId = payee, Quantity = 1,
                OriginKind = work.OriginKind, OriginId = work.OriginId, OriginRevision = work.OriginRevision,
                WorkEvidenceReferences = work.EvidenceReferences, StatusCode = C.Accrued };
            Db.Add(new NeighborhoodSettlementEntryRecord { EntryId = id, AgreementId = agreement, LineKey = line, TermsVersion = 1,
                Kind = kind, AccrualKey = id, DetailsJson = Serialize(dto), StatusCode = C.Accrued });
        }
        public void Distributed(string id, long outboundId, int quantity)
        {
            // 같은 실물 입고 Lot을 두 주문이 나눠 쓰더라도 FK가 있는 원천 계층은 한 번만 준비합니다.
            if (!Db.Set<분산재고LotRecord>().Local.Any(x => x.LotId == "same-lot"))
            {
                Db.Add(new 분산거점Record { SiteId = "site", WarehouseId = 1, OwnerUserId = "seller", ManagerUserId = "seller" });
                Db.Add(new 분산적재위치Record { LocationId = "location", SiteId = "site", Barcode = "shelf-barcode" });
                Db.Add(new 분산입고계획Record { PlanId = "inbound-plan", CreatorUserId = "seller" });
                Db.Add(new 분산재고LotRecord { LotId = "same-lot", PlanId = "inbound-plan", LineKey = "inbound-line",
                    SiteId = "site", LocationId = "location", InboundItemId = 1, OwnerUserId = "seller", SellerUserId = "seller",
                    ExpectedQuantity = 10, ReceivedQuantity = 10 });
            }
            var childId = "warehouse-outbound:" + id; var root = Root("root-" + id, "seller",
                [new() { 원장Id = childId, 원장템플릿Key = CommunityLedgerTemplateKeys.WarehouseOutbound, 역할 = 주문원장포함역할.창고출고 },
                    new() { 원장Id = "transport:cargo-" + id, 원장템플릿Key = CommunityLedgerTemplateKeys.CargoTransport, 역할 = 주문원장포함역할.운송 }]);
            Cargo("cargo-" + id, "transport:cargo-" + id);
            Ledgers.Rows[childId] = new() { 원장Id = childId, 생성자UserId = "seller", Revision = 1, 커뮤니티Id = "platform",
                원장템플릿Key = CommunityLedgerTemplateKeys.WarehouseOutbound,
                외부참조 = new Dictionary<string, string> { ["원천유형"] = "WarehouseOutboundPlanned", ["원천Id"] = id } };
            Db.Add(new 출고예정 { Id = outboundId, 주문참조번호 = id, 커뮤니티원장Id = childId,
                커뮤니티원장템플릿Key = CommunityLedgerTemplateKeys.WarehouseOutbound, 판매자UserId = "seller", 주문자UserId = "buyer", 수량 = quantity });
            var connection = new DistributedOrderLedgerConnection { OrderId = id, RootOrderLedgerId = root.원장Id,
                SellerUserId = "seller", CustomerUserId = "buyer", CargoRequestId = "cargo-" + id, OutboundPlanIds = [outboundId] };
            Db.Add(new 분산배송WaveRecord { WaveId = "wave-" + id, RoundId = "round-" + id, RouteEvidenceJson = Serialize(new
                { orderLedgerConnections = new Dictionary<string, DistributedOrderLedgerConnection> { [id] = connection } }) });
            Db.Add(new 분산배송주문Record { OrderId = id, WaveId = "wave-" + id, SellerUserId = "seller", CustomerUserId = "buyer", CargoRequestId = "cargo-" + id });
            Db.Add(new 분산재고예약Record { ReservationId = "reservation-" + id, OrderId = id, WaveId = "wave-" + id,
                LotId = "same-lot", OutboundPlanId = outboundId, Quantity = quantity });
            Resolver.Rows[C.DistributedOrder + ":" + id] = new() { SourceKind = C.DistributedOrder, SourceId = id,
                SourceVersion = "1", SourceDigest = "digest-" + id, ObservedAtUtc = _at,
                Participants = [new("seller", ["Seller", "Owner"]), new("buyer", ["Buyer"]), new("picker", ["Picker"])],
                CompletedWork = [new(C.Picking, "PickReceipt", "pick-" + id, quantity, _at, ["proof-" + id], "picker", OriginRevision: 1)] };
            Agreement("order-agreement-" + id, Resolver.Rows[C.DistributedOrder + ":" + id], []);
        }
        public NeighborhoodSettlementSourceSnapshot Cargo(string cargoId, string ledgerId)
        {
            Ledgers.Rows[ledgerId] = new() { 원장Id = ledgerId, 생성자UserId = "seller", Revision = 1, 커뮤니티Id = "platform",
                원장템플릿Key = CommunityLedgerTemplateKeys.CargoTransport, 외부참조 = new Dictionary<string, string>
                { ["화주운송의뢰Id"] = cargoId, ["원천유형"] = "CargoTransport", ["원천Id"] = cargoId } };
            Db.Add(new 운송원장 { 의뢰Id = cargoId, 운송번호 = cargoId, 원본의뢰유형 = "CargoTransport", 원본의뢰Id = cargoId,
                커뮤니티원장Id = ledgerId, 커뮤니티원장템플릿Key = CommunityLedgerTemplateKeys.CargoTransport, 확정기사Id = "driver" });
            var source = new NeighborhoodSettlementSourceSnapshot { SourceKind = C.CargoRequest, SourceId = cargoId,
                SourceVersion = "1", SourceDigest = "cargo-digest-" + cargoId, ObservedAtUtc = _at,
                Participants = [new("seller", ["Seller"]), new("buyer", ["Buyer"]), new("driver", ["Driver"])],
                CompletedWork = [new(C.Delivery, "CargoDelivery", cargoId, 1, _at, ["cargo-proof-" + cargoId], "driver", OriginRevision: 1)] };
            Resolver.Rows[C.CargoRequest + ":" + cargoId] = source; return source;
        }
        public void Cost(string id, int quantity, decimal amount)
        {
            var detail = new NeighborhoodInventoryCostAttributionDto { OrderId = id, LotId = "same-lot", SourceEntryId = "inbound-entry",
                SourceAgreementId = "inbound-agreement", SourceEntryRevision = 1, SourceQuantity = 10, AllocatedQuantity = quantity,
                AmountKrw = amount, Kind = C.Inbound, EvidenceDigest = "attribution-proof-" + id };
            Db.Add(new NeighborhoodInventoryCostAttributionRecord { AttributionId = "cost-" + id, SourceEntryId = detail.SourceEntryId,
                SourceAgreementId = detail.SourceAgreementId, OrderId = id, AgreementId = "order-agreement-" + id,
                CalculationDigest = "calculation-" + id, DetailsJson = Serialize(detail), RecordedAtUtc = _at });
        }
        public static async Task<Fixture> Create()
        {
            var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
            var db = new SsalddelContext(new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(connection).Options, new Encryption());
            await db.Database.EnsureCreatedAsync(); return new(connection, db);
        }
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await connection.DisposeAsync(); }
    }
}
