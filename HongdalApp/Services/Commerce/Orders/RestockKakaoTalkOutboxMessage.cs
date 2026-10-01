namespace HongdalApp.Services.Commerce.Orders;

public sealed class RestockKakaoTalkOutboxMessage
{
    public long Id { get; set; }

    public long RestockNotificationId { get; set; }

    public string SellerUserId { get; set; } = string.Empty;

    public string SellerName { get; set; } = string.Empty;

    public string ChannelType { get; set; } = string.Empty;

    public string ChannelOrderNo { get; set; } = string.Empty;

    public string Sku { get; set; } = string.Empty;

    public string TemplateCode { get; set; } = "RESTOCK_REQUIRED";

    public string Message { get; set; } = string.Empty;

    public string Status { get; set; } = "Pending";

    public string? SuppressedReason { get; set; }

    public DateTime CreatedAt { get; set; }
}
