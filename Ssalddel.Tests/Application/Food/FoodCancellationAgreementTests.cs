using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Services.Community;
using Ssalddel.Services.Outbox;
using Ssalddel.Application.Food;
using Ssalddel.Contracts.Food;
using Ssalddel.Services.Food;
using 살뜰.Data;
using 살뜰.Services.Dispatch.Engine;
using 살뜰.Infrastructure.Security;
using 살뜰.도메인.공통;
using 살뜰.도메인.운송;
using 살뜰.도메인.음식;

namespace Ssalddel.Tests.Application.Food;

public sealed class FoodCancellationAgreementTests
{
    [Fact]
    public async Task 제안은자동동의하지않고_양측동의뒤명시실행만주문을취소하며_금융완료를만들지않는다()
    {
        await using var f = await Fixture.CreateAsync();
        var queue = new 운송원장 { 의뢰Id = "food-offer", 원본의뢰Id = "food-agreement", 원본의뢰유형 = 운송의뢰배차원천유형.음식점주문,
            운송번호 = "food-queue", 배차업무유형 = 상태값.배차업무유형.음식배달, 상태 = 상태값.배차대기상태.대기,
            배차큐단계 = 상태값.배차큐단계.배차추천, 배차노출상태 = 상태값.배차노출상태.추천중,
            현재추천대상기사Id = "candidate", 기사지급예정액 = 4720m, 기사제안요금정책판본 = "frozen-policy" };
        f.Db.운송원장.Add(queue); await f.Db.SaveChangesAsync();
        var original = await f.Db.음식주문.SingleAsync(); original.배차대기Id = queue.Id; original.배차상태 = 음식주문배차상태코드.추천중;
        await f.Db.SaveChangesAsync();
        var proposal = f.Proposal();
        var value = await f.Use.제안Async("food-agreement", proposal);
        Assert.False(value.OrdererAgreed); Assert.False(value.RestaurantAgreed); Assert.False(value.CanApply);
        Assert.Equal(음식주문상태코드.주문확인, (await f.Db.음식주문.SingleAsync()).상태);
        value = await f.Use.동의Async(value.OrderNo, value.AgreementId, f.Command(value));
        Assert.True(value.OrdererAgreed); Assert.False(value.RestaurantAgreed);
        f.Actor("restaurant", 41);
        value = await f.Use.동의Async(value.OrderNo, value.AgreementId, f.Command(value));
        Assert.True(value.CanApply); Assert.True(value.RestaurantAgreed);
        var apply = f.Command(value);
        var applied = await f.Use.실행Async(value.OrderNo, value.AgreementId, apply);
        Assert.Equal("Cancelled", applied.StateCode); Assert.Equal("PendingExternalSettlement", applied.FinanceStatusCode);
        Assert.Equal(300m, applied.OrdererCostKrw); Assert.Equal(700m, applied.RestaurantCostKrw);
        Assert.Equal("agreed-cost-reference", applied.CostBasisReference);
        var order = await f.Db.음식주문.Include(x => x.상태이력).SingleAsync();
        Assert.Equal(음식주문상태코드.취소, order.상태);
        Assert.Equal(2, order.상태이력.Count); Assert.DoesNotContain(order.상태이력, x => x.다음상태 == 음식주문상태코드.수령확인);
        Assert.Equal(상태값.배차상태.취소, queue.상태); Assert.Equal(상태값.배차큐단계.종료, queue.배차큐단계);
        Assert.Null(queue.확정기사Id); Assert.Null(queue.현재추천대상기사Id);
        Assert.Equal(4720m, queue.기사지급예정액); Assert.Equal("frozen-policy", queue.기사제안요금정책판본);
        Assert.Empty(await f.Db.음식주문기사정산.ToListAsync()); Assert.Empty(await f.Db.음식주문기사지급검증.ToListAsync());
        Assert.Single(await f.Db.음식마트원장동기화Outbox.ToListAsync());
        Assert.DoesNotContain("private-customer", JsonSerializer.Serialize(applied));
        Assert.DoesNotContain("private-address", JsonSerializer.Serialize(applied));
        Assert.DoesNotContain("010-9999", JsonSerializer.Serialize(applied));
        Assert.DoesNotContain("private-address", (await f.Db.음식마트원장동기화Outbox.SingleAsync()).PayloadJson);

        // POST 응답을 받지 못했다고 가정해 같은 요청을 GET으로 확인한다. 새 취소/환불을 만들지 않는다.
        await using var second = f.NewContext();
        var read = new 음식주문취소합의UseCase(second, f.Http, TimeProvider.System);
        var receipt = await read.조회Async(value.OrderNo, value.AgreementId, apply.ClientRequestId);
        Assert.Equal(apply.ClientRequestId, receipt!.ConfirmedClientRequestId); Assert.Equal("Cancelled", receipt.StateCode);
        var replay = await read.실행Async(value.OrderNo, value.AgreementId, apply);
        Assert.True(replay.IsIdempotentReplay);
        Assert.Equal(2, await second.음식주문상태이력.CountAsync()); Assert.Equal(1, await second.음식마트원장동기화Outbox.CountAsync());
    }

