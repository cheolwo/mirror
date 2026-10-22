using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace 살뜰.도메인.창고;

// 사진을 먼저 접수한 준비 기록이다. 실제 입고·상품·재고 상태는 연결된 전문 원장이 소유한다.
[Table("가입고사진신청")]
public sealed class 가입고사진신청
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("작성자_user_id")]
    [MaxLength(450)]
    public string 작성자UserId { get; set; } = string.Empty;

    [Column("이름")]
    [MaxLength(100)]
    public string 이름 { get; set; } = string.Empty;

    [Column("revision")]
    [ConcurrencyCheck]
    public long Revision { get; set; } = 1;

    [Column("사진_container")]
    [MaxLength(128)]
    public string 사진Container { get; set; } = string.Empty;

    [Column("사진_object_name")]
    [MaxLength(1024)]
    public string 사진ObjectName { get; set; } = string.Empty;

    [Column("사진_sha256")]
    [MaxLength(64)]
    public string 사진Sha256 { get; set; } = string.Empty;

    [Column("최초_요청_해시")]
    [MaxLength(64)]
    public string 최초요청해시 { get; set; } = string.Empty;

    [Column("content_type")]
    [MaxLength(100)]
    public string ContentType { get; set; } = string.Empty;

    [Column("bytes")]
    public long Bytes { get; set; }

    [Column("created_at_utc")]
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    [Column("updated_at_utc")]
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public 입고요청? 입고요청 { get; set; }
}
