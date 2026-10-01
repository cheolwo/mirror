namespace Hongdal.Contracts.Common.Warehouse;

/// <summary>창고 입출고 읽기 API에서 사용하는 다섯 화면 관점 코드입니다.</summary>
public static class 창고업무관점코드
{
    public const string 주문자 = "orderer";
    public const string 판매자 = "seller";
    public const string 창고관리자 = "warehouse-manager";
    public const string 운송담당자 = "transport-operator";
    public const string 공동원장 = "community-ledger";
}

public static class 출고상태코드
{
    public const string 예정 = "출고예정";
    public const string 준비중 = "출고준비중";
    public const string 완료 = "출고완료";
    public const string 취소 = "출고취소";
}

/// <summary>역할 관계로 필터링된 출고 예정 한 건의 읽기 전용 응답입니다.</summary>
public sealed class 출고예정항목응답
{
    public long Id { get; set; }
    public long? 주문Id { get; set; }
    public string 주문참조번호 { get; set; } = string.Empty;
    public long? 판매상품Id { get; set; }
    public long? 입고상품Id { get; set; }
    public string 판매자UserId { get; set; } = string.Empty;
    public string 주문자UserId { get; set; } = string.Empty;
    public long 출고창고Id { get; set; }
    public string 출고창고명 { get; set; } = string.Empty;
    public string 출고창고주소 { get; set; } = string.Empty;
    public long? 출고묶음Id { get; set; }
    public string 상품명 { get; set; } = string.Empty;
    public string SKU { get; set; } = string.Empty;
    public int 수량 { get; set; }
    public string 상태 { get; set; } = string.Empty;
    public string? 운송의뢰Id { get; set; }
    public long? 입고요청Id { get; set; }
    public DateTime? 예정출고일 { get; set; }
    public DateTime? 예정도착일 { get; set; }
    public DateTime? 출고처리일시 { get; set; }
    public string? 커뮤니티원장Id { get; set; }
    public string? 커뮤니티원장템플릿Key { get; set; }
    public string? 커뮤니티원장상태 { get; set; }
    public DateTime 생성일시 { get; set; }
}

/// <summary>출고 예정 서버 목록의 검색·정렬·페이지 조건입니다. Page는 0부터 시작합니다.</summary>
public sealed class 출고예정목록조회요청
{
    public int Page { get; set; }
    public int PageSize { get; set; } = 25;
    public string? Search { get; set; }
    public string? SortBy { get; set; }
    public bool SortDescending { get; set; } = true;
    public long? WarehouseId { get; set; }
}

public sealed class 출고예정페이지응답
{
    public IReadOnlyList<출고예정항목응답> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
