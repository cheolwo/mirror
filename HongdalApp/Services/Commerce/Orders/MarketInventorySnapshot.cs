namespace HongdalApp.Services.Commerce.Orders;

public sealed class MarketInventorySnapshot
{
    public string Sku { get; set; } = string.Empty;

    public string ProductName { get; set; } = string.Empty;

    public int AvailableQuantity { get; set; }

    public int ReservedQuantity { get; set; }

    public int SafetyStockQuantity { get; set; }

    public string ContractNo { get; set; } = string.Empty;

    public string ContractType { get; set; } = string.Empty;

    public bool CanSellToMarket { get; set; }

    public bool RequiresCustoms { get; set; }

    public bool CanOrder => CanSellToMarket && AvailableQuantity > 0;

    public bool NeedsInbound => CanSellToMarket && AvailableQuantity <= SafetyStockQuantity;
}
