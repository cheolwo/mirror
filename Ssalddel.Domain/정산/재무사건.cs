using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace 살뜰.도메인.정산;

[Table("재무사건")]
public sealed class 재무사건
{
    [Key]
    public long Id { get; set; }

    [MaxLength(200)]
    public string StableId { get; set; } = string.Empty;

    [MaxLength(120)]
    public string 원본Event유형 { get; set; } = string.Empty;

    [MaxLength(200)]
    public string 원본StableId { get; set; } = string.Empty;

    public long 원본Revision { get; set; }

    [MaxLength(160)]
    public string 재무영향ProfileStableId { get; set; } = string.Empty;

    [MaxLength(100)]
    public string 재무의미Code { get; set; } = string.Empty;

    [MaxLength(64)]
    public string 재무영향유형Code { get; set; } = string.Empty;

    public decimal 금액 { get; set; }

    [MaxLength(3)]
    public string 통화Code { get; set; } = "KRW";

    public DateTime 업무발생일시Utc { get; set; }

    public DateTime? 현금이동일시Utc { get; set; }

    public DateTime? 지급기일Utc { get; set; }

    [MaxLength(80)]
    public string 상대역할Code { get; set; } = string.Empty;

    [MaxLength(64)]
    public string 증빙Hash { get; set; } = string.Empty;

    [MaxLength(64)]
    public string 투영상태Code { get; set; } = 재무사건투영상태Codes.완료;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public ICollection<관리계정전기> 관리계정전기목록 { get; set; } = new List<관리계정전기>();

    public ICollection<재무사건증빙> 증빙목록 { get; set; } = new List<재무사건증빙>();
}

public static class 재무사건투영상태Codes
{
    public const string 완료 = "Projected";
    public const string 검토필요 = "ReviewRequired";
}

[Table("관리계정전기")]
public sealed class 관리계정전기
{
    [Key]
    public long Id { get; set; }

    public long 재무사건Id { get; set; }

    public int LineNumber { get; set; }

    [MaxLength(160)]
    public string 관리계정StableId { get; set; } = string.Empty;

    [MaxLength(32)]
    public string 전기방향Code { get; set; } = string.Empty;

    public decimal 금액 { get; set; }

    [MaxLength(3)]
    public string 통화Code { get; set; } = "KRW";

    [MaxLength(100)]
    public string 매핑Revision { get; set; } = string.Empty;

    [MaxLength(200)]
    public string 역분개대상StableId { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public 재무사건 재무사건 { get; set; } = null!;
}

[Table("재무사건증빙")]
public sealed class 재무사건증빙
{
    [Key]
    public long Id { get; set; }

    public long 재무사건Id { get; set; }

    [MaxLength(80)]
    public string 증빙유형Code { get; set; } = string.Empty;

    [MaxLength(200)]
    public string 원본참조 { get; set; } = string.Empty;

    public long 원본Revision { get; set; }

    [MaxLength(64)]
    public string 내용Hash { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public 재무사건 재무사건 { get; set; } = null!;
}

[Table("재무대사예외")]
public sealed class 재무대사예외
{
    [Key]
    public long Id { get; set; }

    [MaxLength(200)]
    public string StableId { get; set; } = string.Empty;

    [MaxLength(200)]
    public string 재무사건StableId { get; set; } = string.Empty;

    [MaxLength(80)]
    public string 예외Code { get; set; } = string.Empty;

    [MaxLength(64)]
    public string 상태Code { get; set; } = 재무대사예외상태Codes.확인필요;

    [MaxLength(200)]
    public string 요약Code { get; set; } = string.Empty;

    public DateTime 감지일시Utc { get; set; }

    public DateTime? 해결일시Utc { get; set; }
}

public static class 재무대사예외상태Codes
{
    public const string 확인필요 = "ReviewRequired";
    public const string 해결 = "Resolved";
}
