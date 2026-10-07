using Microsoft.Extensions.DependencyInjection;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Cargo;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Food;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Client.RoleWorkspace;

public static class RoleWorkspaceRegistration
{
    public static IServiceCollection AddUnifiedRoleWorkspaces(this IServiceCollection services)
    {
        services.AddScoped<RoleWorkspaceAccess>();
        services.AddScoped<IRoleWorkspaceAccess>(sp => sp.GetRequiredService<RoleWorkspaceAccess>());
        services.AddScoped<IRoleWorkspaceApi>(sp => sp.GetRequiredService<RoleWorkspaceAccess>());
        services.AddTransient<RoleWorkspaceViewModel>();
        services.AddScoped<RoleWorkspaceState>();
        services.AddScoped<I주문자앱인증Service, RoleOrdererAuthenticationBridge>();
        services.AddScoped<RoleOrdererFoodApiClient>();
        services.AddScoped<I주문자음식주문쓰기Service>(sp => sp.GetRequiredService<RoleOrdererFoodApiClient>());
        services.AddScoped<I주문자음식주문접수결과Service>(sp => sp.GetRequiredService<RoleOrdererFoodApiClient>());
        services.AddScoped<I주문자음식주문읽기Service>(sp => sp.GetRequiredService<RoleOrdererFoodApiClient>());
        services.AddScoped<I주문자음식주문수령확인Service>(sp => sp.GetRequiredService<RoleOrdererFoodApiClient>());
        services.AddScoped<I주문자음식주문취소Service>(sp => sp.GetRequiredService<RoleOrdererFoodApiClient>());
        services.AddScoped<IRoleWorkspaceAdapter, OrdererRoleWorkspaceAdapter>();
        services.AddScoped<IRoleWorkspaceAdapter, RestaurantRoleWorkspaceAdapter>();
        services.AddScoped<IRoleWorkspaceAdapter, FoodDriverRoleWorkspaceAdapter>();
        services.AddScoped<IRoleWorkspaceAdapter, OperatorRoleWorkspaceAdapter>();
        services.AddScoped<ShipperWorkspaceApiClient>();
        services.AddScoped<CargoDriverWorkspaceApiClient>();
        services.AddScoped<WarehouseWorkspaceApiClient>();
        services.AddScoped<IRoleWorkspaceAdapter, ShipperWorkspaceAdapter>();
        services.AddScoped<IRoleWorkspaceAdapter, CargoDriverWorkspaceAdapter>();
        services.AddScoped<IRoleWorkspaceAdapter, WarehouseWorkspaceAdapter>();
        return services;
    }
}
