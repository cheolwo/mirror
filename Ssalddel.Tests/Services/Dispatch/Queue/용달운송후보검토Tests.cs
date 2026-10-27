using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ssalddel.Contracts.Common.Drivers;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Dispatch.AI;
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

public sealed class 용달운송후보검토Tests
{
    [Theory]
    [InlineData("Qualification.Pending")]
    [InlineData("Qualification.Suspended")]
    public async Task 자격이유효하지않은기사는_점수계산전에_제외사유를남긴다(string reason)
    {
        await using var fixture = await Fixture.CreateAsync(Snapshot("blocked", 1m));
        fixture.Qualification = new DeniedQualification(reason);

        var trace = await fixture.Policy().후보검토Async(fixture.Queue);

        Assert.Null(trace.선정후보);
        Assert.Empty(trace.최종허용기사Ids);
        Assert.Equal(reason, Assert.Single(trace.후보목록).판정Code);
        Assert.Equal(0, fixture.Score.Calls);
    }

    [Fact]
    public async Task 자격연결누락을_허용으로대체하지않는다()
    {
        await using var fixture = await Fixture.CreateAsync(Snapshot("blocked", 1m));
        fixture.Qualification = null;
        var trace = await fixture.Policy().후보검토Async(fixture.Queue);
        Assert.Null(trace.선정후보);
        Assert.Equal("FreightCargo.RequiredInformationMissing", trace.중단사유Code);
        Assert.Equal(0, fixture.Score.Calls);
    }

    [Fact]
    public async Task 실제추천은같은한번의계산과관찰결과를반환하고원장을바꾸지않는다()
    {
        await using var fixture = await Fixture.CreateAsync(Snapshot("near", 1m), Snapshot("waiting", 2m, 61));
        var before = JsonSerializer.Serialize(fixture.Queue);

        var result = await fixture.Policy().다음후보선정Async(fixture.Queue);

        var trace = Assert.Single(fixture.Observer.Traces);
        Assert.Equal("request", Assert.Single(fixture.Observer.Requests));
        Assert.Equal(result, trace.선정후보);
        Assert.Equal("waiting", result?.DriverId);
        Assert.Equal(104m, result?.추천점수);
        Assert.Equal(1, fixture.States.GeoReads);
        Assert.Equal(1, fixture.States.ActiveReads);
        Assert.Equal(2, fixture.Score.Calls);
        Assert.Equal(new[] { "near", "waiting" }, trace.최종허용기사Ids);
        var waiting = Assert.Single(trace.후보목록, x => x.기사Id == "waiting");
        Assert.Equal(98m, waiting.기본추천점수);
        Assert.Equal(6m, waiting.대기점수);
        Assert.Equal(result?.추천점수, waiting.추천점수);
        Assert.Equal(result?.추천사유, waiting.추천사유);
        Assert.Equal(before, JsonSerializer.Serialize(fixture.Queue));
        Assert.Empty(fixture.Db.ChangeTracker.Entries().Where(x => x.State != EntityState.Unchanged));
    }

    [Fact]
    public async Task 같은점수와거리이면기존조회순서의첫기사를유지한다()
    {
        await using var fixture = await Fixture.CreateAsync(Snapshot("z-first", 1m), Snapshot("a-second", 1m));

        var trace = await fixture.Policy().후보검토Async(fixture.Queue);

        Assert.Equal("z-first", trace.선정후보?.DriverId);
        Assert.Equal(new[] { "z-first", "a-second" }, trace.후보목록.Select(x => x.기사Id));
        Assert.Empty(fixture.Observer.Traces);
    }

    [Fact]
    public async Task 같은점수이면기존과같이더가까운기사를선택한다()
    {
        await using var fixture = await Fixture.CreateAsync(Snapshot("far-first", 2m), Snapshot("near-second", 1m));
        fixture.Score.FixedScore = 100m;

        var trace = await fixture.Policy().후보검토Async(fixture.Queue);

        Assert.Equal("near-second", trace.선정후보?.DriverId);
        Assert.All(trace.후보목록, x => Assert.Equal(100m, x.추천점수));
    }

