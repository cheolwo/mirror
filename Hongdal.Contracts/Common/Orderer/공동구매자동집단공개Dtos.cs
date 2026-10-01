namespace Hongdal.Contracts.Common.Orderer;

/// <summary>
/// 자동집단 목록에서 공개할 수 있는 집계 정보입니다.
/// 참여자 식별자, 주소, 결제 금액과 내부 원장 식별자는 포함하지 않습니다.
/// </summary>
public class 공동구매자동집단요약응답
{
    public string 자동집단Id { get; set; } = string.Empty;
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
    public int? 목표참여자수 { get; set; }
    public decimal? 목표수량 { get; set; }
    public DateTime 생성시각Utc { get; set; }
    public DateTime 수정시각Utc { get; set; }
}

/// <summary>
/// 수요를 등록한 사용자에게 반환하는 자동집단 응답입니다.
/// 기존 클라이언트와의 JSON 호환을 위해 본인 수요는 <c>수요목록</c>에 한 건만 담습니다.
/// </summary>
public sealed class 공동구매자동집단사용자응답 : 공동구매자동집단요약응답
{
    public string 공동구매주문집계원장Id { get; set; } = string.Empty;
    public IReadOnlyList<공동구매자동본인수요응답> 수요목록 { get; set; } = [];
}

/// <summary>
/// 로그인 사용자가 방금 등록한 본인 수요입니다.
/// 표시명과 주소 참조는 본인 응답에서도 불필요하므로 반환하지 않습니다.
/// </summary>
public sealed class 공동구매자동본인수요응답
{
    public string 수요Id { get; set; } = string.Empty;
    public string 수요출처키 { get; set; } = string.Empty;
    public long? 커뮤니티게시글Id { get; set; }
    public string 자동집단Id { get; set; } = string.Empty;
    public string 상품키 { get; set; } = string.Empty;
    public string 상품명 { get; set; } = string.Empty;
    public string 주문자키 { get; set; } = string.Empty;
    public string 배송권키 { get; set; } = string.Empty;
    public string 배송권명 { get; set; } = string.Empty;
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
