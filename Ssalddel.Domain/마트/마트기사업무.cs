namespace 살뜰.도메인.마트;

/// <summary>마트 원천에만 결속되는 기사 수행 사실. 전달·수령·정산·지급을 합치지 않습니다.</summary>
public sealed class 마트배송시도
{
    public string 시도StableId { get; set; } = string.Empty;
    public string 배차의뢰Id { get; set; } = string.Empty;
    public string 주문참조번호 { get; set; } = string.Empty;
    public string 기사Id { get; set; } = string.Empty;
    public string 상태Code { get; set; } = "Accepted";
    public long Revision { get; set; }
    public int 추천라운드 { get; set; }
    public string? 픽업전표Id { get; set; }
    public long? 픽업전표Revision { get; set; }
    public DateTime 수락시각Utc { get; set; }
    public DateTime? 픽업시각Utc { get; set; }
    public DateTime? 전달시각Utc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

/// <summary>관련 창고 담당자만 복호화할 수 있는 현재 전표. 번호와 QR 원문은 보호 저장합니다.</summary>
public sealed class 마트포장전표
{
    public string 주문참조번호 { get; set; } = string.Empty;
    public string 전표Id { get; set; } = string.Empty;
    public long Revision { get; set; }
    public string 보호코드Json { get; set; } = string.Empty;
    public string QrDigest { get; set; } = string.Empty;
    public bool 활성여부 { get; set; }
    public DateTime 발급시각Utc { get; set; }
    public DateTime? 픽업시각Utc { get; set; }
    public int 확인실패횟수 { get; set; }
    public DateTime? 번호차단만료시각Utc { get; set; }
}

/// <summary>동일 인증 주체의 같은 요청 의미만 재사용합니다. 주소·연락처·QR·번호·응답 JSON은 보관하지 않습니다.</summary>
public sealed class 마트기사요청영수증
{
    public string 작업자UserId { get; set; } = string.Empty;
    public Guid 클라이언트요청Id { get; set; }
    public string 작업Code { get; set; } = string.Empty;
    public string 배차의뢰Id { get; set; } = string.Empty;
    public string 주문참조번호 { get; set; } = string.Empty;
    public string 보호입력Digest { get; set; } = string.Empty;
    public bool 성공여부 { get; set; }
    public string 결과Code { get; set; } = string.Empty;
    public int 결과StatusCode { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

/// <summary>SQL 업무 확정과 같은 transaction에서 예약하고 기존 원장 Adapter로 사후 소비하는 내구성 의도입니다.</summary>
public sealed class 마트기사후속Outbox
{
    public string 멱등키 { get; set; } = string.Empty;
    public string 배차의뢰Id { get; set; } = string.Empty;
    public string 주문참조번호 { get; set; } = string.Empty;
    public string 기사Id { get; set; } = string.Empty;
    public string 시도StableId { get; set; } = string.Empty;
    public long 업무Revision { get; set; }
    public string 종류Code { get; set; } = string.Empty;
    public string 상태Code { get; set; } = "Pending";
    public int 시도횟수 { get; set; }
    public long ClaimRevision { get; set; }
    public string? LeaseOwner { get; set; }
    public DateTime? LeaseUntilUtc { get; set; }
    public DateTime? NextAttemptAtUtc { get; set; }
    public string? 마지막실패Code { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
