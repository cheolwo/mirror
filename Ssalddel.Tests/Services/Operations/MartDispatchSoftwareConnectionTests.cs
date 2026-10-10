using System.Text.Json;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Application.Warehouse;
using Ssalddel.Application.Warehouse.Events;
using Ssalddel.Application.Warehouse.Handlers;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Inventory;
using Ssalddel.Contracts.Common.Operations;
using Ssalddel.Contracts.Common.Versioning;
using Ssalddel.Services.Community;
using Ssalddel.Services.LogisticsProcessing.Warehouse;
using Ssalddel.Services.Operations;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Audit;
using 살뜰.Services.DeliveryZones;
using 살뜰.Services.Dispatch.Coordination;
using 살뜰.Services.Dispatch.Engine;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.Services.Operations;
using 살뜰.Services.Versioning;
using 살뜰.도메인.공통;
using 살뜰.도메인.마트;
using 살뜰.도메인.사용자;
using 살뜰.도메인.운송;
using 살뜰.도메인.운영;
using 살뜰.도메인.운영;
using 살뜰.도메인.화주;
using 살뜰.도메인.창고;

namespace Ssalddel.Tests.Services.Operations;

/// <summary>
/// 실제 마트 배차 Service → 라스트마일 Service → OS Coordinator → 공통 큐의 격리 SQLite 연결 시험입니다.
/// Ready 소비자는 시험 전용이며 Mongo·Redis 경계는 기록 Adapter로 대체합니다. 운영 기사 연결 증거가 아닙니다.
/// </summary>
public sealed class MartDispatchSoftwareConnectionTests
{
    private const string 주문참조 = "MART-SOFTWARE-057";
    private static readonly DateTime 주문시각 = new(2026, 10, 10, 1, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime 판정시각 = 주문시각.AddMinutes(10);

    [Fact]
    public async Task 시험전용소비자준비_전체포장후실제인계와큐를저장하고새Context재처리도중복하지않는다()
    {
        await using var fixture = await 격리DbFixture.생성Async();
        await SeedAsync(fixture.Db);
        var boundaries = new 기록경계();
        var service = CreateService(fixture.Db, boundaries, new 시험전용ReadyPolicy());

        var result = await service.주문포장완료후배차대기생성Async(주문참조, "worker-sample");
        var queue = await fixture.Db.운송원장.AsNoTracking().SingleAsync();
        var handoff = await fixture.Db.운영체제업무인계.AsNoTracking().SingleAsync();
        var outbox = await fixture.Db.운영체제업무인계Outbox.AsNoTracking().OrderBy(item => item.Id).ToListAsync();

        Assert.True(result.생성또는조회됨);
        Assert.Equal(queue.Id, result.배차대기Id);
        Assert.Equal(운송의뢰배차원천유형.살뜰마트포장완료주문, queue.원본의뢰유형);
        Assert.Equal(주문참조, queue.원본의뢰Id);
        Assert.Equal("seller-sample", queue.화주Id);
        Assert.Equal(상태값.배차업무유형.음식배달, queue.배차업무유형);
        Assert.Equal("서울 중랑구 표본 창고로 1", queue.픽업_도로명주소);
        Assert.Equal("서울 중랑구 표본 수령로 2", queue.하차_도로명주소);
        Assert.Equal(운영체제업무인계상태Codes.수락됨, handoff.상태Code);
        Assert.Equal(OperatingSystemIds.SsalddelMartUrbanLogistics, handoff.출발운영체제Id);
        Assert.Equal(OperatingSystemIds.FoodDelivery, handoff.현재책임운영체제Id);
        Assert.Equal($"food-delivery-dispatch-request:{주문참조}", handoff.도착업무StableId);
        Assert.Equal(2, handoff.Revision);
        Assert.Equal(2, outbox.Count);
        var previous = JsonSerializer.Serialize(new { queue, handoff, outbox });

        await using var reloaded = fixture.새Context();
        var replay = await CreateService(reloaded, boundaries, new 시험전용ReadyPolicy())
            .주문포장완료후배차대기생성Async(주문참조, "worker-sample");

        Assert.True(replay.생성또는조회됨);
        Assert.Equal(result.배차대기Id, replay.배차대기Id);
        Assert.Equal(result.인계StableId, replay.인계StableId);
        Assert.Equal(previous, JsonSerializer.Serialize(new
        {
            queue = await reloaded.운송원장.AsNoTracking().SingleAsync(),
            handoff = await reloaded.운영체제업무인계.AsNoTracking().SingleAsync(),
            outbox = await reloaded.운영체제업무인계Outbox.AsNoTracking().OrderBy(item => item.Id).ToListAsync()
        }));
        Assert.Equal("배차대기", (await reloaded.마트주문.AsNoTracking().SingleAsync()).현재단계);
        Assert.All(await reloaded.출고예정.AsNoTracking().ToListAsync(), plan => Assert.Equal(주문참조, plan.운송의뢰Id));
        Assert.Equal(2, await reloaded.재고이력.CountAsync());
        Assert.Equal(4, await reloaded.피킹포장작업.CountAsync());
        Assert.Empty(await reloaded.음식주문.ToListAsync());
        Assert.Equal(2, boundaries.MongoCalls);
        Assert.Equal(2, boundaries.OutboxKeys.Count);
        Assert.Single(boundaries.OutboxKeys.Distinct(StringComparer.Ordinal));
        Assert.Equal(2, boundaries.ZoneCalls);
        Assert.All(boundaries.FoodUpserts, id => Assert.Equal(주문참조, id));
        Assert.Empty(boundaries.CargoUpserts);
    }

    [Fact]
    public async Task 한상품이포장미완료이면_시험전용Ready라도원천상태를유지하고인계와큐를만들지않는다()
    {
        await using var fixture = await 격리DbFixture.생성Async();
        await SeedAsync(fixture.Db, 두번째포장완료: false);
        var before = await 원천상태Async(fixture.Db);
        var boundaries = new 기록경계();

        var result = await CreateService(fixture.Db, boundaries, new 시험전용ReadyPolicy())
            .주문포장완료후배차대기생성Async(주문참조, "worker-sample");

        Assert.False(result.생성또는조회됨);
        Assert.Equal(알뜰살뜰마트배차대기결과코드.포장대기, result.결과코드);
        Assert.Equal(before, await 원천상태Async(fixture.Db));
        await AssertNoDispatchAsync(fixture.Db, boundaries);
    }

    [Theory]
    [InlineData(true, MartLastMileDispatchReadinessCodes.ConsumerBindingUnavailable)]
    [InlineData(false, MartLastMileDispatchReadinessCodes.FeatureDisabled)]
    public async Task 제품Policy는_전체포장후에도소비자미지원또는기능OFF를보류한다(bool enabled, string expectedCode)
    {
        await using var fixture = await 격리DbFixture.생성Async();
        await SeedAsync(fixture.Db);
        var before = await 원천상태Async(fixture.Db);
        var boundaries = new 기록경계();
        var readiness = new MartLastMileDispatchReadinessPolicy(fixture.Db, new 시험기능관문(enabled));

        var result = await CreateService(fixture.Db, boundaries, readiness)
            .주문포장완료후배차대기생성Async(주문참조, "worker-sample");
        var replay = await CreateService(fixture.Db, boundaries, readiness)
            .주문포장완료후배차대기생성Async(주문참조, "worker-sample");

        Assert.False(result.생성또는조회됨);
        Assert.True(result.포장완료);
        Assert.Equal(expectedCode, result.결과코드);
        Assert.Equal(expectedCode, replay.결과코드);
        Assert.Equal(before, await 원천상태Async(fixture.Db));
        await AssertNoDispatchAsync(fixture.Db, boundaries);
    }

    [Theory]
    [InlineData("source-type", false)]
    [InlineData("source-id", false)]
    [InlineData("shipper", false)]
    [InlineData("work-type", false)]
    [InlineData("source-type", true)]
    [InlineData("source-id", true)]
    [InlineData("shipper", true)]
    [InlineData("work-type", true)]
    public async Task 같은운송번호의다른원천은_확정기사유무와관계없이상태를덮지않고인계와인덱스도만들지않는다(string conflict, bool accepted)
    {
        await using var fixture = await 격리DbFixture.생성Async();
        await SeedAsync(fixture.Db);
        fixture.Db.운송원장.Add(new 운송원장
        {
            의뢰Id = 주문참조, 운송번호 = 주문참조,
            원본의뢰유형 = conflict == "source-type" ? 운송의뢰배차원천유형.화주운송의뢰 : 운송의뢰배차원천유형.살뜰마트포장완료주문,
            원본의뢰Id = conflict == "source-id" ? "other-source" : 주문참조,
            화주Id = conflict == "shipper" ? "other-shipper" : "seller-sample",
            배차업무유형 = conflict == "work-type" ? 상태값.배차업무유형.용달운송 : 상태값.배차업무유형.음식배달,
            확정기사Id = accepted ? "previous-driver" : null,
            픽업_도로명주소 = "기존 업무 픽업", 하차_도로명주소 = "기존 업무 전달",
            상태 = 상태값.배차대기상태.대기, CreatedAt = 주문시각, UpdatedAt = 주문시각
        });
        await fixture.Db.SaveChangesAsync();
        var sourceBefore = await 원천상태Async(fixture.Db);
        var queueBefore = JsonSerializer.Serialize(await fixture.Db.운송원장.AsNoTracking().SingleAsync());
        var boundaries = new 기록경계();
        var service = CreateService(fixture.Db, boundaries, new 시험전용ReadyPolicy());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.주문포장완료후배차대기생성Async(주문참조, "worker-sample"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.주문포장완료후배차대기생성Async(주문참조, "worker-sample"));

        Assert.Equal(sourceBefore, await 원천상태Async(fixture.Db));
        Assert.Equal(queueBefore, JsonSerializer.Serialize(await fixture.Db.운송원장.AsNoTracking().SingleAsync()));
        Assert.Empty(await fixture.Db.운영체제업무인계.ToListAsync());
        Assert.Empty(await fixture.Db.운영체제업무인계Outbox.ToListAsync());
        Assert.Equal(0, boundaries.ZoneCalls);
        Assert.Equal(0, boundaries.MongoCalls);
        Assert.Empty(boundaries.OutboxKeys);
        Assert.Empty(boundaries.FoodUpserts);
        Assert.Empty(boundaries.CargoUpserts);
        Assert.False(fixture.Db.ChangeTracker.HasChanges());
    }

    [Fact]
    public async Task 원천이일치하는확정기사배정은_인계및큐를재생성하지않고원래수락을유지한다()
    {
        await using var fixture = await 격리DbFixture.생성Async();
        await SeedAsync(fixture.Db);
        var boundaries = new 기록경계();
        var service = CreateService(fixture.Db, boundaries, new 시험전용ReadyPolicy());
        Assert.True((await service.주문포장완료후배차대기생성Async(주문참조, "worker-sample")).생성또는조회됨);
        var queue = await fixture.Db.운송원장.SingleAsync();
        queue.확정기사Id = "accepted-driver";
        queue.배차큐단계 = 상태값.배차큐단계.확정;
        await fixture.Db.SaveChangesAsync();
        var before = JsonSerializer.Serialize(await fixture.Db.운송원장.AsNoTracking().SingleAsync());
        var sourceBefore = await 원천상태Async(fixture.Db);
        var handoffBefore = JsonSerializer.Serialize(await fixture.Db.운영체제업무인계.AsNoTracking().SingleAsync());
        var zoneCalls = boundaries.ZoneCalls;
        var mongoCalls = boundaries.MongoCalls;

        await using var reloaded = fixture.새Context();
        var replay = await CreateService(reloaded, boundaries, new 시험전용ReadyPolicy())
            .주문포장완료후배차대기생성Async(주문참조, "worker-sample");

        Assert.True(replay.생성또는조회됨);
        Assert.Equal(queue.Id, replay.배차대기Id);
        Assert.Equal(살뜰마트라스트마일배차전략Codes.수락배정유지, replay.전략Code);
        Assert.Equal(before, JsonSerializer.Serialize(await reloaded.운송원장.AsNoTracking().SingleAsync()));
        Assert.Equal(sourceBefore, await 원천상태Async(reloaded));
        Assert.Equal(handoffBefore, JsonSerializer.Serialize(await reloaded.운영체제업무인계.AsNoTracking().SingleAsync()));
        Assert.Equal(2, await reloaded.운영체제업무인계Outbox.CountAsync());
        Assert.Equal(zoneCalls, boundaries.ZoneCalls);
        Assert.Equal(mongoCalls, boundaries.MongoCalls);
        Assert.Single(boundaries.OutboxKeys);
        Assert.False(reloaded.ChangeTracker.HasChanges());
    }

    [Fact]
    public async Task 저장후Mongo경계실패는_명시적재요청시같은큐와인계를유지하며포장근거를중복하지않는다()
    {
        await using var fixture = await 격리DbFixture.생성Async();
        await SeedAsync(fixture.Db);
        var boundaries = new 기록경계 { 다음Mongo실패 = true };
        var service = CreateService(fixture.Db, boundaries, new 시험전용ReadyPolicy());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.주문포장완료후배차대기생성Async(주문참조, "worker-sample"));
        var queueId = (await fixture.Db.운송원장.AsNoTracking().SingleAsync()).Id;
        var handoff = await fixture.Db.운영체제업무인계.AsNoTracking().SingleAsync();
        var sourceAfterFailure = await 원천상태Async(fixture.Db);
        Assert.Empty(boundaries.OutboxKeys);

        await using var reloaded = fixture.새Context();
        var retry = await CreateService(reloaded, boundaries, new 시험전용ReadyPolicy())
            .주문포장완료후배차대기생성Async(주문참조, "worker-sample");

        Assert.True(retry.생성또는조회됨);
        Assert.Equal(queueId, retry.배차대기Id);
        Assert.Equal(handoff.인계StableId, retry.인계StableId);
        Assert.Equal(sourceAfterFailure, await 원천상태Async(reloaded));
        Assert.Equal(2, await reloaded.운영체제업무인계Outbox.CountAsync());
        Assert.Equal(1, await reloaded.운영체제업무인계.CountAsync());
        Assert.Equal(1, await reloaded.운송원장.CountAsync());
        Assert.Equal(2, await reloaded.재고이력.CountAsync());
        Assert.Equal(4, await reloaded.피킹포장작업.CountAsync());
        Assert.Equal(2, boundaries.MongoCalls);
        Assert.Single(boundaries.OutboxKeys);
    }

