namespace Ssalddel.Contracts.Common.Community;

public static class NeighborhoodFulfillmentStates
{
    public const string AwaitingSelection = "AwaitingSelection";
    public const string Submitting = "Submitting";
    public const string ResultUnknown = "ResultUnknown";
    public const string Accepted = "Accepted";
    public const string InTransit = "InTransit";
    public const string Completed = "Completed";
    public const string CancelPending = "CancelPending";
    public const string Cancelled = "Cancelled";
    public const string Failed = "Failed";
}

public sealed class NeighborhoodFulfillmentOption
{
    public string ProviderId { get; set; } = string.Empty;
    public string ProviderName { get; set; } = string.Empty;
    public string QuoteId { get; set; } = string.Empty;
    public bool IsSimulation { get; set; }
    public bool IsAvailable { get; set; }
    public string UnavailableReason { get; set; } = string.Empty;
    public decimal? TotalCostKrw { get; set; }
    public DateTime? EstimatedCompletionUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public bool IsRecommended { get; set; }
}

public sealed class NeighborhoodFulfillmentSelection
{
    public string ProviderId { get; set; } = string.Empty;
    public string QuoteId { get; set; } = string.Empty;
    public decimal ConfirmedCostKrw { get; set; }
    public bool ProviderInformationSharingAgreed { get; set; }
    public bool DirectPaymentAgreed { get; set; }
}

public sealed class NeighborhoodFulfillmentChoiceRequest
{
    public Guid ClientRequestId { get; set; }
    public long ExpectedRevision { get; set; }
    public NeighborhoodFulfillmentSelection Selection { get; set; } = new();
}

public sealed class NeighborhoodFulfillmentActionRequest
{
    public Guid ClientRequestId { get; set; }
    public long ExpectedRevision { get; set; }
}

public sealed class NeighborhoodFulfillmentSnapshot
{
    public string RequestId { get; set; } = string.Empty;
    public string ProviderId { get; set; } = string.Empty;
    public string ProviderName { get; set; } = string.Empty;
    public string StateCode { get; set; } = NeighborhoodFulfillmentStates.AwaitingSelection;
    public long Revision { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public decimal? CostKrw { get; set; }
    public DateTime? EstimatedCompletionUtc { get; set; }
    public bool IsSimulation { get; set; }
    public bool CanCancel { get; set; }
    public bool CanChangeProvider { get; set; }
    public string Notice { get; set; } = string.Empty;
    public string? AttemptId { get; set; }
    public string? ExternalRequestId { get; set; }
    public bool IsOperationalSettlementEligible { get; set; }
}
