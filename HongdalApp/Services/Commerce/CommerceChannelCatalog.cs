using Hongdal.Contracts.Common.Sales;

namespace HongdalApp.Services.Commerce;

public sealed class CommerceChannelCatalog : ICommerceChannelCatalog
{
    private static readonly IReadOnlyList<CommerceChannelDescriptor> Channels =
    [
        new(CommerceChannelKeys.SmartStore, "스마트스토어", "Naver Commerce API", true, true, true, "상품 API 모듈 준비"),
        new(CommerceChannelKeys.Coupang, "쿠팡 WING", "Coupang Open API", true, true, true, "상품 API 모듈 준비"),
        new(CommerceChannelKeys.Shopify, "Shopify", "Shopify Admin GraphQL API", true, true, true, "payload 초안 준비"),
        new(CommerceChannelKeys.Amazon, "Amazon", "Amazon SP-API", true, true, true, "payload 초안 준비"),
        new(CommerceChannelKeys.Ebay, "eBay", "eBay Sell Inventory API", true, true, true, "payload 초안 준비"),
        new(CommerceChannelKeys.Walmart, "Walmart Marketplace", "Walmart Marketplace API", true, true, true, "후속 연동 후보"),
        new(CommerceChannelKeys.Etsy, "Etsy", "Etsy Open API v3", true, true, true, "후속 연동 후보"),
        new(CommerceChannelKeys.TikTokShop, "TikTok Shop", "TikTok Shop Open API", true, true, true, "후속 연동 후보"),
        new(CommerceChannelKeys.Shopee, "Shopee", "Shopee Open Platform", true, true, true, "후속 연동 후보"),
        new(CommerceChannelKeys.Lazada, "Lazada", "Lazada Open Platform", true, true, true, "후속 연동 후보"),
        new(CommerceChannelKeys.ElevenStreet, "11번가", "11st Open API", false, false, false, "후속 연동 후보")
    ];

    public IReadOnlyList<CommerceChannelDescriptor> GetSupportedChannels() => Channels;

    public CommerceChannelDescriptor? FindByChannelType(string channelType)
        => Channels.FirstOrDefault(x => string.Equals(x.ChannelKey, channelType, StringComparison.OrdinalIgnoreCase));
}
