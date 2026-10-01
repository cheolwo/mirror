namespace Hongdal.Contracts.Common.Community;

public static class CommunityLedgerAccessRoleCodes
{
    public const string CustomsBroker = "CustomsBroker";
}

public static class CommunityLedgerNodeViewScopes
{
    public const string RoleOnly = "RoleOnly";
    public const string SelectedNodes = "SelectedNodes";
    public const string EntireDiagram = "EntireDiagram";

    public static bool IsSupported(string? value)
        => value is RoleOnly or SelectedNodes or EntireDiagram;
}

public sealed class CommunityLedgerRoleAccessSettingsResponse
{
    public string LedgerId { get; set; } = string.Empty;
    public bool CanManage { get; set; }
    public long Revision { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public IReadOnlyList<CommunityLedgerRoleAccessNodeResponse> Nodes { get; set; } = [];
    public IReadOnlyList<CommunityLedgerCustomsBrokerCandidateResponse> CustomsBrokers { get; set; } = [];
    public IReadOnlyList<CommunityLedgerRoleGrantResponse> Grants { get; set; } = [];
}

public sealed class CommunityLedgerRoleAccessNodeResponse
{
    public string NodeId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public bool IsCustomsRoleNode { get; set; }
}

public sealed class CommunityLedgerCustomsBrokerCandidateResponse
{
    public string ParticipantId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string OfficeName { get; set; } = string.Empty;
    public string? Region { get; set; }
    public string? SpecialtyMemo { get; set; }
}

public sealed class CommunityLedgerRoleGrantResponse
{
    public string TargetUserId { get; set; } = string.Empty;
    public string TargetDisplayName { get; set; } = string.Empty;
    public string RoleCode { get; set; } = CommunityLedgerAccessRoleCodes.CustomsBroker;
    public bool AccessEnabled { get; set; } = true;
    public string ViewScope { get; set; } = CommunityLedgerNodeViewScopes.RoleOnly;
    public IReadOnlyList<string> VisibleNodeIds { get; set; } = [];
    public IReadOnlyList<string> EditableNodeIds { get; set; } = [];
    public bool CanCoordinateTransport { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public sealed class CommunityLedgerRoleAccessUpdateRequest
{
    public long? ExpectedRevision { get; set; }
    public IReadOnlyList<CommunityLedgerRoleGrantUpdateRequest> Grants { get; set; } = [];
}

public sealed class CommunityLedgerRoleGrantUpdateRequest
{
    public string TargetUserId { get; set; } = string.Empty;
    public bool AccessEnabled { get; set; } = true;
    public string ViewScope { get; set; } = CommunityLedgerNodeViewScopes.RoleOnly;
    public IReadOnlyList<string> VisibleNodeIds { get; set; } = [];
    public IReadOnlyList<string> EditableNodeIds { get; set; } = [];
    public bool CanCoordinateTransport { get; set; }
}
