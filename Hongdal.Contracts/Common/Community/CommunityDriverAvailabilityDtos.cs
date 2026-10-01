namespace Hongdal.Contracts.Common.Community;

public static class CommunityDriverAvailabilityStatusCodes
{
    public const string Active = "active";
    public const string Closed = "closed";
}

public static class CommunityDriverInquiryStatusCodes
{
    public const string Pending = "pending";
    public const string Accepted = "accepted";
    public const string Declined = "declined";
    public const string DriverUnavailable = "driver-unavailable";
}

public static class CommunityDriverInquiryDecisionCodes
{
    public const string Accept = "accept";
    public const string Decline = "decline";
}

public static class CommunityDriverLocationDisclosureLevelCodes
{
    public const string SidoSigungu = "sido-sigungu";
}

public static class CommunityDriverLocationConsentPolicy
{
    public const string CurrentVersion = "2026-07-15-v1";
}

public sealed class CommunityDriverAvailabilityListResponse
{
    public List<CommunityDriverAvailabilityPostResponse> Items { get; set; } = [];
    public DateTimeOffset GeneratedAtUtc { get; set; }
}

public sealed class CommunityDriverAvailabilityPostResponse
{
    public Guid PostId { get; set; }
    public string MaskedDriverDisplayName { get; set; } = string.Empty;
    public string VehicleSummary { get; set; } = string.Empty;
    public string OperatingAreaLabel { get; set; } = string.Empty;
    public string? CurrentDistrictLabel { get; set; }
    public string LocationDisclosureLevelCode { get; set; } = CommunityDriverLocationDisclosureLevelCodes.SidoSigungu;
    public bool DistrictLocationConsentGranted { get; set; }
    public string? DistrictLocationConsentPolicyVersion { get; set; }
    public DateTimeOffset? DistrictLocationConsentRecordedAtUtc { get; set; }
    public DateTimeOffset? DistrictLocationUpdatedAtUtc { get; set; }
    public string StatusCode { get; set; } = CommunityDriverAvailabilityStatusCodes.Active;
    public DateTimeOffset StartedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public bool CanReceiveDirectInquiries { get; set; }
    public bool ContactDetailsDisclosed { get; set; }
    public bool ExactLocationDisclosed { get; set; }
}

public sealed class CommunityDriverInquiryCreateRequest
{
    public string CargoSummary { get; set; } = string.Empty;
    public string QuantitySummary { get; set; } = string.Empty;
    public string PickupAreaLabel { get; set; } = string.Empty;
    public string DropoffAreaLabel { get; set; } = string.Empty;
    public string RequestedPickupWindow { get; set; } = string.Empty;
    public string PublicMessage { get; set; } = string.Empty;
    public Guid? SourceGroupPurchaseCampaignId { get; set; }
    public string? SourceContextLabel { get; set; }
}

public sealed class CommunityDriverInquiryDecisionRequest
{
    public string DecisionCode { get; set; } = string.Empty;
    public string? DriverPublicMessage { get; set; }
}

public sealed class CommunityDriverInquiryResponse
{
    public Guid InquiryId { get; set; }
    public Guid AvailabilityPostId { get; set; }
    public string MaskedDriverDisplayName { get; set; } = string.Empty;
    public string RequesterRoleLabel { get; set; } = string.Empty;
    public string CargoSummary { get; set; } = string.Empty;
    public string QuantitySummary { get; set; } = string.Empty;
    public string PickupAreaLabel { get; set; } = string.Empty;
    public string DropoffAreaLabel { get; set; } = string.Empty;
    public string RequestedPickupWindow { get; set; } = string.Empty;
    public string PublicMessage { get; set; } = string.Empty;
    public Guid? SourceGroupPurchaseCampaignId { get; set; }
    public string? SourceContextLabel { get; set; }
    public string StatusCode { get; set; } = CommunityDriverInquiryStatusCodes.Pending;
    public string? DriverPublicMessage { get; set; }
    public string NextStepMessage { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public bool ContactDetailsDisclosed { get; set; }
}
