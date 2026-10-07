using System;
using 살뜰.도메인.공통;

namespace 살뜰.도메인.화주
{
    public class 화주운송의뢰
    {
        public long Id { get; set; }
        public string 의뢰Id { get; set; } = string.Empty;
        public string 화주Id { get; set; } = string.Empty;
        public string 주문자UserId { get; set; } = string.Empty;
        public string 화물종류 { get; set; } = string.Empty;
        public string 화물설명 { get; set; } = string.Empty;
        public int? 화물수량 { get; set; }
        public int? 화물길이Mm { get; set; }
        public int? 화물폭Mm { get; set; }
        public int? 화물높이Mm { get; set; }
        public int? 화물팔레트개수 { get; set; }
        public decimal? 화물중량Kg { get; set; }
        public decimal? 화물부피Cbm { get; set; }
        public bool 화물파손주의여부 { get; set; }
        public string 화물온도조건 { get; set; } = "상온";
        public string 운송방식 { get; set; } = "혼적";
        public string 차량종류 { get; set; } = string.Empty;
        public string 결제수단 { get; set; } = "카드";
        public string 정산시점 { get; set; } = "선결제";
        public string 증빙방식 { get; set; } = "없음";
        public string 수납주체 { get; set; } = "플랫폼";
        public string 정산상태 { get; set; } = 상태값.결제상태.결제대기;
        public string 정산메모 { get; set; } = string.Empty;
        public string 인수증번호 { get; set; } = string.Empty;
        public DateTime? 인수증등록일시 { get; set; }
        public DateTime? 현장수금확인일시 { get; set; }
        public string 현장지급메모 { get; set; } = string.Empty;
        public bool 세금계산서필요 { get; set; }
        public bool 현금영수증필요 { get; set; }
        public int? 결제예정금액 { get; set; }
        public long? 운임구성Id { get; set; }
        public string 픽업_도로명주소 { get; set; } = string.Empty;
        public string 픽업_상세주소 { get; set; } = string.Empty;
        public decimal? 픽업_위도 { get; set; }
        public decimal? 픽업_경도 { get; set; }
        public string 픽업_연락처_이름 { get; set; } = string.Empty;
        public string 픽업_연락처_전화번호 { get; set; } = string.Empty;
        public DateTime 픽업_시간창_시작일시 { get; set; }
        public DateTime 픽업_시간창_종료일시 { get; set; }
        public string 하차_도로명주소 { get; set; } = string.Empty;
        public string 하차_상세주소 { get; set; } = string.Empty;
        public decimal? 하차_위도 { get; set; }
        public decimal? 하차_경도 { get; set; }
        public string 하차_연락처_이름 { get; set; } = string.Empty;
        public string 하차_연락처_전화번호 { get; set; } = string.Empty;
        public DateTime? 하차_시간창_시작일시 { get; set; }
        public DateTime? 하차_시간창_종료일시 { get; set; }
        public string 서비스레벨 { get; set; } = string.Empty;
        public string 요청사항 { get; set; } = string.Empty;
        public decimal? 대기료 { get; set; }
        public decimal? 수작업비 { get; set; }
        public decimal? 할증 { get; set; }
        public decimal? 최종운임 { get; set; }
        public string? 생활배송접수키 { get; set; }

        public string 클라이언트요청Id { get; set; } = string.Empty;
        public string 상태 { get; set; } = 상태값.의뢰상태.생성됨;
        public string 결제상태 { get; set; } = 상태값.결제상태.결제대기;
        public string 배차상태 { get; set; } = 상태값.배차상태.미시작;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
