using FluentResults;
using Ssalddel.Contracts.Common.Fulfillment;

namespace Ssalddel.Services.Fulfillment;

public sealed class FulfillmentRoutingOptions
{
    public const string SectionName = "FulfillmentRouting";
    public bool Enabled { get; set; }
    public bool AssumedProvidersEnabled { get; set; }
    public int QuoteLifetimeMinutes { get; set; } = 5;
}

public sealed record FulfillmentSourceSnapshot(FulfillmentSourceKey Key, string OwnerId, long SourceRevision,
    string InputDigest, bool IsClosed, bool ReadyForRouting, bool HasCookingStarted, bool CanCancel,
    decimal? DistanceKm, decimal? InternalCostKrw, DateTime EarliestPickupUtc, int PreparationMinutes = 0,
    decimal? WeightKg = null, int Quantity = 1, string VehicleType = "오토바이", long? RestaurantId = null,
    string Leg = "initial", decimal? TravelMinutes = null, string? PackageSizeCode = null);

public sealed record FulfillmentAttemptContext(FulfillmentSourceKey Key, string AttemptId, string ProviderId,
    long Revision, long ConsentRevision, string Leg, decimal QuotedCostKrw, DateTime EstimatedCompletionUtc,
    string? ExternalRequestId);

public sealed record FulfillmentExecutionResult(string StateCode, string? ExternalRequestId = null,
    decimal? ConfirmedIncurredCostKrw = null);

public sealed record FulfillmentExecutionEvidence(FulfillmentAttemptContext Attempt, string EvidenceKind,
    string EvidenceId, string? ExternalDriverReference, DateTime RecordedAtUtc);

public interface I수행원천Adapter
{
    string SourceKind { get; }
    Task<FulfillmentSourceSnapshot?> 읽기Async(FulfillmentSourceKey key, CancellationToken ct);
    Task<FulfillmentExecutionResult> 내부접수Async(FulfillmentAttemptContext attempt, CancellationToken ct);
    Task<FulfillmentExecutionResult> 내부조회Async(FulfillmentAttemptContext attempt, CancellationToken ct);
    Task<FulfillmentExecutionResult> 내부취소Async(FulfillmentAttemptContext attempt, CancellationToken ct);
    Task<Result> 외부증거반영Async(FulfillmentExecutionEvidence evidence, CancellationToken ct);
}

public interface I자동수행Coordinator
{
    bool Enabled { get; }
    IReadOnlyList<AutoFulfillmentProviderDescriptor> 수행처후보(string sourceKind)
        => Enabled && FulfillmentSourceKinds.IsKnown(sourceKind) ? AutoFulfillmentNotice.Providers : [];
    Task<Result<AutoFulfillmentSnapshot>> 사전동의저장Async(FulfillmentSourceKey key, string ownerId, AutoFulfillmentConsent consent, CancellationToken ct = default);
    Task 예약Async(FulfillmentSourceKey key, Guid operationId, CancellationToken ct = default);
    Task<Result<AutoFulfillmentSnapshot>> 선정실행Async(FulfillmentSourceKey key, Guid operationId, CancellationToken ct = default);
    Task<AutoFulfillmentSnapshot?> 상태Async(FulfillmentSourceKey key, CancellationToken ct = default);
    Task<Result<AutoFulfillmentSnapshot>> 재확인Async(FulfillmentSourceKey key, AutoFulfillmentActionRequest request, CancellationToken ct = default);
    Task<Result<AutoFulfillmentSnapshot>> 취소Async(FulfillmentSourceKey key, AutoFulfillmentActionRequest request, CancellationToken ct = default);
    Task<bool> 내부수행허용Async(FulfillmentSourceKey key, string? attemptId = null, CancellationToken ct = default);
    Task<Result<AutoFulfillmentSnapshot>> 모의증거기록Async(string attemptId, FulfillmentMockEvidenceRequest request, CancellationToken ct = default);
    Task<int> 대기처리Async(int take = 100, CancellationToken ct = default);
}
