namespace Hongdal.Contracts.Common.Community;

public static class CommunityLedgerBlockResponsibilityTypes
{
    public const string Primary = "Primary";
    public const string Collaborator = "Collaborator";
    public const string Reviewer = "Reviewer";

    public static bool IsSupported(string? value)
        => value is Primary or Collaborator or Reviewer;

    public static string DisplayName(string? value)
        => value switch
        {
            Collaborator => "협업",
            Reviewer => "검토",
            _ => "주담당"
        };
}

public sealed class CommunityLedgerBlockAssignmentSettingsResponse
{
    public string LedgerId { get; set; } = string.Empty;
    public long Revision { get; set; }
    public string BlockId { get; set; } = string.Empty;
    public string BlockTitle { get; set; } = string.Empty;
    public bool CanManage { get; set; }
    public IReadOnlyList<CommunityLedgerBlockAssigneeCandidateResponse> Candidates { get; set; } = [];
    public IReadOnlyList<PlatformCommunityLedgerBlockAssigneeResponse> Assignments { get; set; } = [];
}

public sealed class CommunityLedgerBlockAssigneeCandidateResponse
{
    public string UserId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string RoleLabel { get; set; } = string.Empty;
    public string ParticipationState { get; set; } = string.Empty;
}

public sealed class CommunityLedgerBlockAssignmentUpdateRequest
{
    public long? ExpectedRevision { get; set; }
    public IReadOnlyList<CommunityLedgerBlockAssigneeUpdateRequest> Assignments { get; set; } = [];
}

public sealed class CommunityLedgerBlockAssigneeUpdateRequest
{
    public string UserId { get; set; } = string.Empty;
    public string ResponsibilityType { get; set; } = CommunityLedgerBlockResponsibilityTypes.Primary;
}
