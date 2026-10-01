namespace HongdalApp.Services.Commerce.Orders;

public interface ICommerceOrderSampleFeedService
{
    IReadOnlyList<ExternalCommerceOrder> GetSampleOrders();
}
