namespace Hongdal.Contracts.Common.Community;

public sealed class DiagramRoomJoinRequest
{
    public string RoomId { get; set; } = string.Empty;

    public string CommunityId { get; set; } = string.Empty;

    public string? LedgerId { get; set; }

    public string? DiagramId { get; set; }

    public string DiagramName { get; set; } = string.Empty;

    public string? LedgerTemplateKey { get; set; }

    public DiagramWorkContextDto? WorkContext { get; set; }
}

public sealed class DiagramRoomJoinedResponse
{
    public string RoomId { get; set; } = string.Empty;

    public string ConnectionId { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string DiagramName { get; set; } = string.Empty;

    public string? LedgerId { get; set; }

    public DiagramWorkContextDto? WorkContext { get; set; }

    public DateTime JoinedAtUtc { get; set; }
}

public sealed class 다이어그램대화방목록Response
{
    public IReadOnlyList<다이어그램대화방Response> Items { get; set; } = [];
}

public sealed class 다이어그램대화방Response
{
    public string RoomId { get; set; } = string.Empty;

    public string CommunityId { get; set; } = string.Empty;

    public string ConversationType { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string? LedgerId { get; set; }

    public string? LedgerTemplateKey { get; set; }

    public string? DiagramId { get; set; }

    public string? DiagramName { get; set; }

    public DiagramWorkContextDto? WorkContext { get; set; }

    public IReadOnlyList<다이어그램대화방참여자Response> Participants { get; set; } = [];

    public string? LastMessageId { get; set; }

    public string? LastMessageSummary { get; set; }

    public string? LastMessageKind { get; set; }

    public DateTime? LastMessageAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}

public sealed class 다이어그램대화방참여자Response
{
    public string? UserId { get; set; }

    public string DisplayName { get; set; } = "익명 참여자";

    public string RoleLabel { get; set; } = "참여자";

    public string ParticipationState { get; set; } = "참여중";

    public string? LastReadMessageId { get; set; }

    public DateTime? LastReadAtUtc { get; set; }
}

public sealed class 다이어그램대화메시지목록Response
{
    public string RoomId { get; set; } = string.Empty;

    public IReadOnlyList<DiagramChatMessageResponse> Items { get; set; } = [];
}

public sealed class DiagramChatMessageRequest
{
    public string RoomId { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string? DiagramId { get; set; }

    public string? DiagramName { get; set; }

    public string MessageKind { get; set; } = DiagramCollaborationMessageKinds.Text;
}

public sealed class DiagramChatMessageResponse
{
    public string MessageId { get; set; } = string.Empty;

    public string RoomId { get; set; } = string.Empty;

    public string SenderUserId { get; set; } = string.Empty;

    public string SenderDisplayName { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string? DiagramId { get; set; }

    public string? DiagramName { get; set; }

    public string MessageKind { get; set; } = DiagramCollaborationMessageKinds.Text;

    public DateTime SentAtUtc { get; set; }
}

public sealed class DiagramSnapshotShareRequest
{
    public string RoomId { get; set; } = string.Empty;

    public DiagramSnapshotDto Snapshot { get; set; } = new();

    public string? Message { get; set; }
}

public sealed class DiagramSnapshotSharedResponse
{
    public string ShareId { get; set; } = string.Empty;

    public string RoomId { get; set; } = string.Empty;

    public string SenderUserId { get; set; } = string.Empty;

    public string SenderDisplayName { get; set; } = string.Empty;

    public DiagramSnapshotDto Snapshot { get; set; } = new();

    public string? Message { get; set; }

    public DateTime SharedAtUtc { get; set; }
}

public sealed class DiagramWorkActionRequest
{
    public string RoomId { get; set; } = string.Empty;

    public string ActionCode { get; set; } = DiagramWorkActionCodes.OpenWorkScreen;

    public string ActionLabel { get; set; } = string.Empty;

    public string TargetRoute { get; set; } = string.Empty;

    public string? LedgerId { get; set; }

    public string? DiagramId { get; set; }

    public string? NodeId { get; set; }

    public DiagramWorkContextDto? WorkContext { get; set; }
}

public sealed class DiagramWorkActionResponse
{
    public string ActionId { get; set; } = string.Empty;

    public string RoomId { get; set; } = string.Empty;

    public string SenderUserId { get; set; } = string.Empty;

    public string SenderDisplayName { get; set; } = string.Empty;

    public string ActionCode { get; set; } = DiagramWorkActionCodes.OpenWorkScreen;

    public string ActionLabel { get; set; } = string.Empty;

    public string TargetRoute { get; set; } = string.Empty;

    public string? LedgerId { get; set; }

    public string? DiagramId { get; set; }

    public string? NodeId { get; set; }

    public DiagramWorkContextDto? WorkContext { get; set; }

