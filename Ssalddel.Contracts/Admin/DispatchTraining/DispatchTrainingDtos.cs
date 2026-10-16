namespace Ssalddel.Contracts.Admin.DispatchTraining;

public static class DispatchTrainingRoutes
{
    public const string Api = "verification/dispatch-training";
    public const string Home = "/training/dispatch";
    public const string Decisions = "/training/dispatch/decisions";
}

public static class DispatchTrainingPhaseCodes
{
    public const string Idle = "Idle", Running = "Running", Paused = "Paused", Draining = "Draining", Completed = "Completed", Failed = "Failed";
}

public static class DispatchTrainingCommandCodes
{
    public const string Start = "start", Pause = "pause", Resume = "resume", Stop = "stop", SupportDrivers = "support-drivers";
}

/// <summary>인증 전에 허용하는 응답입니다. 세션·주문·기사·호스트 정보는 포함하지 않습니다.</summary>
public sealed record DispatchTrainingCapabilityDto
{
    public bool Enabled { get; init; }
}

public record DispatchTrainingCommandRequest
{
    public Guid ClientRequestId { get; init; }
    public long ExpectedRevision { get; init; }
    public string? RunId { get; init; }
}

public sealed record DispatchTrainingSupportDriversRequest : DispatchTrainingCommandRequest
{
    public int Count { get; init; } = 1;
}

public sealed record DispatchTrainingCommandResultDto
{
    public Guid ClientRequestId { get; init; }
    public bool Replayed { get; init; }
    public DispatchTrainingSessionDto Session { get; init; } = new();
}

/// <summary>훈련 서버 정본 조회입니다. 실제 고객·기사·연락처·주소·좌표·인증값을 담지 않습니다.</summary>
public sealed record DispatchTrainingSessionDto
{
    public string? RunId { get; init; }
    public long Revision { get; init; }
    public string PhaseCode { get; init; } = DispatchTrainingPhaseCodes.Idle;
    public DateTime ServerTimeUtc { get; init; }
    public DateTime? StartedAtUtc { get; init; }
    public DateTime? GenerationEndsAtUtc { get; init; }
    public int RemainingGenerationSeconds { get; init; }
    public int DurationSeconds { get; init; } = 1800;
    public int OrderIntervalSeconds { get; init; } = 120;
    public int MaximumOrders { get; init; } = 15;
    public int ActiveDriverCount { get; init; }
    public int ActivatedSupportDriverCount { get; init; }
    public int AvailableSupportDriverCount { get; init; }
    public DispatchTrainingCountsDto Counts { get; init; } = new();
    public DispatchTrainingNextEventDto? NextEvent { get; init; }
    public IReadOnlyList<DispatchTrainingDriverDto> Drivers { get; init; } = [];
    public IReadOnlyList<DispatchTrainingIncidentDto> PendingIncidents { get; init; } = [];
    public DispatchTrainingAiResultDto? LatestAiResult { get; init; }
    public IReadOnlyList<string> AllowedCommands { get; init; } = [];
    /// <summary>조회로 응답 유실을 확인하기 위한 이 회차의 처리 요청 번호입니다. 요청 재전송을 지시하지 않습니다.</summary>
    public IReadOnlyList<Guid> AppliedCommandIds { get; init; } = [];
}

public sealed record DispatchTrainingCountsDto
{
    public int GeneratedOrders { get; init; }
    public int WaitingOrders { get; init; }
    public int AssignedOrders { get; init; }
    public int InProgressOrders { get; init; }
    public int CompletedOrders { get; init; }
    public int FailedOrders { get; init; }
    public int PendingIncidents { get; init; }
}

public sealed record DispatchTrainingNextEventDto
{
    public string KindCode { get; init; } = "";
    public DateTime? DueAtUtc { get; init; }
    public string Label { get; init; } = "";
}

public sealed record DispatchTrainingDriverDto
{
    public string DriverId { get; init; } = "";
    public string Label { get; init; } = "";
    public string StatusCode { get; init; } = "";
    public bool IsSupport { get; init; }
    public int ActiveWorkCount { get; init; }
    public int CompletedWorkCount { get; init; }
}

public sealed record DispatchTrainingIncidentDto
{
    public string IncidentId { get; init; } = "";
    public string? OrderId { get; init; }
    public string KindCode { get; init; } = "";
    public string StatusCode { get; init; } = "";
    public string Message { get; init; } = "";
    public DateTime RecordedAtUtc { get; init; }
}

public sealed record DispatchTrainingAiResultDto
{
    public string StatusCode { get; init; } = "";
    public string? RuleDriverId { get; init; }
    public string? ShadowDriverId { get; init; }
    public bool? SameChoice { get; init; }
    public long? ElapsedMilliseconds { get; init; }
    public string? ReasonCode { get; init; }
    public string? ModelName { get; init; }
}

public sealed record DispatchTrainingDecisionDto
{
    public string DecisionId { get; init; } = "";
    public string? OrderId { get; init; }
    public DateTime RecordedAtUtc { get; init; }
    public string RuleStatusCode { get; init; } = "";
    public string? RuleDriverLabel { get; init; }
    public DispatchTrainingAiResultDto? AiResult { get; init; }
    public string Summary { get; init; } = "";
    public IReadOnlyList<DispatchTrainingDecisionCandidateDto> Candidates { get; init; } = [];
}

/// <summary>실제 판단 trace에서 투영한 익명 후보 근거입니다. 미확인 수치를 0으로 대체하지 않습니다.</summary>
public sealed record DispatchTrainingDecisionCandidateDto
{
    public string Token { get; init; } = "";
    public string DriverLabel { get; init; } = "";
    public string EligibilityCode { get; init; } = "";
    public double? DistanceKm { get; init; }
    public double? BaseScore { get; init; }
    public double? AgingScore { get; init; }
    public double? TotalScore { get; init; }
}

public sealed record DispatchTrainingDecisionsResponse
{
    public string? RunId { get; init; }
    public long Revision { get; init; }
    public IReadOnlyList<DispatchTrainingDecisionDto> Items { get; init; } = [];
}

public sealed record DispatchTrainingIncidentsResponse
{
    public string? RunId { get; init; }
    public long Revision { get; init; }
    public IReadOnlyList<DispatchTrainingIncidentDto> Items { get; init; } = [];
}
