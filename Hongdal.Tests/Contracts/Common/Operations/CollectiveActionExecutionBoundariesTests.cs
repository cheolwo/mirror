using Hongdal.Contracts.Common.Operations;

namespace Hongdal.Tests.Contracts.Common.Operations;

public sealed class CollectiveActionExecutionBoundariesTests
{
    [Fact]
    public void Platform_candidate_information_cannot_confirm_dispatch()
    {
        var decision = CollectiveActionDispatchBoundaryPolicy.Evaluate(
            DispatchConfirmationBoundaryRequest.ForPlatformCandidateInformation("driver-1"));

        Assert.True(decision.CanProvideCandidateInformation);
        Assert.False(decision.CanConfirmDispatch);
        Assert.Equal(
            DispatchConfirmationBoundaryDecisionCodes.CandidateInformationOnly,
            decision.DecisionCode);
    }

    [Fact]
    public void Driver_can_confirm_only_their_own_acceptance()
    {
        var decision = CollectiveActionDispatchBoundaryPolicy.Evaluate(
            DispatchConfirmationBoundaryRequest.ForDriverSelfAcceptance(
                "driver-1",
                "driver-1"));

        Assert.True(decision.CanConfirmDispatch);
        Assert.Equal(
            RegulatedExecutionResponsibilityCodes.ParticipatingTransportProvider,
            decision.ExecutionResponsibilityCode);
    }

    [Fact]
    public void Driver_cannot_confirm_another_driver()
    {
        var decision = CollectiveActionDispatchBoundaryPolicy.Evaluate(
            DispatchConfirmationBoundaryRequest.ForDriverSelfAcceptance(
                "driver-1",
                "driver-2"));

        Assert.False(decision.CanConfirmDispatch);
        Assert.Equal(
            DispatchConfirmationBoundaryDecisionCodes.ParticipantIdentityMismatch,
            decision.DecisionCode);
    }

    [Fact]
    public void Qualified_service_provider_must_match_verified_participant()
    {
        var decision = CollectiveActionDispatchBoundaryPolicy.Evaluate(new(
            DispatchConfirmationDecisionSourceCodes.QualifiedServiceProviderConfirmation,
            ActorParticipantId: "provider-1",
            SelectedDriverParticipantId: "driver-1",
            VerifiedQualifiedServiceProviderParticipantId: "provider-1"));

        Assert.True(decision.CanConfirmDispatch);
        Assert.Equal(
            RegulatedExecutionResponsibilityCodes.ParticipatingQualifiedServiceProvider,
            decision.ExecutionResponsibilityCode);
    }

    [Fact]
    public void Unverified_service_provider_cannot_confirm_dispatch()
    {
        var decision = CollectiveActionDispatchBoundaryPolicy.Evaluate(new(
            DispatchConfirmationDecisionSourceCodes.QualifiedServiceProviderConfirmation,
            ActorParticipantId: "provider-1",
            SelectedDriverParticipantId: "driver-1",
            VerifiedQualifiedServiceProviderParticipantId: "provider-2"));

        Assert.False(decision.CanConfirmDispatch);
        Assert.Equal(
            DispatchConfirmationBoundaryDecisionCodes
                .VerifiedQualifiedServiceProviderRequired,
            decision.DecisionCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("unknown")]
    public void Unknown_decision_source_fails_closed_as_information_only(string? sourceCode)
    {
        var decision = CollectiveActionDispatchBoundaryPolicy.Evaluate(new(
            sourceCode ?? string.Empty,
            ActorParticipantId: "platform",
            SelectedDriverParticipantId: "driver-1"));

        Assert.Equal(
            DispatchConfirmationDecisionSourceCodes.PlatformCandidateInformation,
            decision.DecisionSourceCode);
        Assert.False(decision.CanConfirmDispatch);
    }
}
