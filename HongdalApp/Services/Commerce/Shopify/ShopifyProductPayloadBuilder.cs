using Hongdal.Contracts.Common.Sales;
using System.Text.Json.Nodes;

namespace HongdalApp.Services.Commerce.Shopify;

public sealed class ShopifyProductPayloadBuilder : IProductListingPayloadBuilder
{
    public string ChannelKey => CommerceChannelKeys.Shopify;

    public JsonNode BuildPayloadDraft(판매채널계정항목응답 account, 판매상품항목응답 product)
        => new JsonObject
        {
            ["channelKey"] = ChannelKey,
            ["shopName"] = account.상점명,
            ["adminApi"] = "GraphQL",
            ["mutation"] = "productCreate",
            ["sourceProduct"] = new JsonObject
            {
                ["id"] = product.Id,
                ["title"] = product.대표상품명,
                ["sku"] = product.판매SKU,
                ["price"] = product.판매가,
                ["imageUrl"] = product.Image_Url
            },
            ["mappingStatus"] = "RequiredFieldsPending",
            ["mappingNote"] = "Shopify 상품 생성에는 상품 설명, vendor, productType, variant, media, inventory policy 매핑을 추가로 검토해야 합니다."
        };
}
