using Ssalddel.Contracts.Common.Settlement;

namespace Ssalddel.Application.Settlement;

/// <summary>클라이언트가 주장한 역할·금액이 아니라 기존 업무 원장에서 확인한 정산 근거입니다.</summary>
public sealed record NeighborhoodSettlementSourceParty(string UserId, IReadOnlyList<string> Roles);

/// <summary>원본 업무의 실제 처리량과 근거입니다. 사진 저장만으로 이 기록을 만들지 않습니다.</summary>
public sealed record NeighborhoodSettlementCompletedWork(string Kind, string OriginKind, string OriginId,
    decimal Quantity, DateTime? OccurredAtUtc, IReadOnlyList<string> EvidenceReferences, string? ActorUserId = null,
    string? GoodsOwnerUserId = null, string? SpaceOwnerUserId = null, string? SellerUserId = null,
    long OriginRevision = 0, string? RelatedOrderId = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    DateTime? AgreementDeadlineAtUtc = null);

/// <summary>서버 원장에서 확인한 주문별 고객 수령 수량입니다. 수익·수금 확정과는 별개입니다.</summary>
public sealed record NeighborhoodSettlementRewardOutcome(decimal ConfirmedQuantity, IReadOnlyList<string> EvidenceReferences);

/// <summary>실물 입출고 근거로 확인한 연속 보관 구간입니다. 입고계획·공간 동의일을 대신 넣지 않습니다.</summary>
public sealed record NeighborhoodSettlementStorageInterval(DateTime StartUtc, DateTime? EndUtc, decimal MinimumQuantity = 1m);

/// <summary>검증된 재고 변화 사이의 실제 상수량 보관 구간입니다. 과거 기간의 수량을 이후 피킹으로 낮추지 않습니다.</summary>
public sealed record NeighborhoodSettlementStorageQuantityInterval(DateTime StartUtc, DateTime? EndUtc, decimal Quantity);

/// <summary>전문 원장이 확인한 이 주문의 실제 재고 예약·최초 피킹입니다. 클라이언트 수량을 사용하지 않습니다.</summary>
public sealed record NeighborhoodSettlementInventoryReservation(string ReservationId, string LotId,
    decimal Quantity, DateTime? FirstPickedAtUtc);

public sealed class NeighborhoodSettlementSourceSnapshot
{
    public string SourceKind { get; init; } = "";
    public string SourceId { get; init; } = "";
    // 완료량이 늘어나는 것과 당사자·동결가격이 바뀌는 것을 구별하는 합의 근거 판본입니다.
    public string SourceVersion { get; init; } = "";
    public string SourceDigest { get; init; } = "";
    public IReadOnlyList<NeighborhoodSettlementSourceParty> Participants { get; init; } = [];
    public IReadOnlyList<string> AllowedActivityKinds { get; init; } = [];
    public decimal ActualCompletedUnits { get; init; }
    public IReadOnlyList<string> CompletionEvidenceReferences { get; init; } = [];
    public IReadOnlyList<NeighborhoodSettlementCompletedWork> CompletedWork { get; init; } = [];
    public decimal? HistoricalSaleTotalKrw { get; init; }
    public decimal? FrozenDriverFareKrw { get; init; }
    public string? CanonicalDeliveryOrigin { get; init; }
    public string? OriginalExternalPaymentStatus { get; init; }
    public DateTime ObservedAtUtc { get; init; }
    public DateTime? StorageStartedAtUtc { get; init; }
    public IReadOnlyList<NeighborhoodSettlementStorageInterval> StorageIntervals { get; init; } = [];
    /// <summary>기간 원가귀속용 내부 근거입니다. 연속 보관 구간·청구할 완료 작업과 별도로 유지합니다.</summary>
    public IReadOnlyList<NeighborhoodSettlementStorageQuantityInterval> StorageQuantityIntervals { get; init; } = [];
    /// <summary>입고·피킹·반환 등 보관 구간 계산에 실제 사용한 재고 변화 근거입니다. 청구할 작업 목록과 구별합니다.</summary>
    public IReadOnlyList<string> StorageEvidenceReferences { get; init; } = [];
    public NeighborhoodSettlementRewardOutcome? RewardOutcome { get; init; }
    /// <summary>상품 전량 수령의 전문 근거입니다. null인 과거 동결 원천 JSON은 변경하지 않습니다.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public NeighborhoodSettlementCustomerReceiptDto? CustomerReceipt { get; init; }
    public NeighborhoodSettlementWorkflowDto? Workflow { get; init; }
    public IReadOnlyList<NeighborhoodSettlementInventoryReservation> InventoryReservations { get; init; } = [];
}

public interface INeighborhoodSettlementSourceResolver
{
    Task<NeighborhoodSettlementSourceSnapshot> ResolveAsync(string sourceKind, string sourceId,
        CancellationToken cancellationToken);
}

public sealed class NeighborhoodSettlementOptions
{
    public const string SectionName = "NeighborhoodSettlement";
    public const string RequiredProfile = "neighborhood-settlement-r38";
    public bool Enabled { get; set; }
    public string Profile { get; set; } = RequiredProfile;
    public string PlatformPayeeUserId { get; set; } = "";
}

public sealed class NeighborhoodSettlementException(string code, int statusCode = 409) : Exception(code)
{
    public string Code { get; } = code;
    public int StatusCode { get; } = statusCode;
}
