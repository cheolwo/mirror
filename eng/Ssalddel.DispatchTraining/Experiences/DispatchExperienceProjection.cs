using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Ssalddel.DispatchTraining.Experiences;

/// <summary>훈련 관측을 주문별로 연결한다. 조회되지 않은 사실이나 업무 완료를 추정하지 않는다.</summary>
internal static class DispatchExperienceProjection
{
    internal const string Version = "dispatch-experience-projection.v1";
    internal static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    internal static string RunKey(string runId) => Hash(Encoding.UTF8.GetBytes(runId));
    internal static DispatchExperienceException Error(string code) => new(code);

    internal static DispatchExperienceSnapshot Project(DispatchTrainingSessionState state, string sourceSha)
    {
        DispatchTrainingSessionStore.Validate(state);
        if (string.IsNullOrWhiteSpace(state.RunId) || state.RunId.Length > 256) throw Error("RunIdRequired");
        var slots = state.Slots.ToDictionary(x => x.Index);
        var canonical = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var slot in state.Slots)
            if (slot.CanonicalOrderId is { Length: > 0 } id && !canonical.TryAdd(id, slot.Index))
                throw Error("DuplicateCanonicalOrder");
        var observations = new Dictionary<int, DispatchTrainingOrderObservation>();
        foreach (var observation in state.LastObservation.Orders)
        {
            if (!slots.TryGetValue(observation.Index, out var slot)) throw Error("ObservationIndexMismatch");
            if (slot.ClientRequestId != observation.ClientRequestId) throw Error("ObservationRequestMismatch");
            if (!string.Equals(slot.CanonicalOrderId, observation.CanonicalOrderId, StringComparison.Ordinal))
                throw Error("ObservationCanonicalMismatch");
            if (!observations.TryAdd(observation.Index, observation)) throw Error("DuplicateObservation");
        }
        foreach (var e in state.Events.Where(x => x.OrderIndex.HasValue))
        {
            if (!slots.ContainsKey(e.OrderIndex!.Value)) throw Error("EventIndexMismatch");
        }
        if (state.Decisions.Any(x => string.IsNullOrWhiteSpace(x.DecisionId) || x.Candidates is null)
            || state.Decisions.Select(x => x.DecisionId).Distinct(StringComparer.Ordinal).Count() != state.Decisions.Count)
            throw Error("DecisionIdentityInvalid");
        var attempted = state.Slots.Where(x => x.CanonicalOrderId is { Length: > 0 }
            || x.StatusCode is "Confirmed" or "Submitting" or "Unknown" or "FailedReview").ToArray();
        var orders = attempted.OrderBy(x => x.Index).Select(slot =>
        {
            observations.TryGetValue(slot.Index, out var observation);
            var canonicalRead = state.LastObservation.CanonicalReadSucceeded && observation is not null
                && !string.IsNullOrWhiteSpace(slot.CanonicalOrderId);
            var observedState = canonicalRead && (observation!.StateCode is
                "Waiting" or "Assigned" or "InProgress" or "Completed" or "Failed" or "NotSubmitted")
                    ? observation.StateCode : "Unknown";
            List<string> missing = ["OrderConditions", "StageTimestamps", "PolicyFingerprint", "ModelDigest",
                "PhotoEvidence", "NotificationDelivery", "DriverResponseTimeline", "Settlement", "Payment", "TrainingCaseLink"];
            if (!canonicalRead) missing.Add("CanonicalObservation");
            if (slot.CanonicalOrderId is null) missing.Add("CanonicalOrderLink");
            return new DispatchExperienceOrder
            {
                ExperienceId = "exp-" + Hash(Encoding.UTF8.GetBytes(state.RunId + "|" + slot.Index.ToString(CultureInfo.InvariantCulture)
                    + "|" + slot.ClientRequestId.ToString("D"))),
                RunId = state.RunId, OrderIndex = slot.Index, ClientRequestId = slot.ClientRequestId,
                CanonicalOrderId = slot.CanonicalOrderId, Revision = state.Revision, SnapshotSha256 = sourceSha,
                SubmissionStatusCode = slot.StatusCode, ObservedStateCode = observedState, CanonicalReadSucceeded = canonicalRead,
                Decisions = state.Decisions.Where(x => x.OrderId is not null && x.OrderId == slot.CanonicalOrderId)
                    .Select(x => new DispatchExperienceDecision(x.DecisionId, x.RecordedAtUtc, x.RuleStatusCode,
                        x.RuleDriverLabel, x.AiResult, x.Candidates)).ToArray(),
                // 회차 시작은 예전 Events를 지우지 않는다. 인덱스만 같은 과거 요청을 현재 주문에 붙이지 않는다.
                Events = state.Events.Where(x => x.OrderIndex == slot.Index && x.ClientRequestId == slot.ClientRequestId)
                    .Select(x => new DispatchExperienceEvent(x.Sequence, x.AtUtc, x.KindCode, x.ClientRequestId, x.DetailCode)).ToArray(),
                Incidents = state.Incidents.Where(x => x.OrderId is not null && x.OrderId == slot.CanonicalOrderId)
                    .Select(x => new DispatchExperienceIncident(x.IncidentId, x.KindCode, x.StatusCode, x.RecordedAtUtc)).ToArray(),
                MissingEvidence = missing
            };
        }).ToArray();
        return new(orders,
            state.Decisions.Count(x => x.OrderId is null || !canonical.ContainsKey(x.OrderId)),
            state.Incidents.Count(x => x.OrderId is null || !canonical.ContainsKey(x.OrderId)),
            state.Events.Count - orders.Sum(x => x.Events.Count),
            state.Slots.Count, state.Slots.Count(x => x.StatusCode == "Scheduled"), state.Slots.Count(x => x.StatusCode == "Skipped"));
    }
}

internal sealed record DispatchExperienceSnapshot(IReadOnlyList<DispatchExperienceOrder> Orders,
    int UnlinkedDecisionCount, int UnlinkedIncidentCount, int UnlinkedEventCount,
    int TotalSlotCount, int ScheduledSlotCount, int SkippedSlotCount);
