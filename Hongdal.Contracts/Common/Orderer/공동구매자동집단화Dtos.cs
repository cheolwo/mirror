namespace Hongdal.Contracts.Common.Orderer;

public sealed class 공동구매자동수요등록Command
{
    public string 수요출처키 { get; set; } = string.Empty;
    public long? 커뮤니티게시글Id { get; set; }
    public string 커뮤니티원장Id { get; set; } = string.Empty;
    public string 상품키 { get; set; } = string.Empty;
    public string 상품명 { get; set; } = string.Empty;
    public string HS코드 { get; set; } = string.Empty;
    public string 온도코드 { get; set; } = "상온";
    public string 물류방식 { get; set; } = "LCL";
    public string 주문자키 { get; set; } = string.Empty;
    public string 주문자표시명 { get; set; } = string.Empty;
    public string 배송권키 { get; set; } = string.Empty;
    public string 배송권명 { get; set; } = string.Empty;
    public long? 도착창고Id { get; set; }
    public string 도착창고유형 { get; set; } = string.Empty;
    public string 도착창고명 { get; set; } = string.Empty;
    public string 수령지주소참조키 { get; set; } = string.Empty;
    public string 수령지표시명 { get; set; } = string.Empty;
    public string 수령도로명주소 { get; set; } = string.Empty;
    public string 수령상세주소 { get; set; } = string.Empty;
    public decimal 희망수량 { get; set; }
    public string 수량단위 { get; set; } = "kg";
    public decimal? 예약결제금액 { get; set; }
    public string 수요유형 { get; set; } = 공동구매자동수요유형코드.관심표시;
    public string 결제상태 { get; set; } = 공동구매자동결제상태코드.미결제;
    public string 메모 { get; set; } = string.Empty;
    public int? 목표참여자수 { get; set; }
    public decimal? 목표수량 { get; set; }
}

public sealed class 공동구매자동수요응답
{
    public string 수요Id { get; set; } = string.Empty;
    public string 수요출처키 { get; set; } = string.Empty;
    public long? 커뮤니티게시글Id { get; set; }
    public string 커뮤니티원장Id { get; set; } = string.Empty;
    public string 자동집단Id { get; set; } = string.Empty;
    public string 상품키 { get; set; } = string.Empty;
    public string 상품명 { get; set; } = string.Empty;
    public string 주문자키 { get; set; } = string.Empty;
    public string 주문자표시명 { get; set; } = string.Empty;
    public string 배송권키 { get; set; } = string.Empty;
    public string 배송권명 { get; set; } = string.Empty;
    public long? 도착창고Id { get; set; }
    public string 도착창고유형 { get; set; } = string.Empty;
    public string 도착창고명 { get; set; } = string.Empty;
    public string 수령지주소참조키 { get; set; } = string.Empty;
    public string 입고의미상태 { get; set; } = 공동구매개별주문입고상태코드.미지정;
    public string 공동구매주문집계원장Id { get; set; } = string.Empty;
    public string 개별주문원장Id { get; set; } = string.Empty;
    public string 입고예정원장Id { get; set; } = string.Empty;
    public string 수요유형 { get; set; } = string.Empty;
    public string 결제상태 { get; set; } = string.Empty;
    public decimal 희망수량 { get; set; }
    public string 수량단위 { get; set; } = string.Empty;
    public decimal? 예약결제금액 { get; set; }
    public DateTime 생성시각Utc { get; set; }
}

public sealed class 공동구매자동집단응답
{
    public string 자동집단Id { get; set; } = string.Empty;
    public string 공동구매주문집계원장Id { get; set; } = string.Empty;
    public string 상품키 { get; set; } = string.Empty;
    public string 상품명 { get; set; } = string.Empty;
    public string HS코드 { get; set; } = string.Empty;
    public string 온도코드 { get; set; } = string.Empty;
    public string 물류방식 { get; set; } = string.Empty;
    public string 배송권키 { get; set; } = string.Empty;
    public string 배송권명 { get; set; } = string.Empty;
    public string 현재상태 { get; set; } = 공동구매자동집단상태코드.수요수집중;
    public int 수요건수 { get; set; }
    public int 예약결제건수 { get; set; }
    public decimal 총희망수량 { get; set; }
    public string 수량단위 { get; set; } = string.Empty;
    public decimal 예약결제합계 { get; set; }
    public int? 목표참여자수 { get; set; }
    public decimal? 목표수량 { get; set; }
    public DateTime 생성시각Utc { get; set; }
    public DateTime 수정시각Utc { get; set; }
    public IReadOnlyList<공동구매자동수요응답> 수요목록 { get; set; } = [];
    public IReadOnlyList<공동구매자동집단이벤트응답> 이벤트목록 { get; set; } = [];
}

public sealed class 공동구매자동집단이벤트응답
{
    public string 이벤트유형 { get; set; } = string.Empty;
    public string 요약 { get; set; } = string.Empty;
    public DateTime 발생시각Utc { get; set; }
}

public sealed class 공동구매자동집단조회조건
{
    public string? 상품키 { get; set; }
    public string? 배송권키 { get; set; }
    public string? 현재상태 { get; set; }
}

public static class 공동구매자동수요유형코드
{
    public const string 관심표시 = "InterestOnly";
    public const string 예약결제 = "PaidReservation";
}

public static class 공동구매자동결제상태코드
{
    public const string 미결제 = "NotPaid";
    public const string 예약됨 = "Reserved";
    public const string 결제확정 = "Captured";
}

public static class 공동구매자동집단상태코드
{
    public const string 수요수집중 = "CollectingDemand";
    public const string 확정대기 = "ReadyToConfirm";
    public const string 확정 = "Confirmed";
}

/// <summary>
/// 주문과 입고의 관계를 구분합니다. 주문·결제 시점에는 수령 권리가 생기므로 입고 예정이며,
/// 실제 입고 완료는 물품 도착과 검수 뒤 창고 업무에서만 확정합니다.
/// </summary>
public static class 공동구매개별주문입고상태코드
{
    public const string 미지정 = "NotSpecified";
    public const string 입고예정 = "InboundPlanned";
    public const string 입고완료 = "InboundCompleted";
}
