using System.Security.Cryptography;
using System.Text.Json;

namespace Ssalddel.DispatchTraining.Experiences;

/// <summary>검증된 합성 관측을 사람 검토용으로 묶고 균형 있게 고른다. 원장·의견·학습 상태는 변경하지 않는다.</summary>
public static class DispatchExperienceReviewSelector
{
    private const string Version = "dispatch-experience-review-selection.v1";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly string[] CategoryOrder =
        ["UnknownOutcome", "WorkIssue", "ModelReview", "ModelDisagreement", "NormalOutcome"];

    /// <summary>서명은 현재 보관된 관측의 유사성만 뜻하며 완전한 모델 입력이나 독립 학습 사례의 지문이 아니다.</summary>
    public static DispatchExperienceReviewSelection Select(DispatchExperienceSummary summary, int max = 5)
    {
        ArgumentNullException.ThrowIfNull(summary);
        if (max is < 1 or > 5) throw new ArgumentOutOfRangeException(nameof(max), "검토 대표 수는 1~5개여야 합니다.");
        if (!summary.Synthetic || summary.TrainingEligible || summary.WeightsTrained || summary.OperationalDispatchEnabled
            || summary.Orders.Any(x => !x.Synthetic || x.TrainingEligible || x.WeightsTrained || x.OperationalDispatchEnabled))
            throw new ArgumentException("합성·학습 제외·운영 OFF인 경험만 검토 후보로 선택할 수 있습니다.", nameof(summary));

        var observations = summary.Orders.SelectMany(Observations).ToArray();
        var groups = observations.GroupBy(x => x.SignatureSha256, StringComparer.Ordinal).Select(group =>
        {
            var rows = group.ToArray();
            var pending = rows.Where(x => NeedsReview(x.Order)).ToArray();
            var example = (pending.Length > 0 ? pending : rows)
                .OrderByDescending(x => x.LatestObservedAtUtc)
                .ThenBy(x => x.Order.RunId, StringComparer.Ordinal).ThenBy(x => x.Order.OrderIndex)
                .ThenBy(x => x.Order.ExperienceId, StringComparer.Ordinal)
                .ThenBy(x => x.DecisionId, StringComparer.Ordinal).First();
            return new DispatchExperienceReviewRepresentative
            {
                RepresentativeId = "review-" + group.Key,
                SignatureSha256 = group.Key, CategoryCode = example.CategoryCode,
                CategoryLabel = CategoryLabel(example.CategoryCode), ObservationKind = example.Kind,
                ReasonCodes = example.ReasonCodes.ToArray(),
                ExperienceId = example.Order.ExperienceId, RunId = example.Order.RunId,
                OrderIndex = example.Order.OrderIndex, SnapshotSha256 = example.Order.SnapshotSha256,
                DecisionId = example.DecisionId, ObservationCount = rows.Length,
                DistinctExperienceCount = rows.Select(x => x.Order.ExperienceId).Distinct(StringComparer.Ordinal).Count(),
                DistinctRunCount = rows.Select(x => x.Order.RunId).Distinct(StringComparer.Ordinal).Count(),
                PendingExperienceCount = pending.Select(x => x.Order.ExperienceId).Distinct(StringComparer.Ordinal).Count(),
                ReviewCurrent = example.Order.ReviewCurrent, ReviewOutcome = example.Order.ReviewOutcome,
                ObservedStateCode = example.Order.ObservedStateCode, ModelStatusCode = example.ModelStatusCode,
                LatestObservedAtUtc = example.LatestObservedAtUtc,
                RequiresHumanReview = pending.Length > 0
            };
        }).ToArray();

        var queues = CategoryOrder.ToDictionary(category => category,
            category => new Queue<DispatchExperienceReviewRepresentative>(groups
                .Where(x => x.CategoryCode == category && x.RequiresHumanReview)
                .OrderBy(ModelReviewPriority).ThenByDescending(x => x.LatestObservedAtUtc)
                .ThenByDescending(x => x.DistinctExperienceCount)
                .ThenBy(x => x.SignatureSha256, StringComparer.Ordinal)), StringComparer.Ordinal);
        var categoryOrder = CategoryOrder;
        if (max is >= 2 and < 5 && queues["NormalOutcome"].Count > 0)
            categoryOrder = CategoryOrder.Take(max - 1).Concat(["NormalOutcome"])
                .Concat(CategoryOrder.Skip(max - 1).Where(x => x != "NormalOutcome")).ToArray();
        List<DispatchExperienceReviewRepresentative> selected = [];
        // 반복 수가 많은 한 유형이 검토함을 차지하지 않도록 범주별로 한 개씩 순환한다.
        while (selected.Count < max)
        {
            var added = false;
            foreach (var category in categoryOrder)
            {
                if (selected.Count == max) break;
                if (!queues[category].TryDequeue(out var next)) continue;
                selected.Add(next); added = true;
            }
            if (!added) break;
        }

        return new()
        {
            Schema = Version, MaximumSelected = max,
            ExperienceCount = summary.Orders.Count,
            WorkflowObservationCount = summary.Orders.Count,
            DecisionObservationCount = summary.Orders.Sum(x => x.Decisions.Count),
            SignatureGroupCount = groups.Length,
            RepeatedObservationCount = observations.Length - groups.Length,
            PendingGroupCount = groups.Count(x => x.RequiresHumanReview),
            AlreadyReviewedGroupCount = groups.Count(x => !x.RequiresHumanReview),
            Selected = selected.ToArray()
        };
    }

