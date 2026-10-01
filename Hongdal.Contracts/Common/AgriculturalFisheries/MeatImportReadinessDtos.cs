using Hongdal.Contracts.Common.Community;

namespace Hongdal.Contracts.Common.AgriculturalFisheries;

public sealed class MeatImportReadinessDiagramResponse
{
    public string TemplateCode { get; set; } = MeatImportReadinessCodes.TemplateCode;
    public string TemplateVersion { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string JurisdictionCode { get; set; } = "KR";
    public DateOnly LastReviewedOn { get; set; }
    public bool InformationOnly { get; set; } = true;
    public bool IsBrokerageEnabled { get; set; }
    public string OfficialDecisionBoundary { get; set; } = string.Empty;
    public string JointConfirmationPolicy { get; set; } = string.Empty;
    public IReadOnlyList<MeatImportReadinessLaneResponse> Lanes { get; set; } = [];
    public IReadOnlyList<MeatImportReadinessStepTemplateResponse> Steps { get; set; } = [];
    public IReadOnlyList<MeatImportReadinessSourceResponse> Sources { get; set; } = [];
    public DiagramSnapshotDto Diagram { get; set; } = new();
    public IReadOnlyList<string> Notices { get; set; } = [];
}

public sealed class MeatImportReadinessLaneResponse
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string ResponsibilitySummary { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
}

public sealed class MeatImportReadinessStepTemplateResponse
{
    public string Code { get; set; } = string.Empty;
    public int Sequence { get; set; }
    public string PhaseCode { get; set; } = string.Empty;
    public string PhaseName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public IReadOnlyList<string> LaneCodes { get; set; } = [];
    public IReadOnlyList<string> PrerequisiteStepCodes { get; set; } = [];
    public IReadOnlyList<string> RequiredEvidenceCodes { get; set; } = [];
    public IReadOnlyList<string> SourceKeys { get; set; } = [];
    public string CommunicationPrompt { get; set; } = string.Empty;
    public bool RequiresOfficialResult { get; set; }
    public bool RequiresJointConfirmation { get; set; }
    public bool LiveRecheckRequired { get; set; }
    public bool CanBeNotApplicable { get; set; }
}

public sealed class MeatImportReadinessSourceResponse
{
    public string Key { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string UsageNote { get; set; } = string.Empty;
    public bool LiveCheckRequired { get; set; }
}

public sealed class CreateMeatImportReadinessCaseRequest
{
    public string? CommunityId { get; set; }
    public string InitiatorSideCode { get; set; } = MeatImportReadinessPartySideCodes.Korean;
    public string Title { get; set; } = string.Empty;
    public string ProductTypeCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string HsCode { get; set; } = string.Empty;
    public string OriginCountryCode { get; set; } = string.Empty;
    public string OriginCountryName { get; set; } = string.Empty;
    public string? ProductSpecification { get; set; }
    public string? KoreanImporterUserId { get; set; }
    public string? KoreanImporterDisplayName { get; set; }
    public string KoreanImporterOrganizationName { get; set; } = string.Empty;
    public CreateMeatImportReadinessCounterpartyRequest OverseasCounterparty { get; set; } = new();
}

public sealed class CreateMeatImportReadinessCounterpartyRequest
{
    public string? UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string OrganizationName { get; set; } = string.Empty;
    public string RoleCode { get; set; } = MeatImportReadinessParticipantRoleCodes.OverseasExporter;
    public string? EstablishmentNumber { get; set; }
}

public sealed class MeatImportReadinessCaseListResponse
{
    public IReadOnlyList<MeatImportReadinessCaseSummaryResponse> Items { get; set; } = [];
}

public sealed class MeatImportReadinessCaseSummaryResponse
{
    public string CaseId { get; set; } = string.Empty;
    public long Revision { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string OriginCountryName { get; set; } = string.Empty;
    public string ProcessStatusCode { get; set; } = MeatImportReadinessProcessStatusCodes.Draft;
    public string CurrentStepCode { get; set; } = string.Empty;
    public int ReadinessPercent { get; set; }
    public int OpenBlockingIssueCount { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public sealed class MeatImportReadinessCaseResponse
{
    public string CaseId { get; set; } = string.Empty;
    public long Revision { get; set; }
    public string TemplateCode { get; set; } = MeatImportReadinessCodes.TemplateCode;
    public string TemplateVersion { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string CommunityId { get; set; } = string.Empty;
    public long? SourceCommunityPostId { get; set; }
    public string InitiatorSideCode { get; set; } = MeatImportReadinessPartySideCodes.Korean;
    public string ProductTypeCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string HsCode { get; set; } = string.Empty;
    public string OriginCountryCode { get; set; } = string.Empty;
    public string OriginCountryName { get; set; } = string.Empty;
    public string? ProductSpecification { get; set; }
    public string ProcessStatusCode { get; set; } = MeatImportReadinessProcessStatusCodes.Draft;
    public string CurrentStepCode { get; set; } = string.Empty;
    public int ReadinessPercent { get; set; }
    public int OpenBlockingIssueCount { get; set; }
    public bool InformationOnly { get; set; } = true;
    public bool IsBrokerageEnabled { get; set; }
    public string CollaborationRoomId { get; set; } = string.Empty;
    public IReadOnlyList<MeatImportReadinessParticipantResponse> Participants { get; set; } = [];
    public IReadOnlyList<MeatImportReadinessStepResponse> Steps { get; set; } = [];
    public DiagramSnapshotDto Diagram { get; set; } = new();
    public IReadOnlyList<string> Notices { get; set; } = [];
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public sealed class MeatImportReadinessParticipantResponse
{
    public string ParticipantId { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string OrganizationName { get; set; } = string.Empty;
    public string RoleCode { get; set; } = string.Empty;
    public string SideCode { get; set; } = string.Empty;
    public string ParticipationStateCode { get; set; } = MeatImportReadinessParticipationStateCodes.Active;
    public string? EstablishmentNumber { get; set; }
}

public sealed class MeatImportReadinessStepResponse
{
    public string StepCode { get; set; } = string.Empty;
    public int Sequence { get; set; }
    public string PhaseCode { get; set; } = string.Empty;
    public string PhaseName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string StatusCode { get; set; } = MeatImportReadinessStepStatusCodes.NotStarted;
    public string? LastNote { get; set; }
    public string? OfficialReferenceNumber { get; set; }
    public DateOnly? OfficialResultDate { get; set; }
    public bool RequiresOfficialResult { get; set; }
    public bool RequiresJointConfirmation { get; set; }
    public bool LiveRecheckRequired { get; set; }
    public bool PrerequisitesSatisfied { get; set; }
    public bool CompletionSatisfied { get; set; }
    public IReadOnlyList<string> MissingPrerequisiteStepCodes { get; set; } = [];
    public IReadOnlyList<MeatImportReadinessEvidenceResponse> Evidences { get; set; } = [];
    public IReadOnlyList<MeatImportReadinessDiscussionResponse> Discussions { get; set; } = [];
    public IReadOnlyList<MeatImportReadinessAcknowledgementResponse> Acknowledgements { get; set; } = [];
    public IReadOnlyList<MeatImportReadinessStepEventResponse> History { get; set; } = [];
}

public sealed class UpdateMeatImportReadinessStepStatusRequest
{
    public long ExpectedRevision { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public string? Note { get; set; }
    public string? OfficialReferenceNumber { get; set; }
    public DateOnly? OfficialResultDate { get; set; }
}

public sealed class AddMeatImportReadinessEvidenceRequest
{
    public long ExpectedRevision { get; set; }
    public string EvidenceCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? DocumentNumber { get; set; }
    public string? IssuerName { get; set; }
    public string? ReferenceUri { get; set; }
    public DateOnly? IssuedOn { get; set; }
    public DateOnly? ExpiresOn { get; set; }
    public string? Note { get; set; }
}

public sealed class MeatImportReadinessEvidenceResponse
{
    public string EvidenceId { get; set; } = string.Empty;
    public string EvidenceCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? DocumentNumber { get; set; }
    public string? IssuerName { get; set; }
    public string? ReferenceUri { get; set; }
    public DateOnly? IssuedOn { get; set; }
    public DateOnly? ExpiresOn { get; set; }
    public string? Note { get; set; }
    public string AddedByUserId { get; set; } = string.Empty;
    public string AddedByDisplayName { get; set; } = string.Empty;
    public DateTime AddedAtUtc { get; set; }
}

public sealed class AddMeatImportReadinessDiscussionRequest
{
    public long ExpectedRevision { get; set; }
    public string KindCode { get; set; } = MeatImportReadinessDiscussionKindCodes.Question;
    public string Message { get; set; } = string.Empty;
    public string? ReplyToDiscussionId { get; set; }
    public bool IsBlocking { get; set; }
}

public sealed class ResolveMeatImportReadinessDiscussionRequest
{
    public long ExpectedRevision { get; set; }
    public string ResolutionNote { get; set; } = string.Empty;
}

public sealed class MeatImportReadinessDiscussionResponse
{
    public string DiscussionId { get; set; } = string.Empty;
    public string KindCode { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? ReplyToDiscussionId { get; set; }
    public bool IsBlocking { get; set; }
    public bool IsResolved { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
    public string CreatedByDisplayName { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public string? ResolvedByUserId { get; set; }
    public string? ResolutionNote { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
}

public sealed class AcknowledgeMeatImportReadinessStepRequest
{
    public long ExpectedRevision { get; set; }
    public string Statement { get; set; } = string.Empty;
}

public sealed class MeatImportReadinessAcknowledgementResponse
{
    public string AcknowledgementId { get; set; } = string.Empty;
    public string SideCode { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Statement { get; set; } = string.Empty;
    public DateTime AcknowledgedAtUtc { get; set; }
}

public sealed class MeatImportReadinessStepEventResponse
{
    public string EventId { get; set; } = string.Empty;
    public string EventCode { get; set; } = string.Empty;
    public string? PreviousStatusCode { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public string? Note { get; set; }
    public string ActorUserId { get; set; } = string.Empty;
    public string ActorDisplayName { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; }
}

public static class MeatImportReadinessCodes
{
    public const string TemplateCode = "kr-meat-import-readiness";
    public const string LedgerTemplateKey = CommunityLedgerTemplateKeys.MeatImportReadiness;
    public const string TemplateVersion = "2026-07-15.1";
}

public static class MeatImportReadinessCaseIds
{
    public static string FromCommunityPost(long postId)
    {
        if (postId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(postId));
        }

        return $"import-readiness-community-post-{postId}";
    }
}

public static class MeatImportReadinessProductTypeCodes
{
    public const string Beef = "Beef";
    public const string Pork = "Pork";

    public static bool IsSupported(string? value)
        => string.Equals(value, Beef, StringComparison.OrdinalIgnoreCase)
           || string.Equals(value, Pork, StringComparison.OrdinalIgnoreCase);
}

public static class MeatImportReadinessParticipantRoleCodes
{
    public const string KoreanImporter = "KoreanImporter";
    public const string OverseasExporter = "OverseasExporter";
    public const string OverseasEstablishment = "OverseasEstablishment";
    public const string CustomsBroker = "CustomsBroker";
    public const string LogisticsProvider = "LogisticsProvider";
    public const string Observer = "Observer";

    public static bool IsOverseasCounterparty(string? value)
        => string.Equals(value, OverseasExporter, StringComparison.OrdinalIgnoreCase)
           || string.Equals(value, OverseasEstablishment, StringComparison.OrdinalIgnoreCase);
}

public static class MeatImportReadinessPartySideCodes
{
    public const string Korean = "Korean";
    public const string Overseas = "Overseas";
    public const string Supporting = "Supporting";

    public static bool IsPrimaryParty(string? value)
        => string.Equals(value, Korean, StringComparison.OrdinalIgnoreCase)
           || string.Equals(value, Overseas, StringComparison.OrdinalIgnoreCase);
}

public static class MeatImportReadinessParticipationStateCodes
{
    public const string Active = "Active";
    public const string PendingAccountLink = "PendingAccountLink";
}

public static class MeatImportReadinessStepStatusCodes
{
    public const string NotStarted = "NotStarted";
    public const string InProgress = "InProgress";
    public const string WaitingForCounterparty = "WaitingForCounterparty";
    public const string EvidenceSubmitted = "EvidenceSubmitted";
    public const string ParticipantChecked = "ParticipantChecked";
    public const string OfficialResultRecorded = "OfficialResultRecorded";
    public const string Blocked = "Blocked";
    public const string NotApplicable = "NotApplicable";

    public static bool IsSupported(string? value)
        => value is not null && All.Contains(value.Trim());

    public static IReadOnlySet<string> All { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        NotStarted,
        InProgress,
        WaitingForCounterparty,
        EvidenceSubmitted,
        ParticipantChecked,
        OfficialResultRecorded,
        Blocked,
        NotApplicable
    };
}

public static class MeatImportReadinessProcessStatusCodes
{
    public const string Draft = "Draft";
    public const string Preparing = "Preparing";
    public const string Blocked = "Blocked";
    public const string ReadyForShipment = "ReadyForShipment";
    public const string InTransit = "InTransit";
    public const string BorderInspection = "BorderInspection";
    public const string DomesticReleasePreparation = "DomesticReleasePreparation";
    public const string Completed = "Completed";
}

public static class MeatImportReadinessDiscussionKindCodes
{
    public const string Question = "Question";
    public const string Answer = "Answer";
    public const string Objection = "Objection";
    public const string Note = "Note";

    public static bool IsSupported(string? value)
        => value is not null && All.Contains(value.Trim());

    public static IReadOnlySet<string> All { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Question,
        Answer,
        Objection,
        Note
    };
}
