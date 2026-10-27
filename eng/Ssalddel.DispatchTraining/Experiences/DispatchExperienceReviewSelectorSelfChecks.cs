using System.Text.Json;
using Ssalddel.Contracts.Admin.DispatchTraining;

namespace Ssalddel.DispatchTraining.Experiences;

/// <summary>파일·서버·모델 없이 합성 메모리 자료로 대표 선별과 승인 경계를 검증한다.</summary>
public static class DispatchExperienceReviewSelectorSelfChecks
{
    public static DispatchExperienceReviewSelectorSelfCheckReport Run()
    {
        List<DispatchExperienceReviewSelectorSelfCheckResult> checks = [];
        Check("empty-summary-does-not-invent-cases", () =>
        {
            var result = DispatchExperienceReviewSelector.Select(Summary());
            Require(result.Selected.Count == 0 && result.SignatureGroupCount == 0 && result.ExperienceCount == 0,
                "빈 기록에서 사례·승인·완료를 만들면 안 됩니다.");
            AssertBoundary(result);
        });
        Check("selection-limit-is-one-through-five", () =>
        {
            Reject<ArgumentOutOfRangeException>(() => DispatchExperienceReviewSelector.Select(Summary(), 0));
            Reject<ArgumentOutOfRangeException>(() => DispatchExperienceReviewSelector.Select(Summary(), 6));
            Reject<ArgumentNullException>(() => DispatchExperienceReviewSelector.Select(null!));
            foreach (var max in Enumerable.Range(1, 5))
                Require(DispatchExperienceReviewSelector.Select(BalancedSummary(), max).Selected.Count <= max,
                    "요청한 최대 수를 넘기면 안 됩니다.");
        });
        Check("repeated-decision-observations-are-not-independent-training-cases", () =>
        {
            var order = Order(0) with { Decisions = Enumerable.Range(0, 30).Select(x => Decision(x, "Timeout")).ToArray() };
            var result = DispatchExperienceReviewSelector.Select(Summary(order));
            Require(result.WorkflowObservationCount == 1 && result.DecisionObservationCount == 30
                && result.SignatureGroupCount == 2 && result.RepeatedObservationCount == 29,
                "업무 결과와 판단 관측 수를 구분하고 새 ID·시각만 다른 반복은 같은 묶음으로 줄여야 합니다.");
            var model = result.Selected.Single(x => x.ObservationKind == "DecisionComparison");
            Require(model.ObservationCount == 30 && model.DistinctExperienceCount == 1,
                "한 주문의 반복 30회를 독립 주문 30건으로 표시하면 안 됩니다.");
            AssertBoundary(result);
        });
        Check("important-candidate-state-changes-do-not-collapse", () =>
        {
            var first = Decision(0, "Timeout");
            var second = Decision(1, "Timeout") with
            { Candidates = [Candidate() with { DistanceKm = 2.5, TotalScore = 44 }] };
            var result = DispatchExperienceReviewSelector.Select(Summary(Order(0) with { Decisions = [first, second] }));
            Require(result.Selected.Count(x => x.ObservationKind == "DecisionComparison") == 2,
                "후보 수치가 달라진 관측을 시각·ID만 다른 중복처럼 합치면 안 됩니다.");
        });
        Check("candidate-order-and-source-order-do-not-change-signatures-or-selection", () =>
        {
            var candidates = new[] { Candidate(), Candidate() with { Token = "C2", DriverLabel = "D2", TotalScore = 40 } };
            var first = Order(0) with { Decisions = [Decision(0, "Timeout") with { Candidates = candidates }] };
            var second = Order(1) with { Decisions = [Decision(1, "Applied", sameChoice: false)] };
            var source = Summary(first, second);
            var reversed = Summary(second, first with { Decisions = [first.Decisions[0] with { Candidates = candidates.Reverse().ToArray() }] });
            var a = DispatchExperienceReviewSelector.Select(source);
            var b = DispatchExperienceReviewSelector.Select(reversed);
            Require(a.Selected.Select(x => x.SignatureSha256).SequenceEqual(b.Selected.Select(x => x.SignatureSha256)),
                "원본 나열 순서만 달라져도 검토 대표가 바뀌면 안 됩니다.");
        });
        Check("same-observations-across-runs-keep-occurrence-and-source-counts", () =>
        {
            var a = Order(0) with { Decisions = [Decision(0, "Timeout")] };
            var b = Order(1) with { RunId = "synthetic-run-B", Decisions = [Decision(1, "Timeout")] };
            var result = DispatchExperienceReviewSelector.Select(Summary(a, b));
            Require(result.SignatureGroupCount == 2 && result.Selected.All(x => x.ObservationCount == 2
                && x.DistinctExperienceCount == 2 && x.DistinctRunCount == 2),
                "유사한 두 회차도 원래 경험·회차 수를 잃거나 독립 학습 입력이라고 확정하면 안 됩니다.");
        });
        Check("new-run-normal-and-model-problem-representatives-precede-old-observations", () =>
        {
            var latest = new DateTime(2026, 10, 22, 0, 0, 0, DateTimeKind.Utc);
            var old = Order(0) with
            { RunId = "aaa-old-run", Decisions = Enumerable.Range(0, 100).Select(x => Decision(x, "Timeout")).ToArray() };
            var normal = Order(1) with
            { RunId = "zzz-new-run", Events = [new(1, latest, "SyntheticObservation", null, null)] };
            var modelProblem = Order(2) with
            { RunId = "zzz-new-run", Decisions = [Decision(200, "Timeout") with { RecordedAtUtc = latest }] };
            var summary = Summary(old, normal, modelProblem);
            var before = JsonSerializer.Serialize(summary);
            var result = DispatchExperienceReviewSelector.Select(summary);
            Require(result.Selected.Single(x => x.ObservationKind == "WorkflowOutcome").ExperienceId == normal.ExperienceId
                && result.Selected.Single(x => x.ObservationKind == "DecisionComparison").ExperienceId == modelProblem.ExperienceId,
                "과거 회차의 반복 수·사전순 ID 때문에 최신 정상·모델 문제 관측이 계속 가려지면 안 됩니다.");
            Require(result.Selected.All(x => x.LatestObservedAtUtc == latest),
                "최신성을 사건·판단 관측의 실제 보관 시각에 결속해야 합니다.");
            Require(JsonSerializer.Serialize(summary) == before && result.SignatureGroupCount == 2,
                "최신 대표 선택 때문에 원문을 수정하거나 관측 시각만 다른 반복을 새 내용 묶음으로 만들면 안 됩니다.");
        });
        Check("freshness-wins-over-group-size-within-the-same-model-problem-priority", () =>
        {
            var old = Enumerable.Range(0, 6).Select(x => Order(x) with
            { RunId = "aaa-old-run", Decisions = [Decision(x, "Timeout")] }).ToArray();
            var latest = new DateTime(2026, 10, 23, 0, 0, 0, DateTimeKind.Utc);
            var recent = Order(9) with { RunId = "zzz-new-run", Decisions = [Decision(99, "Timeout") with
                { RecordedAtUtc = latest, Candidates = [Candidate() with { DistanceKm = 8, TotalScore = 30 }] }] };
            var result = DispatchExperienceReviewSelector.Select(Summary(old.Append(recent).ToArray()), 1);
            Require(result.Selected.Single().ExperienceId == recent.ExperienceId
                && result.Selected.Single().DistinctExperienceCount == 1,
                "같은 우선순위에서는 과거 여러 주문의 유사 묶음보다 새로운 문제 관측을 먼저 검토해야 합니다.");
            var noTimestamp = DispatchExperienceReviewSelector.Select(Summary(Order(10))).Selected.Single();
            Require(noTimestamp.LatestObservedAtUtc is null,
                "원문에 관측 시각이 없으면 현재 시각이나 단계 완료 시각으로 채우면 안 됩니다.");
            AssertBoundary(result);
        });
        Check("five-category-coverage-survives-heavy-model-problem-repetition", () =>
        {
            var result = DispatchExperienceReviewSelector.Select(BalancedSummary());
            var expected = new[] { "UnknownOutcome", "WorkIssue", "ModelReview", "ModelDisagreement", "NormalOutcome" };
            Require(result.Selected.Count == 5 && expected.All(x => result.Selected.Any(r => r.CategoryCode == x)),
                "반복이 많은 모델 문제만 고르지 말고 미확인·문제·불일치·정상 흐름을 함께 보여야 합니다.");
            Require(result.Selected.Select(x => x.SignatureSha256).Distinct(StringComparer.Ordinal).Count() == result.Selected.Count,
                "같은 유사 묶음을 여러 대표 슬롯에 반복해서 넣으면 안 됩니다.");
        });
        Check("small-review-batch-reserves-normal-control-when-present", () =>
        {
            foreach (var max in new[] { 2, 3, 4 })
            {
                var result = DispatchExperienceReviewSelector.Select(BalancedSummary(), max);
                Require(result.Selected.Count == max && result.Selected.Any(x => x.CategoryCode == "NormalOutcome"),
                    "작은 검토 묶음도 문제가 있는 사례만으로 채우지 말아야 합니다.");
            }
        });
        Check("current-human-approval-is-not-repeated-or-modified", () =>
        {
            var order = Order(0) with
            { ReviewCurrent = true, ReviewRevision = 7, ReviewOutcome = "approve", Decisions = [Decision(0, "Timeout")] };
            var summary = Summary(order);
            var before = JsonSerializer.Serialize(summary);
            var result = DispatchExperienceReviewSelector.Select(summary);
            Require(result.Selected.Count == 0 && result.AlreadyReviewedGroupCount == 2 && result.PendingGroupCount == 0,
                "현재 사람이 확인한 관측을 새 미검토 사례로 만들면 안 됩니다.");
            Require(JsonSerializer.Serialize(summary) == before, "선별은 원본·검토 의견을 수정하면 안 됩니다.");
        });
        Check("deferred-and-stale-opinions-remain-reviewable-without-autoapproval", () =>
        {
            var deferred = Order(0) with { ReviewCurrent = true, ReviewOutcome = "defer", ReviewRevision = 2 };
            var stale = Order(1) with { ReviewCurrent = false, ReviewOutcome = "approve", ReviewRevision = 5 };
            var result = DispatchExperienceReviewSelector.Select(Summary(deferred, stale));
            Require(result.PendingGroupCount == 1 && result.Selected.Single().PendingExperienceCount == 2,
                "보류와 이전 판본의 의견을 현재 승인으로 승계하면 안 됩니다.");
            AssertBoundary(result);
        });
        Check("unreviewed-source-is-used-as-representative-of-mixed-group", () =>
        {
            var reviewed = Order(0) with { ReviewCurrent = true, ReviewOutcome = "correct", ReviewRevision = 1 };
            var pending = Order(1);
            var group = DispatchExperienceReviewSelector.Select(Summary(reviewed, pending)).Selected.Single();
            Require(group.ExperienceId == pending.ExperienceId && group.ObservationCount == 2
                && group.PendingExperienceCount == 1 && !group.ReviewCurrent,
                "검토된 유사 자료가 있어도 아직 판단하지 않은 원본을 대표로 제시해야 합니다.");
        });
        Check("timeouts-are-not-model-disagreements-or-successes", () =>
        {
            var order = Order(0) with
            { Decisions = [Decision(0, "Timeout", sameChoice: false), Decision(1, "Applied", sameChoice: false)] };
            var result = DispatchExperienceReviewSelector.Select(Summary(order));
            Require(result.Selected.Single(x => x.DecisionId == "decision-0").CategoryCode == "ModelReview"
                && result.Selected.Single(x => x.DecisionId == "decision-1").CategoryCode == "ModelDisagreement",
                "실패한 응답의 비교 필드가 false여도 유효한 다른 선택으로 세면 안 됩니다.");
        });
        Check("single-candidate-and-budget-exhaustion-remain-explicit-skips", () =>
        {
            var order = Order(0) with { Decisions = [Decision(0, "SingleCandidate"), Decision(1, "BudgetExhausted")] };
            var result = DispatchExperienceReviewSelector.Select(Summary(order));
            Require(result.Selected.Where(x => x.ObservationKind == "DecisionComparison")
                .All(x => x.CategoryCode == "ModelReview" && x.ReasonCodes.Contains("ModelComparisonSkipped", StringComparer.Ordinal)),
                "호출 생략을 새 모델 판단 성공이나 잘못된 AI 응답으로 표시하면 안 됩니다.");
        });
        Check("real-model-failure-has-priority-over-benign-skips-within-category", () =>
        {
            var order = Order(0) with { Decisions = [Decision(2, "Timeout")] };
            var newer = Order(1) with { Decisions = [Decision(1, "BudgetExhausted") with
                { RecordedAtUtc = new DateTime(2026, 10, 24, 0, 0, 0, DateTimeKind.Utc) }] };
            var single = Order(2) with { Decisions = [Decision(0, "SingleCandidate")] };
            var result = DispatchExperienceReviewSelector.Select(Summary(order, newer, single), 2);
            Require(result.Selected.Single(x => x.CategoryCode == "ModelReview").ModelStatusCode == "Timeout",
                "모델 실행 문제를 확인할 때 정상적인 단일 후보 생략보다 실패를 먼저 보여야 합니다.");
        });
        Check("canonical-link-missing-is-not-normal-completion", () =>
        {
            var order = Order(0) with { CanonicalOrderId = null, CanonicalReadSucceeded = true, ObservedStateCode = "Completed" };
            Require(DispatchExperienceReviewSelector.Select(Summary(order)).Selected.Single().CategoryCode == "UnknownOutcome",
                "정본 연결 없는 완료 표시는 정상 완료 대표가 아닙니다.");
        });
        Check("resolved-and-closed-incidents-preserve-confirmed-completion", () =>
        {
            foreach (var status in new[] { "Resolved", "Closed" })
            {
                var summary = Summary(Order(0) with
                { Incidents = [new("incident-0", "DriverShortage", status, new DateTime(2026, 10, 21, 0, 0, 0, DateTimeKind.Utc))] });
                var before = JsonSerializer.Serialize(summary);
                var result = DispatchExperienceReviewSelector.Select(summary);
                var workflow = result.Selected.Single();
                Require(workflow.CategoryCode == "NormalOutcome"
                    && workflow.ReasonCodes.Contains("CanonicalCompletedObserved", StringComparer.Ordinal),
                    "서버가 종결한 Resolved·Closed 사건 때문에 정본 완료를 열린 업무 문제로 분류하면 안 됩니다.");
                Require(JsonSerializer.Serialize(summary) == before,
                    "종결 사건을 해석할 때 원래 상태·사건 이력을 바꾸면 안 됩니다.");
                AssertBoundary(result);
            }
        });
        Check("open-or-case-mismatched-incidents-still-require-review", () =>
        {
            foreach (var status in new[] { "Open", "Acknowledged", "closed", "resolved", "Unknown", "" })
            {
                var order = Order(0) with
                { Incidents = [new("incident-0", "DriverShortage", status, new DateTime(2026, 10, 21, 0, 0, 0, DateTimeKind.Utc))] };
                var workflow = DispatchExperienceReviewSelector.Select(Summary(order)).Selected.Single();
                Require(workflow.CategoryCode == "WorkIssue"
                    && workflow.ReasonCodes.Contains("WorkflowNeedsReview", StringComparer.Ordinal),
                    "정본 완료라도 미종결 사건은 검토해야 하며 서버와 다른 대소문자 완화를 적용하면 안 됩니다.");
            }
            var unfinished = Order(0) with { ObservedStateCode = "Assigned",
                Incidents = [new("incident-0", "DriverShortage", "Closed", new DateTime(2026, 10, 21, 0, 0, 0, DateTimeKind.Utc))] };
            Require(DispatchExperienceReviewSelector.Select(Summary(unfinished)).Selected.Single().CategoryCode == "WorkIssue",
                "사건 종결만으로 아직 진행 중인 업무를 완료 처리하면 안 됩니다.");
        });
        Check("normal-agreement-is-only-an-observation-not-a-correct-answer", () =>
        {
            var order = Order(0) with { Decisions = [Decision(0, "Applied", sameChoice: true)] };
            var result = DispatchExperienceReviewSelector.Select(Summary(order));
            Require(result.Selected.Single(x => x.ObservationKind == "DecisionComparison")
                .ReasonCodes.Contains("RuleAndModelAgreeNotVerifiedCorrect", StringComparer.Ordinal),
                "규칙과 AI의 일치를 사람의 정답 확인으로 바꾸면 안 됩니다.");
            AssertBoundary(result);
        });
        Check("contradictory-choice-metadata-is-not-labelled-normal-agreement", () =>
        {
            var decision = Decision(0, "Applied", sameChoice: true);
            var contradictory = decision with { AiResult = decision.AiResult! with { ShadowDriverId = "D2" } };
            var result = DispatchExperienceReviewSelector.Select(Summary(Order(0) with { Decisions = [contradictory] }));
            var model = result.Selected.Single(x => x.ObservationKind == "DecisionComparison");
            Require(model.CategoryCode == "ModelReview"
                && model.ReasonCodes.Contains("ModelComparisonEvidenceMismatch", StringComparer.Ordinal),
                "서로 다른 선택 ID와 일치 표시가 충돌하면 정상 일치로 제시하면 안 됩니다.");
        });
        Check("missing-rule-or-model-choice-id-cannot-prove-agreement-or-disagreement", () =>
        {
            foreach (var sameChoice in new[] { true, false })
            {
                var decision = Decision(0, "Applied", sameChoice);
                foreach (var ai in new[]
                {
                    decision.AiResult! with { RuleDriverId = null },
                    decision.AiResult! with { ShadowDriverId = null },
                    decision.AiResult! with { RuleDriverId = " ", ShadowDriverId = "" }
                })
                {
                    var result = DispatchExperienceReviewSelector.Select(Summary(Order(0) with
                        { Decisions = [decision with { AiResult = ai }] }));
                    var model = result.Selected.Single(x => x.ObservationKind == "DecisionComparison");
                    Require(model.CategoryCode == "ModelReview"
                        && model.ReasonCodes.Contains("ModelComparisonUnconfirmed", StringComparer.Ordinal),
                        "양쪽 선택 ID를 확인하지 못하면 일치·불일치 표시만으로 정상 비교를 확정하면 안 됩니다.");
                }
            }
        });
        Check("unsafe-or-nonsynthetic-boundaries-are-not-silently-relabelled", () =>
        {
            foreach (var invalid in new[]
            {
                Summary(Order(0)) with { Synthetic = false },
                Summary(Order(0)) with { TrainingEligible = true },
                Summary(Order(0)) with { WeightsTrained = true },
                Summary(Order(0)) with { OperationalDispatchEnabled = true },
                Summary(Order(0) with { Synthetic = false })
            }) Reject<ArgumentException>(() => DispatchExperienceReviewSelector.Select(invalid));
        });
        Check("unavailable-categories-are-not-filled-with-invented-examples", () =>
        {
            var result = DispatchExperienceReviewSelector.Select(Summary(Order(0) with
                { CanonicalReadSucceeded = false, ObservedStateCode = "Unknown", CanonicalOrderId = null }));
            Require(result.Selected.Count == 1 && result.Selected[0].CategoryCode == "UnknownOutcome",
                "정상·불일치 사례가 없으면 빈 범주를 가상 사례로 자동 채우면 안 됩니다.");
        });

        return new(checks);

        void Check(string name, Action action)
        {
            try { action(); checks.Add(new(name, true, null)); }
            catch (Exception error) when (error is not OutOfMemoryException) { checks.Add(new(name, false, error.Message)); }
        }
    }

