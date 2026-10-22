using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;
using SsalddelApp.Services.Application;
using SsalddelApp.Services.Warehouse.Fulfillment;
using SsalddelApp.Services.Warehouse.Reconsignment.Commands;
using SsalddelApp.Services.Warehouse.Reconsignment.Events;
using Microsoft.Extensions.DependencyInjection;

namespace SsalddelApp.Services;

internal static class ShipperWarehouseModule
{
    internal static IServiceCollection AddShipperWarehouseModule(this IServiceCollection services)
    {
        services.AddScoped<ShipperWarehouseService>();
        services.AddScoped<IWarehouseWorkspaceService>(provider =>
            provider.GetRequiredService<ShipperWarehouseService>());
        services.AddScoped<IShipperWarehouseWorkflowService>(provider =>
            provider.GetRequiredService<ShipperWarehouseService>());
        services.AddTransient<InboundRequestPageViewModel>();
        services.AddScoped<I가입고사진신청Client, 가입고사진신청Client>();
        services.AddTransient<가입고사진신청ViewModel>();
        services.AddScoped<IWarehousePickingPlanner, WarehousePickingPlanner>();
        services.AddScoped<IAppCommandHandler<CreateReconsignmentOrderCommand, 화주운송의뢰응답?>,
            CreateReconsignmentOrderCommandHandler>();
        services.AddSingleton<IAppEventHandler<ReconsignmentOrderCreatedEvent>,
            ReconsignmentOrderCreatedEventHandler>();
        return services;
    }
}