    [Theory]
    [InlineData(CommunityLedgerTemplateKeys.FoodDelivery)]
    [InlineData(CommunityLedgerTemplateKeys.FoodOrder)]
    public async Task 합의취소의배송및음식투영은_같은Outbox로부분실패뒤재개하고_취소나정산을중복실행하지않는다(string failedTemplate)
    {
        await using var f = await Fixture.CreateAsync();
        var queue = new 운송원장
        {
            의뢰Id = "cancel-project", 운송번호 = "cancel-project", 원본의뢰Id = "food-agreement", 원본의뢰유형 = 운송의뢰배차원천유형.음식점주문,
            배차업무유형 = 상태값.배차업무유형.음식배달, 상태 = 상태값.배차대기상태.대기,
            배차큐단계 = 상태값.배차큐단계.배차추천, 배차노출상태 = 상태값.배차노출상태.추천중,
            현재추천대상기사Id = "candidate", 기사지급예정액 = 4720
        };
        f.Db.운송원장.Add(queue); await f.Db.SaveChangesAsync();
        var source = await f.Db.음식주문.SingleAsync(); source.배차대기Id = queue.Id;
        source.배차상태 = 음식주문배차상태코드.추천중; await f.Db.SaveChangesAsync();
        var ledgers = new ProjectionStore();
        var transport = new 운송원장Mongo동기화Service(f.Db, ledgers, NullLogger<운송원장Mongo동기화Service>.Instance);
        var food = new 음식마트원장Mongo동기화Service(ledgers, f.Db, NullLogger<음식마트원장Mongo동기화Service>.Instance);
        var child = await transport.운송실행투영동기화Async(queue, "seed"); Assert.NotNull(child);
        var root = await food.음식주문동기화Async(new EfSsalddelFoodOrderStore(f.Db).GetOrder("food-agreement")!, "seed");
        Assert.NotNull(root); Assert.NotEqual(커뮤니티원장상태.닫힘, child!.상태);
        var value = await f.Use.제안Async("food-agreement", f.Proposal());
        value = await f.Use.동의Async(value.OrderNo, value.AgreementId, f.Command(value));
        f.Actor("restaurant", 41); value = await f.Use.동의Async(value.OrderNo, value.AgreementId, f.Command(value));
        var apply = f.Command(value); await f.Use.실행Async(value.OrderNo, value.AgreementId, apply);
        var item = await f.Db.음식마트원장동기화Outbox.SingleAsync();
        using (var payload = JsonDocument.Parse(item.PayloadJson))
        {
            var intent = payload.RootElement.GetProperty("agreedCancellation");
            Assert.Equal(new[] { "orderNo", "agreementId", "queueId" }, intent.EnumerateObject().Select(x => x.Name));
        }
        ledgers.FailOnTemplate = failedTemplate;
        var worker = new 음식마트원장동기화OutboxService(f.Db, food, NullLogger<음식마트원장동기화OutboxService>.Instance,
            foodOrderStore: new EfSsalddelFoodOrderStore(f.Db), transportLedgerSync: transport);

        Assert.Equal(1, await worker.대기항목처리Async());
        Assert.Equal(OutboxProcessingStatuses.Pending, item.처리상태);
        Assert.Equal(음식주문상태코드.취소, source.상태);
        Assert.Equal(2, await f.Db.음식주문상태이력.CountAsync());
        if (failedTemplate == CommunityLedgerTemplateKeys.FoodOrder)
        {
            Assert.Equal(커뮤니티원장상태.닫힘, ledgers.Ledgers[child.원장Id].상태);
            Assert.NotEqual(커뮤니티원장상태.닫힘, ledgers.Ledgers[root!.원장Id].상태);
        }
        item.UpdatedAtUtc = DateTime.UtcNow - OutboxProcessingPolicy.RetryDelay - TimeSpan.FromSeconds(1);
        await f.Db.SaveChangesAsync();
        await using var restarted = f.NewContext();
        var resumed = new 음식마트원장동기화OutboxService(restarted,
            new 음식마트원장Mongo동기화Service(ledgers, restarted, NullLogger<음식마트원장Mongo동기화Service>.Instance),
            NullLogger<음식마트원장동기화OutboxService>.Instance,
            foodOrderStore: new EfSsalddelFoodOrderStore(restarted),
            transportLedgerSync: new 운송원장Mongo동기화Service(restarted, ledgers, NullLogger<운송원장Mongo동기화Service>.Instance));
        Assert.Equal(1, await resumed.대기항목처리Async());
        Assert.Equal(0, await resumed.대기항목처리Async());
        Assert.Equal(OutboxProcessingStatuses.Succeeded, (await restarted.음식마트원장동기화Outbox.SingleAsync()).처리상태);
        Assert.Equal(커뮤니티원장상태.닫힘, ledgers.Ledgers[child.원장Id].상태);
        Assert.Equal(상태값.배차상태.취소, ledgers.Ledgers[child.원장Id].현재단계Key);
        Assert.Equal(커뮤니티원장상태.닫힘, ledgers.Ledgers[root!.원장Id].상태);
        Assert.Equal(음식주문상태코드.취소, ledgers.Ledgers[root.원장Id].현재단계Key);
        Assert.Equal(child.원장Id, Assert.Single(ledgers.Ledgers[root.원장Id].포함원장목록).원장Id);
        Assert.True((await f.Use.실행Async(value.OrderNo, value.AgreementId, apply)).IsIdempotentReplay);
        Assert.Equal(1, await restarted.음식마트원장동기화Outbox.CountAsync());
        Assert.Equal(2, await restarted.음식주문상태이력.CountAsync());
        Assert.Empty(await restarted.음식주문기사정산.ToArrayAsync());
        Assert.Empty(await restarted.음식주문기사지급검증.ToArrayAsync());
    }

