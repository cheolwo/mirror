using Hongdal.Contracts.Common.Operations;

namespace Hongdal.Tests.Contracts.Common.Operations;

public sealed class OperatingMarketFreightComplianceProfileCatalogTests
{
    [Fact]
    public void KoreaProfile_ExposesPermitAuditWithoutClaimingAutomaticEnforcement()
    {
        var profile = OperatingMarketFreightComplianceProfileCatalog.Get(
            OperatingMarketCodes.Korea);

        Assert.Equal(
            OperatingMarketRegulatoryAuthorityCodes.KoreaMinistryOfLandInfrastructureAndTransport,
            profile.RegulatoryAuthorityCode);
        Assert.Equal(
            FreightComplianceEnforcementModeCodes.AuditOnly,
            profile.EnforcementModeCode);
        Assert.Contains(
            FreightComplianceRequirementCodes.KoreaFreightBrokeragePermitActive,
            profile.RequiredRequirementCodes);
        Assert.Contains(
            FreightServiceProviderRoleCodes.KoreaFreightTransportBroker,
            profile.EligibleServiceProviderRoleCodes);
    }

    [Fact]
    public void UnitedStatesProfile_RequiresAuthorityFinancialSecurityAndProcessAgent()
    {
        var profile = OperatingMarketFreightComplianceProfileCatalog.Get(
            OperatingMarketCodes.UnitedStates);

        Assert.Equal(
            FreightComplianceEnforcementModeCodes.Required,
            profile.EnforcementModeCode);
        Assert.Contains(
            FreightComplianceRequirementCodes.UnitedStatesBrokerAuthorityActive,
            profile.RequiredRequirementCodes);
        Assert.Contains(
            FreightComplianceRequirementCodes.UnitedStatesFinancialSecurityActive,
            profile.RequiredRequirementCodes);
        Assert.Contains(
            FreightComplianceRequirementCodes.UnitedStatesProcessAgentDesignationActive,
            profile.RequiredRequirementCodes);
        Assert.Contains(
            FreightServiceProviderRoleCodes.UnitedStatesPropertyBroker,
            profile.EligibleServiceProviderRoleCodes);
        Assert.Contains(
            OperatingMarketRegulatoryReferenceCodes
                .UnitedStatesFmcsaBrokerFinancialResponsibility,
            profile.RegulatoryReferenceCodes);
    }
}
