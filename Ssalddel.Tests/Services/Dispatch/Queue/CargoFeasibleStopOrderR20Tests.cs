using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ssalddel.Services.LogisticsProcessing.VehicleLoading;
using Ssalddel.Tests.Services.Dispatch.Common;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.External.Opinet;
using 살뜰.Services.Storage.Local;
using 살뜰.도메인.공통;

namespace Ssalddel.Tests.Services.Dispatch.Queue;

// 같은 실제 큐 정책·일정 평가를 사용한다. 좌표·경로·실차 근거는 모두 합성 시험 자료다.
public sealed partial class CargoNextPickupR64Tests
{
    [Fact]
    public async Task R20_기사추천조회도_A완료뒤이어가기가불가능한_B우선순서를그대로제공한다()
    {
        await using var f = await PrepareDynamicOrderAsync();
        f.Queue.현재추천대상기사Id = "driver";
        f.Queue.배차노출상태 = 상태값.배차노출상태.추천중;
        f.Queue.추천시작시각 = DateTime.UtcNow;
        f.Queue.추천만료시각 = DateTime.UtcNow.AddMinutes(10);
        await f.Db.SaveChangesAsync();
        var service = new 화물배차추천Service(f.Db, new QueryLocations(), new Rejected(), null!, null!, null!, f.Routes,
            new 배차추천판정Service(), new Score(), new Fit(), new 기사운송일정구성Service(f.Db),
            new 운송일정삽입평가Service(f.Routes), new EmptyFuel(), null!, null!, new ReturnAreaOff(),
            운영배차: f.Availability, 후속픽업: f.Anchor(), queueOptions: Options.Create(new 배차큐정책Options()),
            신규업무자격: Ssalddel.Tests.Support.Simulation운송자격Guard.Instance);

        var recommendations = await service.GetDrivingRecommendationsAsync("driver");
        Assert.True(recommendations.Count > 0, await R20DiagnosticAsync(f));
        var result = Assert.Single(recommendations);

        Assert.Equal("next", result.의뢰Id);
        Assert.True(result.일정삽입가능여부);
        Assert.True(result.전체일정완수가능여부);
        Assert.Equal(["추천 상차 next", "추천 하차 next"], result.권장경로순서.Take(2));
        Assert.Equal(1m, result.픽업거리Km);
        Assert.Equal(0, f.Queue.추천라운드);
        Assert.Null(f.Queue.확정기사Id);
    }

    [Fact]
    public async Task R20_B먼저만약속을지키면_A끝점에서이어가기가늦어도_후보를선정한다()
    {
        await using var f = await PrepareDynamicOrderAsync();
        var request = await f.Db.화주운송의뢰.SingleAsync(x => x.의뢰Id == "next");
        var basis = await f.Anchor().조회Async("driver", new(37m, 127m), DateTime.UtcNow);
        Assert.True(basis.확인됨);
        var plan = WithCandidateHandling(basis.기존계획!);
        var evaluation = await new 운송일정삽입평가Service(f.Routes).평가Async(plan, request);
        Assert.False(Assert.Single(evaluation.시도목록.Where(x => x.시도유형 == "하차후이어가기")).전체완수가능여부);
        Assert.Contains(evaluation.시도목록, x => x.전체완수가능여부
            && x.경로순서.Take(2).SequenceEqual(["추천 상차 next", "추천 하차 next"]));

        var review = await f.Policy().후보검토Async(f.Queue);

        Assert.True(review.선정후보 is not null, await R20DiagnosticAsync(f, review));
        Assert.Equal("driver", review.선정후보?.DriverId);
        var selected = Assert.Single(review.후보목록);
        Assert.Equal("CandidateAllowed", selected.판정Code);
        Assert.Equal(1m, selected.거리Km);
        Assert.NotNull(selected.픽업도착예상시각Utc);
        Assert.True(selected.픽업도착예상시각Utc < basis.가용예상시각Utc);
        Assert.True(selected.픽업도착예상시각Utc <= request.픽업_시간창_종료일시);
        Assert.Equal(0, f.Queue.추천라운드);
        Assert.Equal(상태값.배차대기상태.대기, f.Queue.상태);
    }

