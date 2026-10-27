namespace Ssalddel.Contracts.Common.Warehouse;

public static class DistributedStockResolutionKinds
{
    public const string Handover = "handover";
    public const string DamagedDisposal = "damaged-disposal";
    public const string AvailableShortage = "available-shortage";
    public const string DamagedShortage = "damaged-shortage";
    public static string Normalize(string? value) => value ?? Handover;
    public static bool Known(string? value) => Normalize(value) is Handover or DamagedDisposal or AvailableShortage or DamagedShortage;
    public static bool UsesDamagedStock(string? value) => Normalize(value) is DamagedDisposal or DamagedShortage;
}

public sealed class DistributedStockReleaseProposalRequest : DistributedCommandRequest
{
    public string LotId { get; set; } = "";
    public string AssignedUserId { get; set; } = "";
    public int Quantity { get; set; }
    public string HandoverTerms { get; set; } = "";
    public string Reason { get; set; } = "";
    // null인 과거 요청의 JSON·사진 결속 다이제스트는 그대로 유지한다.
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? ResolutionKind { get; set; }
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
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? ResolutionEvidenceReference { get; set; }
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
    IReadOnlyList<string> AllowedActions, string? LocationBarcode = null, string? PickupInstructions = null,
    string? ResolutionKind = null);
public sealed record DistributedStockReleaseCandidateDto(string LotId, string SiteId, string SiteLabel, long SiteRevision,
    string ProductLabel, string GoodsOwnerUserId, int AvailableQuantity, IReadOnlyList<DistributedPartyCandidateDto> AssigneeCandidates,
    int DamagedQuantity = 0);
