using Hongdal.Contracts.Common.Community;

namespace Hongdal.Tests.Contracts.Common.Community;

public sealed class CommunityGroupPurchaseTradeRoutePolicyTests
{
    [Fact]
    public void 해외판매자_해외출발_한국배송_미통관이면_공동수입후보로판정한다()
    {
        var decision = CommunityGroupPurchaseTradeRoutePolicy.Evaluate(
            new CommunityGroupPurchaseTradeRouteInput(
                "cn",
                "cn",
                "kr",
                CommunityGroupPurchaseCustomsClearanceStatusCodes.NotCleared));

        Assert.Equal(
            CommunityGroupPurchaseTradeRouteCodes.InboundGroupImportCandidate,
            decision.RouteCode);
        Assert.True(decision.IsGroupImportCandidate);
        Assert.False(decision.RequiresManualReview);
        Assert.Contains(
            CommunityGroupPurchaseTradeRouteReasonCodes.CustomsClearanceRequired,
            decision.ReasonCodes);
    }

    [Fact]
    public void 해외판매자라도_국내출발재고이면_국내공동구매로판정한다()
    {
        var decision = CommunityGroupPurchaseTradeRoutePolicy.Evaluate(
            new CommunityGroupPurchaseTradeRouteInput(
                "US",
                "KR",
                "KR",
                CommunityGroupPurchaseCustomsClearanceStatusCodes.Cleared));

        Assert.Equal(CommunityGroupPurchaseTradeRouteCodes.Domestic, decision.RouteCode);
        Assert.False(decision.IsGroupImportCandidate);
        Assert.Contains(
            CommunityGroupPurchaseTradeRouteReasonCodes.SellerOutsideKorea,
            decision.ReasonCodes);
    }

    [Fact]
    public void 국내판매자라도_해외출발_한국배송_미통관이면_공동수입후보로판정한다()
    {
        var decision = CommunityGroupPurchaseTradeRoutePolicy.Evaluate(
            new CommunityGroupPurchaseTradeRouteInput(
                "KR",
                "VN",
                "KR",
                CommunityGroupPurchaseCustomsClearanceStatusCodes.NotCleared));

        Assert.Equal(
            CommunityGroupPurchaseTradeRouteCodes.InboundGroupImportCandidate,
            decision.RouteCode);
        Assert.True(decision.IsGroupImportCandidate);
    }

    [Fact]
    public void 해외출발_한국배송인데_통관상태가불명이면_검토필요로판정한다()
    {
        var decision = CommunityGroupPurchaseTradeRoutePolicy.Evaluate(
            new CommunityGroupPurchaseTradeRouteInput(
                "CN",
                "CN",
                "KR",
                CommunityGroupPurchaseCustomsClearanceStatusCodes.Unknown));

        Assert.Equal(CommunityGroupPurchaseTradeRouteCodes.ReviewRequired, decision.RouteCode);
        Assert.True(decision.RequiresManualReview);
        Assert.Contains(
            CommunityGroupPurchaseTradeRouteFieldCodes.CustomsClearanceStatusCode,
            decision.MissingFieldCodes);
    }

    [Fact]
    public void 미국운영에서_한국출발_미국배송_미통관이면_공동수입후보로판정한다()
    {
        var decision = CommunityGroupPurchaseTradeRoutePolicy.Evaluate(
            new CommunityGroupPurchaseTradeRouteInput(
                "KR",
                "KR",
                "US",
                CommunityGroupPurchaseCustomsClearanceStatusCodes.NotCleared,
                CommunityGroupPurchaseTradeRoutePolicy.UnitedStatesCountryCode));

        Assert.Equal(
            CommunityGroupPurchaseTradeRouteCodes.InboundGroupImportCandidate,
            decision.RouteCode);
        Assert.True(decision.IsGroupImportCandidate);
        Assert.Contains(
            CommunityGroupPurchaseTradeRouteReasonCodes.DeliveryToOperatingMarket,
            decision.ReasonCodes);
        Assert.DoesNotContain(
            CommunityGroupPurchaseTradeRouteReasonCodes.DeliveryToKorea,
            decision.ReasonCodes);
    }

    [Fact]
    public void 한국운영에서_한국출발_미국배송은_다른국경간거래로유지한다()
    {
        var decision = CommunityGroupPurchaseTradeRoutePolicy.Evaluate(
            new CommunityGroupPurchaseTradeRouteInput(
                "KR",
                "KR",
                "US",
                CommunityGroupPurchaseCustomsClearanceStatusCodes.NotCleared));

        Assert.Equal(CommunityGroupPurchaseTradeRouteCodes.OtherCrossBorder, decision.RouteCode);
        Assert.False(decision.IsGroupImportCandidate);
    }
}
