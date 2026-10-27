using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Contracts.Driver.Food;
using Ssalddel.Contracts.Driver.Transport;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;

namespace Ssalddel.Ui.Common.Areas.App.RoleWorkspace.DriverCurrentWork;

/// <summary>전문 현재 업무의 GET만 소비합니다. 운행·위치·수신 복구 Adapter를 실행하지 않습니다.</summary>
public interface IDriverCurrentWorkClient
{
    event Action? Changed;
    RoleWorkspaceIdentity Identity(string roleKey);
    Task InitializeAsync(string roleKey, CancellationToken cancellationToken);
    Task<FoodDeliveryDriverCurrentWorkDto> FoodAsync(CancellationToken cancellationToken);
    Task<기사화물운송작업공간응답> CargoAsync(CancellationToken cancellationToken);
    Task<DistributedWorkspaceDto> PickingAsync(CancellationToken cancellationToken);
}

public sealed class DriverCurrentWorkClient(IRoleWorkspaceApi api, IRoleWorkspaceAccess access) : IDriverCurrentWorkClient
{
    public event Action? Changed { add => access.Changed += value; remove => access.Changed -= value; }
    public RoleWorkspaceIdentity Identity(string roleKey) => access.GetIdentity(roleKey);
    public Task InitializeAsync(string roleKey, CancellationToken cancellationToken)
        => access.EnsureInitializedAsync(roleKey, cancellationToken);
    public Task<FoodDeliveryDriverCurrentWorkDto> FoodAsync(CancellationToken cancellationToken)
        => api.GetAsync<FoodDeliveryDriverCurrentWorkDto>(RoleWorkspaceCatalog.FoodDriver,
            "api/v1/driver/food-deliveries/workspace/current", cancellationToken);
    public Task<기사화물운송작업공간응답> CargoAsync(CancellationToken cancellationToken)
        => api.GetAsync<기사화물운송작업공간응답>(RoleWorkspaceCatalog.CargoDriver,
            "api/v1/driver/transports/workspace", cancellationToken);
    public Task<DistributedWorkspaceDto> PickingAsync(CancellationToken cancellationToken)
        => api.GetAsync<DistributedWorkspaceDto>(RoleWorkspaceCatalog.CargoDriver,
            DistributedFulfillmentCodes.BaseRoute + "/workspace?roleKey=driver", cancellationToken);
}
