using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace 살뜰.도메인.음식;

[Table("음식주문")]
public class 음식주문
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("주문번호")]
    [MaxLength(100)]
    public string 주문번호 { get; set; } = string.Empty;

    [Column("클라이언트요청_id")]
    public Guid? 클라이언트요청Id { get; set; }

    [Column("음식점_id")]
    public long 음식점Id { get; set; }

    [Column("음식점명")]
    [MaxLength(200)]
    public string 음식점명 { get; set; } = string.Empty;

    [Column("음식점주소")]
    [MaxLength(500)]
    public string 음식점주소 { get; set; } = string.Empty;

    [Column("음식점상세주소")]
    [MaxLength(300)]
    public string 음식점상세주소 { get; set; } = string.Empty;

    [Column("음식점위도", TypeName = "decimal(18,10)")]
    public decimal? 음식점위도 { get; set; }

    [Column("음식점경도", TypeName = "decimal(18,10)")]
    public decimal? 음식점경도 { get; set; }

    [Column("주문자_user_id")]
    [MaxLength(450)]
    public string 주문자UserId { get; set; } = string.Empty;

    [Column("수령인명")]
    [MaxLength(100)]
    public string 수령인명 { get; set; } = string.Empty;

    [Column("수령인연락처")]
    [MaxLength(50)]
    public string 수령인연락처 { get; set; } = string.Empty;

    [Column("수령지주소")]
    [MaxLength(500)]
    public string 수령지주소 { get; set; } = string.Empty;

    [Column("수령지상세주소")]
    [MaxLength(300)]
    public string 수령지상세주소 { get; set; } = string.Empty;

    [Column("수령요청사항")]
    [MaxLength(1000)]
    public string 수령요청사항 { get; set; } = string.Empty;

    [Column("주문자본인수령여부")]
    public bool 주문자본인수령여부 { get; set; }

    [Column("총주문금액", TypeName = "decimal(18,2)")]
    public decimal 총주문금액 { get; set; }

    [Column("상태")]
    [MaxLength(50)]
    public string 상태 { get; set; } = "주문대기";

    [Column("배차상태")]
    [MaxLength(50)]
    public string 배차상태 { get; set; } = "미요청";

    [Column("배차대기_id")]
    public long? 배차대기Id { get; set; }

    [Column("결제수단")]
    [MaxLength(50)]
    public string? 결제수단 { get; set; }

    // 과거 승인 사실만 보존한다. 현재 환불 잔액·정산·지급 상태가 아니다.
    [Column("payment_approval_id")]
    [MaxLength(100)]
    public string? 결제승인Id { get; set; }

    [Column("payment_approved_amount", TypeName = "decimal(18,2)")]
    public decimal? 결제승인금액 { get; set; }

    [Column("payment_approved_currency")]
    [MaxLength(3)]
    public string? 결제승인통화 { get; set; }

    [Column("payment_approved_at_utc")]
    public DateTime? 결제승인시각Utc { get; set; }

    [Column("음식점수락시각_utc")]
    public DateTime? 음식점수락시각Utc { get; set; }

    [Column("조리예상완료시각_utc")]
    public DateTime? 조리예상완료시각Utc { get; set; }

    [Column("플랫폼_참고_조리_분")]
    public int? 플랫폼참고조리분 { get; set; }

    [Column("음식점_선택_조리_분")]
    public int? 음식점선택조리분 { get; set; }

    [Column("적용_조리_분")]
    public int? 적용조리분 { get; set; }

    [Column("조리_시간_결정_출처_code")]
    [MaxLength(50)]
    public string? 조리시간결정출처Code { get; set; }

    [Column("배차요청시각_utc")]
    public DateTime? 배차요청시각Utc { get; set; }

    [Column("수락메모")]
    [MaxLength(1000)]
    public string? 수락메모 { get; set; }

    [Column("community_ledger_id")]
    [MaxLength(120)]
    public string? 커뮤니티원장Id { get; set; }

    [Column("community_ledger_template_key")]
    [MaxLength(120)]
    public string? 커뮤니티원장템플릿Key { get; set; }

    [Column("community_ledger_state")]
    [MaxLength(80)]
    public string? 커뮤니티원장상태 { get; set; }

    [Column("community_ledger_synced_at_utc")]
    public DateTime? 커뮤니티원장동기화시각Utc { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<음식주문상품> 상품목록 { get; set; } = [];

    public List<음식주문상태이력> 상태이력 { get; set; } = [];
}
