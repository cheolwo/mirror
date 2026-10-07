using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Operations;
using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Services.Community;
using Ssalddel.Services.LogisticsProcessing.Warehouse;
using Ssalddel.Services.Operations;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Dispatch.Engine;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.Services.Versioning;
using 살뜰.도메인.공통;
using 살뜰.도메인.마트;
using 살뜰.도메인.사용자;
using 살뜰.도메인.운송;
using 살뜰.도메인.운영;
using 살뜰.도메인.화주;
using 살뜰.도메인.창고;

namespace Ssalddel.Tests.Services.LogisticsProcessing.Warehouse;

public sealed class 알뜰살뜰마트배차대기ServiceTests
{
    [Fact]
    public async Task 소비자연결이준비된배차Fixture는_실제창고및주문자주소를사용한다()
    {
        await using var db = CreateContext();
        await SeedAsync(db, "서울 성동구 배송로 2");
        var queue = new RecordingDispatchQueue();
        var service = CreateService(db, queue);

        var result = await service.주문포장완료후배차대기생성Async(
            "MART-001",
            "worker-a");

        Assert.True(result.생성또는조회됨);
        Assert.Equal(출고예정운송대상원천유형.살뜰마트주문, queue.Target?.원천유형);
        Assert.Equal("서울 성동구 창고로 1", queue.Target?.상차주소);
        Assert.Equal("서울 성동구 배송로 2", queue.Target?.하차주소);
        Assert.Equal(
            운송의뢰배차원천유형.살뜰마트포장완료주문,
            queue.Options?.원본의뢰유형);
    }

    [Fact]
    public async Task 배송목적지가없으면_포장완료를유지하고배차생성을보류한다()
    {
        await using var db = CreateContext();
        await SeedAsync(db, string.Empty);
        var queue = new RecordingDispatchQueue();
        var service = CreateService(db, queue);

        var result = await service.주문포장완료후배차대기생성Async(
            "MART-001",
            "worker-a");

        Assert.False(result.생성또는조회됨);
        Assert.True(result.포장완료);
        Assert.Equal(알뜰살뜰마트배차대기결과코드.배송목적지없음, result.결과코드);
        Assert.Null(queue.Target);
        Assert.Empty(db.운송원장);
    }

