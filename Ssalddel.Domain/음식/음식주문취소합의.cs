namespace 살뜰.도메인.음식;

/// <summary>음식 원주문의 특정 판본에 대한 불변 비용 제안과 양측 동의·실행 영수증입니다.</summary>
public sealed class 음식주문취소합의
{
    public long Id { get; set; }
    public string 합의StableId { get; set; } = string.Empty;
    public long 음식주문Id { get; set; }
    public string 주문번호 { get; set; } = string.Empty;
    public long 주문판본 { get; set; }
    public string 주문상태 { get; set; } = string.Empty;
    public decimal 주문자부담액 { get; set; }
    public decimal 음식점부담액 { get; set; }
    public string 비용근거참조 { get; set; } = string.Empty;
    public bool 주문자동의 { get; set; }
    public bool 음식점동의 { get; set; }
    public string 영수증Json { get; set; } = "[]";
    public DateTime? 취소반영시각Utc { get; set; }
    public long Revision { get; set; } = 1;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
