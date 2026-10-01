using Hongdal.Contracts.Common.Localization;
using Hongdal.Contracts.Common.Operations;

namespace Hongdal.Services.Operations;

public interface IOperatingMarketRuntimeProfileService
{
    OperatingMarketRuntimeProfileResponse GetCurrent();
}

public sealed class OperatingMarketRuntimeProfileService
    : IOperatingMarketRuntimeProfileService
{
    private readonly IOperatingMarketDeployment _deployment;
    private readonly IOperatingMarketFreightWorkflowPolicy _freightPolicy;

    public OperatingMarketRuntimeProfileService(
        IOperatingMarketDeployment deployment,
        IOperatingMarketFreightWorkflowPolicy freightPolicy)
    {
        ArgumentNullException.ThrowIfNull(deployment);
        ArgumentNullException.ThrowIfNull(freightPolicy);

        if (!string.Equals(
                deployment.MarketCode,
                freightPolicy.MarketCode,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Runtime profile market {deployment.MarketCode} does not match freight policy " +
                $"{freightPolicy.MarketCode}.");
        }

        _deployment = deployment;
        _freightPolicy = freightPolicy;
    }

    public OperatingMarketRuntimeProfileResponse GetCurrent()
    {
        var profile = _deployment.Profile;
        var freightDecision = _freightPolicy.Evaluate(new OperatingMarketFreightWorkflowRequest
        {
            ActivityCode = FreightWorkflowActivityCodes.RegulatedTransportationArrangement
        });

        return new OperatingMarketRuntimeProfileResponse
        {
            MarketCode = profile.MarketCode,
            CountryCode = profile.CountryCode,
            CurrencyCode = profile.CurrencyCode,
            FormattingCultureName = profile.FormattingCultureName,
            TimeZoneId = _deployment.TimeZoneId,
            DistanceUnitCode = profile.DistanceUnitCode,
            WeightUnitCode = profile.WeightUnitCode,
            AddressFormatCode = profile.AddressFormatCode,
            AddressProviderCode = profile.AddressProviderCode,
            MapProviderCode = profile.MapProviderCode,
            PlatformOperatingRoleCode = freightDecision.PlatformOperatingRoleCode,
            DisplayLanguageSelectionIsIndependent = true,
            SupportedDisplayLanguageCodes = DisplayLanguageCodes.Supported,
            PreferredCommerceChannelCodes = profile.PreferredCommerceChannelCodes,
            FreightPolicy = new OperatingMarketFreightRuntimePolicyResponse
            {
                ArrangementModeCode = freightDecision.ArrangementModeCode,
                RegulatoryAuthorityCode = freightDecision.RegulatoryAuthorityCode,
                ComplianceEnforcementModeCode = freightDecision.ComplianceEnforcementModeCode,
                CommunityIntentCoordinationAvailable = true,
                QualifiedProviderParticipationRequestAvailable = true,
                PlatformCanConfirmDispatch = false,
                SupportedDispatchConfirmationDecisionSourceCodes =
                    DispatchConfirmationDecisionSourceCodes.ConfirmationCapable,
                TransportationArrangementAvailable =
                    freightDecision.CanProceed &&
                    string.Equals(
                        freightDecision.VerificationStatusCode,
                        FreightServiceProviderVerificationStatusCodes.Verified,
                        StringComparison.Ordinal),
                RegulatedExecutionResponsibilityCode =
                    freightDecision.RegulatedExecutionResponsibilityCode,
                DecisionCode = freightDecision.DecisionCode,
                VerificationStatusCode = freightDecision.VerificationStatusCode,
                RequiredComplianceRequirementCodes =
                    freightDecision.RequiredComplianceRequirementCodes,
                MissingComplianceRequirementCodes =
                    freightDecision.MissingComplianceRequirementCodes,
                EligibleServiceProviderRoleCodes =
                    freightDecision.EligibleServiceProviderRoleCodes,
                RegulatoryReferenceCodes = freightDecision.RegulatoryReferenceCodes
            }
        };
    }
}