    [Theory]
    [InlineData("mart-disabled", MartLastMileDispatchReadinessCodes.FeatureDisabled)]
    [InlineData("food-disabled", MartLastMileDispatchReadinessCodes.FeatureDisabled)]
    [InlineData("warehouse-disabled", MartLastMileDispatchReadinessCodes.FeatureDisabled)]
    [InlineData("general-outbound", MartLastMileDispatchReadinessCodes.SourceNotBound)]
    [InlineData("mart-source", MartLastMileDispatchReadinessCodes.ConsumerBindingUnavailable)]
    public async Task 포장완료중복이벤트는_창고결과를유지하고연결미지원기사인계를생성하지않는다(
        string scenario,
        string expectedCode)
    {
        await using var db = CreateContext();
        await SeedAsync(db, "서울 성동구 배송로 2");
        var initial = new DateTime(2026, 7, 28, 1, 0, 0, DateTimeKind.Utc);
        var outbound = await db.출고예정.SingleAsync();
        outbound.입고상품Id = 77;
        db.입고상품.Add(new 입고상품
        {
            Id = 77,
            창고Id = outbound.출고창고Id,
            소유자UserId = "seller-a",
            판매자UserId = "seller-a",
            상품명 = "쌀",
            SKU = "RICE-10KG",
            입고수량 = 1,
            가용수량 = 1,
            상태 = "포장완료 - 일반포장",
            CreatedAt = initial,
            UpdatedAt = initial
        });
        db.피킹포장작업.Add(new 피킹포장작업
        {
            작업Key = "pack:MART-001:77",
            작업유형 = 피킹포장작업유형.포장,
            상태 = 피킹포장작업상태.진행중,
            입고상품Id = 77,
            주문참조번호 = "MART-001",
            작업자UserId = "worker-a",
            수량 = 1,
            시작일시Utc = initial,
            CreatedAt = initial,
            UpdatedAt = initial
        });
        if (scenario != "general-outbound")
            db.마트주문.Add(new 마트주문
            {
                주문참조번호 = "MART-001",
                판매자UserId = "seller-a",
                주문자UserId = "orderer-a",
                상태 = "포장 완료",
                현재단계 = "포장",
                CreatedAt = initial,
                UpdatedAt = initial
            });
        await db.SaveChangesAsync();
        var outboundBefore = (outbound.상태, outbound.운송의뢰Id, outbound.UpdatedAt);
        var queue = new RecordingDispatchQueue();
        var handoff = new RecordingLastMileHandoff(queue);
        var sync = new NoOpTransportLedgerSync();
        var outbox = new NoOpFoodMartLedgerSyncOutbox();
        var clock = new MutableTimeProvider(new DateTimeOffset(initial.AddMinutes(5)));
        var service = new 알뜰살뜰마트배차대기Service(
            db, handoff, sync, outbox,
            NullLogger<알뜰살뜰마트배차대기Service>.Instance, clock,
            new MartLastMileDispatchReadinessPolicy(db, new ReadinessFeatures(scenario)));

        var result = Assert.Single(await service.입고상품포장완료반영Async(77, "worker-a"));
        var taskAfter = await db.피킹포장작업.AsNoTracking().SingleAsync();
        clock.UtcNow = clock.UtcNow.AddMinutes(10);
        var replay = Assert.Single(await service.입고상품포장완료반영Async(77, "worker-a"));
        var taskReplay = await db.피킹포장작업.AsNoTracking().SingleAsync();

        Assert.Equal(expectedCode, result.결과코드);
        Assert.Equal(expectedCode, replay.결과코드);
        Assert.False(result.생성또는조회됨);
        Assert.False(replay.생성또는조회됨);
        Assert.True(result.포장완료);
        Assert.True(replay.포장완료);
        Assert.Null(result.배차대기Id);
        Assert.Equal(string.Empty, result.인계StableId);
        Assert.Equal(피킹포장작업상태.완료, taskAfter.상태);
        Assert.Equal(initial, taskAfter.시작일시Utc);
        Assert.Equal(initial.AddMinutes(5), taskAfter.완료일시Utc);
        Assert.Equal(taskAfter.상태, taskReplay.상태);
        Assert.Equal(taskAfter.시작일시Utc, taskReplay.시작일시Utc);
        Assert.Equal(taskAfter.완료일시Utc, taskReplay.완료일시Utc);
        Assert.Equal(taskAfter.UpdatedAt, taskReplay.UpdatedAt);
        var storedOutbound = await db.출고예정.AsNoTracking().SingleAsync();
        Assert.Equal(outboundBefore, (storedOutbound.상태, storedOutbound.운송의뢰Id, storedOutbound.UpdatedAt));
        var inventory = await db.입고상품.AsNoTracking().SingleAsync();
        Assert.Equal("포장완료 - 일반포장", inventory.상태);
        Assert.Equal(1, inventory.가용수량);
        Assert.Equal(initial, inventory.UpdatedAt);
        if (scenario != "general-outbound")
        {
            var order = await db.마트주문.AsNoTracking().SingleAsync();
            Assert.Equal("포장 완료", order.상태);
            Assert.Equal("포장", order.현재단계);
            Assert.Equal(initial, order.UpdatedAt);
        }
        Assert.Equal(0, queue.CallCount);
        Assert.Equal(0, handoff.CallCount);
        Assert.Equal(0, sync.CallCount);
        Assert.Equal(0, outbox.CallCount);
        Assert.Empty(await db.운송원장.ToListAsync());
        Assert.Empty(await db.운영체제업무인계.ToListAsync());
        Assert.Empty(await db.운영체제업무인계Outbox.ToListAsync());
        Assert.False(db.ChangeTracker.HasChanges());
    }

    private static 알뜰살뜰마트배차대기Service CreateService(
        SsalddelContext db,
        RecordingDispatchQueue queue)
        => new(
            db,
            new RecordingLastMileHandoff(queue),
            new NoOpTransportLedgerSync(),
            new NoOpFoodMartLedgerSyncOutbox(),
            NullLogger<알뜰살뜰마트배차대기Service>.Instance,
            new FixedTimeProvider(new DateTimeOffset(2026, 7, 28, 1, 5, 0, TimeSpan.Zero)),
            new ReadyReadinessPolicy());

    private static SsalddelContext CreateContext()
        => new(
            new DbContextOptionsBuilder<SsalddelContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options,
            new PassThroughEncryption());

    private static async Task SeedAsync(SsalddelContext db, string deliveryAddress)
    {
        var now = new DateTime(2026, 7, 28, 1, 0, 0, DateTimeKind.Utc);
        var warehouse = new 창고
        {
            소유자UserId = "seller-a",
            창고명 = "마트 출고 창고",
            주소 = "서울 성동구 창고로 1",
            CreatedAt = now,
            UpdatedAt = now
        };
        db.창고.Add(warehouse);
        db.주문자프로필.Add(new 주문자프로필
        {
            UserId = "orderer-a",
            표시명 = "주문자",
            기본주소 = deliveryAddress,
            CreatedAt = now,
            UpdatedAt = now
        });
        await db.SaveChangesAsync();

        db.출고예정.Add(new 출고예정
        {
            주문참조번호 = "MART-001",
            판매자UserId = "seller-a",
            주문자UserId = "orderer-a",
            출고창고Id = warehouse.Id,
            상품명 = "쌀",
            SKU = "RICE-10KG",
            수량 = 1,
            상태 = 출고상태.출고완료,
            CreatedAt = now,
            UpdatedAt = now
        });
        await db.SaveChangesAsync();
    }

    private sealed class RecordingDispatchQueue : I운송의뢰배차대기Service
    {
        public int CallCount { get; private set; }
        public 출고예정운송대상? Target { get; private set; }
        public 운송의뢰배차대기생성옵션? Options { get; private set; }

