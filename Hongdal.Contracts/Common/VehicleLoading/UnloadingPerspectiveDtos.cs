namespace Hongdal.Contracts.Common.VehicleLoading;

/// <summary>하차 업무를 읽는 다섯 사용자 관점 코드입니다.</summary>
public static class 하차업무관점코드
{
    public const string 주문자 = "orderer";
    public const string 판매자 = "seller";
    public const string 창고관리자 = "warehouse-manager";
    public const string 운송담당자 = "transport-operator";
    public const string 공동원장 = "community-ledger";
}

/// <summary>여러 운송 상태를 하차 업무에서 사용하는 세 단계로 정규화합니다.</summary>
public static class 하차작업상태코드
{
    public const string 대기 = "하차대기";
    public const string 도착 = "하차지도착";
    public const string 완료 = "하차완료";
}

/// <summary>출고 화물·운송원장·후속 입고 요청을 결합한 하차 작업 한 건입니다.</summary>
public sealed class 하차관점항목응답
{
    public string 하차작업Id { get; set; } = string.Empty;
    public long 출고예정Id { get; set; }
    public long 운송원장Id { get; set; }
    public string 운송의뢰Id { get; set; } = string.Empty;
    public string 운송번호 { get; set; } = string.Empty;
    public string 관계코드 { get; set; } = string.Empty;
    public string 조회근거 { get; set; } = string.Empty;
    public string 하차상태 { get; set; } = 하차작업상태코드.대기;
    public string 운송상태 { get; set; } = string.Empty;
    public bool 하차가능여부 { get; set; }
    public bool 하차완료여부 { get; set; }
    public DateTime? 하차완료일시 { get; set; }
    public string 주문참조번호 { get; set; } = string.Empty;
    public string 주문자UserId { get; set; } = string.Empty;
    public string 판매자UserId { get; set; } = string.Empty;
    public string 화주UserId { get; set; } = string.Empty;
    public string? 확정기사UserId { get; set; }
    public long 출고창고Id { get; set; }
    public string 출고창고명 { get; set; } = string.Empty;
    public long? 입고요청Id { get; set; }
    public long? 도착창고Id { get; set; }
    public string 도착창고명 { get; set; } = string.Empty;
    public bool 창고입고연결여부 { get; set; }
    public string 상차주소 { get; set; } = string.Empty;
    public string 상차상세주소 { get; set; } = string.Empty;
    public string 하차주소 { get; set; } = string.Empty;
    public string 하차상세주소 { get; set; } = string.Empty;
    public string 상품명 { get; set; } = string.Empty;
    public string SKU { get; set; } = string.Empty;
    public int 수량 { get; set; }
    public long? 출고묶음Id { get; set; }
    public string? 공동원장Id { get; set; }
    public string? 공동원장템플릿Key { get; set; }
    public DateTime 생성시각Utc { get; set; }
    public DateTime 수정시각Utc { get; set; }
}

/// <summary>하차 목록의 검색·상태·도착 창고·정렬·페이지 조건입니다. Page는 0부터 시작합니다.</summary>
public sealed class 하차관점목록조회요청
{
    public int Page { get; set; }
    public int PageSize { get; set; } = 25;
    public string? Search { get; set; }
    public string? Status { get; set; }
    public string? SortBy { get; set; }
    public bool SortDescending { get; set; } = true;
    public long? WarehouseId { get; set; }
}

public sealed class 하차관점페이지응답
{
    public IReadOnlyList<하차관점항목응답> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
