using Ssalddel.Contracts.Common.Dispatch;
using 살뜰.도메인.배차;

namespace Ssalddel.Tests.Domain.Dispatch;

public sealed class FoodDispatchTimingPolicyTests
{
    private static readonly DateTimeOffset Queued = new(2026, 10, 11, 3, 0, 0, TimeSpan.Zero);
    private readonly FoodDispatchTimingPolicy _policy = new();

    [Fact]
    public void 추가주문근거와_여력이_있으면_최초큐부터_30초까지만_수집한다()
    {
        var result = _policy.Evaluate(Input());

        Assert.Equal("Collect", result.ActionCode);
        Assert.Equal(Queued.AddSeconds(30), result.RecheckAtUtc);
        Assert.Equal(Queued.AddSeconds(60), result.HardDeadlineUtc);
        Assert.Equal(["Collect", "DispatchNow"], result.AllowedActions);
        Assert.Equal(FoodDispatchTimingPolicy.CurrentPolicyVersion, result.PolicyVersion);
    }

    [Fact]
    public void 반복조회와_신규주문이_최초큐의_기한을_연장하지_않는다()
    {
        var first = _policy.Evaluate(Input());
        var later = _policy.Evaluate(Input() with
        {
            NowUtc = Queued.AddSeconds(29), SnapshotAtUtc = Queued.AddSeconds(29),
            WaitingOrders = 500, ExpectedNewOrders = 100m
        });
        var expired = _policy.Evaluate(Input() with
        {
            NowUtc = Queued.AddSeconds(30), SnapshotAtUtc = Queued.AddSeconds(30), WaitingOrders = 501
        });

        Assert.Equal(first.RecheckAtUtc, later.RecheckAtUtc);
        Assert.Equal(first.HardDeadlineUtc, later.HardDeadlineUtc);
        Assert.Equal(first.HardDeadlineUtc, expired.HardDeadlineUtc);
        Assert.Equal("DispatchNow", expired.ActionCode);
        Assert.Equal("CollectionElapsed", expired.ReasonCode);
    }

    [Fact]
    public void 대기500건의_전체대기풀도_호환주문수와_구분한다()
    {
        var result = _policy.Evaluate(Input() with { WaitingOrders = 500, CompatibleOrders = 1 });
        Assert.Equal("Collect", result.ActionCode);
    }

    [Theory]
    [InlineData(1, "Collect")]
    [InlineData(2, "DispatchNow")]
    [InlineData(3, "DispatchNow")]
    public void 호환주문_임계에_도달하면_일찍_배차한다(int compatible, string expected)
    {
        var result = _policy.Evaluate(Input() with { WaitingOrders = 3, CompatibleOrders = compatible });
        Assert.Equal(expected, result.ActionCode);
        if (expected == "DispatchNow") AssertForced(result, "CompatibleOrderThresholdReached");
    }

    [Fact]
    public void 기하상_유망조합이_확인되면_전체적재검증을_주장하지_않고_기존배차로_넘긴다()
        => AssertForced(_policy.Evaluate(Input() with { PromisingPlanAvailable = true }), "PromisingPlanAvailable");

    [Fact]
    public void 확인된_조기마감에서_여유시간을_남기고_재평가한다()
    {
        var result = _policy.Evaluate(Input() with { LatestDispatchAtUtc = Queued.AddSeconds(25) });
        Assert.Equal("Collect", result.ActionCode);
        Assert.Equal(Queued.AddSeconds(25), result.HardDeadlineUtc);
        Assert.Equal(Queued.AddSeconds(10), result.RecheckAtUtc);
    }

    [Fact]
    public void 확인된_늦은마감도_최초큐_60초_상한을_늘리지_않는다()
    {
        var result = _policy.Evaluate(Input() with { LatestDispatchAtUtc = Queued.AddHours(1) });
        Assert.Equal(Queued.AddSeconds(60), result.HardDeadlineUtc);
        Assert.Equal(Queued.AddSeconds(30), result.RecheckAtUtc);
    }

