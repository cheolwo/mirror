using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Application.Warehouse;
using Ssalddel.Application.Warehouse.Events;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Inventory;
using Ssalddel.Contracts.Common.ViewSettings;
using Ssalddel.Contracts.Food;
using Ssalddel.Services.Community;
using Ssalddel.Services.LogisticsProcessing.Warehouse;
using Ssalddel.Services.Operations;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Audit;
using 살뜰.Services.Dispatch.Engine;
using 살뜰.도메인.마트;
using 살뜰.도메인.운송;
using 살뜰.도메인.창고;
using 살뜰.도메인.화주;

namespace Ssalddel.Tests.Application.Warehouse;

public sealed class MartPickingCompletionGuardTests
{
    private static readonly DateTime Initial = new(2026, 10, 10, 1, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task 한상품피킹미완료는_두포장진입을차단하고_기록을추가하지않는다()
    {
        await using var db = CreateContext();
        await SeedAsync(db, fullyPicked: false);
        var publisher = new RecordingPublisher();
        var useCase = Packing(db, publisher);
        var result = await useCase.완료Async(11, Request(), Context(), CancellationToken.None);
        var legacy = new WarehouseOperationService(db, new CurrentUser(), new NoOpSync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => legacy.PackInventoryItemAsync(
            11, new 포장작업요청 { 포장수량 = 2, 포장유형 = "일반포장" }, CancellationToken.None));
        Assert.True(result.IsFailed);
        Assert.Equal(409, result.Errors.Single().Metadata["StatusCode"]);
        Assert.All(await db.입고상품.ToArrayAsync(), item => Assert.Equal("적재완료", item.상태));
        Assert.Empty(await db.재고이력.ToArrayAsync());
        Assert.Empty(await db.재고이동.ToArrayAsync());
        Assert.Empty(publisher.Notifications);
        Assert.False(db.ChangeTracker.HasChanges());
    }

    [Theory]
    [InlineData("missing-task")]
    [InlineData("wrong-quantity")]
    [InlineData("wrong-inventory")]
    [InlineData("wrong-sku")]
    [InlineData("wrong-warehouse")]
    [InlineData("missing-completed-at")]
    [InlineData("cancelled-order")]
    [InlineData("missing-line")]
    public async Task 주문전체피킹근거의누락이나불일치는_포장을보류한다(string defect)
    {
        await using var db = CreateContext();
        await SeedAsync(db);
        var task = await db.피킹포장작업.SingleAsync(value => value.작업Key == "pick:sample:2");
        switch (defect)
        {
            case "missing-task": db.피킹포장작업.Remove(task); break;
            case "wrong-quantity": task.수량 = 1; break;
            case "wrong-inventory": task.입고상품Id = 11; break;
            case "wrong-sku": task.SKU = "OTHER"; break;
            case "wrong-warehouse": task.창고Id = 2; break;
            case "missing-completed-at": task.완료일시Utc = null; break;
            case "cancelled-order": (await db.마트주문.SingleAsync()).상태 = "취소"; break;
            case "missing-line": db.마트주문상품.Remove(await db.마트주문상품.FirstAsync()); break;
        }
        await db.SaveChangesAsync();
        Assert.NotNull(await 마트주문피킹완료Guard.주문차단사유Async(db, "SAMPLE-ORDER", CancellationToken.None));
        Assert.False(db.ChangeTracker.HasChanges());
    }

    [Fact]
    public async Task 전체피킹완료후_포장과동일재요청은_한번만기록한다()
    {
        await using var db = CreateContext(); await SeedAsync(db);
        var publisher = new RecordingPublisher(); var useCase = Packing(db, publisher);
        Assert.Null(await 마트주문피킹완료Guard.주문차단사유Async(db, "SAMPLE-ORDER", CancellationToken.None));
        var first = await useCase.완료Async(11, Request(), Context(), CancellationToken.None);
        var replay = await useCase.완료Async(11, Request(), Context(), CancellationToken.None);
        Assert.True(first.IsSuccess); Assert.True(replay.IsSuccess); Assert.True(replay.Value.IdempotentReplay);
        Assert.Single(await db.재고이력.ToArrayAsync());
        Assert.Single(await db.재고이동.ToArrayAsync());
        Assert.Single(publisher.Notifications.OfType<창고포장완료됨Event>());
        Assert.Single(publisher.Notifications.OfType<창고포장배차재확인요청됨Event>());
    }

    [Fact]
    public async Task 다른주문피킹은_현재주문에섞이지않고_일반창고포장은호환된다()
    {
        await using var db = CreateContext(); await SeedAsync(db, fullyPicked: false);
        Assert.Null(await 마트주문피킹완료Guard.주문차단사유Async(db, "GENERAL", CancellationToken.None));
        db.입고상품.Add(new 입고상품 { Id = 99, 창고Id = 1, 상품명 = "일반 표본", SKU = "GENERAL", 가용수량 = 2, 상태 = "적재완료" });
        await db.SaveChangesAsync();
        var result = await Packing(db).완료Async(99, Request(), Context(), CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Equal("적재완료", (await db.입고상품.FindAsync(11L))!.상태);
    }

    [Fact]
    public async Task 포장되지않은재고의Event는_포장작업을완료시키지않는다()
    {
        await using var db = CreateContext(); await SeedAsync(db);
        var result = Assert.Single(await Queue(db).입고상품포장완료반영Async(11, "worker"));
        Assert.False(result.포장완료);
        Assert.Equal(알뜰살뜰마트배차대기결과코드.포장대기, result.결과코드);
        Assert.All(await db.피킹포장작업.Where(task => task.작업유형 == 피킹포장작업유형.포장).ToArrayAsync(),
            task => Assert.Equal(피킹포장작업상태.대기, task.상태));
        Assert.False(db.ChangeTracker.HasChanges());
    }

    [Fact]
    public async Task 포장상태문자열만있고실제이력이없으면_Event와직접인계모두보류한다()
    {
        await using var db = CreateContext(); await SeedAsync(db);
        foreach (var item in await db.입고상품.ToArrayAsync()) item.상태 = "포장완료-일반포장";
        foreach (var task in await db.피킹포장작업.Where(task => task.작업유형 == 피킹포장작업유형.포장).ToArrayAsync())
        { task.상태 = 피킹포장작업상태.완료; task.완료일시Utc = Initial; }
        await db.SaveChangesAsync();
        Assert.False(Assert.Single(await Queue(db).입고상품포장완료반영Async(11, "worker")).포장완료);
        Assert.False((await Queue(db).주문포장완료후배차대기생성Async("SAMPLE-ORDER", "worker")).포장완료);
        Assert.False(db.ChangeTracker.HasChanges());
    }

    [Fact]
    public async Task 취소포장작업은불변이고_중복Event는완료시각을바꾸지않으며_전체포장전인계를보류한다()
    {
        await using var db = CreateContext(); await SeedAsync(db);
        db.피킹포장작업.Add(new 피킹포장작업 { 작업Key = "cancelled-pack", 작업유형 = 피킹포장작업유형.포장,
            상태 = 피킹포장작업상태.취소, 입고상품Id = 11, 주문참조번호 = "SAMPLE-ORDER", UpdatedAt = Initial });
        await db.SaveChangesAsync();
        await Packing(db).완료Async(11, Request(), Context(), CancellationToken.None);
        var queue = Queue(db);
        var first = Assert.Single(await queue.입고상품포장완료반영Async(11, "worker"));
        var packed = await db.피킹포장작업.AsNoTracking().SingleAsync(task => task.작업Key == "pack:sample:1");
        var before = (packed.시작일시Utc, packed.완료일시Utc, packed.UpdatedAt);
        var replay = Assert.Single(await queue.입고상품포장완료반영Async(11, "worker"));
        var after = await db.피킹포장작업.AsNoTracking().SingleAsync(task => task.작업Key == "pack:sample:1");
        var cancelled = await db.피킹포장작업.AsNoTracking().SingleAsync(task => task.작업Key == "cancelled-pack");
        Assert.False(first.포장완료); Assert.False(replay.포장완료);
        Assert.Equal(피킹포장작업상태.완료, after.상태);
        Assert.Equal(before, (after.시작일시Utc, after.완료일시Utc, after.UpdatedAt));
        Assert.Equal(피킹포장작업상태.취소, cancelled.상태); Assert.Null(cancelled.완료일시Utc); Assert.Equal(Initial, cancelled.UpdatedAt);
        Assert.False(db.ChangeTracker.HasChanges());
    }

    [Fact]
    public async Task 모든상품포장근거후에도_실제마트기사미지원차단은유지한다()
    {
        await using var db = CreateContext(); await SeedAsync(db);
        var packing = Packing(db); var queue = Queue(db);
        foreach (var id in new long[] { 11, 12 })
        {
            Assert.True((await packing.완료Async(id, Request(), Context(), CancellationToken.None)).IsSuccess);
            await queue.입고상품포장완료반영Async(id, "worker");
        }
        var result = await queue.주문포장완료후배차대기생성Async("SAMPLE-ORDER", "worker");
        Assert.True(result.포장완료);
        Assert.Equal(MartLastMileDispatchReadinessCodes.ConsumerBindingUnavailable, result.결과코드);
        Assert.Empty(await db.운송원장.ToArrayAsync());
        Assert.Empty(await db.운영체제업무인계.ToArrayAsync());
    }

    [Fact]
    public async Task 동일상품분할포장은_활성작업의합계로확인하고_취소작업은제외한다()
    {
        await using var db = CreateContext(); await SeedAsync(db);
        var first = await db.피킹포장작업.SingleAsync(task => task.작업Key == "pack:sample:1");
        first.수량 = 1;
        db.피킹포장작업.Add(new 피킹포장작업
        {
            작업Key = "pack:sample:1:split", 라인Key = first.라인Key, 작업유형 = 피킹포장작업유형.포장,
            상태 = 피킹포장작업상태.대기, 창고Id = first.창고Id, 입고상품Id = first.입고상품Id,
            출고예정Id = first.출고예정Id, 상품명 = first.상품명, SKU = first.SKU,
            수량 = 1, 주문참조번호 = first.주문참조번호, 작업자UserId = "worker"
        });
        db.피킹포장작업.Add(new 피킹포장작업
        {
            작업Key = "pack:sample:1:cancelled", 작업유형 = 피킹포장작업유형.포장,
            상태 = 피킹포장작업상태.취소, 입고상품Id = first.입고상품Id,
            주문참조번호 = first.주문참조번호, 수량 = 999, UpdatedAt = Initial
        });
        await db.SaveChangesAsync();
        var queue = Queue(db);
        foreach (var id in new long[] { 11, 12 })
        {
            Assert.True((await Packing(db).완료Async(id, Request(), Context(), CancellationToken.None)).IsSuccess);
            await queue.입고상품포장완료반영Async(id, "worker");
        }
        var result = await queue.주문포장완료후배차대기생성Async("SAMPLE-ORDER", "worker");
        Assert.True(result.포장완료);
        Assert.Equal(MartLastMileDispatchReadinessCodes.ConsumerBindingUnavailable, result.결과코드);
        var splitTasks = await db.피킹포장작업.AsNoTracking().Where(task => task.입고상품Id == 11
            && task.작업유형 == 피킹포장작업유형.포장 && task.상태 != 피킹포장작업상태.취소).ToArrayAsync();
        Assert.Equal(2, splitTasks.Length);
        Assert.All(splitTasks, task => Assert.Equal(피킹포장작업상태.완료, task.상태));
        Assert.Equal(2, splitTasks.Sum(task => task.수량));
        var cancelled = await db.피킹포장작업.AsNoTracking().SingleAsync(task => task.작업Key == "pack:sample:1:cancelled");
        Assert.Equal(피킹포장작업상태.취소, cancelled.상태);
        Assert.Null(cancelled.완료일시Utc);
        Assert.Equal(Initial, cancelled.UpdatedAt);
        Assert.False(db.ChangeTracker.HasChanges());
    }

    [Fact]
    public async Task 포장작업일부만존재하면_전체포장완료로판정하지않는다()
    {
        await using var db = CreateContext(); await SeedAsync(db);
        var packing = Packing(db); var queue = Queue(db);
        foreach (var id in new long[] { 11, 12 }) await packing.완료Async(id, Request(), Context(), CancellationToken.None);
        db.피킹포장작업.Remove(await db.피킹포장작업.SingleAsync(task => task.작업Key == "pack:sample:2"));
        await db.SaveChangesAsync();
        var result = Assert.Single(await queue.입고상품포장완료반영Async(11, "worker"));
        Assert.False(result.포장완료);
        Assert.Equal(알뜰살뜰마트배차대기결과코드.포장대기, result.결과코드);
    }

    private static SsalddelContext CreateContext() => new(new DbContextOptionsBuilder<SsalddelContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options, new Encryption());
    private static 포장작업완료요청 Request() => new() { PackagingQuantity = 2, PackagingType = "일반포장", InventoryConfirmed = true, PackageLabelConfirmed = true };
    private static 창고작업요청Context Context() => new("WarehouseManagerApp", "worker", "표본 작업자", 역할명.창고관리자, "/work/outbound/packing", "sample-trace", "127.0.0.1", "test");
    private static 포장작업UseCase Packing(SsalddelContext db, RecordingPublisher? publisher = null) => new(db, new CurrentUser(), new NoOpLog(), publisher ?? new());
    private static 알뜰살뜰마트배차대기Service Queue(SsalddelContext db) => new(db, new NeverHandoff(), new NoOpSync(), new NoOpOutbox(),
        NullLogger<알뜰살뜰마트배차대기Service>.Instance, TimeProvider.System, new HoldReadiness());

    private static async Task SeedAsync(SsalddelContext db, bool fullyPicked = true)
    {
        db.창고.Add(new 창고 { Id = 1, 소유자UserId = "worker", 창고명 = "합성 표본 창고", 주소 = "표본 출고 주소" });
        db.주문자프로필.Add(new 살뜰.도메인.사용자.주문자프로필 { UserId = "orderer", 기본주소 = "표본 수령 주소" });
        db.입고요청.Add(new 입고요청 { Id = 1, 창고Id = 1, 주문참조번호 = "SAMPLE-ORDER" });
        var order = new 마트주문 { Id = 1, 주문참조번호 = "SAMPLE-ORDER", 판매자UserId = "seller", 주문자UserId = "orderer" };
        for (var index = 1; index <= 2; index++)
        {
            var name = $"합성 상품 {index}"; var sku = $"SYNTHETIC-{index}";
            db.입고상품.Add(new 입고상품 { Id = 10 + index, 입고요청Id = 1, 창고Id = 1, 상품명 = name, SKU = sku, 입고수량 = 2, 가용수량 = 2, 상태 = "적재완료" });
            db.출고예정.Add(new 출고예정 { Id = 20 + index, 입고상품Id = 10 + index, 입고요청Id = 1, 출고창고Id = 1,
                주문참조번호 = "SAMPLE-ORDER", 상품명 = name, SKU = sku, 수량 = 2, 판매자UserId = "seller", 주문자UserId = "orderer" });
            order.상품목록.Add(new 마트주문상품 { Id = index, 출고예정Id = 20 + index, 상품명 = name, SKU = sku, 수량 = 2 });
            foreach (var type in new[] { 피킹포장작업유형.피킹, 피킹포장작업유형.포장 })
            {
                var completed = type == 피킹포장작업유형.피킹 && (fullyPicked || index == 1);
                db.피킹포장작업.Add(new 피킹포장작업 { 작업Key = $"{(type == 피킹포장작업유형.피킹 ? "pick" : "pack")}:sample:{index}",
                    라인Key = $"sample:{index}", 작업유형 = type, 상태 = completed ? 피킹포장작업상태.완료 : 피킹포장작업상태.대기,
                    창고Id = 1, 입고상품Id = 10 + index, 출고예정Id = 20 + index, 상품명 = name, SKU = sku, 수량 = 2,
                    주문참조번호 = "SAMPLE-ORDER", 작업자UserId = "worker", 완료일시Utc = completed ? Initial : null });
            }
        }
        db.마트주문.Add(order); await db.SaveChangesAsync();
    }

    private sealed class Encryption : IPersonalDataEncryptionService { public string? Protect(string? value) => value; public string? Unprotect(string? value) => value; }
    private sealed class CurrentUser : ICurrentUserAccessor { public string? UserId => "worker"; public string? Role => 역할명.창고관리자; }
    private sealed class NoOpLog : I사용자행위로그Service { public Task 기록Async(사용자행위로그기록 entry, CancellationToken cancellationToken = default) => Task.CompletedTask; }
    private sealed class RecordingPublisher : IPublisher
    {
        public List<object> Notifications { get; } = [];
        public Task Publish(object notification, CancellationToken cancellationToken = default) { Notifications.Add(notification); return Task.CompletedTask; }
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification { Notifications.Add(notification); return Task.CompletedTask; }
    }
    private sealed class HoldReadiness : IMartLastMileDispatchReadinessPolicy
    {
        public Task<MartLastMileDispatchReadiness> EvaluateAsync(string orderReference, CancellationToken cancellationToken)
            => Task.FromResult(MartLastMileDispatchReadiness.Hold(MartLastMileDispatchReadinessCodes.ConsumerBindingUnavailable, "표본: 기사 연결 미지원"));
    }
    private sealed class NeverHandoff : I살뜰마트라스트마일배차인계Service
    {
        public Task<살뜰마트라스트마일배차인계결과> 인계Async(살뜰마트라스트마일배차인계요청 request, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("실제 기사 인계는 이 시험에서 허용되지 않습니다.");
    }
    private sealed class NoOpSync : I운송원장Mongo동기화Service
    {
        public Task<커뮤니티원장Dto?> 화주운송의뢰동기화Async(화주운송의뢰 request, string updatedBy, CancellationToken cancellationToken = default) => Task.FromResult<커뮤니티원장Dto?>(null);
        public Task<커뮤니티원장Dto?> 운송실행투영동기화Async(운송원장 ledger, string updatedBy, CancellationToken cancellationToken = default) => Task.FromResult<커뮤니티원장Dto?>(null);
        public Task<운송원장Mongo동기화상태> 상태조회Async(string requestId, CancellationToken cancellationToken = default) => Task.FromResult(운송원장Mongo동기화상태.Empty(requestId, "test"));
    }
    private sealed class NoOpOutbox : I음식마트원장동기화OutboxService
    {
        public Task 음식주문예약후즉시처리Async(음식주문응답 order, string updatedBy, string idempotencyKey, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task 출고원장예약후즉시처리Async(IReadOnlyList<출고예정> outbounds, IReadOnlyList<입고요청> inbounds, string updatedBy, string idempotencyKey, string? currentStageKey = null, string? ledgerTemplateKey = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<int> 대기항목처리Async(int take = 100, CancellationToken cancellationToken = default) => Task.FromResult(0);
    }
}
