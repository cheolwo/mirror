using System.Text.Json.Serialization;

namespace Ssalddel.Contracts.Common.Fulfillment;

public static class FulfillmentSourceKinds
{
    public const string NeighborhoodDelivery = "NeighborhoodDelivery";
    public const string FoodOrder = "FoodOrder";
    public static bool IsKnown(string value) => value is NeighborhoodDelivery or FoodOrder;
}

public sealed record FulfillmentSourceKey(string SourceKind, string SourceId);

/// <summary>플랫폼이 이 조건 안에서 처리처를 정하도록 본인이 확인하는 동의입니다.</summary>
public sealed class AutoFulfillmentConsent
{
    public Guid ClientRequestId { get; set; }
    public long ExpectedRevision { get; set; }
    public decimal MaxUserChargeKrw { get; set; }
    public DateTime? DesiredCompletionUtc { get; set; }
    public IReadOnlyList<string> AllowedProviderIds { get; set; } = [];
    public bool ProviderInformationSharingAgreed { get; set; }
    public bool AutomaticSubmissionAgreed { get; set; }
    public bool DirectPaymentAgreed { get; set; }
    public bool Revoked { get; set; }
    public IReadOnlyList<string> InformationFields { get; set; } = AutoFulfillmentNotice.InformationFields;
    public decimal? ConfirmedRestaurantSupportKrw { get; set; }
    public long? ConfirmedRestaurantSupportRevision { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DeliveryCostSharingPolicy? ConfirmedCostSharingPolicy { get; set; }
    public string NoticeVersion { get; set; } = AutoFulfillmentNotice.Version;
}

public static class AutoFulfillmentNotice
{
    public const string Version = "fulfillment-routing-v1-2026-10-07";
    public const string Simulation = "Simulation 검증입니다. 실제 업체 접수·결제·배송·지급 완료가 아닙니다.";
    public static IReadOnlyList<string> InformationFields { get; } = ["PickupAddress", "DropoffAddress", "ContactName", "PhoneNumber", "DeliveryInstructions"];
    public static IReadOnlyList<AutoFulfillmentProviderDescriptor> Providers { get; } =
        [new("internal", "자체 배차", true), new("mock-quick", "모의 퀵 배송", true), new("mock-local", "모의 생활 배송", true)];
}

public sealed record AutoFulfillmentProviderDescriptor(string ProviderId, string ProviderName, bool IsSimulation);

public static class AutoFulfillmentStates
{
    public const string AwaitingSource = "AwaitingSource";
    public const string AwaitingRouting = "AwaitingRouting";
    public const string NeedsReview = "NeedsReview";
    public const string Submitting = "Submitting";
    public const string ResultUnknown = "ResultUnknown";
    public const string Accepted = "Accepted";
    public const string DriverAssigned = "DriverAssigned";
    public const string InTransit = "InTransit";
    public const string Completed = "Completed";
    public const string CancelPending = "CancelPending";
    public const string Cancelled = "Cancelled";
    public const string Failed = "Failed";
}

public sealed class AutoFulfillmentSnapshot
{
    public string SourceKind { get; set; } = string.Empty;
    public string SourceId { get; set; } = string.Empty;
    public bool SourceClosed { get; set; }
    public bool ExecutionPaused { get; set; }
    public string StateCode { get; set; } = AutoFulfillmentStates.AwaitingRouting;
    public long Revision { get; set; }
    public long ConsentRevision { get; set; }
    public string? AttemptId { get; set; }
    public string? ProviderId { get; set; }
    public string? ProviderName { get; set; }
    public string? ExternalRequestId { get; set; }
    public string? Leg { get; set; }
    public decimal? MaxUserChargeKrw { get; set; }
    public decimal? TotalCostKrw { get; set; }
    public decimal? UserChargeKrw { get; set; }
    public decimal RestaurantContributionKrw { get; set; }
    public decimal FrozenRestaurantSupportKrw { get; set; }
    public long? RestaurantSupportRevision { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DeliveryCostSharingPolicy? CostSharingPolicy { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? CostSharingAllowed { get; set; }
    public DateTime? EstimatedCompletionUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public string SelectionReason { get; set; } = string.Empty;
    public string? ReviewCode { get; set; }
    public bool CanCancel { get; set; }
    public bool CanRenewConsent { get; set; }
    /// <summary>새 접수 조건을 변경할 수 없는 사유입니다. 철회나 원 주문 취소의 판정과 구분합니다.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ConsentRenewalLockedReasonCode { get; set; }
    public bool IsSimulation { get; set; } = true;
    public bool IsOperationalSettlementEligible { get; set; }
    public string Notice { get; set; } = AutoFulfillmentNotice.Simulation;
}

public sealed class AutoFulfillmentActionRequest
{
    public Guid ClientRequestId { get; set; }
    public long ExpectedRevision { get; set; }
}

public sealed class RestaurantFulfillmentSupportRequest
{
    public Guid ClientRequestId { get; set; }
    public long ExpectedRevision { get; set; }
    public decimal FixedContributionKrw { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DeliveryCostSharingPolicy? CostSharingPolicy { get; set; }
    public bool Enabled { get; set; }
    public bool Agreed { get; set; }
}

public sealed class RestaurantFulfillmentSupportResponse
{
    public long RestaurantId { get; set; }
    public long Revision { get; set; }
    public decimal FixedContributionKrw { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DeliveryCostSharingPolicy? CostSharingPolicy { get; set; }
    public bool Enabled { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public static class FulfillmentEvidenceKinds
{
    public const string DriverAssigned = "DriverAssigned";
    public const string AssignmentReleased = "AssignmentReleased";
    public const string PickedUp = "PickedUp";
    public const string Delivered = "Delivered";
    public const string Rejected = "Rejected";
    public const string Cancelled = "Cancelled";
}

/// <summary>시간 경과가 아닌 명시적인 모의 사건으로만 진행합니다.</summary>
public sealed class FulfillmentMockEvidenceRequest
{
    public Guid ClientRequestId { get; set; }
    public long ExpectedRevision { get; set; }
    public string EvidenceKind { get; set; } = string.Empty;
    public string? ExternalDriverReference { get; set; }
    public decimal? ConfirmedIncurredCostKrw { get; set; }
}
