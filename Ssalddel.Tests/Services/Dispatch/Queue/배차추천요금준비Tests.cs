using System.Text.Json;
using FluentResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ssalddel.Application.Pricing;
using Ssalddel.Contracts.Common.Versioning;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Dispatch.Engine;
using 살뜰.Services.Dispatch.Notification;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.Weather;
using 살뜰.도메인.공통;
using 살뜰.도메인.배차;
using 살뜰.도메인.음식;

namespace Ssalddel.Tests.Services.Dispatch.Queue;

public sealed class 배차추천요금준비Tests
{
    [Fact]
    public async Task 요금준비는_소형후음식순서를_유지하고_저장과추천전환을_수행하지않는다()
    {
        await using var db = CreateDb();
        var queue = Queue();
        queue.기사지급예정액 = 200m;
        db.운송원장.Add(queue);
        await db.SaveChangesAsync();
        var calls = new List<string>();
        var small = new SmallPricing(_ => { calls.Add("small"); return Result.Ok(); });
        var food = new FoodPricing(() => calls.Add("food"));
        var preparation = new 배차추천요금준비(db, queue, small, food);

        var result = await preparation.준비Async("driver", default);

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { "small", "food" }, calls);
        Assert.Equal(4000m, queue.기사지급예정액);
        Assert.Equal(JsonSerializer.Serialize(Decision("driver")), queue.기사제안요금계산근거Json);
        Assert.Equal(상태값.배차노출상태.추천대기, queue.배차노출상태);
        Assert.Equal(0, queue.추천라운드);
        Assert.Equal(200m, (await db.운송원장.AsNoTracking().SingleAsync()).기사지급예정액);
    }

    [Fact]
    public async Task 원복은_요금필드만이아닌_기존EF현재값전체를_복원한다()
    {
        await using var db = CreateDb();
        var queue = Queue();
        queue.기사지급예정액 = 200m;
        queue.픽업_상세주소 = "원래 세부값";
        db.운송원장.Add(queue);
        await db.SaveChangesAsync();
        var food = new FoodPricing();
        var preparation = new 배차추천요금준비(db, queue, new SmallPricing(row =>
        {
            row.기사지급예정액 = 9999m;
            row.픽업_상세주소 = "변경된 세부값";
            return Result.Fail("준비 실패");
        }), food);

        Assert.True((await preparation.준비Async("driver", default)).IsFailed);
        Assert.Equal(0, food.Calls);
        Assert.Equal(9999m, queue.기사지급예정액);
        preparation.원복();
        Assert.Equal(200m, queue.기사지급예정액);
        Assert.Equal("원래 세부값", queue.픽업_상세주소);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task 기존동결은_같은기사일때만_유지하고_다음기사는_다시산정한다(bool sameDriver)
    {
        await using var db = CreateDb();
        var queue = Queue();
        queue.기사제안요금정책판본 = "existing";
        queue.기사제안요금계산근거Json = JsonSerializer.Serialize(Decision("first"));
        queue.기사지급예정액 = 1234m;
        var original = queue.기사제안요금계산근거Json;
        var food = new FoodPricing();

        await new 배차추천요금준비(db, queue, null, food).준비Async(sameDriver ? "first" : "next", default);

        Assert.Equal(sameDriver ? 0 : 1, food.Calls);
        Assert.Equal(sameDriver ? 1234m : 4000m, queue.기사지급예정액);
        if (sameDriver) Assert.Equal(original, queue.기사제안요금계산근거Json);
        else Assert.Equal("next", JsonSerializer.Deserialize<음식배달기사제안요금산정결과>(queue.기사제안요금계산근거Json!)!.기사Id);
    }

    [Fact]
    public async Task 잘못된기존근거JSON과_호출자취소는_기존예외로_전파한다()
    {
        await using var db = CreateDb();
        var queue = Queue();
        queue.기사제안요금정책판본 = "existing";
        queue.기사제안요금계산근거Json = "{";
        var food = new FoodPricing();
        var invalid = await Assert.ThrowsAsync<ArgumentException>(() =>
            new 배차추천요금준비(db, queue, null, food).준비Async("driver", default));
        Assert.Equal("FoodPricingEvidenceInvalid", invalid.Message);
        Assert.Equal(0, food.Calls);
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            new 배차추천요금준비(db, queue, new SmallPricing(_ => throw new OperationCanceledException()), food)
                .준비Async("driver", default));
    }

    [Fact]
    public async Task 화물원장은_소형준비만_수행하고_음식요금을_호출하지않는다()
    {
        await using var db = CreateDb();
        var queue = Queue();
        queue.배차업무유형 = 상태값.배차업무유형.용달운송;
        queue.기사지급예정액 = 27000m;
        var small = new SmallPricing(_ => Result.Ok());
        var food = new FoodPricing();

        Assert.True((await new 배차추천요금준비(db, queue, small, food).준비Async("driver", default)).IsSuccess);
        Assert.Equal(1, small.Calls);
        Assert.Equal(0, food.Calls);
        Assert.Equal(27000m, queue.기사지급예정액);
    }

    [Theory]
    [InlineData("result", false)]
    [InlineData("result", true)]
    [InlineData("argument", false)]
    [InlineData("argument", true)]
    [InlineData("agreement", false)]
    [InlineData("agreement", true)]
    public async Task 기존실패경로는_Result에서만원복하며_예외뒤감사저장의_비대칭을_보존한다(string mode, bool audit)
    {
        // 구조 변경의 특성화다. 주입 Port가 금액 변경 후 예외를 던지는 경우이며 실제 운영 발생 증거가 아니다.
        // 예외 경로의 원복 확대는 별도 행동 변경으로 다룬다.
        await using var db = CreateDb();
        var queue = Queue();
        db.운송원장.Add(queue);
        await db.SaveChangesAsync();
        var notification = new Notifications();
        var small = new SmallPricing(row =>
        {
            row.기사지급예정액 = 9999m;
            return mode switch
            {
                "result" => Result.Fail("요금 준비 실패"),
                "argument" => throw new ArgumentException("합성 요금 입력 오류"),
                _ => throw new AgreedRateTableException("SyntheticAgreementFailure")
            };
        });
        var selection = 배차추천후보선정결과.선정됨(new("driver", 1m, "합성 후보"));
        if (audit) selection = selection with { 감사Context = new 배차엔진판단감사Context(
            "synthetic-pricing-correlation", OperatingSystemIds.FoodDelivery,
            EngineFamilyIds.TransportRequestDispatch, "synthetic-pricing-engine") };
        var service = new 배차대기원장전환Service(db, Options.Create(new 배차큐정책Options()),
            new Candidates(selection), notification, null!, new 음식배달배차흐름Resolver(),
            null, new FoodRecord(), smallGoodsPricing: small);

        var result = await service.추천대기처리Async(queue.의뢰Id);

        Assert.False(result.전환여부);
        Assert.Equal(배차대기원장전환결과코드.배차구성오류, result.결과코드);
        Assert.Equal(mode == "result" ? null : (decimal?)9999m, queue.기사지급예정액);
        Assert.Equal(0, notification.Calls);
        Assert.Empty(await db.운영배차활동사건.ToArrayAsync());
        Assert.Equal(audit ? 1 : 0, await db.운송이벤트.CountAsync());
        db.ChangeTracker.Clear();
        var saved = await db.운송원장.AsNoTracking().SingleAsync();
        Assert.Equal(audit && mode != "result" ? (decimal?)9999m : null, saved.기사지급예정액);
        Assert.Null(saved.현재추천대상기사Id);
        Assert.Equal(0, saved.추천라운드);
    }

    private static SsalddelContext CreateDb()
        => new(new DbContextOptionsBuilder<SsalddelContext>().UseInMemoryDatabase("pricing-extraction-" + Guid.NewGuid()).Options,
            new Encryption());

    private static 운송원장 Queue() => new()
    {
        Id = 10, 의뢰Id = "SYNTHETIC-PRICING", 상태 = 상태값.배차대기상태.대기,
        배차업무유형 = 상태값.배차업무유형.음식배달, 원본의뢰유형 = 운송의뢰배차원천유형.음식점주문,
        원본의뢰Id = "SYNTHETIC-FOOD", 배차큐단계 = 상태값.배차큐단계.배차추천,
        배차노출상태 = 상태값.배차노출상태.추천대기, 픽업_위도 = 37.5m, 픽업_경도 = 127m
    };

    private static 음식배달기사제안요금산정결과 Decision(string driver) => new(
        new 음식배달기사제안요금판정(2500m, 1000m, 500m, 4000m, true, true),
        new 픽업지기상관측결과(true, true, 픽업지기상자료상태Code.Available,
            "1", "1.0", DateTime.UnixEpoch, 픽업지기상관측결과.공식자료출처, new string('a', 64)),
        "synthetic-pricing-r1", DateTime.UnixEpoch) { 기사Id = driver };

    private sealed class SmallPricing(Func<운송원장, Result> action) : I소형배송기사제안요금Service
    {
        public int Calls { get; private set; }
        public Task<Result> 준비Async(운송원장 queue, CancellationToken ct = default) { Calls++; return Task.FromResult(action(queue)); }
        public Task<Result> 조회검증Async(운송원장 queue, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Result> 수락검증Async(운송원장 queue, string driverId, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class FoodPricing(Action? onCall = null) : I음식배달기사제안요금Service
    {
        public int Calls { get; private set; }
        public Task<음식배달기사제안요금산정결과> 산정Async(운송원장 queue, CancellationToken ct = default)
            => 산정Async(queue, "driver", ct);
        public Task<음식배달기사제안요금산정결과> 산정Async(운송원장 queue, string driverId, CancellationToken ct = default)
        { Calls++; onCall?.Invoke(); return Task.FromResult(Decision(driverId)); }
    }

    private sealed class Candidates(배차추천후보선정결과 result) : I배차추천후보선정Service
    {
        public Task<배차추천후보선정결과> 다음후보선정Async(string requestId, string? 제외기사Id = null, CancellationToken cancellationToken = default)
            => Task.FromResult(result);
    }

    private sealed class Notifications : I배차추천알림Service
    {
        public int Calls { get; private set; }
        public Task 추천알림요청생성Async(long queueId, string requestId, string driverId, int round, CancellationToken cancellationToken = default)
        { Calls++; return Task.CompletedTask; }
        public Task<int> 대기알림발송Async(int take = 100, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FoodRecord : I음식배달기사추천기록Service
    {
        public Task 추천기록Async(string driverId, DateTime at, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class Encryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
