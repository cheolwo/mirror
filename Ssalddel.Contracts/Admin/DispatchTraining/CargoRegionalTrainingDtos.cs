namespace Ssalddel.Contracts.Admin.DispatchTraining;

/// <summary>학습 준비용 비공개 사례 검토. 운영 배차·모델 가중치 변경 계약이 아니다.</summary>
public interface ICargoRegionalTrainingReviewClient
{
    Task<CargoTrainingWorkspace> LoadAsync(CancellationToken cancellationToken = default);
    Task<CargoTrainingWorkspace> RecordAsync(CargoTrainingReviewCommand command, CancellationToken cancellationToken = default);
    Task<CargoTrainingWorkspace> CompareAsync(string caseId, string inputSha256, CancellationToken cancellationToken = default);
}

public sealed class CargoTrainingReviewException(string code) : Exception(code)
{
    public string Code { get; } = code;
}

public sealed record CargoTrainingRegion(string LegalDongCode, string DisplayName);

public sealed record CargoTrainingCandidate
{
    public string Id { get; init; } = "";
    public string Vehicle { get; init; } = "오토바이";
    public CargoTrainingRegion CurrentRegion { get; init; } = new("", "미확인");
    public CargoTrainingRegion AvailableRegion { get; init; } = new("", "미확인");
    public decimal AvailableAfterMinutes { get; init; }
    public decimal? TravelAfterAvailableMinutes { get; init; }
    public decimal IdleWaitMinutes { get; init; }
    public decimal CapacityKg { get; init; } = 10;
    public decimal CapacityLiters { get; init; } = 25;
    public decimal LocationAgeMinutes { get; init; }
    public bool Receiving { get; init; } = true;
    public bool CommitmentsFeasible { get; init; } = true;
    public bool ReturnDirection { get; init; }
    public bool Eligible { get; init; }
    public string Eligibility { get; init; } = "unknown";
    public decimal? PickupEtaMinutes { get; init; }
    public decimal Score { get; init; }
    public Dictionary<string, decimal> ScoreParts { get; init; } = [];
    public List<string> ExclusionReasons { get; init; } = [];
    public CargoTrainingPlan? Plan { get; init; }
    public bool IsFeasibleChoice => Eligible && (Plan is null || Plan.Verified && Plan.Feasible);
}

/// <summary>주소·좌표를 포함하지 않는 검토용 수행 순서. 실제 운영 계획과 합성 계획의 출처를 구분한다.</summary>
public sealed record CargoTrainingPlanStop(string RequestId, string Kind, int Sequence,
    decimal? ArrivalMinutes, decimal? HandlingMinutes, decimal? FinishMinutes, decimal? DeadlineMinutes);

public sealed record CargoTrainingLoadSegment(int Sequence, string RequestId, string Kind,
    IReadOnlyList<string> LoadedRequestIds, decimal WeightKg, decimal VolumeCbm, int Pallets,
    decimal NonStackableAreaM2, IReadOnlyList<string> CargoInputSha256);

public sealed record CargoTrainingPlan
{
    public string PlanId { get; init; } = "";
    public string DriverId { get; init; } = "";
    public string Source { get; init; } = "synthetic";
    public string InputSha256 { get; init; } = "";
    public string PlanSha256 { get; init; } = "";
    public bool Verified { get; init; }
    public bool Feasible { get; init; }
    public string ReasonCode { get; init; } = "CargoPlan.NotVerified";
    public string TimingEvidence { get; init; } = "missing";
    public string LoadingEvidence { get; init; } = "missing";
    public List<string> RequestIds { get; init; } = [];
    public List<CargoTrainingPlanStop> Stops { get; init; } = [];
    public List<CargoTrainingLoadSegment> Segments { get; init; } = [];
    public decimal? TotalMinutes { get; init; }
    public decimal? TotalDistanceKm { get; init; }
    public decimal? AgreedDriverCompensation { get; init; }
    public string SearchScope { get; init; } = "기존 순서를 유지한 삽입 후보 · 전체 최적성 미확인";
}

