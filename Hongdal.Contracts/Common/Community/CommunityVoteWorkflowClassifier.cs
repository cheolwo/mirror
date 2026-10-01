namespace Hongdal.Contracts.Common.Community;

public static class CommunityVoteWorkflowClassifier
{
    public static bool IsGroupImport(CommunityVoteResponse campaign)
    {
        ArgumentNullException.ThrowIfNull(campaign);

        var explicitTradeRouteCode = campaign.GroupPurchase?.TradeRouteCode;
        if (!string.IsNullOrWhiteSpace(explicitTradeRouteCode))
        {
            return CommunityGroupPurchaseTradeRouteCodes.IsGroupImport(explicitTradeRouteCode);
        }

        // 거래경로가 저장되기 전의 캠페인만 HS 코드로 추론합니다.
        return campaign.Options.Any(option => !string.IsNullOrWhiteSpace(option.HsCode))
               || !string.IsNullOrWhiteSpace(campaign.GroupPurchase?.HsCode);
    }
}
