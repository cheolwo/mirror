using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace 살뜰.도메인.창고;

[Table("생활권물류거점")]
public sealed class 생활권물류거점
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [MaxLength(100)]
    public string StableId { get; set; } = string.Empty;

    [MaxLength(100)]
    public string 신청가원장Id { get; set; } = string.Empty;

    [MaxLength(450)]
    public string 신청자UserId { get; set; } = string.Empty;

    [MaxLength(450)]
    public string 관리담당자UserId { get; set; } = string.Empty;

    [MaxLength(160)]
    public string 공간StableId { get; set; } = string.Empty;

    [MaxLength(80)]
    public string 공간유형Code { get; set; } = 생활권물류거점공간유형Codes.독립비주거공간;

    [MaxLength(120)]
    public string 생활권Key { get; set; } = string.Empty;

    [MaxLength(160)]
    public string 대략위치Label { get; set; } = string.Empty;

    [MaxLength(200)]
    public string 정확위치보호참조 { get; set; } = string.Empty;

    public bool 소유자동의 { get; set; }
    public DateTime? 소유자동의시각Utc { get; set; }
    public DateTime? 소유자동의철회시각Utc { get; set; }

    public bool 관리주체동의 { get; set; }
    public DateTime? 관리주체동의시각Utc { get; set; }
    public DateTime? 관리주체동의철회시각Utc { get; set; }

    public bool 플랫폼승인 { get; set; }
    public DateTime? 플랫폼승인시각Utc { get; set; }
    public DateTime? 현장확인시각Utc { get; set; }

    public bool 단기보관가능 { get; set; } = true;
    public bool 기사인계가능 { get; set; } = true;
    public bool 주문자수령가능 { get; set; } = true;
    public bool 상온밀봉품만허용 { get; set; } = true;

    public int 최대동시보관건수 { get; set; }
    public int 현재예약건수 { get; set; }

    [Column(TypeName = "decimal(12,3)")]
    public decimal 최대총중량Kg { get; set; }

    public int 최대보관시간분 { get; set; }

    [MaxLength(160)]
    public string 입고가능시간창 { get; set; } = string.Empty;

    [MaxLength(160)]
    public string 수령가능시간창 { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal 완료건당고정보상 { get; set; }

    [MaxLength(3)]
    public string 보상통화Code { get; set; } = "KRW";

    [MaxLength(30)]
    public string 상태Code { get; set; } = 생활권물류거점상태Codes.Candidate;

    [MaxLength(500)]
    public string 상태사유 { get; set; } = string.Empty;

    public long? 연결창고Id { get; set; }
    public bool 실운영허용 { get; set; }
    public long Revision { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime StatusChangedAtUtc { get; set; }
}

[Table("생활권물류거점용량예약")]
public sealed class 생활권물류거점용량예약
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid 거점Id { get; set; }

    [MaxLength(160)]
    public string 업무StableId { get; set; } = string.Empty;

    [MaxLength(100)]
    public string 멱등성Key { get; set; } = string.Empty;

    [Column(TypeName = "decimal(12,3)")]
    public decimal 중량Kg { get; set; }

    public int 보관시간분 { get; set; }

    [MaxLength(30)]
    public string 상태Code { get; set; } = 생활권물류거점예약상태Codes.Reserved;

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

[Table("생활권물류거점보상기록")]
public sealed class 생활권물류거점보상기록
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid 거점Id { get; set; }

    [MaxLength(160)]
    public string 업무StableId { get; set; } = string.Empty;

    [MaxLength(100)]
    public string 멱등성Key { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal 금액 { get; set; }

    [MaxLength(3)]
    public string 통화Code { get; set; } = "KRW";

    public bool 실제지급대상 { get; set; }

    [MaxLength(30)]
    public string 실행모드Code { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }
}

public static class 생활권물류거점공간유형Codes
{
    public const string 독립비주거공간 = "IndependentNonResidentialSpace";
}

public static class 생활권물류거점상태Codes
{
    public const string Candidate = "Candidate";
    public const string UnderReview = "UnderReview";
    public const string Pilot = "Pilot";
    public const string Active = "Active";
    public const string Paused = "Paused";
    public const string Closed = "Closed";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Candidate, UnderReview, Pilot, Active, Paused, Closed
    };
}