public sealed record CargoTrainingDecision
{
    public string? RunId { get; init; }
    public string Status { get; init; } = "not_run";
    public string? CandidateId { get; init; }
    public List<string> ReasonCodes { get; init; } = [];
    public string Source { get; init; } = "not_run";
    public string? Model { get; init; }
    public string? ModelDigest { get; init; }
    public string? PromptSha256 { get; init; }
    public string? InputSha256 { get; init; }
    public string? ErrorCode { get; init; }
    public long? ElapsedMilliseconds { get; init; }
    public string? RawResponse { get; init; }
    public string? ResponseSha256 { get; init; }
    public bool? StructureValid { get; init; }
    public bool? FactsValid { get; init; }
    public string? PlanSha256 { get; init; }
}

public sealed record CargoTrainingCase
{
    public string CaseId { get; init; } = "";
    public string FamilyId { get; init; } = "";
    public string Split { get; init; } = "review";
    public string Title { get; init; } = "";
    public string Situation { get; init; } = "";
    public string TaskType { get; init; } = "SmallQuick";
    public CargoTrainingRegion PickupRegion { get; init; } = new("", "미확인");
    public CargoTrainingRegion DropoffRegion { get; init; } = new("", "미확인");
    public decimal WeightKg { get; init; } = 3;
    public decimal VolumeLiters { get; init; } = 8;
    public decimal PickupDeadlineMinutes { get; init; } = 15;
    public decimal Fare { get; init; } = 5000;
    public string InputSha256 { get; init; } = "";
    public List<CargoTrainingCandidate> Candidates { get; init; } = [];
    public CargoTrainingDecision Engine { get; init; } = new();
    public CargoTrainingDecision Ai { get; init; } = new();
    public CargoTrainingReview? Review { get; init; }
    public string? PlanSetSha256 { get; init; }
    public string Source { get; init; } = "synthetic";
    public CargoTrainingDecision? CodexOpinion { get; init; }
    public long ReviewRevision => Review?.Revision ?? 0;
    public bool ReviewCurrent => Review is not null && Review.InputSha256 == InputSha256
        && Review.PlanSetSha256 == PlanSetSha256 && Review.ObservedModelRunId == Ai.RunId;
}

public sealed record CargoTrainingReviewCommand
{
    public string RequestId { get; init; } = "";
    public string DatasetSha256 { get; init; } = "";
    public string CaseId { get; init; } = "";
    public string InputSha256 { get; init; } = "";
    public long ExpectedRevision { get; init; }
    public string? ObservedModelRunId { get; init; }
    public string Outcome { get; init; } = "defer";
    public List<string> AcceptableCandidateIds { get; init; } = [];
    public bool AcceptHold { get; init; }
    public string Note { get; init; } = "";
    public string? PlanSetSha256 { get; init; }
}

public sealed record CargoTrainingReview
{
    public string RequestId { get; init; } = "";
    public string CaseId { get; init; } = "";
    public string DatasetSha256 { get; init; } = "";
    public string InputSha256 { get; init; } = "";
    public string PolicySha256 { get; init; } = "";
    public string? ObservedModelRunId { get; init; }
    public string? EngineCandidateId { get; init; }
    public long Revision { get; init; }
    public string Outcome { get; init; } = "defer";
    public List<string> AcceptableCandidateIds { get; init; } = [];
    public bool AcceptHold { get; init; }
    public string Note { get; init; } = "";
    public DateTimeOffset RecordedAtUtc { get; init; }
    public string Actor { get; init; } = "local-human-reviewer";
    public bool TrainingEligible { get; init; }
    public string? PlanSetSha256 { get; init; }
}

public sealed record CargoTrainingWorkspace
{
    public string DatasetId { get; init; } = "";
    public string DatasetSha256 { get; init; } = "";
    public string PolicySha256 { get; init; } = "";
    public string RegionSourceSha256 { get; init; } = "";
    public string RegionSnapshotDate { get; init; } = "2026-08-11 수집본 · 기준일 미확인";
    public string DataSource { get; init; } = "가상 주문·기사 / 공식 법정동 코드";
    public List<CargoTrainingCase> Cases { get; init; } = [];
    public bool WeightsTrained { get; init; }
    public bool OperationalDispatchEnabled { get; init; }
    public int TrainingEligibleCount => Cases.Count(c => c.Review?.TrainingEligible == true && c.ReviewCurrent);
    public int ModelComparedCount => Cases.Count(c => c.Ai.Status != "not_run");
}
