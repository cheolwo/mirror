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
using 살뜰.도메인.공통;
using 살뜰.도메인.사용자;
using 살뜰.도메인.운송;
using 살뜰.도메인.운영;
using 살뜰.도메인.화주;
using 살뜰.도메인.창고;

namespace Ssalddel.Tests.Services.LogisticsProcessing.Warehouse;

public sealed class 알뜰살뜰마트배차대기ServiceTests
{
    [Fact]
    public async Task 포장완료는_마트원천과실제창고및주문자주소로배차대기를만든다()
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

    private static 알뜰살뜰마트배차대기Service CreateService(
        SsalddelContext db,
        RecordingDispatchQueue queue)
        => new(
            db,
            new RecordingLastMileHandoff(queue),
            new NoOpTransportLedgerSync(),
            new NoOpFoodMartLedgerSyncOutbox(),
            NullLogger<알뜰살뜰마트배차대기Service>.Instance,
            new FixedTimeProvider(new DateTimeOffset(2026, 7, 28, 1, 5, 0, TimeSpan.Zero)));

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
        public 출고예정운송대상? Target { get; private set; }
        public 운송의뢰배차대기생성옵션? Options { get; private set; }

        public Task<운송원장> 생성또는조회Async(
            출고예정운송대상 target,
            운송의뢰배차대기생성옵션? options = null,
            CancellationToken cancellationToken = default)
        {
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
        public async Task<살뜰마트라스트마일배차인계결과> 인계Async(
            살뜰마트라스트마일배차인계요청 요청,
            CancellationToken cancellationToken = default)
        {
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
        public Task<커뮤니티원장Dto?> 화주운송의뢰동기화Async(
            화주운송의뢰 의뢰,
            string updatedBy,
            CancellationToken cancellationToken = default)
            => Task.FromResult<커뮤니티원장Dto?>(null);

        public Task<커뮤니티원장Dto?> 운송실행투영동기화Async(
            운송원장 운송실행투영,
            string updatedBy,
            CancellationToken cancellationToken = default)
            => Task.FromResult<커뮤니티원장Dto?>(null);

        public Task<운송원장Mongo동기화상태> 상태조회Async(
            string 의뢰Id,
            CancellationToken cancellationToken = default)
            => Task.FromResult(운송원장Mongo동기화상태.Empty(의뢰Id, "test"));
    }

    private sealed class NoOpFoodMartLedgerSyncOutbox : I음식마트원장동기화OutboxService
    {
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
            => Task.CompletedTask;

        public Task<int> 대기항목처리Async(
            int take = 100,
            CancellationToken cancellationToken = default)
            => Task.FromResult(0);
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
