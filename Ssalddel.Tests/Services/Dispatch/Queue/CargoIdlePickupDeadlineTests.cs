using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ssalddel.Contracts.Common.Drivers;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.Storage.Local;
using 살뜰.도메인.공통;
using 살뜰.도메인.기사;
using 살뜰.도메인.차량;
using 살뜰.도메인.화물;
using 살뜰.도메인.화주;
using 살뜰.도메인.운송;

namespace Ssalddel.Tests.Services.Dispatch.Queue;

public sealed class CargoIdlePickupDeadlineTests
{
    [Theory]
    [InlineData("expired", "IdleSchedulePickupLate")]
    [InlineData("late", "IdleSchedulePickupLate")]
    [InlineData("pickup-window", "IdleScheduleEvidenceMissing")]
    [InlineData("dropoff-window", "IdleScheduleEvidenceMissing")]
    [InlineData("dropoff-coordinates", "IdleScheduleEvidenceMissing")]
    [InlineData("route-missing", "IdleSchedulePickupRouteUnverified")]
    [InlineData("route-approximate", "IdleSchedulePickupRouteUnverified")]
    [InlineData("route-duration", "IdleSchedulePickupRouteUnverified")]
    [InlineData("route-option", "IdleSchedulePickupRouteUnverified")]
    [InlineData("dropoff-late", "IdleScheduleCandidateInfeasible")]
    [InlineData("handling-late", "IdleScheduleCandidateInfeasible")]
    public async Task 반경안대기기사도_약속과전체일정근거가없으면_점수로상쇄하지않는다(string condition, string expected)
    {
        await using var f = await Fixture.CreateAsync();
        if (condition == "expired") f.Request.픽업_시간창_종료일시 = DateTime.UtcNow.AddMinutes(-1);
        if (condition == "late") f.Routes.Duration = TimeSpan.FromHours(2);
        if (condition == "pickup-window") f.Request.픽업_시간창_종료일시 = default;
        if (condition == "dropoff-window") f.Request.하차_시간창_종료일시 = null;
        if (condition == "dropoff-coordinates") f.Request.하차_위도 = null;
        if (condition == "route-missing") f.Routes.Missing = true;
        if (condition == "route-approximate") f.Routes.Actual = false;
        if (condition == "route-duration") f.Routes.Duration = null;
        if (condition == "route-option") f.Routes.Option = null;
        if (condition == "dropoff-late") f.Request.하차_시간창_종료일시 = DateTime.UtcNow.AddMinutes(3);
        if (condition == "handling-late") f.Request.픽업_작업시간분 = 90m;
        await f.Db.SaveChangesAsync();

        var review = await f.Policy().후보검토Async(f.Queue);

        Assert.Null(review.선정후보);
        Assert.Equal(expected, Assert.Single(review.후보목록).판정Code);
        Assert.Equal(0, f.Score.Calls);
        Assert.Equal(0, f.Queue.추천라운드);
    }

    [Fact]
    public async Task 실제경로와전체시간창을지키면_현재Gps기준도착과여유를제공한다()
    {
        await using var f = await Fixture.CreateAsync();
        var started = DateTime.UtcNow;
        var review = await f.Policy().후보검토Async(f.Queue);

        Assert.Equal("driver", review.선정후보?.DriverId);
        var candidate = Assert.Single(review.후보목록);
        Assert.Equal("CandidateAllowed", candidate.판정Code);
        Assert.Equal(0.1m, candidate.거리Km);
        Assert.InRange(candidate.픽업도착예상시각Utc!.Value, started.AddMinutes(2), DateTime.UtcNow.AddMinutes(2));
        Assert.True(f.Score.Slack > 50m);
        Assert.NotNull(f.Score.Schedule);
        Assert.True(f.Score.Schedule!.후보포함실제경로확인됨);
        Assert.False(review.선정후보!.화물수행근거!.활성업무있음);
        Assert.Empty(f.Db.ChangeTracker.Entries().Where(x => x.State != EntityState.Unchanged));
    }

