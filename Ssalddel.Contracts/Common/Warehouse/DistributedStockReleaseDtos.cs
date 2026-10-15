namespace Ssalddel.Contracts.Common.Warehouse;

public sealed class DistributedStockReleaseProposalRequest : DistributedCommandRequest
{
    public string LotId { get; set; } = "";
    public string AssignedUserId { get; set; } = "";
    public int Quantity { get; set; }
    public string HandoverTerms { get; set; } = "";
    public string Reason { get; set; } = "";
}
public sealed class DistributedStockReleaseConsentRequest : DistributedCommandRequest
{
    public string ReleaseId { get; set; } = "";
    public bool Granted { get; set; }
    public bool CancelRemaining { get; set; }
}
public sealed class DistributedStockReleaseHandoverRequest : DistributedCommandRequest
{
    public string ReleaseId { get; set; } = "";
    public string LocationBarcode { get; set; } = "";
    public int Quantity { get; set; }
    public IReadOnlyList<string> PhotoEvidenceIds { get; set; } = [];
}
public sealed class DistributedSiteCloseRequest : DistributedCommandRequest
{
    public bool NoRemainingCustodyConfirmed { get; set; }
    public string EvidenceReference { get; set; } = "";
}
public sealed record DistributedStockReleaseDto(string ReleaseId, string SiteId, string SiteLabel,
    long SiteRevision, string LotId, string ProductLabel, string GoodsOwnerUserId, string SpaceOwnerUserId,
    string AssignedUserId, string StatusCode, int AgreedQuantity, int ReleasedQuantity,
    bool GoodsOwnerConsented, bool SpaceOwnerConsented, string HandoverTerms, string NextActorLabel,
    IReadOnlyList<string> AllowedActions, string? LocationBarcode = null, string? PickupInstructions = null);
public sealed record DistributedStockReleaseCandidateDto(string LotId, string SiteId, string SiteLabel, long SiteRevision,
    string ProductLabel, string GoodsOwnerUserId, int AvailableQuantity, IReadOnlyList<DistributedPartyCandidateDto> AssigneeCandidates);
