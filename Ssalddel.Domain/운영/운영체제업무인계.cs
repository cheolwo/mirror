namespace 살뜰.도메인.운영;

public sealed class 운영체제업무인계
{
    public string 인계StableId { get; set; } = string.Empty;
    public string 생성멱등Key { get; set; } = string.Empty;
    public string 출발운영체제Id { get; set; } = string.Empty;
    public string 도착운영체제Id { get; set; } = string.Empty;
    public string 현재책임운영체제Id { get; set; } = string.Empty;
    public string 출발업무유형Code { get; set; } = string.Empty;
    public string 출발업무StableId { get; set; } = string.Empty;
    public long 출발업무Revision { get; set; }
    public string 도착업무StableId { get; set; } = string.Empty;
    public string 인계계약Code { get; set; } = string.Empty;
    public string 인계계약Revision { get; set; } = string.Empty;
    public string 최소상태사본Json { get; set; } = "{}";
    public string 공개범위Code { get; set; } = string.Empty;
    public string 상태Code { get; set; } = string.Empty;
    public string 마지막응답요청Id { get; set; } = string.Empty;
    public string 마지막응답결정Code { get; set; } = string.Empty;
    public string 응답사유Code { get; set; } = string.Empty;
    public long Revision { get; set; }
    public DateTime 요청시각Utc { get; set; }
    public DateTime 만료시각Utc { get; set; }
    public DateTime? 응답시각Utc { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public static class 운영체제업무인계Outbox상태Codes
{
    public const string 대기 = "Pending";
    public const string 처리중 = "Processing";
    public const string 완료 = "Completed";
    public const string 재시도대기 = "RetryPending";
}

public sealed class 운영체제업무인계Outbox
{
    public long Id { get; set; }
    public string 멱등Key { get; set; } = string.Empty;
    public string 인계StableId { get; set; } = string.Empty;
    public string 이벤트Type { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "{}";
    public string 처리상태Code { get; set; } = 운영체제업무인계Outbox상태Codes.대기;
    public int 처리시도수 { get; set; }
    public DateTime? 다음처리시각Utc { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
