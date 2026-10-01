namespace Hongdal.Contracts.Common.Community;

public static class 개별주문관점코드
{
    public const string 주문자 = "orderer";
    public const string 판매자 = "seller";
    public const string 창고관리자 = "warehouse-manager";
    public const string 운송담당자 = "transport-operator";
    public const string 공동원장 = "community-ledger";
}

/// <summary>역할 관계로 제한된 개별 주문 원장 목록의 한 항목입니다.</summary>
public sealed class 개별주문관점항목응답
{
    public string 주문원장Id { get; set; } = string.Empty;
    public long Revision { get; set; }
    public string 원장템플릿Key { get; set; } = string.Empty;
    public string 제목 { get; set; } = string.Empty;
    public string 상태 { get; set; } = string.Empty;
    public string? 현재단계Key { get; set; }
    public string? 주문자표시명 { get; set; }
    public string 관계코드 { get; set; } = string.Empty;
    public string 조회근거 { get; set; } = string.Empty;
    public string? 공동원장Id { get; set; }
    public IReadOnlyList<string> 관련원장역할목록 { get; set; } = [];
    public int 관련하위원장수 { get; set; }
    public int 상세공개요청필요수 { get; set; }
    public DateTime 생성시각Utc { get; set; }
    public DateTime 수정시각Utc { get; set; }
}

public sealed class 개별주문관점목록조회요청
{
    public int Page { get; set; }
    public int PageSize { get; set; } = 25;
    public string? Search { get; set; }
    public string? Status { get; set; }
    public string? SortBy { get; set; }
    public bool SortDescending { get; set; } = true;
}

public sealed class 개별주문관점페이지응답
{
    public IReadOnlyList<개별주문관점항목응답> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
