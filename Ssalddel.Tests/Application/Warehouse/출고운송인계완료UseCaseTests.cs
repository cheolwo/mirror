using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Application.Driver.Transport;
using Ssalddel.Application.Warehouse;
using Ssalddel.Application.Warehouse.Events;
using Ssalddel.Contracts.Common.Inventory;
using Ssalddel.Contracts.Food;
using Ssalddel.Services.Community;
using Ssalddel.Services.Operations;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Audit;
using 살뜰.도메인.기사;
using 살뜰.도메인.운송;
using 살뜰.도메인.화주;
using 살뜰.도메인.창고;

namespace Ssalddel.Tests.Application.Warehouse;

public sealed class 출고운송인계완료UseCaseTests
{
    [Fact]
    public async Task 기사도착후_창고인계가선행되면_상차완료되고재출고하지않는다()
    {
        await using var db = CreateContext();
        var planId = await SeedAsync(db);
        var transport = await db.운송원장.SingleAsync();
        transport.기사_운송자 = "driver-7";
        transport.운송번호 = "display-transport-31";
        var shipperRequest = await db.화주운송의뢰.SingleAsync();
        shipperRequest.증빙방식 = "인수증";
        shipperRequest.정산메모 = "서명 필수";
        await db.SaveChangesAsync();
        var executor = CreateDriverExecutor(db);

        var arrived = await new 운송상차지도착CommandHandler(executor)
            .Handle(new 운송상차지도착Command("driver-7", transport.Id), CancellationToken.None);
        Assert.True(arrived.IsSuccess);
        Assert.Equal("상차지도착", (await db.화주운송의뢰.SingleAsync()).배차상태);

        var warehouse = CreateUseCase(db);
        var released = await warehouse.완료Async(planId, Request(), RequestContext(), CancellationToken.None);
        Assert.True(released.IsSuccess);
        var unsigned = await new 운송상차완료CommandHandler(db, executor, new 운송증빙첨부JsonWriter())
            .Handle(new 운송상차완료Command("driver-7", transport.Id)
            { 상차사진ObjectName = "private/test/pickup.jpg" }, CancellationToken.None);
        Assert.True(unsigned.IsFailed);
        Assert.Contains("서명", unsigned.Errors.Single().Message, StringComparison.Ordinal);
        Assert.Equal("상차지도착", transport.상태);
        var pickup = await new 운송상차완료CommandHandler(db, executor, new 운송증빙첨부JsonWriter())
            .Handle(new 운송상차완료Command("driver-7", transport.Id)
            {
                상차사진ObjectName = "private/test/pickup.jpg", 인수증확인완료 = true,
                인수자명 = "인수자", 인수자서명 = "test-recipient-signature", 기사서명 = "test-driver-signature"
            }, CancellationToken.None);
        Assert.True(pickup.IsSuccess);
        Assert.Equal("상차완료", (await db.운송원장.SingleAsync()).상태);
        Assert.Equal("상차완료", (await db.화주운송의뢰.SingleAsync()).배차상태);

        var replay = await warehouse.완료Async(planId, Request(), RequestContext(), CancellationToken.None);
        Assert.True(replay.IsSuccess);
        Assert.True(replay.Value.IdempotentReplay);
        Assert.Equal(0, (await db.입고상품.SingleAsync()).예약수량);
        Assert.Single(await db.재고이동.Where(x => x.이동유형 == 재고이동유형.출고).ToArrayAsync());
        Assert.Single(await db.재고이력.Where(x => x.이력유형 == 재고이동유형.출고).ToArrayAsync());
    }

