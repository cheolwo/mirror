using Hongdal.Contracts.Common.Sales;

namespace HongdalApp.Services.Commerce.Orders;

public sealed class CommerceOrderSampleFeedService : ICommerceOrderSampleFeedService
{
    public IReadOnlyList<ExternalCommerceOrder> GetSampleOrders() =>
    [
        new()
        {
            ChannelType = CommerceChannelKeys.SmartStore,
            ChannelOrderNo = "NS-ORDER-240703-001",
            BuyerName = "김국내",
            RecipientName = "김국내",
            RecipientAddress = "서울 마포구 월드컵북로 10",
            OrderedAt = DateTime.UtcNow.AddMinutes(-35),
            Items =
            [
                new()
                {
                    ChannelProductNo = "NS-90001",
                    Sku = "SALE-CHAIR-SET-01",
                    ProductName = "원목 의자 세트",
                    Quantity = 1
                },
                new()
                {
                    ChannelProductNo = "NS-90002",
                    Sku = "SKU-FOOD-002",
                    ProductName = "냉장 간편식 박스",
                    Quantity = 2
                }
            ]
        },
        new()
        {
            ChannelType = CommerceChannelKeys.Coupang,
            ChannelOrderNo = "CP-ORDER-240703-014",
            BuyerName = "박쿠팡",
            RecipientName = "박쿠팡",
            RecipientAddress = "경기 성남시 분당구 판교역로 20",
            OrderedAt = DateTime.UtcNow.AddMinutes(-20),
            Items =
            [
                new()
                {
                    ChannelProductNo = "CP-71000",
                    Sku = "SKU-FOOD-002",
                    ProductName = "냉장 간편식 박스",
                    Quantity = 2
                }
            ]
        },
        new()
        {
            ChannelType = CommerceChannelKeys.Amazon,
            ChannelOrderNo = "AMZ-ORDER-240703-101",
            BuyerName = "Global Buyer",
            RecipientName = "Global Buyer",
            RecipientAddress = "Seattle, WA, United States",
            OrderedAt = DateTime.UtcNow.AddMinutes(-10),
            Items =
            [
                new()
                {
                    ChannelProductNo = "AMZ-ASIN-DRAFT",
                    Sku = "SALE-CHAIR-SET-01",
                    ProductName = "Wood Chair Set",
                    Quantity = 1
                }
            ]
        }
    ];
}
