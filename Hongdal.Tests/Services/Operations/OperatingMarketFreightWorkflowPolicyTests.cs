using Hongdal.Contracts.Common.Operations;
using Hongdal.Services.Operations;

namespace Hongdal.Tests.Services.Operations;

public sealed class OperatingMarketFreightWorkflowPolicyTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 17, 0, 0, 0, TimeSpan.Zero);

    private static readonly string[] UnitedStatesRequirements =
    [
        FreightComplianceRequirementCodes.UnitedStatesBrokerAuthorityActive,
        FreightComplianceRequirementCodes.UnitedStatesFinancialSecurityActive,
        FreightComplianceRequirementCodes.UnitedStatesProcessAgentDesignationActive
    ];

    [Fact]
    public void KoreaPolicy_ReportsProviderPermitAuditWithoutBlockingTransitionWorkflow()
    {
        var policy = new KoreaOperatingMarketFreightWorkflowPolicy();

        var decision = policy.Evaluate(new OperatingMarketFreightWorkflowRequest
        {
            ActivityCode = FreightWorkflowActivityCodes.RegulatedTransportationArrangement
        });

        Assert.True(decision.CanProceed);
        Assert.True(decision.RequiresVerifiedRegulatedServiceProvider);
        Assert.False(decision.RequiresVerifiedLicensedBrokerPartner);
        Assert.Equal(
            PlatformOperatingRoleCodes.CollectiveActionFacilitator,
            decision.PlatformOperatingRoleCode);
        Assert.Equal(
            RegulatedExecutionResponsibilityCodes.ParticipatingQualifiedServiceProvider,
            decision.RegulatedExecutionResponsibilityCode);
        Assert.Equal(
            FreightComplianceEnforcementModeCodes.AuditOnly,
            decision.ComplianceEnforcementModeCode);
        Assert.Equal(
            FreightServiceProviderVerificationStatusCodes.NotConfigured,
            decision.VerificationStatusCode);
        Assert.Contains(
            FreightComplianceRequirementCodes.KoreaFreightBrokeragePermitActive,
            decision.MissingComplianceRequirementCodes);
    }

    [Theory]
    [InlineData(FreightWorkflowActivityCodes.CommunityIntentCoordination)]
    [InlineData(FreightWorkflowActivityCodes.QualifiedProviderParticipationRequest)]
    public void UnitedStatesPolicy_AllowsFacilitationActivitiesWithoutProvider(
        string activityCode)
    {
        var policy = CreateUnitedStatesPolicy();

        var decision = policy.Evaluate(new OperatingMarketFreightWorkflowRequest
        {
            ActivityCode = activityCode
        });

        Assert.True(decision.CanProceed);
        Assert.False(decision.RequiresVerifiedRegulatedServiceProvider);
        Assert.Equal(activityCode, decision.ActivityCode);
        Assert.Equal(OperatingMarketFreightDecisionCodes.Allowed, decision.DecisionCode);
        Assert.Equal(
            FreightServiceProviderVerificationStatusCodes.NotRequired,
            decision.VerificationStatusCode);
    }

    [Fact]
    public void UnitedStatesPolicy_BlocksRegulatedArrangementWithoutVerifiedProvider()
    {
        var policy = CreateUnitedStatesPolicy();

        var decision = policy.Evaluate(new OperatingMarketFreightWorkflowRequest
        {
            ActivityCode = FreightWorkflowActivityCodes.RegulatedTransportationArrangement
        });

        Assert.False(decision.CanProceed);
        Assert.True(decision.RequiresVerifiedRegulatedServiceProvider);
        Assert.True(decision.RequiresVerifiedLicensedBrokerPartner);
        Assert.Equal(
            OperatingMarketFreightDecisionCodes.VerifiedRegulatedServiceProviderRequired,
            decision.DecisionCode);
        Assert.Equal(
            FreightServiceProviderVerificationStatusCodes.NotConfigured,
            decision.VerificationStatusCode);
    }

    [Fact]
    public void UnitedStatesPolicy_DoesNotTrustParticipantIdByItself()
    {
        var policy = CreateUnitedStatesPolicy(new OperatingMarketFreightServiceProviderOptions
        {
            ParticipantId = "broker-participant-1"
        });

        var decision = policy.Evaluate(new OperatingMarketFreightWorkflowRequest
        {
            ActivityCode = FreightWorkflowActivityCodes.RegulatedTransportationArrangement
        });

        Assert.False(decision.CanProceed);
        Assert.Equal(
            OperatingMarketFreightDecisionCodes
                .VerifiedRegulatedServiceProviderComplianceIncomplete,
            decision.DecisionCode);
        Assert.Contains(
            FreightComplianceRequirementCodes.ServiceProviderRole,
            decision.MissingComplianceRequirementCodes);
        Assert.Contains(
            FreightComplianceRequirementCodes.AuthorityReference,
            decision.MissingComplianceRequirementCodes);
    }

    [Fact]
    public void UnitedStatesPolicy_BlocksExpiredProviderVerification()
    {
        var options = CreateCompleteServiceProviderOptions();
        options.VerificationExpiresAtUtc = Now.AddMinutes(-1);
        var policy = CreateUnitedStatesPolicy(options);

        var decision = policy.Evaluate(new OperatingMarketFreightWorkflowRequest
        {
            ActivityCode = FreightWorkflowActivityCodes.RegulatedTransportationArrangement
        });

        Assert.False(decision.CanProceed);
        Assert.Equal(
            FreightServiceProviderVerificationStatusCodes.Expired,
            decision.VerificationStatusCode);
        Assert.Equal(
            OperatingMarketFreightDecisionCodes
                .VerifiedRegulatedServiceProviderVerificationExpired,
            decision.DecisionCode);
    }

    [Fact]
    public void UnitedStatesPolicy_AllowsArrangementByCurrentQualifiedParticipant()
    {
        var policy = CreateUnitedStatesPolicy(CreateCompleteServiceProviderOptions());

        var decision = policy.Evaluate(new OperatingMarketFreightWorkflowRequest
        {
            ActivityCode = FreightWorkflowActivityCodes.RegulatedTransportationArrangement
        });

        Assert.True(decision.CanProceed);
        Assert.True(decision.RequiresVerifiedRegulatedServiceProvider);
        Assert.Equal(OperatingMarketFreightDecisionCodes.Allowed, decision.DecisionCode);
        Assert.Equal(
            FreightServiceProviderVerificationStatusCodes.Verified,
            decision.VerificationStatusCode);
        Assert.Empty(decision.MissingComplianceRequirementCodes);
        Assert.Equal(
            "broker-participant-1",
            decision.VerifiedServiceProviderParticipantId);
        Assert.Equal(
            FreightServiceProviderRoleCodes.UnitedStatesPropertyBroker,
            decision.VerifiedServiceProviderRoleCode);
    }

    private static UnitedStatesOperatingMarketFreightWorkflowPolicy CreateUnitedStatesPolicy(
        OperatingMarketFreightServiceProviderOptions? options = null)
        => new(
            new DeploymentOperatingMarketFreightServiceProviderRegistry(
                OperatingMarketCodes.UnitedStates,
                options),
            new FixedTimeProvider(Now));

    private static OperatingMarketFreightServiceProviderOptions
        CreateCompleteServiceProviderOptions()
        => new()
        {
            ParticipantId = "broker-participant-1",
            ParticipantRoleCode = FreightServiceProviderRoleCodes.UnitedStatesPropertyBroker,
            AuthorityReference = "MC-123456",
            VerifiedAtUtc = Now.AddDays(-1),
            VerificationExpiresAtUtc = Now.AddDays(30),
            SatisfiedRequirementCodes = UnitedStatesRequirements
        };

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