    [Fact]
    public async Task 한쪽동의만으로실행할수없고_동의철회후에도실행되지않는다()
    {
        await using var f = await Fixture.CreateAsync();
        var value = await f.Use.제안Async("food-agreement", f.Proposal());
        value = await f.Use.동의Async(value.OrderNo, value.AgreementId, f.Command(value));
        await Error("BothPartiesConsentRequired", () => f.Use.실행Async(value.OrderNo, value.AgreementId, f.Command(value)));
        value = await f.Use.동의Async(value.OrderNo, value.AgreementId, f.Command(value, granted: false));
        Assert.False(value.OrdererAgreed); Assert.False(value.CanApply);
        Assert.Equal(음식주문상태코드.주문확인, (await f.Db.음식주문.SingleAsync()).상태);
        Assert.Empty(await f.Db.음식마트원장동기화Outbox.ToListAsync());
    }

    [Theory]
    [InlineData("other-customer", null, true)]
    [InlineData("wrong-restaurant", 99, true)]
    [InlineData("claim-without-role", 41, false)]
    public async Task 다른주문자_다른음식점_역할없는음식점Claim은_합의와고객정보를조회하지못한다(string actor, int? restaurant, bool role)
    {
        await using var f = await Fixture.CreateAsync();
        var value = await f.Use.제안Async("food-agreement", f.Proposal());
        f.Actor(actor, restaurant, role);
        await Error("OrderNotFound", () => f.Use.조회Async(value.OrderNo, value.AgreementId));
        await Error("OrderNotFound", () => f.Use.목록Async(value.OrderNo));
        await Error("OrderNotFound", () => f.Use.동의Async(value.OrderNo, value.AgreementId, f.Command(value)));
        Assert.False((await f.Db.Set<음식주문취소합의>().SingleAsync()).음식점동의);
    }