    [Theory]
    [InlineData(9, "Collect")]
    [InlineData(10, "DispatchNow")]
    [InlineData(25, "DispatchNow")]
    public void 조기마감_여유의_정확한_경계부터는_기다리지_않는다(int elapsed, string expected)
    {
        var now = Queued.AddSeconds(elapsed);
        var result = _policy.Evaluate(Input() with
        {
            NowUtc = now, SnapshotAtUtc = now, LatestDispatchAtUtc = Queued.AddSeconds(25)
        });
        Assert.Equal(expected, result.ActionCode);
        if (expected == "DispatchNow") AssertForced(result, "DeadlineApproaching");
    }

    [Theory]
    [InlineData(44, "Collect")]
    [InlineData(45, "DispatchNow")]
    [InlineData(60, "DispatchNow")]
    public void 절대최대대기의_여유를_지나면_새로운_수집을_허용하지_않는다(int elapsed, string expected)
    {
        var now = Queued.AddSeconds(elapsed);
        var result = _policy.Evaluate(Input() with { NowUtc = now, SnapshotAtUtc = now },
            new() { CollectionSeconds = 60 });
        Assert.Equal(expected, result.ActionCode);
        if (expected == "DispatchNow") AssertForced(result, "DeadlineApproaching");
    }

    [Fact]
    public void 지연위험은_신규주문예상이나_유망조합보다_먼저_즉시진행한다()
        => AssertForced(_policy.Evaluate(Input() with
        { DelayRisk = true, PromisingPlanAvailable = true, ExpectedNewOrders = 100m }), "DelayRisk");

    [Fact]
    public void 허용기사가_없으면_주문을_모으며_기다리지_않는다()
        => AssertForced(_policy.Evaluate(Input() with { AvailableDrivers = 0 }), "NoAvailableDrivers");

