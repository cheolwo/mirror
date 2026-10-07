using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ssalddel.Contracts.Common.PrivacyRetention;
using Ssalddel.Services.PrivacyRetention;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.도메인.음식;
using 살뜰.도메인.운송;
using 살뜰.도메인.화주;
using 살뜰.도메인.공통;
using 살뜰.도메인.설정;
using Ssalddel.Services.Outbox;
using Ssalddel.Services.Community;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Food;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Ssalddel.Services.Food;
using 살뜰.도메인.창고;

namespace Ssalddel.Tests.Services.PrivacyRetention;

public sealed class PrivacyRetentionTests
{
    private static readonly DateTime Now = new(2026, 10, 6, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task 보존준비미확인시_스캔과실제파기를수행하지않는다()
    {
        var store = new Store(); var adapter = new Adapter();
        var service = Service(store, adapter, new 개인정보보존Options { Enabled = true });
        Assert.Equal(0, await service.실행Async());
        Assert.Equal(0, adapter.Discoveries); Assert.Empty(store.Jobs);
    }

    [Fact]
    public async Task 정확한만료시점에_한번파기하고_실제확인후만완료한다()
    {
        var store = new Store(); var adapter = new Adapter(); var service = Service(store, adapter);
        Assert.Equal(1, await service.실행Async());
        Assert.Equal(0, await service.실행Async());
        Assert.Equal(1, adapter.Purges);
        var result = await service.결과Async(adapter.SourceCode, "order", "owner");
        Assert.Equal(개인정보파기상태Codes.Completed, result!.StatusCode);
        Assert.NotNull(result.CompletedAtUtc);
        Assert.Null(await service.결과Async(adapter.SourceCode, "order", "other"));
        Assert.False(await service.복원허용Async(adapter.SourceCode, "order"));
    }

    [Fact]
    public async Task 분쟁보존은검토기한이지나도자동해제하지않고_명시해제후만파기한다()
    {
        var store = new Store(); var adapter = new Adapter(); var clock = new Clock(Now);
        var service = Service(store, adapter, clock: clock);
        Assert.False(await service.보존정지해제확인Async(adapter.SourceCode, "order", "case-1"));
        await service.보존정지Async(adapter.SourceCode, "order", "case-1", "Dispute", Now.AddDays(1));
        Assert.False(await service.보존정지해제확인Async(adapter.SourceCode, "order", "case-1"));
        Assert.Equal(0, await service.실행Async());
        clock.Utc = Now.AddDays(2);
        Assert.Equal(0, await service.실행Async());
        Assert.Equal("LegalHoldReviewOverdue", (await store.GetAsync(개인정보파기Job.Key(adapter.SourceCode, "order")))!.FailureCode);
        Assert.Equal(0, adapter.Purges);
        Assert.Equal(Now.AddDays(1), (await service.보존정지조회Async(adapter.SourceCode, "order", "case-1"))!.ReviewAtUtc);
        await service.보존정지해제Async(adapter.SourceCode, "order", "case-1");
        Assert.True(await service.보존정지해제확인Async(adapter.SourceCode, "order", "case-1"));
        Assert.Equal(1, await service.실행Async());
    }

    [Fact]
    public async Task 실제파기미확인과예외는완료를반환하지않고_민감한오류본문을저장하지않는다()
    {
        var store = new Store(); var adapter = new Adapter { Throw = true }; var service = Service(store, adapter);
        Assert.Equal(0, await service.실행Async());
        var job = Assert.Single(store.Jobs.Values);
        Assert.Equal(개인정보파기상태Codes.Retry, job.StatusCode);
        Assert.DoesNotContain("010", job.FailureCode);
        Assert.Null(job.CompletedAtUtc);
        adapter.Throw = false; adapter.Verified = false;
        job.NextAttemptAtUtc = Now;
        Assert.Equal(0, await service.실행Async());
        Assert.Equal(개인정보파기상태Codes.Blocked, Assert.Single(store.Jobs.Values).StatusCode);
    }

    [Fact]
    public async Task 보존정지의무기한과이미시작한파기에대한뒤늦은보존을거부한다()
    {
        var store = new Store(); var adapter = new Adapter(); var service = Service(store, adapter);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.보존정지Async(adapter.SourceCode, "order", "case", "Dispute", Now.AddDays(91)));
        await service.실행Async();
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.보존정지Async(adapter.SourceCode, "order", "case", "Dispute", Now.AddDays(1)));
    }

    [Fact]
    public async Task 사건별보존순번은해제후에도남아_늦은보존과동일순번의다른내용을차단한다()
    {
        var store = new Store(); var adapter = new Adapter(); var clock = new Clock(Now);
        var service = Service(store, adapter, clock: clock); var review = Now.AddDays(1).AddTicks(4321);
        await service.보존상태적용Async(adapter.SourceCode, "order", "case", 1, true, "Dispute", review);
        var version = Assert.Single(store.Jobs.Values).Revision;
        var persistedReview = (await service.보존적용조회Async(adapter.SourceCode, "order", "case"))!.ReviewAtUtc;
        Assert.Equal(0, persistedReview!.Value.Ticks % TimeSpan.TicksPerMillisecond);
        clock.Utc = Now.AddDays(2);
        await service.보존상태적용Async(adapter.SourceCode, "order", "case", 1, true, "Dispute", persistedReview);
        Assert.Equal(version, Assert.Single(store.Jobs.Values).Revision); // Persisted intent retry succeeds after its review date.
        await service.보존상태적용Async(adapter.SourceCode, "order", "case", 2, false, "Dispute", null);
        var receipt = await service.보존적용조회Async(adapter.SourceCode, "order", "case");
        Assert.Equal(2, receipt!.Sequence); Assert.False(receipt.Hold); Assert.Null(receipt.ReviewAtUtc);
        Assert.Empty(Assert.Single(store.Jobs.Values).Holds);
        var stale = await Assert.ThrowsAsync<InvalidOperationException>(() => service.보존상태적용Async(adapter.SourceCode, "order", "case", 1, true, "Dispute", review));
        Assert.Equal("RetentionIntentStale", stale.Message);
        var conflict = await Assert.ThrowsAsync<InvalidOperationException>(() => service.보존상태적용Async(adapter.SourceCode, "order", "case", 2, true, "Dispute", clock.Utc.AddDays(1)));
        Assert.Equal("RetentionIntentConflict", conflict.Message);
        var unsequenced = await Assert.ThrowsAsync<InvalidOperationException>(() => service.보존정지Async(adapter.SourceCode, "order", "case", "Dispute", clock.Utc.AddDays(1)));
        Assert.Equal("RetentionIntentSequenceRequired", unsequenced.Message);
        await service.보존상태적용Async(adapter.SourceCode, "order", "other-case", 1, true, "Dispute", clock.Utc.AddDays(1));
        Assert.Equal("other-case", Assert.Single(Assert.Single(store.Jobs.Values).Holds).CaseId);
    }

    [Fact]
    public async Task 보존CAS직전새로운해제가먼저적용되면_오래된작업자는보존을재적용할수없다()
    {
        var store = new Store(); var adapter = new Adapter(); var service = Service(store, adapter);
        store.BeforeReplace = (_, _) => service.보존상태적용Async(adapter.SourceCode, "order", "case", 20, false, "Dispute", null);
        var stale = await Assert.ThrowsAsync<InvalidOperationException>(() => service.보존상태적용Async(adapter.SourceCode, "order", "case", 10, true, "Dispute", Now.AddDays(1)));
        Assert.Equal("RetentionIntentStale", stale.Message);
        var restarted = Service(store, adapter);
        Assert.Equal(20, (await restarted.보존적용조회Async(adapter.SourceCode, "order", "case"))!.Sequence);
        Assert.Empty(Assert.Single(store.Jobs.Values).Holds);
        Assert.True(await restarted.보존정지해제확인Async(adapter.SourceCode, "order", "case"));
    }

    [Fact]
    public async Task 음식완료사본만지우고_메뉴금액정산과다른원본은보존한다()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var order = new 음식주문 { 주문번호 = "food-1", 주문자UserId = "owner", 상태 = "수령확인", 총주문금액 = 21000,
            수령인명 = "고객개인정보", 수령인연락처 = "010-1234-5678", 수령지주소 = "주거주소", 수령요청사항 = "비밀번호1234",
            UpdatedAt = Now.AddDays(-3), 상품목록 = [new 음식주문상품 { 상품명 = "메뉴", 수량 = 1, 단가 = 21000 }] };
        var transport = new 운송원장 { 의뢰Id = "food-offer", 원본의뢰Id = "food-1", 원본의뢰유형 = "FoodOrder", 배차업무유형 = 상태값.배차업무유형.음식배달,
            배차큐단계 = 상태값.배차큐단계.종료, 하차_도로명주소 = "주거주소", 하차_위도 = 37, 픽업_도로명주소 = "공개매장주소", 기사지급예정액 = 4000 };
        var unrelated = new 음식주문 { 주문번호 = "other", 상태 = "주문대기", 수령인연락처 = "untouched" };
        fixture.Db.AddRange(order, transport, unrelated); await fixture.Db.SaveChangesAsync();
        var store = new Store(); var ledgers = new Ledger();
        var adapter = new 음식배송개인정보파기Adapter(fixture.Db, store, ledgers);
        var result = await adapter.PurgeAsync(new 개인정보파기Job { Id = "job", RecordId = "food-1", DueAtUtc = Now }, Now, default);
        Assert.True(result.Verified);
        fixture.Db.ChangeTracker.Clear();
        var saved = await fixture.Db.음식주문.Include(x => x.상품목록).SingleAsync(x => x.주문번호 == "food-1");
        Assert.Empty(saved.수령인연락처); Assert.Empty(saved.수령지주소); Assert.Empty(saved.수령요청사항);
        Assert.Equal(21000, saved.총주문금액); Assert.Equal("메뉴", Assert.Single(saved.상품목록).상품명);
        var savedTransport = await fixture.Db.운송원장.SingleAsync();
        Assert.Equal(4000, savedTransport.기사지급예정액); Assert.Equal("공개매장주소", savedTransport.픽업_도로명주소);
        Assert.Null(savedTransport.하차_위도);
        Assert.Equal("untouched", (await fixture.Db.음식주문.SingleAsync(x => x.주문번호 == "other")).수령인연락처);
        Assert.DoesNotContain("010", Assert.Single(store.Evidence)); Assert.DoesNotContain("주거주소", Assert.Single(store.Evidence));
        Assert.Contains("Mongo.community_ledgers", result.VerifiedStorageCodes);
    }

    [Fact]
    public async Task 생활배송완료사본은개인픽업도지우고_일반화물은건드리지않는다()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var request = new 화주운송의뢰 { 의뢰Id = "neighborhood", 주문자UserId = "owner", 클라이언트요청Id = "neighborhood-delivery:abc",
            픽업_도로명주소 = "집주소", 픽업_연락처_전화번호 = "010", 하차_도로명주소 = "받는집", 하차_경도 = 127, 최종운임 = 3500 };
        // The canonical client prefix is supplied by the shared contract.
        request.클라이언트요청Id = Ssalddel.Contracts.Common.Community.NeighborhoodDeliveryRoutes.ClientRequestPrefix + "abc";
        var t = new 운송원장 { 의뢰Id = request.의뢰Id, 원본의뢰Id = request.의뢰Id, 배차큐단계 = 상태값.배차큐단계.종료, UpdatedAt = Now.AddDays(-3), 픽업_도로명주소 = "집주소", 하차_도로명주소 = "받는집" };
        var ordinary = new 화주운송의뢰 { 의뢰Id = "cargo", 클라이언트요청Id = "cargo", 픽업_도로명주소 = "운영중창고" };
        fixture.Db.AddRange(request, t, ordinary); await fixture.Db.SaveChangesAsync();
        var adapter = new 생활배송개인정보파기Adapter(fixture.Db, new Store(), new Ledger());
        Assert.Null(await adapter.FindAsync("cargo", default));
        Assert.True((await adapter.PurgeAsync(new 개인정보파기Job { Id = "n", RecordId = "neighborhood" }, Now, default)).Verified);
        fixture.Db.ChangeTracker.Clear();
        var saved = await fixture.Db.화주운송의뢰.SingleAsync(x => x.의뢰Id == "neighborhood");
        Assert.Empty(saved.픽업_도로명주소); Assert.Empty(saved.픽업_연락처_전화번호); Assert.Null(saved.하차_경도); Assert.Equal(3500, saved.최종운임);
        Assert.Equal("운영중창고", (await fixture.Db.화주운송의뢰.SingleAsync(x => x.의뢰Id == "cargo")).픽업_도로명주소);
    }

    [Fact]
    public async Task 소유관계미확인첨부는삭제성공으로단정하지않는다()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var order = new 음식주문 { 주문번호 = "f", 상태 = "전달완료", 수령인연락처 = "preserve" };
        fixture.Db.AddRange(order, new 운송원장 { 의뢰Id = "t", 원본의뢰Id = "f", 원본의뢰유형 = "FoodOrder", 배차업무유형 = 상태값.배차업무유형.음식배달,
            배차큐단계 = 상태값.배차큐단계.종료, 첨부_json = "[\"unknown-owner.png\"]" });
        await fixture.Db.SaveChangesAsync();
        var result = await new 음식배송개인정보파기Adapter(fixture.Db, new Store(), new Ledger()).PurgeAsync(new 개인정보파기Job { RecordId = "f" }, Now, default);
        Assert.False(result.Verified); Assert.Equal("AttachmentsNeedOwnedObjectInventory", result.FailureCode); Assert.Equal("preserve", order.수령인연락처);
    }

    [Fact]
    public async Task 처리중인사본을잡은Outbox가있으면파기를막고_완료된정확한원천사본만비운다()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var order = new 음식주문 { 주문번호 = "f", 상태 = "전달완료", UpdatedAt = Now.AddDays(-3), 수령인연락처 = "preserve" };
        var outbox = new 음식마트원장동기화Outbox { 멱등키 = "f-sync", 원천Id = "f", 동기화유형 = 음식마트원장동기화유형코드.음식주문,
            처리상태 = OutboxProcessingStatuses.Processing, PayloadJson = "{\"phone\":\"010\"}" };
        var other = new 음식마트원장동기화Outbox { 멱등키 = "warehouse-sync", 원천Id = "f", 동기화유형 = 음식마트원장동기화유형코드.창고출고,
            PayloadJson = "{\"warehouse\":\"retained\"}" };
        fixture.Db.AddRange(order, outbox, other); await fixture.Db.SaveChangesAsync();
        var store = new Store(); var adapter = new 음식배송개인정보파기Adapter(fixture.Db, store, new Ledger());
        var job = new 개인정보파기Job { Id = "job-f", RecordId = "f" };
        var blocked = await adapter.PurgeAsync(job, Now, default);
        Assert.Equal("SourceOutboxStillProcessing", blocked.FailureCode); Assert.False(blocked.Verified);
        Assert.Equal("preserve", order.수령인연락처); Assert.Empty(store.Evidence);
        outbox.처리상태 = OutboxProcessingStatuses.Succeeded; await fixture.Db.SaveChangesAsync();
        Assert.True((await adapter.PurgeAsync(job, Now, default)).Verified);
        fixture.Db.ChangeTracker.Clear();
        var saved = await fixture.Db.음식마트원장동기화Outbox.SingleAsync(x => x.멱등키 == "f-sync");
        Assert.Equal("{}", saved.PayloadJson); Assert.Equal(음식개인정보OutboxPolicy.PrivacyExpired, saved.처리상태);
        Assert.Equal(other.PayloadJson, (await fixture.Db.음식마트원장동기화Outbox.SingleAsync(x => x.멱등키 == "warehouse-sync")).PayloadJson);
    }

    [Fact]
    public async Task 지연된원장상태이벤트도자유본문과개인정보를다시저장하지않는다()
    {
        await using var fixture = await DbFixture.CreateAsync();
        var service = new 커뮤니티원장상태이벤트Service(fixture.Db);
        var ledger = new 커뮤니티원장Dto { 원장Id = "food-order:f", 원장템플릿Key = "food-order", 제목 = "010 개인주소",
            원함 = "비밀번호", 상태 = "완료", 확장속성 = new Dictionary<string, string> { ["privateAddress"] = "private" } };
        await service.상태변경이벤트기록Async(new 커뮤니티원장상태변경요청 { 상태 = "완료", 메모 = "010 비밀번호" }, ledger, "actor", "late-event", Now);
        var state = await fixture.Db.커뮤니티원장상태이벤트.SingleAsync();
        Assert.Null(state.변경사유); Assert.DoesNotContain("010", state.SnapshotJson); Assert.DoesNotContain("비밀번호", state.SnapshotJson);
        Assert.Contains("food-order:f", state.SnapshotJson);
    }

    [Fact]
    public void 새음식Outbox는개인사본을보관하지않고원본및금액은보존한다()
    {
        var order = new 음식주문응답 { 주문번호 = "f", 총주문금액 = 21000, 수령인정보 = new() { 수령인명 = "private", 연락처 = "010", 주소 = "집" }, 수락메모 = "memo" };
        var copy = 음식개인정보OutboxPolicy.Minimized(order);
        Assert.Equal(21000, copy.총주문금액); Assert.Empty(copy.수령인정보.주소); Assert.Null(copy.수락메모);
        Assert.Equal("집", order.수령인정보.주소); Assert.Equal("memo", order.수락메모);
        var minimized = 배송개인정보이력파기.MinimizeEventJson("{\"DriverLatitude\":37,\"TargetLongitude\":127,\"Note\":\"010\",\"DistanceKm\":2.4,\"Action\":\"Delivered\"}");
        Assert.DoesNotContain("Latitude", minimized); Assert.DoesNotContain("Longitude", minimized); Assert.DoesNotContain("010", minimized);
        Assert.Contains("DistanceKm", minimized); Assert.Contains("Delivered", minimized);
    }

    [Fact]
    public async Task 활성주문원장투영은최소Outbox에서현재원본을다시읽어필요한전달정보를유지한다()
    {
        await using var fixture = await DbFixture.CreateAsync();
        fixture.Db.음식주문.Add(new 음식주문 { 주문번호 = "active", 상태 = "주문대기", 수령지주소 = "필요한현재주소", 수령인연락처 = "010", UpdatedAt = Now });
        await fixture.Db.SaveChangesAsync();
        var sync = new Sync(); var store = new EfSsalddelFoodOrderStore(fixture.Db);
        var service = new 음식마트원장동기화OutboxService(fixture.Db, sync, NullLogger<음식마트원장동기화OutboxService>.Instance,
            new 개인정보복원차단Service(new Store()), store);
        await service.음식주문예약후즉시처리Async(store.GetOrder("active")!, "actor", "active-event");
        Assert.Equal("필요한현재주소", sync.LastOrder!.수령인정보.주소);
        Assert.DoesNotContain("필요한현재주소", (await fixture.Db.음식마트원장동기화Outbox.SingleAsync()).PayloadJson);
    }

    [Fact]
    public async Task 복원차단Singleton은Scoped파기조정기없이Root에서안전하게생성된다()
    {
        var services = new ServiceCollection();
        services.Add개인정보보존Services(new ConfigurationBuilder().Build());
        services.AddSingleton<I개인정보보존Store>(new Store());
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var barrier = provider.GetRequiredService<I개인정보복원차단Service>();
        Assert.IsType<개인정보복원차단Service>(barrier);
        using var scope = provider.CreateScope();
        Assert.Same(barrier, scope.ServiceProvider.GetRequiredService<I개인정보복원차단Service>());
        Assert.True(await barrier.복원허용Async(개인정보파기원천Codes.FoodOrder, "f"));
        Assert.Equal(ServiceLifetime.Scoped, Assert.Single(services.Where(x => x.ServiceType == typeof(개인정보보존조정Service))).Lifetime);
    }

    [Theory]
    [InlineData("2026-02-28T14:59:59Z", "2026-02", "2025-12-31T15:00:00Z", "2026-01-31T15:00:00Z", "2026-02-28T15:00:00Z")]
    [InlineData("2026-02-28T15:00:00Z", "2026-03", "2026-01-31T15:00:00Z", "2026-02-28T15:00:00Z", "2026-03-31T15:00:00Z")]
    [InlineData("2028-02-29T15:00:00Z", "2028-03", "2028-01-31T15:00:00Z", "2028-02-29T15:00:00Z", "2028-03-31T15:00:00Z")]
    public void 접속기록점검은한국달력의매월경계로계산한다(string utc, string key, string start, string end, string deadline)
    {
        static DateTime Parse(string value) => DateTime.Parse(value, null, System.Globalization.DateTimeStyles.AdjustToUniversal);
        Assert.Equal(key, 개인정보접속점검Service.점검월(Parse(utc)));
        var period = 개인정보접속점검Service.요구점검기간(Parse(utc));
        Assert.Equal(Parse(start), period.StartUtc); Assert.Equal(Parse(end), period.EndUtc); Assert.Equal(Parse(deadline), period.DeadlineUtc);
    }

    private static 개인정보보존조정Service Service(Store store, Adapter adapter, 개인정보보존Options? options = null, Clock? clock = null)
        => new(store, [adapter], Options.Create(options ?? new 개인정보보존Options { Enabled = true, InventoryVerified = true, ProvidersVerified = true, BackupRestoreBarrierVerified = true }), clock ?? new Clock(Now));
    private sealed class Clock(DateTime utc) : TimeProvider { public DateTime Utc { get; set; } = utc; public override DateTimeOffset GetUtcNow() => new(Utc); }
    private sealed class Adapter : I개인정보파기Adapter
    {
        public string SourceCode => 개인정보파기원천Codes.FoodOrder;
        public int Discoveries, Purges; public bool Throw, Verified = true;
        public Task<IReadOnlyList<개인정보파기대상>> DiscoverAsync(DateTime cutoff, int limit, CancellationToken ct, long afterSequence = 0)
        { Discoveries++; return Task.FromResult<IReadOnlyList<개인정보파기대상>>([new(SourceCode, "order", Now.AddDays(-3), "owner")]); }
        public Task<개인정보파기대상?> FindAsync(string recordId, CancellationToken ct) => Task.FromResult<개인정보파기대상?>(new(SourceCode, recordId, Now.AddDays(-3), "owner"));
        public Task<개인정보파기Adapter결과> PurgeAsync(개인정보파기Job job, DateTime now, CancellationToken ct)
        { Purges++; if (Throw) throw new IOException("010 confidential"); return Task.FromResult(new 개인정보파기Adapter결과(Verified, Verified ? "" : "ProviderNotVerified", ["RDB"])); }
    }
    private sealed class Store : I개인정보보존Store
    {
        public Func<개인정보파기Job, long, Task>? BeforeReplace;
        public Dictionary<string, 개인정보파기Job> Jobs { get; } = [];
        public HashSet<string> Manifest { get; } = [];
        public List<string> Evidence { get; } = [];
        private static 개인정보파기Job Copy(개인정보파기Job x) => JsonSerializer.Deserialize<개인정보파기Job>(JsonSerializer.Serialize(x))!;
        public Task<개인정보파기Job?> GetAsync(string id, CancellationToken ct = default) => Task.FromResult(Jobs.TryGetValue(id, out var x) ? Copy(x) : null);
        public Task CreateAsync(개인정보파기Job job, CancellationToken ct = default) { Jobs.TryAdd(job.Id, Copy(job)); return Task.CompletedTask; }
        public async Task<bool> ReplaceAsync(개인정보파기Job job, long expectedRevision, CancellationToken ct = default)
        {
            var before = BeforeReplace; BeforeReplace = null;
            if (before is not null) await before(job, expectedRevision);
            if (Jobs[job.Id].Revision != expectedRevision) return false;
            job.Revision = expectedRevision + 1; Jobs[job.Id] = Copy(job); return true;
        }
        public Task<IReadOnlyList<개인정보파기Job>> DueAsync(DateTime now, int limit, CancellationToken ct = default, int maxAttempts = 8)
            => Task.FromResult<IReadOnlyList<개인정보파기Job>>(Jobs.Values.Where(x => x.CompletedAtUtc is null && x.DueAtUtc <= now && x.NextAttemptAtUtc <= now
                && (x.Attempts < maxAttempts || x.Holds.Count > 0) && (x.LeaseUntilUtc is null || x.LeaseUntilUtc <= now)).Take(limit).Select(Copy).ToArray());
        public Task SaveEvidenceAsync(string key, string sourceCode, string recordId, string minimizedJson, DateTime expiresAtUtc, CancellationToken ct = default) { Evidence.Add(minimizedJson); return Task.CompletedTask; }
        public Task MarkDeletionAsync(개인정보파기Job job, DateTime now, CancellationToken ct = default) { Manifest.Add(job.Id); return Task.CompletedTask; }
        public Task VerifyDeletionAsync(string key, DateTime now, CancellationToken ct = default) => Task.CompletedTask;
        public Task<bool> HasDeletionAsync(string sourceCode, string recordId, CancellationToken ct = default) => Task.FromResult(Manifest.Contains(개인정보파기Job.Key(sourceCode, recordId)));
        public Task<int> DeleteExpiredEvidenceAsync(DateTime now, CancellationToken ct = default) => Task.FromResult(0);
    }
    private sealed class Ledger : I배송원장개인정보파기Service
    { public Task<bool> 파기Async(string ledgerId, string sourceCode, string recordId, CancellationToken ct) => Task.FromResult(true); }
    private sealed class Sync : I음식마트원장Mongo동기화Service
    {
        public 음식주문응답? LastOrder;
        public Task<커뮤니티원장Dto?> 음식주문동기화Async(음식주문응답 주문, string updatedBy, CancellationToken cancellationToken = default)
        { LastOrder = 주문; return Task.FromResult<커뮤니티원장Dto?>(new 커뮤니티원장Dto { 원장Id = "food-order:" + 주문.주문번호 }); }
        public Task<커뮤니티원장Dto?> 출고원장동기화Async(IReadOnlyList<출고예정> 출고목록, IReadOnlyList<입고요청> 입고목록, string updatedBy,
            string? 현재단계Key = null, string? 원장템플릿Key = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    private sealed class DbFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection; public SsalddelContext Db { get; }
        private DbFixture(SqliteConnection connection) { _connection = connection; Db = new(new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(connection).Options, new Crypto()); }
        public static async Task<DbFixture> CreateAsync() { var c = new SqliteConnection("Data Source=:memory:"); await c.OpenAsync(); var f = new DbFixture(c); await f.Db.Database.EnsureCreatedAsync(); return f; }
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await _connection.DisposeAsync(); }
    }
    private sealed class Crypto : IPersonalDataEncryptionService
    { public string? Protect(string? value) => value is null ? null : "enc:" + value; public string? Unprotect(string? value) => value?.StartsWith("enc:") == true ? value[4..] : value; }
}