    [Theory]
    [InlineData("app", "DriverAppMismatch")]
    [InlineData("vehicle", "ActiveVehicleMissing")]
    [InlineData("excluded", "DriverExcluded")]
    [InlineData("rejected", "DriverRejected")]
    [InlineData("stopped", "DriverNotDriving")]
    [InlineData("stale", "DriverLocationUnavailableOrStale")]
    [InlineData("distance", "IdleSchedulePickupRouteUnverified")]
    [InlineData("radius", "OutsideApproachAllowance")]
    [InlineData("fit", "VehicleIncompatible")]
    public async Task 실제탈락한첫관문과그때까지확인한거리만남긴다(string condition, string expected)
    {
        var state = Snapshot("blocked", condition == "radius" ? 50m : 1m);
        if (condition == "app") state = state with { AppKey = 기사앱식별자.FoodDeliveryDriverApp };
        if (condition == "stopped") state = state with { 운행상태 = "종료" };
        if (condition == "stale") state = state with { 위치수신시각Utc = DateTime.UtcNow.AddHours(-1) };
        await using var fixture = await Fixture.CreateAsync(state);
        if (condition == "vehicle")
        {
            fixture.Db.용달기사.RemoveRange(fixture.Db.용달기사);
            await fixture.Db.SaveChangesAsync();
        }
        if (condition == "rejected") fixture.Rejected.Ids.Add("blocked");
        if (condition == "distance") fixture.Routes.MissingDistance = true;
        if (condition == "fit") fixture.Fit.Allowed = false;

        var trace = await fixture.Policy().후보검토Async(fixture.Queue, condition == "excluded" ? "blocked" : null);

        Assert.Null(trace.선정후보);
        Assert.Empty(trace.최종허용기사Ids);
        Assert.Equal("NoEligibleCandidate", trace.중단사유Code);
        var item = Assert.Single(trace.후보목록);
        Assert.Equal(expected, item.판정Code);
        Assert.Equal(condition == "radius" ? 50m : condition == "fit" ? 1m : (decimal?)null, item.거리Km);
        Assert.Null(item.기본추천점수);
        Assert.Null(item.대기점수);
        Assert.Null(item.추천점수);
        Assert.Equal(0, fixture.Score.Calls);
    }

    [Theory]
    [InlineData(true, "RequestNotFound")]
    [InlineData(false, "PickupCoordinatesMissing")]
    public async Task 선행정보가없으면후보를읽었다고표시하지않는다(bool missingRequest, string expected)
    {
        await using var fixture = await Fixture.CreateAsync(Snapshot("unread", 1m));
        if (missingRequest)
        {
            fixture.Db.화주운송의뢰.RemoveRange(fixture.Db.화주운송의뢰);
            await fixture.Db.SaveChangesAsync();
        }
        else fixture.Queue.픽업_위도 = null;

        var result = await fixture.Policy().다음후보선정Async(fixture.Queue);

        Assert.Null(result);
        var trace = Assert.Single(fixture.Observer.Traces);
        Assert.Equal(expected, trace.중단사유Code);
        Assert.Empty(trace.후보목록);
        Assert.Empty(trace.최종허용기사Ids);
        Assert.Equal(0, fixture.States.GeoReads);
        Assert.Equal(0, fixture.States.ActiveReads);
    }

    [Fact]
    public async Task 조회되지않은기사를만들지않고후보없는상태를관찰한다()
    {
        await using var fixture = await Fixture.CreateAsync();

        var trace = await fixture.Policy().후보검토Async(fixture.Queue);

        Assert.Null(trace.선정후보);
        Assert.Empty(trace.후보목록);
        Assert.Equal("NoCandidatesInSearchScope", trace.중단사유Code);
    }

    [Fact]
    public async Task 점수가미확인이면기존영점대체계산은유지하되원본null을보존한다()
    {
        await using var fixture = await Fixture.CreateAsync(Snapshot("waiting", 1m, 61));
        fixture.Score.NullScore = true;

        var trace = await fixture.Policy().후보검토Async(fixture.Queue);

        Assert.Null(Assert.Single(trace.후보목록).기본추천점수);
        Assert.Equal(6m, trace.선정후보?.추천점수);
    }

