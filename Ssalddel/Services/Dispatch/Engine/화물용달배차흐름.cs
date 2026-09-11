using 살뜰.도메인.배차;

namespace 살뜰.Services.Dispatch.Engine;

public static class 화물용달배차원본유형
{
    public const string 화주운송의뢰 = 운송의뢰배차원천유형.화주운송의뢰;
    public const string 주문자화물주문 = 운송의뢰배차원천유형.주문자화물주문;
    public const string 수입화물운송 = 운송의뢰배차원천유형.수입화물운송;
    public const string 창고출고연계운송 = 운송의뢰배차원천유형.창고출고연계운송;
    public const string 판매채널출고 = 운송의뢰배차원천유형.판매채널출고;
    public const string 살뜰마트출고 = 운송의뢰배차원천유형.살뜰마트출고;
    public const string 공동주문국내운송 = 운송의뢰배차원천유형.공동주문국내운송;
    public const string Fcl연계운송 = 운송의뢰배차원천유형.Fcl연계운송;
    public const string Lcl연계운송 = 운송의뢰배차원천유형.Lcl연계운송;
}

public sealed record 화물용달배차흐름(
    string 흐름코드,
    string 표시명,
    string 운송단위,
    string 우선확인정보,
    string 배차시작조건);

public interface I화물용달배차흐름Resolver
{
    화물용달배차흐름 Resolve(운송원장 queue, string? 화물운송방식 = null);
}

public sealed class 화물용달배차흐름Resolver : I화물용달배차흐름Resolver
{
    public 화물용달배차흐름 Resolve(운송원장 queue, string? 화물운송방식 = null)
    {
        if (string.Equals(queue.원본의뢰유형, 화물용달배차원본유형.Fcl연계운송, StringComparison.OrdinalIgnoreCase)
            || ContainsAny(화물운송방식, "FCL", "컨테이너", "독차"))
        {
            return new 화물용달배차흐름(
                화물용달배차원본유형.Fcl연계운송,
                "FCL/독차 화물 운송",
                "차량 또는 컨테이너 단위",
                "차량 제원, 팔레트 수, 중량, 상하차 장비, 시간창",
                "결제 또는 후불 승인 후 배차대기 생성 시 배차를 시작합니다.");
        }

        if (string.Equals(queue.원본의뢰유형, 화물용달배차원본유형.Lcl연계운송, StringComparison.OrdinalIgnoreCase)
            || ContainsAny(화물운송방식, "LCL", "혼적", "합짐"))
        {
            return new 화물용달배차흐름(
                화물용달배차원본유형.Lcl연계운송,
                "LCL/혼적 화물 운송",
                "혼적 가능 화물 단위",
                "혼적 가능 여부, 온도/파손 민감도, 경유 가능 시간, 하차 순서",
                "혼적 조건과 시간창이 맞는 후보 기사에게만 추천합니다.");
        }

        if (운송의뢰배차원천유형.Is수입통관연계운송(queue.원본의뢰유형))
        {
            return new 화물용달배차흐름(
                string.IsNullOrWhiteSpace(queue.원본의뢰유형) ? 화물용달배차원본유형.수입화물운송 : queue.원본의뢰유형,
                "수입/통관 연계 화물 운송",
                "통관 완료 또는 반출 가능 화물 단위",
                "통관 상태, 보세/창고 위치, 반출 가능 시각, HS 코드 위험 태그",
                "통관 또는 반출 가능 상태가 확인된 뒤 배차를 시작합니다.");
        }

        if (운송의뢰배차원천유형.Is창고출고연계운송(queue.원본의뢰유형))
        {
            return new 화물용달배차흐름(
                string.IsNullOrWhiteSpace(queue.원본의뢰유형) ? 화물용달배차원본유형.창고출고연계운송 : queue.원본의뢰유형,
                "창고 출고 연계 화물 운송",
                "피킹/포장 완료 출고 단위",
                "출고 준비 상태, 적재 위치, 상차 가능 시각, 하차지 결제 조건",
                "창고 피킹/포장 또는 출고예정 상태가 확인된 뒤 배차를 시작합니다.");
        }

        if (string.Equals(queue.원본의뢰유형, 화물용달배차원본유형.주문자화물주문, StringComparison.OrdinalIgnoreCase))
        {
            return new 화물용달배차흐름(
                화물용달배차원본유형.주문자화물주문,
                "주문자 화물/공산품 운송",
                "주문자가 만든 운송 요청 단위",
                "주문자 연락 가능 여부, 픽업/하차 주소, 상품 크기와 파손 주의",
                "주문자의 결제 또는 운송 요청 확정 후 배차를 시작합니다.");
        }

        return new 화물용달배차흐름(
            화물용달배차원본유형.화주운송의뢰,
            "화주 운송 의뢰",
            "화주가 등록한 운송 의뢰 단위",
            "상차지, 하차지, 화물 제원, 운임, 결제/정산 조건",
            "화주 의뢰가 결제 완료 또는 후불/현장지급 승인되면 배차를 시작합니다.");
    }

    private static bool ContainsAny(string? value, params string[] tokens)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return tokens.Any(token => value.Contains(token, StringComparison.OrdinalIgnoreCase));
    }
}
