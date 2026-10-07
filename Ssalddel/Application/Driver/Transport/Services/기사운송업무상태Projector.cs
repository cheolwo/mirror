using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Driver.Transport;
using 살뜰.도메인.운송;

namespace Ssalddel.Application.Driver.Transport;

/// <summary>소유 운송의 신고·사건 상태를 조회에 복원하고 실제 전이 정책과 다음 행동을 결속합니다.</summary>
public static class 기사운송업무상태Projector
{
    public static async Task<IReadOnlyList<비정상운송사건>> 사건조회Async(
        SsalddelContext db, IReadOnlyCollection<long> transportIds, CancellationToken cancellationToken)
        => transportIds.Count == 0 ? [] : await db.비정상운송사건.AsNoTracking()
            .Where(item => Enumerable.Contains(transportIds, item.운송Id)).ToListAsync(cancellationToken);

    public static IReadOnlyList<비정상운송사건> 관계있는사건(
        운송원장 transport, IEnumerable<비정상운송사건> incidents)
    {
        var requestId = string.IsNullOrWhiteSpace(transport.의뢰Id) ? transport.운송번호 : transport.의뢰Id;
        return incidents.Where(item => item.운송Id == transport.Id
                && string.Equals(item.운송의뢰Id, requestId, StringComparison.Ordinal))
            .OrderByDescending(item => item.UpdatedAt).ThenBy(item => item.사건StableId, StringComparer.Ordinal).ToArray();
    }

    public static bool 진행보류인가(IEnumerable<비정상운송사건> incidents)
        => incidents.Any(item => item.업무통제상태Code == 비정상운송업무통제상태Codes.전체보류
            || item.보류범위Code == 비정상운송보류범위Codes.전체운송);

    public static void 투영(기사운송요약응답 response, 운송원장 transport, IEnumerable<비정상운송사건> incidents)
    {
        var related = 관계있는사건(transport, incidents);
        var report = 최근신고(transport.첨부_json);
        if (report is { } last)
        {
            response.예외신고됨 = true;
            response.최근예외단계 = Text(last, "stage");
            response.최근예외코드 = Text(last, "exceptionCode");
            response.최근예외메시지 = Text(last, "reason");
            response.다음행동안내 = Text(last, "nextAction");
            response.관리자확인필요 = Flag(last, "adminReviewRequired");
        }
        response.예외검토목록 = related.Select(item => new 기사운송예외검토응답(
            item.사건StableId, item.단계Code, item.원본예외Code, item.상태Code, item.현재담당Code,
            item.업무통제상태Code, item.보류범위Code, item.정산보류적용여부, item.Revision, item.UpdatedAt)).ToArray();
        response.운송진행보류 = 진행보류인가(related)
            || (report is { } pendingReport && Flag(pendingReport, "workCannotContinue")
                && !related.Any(item => item.원본예외Code == response.최근예외코드));
        response.정산보류 = related.Any(item => item.정산보류적용여부
            || item.상태Code == 비정상운송사건상태Codes.운영검토대기);
        if (related.Count > 0)
        {
            response.예외신고됨 = true;
            // 사건 대상 신고는 현재 검토 원장을 우선한다. 별도 일반 신고의 확인 요청은 보존한다.
            var reportedIncident = related.FirstOrDefault(item => item.원본예외Code == response.최근예외코드);
            response.관리자확인필요 = related.Any(item => item.상태Code == 비정상운송사건상태Codes.운영검토대기)
                || (reportedIncident is null && response.관리자확인필요);
            response.다음행동안내 = response.운송진행보류
                ? "전체 운송이 보류되었습니다. 운영 담당자의 재개 확인을 기다려 주세요."
                : related.Any(item => item.업무통제상태Code == 비정상운송업무통제상태Codes.일부보류)
                    ? "영향 수량은 보류하고 확인된 정상 수량의 업무를 진행해 주세요."
                    : response.관리자확인필요
                        ? "운영 검토 중입니다. 현장 조건을 확인하며 현재 허용된 업무를 진행해 주세요."
                        : "운송 예외 검토 결과를 확인하고 현재 업무를 진행해 주세요.";
        }
        else if (response.운송진행보류)
            response.다음행동안내 = "현장 진행 불가가 신고되었습니다. 현장 조건과 운영 확인 후 다시 진행 가능 여부를 알려 주세요.";
        response.가능한행동 = 가능한행동(transport.상태, response.운송진행보류);
    }

    private static IReadOnlyList<string> 가능한행동(string state, bool held)
    {
        if (state is "완료" or "운송완료" or "인수완료" or "하차완료" or "배송완료" or "취소" or "운송취소" or "의뢰취소") return [];
        var result = new List<string>();
        if (!held)
            foreach (var (key, target) in new[] { ("arrive-pickup", "상차지도착"), ("pickup", "상차완료"),
                         ("arrive-dropoff", "하차지도착"), ("dropoff", "인수완료") })
                if (state != target && 기사운송상태전이Policy.가능한가(state, target)) result.Add(key);
        result.Add("issue");
        return result;
    }

    private static JsonElement? 최근신고(string? attachments)
    {
        if (string.IsNullOrWhiteSpace(attachments)) return null;
        try
        {
            using var json = JsonDocument.Parse(attachments);
            if (json.RootElement.ValueKind != JsonValueKind.Array) return null;
            // Writer가 최초 저장 순서대로 추가한다. 비신고 증빙은 최근 신고를 지우지 않는다.
            return json.RootElement.EnumerateArray().LastOrDefault(item => item.ValueKind == JsonValueKind.Object
                && Text(item, "kind") == "transport-field-exception") is { ValueKind: JsonValueKind.Object } last
                ? last.Clone() : null;
        }
        catch (JsonException) { return null; }
    }
    private static string Text(JsonElement item, string name)
        => item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";
    private static bool Flag(JsonElement item, string name)
        => item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;
}
