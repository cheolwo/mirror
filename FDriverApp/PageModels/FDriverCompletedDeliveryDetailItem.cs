using CommunityToolkit.Mvvm.ComponentModel;
using Ssalddel.Contracts.Food;

namespace FDriverApp.PageModels;

/// <summary>Only the financial projection survives expiry. Private text is never persisted.</summary>
public sealed partial class FDriverCompletedDeliveryDetailItem : ObservableObject
{
    public required FDriverDailySettlementItem Settlement { get; init; }
    [ObservableProperty] private bool _hasOrderDetails;
    [ObservableProperty] private bool _hasCustomerDetails;
    [ObservableProperty] private string _restaurantAddress = string.Empty;
    [ObservableProperty] private string _totalOrderAmountText = string.Empty;
    [ObservableProperty] private string _menuText = string.Empty;
    [ObservableProperty] private string _customerName = string.Empty;
    [ObservableProperty] private string _customerPhone = string.Empty;
    [ObservableProperty] private string _customerAddress = string.Empty;
    [ObservableProperty] private string _deliveryInstructions = string.Empty;

    public void ApplyPrivateDetails(FoodDeliveryCompletedDeliveryDetailDto detail)
    {
        ClearPrivateDetails();
        if (detail.OrderDetails is { } order)
        {
            RestaurantAddress = order.RestaurantAddress ?? string.Empty;
            TotalOrderAmountText = $"{order.TotalOrderAmount:#,0.##}원";
            MenuText = string.Join(Environment.NewLine, (order.Items ?? []).Select(item =>
                $"{item.MenuName} · {item.Quantity:N0}개 · 단가 {item.UnitPrice:#,0.##}원"));
            HasOrderDetails = true;
        }
        if (detail.CustomerDetails is { } customer)
        {
            CustomerName = customer.DisplayName ?? string.Empty;
            CustomerPhone = customer.ContactPhone ?? string.Empty;
            CustomerAddress = customer.Address ?? string.Empty;
            DeliveryInstructions = customer.DeliveryInstructions ?? string.Empty;
            HasCustomerDetails = true;
        }
    }

    public void ClearPrivateDetails()
    {
        HasOrderDetails = false;
        HasCustomerDetails = false;
        RestaurantAddress = TotalOrderAmountText = MenuText = string.Empty;
        CustomerName = CustomerPhone = CustomerAddress = DeliveryInstructions = string.Empty;
    }
}
