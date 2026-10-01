namespace Hongdal.Contracts.Common.Sales;

public static class 판매자유형코드
{
    public const string 일반판매자 = "GeneralSeller";
    public const string 농가생산자 = "FarmProducer";
    public const string 수출업자 = "Exporter";
    public const string 제조자 = "Manufacturer";
    public const string 협동조합 = "Cooperative";
    public const string 기타 = "Other";
}

public static class 판매페이지상태코드
{
    public const string 초안 = "Draft";
    public const string 검수대기 = "ReviewPending";
}

/// <summary>
/// 판매자가 직접 작성하거나 외부 상품 상세를 참고해 Hongdal 판매 페이지 초안을 만드는 요청입니다.
/// 공동주문은 판매 페이지 유형이 아니라 선택 가능한 주문 방식 중 하나입니다.
/// </summary>
public sealed class 판매페이지초안생성요청
{
    public string 판매자유형 { get; set; } = 판매자유형코드.일반판매자;
    public string 판매자표시명 { get; set; } = string.Empty;
    public string 상품명 { get; set; } = string.Empty;
    public string 한줄소개 { get; set; } = string.Empty;
    public string 상세설명 { get; set; } = string.Empty;
    public string? 원산지표시 { get; set; }
    public string? 출고지표시 { get; set; }
    public decimal? 판매가 { get; set; }
    public string 통화코드 { get; set; } = "KRW";
    public int 최소주문수량 { get; set; } = 1;
    public bool 개별주문허용 { get; set; } = true;
    public bool 공동주문허용 { get; set; } = true;
    public int? 공동주문최소수량 { get; set; }
    public string? Amazon상품Url { get; set; }
}

public sealed class 판매페이지초안수정요청
{
    public long 기대Revision { get; set; }
    public string 판매자유형 { get; set; } = 판매자유형코드.일반판매자;
    public string 판매자표시명 { get; set; } = string.Empty;
    public string 상품명 { get; set; } = string.Empty;
    public string 한줄소개 { get; set; } = string.Empty;
    public string 상세설명 { get; set; } = string.Empty;
    public string? 원산지표시 { get; set; }
    public string? 출고지표시 { get; set; }
    public decimal? 판매가 { get; set; }
    public string 통화코드 { get; set; } = "KRW";
    public int 최소주문수량 { get; set; } = 1;
    public bool 개별주문허용 { get; set; } = true;
    public bool 공동주문허용 { get; set; } = true;
    public int? 공동주문최소수량 { get; set; }
    public IReadOnlyList<string> 이미지Url목록 { get; set; } = [];
    public IReadOnlyList<string> 핵심정보목록 { get; set; } = [];
}

public sealed record 판매페이지외부참고자료Dto(
    string 제공자,
    string 마켓플레이스,
    string 참조키,
    string 상품Url,
    string? 외부상품번호,
    decimal? 관측가격,
    string? 관측통화코드,
    bool? 관측재고여부,
    decimal? 관측평점,
    int? 관측리뷰수,
    DateTime 관측일시Utc,
    string 안내문);

public sealed class 판매페이지초안응답
{
    public string 페이지Id { get; set; } = string.Empty;
    public string 상태 { get; set; } = 판매페이지상태코드.초안;
    public string 판매자유형 { get; set; } = 판매자유형코드.일반판매자;
    public string 판매자표시명 { get; set; } = string.Empty;
    public string 상품명 { get; set; } = string.Empty;
    public string 한줄소개 { get; set; } = string.Empty;
    public string 상세설명 { get; set; } = string.Empty;
    public string? 원산지표시 { get; set; }
    public string? 출고지표시 { get; set; }
    public decimal? 판매가 { get; set; }
    public string 통화코드 { get; set; } = "KRW";
    public int 최소주문수량 { get; set; } = 1;
    public bool 개별주문허용 { get; set; }
    public bool 공동주문허용 { get; set; }
    public int? 공동주문최소수량 { get; set; }
    public IReadOnlyList<string> 이미지Url목록 { get; set; } = [];
    public IReadOnlyList<string> 핵심정보목록 { get; set; } = [];
    public 판매페이지외부참고자료Dto? 외부참고자료 { get; set; }
    public long? 연결된판매상품Id { get; set; }
    public string 판매준비안내 { get; set; } = string.Empty;
    public long Revision { get; set; }
    public DateTime 생성시각Utc { get; set; }
    public DateTime 수정시각Utc { get; set; }
}

public sealed class 판매페이지초안목록응답
{
    public IReadOnlyList<판매페이지초안응답> Items { get; set; } = [];
}
