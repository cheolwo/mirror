using Ssalddel.Contracts.Common.Dispatch;
using 살뜰.도메인.배차;

namespace Ssalddel.Tests.Domain.Dispatch;

public sealed class FoodDispatchInformationReadinessPolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 11, 3, 0, 0, TimeSpan.Zero);
    private static readonly string[] Keys =
    [
        "Order", "Preparation", "Locations", "OrderPool", "Demand", "DriverCapacity", "CustomerPromise",
        "CostConsent", "DriverScheduleAndLoad", "DriverQuote", "PlatformFunds"
    ];
    private readonly FoodDispatchInformationReadinessPolicy _policy = new();

    [Fact]
    public void 수집정보_여섯개만_현재이면_전체조율이_준비됐다고_인증하지_않는다()
    {
        var result = Evaluate(Current().Take(6).ToArray());

        Assert.Equal("Current", result.SchedulingStateCode);
        Assert.Equal("Missing", result.CoordinationStateCode);
        Assert.Equal(11, result.Evidence.Count);
        Assert.All(result.Evidence.Skip(6), item => Assert.Equal("Missing", item.StateCode));
        Assert.Equal(FoodDispatchInformationReadinessPolicy.CurrentPolicyVersion, result.PolicyVersion);
    }

    [Fact]
    public void 전체_필수근거가_현재이면_두준비상태가_현재다()
    {
        var result = Evaluate(Current());
        Assert.Equal("Current", result.SchedulingStateCode);
        Assert.Equal("Current", result.CoordinationStateCode);
        Assert.Equal(Keys, result.Evidence.Select(item => item.Key));
        Assert.All(result.Evidence, item => Assert.Equal("InformationObserved", item.ReasonCode));
    }

    [Fact]
    public void 적용불가이면_원시정보와_누락_시각을_평가하거나_반환하지_않는다()
    {
        var result = _policy.Evaluate(false, default, 15,
            [new("개인원문", "잘못된상태", "상세 주소 및 오류 원문")]);

        Assert.Equal("NotApplicable", result.SchedulingStateCode);
        Assert.Equal("NotApplicable", result.CoordinationStateCode);
        Assert.Equal(Keys, result.Evidence.Select(item => item.Key));
        Assert.All(result.Evidence, item =>
        {
            Assert.Equal("NotApplicable", item.StateCode);
            Assert.Equal("InformationNotApplicable", item.ReasonCode);
            Assert.Null(item.ObservedAtUtc);
            Assert.Null(item.SourceRevision);
        });
    }

    [Fact]
    public void 필수근거의_적용불가는_현재자료로_계산하지_않는다()
    {
        var result = Evaluate(Replace("Order", "NotApplicable"));
        Assert.Equal("Missing", result.SchedulingStateCode);
        Assert.Equal("InformationRequired", Item(result, "Order").ReasonCode);
    }

    [Fact]
    public void 만료는_같은집계의_누락보다_우선하고_전체조율에만_있는_만료는_수집집계에_번지지_않는다()
    {
        var evidence = Replace("CustomerPromise", "Stale");
        var result = Evaluate(evidence.Where(item => item.Key != "PlatformFunds").ToArray());
        Assert.Equal("Current", result.SchedulingStateCode);
        Assert.Equal("Stale", result.CoordinationStateCode);

        result = Evaluate(Replace("Order", "Stale").Where(item => item.Key != "Demand").ToArray());
        Assert.Equal("Stale", result.SchedulingStateCode);
        Assert.Equal("Stale", result.CoordinationStateCode);
    }

    [Fact]
    public void 중복근거는_유효한_하나를_골라_현재로_승격하지_않는다()
    {
        var result = Evaluate([.. Current(), new("Order", "Missing", "OrderNotFound")]);
        Assert.Equal("Missing", result.SchedulingStateCode);
        Assert.Equal("EvidenceDuplicate", Item(result, "Order").ReasonCode);
    }

    [Theory]
    [InlineData("Ready")]
    [InlineData("current")]
    [InlineData("")]
    [InlineData(null)]
    public void 알수없는_상태는_누락으로_정규화한다(string? state)
    {
        var result = Evaluate(Replace("Order", state!));
        Assert.Equal("Missing", Item(result, "Order").StateCode);
        Assert.Equal("UnknownState", Item(result, "Order").ReasonCode);
    }

    [Fact]
    public void 관측시각이_없는_현재표시는_인증하지_않는다()
    {
        foreach (DateTimeOffset? observed in new DateTimeOffset?[] { null, default(DateTimeOffset) })
        {
            var result = Evaluate(Current().Select(item => item.Key == "Order"
                ? item with { ObservedAtUtc = observed } : item).ToArray());
            Assert.Equal("Missing", Item(result, "Order").StateCode);
            Assert.Equal("ObservationMissing", Item(result, "Order").ReasonCode);
        }
    }

    [Fact]
    public void 미래관측은_출처사유보다_우선하여_만료로_표시한다()
    {
        var result = Evaluate(Current().Select(item => item.Key == "Order"
            ? item with { ObservedAtUtc = Now.AddTicks(1), ReasonCode = "OrderNotFound" } : item).ToArray());
        Assert.Equal("Stale", result.SchedulingStateCode);
        Assert.Equal("ObservationFuture", Item(result, "Order").ReasonCode);
    }

    [Fact]
    public void 유효시간_정확한_경계는_현재이고_한틱_초과부터_만료다()
    {
        var boundary = Current().Select(item => item with { ObservedAtUtc = Now.AddSeconds(-15) }).ToArray();
        Assert.Equal("Current", Evaluate(boundary).CoordinationStateCode);
        var stale = boundary.Select(item => item.Key == "Order"
            ? item with { ObservedAtUtc = item.ObservedAtUtc!.Value.AddTicks(-1) } : item).ToArray();
        var result = Evaluate(stale);
        Assert.Equal("Stale", result.SchedulingStateCode);
        Assert.Equal("ObservationStale", Item(result, "Order").ReasonCode);
    }

    [Fact]
    public void 확인된_영판본과_선택판본_미지정은_정보누락이_아니다()
    {
        var evidence = Current().Select((item, index) => item with { SourceRevision = index % 2 == 0 ? 0 : null }).ToArray();
        Assert.Equal("Current", Evaluate(evidence).CoordinationStateCode);
    }

    [Fact]
    public void 음수판본은_누락으로_표시하고_잘못된판본을_반환하지_않는다()
    {
        var result = Evaluate(Current().Select(item => item.Key == "Order"
            ? item with { SourceRevision = -1 } : item).ToArray());
        var order = Item(result, "Order");
        Assert.Equal("Missing", order.StateCode);
        Assert.Equal("InvalidRevision", order.ReasonCode);
        Assert.Null(order.SourceRevision);
    }

    [Fact]
    public void 판정시각이_없으면_근거시각이_있어도_현재로_승격하지_않는다()
    {
        var result = _policy.Evaluate(true, default, 15, Current());
        Assert.Equal("Missing", result.SchedulingStateCode);
        Assert.Equal("Missing", result.CoordinationStateCode);
        Assert.All(result.Evidence, item => Assert.Equal("InvalidEvaluationTime", item.ReasonCode));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(121)]
    public void 유효시간_범위밖_설정을_거부한다(int maxAge)
        => Assert.Throws<ArgumentOutOfRangeException>(() => _policy.Evaluate(true, Now, maxAge, Current()));

    [Theory]
    [InlineData(1)]
    [InlineData(120)]
    public void 유효시간_허용범위_끝값을_사용할수_있다(int maxAge)
    {
        var applicable = _policy.Evaluate(true, Now, maxAge, Current());
        var notApplicable = _policy.Evaluate(false, Now, maxAge, Current());
        Assert.Equal("Current", applicable.CoordinationStateCode);
        Assert.Equal(maxAge, applicable.MaxAgeSeconds);
        Assert.Equal("NotApplicable", notApplicable.CoordinationStateCode);
        Assert.Equal(maxAge, notApplicable.MaxAgeSeconds);
    }

    [Fact]
    public void 근거목록이_없어도_임의의_현재자료를_만들지_않는다()
    {
        var result = Evaluate(null!);
        Assert.Equal("Missing", result.SchedulingStateCode);
        Assert.All(result.Evidence, item => Assert.Equal("EvidenceMissing", item.ReasonCode));
    }

    [Fact]
    public void 원시_항목과_사유는_결과에_노출하지_않고_알려진_항목만_고정순서로_반환한다()
    {
        var evidence = Replace("Order", "Missing", "주소와 이름을 포함한 오류 원문");
        var result = Evaluate([.. evidence.Reverse(), null!, new("원시 상세주소", "Current", "원문", Now)]);
        Assert.Equal(Keys, result.Evidence.Select(item => item.Key));
        Assert.Equal("InformationMissing", Item(result, "Order").ReasonCode);
        Assert.DoesNotContain(result.Evidence, item => item.Key == "원시 상세주소");
    }

    [Fact]
    public void 대소문자가_다른_항목을_필수근거의_별칭으로_인증하지_않는다()
    {
        var result = Evaluate(Current().Select(item => item.Key == "Order" ? item with { Key = "order" } : item).ToArray());
        Assert.Equal("EvidenceMissing", Item(result, "Order").ReasonCode);
    }

    [Theory]
    [InlineData("OrderNotFound")]
    [InlineData("PreparationChanged")]
    [InlineData("QueuePoolTruncated")]
    [InlineData("SnapshotReadFailed")]
    [InlineData("PlatformFundsNotRecorded")]
    public void 허용된_고정출처사유만_보존한다(string reason)
    {
        var result = Evaluate(Replace("Order", "Missing", reason));
        Assert.Equal(reason, Item(result, "Order").ReasonCode);
    }

    [Fact]
    public void 만료라고_제공된_근거를_새시각만으로_현재로_바꾸지_않는다()
    {
        var result = Evaluate(Replace("Order", "Stale", "OrderChanged"));
        Assert.Equal("Stale", result.SchedulingStateCode);
        Assert.Equal("OrderChanged", Item(result, "Order").ReasonCode);
    }

    [Fact]
    public void 현재자료의_출처사유는_원문대신_고정된_관측사유만_반환한다()
    {
        var result = Evaluate(Replace("Order", "Current", "OrderNotFound"));
        Assert.Equal("Current", result.SchedulingStateCode);
        Assert.Equal("InformationObserved", Item(result, "Order").ReasonCode);
    }

    [Fact]
    public void 판정은_입력을_수정하지_않고_같은입력에_같은근거를_반환한다()
    {
        var evidence = Current();
        var before = evidence.ToArray();
        var first = Evaluate(evidence);
        var second = Evaluate(evidence);
        Assert.Equal(first.SchedulingStateCode, second.SchedulingStateCode);
        Assert.Equal(first.CoordinationStateCode, second.CoordinationStateCode);
        Assert.Equal(first.Evidence, second.Evidence);
        Assert.Equal(before, evidence);
    }

    private FoodDispatchInformationReadiness Evaluate(IReadOnlyList<FoodDispatchInformationEvidence> evidence)
        => _policy.Evaluate(true, Now, 15, evidence);

    private static FoodDispatchInformationEvidence[] Current()
        => Keys.Select(key => new FoodDispatchInformationEvidence(key, "Current", "InputReason", Now, 0)).ToArray();

    private static FoodDispatchInformationEvidence[] Replace(string key, string state, string reason = "InputReason")
        => Current().Select(item => item.Key == key ? item with { StateCode = state, ReasonCode = reason } : item).ToArray();

    private static FoodDispatchInformationEvidence Item(FoodDispatchInformationReadiness result, string key)
        => Assert.Single(result.Evidence, item => item.Key == key);
}
