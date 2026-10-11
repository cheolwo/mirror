using System.Text.Json;
using Ssalddel.Contracts.Common.Dispatch;
using 살뜰.도메인.배차;

namespace Ssalddel.LocalDispatchScenarios;

internal sealed record ScenarioPolicyObservation(int Minute, int OldestWaitingMinutes,
    bool ExplicitFaultPresent, JsonElement Input, JsonElement Decision);
internal sealed record ScenarioAdvisoryAlternative(string AlternativeId, string Kind,
    decimal AssignedWaitP95Minutes, decimal CompletedLeadP95Minutes, int UnassignedOrders,
    int UnfinishedOrders, string ReviewStatus);
internal sealed record ScenarioAdvisoryCase(string CaseId, string ScenarioId, string Title,
    int Minute, decimal PlanningThroughput, string[] SelectionReasons, JsonElement Input,
    JsonElement Decision, ScenarioAdvisoryAlternative[] Alternatives);
internal sealed record ScenarioAdvisoryDocument(int SchemaVersion, bool SyntheticOnly,
    string CampaignEvidenceId, string PolicyVersion, string CampaignDigest,
    string FixtureSha256, IReadOnlyDictionary<string, string> SourceHashes,
    string CasesFile, string CasesDigest, ScenarioAdvisoryCase[] Cases);

// 현재 관측만 모델에 전달한다. 알려진 미래 도착표는 oracle 비교의 완료 지표로만 구분한다.
internal static class ScenarioAdvisoryEvidence
{
    public const decimal PlanningThroughput = 3.5m;
    public const string CasesFile = "advisory-cases.json";
    private static readonly string[] AlternativeModes = ["observed", "policy_off", "over", "oracle"];

    public static ScenarioAdvisoryCase[] Select(CampaignSpec campaign, RunResult[] runs,
        IReadOnlyDictionary<string, List<ScenarioPolicyObservation>> observations)
    {
        var cases = new List<ScenarioAdvisoryCase>();
        foreach (var scenario in campaign.Scenarios)
        {
            if (!observations.TryGetValue(scenario.Id, out var snapshots)
                || snapshots.Count != campaign.HorizonMinutes + campaign.DrainMinutes)
                throw new InvalidOperationException("관측 방식·3.5건/h의 모든 정책 평가 사본이 필요합니다.");
            var ordered = snapshots.OrderBy(x => x.Minute).ToArray();
            var selected = new Dictionary<int, List<string>>();
            void Add(ScenarioPolicyObservation observation, string reason)
            {
                if (!selected.TryGetValue(observation.Minute, out var reasons))
                    selected[observation.Minute] = reasons = [];
                reasons.Add(reason);
            }
            Add(ordered[0], "start");
            var fault = ordered.FirstOrDefault(x => x.ExplicitFaultPresent);
            if (fault is not null) Add(fault, "first_explicit_fault");
            else
            {
                var exception = ordered.FirstOrDefault(IsException);
                if (exception is not null) Add(exception, "first_policy_exception");
                else
                {
                    var change = ordered.Skip(1).FirstOrDefault(x =>
                        DecisionSignature(x.Decision) != DecisionSignature(ordered[x.Minute - 1].Decision));
                    if (change is not null) Add(change, "first_substantive_decision_change");
                }
            }
            var maximumWait = ordered.OrderByDescending(x => x.OldestWaitingMinutes)
                .ThenByDescending(x => x.Input.GetProperty("대기주문수").GetInt32())
                .ThenBy(x => x.Minute).First();
            Add(maximumWait, "maximum_open_assignment_wait");
            var alternatives = AlternativeModes.Select(mode =>
            {
                var run = runs.Single(x => x.ScenarioId == scenario.Id
                    && x.PlanningThroughput == PlanningThroughput && x.Mode == mode);
                return new ScenarioAdvisoryAlternative(mode,
                    mode == "oracle" ? "known_future_upper_bound" : "synthetic_comparison",
                    run.AssignedWaitP95Minutes, run.CompletedLeadP95Minutes,
                    run.UnassignedOrders, run.UnfinishedOrders, run.ReviewStatus);
            }).ToArray();
            foreach (var (minute, reasons) in selected.OrderBy(x => x.Key))
            {
                var snapshot = ordered[minute];
                cases.Add(new(scenario.Id + "-" + minute.ToString("000", System.Globalization.CultureInfo.InvariantCulture),
                    scenario.Id, scenario.Title, minute, PlanningThroughput, reasons.ToArray(),
                    snapshot.Input, snapshot.Decision, alternatives));
            }
        }
        return cases.ToArray();
    }

    public static ScenarioAdvisoryDocument Document(string evidenceId, string campaignDigest,
        string fixtureHash, IReadOnlyDictionary<string, string> sourceHashes,
        ScenarioAdvisoryCase[] cases, byte[] casesBytes)
        => new(1, true, evidenceId, 음식배달수요참여Policy.현재정책판본,
            campaignDigest, fixtureHash, sourceHashes, CasesFile, ScenarioCampaign.Hash(casesBytes), cases);

    private static bool IsException(ScenarioPolicyObservation observation)
        => observation.Decision.GetProperty("데이터상태Code").GetString() != 음식배달수요자료상태Code.최신
            || observation.Decision.GetProperty("기사판정목록").EnumerateArray().Any(x =>
                x.GetProperty("사유Code").GetString() is "LongWaitOpportunity" or "OrderDelayCapacityException");

    private static string DecisionSignature(JsonElement decision)
        => ScenarioCampaign.Hash(new
        {
            dataStatus = decision.GetProperty("데이터상태Code").GetString(),
            reason = decision.GetProperty("사유Code").GetString(),
            target = decision.GetProperty("목표활동인원"),
            protectedCount = decision.GetProperty("보호활동기사수").GetInt32(),
            admissionCount = decision.GetProperty("추가참여인원").GetInt32(),
            drivers = decision.GetProperty("기사판정목록").EnumerateArray().Select(x => new
            {
                id = x.GetProperty("기사Id").GetString(),
                status = x.GetProperty("상태Code").GetString(),
                reason = x.GetProperty("사유Code").GetString(),
                allowed = x.GetProperty("신규제안허용").GetBoolean(),
                promoted = x.GetProperty("승격여부").GetBoolean(),
                priority = x.GetProperty("우선순위").GetInt32()
            }).ToArray()
        });
}