    [Theory]
    [InlineData("cooking")]
    [InlineData("assigned")]
    [InlineData("picked")]
    public async Task 주문_조리_기사상태가바뀌면_기존조건을다시동의하거나취소실행할수없다(string phase)
    {
        await using var f = await Fixture.CreateAsync();
        var value = await f.Use.제안Async("food-agreement", f.Proposal());
        var order = await f.Db.음식주문.SingleAsync();
        if (phase == "cooking")
        {
            order.상태 = 음식주문상태코드.조리중;
            order.상태이력.Add(new() { 이전상태 = 음식주문상태코드.주문확인, 다음상태 = order.상태, 사유 = "음식점 조리 시작", 전이시각Utc = DateTime.UtcNow });
        }
        else
        {
            order.상태 = phase == "assigned" ? 음식주문상태코드.기사배정 : 음식주문상태코드.픽업완료;
            f.Db.음식배달시도.Add(new() { 시도StableId = "attempt", 주문번호 = order.주문번호, 제안Id = "offer", 기사Id = "driver",
                시도순번 = 1, 수락시각Utc = DateTime.UtcNow, 픽업완료시각Utc = phase == "picked" ? DateTime.UtcNow : null });
        }
        await f.Db.SaveChangesAsync();
        var current = await f.Use.조회Async(value.OrderNo, value.AgreementId);
        Assert.Equal("ReconfirmationRequired", current!.StateCode); Assert.False(current.CanConsent); Assert.False(current.CanApply);
        Assert.NotEmpty(current.BlockReasonCode);
        Assert.NotNull(await Record.ExceptionAsync(() => f.Use.동의Async(value.OrderNo, value.AgreementId, f.Command(value))));
        Assert.NotNull(await Record.ExceptionAsync(() => f.Use.실행Async(value.OrderNo, value.AgreementId, f.Command(value))));
        Assert.Null((await f.Db.Set<음식주문취소합의>().SingleAsync()).취소반영시각Utc);
    }

    [Fact]
    public async Task 공통전이가합의취소를허용해도_주문자직접취소와일반음식점진행은수락후취소를우회하지못한다()
    {
        await using var f = await Fixture.CreateAsync();
        var store = new EfSsalddelFoodOrderStore(f.Db);
        Assert.Throws<InvalidOperationException>(() => store.주문자취소("food-agreement", new() { 클라이언트요청Id = Guid.NewGuid() }, "buyer"));
        Assert.Throws<ArgumentException>(() => store.음식점진행변경("food-agreement", new() { 클라이언트요청Id = Guid.NewGuid(), 작업 = "취소" }, "restaurant"));
        Assert.Equal(음식주문상태코드.주문확인, (await f.Db.음식주문.SingleAsync()).상태);
        Assert.Empty(await f.Db.Set<음식주문취소합의>().ToListAsync());
    }

    [Fact]
    public async Task 미확정비용_다른입력의요청재사용_낮은합의판본은저장하지않고_영수증은본인요청만노출한다()
    {
        await using var f = await Fixture.CreateAsync();
        var proposal = f.Proposal(); proposal.OrdererCostKrw = null;
        await Error("ExplicitCostTermsRequired", () => f.Use.제안Async("food-agreement", proposal));
        proposal.OrdererCostKrw = 0;
        var value = await f.Use.제안Async("food-agreement", proposal);
        proposal.RestaurantCostKrw++;
        await Error("IdempotencyConflict", () => f.Use.제안Async("food-agreement", proposal));
        var consent = f.Command(value); value = await f.Use.동의Async(value.OrderNo, value.AgreementId, consent);
        await Error("CancellationAgreementStale", () => f.Use.동의Async(value.OrderNo, value.AgreementId, new()
        { ClientRequestId = Guid.NewGuid(), ExpectedAgreementRevision = 1, ExpectedOrderRevision = 1, Granted = true }));
        f.Actor("restaurant", 41);
        Assert.Null(await f.Use.조회Async(value.OrderNo, value.AgreementId, consent.ClientRequestId));
        Assert.Null(await f.Use.조회Async(value.OrderNo, value.AgreementId, proposal.ClientRequestId));
        Assert.Equal(1, await f.Db.Set<음식주문취소합의>().CountAsync());
    }

    private static async Task Error(string code, Func<Task> action)
        => Assert.Equal(code, (await Assert.ThrowsAsync<음식주문취소합의Exception>(action)).Code);

