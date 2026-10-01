namespace Hongdal.Contracts.Common.Community;

public sealed class CommunityWorkClassificationResponse
{
    public string Code { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string WorkflowTag { get; init; } = string.Empty;
    public string FeatureFlagKey { get; init; } = string.Empty;
    public IReadOnlyList<string> LedgerTemplateKeys { get; init; } = [];
}

public static class CommunityWorkClassificationCatalog
{
    private static readonly IReadOnlyList<CommunityWorkClassificationResponse> Classifications =
    [
        Classification("community", "커뮤니티 대화", "커뮤니티 신뢰", "CommunityTrustWorkflow"),
        Classification("cargo-transport", "화물 운송", "국내 화물 운송", "DomesticTransportWorkflow", CommunityLedgerTemplateKeys.CargoTransport),
        Classification("warehouse", "창고 입출고", "창고 입출고", "WarehouseFulfillmentWorkflow", CommunityLedgerTemplateKeys.WarehouseInbound, CommunityLedgerTemplateKeys.WarehouseOutbound),
        Classification("group-purchase", "공동구매", "공동구매", "CommunityTrustWorkflow", CommunityLedgerTemplateKeys.Order, CommunityLedgerTemplateKeys.GroupPurchase),
        Classification("group-import", "공동수입", "공동수입", "GroupPurchaseImportWorkflow", CommunityLedgerTemplateKeys.GroupImport),
        Classification("meat-import-readiness", "육류 수입 준비 정보", "육류 수입 준비", "CommunityTrustWorkflow", CommunityLedgerTemplateKeys.MeatImportReadiness),
        Classification("food-order", "음식 주문", "음식 주문", "FoodDeliveryWorkflow", CommunityLedgerTemplateKeys.FoodOrder),
        Classification("food-delivery", "음식 배달", "음식 배달", "FoodDeliveryWorkflow", CommunityLedgerTemplateKeys.FoodDelivery),
        Classification("hongdal-mart", "알뜰살뜰 마트", "알뜰살뜰 마트", "HongdalMartWorkflow", CommunityLedgerTemplateKeys.HongdalMart),
        Classification("local-sale", "생활 판매", "생활 판매", "SalesChannelFulfillmentWorkflow", CommunityLedgerTemplateKeys.LocalSale),
        Classification("errand", "생활 요청", "생활 요청 원장", "CommunityTrustWorkflow", CommunityLedgerTemplateKeys.Errand)
    ];

    public static IReadOnlyList<CommunityWorkClassificationResponse> All => Classifications;

    public static CommunityWorkClassificationResponse? FindByWorkflowTag(string? workflowTag)
        => string.IsNullOrWhiteSpace(workflowTag)
            ? null
            : Classifications.FirstOrDefault(x =>
                string.Equals(x.WorkflowTag, workflowTag.Trim(), StringComparison.OrdinalIgnoreCase));

    public static CommunityWorkClassificationResponse? FindByLedgerTemplate(string? ledgerTemplateKey)
        => string.IsNullOrWhiteSpace(ledgerTemplateKey)
            ? null
            : Classifications.FirstOrDefault(x =>
                x.LedgerTemplateKeys.Contains(ledgerTemplateKey.Trim(), StringComparer.OrdinalIgnoreCase));

    private static CommunityWorkClassificationResponse Classification(
        string code,
        string displayName,
        string workflowTag,
        string featureFlagKey,
        params string[] ledgerTemplateKeys)
        => new()
        {
            Code = code,
            DisplayName = displayName,
            WorkflowTag = workflowTag,
            FeatureFlagKey = featureFlagKey,
            LedgerTemplateKeys = ledgerTemplateKeys
        };
}
