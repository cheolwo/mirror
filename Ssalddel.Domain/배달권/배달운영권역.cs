using Ssalddel.Contracts.Common.DeliveryZones;
using Ssalddel.Contracts.Common.Metadata;

namespace 살뜰.도메인.배달권;

[SsalddelCodeMetadata(
    SsalddelCodeFeatureKeys.PlatformDeliveryZoneLedger,
    SsalddelCodeLayer.Domain,
    "관리자가 공식 행정동 셀을 묶어 만드는 배달운영권역의 Draft 원장을 보존한다.",
    Effects = SsalddelCodeEffect.None,
    FlowOrder = 31,
    Boundary = "기존 플랫폼배달권·food-cell 키를 대체하지 않으며 이번 판본은 주문·배차 효과를 만들지 않는다.")]
public sealed class 배달운영권역
{
    public long Id { get; set; }

    public string 권역고유식별자 { get; set; } = string.Empty;

    public string 표시명 { get; set; } = string.Empty;

    public string SourceScopeStableId { get; set; } = string.Empty;

    public string 상태Code { get; set; } = 배달운영권역상태Codes.Draft;

    public long Revision { get; set; } = 1;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<배달운영권역행정동Membership> 행정동Memberships { get; set; } =
        new List<배달운영권역행정동Membership>();
}

public sealed class 배달운영권역행정동Membership
{
    public long Id { get; set; }

    public long 배달운영권역Id { get; set; }

    public 배달운영권역 배달운영권역 { get; set; } = null!;

    public string 행정동고유식별자 { get; set; } = string.Empty;

    public string 행정동표시명 { get; set; } = string.Empty;

    public string 법정동목록Json { get; set; } = "[]";

    public string 상태Code { get; set; } = 배달운영권역행정동상태Codes.Included;

    /// <summary>
    /// Included 상태에서만 행정동 고유 식별자를 보존한다. nullable unique index로 같은 행정동의
    /// 현행 권역 중복을 DB에서도 차단하고 Excluded 이력은 남긴다.
    /// </summary>
    public string? 현행행정동유일성Key { get; set; }

    public string 관할SourceId { get; set; } = string.Empty;

    public string 관할DataRevision { get; set; } = string.Empty;

    public DateTime IncludedAtUtc { get; set; }

    public DateTime? ExcludedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}

public sealed class 배달운영권역CommandReceipt
{
    public long Id { get; set; }

    public string ClientRequestId { get; set; } = string.Empty;

    public string OperationCode { get; set; } = string.Empty;

    public string RequestHashSha256 { get; set; } = string.Empty;

    public string ActorUserStableId { get; set; } = string.Empty;

    public string 배달운영권역고유식별자 { get; set; } = string.Empty;

    public long ResultRevision { get; set; }

    public string ResultJson { get; set; } = "{}";

    public DateTime CreatedAtUtc { get; set; }
}

public sealed class 배달운영권역변경Outbox
{
    public long Id { get; set; }

    public string EventStableId { get; set; } = string.Empty;

    public string AggregateStableId { get; set; } = string.Empty;

    public long AggregateRevision { get; set; }

    public string EventTypeCode { get; set; } = string.Empty;

    public string ActorUserStableId { get; set; } = string.Empty;

    public string PayloadJson { get; set; } = "{}";

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? PublishedAtUtc { get; set; }
}
