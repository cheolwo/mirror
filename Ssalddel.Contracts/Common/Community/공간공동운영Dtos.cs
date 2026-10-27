namespace Ssalddel.Contracts.Common.Community;

/// <summary>공간 공동운영의 제안·관심·조건 합의이며 근무·계약·공간 사용 권한을 부여하지 않습니다.</summary>
public static class 공간공동운영Routes
{
    public const string Api = "api/v1/common/space-cooperations";
    public const string Home = "/community/space-cooperations";
}

public static class 공간공동운영역할Codes
{
    public const string SpaceProvider = "space-provider";
    public const string Supplier = "supplier";
    public const string Driver = "driver";
    public const string Resident = "resident";
    public const string Picker = "picker";
    public const string Coordinator = "coordinator";
    public const string InboundInspector = "inbound-inspector";
    public static IReadOnlyList<string> All { get; } = [SpaceProvider, Supplier, Driver, Resident, Picker, Coordinator, InboundInspector];
    public static bool IsKnown(string? value) => All.Contains(value ?? "", StringComparer.Ordinal);
}

public static class 공간공동운영명령Codes
{
    public const string Join = "join";
    public const string AcceptRole = "accept-role";
    public const string Withdraw = "withdraw";
    public const string IntroductionConsent = "introduction-consent";
    public const string AcceptOperator = "accept-operator";
    public const string ReleaseOperator = "release-operator";
    public const string UpdateTerms = "update-terms";
    public const string AgreeTerms = "agree-terms";
    public const string CreateCycle = "create-cycle";
    public const string LinkReference = "link-reference";
    public const string UnlinkReference = "unlink-reference";
    public const string StartCycle = "start-cycle";
    public const string CompleteCycle = "complete-cycle";
    public const string Pause = "pause";
    public const string Resume = "resume";
    public const string Close = "close";
    public const string ProposalVisibility = "proposal-visibility";
    public const string ProposeOperatorHandover = "propose-operator-handover";
    public const string AcceptOperatorHandover = "accept-operator-handover";
}

public sealed class 공간공동운영조건Dto
{
    public string Title { get; set; } = "";
    public string Purpose { get; set; } = "";
    public string PublicNeighborhoodRegionKey { get; set; } = "";
    public string SpaceUse { get; set; } = "";
    public string AllowedGoods { get; set; } = "";
    public string Capacity { get; set; } = "";
    public string OperatingHours { get; set; } = "";
    public string CostAndCompensation { get; set; } = "";
    public string Responsibilities { get; set; } = "";
    public string StopAndReturnPlan { get; set; } = "";
    public IReadOnlyList<string> RequiredRoleCodes { get; set; } =
        [공간공동운영역할Codes.SpaceProvider, 공간공동운영역할Codes.Supplier, 공간공동운영역할Codes.Driver, 공간공동운영역할Codes.Resident];
}

public sealed class 공간공동운영생성Request
{
    public Guid ClientRequestId { get; set; }
    public 공간공동운영조건Dto Terms { get; set; } = new();
    public string RoleCode { get; set; } = 공간공동운영역할Codes.Coordinator;
    public bool ConfirmNonBinding { get; set; }
    public bool PublishProposal { get; set; }
    public bool IntroductionConsented { get; set; }
    public string PublicDisplayName { get; set; } = "";
}

public sealed class 공간공동운영명령Request
{
    public Guid ClientRequestId { get; set; }
    public long ExpectedRevision { get; set; }
    public string Action { get; set; } = "";
    public string? RoleCode { get; set; }
    public string? TargetUserId { get; set; }
    public bool ConfirmNonBinding { get; set; }
    public bool? Consented { get; set; }
    public string? PublicDisplayName { get; set; }
    public long? ExpectedTermsRevision { get; set; }
    public 공간공동운영조건Dto? Terms { get; set; }
    public string? CycleId { get; set; }
    public 공간공동운영전문참조Dto? Reference { get; set; }
    public string? ResultNote { get; set; }
}

/// <summary>사용자가 선택한 참조는 검사 요청일 뿐 실제 상태나 권한의 근거가 아닙니다.</summary>
public sealed class 공간공동운영전문참조Dto
{
    public string KindCode { get; set; } = "";
    public string ReferenceId { get; set; } = "";
    public string? ParentId { get; set; }
    public long? ExpectedRevision { get; set; }
}