    private static bool NeedsReview(DispatchExperienceOrder order)
        => !order.ReviewCurrent || order.ReviewOutcome is not ("approve" or "correct");

    private static int ModelReviewPriority(DispatchExperienceReviewRepresentative item)
        => item.ReasonCodes.Any(x => x is "ModelComparisonUnusable" or "ModelComparisonEvidenceMismatch") ? 0
            : item.ModelStatusCode is "BudgetExhausted" or "CallBudgetExhausted" ? 2
            : item.ModelStatusCode == "SingleCandidate" ? 3 : 1;

    private static IEnumerable<Observation> Observations(DispatchExperienceOrder order)
    {
        // 보관된 관측의 최신성일 뿐 상차·하차 같은 실제 단계의 수행 시각을 복원한 값이 아니다.
        var timestamps = order.Events.Select(x => x.AtUtc).Concat(order.Decisions.Select(x => x.RecordedAtUtc))
            .Where(x => x != default).ToArray();
        DateTime? latestObservedAtUtc = timestamps.Length == 0 ? null : timestamps.Max();
        var context = new
        {
            order.SubmissionStatusCode, order.ObservedStateCode, order.CanonicalReadSucceeded,
            canonicalOrderLinked = !string.IsNullOrWhiteSpace(order.CanonicalOrderId),
            missingEvidence = order.MissingEvidence.Order(StringComparer.Ordinal).Distinct(StringComparer.Ordinal).ToArray(),
            incidents = order.Incidents.Select(x => new { x.KindCode, x.StatusCode })
                .OrderBy(x => x.KindCode, StringComparer.Ordinal).ThenBy(x => x.StatusCode, StringComparer.Ordinal).Distinct().ToArray()
        };
        var unknown = !order.CanonicalReadSucceeded || string.IsNullOrWhiteSpace(order.CanonicalOrderId)
            || order.ObservedStateCode == "Unknown";
        var issue = order.SubmissionStatusCode == "FailedReview" || order.ObservedStateCode is "Failed" or "NotSubmitted"
            || order.Incidents.Any(x => x.StatusCode is not ("Resolved" or "Closed"));
        var workflowCategory = unknown ? "UnknownOutcome" : issue ? "WorkIssue"
            : order.ObservedStateCode == "Completed" ? "NormalOutcome" : "WorkIssue";
        var workflowReason = unknown ? "CanonicalOutcomeUnconfirmed" : issue ? "WorkflowNeedsReview"
            : order.ObservedStateCode == "Completed" ? "CanonicalCompletedObserved" : "WorkflowStillOpen";
        yield return new(order, "WorkflowOutcome", workflowCategory, [workflowReason], null, null,
            Hash(new { version = Version, kind = "WorkflowOutcome", context }), latestObservedAtUtc);

        foreach (var decision in order.Decisions)
        {
            var ai = decision.AiResult;
            var comparisonLinked = ai is not null && !string.IsNullOrWhiteSpace(ai.RuleDriverId)
                && !string.IsNullOrWhiteSpace(ai.ShadowDriverId);
            var inconsistentComparison = ai is { StatusCode: "Applied", SameChoice: not null }
                && comparisonLinked
                && string.Equals(ai.RuleDriverId, ai.ShadowDriverId, StringComparison.Ordinal) != ai.SameChoice.Value;
            var category = ai is null || ai.StatusCode != "Applied" || !comparisonLinked || ai.SameChoice is null || inconsistentComparison ? "ModelReview"
                : ai.SameChoice == false ? "ModelDisagreement" : "NormalOutcome";
            var reason = ai is null ? "ModelResultMissing"
                : ai.StatusCode is "SingleCandidate" or "BudgetExhausted" or "CallBudgetExhausted" ? "ModelComparisonSkipped"
                : ai.StatusCode != "Applied" ? "ModelComparisonUnusable"
                : inconsistentComparison ? "ModelComparisonEvidenceMismatch"
                : !comparisonLinked || ai.SameChoice is null ? "ModelComparisonUnconfirmed"
                : ai.SameChoice == false ? "RuleAndModelDisagree" : "RuleAndModelAgreeNotVerifiedCorrect";
            // 실측 실패·생략과 규칙/모델 일치를 분리한다. 같은 선택이어도 정답이나 업무 적용을 뜻하지 않는다.
            yield return new(order, "DecisionComparison", category, [reason], decision.DecisionId, ai?.StatusCode,
                Hash(new
                {
                    version = Version, kind = "DecisionComparison", context,
                    decision.RuleStatusCode, decision.RuleDriverLabel,
                    ai = ai is null ? null : new
                    {
                        ai.StatusCode, ai.RuleDriverId, ai.ShadowDriverId, ai.SameChoice, ai.ReasonCode, ai.ModelName
                    },
                    candidates = decision.Candidates.Select(x => new
                    {
                        x.Token, x.DriverLabel, x.EligibilityCode, x.DistanceKm, x.BaseScore, x.AgingScore, x.TotalScore
                    }).OrderBy(x => x.Token, StringComparer.Ordinal).ThenBy(x => x.DriverLabel, StringComparer.Ordinal)
                        .ThenBy(x => x.EligibilityCode, StringComparer.Ordinal).ThenBy(x => x.DistanceKm)
                        .ThenBy(x => x.BaseScore).ThenBy(x => x.AgingScore).ThenBy(x => x.TotalScore).ToArray()
                }), latestObservedAtUtc);
        }
    }

