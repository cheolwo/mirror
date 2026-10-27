using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace 살뜰.도메인.판매;

[Table("판매채널계정")]
public class 판매채널계정
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("user_id")]
    [MaxLength(450)]
    public string UserId { get; set; } = string.Empty;

    [Column("채널종류")]
    [MaxLength(50)]
    public string 채널종류 { get; set; } = string.Empty;

    [Column("상점명")]
    [MaxLength(200)]
    public string 상점명 { get; set; } = string.Empty;

    [Column("연결상태")]
    [MaxLength(50)]
    public string 연결상태 { get; set; } = "준비";

    [Column("토큰암호화저장값")]
    [MaxLength(2000)]
    public string 토큰암호화저장값 { get; set; } = string.Empty;

    [Column("마지막동기화일시")]
    public DateTime? 마지막동기화일시 { get; set; }

    [Column("동기화진단_json")]
    public string? 동기화진단Json { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
