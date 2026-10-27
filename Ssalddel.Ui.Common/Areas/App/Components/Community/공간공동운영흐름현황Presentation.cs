using Ssalddel.Contracts.Common.Community;

namespace Ssalddel.Ui.Common.Areas.App.Components.Community;

public sealed record 공간공동운영단계표시(string StageKey, string Tone, string Label,
    IReadOnlyList<공간공동운영준비보류Dto> Issues, IReadOnlyList<string> SourceLabels, int RelatedOrderCount);

public sealed record 공간공동운영흐름현황
{
    public 공간공동운영업무연결Model Flow { get; init; } = new();
    public string GateTone { get; init; } = "unknown";
    public string GateTitle { get; init; } = "회차 점검 확인 필요";
    public string GateMessage { get; init; } = "선택한 회차의 현재 점검 자료를 다시 확인해 주세요.";
    public bool GateIsCurrent { get; init; }
    public DateTimeOffset? CheckedAtUtc { get; init; }
    public IReadOnlyList<공간공동운영준비보류Dto> Issues { get; init; } = [];
    public IReadOnlyList<string> MissingRoleCodes { get; init; } = [];
    public IReadOnlyList<공간공동운영단계표시> Nodes { get; init; } = [];
    public int LinkedSourceCount { get; init; }
    public int LinkedOrderCount { get; init; }
    public string SettlementLabel { get; init; } = "정산 원장 미연결";
    public int PaymentReadyEvidenceCount { get; init; }
}

/// <summary>선택 회차의 서버 점검과 확인된 자료를 표시합니다. 업무 배정·완료·이익을 새로 판정하지 않습니다.</summary>
public static class 공간공동운영흐름현황Presentation
{
    public static 공간공동운영흐름현황 Build(공간공동운영상세Response detail, 공간공동운영통합Response snapshot, string? cycleId)
    {
        var flow = 공간공동운영업무연결Presentation.Build(detail, snapshot, cycleId);
        if (!flow.Available || flow.Cycle is null) return new() { Flow = flow };

        var gate = BuildCycleGate(detail.Readiness, snapshot, flow.Cycle);
        var nodes = flow.Nodes.Select(node => BuildStage(node, flow.Relations, snapshot.OrdersReadStatusCode, gate)).ToArray();
        var settlement = BuildSettlement(flow.Nodes.Single(node => node.Stage.Key == "settlement").Sources);
        return new()
        {
            Flow = flow, GateTone = gate.Tone, GateTitle = gate.Title, GateMessage = gate.Message,
            GateIsCurrent = gate.IsCurrent, CheckedAtUtc = gate.CheckedAtUtc, Issues = gate.Issues,
            MissingRoleCodes = gate.Issues.Where(issue => issue.ScopeCode == "cooperation").SelectMany(issue => issue.RoleCodes)
                .Distinct(StringComparer.Ordinal).ToArray(),
            Nodes = nodes,
            LinkedSourceCount = flow.Cycle.SharedSourceKeys.Count, LinkedOrderCount = flow.Cycle.Orders.Count,
            SettlementLabel = settlement.Label, PaymentReadyEvidenceCount = settlement.ReadyCount
        };
    }

    private static CycleGate BuildCycleGate(공간공동운영실행점검Dto? check, 공간공동운영통합Response snapshot,
        공간공동운영통합회차Dto cycle)
    {
        // 기존 서버는 마지막 회차만 점검합니다. 명시된 동일 회차의 결과만 사용하고 과거 회차에 전파하지 않습니다.
        var current = check is not null && check.CycleId == cycle.CycleId && check.CycleTermsRevision == cycle.TermsRevision
            && check.AsOfUtc != default && check.Issues is not null && check.Blockers is not null;
        var prepared = cycle.StatusCode == "prepared";
        var validIssues = current ? check!.Issues!.Where(ValidIssue).Select(CloneIssue).ToArray() : [];
        var aligned = current && validIssues.Length == check!.Issues!.Count
            && validIssues.Select(issue => issue.Message).SequenceEqual(check.Blockers!)
            && SourcesMatch(check, snapshot, cycle);
        var issues = aligned && prepared ? validIssues : [];
        var blocked = aligned && prepared && !check!.CanStart && issues.Length > 0;
        var gateTone = blocked ? "blocked" : aligned && prepared && check!.CanStart && issues.Length == 0 ? "ready"
            : cycle.StatusCode is "running" or "completed" ? "neutral" : "unknown";
        var gateTitle = gateTone switch
        {
            "blocked" => "회차 시작 보류", "ready" => "회차 시작 조건 확인",
            _ when cycle.StatusCode == "running" => "진행 중인 회차",
            _ when cycle.StatusCode == "completed" => "마친 회차의 기록",
            _ => "회차 점검 확인 필요"
        };
        var gateMessage = gateTone switch
        {
            "blocked" => "부족한 역할·동의·업무 연결을 먼저 확인해 주세요.",
            "ready" => "서버가 시작 조건을 확인했어요. 실제 시작은 운영 담당자가 선택해요.",
            _ when cycle.StatusCode == "running" => aligned && check!.CanComplete
                ? "원 업무의 마감 근거를 확인했어요. 회차 마침은 운영 담당자가 선택해요."
                : "각 원 업무의 진행과 마감 근거를 확인해 주세요.",
            _ when cycle.StatusCode == "completed" => "저장된 회차 기록과 현재 원 업무 확인을 구분해 보여요.",
            _ => "이 회차의 현재 점검을 확인하지 못했어요. 다른 회차의 막힘을 가져오지 않아요."
        };
        return new(aligned, blocked, aligned ? check!.AsOfUtc : null, issues, gateTone, gateTitle, gateMessage);
    }

