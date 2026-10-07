using MudBlazor;

namespace Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;

/// <summary>업무 화면을 고르는 목록입니다. 인증 역할이나 서버 권한을 부여하지 않습니다.</summary>
public static class RoleWorkspaceCatalog
{
    public const string Community = "community";
    public const string Orderer = "orderer";
    public const string Restaurant = "restaurant";
    public const string FoodDriver = "food-driver";
    public const string Shipper = "shipper";
    public const string CargoDriver = "cargo-driver";
    public const string Warehouse = "warehouse";
    public const string Operator = "operator";

    public static IReadOnlyList<RoleWorkspaceDefinition> Roles { get; } = Array.AsReadOnly<RoleWorkspaceDefinition>(
    [
        new(Community, "생활", "제공할 것과 필요한 것을 이웃과 나누기", Icons.Material.Filled.Handshake),
        new(Orderer, "주문자", "음식을 주문하고 배달 진행 확인하기", Icons.Material.Filled.ShoppingBag),
        new(Restaurant, "음식점", "받은 주문을 조리하고 기사에게 전달하기", Icons.Material.Filled.Restaurant),
        new(FoodDriver, "음식기사", "배달 요청을 확인하고 음식 픽업·전달하기", Icons.Material.Filled.DeliveryDining),
        new(Shipper, "화주", "보낼 물건의 운송을 요청하고 진행 확인하기", Icons.Material.Filled.Inventory2),
        new(CargoDriver, "화물기사", "추천 운송을 확인하고 상차·하차하기", Icons.Material.Filled.LocalShipping),
        new(Warehouse, "창고", "물품을 받아 검수하고 보관·출고하기", Icons.Material.Filled.Warehouse),
        new(Operator, "운영자", "음식 주문의 진행과 예외 확인하기", Icons.Material.Filled.ManageAccounts)
    ]);

    public static RoleWorkspaceDefinition? Find(string? key)
        => Roles.FirstOrDefault(role => string.Equals(role.Key, key?.Trim(), StringComparison.OrdinalIgnoreCase));

    public static string? Normalize(string? key) => Find(key)?.Key;
}

public sealed record RoleWorkspaceDefinition(string Key, string Label, string Description, string Icon)
{
    public string Href => "/workspace/" + Key;
}
