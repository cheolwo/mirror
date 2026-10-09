using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Versioning;

namespace Ssalddel.Tests.Contracts.Common;

public sealed class NeighborhoodMemberDeliveryCapabilityTests
{
    [Theory]
    [InlineData(SsalddelPageAppCodes.IntegratedWeb)]
    [InlineData(SsalddelPageAppCodes.Shipper)]
    public void 회원배송경로는기존일반배송상세로오인하지않고현재책임을부여한다(string app)
    {
        var prefix = app == SsalddelPageAppCodes.Shipper ? "shipper-member-delivery" : "community-member-delivery";
        foreach (var (route, suffix, boundary, external) in new[]
        {
            (NeighborhoodMemberDeliveryRoutes.Home, "participation", PageInteractionBoundary.PlatformPersistence, false),
            (NeighborhoodMemberDeliveryRoutes.Offers, "offers", PageInteractionBoundary.ReadOnly, false),
            (NeighborhoodMemberDeliveryRoutes.Offer("NMO-test"), "offer-detail", PageInteractionBoundary.PlatformPersistence, true),
            (NeighborhoodMemberDeliveryRoutes.Work, "work", PageInteractionBoundary.ReadOnly, false),
            (NeighborhoodMemberDeliveryRoutes.WorkDetail("NMD-test"), "work-detail", PageInteractionBoundary.PlatformPersistence, true)
        })
        {
            Assert.True(SsalddelPageCapabilityCatalog.TryResolve(app, route, out var value));
            Assert.Equal(prefix + "-" + suffix, value.PageKey);
            Assert.True(value.RequiresAuthentication);
            Assert.Equal(PageCapabilityStage.Beta, value.Stage);
            Assert.Equal(SsalddelProductRoadmapCatalog.TransportVersion, value.IntroducedVersion);
            Assert.Equal(boundary, value.Boundary); Assert.Equal(external, value.HasExternalEffects);
            Assert.Equal(["CommunityTrustWorkflow", "DomesticTransportWorkflow"], value.FeatureKeys);
            Assert.DoesNotContain("FoodDelivery", value.WorkflowCodes);
        }
        Assert.True(SsalddelPageCapabilityCatalog.TryResolve(app, NeighborhoodDeliveryRoutes.Detail("legacy-test"), out var legacy));
        Assert.DoesNotContain("member-delivery", legacy.PageKey);
        Assert.Equal(PageInteractionBoundary.ReadOnly, legacy.Boundary);
    }
}
