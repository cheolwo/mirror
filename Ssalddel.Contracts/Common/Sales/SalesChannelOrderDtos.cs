namespace Ssalddel.Contracts.Common.Sales;

/// <summary>판매채널 주문에서 영속 출고 후보로 투영된 원장의 서버 조회 조건입니다.</summary>
public sealed class 판매채널주문목록조회요청
{
    public int Page { get; set; }
    public int PageSize { get; set; } = 25;
    public string? Search { get; set; }
    public string? SyncScope { get; set; }
    public string? Status { get; set; }
}

/// <summary>같은 판매채널 주문참조번호로 묶인 출고 후보 요약입니다.</summary>
public sealed class 판매채널주문요약응답
{
    public long OrderId { get; set; }
    public string 주문참조번호 { get; set; } = string.Empty;
    public string 채널종류 { get; set; } = string.Empty;
    public string 채널주문번호 { get; set; } = string.Empty;
    public string 국내외구분 { get; set; } = string.Empty;
    public string 출고상태 { get; set; } = string.Empty;
    public int 출고라인수 { get; set; }
    public int 총수량 { get; set; }
    public string 대표상품명 { get; set; } = string.Empty;
    public int 출고창고수 { get; set; }
    public string 출고창고표시 { get; set; } = string.Empty;
    public bool 운송인계여부 { get; set; }
    public DateTime 생성일시 { get; set; }
    public DateTime 수정일시 { get; set; }
}

public sealed class 판매채널주문목록응답
{
    public IReadOnlyList<판매채널주문요약응답> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public IReadOnlyList<판매채널주문보류진단> SyncIssues { get; set; } = [];
}

/// <summary>개인 식별 정보와 전체 창고 주소를 제외한 출고 후보 라인입니다.</summary>
public sealed class 판매채널주문출고라인응답
{
    public long Id { get; set; }
    public long? 판매상품Id { get; set; }
    public long? 입고상품Id { get; set; }
    public long 출고창고Id { get; set; }
    public string 출고창고명 { get; set; } = string.Empty;
    public long? 출고묶음Id { get; set; }
    public string 상품명 { get; set; } = string.Empty;
    public string SKU { get; set; } = string.Empty;
    public int 수량 { get; set; }
    public string 상태 { get; set; } = string.Empty;
    public string? 운송의뢰Id { get; set; }
    public DateTime? 출고처리일시 { get; set; }
    public DateTime 생성일시 { get; set; }
    public DateTime 수정일시 { get; set; }
}

public sealed class 판매채널주문상세응답
{
    public 판매채널주문요약응답 주문 { get; set; } = new();
    public IReadOnlyList<판매채널주문출고라인응답> 출고라인목록 { get; set; } = [];
}

public sealed class 판매채널주문동기화요청
{
    public string SyncScope { get; set; } = CommerceChannelOrderSyncScopes.Domestic;
}

public sealed class 판매채널주문동기화응답
{
    public string SyncScope { get; set; } = string.Empty;
    public int AccountCount { get; set; }
    public int FetchedOrderCount { get; set; }
    public int CreatedOutboundCount { get; set; }
    public int SkippedOrderCount { get; set; }
}
