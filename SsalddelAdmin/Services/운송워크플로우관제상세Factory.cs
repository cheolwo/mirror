using Ssalddel.Contracts.Common.Workflow;
using SsalddelAdmin.ViewModels;

namespace SsalddelAdmin.Services;

public static partial class 운송워크플로우관제상세Factory
{
    public static 운송워크플로우관제상세응답? Build(
        string requestId,
        화주운송의뢰응답? 의뢰,
        IReadOnlyList<결제목록응답> 결제목록,
        IReadOnlyList<배차대기응답> 배차대기목록,
        IReadOnlyList<운송진행응답> 운송목록,
        IReadOnlyList<운송이벤트로그응답> 이벤트목록,
        IReadOnlyList<파일POD응답> 증빙목록,
        IReadOnlyList<기사월정산관리응답> 정산후보목록,
        기사운송료지급조회응답? 지급조회 = null)
    {
        var normalizedRequestId = requestId.Trim();
        var 결제 = 결제목록.FirstOrDefault(x => IsSame(x.의뢰Id, normalizedRequestId));
        var 배차대기 = 배차대기목록.FirstOrDefault(x => IsSame(x.의뢰Id, normalizedRequestId));
        var 운송 = 운송목록.FirstOrDefault(x => IsSame(x.운송번호, normalizedRequestId));

        if (의뢰 is null && 결제 is null && 배차대기 is null && 운송 is null && 이벤트목록.Count == 0 && 증빙목록.Count == 0)
        {
            return null;
        }

        var 지급표시 = TransportWorkflowSettlementViewModel.Create(normalizedRequestId, 의뢰, 결제, 운송, 지급조회);
        var 단계목록 = Build단계목록(의뢰, 결제, 배차대기, 운송, 이벤트목록, 증빙목록, 지급표시);
        var 운영확인목록 = Build운영확인목록(의뢰, 결제, 배차대기, 운송, 이벤트목록, 증빙목록, 지급표시);
        var currentStep = 단계목록.LastOrDefault(x => x.완료됨 || x.진행중 || x.확인필요) ?? 단계목록.First();
        var nextAction = 운영확인목록.Count == 0
            ? "현재 즉시 개입할 항목은 없습니다. 이벤트와 증빙이 계속 정상 갱신되는지만 확인하세요."
            : 운영확인목록[0].조치안내;

        return new 운송워크플로우관제상세응답
        {
            의뢰Id = normalizedRequestId,
            제목 = 의뢰?.요약?.화물종류 ?? 운송?.운송번호 ?? normalizedRequestId,
            화주Id = 결제?.화주Id ?? 배차대기?.화주Id ?? string.Empty,
            운송방식 = 의뢰?.운송방식 ?? "미확인",
            경로표시 = Build경로표시(의뢰, 배차대기, 운송),
            현재상태라벨 = currentStep.확인필요 ? "확인 필요" : currentStep.진행중 ? "진행 중" : currentStep.완료됨 ? "완료" : "대기",
            현재상태색상 = currentStep.색상,
            관리자다음행동 = nextAction,
            의뢰 = 의뢰,
            결제 = 결제,
            배차대기 = 배차대기,
            운송 = 운송,
            단계목록 = 단계목록,
            운영확인목록 = 운영확인목록,
            이벤트목록 = 이벤트목록.OrderByDescending(x => x.이벤트시각).ToArray(),
            증빙목록 = 증빙목록.OrderByDescending(x => x.UploadedAtUtc).ToArray(),
            정산후보목록 = 정산후보목록.OrderByDescending(x => x.UpdatedAt).ToArray(),
            기사운송료지급조회 = 지급조회 is null ? null : new()
            {
                조회년 = 지급조회.조회년,
                조회월 = 지급조회.조회월,
                조회완료 = 지급표시.QueryCompleted,
                조회실패 = 지급표시.QueryFailed,
                지급요청목록 = 지급표시.PayoutRequests
            }
        };
    }

    private static string Build경로표시(화주운송의뢰응답? 의뢰, 배차대기응답? 배차대기, 운송진행응답? 운송)
    {
        var pickup = 의뢰?.픽업지 ?? 배차대기?.픽업_도로명주소 ?? 운송?.출발지 ?? "상차지 미확인";
        var dropoff = 의뢰?.하차지 ?? 배차대기?.하차_도로명주소 ?? 운송?.도착지 ?? "하차지 미확인";
        return $"{pickup} → {dropoff}";
    }

    private static bool IsExceptionAt(운송진행응답? 운송, string stage)
        => 운송?.예외신고됨 == true && 운송.최근예외단계.Contains(stage, StringComparison.OrdinalIgnoreCase);

    private static bool IsStale(운송진행응답 item)
        => ContainsAny(item.상태, "배차확정", "운송중") && item.UpdatedAt < DateTime.UtcNow.AddHours(-2);

    private static bool IsSame(string? left, string? right)
        => string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);

    private static bool ContainsAny(string? value, params string[] tokens)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return tokens.Any(token => value.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasProblem(string? value)
        => ContainsAny(value, "취소", "환불", "실패", "반려", "거절", "예외", "미수");

    private static string Display(string? value)
        => string.IsNullOrWhiteSpace(value) ? "미확인" : value;
}
