using Ssalddel.Contracts.Admin.Operations;

namespace Ssalddel.Contracts.Admin.Finance;

public sealed class 운영재무메타데이터응답Dto
{
    public IReadOnlyList<운영관리계정Dto> 관리계정목록 { get; set; } = [];
    public IReadOnlyList<운영재무영향ProfileDto> 재무영향Profile목록 { get; set; } = [];
}

public sealed class 운영관리계정Dto
{
    public string StableId { get; set; } = string.Empty;
    public string 표시명 { get; set; } = string.Empty;
    public string 분류Code { get; set; } = string.Empty;
    public string 정상잔액방향Code { get; set; } = string.Empty;
    public string 운영의미 { get; set; } = string.Empty;
    public bool 실제회계계정승인여부 { get; set; }
}

public sealed class 운영재무영향ProfileDto
{
    public string StableId { get; set; } = string.Empty;
    public string 재무의미Code { get; set; } = string.Empty;
    public string 영향유형Code { get; set; } = string.Empty;
    public string 인식시점Code { get; set; } = string.Empty;
    public string 금액근거Code { get; set; } = string.Empty;
    public string 매핑Revision { get; set; } = string.Empty;
    public string 승인상태Code { get; set; } = string.Empty;
    public bool Simulation전용 { get; set; }
    public bool 운영전표쓰기허용 { get; set; }
    public IReadOnlyList<운영재무영향계정Dto> 계정목록 { get; set; } = [];
}

public sealed class 운영재무영향계정Dto
{
    public string 관리계정StableId { get; set; } = string.Empty;
    public string 관리계정표시명 { get; set; } = string.Empty;
    public string 역할Code { get; set; } = string.Empty;
}

public sealed class 운영재무사건목록응답Dto
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public long TotalCount { get; set; }
    public IReadOnlyList<운영재무사건Dto> Items { get; set; } = [];
}

public sealed class 운영재무사건Dto
{
    public string StableId { get; set; } = string.Empty;
    public string 원본Event유형 { get; set; } = string.Empty;
    public string 원본StableId { get; set; } = string.Empty;
    public long 원본Revision { get; set; }
    public string 재무영향ProfileStableId { get; set; } = string.Empty;
    public string 재무의미Code { get; set; } = string.Empty;
    public decimal 금액 { get; set; }
    public string 통화Code { get; set; } = string.Empty;
    public DateTime 업무발생일시Utc { get; set; }
    public DateTime? 현금이동일시Utc { get; set; }
    public DateTime? 지급기일Utc { get; set; }
    public string 투영상태Code { get; set; } = string.Empty;
    public string 증빙Hash { get; set; } = string.Empty;
    public IReadOnlyList<운영관리계정전기Dto> 전기목록 { get; set; } = [];
}

public sealed class 운영관리계정전기Dto
{
    public int LineNumber { get; set; }
    public string 관리계정StableId { get; set; } = string.Empty;
    public string 관리계정표시명 { get; set; } = string.Empty;
    public string 전기방향Code { get; set; } = string.Empty;
    public decimal 금액 { get; set; }
    public string 통화Code { get; set; } = string.Empty;
    public string 매핑Revision { get; set; } = string.Empty;
}

public sealed class 운영관리계정잔액Dto
{
    public string 관리계정StableId { get; set; } = string.Empty;
    public string 관리계정표시명 { get; set; } = string.Empty;
    public string 통화Code { get; set; } = string.Empty;
    public decimal 차변합계 { get; set; }
    public decimal 대변합계 { get; set; }
    public decimal 정상잔액 { get; set; }
    public string 정상잔액방향Code { get; set; } = string.Empty;
}

public sealed class 운영재무대사예외Dto
{
    public string StableId { get; set; } = string.Empty;
    public string 재무사건StableId { get; set; } = string.Empty;
    public string 예외Code { get; set; } = string.Empty;
    public string 상태Code { get; set; } = string.Empty;
    public string 요약Code { get; set; } = string.Empty;
    public DateTime 감지일시Utc { get; set; }
    public DateTime? 해결일시Utc { get; set; }
}

public sealed class 플랫폼운영경제성원장평가요청Dto
{
    public string 시나리오StableId { get; set; } = string.Empty;
    public string 국가Code { get; set; } = string.Empty;
    public string 관할Code { get; set; } = string.Empty;
    public string 통화Code { get; set; } = string.Empty;
    public DateOnly 기간시작일 { get; set; }
    public DateOnly 기간종료일 { get; set; }
    public long 완료주문수 { get; set; }
    public decimal 기초가용현금 { get; set; }
    public string 본인대리인가정Code { get; set; } = 플랫폼운영경제성본인대리인가정Codes.미정;
    public IReadOnlyList<플랫폼운영경제성금액항목Dto> 시나리오항목 { get; set; } = [];
}

public sealed class 플랫폼운영경제성원장평가응답Dto
{
    public string 원장SnapshotHash { get; set; } = string.Empty;
    public 플랫폼운영경제성평가응답Dto 평가 { get; set; } = new();
}
