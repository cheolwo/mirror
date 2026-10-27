using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentResults;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Application.Mart;
using Ssalddel.Application.Warehouse;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Controllers.Common;
using Ssalddel.Services.Community;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Options;
using 살뜰.Services.Versioning;
using 살뜰.도메인.창고;

namespace Ssalddel.Tests.Services.Community;

/// <summary>격리 조회·관계 검증 시험이며 실제 주문·작업·배송·기기 실행의 증거가 아닙니다.</summary>
public sealed class 공간공동운영통합Tests
{
    [Fact]
    public async Task 기존상위원장_조회권한이_없으면_주문과전문원장을_읽지않는다()
    {
        await using var f = new Fixture();
        Assert.Null(await f.Service(null).조회Async(f.Detail.LedgerId));
        Assert.Null(await f.Service("stranger").조회Async(f.Detail.LedgerId));
        Assert.Equal(0, f.OrderCalls); Assert.Empty(f.Ledgers.ReadIds);
    }

    [Fact]
    public async Task 기능OFF와_전문업무OFF는_빈목록확정이아닌_조회차단으로보이고_상위회차는유지한다()
    {
        await using var f = new Fixture(); f.Features.Enabled = false;
        var disabled = await f.Read();
        Assert.Equal("feature-disabled", disabled.OrdersReadStatusCode);
        Assert.True(disabled.HasIncompleteVisibility); Assert.Single(disabled.Cycles); Assert.Equal(0, f.OrderCalls);
        f.Features.Enabled = true; f.OrderFailure = new SpaceOrderException("SpaceOrderWorkflowDisabled", 404);
        var workflow = await f.Read();
        Assert.Equal("workflow-disabled", workflow.OrdersReadStatusCode); Assert.Single(workflow.Cycles);
        Assert.Empty(f.Ledgers.ReadIds);
    }

    [Fact]
    public async Task 주문목록의_부모판본이다르면_다른조회판본을_계층으로합치지않는다()
    {
        await using var f = new Fixture(); f.RowsRevision = f.Detail.Revision + 1;
        f.AddOrder("a");
        var result = await f.Read();
        Assert.Equal("revision-changed", result.OrdersReadStatusCode);
        Assert.Empty(result.Cycles.Single().Orders); Assert.Empty(f.Ledgers.ReadIds);
    }

    [Fact]
    public async Task 주문제안과_실제원장미생성은_구분하며_일반완료문구로_전문완료를만들지않는다()
    {
        await using var f = new Fixture(); var proposed = f.AddOrder("proposal");
        var before = await f.Read();
        var first = Assert.Single(before.Cycles.Single().Orders);
        Assert.Equal("proposed", first.AgreementStatusCode); Assert.Equal("not-created", first.RootReadStatusCode);
        Assert.Empty(first.Children);
        proposed.Row.BuyerConsented = true; proposed.Row.SellerConsented = true;
        f.Ledgers.Rows.Add(proposed.Row.RootOrderLedgerId, f.Root(proposed.Row));
        f.Ledgers.Rows[proposed.Row.RootOrderLedgerId].상태 = "완료";
        var connected = Assert.Single((await f.Read()).Cycles.Single().Orders);
        Assert.Equal("verified-link", connected.RootReadStatusCode); Assert.Empty(connected.Children);
    }

