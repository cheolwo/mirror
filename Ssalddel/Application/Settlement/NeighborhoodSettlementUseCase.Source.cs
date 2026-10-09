namespace Ssalddel.Application.Settlement;

/// <summary>클라이언트가 주장한 역할·금액이 아니라 기존 업무 원장에서 확인한 정산 근거입니다.</summary>
public sealed record NeighborhoodSettlementSourceParty(string UserId, IReadOnlyList<string> Roles);

/// <summary>원본 업무의 실제 처리량과 근거입니다. 사진 저장만으로 이 기록을 만들지 않습니다.</summary>
public sealed record NeighborhoodSettlementCompletedWork(string Kind, string OriginKind, string OriginId,
    decimal Quantity, DateTime? OccurredAtUtc, IReadOnlyList<string> EvidenceReferences, string? ActorUserId = null,
    string? GoodsOwnerUserId = null, string? SpaceOwnerUserId = null, string? SellerUserId = null);

/// <summary>실물 입출고 근거로 확인한 연속 보관 구간입니다. 입고계획·공간 동의일을 대신 넣지 않습니다.</summary>
public sealed record NeighborhoodSettlementStorageInterval(DateTime StartUtc, DateTime? EndUtc, decimal MinimumQuantity = 1m);

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
}

public sealed class NeighborhoodSettlementException(string code, int statusCode = 409) : Exception(code)
{
    public string Code { get; } = code;
    public int StatusCode { get; } = statusCode;
}
