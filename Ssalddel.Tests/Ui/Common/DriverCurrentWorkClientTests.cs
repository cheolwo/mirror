using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Contracts.Driver.Food;
using Ssalddel.Contracts.Driver.Transport;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.DriverCurrentWork;

namespace Ssalddel.Tests.Ui.Common;

public sealed class DriverCurrentWorkClientTests
{
    [Fact]
    public async Task 본인전문현재업무세개만GET하고_운행위치수신명령은호출하지않는다()
    {
        var transport = new ReadOnlyTransport(); var client = new DriverCurrentWorkClient(transport, transport);
        await client.InitializeAsync(RoleWorkspaceCatalog.FoodDriver, default);
        await client.FoodAsync(default); await client.CargoAsync(default); await client.PickingAsync(default);
        Assert.Equal(new[]
        {
            ("food-driver", "api/v1/driver/food-deliveries/workspace/current"),
            ("cargo-driver", "api/v1/driver/transports/workspace"),
            ("cargo-driver", "api/v1/distributed-fulfillment/workspace?roleKey=driver")
        }, transport.Reads);
        Assert.Equal(["food-driver"], transport.Initialized);
        var changed = 0; Action handler = () => changed++; client.Changed += handler;
        transport.Raise(); Assert.Equal(1, changed); client.Changed -= handler;
        transport.Raise(); Assert.Equal(1, changed);
    }

    private sealed class ReadOnlyTransport : IRoleWorkspaceApi, IRoleWorkspaceAccess
    {
        public event Action? Changed;
        public List<(string, string)> Reads { get; } = [];
        public List<string> Initialized { get; } = [];
        public RoleWorkspaceIdentity GetIdentity(string roleKey) => new("owner", 1, true);
        public Task EnsureInitializedAsync(string roleKey, CancellationToken cancellationToken = default)
        { Initialized.Add(roleKey); return Task.CompletedTask; }
        public Task<T> GetAsync<T>(string roleKey, string path, CancellationToken cancellationToken)
        {
            Reads.Add((roleKey, path));
            object value = typeof(T) == typeof(FoodDeliveryDriverCurrentWorkDto) ? new FoodDeliveryDriverCurrentWorkDto()
                : typeof(T) == typeof(기사화물운송작업공간응답) ? new 기사화물운송작업공간응답() : new DistributedWorkspaceDto();
            return Task.FromResult((T)value);
        }
        public Task<T?> PostAsync<T>(string roleKey, string path, object body, CancellationToken cancellationToken) => throw new InvalidOperationException("Unexpected POST");
        public Task<T?> PutAsync<T>(string roleKey, string path, object body, CancellationToken cancellationToken) => throw new InvalidOperationException("Unexpected PUT");
        public Task<T?> UploadAsync<T>(string roleKey, string path, HttpContent body, CancellationToken cancellationToken) => throw new InvalidOperationException("Unexpected upload");
        public Task SignInAsync(string roleKey, string name, string password, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Unexpected sign-in");
        public Task SignOutAsync(string roleKey, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Unexpected sign-out");
        public void Raise() => Changed?.Invoke();
    }
}
