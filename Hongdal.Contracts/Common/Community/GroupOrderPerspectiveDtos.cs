namespace Hongdal.Contracts.Common.Community;

public static class 공동주문관점코드
{
    public const string 주문자 = "orderer";
    public const string 판매자 = "seller";
    public const string 창고관리자 = "warehouse-manager";
    public const string 운송담당자 = "transport-operator";
    public const string 공동원장 = "community-ledger";
}

/// <summary>개별 주문 원장의 집합으로 계산되는 공동주문 한 건의 역할별 읽기 모델입니다.</summary>
public sealed class 공동주문관점항목응답
{
    public string 공동주문원장Id { get; set; } = string.Empty;
    public long Revision { get; set; }
    public string 제목 { get; set; } = string.Empty;
    public string 상태 { get; set; } = string.Empty;
    public string? 현재단계Key { get; set; }
    public string 관계코드 { get; set; } = string.Empty;
    public string 조회근거 { get; set; } = string.Empty;
    public string? 공동원장Id { get; set; }
    public string? 자동집단Id { get; set; }
    public string? 상품키 { get; set; }
    public string? 상품명 { get; set; }
    public int 개별주문수 { get; set; }
    public int 완료개별주문수 { get; set; }
    public bool 필수개별주문완료여부 { get; set; }
    public int 서명대상주문수 { get; set; }
    public int 서명완료주문수 { get; set; }
    public bool 전체주문서명완료여부 { get; set; }
    public IReadOnlyList<string> 미서명주문Ids { get; set; } = [];
    public DateTime 생성시각Utc { get; set; }
    public DateTime 수정시각Utc { get; set; }
}

public sealed class 공동주문관점목록조회요청
{
    public int Page { get; set; }
    public int PageSize { get; set; } = 25;
    public string? Search { get; set; }
    public string? Status { get; set; }
    public string? SortBy { get; set; }
    public bool SortDescending { get; set; } = true;
}

public sealed class 공동주문관점페이지응답
{
    public IReadOnlyList<공동주문관점항목응답> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
