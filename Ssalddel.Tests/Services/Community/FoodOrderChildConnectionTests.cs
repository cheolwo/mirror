using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Food;
using Ssalddel.Services.Community;
using Ssalddel.Services.Outbox;
using Ssalddel.Services.PrivacyRetention;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Dispatch.Engine;
using 살뜰.도메인.공통;

namespace Ssalddel.Tests.Services.Community;

public sealed class FoodOrderChildConnectionTests
{
    private const string OrderNo = "food-connection-1";
    private const string ChildId = "transport:food-connection-1";

    [Theory]
    [InlineData(false, 운송의뢰배차원천유형.음식점주문)]
    [InlineData(true, 운송의뢰배차원천유형.음식점주문)]
    [InlineData(false, 운송의뢰배차원천유형.음식주문)]
    public async Task 동기화는_기존운송원장을_주문배송관계로한번연결하고_재처리에서보존한다(bool customChildId, string sourceType)
    {
        await using var db = Context();
        var store = new LedgerStore();
        var childId = customChildId ? "transport:existing-food-ledger" : ChildId;
        await SeedQueueAsync(db, childId: customChildId ? childId : null, sourceType: sourceType);
        store.Ledgers[childId] = Child(childId, sourceType);
        var service = Service(db, store);

        var first = await service.음식주문동기화Async(Order(), "worker");
        var second = await service.음식주문동기화Async(Order(), "worker");

        var relation = Assert.Single(Assert.IsType<커뮤니티원장Dto>(second).포함원장목록);
        Assert.Equal(childId, relation.원장Id);
        Assert.Equal(주문원장포함역할.배송, relation.역할);
        Assert.Equal(CommunityLedgerRelationTypes.Contains, relation.관계유형);
        Assert.True(relation.필수여부);
        Assert.Equal(new long?[] { 0, first!.Revision }, store.SaveRequests.Select(x => x.기대Revision));
        Assert.Null(store.SaveRequests[1].포함원장목록);
        Assert.Single(await db.운송원장.ToListAsync());
        Assert.Equal(2, store.Ledgers.Count);
    }

    [Fact]
    public async Task 기존배송참조의_역할필수여부순서를_동기화가덮어쓰지않는다()
    {
        await using var db = Context();
        await SeedQueueAsync(db);
        var store = new LedgerStore();
        store.Ledgers[ChildId] = Child();
        var relation = new 커뮤니티포함원장참조Dto
        {
            원장Id = ChildId, 원장템플릿Key = CommunityLedgerTemplateKeys.FoodDelivery,
            역할 = 주문원장포함역할.운송, 관계유형 = CommunityLedgerRelationTypes.Reference,
            필수여부 = false, 표시순서 = 8
        };
        store.Ledgers[RootId] = Root([relation]);

        var result = await Service(db, store).음식주문동기화Async(Order(), "worker");

        Assert.Same(relation, Assert.Single(result!.포함원장목록));
        Assert.Null(Assert.Single(store.SaveRequests).포함원장목록);
    }

    [Theory]
    [InlineData("queue-source")]
    [InlineData("queue-origin")]
    [InlineData("child-source")]
    [InlineData("child-origin")]
    [InlineData("child-type")]
    [InlineData("child-community")]
    [InlineData("child-purged")]
    public async Task 주문과업무출처가다르면_잘못된배송관계를저장하지않는다(string mismatch)
    {
        await using var db = Context();
        await SeedQueueAsync(db, sourceId: mismatch == "queue-source" ? "other-order" : OrderNo,
            sourceType: mismatch == "queue-origin" ? 운송의뢰배차원천유형.화주운송의뢰 : 운송의뢰배차원천유형.음식점주문);
        var child = Child();
        if (mismatch == "child-source") child.외부참조 = new Dictionary<string, string>
        { ["원천유형"] = 운송의뢰배차원천유형.음식점주문, ["원천Id"] = "other-order" };
        if (mismatch == "child-origin") child.외부참조 = new Dictionary<string, string>
        { ["원천유형"] = 운송의뢰배차원천유형.화주운송의뢰, ["원천Id"] = OrderNo };
        if (mismatch == "child-type") child.원장템플릿Key = CommunityLedgerTemplateKeys.CargoTransport;
        if (mismatch == "child-community") child.커뮤니티Id = "another-community";
        if (mismatch == "child-purged") child.확장속성 = new Dictionary<string, string>
        { [배송원장개인정보파기Service.Marker] = "true" };
        var store = new LedgerStore(); store.Ledgers[ChildId] = child;

        Assert.Null(await Service(db, store).음식주문동기화Async(Order(), "worker"));
        Assert.Empty(store.SaveRequests);
        Assert.False(store.Ledgers.ContainsKey(RootId));
    }