    [Fact]
    public async Task R20_B먼저는A약속을어기고_A먼저는B약속을어기면_후보를선정하지않는다()
    {
        await using var f = await PrepareDynamicOrderAsync();
        (await f.Db.화주운송의뢰.SingleAsync(x => x.의뢰Id == "active-1"))
            .하차_시간창_종료일시 = DateTime.UtcNow.AddMinutes(4);
        await f.Db.SaveChangesAsync();

        Assert.Null(await f.Policy().다음후보선정Async(f.Queue));
        Assert.Equal(0, f.Queue.추천라운드);
    }

    [Theory]
    [InlineData("approximate")]
    [InlineData("missing-route")]
    [InlineData("mixed-option")]
    [InlineData("candidate-pickup-time")]
    [InlineData("candidate-dropoff-time")]
    [InlineData("existing-handling-time")]
    [InlineData("candidate-handling-time")]
    [InlineData("candidate-pickup-handling-late")]
    [InlineData("candidate-dropoff-handling-late")]
    [InlineData("cargo-weight")]
    [InlineData("over-capacity")]
    [InlineData("exclusive")]
    [InlineData("vehicle-spec")]
    public async Task R20_시간상유리해도_같은삽입순서의확인근거와적재조건이부족하면보류한다(string condition)
    {
        await using var f = await PrepareDynamicOrderAsync();
        var candidate = await f.Db.화주운송의뢰.SingleAsync(x => x.의뢰Id == "next");
        var active = await f.Db.화주운송의뢰.SingleAsync(x => x.의뢰Id == "active-1");
        if (condition == "approximate") f.Routes.Actual = false;
        if (condition == "missing-route") f.Routes.Fail = true;
        if (condition == "mixed-option") f.Routes.ExplicitOptionMismatch = true;
        if (condition == "candidate-pickup-time") candidate.픽업_시간창_종료일시 = default;
        if (condition == "candidate-dropoff-time") candidate.하차_시간창_종료일시 = null;
        if (condition == "existing-handling-time") active.하차_작업시간분 = null;
        if (condition == "candidate-handling-time") candidate.픽업_작업시간분 = null;
        if (condition == "candidate-pickup-handling-late") candidate.픽업_작업시간분 = 5m;
        if (condition == "candidate-dropoff-handling-late") candidate.하차_작업시간분 = 5m;
        if (condition == "cargo-weight") candidate.화물중량Kg = null;
        if (condition == "over-capacity")
        {
            foreach (var request in await f.Db.화주운송의뢰.ToListAsync()) request.화물중량Kg = 40m;
        }
        if (condition == "exclusive") active.운송방식 = "독차";
        if (condition == "vehicle-spec") f.Db.차량제원.RemoveRange(await f.Db.차량제원.ToListAsync());
        await f.Db.SaveChangesAsync();

        var review = await f.Policy().후보검토Async(f.Queue);

        Assert.Null(review.선정후보);
        Assert.DoesNotContain(review.후보목록, x => x.판정Code == "CandidateAllowed");
        Assert.Equal(0, f.Queue.추천라운드);
        Assert.Null(f.Queue.확정기사Id);
    }

    [Fact]
    public async Task R20_합산중량이차량한도와정확히같으면_B우선삽입을허용한다()
    {
        await using var f = await PrepareDynamicOrderAsync();
        foreach (var request in await f.Db.화주운송의뢰.Where(x => x.의뢰Id != "next").ToListAsync())
            request.화물중량Kg = 40m;
        (await f.Db.화주운송의뢰.SingleAsync(x => x.의뢰Id == "next")).화물중량Kg = 20m;
        await f.Db.SaveChangesAsync();

        var review = await f.Policy().후보검토Async(f.Queue);
        Assert.True(review.선정후보 is not null, await R20DiagnosticAsync(f, review));
        Assert.Equal("driver", review.선정후보?.DriverId);
    }

