using Ssalddel.Contracts.Common.Workflow;
using SsalddelAdmin.ViewModels;

namespace SsalddelAdmin.Services;

public static partial class 운송워크플로우관제상세Factory
{
    private static IReadOnlyList<운송워크플로우단계응답> Build단계목록(
        화주운송의뢰응답? 의뢰,
        결제목록응답? 결제,
        배차대기응답? 배차대기,
        운송진행응답? 운송,
        IReadOnlyList<운송이벤트로그응답> 이벤트목록,
        IReadOnlyList<파일POD응답> 증빙목록,
        TransportWorkflowSettlementViewModel 지급표시)
    {
        return
        [
            Step(1, "request", "의뢰 접수", 의뢰 is not null, false, false,
                의뢰?.의뢰상태 ?? "의뢰 정보 미확인",
                의뢰 is null ? "서버 원장에서 의뢰 상세를 찾지 못했습니다." : $"{의뢰.생성일시.ToLocalTime():MM-dd HH:mm} 접수",
                "화주 입력값과 경로를 확인합니다."),
            Build결제단계(결제, 의뢰, 지급표시),
            Build배차단계(배차대기, 운송, 의뢰),
            Build수락단계(운송, 의뢰),
            Build상차단계(운송, 이벤트목록, 의뢰),
            Build하차단계(운송, 이벤트목록, 의뢰),
            Build증빙단계(운송, 증빙목록, 이벤트목록),
            Build정산단계(의뢰, 운송, 지급표시)
        ];
    }

    private static 운송워크플로우단계응답 Build결제단계(결제목록응답? 결제, 화주운송의뢰응답? 의뢰, TransportWorkflowSettlementViewModel 지급표시)
    {
        var state = 결제?.결제상태 ?? 의뢰?.결제상태;
        var attention = HasProblem(state);
        var completed = 지급표시.CollectionConfirmed;
        var active = !completed && ContainsAny(state, "결제대기", "미결제", "입금대기", "청구", "대기");
        return Step(2, "payment", "화주 수납", completed, active, attention,
            completed ? "수납 확인" : Display(state),
            결제 is null ? "결제 원장 연결 전입니다." : $"{결제.결제수단} {결제.결제금액:N0}원 · {결제.생성일시Utc.ToLocalTime():MM-dd HH:mm}",
            "화주 결제 또는 후불/현장 지급 조건을 확인합니다. 기사 실지급과 별개입니다.");
    }

    private static 운송워크플로우단계응답 Build배차단계(배차대기응답? 배차대기, 운송진행응답? 운송, 화주운송의뢰응답? 의뢰)
    {
        var state = 배차대기?.상태 ?? 의뢰?.배차상태;
        var completed = 운송 is not null || ContainsAny(state, "확정", "수락", "기사배정", "상차", "운송", "하차", "완료");
        var active = !completed && (배차대기 is not null || ContainsAny(state, "배차대기", "매칭", "추천", "대기"));
        var attention = HasProblem(state) || ContainsAny(state, "보류", "후보부족");
        return Step(3, "dispatch", "배차", completed, active, attention,
            Display(state),
            배차대기 is null ? "배차대기 원장 없음" : $"{배차대기.픽업_도로명주소} → {배차대기.하차_도로명주소}",
            "배차 엔진 또는 운영자가 추천 후보를 만들고 잠금 상태를 확인합니다.");
    }

    private static 운송워크플로우단계응답 Build수락단계(운송진행응답? 운송, 화주운송의뢰응답? 의뢰)
    {
        var state = 운송?.상태 ?? 의뢰?.배차상태;
        var completed = 운송 is not null && ContainsAny(state, "배차확정", "상차", "운송", "하차", "인수", "완료");
        var active = !completed && ContainsAny(state, "추천", "매칭", "배차대기");
        return Step(4, "acceptance", "기사 수락", completed, active, HasProblem(state),
            completed ? "수락 완료" : active ? "기사 응답 대기" : "수락 전",
            운송 is null ? "진행 운송 생성 전입니다." : $"{운송.기사_운송자} · 운송번호 {운송.운송번호}",
            "기사 수락 이후에는 상차 준비 알림과 진행 운송 원장이 이어져야 합니다.");
    }

