using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.Storage.Local;
using 살뜰.도메인.기사;
using 살뜰.도메인.화주;

namespace Ssalddel.Tests.Services.Dispatch.Recommendation;

public class 운송일정삽입평가ServiceTests
{
    [Theory]
    [InlineData(15, true)]
    [InlineData(12, false)]
    public async Task 이미상차한A보다B를먼저완료할때도_작업시간을포함해두약속을모두검사한다(int aDeadlineMinutes, bool feasible)
    {
        var now = new DateTime(2026, 10, 27, 1, 0, 0, DateTimeKind.Utc);
        var plan = new 기사운송일정계획("driver-1", now, new(0m, 0m),
        [
            new 기사운송일정항목("A", "dropoff", "합성 A 하차지", new(1m, 0m), null,
                now.AddMinutes(aDeadlineMinutes), 0, 1, true, false) { 작업시간분 = 1m }
        ])
        {
            상하차작업시간 = new Dictionary<string, 화물상하차작업시간>
            { ["A"] = new(1m, 1m), ["B"] = new(1m, 1m) }
        };
        var candidate = new 화주운송의뢰
        {
            의뢰Id = "B", 픽업_도로명주소 = "합성 B 상차지", 픽업_위도 = .1m, 픽업_경도 = 0m,
            하차_도로명주소 = "합성 B 하차지", 하차_위도 = .2m, 하차_경도 = 0m,
            픽업_시간창_종료일시 = now.AddMinutes(3), 하차_시간창_종료일시 = now.AddMinutes(5)
        };

        var result = await new 운송일정삽입평가Service(new 직선시간경로Service()).평가Async(plan, candidate);

        Assert.False(Assert.Single(result.시도목록.Where(x => x.시도유형 == "하차후이어가기")).전체완수가능여부);
        Assert.Equal(feasible, result.삽입가능여부);
        Assert.Equal(feasible, result.전체완수가능여부);
        if (feasible)
        {
            Assert.Equal(["추천 상차 B", "추천 하차 B", "기존 하차 A"], result.권장경로순서);
            Assert.Equal(now.AddMinutes(12), Assert.Single(result.도착예상목록.Where(x => x.의뢰Id == "A")).예상도착시각);
            Assert.Equal(13m, result.총소요시간분); // 마지막 A 하차 작업 1분까지 포함한다.
        }
        else Assert.DoesNotContain(result.시도목록, x => x.전체완수가능여부);
    }

    [Fact]
    public async Task 평가_분리삽입이_단순이어가기보다_빠르면_경로변경이점으로_선택한다()
    {
        var service = new 운송일정삽입평가Service(new 직선시간경로Service());
        var now = new DateTime(2026, 7, 8, 1, 0, 0, DateTimeKind.Utc);
        var plan = new 기사운송일정계획(
            "driver-1",
            now,
            new 배차경로좌표(0m, 0m),
            [
                new 기사운송일정항목("기존-1", "pickup", "기존 상차지", new 배차경로좌표(1m, 0m), null, now.AddMinutes(15), 0, 10, true, false),
                new 기사운송일정항목("기존-1", "dropoff", "기존 하차지", new 배차경로좌표(10m, 0m), null, now.AddHours(4), 1, 10, true, false)
            ]);
        var candidate = new 화주운송의뢰
        {
            의뢰Id = "추천-1",
            픽업_도로명주소 = "추천 상차지",
            픽업_위도 = 2m,
            픽업_경도 = 0m,
            픽업_시간창_종료일시 = now.AddHours(3),
            하차_도로명주소 = "추천 하차지",
            하차_위도 = 3m,
            하차_경도 = 0m,
            하차_시간창_종료일시 = now.AddHours(3)
        };

        var result = await service.평가Async(plan, candidate);

        Assert.True(result.삽입가능여부);
        Assert.True(result.전체완수가능여부);
        Assert.True(result.경로변경이점여부);
        Assert.Equal(90m, result.경로변경절감분);
        Assert.Equal(0m, result.총추가지연분);
        Assert.Equal(
            ["기존 상차 기존-1", "추천 상차 추천-1", "추천 하차 추천-1", "기존 하차 기존-1"],
            result.권장경로순서);
    }

    private sealed class 직선시간경로Service : I배차추천경로Service
    {
        public Task<배차경로좌표?> ResolveOriginLocationAsync(string driverId, 용달기사? driver, DriverLocationSnapshot? currentLocation, 배차추천검색조건? criteria)
            => Task.FromResult<배차경로좌표?>(null);

        public Task<배차경로좌표?> ResolveRouteAnchorLocationAsync(string driverId, 용달기사? driver, DriverLocationSnapshot? currentLocation)
            => Task.FromResult<배차경로좌표?>(null);

        public Task<배차경로예상결과?> EstimateRouteAsync(배차경로좌표? origin, 배차경로좌표? destination)
        {
            if (origin is null || destination is null)
            {
                return Task.FromResult<배차경로예상결과?>(null);
            }

            var distance = Math.Abs(destination.Latitude - origin.Latitude) + Math.Abs(destination.Longitude - origin.Longitude);
            var duration = TimeSpan.FromMinutes((double)(distance * 10m));
            return Task.FromResult<배차경로예상결과?>(new 배차경로예상결과(distance, duration, null));
        }

        public async Task<배차경로예상결과?> EstimateOrderedRouteAsync(
            배차경로좌표? origin,
            IReadOnlyList<배차경로좌표> orderedStops,
            CancellationToken cancellationToken = default)
        {
            if (origin is null || orderedStops.Count == 0)
            {
                return null;
            }

            var current = origin;
            decimal distance = 0m;
            TimeSpan duration = TimeSpan.Zero;
            foreach (var stop in orderedStops)
            {
                var route = await EstimateRouteAsync(current, stop);
                distance += route?.DistanceKm ?? 0m;
                duration += route?.Duration ?? TimeSpan.Zero;
                current = stop;
            }

            return new 배차경로예상결과(distance, duration, null);
        }

        public Task<배차삽입경로예상결과?> EstimateInsertionDelayAsync(배차경로좌표? origin, 배차경로좌표? routeAnchor, 배차경로좌표? pickup, 배차경로좌표? dropoff)
            => Task.FromResult<배차삽입경로예상결과?>(null);

        public decimal? CalculateDistanceKm(배차경로좌표 source, 배차경로좌표 target)
            => Math.Abs(target.Latitude - source.Latitude) + Math.Abs(target.Longitude - source.Longitude);
    }
}