    private static 공간공동운영단계표시 BuildStage(공간공동운영업무노드 node,
        IReadOnlyList<공간공동운영통합관계Dto> relations, string ordersReadStatusCode, CycleGate gate)
    {
        var stageIssues = gate.Issues.Where(issue => issue.StageKeys.Contains(node.Stage.Key, StringComparer.Ordinal)).ToArray();
        var attention = node.Sources.Any(source => !source.Verified || source.RequiresAttention);
        var unreadOrders = node.Stage.Key == "order" && ordersReadStatusCode != "current";
        var count = node.Sources.Count + node.Orders.Count;
        var tone = gate.Blocked && stageIssues.Length > 0 ? "blocked" : attention || unreadOrders ? "attention" : count > 0 ? "linked" : "neutral";
        var label = tone switch
        {
            "blocked" => "시작 전 보완 필요", "attention" => "현재 자료 확인 필요",
            "linked" when node.Stage.Key == "order" && node.Sources.Count == 0
                && node.Orders.All(order => order.RootReadStatusCode != "verified-link") => "주문 협의 연결",
            "linked" => "원장 연결 확인", _ => "연결 전"
        };
        var relationCount = relations.Where(relation => relation.RelationCode == "source-lineage"
            && node.Sources.Any(source => source.NodeKey == relation.ToKey)).Select(relation => relation.FromKey).Distinct(StringComparer.Ordinal).Count();
        return new(node.Stage.Key, tone, label, stageIssues,
            node.Sources.Select(source => source.Label).Distinct(StringComparer.Ordinal).ToArray(), relationCount);
    }

    private static (string Label, int ReadyCount) BuildSettlement(IReadOnlyList<공간공동운영통합원천Dto> settlements)
    {
        var readyCount = settlements.Count(source => source.Verified && !source.RequiresAttention && source.Completed);
        var settlementLabel = settlements.Count == 0 ? "정산 원장 미연결"
            : settlements.Any(source => !source.Verified || source.RequiresAttention) ? "정산 근거 재확인 필요"
            : readyCount == settlements.Count ? "연결된 정산의 지급 준비 확인" : "정산 원장 연결 · 지급 준비 확인 전";
        return (settlementLabel, readyCount);
    }

    private sealed record CycleGate(bool IsCurrent, bool Blocked, DateTimeOffset? CheckedAtUtc,
        IReadOnlyList<공간공동운영준비보류Dto> Issues, string Tone, string Title, string Message);

    private static bool ValidIssue(공간공동운영준비보류Dto? issue) => issue is not null && !string.IsNullOrWhiteSpace(issue.Code)
        && !string.IsNullOrWhiteSpace(issue.Message) && (issue.ScopeCode is "cooperation" or "cycle" or "source" or "required-appointment")
        && issue.StageKeys is not null && issue.RoleCodes is not null
        && issue.StageKeys.All(공간공동운영업무Routes.IsKnownStage)
        && issue.RoleCodes.All(role => 공간공동운영역할Codes.All.Contains(role, StringComparer.Ordinal))
        && (issue.ScopeCode == "source" || issue.StageKeys.Length == 0);
    private static 공간공동운영준비보류Dto CloneIssue(공간공동운영준비보류Dto issue) => new()
    { Code = issue.Code, ScopeCode = issue.ScopeCode, Message = issue.Message, StageKeys = issue.StageKeys.ToArray(), RoleCodes = issue.RoleCodes.ToArray() };

    private static bool SourcesMatch(공간공동운영실행점검Dto check, 공간공동운영통합Response snapshot, 공간공동운영통합회차Dto cycle)
    {
        // 전문 원천은 부모 원장 판본을 바꾸지 않고도 철회·진행될 수 있습니다. 뒤의 조회와 다르면 다시 확인합니다.
        if (check.Sources is null || snapshot.AsOfUtc == default || check.AsOfUtc > snapshot.AsOfUtc
            || check.Sources.Count != cycle.SharedSourceKeys.Count) return false;
        foreach (var key in cycle.SharedSourceKeys)
        {
            var earlier = check.Sources.Where(source => source is not null && $"source:{source.KindCode}:{source.ReferenceId}" == key).ToArray();
            var later = snapshot.SharedSources.Where(source => source.NodeKey == key).ToArray();
            if (earlier.Length != 1 || later.Length != 1 || earlier[0].Verified != later[0].Verified
                || earlier[0].Completed != later[0].Completed || earlier[0].RequiresAttention != later[0].RequiresAttention
                || earlier[0].StatusCode != later[0].StatusCode) return false;
        }
        return true;
    }
}
