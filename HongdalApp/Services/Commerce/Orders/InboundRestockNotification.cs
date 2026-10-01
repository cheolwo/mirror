namespace HongdalApp.Services.Commerce.Orders;

public sealed class InboundRestockNotification
{
    public long Id { get; set; }

    public string ChannelType { get; set; } = string.Empty;

    public string ChannelOrderNo { get; set; } = string.Empty;

    public long InboundProductId { get; set; }

    public string SellerUserId { get; set; } = string.Empty;

    public string ProductName { get; set; } = string.Empty;

    public string Sku { get; set; } = string.Empty;

    public string ContractNo { get; set; } = string.Empty;

    public string ContractPartnerName { get; set; } = string.Empty;

    public int AvailableQuantity { get; set; }

    public int SafetyStockQuantity { get; set; }

    public int SuggestedInboundQuantity { get; set; }

    public string Message { get; set; } = string.Empty;

    public bool IsInternalNotificationVisible { get; set; } = true;

    public DateTime CreatedAt { get; set; }
}
