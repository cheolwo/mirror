using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ssalddel.Services.LogisticsProcessing.VehicleLoading;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Dispatch.Coordination;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.External.Opinet;
using 살뜰.Services.Options;
using 살뜰.Services.Storage.Local;
using 살뜰.도메인.공통;
using 살뜰.도메인.기사;
using 살뜰.도메인.차량;
using 살뜰.도메인.화물;
using 살뜰.도메인.화주;

namespace Ssalddel.Tests.Services.Dispatch.Recommendation;

public sealed class CargoReturnAreaTests
{
    [Fact]
    public async Task 분리삽입은_후보하차지가아닌_전체일정끝점을보존한다()
    {
        var now = DateTime.UtcNow;
        var plan = new 기사운송일정계획("driver-1", now, new(0m, 0m),
        [
            Stop("existing", "pickup", 1m, 0, now),
            Stop("existing", "dropoff", 10m, 1, now)
        ]);
        var result = await new 운송일정삽입평가Service(new FixtureRoutes()).평가Async(plan, Request("candidate", 2m, 3m));

        Assert.True(result.삽입가능여부);
        Assert.True(result.경로변경이점여부);
        Assert.Equal(new 배차경로좌표(10m, 0m), result.기존일정종료좌표);
        Assert.Equal(result.기존일정종료좌표, result.후보포함종료좌표);
        Assert.Equal("기존 하차 existing", result.권장경로순서[^1]);
        Assert.True(result.기존일정실제경로확인됨);
        Assert.True(result.후보포함실제경로확인됨);
        Assert.Equal("traoptimal", result.복귀비교경로옵션);
    }

    [Fact]
    public async Task 기존업무없는계획의_기준끝점은_실제시작위치다()
    {
        var plan = new 기사운송일정계획("driver-1", DateTime.UtcNow, new(8m, 0m), []);
        var result = await new 운송일정삽입평가Service(new FixtureRoutes()).평가Async(plan, Request("candidate", 7m, 5m));

        Assert.True(result.삽입가능여부);
        Assert.Equal(plan.시작좌표, result.기존일정종료좌표);
        Assert.Equal(new 배차경로좌표(5m, 0m), result.후보포함종료좌표);
        Assert.True(result.기존일정실제경로확인됨);
        Assert.True(result.후보포함실제경로확인됨);
    }

    [Fact]
    public async Task 기존계획이이미복귀할때_현재위치보다가까운후보라도_계획대비악화를구분한다()
    {
        var now = DateTime.UtcNow;
        var plan = new 기사운송일정계획("driver-1", now, new(8m, 0m), [Stop("existing", "dropoff", 4m, 0, now)]);
        var result = await new 운송일정삽입평가Service(new FixtureRoutes()).평가Async(plan, Request("candidate", 5m, 6m));
        var area = new CapturingReturnArea(new(true, true, 8m, new(0m, 0m), 1, "fixture"));
        var comparison = await area.평가Async(area.State, result.기존일정종료좌표, result.후보포함종료좌표,
            result.기존일정실제경로확인됨 && result.후보포함실제경로확인됨,
            result.삽입가능여부 && result.전체완수가능여부);

        Assert.Equal(new 배차경로좌표(4m, 0m), result.기존일정종료좌표);
        Assert.Equal(new 배차경로좌표(6m, 0m), result.후보포함종료좌표);
        Assert.Equal(0m, comparison.가점);
    }

