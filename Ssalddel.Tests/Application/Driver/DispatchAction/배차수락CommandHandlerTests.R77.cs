using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Application.Driver.DispatchAction;
using Ssalddel.Contracts.Common.Operations;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Services.Dispatch.Participation;
using Ssalddel.Services.LogisticsProcessing.VehicleLoading;
using 살뜰.Services.Dispatch.Continuity;
using 살뜰.Services.Dispatch.Coordination;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.Storage.Local;
using 살뜰.도메인.공통;
using 살뜰.도메인.기사;
using 살뜰.도메인.운송;
using 살뜰.도메인.화주;

namespace Ssalddel.Tests.Application.Food;

// 기존 격리 SQLite·실제 상태 관문을 재사용한다. 경로 응답/실차 확인은 명시 합성 대역이다.
public sealed partial class 음식배달정상수직생명주기Tests
{
    [Fact]
    public async Task R77_신선한heartbeat만달라지면_본인추천의라운드보수근거와알림을바꾸지않는다()
    {
        await using var f = await R76StateFixture.CreateAsync();
        await R77Prepare(f, R76StateFixture.Id);
        await R77StoreOffers(f, R76StateFixture.Id);
        var before = JsonSerializer.Serialize(await f.Db.운송원장.AsNoTracking().SingleAsync());
        f.States.Current = f.States.Current with { 위치수신시각Utc = f.Now.AddSeconds(-1), 위치기록시각Utc = f.Now.AddSeconds(-1) };
        var routes = new R77Routes();
        var service = R77Refresh(f, routes);
        var result = await service.갱신Async("driver");
        Assert.Equal([R76StateFixture.Id], result.유효의뢰Ids);
        Assert.Empty(result.갱신의뢰Ids); Assert.Empty(result.보류의뢰Ids);
        Assert.Equal(0, routes.Calls);
        Assert.Equal(before, JsonSerializer.Serialize(await f.Db.운송원장.AsNoTracking().SingleAsync()));
        Assert.Empty(f.Db.배차추천알림Outbox);
        Assert.Equal(0m, f.States.Current.Aging점수);
    }

    [Theory]
    [InlineData(5, true)]
    [InlineData(100, false)]
    public async Task R77_이동후에는실제일정검사를다시실행하고_새계획또는보류로남긴다(int routeMinutes, bool feasible)
    {
        await using var f = await R76StateFixture.CreateAsync();
        await R77Prepare(f, R76StateFixture.Id);
        await R77StoreOffers(f, R76StateFixture.Id);
        var before = await f.Db.운송원장.AsNoTracking().SingleAsync();
        f.States.Current = f.States.Current with { Latitude = 36.05m };
        var routes = new R77Routes { Minutes = routeMinutes };
        var result = await R77Refresh(f, routes).갱신Async("driver");
        var row = await f.Db.운송원장.AsNoTracking().SingleAsync();
        Assert.True(routes.Calls > 0);
        Assert.Equal(before.기사지급예정액, row.기사지급예정액);
        Assert.Equal(before.추천라운드 + 1, row.추천라운드);
        Assert.Empty(f.Db.배차추천알림Outbox);
        var envelope = JsonSerializer.Deserialize<국내화물추천저장근거>(row.추천판정근거Json!)!;
        if (feasible)
        {
            Assert.Equal([row.의뢰Id], result.갱신의뢰Ids);
            Assert.Equal("driver", row.현재추천대상기사Id);
            Assert.Equal(before.추천만료시각, row.추천만료시각);
            Assert.StartsWith("cargo-plan-", envelope.수행계획!.계획Id);
            Assert.Equal(2, envelope.수행계획.경로순서.Count);
            Assert.True(envelope.수행계획.구간검증결과!.적합여부);
            Assert.True(await f.Snapshots.현재근거확인Async(envelope.판정근거!));
            Assert.Equal(3m, envelope.수행계획.상하차작업시간[row.의뢰Id].상차분);
        }
        else
        {
            Assert.Equal([row.의뢰Id], result.보류의뢰Ids);
            Assert.Null(row.현재추천대상기사Id);
            Assert.Equal(상태값.배차노출상태.추천후보없음, row.배차노출상태);
            Assert.NotEqual("Recomputed", envelope.재검증상태Code);
        }
    }

