using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace 살뜰.도메인.음식;

[Table("음식배달경험공유설정")]
public sealed class 음식배달경험공유설정
{
    [Key, MaxLength(450)] public string 기사Id { get; set; } = string.Empty;
    public bool 현재관계자공유 { get; set; }
    public long Revision { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

/// <summary>주소 검색 원천을 대조한 주문 시점의 공개 지역 식별자. 상세주소를 복제하지 않습니다.</summary>
[Table("음식배달전달지역Snapshot")]
public sealed class 음식배달전달지역Snapshot
{
    [Key, MaxLength(100)] public string 주문번호 { get; set; } = string.Empty;
    [MaxLength(80)] public string? 도로Id { get; set; }
    [MaxLength(200)] public string? 도로표시명 { get; set; }
    [MaxLength(80)] public string? 단지Id { get; set; }
    [MaxLength(200)] public string? 단지표시명 { get; set; }
    [MaxLength(80)] public string 확인상태Code { get; set; } = "Unknown";
    [MaxLength(80)] public string 원천Code { get; set; } = "JusoAndApartmentPublicData";
    public DateTime VerifiedAtUtc { get; set; }
}

/// <summary>기사+주문+완료종류+실행모드당 한 건. 생성은 업무 완료와 같은 트랜잭션입니다.</summary>
[Table("음식배달경험완료Outbox")]
public sealed class 음식배달경험완료Outbox
{
    [Key, MaxLength(64)] public string Id { get; set; } = string.Empty;
    [MaxLength(450)] public string 기사Id { get; set; } = string.Empty;
    [MaxLength(100)] public string 주문번호 { get; set; } = string.Empty;
    [MaxLength(220)] public string 원천시도Id { get; set; } = string.Empty;
    [MaxLength(32)] public string 완료종류Code { get; set; } = string.Empty;
    [MaxLength(32)] public string 실행모드Code { get; set; } = string.Empty;
    [MaxLength(80)] public string? 지점Id { get; set; }
    [MaxLength(200)] public string? 지점표시명 { get; set; }
    [MaxLength(80)] public string? 도로Id { get; set; }
    [MaxLength(200)] public string? 도로표시명 { get; set; }
    [MaxLength(80)] public string? 단지Id { get; set; }
    [MaxLength(200)] public string? 단지표시명 { get; set; }
    public DateTime CompletedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ProjectedAtUtc { get; set; }
}

/// <summary>완료 Outbox를 멱등 투영한 비공개 집계 근거. 고객 ID·상세주소·좌표는 없습니다.</summary>
[Table("음식배달경험Fact")]
public sealed class 음식배달경험Fact
{
    [Key, MaxLength(64)] public string Id { get; set; } = string.Empty;
    [MaxLength(64)] public string 완료OutboxId { get; set; } = string.Empty;
    [MaxLength(450)] public string 기사Id { get; set; } = string.Empty;
    [MaxLength(32)] public string 완료종류Code { get; set; } = string.Empty;
    [MaxLength(32)] public string 장소종류Code { get; set; } = string.Empty;
    [MaxLength(80)] public string 장소Id { get; set; } = string.Empty;
    [MaxLength(200)] public string 장소표시명 { get; set; } = string.Empty;
    [MaxLength(32)] public string 실행모드Code { get; set; } = string.Empty;
    public DateTime ProjectedAtUtc { get; set; }
}
