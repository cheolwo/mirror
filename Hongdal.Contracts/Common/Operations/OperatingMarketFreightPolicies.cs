namespace Hongdal.Contracts.Common.Operations;

public static class OperatingMarketFreightDecisionCodes
{
    public const string Allowed = "Allowed";
    public const string VerifiedLicensedBrokerPartnerRequired =
        "VerifiedLicensedBrokerPartnerRequired";
    public const string VerifiedLicensedBrokerPartnerComplianceIncomplete =
        "VerifiedLicensedBrokerPartnerComplianceIncomplete";
    public const string VerifiedLicensedBrokerPartnerVerificationNotEffective =
        "VerifiedLicensedBrokerPartnerVerificationNotEffective";
    public const string VerifiedLicensedBrokerPartnerVerificationExpired =
        "VerifiedLicensedBrokerPartnerVerificationExpired";

    public const string VerifiedRegulatedServiceProviderRequired =
        VerifiedLicensedBrokerPartnerRequired;
    public const string VerifiedRegulatedServiceProviderComplianceIncomplete =
        VerifiedLicensedBrokerPartnerComplianceIncomplete;
    public const string VerifiedRegulatedServiceProviderVerificationNotEffective =
        VerifiedLicensedBrokerPartnerVerificationNotEffective;
    public const string VerifiedRegulatedServiceProviderVerificationExpired =
        VerifiedLicensedBrokerPartnerVerificationExpired;
}

public static class FreightComplianceEnforcementModeCodes
{
    public const string AuditOnly = "AuditOnly";
    public const string Required = "Required";
}

public static class FreightServiceProviderVerificationStatusCodes
{
    public const string NotRequired = "NotRequired";
    public const string NotConfigured = "NotConfigured";
    public const string Incomplete = "Incomplete";
    public const string NotEffective = "NotEffective";
    public const string Expired = "Expired";
    public const string Verified = "Verified";
}

public static class OperatingMarketRegulatoryAuthorityCodes
{
    public const string KoreaMinistryOfLandInfrastructureAndTransport = "KR-MOLIT";
    public const string UnitedStatesFederalMotorCarrierSafetyAdministration = "US-FMCSA";
}

public static class FreightComplianceRequirementCodes
{
    public const string ServiceProviderIdentity = "ServiceProviderIdentity";
    public const string ServiceProviderRole = "ServiceProviderRole";
    public const string AuthorityReference = "AuthorityReference";
    public const string VerificationPeriod = "VerificationPeriod";
    public const string KoreaFreightBrokeragePermitActive = "KR.FreightBrokeragePermit.Active";
    public const string UnitedStatesBrokerAuthorityActive = "US.BrokerAuthority.Active";
    public const string UnitedStatesFinancialSecurityActive = "US.FinancialSecurity.Active";
    public const string UnitedStatesProcessAgentDesignationActive =
        "US.ProcessAgentDesignation.Active";
}

public static class OperatingMarketRegulatoryReferenceCodes
{
    public const string KoreaFreightTransportBusinessActArticle24 =
        "KR.FreightTransportBusinessAct.Article24";
    public const string UnitedStatesFmcsaBrokerRegistration =
        "US.FMCSA.BrokerRegistration";
    public const string UnitedStatesFmcsaBrokerFinancialResponsibility =
        "US.FMCSA.BrokerFinancialResponsibility";
    public const string UnitedStatesFmcsaProcessAgentDesignation =
        "US.FMCSA.ProcessAgentDesignation";
}

public sealed record OperatingMarketFreightComplianceProfile(
    string MarketCode,
    string RegulatoryAuthorityCode,
    string EnforcementModeCode,
    IReadOnlyList<string> RequiredRequirementCodes,
    IReadOnlyList<string> EligibleServiceProviderRoleCodes,
    IReadOnlyList<string> RegulatoryReferenceCodes);

public static class OperatingMarketFreightComplianceProfileCatalog
{
    private static readonly IReadOnlyList<string> CommonRequirements =
    [
        FreightComplianceRequirementCodes.ServiceProviderIdentity,
        FreightComplianceRequirementCodes.ServiceProviderRole,
        FreightComplianceRequirementCodes.AuthorityReference,
        FreightComplianceRequirementCodes.VerificationPeriod
    ];

