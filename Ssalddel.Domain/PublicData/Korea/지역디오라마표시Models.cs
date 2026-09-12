namespace Ssalddel.Domain.PublicData.Korea;

public static class 지역사업장표시ClaimStatusCodes
{
    public const string PendingReview = "PendingReview";
    public const string Verified = "Verified";
    public const string Rejected = "Rejected";
    public const string Revoked = "Revoked";
}

public static class 지역디오라마후원CampaignStatusCodes
{
    public const string PendingReview = "PendingReview";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Suspended = "Suspended";
    public const string Revoked = "Revoked";
}

/// <summary>공개 인허가 사업장과 신청자의 표시 권한 주장을 분리해 검토하는 RDB 원장입니다.</summary>
public sealed class 지역사업장표시Claim
{
    public Guid Id { get; set; }
    public string ClaimStableId { get; set; } = string.Empty;
    public Guid PublicBusinessRecordId { get; set; }
    public 공개인허가사업장Record PublicBusinessRecord { get; set; } = null!;
    public string AdministrativeRegionStableId { get; set; } = string.Empty;
    public string SemanticPlaceStableId { get; set; } = string.Empty;
    public string RequestedDisplayName { get; set; } = string.Empty;
    public string CategoryCode { get; set; } = string.Empty;
    public string RequestedByUserStableId { get; set; } = string.Empty;
    public string StatusCode { get; set; } = 지역사업장표시ClaimStatusCodes.PendingReview;
    public long Revision { get; set; } = 1;
    public DateTimeOffset RequestedAtUtc { get; set; }
    public DateTimeOffset? ReviewedAtUtc { get; set; }
    public string? ReviewedByUserStableId { get; set; }
    public string? ReviewReasonCode { get; set; }
}

/// <summary>표시 Claim 검증 뒤에만 활성 후보가 되는 후원 표시 원장입니다. 결제나 순위를 소유하지 않습니다.</summary>
public sealed class 지역디오라마후원Campaign
{
    public Guid Id { get; set; }
    public string CampaignStableId { get; set; } = string.Empty;
    public Guid BusinessDisplayClaimId { get; set; }
    public 지역사업장표시Claim BusinessDisplayClaim { get; set; } = null!;
    public string BadgeText { get; set; } = string.Empty;
    public string DetailCardText { get; set; } = string.Empty;
    public DateTimeOffset StartsAtUtc { get; set; }
    public DateTimeOffset EndsAtUtc { get; set; }
    public string StatusCode { get; set; } = 지역디오라마후원CampaignStatusCodes.PendingReview;
    public long Revision { get; set; } = 1;
    public DateTimeOffset RequestedAtUtc { get; set; }
    public DateTimeOffset? ReviewedAtUtc { get; set; }
    public string RequestedByUserStableId { get; set; } = string.Empty;
    public string? ReviewedByUserStableId { get; set; }
    public string? ReviewReasonCode { get; set; }
}
