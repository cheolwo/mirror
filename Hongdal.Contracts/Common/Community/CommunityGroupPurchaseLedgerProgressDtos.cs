namespace Hongdal.Contracts.Common.Community;

/// <summary>공동구매 화면과 원장이 함께 사용하는 절차 단계 코드입니다.</summary>
public static class CommunityGroupPurchaseLedgerStageCodes
{
    public const string Proposal = "proposal";
    public const string TradeRoute = "trade-route";
    public const string Recruitment = "recruitment";
    public const string Counterparty = "counterparty";
    public const string SupplyNegotiation = "supply-negotiation";
    public const string Objection = "objection";
    public const string Resolution = "resolution";
    public const string Signature = "signature";
    public const string FulfillmentPlan = "fulfillment-plan";
    public const string Execution = "execution";
    public const string Commerce = "commerce";

    public static IReadOnlyList<string> Ordered { get; } =
    [
        Proposal,
        TradeRoute,
        Recruitment,
        Counterparty,
        SupplyNegotiation,
        Objection,
        Resolution,
        Signature,
        FulfillmentPlan,
        Execution,
        Commerce
    ];

    public static bool IsSupported(string? value)
        => !string.IsNullOrWhiteSpace(value)
           && Ordered.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase);

    public static int OrderOf(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return -1;
        }

        for (var index = 0; index < Ordered.Count; index++)
        {
            if (string.Equals(Ordered[index], value.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }
}

public sealed class CommunityGroupPurchaseLedgerProgressRequest
{
    public string StageCode { get; set; } = string.Empty;
    public string Memo { get; set; } = string.Empty;
    public long? ExpectedRevision { get; set; }
}

public sealed class CommunityGroupPurchaseLedgerProgressResponse
{
    public Guid GroupPurchaseCampaignId { get; set; }
    public string CommunityLedgerId { get; set; } = string.Empty;
    public long Revision { get; set; }
    public string LedgerStatus { get; set; } = string.Empty;
    public string CurrentStageCode { get; set; } = CommunityGroupPurchaseLedgerStageCodes.Proposal;
    public bool AutomaticallyLinked { get; set; }
    public IReadOnlyList<CommunityGroupPurchaseLedgerStageHistoryItem> History { get; set; } = [];
}

public sealed class CommunityGroupPurchaseLedgerStageHistoryItem
{
    public string StageCode { get; set; } = string.Empty;
    public string LedgerStatus { get; set; } = string.Empty;
    public string Memo { get; set; } = string.Empty;
    public string ChangedBy { get; set; } = string.Empty;
    public DateTime ChangedAtUtc { get; set; }
}
