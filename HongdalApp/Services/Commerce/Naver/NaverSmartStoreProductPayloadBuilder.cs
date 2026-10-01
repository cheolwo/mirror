using Hongdal.Contracts.Common.Sales;
using HongdalApp.Services.Commerce;
using System.Text.Json.Nodes;

namespace HongdalApp.Services.Commerce.Naver;

public sealed class NaverSmartStoreProductPayloadBuilder : IProductListingPayloadBuilder
{
    public string ChannelKey => CommerceChannelKeys.SmartStore;

    public JsonNode BuildPayloadDraft(판매채널계정항목응답 account, 판매상품항목응답 product)
        => new JsonObject
        {
            ["channelKey"] = ChannelKey,
            ["storeName"] = account.상점명,
            ["sourceProduct"] = new JsonObject
            {
                ["id"] = product.Id,
                ["name"] = product.대표상품명,
                ["sku"] = product.판매SKU,
                ["salePrice"] = product.판매가,
                ["imageUrl"] = product.Image_Url
            },
            ["mappingStatus"] = "RequiredFieldsPending",
            ["mappingNote"] = "네이버 상품 등록에는 카테고리, 배송, A/S, 원상품 상세정보 매핑이 추가로 필요합니다."
        };
}
