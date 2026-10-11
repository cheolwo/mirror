using Ssalddel.Contracts.Common.Dispatch;
using 살뜰.Services.Dispatch.AI;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.Storage.Local;
using 살뜰.도메인.공통;
using 살뜰.도메인.기사;
using 살뜰.도메인.배차;
using 살뜰.도메인.운송;

namespace Ssalddel.Tests.Services.Dispatch.AI;

public sealed class 음식권역배차관리ServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 11, 8, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(1, "ExistingCapacitySufficient", 0)]
    [InlineData(12, "AdditionalParticipationNeeded", 2)]
    public async Task 실제수요Policy의부족과과잉참여판정및우선순위를변경하지않는다(int pending, string action, int admissions)
    {
        var demand = Demand(pending);
        var before = System.Text.Json.JsonSerializer.Serialize(demand);
        var ai = new AIPort();
        var result = await Service(ai).검토자료Async(demand, Request());

        Assert.Equal(action, result.수요ActionCode);
        Assert.Equal(admissions, result.수요판정!.추가참여인원);
        Assert.Equal(demand.기사판정목록.Where(x => x.신규제안허용).OrderBy(x => x.우선순위).Select(x => x.기사Id), result.제안허용기사Ids);
        Assert.Equal(before, System.Text.Json.JsonSerializer.Serialize(demand));
        Assert.Equal("Applied", result.AIStatusCode);
        Assert.Equal(1, ai.Calls);
        Assert.False(result.OperationalApplied);
        Assert.True(result.RequiresCurrentGates);
        Assert.False(result.실제적재검증됨);
        Assert.False(result.전체약속시간검증됨);
    }

    [Fact]
    public async Task 오래기다린신규기사의기회도기존수요Policy결과그대로보존한다()
    {
        var settings = new 음식배달수요참여설정 { 장기대기기회분 = 30m, 장기대기추가인원 = 1, 주문지연추가인원 = 0 };
        var demand = new 음식배달수요참여Policy().평가(Input(1), settings);
        var result = await Service(new AIPort()).검토자료Async(demand, Request());
        Assert.Equal("LongWaitOpportunity", demand.기사판정목록[0].사유Code);
        Assert.Equal("new-older", result.제안허용기사Ids[0]);
        Assert.Equal("AdditionalParticipationNeeded", result.수요ActionCode);
        Assert.DoesNotContain("new-near", result.제안허용기사Ids);
    }

    [Fact]
    public async Task 서버진입은넘겨받은음식원장의실제수요조회포트를호출한다()
    {
        var port = new DemandPort(Demand());
        var ai = new AIPort();
        var service = Service(ai, demand: port);
        var queue = new 운송원장 { 의뢰Id = "queue", 배차업무유형 = 상태값.배차업무유형.음식배달 };
        var result = await service.검토Async(queue, Request());
        Assert.Same(queue, port.Queue);
        Assert.Same(port.Value, result.수요판정);
        Assert.Equal(1, port.Calls);
    }

    [Fact]
    public async Task 화물원장은수요조회와조합생성과모델을호출하지않는다()
    {
        var demand = new DemandPort(Demand());
        var bundle = new BundlePort([]);
        var ai = new AIPort();
        var result = await Service(ai, bundle, demand).검토Async(new 운송원장
            { 배차업무유형 = 상태값.배차업무유형.용달운송 }, Request());
        Assert.Equal("NotFood", result.AIStatusCode);
        Assert.Equal(0, demand.Calls);
        Assert.Equal(0, bundle.Calls);
        Assert.Equal(0, ai.Calls);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("unknown")]
    [InlineData("stale")]
    [InlineData("future")]
    [InlineData("version")]
    [InlineData("duplicate-driver")]
    [InlineData("duplicate-priority")]
    public async Task 미확인또는오래되거나충돌한수요는모델에보내지않는다(string variation)
    {
        음식배달수요참여결과? demand = Demand();
        switch (variation)
        {
            case "missing": demand = null; break;
            case "unknown": demand.데이터상태Code = 음식배달수요자료상태Code.미확인; break;
            case "stale": demand.판정시각Utc = Now.AddMinutes(-6); break;
            case "future": demand.판정시각Utc = Now.AddSeconds(1); break;
            case "version": demand.정책판본 = "unapproved"; break;
            case "duplicate-driver": demand.기사판정목록 = [demand.기사판정목록[0], demand.기사판정목록[0]]; break;
            case "duplicate-priority": demand.기사판정목록[1].우선순위 = demand.기사판정목록[0].우선순위; break;
        }
        var ai = new AIPort();
        var result = await Service(ai).검토자료Async(demand, Request());
        Assert.Equal("DemandUnknown", result.수요ActionCode);
        Assert.Equal("DemandUnknown", result.AIStatusCode);
        Assert.Same(result.기준조합, result.선택조합);
        Assert.Empty(result.제안허용기사Ids);
        Assert.Equal(0, ai.Calls);
    }

    [Fact]
    public async Task 제안허용기사가없으면새로만들거나AI로참여를승격하지않는다()
    {
        var demand = Demand();
        foreach (var driver in demand.기사판정목록) driver.신규제안허용 = false;
        var ai = new AIPort();
        var result = await Service(ai).검토자료Async(demand, Request());
        Assert.Equal("NoParticipatingDriver", result.AIStatusCode);
        Assert.Null(result.선택조합);
        Assert.Equal(0, ai.Calls);
    }

    [Fact]
    public async Task 기존생성기의단건과2건조합만사용하고선택한유효대안을반환한다()
    {
        var ai = new AIPort(review => new(review.유효조합목록.Last().조합키, "Applied"));
        var result = await Service(ai).검토자료Async(Demand(), Request() with { 최대묶음크기 = 4 });
        Assert.Equal(3, result.생성조합목록.Count);
        Assert.All(result.유효조합목록, x => Assert.InRange(x.작업목록.Count, 1, 2));
        Assert.Equal(result.유효조합목록.Last().조합키, result.선택조합!.조합키);
        Assert.Equal("AdditionalParticipationNeeded", (await Service(ai).검토자료Async(Demand(12), Request())).수요ActionCode);
    }

    [Fact]
    public async Task 제외된거리조합은AI의선택목록에넣지않는다()
    {
        var jobs = Request().작업목록.ToArray();
        jobs[1] = jobs[1] with { 픽업좌표 = new(37.70m, 127.20m), 하차좌표 = new(37.71m, 127.21m) };
        var ai = new AIPort();
        var result = await Service(ai).검토자료Async(Demand(), Request() with { 작업목록 = jobs });
        Assert.Contains(result.생성조합목록, x => !x.조합가능여부);
        Assert.All(ai.Review!.유효조합목록, x => Assert.True(x.조합가능여부));
        Assert.DoesNotContain(ai.Review.유효조합목록, x => x.작업목록.Count == 2);
    }

    [Fact]
    public async Task 입력에없는주문이나중복조합은복원하지않는다()
    {
        var request = Request();
        var original = new 음식멀티배차조합Service(new RoutePort()).조합생성(request);
        var bad = original[0] with { 조합키 = "invented", 의뢰Ids = ["not-in-input"] };
        var duplicate = original[1];
        var port = new BundlePort([bad, duplicate, duplicate, original[2]]);
        var ai = new AIPort();
        var result = await Service(ai, port).검토자료Async(Demand(), request);
        Assert.Single(result.유효조합목록);
        Assert.Equal(original[2].조합키, result.선택조합!.조합키);
        Assert.Equal("SinglePlan", result.AIStatusCode);
        Assert.Equal(0, ai.Calls);
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("empty")]
    [InlineData("too-many")]
    public async Task 불명확하거나너무큰원요청은생성기까지보내지않는다(string variation)
    {
        var job = Request().작업목록[0];
        var request = Request() with { 작업목록 = variation switch
        {
            "duplicate" => [job, job], "empty" => [job with { 의뢰Id = "" }],
            _ => Enumerable.Range(0, 13).Select(x => job with { 의뢰Id = $"job-{x}" }).ToArray()
        } };
        var port = new BundlePort([]);
        var ai = new AIPort();
        var result = await Service(ai, port).검토자료Async(Demand(), request);
        Assert.Equal("InvalidBundleRequest", result.AIStatusCode);
        Assert.Equal(0, port.Calls);
        Assert.Equal(0, ai.Calls);
    }

    [Fact]
    public async Task 잘못된AI선택은원래규칙조합으로복귀한다()
    {
        var ai = new AIPort(_ => new("invented-plan", "Applied"));
        var result = await Service(ai).검토자료Async(Demand(), Request());
        Assert.Equal("InvalidSelection", result.AIStatusCode);
        Assert.Same(result.기준조합, result.선택조합);
    }

    [Theory]
    [InlineData("HttpFailure")]
    [InlineData("Timeout")]
    public async Task 모델실패는기준조합과기존참여판정을보존한다(string status)
    {
        var ai = new AIPort(_ => new(null, status));
        var result = await Service(ai).검토자료Async(Demand(), Request());
        Assert.Equal(status, result.AIStatusCode);
        Assert.Same(result.기준조합, result.선택조합);
        Assert.NotEmpty(result.제안허용기사Ids);
    }

    [Fact]
    public async Task 비활성화된모델과단일조합은추론하지않는다()
    {
        var ai = new AIPort { Enabled = false };
        Assert.Equal("Disabled", (await Service(ai).검토자료Async(Demand(), Request())).AIStatusCode);
        ai.Enabled = true;
        var request = Request() with { 작업목록 = [Request().작업목록[0]] };
        Assert.Equal("SinglePlan", (await Service(ai).검토자료Async(Demand(), request)).AIStatusCode);
        Assert.Equal(0, ai.Calls);
    }

    [Fact]
    public async Task 호출자취소는추론이나수요조회전에전달한다()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var ai = new AIPort();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Service(ai).검토자료Async(Demand(), Request(), cancellation.Token));
        Assert.Equal(0, ai.Calls);
    }

    private static 음식권역배차관리Service Service(AIPort ai, I음식멀티배차조합Service? bundle = null, DemandPort? demand = null)
        => new(demand ?? new DemandPort(Demand()), bundle ?? new 음식멀티배차조합Service(new RoutePort()), ai, new Clock());
    private static 음식배달수요참여결과 Demand(int pending = 1)
        => new 음식배달수요참여Policy().평가(Input(pending), new() { 장기대기추가인원 = 0, 주문지연추가인원 = 0 });
    private static 음식배달수요참여입력 Input(int pending) => new()
    {
        권역Key = "PRIVATE-REGION", 판정시각Utc = Now, 수요관측시각Utc = Now,
        대기주문수 = pending, 최근시간창신규주문수 = 0, 관측시간창분 = 15m,
        기사목록 = [Driver("active-older", true, 40), Driver("active-near", true, 5),
            Driver("new-older", false, 50), Driver("new-near", false, 1)]
    };
    private static 음식배달수요기사입력 Driver(string id, bool active, int wait) => new()
    {
        기사Id = id, 기존활동여부 = active, 운행시작시각Utc = Now.AddMinutes(-60), 대기시작시각Utc = Now.AddMinutes(-wait),
        마지막관측시각Utc = Now, 남은수행시간분 = 0m, 최근시간창배차수 = active ? 1 : 0, 적격여부 = true
    };
    private static 멀티배차조합요청 Request() => new([
        new("PRIVATE-ORDER-A", "PRIVATE-PICKUP-A", new(37.59m, 127.08m), Now.UtcDateTime.AddMinutes(15),
            "PRIVATE-DROPOFF-A", new(37.592m, 127.082m), Now.UtcDateTime.AddMinutes(40), 하차배달권키: "bjd-sigungu:11260"),
        new("PRIVATE-ORDER-B", "PRIVATE-PICKUP-B", new(37.591m, 127.081m), Now.UtcDateTime.AddMinutes(17),
            "PRIVATE-DROPOFF-B", new(37.593m, 127.083m), Now.UtcDateTime.AddMinutes(42), 하차배달권키: "bjd-sigungu:11260")]);
    private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => Now; }
    private sealed class DemandPort(음식배달수요참여결과? value) : I음식배달수요참여Service
    {
        public 음식배달수요참여결과? Value => value;
        public int Calls { get; private set; }
        public 운송원장? Queue { get; private set; }
        public Task<음식배달수요참여결과?> 평가Async(운송원장 queue, CancellationToken cancellationToken = default)
        { Calls++; Queue = queue; return Task.FromResult(value); }
    }
    private sealed class AIPort(Func<음식권역배차AI검토, 음식권역배차AI선택>? select = null) : I음식권역배차AI
    {
        public bool Enabled { get; set; } = true;
        public int Calls { get; private set; }
        public 음식권역배차AI검토? Review { get; private set; }
        public Task<음식권역배차AI선택> 선택Async(음식권역배차AI검토 review, CancellationToken cancellationToken = default)
        { Calls++; Review = review; return Task.FromResult(select?.Invoke(review) ?? new(review.유효조합목록[0].조합키, "Applied")); }
    }
    private sealed class BundlePort(IReadOnlyList<멀티배차조합후보> values) : I음식멀티배차조합Service
    {
        public int Calls { get; private set; }
        public IReadOnlyList<멀티배차조합후보> 조합생성(멀티배차조합요청 request) { Calls++; return values; }
    }
    private sealed class RoutePort : I배차추천경로Service
    {
        public decimal? CalculateDistanceKm(배차경로좌표 source, 배차경로좌표 target)
            => (Math.Abs(target.Latitude - source.Latitude) + Math.Abs(target.Longitude - source.Longitude)) * 100m;
        public Task<배차경로좌표?> ResolveOriginLocationAsync(string driverId, 용달기사? driver, DriverLocationSnapshot? currentLocation, 배차추천검색조건? criteria)
            => throw new NotSupportedException();
        public Task<배차경로좌표?> ResolveRouteAnchorLocationAsync(string driverId, 용달기사? driver, DriverLocationSnapshot? currentLocation)
            => throw new NotSupportedException();
        public Task<배차경로예상결과?> EstimateRouteAsync(배차경로좌표? origin, 배차경로좌표? destination)
            => throw new NotSupportedException();
        public Task<배차경로예상결과?> EstimateOrderedRouteAsync(배차경로좌표? origin, IReadOnlyList<배차경로좌표> orderedStops, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<배차삽입경로예상결과?> EstimateInsertionDelayAsync(배차경로좌표? origin, 배차경로좌표? routeAnchor, 배차경로좌표? pickup, 배차경로좌표? dropoff)
            => throw new NotSupportedException();
    }
}
