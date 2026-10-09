using System.Text.Json.Serialization;

namespace Ssalddel.Contracts.Common.Warehouse;

public static class DistributedFulfillmentCodes
{
    public const string Profile = "distributed-fulfillment-r36";
    public const string BaseRoute = "api/v1/distributed-fulfillment";
    public const string Pickup = "Pickup", Delivery = "Delivery";
}

public class DistributedCommandRequest
{
    public Guid RequestId { get; set; }
    public long ExpectedRevision { get; set; }
}
public sealed class DistributedSiteCreateRequest : DistributedCommandRequest
{
    public DistributedNewWarehouseRequest? NewWarehouse { get; set; }
    public long WarehouseId { get; set; }
    public string PlaceId { get; set; } = "";
    public string PublicCandidateRef { get; set; } = "";
    public string PublicLabel { get; set; } = "";
    public string ManagerUserId { get; set; } = "";
    public string Floor { get; set; } = "";
    public string Subspace { get; set; } = "";
    public string PickupInstructions { get; set; } = "";
    public bool SpaceConfirmed { get; set; }
    public bool PickupPointConfirmed { get; set; }
}
public sealed class DistributedNewWarehouseRequest
{
    public string Name { get; set; } = "";
    public string ConfirmedRoadAddress { get; set; } = "";
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public bool AddressConfirmed { get; set; }
    public bool CoordinatesConfirmed { get; set; }
}
public sealed class DistributedConsentRequest : DistributedCommandRequest
{
    public string ConsentKind { get; set; } = ""; // Owner or Manager, independently confirmed.
    public bool Granted { get; set; }
    public string EvidenceReference { get; set; } = "";
}
public sealed class DistributedLocationCreateRequest : DistributedCommandRequest
{
    public string LocationLabel { get; set; } = "";
}
public sealed class DistributedInboundPlanRequest : DistributedCommandRequest
{
    public DistributedNewInboundSourceRequest? NewSource { get; set; }
    public string SourceReference { get; set; } = "";
    public IReadOnlyList<DistributedInboundLineRequest> Lines { get; set; } = [];
}
public sealed class DistributedNewInboundSourceRequest
{
    public string ProductName { get; set; } = "";
    public string Sku { get; set; } = "";
    public int ExpectedQuantity { get; set; }
    public bool StockOwnerIsCurrentSellerConfirmed { get; set; }
    public string OwnershipEvidenceReference { get; set; } = "";
}
public sealed class DistributedInboundLineRequest
{
    public string LineKey { get; set; } = "";
    public long SourceInboundId { get; set; }
    public string SiteId { get; set; } = "";
    public string LocationId { get; set; } = "";
    public string Sku { get; set; } = "";
    public string ProductName { get; set; } = "";
    public string LotCode { get; set; } = "";
    public int ExpectedQuantity { get; set; }
    public bool StockOwnerIsCurrentSellerConfirmed { get; set; }
    public string OwnershipEvidenceReference { get; set; } = "";
}
public sealed class DistributedExistingLotRequest : DistributedCommandRequest
{
    public IReadOnlyList<string> PhotoEvidenceIds { get; set; } = [];
    public long InboundItemId { get; set; }
    public string LocationId { get; set; } = "";
    public string LotCode { get; set; } = "";
    public string LocationBarcode { get; set; } = "";
    public bool ActualLocationConfirmed { get; set; }
    public string EvidenceReference { get; set; } = "";
}
public sealed class DistributedInboundReceiptRequest : DistributedCommandRequest
{
    public IReadOnlyList<string> PhotoEvidenceIds { get; set; } = [];
    public string LineKey { get; set; } = "";
    public string LocationBarcode { get; set; } = "";
    public string Sku { get; set; } = "";
    public int Quantity { get; set; }
    public string EvidenceReference { get; set; } = "";
}
public sealed class DistributedWaveCreateRequest : DistributedCommandRequest
{
    public string Label { get; set; } = "";
    public DateTime CutoffUtc { get; set; }
    public DateTime PickupStartUtc { get; set; }
    public DateTime PickupEndUtc { get; set; }
    public DateTime DeliveryStartUtc { get; set; }
    public DateTime DeliveryEndUtc { get; set; }
    public decimal PickupServiceMinutes { get; set; }
    public decimal DeliveryServiceMinutes { get; set; }
}
public sealed class DistributedOrderRequest : DistributedCommandRequest
{
    public string OrderReference { get; set; } = "";
    // Existing cargo API owns destination, time windows and fare; never invent them here.
    public string CargoRequestId { get; set; } = "";
    public IReadOnlyList<DistributedOrderLineRequest> Lines { get; set; } = [];
    // 표시명이 아닌 기존 주문 원장 선택입니다. 생략한 구형 요청의 직렬화와 영수증 해시는 유지합니다.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RootOrderLedgerId { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? ExpectedRootRevision { get; set; }
}
public sealed class DistributedOrderLedgerLinkRequest : DistributedCommandRequest
{
    public string RootOrderLedgerId { get; set; } = "";
    public long ExpectedRootRevision { get; set; }
}
public sealed record DistributedOrderLedgerReferenceDto(string WaveId, string OrderId, string RootOrderLedgerId,
    string OrderLedgerConnectionStatusCode);