    [Fact]
    public async Task R77_재계산도중추천소유라운드가달라지면_새소유자의원장을덮지않는다()
    {
        await using var f = await R76StateFixture.CreateAsync();
        await R77Prepare(f, R76StateFixture.Id); await R77StoreOffers(f, R76StateFixture.Id);
        f.States.Current = f.States.Current with { Latitude = 36.05m };
        var routes = new R77Routes();
        routes.DuringRoute = () =>
        {
            var row = f.Db.운송원장.Single(); row.현재추천대상기사Id = "other-driver"; row.추천라운드 = 9;
            f.Db.SaveChanges(); routes.DuringRoute = null;
        };
        var result = await R77Refresh(f, routes).갱신Async("driver");
        Assert.Empty(result.갱신의뢰Ids);
        var current = await f.Db.운송원장.AsNoTracking().SingleAsync();
        Assert.Equal("other-driver", current.현재추천대상기사Id); Assert.Equal(9, current.추천라운드);
        Assert.Equal(상태값.배차노출상태.추천중, current.배차노출상태);
    }

    [Fact]
    public async Task R77_정본작업시간이없으면_이동후과거계획의시간으로새확인을만들지않는다()
    {
        await using var f = await R76StateFixture.CreateAsync();
        await R77Prepare(f, R76StateFixture.Id);
        var request = await f.Db.화주운송의뢰.SingleAsync(); request.픽업_작업시간분 = null; request.하차_작업시간분 = null;
        await f.Db.SaveChangesAsync(); await R77StoreOffers(f, R76StateFixture.Id);
        f.States.Current = f.States.Current with { Latitude = 36.05m };
        var result = await R77Refresh(f, new()).갱신Async("driver");
        Assert.Empty(result.갱신의뢰Ids); Assert.Single(result.보류의뢰Ids);
        Assert.Null((await f.Db.운송원장.SingleAsync()).현재추천대상기사Id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("{}")]
    public async Task R77_검증근거없는구형직접추천은_새갱신에서회수하거나확인완료로승격하지않는다(string? legacyJson)
    {
        await using var f = await R76StateFixture.CreateAsync();
        await R77Prepare(f, R76StateFixture.Id); await R77StoreOffers(f, R76StateFixture.Id);
        var row = await f.Db.운송원장.SingleAsync(); row.추천판정근거Json = legacyJson; await f.Db.SaveChangesAsync();
        var before = JsonSerializer.Serialize(await f.Db.운송원장.AsNoTracking().SingleAsync());
        f.States.Current = f.States.Current with { Latitude = 36.05m };
        var result = await R77Refresh(f, new()).갱신Async("driver");
        Assert.Empty(result.유효의뢰Ids); Assert.Empty(result.갱신의뢰Ids); Assert.Empty(result.보류의뢰Ids);
        Assert.Equal(before, JsonSerializer.Serialize(await f.Db.운송원장.AsNoTracking().SingleAsync()));
    }

    [Fact]
    public async Task R77_이동경로재검사중신규수신을끄면_새계획저장과수락노출을보류한다()
    {
        await using var f = await R76StateFixture.CreateAsync();
        await R77Prepare(f, R76StateFixture.Id); await R77StoreOffers(f, R76StateFixture.Id);
        f.States.Current = f.States.Current with { Latitude = 36.05m };
        var routes = new R77Routes { DuringRoute = () => f.Availability.On = false };
        var result = await R77Refresh(f, routes).갱신Async("driver");
        Assert.Single(result.보류의뢰Ids); Assert.Empty(result.갱신의뢰Ids);
        var row = await f.Db.운송원장.AsNoTracking().SingleAsync();
        Assert.Null(row.현재추천대상기사Id); Assert.Equal(5000m, row.기사지급예정액);
        Assert.Empty(f.Db.배차추천알림Outbox);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(30, false)]
    public async Task R77_근거JSON없는일반큐수락도_정본작업시간을약속계산에사용한다(int pickupMinutes, bool possible)
    {
        await using var f = await R76StateFixture.CreateAsync(now: DateTime.UtcNow);
        await R77Prepare(f, R76StateFixture.Id);
        var request = await f.Db.화주운송의뢰.SingleAsync(); request.픽업_작업시간분 = pickupMinutes;
        await f.Db.SaveChangesAsync();
        var schedule = new 기사운송일정구성Service(f.Db); var routes = new R77Routes();
        var service = new 화물배차수락적격성Service(f.Db, new R77Location(f), new 차량화물적합성Service(new 차량적재추천Engine()),
            schedule, new 운송일정삽입평가Service(routes), f.Vehicle);
        var result = await service.평가Async("driver", request, [화물배차수락경고코드.차량주의사항]);
        Assert.Equal(possible, result.수락가능);
        if (!possible) Assert.Equal(화물배차수락오류코드.일정실패, result.오류코드);
    }

    [Fact]
    public async Task R77_첫수락을현재수행에포함해남은추천을재계산하고_구형클릭을거절한뒤연속수락한다()
    {
        await using var f = await R76StateFixture.CreateAsync(now: DateTime.UtcNow);
        const string secondId = "r77-second-request";
        await R77Prepare(f, R76StateFixture.Id); await R77Prepare(f, secondId);
        await R77StoreOffers(f, R76StateFixture.Id, secondId);
        var routes = new R77Routes(); var refresh = R77Refresh(f, routes);
        var handler = R77Handler(f, routes, refresh);
        var first = await handler.Handle(new("driver", R76StateFixture.Id, 1,
            [화물배차수락경고코드.차량주의사항]), default);
        Assert.True(first.IsSuccess, string.Join(";", first.Errors.Select(x => x.Message)));
        var remaining = await f.Db.운송원장.AsNoTracking().SingleAsync(x => x.의뢰Id == secondId);
        Assert.Equal(2, remaining.추천라운드);
        var plan = JsonSerializer.Deserialize<국내화물추천저장근거>(remaining.추천판정근거Json!)!.수행계획!;
        Assert.Contains(plan.경로순서, x => x.의뢰Id == R76StateFixture.Id);
        Assert.Contains(plan.경로순서, x => x.의뢰Id == secondId);
        var stale = await handler.Handle(new("driver", secondId, 1), default);
        Assert.True(stale.IsFailed);
        Assert.Equal(화물배차수락오류코드.추천판본불일치, Assert.Single(stale.Errors).Metadata["ErrorCode"]);
        var second = await handler.Handle(new("driver", secondId, remaining.추천라운드,
            [화물배차수락경고코드.차량주의사항]), default);
        Assert.True(second.IsSuccess, string.Join(";", second.Errors.Select(x => x.Message)));
        Assert.Equal(2, await f.Db.운송원장.CountAsync(x => x.배차큐단계 == 상태값.배차큐단계.확정));
        Assert.True((await handler.Handle(new("driver", R76StateFixture.Id, 1), default)).IsSuccess);
        Assert.Equal(2, await f.Db.Command알림Outbox.CountAsync());
        Assert.Empty(f.Db.기사운송대금지급요청);
    }

    private static 국내화물추천재검증Service R77Refresh(R76StateFixture f, R77Routes routes)
        => new(f.Db, f.Snapshots, f.States,
            new 국내화물배차계획검증Service(f.Db, new 기사운송일정구성Service(f.Db), new 운송일정삽입평가Service(routes),
                f.Vehicle, new 화물구간적재검증Service(new 차량적재추천Engine()), f.Snapshots),
            new SharedDriverWorkAdmissionService(f.Db, Options.Create(f.ParticipationOptions)), new R76Clock(f.Now));

    private static 배차수락CommandHandler R77Handler(R76StateFixture f, R77Routes routes, I국내화물추천재검증Service refresh)
    {
        var schedule = new 기사운송일정구성Service(f.Db); var insertion = new 운송일정삽입평가Service(routes);
        var location = new R77Location(f); var continuity = new R77Continuity();
        var eligibility = new 화물배차수락적격성Service(f.Db, location, new 차량화물적합성Service(new 차량적재추천Engine()),
            schedule, insertion, f.Vehicle);
        return new(f.Db, new RecordingPublisher(), new TestCurrentUserAccessor("driver", "기사"), new 참여자실행권한검사(),
            new WorkRelationshipSnapshotCollector(), eligibility, continuity, NullLogger<배차수락CommandHandler>.Instance,
            sharedAdmission: new SharedDriverWorkAdmissionService(f.Db, Options.Create(f.ParticipationOptions)),
            운영배차: f.Availability, 후속픽업: new 화물후속픽업기준Service(f.Db, schedule, insertion), 기사위치: location,
            신규업무자격: f.Qualification, 상태근거: f.Snapshots, 추천재검증: refresh);
    }

    private static async Task R77Prepare(R76StateFixture f, string id)
    {
        if (id != R76StateFixture.Id)
        {
            await QualificationSeed(f.Db, id);
            f.Db.운임구성.Add(new 운임구성 { 의뢰Id = id, 기사지급예정운임 = 5000, 최종운임 = 6000, UpdatedAt = f.Now });
            await f.Db.SaveChangesAsync();
        }
        var r = await f.Db.화주운송의뢰.SingleAsync(x => x.의뢰Id == id);
        r.운임구성Id = (await f.Db.운임구성.SingleAsync(x => x.의뢰Id == id)).Id;
        r.픽업_위도 = 36.01m; r.픽업_경도 = 127m; r.하차_위도 = 36.02m; r.하차_경도 = 127m;
        r.픽업_시간창_시작일시 = f.Now; r.픽업_시간창_종료일시 = f.Now.AddMinutes(25);
        r.하차_시간창_시작일시 = f.Now; r.하차_시간창_종료일시 = f.Now.AddHours(1);
        r.픽업_작업시간분 = 3; r.하차_작업시간분 = 2;
        r.화물수량 = 1; r.화물바닥회전가능여부 = true; r.화물적층가능여부 = true; r.화물부피Cbm = .006m;
        // 팔레트를 쓰지 않는 상온 상자라는 합성 시나리오다. 미확인 null을 엔진에서 0으로 바꾸지 않는다.
        r.화물팔레트개수 = 0; r.화물온도조건 = "상온";
        r.화주Id = "shipper"; r.주문자UserId = "shipper"; r.정산시점 = "운송완료후정산"; r.정산상태 = "후불승인완료";
        r.결제상태 = 상태값.결제상태.결제대기; r.배차상태 = 상태값.배차상태.매칭중;
        var q = await f.Db.운송원장.SingleAsync(x => x.의뢰Id == id);
        q.픽업_위도 = r.픽업_위도; q.픽업_경도 = r.픽업_경도; q.하차_위도 = r.하차_위도; q.하차_경도 = r.하차_경도;
        await f.Db.SaveChangesAsync(); f.Db.ChangeTracker.Clear();
    }

    private static async Task R77StoreOffers(R76StateFixture f, params string[] ids)
    {
        var evidence = new Dictionary<string, 국내화물배차판정근거>();
        foreach (var id in ids) evidence[id] = await f.Snapshots.조회Async(id, "driver", f.Now);
        var hash = 화물배차상태SnapshotService.지문(evidence.Select(x => x.Value.입력지문).ToArray());
        foreach (var id in ids)
        {
            var row = await f.Db.운송원장.SingleAsync(x => x.의뢰Id == id);
            row.배차큐단계 = 상태값.배차큐단계.배차추천; row.배차노출상태 = 상태값.배차노출상태.추천중;
            row.현재추천대상기사Id = "driver"; row.추천라운드 = 1; row.추천시작시각 = f.Now; row.추천만료시각 = f.Now.AddMinutes(5);
            row.기사지급예정액 = 5000;
            // 최초 제안은 명시 fixture다. 갱신은 실제 일정삽입/구간검증 Service를 실행한다.
            row.추천판정근거Json = JsonSerializer.Serialize(new 국내화물추천저장근거
            {
                판정근거 = evidence[id], 조율입력지문 = hash,
                수행계획 = new() { 계획Id = "explicit-r77-initial-plan", 기사Id = "driver", 의뢰Ids = ids,
                    확인됨 = true, 가능여부 = true, 입력지문 = hash,
                    상하차작업시간 = ids.ToDictionary(x => x, _ => new 화물상하차작업시간(3, 2), StringComparer.Ordinal) }
            });
        }
        await f.Db.SaveChangesAsync(); f.Db.ChangeTracker.Clear();
    }

    private sealed class R77Routes : I배차추천경로Service
    {
        public int Minutes { get; set; } = 5;
        public int Calls { get; private set; }
        public Action? DuringRoute { get; set; }
        public Task<배차경로예상결과?> EstimateRouteAsync(배차경로좌표? a, 배차경로좌표? b)
        { Calls++; DuringRoute?.Invoke(); return Task.FromResult<배차경로예상결과?>(new(1, TimeSpan.FromMinutes(Minutes), 0, "synthetic-provider", true) { RouteOption = "synthetic-car" }); }
        public Task<배차경로예상결과?> EstimateRouteAsync(배차경로좌표? a, 배차경로좌표? b, string option, CancellationToken ct) => EstimateRouteAsync(a, b);
        public decimal? CalculateDistanceKm(배차경로좌표 a, 배차경로좌표 b) => 1;
        public Task<배차경로좌표?> ResolveOriginLocationAsync(string id, 용달기사? d, DriverLocationSnapshot? l, 배차추천검색조건? c) => Task.FromResult<배차경로좌표?>(new(36, 127));
        public Task<배차경로좌표?> ResolveRouteAnchorLocationAsync(string id, 용달기사? d, DriverLocationSnapshot? l) => Task.FromResult<배차경로좌표?>(new(36, 127));
        public Task<배차경로예상결과?> EstimateOrderedRouteAsync(배차경로좌표? a, IReadOnlyList<배차경로좌표> b, CancellationToken ct = default) => EstimateRouteAsync(a, b.LastOrDefault());
        public Task<배차삽입경로예상결과?> EstimateInsertionDelayAsync(배차경로좌표? a, 배차경로좌표? b, 배차경로좌표? c, 배차경로좌표? d) => Task.FromResult<배차삽입경로예상결과?>(null);
    }

    private sealed class R77Location(R76StateFixture f) : IDriverLocationStore
    {
        public void Upsert(DriverLocationSnapshot value) => throw new NotSupportedException();
        public bool TryGetLatest(string id, out DriverLocationSnapshot value)
        { value = new(id, f.States.Current.Latitude!.Value, f.States.Current.Longitude!.Value, 5m, "운행중", f.Now, f.Now); return true; }
    }

    private sealed class R77Continuity : I화물연속배차UseCase
    {
        public Task 수락완료Async(string id, string request, CancellationToken ct = default) => Task.CompletedTask;
        public Task 시간약속잠금Async(string id, 화주운송의뢰 request, CancellationToken ct = default) => Task.CompletedTask;
        public Task<화물예약검증결과> 수락예약검증Async(string id, string request, string? reservation, long? revision, CancellationToken ct = default) => Task.FromResult(new 화물예약검증결과(true));
        public Task<화물연속배차상태Dto> 조회Async(string id, CancellationToken ct = default) => Task.FromResult(new 화물연속배차상태Dto { 기사Id = id });
        public Task<화물연속배차상태Dto> 의사변경Async(string id, 화물연속배차의사변경요청 value, CancellationToken ct = default) => 조회Async(id, ct);
        public Task<화물다음콜예약Dto?> 추천탐색Async(string id, CancellationToken ct = default) => Task.FromResult<화물다음콜예약Dto?>(null);
        public Task 완료기록Async(string id, DateTime time, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<화물운송시간약속Dto>> 시간약속목록Async(string id, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<화물운송시간약속Dto>>([]);
        public Task<화물경로위험Dto> 현재위험조회Async(string id, CancellationToken ct = default) => Task.FromResult(new 화물경로위험Dto());
    }
}
