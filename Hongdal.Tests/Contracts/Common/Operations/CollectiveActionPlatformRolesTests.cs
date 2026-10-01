using Hongdal.Contracts.Common.Operations;

namespace Hongdal.Tests.Contracts.Common.Operations;

public sealed class CollectiveActionPlatformRolesTests
{
    [Theory]
    [InlineData(FreightWorkflowActivityCodes.CommunityIntentCoordination)]
    [InlineData(FreightWorkflowActivityCodes.QualifiedProviderParticipationRequest)]
    public void FacilitationActivity_DoesNotClaimRegulatedExecution(string activityCode)
        => Assert.False(
            FreightWorkflowActivityCodes.RequiresRegulatedServiceProvider(activityCode));

    [Fact]
    public void RegulatedArrangement_RequiresQualifiedParticipant()
        => Assert.True(FreightWorkflowActivityCodes.RequiresRegulatedServiceProvider(
            FreightWorkflowActivityCodes.RegulatedTransportationArrangement));

    [Fact]
    public void UnknownActivity_FailsClosedAsRegulatedArrangement()
        => Assert.Equal(
            FreightWorkflowActivityCodes.RegulatedTransportationArrangement,
            FreightWorkflowActivityCodes.Normalize("unknown-activity"));
}
