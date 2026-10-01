using Hongdal.Contracts.Common.Localization;

namespace Hongdal.Contracts.Common.Operations;

public sealed class OperatingMarketRuntimeProfileResponse
{
    public string MarketCode { get; init; } = string.Empty;

    public string CountryCode { get; init; } = string.Empty;

    public string CurrencyCode { get; init; } = string.Empty;

    public string FormattingCultureName { get; init; } = string.Empty;

    public string TimeZoneId { get; init; } = string.Empty;

    public string DistanceUnitCode { get; init; } = string.Empty;

    public string WeightUnitCode { get; init; } = string.Empty;

    public string AddressFormatCode { get; init; } = string.Empty;

    public string AddressProviderCode { get; init; } = string.Empty;

    public string MapProviderCode { get; init; } = string.Empty;

    public string PlatformOperatingRoleCode { get; init; } =
        PlatformOperatingRoleCodes.CollectiveActionFacilitator;

    public bool DisplayLanguageSelectionIsIndependent { get; init; } = true;

    public IReadOnlyList<string> SupportedDisplayLanguageCodes { get; init; } =
        DisplayLanguageCodes.Supported;

    public IReadOnlyList<string> PreferredCommerceChannelCodes { get; init; } = [];

    public OperatingMarketFreightRuntimePolicyResponse FreightPolicy { get; init; } = new();
}

public sealed class OperatingMarketFreightRuntimePolicyResponse
{
    public string ArrangementModeCode { get; init; } = string.Empty;

    public string RegulatoryAuthorityCode { get; init; } = string.Empty;

    public string ComplianceEnforcementModeCode { get; init; } = string.Empty;

    public bool CommunityIntentCoordinationAvailable { get; init; } = true;

    public bool QualifiedProviderParticipationRequestAvailable { get; init; } = true;

    public bool PlatformCanConfirmDispatch { get; init; }

    public IReadOnlyList<string> SupportedDispatchConfirmationDecisionSourceCodes { get; init; } =
        DispatchConfirmationDecisionSourceCodes.ConfirmationCapable;

    public bool TransportationArrangementAvailable { get; init; }

    public string RegulatedExecutionResponsibilityCode { get; init; } =
        RegulatedExecutionResponsibilityCodes.ParticipatingQualifiedServiceProvider;

    public string DecisionCode { get; init; } = string.Empty;

    public string VerificationStatusCode { get; init; } = string.Empty;

    public IReadOnlyList<string> RequiredComplianceRequirementCodes { get; init; } = [];

    public IReadOnlyList<string> MissingComplianceRequirementCodes { get; init; } = [];

    public IReadOnlyList<string> EligibleServiceProviderRoleCodes { get; init; } = [];

    public IReadOnlyList<string> RegulatoryReferenceCodes { get; init; } = [];
}