    private static DispatchExperienceSummary Summary(params DispatchExperienceOrder[] orders) => new()
    { RunCount = orders.Select(x => x.RunId).Distinct(StringComparer.Ordinal).Count(), SnapshotCount = orders.Length, Orders = orders };

    private static DispatchExperienceOrder Order(int index) => new()
    {
        ExperienceId = "synthetic-experience-" + index, RunId = "synthetic-run-A", OrderIndex = index,
        SnapshotSha256 = new string('a', 64), CanonicalOrderId = "synthetic-canonical-" + index,
        ClientRequestId = GuidFrom(index), SubmissionStatusCode = "Confirmed", ObservedStateCode = "Completed",
        CanonicalReadSucceeded = true, MissingEvidence = ["OrderConditions", "ModelDigest", "TrainingCaseLink"]
    };

    private static DispatchTrainingDecisionCandidateDto Candidate() => new()
    {
        Token = "C1", DriverLabel = "D1", EligibilityCode = "CandidateAllowed", DistanceKm = 1,
        BaseScore = 50, AgingScore = 5, TotalScore = 55
    };

    private static DispatchExperienceDecision Decision(int index, string status, bool? sameChoice = null) => new(
        "decision-" + index, new DateTime(2026, 10, 21, 0, 0, 0, DateTimeKind.Utc).AddSeconds(index), "Selected", "D1",
        new() { StatusCode = status, RuleDriverId = "D1", ShadowDriverId = status == "Applied" ? sameChoice == true ? "D1" : "D2" : null,
            SameChoice = sameChoice, ReasonCode = status, ModelName = "synthetic-model", ElapsedMilliseconds = index }, [Candidate()]);