    [Fact]
    public async Task 실제포장UseCase와두EventHandler를통한후속실패재요청도_포장사실과배차원장을한번만저장한다()
    {
        await using var fixture = await 격리DbFixture.생성Async();
        await SeedAsync(fixture.Db);
        var item = await fixture.Db.입고상품.SingleAsync(value => value.SKU == "SAMPLE-2");
        item.상태 = "적재완료";
        fixture.Db.재고이력.Remove(await fixture.Db.재고이력.SingleAsync(value => value.입고상품Id == item.Id));
        var task = await fixture.Db.피킹포장작업.SingleAsync(value => value.입고상품Id == item.Id && value.작업유형 == 피킹포장작업유형.포장);
        task.상태 = 피킹포장작업상태.대기;
        task.시작일시Utc = null;
        task.완료일시Utc = null;
        await fixture.Db.SaveChangesAsync();
        var itemId = item.Id;
        var boundaries = new 기록경계 { 다음Mongo실패 = true };
        var events = new List<INotification>();
        var logs = new 기록행위로그();
        var request = new 포장작업완료요청
        {
            PackagingQuantity = 2, PackagingType = 포장유형코드.일반포장,
            InventoryConfirmed = true, PackageLabelConfirmed = true
        };
        var context = new 창고작업요청Context(
            "WarehouseManagerApp", "seller-sample", "표본 창고 담당자", 역할명.창고관리자,
            "/work/outbound/packing", "sample-pack-r57", "127.0.0.1", "test");

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreatePackingUseCase(fixture.Db, boundaries, events, logs)
            .완료Async(itemId, request, context, CancellationToken.None));
        var history = await fixture.Db.재고이력.AsNoTracking().SingleAsync(value => value.입고상품Id == itemId && value.이력유형 == "포장");
        var completedTask = await fixture.Db.피킹포장작업.AsNoTracking().SingleAsync(value => value.Id == task.Id);
        var taskBeforeReplay = JsonSerializer.Serialize(completedTask);
        var sourceAfterFailure = await 원천상태Async(fixture.Db);
        var queueId = (await fixture.Db.운송원장.AsNoTracking().SingleAsync()).Id;
        var handoff = await fixture.Db.운영체제업무인계.AsNoTracking().SingleAsync();