    [Fact]
    public async Task 현재계정의_기존주문필터를_유지하고_다른주문ID나상품명을_상위참여자에게노출하지않는다()
    {
        await using var f = new Fixture(); f.AddOrder("visible", buyer: "buyer");
        var secret = f.AddOrder("secret-product", buyer: "other-buyer", seller: "other-seller");
        var buyer = await f.Read("buyer");
        Assert.Equal("visible", Assert.Single(buyer.Cycles.Single().Orders).ProductName);
        var json = JsonSerializer.Serialize(buyer);
        Assert.DoesNotContain(secret.Row.OrderRequestId.ToString(), json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret-product", json, StringComparison.Ordinal);
        var operatorOnly = await f.Read("operator");
        Assert.Empty(operatorOnly.Cycles.Single().Orders);
        Assert.Empty(f.Ledgers.ReadIds.Where(id => id == secret.Row.RootOrderLedgerId));
    }

    [Fact]
    public async Task 여러주문은_같은입고를_공유하되_실제예약계보와다른주문을_하위로붙이지않는다()
    {
        await using var f = new Fixture(); var first = f.AddOrder("first"); var second = f.AddOrder("second");
        f.AddCycleSource("DistributedSite", "site"); f.AddCycleSource("InboundPlan", "inbound");
        f.AddCycleSource("DistributedWave", "wave"); f.AddCycleSource("InboundPlan", "unrelated-inbound");
        await f.SeedActual(first); await f.SeedActual(second);
        var result = await f.Read(); var rows = result.Cycles.Single().Orders;
        Assert.Equal(2, rows.Count);
        Assert.Single(result.SharedSources.Where(source => source.KindCode == "InboundPlan" && source.ReferenceId == "inbound"));
        foreach (var row in rows)
        {
            Assert.Equal("verified-link", row.RootReadStatusCode);
            Assert.Contains(row.Children, source => source.KindCode == "InboundPlan" && source.ReferenceId == "inbound");
            Assert.DoesNotContain(row.Children, source => source.ReferenceId == "unrelated-inbound");
            Assert.Single(row.Children.Where(source => source.KindCode == "DistributedOrder"));
            var other = row.OrderRequestId == first.Row.OrderRequestId ? second : first;
            Assert.DoesNotContain(row.Children, source => source.ReferenceId == other.Row.DistributedOrderId);
        }
        Assert.Contains(result.Relations, relation => relation.FromKey == rows[0].NodeKey
            && relation.ToKey == "source:InboundPlan:inbound" && relation.RelationCode == "source-lineage");
        Assert.Equal(result.Relations.Count, result.Relations.Select(link => (link.FromKey, link.ToKey, link.RelationCode)).Distinct().Count());
        Assert.Equal(0, f.Db.ChangeTracker.Entries().Count(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted));
    }

    [Fact]
    public async Task 주문계보가바뀌거나_root당사자가아니면_일반원장완료와무관하게_하위를숨긴다()
    {
        await using var f = new Fixture(); var order = f.AddOrder("one"); await f.SeedActual(order);
        var actual = await f.Db.Set<분산배송주문Record>().SingleAsync(); actual.CargoRequestId = "other-cargo";
        await f.Db.SaveChangesAsync();
        var changed = Assert.Single((await f.Read()).Cycles.Single().Orders);
        Assert.Equal("lineage-unverified", changed.RootReadStatusCode); Assert.Empty(changed.Children);
        var root = f.Ledgers.Rows[order.Row.RootOrderLedgerId]; root.생성자UserId = "other"; root.참여자목록 = [];
        var hidden = Assert.Single((await f.Read()).Cycles.Single().Orders);
        Assert.Equal("not-visible", hidden.RootReadStatusCode); Assert.Empty(hidden.Children);
    }

    [Fact]
    public async Task 실제주문의_root지문이변조되면_검증된연결과하위를유지하지않는다()
    {
        await using var f = new Fixture(); var order = f.AddOrder("one"); await f.SeedActual(order);
        Assert.NotEmpty(Assert.Single((await f.Read()).Cycles.Single().Orders).Children);
        var root = f.Ledgers.Rows[order.Row.RootOrderLedgerId];
        root.외부참조 = new Dictionary<string, string>(root.외부참조) { ["space-cooperation-order-fingerprint"] = "changed-fingerprint" };

        var result = await f.Read(); var changed = Assert.Single(result.Cycles.Single().Orders);
        Assert.Equal("lineage-unverified", changed.RootReadStatusCode); Assert.Empty(changed.Children);
        Assert.DoesNotContain(result.Relations, link => link.FromKey == changed.NodeKey && link.RelationCode == "source-lineage");
    }

    [Fact]
    public async Task 실제주문의_root조건블록이변조되면_검증된연결과하위를유지하지않는다()
    {
        await using var f = new Fixture(); var order = f.AddOrder("one"); await f.SeedActual(order);
        Assert.NotEmpty(Assert.Single((await f.Read()).Cycles.Single().Orders).Children);
        var terms = JsonSerializer.Deserialize<공간공동운영주문조건Dto>(JsonSerializer.Serialize(order.Row.Terms))!;
        terms.Quantity++;
        var block = f.Ledgers.Rows[order.Row.RootOrderLedgerId].블록목록.Single(block => block.BlockId == "space-order-terms");
        block.Data = new Dictionary<string, string>(block.Data)
        { ["terms"] = JsonSerializer.Serialize(terms, new JsonSerializerOptions(JsonSerializerDefaults.Web)) };

        var result = await f.Read(); var changed = Assert.Single(result.Cycles.Single().Orders);
        Assert.Equal("lineage-unverified", changed.RootReadStatusCode); Assert.Empty(changed.Children);
        Assert.DoesNotContain(result.Relations, link => link.FromKey == changed.NodeKey && link.RelationCode == "source-lineage");
    }

    [Fact]
    public async Task 모드불일치와_하위조회실패를_정상자료나_예외원문으로바꾸지않는다()
    {
        await using var f = new Fixture(); f.Detail.ExecutionMode = "Operational"; f.AddCycleSource("InboundPlan", "inbound");
        var mismatch = await f.Read();
        Assert.Equal("execution-mode-mismatch", mismatch.OrdersReadStatusCode);
        Assert.Equal(0, f.OrderCalls); Assert.False(Assert.Single(mismatch.SharedSources).Verified);
        f.Detail.ExecutionMode = "Simulation"; f.OrderFailure = new InvalidOperationException("private-connection-string");
        var unavailable = await f.Read();
        Assert.Equal("temporarily-unavailable", unavailable.OrdersReadStatusCode);
        Assert.DoesNotContain("private-connection-string", JsonSerializer.Serialize(unavailable), StringComparison.Ordinal);
    }

    [Fact]
    public async Task API는_인증필수와_private_no_store를_유지한다()
    {
        await using var f = new Fixture();
        Assert.NotEmpty(typeof(공간공동운영통합Controller).GetCustomAttributes(typeof(AuthorizeAttribute), true));
        var controller = new 공간공동운영통합Controller(f.Service())
        { ControllerContext = new() { HttpContext = new DefaultHttpContext() } };
        Assert.IsType<OkObjectResult>(await controller.조회(f.Detail.LedgerId, default));
        Assert.Equal("private, no-store", controller.Response.Headers.CacheControl.ToString());
    }

    private sealed record PartyOrder(공간공동운영주문Response Row, string Buyer, string Seller);
    private sealed class Fixture : IAsyncDisposable
    {
        public SsalddelContext Db { get; } = new(new DbContextOptionsBuilder<SsalddelContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options, new Encryption());
        public 공간공동운영상세Response Detail { get; } = new()
        {
            LedgerId = "cooperation", Revision = 3, TermsRevision = 1, ExecutionMode = "Simulation",
            Cycles = [new() { CycleId = "cycle", Number = 1, TermsRevision = 1 }],
            SharedOutcomes = new() { Cycles = [new() { CycleId = "cycle", Number = 1 }] }
        };
        public Flags Features { get; } = new(); public LedgerStore Ledgers { get; } = new();
        public Dictionary<Guid, 공간공동운영주문UseCase.OrderRecord> CanonicalOrders { get; } = [];
        public List<PartyOrder> Rows { get; } = []; public long? RowsRevision { get; set; }
        public Exception? OrderFailure { get; set; } public int OrderCalls { get; set; }
        public 공간공동운영통합Query Service(string? actor = "buyer") => new(new Core(this, actor), new Orders(this, actor), Ledgers,
            Db, new Actor(actor), new ExecutionModeFake(), Features, TimeProvider.System);
        public async Task<공간공동운영통합Response> Read(string actor = "buyer")
            => Assert.IsType<공간공동운영통합Response>(await Service(actor).조회Async(Detail.LedgerId));
        public PartyOrder AddOrder(string product, string buyer = "buyer", string seller = "seller")
        {
            var id = Guid.NewGuid(); var row = new 공간공동운영주문Response
            {
                LedgerId = Detail.LedgerId, Revision = Detail.Revision, OrderRequestId = id, TermsVersion = 1,
                ProductName = product, StatusCode = "proposed", RootOrderLedgerId = "space-order:" + id.ToString("N"),
                Terms = new() { OrderRequestId = id, CycleId = "cycle", SiteId = "site", WaveId = "wave", LotId = "lot", CargoRequestId = "cargo-" + id.ToString("N"), Quantity = 2 }
            };
            var party = new PartyOrder(row, buyer, seller); Rows.Add(party); return party;
        }
        public void AddCycleSource(string kind, string id)
        {
            var reference = new 공간공동운영전문참조Dto { KindCode = kind, ReferenceId = id };
            Detail.Cycles[0].References = Detail.Cycles[0].References.Append(reference).ToArray();
            var cycle = Detail.SharedOutcomes!.Cycles[0];
            cycle.Sources = cycle.Sources.Append(new 공간공동운영원천상태Dto
            { KindCode = kind, ReferenceId = id, Verified = true, Completed = true, VerifiedRevision = 1, StatusCode = "confirmed", Label = kind }).ToArray();
        }
        public 커뮤니티원장Dto Root(공간공동운영주문Response row)
        {
            var party = Rows.Single(item => item.Row == row); var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            var buyerAt = new DateTimeOffset(2026, 10, 24, 0, 0, 0, TimeSpan.Zero); var sellerAt = buyerAt.AddMinutes(1);
            var sourceDigest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("source:" + row.OrderRequestId.ToString("N"))));
            CanonicalOrders[row.OrderRequestId] = new()
            {
                Terms = JsonSerializer.Deserialize<공간공동운영주문조건Dto>(JsonSerializer.Serialize(row.Terms, json), json)!,
                TermsVersion = row.TermsVersion, BuyerUserId = party.Buyer, SellerUserId = party.Seller,
                BuyerConsentedAtUtc = buyerAt, SellerConsentedAtUtc = sellerAt, SourceDigest = sourceDigest,
                RootOrderLedgerId = row.RootOrderLedgerId
            };
            var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
            {
                Detail.LedgerId, row.Terms, row.TermsVersion, BuyerUserId = party.Buyer, SellerUserId = party.Seller,
                BuyerConsentedAtUtc = (DateTimeOffset?)buyerAt, SellerConsentedAtUtc = (DateTimeOffset?)sellerAt, SourceDigest = sourceDigest
            }, json))));
            return new()
            {
                원장Id = row.RootOrderLedgerId, Revision = 1, 커뮤니티Id = "platform", 원장템플릿Key = CommunityLedgerTemplateKeys.Order,
                생성자UserId = party.Buyer,
                외부참조 = new Dictionary<string, string> { ["space-cooperation-id"] = Detail.LedgerId,
                    ["space-cooperation-order-request"] = row.OrderRequestId.ToString("N"), ["space-cooperation-order-fingerprint"] = fingerprint },
                블록목록 = [new() { BlockId = "space-order-terms", BlockType = CommunityLedgerBlockTypes.Order, State = "확인됨",
                    Data = new Dictionary<string, string> { ["terms"] = JsonSerializer.Serialize(row.Terms, json), ["termsVersion"] = row.TermsVersion.ToString(CultureInfo.InvariantCulture),
                        ["sourceFingerprint"] = sourceDigest, ["buyerConsentedAtUtc"] = buyerAt.ToString("O"), ["sellerConsentedAtUtc"] = sellerAt.ToString("O") } }],
                참여자목록 = [new() { UserId = party.Buyer, RoleLabel = "구매자", ParticipationState = "참여중" },
                    new() { UserId = party.Seller, RoleLabel = "판매자", ParticipationState = "참여중" }]
            };
        }
        public async Task SeedActual(PartyOrder order)
        {
            var row = order.Row; row.StatusCode = "confirmed"; row.DistributedOrderId = "distributed-" + row.OrderRequestId.ToString("N");
            row.BuyerConsented = true; row.SellerConsented = true;
            Ledgers.Rows.Add(row.RootOrderLedgerId, Root(row));
            if (!await Db.Set<분산재고LotRecord>().AnyAsync()) Db.Add(new 분산재고LotRecord
            { LotId = "lot", PlanId = "inbound", SiteId = "site", SellerUserId = order.Seller, OwnerUserId = order.Seller });
            var wave = await Db.Set<분산배송WaveRecord>().SingleOrDefaultAsync();
            if (wave is null) { wave = new() { WaveId = "wave" }; Db.Add(wave); }
            var connections = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(wave.RouteEvidenceJson)!
                .GetValueOrDefault("orderLedgerConnections");
            var all = connections.ValueKind == JsonValueKind.Object
                ? connections.Deserialize<Dictionary<string, DistributedOrderLedgerConnection>>(new JsonSerializerOptions(JsonSerializerDefaults.Web))! : new();
            all[row.DistributedOrderId] = new() { OrderId = row.DistributedOrderId, RootOrderLedgerId = row.RootOrderLedgerId,
                SellerUserId = order.Seller, CustomerUserId = order.Buyer, CargoRequestId = row.Terms.CargoRequestId };
            wave.RouteEvidenceJson = JsonSerializer.Serialize(new { orderLedgerConnections = all }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            Db.Add(new 분산배송주문Record { OrderId = row.DistributedOrderId, WaveId = "wave", SellerUserId = order.Seller,
                CustomerUserId = order.Buyer, CargoRequestId = row.Terms.CargoRequestId, OrderReference = row.RootOrderLedgerId });
            Db.Add(new 분산재고예약Record { ReservationId = "reservation-" + row.OrderRequestId.ToString("N"), OrderId = row.DistributedOrderId,
                WaveId = "wave", LotId = "lot", Quantity = 2 });
            await Db.SaveChangesAsync(); AddCycleSource("DistributedOrder", row.DistributedOrderId);
        }
        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
    private sealed record Actor(string? UserId) : ICurrentUserAccessor { public string? Role => "community"; }
    private sealed class ExecutionModeFake : ISsalddelExecutionModePolicy
    { public SsalddelExecutionMode Mode => SsalddelExecutionMode.Simulation; public bool IsSimulation => true; public bool IsOperational => false; }
    private sealed class Flags : IVersionFeatureFlagService
    {
        public bool Enabled { get; set; } = true; public bool IsEnabled(string featureKey) => Enabled;
        public IReadOnlyDictionary<string, bool> GetAll() => new Dictionary<string, bool>();
    }
    private sealed class Encryption : IPersonalDataEncryptionService
    { public string? Protect(string? value) => value; public string? Unprotect(string? value) => value; }
    private sealed class Orders(Fixture fixture, string? actor) : I공간공동운영주문UseCase
    {
        public Task<공간공동운영주문목록Response> 목록Async(string ledgerId, CancellationToken ct = default)
        {
            fixture.OrderCalls++; if (fixture.OrderFailure is { } failure) throw failure;
            return Task.FromResult(new 공간공동운영주문목록Response { LedgerId = ledgerId, Revision = fixture.RowsRevision ?? fixture.Detail.Revision,
                Items = fixture.Rows.Where(item => item.Buyer == actor || item.Seller == actor).Select(item => item.Row).ToArray() });
        }
        public Task<공간공동운영주문후보목록Response> 후보Async(string ledgerId, string cycleId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> 원주문연결검증Async(string ledgerId, long expectedRevision, Guid orderRequestId, 커뮤니티원장Dto root, CancellationToken ct = default)
        {
            var order = fixture.CanonicalOrders.GetValueOrDefault(orderRequestId);
            return Task.FromResult(ledgerId == fixture.Detail.LedgerId && expectedRevision == fixture.Detail.Revision
                && order is not null && (order.BuyerUserId == actor || order.SellerUserId == actor)
                && root.원장Id == order.RootOrderLedgerId && 공간공동운영주문UseCase.원주문과일치(ledgerId, order, root));
        }
        public Task<공간공동운영주문Response?> 조회Async(string ledgerId, Guid orderRequestId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<공간공동운영주문Response?> 요청결과Async(string ledgerId, Guid requestId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Result<공간공동운영주문Response>> 변경Async(string ledgerId, Guid orderRequestId, 공간공동운영주문명령Request request, CancellationToken ct = default) => throw new NotSupportedException();
    }
    private sealed class Core(Fixture fixture, string? actor) : I공간공동운영UseCase
    {
        public Task<공간공동운영상세Response?> 상세Async(string ledgerId, CancellationToken cancellationToken = default)
            => Task.FromResult(ledgerId == fixture.Detail.LedgerId && actor is "buyer" or "seller" or "operator" ? fixture.Detail : null);
        public Task<공간공동운영공개Response> 공개Async(string? regionKey = null, int page = 1, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<공간공동운영공개제안Dto?> 초대조회Async(string ledgerId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<공간공동운영내목록Response> 내목록Async(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<공간공동운영상세Response?> 요청결과Async(Guid clientRequestId, string? ledgerId = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<공간공동운영상세Response>> 생성Async(공간공동운영생성Request request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<공간공동운영상세Response>> 변경Async(string ledgerId, 공간공동운영명령Request request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<공간공동운영연결후보Dto>?> 연결후보Async(string ledgerId, string cycleId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    private sealed class LedgerStore : I커뮤니티원장저장소
    {
        public Dictionary<string, 커뮤니티원장Dto> Rows { get; } = new(StringComparer.Ordinal); public List<string> ReadIds { get; } = [];
        public Task<커뮤니티원장Dto?> 원장조회Async(string 원장Id, CancellationToken cancellationToken = default)
        { ReadIds.Add(원장Id); return Task.FromResult(Rows.GetValueOrDefault(원장Id)); }
        public Task<IReadOnlyList<커뮤니티원장Dto>> 원장목록조회Async(커뮤니티원장조회조건 query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<커뮤니티원장Dto> 원장저장Async(커뮤니티원장저장요청 request, string updatedBy, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<커뮤니티원장Dto?> 원장상태변경Async(커뮤니티원장상태변경요청 request, string updatedBy, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
