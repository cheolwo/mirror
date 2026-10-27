using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Common.Drivers;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.도메인.운송;

namespace Ssalddel.Tests.Services.Dispatch.Queue;

public sealed partial class 음식배달배차업무정책Tests
{
    [Theory]
    [InlineData("baseline", "gap")]
    [InlineData("return", "score")]
    [InlineData("demand", "priority")]
    public void 세순위분기는_도착차이와점수와수요우선권의_서로다른순서를보존한다(string branch, string expected)
    {
        var candidates = new[]
        {
            RankingCandidate("gap", 0m, 10m),
            RankingCandidate("score", 5m, 100m, branch == "return"),
            RankingCandidate("priority", 10m, 0m)
        };
        var original = candidates.ToArray();
        var decisions = branch == "demand"
            ? candidates.ToDictionary(x => x.기사.DriverId,
                x => DemandDriver(x.기사.DriverId, true, x.기사.DriverId == "priority" ? 1 : 2), StringComparer.Ordinal)
            : null;

        var selected = 음식배달후보순위Policy.선택(candidates, decisions);

        Assert.Equal(expected, selected?.기사.DriverId);
        Assert.Equal(original, candidates);
        Assert.Same(candidates.Single(x => x.기사.DriverId == expected), selected);
    }

    [Theory]
    [InlineData("baseline")]
    [InlineData("return")]
    [InlineData("demand")]
    public void 세순위분기의_완전동률은_입력순서와무관하게_Ordinal기사Id로결정한다(string branch)
    {
        var candidates = new[]
        {
            RankingCandidate("a", 0m, 10m, branch == "return"),
            RankingCandidate("B", 0m, 10m, branch == "return")
        };
        var decisions = branch == "demand"
            ? candidates.ToDictionary(x => x.기사.DriverId, x => DemandDriver(x.기사.DriverId, true, 1), StringComparer.Ordinal)
            : null;

        Assert.Equal("B", 음식배달후보순위Policy.선택(candidates, decisions)?.기사.DriverId);
        Assert.Equal("B", 음식배달후보순위Policy.선택(candidates.Reverse().ToArray(), decisions)?.기사.DriverId);
        Assert.Equal(new[] { "a", "B" }, candidates.Select(x => x.기사.DriverId));
    }

    [Fact]
    public async Task 후보별일정평가는_같은조회시각과_검토순서를공유한다()
    {
        var now = DateTime.UtcNow;
        var states = new FakeDriverStateStore([
            Snapshot("first", 기사앱식별자.FoodDeliveryDriverApp, 37.5001m, 127m, now, 0),
            Snapshot("second", 기사앱식별자.FoodDeliveryDriverApp, 37.5002m, 127m, now, 0)]);
        var spaces = await CreateFoodSpaceStoreAsync(("first", 37.5001m, 127m), ("second", 37.5002m, 127m));
        var plans = new RankingClockPlan();

        var review = await AnchorPolicy(states, spaces, new AnchorRoutePort(), plans, null).후보검토Async(Queue());

        Assert.Equal(2, plans.Observations.Count);
        Assert.Single(plans.Observations.Select(x => x.At).Distinct());
        Assert.Equal(plans.Observations.Select(x => x.Id), review.후보목록.Select(x => x.기사Id));
        Assert.Equal(plans.Observations.Select(x => x.Id), review.최종허용기사Ids);
    }

    private static 음식배달기사항목 RankingCandidate(string id, decimal gap, decimal score, bool returning = false)
        => new(Snapshot(id, 기사앱식별자.FoodDeliveryDriverApp, 37.5m, 127m,
                new DateTime(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc), 0m),
            1m, 0, "same", new Ssalddel.WorkflowRules.음식배달픽업평가(0, gap, "fixture", score),
            returning ? new 기사권역복귀평가(1m, true, 2m, 1m, "fixture") : null);

    private sealed class RankingClockPlan : I음식권역복귀일정평가Service
    {
        public List<(string Id, DateTime At)> Observations { get; } = [];
        public Task<음식권역복귀일정평가결과> 평가Async(string driverId, 운송원장 queue,
            배차경로좌표 current, DateTime now, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public async Task<음식후속픽업기준결과> 후속픽업기준조회Async(string driverId, 운송원장 queue,
            배차경로좌표 current, DateTime now, CancellationToken cancellationToken = default)
        {
            Observations.Add((driverId, now));
            await Task.Yield();
            return new(false, true, current, now, null, "NoActiveSchedule");
        }
    }
}
