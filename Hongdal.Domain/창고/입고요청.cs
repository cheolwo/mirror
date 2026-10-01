using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace 홍달.도메인.창고;

[Table("입고요청")]
public class 입고요청
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("창고_id")]
    public long 창고Id { get; set; }

    [Column("입고흐름유형")]
    [MaxLength(50)]
    public string 입고흐름유형 { get; set; } = "ContractBased";

    [Column("입고생성경로")]
    [MaxLength(100)]
    public string 입고생성경로 { get; set; } = string.Empty;

    [Column("계약선행여부")]
    public bool 계약선행여부 { get; set; } = true;

    [Column("자동생성여부")]
    public bool 자동생성여부 { get; set; }

    [Column("주문_id")]
    public long? 주문Id { get; set; }

    [Column("주문참조번호")]
    [MaxLength(100)]
    public string 주문참조번호 { get; set; } = string.Empty;

    [Column("주문자_user_id")]
    [MaxLength(450)]
    public string 주문자UserId { get; set; } = string.Empty;

    [Column("판매자_user_id")]
    [MaxLength(450)]
    public string 판매자UserId { get; set; } = string.Empty;

    [Column("출고예정_id")]
    public long? 출고예정Id { get; set; }

    [Column("운송의뢰_id")]
    [MaxLength(100)]
    public string? 운송의뢰Id { get; set; }

    [Column("공급처명")]
    [MaxLength(200)]
    public string 공급처명 { get; set; } = string.Empty;

    [Column("공급처코드")]
    [MaxLength(100)]
    public string 공급처코드 { get; set; } = string.Empty;

    [Column("원주문참조번호")]
    [MaxLength(100)]
    public string 원주문참조번호 { get; set; } = string.Empty;

    [Column("상태")]
    [MaxLength(50)]
    public string 상태 { get; set; } = "입고예정";

    [Column("예정도착일")]
    public DateTime? 예정도착일 { get; set; }

    [Column("비고")]
    [MaxLength(1000)]
    public string 비고 { get; set; } = string.Empty;

    [Column("계약번호")]
    [MaxLength(100)]
    public string 계약번호 { get; set; } = string.Empty;

    [Column("계약유형")]
    [MaxLength(50)]
    public string 계약유형 { get; set; } = string.Empty;

    [Column("계약상대방명")]
    [MaxLength(200)]
    public string 계약상대방명 { get; set; } = string.Empty;

    [Column("정산방식")]
    [MaxLength(100)]
    public string 정산방식 { get; set; } = string.Empty;

    [Column("판매수수료율", TypeName = "decimal(9,2)")]
    public decimal 판매수수료율 { get; set; }

    [Column("보관료일단가", TypeName = "decimal(18,2)")]
    public decimal 보관료일단가 { get; set; }

    [Column("통관필요여부")]
    public bool 통관필요여부 { get; set; }

    [Column("계약시작일")]
    public DateTime? 계약시작일 { get; set; }

    [Column("계약종료일")]
    public DateTime? 계약종료일 { get; set; }

    [Column("계약메모")]
    [MaxLength(1000)]
    public string 계약메모 { get; set; } = string.Empty;

    [Column("입고완료일시")]
    public DateTime? 입고완료일시 { get; set; }

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
}