    [Fact]
    public async Task 배송Mongo가아직없으면_Outbox를대기로남기고_새Context재시도에서원주문에연결한다()
    {
        var databaseName = Guid.NewGuid().ToString("N");
        var store = new LedgerStore();
        await using (var db = Context(databaseName))
        {
            await SeedQueueAsync(db);
            await Outbox(db, store).음식주문예약후즉시처리Async(Order(), "worker", "food-event:connection");
            var item = await db.음식마트원장동기화Outbox.SingleAsync();
            Assert.Equal(OutboxProcessingStatuses.Pending, item.처리상태);
            Assert.Equal(1, item.시도횟수);
            Assert.False(store.Ledgers.ContainsKey(RootId));
            item.UpdatedAtUtc = DateTime.UtcNow - OutboxProcessingPolicy.RetryDelay - TimeSpan.FromSeconds(1);
            await db.SaveChangesAsync();
        }
        store.Ledgers[ChildId] = Child();
        await using (var retry = Context(databaseName))
        {
            Assert.Equal(1, await Outbox(retry, store).대기항목처리Async());
            var item = await retry.음식마트원장동기화Outbox.SingleAsync();
            Assert.Equal(OutboxProcessingStatuses.Succeeded, item.처리상태);
            Assert.Equal(2, item.시도횟수);
            Assert.Single(await retry.운송원장.ToListAsync());
            Assert.Equal(ChildId, Assert.Single(store.Ledgers[RootId].포함원장목록).원장Id);
            await Outbox(retry, store).음식주문예약후즉시처리Async(Order(), "worker", "food-event:connection");
            Assert.Single(await retry.음식마트원장동기화Outbox.ToListAsync());
            Assert.Single(store.SaveRequests);
        }
    }

    [Fact]
    public async Task 배송연결과다른관계편집이겹치면_최신판본의관계를합쳐재시도한다()
    {
        await using var db = Context();
        await SeedQueueAsync(db);
        var store = new LedgerStore { ConflictOnce = true };
        store.Ledgers[ChildId] = Child();

        var result = await Service(db, store).음식주문동기화Async(Order(), "worker");

        Assert.NotNull(result);
        Assert.Equal(new long?[] { 0, 1 }, store.SaveRequests.Select(x => x.기대Revision));
        Assert.Equal(2, result.포함원장목록.Count);
        Assert.Contains(result.포함원장목록, x => x.원장Id == "settlement:concurrent");
        Assert.Equal(5, result.포함원장목록.Single(x => x.원장Id == ChildId).표시순서);
    }

    [Fact]
    public async Task 배차가없는주문은_배송원장을새로생성하지않고_기존관계도보존한다()
    {
        await using var db = Context();
        var store = new LedgerStore();
        store.Ledgers[RootId] = Root([OtherReference()]);
        var order = Order(); order.배차대기Id = null;

        var result = await Service(db, store).음식주문동기화Async(order, "worker");

        Assert.Equal("settlement:concurrent", Assert.Single(result!.포함원장목록).원장Id);
        Assert.Null(Assert.Single(store.SaveRequests).포함원장목록);
        Assert.Empty(await db.운송원장.ToListAsync());
        Assert.Single(store.Ledgers);
    }

