using Hongdal.Contracts.Common.Sales;
using System.Text.Json.Nodes;

namespace HongdalApp.Services.Commerce.Amazon;

public sealed class AmazonSpApiProductPayloadBuilder : IProductListingPayloadBuilder
{
    public string ChannelKey => CommerceChannelKeys.Amazon;

    public JsonNode BuildPayloadDraft(판매채널계정항목응답 account, 판매상품항목응답 product)
        => new JsonObject
        {
            ["channelKey"] = ChannelKey,
            ["sellerAccountName"] = account.상점명,
            ["api"] = "Listings Items API",
            ["sourceProduct"] = new JsonObject
            {
                ["id"] = product.Id,
                ["sku"] = product.판매SKU,
                ["itemName"] = product.대표상품명,
                ["price"] = product.판매가,
                ["imageUrl"] = product.Image_Url
            },
            ["mappingStatus"] = "RequiredFieldsPending",
            ["mappingNote"] = "Amazon SP-API 출품에는 marketplaceId, sellerId, productType, Product Type Definitions JSON Schema, condition, fulfillmentAvailability 매핑이 필요합니다."
        };
}