public sealed record DistributedRootOrderCandidateDto(string RootOrderLedgerId, long Revision, string Label, string StatusCode);
public sealed record DistributedRootOrderCandidatesDto(string WaveId, string CargoRequestId, string CurrentUserId,
    long WaveRevision, IReadOnlyList<DistributedRootOrderCandidateDto> Candidates);
public sealed class DistributedOrderLineRequest
{
    public string LotId { get; set; } = "";
    public int Quantity { get; set; }
    // Import an already reserved outbound allocation without reserving it again.
    public long? ExistingOutboundPlanId { get; set; }
}
public sealed class DistributedRoundPlanRequest : DistributedCommandRequest
{
    public string DriverId { get; set; } = "";
}
public sealed class DistributedRoundAcceptRequest : DistributedCommandRequest
{
    public IReadOnlyList<string> AcknowledgedWarningCodes { get; set; } = [];
}
public sealed class DistributedPickRequest : DistributedCommandRequest
{
    public IReadOnlyList<string> PhotoEvidenceIds { get; set; } = [];
    public string StopId { get; set; } = "";
    public string ReservationId { get; set; } = "";
    public string LocationBarcode { get; set; } = "";
    public string OwnerUserId { get; set; } = "";
    public string Sku { get; set; } = "";
    public int Quantity { get; set; }
    public string EvidenceReference { get; set; } = "";
}
public sealed class DistributedDeliveryRequest : DistributedCommandRequest
{
    public IReadOnlyList<string> PhotoEvidenceIds { get; set; } = [];
    public string StopId { get; set; } = "";
    public string OrderId { get; set; } = "";
    public string EvidenceReference { get; set; } = "";
}
public sealed class DistributedAcknowledgementRequest : DistributedCommandRequest
{
    public string OrderId { get; set; } = "";
    public string EvidenceReference { get; set; } = "";
}
public sealed class DistributedCancelRequest : DistributedCommandRequest
{
    public string OrderId { get; set; } = "";
    public string Reason { get; set; } = "";
}
public sealed class DistributedReturnRequest : DistributedCommandRequest
{
    public IReadOnlyList<string> PhotoEvidenceIds { get; set; } = [];
    public string ReservationId { get; set; } = "";
    public string LocationBarcode { get; set; } = "";
    public int Quantity { get; set; }
    public string EvidenceReference { get; set; } = "";
}
public sealed record DistributedCommandResponse(Guid RequestId, string AggregateId, long Revision, string StatusCode, bool Replayed)
{
    public string? OrderLedgerConnectionStatusCode { get; init; }
    public string? OrderLedgerConnectionErrorCode { get; init; }
}
public sealed class DistributedWorkspaceDto
{
    public IReadOnlyList<DistributedStorageAgreementDto> StorageAgreements { get; set; } = [];
    public IReadOnlyList<DistributedPartyCandidateDto> GoodsOwnerCandidates { get; set; } = [];
    public string CurrentUserId { get; set; } = "";
    public bool IsSimulation { get; set; }
    public string Profile { get; set; } = DistributedFulfillmentCodes.Profile;
    public IReadOnlyList<string> AllowedActions { get; set; } = [];
    public IReadOnlyList<DistributedSiteDto> Sites { get; set; } = [];
    public IReadOnlyList<DistributedLotDto> Lots { get; set; } = [];
    public IReadOnlyList<DistributedInboundDto> InboundPlans { get; set; } = [];
    public IReadOnlyList<DistributedWaveDto> Waves { get; set; } = [];
    public IReadOnlyList<DistributedReceiptDto> Receipts { get; set; } = [];
    public IReadOnlyList<DistributedWarehouseCandidateDto> WarehouseCandidates { get; set; } = [];
    public IReadOnlyList<DistributedInboundSourceCandidateDto> InboundSourceCandidates { get; set; } = [];
    public IReadOnlyList<DistributedCargoCandidateDto> CargoCandidates { get; set; } = [];
    public IReadOnlyList<DistributedPartyCandidateDto> DriverCandidates { get; set; } = [];
    public IReadOnlyList<DistributedExistingStockCandidateDto> ExistingStockCandidates { get; set; } = [];
    public IReadOnlyList<DistributedExistingOutboundCandidateDto> ExistingOutboundCandidates { get; set; } = [];
}
public sealed record DistributedExistingOutboundCandidateDto(long ExistingOutboundPlanId, string CargoRequestId,
    string LotId, string SiteId, string OwnerUserId, string SellerUserId, string Sku, int Quantity,
    string ProductLabel, string OwnerLabel);
