namespace 살뜰.도메인.운송;

/// <summary>
/// 운송 현장 문제 중 운영 검토와 정산 보류가 필요한 사건의 최소 실행 원장입니다.
/// 원문 메모, 상세 주소, 좌표, 증빙 URL은 기존 권한 경계에만 두고 이 원장에는 복제하지 않습니다.
/// </summary>
public sealed class 비정상운송사건
{
    public string 사건StableId { get; set; } = string.Empty;

    public long 운송Id { get; set; }

    public string 운송의뢰Id { get; set; } = string.Empty;

    public string 사건유형Code { get; set; } = string.Empty;

    public string 원본예외Code { get; set; } = string.Empty;

    public string 단계Code { get; set; } = string.Empty;

    public string 상태Code { get; set; } = 비정상운송사건상태Codes.운영검토대기;

    public string 현재담당Code { get; set; } = 비정상운송사건담당Codes.플랫폼운영검토;

    public string 보류전정산상태Code { get; set; } = string.Empty;

    public bool 정산보류적용여부 { get; set; }

    public int? 전체수량 { get; set; }

    public int? 정상확인수량 { get; set; }

    public int? 영향수량 { get; set; }

    public string 업무통제상태Code { get; set; } = 비정상운송업무통제상태Codes.주의진행;

    public string 보류범위Code { get; set; } = 비정상운송보류범위Codes.없음;

    public bool 현장진행불가 { get; set; }

    public string 보험검토상태Code { get; set; } = 비정상운송보험검토상태Codes.미요청;

    public bool 증빙참조있음 { get; set; }

    public DateTime 최초신고시각Utc { get; set; }

    public DateTime 최근신고시각Utc { get; set; }

    public long Revision { get; set; } = 1;

    public string 해결결과Code { get; set; } = string.Empty;

    public Guid? 최근검토요청Id { get; set; }

    public string 최근검토자UserId { get; set; } = string.Empty;

    public string 최근검토사유 { get; set; } = string.Empty;

    public DateTime? 최근검토시각Utc { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}

public static class 비정상운송사건유형Codes
{
    public const string 수량불일치 = "QuantityMismatch";
    public const string 화물훼손 = "CargoDamage";
    public const string 하차지부재 = "DropoffRecipientUnavailable";
}

public static class 비정상운송사건상태Codes
{
    public const string 운영검토대기 = "OperationsReviewPending";
    public const string 조치결정 = "ActionDecided";
    public const string 종료 = "Closed";

    public static bool 열림인가(string? 상태Code)
        => string.Equals(상태Code, 운영검토대기, StringComparison.Ordinal);
}

public static class 비정상운송업무통제상태Codes
{
    public const string 주의진행 = "ContinueWithCaution";
    public const string 일부보류 = "PartiallyHeld";
    public const string 전체보류 = "FullyHeld";
    public const string 재개 = "Resumed";
    public const string 종료 = "Closed";
}

public static class 비정상운송보류범위Codes
{
    public const string 없음 = "None";
    public const string 영향수량 = "AffectedQuantity";
    public const string 전체운송 = "EntireTransport";
}

public static class 비정상운송해결결과Codes
{
    public const string 정상분인수영향분보류 = "NormalAcceptedAffectedHeld";
    public const string 전체운송보류 = "EntireTransportHeld";
    public const string 운송재개 = "TransportResumed";
    public const string 사건종료 = "IncidentClosed";
}

public static class 비정상운송보험검토상태Codes
{
    public const string 미요청 = "NotRequested";
    public const string 적용가능성검토대기 = "EligibilityReviewPending";
}

public static class 비정상운송사건담당Codes
{
    public const string 플랫폼운영검토 = "PlatformOperationsReview";
}
