using System.Text.Json;
using Hongdal.Contracts.Common.Localization;
using Hongdal.Contracts.Common.Operations;
using Hongdal.Services.Operations;

namespace Hongdal.Tests.Services.Operations;

public sealed class OperatingMarketRuntimeProfileServiceTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 17, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void GetCurrent_KeepsDisplayLanguageIndependentFromKoreaDeployment()
    {
        var service = new OperatingMarketRuntimeProfileService(
            new OperatingMarketDeployment(OperatingMarketCodes.Korea),
            new KoreaOperatingMarketFreightWorkflowPolicy());

        var response = service.GetCurrent();

        Assert.Equal(OperatingMarketCodes.Korea, response.MarketCode);
        Assert.Equal("KRW", response.CurrencyCode);
        Assert.Equal(OperatingTimeZoneIds.Korea, response.TimeZoneId);
        Assert.True(response.DisplayLanguageSelectionIsIndependent);
        Assert.Contains(DisplayLanguageCodes.Korean, response.SupportedDisplayLanguageCodes);
        Assert.Contains(DisplayLanguageCodes.English, response.SupportedDisplayLanguageCodes);
        Assert.False(response.FreightPolicy.PlatformCanConfirmDispatch);
        Assert.Contains(
            DispatchConfirmationDecisionSourceCodes.ParticipatingDriverSelfAcceptance,
            response.FreightPolicy.SupportedDispatchConfirmationDecisionSourceCodes);
        Assert.False(response.FreightPolicy.TransportationArrangementAvailable);
    }

    [Fact]
    public void GetCurrent_ReportsUsProviderBoundaryWithoutExposingParticipantIdentity()
    {
        var options = new OperatingMarketFreightServiceProviderOptions
        {
            ParticipantId = "broker-participant-1",
            ParticipantRoleCode = FreightServiceProviderRoleCodes.UnitedStatesPropertyBroker,
            AuthorityReference = "MC-123456",
            VerifiedAtUtc = Now.AddDays(-1),
            VerificationExpiresAtUtc = Now.AddDays(30),
            SatisfiedRequirementCodes =
            [
                FreightComplianceRequirementCodes.UnitedStatesBrokerAuthorityActive,
                FreightComplianceRequirementCodes.UnitedStatesFinancialSecurityActive,
                FreightComplianceRequirementCodes.UnitedStatesProcessAgentDesignationActive
            ]
        };
        var policy = new UnitedStatesOperatingMarketFreightWorkflowPolicy(
            new DeploymentOperatingMarketFreightServiceProviderRegistry(
                OperatingMarketCodes.UnitedStates,
                options),
            new FixedTimeProvider(Now));
        var service = new OperatingMarketRuntimeProfileService(
            new OperatingMarketDeployment(
                OperatingMarketCodes.UnitedStates,
                options.ParticipantId,
                "America/New_York"),
            policy);

        var response = service.GetCurrent();
        var serialized = JsonSerializer.Serialize(response);

        Assert.Equal("USD", response.CurrencyCode);
        Assert.Equal("America/New_York", response.TimeZoneId);
        Assert.Equal(
            PlatformOperatingRoleCodes.CollectiveActionFacilitator,
            response.PlatformOperatingRoleCode);
        Assert.True(response.FreightPolicy.CommunityIntentCoordinationAvailable);
        Assert.True(response.FreightPolicy.QualifiedProviderParticipationRequestAvailable);
        Assert.False(response.FreightPolicy.PlatformCanConfirmDispatch);
        Assert.Contains(
            DispatchConfirmationDecisionSourceCodes.QualifiedServiceProviderConfirmation,
            response.FreightPolicy.SupportedDispatchConfirmationDecisionSourceCodes);
        Assert.True(response.FreightPolicy.TransportationArrangementAvailable);
        Assert.Equal(
            FreightServiceProviderVerificationStatusCodes.Verified,
            response.FreightPolicy.VerificationStatusCode);
        Assert.Contains(
            FreightServiceProviderRoleCodes.UnitedStatesPropertyBroker,
            response.FreightPolicy.EligibleServiceProviderRoleCodes);
        Assert.DoesNotContain("broker-participant-1", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("MC-123456", serialized, StringComparison.Ordinal);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
