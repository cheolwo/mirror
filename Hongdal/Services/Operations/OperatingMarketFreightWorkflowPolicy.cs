using Hongdal.Contracts.Common.Operations;

namespace Hongdal.Services.Operations;

public interface IOperatingMarketFreightWorkflowPolicy
{
    string MarketCode { get; }

    OperatingMarketFreightWorkflowDecision Evaluate(
        OperatingMarketFreightWorkflowRequest request);
}

public abstract class OperatingMarketFreightWorkflowPolicyBase
    : IOperatingMarketFreightWorkflowPolicy
{
    private readonly IOperatingMarketFreightServiceProviderRegistry _serviceProviderRegistry;
    private readonly TimeProvider _timeProvider;
    private readonly OperatingMarketFreightComplianceProfile _complianceProfile;

    protected OperatingMarketFreightWorkflowPolicyBase(
        string marketCode,
        IOperatingMarketFreightServiceProviderRegistry serviceProviderRegistry,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProviderRegistry);
        ArgumentNullException.ThrowIfNull(timeProvider);

        if (!OperatingMarketCodes.TryNormalize(marketCode, out var normalizedMarketCode))
        {
            throw new ArgumentOutOfRangeException(nameof(marketCode), marketCode, "Unsupported market.");
        }

        if (!string.Equals(
                normalizedMarketCode,
                serviceProviderRegistry.MarketCode,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Freight policy {normalizedMarketCode} cannot use service provider registry " +
                $"{serviceProviderRegistry.MarketCode}.");
        }

        MarketCode = normalizedMarketCode;
        _serviceProviderRegistry = serviceProviderRegistry;
        _timeProvider = timeProvider;
        _complianceProfile = OperatingMarketFreightComplianceProfileCatalog.Get(MarketCode);
    }

    public string MarketCode { get; }

    protected abstract string ArrangementModeCode { get; }

    public OperatingMarketFreightWorkflowDecision Evaluate(
        OperatingMarketFreightWorkflowRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var activityCode = FreightWorkflowActivityCodes.Normalize(
            request.ActivityCode,
            request.RequestsTransportationArrangement);
        var requiresRegulatedServiceProvider =
            FreightWorkflowActivityCodes.RequiresRegulatedServiceProvider(activityCode);

        if (!requiresRegulatedServiceProvider)
        {
            return CreateDecision(
                activityCode,
                canProceed: true,
                requiresVerifiedServiceProvider: false,
                OperatingMarketFreightDecisionCodes.Allowed,
                FreightServiceProviderVerificationStatusCodes.NotRequired,
                verification: null,
                requiredRequirements: [],
                missingRequirements: []);
        }

        var verification = _serviceProviderRegistry.Current;
        var assessment = Assess(verification, _timeProvider.GetUtcNow());
        var enforcementRequired = string.Equals(
            _complianceProfile.EnforcementModeCode,
            FreightComplianceEnforcementModeCodes.Required,
            StringComparison.OrdinalIgnoreCase);
        var canProceed = !enforcementRequired || assessment.IsVerified;

        return CreateDecision(
            activityCode,
            canProceed,
            requiresVerifiedServiceProvider: true,
            canProceed
                ? OperatingMarketFreightDecisionCodes.Allowed
                : DecisionCodeFor(assessment.StatusCode),
            assessment.StatusCode,
            verification,
            _complianceProfile.RequiredRequirementCodes,
            assessment.MissingRequirementCodes);
    }

    private OperatingMarketFreightWorkflowDecision CreateDecision(
        string activityCode,
        bool canProceed,
        bool requiresVerifiedServiceProvider,
        string decisionCode,
        string verificationStatusCode,
        OperatingMarketFreightServiceProviderVerification? verification,
        IReadOnlyList<string> requiredRequirements,
        IReadOnlyList<string> missingRequirements)
        => new()
        {
            MarketCode = MarketCode,
            ActivityCode = activityCode,
            PlatformOperatingRoleCode = PlatformOperatingRoleCodes.CollectiveActionFacilitator,
            RegulatedExecutionResponsibilityCode =
                RegulatedExecutionResponsibilityCodes.ParticipatingQualifiedServiceProvider,
            ArrangementModeCode = ArrangementModeCode,
            RegulatoryAuthorityCode = _complianceProfile.RegulatoryAuthorityCode,
            ComplianceEnforcementModeCode = _complianceProfile.EnforcementModeCode,
            CanProceed = canProceed,
            RequiresVerifiedLicensedBrokerPartner = requiresVerifiedServiceProvider
                                                      && MarketCode ==
                                                      OperatingMarketCodes.UnitedStates,
            RequiresVerifiedRegulatedServiceProvider = requiresVerifiedServiceProvider,
            DecisionCode = decisionCode,
            VerificationStatusCode = verificationStatusCode,
            VerifiedServiceProviderParticipantId = NullIfWhiteSpace(
                verification?.ServiceProviderParticipantId),
            VerifiedServiceProviderRoleCode = NullIfWhiteSpace(
                verification?.ServiceProviderRoleCode),
            VerificationExpiresAtUtc = verification?.VerificationExpiresAtUtc,
            RequiredComplianceRequirementCodes = requiredRequirements,
            MissingComplianceRequirementCodes = missingRequirements,
            EligibleServiceProviderRoleCodes = _complianceProfile.EligibleServiceProviderRoleCodes,
            RegulatoryReferenceCodes = _complianceProfile.RegulatoryReferenceCodes
        };

    private ComplianceAssessment Assess(
        OperatingMarketFreightServiceProviderVerification? verification,
        DateTimeOffset now)
    {
        var required = _complianceProfile.RequiredRequirementCodes;
        if (verification is null)
        {
            return new ComplianceAssessment(
                FreightServiceProviderVerificationStatusCodes.NotConfigured,
                required);
        }

        if (!string.Equals(
                verification.MarketCode,
                MarketCode,
                StringComparison.OrdinalIgnoreCase))
        {
            return new ComplianceAssessment(
                FreightServiceProviderVerificationStatusCodes.Incomplete,
                required);
        }

        var satisfied = new HashSet<string>(
            verification.SatisfiedRequirementCodes ?? [],
            StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(verification.ServiceProviderParticipantId))
        {
            satisfied.Add(FreightComplianceRequirementCodes.ServiceProviderIdentity);
        }

        if (_complianceProfile.EligibleServiceProviderRoleCodes.Any(roleCode => string.Equals(
                roleCode,
                verification.ServiceProviderRoleCode,
                StringComparison.OrdinalIgnoreCase)))
        {
            satisfied.Add(FreightComplianceRequirementCodes.ServiceProviderRole);
        }

        if (!string.IsNullOrWhiteSpace(verification.AuthorityReference))
        {
            satisfied.Add(FreightComplianceRequirementCodes.AuthorityReference);
        }

        var hasValidVerificationPeriod = verification.VerifiedAtUtc.HasValue
                                         && verification.VerificationExpiresAtUtc.HasValue
                                         && verification.VerificationExpiresAtUtc.Value >
                                         verification.VerifiedAtUtc.Value;
        if (hasValidVerificationPeriod)
        {
            satisfied.Add(FreightComplianceRequirementCodes.VerificationPeriod);
        }

        var missing = required
            .Where(requirement => !satisfied.Contains(requirement))
            .ToArray();

        var commonRequirementsMissing = missing.Any(requirement =>
            requirement == FreightComplianceRequirementCodes.ServiceProviderIdentity
            || requirement == FreightComplianceRequirementCodes.ServiceProviderRole
            || requirement == FreightComplianceRequirementCodes.AuthorityReference
            || requirement == FreightComplianceRequirementCodes.VerificationPeriod);
        if (commonRequirementsMissing)
        {
            return new ComplianceAssessment(
                FreightServiceProviderVerificationStatusCodes.Incomplete,
                missing);
        }

        if (verification.VerifiedAtUtc!.Value > now)
        {
            return new ComplianceAssessment(
                FreightServiceProviderVerificationStatusCodes.NotEffective,
                missing);
        }

        if (verification.VerificationExpiresAtUtc!.Value <= now)
        {
            return new ComplianceAssessment(
                FreightServiceProviderVerificationStatusCodes.Expired,
                missing);
        }

        return missing.Length == 0
            ? new ComplianceAssessment(FreightServiceProviderVerificationStatusCodes.Verified, [])
            : new ComplianceAssessment(FreightServiceProviderVerificationStatusCodes.Incomplete, missing);
    }

    private static string DecisionCodeFor(string verificationStatusCode)
        => verificationStatusCode switch
        {
            FreightServiceProviderVerificationStatusCodes.NotEffective =>
                OperatingMarketFreightDecisionCodes
                    .VerifiedRegulatedServiceProviderVerificationNotEffective,
            FreightServiceProviderVerificationStatusCodes.Expired =>
                OperatingMarketFreightDecisionCodes
                    .VerifiedRegulatedServiceProviderVerificationExpired,
            FreightServiceProviderVerificationStatusCodes.Incomplete =>
                OperatingMarketFreightDecisionCodes
                    .VerifiedRegulatedServiceProviderComplianceIncomplete,
            _ => OperatingMarketFreightDecisionCodes.VerifiedRegulatedServiceProviderRequired
        };

    private static string? NullIfWhiteSpace(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record ComplianceAssessment(
        string StatusCode,
        IReadOnlyList<string> MissingRequirementCodes)
    {
        public bool IsVerified => string.Equals(
            StatusCode,
            FreightServiceProviderVerificationStatusCodes.Verified,
            StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class KoreaOperatingMarketFreightWorkflowPolicy
    : OperatingMarketFreightWorkflowPolicyBase
{
    public KoreaOperatingMarketFreightWorkflowPolicy()
        : this(
            new DeploymentOperatingMarketFreightServiceProviderRegistry(
                OperatingMarketCodes.Korea,
                options: null),
            TimeProvider.System)
    {
    }

    public KoreaOperatingMarketFreightWorkflowPolicy(
        IOperatingMarketFreightServiceProviderRegistry serviceProviderRegistry,
        TimeProvider timeProvider)
        : base(OperatingMarketCodes.Korea, serviceProviderRegistry, timeProvider)
    {
    }

    protected override string ArrangementModeCode =>
        FreightArrangementModeCodes.KoreaDomesticTransport;
}

public sealed class UnitedStatesOperatingMarketFreightWorkflowPolicy
    : OperatingMarketFreightWorkflowPolicyBase
{
    public UnitedStatesOperatingMarketFreightWorkflowPolicy(
        IOperatingMarketDeployment deployment)
        : this(
            DeploymentOperatingMarketFreightServiceProviderRegistry
                .FromLegacyDeployment(deployment),
            TimeProvider.System)
    {
    }

    public UnitedStatesOperatingMarketFreightWorkflowPolicy(
        IOperatingMarketFreightServiceProviderRegistry serviceProviderRegistry,
        TimeProvider timeProvider)
        : base(OperatingMarketCodes.UnitedStates, serviceProviderRegistry, timeProvider)
    {
    }

    protected override string ArrangementModeCode =>
        FreightArrangementModeCodes.UnitedStatesLicensedBrokerPartner;
}