    // HttpContextAccessor의 AsyncLocal을 async fixture 생성 바깥으로 전달하지 않는다.
    private sealed class FixtureHttpAccessor : IHttpContextAccessor
    { public HttpContext? HttpContext { get; set; } }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<SsalddelContext> _options;
        public SsalddelContext Db { get; }
        public IHttpContextAccessor Http { get; } = new FixtureHttpAccessor { HttpContext = new DefaultHttpContext() };
        public 음식주문취소합의UseCase Use => new(Db, Http, TimeProvider.System);
        private Fixture(SqliteConnection connection)
        {
            _connection = connection; _options = new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(connection).Options;
            Db = NewContext(); Actor("buyer");
        }
        public SsalddelContext NewContext() => new(_options, new Encryption());
        public void Actor(string id, int? restaurant = null, bool role = true)
        {
            var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, id) };
            if (restaurant is { } value) { claims.Add(new(음식점접근ClaimTypes.음식점Id, value.ToString())); if (role) claims.Add(new(ClaimTypes.Role, 역할명.음식점)); }
            Http.HttpContext!.User = new(new ClaimsIdentity(claims, "test"));
        }
        public 음식주문취소합의제안요청 Proposal() => new() { ClientRequestId = Guid.NewGuid(), ExpectedOrderRevision = 1,
            OrdererCostKrw = 300, RestaurantCostKrw = 700, CostBasisReference = "agreed-cost-reference" };
        public 음식주문취소합의변경요청 Command(음식주문취소합의응답 value, bool granted = true) => new()
        { ClientRequestId = Guid.NewGuid(), ExpectedAgreementRevision = value.Revision, ExpectedOrderRevision = value.FrozenOrderRevision, Granted = granted };
        public static async Task<Fixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
            var f = new Fixture(connection); await f.Db.Database.EnsureCreatedAsync();
            f.Db.음식주문.Add(new() { 주문번호 = "food-agreement", 주문자UserId = "buyer", 음식점Id = 41,
                수령인명 = "private-customer", 수령인연락처 = "010-9999", 수령지주소 = "private-address",
                상태 = 음식주문상태코드.주문확인, 음식점수락시각Utc = DateTime.UtcNow,
                상태이력 = [new() { 이전상태 = 음식주문상태코드.주문대기, 다음상태 = 음식주문상태코드.주문확인, 사유 = "음식점 주문 수락", 전이시각Utc = DateTime.UtcNow }] });
            await f.Db.SaveChangesAsync(); return f;
        }
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await _connection.DisposeAsync(); }
    }
    // 실제 운송/음식 투영 Service와 CAS 요청을 실행하되 Mongo 접속 대신 격리 메모리 문서를 사용한다.
    private sealed class ProjectionStore : I커뮤니티원장저장소
    {
        public Dictionary<string, 커뮤니티원장Dto> Ledgers { get; } = new(StringComparer.Ordinal);
        public string? FailOnTemplate { get; set; }
        public Task<커뮤니티원장Dto> 원장저장Async(커뮤니티원장저장요청 request, string updatedBy, CancellationToken cancellationToken = default)
        {
            if (request.원장템플릿Key == FailOnTemplate)
            { FailOnTemplate = null; throw new InvalidOperationException("one-time projection unavailable"); }
            var previous = Ledgers.GetValueOrDefault(request.원장Id!);
            Assert.Equal(previous?.Revision ?? 0, request.기대Revision);
            var value = new 커뮤니티원장Dto
            {
                원장Id = request.원장Id!, Revision = (previous?.Revision ?? 0) + 1,
                커뮤니티Id = request.커뮤니티Id, 원장템플릿Key = request.원장템플릿Key,
                제목 = request.제목, 상태 = request.상태!, 현재단계Key = request.현재단계Key,
                블록목록 = request.블록목록, 외부참조 = request.외부참조, 확장속성 = request.확장속성,
                포함원장목록 = request.포함원장목록 ?? previous?.포함원장목록 ?? []
            };
            Ledgers[value.원장Id] = value; return Task.FromResult(value);
        }
        public Task<커뮤니티원장Dto?> 원장조회Async(string ledgerId, CancellationToken cancellationToken = default)
            => Task.FromResult(Ledgers.GetValueOrDefault(ledgerId));
        public Task<IReadOnlyList<커뮤니티원장Dto>> 원장목록조회Async(커뮤니티원장조회조건 query, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<커뮤니티원장Dto>>([]);
        public Task<커뮤니티원장Dto?> 원장상태변경Async(커뮤니티원장상태변경요청 request, string updatedBy, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
    private sealed class Encryption : IPersonalDataEncryptionService
    { public string? Protect(string? value) => value; public string? Unprotect(string? value) => value; }
}