    [Fact]
    public async Task 창고인계전의_기사상차완료는409이고_사진상태재고를변경하지않는다()
    {
        await using var db = CreateContext();
        await SeedAsync(db, dispatchStatus: "상차지도착");
        var transport = await db.운송원장.SingleAsync();
        transport.기사_운송자 = "driver-7";
        transport.상태 = "상차지도착";
        await db.SaveChangesAsync();

        var result = await new 운송상차완료CommandHandler(db, CreateDriverExecutor(db), new 운송증빙첨부JsonWriter())
            .Handle(new 운송상차완료Command("driver-7", transport.Id)
            { 상차사진ObjectName = "private/test/pickup.jpg" }, CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(409, result.Errors.Single().Metadata["StatusCode"]);
        Assert.Contains("창고의 상품 인계 완료", result.Errors.Single().Message, StringComparison.Ordinal);
        Assert.Equal("상차지도착", transport.상태);
        Assert.Equal("상차지도착", (await db.화주운송의뢰.SingleAsync()).배차상태);
        Assert.Equal(출고상태.준비중, (await db.출고예정.SingleAsync()).상태);
        Assert.Equal(9, (await db.입고상품.SingleAsync()).예약수량);
        Assert.Equal("[]", transport.첨부_json);
        Assert.Empty(db.운송이벤트);
        Assert.Empty(db.재고이동);
    }

    [Fact]
    public async Task 창고출고가없는_일반화주의_기사상차완료흐름은유지한다()
    {
        await using var db = CreateContext();
        await SeedAsync(db, dispatchStatus: "상차지도착");
        db.출고예정.Remove(await db.출고예정.SingleAsync());
        var transport = await db.운송원장.SingleAsync();
        transport.기사_운송자 = "driver-7";
        transport.상태 = "상차지도착";
        await db.SaveChangesAsync();

        var result = await new 운송상차완료CommandHandler(db, CreateDriverExecutor(db), new 운송증빙첨부JsonWriter())
            .Handle(new 운송상차완료Command("driver-7", transport.Id)
            { 상차사진ObjectName = "private/test/pickup.jpg" }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("상차완료", transport.상태);
    }

    [Theory]
    [InlineData("상차완료")]
    [InlineData("하차지도착")]
    [InlineData("인수완료")]
    public async Task 이미상차이후의_미완료창고출고는_뒤늦게인계확정하지않는다(string state)
    {
        await using var db = CreateContext();
        var id = await SeedAsync(db, dispatchStatus: state);
        var result = await CreateUseCase(db).완료Async(id, Request(), RequestContext(), CancellationToken.None);
        Assert.True(result.IsFailed);
        Assert.Equal(9, (await db.입고상품.SingleAsync()).예약수량);
        Assert.Empty(db.재고이동);
    }

    [Fact]
    public async Task 기사수락전에는_예약재고와출고원장을변경하지않는다()
    {
        await using var db = CreateContext();
        var planId = await SeedAsync(db, dispatchStatus: "매칭중");
        var useCase = CreateUseCase(db);

        var result = await useCase.완료Async(
            planId,
            Request(),
            RequestContext(),
            CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(409, result.Errors.Single().Metadata["StatusCode"]);
        Assert.Equal(출고상태.준비중, (await db.출고예정.SingleAsync()).상태);
        Assert.Equal(9, (await db.입고상품.SingleAsync()).예약수량);
        Assert.Empty(db.재고이동.Where(x => x.이동유형 == 재고이동유형.출고));
    }

    [Fact]
    public async Task 요청차량과기사등록차량이다르면_출고를차단한다()
    {
        await using var db = CreateContext();
        var planId = await SeedAsync(db);
        var transportRequest = await db.화주운송의뢰.SingleAsync();
        transportRequest.차량종류 = "1톤 카고";
        await db.SaveChangesAsync();

        var result = await CreateUseCase(db).완료Async(
            planId,
            Request(),
            RequestContext(),
            CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(409, result.Errors.Single().Metadata["StatusCode"]);
        Assert.Contains("일치하지 않습니다", result.Errors.Single().Message);
        Assert.Equal(출고상태.준비중, (await db.출고예정.SingleAsync()).상태);
        Assert.Equal(9, (await db.입고상품.SingleAsync()).예약수량);
    }

    [Fact]
    public async Task 기사와등록차량확인후_예약재고를한번만출고하고_같은의뢰Id를유지한다()
    {
        await using var db = CreateContext();
        var planId = await SeedAsync(db);
        var logs = new RecordingLog();
        var outbox = new RecordingOutbox();
        var publisher = new RecordingPublisher();
        var useCase = CreateUseCase(db, logs, outbox, publisher);

        var first = await useCase.완료Async(
            planId,
            Request(),
            RequestContext(),
            CancellationToken.None);
        var replay = await useCase.완료Async(
            planId,
            Request(),
            RequestContext(),
            CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.False(first.Value.IdempotentReplay);
        Assert.Equal("warehouse-outbound-31", first.Value.TransportRequestId);
        Assert.Equal("driver-7", first.Value.AssignedDriverId);
        Assert.Equal("1톤 냉장탑차", first.Value.AssignedDriverVehicle);
        Assert.True(replay.IsSuccess);
        Assert.True(replay.Value.IdempotentReplay);

        var plan = await db.출고예정.SingleAsync();
        var inventory = await db.입고상품.SingleAsync();
        Assert.Equal(출고상태.출고완료, plan.상태);
        Assert.NotNull(plan.출고처리일시);
        Assert.Equal(0, inventory.예약수량);
        Assert.Equal(출고상태.출고완료, inventory.상태);
        Assert.Single(await db.재고이동.Where(x => x.이동유형 == 재고이동유형.출고).ToArrayAsync());
        Assert.Single(await db.재고이력.Where(x => x.이력유형 == 재고이동유형.출고).ToArrayAsync());
        Assert.Single(logs.Entries);
        Assert.IsType<창고출고운송인계완료됨Event>(Assert.Single(publisher.Notifications));
        Assert.Equal(2, outbox.Calls.Count);
        Assert.All(outbox.Calls, call => Assert.Equal("warehouse-transport-handoff:31", call.IdempotencyKey));
        var osHandoff = await db.운영체제업무인계.SingleAsync();
        Assert.Equal("WarehouseCommerceFulfillmentOS", osHandoff.출발운영체제Id);
        Assert.Equal("DomesticCargoTransportOS", osHandoff.도착운영체제Id);
        Assert.Equal("Accepted", osHandoff.상태Code);
        Assert.Equal("cargo-request:warehouse-outbound-31", osHandoff.도착업무StableId);
        Assert.Equal(2, await db.운영체제업무인계Outbox.CountAsync());
    }

    [Fact]
    public async Task 관계형인계저장중일시실패는_재시도해도재고이력과OS인계를한번만저장한다()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<SsalddelContext>()
            .UseSqlite(connection)
            .ReplaceService<IExecutionStrategyFactory, RetryStrategyFactory>()
            .Options;
        await using var db = new SsalddelContext(options, new DummyEncryption());
        await db.Database.EnsureCreatedAsync();
        var planId = await SeedAsync(db);
        var logs = new RecordingLog();
        var outbox = new RecordingOutbox();
        var publisher = new RecordingPublisher();
        var handoff = new FailOnceHandoff(new 출고화물운송운영체제인계Service(
            new 살뜰.Services.Operations.운영체제업무인계Coordinator(db, TimeProvider.System)));

        var first = await CreateUseCase(db, logs, outbox, publisher, handoff)
            .완료Async(planId, Request(), RequestContext(), CancellationToken.None);
        await using var read = new SsalddelContext(options, new DummyEncryption());
        var replay = await CreateUseCase(read, logs, outbox, publisher)
            .완료Async(planId, Request(), RequestContext(), CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.False(first.Value.IdempotentReplay);
        Assert.True(replay.IsSuccess);
        Assert.True(replay.Value.IdempotentReplay);
        Assert.Equal(first.Value.HandoffCompletedAtUtc, replay.Value.HandoffCompletedAtUtc);
        Assert.Equal(2, handoff.Calls);
        Assert.Equal(0, (await read.입고상품.AsNoTracking().SingleAsync()).예약수량);
        Assert.Equal(출고상태.출고완료, (await read.출고예정.AsNoTracking().SingleAsync()).상태);
        Assert.Single(await read.재고이력.ToArrayAsync());
        Assert.Single(await read.재고이동.ToArrayAsync());
        Assert.Single(await read.운영체제업무인계.ToArrayAsync());
        Assert.Equal(2, await read.운영체제업무인계Outbox.CountAsync());
        Assert.Single(logs.Entries);
        Assert.Single(publisher.Notifications);
        Assert.Equal(2, outbox.Calls.Count);
    }

    [Fact]
    public async Task 관계형인계의OS저장실패는_출고상태와SQL재고차감및이력을함께롤백한다()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(connection).Options;
        await using var db = new SsalddelContext(options, new DummyEncryption());
        await db.Database.EnsureCreatedAsync();
        var planId = await SeedAsync(db);
        var logs = new RecordingLog();
        var outbox = new RecordingOutbox();
        var publisher = new RecordingPublisher();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateUseCase(db, logs, outbox, publisher, new ThrowingHandoff())
                .완료Async(planId, Request(), RequestContext(), CancellationToken.None));

        await using var read = new SsalddelContext(options, new DummyEncryption());
        Assert.Equal(출고상태.준비중, (await read.출고예정.SingleAsync()).상태);
        Assert.Equal(9, (await read.입고상품.SingleAsync()).예약수량);
        Assert.Empty(await read.재고이력.ToArrayAsync());
        Assert.Empty(await read.재고이동.ToArrayAsync());
        Assert.Empty(await read.운영체제업무인계.ToArrayAsync());
        Assert.Empty(logs.Entries);
        Assert.Empty(publisher.Notifications);
        Assert.Empty(outbox.Calls);
    }

    private static 기사운송상태변경CommandExecutor CreateDriverExecutor(SsalddelContext db)
        => new(db, new 기사운송상태전이Service(), new RecordingPublisher(),
            new FakeCurrentUserAccessor("driver-7", 역할명.기사), new 참여자실행권한검사(),
            NullLogger<기사운송상태변경CommandExecutor>.Instance);

    private static 출고운송인계완료요청 Request()
        => new()
        {
            DriverIdentityConfirmed = true,
            VehicleConfirmed = true,
            CargoReleasedConfirmed = true,
            Memo = "봉인 상태 확인"
        };

    private static 출고운송인계완료UseCase CreateUseCase(
        SsalddelContext db,
        RecordingLog? logs = null,
        RecordingOutbox? outbox = null,
        RecordingPublisher? publisher = null,
        I출고화물운송운영체제인계Service? operatingHandoff = null)
        => new(
            db,
            new FakeCurrentUserAccessor("worker-a", 역할명.창고관리자),
            logs ?? new RecordingLog(),
            outbox ?? new RecordingOutbox(),
            operatingHandoff ?? new 출고화물운송운영체제인계Service(
                new 살뜰.Services.Operations.운영체제업무인계Coordinator(db, TimeProvider.System)),
            publisher ?? new RecordingPublisher());

    private static 창고작업요청Context RequestContext()
        => new(
            "WarehouseManagerApp",
            "worker-a",
            "출고 작업자",
            역할명.창고관리자,
            "/api/v1/warehouse-operations/outbound-plan-reviews/31/handoff-complete",
            "trace-handoff-complete",
            "127.0.0.1",
            "test");

    private static SsalddelContext CreateContext()
        => new(
            new DbContextOptionsBuilder<SsalddelContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options,
            new DummyEncryption());

    private static async Task<long> SeedAsync(
        SsalddelContext db,
        string dispatchStatus = "배차확정")
    {
        var now = new DateTime(2026, 7, 21, 1, 0, 0, DateTimeKind.Utc);
        var warehouse = new 창고
        {
            소유자UserId = "owner-a",
            창고명 = "공동 창고 A",
            주소 = "서울특별시 송파구 테스트로 1",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.창고.Add(warehouse);
        await db.SaveChangesAsync();
        db.창고사용자.Add(new 창고사용자
        {
            창고Id = warehouse.Id,
            UserId = "worker-a",
            역할명 = "출고",
            CreatedAt = now,
            UpdatedAt = now
        });
        var inbound = new 입고요청
        {
            창고Id = warehouse.Id,
            주문참조번호 = "ORDER-31",
            판매자UserId = "seller-a",
            주문자UserId = "buyer-a",
            CreatedAt = now,
            UpdatedAt = now
        };
        db.입고요청.Add(inbound);
        await db.SaveChangesAsync();
        var inventory = new 입고상품
        {
            입고요청Id = inbound.Id,
            창고Id = warehouse.Id,
            상품명 = "냉장 감자",
            SKU = "POTATO",
            입고수량 = 9,
            가용수량 = 0,
            예약수량 = 9,
            보관위치 = "A-02",
            상태 = "재위탁대기",
            CreatedAt = now,
            UpdatedAt = now
        };
        db.입고상품.Add(inventory);
        await db.SaveChangesAsync();
        var plan = new 출고예정
        {
            Id = 31,
            입고상품Id = inventory.Id,
            입고요청Id = inbound.Id,
            출고창고Id = warehouse.Id,
            상품명 = inventory.상품명,
            SKU = inventory.SKU,
            주문참조번호 = inbound.주문참조번호,
            수량 = 9,
            상태 = 출고상태.준비중,
            운송의뢰Id = "warehouse-outbound-31",
            CreatedAt = now,
            UpdatedAt = now
        };
        db.출고예정.Add(plan);
        db.화주운송의뢰.Add(new 화주운송의뢰
        {
            의뢰Id = plan.운송의뢰Id,
            배차상태 = dispatchStatus,
            상태 = "생성됨",
            차량종류 = "1톤 냉장탑차",
            CreatedAt = now,
            UpdatedAt = now
        });
        db.운송원장.Add(new 운송원장
        {
            운송번호 = plan.운송의뢰Id,
            의뢰Id = plan.운송의뢰Id,
            상태 = "확정",
            확정기사Id = "driver-7",
            CreatedAt = now,
            UpdatedAt = now
        });
        db.용달기사.Add(new 용달기사
        {
            기사Id = "driver-7",
            기사명 = "테스트 기사",
            차량 = "1톤 냉장탑차",
            CreatedAt = now,
            UpdatedAt = now
        });
        db.운송의뢰상품연결.Add(new 운송의뢰상품연결
        {
            운송의뢰Id = plan.운송의뢰Id,
            입고상품Id = inventory.Id,
            할당수량 = 9,
            CreatedAt = now
        });
        await db.SaveChangesAsync();
        return plan.Id;
    }

    private sealed class FailOnceHandoff(I출고화물운송운영체제인계Service inner) : I출고화물운송운영체제인계Service
    {
        public int Calls { get; private set; }

        public Task<Ssalddel.Contracts.Common.Operations.운영체제업무인계Dto> 인계Async(
            출고예정 plan,
            CancellationToken cancellationToken)
        {
            Calls++;
            return Calls == 1
                ? Task.FromException<Ssalddel.Contracts.Common.Operations.운영체제업무인계Dto>(new RetryableWriteException())
                : inner.인계Async(plan, cancellationToken);
        }
    }

    private sealed class ThrowingHandoff : I출고화물운송운영체제인계Service
    {
        public Task<Ssalddel.Contracts.Common.Operations.운영체제업무인계Dto> 인계Async(
            출고예정 plan,
            CancellationToken cancellationToken)
            => Task.FromException<Ssalddel.Contracts.Common.Operations.운영체제업무인계Dto>(
                new InvalidOperationException("Synthetic handoff failure"));
    }

    private sealed class RetryableWriteException : Exception { }

    private sealed class RetryStrategyFactory : IExecutionStrategyFactory
    {
        private readonly ExecutionStrategyDependencies _dependencies;

        public RetryStrategyFactory(ExecutionStrategyDependencies dependencies) => _dependencies = dependencies;

        public IExecutionStrategy Create() => new RetryStrategy(_dependencies);
    }

    private sealed class RetryStrategy(ExecutionStrategyDependencies dependencies)
        : ExecutionStrategy(dependencies, 1, TimeSpan.Zero)
    {
        protected override bool ShouldRetryOn(Exception exception) => exception is RetryableWriteException;
    }

    private sealed class FakeCurrentUserAccessor(string? userId, string? role) : ICurrentUserAccessor
    {
        public string? UserId { get; } = userId;
        public string? Role { get; } = role;
    }

    private sealed class DummyEncryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }

    private sealed class RecordingLog : I사용자행위로그Service
    {
        public List<사용자행위로그기록> Entries { get; } = [];

        public Task 기록Async(
            사용자행위로그기록 entry,
            CancellationToken cancellationToken = default)
        {
            Entries.Add(entry);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingOutbox : I음식마트원장동기화OutboxService
    {
        public List<(string IdempotencyKey, string? Stage)> Calls { get; } = [];

        public Task 음식주문예약후즉시처리Async(
            음식주문응답 order,
            string updatedBy,
            string idempotencyKey,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task 출고원장예약후즉시처리Async(
            IReadOnlyList<출고예정> outbounds,
            IReadOnlyList<입고요청> inbounds,
            string updatedBy,
            string idempotencyKey,
            string? currentStageKey = null,
            string? ledgerTemplateKey = null,
            CancellationToken cancellationToken = default)
        {
            Calls.Add((idempotencyKey, currentStageKey));
            return Task.CompletedTask;
        }

        public Task<int> 대기항목처리Async(
            int take = 100,
            CancellationToken cancellationToken = default)
            => Task.FromResult(0);
    }

    private sealed class RecordingPublisher : IPublisher
    {
        public List<object> Notifications { get; } = [];

        public Task Publish(
            object notification,
            CancellationToken cancellationToken = default)
        {
            Notifications.Add(notification);
            return Task.CompletedTask;
        }

        public Task Publish<TNotification>(
            TNotification notification,
            CancellationToken cancellationToken = default)
            where TNotification : INotification
        {
            Notifications.Add(notification);
            return Task.CompletedTask;
        }
    }
}
