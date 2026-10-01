using Hongdal.Contracts.Common.Sales;
using Hongdal.Ui.Common.Areas.App.Services;
using HongdalApp.Services.Application;
using HongdalApp.Services.Commerce;
using HongdalApp.Services.Commerce.Amazon;
using HongdalApp.Services.Commerce.Coupang;
using HongdalApp.Services.Commerce.Ebay;
using HongdalApp.Services.Commerce.Listings.Commands;
using HongdalApp.Services.Commerce.Listings.Events;
using HongdalApp.Services.Commerce.Naver;
using HongdalApp.Services.Commerce.Orders;
using HongdalApp.Services.Commerce.Orders.Commands;
using HongdalApp.Services.Commerce.Orders.Events;
using HongdalApp.Services.Commerce.Shopify;
using HongdalApp.ViewModels.Shipper;
using Microsoft.Extensions.DependencyInjection;

namespace HongdalApp.Services;

internal static class ShipperSalesModule
{
    internal static IServiceCollection AddShipperSalesModule(this IServiceCollection services)
    {
        services.AddScoped<ShipperSalesService>();
        services.AddScoped<IShipperSalesService>(provider => provider.GetRequiredService<ShipperSalesService>());
        services.AddScoped<I판매채널계정Service>(provider => provider.GetRequiredService<ShipperSalesService>());
        services.AddScoped<I상품등록Service>(provider => provider.GetRequiredService<ShipperSalesService>());
        services.AddScoped<I채널출품Service>(provider => provider.GetRequiredService<ShipperSalesService>());
        services.AddTransient<화주판매채널계정PageViewModel>();
        services.AddSingleton<ICommerceChannelCatalog, CommerceChannelCatalog>();
        services.AddScoped<ICommerceChannelListingService, CommerceChannelListingService>();
        services.AddScoped<ICommerceOrderFulfillmentService, CommerceOrderFulfillmentService>();
        services.AddSingleton<ICommerceOrderSampleFeedService, CommerceOrderSampleFeedService>();
        services.AddScoped<IAppCommandHandler<ProcessCommerceOrderCommand, CommerceOrderFulfillmentResult>,
            ProcessCommerceOrderCommandHandler>();
        services.AddSingleton<IAppEventHandler<CommerceOrderProcessedEvent>, CommerceOrderProcessedEventHandler>();
        services.AddScoped<IAppCommandHandler<CreateChannelListingCommand, 채널출품항목응답?>,
            CreateChannelListingCommandHandler>();
        services.AddSingleton<IAppEventHandler<ChannelListingCreatedEvent>, ChannelListingCreatedEventHandler>();
        services.AddSingleton<IProductListingPayloadBuilder, NaverSmartStoreProductPayloadBuilder>();
        services.AddSingleton<IProductListingPayloadBuilder, CoupangWingProductPayloadBuilder>();
        services.AddSingleton<IProductListingPayloadBuilder, ShopifyProductPayloadBuilder>();
        services.AddSingleton<IProductListingPayloadBuilder, AmazonSpApiProductPayloadBuilder>();
        services.AddSingleton<IProductListingPayloadBuilder, EbayInventoryProductPayloadBuilder>();
        services.AddScoped<화주운송의뢰BulkApiService>();
        services.AddScoped<IShipperBulkRequestService>(provider =>
            provider.GetRequiredService<화주운송의뢰BulkApiService>());
        return services;
    }
}