    private static async Task<Fixture> PrepareDynamicOrderAsync()
    {
        var f = await Fixture.CreateAsync();
        var now = DateTime.UtcNow;
        var request = await f.Db.화주운송의뢰.SingleAsync(x => x.의뢰Id == "next");
        f.Queue.픽업_위도 = request.픽업_위도 = 37.01m;
        f.Queue.하차_위도 = request.하차_위도 = 37.02m;
        request.픽업_시간창_종료일시 = now.AddMinutes(4);
        request.하차_시간창_종료일시 = now.AddMinutes(8);
        foreach (var active in await f.Db.화주운송의뢰.Where(x => x.의뢰Id != "next").ToListAsync())
            active.하차_시간창_종료일시 = now.AddMinutes(30);
        await f.Db.SaveChangesAsync();
        return f;
    }

    private static 기사운송일정계획 WithCandidateHandling(기사운송일정계획 plan)
    {
        var times = plan.상하차작업시간?.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal)
            ?? new Dictionary<string, 화물상하차작업시간>(StringComparer.Ordinal);
        times["next"] = new(1m, 1m);
        return plan with { 상하차작업시간 = times };
    }

    private static async Task<string> R20DiagnosticAsync(Fixture f, 용달운송후보검토결과? review = null)
    {
        var basis = await f.Anchor().조회Async("driver", new(37m, 127m), DateTime.UtcNow);
        var vehicle = await new 기사실차적재근거조회Service(f.Db).조회Async("driver");
        var request = await f.Db.화주운송의뢰.SingleAsync(x => x.의뢰Id == "next");
        var insertion = await new 화물운행중삽입평가Service(f.Db, f.Routes, new 운송일정삽입평가Service(f.Routes))
            .검토Async("driver", basis, request, vehicle.유효제원);
        var schedule = basis.기존계획 is null ? null
            : await new 운송일정삽입평가Service(f.Routes).평가Async(WithCandidateHandling(basis.기존계획), request);
        var jobs = (await f.Db.화주운송의뢰.ToListAsync()).Select(x => new 화물구간적재작업(x, null, x.의뢰Id != "next")).ToArray();
        var capacities = schedule?.시도목록.Where(x => x.전체완수가능여부).Select(attempt =>
        {
            var stops = attempt.도착예상목록.Select(x => new 기사운송일정항목(x.의뢰Id, x.단계유형, "", null, null,
                x.시간창종료일시, x.순서, null, true, x.의뢰Id == "next")).ToArray();
            var capacity = new 화물구간적재검증Service(new 차량적재추천Engine()).평가(vehicle.유효제원, jobs, stops);
            return new { attempt.시도유형, attempt.경로순서, capacity.사유Code, capacity.사유 };
        }).ToArray();
        return System.Text.Json.JsonSerializer.Serialize(new { review, basis.사유Code, basis.확인됨, vehicle, insertion, capacities });
    }

    private sealed class EmptyFuel : IOpinetAveragePriceService
    {
        public Task<IReadOnlyList<OpinetAveragePriceItem>> GetAveragePricesAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<OpinetAveragePriceItem>>([]);
    }

    private sealed class ReturnAreaOff : I기사권역복귀Service
    {
        public Task<기사권역복귀상태> 상태조회Async(string id, 배차경로좌표? point, DateTime? recorded,
            decimal? accuracy = null, CancellationToken ct = default)
            => Task.FromResult(new 기사권역복귀상태(false, false, null, null, 0, "SyntheticPolicyOff"));
        public Task<기사권역복귀평가> 평가Async(기사권역복귀상태 state, 배차경로좌표? before,
            배차경로좌표? after, bool verified, bool eligible, string? option = null, CancellationToken ct = default)
            => Task.FromResult(기사권역복귀평가.보류("SyntheticPolicyOff"));
    }

    private sealed class QueryLocations : IDriverLocationStore
    {
        public bool TryGetLatest(string id, out DriverLocationSnapshot item)
        {
            var now = DateTime.UtcNow;
            item = new(id, 37m, 127m, 1m, 상태값.기사운행상태.운행중, now, now);
            return true;
        }
        public void Upsert(DriverLocationSnapshot item) => throw new NotSupportedException();
    }
}
