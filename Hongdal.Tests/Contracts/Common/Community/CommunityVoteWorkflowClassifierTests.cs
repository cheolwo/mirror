using Hongdal.Contracts.Common.Community;

namespace Hongdal.Tests.Contracts.Common.Community;

public sealed class CommunityVoteWorkflowClassifierTests
{
    [Fact]
    public void HS코드가_있는_상품_선택은_공동수입으로_분류한다()
    {
        var campaign = new CommunityVoteResponse
        {
            Options =
            [
                new CommunityVoteOptionResponse
                {
                    Text = "조제식품",
                    HsCode = "2106.90"
                }
            ]
        };

        Assert.True(CommunityVoteWorkflowClassifier.IsGroupImport(campaign));
    }

    [Fact]
    public void HS코드가_없는_국내_상품_선택은_공동수입으로_분류하지_않는다()
    {
        var campaign = new CommunityVoteResponse
        {
            Options =
            [
                new CommunityVoteOptionResponse
                {
                    Text = "제철 고구마"
                }
            ]
        };

        Assert.False(CommunityVoteWorkflowClassifier.IsGroupImport(campaign));
    }

    [Fact]
    public void 명시적인_국내거래경로는_HS코드가있어도_공동수입으로분류하지않는다()
    {
        var campaign = new CommunityVoteResponse
        {
            Options = [new CommunityVoteOptionResponse { HsCode = "0202.30" }],
            GroupPurchase = new CommunityGroupPurchaseVoteResponse
            {
                TradeRouteCode = CommunityGroupPurchaseTradeRouteCodes.Domestic
            }
        };

        Assert.False(CommunityVoteWorkflowClassifier.IsGroupImport(campaign));
    }

    [Fact]
    public void 명시적인_공동수입후보는_HS코드가없어도_공동수입으로분류한다()
    {
        var campaign = new CommunityVoteResponse
        {
            GroupPurchase = new CommunityGroupPurchaseVoteResponse
            {
                TradeRouteCode = CommunityGroupPurchaseTradeRouteCodes.InboundGroupImportCandidate
            }
        };

        Assert.True(CommunityVoteWorkflowClassifier.IsGroupImport(campaign));
    }
}
