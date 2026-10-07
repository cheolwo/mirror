namespace Ssalddel.Contracts.Food;

/// <summary>정산 기록은 유지하며, 완료 배달의 주문·고객 정보는 서버의 열람 기한 안에서만 반환합니다.</summary>
public sealed class FoodDeliveryCompletedDeliveryDetailDto
{
    public FoodDeliveryOrderSettlementDto Settlement { get; set; } = new();
    public string DetailAccessStatusCode { get; set; } = FoodDeliveryCompletedDetailAccessStatusCodes.PolicyNotConfigured;
    public DateTime? DetailExpiresAtUtc { get; set; }
    public DateTime ServerNowUtc { get; set; }
    public FoodDeliveryCompletedOrderDetailsDto? OrderDetails { get; set; }
    public FoodDeliveryCompletedCustomerDetailsDto? CustomerDetails { get; set; }
}

public sealed class FoodDeliveryCompletedOrderDetailsDto
{
    public string RestaurantAddress { get; set; } = string.Empty;
    public decimal TotalOrderAmount { get; set; }
    public IReadOnlyList<FoodDeliveryCompletedOrderItemDto> Items { get; set; } = [];
}

public sealed class FoodDeliveryCompletedOrderItemDto
{
    public string MenuName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

public sealed class FoodDeliveryCompletedCustomerDetailsDto
{
    public string DisplayName { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string DeliveryInstructions { get; set; } = string.Empty;
}

public static class FoodDeliveryCompletedDetailAccessStatusCodes
{
    public const string Allowed = "Allowed";
    public const string Expired = "Expired";
    public const string PolicyNotConfigured = "PolicyNotConfigured";
    public const string CompletionEvidenceUnavailable = "CompletionEvidenceUnavailable";
}
