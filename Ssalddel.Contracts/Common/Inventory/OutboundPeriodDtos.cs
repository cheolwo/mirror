namespace Ssalddel.Contracts.Common.Inventory;

/// <summary>선택 구간에 접수된 출고와 이전 접수 미완료를 현재 원장으로 집계합니다.</summary>
public sealed class OutboundPeriodResponse
{
    public long WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public DateTime PeriodStartUtc { get; set; }
    public DateTime PeriodEndUtc { get; set; }
    public DateTime ObservedAtUtc { get; set; }
    public OutboundPeriodTotals Current { get; set; } = new();
    public OutboundPeriodTotals Carryover { get; set; } = new();
    public OutboundPeriodTotals Today { get; set; } = new();
    public IReadOnlyList<OutboundPeriodDay> PreviousDays { get; set; } = [];
    public OutboundPeriodWorkforce Workforce { get; set; } = new();
    public OutboundPeriodWorkflow? Workflow { get; set; }
    public IReadOnlyList<OutboundPeriodItem> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public bool HasNextPage => ((long)Page + 1) * PageSize < TotalCount;
}

public static class OutboundWorkflowStageCodes
{
    public const string Review = "review";
    public const string Picking = "picking";
    public const string Packing = "packing";
    public const string Handoff = "handoff";
    public static IReadOnlyList<string> All { get; } = [Review, Picking, Packing, Handoff];
}

public sealed class OutboundPeriodWorkflow
{
    public string SelectedStage { get; set; } = OutboundWorkflowStageCodes.Review;
    public IReadOnlyList<OutboundPeriodStageSummary> Stages { get; set; } = [];
    public IReadOnlyList<OutboundPeriodGroup> Groups { get; set; } = [];
    public int TotalGroups { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public bool HasNextPage => ((long)Page + 1) * PageSize < TotalGroups;
    public string? SelectedGroupKey { get; set; }
    public OutboundPeriodGroup? SelectedGroup { get; set; }
}

public sealed class OutboundPeriodStageSummary
{
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int SourcePlanCount { get; set; }
    public int TaskCount { get; set; }
    public int RecordedBatchCount { get; set; }
    public int SingleWorkCount { get; set; }
    public int UnconfirmedGroupCount { get; set; }
    public int CancelledCount { get; set; }
    public long SourceQuantity { get; set; }
    public long WorkQuantity { get; set; }
    public long? CompletedQuantity { get; set; }
}

public sealed class OutboundPeriodGroup
{
    public string Key { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int SourcePlanCount { get; set; }
    public int TaskCount { get; set; }
    public long Quantity { get; set; }
    public long? CompletedQuantity { get; set; }
    public bool NeedsReview { get; set; }
    public string Reason { get; set; } = string.Empty;
    public bool MembershipComplete { get; set; }
    public bool HasOutsideScopeMembers { get; set; }
    public int MemberCount { get; set; }
    public int MemberPage { get; set; }
    public int MemberPageSize { get; set; }
    public bool HasNextMemberPage => MemberPageSize > 0 && ((long)MemberPage + 1) * MemberPageSize < MemberCount;
    public IReadOnlyList<OutboundPeriodMember> Members { get; set; } = [];
}

public sealed class OutboundPeriodMember
{
    public long OutboundPlanId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? TaskKey { get; set; }
    public long Quantity { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsCarryover { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string WorkerDisplayName { get; set; } = string.Empty;
    public string? PreviousTaskKey { get; set; }
    public string? NextTaskKey { get; set; }
    public bool CanOpenReview { get; set; }
}

public sealed class OutboundPeriodTotals
{
    public int PlanCount { get; set; }
    public long ReceivedQuantity { get; set; }
    public long PickedQuantity { get; set; }
    public long PackedQuantity { get; set; }
    public long HandedOffQuantity { get; set; }
    public int NeedsReviewCount { get; set; }
}

public sealed class OutboundPeriodDay
{
    public string DateKst { get; set; } = string.Empty;
    public OutboundPeriodTotals Totals { get; set; } = new();
}

/// <summary>등록·배정 원장의 수입니다. 실제 출근과 작업 가능 여부를 나타내지 않습니다.</summary>
public sealed class OutboundPeriodWorkforce
{
    public int RegisteredWorkerCount { get; set; }
    public int AssignedWorkerCount { get; set; }
    public int OpenTaskCount { get; set; }
    public bool AttendanceConfirmed { get; set; }
}

public sealed class OutboundPeriodItem
{
    public long OutboundPlanId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string OrderReference { get; set; } = string.Empty;
    public long Quantity { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public string OutboundStatus { get; set; } = string.Empty;
    public bool IsCarryover { get; set; }
    public bool CanOpenReview { get; set; } = true;
    public long PickedQuantity { get; set; }
    public long PackedQuantity { get; set; }
    public long HandedOffQuantity { get; set; }
    public IReadOnlyList<string> WorkerDisplayNames { get; set; } = [];
    public string NextActor { get; set; } = string.Empty;
    public string WaitingReason { get; set; } = string.Empty;
    public bool NeedsReview { get; set; }
    public string ReviewReason { get; set; } = string.Empty;
}
