namespace 살뜰.도메인.기사;

/// <summary>
/// 완료 운송의 기사 지급을 관리자가 검토·승인한 내부 기록입니다.
/// 이 기록과 Outbox 처리는 실제 송금 완료를 의미하지 않습니다.
/// </summary>
public sealed class 기사운송대금지급요청
{
    public long Id { get; set; }

    public long 운송Id { get; set; }

    public string 운송번호 { get; set; } = string.Empty;

    public string 의뢰Id { get; set; } = string.Empty;

    public string 기사Id { get; set; } = string.Empty;

    public decimal 지급예정금액 { get; set; }

    public string 통화코드 { get; set; } = "KRW";

    public string 멱등키 { get; set; } = string.Empty;

    public string 상태코드 { get; set; } = 기사지급요청상태코드.승인됨;

    public string 승인관리자Id { get; set; } = string.Empty;

    public string 승인사유 { get; set; } = string.Empty;

    public string 실행모드코드 { get; set; } = string.Empty;

    public DateTime 승인일시Utc { get; set; }

    public DateTime? Simulation검증일시Utc { get; set; }

    public string 마지막처리코드 { get; set; } = string.Empty;

    public string 마지막처리메시지 { get; set; } = string.Empty;

    /// <summary>자동 명세 후보 점검의 진행 표시입니다. 승인·지급·Provider 처리 결과가 아닙니다.</summary>
    public DateTime? 자동명세검토시각Utc { get; set; }
    public string? 자동명세검토코드 { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public static class 기사지급요청상태코드
{
    public const string 승인됨 = "Approved";
    public const string Simulation검증완료 = "SimulationVerified";
    public const string 재시도대기 = "RetryScheduled";
    public const string 운영Provider미구성 = "OperationalProviderNotConfigured";
}
