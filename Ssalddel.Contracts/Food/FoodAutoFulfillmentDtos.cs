using Ssalddel.Contracts.Common.Fulfillment;
using System.Text.Json.Serialization;

namespace Ssalddel.Contracts.Food;

/// <summary>현재 기능·식당 지원 설정입니다. 실제 접수 견적은 식당 확인 후 서버가 다시 산정합니다.</summary>
public sealed class FoodAutoFulfillmentQuoteResponse
{
    public bool Enabled { get; set; }
    public long RestaurantId { get; set; }
    public decimal RestaurantSupportKrw { get; set; }
    public long RestaurantSupportRevision { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DeliveryCostSharingPolicy? CostSharingPolicy { get; set; }
    public string Notice { get; set; } = AutoFulfillmentNotice.Simulation;
    public IReadOnlyList<AutoFulfillmentProviderDescriptor> Providers { get; set; } = [];
}