    private static readonly IReadOnlyDictionary<string, OperatingMarketFreightComplianceProfile> Profiles =
        new Dictionary<string, OperatingMarketFreightComplianceProfile>(StringComparer.OrdinalIgnoreCase)
        {
            [OperatingMarketCodes.Korea] = new(
                OperatingMarketCodes.Korea,
                OperatingMarketRegulatoryAuthorityCodes.KoreaMinistryOfLandInfrastructureAndTransport,
                FreightComplianceEnforcementModeCodes.AuditOnly,
                [
                    .. CommonRequirements,
                    FreightComplianceRequirementCodes.KoreaFreightBrokeragePermitActive
                ],
                [FreightServiceProviderRoleCodes.KoreaFreightTransportBroker],
                [OperatingMarketRegulatoryReferenceCodes.KoreaFreightTransportBusinessActArticle24]),
            [OperatingMarketCodes.UnitedStates] = new(
                OperatingMarketCodes.UnitedStates,
                OperatingMarketRegulatoryAuthorityCodes
                    .UnitedStatesFederalMotorCarrierSafetyAdministration,
                FreightComplianceEnforcementModeCodes.Required,
                [
                    .. CommonRequirements,
                    FreightComplianceRequirementCodes.UnitedStatesBrokerAuthorityActive,
                    FreightComplianceRequirementCodes.UnitedStatesFinancialSecurityActive,
                    FreightComplianceRequirementCodes.UnitedStatesProcessAgentDesignationActive
                ],
                [FreightServiceProviderRoleCodes.UnitedStatesPropertyBroker],
                [
                    OperatingMarketRegulatoryReferenceCodes.UnitedStatesFmcsaBrokerRegistration,
                    OperatingMarketRegulatoryReferenceCodes
                        .UnitedStatesFmcsaBrokerFinancialResponsibility,
                    OperatingMarketRegulatoryReferenceCodes
                        .UnitedStatesFmcsaProcessAgentDesignation
                ])
        };

    public static OperatingMarketFreightComplianceProfile Get(string? marketCode)
        => Profiles[OperatingMarketCodes.Normalize(marketCode)];

    public static bool TryGet(
        string? marketCode,
        out OperatingMarketFreightComplianceProfile profile)
    {
        if (!OperatingMarketCodes.TryNormalize(marketCode, out var normalizedCode))
        {
            profile = Profiles[OperatingMarketCodes.Korea];
            return false;
        }

        profile = Profiles[normalizedCode];
        return true;
    }
}

public sealed class OperatingMarketFreightServiceProviderVerification
{
    public string MarketCode { get; init; } = string.Empty;

    public string ServiceProviderParticipantId { get; init; } = string.Empty;

    public string ServiceProviderRoleCode { get; init; } = string.Empty;

    public string AuthorityReference { get; init; } = string.Empty;

    public DateTimeOffset? VerifiedAtUtc { get; init; }

    public DateTimeOffset? VerificationExpiresAtUtc { get; init; }

    public IReadOnlyList<string> SatisfiedRequirementCodes { get; init; } = [];
}

public sealed class OperatingMarketFreightWorkflowRequest
{
    public string ActivityCode { get; init; } =
        FreightWorkflowActivityCodes.CommunityIntentCoordination;

    // Compatibility bridge for callers created before activity codes were introduced.
    public bool RequestsTransportationArrangement { get; init; }
}

public sealed class OperatingMarketFreightWorkflowDecision
{
    public string MarketCode { get; init; } = string.Empty;

    public string ActivityCode { get; init; } = string.Empty;

    public string PlatformOperatingRoleCode { get; init; } =
        PlatformOperatingRoleCodes.CollectiveActionFacilitator;

    public string RegulatedExecutionResponsibilityCode { get; init; } =
        RegulatedExecutionResponsibilityCodes.ParticipatingQualifiedServiceProvider;

    public string ArrangementModeCode { get; init; } = string.Empty;

    public string RegulatoryAuthorityCode { get; init; } = string.Empty;

    public string ComplianceEnforcementModeCode { get; init; } = string.Empty;

    public bool CanProceed { get; init; }

    public bool RequiresVerifiedLicensedBrokerPartner { get; init; }

    public bool RequiresVerifiedRegulatedServiceProvider { get; init; }

    public string DecisionCode { get; init; } = string.Empty;

    public string VerificationStatusCode { get; init; } = string.Empty;

    public string? VerifiedServiceProviderParticipantId { get; init; }

    public string? VerifiedServiceProviderRoleCode { get; init; }

    public DateTimeOffset? VerificationExpiresAtUtc { get; init; }

    public IReadOnlyList<string> RequiredComplianceRequirementCodes { get; init; } = [];

    public IReadOnlyList<string> MissingComplianceRequirementCodes { get; init; } = [];

    public IReadOnlyList<string> EligibleServiceProviderRoleCodes { get; init; } = [];

    public IReadOnlyList<string> RegulatoryReferenceCodes { get; init; } = [];
}
