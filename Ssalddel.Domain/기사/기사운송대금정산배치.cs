namespace 살뜰.도메인.기사;

/// <summary>이미 승인된 운송별 채무의 지급 준비 명세입니다. 새 채무나 실송금 기록이 아닙니다.</summary>
public sealed class 기사운송대금정산배치
{
    public long Id { get; set; }
    public long Revision { get; set; } = 1;
    public string 기사Id { get; set; } = string.Empty;
    public string 통화코드 { get; set; } = "KRW";
    public string 실행모드코드 { get; set; } = string.Empty;
    public string 정책판본 { get; set; } = string.Empty;
    public string 일정코드 { get; set; } = 배송료정산배치Codes.주간;
    /// <summary>원운송의 실제 완료 시각을 기준으로 하는 포함 시작/제외 종료 UTC 기간입니다.</summary>
    public DateTime 기간시작Utc { get; set; }
    public DateTime 기간종료Utc { get; set; }
    public string 기간기준Code { get; set; } = 배송료정산배치Codes.작업완료일;
    /// <summary>한국 시간으로 계산한 예정 시각입니다. 은행 휴일 조정·실제 송금 완료 근거가 아닙니다. 건별은 미정입니다.</summary>
    public DateTime? PayoutScheduledAtUtc { get; set; }
    public string 멱등키 { get; set; } = string.Empty;
    public string ContentDigest { get; set; } = string.Empty;
    public string 상태코드 { get; set; } = 배송료정산배치Codes.준비;
    public decimal 승인합계금액 { get; set; }
    public decimal 준비합계금액 { get; set; }
    public int 준비건수 { get; set; }
    public int 보류건수 { get; set; }
    public string 생성관리자Id { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public List<기사운송대금정산배치행> 행목록 { get; set; } = [];
}

public sealed class 기사운송대금정산배치행
{
    public long Id { get; set; }
    public long 정산배치Id { get; set; }
    public 기사운송대금정산배치 정산배치 { get; set; } = null!;
    public long 지급요청Id { get; set; }
    public 기사운송대금지급요청 지급요청 { get; set; } = null!;
    public long 운송Id { get; set; }
    public string 의뢰Id { get; set; } = string.Empty;
    public decimal 동결금액 { get; set; }
    public string 상태코드 { get; set; } = 배송료정산배치Codes.준비;
    public string 보류사유코드 { get; set; } = string.Empty;
    public string 보류메시지 { get; set; } = string.Empty;
    public DateTime 최근검증시각Utc { get; set; }
}

public static class 배송료정산배치Codes
{
    public const string 건별 = "Single";
    public const string 주간 = "Weekly";
    public const string 준비 = "Prepared";
    public const string 일부보류 = "PartiallyHeld";
    public const string 보류 = "Held";
    public const string 작업완료일 = "WorkCompletedAt";
}
