using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Common.Metadata;

namespace 살뜰.도메인.배차;

[SsalddelCodeMetadata(
    SsalddelCodeFeatureKeys.OperationalDispatchCore,
    SsalddelCodeLayer.Domain,
    "최초 큐의 고정 기한 안에서 음식 주문을 짧게 모을지 기존 배차를 바로 진행할지 판정한다.",
    Effects = SsalddelCodeEffect.None,
    FlowOrder = 41,
    StepKey = "domain.food-dispatch-timing-policy",
    DependsOnStepKeys = ["contract.food-dispatch-timing"],
    ExecutionStage = SsalddelCodeExecutionStage.Preview,
    ReadsFrom = SsalddelCodeDataScope.OperationalState,
    WritesTo = SsalddelCodeDataScope.None,
    Boundary = "시계·DB·모델을 호출하거나 원장을 변경하지 않는다. 미확인·지연·기한 만료는 기존 배차 관문으로 복귀하며 신규 주문으로 대기 기한을 연장하지 않는다.")]
public sealed class FoodDispatchTimingPolicy
{
    public const string CurrentPolicyVersion = "food-dispatch-timing/v1";

    public FoodDispatchTimingDecision Evaluate(
        FoodDispatchTimingInput input,
        FoodDispatchTimingSettings? settings = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        settings ??= new();
        ValidateSettings(settings);

        var now = input.NowUtc.ToUniversalTime();
        var queued = input.QueuedAtUtc.ToUniversalTime();
        var boundedDeadline = AddSecondsBounded(queued, settings.MaxWaitSeconds);
        var hardDeadline = input.LatestDispatchAtUtc is { } confirmedDeadline
            ? Earlier(boundedDeadline, confirmedDeadline.ToUniversalTime())
            : boundedDeadline;

        FoodDispatchTimingDecision Existing(string reason) => new(
            FoodDispatchTimingActionCodes.ExistingFlow, reason, null, hardDeadline,
            [FoodDispatchTimingActionCodes.ExistingFlow]);
        FoodDispatchTimingDecision Dispatch(string reason) => new(
            FoodDispatchTimingActionCodes.DispatchNow, reason, null, hardDeadline,
            [FoodDispatchTimingActionCodes.DispatchNow]);

        if (!input.Applicable) return Existing("NotApplicable");
        if (input.HasActiveOffer) return Existing("ActiveOffer");
        if (input.IsRetry) return Existing("Retry");

        if (input.NowUtc == default || input.QueuedAtUtc == default || queued > now
            || queued > DateTimeOffset.MaxValue.AddSeconds(-settings.MaxWaitSeconds))
            return Dispatch("InvalidTimestamps");
        if (input.WaitingOrders < 0 || input.CompatibleOrders < 0 || input.AvailableDrivers < 0
            || input.CompatibleOrders > input.WaitingOrders)
            return Dispatch("InvalidCounts");
        if (input.SnapshotAtUtc is not { } snapshot) return Dispatch("SnapshotUnknown");
        if (snapshot > now) return Dispatch("SnapshotFuture");
        if ((now - snapshot).TotalSeconds > settings.MaxSnapshotAgeSeconds)
            return Dispatch("SnapshotStale");
        if (!input.DemandKnown) return Dispatch("DemandUnknown");
        if (!input.DriverCapacityKnown) return Dispatch("DriverCapacityUnknown");
        // 서버가 결속한 정보가 부족하거나 바뀌면 주문을 더 모으지 않는다. 기존 단건 관문은 그대로 적용한다.
        // null은 기존 오프라인 순수 정책 입력의 호환 경계이며 제품 전환 서비스는 null 수집을 별도로 거부한다.
        if (input.InformationReadiness is { SchedulingStateCode: not FoodDispatchInformationStateCodes.Current } information)
            return Dispatch(information.SchedulingStateCode == FoodDispatchInformationStateCodes.Stale
                ? "InformationStale" : "InformationMissing");
        if (input.WaitingOrders == 0) return Dispatch("NoWaitingOrders");

        var bufferedDeadline = AddSecondsBounded(hardDeadline, -settings.DeadlineBufferSeconds);
        if (input.DelayRisk) return Dispatch("DelayRisk");
        if (now >= bufferedDeadline) return Dispatch("DeadlineApproaching");
        var collectionEnd = Earlier(AddSecondsBounded(queued, settings.CollectionSeconds), bufferedDeadline);
        if (now >= collectionEnd) return Dispatch("CollectionElapsed");
        if (input.AvailableDrivers == 0) return Dispatch("NoAvailableDrivers");
        if (input.PromisingPlanAvailable) return Dispatch("PromisingPlanAvailable");
        if (input.CompatibleOrders >= settings.MinimumCompatibleOrders)
            return Dispatch("CompatibleOrderThresholdReached");
        if (input.ExpectedNewOrders is null or <= 0m) return Dispatch("NoExpectedNewOrders");

        return new FoodDispatchTimingDecision(
            FoodDispatchTimingActionCodes.Collect, "ShortCollectionWindow", collectionEnd, hardDeadline,
            [FoodDispatchTimingActionCodes.Collect, FoodDispatchTimingActionCodes.DispatchNow]);
    }

    private static void ValidateSettings(FoodDispatchTimingSettings settings)
    {
        if (settings.CollectionSeconds < 0 || settings.MaxWaitSeconds is <= 0 or > 120
            || settings.CollectionSeconds > settings.MaxWaitSeconds
            || settings.MinimumCompatibleOrders < 1
            || settings.DeadlineBufferSeconds < 0 || settings.DeadlineBufferSeconds > settings.MaxWaitSeconds
            || settings.MaxSnapshotAgeSeconds is <= 0 or > 120)
            throw new ArgumentOutOfRangeException(nameof(settings),
                "수집은 최대 대기 이내, 최대 대기는 120초 이내이며 임계·자료 유효시간은 양수여야 합니다.");
    }

    private static DateTimeOffset Earlier(DateTimeOffset left, DateTimeOffset right)
        => left <= right ? left : right;

    private static DateTimeOffset AddSecondsBounded(DateTimeOffset value, int seconds)
    {
        if (seconds > 0 && value > DateTimeOffset.MaxValue.AddSeconds(-seconds))
            return DateTimeOffset.MaxValue;
        if (seconds < 0 && value < DateTimeOffset.MinValue.AddSeconds(-seconds))
            return DateTimeOffset.MinValue;
        return value.AddSeconds(seconds);
    }
}
