namespace Ssalddel.Contracts.Common.Community;

public static class 공간공동운영예약Routes
{
    public const string Api = 공간공동운영Routes.Api + "/{ledgerId}/work-reservations";
}

public static class 공간공동운영예약종류Codes
{
    public const string Supply = "supply", Purchase = "purchase", Inbound = "inbound", Picking = "picking", Handoff = "handoff";
    public static IReadOnlyList<string> All { get; } = [Supply, Purchase, Inbound, Picking, Handoff];
}

/// <summary>현재 참여자 사이의 시간 약속입니다. 실제 주문·재고 예약·근무·배차 또는 지급 의무가 아닙니다.</summary>
public sealed class 공간공동운영예약조건Dto
{
    public string CycleId { get; set; } = "";
    public string KindCode { get; set; } = "";
    public DateTimeOffset StartsAtUtc { get; set; }
    public DateTimeOffset EndsAtUtc { get; set; }
    public int Capacity { get; set; }
    /// <summary>취소·거절된 같은 회차·종류 약속의 명시적 대체입니다. 제안 생성 때만 지정하며 기존 이력을 삭제하지 않습니다.</summary>
    public Guid? ReplacesBookingId { get; set; }
    /// <summary>운영 담당자가 회차 시작에 필수로 지정한 약속입니다. 일반 참여자의 시간 제안은 기본적으로 회차 시작을 막지 않습니다.</summary>
    public bool RequiredForCycle { get; set; }
    public IReadOnlyList<공간공동운영예약당사자Request> Parties { get; set; } = [];
    public IReadOnlyList<공간공동운영전문참조Dto> References { get; set; } = [];
}

public sealed class 공간공동운영예약당사자Request
{
    public string UserId { get; set; } = "";
    public string RoleCode { get; set; } = "";
}

public sealed class 공간공동운영예약명령Request
{
    public Guid ClientRequestId { get; set; }
    public long ExpectedRevision { get; set; }
    public long ExpectedBookingRevision { get; set; }
    public long ExpectedTermsRevision { get; set; }
    public string Action { get; set; } = "";
    public 공간공동운영예약조건Dto? Terms { get; set; }
    public bool ConfirmNonBinding { get; set; }
    public string? ImpactDigest { get; set; }
    public string? Reason { get; set; }
}

public sealed class 공간공동운영예약당사자Dto
{
    public string UserId { get; set; } = "";
    public string RoleCode { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public bool IsMe { get; set; }
    public string PartyStateCode { get; set; } = "invited";
    public DateTimeOffset? AcceptedAtUtc { get; set; }
}

public sealed class 공간공동운영예약Dto
{
    public string LedgerId { get; set; } = "";
    public long ParentRevision { get; set; }
    public Guid BookingId { get; set; }
    public long Revision { get; set; }
    public long CooperationTermsRevision { get; set; }
    public 공간공동운영예약조건Dto Terms { get; set; } = new();
    public string StatusCode { get; set; } = "proposed";
    public IReadOnlyList<공간공동운영예약당사자Dto> Parties { get; set; } = [];
    public IReadOnlyList<string> AllowedActions { get; set; } = [];
    public IReadOnlyList<string> ReadinessBlockers { get; set; } = [];
    public IReadOnlyList<공간공동운영예약변경Dto> History { get; set; } = [];
    public bool ProfessionalReviewReady { get; set; }
    public bool NonBinding { get; set; } = true;
    public bool GrantsHrAuthority { get; set; }
    public bool WorkCompleted { get; set; }
    public bool InventoryReserved { get; set; }
    public bool FinancialObligationConfirmed { get; set; }
    public bool ExternalSchedulesVerified { get; set; }
    public bool ProfessionalCleanupRequired { get; set; }
    public string ConflictScope { get; set; } = "this-integrated-ledger";
    public bool Replayed { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class 공간공동운영예약변경Dto
{
    public Guid EventId { get; set; }
    public string Action { get; set; } = "";
    public bool IsMine { get; set; }
    public string? Reason { get; set; }
    public DateTimeOffset OccurredAtUtc { get; set; }
}

public sealed class 공간공동운영예약알림Dto
{
    public Guid EventId { get; set; }
    public Guid BookingId { get; set; }
    public string Action { get; set; } = "";
    public string Message { get; set; } = "";
    public DateTimeOffset OccurredAtUtc { get; set; }
}

public sealed class 공간공동운영예약목록Response
{
    public string LedgerId { get; set; } = "";
    public long Revision { get; set; }
    public long TermsRevision { get; set; }
    public string ExecutionMode { get; set; } = "";
    public DateTimeOffset AsOfUtc { get; set; }
    public bool CanPropose { get; set; }
    public string ConflictScope { get; set; } = "this-integrated-ledger";
    public bool ExternalSchedulesVerified { get; set; }
    public IReadOnlyList<공간공동운영예약Dto> Items { get; set; } = [];
    public IReadOnlyList<공간공동운영예약알림Dto> Notices { get; set; } = [];
}

public sealed class 공간공동운영예약후보Response
{
    public string LedgerId { get; set; } = "";
    public long Revision { get; set; }
    public long TermsRevision { get; set; }
    public IReadOnlyList<공간공동운영예약당사자Dto> Participants { get; set; } = [];
    public IReadOnlyList<공간공동운영예약회차후보Dto> Cycles { get; set; } = [];
}

public sealed class 공간공동운영예약회차후보Dto
{
    public string CycleId { get; set; } = "";
    public int Number { get; set; }
    public long TermsRevision { get; set; }
    public string StatusCode { get; set; } = "";
    public IReadOnlyList<공간공동운영연결후보Dto> References { get; set; } = [];
}

/// <summary>읽기 전용 영향표. 비용·기대 손실·배상·금전 정산을 산출하거나 전문 원장을 취소하지 않습니다.</summary>
public sealed class 공간공동운영예약취소영향Dto
{
    public string LedgerId { get; set; } = "";
    public Guid BookingId { get; set; }
    public long ParentRevision { get; set; }
    public long BookingRevision { get; set; }
    public long TermsRevision { get; set; }
    public DateTimeOffset AsOfUtc { get; set; }
    public string Digest { get; set; } = "";
    public bool CanCancel { get; set; }
    public bool ProfessionalCleanupRequired { get; set; }
    public bool CostsVerified { get; set; }
    public bool ExpectedLossVerified { get; set; }
    public bool FinancialObligationConfirmed { get; set; }
    public bool ProfessionalMutationPerformed { get; set; }
    public IReadOnlyList<공간공동운영원천상태Dto> Sources { get; set; } = [];
    public IReadOnlyList<string> Warnings { get; set; } = [];
}
