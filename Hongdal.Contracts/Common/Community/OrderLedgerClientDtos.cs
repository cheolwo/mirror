using Hongdal.Contracts.Common.ContractManagement;

namespace Hongdal.Contracts.Common.Community;

/// <summary>
/// 주문원장 API가 반환하는 원장의 클라이언트용 요약 투영입니다.
/// 서버 원장 문서의 전체 내부 구조를 노출하지 않고 화면 흐름에 필요한 필드만 받습니다.
/// </summary>
public sealed class 주문원장원장요약Dto
{
    public string 원장Id { get; set; } = string.Empty;
    public long Revision { get; set; }
    public string 커뮤니티Id { get; set; } = string.Empty;
    public string 원장템플릿Key { get; set; } = string.Empty;
    public string 제목 { get; set; } = string.Empty;
    public string 상태 { get; set; } = string.Empty;
    public string? 현재단계Key { get; set; }
    public string 생성자표시명 { get; set; } = string.Empty;
    public IReadOnlyList<주문원장포함원장참조Dto> 포함원장목록 { get; set; } = [];
    public IReadOnlyDictionary<string, string> 외부참조 { get; set; } = new Dictionary<string, string>();
    public IReadOnlyDictionary<string, string> 확장속성 { get; set; } = new Dictionary<string, string>();
    public DateTime 생성시각Utc { get; set; }
    public DateTime 수정시각Utc { get; set; }
}

public sealed class 주문원장포함원장참조Dto
{
    public string 원장Id { get; set; } = string.Empty;
    public string 원장템플릿Key { get; set; } = string.Empty;
    public string 역할 { get; set; } = string.Empty;
    public bool 필수여부 { get; set; }
    public int 표시순서 { get; set; }
}

public sealed class 주문하위원장연결ClientRequest
{
    public string 하위원장Id { get; set; } = string.Empty;
    public string 역할 { get; set; } = string.Empty;
    public bool 필수여부 { get; set; } = true;
    public int? 표시순서 { get; set; }
    public long? 기대Revision { get; set; }
}

public sealed class 주문원장통합공개Dto
{
    public 주문원장원장요약Dto 주문원장 { get; set; } = new();
    public 주문원장서명상태공개Dto? 주문자서명상태 { get; set; }
    public IReadOnlyList<주문포함원장공개Dto> 포함원장목록 { get; set; } = [];
    public int 전체하위원장수 { get; set; }
    public int 완료하위원장수 { get; set; }
    public bool 필수하위원장완료여부 { get; set; }
    public int 서명대상주문수 { get; set; }
    public int 서명완료주문수 { get; set; }
    public IReadOnlyList<string> 미서명주문Ids { get; set; } = [];
    public bool 전체주문서명완료여부 { get; set; }
}

public sealed class 주문포함원장공개Dto
{
    public string 원장Id { get; set; } = string.Empty;
    public string 원장템플릿Key { get; set; } = string.Empty;
    public string 역할 { get; set; } = string.Empty;
    public bool 필수여부 { get; set; }
    public int 표시순서 { get; set; }
    public string 조회상태 { get; set; } = string.Empty;
    public 주문원장원장요약Dto? 원장 { get; set; }
    public 주문원장서명상태공개Dto? 주문자서명상태 { get; set; }
}

public sealed class 주문원장역할별조회공개Dto
{
    public string 주문원장Id { get; set; } = string.Empty;
    public string 조회역할 { get; set; } = string.Empty;
    public string 주문원장상태 { get; set; } = string.Empty;
    public string 주문원장조회근거 { get; set; } = string.Empty;
    public 주문원장원장요약Dto? 주문원장상세 { get; set; }
    public IReadOnlyList<주문역할별원장항목공개Dto> 관련원장목록 { get; set; } = [];
    public int 상세공개요청필요수 { get; set; }
}

public sealed class 주문역할별원장항목공개Dto
{
    public string 원장Id { get; set; } = string.Empty;
    public string 원장템플릿Key { get; set; } = string.Empty;
    public string 주문안역할 { get; set; } = string.Empty;
    public bool 필수여부 { get; set; }
    public string 조회근거 { get; set; } = string.Empty;
    public bool 상세조회가능여부 { get; set; }
    public bool 공개요청가능여부 { get; set; }
    public 주문원장원장요약Dto? 원장상세 { get; set; }
}

public sealed class 주문원장서명준비ClientRequest
{
    public string 계약문서번호 { get; set; } = string.Empty;
    public string 문서Hash { get; set; } = string.Empty;
    public string? 주문자표시명 { get; set; }
    public DateTimeOffset? 만료시각Utc { get; set; }
    public long? 기대Revision { get; set; }
}

public sealed class 주문원장서명등록ClientRequest
{
    public string 문서Hash { get; set; } = string.Empty;
    public string 동의문Hash { get; set; } = string.Empty;
    public string 서명증적Hash { get; set; } = string.Empty;
    public string 서명방법Code { get; set; } = ContractSignatureMethodCode.PlatformClickSign;
    public string? 접속IpHash { get; set; }
    public DateTimeOffset? 서명시각Utc { get; set; }
    public long? 기대Revision { get; set; }
}

public sealed class 주문원장서명상태공개Dto
{
    public string 주문원장Id { get; set; } = string.Empty;
    public long Revision { get; set; }
    public string 상태Code { get; set; } = ContractSignatureStatusCode.Draft;
    public int 필수서명자수 { get; set; }
    public int 서명완료자수 { get; set; }
    public bool 전체서명완료여부 { get; set; }
    public DateTimeOffset? 최근서명시각Utc { get; set; }
    public DateTimeOffset? 만료시각Utc { get; set; }
}
