using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace 살뜰.도메인.음식;

[Table("음식운영정책")]
public sealed class 음식운영정책
{
    [Key]
    [Column("id")]
    public long Id { get; set; } = 1;

    [Column("기본저평점게시일수")]
    public int 기본저평점게시일수 { get; set; } = 3;

    [Column("기본요금", TypeName = "decimal(18,2)")]
    public decimal 기본요금 { get; set; } = 3000m;

    [Column("포함거리_m")]
    public int 포함거리Meters { get; set; } = 1000;

    [Column("거리단위_m")]
    public int 거리단위Meters { get; set; } = 100;

    [Column("거리단위요금", TypeName = "decimal(18,2)")]
    public decimal 거리단위요금 { get; set; } = 120m;

    [Column("최소요금", TypeName = "decimal(18,2)")]
    public decimal 최소요금 { get; set; } = 3000m;

    [Column("기사기본지급액", TypeName = "decimal(18,2)")]
    public decimal 기사기본지급액 { get; set; } = 2500m;

    // null은 기존 기본 지급액의 픽업/전달 배분이 미정임을 뜻한다. 총 기본 지급액은 바꾸지 않는다.
    [Column("기사픽업지급액", TypeName = "decimal(18,2)")]
    public decimal? 기사픽업지급액 { get; set; }

    [Column("기사거리단위지급액", TypeName = "decimal(18,2)")]
    public decimal 기사거리단위지급액 { get; set; } = 90m;

    [Column("기사최소지급액", TypeName = "decimal(18,2)")]
    public decimal 기사최소지급액 { get; set; } = 2500m;

    [Column("기사기상할증활성화여부")]
    public bool 기사기상할증활성화여부 { get; set; } = true;

    [Column("기사기상할증액", TypeName = "decimal(18,2)")]
    public decimal 기사기상할증액 { get; set; } = 1000m;

    [Column("기사기상할증정책판본")]
    [MaxLength(100)]
    public string 기사기상할증정책판본 { get; set; } = "food-weather-surcharge.r1";

    [Column("기사한시수요할증액", TypeName = "decimal(18,2)")]
    public decimal 기사한시수요할증액 { get; set; }

    [Column("기사한시수요할증시작일시_utc")]
    public DateTime? 기사한시수요할증시작일시Utc { get; set; }

    [Column("기사한시수요할증종료일시_utc")]
    public DateTime? 기사한시수요할증종료일시Utc { get; set; }

    [Column("기사한시수요할증사유_code")]
    [MaxLength(80)]
    public string 기사한시수요할증사유Code { get; set; } = string.Empty;

    [Column("기사한시수요할증범위_code")]
    [MaxLength(80)]
    public string 기사한시수요할증범위Code { get; set; } = "AllFoodDelivery";

    [Column("기사한시수요할증_revision")]
    public long 기사한시수요할증Revision { get; set; }

    [Column("기사한시수요할증_client_request_id")]
    [MaxLength(36)]
    public string 기사한시수요할증ClientRequestId { get; set; } = string.Empty;

    [Column("수정자_user_id")]
    [MaxLength(450)]
    public string 수정자UserId { get; set; } = string.Empty;

    [Column("updated_at_utc")]
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
