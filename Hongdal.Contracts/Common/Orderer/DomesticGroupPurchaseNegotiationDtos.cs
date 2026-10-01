namespace Hongdal.Contracts.Common.Orderer;

public static class DomesticGroupPurchaseNegotiationVisibilityCodes
{
    public const string CommunityMembers = "community-members";
}

public static class DomesticGroupPurchaseNegotiationEventTypeCodes
{
    public const string Proposal = "proposal";
    public const string CounterProposal = "counter-proposal";
    public const string Clarification = "clarification";
    public const string Agreement = "agreement";
    public const string IssueRaised = "issue-raised";
    public const string DeliberationOpinion = "deliberation-opinion";
    public const string Resolution = "resolution";
}

public static class DomesticGroupPurchaseNegotiationIssueStatusCodes
{
    public const string Deliberating = "deliberating";
    public const string Resolved = "resolved";
}

public static class DomesticGroupPurchaseDeliberationPositionCodes
{
    public const string Support = "support";
    public const string Concern = "concern";
    public const string Alternative = "alternative";
}

public sealed class DomesticGroupPurchaseNegotiationTimelineResponse
{
    public Guid GroupPurchaseCampaignId { get; set; }
    public string VisibilityCode { get; set; } = DomesticGroupPurchaseNegotiationVisibilityCodes.CommunityMembers;
    public bool CommunityVisible { get; set; } = true;
    public bool ContactDetailsDisclosed { get; set; }
    public List<DomesticGroupPurchaseNegotiationEventResponse> Events { get; set; } = [];
    public List<DomesticGroupPurchaseNegotiationIssueResponse> Issues { get; set; } = [];
}

public sealed class DomesticGroupPurchaseNegotiationEventRequest
{
    public string EventTypeCode { get; set; } = DomesticGroupPurchaseNegotiationEventTypeCodes.Proposal;
    public string MaskedActorDisplayName { get; set; } = string.Empty;
    public string ActorRoleLabel { get; set; } = string.Empty;
    public string PublicSummary { get; set; } = string.Empty;
}

public sealed class DomesticGroupPurchaseNegotiationEventResponse
{
    public Guid EventId { get; set; }
    public string EventTypeCode { get; set; } = string.Empty;
    public string MaskedActorDisplayName { get; set; } = string.Empty;
    public string ActorRoleLabel { get; set; } = string.Empty;
    public string PublicSummary { get; set; } = string.Empty;
    public Guid? RelatedIssueId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public bool ContactDetailsDisclosed { get; set; }
}

public sealed class DomesticGroupPurchaseNegotiationIssueRequest
{
    public string Title { get; set; } = string.Empty;
    public string PublicSummary { get; set; } = string.Empty;
    public string MaskedReporterDisplayName { get; set; } = string.Empty;
    public string ReporterRoleLabel { get; set; } = string.Empty;
    public int DeliberationHours { get; set; } = 24;
}

public sealed class DomesticGroupPurchaseNegotiationIssueResponse
{
    public Guid IssueId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string PublicSummary { get; set; } = string.Empty;
    public string MaskedReporterDisplayName { get; set; } = string.Empty;
    public string ReporterRoleLabel { get; set; } = string.Empty;
    public string StatusCode { get; set; } = DomesticGroupPurchaseNegotiationIssueStatusCodes.Deliberating;
    public DateTimeOffset OpenedAtUtc { get; set; }
    public DateTimeOffset DeliberationClosesAtUtc { get; set; }
    public int DistinctParticipantCount { get; set; }
    public bool CanResolve { get; set; }
    public bool ContactDetailsDisclosed { get; set; }
    public List<DomesticGroupPurchaseDeliberationPositionResponse> Positions { get; set; } = [];
    public DomesticGroupPurchaseNegotiationResolutionResponse? Resolution { get; set; }
}

public sealed class DomesticGroupPurchaseDeliberationPositionRequest
{
    public string PositionCode { get; set; } = DomesticGroupPurchaseDeliberationPositionCodes.Concern;
    public string MaskedParticipantDisplayName { get; set; } = string.Empty;
    public string ParticipantRoleLabel { get; set; } = string.Empty;
    public string PublicRationale { get; set; } = string.Empty;
}

public sealed class DomesticGroupPurchaseDeliberationPositionResponse
{
    public Guid PositionId { get; set; }
    public string PositionCode { get; set; } = string.Empty;
    public string MaskedParticipantDisplayName { get; set; } = string.Empty;
    public string ParticipantRoleLabel { get; set; } = string.Empty;
    public string PublicRationale { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public bool ContactDetailsDisclosed { get; set; }
}

public sealed class DomesticGroupPurchaseNegotiationResolutionRequest
{
    public string MaskedResolverDisplayName { get; set; } = string.Empty;
    public string ResolverRoleLabel { get; set; } = string.Empty;
    public string ResolutionSummary { get; set; } = string.Empty;
    public string DecisionRationale { get; set; } = string.Empty;
}

public sealed class DomesticGroupPurchaseNegotiationResolutionResponse
{
    public string MaskedResolverDisplayName { get; set; } = string.Empty;
    public string ResolverRoleLabel { get; set; } = string.Empty;
    public string ResolutionSummary { get; set; } = string.Empty;
    public string DecisionRationale { get; set; } = string.Empty;
    public DateTimeOffset ResolvedAtUtc { get; set; }
    public bool ContactDetailsDisclosed { get; set; }
}
