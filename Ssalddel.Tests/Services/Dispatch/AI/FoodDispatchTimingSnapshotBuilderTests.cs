using System.Text.Json;
using Ssalddel.Contracts.Common.Dispatch;
using 살뜰.Services.Dispatch.AI;
using 살뜰.Services.Dispatch.Coordination;
using 살뜰.Services.Dispatch.Engine;
using 살뜰.도메인.공통;
using 살뜰.도메인.배차;
using 살뜰.도메인.운송;

namespace Ssalddel.Tests.Services.Dispatch.AI;

public sealed class FoodDispatchTimingSnapshotBuilderTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void 같은원주문의가장큰Id와대기자격을고르고입력은변경하지않는다()
    {
        var current = Queue(10, "current");
        var olderId = Queue(5, "current");
        olderId.CreatedAt = Now.UtcDateTime;
        var offered = Queue(11, "offered");
        offered.현재추천대상기사Id = "driver";
        var assigned = Queue(12, "assigned");
        assigned.확정기사Id = "driver";
        var invalid = Queue(13, "invalid");
        invalid.픽업_위도 = 91m;
        var completed = Queue(14, "completed");
        completed.상태 = "완료";
        var rows = new[] { olderId, current, offered, assigned, invalid, completed };
        var before = JsonSerializer.Serialize(rows);

        var pool = FoodDispatchTimingSnapshotBuilder.BuildPool(rows, Keys(), Now);

        Assert.Equal(new long[] { 10, 11, 12, 14 }, pool.Regional.Select(row => row.Id));
        Assert.Same(current, Assert.Single(pool.Pending));
        Assert.Equal(before, JsonSerializer.Serialize(rows));
    }

    [Theory]
    [InlineData(600, false)]
    [InlineData(601, true)]
    public void 지연경계와관측기간의실제유입만통계에포함한다(int oldestAgeSeconds, bool delayRisk)
    {
        var current = Queue(1, "current");
        current.CreatedAt = Now.AddSeconds(-oldestAgeSeconds).UtcDateTime;
        var arrival = Queue(2, "arrived");
        arrival.상태 = "완료";
        var future = Queue(3, "future");
        future.CreatedAt = Now.AddSeconds(1).UtcDateTime;
        future.상태 = "완료";
        var expired = Queue(4, "old-arrival");
        expired.CreatedAt = Now.AddSeconds(-301).UtcDateTime;
        expired.상태 = "완료";
        var pool = FoodDispatchTimingSnapshotBuilder.BuildPool([current, arrival, future, expired], Keys(), Now);
        var input = new FoodDispatchTimingInput
        {
            NowUtc = Now, QueuedAtUtc = current.CreatedAt, LatestDispatchAtUtc = Now.AddSeconds(20),
            IsRetry = true, HasActiveOffer = true
        };

        var result = FoodDispatchTimingSnapshotBuilder.WithStatistics(input, current, pool, Demand(),
            Now.AddSeconds(1), Now, Now.AddSeconds(-300).UtcDateTime, new(), 300);

        Assert.Equal(delayRisk, result.DelayRisk);
        Assert.Equal(0.1m, result.ExpectedNewOrders);
        Assert.Equal(1, result.WaitingOrders);
        Assert.Equal(1, result.CompatibleOrders);
        Assert.Equal(1, result.AvailableDrivers);
        Assert.True(result.DemandKnown);
        Assert.Equal(Now, result.SnapshotAtUtc);
        Assert.True(result.IsRetry);
        Assert.True(result.HasActiveOffer);
        Assert.Equal(input.LatestDispatchAtUtc, result.LatestDispatchAtUtc);
        Assert.Equal(Now, input.NowUtc);
    }

    [Theory]
    [InlineData("Missing")]
    [InlineData("Stale")]
    [InlineData("Future")]
    [InlineData("DuplicateDriver")]
    [InlineData("DuplicatePriority")]
    public void 현재성을확인할수없는수요는기사여력을만들지않는다(string scenario)
    {
        var current = Queue(1, "current");
        var pool = FoodDispatchTimingSnapshotBuilder.BuildPool([current], Keys(), Now);
        var demand = Demand();
        if (scenario == "Stale") demand.판정시각Utc = Now.AddMinutes(-6);
        if (scenario == "Future") demand.판정시각Utc = Now.AddSeconds(1);
        if (scenario == "DuplicateDriver") demand.기사판정목록 = [Driver("same", 1), Driver("same", 2)];
        if (scenario == "DuplicatePriority") demand.기사판정목록 = [Driver("one", 1), Driver("two", 1)];

        var result = FoodDispatchTimingSnapshotBuilder.WithStatistics(new(), current, pool,
            scenario == "Missing" ? null : demand, Now, Now, Now.AddMinutes(-5).UtcDateTime, new(), 300);

        Assert.False(result.DemandKnown);
        Assert.False(result.DriverCapacityKnown);
        Assert.Equal(0, result.AvailableDrivers);
        Assert.Null(result.SnapshotAtUtc);
    }

    private static HashSet<string> Keys()
        => [음식배달권정책.판정(new(37.5m, 127m), null).배달권키];

    private static 운송원장 Queue(long id, string source) => new()
    {
        Id = id, 의뢰Id = source, 원본의뢰Id = source, 운송번호 = $"transport-{id}",
        배차업무유형 = 상태값.배차업무유형.음식배달,
        상태 = 상태값.배차대기상태.대기, 배차큐단계 = 상태값.배차큐단계.배차추천,
        픽업_위도 = 37.5m, 픽업_경도 = 127m, 하차_위도 = 37.501m, 하차_경도 = 127.001m,
        CreatedAt = Now.AddSeconds(-5).UtcDateTime
    };

    private static 음식배달수요기사판정 Driver(string id, int priority)
        => new() { 기사Id = id, 우선순위 = priority, 신규제안허용 = true };

    private static 음식배달수요참여결과 Demand() => new()
    {
        정책판본 = 음식배달수요참여Policy.현재정책판본, 데이터상태Code = 음식배달수요자료상태Code.최신,
        판정시각Utc = Now, 예상업무량건수 = 1m, 기존기사가용처리건수 = 4m,
        기사판정목록 = [Driver("available", 1)]
    };
}
