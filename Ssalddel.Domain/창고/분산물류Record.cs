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
    // null은 기존 수용 정책 미설정 상태이며 미확인 조건을 임의로 허용한 뜻이 아닙니다.
    public string? AcceptancePolicyJson { get; set; }
    public long AcceptancePolicyRevision { get; set; }
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
    // 기존 입고 계획의 행별 협의·승인 이력입니다. 재고 원장의 수량 권위를 대체하지 않습니다.
    public string? InboundCollaborationJson { get; set; }
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
    // 구형 영수증은 null입니다. 총수량/사진의 집합으로 행별 작업을 소급 추정하지 않습니다.
    public string? CompletionLinesJson { get; set; }
}

/// <summary>완료 명령과 함께 저장한 행별 실물 근거입니다. 업무 상태나 새 재고 원장을 소유하지 않습니다.</summary>
public sealed record 분산물류완료행
{
    public string LineKey { get; init; } = "";
    public string WorkKind { get; init; } = "";
    public string ActorUserId { get; init; } = "";
    public string LotId { get; init; } = "";
    public long InboundItemId { get; init; }
    public long? InventoryHistoryId { get; init; }
    public string? ReservationId { get; init; }
    public string? OrderId { get; init; }
    public string? RootOrderLedgerId { get; init; }
    public int Quantity { get; init; }
    public IReadOnlyList<string> PhotoEvidenceIds { get; init; } = [];
}

public sealed record 분산물류완료근거(int SchemaVersion, IReadOnlyList<분산물류완료행> Lines);
