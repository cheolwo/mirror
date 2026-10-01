namespace HongdalApp.Services.Customs;

public sealed class CustomsHsReviewRequest
{
    public long Id { get; set; }

    public string TransportRequestId { get; set; } = string.Empty;

    public string ShipperUserId { get; set; } = string.Empty;

    public string CargoName { get; set; } = string.Empty;

    public string FlowDirection { get; set; } = string.Empty;

    public string PickupLocation { get; set; } = string.Empty;

    public string DropoffLocation { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string? AssignedBrokerId { get; set; }

    public string? AssignedBrokerName { get; set; }

    public string? ConfirmedHsCode { get; set; }

    public string? BrokerComment { get; set; }

    public IReadOnlyList<HsCodeSuggestion> Suggestions { get; set; } = [];

    public DateTime CreatedAt { get; set; }

    public DateTime? ReviewedAt { get; set; }
}