public sealed record DistributedAcceptanceWarningDto(string CargoRequestId, string Code, string Message);
public sealed record DistributedPartyCandidateDto(string UserId, string Label);
public sealed record DistributedBuildingCandidateDto(string PlaceId, string PublicCandidateRef, string PublicLabel);
public sealed record DistributedWarehouseCandidateDto(long WarehouseId, string Label,
    IReadOnlyList<DistributedBuildingCandidateDto> BuildingCandidates, IReadOnlyList<DistributedPartyCandidateDto> ManagerCandidates);
public sealed record DistributedInboundSourceCandidateDto(long SourceInboundId, string Label, string Sku, int ExpectedQuantity,
    string SellerUserId, string? OwnerUserId);
public sealed record DistributedCargoCandidateDto(string CargoRequestId, string Label, DateTime PickupStartUtc, DateTime PickupEndUtc,
    DateTime? DeliveryStartUtc, DateTime? DeliveryEndUtc);
public sealed record DistributedExistingStockCandidateDto(long InboundItemId, string SiteId, string Label, string OwnerUserId,
    string SellerUserId, string Sku, int ReceivedQuantity, int AvailableQuantity);
public sealed class DistributedSiteDto
{
    public string SiteId { get; set; } = "";
    public long WarehouseId { get; set; }
    public string PlaceId { get; set; } = "";
    public string PublicLabel { get; set; } = "";
    public string PublicCandidateRef { get; set; } = "";
    public string StatusCode { get; set; } = "";
    public long Revision { get; set; }
    public bool OwnerConsented { get; set; }
    public bool ManagerConsented { get; set; }
    public string? Floor { get; set; }
    public string? Subspace { get; set; }
    public string? PickupInstructions { get; set; }
    public IReadOnlyList<string> AllowedActions { get; set; } = [];
    public IReadOnlyList<DistributedLocationDto> Locations { get; set; } = [];
}
public sealed record DistributedLocationDto(string LocationId, string SiteId, string Barcode, string Label);
public sealed record DistributedLotDto(string LotId, string SiteId, string LocationId, string OwnerUserId, string SellerUserId, string Sku,
    string LotCode, long InboundItemId, int ReceivedQuantity, int AvailableQuantity, int ReservedQuantity,
    string OwnerLabel = "", string ProductLabel = "");
public sealed record DistributedInboundLineDto(string LineKey, string SiteId, string LocationId, string OwnerUserId,
    string Sku, string LotCode, int ExpectedQuantity, int ReceivedQuantity);