    private static DispatchExperienceSummary BalancedSummary() => Summary(
        Order(0) with { CanonicalReadSucceeded = false, CanonicalOrderId = null, ObservedStateCode = "Unknown" },
        Order(1) with { ObservedStateCode = "Failed" },
        Order(2) with { Decisions = Enumerable.Range(0, 100).Select(x => Decision(x, "Timeout")).ToArray() },
        Order(3) with { Decisions = [Decision(101, "Applied", sameChoice: false)] }, Order(4));

    private static Guid GuidFrom(int index)
    {
        var bytes = new byte[16]; BitConverter.GetBytes(index + 1).CopyTo(bytes, 0); return new Guid(bytes);
    }

    private static void AssertBoundary(DispatchExperienceReviewSelection result)
    {
        Require(result.Synthetic && !result.IndependentTrainingCasesVerified && !result.HumanApprovalCreated
            && !result.TrainingEligible && !result.WeightsTrained && !result.OperationalDispatchEnabled,
            "대표 선별을 학습·자동 승인·운영 적용으로 승격하면 안 됩니다.");
        Require(result.SignatureMeaning == "available_observations_only_not_complete_model_input",
            "유사 서명을 완전한 모델 입력이나 독립 학습 사례의 증거로 표시하면 안 됩니다.");
        Require(result.FreshnessMeaning == "latest_available_journal_or_decision_observation_not_stage_completion_time",
            "관측 최신 시각을 실제 업무 단계의 완료 시각으로 해석하면 안 됩니다.");
    }

    private static void Reject<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new InvalidOperationException("거부해야 하는 선택 입력을 허용했습니다.");
    }

    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
}

public sealed record DispatchExperienceReviewSelectorSelfCheckResult(string Name, bool Passed, string? Failure);
public sealed record DispatchExperienceReviewSelectorSelfCheckReport(IReadOnlyList<DispatchExperienceReviewSelectorSelfCheckResult> Checks)
{
    public bool Passed => Checks.All(x => x.Passed);
    public int PassedCount => Checks.Count(x => x.Passed);
    public int FailedCount => Checks.Count(x => !x.Passed);
}
