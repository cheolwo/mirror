using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Versioning;

namespace Ssalddel.Tests.Contracts.Common;

public sealed class NeighborhoodExchangeCapabilityTests
{
    [Theory]
    [InlineData(SsalddelPageAppCodes.IntegratedWeb, "/community/exchange", "community-exchange-list", PageInteractionBoundary.ReadOnly)]
    [InlineData(SsalddelPageAppCodes.IntegratedWeb, "/community/exchange/write", "community-exchange-write", PageInteractionBoundary.PlatformPersistence)]
    [InlineData(SsalddelPageAppCodes.IntegratedWeb, "/community/exchange/posts/42?return=offer", "community-exchange-detail", PageInteractionBoundary.PlatformPersistence)]
    [InlineData(SsalddelPageAppCodes.Shipper, "/community/exchange", "shipper-community-exchange-list", PageInteractionBoundary.ReadOnly)]
    [InlineData(SsalddelPageAppCodes.Shipper, "/community/exchange/write", "shipper-community-exchange-write", PageInteractionBoundary.PlatformPersistence)]
    [InlineData(SsalddelPageAppCodes.Shipper, "/community/exchange/posts/42?return=offer", "shipper-community-exchange-detail", PageInteractionBoundary.PlatformPersistence)]
    public void 생활교류는_Web과통합앱에서_공개커뮤니티책임으로해석한다(
        string appCode,
        string route,
        string pageKey,
        PageInteractionBoundary boundary)
    {
        Assert.True(SsalddelPageCapabilityCatalog.TryResolve(appCode, route, out var capability));
        Assert.Equal(pageKey, capability.PageKey);
        Assert.Equal(PageCapabilityStage.Live, capability.Stage);
        Assert.Equal(boundary, capability.Boundary);
        Assert.Equal("0.0", capability.IntroducedVersion);
        Assert.False(capability.RequiresAuthentication);
        Assert.False(capability.HasExternalEffects);
        Assert.Equal(["CommunityTrustWorkflow"], capability.FeatureKeys);
        Assert.Equal(["CommunityTrust"], capability.WorkflowCodes);
    }

    [Theory]
    [InlineData(SsalddelPageAppCodes.IntegratedWeb)]
    [InlineData(SsalddelPageAppCodes.Shipper)]
    public void 교류목록규칙이_새로운배송업무를포괄허용하지않는다(string appCode)
    {
        var exchangeRule = Assert.Single(SsalddelPageCapabilityCatalog.GetAll(),
            rule => rule.AppCode == appCode && rule.RoutePattern == NeighborhoodExchange.Home);

        Assert.Equal(PageCapabilityMatchKind.Exact, exchangeRule.MatchKind);
        if (SsalddelPageCapabilityCatalog.TryResolve(appCode, NeighborhoodExchange.Home + "/delivery/new", out var found))
        {
            Assert.NotEqual(exchangeRule.PageKey, found.PageKey);
        }
    }

    [Theory]
    [InlineData(SsalddelPageAppCodes.IntegratedWeb, "/community/exchange/deliveries", "community-delivery-list", false)]
    [InlineData(SsalddelPageAppCodes.IntegratedWeb, "/community/exchange/deliveries/new", "community-delivery-create", true)]
    [InlineData(SsalddelPageAppCodes.IntegratedWeb, "/community/exchange/deliveries/NEIGHBOR-42", "community-delivery-detail", false)]
    [InlineData(SsalddelPageAppCodes.Shipper, "/community/exchange/deliveries", "shipper-community-delivery-list", false)]
    [InlineData(SsalddelPageAppCodes.Shipper, "/community/exchange/deliveries/new", "shipper-community-delivery-create", true)]
    [InlineData(SsalddelPageAppCodes.Shipper, "/community/exchange/deliveries/NEIGHBOR-42", "shipper-community-delivery-detail", false)]
    public void 생활배송은_공개글과분리된_인증된2점0화물업무로해석한다(
        string appCode, string route, string pageKey, bool create)
    {
        Assert.True(SsalddelPageCapabilityCatalog.TryResolve(appCode, route, out var capability));

        Assert.Equal(pageKey, capability.PageKey);
        Assert.Equal(PageCapabilityStage.Beta, capability.Stage);
        Assert.Equal(SsalddelProductRoadmapCatalog.TransportVersion, capability.IntroducedVersion);
        Assert.True(capability.RequiresAuthentication);
        Assert.Equal(create, capability.HasExternalEffects);
        Assert.Equal(create ? PageInteractionBoundary.PlatformPersistence : PageInteractionBoundary.ReadOnly,
            capability.Boundary);
        Assert.Equal(["DomesticTransportWorkflow"], capability.FeatureKeys);
        Assert.Equal(["DomesticTransport"], capability.WorkflowCodes);
        Assert.DoesNotContain("FoodDelivery", capability.WorkflowCodes);
    }
}