    private static string RootId => 음식마트원장Mongo동기화Builder.음식주문원장Id생성(OrderNo);
    private static 음식마트원장Mongo동기화Service Service(SsalddelContext db, I커뮤니티원장저장소 store)
        => new(store, db, NullLogger<음식마트원장Mongo동기화Service>.Instance);
    private static 음식마트원장동기화OutboxService Outbox(SsalddelContext db, I커뮤니티원장저장소 store)
        => new(db, Service(db, store), NullLogger<음식마트원장동기화OutboxService>.Instance);
    private static SsalddelContext Context(string? databaseName = null)
        => new(new DbContextOptionsBuilder<SsalddelContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString("N")).Options, new NoEncryption());
    private static async Task SeedQueueAsync(SsalddelContext db, string sourceId = OrderNo, string? childId = null,
        string sourceType = 운송의뢰배차원천유형.음식점주문)
    {
        db.운송원장.Add(new()
        {
            Id = 81, 의뢰Id = OrderNo, 운송번호 = OrderNo,
            배차업무유형 = 상태값.배차업무유형.음식배달,
            원본의뢰유형 = sourceType, 원본의뢰Id = sourceId,
            커뮤니티원장Id = childId
        });
        await db.SaveChangesAsync();
    }
    private static 음식주문응답 Order() => new()
    { 주문번호 = OrderNo, 주문자UserId = "customer-1", 음식점Id = 41, 상태 = 음식주문상태코드.주문확인, 배차대기Id = 81 };
    private static 커뮤니티원장Dto Child(string childId = ChildId, string sourceType = 운송의뢰배차원천유형.음식점주문) => new()
    {
        원장Id = childId, 커뮤니티Id = "platform", 원장템플릿Key = CommunityLedgerTemplateKeys.FoodDelivery,
        외부참조 = new Dictionary<string, string>
        { ["원천유형"] = sourceType, ["원천Id"] = OrderNo }
    };
    private static 커뮤니티원장Dto Root(IReadOnlyList<커뮤니티포함원장참조Dto> references) => new()
    { 원장Id = RootId, Revision = 1, 커뮤니티Id = "platform", 원장템플릿Key = CommunityLedgerTemplateKeys.FoodOrder, 포함원장목록 = references };
    private static 커뮤니티포함원장참조Dto OtherReference() => new()
    {
        원장Id = "settlement:concurrent", 원장템플릿Key = CommunityLedgerTemplateKeys.CargoTransport,
        역할 = 주문원장포함역할.운송, 표시순서 = 4
    };
    private sealed class NoEncryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
    private sealed class LedgerStore : I커뮤니티원장저장소
    {
        public Dictionary<string, 커뮤니티원장Dto> Ledgers { get; } = new(StringComparer.Ordinal);
        public List<커뮤니티원장저장요청> SaveRequests { get; } = [];
        public bool ConflictOnce { get; init; }
        public Task<커뮤니티원장Dto> 원장저장Async(커뮤니티원장저장요청 request, string updatedBy, CancellationToken cancellationToken = default)
        {
            SaveRequests.Add(request);
            if (ConflictOnce && SaveRequests.Count == 1)
            {
                Ledgers[request.원장Id!] = Root([OtherReference()]);
                throw new InvalidOperationException("원장의 현재 상태가 다른 요청에서 먼저 변경되었습니다.");
            }
            var existing = Ledgers.GetValueOrDefault(request.원장Id!);
            Assert.Equal(existing?.Revision ?? 0, request.기대Revision);
            var result = new 커뮤니티원장Dto
            {
                원장Id = request.원장Id!, Revision = (existing?.Revision ?? 0) + 1,
                커뮤니티Id = request.커뮤니티Id, 원장템플릿Key = request.원장템플릿Key,
                제목 = request.제목, 상태 = request.상태!, 현재단계Key = request.현재단계Key,
                블록목록 = request.블록목록, 외부참조 = request.외부참조,
                포함원장목록 = request.포함원장목록 ?? existing?.포함원장목록 ?? []
            };
            Ledgers[result.원장Id] = result;
            return Task.FromResult(result);
        }
        public Task<커뮤니티원장Dto?> 원장조회Async(string 원장Id, CancellationToken cancellationToken = default)
            => Task.FromResult(Ledgers.GetValueOrDefault(원장Id));
        public Task<IReadOnlyList<커뮤니티원장Dto>> 원장목록조회Async(커뮤니티원장조회조건 query, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<커뮤니티원장Dto>>([]);
        public Task<커뮤니티원장Dto?> 원장상태변경Async(커뮤니티원장상태변경요청 request, string updatedBy, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