    [Fact]
    public async Task 최신위치대체가있으면기존과같이그위치로판정한다()
    {
        var stale = Snapshot("fallback", 9m) with { 위치수신시각Utc = DateTime.UtcNow.AddHours(-1) };
        await using var fixture = await Fixture.CreateAsync(stale);
        fixture.Locations.Current = new("fallback", 1m, 0m, 1m, 상태값.기사운행상태.운행중, DateTime.UtcNow, DateTime.UtcNow);

        var trace = await fixture.Policy().후보검토Async(fixture.Queue);

        Assert.Equal(1m, Assert.Single(trace.후보목록).거리Km);
        Assert.Equal("fallback", trace.선정후보?.DriverId);
    }

    [Fact]
    public async Task 관찰자실패는실제추천결과를바꾸지않는다()
    {
        await using var fixture = await Fixture.CreateAsync(Snapshot("allowed", 1m));
        fixture.Observer.Throw = true;

        var result = await fixture.Policy().다음후보선정Async(fixture.Queue);

        Assert.Equal("allowed", result?.DriverId);
        Assert.Equal(99m, result?.추천점수);
        Assert.Equal(1, fixture.Score.Calls);
    }

    [Fact]
    public async Task 실제조회실패는관찰실패와달리숨기지않고전파한다()
    {
        await using var fixture = await Fixture.CreateAsync(Snapshot("unread", 1m));
        fixture.States.Throw = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Policy().다음후보선정Async(fixture.Queue));

