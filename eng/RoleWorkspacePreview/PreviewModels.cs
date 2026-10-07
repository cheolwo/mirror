using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Cargo;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Food;
using Ssalddel.Ui.Common.Areas.App.Models;
using Ssalddel.Ui.Common.Areas.App.Services;
using Microsoft.AspNetCore.Components;
namespace RoleWorkspacePreview;

// 로컬 UI 예시이며 서버 업무·배차·입금·실제 기사 GPS의 증거가 아닙니다.
public sealed class PreviewAccess : IRoleWorkspaceAccess
{
    public event Action? Changed;
    private long _revision = 1;
    private bool _authenticated = true;
    public RoleWorkspaceIdentity GetIdentity(string roleKey) => new(_authenticated ? "preview-owner" : null, _revision, _authenticated);
    public Task EnsureInitializedAsync(string roleKey, CancellationToken ct = default) => Task.CompletedTask;
    public Task SignInAsync(string roleKey, string name, string password, CancellationToken ct = default) { _authenticated = true; _revision++; Changed?.Invoke(); return Task.CompletedTask; }
    public Task SignOutAsync(string roleKey, CancellationToken ct = default) { _authenticated = false; _revision++; Changed?.Invoke(); return Task.CompletedTask; }
}
public sealed class PreviewAdapter : IRoleWorkspaceAdapter
{
    private readonly string key;
    private readonly IRoleWorkspaceAccess access;
    private readonly PreviewRoleApi roleApi;
    private readonly IRoleWorkspaceAdapter? product;
    public PreviewAdapter(string key, IRoleWorkspaceAccess access, NavigationManager? navigation = null, PreviewRoleApi? sharedApi = null)
    {
        this.key = key; this.access = access;
        roleApi = sharedApi ?? new(key, access, navigation);
        product = key switch
        {
            "orderer" => new OrdererRoleWorkspaceAdapter(roleApi),
            "restaurant" => new RestaurantRoleWorkspaceAdapter(roleApi),
            "food-driver" => new FoodDriverRoleWorkspaceAdapter(roleApi, new PreviewFoodLocationProvider()),
            "operator" => new OperatorRoleWorkspaceAdapter(roleApi),
            "shipper" => new ShipperWorkspaceAdapter(new(roleApi)),
            "cargo-driver" => new CargoDriverWorkspaceAdapter(new(roleApi)),
            "warehouse" => new WarehouseWorkspaceAdapter(new(roleApi)),
            _ => null
        };
    }
    public string RoleKey => key;
    private bool _completed;
    public Task<RoleWorkspaceSnapshot> LoadAsync(string? selected, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!access.GetIdentity(key).IsAuthenticated) throw new RoleWorkspaceAccessException(401, "로그인 후 업무를 확인해 주세요.");
        if (product is not null) return product.LoadAsync(selected ?? roleApi.DefaultSelection, ct);
        var title = key switch { "community" => "함께하는 동네 일", "orderer" => "음식점 1의 주문", "restaurant" => "주문 1", "food-driver" => "음식점 1", "shipper" => "운송 의뢰 1", "cargo-driver" => "운송 1", "warehouse" => "입고 확인", _ => "주문 1 진행 확인" };
        var status = _completed ? "처리 완료" : key switch { "orderer" => "배달 중", "restaurant" => "조리 시작 대기", "food-driver" => "음식점 이동 중", "shipper" => "운송 중", "cargo-driver" => "상차지 이동 중", "warehouse" => "입고 대기", "operator" => "확인 필요", _ => "진행 중" };
        var action = key switch { "restaurant" => "조리 시작", "food-driver" => "음식점 도착", "cargo-driver" => "상차지 도착", "warehouse" => "입고 확인", "orderer" => "수령 확인", _ => "처리 확인" };
        var item = new RoleWorkspaceItem("item-1", title, status, key is "food-driver" or "cargo-driver" ? "현재 처리할 업무" : "선택한 업무",
            [new("장소 정보", [new("주소", "서울 중랑구 사가정로"), new("진행", status)]),
             new("요청 사항", [new("전달 요청", "문 앞에 놓아 주세요")]), new("주문 정보", [new("주문", "주문 1"), new("배달료", "4,000원")])],
            _completed ? [] : [new("advance", action, IsPrimary: true)],
            [new("item-1|pickup", "픽업", "pickup", 37.580, 127.087), new("item-1|dropoff", "전달", "dropoff", 37.583, 127.089)], IsCurrent: true);
        return Task.FromResult(new RoleWorkspaceSnapshot(key, [item], SelectedId: item.Id));
    }

    public Task PerformAsync(string itemId, string actionKey, Guid requestId, CancellationToken ct)
    {
        if (product is not null) return product.PerformAsync(itemId, actionKey, requestId, ct);
        _completed = true; return Task.CompletedTask;
    }
    public void Clear() { product?.Clear(); }
}
