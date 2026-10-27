using Ssalddel.Contracts.Admin.DispatchTraining;

namespace Ssalddel.DispatchTraining.Experiences;

public sealed record DispatchExperienceImportResult(string RunId, long Revision, string SnapshotSha256,
    bool Reused, int OrderCount);

public sealed record DispatchExperienceSummary
{
    public int SnapshotCount { get; init; }
    public int RunCount { get; init; }
    public int TotalSlotCount { get; init; }
    public int ScheduledSlotCount { get; init; }
    public int SkippedSlotCount { get; init; }
    public IReadOnlyList<DispatchExperienceOrder> Orders { get; init; } = [];
    public int UnlinkedDecisionCount { get; init; }
    public int UnlinkedIncidentCount { get; init; }
    public int UnlinkedEventCount { get; init; }
    public bool Synthetic { get; init; } = true;
    public bool OperationalDispatchEnabled { get; init; }
    public bool TrainingEligible { get; init; }
    public bool WeightsTrained { get; init; }
}

public sealed record DispatchExperienceOrder
{
    public string ExperienceId { get; init; } = "";
    public string RunId { get; init; } = "";
    public int OrderIndex { get; init; }
    public Guid ClientRequestId { get; init; }
    public string? CanonicalOrderId { get; init; }
    public long Revision { get; init; }
    public string SnapshotSha256 { get; init; } = "";
    public string SubmissionStatusCode { get; init; } = "";
    public string ObservedStateCode { get; init; } = "Unknown";
    public bool CanonicalReadSucceeded { get; init; }
    public IReadOnlyList<DispatchExperienceDecision> Decisions { get; init; } = [];
    public IReadOnlyList<DispatchExperienceEvent> Events { get; init; } = [];
    public IReadOnlyList<DispatchExperienceIncident> Incidents { get; init; } = [];
    public IReadOnlyList<string> MissingEvidence { get; init; } = [];
    public long ReviewRevision { get; init; }
    public string? ReviewOutcome { get; init; }
    public string? ReviewSnapshotSha256 { get; init; }
    public bool ReviewCurrent { get; init; }
    public bool Synthetic { get; init; } = true;
    public bool TrainingEligible { get; init; }
    public bool WeightsTrained { get; init; }
    public bool OperationalDispatchEnabled { get; init; }
}

public sealed record DispatchExperienceDecision(string DecisionId, DateTime RecordedAtUtc,
    string RuleStatusCode, string? RuleDriverLabel, DispatchTrainingAiResultDto? AiResult,
    IReadOnlyList<DispatchTrainingDecisionCandidateDto> Candidates);
public sealed record DispatchExperienceEvent(long Sequence, DateTime AtUtc, string KindCode,
    Guid? ClientRequestId, string? DetailCode);
public sealed record DispatchExperienceIncident(string IncidentId, string KindCode, string StatusCode, DateTime RecordedAtUtc);

public sealed record DispatchExperienceReviewCommand(Guid RequestId, string ExperienceId,
    string SnapshotSha256, long ExpectedReviewRevision, string Outcome, string? Note);
public sealed record DispatchExperienceReviewResult(long ReviewRevision, bool Reused);

public sealed class DispatchExperienceException(string code) : IOException(code)
{
    public string Code { get; } = code;
}
