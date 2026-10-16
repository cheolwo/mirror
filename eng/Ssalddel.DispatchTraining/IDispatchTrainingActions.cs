using Ssalddel.Contracts.Admin.DispatchTraining;

namespace Ssalddel.DispatchTraining;

/// <summary>All business mutations use the existing authenticated APIs. These methods never override a dispatch decision.</summary>
public interface IDispatchTrainingActions
{
    /// <summary>The stable request ID is an idempotency key. The actor re-queries its canonical result before any retry.</summary>
    Task<DispatchTrainingSubmissionResult> SubmitOrderAsync(string runId, int index, Guid requestId, CancellationToken ct = default);
    /// <summary>Reconcile uncertain requests and continue independent work. Business status comes only from canonical API reads.</summary>
    Task<DispatchTrainingTickResult> TickAsync(string runId, CancellationToken ct = default);
    /// <summary>Called only by an explicit mobile command; never by a shortage or timer.</summary>
    Task<DispatchTrainingSupportResult> ActivateSupportDriversAsync(string runId, Guid requestId, int count, CancellationToken ct = default);
}

public static class DispatchTrainingActionOutcomes
{
    public const string Confirmed = "Confirmed", Unknown = "Unknown", Rejected = "Rejected";
}

public sealed record DispatchTrainingSubmissionResult(string OutcomeCode, string? CanonicalOrderId = null,
    string? ErrorCode = null, string? Message = null);

public sealed record DispatchTrainingSupportResult(string OutcomeCode, int ActivatedSupportDriverCount = 0,
    string? ErrorCode = null, string? Message = null);

public sealed record DispatchTrainingOrderObservation
{
    public int Index { get; init; }
    public Guid ClientRequestId { get; init; }
    public string? CanonicalOrderId { get; init; }
    public string StateCode { get; init; } = "Unknown";
}

public sealed record DispatchTrainingTickResult
{
    public bool CanonicalReadSucceeded { get; init; } = true;
    public int WaitingOrders { get; init; }
    public int AssignedOrders { get; init; }
    public int InProgressOrders { get; init; }
    public int CompletedOrders { get; init; }
    public int FailedOrders { get; init; }
    public int UnfinishedOrders { get; init; }
    public int ActivatedSupportDriverCount { get; init; }
    public IReadOnlyList<DispatchTrainingOrderObservation> Orders { get; init; } = [];
    public IReadOnlyList<DispatchTrainingDriverDto> Drivers { get; init; } = [];
    public IReadOnlyList<DispatchTrainingIncidentDto> Incidents { get; init; } = [];
    public IReadOnlyList<DispatchTrainingDecisionDto> Decisions { get; init; } = [];
    public DispatchTrainingAiResultDto? LatestAiResult { get; init; }
}
