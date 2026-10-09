namespace 살뜰.도메인.창고;

public sealed class 분산거점Record
{
    public string SiteId { get; set; } = "";
    public long WarehouseId { get; set; }
    public string PlaceId { get; set; } = "";
    public string PublicCandidateRef { get; set; } = "";
    public string PublicLabel { get; set; } = "";
    public string OwnerUserId { get; set; } = "";
    public string ManagerUserId { get; set; } = "";
    public string PrivateDetailsJson { get; set; } = "{}";
    public bool SpaceConfirmed { get; set; }
    public bool PickupPointConfirmed { get; set; }
    public bool OwnerConsented { get; set; }
    public bool ManagerConsented { get; set; }
    public string ConsentEvidenceJson { get; set; } = "[]";
    public string StatusCode { get; set; } = "PendingConsent";
    public long Revision { get; set; } = 1;
}
public sealed class 분산적재위치Record
{
    public string LocationId { get; set; } = "";
    public string SiteId { get; set; } = "";
    public string Barcode { get; set; } = "";
    public string Label { get; set; } = "";
}
public sealed class 분산입고계획Record
{
    public string PlanId { get; set; } = "";
    public string SourceReference { get; set; } = "";
    public string CreatorUserId { get; set; } = "";
    public string StatusCode { get; set; } = "Planned";
    public long Revision { get; set; } = 1;
}
public sealed class 분산재고LotRecord
{
    public string? StorageAgreementId { get; set; }
    public string StorageAgreementSnapshotJson { get; set; } = "[]";
    public string LotId { get; set; } = "";
    public string PlanId { get; set; } = "";
    public string LineKey { get; set; } = "";
    public string SiteId { get; set; } = "";
    public string LocationId { get; set; } = "";
    public long SourceInboundId { get; set; }
    public long TargetInboundId { get; set; }
    public long InboundItemId { get; set; }
    public string OwnerUserId { get; set; } = "";
    public string SellerUserId { get; set; } = "";
    public string Sku { get; set; } = "";
    public string LotCode { get; set; } = "";
    public int ExpectedQuantity { get; set; }
    public int ReceivedQuantity { get; set; }
    public long Revision { get; set; } = 1;
}
public sealed class 분산배송WaveRecord
{
    public string WaveId { get; set; } = "";
    public string RoundId { get; set; } = "";
    public string CreatorUserId { get; set; } = "";
    public string Label { get; set; } = "";
    public string StatusCode { get; set; } = "Open";
    public string? DriverId { get; set; }
    public DateTime CutoffUtc { get; set; }
    public DateTime PickupStartUtc { get; set; }
    public DateTime PickupEndUtc { get; set; }
    public DateTime DeliveryStartUtc { get; set; }
    public DateTime DeliveryEndUtc { get; set; }
    public decimal PickupServiceMinutes { get; set; }
    public decimal DeliveryServiceMinutes { get; set; }
    public string RouteJson { get; set; } = "[]";
    public string RouteEvidenceJson { get; set; } = "{}";
    public decimal? TotalElapsedMinutes { get; set; }
    public decimal? TotalDistanceKm { get; set; }
    public decimal? OfferedDriverFareKrw { get; set; }
    public long Revision { get; set; } = 1;
}
public sealed class 분산배송주문Record
{
    public string OrderId { get; set; } = "";
    public string WaveId { get; set; } = "";
    public string OrderReference { get; set; } = "";
    public string CargoRequestId { get; set; } = "";
    public string CargoEvidenceDigest { get; set; } = "";
    public string SellerUserId { get; set; } = "";
    public string CustomerUserId { get; set; } = "";
    public string StatusCode { get; set; } = "Reserved";
    public bool CustomerAcknowledged { get; set; }
    public long Revision { get; set; } = 1;
}
public sealed class 분산재고예약Record
{
    public string ReservationId { get; set; } = "";
    public string WaveId { get; set; } = "";
    public string OrderId { get; set; } = "";
    public string LotId { get; set; } = "";
    public long OutboundPlanId { get; set; }
    public int Quantity { get; set; }
    public int PickedQuantity { get; set; }
    public int DeliveredQuantity { get; set; }
    public int ReleasedQuantity { get; set; }
    public int ReturnedQuantity { get; set; }
    public long Revision { get; set; } = 1;
}
public sealed class 분산물류CommandReceipt
{
    public string ActorUserId { get; set; } = "";
    public Guid RequestId { get; set; }
    public string InputDigest { get; set; } = "";
    public string Kind { get; set; } = "";
    public string AggregateId { get; set; } = "";
    public string? ReservationId { get; set; }
    public int Quantity { get; set; }
    public long Revision { get; set; }
    public string StatusCode { get; set; } = "";
    public DateTime RecordedAtUtc { get; set; }
}
