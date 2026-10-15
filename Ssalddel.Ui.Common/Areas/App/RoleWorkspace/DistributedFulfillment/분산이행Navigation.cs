using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;

namespace Ssalddel.Ui.Common.Areas.App.RoleWorkspace.DistributedFulfillment;

public static class 분산이행Navigation
{
    public static bool Supports(string? role) => role is "warehouse" or "shipper" or "cargo-driver" or "orderer" or "operator";
    public static string ValidateRole(string role) => Supports(role) ? role
        : throw new ArgumentException("분산 배송을 사용하는 활동 역할을 확인해 주세요.");
    public static string DefaultScreen(string role) => ValidateRole(role) switch
    { "warehouse" => "sites", "shipper" => "inventory", "cargo-driver" => "round", "operator" => "waves", _ => "orders" };
    public static bool AllowsScreen(string role, string screen) => role switch
    {
        "warehouse" => screen is "sites" or "agreements" or "inventory" or "inbound" or "returns" or "waves" or "stock-release",
        "shipper" => screen is "agreements" or "inventory" or "inbound" or "orders" or "stock-release",
        "cargo-driver" => screen is "round" or "stock-release",
        "orderer" => screen == "orders",
        "operator" => screen == "waves",
        _ => false
    };
    public static string Href(string role, string? screen = null, string? id = null)
    {
        role = ValidateRole(role); screen ??= DefaultScreen(role);
        if (!AllowsScreen(role, screen)) throw new ArgumentException("활동 역할의 업무 화면을 확인해 주세요.");
        var clean = RoleWorkspaceNavigation.StableId(id);
        return $"/workspace/{role}/distributed/{screen}" + (clean is null ? "" : "?selected=" + Uri.EscapeDataString(clean));
    }
    public static string Title(string screen, string role = "") => screen switch
    {
        "sites" => "보관 공간과 동의",
        "agreements" => "공간 보관 합의",
        "returns" => "반품 입고 확인",
        "inventory" => "거점별 상품 재고",
        "stock-release" => "잔여 재고 반출",
        "inbound" => "예정 상품 입고",
        "round" => "현재 순회 배송",
        "waves" => "시간대 배송 묶음",
        _ => role == "shipper" ? "상품 출고 주문" : "내 상품 배송"
    };
    public static string ResumeHref(string role, string screen, string? selectedId, Guid? requestId, string? returnHref, bool driverAlias = false)
    {
        if (!AllowsScreen(role, screen)) return "/";
        var path = driverAlias && role == "cargo-driver" ? $"/driver/distributed/{screen}" : $"/workspace/{role}/distributed/{screen}";
        var query = new List<string>();
        if (RoleWorkspaceNavigation.StableId(selectedId) is { } id) query.Add("selected=" + Uri.EscapeDataString(id));
        if (requestId is { } request && request != Guid.Empty) query.Add("requestId=" + request.ToString("D"));
        if (RoleWorkspaceNavigation.LocalRoute(returnHref) is { } safe) query.Add("returnUrl=" + Uri.EscapeDataString(safe));
        return path + (query.Count == 0 ? "" : "?" + string.Join('&', query));
    }
    public static string WarehouseLoginReturn(string? returnUrl)
    {
        var safe = RoleWorkspaceNavigation.LocalRoute(returnUrl);
        return safe is not null && safe.Split('?', 2)[0].StartsWith("/workspace/warehouse/distributed/", StringComparison.Ordinal)
            ? safe : Href("warehouse", "sites");
    }
}
