namespace HongdalApp.Services.Commerce.Orders;

public sealed class ExternalCommerceOrder
{
    public string ChannelType { get; set; } = string.Empty;

    public string ChannelOrderNo { get; set; } = string.Empty;

    public string BuyerName { get; set; } = string.Empty;

    public string RecipientName { get; set; } = string.Empty;

    public string RecipientAddress { get; set; } = string.Empty;

    public DateTime OrderedAt { get; set; } = DateTime.UtcNow;

    public IReadOnlyList<ExternalCommerceOrderItem> Items { get; set; } = [];
}
