namespace Ssalddel.Contracts.Common.Warehouse;

public sealed class DistributedStorageTerms
{
    public IReadOnlyList<string> AllowedSkus { get; set; } = [];
    public int MaximumStoredQuantity { get; set; }
    public DateTime StartsAtUtc { get; set; }
    public DateTime EndsAtUtc { get; set; }
    public string AccessHours { get; set; } = "";
    public string PriceOrFreeTerms { get; set; } = "";
    public string CustodianAndCare { get; set; } = "";
    public string LossDamageAndReturnTerms { get; set; } = "";
}
public sealed class DistributedStorageAgreementRequest : DistributedCommandRequest
{
    public string LocationId { get; set; } = "";
    public string GoodsOwnerUserId { get; set; } = "";
    public DistributedStorageTerms Terms { get; set; } = new();
}
public sealed class DistributedStorageAgreementConsentRequest : DistributedCommandRequest
{
    public int TermsVersion { get; set; }
    public bool Granted { get; set; }
}
public sealed record DistributedStorageAgreementDto(string AgreementId, string SiteId, string LocationId,
    string SpaceOwnerUserId, string GoodsOwnerUserId, int TermsVersion, long Revision, string StatusCode,
    bool SpaceOwnerConsented, bool GoodsOwnerConsented, DistributedStorageTerms Terms,
    IReadOnlyList<string> AllowedActions);

public sealed class DistributedInboundReceiptBatchRequest : DistributedCommandRequest
{
    public string LocationBarcode { get; set; } = "";
    public IReadOnlyList<DistributedInboundReceiptLineRequest> Lines { get; set; } = [];
    public IReadOnlyList<string> PhotoEvidenceIds { get; set; } = [];
}
public sealed class DistributedInboundReceiptLineRequest
{
    public string LineKey { get; set; } = "";
    public string Sku { get; set; } = "";
    public int Quantity { get; set; }
}
public sealed class DistributedPickBatchRequest : DistributedCommandRequest
{
    public string StopId { get; set; } = "";
    public string LocationBarcode { get; set; } = "";
    public IReadOnlyList<DistributedPickLineRequest> Lines { get; set; } = [];
    public IReadOnlyList<string> PhotoEvidenceIds { get; set; } = [];
}
public sealed class DistributedPickLineRequest
{
    public string ReservationId { get; set; } = "";
    public string OwnerUserId { get; set; } = "";
    public string Sku { get; set; } = "";
    public int Quantity { get; set; }
}
public sealed class DistributedDeliveryBatchRequest : DistributedCommandRequest
{
    public string StopId { get; set; } = "";
    public IReadOnlyList<DistributedDeliveryLineRequest> Lines { get; set; } = [];
    public IReadOnlyList<string> PhotoEvidenceIds { get; set; } = [];
}
public sealed class DistributedDeliveryLineRequest
{
    public string ReservationId { get; set; } = "";
    public int Quantity { get; set; }
}
public sealed class DistributedReturnBatchRequest : DistributedCommandRequest
{
    public string LocationBarcode { get; set; } = "";
    public IReadOnlyList<DistributedDeliveryLineRequest> Lines { get; set; } = [];
    public IReadOnlyList<string> PhotoEvidenceIds { get; set; } = [];
}
