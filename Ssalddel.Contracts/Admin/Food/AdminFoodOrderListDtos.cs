namespace Ssalddel.Contracts.Admin.Food;

/// <summary>운영자 목록 전용 투영. 고객 연락처·주소·주문 payload를 포함하지 않습니다.</summary>
public sealed class AdminFoodOrderListDto
{
    public IReadOnlyList<AdminFoodOrderListItemDto> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public DateTime ServerNowUtc { get; set; }
}

public sealed class AdminFoodOrderListItemDto
{
    public string OrderNo { get; set; } = string.Empty;
    public string RestaurantName { get; set; } = string.Empty;
    public string OrderStatus { get; set; } = string.Empty;
    public string DispatchStatus { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