public sealed class 공간공동운영연결후보Dto
{
    public 공간공동운영전문참조Dto Reference { get; set; } = new();
    public string Label { get; set; } = "";
}

public sealed class 공간공동운영참여Dto
{
    public string UserId { get; set; } = "";
    public string RoleCode { get; set; } = "";
    public string ParticipationStateCode { get; set; } = "interested";
    public bool IsMe { get; set; }
    public bool IntroductionConsented { get; set; }
    public string PublicDisplayName { get; set; } = "";
    public long AgreedTermsRevision { get; set; }
}

public sealed class 공간공동운영회차Dto
{
    public string CycleId { get; set; } = "";
    public int Number { get; set; }
    public long TermsRevision { get; set; }
    public string StatusCode { get; set; } = "prepared";
    public IReadOnlyList<공간공동운영전문참조Dto> References { get; set; } = [];
    public string ResultNote { get; set; } = "";
    public DateTimeOffset? StartedAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public bool SourceCompletionVerified { get; set; }
}

public sealed class 공간공동운영원천상태Dto
{
    public string KindCode { get; set; } = "";
    public string ReferenceId { get; set; } = "";
    public string StatusCode { get; set; } = "unverified";
    public string Label { get; set; } = "";
    public bool Verified { get; set; }
    public bool Completed { get; set; }
    public long? VerifiedRevision { get; set; }
    public bool RequiresAttention { get; set; }
    public string NextActionLabel { get; set; } = "";
    public string Href { get; set; } = "";
}

public sealed class 공간공동운영실행점검Dto
{
    /// <summary>실제로 점검한 회차입니다. 다른 회차의 준비 상태로 전파하지 않습니다.</summary>
    public string CycleId { get; set; } = "";
    public long CycleTermsRevision { get; set; }
    public DateTimeOffset AsOfUtc { get; set; }
    public bool CanStart { get; set; }
    public bool CanComplete { get; set; }
    public IReadOnlyList<string> Blockers { get; set; } = [];
    public IReadOnlyList<공간공동운영준비보류Dto> Issues { get; set; } = [];
    public IReadOnlyList<공간공동운영원천상태Dto> Sources { get; set; } = [];
}

/// <summary>기존 회차 시작 관문이 확인한 읽기 전용 보류 근거입니다. 실제 담당 배정·근무·단계 미완료를 추정하지 않습니다.</summary>
public sealed class 공간공동운영준비보류Dto
{
    public string Code { get; set; } = "";
    public string ScopeCode { get; set; } = "";
    public string[] StageKeys { get; set; } = [];
    public string[] RoleCodes { get; set; } = [];
    public string Message { get; set; } = "";
}

/// <summary>현재 참여 상태와 회차별 전문 원장 재조회의 비공개 결과입니다. 실제 근무·효율·이익·지급 집계가 아닙니다.</summary>
public sealed class 공간공동운영변화Dto
{
    public DateTimeOffset AsOfUtc { get; set; }
    public string ExecutionMode { get; set; } = "";
    public bool MatchesCurrentExecutionMode { get; set; }
    public long TermsRevision { get; set; }
    /// <summary>현재 관심 역할이 있고 수락 역할은 없는 고유 계정 수입니다. 철회된 역할은 제외합니다.</summary>
    public int InterestedParticipantCount { get; set; }
    /// <summary>현재 수락 역할이 하나 이상인 고유 계정 수이며 과거 회차의 실제 수행 인원이 아닙니다.</summary>
    public int AcceptedParticipantCount { get; set; }
    public IReadOnlyList<string> AcceptedRoleCodes { get; set; } = [];
    /// <summary>그 역할을 현재 수락한 모든 참여자가 현 조건에 동의한 역할입니다.</summary>
    public IReadOnlyList<string> CurrentTermsAgreedRoleCodes { get; set; } = [];
    /// <summary>현재 수락자 중 현 조건에 아직 동의하지 않은 참여자가 있는 역할입니다.</summary>
    public IReadOnlyList<string> PendingTermsAgreementRoleCodes { get; set; } = [];
    public IReadOnlyList<string> MissingRoleCodes { get; set; } = [];
    public IReadOnlyList<공간공동운영회차변화Dto> Cycles { get; set; } = [];
}