    public DateTime RequestedAtUtc { get; set; }
}

public sealed class DiagramSnapshotDto
{
    public string DiagramId { get; set; } = string.Empty;

    public string DiagramName { get; set; } = string.Empty;

    public string? LedgerId { get; set; }

    public string? LedgerTemplateKey { get; set; }

    public string? WorkflowModeKey { get; set; }

    public IReadOnlyList<DiagramNodeDto> Nodes { get; set; } = [];

    public IReadOnlyList<DiagramEdgeDto> Edges { get; set; } = [];

    public IReadOnlyDictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
}

public static class DiagramOrganizationSourceKindCodes
{
    public const string ManualResearch = "manual-research";

    public const string ThirdPartyLogisticsDirectory = "third-party-logistics-directory";
}

public static class DiagramOrganizationVerificationStatusCodes
{
    public const string VerificationRequired = "verification-required";

    public const string PublicSourceReviewed = "public-source-reviewed";
}

public sealed class DiagramOrganizationReferenceDto
{
    public string ReferenceId { get; set; } = string.Empty;

    public string OrganizationKey { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string RoleLabel { get; set; } = string.Empty;

    public string CountryCode { get; set; } = "ZZ";

    public string OfficialWebsiteUrl { get; set; } = string.Empty;

    public string SourceKindCode { get; set; } = DiagramOrganizationSourceKindCodes.ManualResearch;

    public string SourceReferenceUrl { get; set; } = string.Empty;

    public string DirectoryStatusCode { get; set; } = string.Empty;

    public string PlatformRelationshipStatusCode { get; set; } = string.Empty;

    public string CompanySourceVerificationStatusCode { get; set; } =
        DiagramOrganizationVerificationStatusCodes.VerificationRequired;

    public string RegulatoryVerificationStatusCode { get; set; } = string.Empty;

    public bool IsPlatformPartner { get; set; }

    public bool CanBeSelectedForOperations { get; set; }

    public IReadOnlyList<string> CapabilityCodes { get; set; } = [];
}

public sealed class DiagramNodeDto
{
    public string NodeId { get; set; } = string.Empty;

    public string Kind { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string? GroupLabel { get; set; }

    public string? Description { get; set; }

    public double X { get; set; }

    public double Y { get; set; }

    public string? RelatedRoute { get; set; }

    public IReadOnlyList<DiagramOrganizationReferenceDto> OrganizationReferences { get; set; } = [];

    public IReadOnlyDictionary<string, string> Data { get; set; } = new Dictionary<string, string>();
}

public sealed class DiagramEdgeDto
{
    public string EdgeId { get; set; } = string.Empty;

    public string FromNodeId { get; set; } = string.Empty;

    public string ToNodeId { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string? MeaningCode { get; set; }

    public IReadOnlyDictionary<string, string> Data { get; set; } = new Dictionary<string, string>();
}

public sealed class DiagramWorkContextDto
{
    public string WorkType { get; set; } = string.Empty;

    public string WorkLabel { get; set; } = string.Empty;

    public string? AppKey { get; set; }

    public string? PrimaryRoute { get; set; }

    public string? PrimaryActionLabel { get; set; }

    public IReadOnlyDictionary<string, string> Parameters { get; set; } = new Dictionary<string, string>();
}

public sealed class DiagramLedgerChangedResponse
{
    public string LedgerId { get; set; } = string.Empty;
    public long Revision { get; set; }
    public string State { get; set; } = string.Empty;
    public string? CurrentStep { get; set; }
    public string? NodeId { get; set; }
    public DateTime ChangedAtUtc { get; set; }
}

public static class DiagramLedgerRoomIds
{
    public static string Build(string ledgerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ledgerId);
        return $"community:ledger:{ledgerId.Trim()}:diagram";
    }
}

public static class DiagramCollaborationClientMethods
{
    public const string RoomJoined = "DiagramRoomJoined";

    public const string ParticipantJoined = "DiagramParticipantJoined";

    public const string ParticipantLeft = "DiagramParticipantLeft";

    public const string ReceiveMessage = "ReceiveDiagramMessage";

    public const string ReceiveSnapshot = "ReceiveDiagramSnapshot";

    public const string ReceiveWorkAction = "ReceiveDiagramWorkAction";

    public const string ReceiveLedgerChanged = "ReceiveDiagramLedgerChanged";
}

public static class DiagramCollaborationMessageKinds
{
    public const string Text = "Text";

    public const string DiagramNote = "DiagramNote";

    public const string System = "System";
}

public static class DiagramWorkActionCodes
{
    public const string OpenWorkScreen = "OpenWorkScreen";

    public const string CreateLedgerDraft = "CreateLedgerDraft";

    public const string RequestWarehouseProxy = "RequestWarehouseProxy";

    public const string ContinuePayment = "ContinuePayment";

    public const string CheckTransportProgress = "CheckTransportProgress";
}
