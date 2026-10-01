using HongdalApp.Services.Application;

namespace HongdalApp.Services.Commerce.Orders.Commands;

public sealed record ProcessCommerceOrderCommand(ExternalCommerceOrder Order)
    : IAppCommand<CommerceOrderFulfillmentResult>;
