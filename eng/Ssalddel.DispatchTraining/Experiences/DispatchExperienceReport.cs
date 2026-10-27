using System.Text;
using System.Text.Json;
using System.Security.Cryptography;

namespace Ssalddel.DispatchTraining.Experiences;

/// <summary>검증된 원장의 파생 조회 보고서를 만든다. 원문·사람 검토를 수정하지 않는다.</summary>
internal static class DispatchExperienceReport
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static async Task WriteAsync(string root, DispatchExperienceSummary summary, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(summary, Json);
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
        var reports = Path.Combine(root, "reports");
        DispatchExperienceCommand.CheckLinks(reports);
        Directory.CreateDirectory(reports);
        var snapshotName = hash + ".json";
        var snapshotPath = Path.Combine(reports, snapshotName);
        DispatchExperienceCommand.CheckLinks(snapshotPath);
        if (File.Exists(snapshotPath))
        {
            if (await File.ReadAllTextAsync(snapshotPath, cancellationToken) != json)
                throw new DispatchExperienceException("ReportSnapshotCorrupt");
        }
        else await AtomicReportAsync(snapshotPath, json, cancellationToken);
        var text = new StringBuilder("# 배차 운영 경험 누적 현황\n\n");
        text.AppendLine($"생성 시각(UTC): {DateTimeOffset.UtcNow:O}\n");
        text.AppendLine("기존 로컬 가상 운영 기록을 보관·색인한 결과입니다. 실제 운영·모델 학습·사람 승인과 구분합니다.\n");
        text.AppendLine($"- 회차 {summary.RunCount}개 / 원문 판본 {summary.SnapshotCount}개 / 접수 시도 경험 {summary.Orders.Count}건");
        text.AppendLine($"- 전체 일정 {summary.TotalSlotCount}슬롯 중 예약 {summary.ScheduledSlotCount}개·생략 {summary.SkippedSlotCount}개는 별도 집계");
        text.AppendLine($"- 접수 확인 {summary.Orders.Count(x => x.SubmissionStatusCode == "Confirmed")}개 / 정본에서 완료 관측 {summary.Orders.Count(x => x.ObservedStateCode == "Completed")}개");
        text.AppendLine($"- 현재 판본의 사람 검토 {summary.Orders.Count(x => x.ReviewCurrent)}개 / 주문별 연결 밖의 판단 {summary.UnlinkedDecisionCount}개·문제 기록 {summary.UnlinkedIncidentCount}개·사건 {summary.UnlinkedEventCount}개");
        text.AppendLine("- 실제 배차 활성화·학습 적격·가중치 학습: 모두 false\n");
        text.AppendLine("연결 밖에는 회차 공통 관측·이전 회차 사건·접수되지 않은 슬롯 기록이 포함됩니다. 원문별 기록 수로, 중복될 수 있으며 미해결 주문 건수가 아닙니다.\n");
        text.AppendLine("```text\n가상 회차 원문\n└─ 회차·판본·해시 확인 → 동일 재접수는 재사용\n   └─ 주문별 접수·판단·관측 결과\n      ├─ 미확보 자료 → 미확인으로 보존\n      └─ 나중에 사용자와 검토 → 의견을 별도 누적\n         └─ 새 관측 → 이전 의견 보존·새 판본 재검토\n```\n");
        var selection = DispatchExperienceReviewSelector.Select(summary);
        await WriteRepresentativesAsync(root, summary, selection, snapshotName, cancellationToken);
        text.AppendLine($"## 함께 볼 사례\n\n[대표 사례 {selection.Selected.Count}개 보기](current-representatives.md). 관측 {selection.WorkflowObservationCount + selection.DecisionObservationCount}개를 내용 기준 {selection.SignatureGroupCount}묶음으로 나누고, 미확인·업무 문제·모델 비교·정상 결과를 골랐습니다. 반복 관측은 독립 학습 사례가 아닙니다.\n");
        foreach (var run in summary.Orders.GroupBy(x => x.RunId))
        {
            text.AppendLine($"## 회차 `{Cell(run.Key)}`\n");
            text.AppendLine("| 슬롯 | 접수 | 마지막 정본 관측 | 판단 수 | 검토 | 경험 ID |\n| --- | --- | --- | ---: | --- | --- |");
            foreach (var order in run.OrderBy(x => x.OrderIndex))
            {
                var review = order.ReviewOutcome is null ? "미검토" : order.ReviewCurrent ? order.ReviewOutcome : "이전 판본 검토";
                text.AppendLine($"| {order.OrderIndex + 1} | {Cell(Submission(order.SubmissionStatusCode))} | {Cell(Observed(order.ObservedStateCode))} | {order.Decisions.Count} | {Cell(review)} | `{Cell(order.ExperienceId[..12])}` |");
            }
            text.AppendLine();
        }
        text.AppendLine("## 다음 사용\n\n운영 연습 뒤 Codex에 **‘대표 사례부터 같이 검토하자’**라고 요청하면 됩니다. 유한 시간 캠페인을 명시적으로 실행하면 중간 결과가 주기적으로 접수됩니다. 이 조회 명령 자체는 캠페인·자동 승인·학습을 시작하지 않습니다.\n");
        text.AppendLine($"기존 원문에 없던 주문 조건·단계별 시각·정책/모델 해시·증빙/정산은 미확보입니다. 회차 종료나 접수 확인을 배송 완료로 세지 않습니다. 표의 경험 ID는 앞 12자리이며, 검토 명령에는 [이 보고서와 같은 판본의 전체 자료](reports/{snapshotName})에 있는 전체 ID와 판본·revision을 사용합니다. 이 파일에는 세부 사건과 원장 참조도 있습니다.\n");
        text.AppendLine("새 파일 접수와 검토 명령은 `eng/Ssalddel.DispatchTraining/README.md`를 따릅니다. 이 보고서는 다시 만드는 조회 자료이며 원문·검토 원장의 정본이 아닙니다.");
        await AtomicReportAsync(Path.Combine(root, "current-summary.json"), json, cancellationToken);
        await AtomicReportAsync(Path.Combine(root, "current-summary.md"), text.ToString(), cancellationToken);
    }

    private static async Task WriteRepresentativesAsync(string root, DispatchExperienceSummary summary, DispatchExperienceReviewSelection selection,
        string snapshotName, CancellationToken cancellationToken)
    {
        var text = new StringBuilder("# 나중에 함께 볼 배차 대표 사례\n\n");
        var packet = JsonSerializer.Serialize(new { schemaVersion = "dispatch-experience-review-packet.v1",
            sourceSummaryReport = snapshotName, selection }, Json);
        var packetHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(packet)));
        var packetName = "review-" + packetHash + ".json";
        await AtomicReportAsync(Path.Combine(root, "reports", packetName), packet, cancellationToken, immutable: true);
        text.AppendLine($"가상 주문 경험 {selection.ExperienceCount}건 / 관측 내용 {selection.SignatureGroupCount}묶음 / 대표 {selection.Selected.Count}개.\n");
        text.AppendLine($"검토 목록 ID: `{packetHash}` · [이번 대표 목록 고정본](reports/{packetName}). 실행 중 새 관측이 들어오면 목록이 바뀔 수 있으므로 의견은 이 고정본의 경험 ID·원문 SHA에 연결합니다.\n");
        text.AppendLine("반복 횟수는 신뢰도나 학습량 점수가 아닙니다. 화물 수행은 기존 규칙, Ollama는 비교 전용입니다. 사람 승인·가중치 학습·실제 운영은 실행하지 않았습니다.\n");
        text.AppendLine("```text\n가상 주문 접수\n└─ 규칙 추천 → 가상 기사 수락 → 픽업 → 전달 완료 정본 조회\n   ├─ 정상 결과 → 비교할 대표 사례\n   ├─ 실패·미확인 → 결과와 막힌 단계 보존\n   └─ Ollama 비교 → 성공/다른 선택/시간 초과를 별도 보존\n      └─ 사용자와 검토 → 명시적 의견 저장 (자동 승인 없음)\n```\n");
        for (var i = 0; i < selection.Selected.Count; i++)
        {
            var item = selection.Selected[i];
            text.AppendLine($"## {i + 1}. {Cell(item.CategoryLabel)} — 슬롯 {item.OrderIndex + 1}\n");
            text.AppendLine($"- 관측: {(item.ObservationKind == "WorkflowOutcome" ? "업무 결과" : "모델 판단 비교")} / 정본 상태: {Cell(Observed(item.ObservedStateCode))} / 모델: {Cell(item.ModelStatusCode ?? "해당 없음")}");
            text.AppendLine($"- 같은 내용 관측 {item.ObservationCount}개, 주문 경험 {item.DistinctExperienceCount}건, 회차 {item.DistinctRunCount}개. 입력 전체가 같다는 검증은 아닙니다.");
            text.AppendLine($"- 선별 근거: {string.Join(", ", item.ReasonCodes.Select(Cell))}");
            text.AppendLine($"- 경험 ID: `{Cell(item.ExperienceId)}` / 원문 SHA: `{Cell(item.SnapshotSha256)}`");
            if (item.DecisionId is not null) text.AppendLine($"- 비교 사건 ID: `{Cell(item.DecisionId)}`");
            text.AppendLine($"- 회차: `{Cell(item.RunId)}` / 검토: {(item.ReviewCurrent ? "현재 판본 의견 있음" : "미검토 또는 이전 판본")}");
            var order = summary.Orders.Single(x => x.ExperienceId == item.ExperienceId);
            if (order.Incidents.Count > 0)
                text.AppendLine($"- 업무 문제 이력: {string.Join(", ", order.Incidents.Select(x => Cell(x.KindCode) + " (" + Cell(x.StatusCode) + ")").Distinct(StringComparer.Ordinal))}. 해결된 기록도 보존합니다.");
            var decision = item.DecisionId is null ? null : order.Decisions.Single(x => x.DecisionId == item.DecisionId);
            if (decision is not null)
            {
                text.AppendLine($"- 기존 규칙: {Cell(decision.RuleStatusCode)} / 선택: {Cell(decision.RuleDriverLabel ?? "미확인")} / 모델 비교 사유: {Cell(decision.AiResult?.ReasonCode ?? "미확인")} / 응답 관측: {(decision.AiResult is { } ai ? ai.ElapsedMilliseconds + " ms" : "미확인")}\n");
                text.AppendLine("| 후보 | 기사 | 거리(km) | 대기 가점 | 총점 | 적합 판정 |\n| --- | --- | ---: | ---: | ---: | --- |");
                foreach (var candidate in decision.Candidates)
                    text.AppendLine($"| {Cell(candidate.Token)} | {Cell(candidate.DriverLabel ?? "미확인")} | {candidate.DistanceKm?.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) ?? "미확인"} | {candidate.AgingScore?.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) ?? "미확인"} | {candidate.TotalScore?.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) ?? "미확인"} | {Cell(candidate.EligibilityCode)} |");
                text.AppendLine("\n표의 소수는 읽기 쉽게 반올림했으며 원문 값은 전체 근거에 있습니다. 대기 가점은 실제 대기 분수가 아닙니다. 약속 시각·완전한 적재 조건 등 빠진 근거는 이 표로 보충됐다고 보지 않습니다.\n");
            }
            text.AppendLine("- 함께 정할 것: 이 상황에서 기존 판단을 유지할까요, 다른 후보·보류가 맞을까요, 근거가 더 필요할까요?\n");
        }
        if (selection.Selected.Count == 0) text.AppendLine("아직 검토할 새 관측이 없습니다. 이미 검토한 원문은 그대로 보존합니다.\n");
        text.AppendLine($"[이 선별과 같은 판본의 주문별 전체 근거](reports/{snapshotName}) · [전체 누적 현황](current-summary.md). 새 판본은 이 조회 파일을 갱신하지만 과거 원문과 사람 의견을 덮어쓰지 않습니다. 단계별 실제 시각·완전한 판단 입력이 없는 기록은 추정해 채우지 않습니다.");
        await AtomicReportAsync(Path.Combine(root, "current-representatives.json"), JsonSerializer.Serialize(selection, Json), cancellationToken);
        await AtomicReportAsync(Path.Combine(root, "current-representatives.md"), text.ToString(), cancellationToken);
    }

    private static string Cell(string value) => value.Replace('|', '/').Replace('`', '\'').Replace('\r', ' ').Replace('\n', ' ');
    private static string Submission(string value) => value switch
    { "Confirmed" => "접수 확인", "Scheduled" => "예약", "Skipped" => "생략", "Submitting" => "접수 중", "FailedReview" => "접수 검토 필요", _ => "미확인" };
    private static string Observed(string value) => value switch
    { "Completed" => "전달 완료 관측", "Waiting" => "배차 대기", "Assigned" => "배정", "InProgress" => "수행 중", "Failed" => "실패", "NotSubmitted" => "미접수", _ => "미확인" };

    private static async Task AtomicReportAsync(string path, string value, CancellationToken cancellationToken, bool immutable = false)
    {
        DispatchExperienceCommand.CheckLinks(path);
        if (immutable && File.Exists(path))
        {
            if (await File.ReadAllTextAsync(path, cancellationToken) != value)
                throw new DispatchExperienceException("ReviewPacketSnapshotCorrupt");
            return;
        }
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllTextAsync(temp, value, new UTF8Encoding(false), cancellationToken);
            File.Move(temp, path, overwrite: !immutable);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
