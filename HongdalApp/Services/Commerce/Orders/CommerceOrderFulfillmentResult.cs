namespace HongdalApp.Services.Commerce.Orders;

public sealed class CommerceOrderFulfillmentResult
{
    public string OrderScope { get; set; } = string.Empty;

    public string ChannelType { get; set; } = string.Empty;

    public string ChannelOrderNo { get; set; } = string.Empty;

    public IReadOnlyList<WarehouseOutboundNotification> Notifications { get; set; } = [];

    public IReadOnlyList<InboundRestockNotification> RestockNotifications { get; set; } = [];
}
