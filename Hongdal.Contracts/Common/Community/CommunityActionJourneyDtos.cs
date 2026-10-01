namespace Hongdal.Contracts.Common.Community;

public sealed class CommunityActionJourneyResponse
{
    public long PostId { get; set; }
    public bool IsAvailable { get; set; } = true;
    public bool NonBinding { get; set; } = true;
    public bool AutoStartsWorkflow { get; set; }
    public string CurrentStageCode { get; set; } = CommunityActionJourneyStageCodes.Conversation;
    public string CurrentStageLabel { get; set; } = "이야기 나누는 중";
    public Guid? InterestVoteId { get; set; }
    public string? ProvisionalLedgerId { get; set; }
    public int ParticipantCount { get; set; }
    public int RequiredRoleCount { get; set; }
    public int FilledRequiredRoleCount { get; set; }
    public bool IsReadyForExecutionReview { get; set; }
    public bool HasExecutionLedger { get; set; }
    public CommunityActionJourneySalesSummaryResponse Sales { get; set; } = new();
    public CommunityActionJourneyEconomicsSummaryResponse Economics { get; set; } = new();
    public CommunityActionJourneyDiagramSummaryResponse Diagram { get; set; } = new();
    public IReadOnlyList<CommunityActionJourneyRoleSlotResponse> RoleSlots { get; set; } = [];
    public IReadOnlyList<CommunityActionJourneyLedgerResponse> Ledgers { get; set; } = [];
    public IReadOnlyList<CommunityActionJourneyTimelineItemResponse> Timeline { get; set; } = [];

    public bool HasStarted => InterestVoteId.HasValue || !string.IsNullOrWhiteSpace(ProvisionalLedgerId);
}

public sealed class CommunityActionJourneySalesSummaryResponse
{
    public bool HasSalesOffer { get; set; }
    public string ProductTitle { get; set; } = string.Empty;
    public decimal AvailableQuantity { get; set; }
    public string QuantityUnit { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public bool AllowsGroupPurchase { get; set; }
    public string StatusCode { get; set; } = string.Empty;
}

public sealed class CommunityActionJourneyEconomicsSummaryResponse
{
    public bool HasPlan { get; set; }
    public Guid? PlanId { get; set; }
    public long PlanRevision { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public string CurrencyCode { get; set; } = string.Empty;
    public string QuantityUnit { get; set; } = string.Empty;
    public decimal CurrentCommittedQuantity { get; set; }
    public decimal MinimumOrderQuantity { get; set; }
    public decimal? MinimumViableQuantity { get; set; }
    public decimal? RecommendedQuantity { get; set; }
    public decimal? EstimatedUnitLandedCost { get; set; }
    public bool CurrentQuantityEconomicallyViable { get; set; }
    public bool ExecutionReady { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }
    public string DisclosureLevelCode { get; set; } = "aggregate-only";
    public bool ContainsParticipantPrivateMinimums { get; set; }
}

public sealed class CommunityActionJourneyDiagramSummaryResponse
{
    public bool IsAvailable { get; set; }
    public string DiagramId { get; set; } = string.Empty;
    public string DiagramName { get; set; } = string.Empty;
    public string? LedgerId { get; set; }
    public int NodeCount { get; set; }
    public int EdgeCount { get; set; }
}

public sealed class CommunityActionJourneyRoleSlotResponse
{
    public string RoleCode { get; set; } = string.Empty;
    public string CategoryCode { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public bool IsRecommended { get; set; }
    public int InterestCount { get; set; }
    public int ConfirmedParticipantCount { get; set; }
    public string StateCode { get; set; } = string.Empty;
    public bool ExternalCredentialVerificationRequired { get; set; }
    public bool ExternalCredentialVerified { get; set; }
}

public sealed class CommunityActionJourneyLedgerResponse
{
    public string LedgerId { get; set; } = string.Empty;
    public string LedgerTemplateKey { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string CurrentStageCode { get; set; } = string.Empty;
    public string RelationCode { get; set; } = string.Empty;
    public bool IsProvisional { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class CommunityActionJourneyTimelineItemResponse
{
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public DateTimeOffset OccurredAtUtc { get; set; }
    public bool IsCompleted { get; set; }
    public string? LedgerId { get; set; }
}

public static class CommunityActionJourneyStageCodes
{
    public const string Conversation = "conversation";
    public const string Gathering = "gathering";
    public const string ProvisionalLedger = "provisional-ledger";
    public const string Conditions = "conditions";
    public const string Party = "party";
    public const string Readiness = "readiness";
    public const string InProgress = "in-progress";
    public const string Completed = "completed";
    public const string Unavailable = "unavailable";
}