    private static 운송워크플로우단계응답 Build상차단계(운송진행응답? 운송, IReadOnlyList<운송이벤트로그응답> 이벤트목록, 화주운송의뢰응답? 의뢰)
    {
        var state = 운송?.상태 ?? 의뢰?.배차상태;
        var hasPickupEvent = 이벤트목록.Any(x => ContainsAny(x.이벤트타입, "상차"));
        var completed = hasPickupEvent || ContainsAny(state, "상차완료", "운송중", "하차", "인수", "완료");
        var active = !completed && ContainsAny(state, "배차확정", "상차지도착", "상차대기", "수락");
        var attention = IsExceptionAt(운송, "상차") || 이벤트목록.Any(x => ContainsAny(x.이벤트타입, "상차") && ContainsAny(x.이벤트타입, "예외"));
        return Step(5, "pickup", "상차", completed, active, attention,
            completed ? "상차 완료" : active ? "상차 준비" : "상차 전",
            운송?.출발_픽업 is null ? "상차 시각 미기록" : $"{운송.출발_픽업.Value.ToLocalTime():MM-dd HH:mm}",
            "상차 사진, 인수증, 서명 또는 생략 사유가 남아야 합니다.");
    }

    private static 운송워크플로우단계응답 Build하차단계(운송진행응답? 운송, IReadOnlyList<운송이벤트로그응답> 이벤트목록, 화주운송의뢰응답? 의뢰)
    {
        var state = 운송?.상태 ?? 의뢰?.의뢰상태;
        var hasDropoffEvent = 이벤트목록.Any(x => ContainsAny(x.이벤트타입, "하차", "인수"));
        var completed = hasDropoffEvent || ContainsAny(state, "하차완료", "인수완료", "배송완료", "완료");
        var active = !completed && ContainsAny(state, "운송중", "하차지도착", "상차완료");
        var attention = IsExceptionAt(운송, "하차") || 이벤트목록.Any(x => ContainsAny(x.이벤트타입, "하차") && ContainsAny(x.이벤트타입, "예외"));
        return Step(6, "dropoff", "하차", completed, active, attention,
            completed ? "하차 완료" : active ? "하차 진행" : "하차 전",
            운송?.도착 is null ? "하차 시각 미기록" : $"{운송.도착.Value.ToLocalTime():MM-dd HH:mm}",
            "하차 사진과 인수 확인이 들어오면 정산 후보로 넘어갑니다.");
    }

    private static 운송워크플로우단계응답 Build증빙단계(운송진행응답? 운송, IReadOnlyList<파일POD응답> 증빙목록, IReadOnlyList<운송이벤트로그응답> 이벤트목록)
    {
        var completed = 증빙목록.Any(x => ContainsAny(x.UploadStatus, "검수완료", "완료"));
        var active = !completed && 증빙목록.Count > 0;
        var 운송완료 = 운송 is not null && ContainsAny(운송.상태, "하차완료", "인수완료", "완료");
        var attention = 운송완료 && 증빙목록.Count == 0 || 이벤트목록.Any(x => ContainsAny(x.이벤트타입, "증빙") && ContainsAny(x.이벤트타입, "실패", "예외"));
        return Step(7, "proof", "증빙/POD", completed, active, attention,
            completed ? "검수 완료" : active ? "검수 대기" : "증빙 전",
            증빙목록.Count == 0 ? "업로드된 증빙 없음" : $"{증빙목록.Count:N0}건 · 최근 {증빙목록.Max(x => x.UploadedAtUtc).ToLocalTime():MM-dd HH:mm}",
            "사진, 인수증, POD 파일의 업로드와 검수 상태를 확인합니다.");
    }

    private static 운송워크플로우단계응답 Build정산단계(화주운송의뢰응답? 의뢰, 운송진행응답? 운송, TransportWorkflowSettlementViewModel 지급표시)
    {
        var state = 의뢰?.정산상태;
        var completed = 지급표시.HasActualTransferConfirmation;
        var active = !completed && (지급표시.PayoutRequests.Count > 0 || ContainsAny(state, "정산대기", "입금대기", "청구대기", "대기", "입금확인", "정산완료") || ContainsAny(운송?.상태, "하차완료", "인수완료", "완료"));
        return Step(8, "settlement", "기사 지급", completed, active, HasProblem(state) || 지급표시.RequiresAttention,
            completed ? 지급표시.ActualTransferStatus : $"{지급표시.RequestStatus} · {지급표시.ActualTransferStatus}",
            지급표시.QueryScope,
            "화주 수납·월 이용료·시험 지급 검증은 기사 실지급 완료 근거가 아닙니다.");
    }

    private static 운송워크플로우단계응답 Step(
        int 순번,
        string 코드,
        string 제목,
        bool 완료,
        bool 진행,
        bool 확인필요,
        string 상태,
        string 증빙,
        string 설명)
        => new()
        {
            순번 = 순번,
            단계코드 = 코드,
            제목 = 제목,
            상태 = 상태,
            증빙 = 증빙,
            설명 = 설명,
            완료됨 = 완료 && !확인필요,
            진행중 = 진행 && !확인필요,
            확인필요 = 확인필요,
            색상 = 확인필요 ? "danger" : 완료 ? "success" : 진행 ? "primary" : "secondary"
        };
}