        Assert.Empty(fixture.Observer.Traces);
        Assert.Equal(0, fixture.Score.Calls);
    }

    [Fact]
    public async Task 화물AI는_다른적격후보를선택하고_규칙관찰과현재수행근거를보존한다()
    {
        await using var fixture = await Fixture.CreateAsync(Snapshot("near", 1m), Snapshot("other", 2m));
        await fixture.PersistQueueAsync();
        var before = JsonSerializer.Serialize(fixture.Queue);
        var ai = new CandidateAI(new("other", "Applied"));

        var result = await fixture.Policy(ai).다음후보선정Async(fixture.Queue);

        Assert.Equal("other", result?.DriverId);
        Assert.Equal(98m, result?.추천점수);
        Assert.EndsWith("CargoDispatchAI.Applied", result?.추천사유);
        Assert.False(result!.화물수행근거!.활성업무있음);
        Assert.Equal(new 배차경로좌표(2m, 0m), result.화물수행근거.기준좌표);
        var baseline = Assert.Single(fixture.Observer.Traces);
        Assert.Equal("near", baseline.선정후보?.DriverId);
        Assert.DoesNotContain("CargoDispatchAI", baseline.선정후보!.추천사유);
        Assert.All(baseline.후보목록, x => Assert.NotNull(x.픽업도착예상시각Utc));
        Assert.Same(baseline, ai.Input);
        Assert.Equal(1, ai.Calls);
        Assert.Equal(2, fixture.States.GeoReads);
        Assert.Equal(before, JsonSerializer.Serialize(fixture.Queue));
        Assert.Empty(fixture.Db.ChangeTracker.Entries().Where(x => x.State != EntityState.Unchanged));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 화물AI응답대기중_선택기사차량이중지되거나삭제되면_현재규칙후보로복귀한다(bool remove)
    {
        await using var fixture = await Fixture.CreateAsync(Snapshot("near", 1m), Snapshot("other", 2m));
        await fixture.PersistQueueAsync();
        var ai = new CandidateAI(new("other", "Applied"), async () =>
        {
            var driver = await fixture.Db.용달기사.SingleAsync(x => x.기사Id == "other");
            if (remove) fixture.Db.용달기사.Remove(driver); else driver.상태 = "중지";
            await fixture.Db.SaveChangesAsync();
        });

        var result = await fixture.Policy(ai).다음후보선정Async(fixture.Queue);

        Assert.Equal("near", result?.DriverId);
        Assert.EndsWith("CargoDispatchAI.CandidateChanged", result?.추천사유);
        Assert.NotNull(result!.화물수행근거);
        Assert.Equal("near", Assert.Single(fixture.Observer.Traces).선정후보?.DriverId);
        Assert.Equal(1, ai.Calls);
        Assert.Equal(0, fixture.Queue.추천라운드);
    }

    [Theory]
    [InlineData("request-fare")]
    [InlineData("stored-queue")]
    [InlineData("caller-queue")]
    public async Task 화물AI응답대기중_원입력이나원장판본이달라지면_과거추천을반환하지않는다(string change)
    {
        await using var fixture = await Fixture.CreateAsync(Snapshot("near", 1m), Snapshot("other", 2m));
        await fixture.PersistQueueAsync();
        var ai = new CandidateAI(new("other", "Applied"), async () =>
        {
            if (change == "request-fare")
                (await fixture.Db.화주운송의뢰.SingleAsync()).최종운임 = 20000m;
            else if (change == "stored-queue") fixture.Queue.운임 = 20000m;
            else fixture.Queue.추천라운드++;
            if (change != "caller-queue") await fixture.Db.SaveChangesAsync();
        });

        var result = await fixture.Policy(ai).다음후보선정Async(fixture.Queue);

        Assert.Null(result);
        Assert.Equal("near", Assert.Single(fixture.Observer.Traces).선정후보?.DriverId);
        Assert.Equal(1, ai.Calls);
        Assert.Null(fixture.Queue.확정기사Id);
    }

    [Fact]
    public async Task 화물AI시간초과는_대기중달라진현재규칙결과로복귀하고_초기관찰을바꾸지않는다()
    {
        await using var fixture = await Fixture.CreateAsync(Snapshot("near", 1m), Snapshot("other", 2m));
        await fixture.PersistQueueAsync();
        var ai = new CandidateAI(new(null, "Timeout"), async () =>
        {
            (await fixture.Db.용달기사.SingleAsync(x => x.기사Id == "near")).상태 = "중지";
            await fixture.Db.SaveChangesAsync();
        });

        var result = await fixture.Policy(ai).다음후보선정Async(fixture.Queue);

        Assert.Equal("other", result?.DriverId);
        Assert.EndsWith("CargoDispatchAI.Timeout", result?.추천사유);
        Assert.Equal(new 배차경로좌표(2m, 0m), result!.화물수행근거!.기준좌표);
        Assert.Equal("near", Assert.Single(fixture.Observer.Traces).선정후보?.DriverId);
        Assert.Equal(3, fixture.Score.Calls);
    }

    [Fact]
    public async Task 화물AI비활성은_모델과추가정본요구없이_기존한번의규칙관찰을유지한다()
    {
        await using var fixture = await Fixture.CreateAsync(Snapshot("near", 1m), Snapshot("other", 2m));
        // 기존 최소 fixture처럼 DB queue 정본이 없어도 비활성 경로는 새 선행조건을 요구하지 않는다.
        Assert.Empty(await fixture.Db.운송원장.ToArrayAsync());
        var ai = new CandidateAI(new("other", "Applied")) { Enabled = false };

        var result = await fixture.Policy(ai).다음후보선정Async(fixture.Queue);

        Assert.Equal("near", result?.DriverId);
        Assert.Equal(0, ai.Calls);
        Assert.Equal(1, fixture.States.GeoReads);
        Assert.Equal(2, fixture.Score.Calls);
        Assert.Same(Assert.Single(fixture.Observer.Traces).선정후보, result);
        Assert.DoesNotContain("CargoDispatchAI", result!.추천사유);
        Assert.Empty(await fixture.Db.운송원장.ToArrayAsync());
    }

    private static 국내화물운송기사상태Snapshot Snapshot(string id, decimal distance, int waitingMinutes = 0)
    {
        var now = DateTime.UtcNow;
        return new(id, 1, 상태값.기사운행상태.운행중, now.AddHours(-3), now.AddMinutes(-waitingMinutes), 999m,
            distance, 0m, 1m, now, now, null, null, 0, null, null, null, AppKey: 기사앱식별자.CargoYongdalDriverApp);
    }

    private sealed class Fixture(SsalddelContext db, States states) : IAsyncDisposable
    {
        public SsalddelContext Db { get; } = db;
        public States States { get; } = states;
        public Observer Observer { get; } = new();
        public Score Score { get; } = new();
        public Fit Fit { get; } = new();
        public Routes Routes { get; } = new();
        public Locations Locations { get; } = new();
        public Rejected Rejected { get; } = new();
        public Ssalddel.Services.Dispatch.Compliance.I신규화물업무자격Guard? Qualification { get; set; }
            = Ssalddel.Tests.Support.Simulation운송자격Guard.Instance;
        public 운송원장 Queue { get; } = new()
        {
            의뢰Id = "request", 운송번호 = "request", 배차업무유형 = 상태값.배차업무유형.용달운송,
            픽업_위도 = 0m, 픽업_경도 = 0m
        };

        public static async Task<Fixture> CreateAsync(params 국내화물운송기사상태Snapshot[] states)
        {
            var db = new SsalddelContext(new DbContextOptionsBuilder<SsalddelContext>()
                .UseInMemoryDatabase($"cargo-candidate-trace-{Guid.NewGuid():N}").Options, new Encryption());
            db.화주운송의뢰.Add(new()
            {
                의뢰Id = "request", 픽업_위도 = 0m, 픽업_경도 = 0m, 하차_위도 = 0m, 하차_경도 = 0m, 화물종류 = "상자",
                픽업_시간창_시작일시 = DateTime.UtcNow.AddHours(-1), 픽업_시간창_종료일시 = DateTime.UtcNow.AddHours(12),
                하차_시간창_시작일시 = DateTime.UtcNow.AddHours(-1), 하차_시간창_종료일시 = DateTime.UtcNow.AddHours(13)
            });
            foreach (var state in states)
                db.용달기사.Add(new() { 기사Id = state.DriverId, 차량 = "fixture", 상태 = "활동중" });
            await db.SaveChangesAsync();
            return new(db, new(states));
        }

        public async Task PersistQueueAsync()
        {
            Db.운송원장.Add(Queue);
            await Db.SaveChangesAsync();
        }

        public 용달운송배차업무정책 Policy(ICargoDispatchCandidateAI? candidateAI = null) => new(Db, Locations, States,
            Options.Create(new 배차큐정책Options { 기사후보검색반경Km = 10m }), Routes,
            Fit, new 배차추천판정Service(), Score, Rejected, Observer,
            운영배차: Ssalddel.Tests.Services.Dispatch.Common.운영배차시험수신UseCase.허용,
            신규업무자격: Qualification, candidateAI: candidateAI);
        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }

    private sealed class CandidateAI(CargoDispatchAISelection selection, Func<Task>? beforeReturn = null) : ICargoDispatchCandidateAI
    {
        public bool Enabled { get; init; } = true;
        public int Calls { get; private set; }
        public 용달운송후보검토결과? Input { get; private set; }
        public async Task<CargoDispatchAISelection> SelectAsync(용달운송후보검토결과 trace, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            Calls++;
            Input = trace;
            if (beforeReturn is not null) await beforeReturn();
            return selection;
        }
    }

    private sealed class DeniedQualification(string reason) : Ssalddel.Services.Dispatch.Compliance.I신규화물업무자격Guard
    {
        public bool RequiresCargoEvidence => false;
        public Task<Ssalddel.Contracts.Driver.Profile.운송자격평가결과> 평가Async(string driverId, CancellationToken ct = default)
            => Task.FromResult(new Ssalddel.Contracts.Driver.Profile.운송자격평가결과(false, reason, "내부 검토 대기", "Pending"));
    }

    private sealed class Observer : I용달운송후보검토Observer
    {
        public List<용달운송후보검토결과> Traces { get; } = [];
        public List<string> Requests { get; } = [];
        public bool Throw { get; set; }
        public void OnReviewed(string id, 용달운송후보검토결과 trace)
        {
            Traces.Add(trace); Requests.Add(id);
            if (Throw) throw new InvalidOperationException("fixture observer failure");
        }
    }

    private sealed class States(IReadOnlyList<국내화물운송기사상태Snapshot> states) : I국내화물운송기사상태Store
    {
        public int GeoReads { get; private set; }
        public int ActiveReads { get; private set; }
        public bool Throw { get; set; }
        public Task<IReadOnlyList<국내화물운송기사상태Snapshot>> 위치반경조회Async(decimal lat, decimal lng, decimal radius, int take, CancellationToken cancellationToken = default)
        {
            GeoReads++;
            if (Throw) throw new InvalidOperationException("fixture source failure");
            return Task.FromResult(states);
        }
        public Task<IReadOnlyList<국내화물운송기사상태Snapshot>> 활성기사조회Async(int take, CancellationToken cancellationToken = default)
        { ActiveReads++; return Task.FromResult(states); }
        public Task<국내화물운송기사상태Snapshot?> GetAsync(string id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpsertAsync(국내화물운송기사상태Snapshot snapshot, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task RemoveAsync(string id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class Locations : IDriverLocationStore
    {
        public DriverLocationSnapshot? Current { get; set; }
        public bool TryGetLatest(string id, out DriverLocationSnapshot snapshot) { snapshot = Current!; return Current is not null; }
        public void Upsert(DriverLocationSnapshot snapshot) => throw new NotSupportedException();
    }

    private sealed class Rejected : IDriverRejectedRequestStore
    {
        public HashSet<string> Ids { get; } = [];
        public Task<IReadOnlySet<string>> GetRejectedDriverIdsAsync(string id, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlySet<string>>(Ids);
        public Task<IReadOnlySet<string>> GetRejectedRequestIdsAsync(string id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> IsRejectedAsync(string driver, string request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task RejectAsync(string driver, string request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class Score : I배차추천평가Service
    {
        public int Calls { get; private set; }
        public decimal? FixedScore { get; set; }
        public bool NullScore { get; set; }
        public 배차추천평가결과 평가(화주운송의뢰? request, 배차추천판정결과 decision, 운송삽입평가결과? schedule,
            decimal? profit, decimal? delay, decimal? distance, decimal? duration, decimal? slack,
            decimal? returnDetour, bool returnBasis, string? returnSource)
        { Calls++; return new(NullScore ? null : FixedScore ?? 100m - distance, [], [], "fixture score", null); }
    }

    private sealed class Fit : I차량화물적합성Service
    {
        public bool Allowed { get; set; } = true;
        public 차량화물적합성결과 판정(차량제원? vehicle, 화주운송의뢰 request, 화물요구조건? requirement)
            => new(Allowed, [], Allowed ? [] : ["fixture incompatible"]);
    }

    private sealed class Routes : I배차추천경로Service
    {
        public bool MissingDistance { get; set; }
        public decimal? CalculateDistanceKm(배차경로좌표 source, 배차경로좌표 target) => MissingDistance ? null : Math.Abs(source.Latitude - target.Latitude);
        public Task<배차경로좌표?> ResolveOriginLocationAsync(string id, 용달기사? driver, DriverLocationSnapshot? location, 배차추천검색조건? criteria) => throw new NotSupportedException();
        public Task<배차경로좌표?> ResolveRouteAnchorLocationAsync(string id, 용달기사? driver, DriverLocationSnapshot? location) => throw new NotSupportedException();
        public Task<배차경로예상결과?> EstimateRouteAsync(배차경로좌표? origin, 배차경로좌표? destination)
            => Task.FromResult<배차경로예상결과?>(MissingDistance || origin is null || destination is null ? null
                : new(CalculateDistanceKm(origin, destination), TimeSpan.FromMinutes(2), 0m, "fixture", true) { RouteOption = "trafast" });
        public Task<배차경로예상결과?> EstimateOrderedRouteAsync(배차경로좌표? origin, IReadOnlyList<배차경로좌표> stops, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<배차삽입경로예상결과?> EstimateInsertionDelayAsync(배차경로좌표? origin, 배차경로좌표? anchor, 배차경로좌표? pickup, 배차경로좌표? dropoff) => throw new NotSupportedException();
    }

    private sealed class Encryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
