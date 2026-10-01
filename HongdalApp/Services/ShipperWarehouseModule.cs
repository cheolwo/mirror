using Hongdal.Contracts.Shipper.Request;
using Hongdal.Ui.Common.Areas.App.Services;
using HongdalApp.Services.Application;
using HongdalApp.Services.Warehouse.Fulfillment;
using HongdalApp.Services.Warehouse.Reconsignment.Commands;
using HongdalApp.Services.Warehouse.Reconsignment.Events;
using Microsoft.Extensions.DependencyInjection;

namespace HongdalApp.Services;

internal static class ShipperWarehouseModule
{
    internal static IServiceCollection AddShipperWarehouseModule(this IServiceCollection services)
    {
        services.AddScoped<ShipperWarehouseService>();
        services.AddScoped<IWarehouseWorkspaceService>(provider =>
            provider.GetRequiredService<ShipperWarehouseService>());
        services.AddScoped<IShipperWarehouseWorkflowService>(provider =>
            provider.GetRequiredService<ShipperWarehouseService>());
        services.AddScoped<IWarehousePickingPlanner, WarehousePickingPlanner>();
        services.AddScoped<IAppCommandHandler<CreateReconsignmentOrderCommand, 화주운송의뢰응답?>,
            CreateReconsignmentOrderCommandHandler>();
        services.AddSingleton<IAppEventHandler<ReconsignmentOrderCreatedEvent>,
            ReconsignmentOrderCreatedEventHandler>();
        return services;
    }
}
