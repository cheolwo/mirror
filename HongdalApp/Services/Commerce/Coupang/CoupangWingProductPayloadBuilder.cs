using Hongdal.Contracts.Common.Sales;
using HongdalApp.Services.Commerce;
using System.Text.Json.Nodes;

namespace HongdalApp.Services.Commerce.Coupang;

public sealed class CoupangWingProductPayloadBuilder : IProductListingPayloadBuilder
{
    public string ChannelKey => CommerceChannelKeys.Coupang;

    public JsonNode BuildPayloadDraft(판매채널계정항목응답 account, 판매상품항목응답 product)
        => new JsonObject
        {
            ["channelKey"] = ChannelKey,
            ["vendorStoreName"] = account.상점명,
            ["sourceProduct"] = new JsonObject
            {
                ["id"] = product.Id,
                ["name"] = product.대표상품명,
                ["sku"] = product.판매SKU,
                ["salePrice"] = product.판매가,
                ["imageUrl"] = product.Image_Url
            },
            ["mappingStatus"] = "RequiredFieldsPending",
            ["mappingNote"] = "쿠팡 상품 생성에는 vendorId, 카테고리, 고시정보, 출고/반품지, 옵션 매핑이 추가로 필요합니다."
        };
}
