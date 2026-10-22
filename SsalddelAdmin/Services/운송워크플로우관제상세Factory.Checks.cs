using Ssalddel.Contracts.Common.Workflow;
using SsalddelAdmin.ViewModels;

namespace SsalddelAdmin.Services;

public static partial class 운송워크플로우관제상세Factory
{
    private static IReadOnlyList<운송워크플로우운영확인응답> Build운영확인목록(
        화주운송의뢰응답? 의뢰,
        결제목록응답? 결제,
        배차대기응답? 배차대기,
        운송진행응답? 운송,
        IReadOnlyList<운송이벤트로그응답> 이벤트목록,
        IReadOnlyList<파일POD응답> 증빙목록,
        TransportWorkflowSettlementViewModel 지급표시)
    {
        var items = new List<운송워크플로우운영확인응답>();

        if (ContainsAny(결제?.결제상태 ?? 의뢰?.결제상태, "결제대기", "미결제", "입금대기"))
        {
            items.Add(Check("결제확인", "높음", "화주 결제 또는 가상계좌 입금 상태를 확인하고 필요하면 입금 안내를 보냅니다."));
        }

        if (배차대기 is not null && ContainsAny(배차대기.상태, "보류", "후보부족"))
        {
            items.Add(Check("배차보류", "높음", "추천 후보 부족 또는 보류 사유를 확인하고 수동 배차나 조건 수정을 검토합니다."));
        }
        else if (운송 is null && 배차대기 is not null)
        {
            items.Add(Check("배차대기", "보통", "배차 잠금이 오래 유지되지 않는지 확인합니다."));
        }

        if (운송?.관리자확인필요 == true || 운송?.예외신고됨 == true)
        {
            items.Add(Check("운송예외", 운송.관리자확인필요 ? "높음" : "보통", $"{운송.최근예외단계} · {운송.최근예외코드} · {운송.최근예외메시지}"));
        }

        if (운송 is not null && IsStale(운송))
        {
            items.Add(Check("상태지연", "높음", $"{운송.상태} 상태가 2시간 이상 갱신되지 않았습니다. 기사 위치/연락 상태를 확인합니다."));
        }

        if (운송 is not null && ContainsAny(운송.상태, "하차완료", "인수완료", "완료") && 증빙목록.Count == 0)
        {
            items.Add(Check("증빙누락", "높음", "운송 완료 상태인데 POD/사진 증빙이 없습니다. 기사 앱 업로드 결과를 확인합니다."));
        }

        if (지급표시.QueryFailed)
        {
            items.Add(Check("지급조회", "보통", "기사 지급요청을 다시 조회하세요. 화주 수납만으로 실지급을 확정하지 않습니다."));
        }
        else if (운송 is not null && ContainsAny(운송.상태, "하차완료", "인수완료", "완료") && !지급표시.HasActualTransferConfirmation)
        {
            items.Add(Check("지급확인", "보통", "화주 수납과 별도로 기사 지급준비·요청·실지급 근거를 확인하세요."));
        }

        return items
            .OrderByDescending(x => x.우선도 == "높음")
            .ThenBy(x => x.구분)
            .ToArray();
    }

    private static 운송워크플로우운영확인응답 Check(string 구분, string 우선도, string 조치안내)
        => new()
        {
            구분 = 구분,
            우선도 = 우선도,
            조치안내 = 조치안내
        };
}
