namespace HongdalApp.Services.Commerce.Orders;

public sealed class ExternalCommerceOrderItem
{
    public string ChannelProductNo { get; set; } = string.Empty;

    public string Sku { get; set; } = string.Empty;

    public string ProductName { get; set; } = string.Empty;

    public int Quantity { get; set; }
}
