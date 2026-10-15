using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Common.Metadata;

namespace 살뜰.도메인.배차;

[SsalddelCodeMetadata(
    SsalddelCodeFeatureKeys.OperationalDispatchCore,
    SsalddelCodeLayer.Domain,
    "음식 배차의 필수 정보 근거를 정규화하고 수집·전체 조율 준비를 각각 판정한다.",
    Effects = SsalddelCodeEffect.None,
    FlowOrder = 42,
    StepKey = "domain.food-dispatch-information-policy",
    DependsOnStepKeys = ["contract.food-dispatch-information"],
    ExecutionStage = SsalddelCodeExecutionStage.Preview,
    ReadsFrom = SsalddelCodeDataScope.OperationalState,
    WritesTo = SsalddelCodeDataScope.None,
    Boundary = "시계·DB·외부 API·모델을 호출하거나 원장을 변경하지 않는다. 확인된 0은 누락으로 바꾸지 않으며 수집 준비를 전체 배차 검증으로 승격하지 않는다.")]
public sealed class FoodDispatchInformationReadinessPolicy
{
    public const string CurrentPolicyVersion = "food-dispatch-information/v1";

    private static readonly string[] RequiredKeys =
    [
        FoodDispatchInformationKeys.Order,
        FoodDispatchInformationKeys.Preparation,
        FoodDispatchInformationKeys.Locations,
        FoodDispatchInformationKeys.OrderPool,
        FoodDispatchInformationKeys.Demand,
        FoodDispatchInformationKeys.DriverCapacity,
        FoodDispatchInformationKeys.CustomerPromise,
        FoodDispatchInformationKeys.CostConsent,
        FoodDispatchInformationKeys.DriverScheduleAndLoad,
        FoodDispatchInformationKeys.DriverQuote,
        FoodDispatchInformationKeys.PlatformFunds
    ];

    private static readonly HashSet<string> SafeSourceReasons = new(StringComparer.Ordinal)
    {
        "OrderNotFound", "OrderNotActive", "OrderLinkMismatch", "OrderChanged", "PreparationChanged",
        "LocationsMissing", "QueuePoolTruncated", "CurrentQueueNotObserved", "SnapshotReadFailed",
        "DemandUnknown", "DriverCapacityUnknown", "NotCapturedAtTimingStage", "CustomerPromiseNotCaptured",
        "CostConsentNotCaptured", "DriverScheduleAndLoadNotEvaluated", "DriverQuoteNotCalculated",
        "PlatformFundsNotRecorded", "OrderSourceUnavailable", "InvalidPreparation"
    };

    public FoodDispatchInformationReadiness Evaluate(
        bool applicable,
        DateTimeOffset now,
        int maxAgeSeconds,
        IReadOnlyList<FoodDispatchInformationEvidence> evidence)
    {
        if (maxAgeSeconds is < 1 or > 120)
            throw new ArgumentOutOfRangeException(nameof(maxAgeSeconds), "정보 유효시간은 1초 이상 120초 이하여야 합니다.");

        if (!applicable)
            return new(FoodDispatchInformationStateCodes.NotApplicable, FoodDispatchInformationStateCodes.NotApplicable,
                RequiredKeys.Select(key => new FoodDispatchInformationEvidence(key,
                    FoodDispatchInformationStateCodes.NotApplicable, "InformationNotApplicable")).ToArray())
            { MaxAgeSeconds = maxAgeSeconds };

        var normalized = RequiredKeys.Select(key => Normalize(key, now, maxAgeSeconds, evidence)).ToArray();
        return new(Aggregate(normalized.Take(6)), Aggregate(normalized), normalized)
        { MaxAgeSeconds = maxAgeSeconds };
    }

    private static FoodDispatchInformationEvidence Normalize(string key, DateTimeOffset now,
        int maxAgeSeconds, IReadOnlyList<FoodDispatchInformationEvidence>? evidence)
    {
        FoodDispatchInformationEvidence Missing(string reason)
            => new(key, FoodDispatchInformationStateCodes.Missing, reason);

        if (now == default) return Missing("InvalidEvaluationTime");
        var entries = evidence?.Where(item => item is not null && string.Equals(item.Key, key, StringComparison.Ordinal))
            .Take(2).ToArray() ?? [];
        if (entries.Length == 0) return Missing("EvidenceMissing");
        if (entries.Length != 1) return Missing("EvidenceDuplicate");
        var source = entries[0];
        if (source.StateCode is not (FoodDispatchInformationStateCodes.Current or FoodDispatchInformationStateCodes.Missing
            or FoodDispatchInformationStateCodes.Stale or FoodDispatchInformationStateCodes.NotApplicable))
            return Missing("UnknownState");
        if (source.SourceRevision is < 0) return Missing("InvalidRevision");
        if (source.StateCode == FoodDispatchInformationStateCodes.NotApplicable)
            return Missing("InformationRequired");
        if (source.StateCode == FoodDispatchInformationStateCodes.Current)
        {
            if (source.ObservedAtUtc is not { } observed || observed == default)
                return Missing("ObservationMissing");
            if (observed > now)
                return new(key, FoodDispatchInformationStateCodes.Stale, "ObservationFuture", observed, source.SourceRevision);
            if ((now - observed).TotalSeconds > maxAgeSeconds)
                return new(key, FoodDispatchInformationStateCodes.Stale, "ObservationStale", observed, source.SourceRevision);
            return new(key, FoodDispatchInformationStateCodes.Current, "InformationObserved", observed, source.SourceRevision);
        }

        var fallback = source.StateCode == FoodDispatchInformationStateCodes.Stale ? "InformationStale" : "InformationMissing";
        var safeReason = SafeSourceReasons.Contains(source.ReasonCode ?? string.Empty) ? source.ReasonCode! : fallback;
        return new(key, source.StateCode, safeReason, source.ObservedAtUtc, source.SourceRevision);
    }

    private static string Aggregate(IEnumerable<FoodDispatchInformationEvidence> evidence)
    {
        var states = evidence.Select(item => item.StateCode).ToArray();
        if (states.Contains(FoodDispatchInformationStateCodes.Stale, StringComparer.Ordinal))
            return FoodDispatchInformationStateCodes.Stale;
        return states.All(state => state == FoodDispatchInformationStateCodes.Current)
            ? FoodDispatchInformationStateCodes.Current : FoodDispatchInformationStateCodes.Missing;
    }
}
