using Hongdal.Contracts.Common.Sales;
using System.Text.Json.Nodes;

namespace HongdalApp.Services.Commerce.Ebay;

public sealed class EbayInventoryProductPayloadBuilder : IProductListingPayloadBuilder
{
    public string ChannelKey => CommerceChannelKeys.Ebay;

    public JsonNode BuildPayloadDraft(판매채널계정항목응답 account, 판매상품항목응답 product)
        => new JsonObject
        {
            ["channelKey"] = ChannelKey,
            ["sellerAccountName"] = account.상점명,
            ["api"] = "Sell Inventory API",
            ["listingWorkflow"] = "inventoryItem -> offer -> publish",
            ["sourceProduct"] = new JsonObject
            {
                ["id"] = product.Id,
                ["sku"] = product.판매SKU,
                ["title"] = product.대표상품명,
                ["price"] = product.판매가,
                ["imageUrl"] = product.Image_Url
            },
            ["mappingStatus"] = "RequiredFieldsPending",
            ["mappingNote"] = "eBay 출품에는 marketplaceId, categoryId, aspects, merchant location, fulfillment/payment/return policy, offer 가격 매핑이 필요합니다."
        };
}