    [Fact]
    public async Task 후보좌표가누락되면_일정적격성과복귀경로확인을인증하지않는다()
    {
        var plan = new 기사운송일정계획("driver-1", DateTime.UtcNow, new(8m, 0m), []);
        var request = Request("candidate", 7m, 5m);
        request.하차_위도 = null;
        var result = await new 운송일정삽입평가Service(new FixtureRoutes()).평가Async(plan, request);

        Assert.False(result.삽입가능여부);
        Assert.False(result.전체완수가능여부);
        Assert.False(result.후보포함실제경로확인됨);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    public async Task 근사경로나거리옵션누락은_기존일정판정을바꾸지않고_실경로확인을보류한다(bool actual, bool missingDistance, bool missingOption)
    {
        var now = DateTime.UtcNow;
        var routes = new FixtureRoutes { Actual = actual, MissingDistance = missingDistance, MissingOption = missingOption };
        var plan = new 기사운송일정계획("driver-1", now, new(0m, 0m), [Stop("existing", "dropoff", 4m, 0, now)]);
        var result = await new 운송일정삽입평가Service(routes).평가Async(plan, Request("candidate", 5m, 6m));

        Assert.True(result.삽입가능여부);
        Assert.True(result.전체완수가능여부);
        Assert.Equal(new 배차경로좌표(4m, 0m), result.기존일정종료좌표);
        Assert.False(result.기존일정실제경로확인됨);
        Assert.False(result.후보포함실제경로확인됨);
    }

    [Fact]
    public async Task 경로옵션이섞인계획은_복귀비교실경로로인증하지않는다()
    {
        var now = DateTime.UtcNow;
        var routes = new FixtureRoutes { MixedOptions = true };
        var plan = new 기사운송일정계획("driver-1", now, new(0m, 0m), [Stop("existing", "dropoff", 4m, 0, now)]);
        var result = await new 운송일정삽입평가Service(routes).평가Async(plan, Request("candidate", 2m, 3m));

        Assert.True(result.삽입가능여부);
        Assert.False(result.후보포함실제경로확인됨);
    }

    [Fact]
    public async Task 활성복귀우대는_열한번째후보를평가하고_평가군40과최종10을지킨다()
    {
        await using var fixture = await Fixture.CreateAsync(50);
        var result = await fixture.Recommendations().GetDrivingRecommendationsAsync("driver-1");

        Assert.Equal(10, result.Count);
        Assert.Equal("candidate-11", result[0].의뢰Id);
        Assert.Equal(40, fixture.ReturnArea.Evaluations.Count);
        Assert.DoesNotContain(fixture.ReturnArea.Evaluations, x => x.Candidate == new 배차경로좌표(0.006m, 0m));
        Assert.Equal(20m, result[0].권역복귀가점);
        Assert.Equal(10m, result[0].전체일정기존복귀거리Km);
        Assert.Equal(7m, result[0].전체일정후보복귀거리Km);
        Assert.All(fixture.Score.ReturnBasisFlags, Assert.False);
    }

    [Fact]
    public async Task 꺼진정책은_기존거리순10과기존복귀항목을보존한다()
    {
        await using var fixture = await Fixture.CreateAsync(12, selected: false, active: false);
        var result = await fixture.Recommendations().GetDrivingRecommendationsAsync("driver-1");

        Assert.Equal(Enumerable.Range(1, 10).Select(Id), result.Select(x => x.의뢰Id));
        Assert.Empty(fixture.ReturnArea.Evaluations);
        Assert.All(fixture.Score.ReturnBasisFlags, Assert.True);
        Assert.All(result, x =>
        {
            Assert.Equal(20m, x.추천점수);
            Assert.Null(x.권역복귀정책선택됨);
            Assert.Null(x.전체일정기존복귀거리Km);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 선택한정책이비활성이나미확인이어도_기존복귀가점으로되돌리지않는다(bool unknown)
    {
        await using var fixture = await Fixture.CreateAsync(1, active: false);
        if (unknown) fixture.ReturnArea.State = fixture.ReturnArea.State with { 기준점 = null, 사유 = "위치 미확인" };
        var result = Assert.Single(await fixture.Recommendations().GetDrivingRecommendationsAsync("driver-1"));

        Assert.True(result.권역복귀정책선택됨);
        Assert.False(result.권역복귀활성여부);
        Assert.False(result.권역복귀확인됨);
        Assert.Equal(0m, result.추천점수);
        Assert.All(fixture.Score.ReturnBasisFlags, Assert.False);
    }

    [Fact]
    public async Task 검색중심은_우대진입과기존전체일정끝점에_현재GPS로대입하지않는다()
    {
        await using var fixture = await Fixture.CreateAsync(2);
        var criteria = new 배차추천검색조건(0.012m, 0m, 50m);
        await fixture.Recommendations().GetIdleRecommendationsAsync("driver-1", criteria);

        Assert.Equal(new 배차경로좌표(0.010m, 0m), fixture.ReturnArea.LastPosition);
        Assert.All(fixture.ReturnArea.Evaluations, x => Assert.Equal(fixture.ReturnArea.LastPosition, x.Baseline));
        Assert.NotEqual(new 배차경로좌표(criteria.Latitude, criteria.Longitude), fixture.ReturnArea.LastPosition);
    }

    [Fact]
    public async Task 복귀우대도_검색반경과거절및기사노출범위를넘지않는다()
    {
        await using var fixture = await Fixture.CreateAsync(12);
        fixture.Rejected.Ids.Add(Id(1));
        var foreign = await fixture.Db.운송원장.SingleAsync(x => x.의뢰Id == Id(3));
        foreign.현재추천대상기사Id = "other-driver";
        await fixture.Db.SaveChangesAsync();
        var result = await fixture.Recommendations().GetDrivingRecommendationsAsync("driver-1", new(0.010m, 0m, 0.55m));

        Assert.Equal(3, result.Count);
        Assert.DoesNotContain(result, x => x.의뢰Id is "candidate-1" or "candidate-3" or "candidate-11");
        Assert.Equal(3, fixture.ReturnArea.Evaluations.Count);
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public async Task 다른업무나적재부적합이나시간위반은_복귀가점을받지않는다(bool otherWork, bool badFit, bool late)
    {
        await using var fixture = await Fixture.CreateAsync(11);
        if (otherWork)
        {
            fixture.Db.운송원장.Add(new()
            {
                의뢰Id = "food-active", 운송번호 = "food-active", 기사_운송자 = "driver-1",
                배차업무유형 = 상태값.배차업무유형.음식배달,
                배차큐단계 = 상태값.배차큐단계.확정, 상태 = "픽업완료"
            });
        }
        fixture.Fit.Allowed = !badFit;
        if (late)
        {
            var request = await fixture.Db.화주운송의뢰.SingleAsync(x => x.의뢰Id == Id(11));
            request.픽업_시간창_종료일시 = DateTime.UtcNow.AddMinutes(-1);
        }
        await fixture.Db.SaveChangesAsync();
        var result = await fixture.Recommendations().GetDrivingRecommendationsAsync("driver-1");

        Assert.All(result, x => Assert.Equal(0m, x.권역복귀가점));
        if (otherWork) Assert.All(fixture.ReturnArea.Evaluations, x => Assert.False(x.Verified));
        if (badFit) Assert.All(fixture.ReturnArea.Evaluations, x => Assert.False(x.Eligible));
        if (late) Assert.Contains(fixture.ReturnArea.Evaluations, x => x.Candidate == new 배차경로좌표(0.007m, 0m) && !x.Eligible);
    }

    [Fact]
    public async Task 조율은_선택한복귀정책을점수에한번반영하고_기존부담비용을끄며_기사별상태를재사용한다()
    {
        await using var fixture = await Fixture.CreateAsync(2);
        foreach (var queue in fixture.Db.운송원장)
            queue.현재추천대상기사Id = null;
        var first = await fixture.Db.화주운송의뢰.SingleAsync(x => x.의뢰Id == Id(1));
        first.하차_위도 = 0.007m;
        var queueOne = await fixture.Db.운송원장.SingleAsync(x => x.의뢰Id == Id(1));
        queueOne.하차_위도 = 0.007m;
        await fixture.Db.SaveChangesAsync();

        var active = await fixture.Factory().생성Async(new() { 기사Ids = ["driver-1"] });
        Assert.Equal(2, active.조합평가목록.Count);
        Assert.All(active.조합평가목록, x => Assert.Equal(0m, x.복귀시간대부담점수));
        Assert.Equal(1, fixture.ReturnArea.StateReads);
        var activeScore = active.조합평가목록.Single(x => x.의뢰Id == Id(1)).추천점수;

        fixture.ReturnArea.State = fixture.ReturnArea.State with { 활성여부 = false };
        var inactive = await fixture.Factory().생성Async(new() { 기사Ids = ["driver-1"] });
        Assert.Equal(20m, activeScore - inactive.조합평가목록.Single(x => x.의뢰Id == Id(1)).추천점수);
        Assert.All(inactive.조합평가목록, x => Assert.Equal(0m, x.복귀시간대부담점수));

        fixture.ReturnArea.State = fixture.ReturnArea.State with { 정책선택됨 = false };
        var legacy = await fixture.Factory().생성Async(new() { 기사Ids = ["driver-1"] });
        Assert.All(legacy.조합평가목록, x => Assert.True(x.복귀시간대부담점수 > 0m));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task 차량명세누락이나_구간적재미확인은_적합성허용응답이있어도_복귀가점적격으로확정하지않는다(bool existingCargo, bool coordination)
    {
        await using var fixture = await Fixture.CreateAsync(1);
        var candidate = await fixture.Db.화주운송의뢰.SingleAsync();
        candidate.픽업_위도 = 0.015m;
        candidate.하차_위도 = 0.007m;
        candidate.화물중량Kg = 500m;
        var queue = await fixture.Db.운송원장.SingleAsync();
        queue.픽업_위도 = candidate.픽업_위도;
        queue.하차_위도 = candidate.하차_위도;
        if (coordination) queue.현재추천대상기사Id = null;
        if (existingCargo)
        {
            var current = Request("existing-cargo", 0.020m, 0.010m);
            current.화물중량Kg = 800m;
            fixture.Db.화주운송의뢰.Add(current);
            fixture.Db.운송원장.Add(new()
            {
                의뢰Id = current.의뢰Id, 운송번호 = current.의뢰Id, 기사_운송자 = "driver-1",
                배차업무유형 = 상태값.배차업무유형.용달운송,
                배차큐단계 = 상태값.배차큐단계.확정, 상태 = "배차확정"
            });
        }
        else
            fixture.Db.차량제원.RemoveRange(fixture.Db.차량제원);
        await fixture.Db.SaveChangesAsync();

        var loading = new 차량화물적합성Service(new 차량적재추천Engine());
        var vehicle = await fixture.Db.차량제원.SingleOrDefaultAsync();
        Assert.Equal(existingCargo, loading.판정(vehicle, candidate, null).적합여부);
        if (existingCargo)
        {
            var current = await fixture.Db.화주운송의뢰.SingleAsync(x => x.의뢰Id == "existing-cargo");
            Assert.True(loading.판정(vehicle, current, null).적합여부);
            Assert.True(current.화물중량Kg + candidate.화물중량Kg > vehicle!.최대적재중량Kg);
        }

        if (coordination)
        {
            // 제원 누락 시에도 잘못된 적합 응답을 별도로 주어 복귀 가점의 독립 방어를 유지한다.
            var result = await fixture.Factory(realFit: existingCargo).생성Async(new() { 기사Ids = ["driver-1"] });
            Assert.Single(result.조합평가목록);
            Assert.Equal(0m, Assert.Single(result.조합평가목록).복귀시간대부담점수);
        }
        else
        {
            var result = Assert.Single(await fixture.Recommendations(realFit: existingCargo).GetDrivingRecommendationsAsync("driver-1"));
            Assert.True(result.차량적합여부);
            Assert.False(result.권역복귀확인됨);
            Assert.Equal(0m, result.권역복귀가점);
        }

        var comparison = Assert.Single(fixture.ReturnArea.Evaluations);
        Assert.False(comparison.Eligible);
        Assert.True(comparison.Verified);
        Assert.Equal(new 배차경로좌표(0.010m, 0m), comparison.Baseline);
        Assert.Equal(new 배차경로좌표(0.007m, 0m), comparison.Candidate);
    }

    [Theory]
    [InlineData("cargo-unknown", false)]
    [InlineData("cargo-unknown", true)]
    [InlineData("weight-limit-unknown", false)]
    [InlineData("weight-limit-unknown", true)]
    [InlineData("volume-limit-unknown", false)]
    [InlineData("volume-limit-unknown", true)]
    [InlineData("pallet-limit-unknown", false)]
    [InlineData("pallet-limit-unknown", true)]
    [InlineData("negative-input", false)]
    [InlineData("negative-input", true)]
    public async Task 적합true여도_화물요구나양수적재조건의차량한도가미확인이면_복귀가점을보류한다(string condition, bool coordination)
    {
        await using var fixture = await Fixture.CreateAsync(1);
        var request = await fixture.Db.화주운송의뢰.SingleAsync();
        request.하차_위도 = 0.007m;
        var queue = await fixture.Db.운송원장.SingleAsync();
        queue.하차_위도 = request.하차_위도;
        if (coordination) queue.현재추천대상기사Id = null;
        var vehicle = await fixture.Db.차량제원.SingleAsync();
        switch (condition)
        {
            case "cargo-unknown":
                request.화물중량Kg = null;
                request.화물부피Cbm = null;
                request.화물팔레트개수 = null;
                break;
            case "weight-limit-unknown":
                vehicle.최대적재중량Kg = 0;
                break;
            case "volume-limit-unknown":
                vehicle.권장최대CBM = null;
                vehicle.적재함높이Mm = null;
                break;
            case "pallet-limit-unknown":
                request.화물팔레트개수 = 1;
                vehicle.팔레트적재개수 = null;
                break;
            case "negative-input":
                request.화물중량Kg = -1m;
                break;
        }
        await fixture.Db.SaveChangesAsync();
        var fit = new 차량화물적합성Service(new 차량적재추천Engine());
        Assert.Equal(condition == "cargo-unknown", fit.판정(vehicle, request, null).적합여부);

        // 외부 적합 응답이 true여도 별도의 적재 근거가 없으면 복귀 가점을 인증하지 않는다.
        if (coordination)
            Assert.Single((await fixture.Factory().생성Async(new() { 기사Ids = ["driver-1"] })).조합평가목록);
        else
            Assert.Equal(0m, Assert.Single(await fixture.Recommendations().GetDrivingRecommendationsAsync("driver-1")).권역복귀가점);

        var comparison = Assert.Single(fixture.ReturnArea.Evaluations);
        Assert.True(comparison.Verified);
        Assert.False(comparison.Eligible);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task 실제적합성에서_제원이나요구부피기준이미확인이면_조합후보와복귀가점을인증하지않는다(
        bool missingVehicle, bool coordination)
    {
        await using var fixture = await Fixture.CreateAsync(1);
        if (missingVehicle)
            fixture.Db.차량제원.RemoveRange(fixture.Db.차량제원);
        else
        {
            var vehicle = await fixture.Db.차량제원.SingleAsync();
            vehicle.권장최대CBM = null;
            vehicle.적재함높이Mm = null;
        }
        var queue = await fixture.Db.운송원장.SingleAsync();
        if (coordination) queue.현재추천대상기사Id = null;
        await fixture.Db.SaveChangesAsync();

        if (coordination)
        {
            var result = Assert.Single((await fixture.Factory(realFit: true)
                .생성Async(new() { 기사Ids = ["driver-1"] })).조합평가목록);
            Assert.False(result.추천가능여부);
            Assert.Contains(result.제외사유, x => x.Contains("검증 불가", StringComparison.Ordinal));
        }
        else
        {
            var result = Assert.Single(await fixture.Recommendations(realFit: true).GetDrivingRecommendationsAsync("driver-1"));
            Assert.False(result.차량적합여부);
            Assert.Contains(result.차량부적합사유, x => x.Contains("검증 불가", StringComparison.Ordinal));
            Assert.Equal(0m, result.권역복귀가점);
        }
        Assert.False(Assert.Single(fixture.ReturnArea.Evaluations).Eligible);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 단독후보의_양수중량과차량한도가확인되면_다른요구미입력만으로가점을막지않는다(bool coordination)
    {
        await using var fixture = await Fixture.CreateAsync(1);
        var request = await fixture.Db.화주운송의뢰.SingleAsync();
        request.하차_위도 = 0.007m;
        request.화물부피Cbm = null;
        request.화물팔레트개수 = null;
        var queue = await fixture.Db.운송원장.SingleAsync();
        queue.하차_위도 = request.하차_위도;
        if (coordination) queue.현재추천대상기사Id = null;
        await fixture.Db.SaveChangesAsync();

        if (coordination)
            Assert.Single((await fixture.Factory(realFit: true).생성Async(new() { 기사Ids = ["driver-1"] })).조합평가목록);
        else
            Assert.Equal(20m, Assert.Single(await fixture.Recommendations(realFit: true).GetDrivingRecommendationsAsync("driver-1")).권역복귀가점);
        Assert.True(Assert.Single(fixture.ReturnArea.Evaluations).Eligible);
    }

    private static string Id(int index) => $"candidate-{index}";
    private static 기사운송일정항목 Stop(string id, string stage, decimal latitude, int sequence, DateTime now)
        => new(id, stage, id, new(latitude, 0m), null, now.AddHours(12), sequence, null, true, false);

    private static 화주운송의뢰 Request(string id, decimal pickup, decimal dropoff) => new()
    {
        의뢰Id = id, 화물종류 = "상자", 운송방식 = "혼적", 화물온도조건 = "상온", 최종운임 = 10_000m,
        결제상태 = 상태값.결제상태.결제완료, 정산상태 = "결제완료",
        화물중량Kg = 100m, 화물부피Cbm = 1m, 화물팔레트개수 = 0,
        픽업_도로명주소 = "fixture pickup", 픽업_위도 = pickup, 픽업_경도 = 0m,
        하차_도로명주소 = "fixture dropoff", 하차_위도 = dropoff, 하차_경도 = 0m,
        픽업_시간창_시작일시 = DateTime.UtcNow.AddMinutes(-10), 픽업_시간창_종료일시 = DateTime.UtcNow.AddHours(12),
        하차_시간창_시작일시 = DateTime.UtcNow.AddMinutes(-10), 하차_시간창_종료일시 = DateTime.UtcNow.AddHours(12)
    };

    private sealed class Fixture(SsalddelContext db, FixtureRoutes routes, CapturingReturnArea area) : IAsyncDisposable
    {
        public SsalddelContext Db { get; } = db;
        public CapturingReturnArea ReturnArea { get; } = area;
        public CapturingScore Score { get; } = new();
        public FixtureFit Fit { get; } = new();
        public FixtureRejected Rejected { get; } = new();
        private readonly FixtureLocation location = new();

        public static async Task<Fixture> CreateAsync(int count, bool selected = true, bool active = true)
        {
            var db = new SsalddelContext(new DbContextOptionsBuilder<SsalddelContext>()
                .UseInMemoryDatabase($"cargo-return-{Guid.NewGuid():N}").Options, new PlainEncryption());
            db.용달기사.Add(new()
            {
                기사Id = "driver-1", 차량 = "fixture-vehicle", 상태 = "활동중", 운행상태 = 상태값.기사운행상태.운행중,
                기본복귀지주소 = "private fixture anchor", 기본복귀지위도 = -1m, 기본복귀지경도 = 0m
            });
            db.차량제원.Add(new()
            {
                차량코드 = "fixture-vehicle", 차량명 = "fixture 1t", 최대적재중량Kg = 1000,
                적재함길이Mm = 2000, 적재함폭Mm = 2000, 적재함높이Mm = 2000, 권장최대CBM = 8m,
                팔레트적재개수 = 2
            });
            for (var i = 1; i <= count; i++)
            {
                var request = Request(Id(i), 0.010m + i * 0.0001m, i == 11 ? 0.007m : i == 41 ? 0.006m : 0.050m);
                db.화주운송의뢰.Add(request);
                db.운송원장.Add(new()
                {
                    의뢰Id = request.의뢰Id, 운송번호 = request.의뢰Id,
                    배차업무유형 = 상태값.배차업무유형.용달운송, 상태 = 상태값.배차대기상태.대기,
                    배차큐단계 = 상태값.배차큐단계.배차추천, 배차노출상태 = 상태값.배차노출상태.추천중,
                    현재추천대상기사Id = "driver-1", 추천시작시각 = DateTime.UtcNow, 추천만료시각 = DateTime.UtcNow.AddHours(1),
                    픽업_도로명주소 = request.픽업_도로명주소, 픽업_위도 = request.픽업_위도, 픽업_경도 = request.픽업_경도,
                    하차_도로명주소 = request.하차_도로명주소, 하차_위도 = request.하차_위도, 하차_경도 = request.하차_경도
                });
            }
            await db.SaveChangesAsync();
            return new(db, new() { Scale = 1000m }, new(new(selected, active, 10m, new(0m, 0m), 1, "fixture")));
        }

        public 화물배차추천Service Recommendations(bool realFit = false) => new(Db, location, Rejected, null!, null!, null!, routes,
            new 배차추천판정Service(), Score, realFit ? new 차량화물적합성Service(new 차량적재추천Engine()) : Fit,
            new 기사운송일정구성Service(Db), new 운송일정삽입평가Service(routes),
            new FixtureFuel(), null!, null!, ReturnArea,
            운영배차: Ssalddel.Tests.Services.Dispatch.Common.운영배차시험수신UseCase.허용,
            신규업무자격: Ssalddel.Tests.Support.Simulation운송자격Guard.Instance);

        public 국내화물배차조율입력Factory Factory(bool realFit = false) => new(Db, new FixtureDriverState(), Rejected, routes,
            realFit ? new 차량화물적합성Service(new 차량적재추천Engine()) : Fit,
            new 배차추천판정Service(), Score, new 기사운송일정구성Service(Db), new 운송일정삽입평가Service(routes),
            new 운송의뢰수익묶음Service(), Options.Create(new 배차큐정책Options()),
            Options.Create(new 국내화물배차AI정책Options()), ReturnArea,
            신규업무자격: Ssalddel.Tests.Support.Simulation운송자격Guard.Instance);

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }

    private sealed class CapturingReturnArea(기사권역복귀상태 state) : I기사권역복귀Service
    {
        public 기사권역복귀상태 State { get; set; } = state;
        public 배차경로좌표? LastPosition { get; private set; }
        public int StateReads { get; private set; }
        public List<(배차경로좌표? Baseline, 배차경로좌표? Candidate, bool Verified, bool Eligible)> Evaluations { get; } = [];
        public Task<기사권역복귀상태> 상태조회Async(string id, 배차경로좌표? point, DateTime? recorded, decimal? accuracy = null, CancellationToken cancellationToken = default)
        {
            LastPosition = point;
            StateReads++;
            return Task.FromResult(State);
        }
        public Task<기사권역복귀평가> 평가Async(기사권역복귀상태 current, 배차경로좌표? before, 배차경로좌표? after,
            bool fullRouteVerified, bool eligible, string? routeOption = null, CancellationToken cancellationToken = default)
        {
            Evaluations.Add((before, after, fullRouteVerified, eligible));
            if (!current.활성여부 || !fullRouteVerified || !eligible || before is null || after is null)
                return Task.FromResult(기사권역복귀평가.보류("fixture unconfirmed"));
            var beforeKm = Math.Abs(before.Latitude) * 1000m;
            var afterKm = Math.Abs(after.Latitude) * 1000m;
            return Task.FromResult(new 기사권역복귀평가(Math.Clamp((beforeKm - afterKm) / 3m, 0m, 1m) * 20m,
                true, beforeKm, afterKm, "fixture full schedule"));
        }
    }

    private sealed class FixtureRoutes : I배차추천경로Service
    {
        public decimal Scale { get; init; } = 1m;
        public bool Actual { get; init; } = true;
        public bool MissingDistance { get; init; }
        public bool MissingOption { get; init; }
        public bool MixedOptions { get; init; }
        public Task<배차경로좌표?> ResolveOriginLocationAsync(string id, 용달기사? driver, DriverLocationSnapshot? location, 배차추천검색조건? criteria)
            => Task.FromResult(criteria is null ? Point(location) : new 배차경로좌표(criteria.Latitude, criteria.Longitude));
        public Task<배차경로좌표?> ResolveRouteAnchorLocationAsync(string id, 용달기사? driver, DriverLocationSnapshot? location)
            => Task.FromResult(Point(location));
        public Task<배차경로예상결과?> EstimateRouteAsync(배차경로좌표? origin, 배차경로좌표? destination)
        {
            if (origin is null || destination is null) return Task.FromResult<배차경로예상결과?>(null);
            var distance = CalculateDistanceKm(origin, destination)!.Value;
            return Task.FromResult<배차경로예상결과?>(new(MissingDistance ? null : distance, TimeSpan.FromMinutes((double)distance), 0m,
                "fixture", Actual) { RouteOption = MissingOption ? null : MixedOptions && destination.Latitude == 2m ? "traavoidcaronly" : "traoptimal" });
        }
        public Task<배차경로예상결과?> EstimateOrderedRouteAsync(배차경로좌표? origin, IReadOnlyList<배차경로좌표> stops, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<배차경로예상결과?> EstimateRouteAsync(배차경로좌표? origin, 배차경로좌표? destination,
            string routeOption, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return EstimateRouteAsync(origin, destination);
        }
        public Task<배차삽입경로예상결과?> EstimateInsertionDelayAsync(배차경로좌표? origin, 배차경로좌표? anchor, 배차경로좌표? pickup, 배차경로좌표? dropoff)
            => Task.FromResult<배차삽입경로예상결과?>(null);
        public decimal? CalculateDistanceKm(배차경로좌표 source, 배차경로좌표 target)
            => (Math.Abs(target.Latitude - source.Latitude) + Math.Abs(target.Longitude - source.Longitude)) * Scale;
        private static 배차경로좌표? Point(DriverLocationSnapshot? snapshot)
            => snapshot is null ? null : new(snapshot.Latitude, snapshot.Longitude);
    }

    private sealed class CapturingScore : I배차추천평가Service
    {
        public List<bool> ReturnBasisFlags { get; } = [];
        public 배차추천평가결과 평가(화주운송의뢰? request, 배차추천판정결과 decision, decimal? profit, decimal? delay,
            decimal? anchorDistance, decimal? duration, decimal? slack, decimal? returnDetour, bool returnBasis, string? returnSource)
            => 평가(request, decision, null, profit, delay, anchorDistance, duration, slack, returnDetour, returnBasis, returnSource);
        public 배차추천평가결과 평가(화주운송의뢰? request, 배차추천판정결과 decision, 운송삽입평가결과? schedule,
            decimal? profit, decimal? delay, decimal? anchorDistance, decimal? duration, decimal? slack,
            decimal? returnDetour, bool returnBasis, string? returnSource)
        {
            ReturnBasisFlags.Add(returnBasis);
            return new(returnBasis ? 20m : 0m, [], [], "fixture score", returnBasis ? "legacy" : null);
        }
    }

    private sealed class FixtureFit : I차량화물적합성Service
    {
        public bool Allowed { get; set; } = true;
        public 차량화물적합성결과 판정(차량제원? vehicle, 화주운송의뢰 request, 화물요구조건? cargo)
            => new(Allowed, [], Allowed ? [] : ["fixture incompatible"]);
    }
    private sealed class FixtureFuel : IOpinetAveragePriceService
    {
        public Task<IReadOnlyList<OpinetAveragePriceItem>> GetAveragePricesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<OpinetAveragePriceItem>>([]);
    }
    private sealed class FixtureLocation : IDriverLocationStore
    {
        public void Upsert(DriverLocationSnapshot snapshot) => throw new NotSupportedException();
        public bool TryGetLatest(string id, out DriverLocationSnapshot snapshot)
        {
            snapshot = new(id, 0.010m, 0m, 5m, 상태값.기사운행상태.운행중, DateTime.UtcNow, DateTime.UtcNow);
            return true;
        }
    }
    private sealed class FixtureRejected : IDriverRejectedRequestStore
    {
        public HashSet<string> Ids { get; } = [];
        public Task RejectAsync(string id, string request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> IsRejectedAsync(string id, string request, CancellationToken cancellationToken = default) => Task.FromResult(Ids.Contains(request));
        public Task<IReadOnlySet<string>> GetRejectedRequestIdsAsync(string id, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlySet<string>>(Ids);
        public Task<IReadOnlySet<string>> GetRejectedDriverIdsAsync(string request, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlySet<string>>(new HashSet<string>());
    }
    private sealed class FixtureDriverState : I국내화물운송기사상태Store
    {
        private static 국내화물운송기사상태Snapshot Current => new("driver-1", null, 상태값.기사운행상태.운행중,
            DateTime.UtcNow, DateTime.UtcNow, 0m, 0.010m, 0m, 5m, DateTime.UtcNow, DateTime.UtcNow,
            null, null, 0, null, null, null);
        public Task UpsertAsync(국내화물운송기사상태Snapshot snapshot, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<국내화물운송기사상태Snapshot?> GetAsync(string id, CancellationToken cancellationToken = default) => Task.FromResult<국내화물운송기사상태Snapshot?>(Current);
        public Task<IReadOnlyList<국내화물운송기사상태Snapshot>> 위치반경조회Async(decimal lat, decimal lng, decimal radius, int take, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<국내화물운송기사상태Snapshot>>([Current]);
        public Task<IReadOnlyList<국내화물운송기사상태Snapshot>> 활성기사조회Async(int take, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<국내화물운송기사상태Snapshot>>([Current]);
        public Task RemoveAsync(string id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    private sealed class PlainEncryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
