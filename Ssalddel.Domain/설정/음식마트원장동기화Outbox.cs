using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace 살뜰.도메인.설정;

[Table("음식마트원장동기화_Outbox")]
public sealed class 음식마트원장동기화Outbox
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("idempotency_key")]
    [MaxLength(200)]
    public string 멱등키 { get; set; } = string.Empty;

    [Column("sync_type")]
    [MaxLength(40)]
    public string 동기화유형 { get; set; } = string.Empty;

    [Column("source_id")]
    [MaxLength(160)]
    public string 원천Id { get; set; } = string.Empty;

    [Column("updated_by")]
    [MaxLength(160)]
    public string 변경자 { get; set; } = string.Empty;

    [Column("payload_json")]
    public string PayloadJson { get; set; } = string.Empty;

    [Column("status")]
    [MaxLength(40)]
    public string 처리상태 { get; set; } = "Pending";

    [Column("attempt_count")]
    public int 시도횟수 { get; set; }

    [Column("last_attempted_at_utc")]
    public DateTime? 마지막시도시각Utc { get; set; }

    [Column("last_error")]
    [MaxLength(2000)]
    public string 마지막오류 { get; set; } = string.Empty;

    [Column("created_at_utc")]
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    [Column("updated_at_utc")]
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public static class 음식마트원장동기화유형코드
{
    public const string 음식주문 = "FoodOrder";
    public const string 창고출고 = "WarehouseOutbound";
    public const string 음식배차요청 = "FoodDispatchRequest";
    public const string 음식배달완료WorldProjection = "FoodDeliveryCompletedWorldProjection";
}
