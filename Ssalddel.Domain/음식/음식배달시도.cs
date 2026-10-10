using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace 살뜰.도메인.음식;

[Table("음식배달시도")]
public sealed class 음식배달시도
{
    // 새 수락에서 확인한 모드만 기록합니다. 유산 기록의 모드를 추정하지 않습니다.
    [Column("experience_execution_mode_code"), MaxLength(32)]
    public string? 경험실행모드Code { get; set; }
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("시도_stable_id")]
    [MaxLength(220)]
    public string 시도StableId { get; set; } = string.Empty;

    [Column("주문번호")]
    [MaxLength(100)]
    public string 주문번호 { get; set; } = string.Empty;

    [Column("제안_id")]
    [MaxLength(120)]
    public string 제안Id { get; set; } = string.Empty;

    [Column("기사_id")]
    [MaxLength(450)]
    public string 기사Id { get; set; } = string.Empty;

    [Column("추천_라운드")]
    public int 추천라운드 { get; set; }

    [Column("시도_순번")]
    public int 시도순번 { get; set; }

    [Column("상태_code")]
    [MaxLength(40)]
    public string 상태Code { get; set; } = string.Empty;

    [Column("revision")]
    public long Revision { get; set; }

    [Column("수락_시각_utc")]
    public DateTime 수락시각Utc { get; set; }

    [Column("표시_준비_예정_시각_utc")]
    public DateTime? 표시준비예정시각Utc { get; set; }

    [Column("가게_도착_시각_utc")]
    public DateTime? 가게도착시각Utc { get; set; }

    [Column("픽업_완료_시각_utc")]
    public DateTime? 픽업완료시각Utc { get; set; }

    [Column("중단_시각_utc")]
    public DateTime? 중단시각Utc { get; set; }

    [Column("전달_완료_시각_utc")]
    public DateTime? 전달완료시각Utc { get; set; }

    [Column("현장_대기_초")]
    public int? 현장대기초 { get; set; }

    [Column("중단_사유_code")]
    [MaxLength(80)]
    public string? 중단사유Code { get; set; }

    [Column("중단_메모")]
    [MaxLength(500)]
    public string? 중단메모 { get; set; }

    [Column("책임_code")]
    [MaxLength(40)]
    public string? 책임Code { get; set; }

    [Column("조리_지연_재배차_여부")]
    public bool 조리지연재배차여부 { get; set; }

    [Column("재조리_요청_stable_id")]
    [MaxLength(220)]
    public string? 재조리요청StableId { get; set; }

    [Column("재조리_요청_시각_utc")]
    public DateTime? 재조리요청시각Utc { get; set; }

    [Column("유산_추정_여부")]
    public bool 유산추정여부 { get; set; }

    [Column("마지막_요청_id")]
    public Guid? 마지막요청Id { get; set; }

    [Column("검토_요청_id")]
    public Guid? 검토요청Id { get; set; }

    [Column("검토_user_id")]
    [MaxLength(450)]
    public string? 검토UserId { get; set; }

    [Column("검토_사유")]
    [MaxLength(1000)]
    public string? 검토사유 { get; set; }

    [Column("악용_확정_여부")]
    public bool 악용확정여부 { get; set; }

    [Column("created_at_utc")]
    public DateTime CreatedAtUtc { get; set; }

    [Column("updated_at_utc")]
    public DateTime UpdatedAtUtc { get; set; }
}