        Assert.Equal(피킹포장작업상태.완료, completedTask.상태);
        Assert.Equal(history.처리일시, completedTask.시작일시Utc);
        Assert.Equal(history.처리일시, completedTask.완료일시Utc);
        await using var reloaded = fixture.새Context();
        var replay = await CreatePackingUseCase(reloaded, boundaries, events, logs)
            .완료Async(itemId, request, context, CancellationToken.None);

        Assert.True(replay.IsSuccess);
        Assert.True(replay.Value.IdempotentReplay);
        Assert.Equal(history.처리일시, replay.Value.PackedAtUtc);
        Assert.Equal(sourceAfterFailure, await 원천상태Async(reloaded));
        Assert.Equal(taskBeforeReplay, JsonSerializer.Serialize(await reloaded.피킹포장작업.AsNoTracking().SingleAsync(value => value.Id == task.Id)));
        Assert.Single(await reloaded.재고이력.Where(value => value.입고상품Id == itemId && value.이력유형 == "포장").ToListAsync());
        Assert.Single(await reloaded.재고이동.Where(value => value.입고상품Id == itemId && value.이동유형 == "포장").ToListAsync());
        Assert.Single(events.OfType<창고포장완료됨Event>());
        Assert.Single(events.OfType<창고포장배차재확인요청됨Event>());
        Assert.Single(logs.Entries);
        Assert.Equal(queueId, (await reloaded.운송원장.AsNoTracking().SingleAsync()).Id);
        Assert.Equal(handoff.인계StableId, (await reloaded.운영체제업무인계.AsNoTracking().SingleAsync()).인계StableId);
        Assert.Equal(2, await reloaded.운영체제업무인계Outbox.CountAsync());
        Assert.Equal(2, boundaries.MongoCalls);
        Assert.Single(boundaries.OutboxKeys);
        Assert.False(reloaded.ChangeTracker.HasChanges());
    }

    private static 포장작업UseCase CreatePackingUseCase(
        SsalddelContext db, 기록경계 boundaries, List<INotification> events, 기록행위로그 logs)
    {
        var handler = new 알뜰살뜰마트포장완료배차대기EventHandler(
            CreateService(db, boundaries, new 시험전용ReadyPolicy()),
            NullLogger<알뜰살뜰마트포장완료배차대기EventHandler>.Instance);
        return new 포장작업UseCase(db, new 고정창고담당자(), logs, new 실제HandlerPublisher(handler, events));
    }

    private static 알뜰살뜰마트배차대기Service CreateService(
        SsalddelContext db, 기록경계 boundaries, IMartLastMileDispatchReadinessPolicy readiness)
    {
        var clock = new 고정시계();
        var queue = new 운송의뢰배차대기Service(db, new 운송의뢰배차원천분류Service(), boundaries, boundaries, boundaries);
        var handoff = new 살뜰마트라스트마일배차인계Service(
            new 운영체제업무인계Coordinator(db, clock), queue, new 살뜰마트라스트마일배차Policy(), readiness);
        return new 알뜰살뜰마트배차대기Service(
            db, handoff, boundaries, boundaries, NullLogger<알뜰살뜰마트배차대기Service>.Instance, clock, readiness);
    }

    private static async Task SeedAsync(SsalddelContext db, bool 두번째포장완료 = true)
    {
        var warehouse = new 창고
        {
            소유자UserId = "seller-sample", 창고명 = "격리 표본 창고", 주소 = "서울 중랑구 표본 창고로 1",
            위도 = 37.58m, 경도 = 127.08m, CreatedAt = 주문시각, UpdatedAt = 주문시각
        };
        db.창고.Add(warehouse);
        db.주문자프로필.Add(new 주문자프로필
        {
            UserId = "orderer-sample", 표시명 = "격리 표본 주문자", 기본주소 = "서울 중랑구 표본 수령로 2",
            CreatedAt = 주문시각, UpdatedAt = 주문시각
        });
        await db.SaveChangesAsync();
        var order = new 마트주문
        {
            주문참조번호 = 주문참조, 판매자UserId = "seller-sample", 주문자UserId = "orderer-sample",
            상태 = "포장 진행", 현재단계 = "포장", CreatedAt = 주문시각, UpdatedAt = 주문시각
        };
        db.마트주문.Add(order);
        for (var index = 1; index <= 2; index++)
        {
            var packed = index == 1 || 두번째포장완료;
            var item = new 입고상품
            {
                창고Id = warehouse.Id, 소유자UserId = "seller-sample", 판매자UserId = "seller-sample",
                상품명 = $"표본 상품 {index}", SKU = $"SAMPLE-{index}", 입고수량 = index, 가용수량 = index,
                상태 = packed ? "포장완료 - 일반포장" : "피킹완료", CreatedAt = 주문시각, UpdatedAt = 주문시각
            };
            db.입고상품.Add(item);
            await db.SaveChangesAsync();
            var outbound = new 출고예정
            {
                주문참조번호 = 주문참조, 판매자UserId = "seller-sample", 주문자UserId = "orderer-sample",
                출고창고Id = warehouse.Id, 입고상품Id = item.Id, 상품명 = item.상품명, SKU = item.SKU,
                수량 = index, 상태 = 출고상태.준비중, CreatedAt = 주문시각, UpdatedAt = 주문시각
            };
            db.출고예정.Add(outbound);
            await db.SaveChangesAsync();
            order.상품목록.Add(new 마트주문상품
            {
                출고예정Id = outbound.Id, 상품명 = item.상품명, SKU = item.SKU, 수량 = index,
                상태 = "포장 진행", CreatedAt = 주문시각, UpdatedAt = 주문시각
            });
            foreach (var taskType in new[] { 피킹포장작업유형.피킹, 피킹포장작업유형.포장 })
            {
                var done = taskType == 피킹포장작업유형.피킹 || packed;
                db.피킹포장작업.Add(new 피킹포장작업
                {
                    작업Key = $"sample:{taskType}:{index}", 라인Key = $"sample-line:{index}", 작업유형 = taskType,
                    상태 = done ? 피킹포장작업상태.완료 : 피킹포장작업상태.진행중,
                    출고예정Id = outbound.Id, 입고상품Id = item.Id, 창고Id = warehouse.Id,
                    상품명 = item.상품명, SKU = item.SKU, 수량 = index, 주문참조번호 = 주문참조,
                    작업자UserId = "worker-sample", 시작일시Utc = 주문시각.AddMinutes(1),
                    완료일시Utc = done ? 주문시각.AddMinutes(5) : null, CreatedAt = 주문시각, UpdatedAt = 주문시각
                });
            }
            if (packed)
                db.재고이력.Add(new 재고이력
                {
                    입고상품Id = item.Id, 이력유형 = "포장", 원인유형 = "포장작업", 변경수량 = 0,
                    변경후수량 = index, 처리UserId = "worker-sample", 처리일시 = 주문시각.AddMinutes(5)
                });
            await db.SaveChangesAsync();
        }
    }

    private static async Task<string> 원천상태Async(SsalddelContext db)
        => JsonSerializer.Serialize(new
        {
            orders = await db.마트주문.AsNoTracking().OrderBy(item => item.Id)
                .Select(item => new { item.Id, item.상태, item.현재단계, item.UpdatedAt }).ToListAsync(),
            lines = await db.마트주문상품.AsNoTracking().OrderBy(item => item.Id).ToListAsync(),
            outbounds = await db.출고예정.AsNoTracking().OrderBy(item => item.Id).ToListAsync(),
            inventory = await db.입고상품.AsNoTracking().OrderBy(item => item.Id).ToListAsync(),
            tasks = await db.피킹포장작업.AsNoTracking().OrderBy(item => item.Id).ToListAsync(),
            history = await db.재고이력.AsNoTracking().OrderBy(item => item.Id).ToListAsync()
        });

    private static async Task AssertNoDispatchAsync(SsalddelContext db, 기록경계 boundaries)
    {
        Assert.Empty(await db.운송원장.ToListAsync());
        Assert.Empty(await db.운영체제업무인계.ToListAsync());
        Assert.Empty(await db.운영체제업무인계Outbox.ToListAsync());
        Assert.Equal(0, boundaries.ZoneCalls);
        Assert.Equal(0, boundaries.MongoCalls);
        Assert.Empty(boundaries.OutboxKeys);
        Assert.Empty(boundaries.FoodUpserts);
        Assert.Empty(boundaries.CargoUpserts);
        Assert.False(db.ChangeTracker.HasChanges());
    }

    private sealed class 격리DbFixture(SqliteConnection connection, SsalddelContext db) : IAsyncDisposable
    {
        public SsalddelContext Db { get; } = db;
        public static async Task<격리DbFixture> 생성Async()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new SsalddelContext(new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(connection).Options, new 통과암호화());
            await db.Database.EnsureCreatedAsync();
            return new 격리DbFixture(connection, db);
        }
        public SsalddelContext 새Context()
            => new(new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(connection).Options, new 통과암호화());
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await connection.DisposeAsync(); }
    }

    private sealed class 통과암호화 : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }

    private sealed class 고정시계 : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(판정시각);
    }

    private sealed class 고정창고담당자 : ICurrentUserAccessor
    {
        public string? UserId => "seller-sample";
        public string? Role => 역할명.창고관리자;
    }

    private sealed class 기록행위로그 : I사용자행위로그Service
    {
        public List<사용자행위로그기록> Entries { get; } = [];
        public Task 기록Async(사용자행위로그기록 entry, CancellationToken cancellationToken = default)
        { Entries.Add(entry); return Task.CompletedTask; }
    }

    private sealed class 실제HandlerPublisher(알뜰살뜰마트포장완료배차대기EventHandler handler, List<INotification> events) : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default)
        {
            if (notification is not INotification typed) throw new ArgumentException("시험 Event가 아닙니다.", nameof(notification));
            events.Add(typed);
            return typed switch
            {
                창고포장완료됨Event completed => handler.Handle(completed, cancellationToken),
                창고포장배차재확인요청됨Event recheck => handler.Handle(recheck, cancellationToken),
                _ => throw new InvalidOperationException("시험 범위 밖 Event입니다.")
            };
        }
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification
            => Publish((object)notification, cancellationToken);
    }

    // 제품 readiness는 이 fixture로 교체하지 않습니다. 이 시험만 실제 큐·인계 저장 연결을 열어 봅니다.
    private sealed class 시험전용ReadyPolicy : IMartLastMileDispatchReadinessPolicy
    {
        public Task<MartLastMileDispatchReadiness> EvaluateAsync(string orderReference, CancellationToken cancellationToken)
            => Task.FromResult(new MartLastMileDispatchReadiness(true, "TestConsumerReady", string.Empty));
    }

    private sealed class 시험기능관문(bool enabled) : IVersionFeatureFlagService
    {
        public bool IsEnabled(string featureKey) => enabled;
        public IReadOnlyDictionary<string, bool> GetAll() => new Dictionary<string, bool>();
    }

    // 외부 Mongo와 Redis의 대용입니다. 호출 의도만 기록하며 실제 네트워크나 운영 원장을 만들지 않습니다.
    private sealed class 기록경계 : I운송원장배달권연결Service, I음식배달권실행공간Store,
        I국내화물배달권실행공간Store, I운송원장Mongo동기화Service, I음식마트원장동기화OutboxService
    {
        public int ZoneCalls { get; private set; }
        public int MongoCalls { get; private set; }
        public bool 다음Mongo실패 { get; set; }
        public List<string> FoodUpserts { get; } = [];
        public List<string> CargoUpserts { get; } = [];
        public List<string> OutboxKeys { get; } = [];
        public Task<운송원장배달권연결결과> 투영추적Async(운송원장 queue, CancellationToken cancellationToken = default)
        {
            ZoneCalls++;
            var zone = new 배달권판정결과("sample-zone", "격리 표본", "TestAdapter");
            return Task.FromResult(new 운송원장배달권연결결과(zone, zone));
        }
        public Task Upsert기사Async(string zone, string driver, IReadOnlyList<string> adjacent, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
        public Task Remove기사Async(string driver, CancellationToken cancellationToken = default) => Task.CompletedTask;
        Task I음식배달권실행공간Store.Upsert운송의뢰Async(string zone, string id, IReadOnlyList<string> adjacent, CancellationToken cancellationToken)
        { FoodUpserts.Add(id); return Task.CompletedTask; }
        Task I국내화물배달권실행공간Store.Upsert운송의뢰Async(string zone, string id, IReadOnlyList<string> adjacent, CancellationToken cancellationToken)
        { CargoUpserts.Add(id); return Task.CompletedTask; }
        public Task Remove운송의뢰Async(string id, CancellationToken cancellationToken = default) => Task.CompletedTask;
        Task<음식배달권실행공간Snapshot?> I음식배달권실행공간Store.GetAsync(string zone, CancellationToken cancellationToken)
            => Task.FromResult<음식배달권실행공간Snapshot?>(null);
        Task<국내화물배달권실행공간Snapshot?> I국내화물배달권실행공간Store.GetAsync(string zone, CancellationToken cancellationToken)
            => Task.FromResult<국내화물배달권실행공간Snapshot?>(null);
        Task<IReadOnlyList<음식배달권실행공간Snapshot>> I음식배달권실행공간Store.SnapshotAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<음식배달권실행공간Snapshot>>([]);
        Task<IReadOnlyList<국내화물배달권실행공간Snapshot>> I국내화물배달권실행공간Store.SnapshotAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<국내화물배달권실행공간Snapshot>>([]);
        public Task<커뮤니티원장Dto?> 화주운송의뢰동기화Async(화주운송의뢰 request, string updatedBy, CancellationToken cancellationToken = default)
            => Task.FromResult<커뮤니티원장Dto?>(null);
        public Task<커뮤니티원장Dto?> 운송실행투영동기화Async(운송원장 queue, string updatedBy, CancellationToken cancellationToken = default)
        {
            MongoCalls++;
            if (다음Mongo실패) { 다음Mongo실패 = false; throw new InvalidOperationException("Test Mongo boundary unavailable"); }
            return Task.FromResult<커뮤니티원장Dto?>(null);
        }
        public Task<운송원장Mongo동기화상태> 상태조회Async(string id, CancellationToken cancellationToken = default)
            => Task.FromResult(운송원장Mongo동기화상태.Empty(id, "TestAdapter"));
        public Task 음식주문예약후즉시처리Async(Ssalddel.Contracts.Food.음식주문응답 order, string updatedBy, string idempotencyKey, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
        public Task 출고원장예약후즉시처리Async(IReadOnlyList<출고예정> outbounds, IReadOnlyList<입고요청> inbounds,
            string updatedBy, string idempotencyKey, string? currentStageKey = null, string? ledgerTemplateKey = null, CancellationToken cancellationToken = default)
        { OutboxKeys.Add(idempotencyKey); return Task.CompletedTask; }
        public Task<int> 대기항목처리Async(int take = 100, CancellationToken cancellationToken = default) => Task.FromResult(0);
    }
}