    [Fact]
    public void 주문이_없으면_수집하지_않는다()
        => AssertForced(_policy.Evaluate(Input() with { WaitingOrders = 0, CompatibleOrders = 0 }), "NoWaitingOrders");

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-1)]
    public void 추가주문_근거가_없으면_희박수요를_0으로_인증하지_않고_기존배차로_진행한다(int? expected)
        => AssertForced(_policy.Evaluate(Input() with { ExpectedNewOrders = expected }), "NoExpectedNewOrders");

    [Fact]
    public void 관측이_없으면_추정수요가_커도_기다리지_않는다()
        => AssertForced(_policy.Evaluate(Input() with { SnapshotAtUtc = null }), "SnapshotUnknown");

    [Fact]
    public void 미래_관측은_최신자료로_취급하지_않는다()
        => AssertForced(_policy.Evaluate(Input() with { SnapshotAtUtc = Queued.AddSeconds(1) }), "SnapshotFuture");

    [Theory]
    [InlineData(15, "Collect")]
    [InlineData(16, "DispatchNow")]
    public void 관측자료_최대경과시간의_경계를_확인한다(int age, string expected)
    {
        var result = _policy.Evaluate(Input() with { SnapshotAtUtc = Queued.AddSeconds(-age) });
        Assert.Equal(expected, result.ActionCode);
        if (expected == "DispatchNow") AssertForced(result, "SnapshotStale");
    }

    [Fact]
    public void 수요가_미확인이면_추가주문숫자만으로_수집하지_않는다()
        => AssertForced(_policy.Evaluate(Input() with { DemandKnown = false }), "DemandUnknown");

    [Fact]
    public void 기사여력이_미확인이면_기사수만으로_수집하지_않는다()
        => AssertForced(_policy.Evaluate(Input() with { DriverCapacityKnown = false }), "DriverCapacityUnknown");

    [Theory]
    [InlineData("NotApplicable")]
    [InlineData("ActiveOffer")]
    [InlineData("Retry")]
    public void 비대상과_기존제안과_재추천은_오래된관측이어도_기존흐름만_허용한다(string reason)
    {
        var input = Input() with { SnapshotAtUtc = null, DemandKnown = false };
        input = reason switch
        {
            "NotApplicable" => input with { Applicable = false },
            "ActiveOffer" => input with { HasActiveOffer = true },
            _ => input with { IsRetry = true }
        };
        var result = _policy.Evaluate(input);
        Assert.Equal("ExistingFlow", result.ActionCode);
        Assert.Equal(reason, result.ReasonCode);
        Assert.Equal(["ExistingFlow"], result.AllowedActions);
        Assert.Null(result.RecheckAtUtc);
    }

    [Theory]
    [InlineData(-1, 0, 1)]
    [InlineData(1, -1, 1)]
    [InlineData(1, 0, -1)]
    [InlineData(1, 2, 1)]
    public void 잘못된_건수는_기다리지_않는다(int waiting, int compatible, int drivers)
        => AssertForced(_policy.Evaluate(Input() with
        { WaitingOrders = waiting, CompatibleOrders = compatible, AvailableDrivers = drivers }), "InvalidCounts");

    [Fact]
    public void 미래_큐시각으로_대기상한을_연장할_수_없다()
        => AssertForced(_policy.Evaluate(Input() with { QueuedAtUtc = Queued.AddSeconds(1) }), "InvalidTimestamps");

    [Fact]
    public void 누락된_최초큐시각을_현재시각으로_채우지_않는다()
        => AssertForced(_policy.Evaluate(Input() with { QueuedAtUtc = default }), "InvalidTimestamps");

    [Fact]
    public void 시각연산_범위를_넘어도_대기를_만들거나_예외로_업무를_중단하지_않는다()
    {
        var result = _policy.Evaluate(Input() with
        { QueuedAtUtc = DateTimeOffset.MaxValue, NowUtc = DateTimeOffset.MaxValue, SnapshotAtUtc = DateTimeOffset.MaxValue });
        AssertForced(result, "InvalidTimestamps");
    }

    [Fact]
    public void 수집0초는_즉시진행설정으로_동작한다()
        => AssertForced(_policy.Evaluate(Input(), new() { CollectionSeconds = 0 }), "CollectionElapsed");

    [Theory]
    [InlineData("CollectionNegative")]
    [InlineData("CollectionBeyondMax")]
    [InlineData("MaxZero")]
    [InlineData("MaxBeyond120")]
    [InlineData("ThresholdZero")]
    [InlineData("BufferNegative")]
    [InlineData("BufferBeyondMax")]
    [InlineData("SnapshotAgeZero")]
    [InlineData("SnapshotAgeBeyond120")]
    public void 잘못된_설정은_판정전에_거부한다(string option)
    {
        FoodDispatchTimingSettings settings = option switch
        {
            "CollectionNegative" => new FoodDispatchTimingSettings { CollectionSeconds = -1 },
            "CollectionBeyondMax" => new() { CollectionSeconds = 61 },
            "MaxZero" => new() { MaxWaitSeconds = 0 },
            "MaxBeyond120" => new() { MaxWaitSeconds = 121 },
            "ThresholdZero" => new() { MinimumCompatibleOrders = 0 },
            "BufferNegative" => new() { DeadlineBufferSeconds = -1 },
            "BufferBeyondMax" => new() { DeadlineBufferSeconds = 61 },
            "SnapshotAgeZero" => new() { MaxSnapshotAgeSeconds = 0 },
            _ => new() { MaxSnapshotAgeSeconds = 121 }
        };
        Assert.Throws<ArgumentOutOfRangeException>(() => _policy.Evaluate(Input(), settings));
    }

    [Fact]
    public void 같은입력은_같은결정을_반환하고_입력은_변경하지_않는다()
    {
        var input = Input();
        var copy = input with { };
        var first = _policy.Evaluate(input);
        var second = _policy.Evaluate(input);
        Assert.Equal(first.ActionCode, second.ActionCode);
        Assert.Equal(first.ReasonCode, second.ReasonCode);
        Assert.Equal(first.RecheckAtUtc, second.RecheckAtUtc);
        Assert.Equal(first.HardDeadlineUtc, second.HardDeadlineUtc);
        Assert.Equal(first.AllowedActions, second.AllowedActions);
        Assert.Equal(copy, input);
    }

    private static FoodDispatchTimingInput Input() => new()
    {
        NowUtc = Queued, QueuedAtUtc = Queued, SnapshotAtUtc = Queued,
        DemandKnown = true, DriverCapacityKnown = true,
        WaitingOrders = 1, CompatibleOrders = 1, AvailableDrivers = 2, ExpectedNewOrders = 1m
    };

    private static void AssertForced(FoodDispatchTimingDecision result, string reason)
    {
        Assert.Equal("DispatchNow", result.ActionCode);
        Assert.Equal(reason, result.ReasonCode);
        Assert.Null(result.RecheckAtUtc);
        Assert.Equal(["DispatchNow"], result.AllowedActions);
    }
}
