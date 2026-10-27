namespace Ssalddel.Contracts.Mart;

public static class 마트주문요청상태코드
{
    public const string 제출됨 = "Submitted";
    public const string 철회됨 = "Withdrawn";
    public const string 확정확인중 = "ConfirmationPending";
    public const string 확정됨 = "Confirmed";

    public static string 표시명(string? value)
        => value switch
        {
            제출됨 => "접수 대기",
            철회됨 => "철회됨",
            확정확인중 => "주문 확정 결과 확인 중",
            확정됨 => "전문 주문으로 연결됨",
            _ => "알 수 없는 상태"
        };

    public static bool 지원됨(string? value)
        => string.Equals(value, 제출됨, StringComparison.Ordinal)
           || string.Equals(value, 철회됨, StringComparison.Ordinal)
           || string.Equals(value, 확정확인중, StringComparison.Ordinal)
           || string.Equals(value, 확정됨, StringComparison.Ordinal);
}

public static class 마트주문요청안내
{
    public const string 현재버전 = "2026-07-20";

    public static IReadOnlyList<string> 문구 { get; } =
    [
        "이 요청은 상품과 수량에 대한 비구속 구매 의향을 플랫폼 원장에 저장합니다.",
        "제출 시점의 공개 가격과 판매 가능 수량을 서버가 다시 확인하지만 재고를 차감하거나 예약하지 않습니다.",
        "결제, 주문 확정, 피킹·포장, 배송과 계약은 이 단계에서 실행되지 않습니다.",
        "수령인, 전화번호, 주소와 결제정보는 이 페이지에서 수집하지 않습니다."
    ];

    public static bool 유효한확인(마트주문요청등록요청? request)
        => request is not null
           && request.비구속주문요청확인
           && string.Equals(request.안내버전, 현재버전, StringComparison.Ordinal);
}

public sealed class 마트주문요청등록요청
{
    public Guid? 신청개인정보동의증적Id { get; set; }

    public string 신청출처Code { get; set; } = string.Empty;

    public Guid 클라이언트요청Id { get; set; }

    public long 공개상품Id { get; set; }

    public int 수량 { get; set; } = 1;

    public bool 비구속주문요청확인 { get; set; }

    public string 안내버전 { get; set; } = string.Empty;
}

public sealed class 마트주문요청목록조회요청
{
    public string? 상태코드 { get; set; }

    public string? Search { get; set; }

    public int Page { get; set; }

    public int PageSize { get; set; } = 20;
}

public sealed class 마트주문요청수량변경요청
{
    public int 수량 { get; set; }

    public string 기대상태코드 { get; set; } = 마트주문요청상태코드.제출됨;

    public bool 비구속주문요청확인 { get; set; }

    public string 안내버전 { get; set; } = string.Empty;
}

public sealed class 마트주문요청철회요청
{
    public string 기대상태코드 { get; set; } = 마트주문요청상태코드.제출됨;
}

public sealed class 마트주문요청응답
{
    public Guid 주문요청Id { get; set; }

    public long 공개상품Id { get; set; }

    public string 상품명 { get; set; } = string.Empty;

    public string 판매단위 { get; set; } = string.Empty;

    public decimal 단가 { get; set; }

    public int 수량 { get; set; }

    public decimal 합계 { get; set; }

    public string 통화 { get; set; } = "KRW";

    public int 제출시판매가능수량 { get; set; }

    public DateTime 재고기준시각Utc { get; set; }

    public string 상태코드 { get; set; } = 마트주문요청상태코드.제출됨;

    public string 상태명 { get; set; } = 마트주문요청상태코드.표시명(마트주문요청상태코드.제출됨);

    public string 안내버전 { get; set; } = string.Empty;

    public DateTime 제출일시Utc { get; set; }

    public bool 재고예약됨 { get; set; }

    public bool 결제됨 { get; set; }
}

public sealed class 마트주문요청목록응답
{
    public IReadOnlyList<마트주문요청응답> Items { get; set; } = [];

    public int TotalCount { get; set; }

    public int Page { get; set; }

    public int PageSize { get; set; }
}
