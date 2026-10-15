using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ssalddel.Contracts.Common.Dispatch;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Dispatch.AI;
using 살뜰.Services.Dispatch.Engine;
using 살뜰.Services.Dispatch.Notification;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.Storage.Local;
using 살뜰.Services.Weather;
using 살뜰.도메인.공통;
using 살뜰.도메인.배차;
using 살뜰.도메인.운송;
using 살뜰.도메인.음식;

namespace Ssalddel.Tests.Services.Dispatch.Queue;

public sealed class FoodDispatchTimingTransitionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 수집중에는_자동과직접추천_모두_요금알림원장을_만들지않는다(bool direct)
    {
        await using var f = new Fixture();
        var created = f.Queue.CreatedAt;
        var updated = f.Queue.UpdatedAt;
        var result = direct ? await f.Service.추천시작Async("timing-order", "driver")
            : await f.Service.추천대기처리Async("timing-order");
        Assert.Equal("FoodDispatchTiming.Collect", result.결과코드);
        Assert.False(result.전환여부);
        Assert.Equal(0, f.Selection.Calls);
        Assert.Equal(0, f.Pricing.Calls);
        Assert.Equal(0, f.Notifications.Calls);
        f.Db.ChangeTracker.Clear();
        var saved = await f.Db.운송원장.SingleAsync();
        Assert.Equal(created, saved.CreatedAt);
        Assert.Equal(updated, saved.UpdatedAt);
        Assert.Null(saved.현재추천대상기사Id);
        Assert.Null(saved.기사지급예정액);
        Assert.Empty(await f.Db.운송이벤트.ToListAsync());
        Assert.Empty(await f.Db.운영배차활동사건.ToListAsync());
    }

    [Fact]
    public async Task 최초_계획큐전환에서도_수집판정이_적용된다()
    {
        await using var f = new Fixture();
        f.Queue.배차큐단계 = 상태값.배차큐단계.계획배차;
        f.Queue.배차노출상태 = 상태값.배차노출상태.계획대기;
        await f.Db.SaveChangesAsync();
        Assert.Equal("FoodDispatchTiming.Collect", (await f.Service.계획배차에서추천으로전환Async("timing-order")).결과코드);
        Assert.Equal(1, f.Queue.계획배차시도횟수);
        Assert.Equal(0, f.Selection.Calls);
        Assert.Equal(0, f.Notifications.Calls);
    }

    [Fact]
    public async Task 수집기한뒤에는_기존추천으로진행하고_반복호출은_중복제안하지않는다()
    {
        await using var f = new Fixture();
        Assert.Equal("FoodDispatchTiming.Collect", (await f.Service.추천대기처리Async("timing-order")).결과코드);
        f.Clock.Now += TimeSpan.FromSeconds(31);
        Assert.True((await f.Service.추천대기처리Async("timing-order")).전환여부);
        Assert.False((await f.Service.추천대기처리Async("timing-order")).전환여부);
        f.Db.ChangeTracker.Clear();
        var saved = await f.Db.운송원장.SingleAsync();
        Assert.Equal("driver", saved.현재추천대상기사Id);
        Assert.Equal(1, saved.추천라운드);
        Assert.Equal(4000m, saved.기사지급예정액);
        Assert.Equal(1, f.Selection.Calls);
        Assert.Equal(1, f.Pricing.Calls);
        Assert.Equal(1, f.Notifications.Calls);
        Assert.Single(await f.Db.운영배차활동사건.ToListAsync());
    }

    [Theory]
    [InlineData("ready")]
    [InlineData("urgent")]
    [InlineData("unknown")]
    [InlineData("sparse")]
    public async Task 조합발견_긴급_미확인_희박수요는_수집을건너뛴다(string mode)
    {
        await using var f = new Fixture();
        f.Timing.Mode = mode;
        Assert.True((await f.Service.추천대기처리Async("timing-order")).전환여부);
        Assert.Equal(1, f.Selection.Calls);
        Assert.Equal(1, f.Notifications.Calls);
    }

    [Theory]
    [InlineData("missing-information")]
    [InlineData("stale-information")]
    [InlineData("unbound-information")]
    public async Task 준비근거없는Collect응답도_자동과직접경로에서_기존요금관문으로복귀한다(string mode)
    {
        foreach (var direct in new[] { false, true })
        {
            await using var f = new Fixture();
            f.Timing.Mode = mode;
            var result = direct ? await f.Service.추천시작Async("timing-order", "driver")
                : await f.Service.추천대기처리Async("timing-order");
            Assert.True(result.전환여부);
            Assert.Equal(1, f.Pricing.Calls);
            Assert.Equal(1, f.Notifications.Calls);
            Assert.Equal(4000m, (await f.Db.운송원장.AsNoTracking().SingleAsync()).기사지급예정액);
        }
    }

    [Fact]
    public async Task 판단후원장재조회사이에정보가만료되면_수집기한전이어도_기존배차를진행한다()
    {
        await using var f = new Fixture();
        // 수집 종료까지는 25초가 남지만 관측 유효시간 15초는 지납니다.
        f.Timing.BeforeReturn = () => { f.Clock.Now += TimeSpan.FromSeconds(16); return Task.CompletedTask; };
        var result = await f.Service.추천대기처리Async("timing-order");
        Assert.True(result.전환여부);
        Assert.Equal(1, f.Selection.Calls);
        Assert.Equal(1, f.Pricing.Calls);
        Assert.Equal(1, f.Notifications.Calls);
    }

    [Fact]
    public async Task 판단중_원장이취소되면_낡은판단으로_추천하지않는다()
    {
        await using var f = new Fixture();
        f.Timing.BeforeReturn = async () =>
        {
            f.Queue.상태 = 상태값.배차상태.취소;
            await f.Db.SaveChangesAsync();
        };
        var result = await f.Service.추천대기처리Async("timing-order");
        Assert.Equal("FoodDispatchTiming.QueueChanged", result.메시지);
        Assert.Equal(0, f.Selection.Calls);
        Assert.Equal(0, f.Notifications.Calls);
        Assert.Equal(상태값.배차상태.취소, (await f.Db.운송원장.AsNoTracking().SingleAsync()).상태);
    }

    [Fact]
    public async Task 판단응답이_수집기한뒤에도착하면_새로운대기를_시작하지않는다()
    {
        await using var f = new Fixture();
        f.Timing.BeforeReturn = () => { f.Clock.Now += TimeSpan.FromSeconds(40); return Task.CompletedTask; };
        Assert.True((await f.Service.추천대기처리Async("timing-order")).전환여부);
        Assert.Equal(1, f.Notifications.Calls);
    }

    [Theory]
    [InlineData("off")]
    [InlineData("cargo")]
    [InlineData("mart")]
    [InlineData("retry")]
    public async Task 비활성_다른업무_마트_재추천에는_수집판정을_호출하지않는다(string mode)
    {
        await using var f = new Fixture();
        if (mode == "off") f.Timing.Enabled = false;
        if (mode == "cargo") f.Queue.배차업무유형 = 상태값.배차업무유형.용달운송;
        if (mode == "mart") f.Queue.원본의뢰유형 = 운송의뢰배차원천유형.살뜰마트음식주문;
        if (mode == "retry") f.Queue.추천라운드 = 1;
        await f.Db.SaveChangesAsync();
        await f.Service.추천대기처리Async("timing-order");
        Assert.Equal(0, f.Timing.Calls);
        Assert.Equal(mode == "cargo" ? 1 : 0, f.Cargo.Calls);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection = new("Data Source=:memory:");
        public SsalddelContext Db { get; }
        public Clock Clock { get; } = new();
        public 운송원장 Queue { get; }
        public TimingPort Timing { get; }
        public SelectionPort Selection { get; } = new();
        public PricingPort Pricing { get; } = new();
        public NotificationPort Notifications { get; } = new();
        public CargoPort Cargo { get; } = new();
        public 배차대기원장전환Service Service { get; }
        public Fixture()
        {
            connection.Open();
            Db = new(new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(connection).Options, new PersonalDataPort());
            Db.Database.EnsureCreated();
            Queue = new() { 의뢰Id = "timing-order", 원본의뢰유형 = 운송의뢰배차원천유형.음식점주문,
                상태 = 상태값.배차대기상태.대기, 배차업무유형 = 상태값.배차업무유형.음식배달,
                배차큐단계 = 상태값.배차큐단계.배차추천, 배차노출상태 = 상태값.배차노출상태.추천대기,
                CreatedAt = Clock.Now.UtcDateTime.AddSeconds(-5), UpdatedAt = Clock.Now.UtcDateTime };
            Db.운송원장.Add(Queue); Db.SaveChanges();
            Timing = new(Clock);
            Service = new(Db, Options.Create(new 배차큐정책Options()), Selection, Notifications, Cargo,
                new 음식배달배차흐름Resolver(), Pricing, new FoodRecordPort(), dispatchTiming: Timing, timeProvider: Clock);
        }
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await connection.DisposeAsync(); }
    }

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class TimingPort(Clock clock) : IFoodDispatchTimingService
    {
        public bool Enabled { get; set; } = true;
        public int Calls { get; private set; }
        public string Mode { get; set; } = "collect";
        public Func<Task>? BeforeReturn { get; set; }
        public async Task<FoodDispatchTimingEvaluation> EvaluateAsync(운송원장 queue, CancellationToken ct = default)
        {
            Calls++;
            var input = new FoodDispatchTimingInput { NowUtc = clock.Now, QueuedAtUtc = new(queue.CreatedAt, TimeSpan.Zero),
                SnapshotAtUtc = clock.Now, DemandKnown = Mode != "unknown", DriverCapacityKnown = true,
                WaitingOrders = Mode == "ready" ? 2 : 1, CompatibleOrders = Mode == "ready" ? 2 : 1,
                AvailableDrivers = 2, ExpectedNewOrders = Mode == "sparse" ? 0m : 1m, DelayRisk = Mode == "urgent",
                InformationReadiness = Mode == "unbound-information" ? null : new(
                    Mode == "missing-information" ? FoodDispatchInformationStateCodes.Missing
                        : Mode == "stale-information" ? FoodDispatchInformationStateCodes.Stale : FoodDispatchInformationStateCodes.Current,
                    FoodDispatchInformationStateCodes.Missing,
                    new[] { FoodDispatchInformationKeys.Order, FoodDispatchInformationKeys.Preparation,
                        FoodDispatchInformationKeys.Locations, FoodDispatchInformationKeys.OrderPool,
                        FoodDispatchInformationKeys.Demand, FoodDispatchInformationKeys.DriverCapacity }
                        .Select(key => new FoodDispatchInformationEvidence(key,
                            key == FoodDispatchInformationKeys.Order && Mode == "missing-information" ? "Missing"
                                : key == FoodDispatchInformationKeys.Order && Mode == "stale-information" ? "Stale" : "Current",
                            "InformationObserved", clock.Now)).ToArray()) };
            var decision = new FoodDispatchTimingPolicy().Evaluate(input, new());
            // 전환 경계에서 오래된/결속 안 된 외부 Collect 응답도 다시 거부하는지 확인합니다.
            if (Mode is "missing-information" or "stale-information" or "unbound-information")
                decision = decision with { ActionCode = "Collect", RecheckAtUtc = clock.Now.AddSeconds(10), AllowedActions = ["Collect", "DispatchNow"] };
            if (BeforeReturn is not null) await BeforeReturn();
            return new(input, decision, "Fake");
        }
    }
    private sealed class SelectionPort : I배차추천후보선정Service
    {
        public int Calls { get; private set; }
        public Task<배차추천후보선정결과> 다음후보선정Async(string requestId, string? 제외기사Id = null, CancellationToken cancellationToken = default)
        { Calls++; return Task.FromResult(배차추천후보선정결과.선정됨(new("driver", 90m, "TimingTest"))); }
    }
    private sealed class NotificationPort : I배차추천알림Service
    {
        public int Calls { get; private set; }
        public Task 추천알림요청생성Async(long id, string request, string driver, int round, CancellationToken ct = default)
        { Calls++; return Task.CompletedTask; }
        public Task<int> 대기알림발송Async(int take = 100, CancellationToken ct = default) => throw new NotSupportedException();
    }
    private sealed class FoodRecordPort : I음식배달기사추천기록Service
    { public Task 추천기록Async(string driver, DateTime now, CancellationToken ct = default) => Task.CompletedTask; }
    private sealed class CargoPort : I국내화물운송기사상태Service
    {
        public int Calls { get; private set; }
        public Task<국내화물운송기사상태Snapshot?> 추천기록Async(string driverId, DateTime 추천시각Utc,
            CancellationToken cancellationToken = default)
        { Calls++; return Task.FromResult<국내화물운송기사상태Snapshot?>(null); }
        public Task<국내화물운송기사상태Snapshot> 운행시작Async(string driverId, long shiftId, DateTime startedAtUtc,
            string startMode, string startLocation, string? returnDestination, string? 복귀콜선호 = null,
            string? appKey = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<국내화물운송기사상태Snapshot> 위치갱신Async(DriverLocationSnapshot location, long? shiftId = null,
            decimal? 상차접근허용반경Km = null, string? appKey = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<국내화물운송기사상태Snapshot?> 후보없음기록Async(string driverId, DateTime 기준시각Utc,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task 운행종료Async(string driverId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    private sealed class PricingPort : I음식배달기사제안요금Service
    {
        public int Calls { get; private set; }
        public Task<음식배달기사제안요금산정결과> 산정Async(운송원장 queue, CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult(new 음식배달기사제안요금산정결과(new 음식배달기사제안요금판정(2500m, 1000m, 500m, 4000m, true, true),
                new 픽업지기상관측결과(true, true, 픽업지기상자료상태Code.Available, "1", "1.0", DateTime.UnixEpoch,
                    픽업지기상관측결과.공식자료출처, new string('a', 64)), "timing-test", DateTime.UtcNow));
        }
    }
    private sealed class PersonalDataPort : IPersonalDataEncryptionService
    { public string? Protect(string? value) => value; public string? Unprotect(string? value) => value; }
}
