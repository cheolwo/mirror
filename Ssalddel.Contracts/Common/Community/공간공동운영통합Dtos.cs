namespace Ssalddel.Contracts.Common.Community;

/// <summary>기존 공동운영 원장 아래 회차·본인 주문·전문 원천의 현재 관계를 읽기만 하는 비공개 조회입니다.</summary>
public sealed class 공간공동운영통합Response
{
    public string LedgerId { get; set; } = "";
    public long Revision { get; set; }
    public string ExecutionMode { get; set; } = "";
    public string StatusCode { get; set; } = "current";
    public string OrdersReadStatusCode { get; set; } = "current";
    public DateTimeOffset AsOfUtc { get; set; }
    public bool HasIncompleteVisibility { get; set; }
    public IReadOnlyList<공간공동운영통합회차Dto> Cycles { get; set; } = [];
    public IReadOnlyList<공간공동운영통합원천Dto> SharedSources { get; set; } = [];
    public IReadOnlyList<공간공동운영통합관계Dto> Relations { get; set; } = [];
}

public sealed class 공간공동운영통합회차Dto
{
    public string NodeKey { get; set; } = "";
    public string CycleId { get; set; } = "";
    public int Number { get; set; }
    public long TermsRevision { get; set; }
    public string StatusCode { get; set; } = "";
    public IReadOnlyList<공간공동운영통합주문Dto> Orders { get; set; } = [];
    public IReadOnlyList<string> SharedSourceKeys { get; set; } = [];
}

public sealed class 공간공동운영통합주문Dto
{
    public string NodeKey { get; set; } = "";
    public Guid OrderRequestId { get; set; }
    public int TermsVersion { get; set; }
    public string ProductName { get; set; } = "";
    /// <summary>공동운영 주문의 협의 상태이며 실제 배송·수령·지급 상태가 아닙니다.</summary>
    public string AgreementStatusCode { get; set; } = "";
    public string RootOrderLedgerId { get; set; } = "";
    public string RootReadStatusCode { get; set; } = "not-created";
    /// <summary>현재 조회 권한과 전문 원천 계보를 모두 확인한 연결만 포함합니다.</summary>
    public IReadOnlyList<공간공동운영통합원천Dto> Children { get; set; } = [];
}

/// <summary>중복 복제하지 않는 전문 원천 노드입니다. 정산의 Completed는 지급 준비이며 실제 입금 확인이 아닙니다.</summary>
public sealed class 공간공동운영통합원천Dto
{
    public string NodeKey { get; set; } = "";
    public string KindCode { get; set; } = "";
    public string ReferenceId { get; set; } = "";
    public string StatusCode { get; set; } = "";
    public string Label { get; set; } = "";
    public bool Verified { get; set; }
    public bool Completed { get; set; }
    public bool RequiresAttention { get; set; }
    public string Href { get; set; } = "";
}

public sealed class 공간공동운영통합관계Dto
{
    public string FromKey { get; set; } = "";
    public string ToKey { get; set; } = "";
    public string RelationCode { get; set; } = "";
}