/// <summary>각 회차의 연결을 조회자 권한으로 다시 읽은 결과입니다. 묶음·주문·입고를 합산한 실적 건수가 아닙니다.</summary>
public sealed class 공간공동운영회차변화Dto
{
    public string CycleId { get; set; } = "";
    public int Number { get; set; }
    public long TermsRevision { get; set; }
    public string StatusCode { get; set; } = "";
    public DateTimeOffset? StartedAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    /// <summary>저장 당시의 마감 근거 확인 기록이며 현재 원천 재확인 결과와 구분합니다.</summary>
    public bool SourceCompletionRecorded { get; set; }
    /// <summary>종류와 ID로 구분한 연결 원장의 수이며 거래·상품·실제 작업 건수가 아닙니다.</summary>
    public int LinkedSourceCount { get; set; }
    public int DuplicateReferenceCount { get; set; }
    public int UnverifiedSourceCount { get; set; }
    public int AttentionSourceCount { get; set; }
    /// <summary>해당 회차에 저장된 연결만 포함합니다. 정산의 Completed는 지급 준비이며 실제 지급이 아닙니다.</summary>
    public IReadOnlyList<공간공동운영원천상태Dto> Sources { get; set; } = [];
}

public sealed class 공간공동운영상세Response
{
    public string LedgerId { get; set; } = "";
    public long Revision { get; set; }
    public long TermsRevision { get; set; }
    public string ExecutionMode { get; set; } = "";
    public string StatusCode { get; set; } = "gathering";
    public string OwnerUserId { get; set; } = "";
    public string? OperatorUserId { get; set; }
    public string? PendingOperatorUserId { get; set; }
    public bool IsOwner { get; set; }
    public bool IsOperator { get; set; }
    public bool PublishProposal { get; set; }
    public bool IdempotentReplay { get; set; }
    public 공간공동운영조건Dto Terms { get; set; } = new();
    public IReadOnlyList<공간공동운영참여Dto> Participants { get; set; } = [];
    public IReadOnlyList<공간공동운영회차Dto> Cycles { get; set; } = [];
    public IReadOnlyList<string> MissingRoleCodes { get; set; } = [];
    public 공간공동운영실행점검Dto Readiness { get; set; } = new();
    public 공간공동운영변화Dto? SharedOutcomes { get; set; }
    public IReadOnlyList<string> AllowedActions { get; set; } = [];
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class 공간공동운영공개소개Dto
{
    public string DisplayName { get; set; } = "";
    public string RoleCode { get; set; } = "";
    public string PublicNeighborhoodRegionKey { get; set; } = "";
}

public sealed class 공간공동운영공개제안Dto
{
    public string LedgerId { get; set; } = "";
    public long Revision { get; set; }
    public long TermsRevision { get; set; }
    public string Title { get; set; } = "";
    public string Purpose { get; set; } = "";
    public string PublicNeighborhoodRegionKey { get; set; } = "";
    public string NeighborhoodLabel { get; set; } = "";
    public string StatusCode { get; set; } = "";
    public string ExecutionMode { get; set; } = "";
    public IReadOnlyList<string> MissingRoleCodes { get; set; } = [];
    public IReadOnlyList<공간공동운영공개소개Dto> Introductions { get; set; } = [];
}

public sealed class 공간공동운영공개Response
{
    public string ExecutionMode { get; set; } = "";
    public string StatusCode { get; set; } = "recorded";
    public DateTimeOffset AsOfUtc { get; set; }
    public int PublicParticipantCount { get; set; }
    public int SpaceCount { get; set; }
    public int RecruitmentCount { get; set; }
    public int CompletedCycleCount { get; set; }
    public int Page { get; set; } = 1;
    public bool HasMore { get; set; }
    public bool CountsArePartial { get; set; }
    public IReadOnlyList<공간공동운영공개제안Dto> Items { get; set; } = [];
}

public sealed class 공간공동운영내목록Response
{
    public IReadOnlyList<공간공동운영상세Response> Items { get; set; } = [];
}