    private static string Hash<T>(T value) => Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value, Json)));
    private static string CategoryLabel(string category) => category switch
    {
        "UnknownOutcome" => "업무 결과 미확인",
        "WorkIssue" => "진행 중·문제 상황",
        "ModelReview" => "모델 실행·생략 확인",
        "ModelDisagreement" => "규칙과 모델의 다른 선택",
        "NormalOutcome" => "정상 흐름·일치 관측",
        _ => throw new InvalidOperationException("UnknownReviewCategory")
    };

    private sealed record Observation(DispatchExperienceOrder Order, string Kind, string CategoryCode,
        IReadOnlyList<string> ReasonCodes, string? DecisionId, string? ModelStatusCode, string SignatureSha256,
        DateTime? LatestObservedAtUtc);
}

/// <summary>현재 관측을 읽기 편하게 줄인 검토함이다. 독립 학습 사례 수·학습 적격·자동 승인을 생성하지 않는다.</summary>
public sealed record DispatchExperienceReviewSelection
{
    public string Schema { get; init; } = "dispatch-experience-review-selection.v1";
    public int MaximumSelected { get; init; }
    public int ExperienceCount { get; init; }
    public int WorkflowObservationCount { get; init; }
    public int DecisionObservationCount { get; init; }
    public int SignatureGroupCount { get; init; }
    public int RepeatedObservationCount { get; init; }
    public int PendingGroupCount { get; init; }
    public int AlreadyReviewedGroupCount { get; init; }
    public IReadOnlyList<DispatchExperienceReviewRepresentative> Selected { get; init; } = [];
    public string SignatureMeaning { get; init; } = "available_observations_only_not_complete_model_input";
    public string FreshnessMeaning { get; init; } = "latest_available_journal_or_decision_observation_not_stage_completion_time";
    public bool IndependentTrainingCasesVerified { get; init; }
    public bool Synthetic { get; init; } = true;
    public bool HumanApprovalCreated { get; init; }
    public bool TrainingEligible { get; init; }
    public bool WeightsTrained { get; init; }
    public bool OperationalDispatchEnabled { get; init; }
}

public sealed record DispatchExperienceReviewRepresentative
{
    public string RepresentativeId { get; init; } = "";
    public string SignatureSha256 { get; init; } = "";
    public string CategoryCode { get; init; } = "";
    public string CategoryLabel { get; init; } = "";
    public string ObservationKind { get; init; } = "";
    public IReadOnlyList<string> ReasonCodes { get; init; } = [];
    public string ExperienceId { get; init; } = "";
    public string RunId { get; init; } = "";
    public int OrderIndex { get; init; }
    public string SnapshotSha256 { get; init; } = "";
    public string? DecisionId { get; init; }
    public int ObservationCount { get; init; }
    public int DistinctExperienceCount { get; init; }
    public int DistinctRunCount { get; init; }
    public int PendingExperienceCount { get; init; }
    public bool RequiresHumanReview { get; init; }
    public bool ReviewCurrent { get; init; }
    public string? ReviewOutcome { get; init; }
    public string ObservedStateCode { get; init; } = "Unknown";
    public string? ModelStatusCode { get; init; }
    public DateTime? LatestObservedAtUtc { get; init; }
}