        public Task<운송원장> 생성또는조회Async(
            출고예정운송대상 target,
            운송의뢰배차대기생성옵션? options = null,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            Target = target;
            Options = options;
            return Task.FromResult(new 운송원장
            {
                Id = 91,
                의뢰Id = options?.의뢰Id ?? target.운송의뢰Id ?? target.원천참조번호,
                운송번호 = options?.의뢰Id ?? target.원천참조번호,
                화주Id = options?.화주Id ?? target.판매자UserId,
                원본의뢰유형 = options?.원본의뢰유형 ?? target.원천유형,
                원본의뢰Id = options?.원본의뢰Id ?? target.원천참조번호,
                배차업무유형 = options?.배차업무유형 ?? 상태값.배차업무유형.음식배달
            });
        }
    }

    private sealed class RecordingLastMileHandoff(RecordingDispatchQueue queue)
        : I살뜰마트라스트마일배차인계Service
    {
        public int CallCount { get; private set; }

        public async Task<살뜰마트라스트마일배차인계결과> 인계Async(
            살뜰마트라스트마일배차인계요청 요청,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            var dispatch = await queue.생성또는조회Async(
                요청.배차대상,
                new 운송의뢰배차대기생성옵션
                {
                    의뢰Id = 요청.주문참조번호,
                    화주Id = 요청.배차대상.판매자UserId,
                    배차업무유형 = 상태값.배차업무유형.음식배달,
                    원본의뢰유형 = 운송의뢰배차원천유형.살뜰마트포장완료주문,
                    원본의뢰Id = 요청.주문참조번호,
                    상태 = 상태값.배차대기상태.대기
                },
                cancellationToken);
            var policy = new 살뜰마트라스트마일배차Policy().Evaluate(요청.정책입력);
            return new 살뜰마트라스트마일배차인계결과(
                new 운영체제업무인계Dto
                {
                    인계StableId = "os-handoff:test",
                    상태Code = 운영체제업무인계상태Codes.수락됨
                },
                dispatch,
                policy);
        }
    }

    private sealed class NoOpTransportLedgerSync : I운송원장Mongo동기화Service
    {
        public int CallCount { get; private set; }

        public Task<커뮤니티원장Dto?> 화주운송의뢰동기화Async(
            화주운송의뢰 의뢰,
            string updatedBy,
            CancellationToken cancellationToken = default)
            => Task.FromResult<커뮤니티원장Dto?>(null);

        public Task<커뮤니티원장Dto?> 운송실행투영동기화Async(
            운송원장 운송실행투영,
            string updatedBy,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult<커뮤니티원장Dto?>(null);
        }

        public Task<운송원장Mongo동기화상태> 상태조회Async(
            string 의뢰Id,
            CancellationToken cancellationToken = default)
            => Task.FromResult(운송원장Mongo동기화상태.Empty(의뢰Id, "test"));
    }

    private sealed class NoOpFoodMartLedgerSyncOutbox : I음식마트원장동기화OutboxService
    {
        public int CallCount { get; private set; }

        public Task 음식주문예약후즉시처리Async(
            Ssalddel.Contracts.Food.음식주문응답 주문,
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
            CallCount++;
            return Task.CompletedTask;
        }

        public Task<int> 대기항목처리Async(
            int take = 100,
            CancellationToken cancellationToken = default)
            => Task.FromResult(0);
    }

    // 주소 및 큐 조율의 기존 정책 시험용 fixture이며 제품 consumer 준비 여부는 별도 관문이 확인합니다.
    private sealed class ReadyReadinessPolicy : IMartLastMileDispatchReadinessPolicy
    {
        public Task<MartLastMileDispatchReadiness> EvaluateAsync(string orderReference, CancellationToken cancellationToken)
            => Task.FromResult(new MartLastMileDispatchReadiness(true, "TestConsumerReady", string.Empty));
    }

    private sealed class ReadinessFeatures(string scenario) : IVersionFeatureFlagService
    {
        public bool IsEnabled(string featureKey)
            => !(scenario == "mart-disabled" && featureKey == VersionFeatureFlagKeys.SsalddelMartWorkflow)
               && !(scenario == "food-disabled" && featureKey == VersionFeatureFlagKeys.FoodDeliveryWorkflow)
               && !(scenario == "warehouse-disabled" && featureKey == VersionFeatureFlagKeys.WarehouseFulfillmentWorkflow);

        public IReadOnlyDictionary<string, bool> GetAll()
            => new Dictionary<string, bool>
            {
                [VersionFeatureFlagKeys.SsalddelMartWorkflow] = IsEnabled(VersionFeatureFlagKeys.SsalddelMartWorkflow),
                [VersionFeatureFlagKeys.FoodDeliveryWorkflow] = IsEnabled(VersionFeatureFlagKeys.FoodDeliveryWorkflow),
                [VersionFeatureFlagKeys.WarehouseFulfillmentWorkflow] = IsEnabled(VersionFeatureFlagKeys.WarehouseFulfillmentWorkflow)
            };
    }

    private sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;
        public override DateTimeOffset GetUtcNow() => UtcNow;
    }

    private sealed class PassThroughEncryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