public sealed record DistributedInboundDto(string PlanId, string SourceReference, string StatusCode, long Revision,
    IReadOnlyList<DistributedInboundLineDto> Lines, IReadOnlyList<string> AllowedActions);
public sealed class DistributedWaveDto
{
    public string WaveId { get; set; } = "";
    public string RoundId { get; set; } = "";
    public string Label { get; set; } = "";
    public string StatusCode { get; set; } = "";
    public long Revision { get; set; }
    public string? DriverId { get; set; }
    public DateTime CutoffUtc { get; set; }
    public DateTime PickupStartUtc { get; set; }
    public DateTime PickupEndUtc { get; set; }
    public DateTime DeliveryStartUtc { get; set; }
    public DateTime DeliveryEndUtc { get; set; }
    public decimal? TotalElapsedMinutes { get; set; }
    public decimal? TotalDistanceKm { get; set; }
    public decimal? OfferedDriverFareKrw { get; set; }
    public string? CurrentStopId { get; set; }
    public IReadOnlyList<string> BlockCodes { get; set; } = [];
    public IReadOnlyList<string> AllowedActions { get; set; } = [];
    public IReadOnlyList<DistributedStopDto> Stops { get; set; } = [];
    public IReadOnlyList<DistributedOrderDto> Orders { get; set; } = [];
    public IReadOnlyList<DistributedReservationDto> Reservations { get; set; } = [];
    public IReadOnlyList<DistributedAcceptanceWarningDto> AcceptanceWarnings { get; set; } = [];
    public string? AcceptanceErrorCode { get; set; }
    public string? AcceptanceErrorMessage { get; set; }
}
public sealed record DistributedStopDto(string StopId, int Sequence, string Kind, string? SiteId,
    IReadOnlyList<string> OrderIds, DateTime ExpectedArrivalUtc, DateTime WindowStartUtc, DateTime WindowEndUtc,
    decimal ServiceMinutes, string StatusCode, string? Address, decimal? Latitude, decimal? Longitude);
public sealed record DistributedOrderDto(string OrderId, string OrderReference, string CargoRequestId,
    string StatusCode, int ReservedQuantity, int PickedQuantity, int DeliveredQuantity, int ReturnedQuantity,
    bool IsCurrentCustomer, bool CustomerAcknowledged, DateTime? DeliveryStartUtc, DateTime? DeliveryEndUtc,
    IReadOnlyList<string> AllowedActions)
{
    public string? RootOrderLedgerId { get; init; }
    public long? RootOrderLedgerRevision { get; init; }
    public string? OrderLedgerConnectionStatusCode { get; init; }
    public string? OrderLedgerConnectionErrorCode { get; init; }
}
public sealed record DistributedReservationDto(string ReservationId, string OrderId, string LotId, string SiteId,
    string LocationId, string OwnerUserId, string Sku, int Quantity, int PickedQuantity, int DeliveredQuantity,
    int ReleasedQuantity, int ReturnedQuantity, long OutboundPlanId, IReadOnlyList<string> AllowedActions,
    string OwnerLabel = "", string ProductLabel = "");
public sealed record DistributedReceiptDto(Guid RequestId, string Kind, string AggregateId, string? ReservationId,
    int Quantity, long Revision, DateTime RecordedAtUtc);
// Authenticated scoped projection. No precise address, coordinates, contact, owner/customer identity or barcode.
public sealed record DistributedWorldProjectionDto(bool IsSimulation, string Profile,
    IReadOnlyList<DistributedWorldSiteDto> Sites, IReadOnlyList<DistributedWorldRoundDto> Rounds);
public sealed record DistributedWorldSiteDto(string SiteId, string PlaceId, string PublicLabel, string StatusCode,
    long Revision, int LocationCount, int ReceivedQuantity, int ReservedQuantity, int PickedQuantity);
public sealed record DistributedWorldRoundDto(string WaveId, string RoundId, string StatusCode, long Revision,
    IReadOnlyList<DistributedWorldStopDto> Stops);
public sealed record DistributedWorldStopDto(string StopId, int Sequence, string Kind, string? SiteId,
    string StatusCode, int OrderCount, int PickedQuantity, int DeliveredQuantity);