public static class 생활권물류거점예약상태Codes
{
    public const string Reserved = "Reserved";
    public const string Completed = "Completed";
    public const string Released = "Released";
}

public static class 생활권물류거점Policy
{
    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> AllowedTransitions =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
        {
            [생활권물류거점상태Codes.Candidate] = Set(생활권물류거점상태Codes.UnderReview, 생활권물류거점상태Codes.Closed),
            [생활권물류거점상태Codes.UnderReview] = Set(생활권물류거점상태Codes.Candidate, 생활권물류거점상태Codes.Pilot, 생활권물류거점상태Codes.Closed),
            [생활권물류거점상태Codes.Pilot] = Set(생활권물류거점상태Codes.Active, 생활권물류거점상태Codes.Paused, 생활권물류거점상태Codes.UnderReview),
            [생활권물류거점상태Codes.Active] = Set(생활권물류거점상태Codes.Paused),
            [생활권물류거점상태Codes.Paused] = Set(생활권물류거점상태Codes.Pilot, 생활권물류거점상태Codes.Active, 생활권물류거점상태Codes.Closed),
            [생활권물류거점상태Codes.Closed] = Set(생활권물류거점상태Codes.Candidate)
        };

    public static bool CanTransition(string current, string target)
        => AllowedTransitions.TryGetValue(current, out var targets) && targets.Contains(target);

    public static string? GetReadinessError(생활권물류거점 hub)
    {
        if (!string.Equals(hub.공간유형Code, 생활권물류거점공간유형Codes.독립비주거공간, StringComparison.Ordinal))
            return "첫 Pilot은 독립된 비주거 공간만 허용합니다.";
        if (string.IsNullOrWhiteSpace(hub.신청가원장Id)) return "Mongo 지도 신청 가원장 연결이 필요합니다.";
        if (!hub.소유자동의 || hub.소유자동의철회시각Utc.HasValue) return "유효한 소유자 동의가 필요합니다.";
        if (!hub.관리주체동의 || hub.관리주체동의철회시각Utc.HasValue) return "유효한 관리주체 동의가 필요합니다.";
        if (!hub.플랫폼승인) return "플랫폼 승인이 필요합니다.";
        if (!hub.현장확인시각Utc.HasValue) return "현장 확인이 필요합니다.";
        if (!hub.상온밀봉품만허용) return "첫 Pilot은 상온 밀봉품 전용이어야 합니다.";
        if (!hub.단기보관가능 || (!hub.기사인계가능 && !hub.주문자수령가능)) return "단기 보관과 하나 이상의 인계 기능이 필요합니다.";
        if (hub.최대동시보관건수 <= 0 || hub.최대총중량Kg <= 0 || hub.최대보관시간분 <= 0) return "운영 용량과 최대 보관시간이 필요합니다.";
        if (string.IsNullOrWhiteSpace(hub.정확위치보호참조)) return "정확한 위치의 보호된 참조가 필요합니다.";
        return null;
    }

    public static bool CanReserve(생활권물류거점 hub, decimal weightKg, int storageMinutes)
        => hub.상태Code is 생활권물류거점상태Codes.Pilot or 생활권물류거점상태Codes.Active
           && 생활권물류거점Policy.GetReadinessError(hub) is null
           && weightKg > 0
           && storageMinutes > 0
           && hub.현재예약건수 < hub.최대동시보관건수
           && weightKg <= hub.최대총중량Kg
           && storageMinutes <= hub.최대보관시간분;

    private static IReadOnlySet<string> Set(params string[] values)
        => new HashSet<string>(values, StringComparer.Ordinal);
}