    [Theory]
    [InlineData("pickup-window")]
    [InlineData("dropoff-coordinates")]
    [InlineData("handling")]
    public async Task 비동기경로계산중주문근거가변경되면_과거조건으로추천하지않는다(string change)
    {
        await using var f = await Fixture.CreateAsync();
        f.Routes.BeforeFirst = async () =>
        {
            if (change == "pickup-window") f.Request.픽업_시간창_종료일시 = DateTime.UtcNow.AddMinutes(1);
            if (change == "dropoff-coordinates") f.Request.하차_위도 = 37.6m;
            if (change == "handling") f.Request.하차_작업시간분 = 15m;
            await f.Db.SaveChangesAsync();
        };

        var review = await f.Policy().후보검토Async(f.Queue);

        Assert.Null(review.선정후보);
        Assert.Equal("IdleScheduleRequestChanged", Assert.Single(review.후보목록).판정Code);
    }

    [Fact]
    public async Task 경로공급자가취소를무시해도_호출자취소는미확인후보로흡수하지않는다()
    {
        await using var f = await Fixture.CreateAsync();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var blocked = new TaskCompletionSource<배차경로예상결과?>(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Routes.Pending = () => { entered.SetResult(); return blocked.Task; };
        using var cts = new CancellationTokenSource();
        var review = f.Policy().후보검토Async(f.Queue, cancellationToken: cts.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => review.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(0, f.Score.Calls);
        blocked.TrySetResult(null);
    }

    [Fact]
    public async Task 끝점기준옵션Off도_기본반경으로약속실패를우회하지않는다()
    {
        await using var f = await Fixture.CreateAsync();
        f.Request.픽업_시간창_종료일시 = DateTime.UtcNow.AddMinutes(-1);
        await f.Db.SaveChangesAsync();

        var review = await f.Policy(useEndAnchor: false).후보검토Async(f.Queue);

        Assert.Null(review.선정후보);
        Assert.Equal("IdleSchedulePickupLate", Assert.Single(review.후보목록).판정Code);
    }

    private sealed class Fixture(SsalddelContext db) : IAsyncDisposable
    {
        public SsalddelContext Db { get; } = db;
        public Routes Routes { get; } = new();
        public Score Score { get; } = new();
        public 화주운송의뢰 Request { get; } = new()
        {
            의뢰Id = "request", 화물종류 = "상자", 픽업_위도 = 37.58m, 픽업_경도 = 127.08m,
            하차_위도 = 37.59m, 하차_경도 = 127.09m,
            픽업_시간창_시작일시 = DateTime.UtcNow.AddHours(-1), 픽업_시간창_종료일시 = DateTime.UtcNow.AddHours(1),
            하차_시간창_시작일시 = DateTime.UtcNow.AddHours(-1), 하차_시간창_종료일시 = DateTime.UtcNow.AddHours(2),
            픽업_작업시간분 = 0m, 하차_작업시간분 = 0m
        };
        public 운송원장 Queue { get; } = new()
        { 의뢰Id = "request", 운송번호 = "request", 배차업무유형 = 상태값.배차업무유형.용달운송, 픽업_위도 = 37.58m, 픽업_경도 = 127.08m };
        public static async Task<Fixture> CreateAsync()
        {
            var f = new Fixture(new SsalddelContext(new DbContextOptionsBuilder<SsalddelContext>()
                .UseInMemoryDatabase($"idle-deadline-{Guid.NewGuid():N}").Options, new Encryption()));
            f.Db.화주운송의뢰.Add(f.Request);
            f.Db.용달기사.Add(new() { 기사Id = "driver", 차량 = "fixture", 상태 = "활동중" });
            await f.Db.SaveChangesAsync();
            return f;
        }
        public 용달운송배차업무정책 Policy(bool useEndAnchor = true) => new(Db, new Locations(), new States(),
            Options.Create(new 배차큐정책Options { 기사후보검색반경Km = 5m, 화물수행종료기준사용 = useEndAnchor }),
            Routes, new Fit(), new 배차추천판정Service(), Score, new Rejected(),
            운영배차: Ssalddel.Tests.Services.Dispatch.Common.운영배차시험수신UseCase.허용,
            신규업무자격: Ssalddel.Tests.Support.Simulation운송자격Guard.Instance);
        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }

    private sealed class Routes : I배차추천경로Service
    {
        public bool Missing { get; set; }
        public bool Actual { get; set; } = true;
        public TimeSpan? Duration { get; set; } = TimeSpan.FromMinutes(2);
        public string? Option { get; set; } = "trafast";
        public Func<Task>? BeforeFirst { get; set; }
        public Func<Task<배차경로예상결과?>>? Pending { get; set; }
        public decimal? CalculateDistanceKm(배차경로좌표 a, 배차경로좌표 b) => 0.1m;
        public async Task<배차경로예상결과?> EstimateRouteAsync(배차경로좌표? a, 배차경로좌표? b)
        {
            if (BeforeFirst is { } before) { BeforeFirst = null; await before(); }
            if (Pending is { } pending) return await pending();
            return Missing ? null : new(0.1m, Duration, 0m, "fixture", Actual) { RouteOption = Option };
        }
        public Task<배차경로좌표?> ResolveOriginLocationAsync(string id, 용달기사? driver, DriverLocationSnapshot? current, 배차추천검색조건? criteria) => throw new NotSupportedException();
        public Task<배차경로좌표?> ResolveRouteAnchorLocationAsync(string id, 용달기사? driver, DriverLocationSnapshot? current) => throw new NotSupportedException();
        public Task<배차경로예상결과?> EstimateOrderedRouteAsync(배차경로좌표? origin, IReadOnlyList<배차경로좌표> stops, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<배차삽입경로예상결과?> EstimateInsertionDelayAsync(배차경로좌표? origin, 배차경로좌표? anchor, 배차경로좌표? pickup, 배차경로좌표? dropoff) => throw new NotSupportedException();
    }
    private sealed class States : I국내화물운송기사상태Store
    {
        private static IReadOnlyList<국내화물운송기사상태Snapshot> Read()
        {
            var now = DateTime.UtcNow;
            return [new("driver", 1, 상태값.기사운행상태.운행중, now.AddHours(-1), now.AddMinutes(-45), 0m,
                37.58m, 127.079m, 1m, now, now, null, null, 0, null, null, null, AppKey: 기사앱식별자.CargoYongdalDriverApp)];
        }
        public Task<IReadOnlyList<국내화물운송기사상태Snapshot>> 위치반경조회Async(decimal lat, decimal lng, decimal radius, int take, CancellationToken ct = default) => Task.FromResult(Read());
        public Task<IReadOnlyList<국내화물운송기사상태Snapshot>> 활성기사조회Async(int take, CancellationToken ct = default) => Task.FromResult(Read());
        public Task<국내화물운송기사상태Snapshot?> GetAsync(string id, CancellationToken ct = default) => Task.FromResult<국내화물운송기사상태Snapshot?>(Read()[0]);
        public Task UpsertAsync(국내화물운송기사상태Snapshot snapshot, CancellationToken ct = default) => throw new NotSupportedException();
        public Task RemoveAsync(string id, CancellationToken ct = default) => throw new NotSupportedException();
    }
    private sealed class Locations : IDriverLocationStore
    {
        public bool TryGetLatest(string id, out DriverLocationSnapshot item) { item = null!; return false; }
        public void Upsert(DriverLocationSnapshot item) => throw new NotSupportedException();
    }
    private sealed class Fit : I차량화물적합성Service
    { public 차량화물적합성결과 판정(차량제원? vehicle, 화주운송의뢰 request, 화물요구조건? requirement) => new(true, [], []); }
    private sealed class Score : I배차추천평가Service
    {
        public int Calls { get; private set; }
        public decimal? Slack { get; private set; }
        public 운송삽입평가결과? Schedule { get; private set; }
        public 배차추천평가결과 평가(화주운송의뢰? request, 배차추천판정결과 decision, 운송삽입평가결과? schedule,
            decimal? profit, decimal? delay, decimal? distance, decimal? duration, decimal? slack, decimal? detour, bool returnBasis, string? source)
        { Calls++; Slack = slack; Schedule = schedule; return new(100m, [], [], "fixture", null); }
    }
    private sealed class Rejected : IDriverRejectedRequestStore
    {
        public Task<IReadOnlySet<string>> GetRejectedDriverIdsAsync(string id, CancellationToken ct = default) => Task.FromResult<IReadOnlySet<string>>(new HashSet<string>());
        public Task<IReadOnlySet<string>> GetRejectedRequestIdsAsync(string id, CancellationToken ct = default) => Task.FromResult<IReadOnlySet<string>>(new HashSet<string>());
        public Task<bool> IsRejectedAsync(string driver, string request, CancellationToken ct = default) => Task.FromResult(false);
        public Task RejectAsync(string driver, string request, CancellationToken ct = default) => throw new NotSupportedException();
    }
    private sealed class Encryption : IPersonalDataEncryptionService
    { public string? Protect(string? value) => value; public string? Unprotect(string? value) => value; }
}
