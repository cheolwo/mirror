namespace Hongdal.Contracts.Common.Operations;

public static class ThirdPartyLogisticsProviderOutreachScopeCodes
{
    public const string BondedToDoorPilot = "BondedToDoorPilot";
}

public static class ThirdPartyLogisticsProviderOutreachContactChannelCodes
{
    public const string VerifiedPublicBusinessEmail =
        "VerifiedPublicBusinessEmail";
    public const string OfficialInquiryForm = "OfficialInquiryForm";
}

public static class ThirdPartyLogisticsProviderOutreachReadinessCodes
{
    public const string MissingSenderRequirements = "MissingSenderRequirements";
    public const string ReadyForManualApproval = "ReadyForManualApproval";
}

public static class ThirdPartyLogisticsProviderOutreachRequiredFieldCodes
{
    public const string SenderName = "SenderName";
    public const string SenderOrganizationName = "SenderOrganizationName";
    public const string SenderEmail = "SenderEmail";
    public const string ReplyToEmail = "ReplyToEmail";
    public const string SenderOrganizationWebsiteUrl =
        "SenderOrganizationWebsiteUrl";
    public const string PhysicalPostalAddress = "PhysicalPostalAddress";
    public const string SenderIdentityAccuracyConfirmation =
        "SenderIdentityAccuracyConfirmation";
    public const string PhysicalAddressValidityConfirmation =
        "PhysicalAddressValidityConfirmation";
    public const string SuppressionListCheckConfirmation =
        "SuppressionListCheckConfirmation";
    public const string PerRecipientReviewConfirmation =
        "PerRecipientReviewConfirmation";
}

public static class ThirdPartyLogisticsProviderOutreachComplianceRequirementCodes
{
    public const string AccurateSenderAndRoutingInformation =
        "AccurateSenderAndRoutingInformation";
    public const string NonDeceptiveSubject = "NonDeceptiveSubject";
    public const string CommercialInquiryDisclosure =
        "CommercialInquiryDisclosure";
    public const string ValidPhysicalPostalAddress =
        "ValidPhysicalPostalAddress";
    public const string WorkingOptOutMechanism = "WorkingOptOutMechanism";
    public const string SuppressionListChecked = "SuppressionListChecked";
    public const string OfficialRecipientSourceReverified =
        "OfficialRecipientSourceReverified";
    public const string OneRecipientAtATimeApproval =
        "OneRecipientAtATimeApproval";

    public static IReadOnlyList<string> All { get; } =
    [
        AccurateSenderAndRoutingInformation,
        NonDeceptiveSubject,
        CommercialInquiryDisclosure,
        ValidPhysicalPostalAddress,
        WorkingOptOutMechanism,
        SuppressionListChecked,
        OfficialRecipientSourceReverified,
        OneRecipientAtATimeApproval
    ];
}

public static class ThirdPartyLogisticsProviderOutreachErrorCodes
{
    public const string MarketNotAvailableInDeployment =
        "MarketNotAvailableInDeployment";
    public const string UnsupportedScope = "UnsupportedScope";
}

public sealed class PrepareThirdPartyLogisticsProviderOutreachRequest
{
    public string ScopeCode { get; init; } =
        ThirdPartyLogisticsProviderOutreachScopeCodes.BondedToDoorPilot;

    public IReadOnlyList<string> ProviderKeys { get; init; } = [];

    public string SenderName { get; init; } = string.Empty;

    public string SenderOrganizationName { get; init; } = string.Empty;

    public string SenderRole { get; init; } = string.Empty;

    public string SenderEmail { get; init; } = string.Empty;

    public string ReplyToEmail { get; init; } = string.Empty;

    public string SenderOrganizationWebsiteUrl { get; init; } = string.Empty;

    public string PhysicalPostalAddress { get; init; } = string.Empty;

    public string PlannedCargoDescription { get; init; } = string.Empty;

    public string OriginDescription { get; init; } = string.Empty;

    public string DestinationDescription { get; init; } = "United States";

    public string EstimatedVolumeDescription { get; init; } = string.Empty;

    public string TargetTimingDescription { get; init; } = string.Empty;

    public bool ConfirmSenderIdentityAccuracy { get; init; }

    public bool ConfirmPhysicalAddressValidity { get; init; }

    public bool ConfirmSuppressionListChecked { get; init; }

    public bool ConfirmPerRecipientReview { get; init; }
}

public sealed class ThirdPartyLogisticsProviderOutreachDraft
{
    public string ProviderKey { get; init; } = string.Empty;

    public string ProviderDisplayName { get; init; } = string.Empty;

    public string ContactChannelCode { get; init; } = string.Empty;

    public string OfficialInquiryUrl { get; init; } = string.Empty;

    public string RecipientEmailAddress { get; init; } = string.Empty;

    public bool RecipientEmailVerifiedFromOfficialSource { get; init; }

    public string ContactSourceTitle { get; init; } = string.Empty;

    public string ContactSourceUrl { get; init; } = string.Empty;

    public DateOnly ContactReviewedOn { get; init; }

    public IReadOnlyList<string> SupportedStageCodes { get; init; } = [];

    public string Subject { get; init; } = string.Empty;

    public string PlainTextBody { get; init; } = string.Empty;

    public string ReadinessCode { get; init; } =
        ThirdPartyLogisticsProviderOutreachReadinessCodes
            .MissingSenderRequirements;

    public IReadOnlyList<string> MissingSenderRequirementCodes { get; init; } = [];

    public IReadOnlyList<string> ComplianceRequirementCodes { get; init; } = [];

    public bool CanCreateManualEmailDraft { get; init; }

    public bool CanUseOfficialInquiryForm { get; init; }

    public bool RequiresRecipientAddressReverification { get; init; } = true;

    public bool RequiresPerRecipientApproval { get; init; } = true;

    public bool AutomaticDispatchEnabled { get; init; }
}

public sealed class ThirdPartyLogisticsProviderOutreachPreparationResponse
{
    public bool Success { get; init; }

    public string MarketCode { get; init; } = string.Empty;

    public string ScopeCode { get; init; } = string.Empty;

    public string CatalogVersion { get; init; } = string.Empty;

    public DateOnly? ContactSnapshotReviewedOn { get; init; }

    public bool AutomaticDispatchEnabled { get; init; }

    public bool RequiresPerRecipientApproval { get; init; } = true;

    public string ComplianceGuidanceUrl { get; init; } = string.Empty;

    public string? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public int TotalDraftCount { get; init; }

    public int ReadyForManualApprovalCount { get; init; }

    public int DirectEmailDraftCount { get; init; }

    public int OfficialInquiryFormDraftCount { get; init; }

    public int BlockedDraftCount { get; init; }

    public IReadOnlyList<string> UnknownProviderKeys { get; init; } = [];

    public IReadOnlyList<string> MissingSenderRequirementCodes { get; init; } = [];

    public IReadOnlyList<ThirdPartyLogisticsProviderOutreachDraft> Items { get; init; } = [];
}
