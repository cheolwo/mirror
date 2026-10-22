using Ssalddel.Contracts.Admin.BusinessPackages;

namespace Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;

/// <summary>화면의 업무 문맥입니다. 역할 인증이나 서버 권한을 새로 만들지 않습니다.</summary>
public sealed record RoleWorkspaceBusinessContext(string RoleKey, string? BusinessCode)
{
    public string StateKey => Key(RoleKey, BusinessCode);

    public static string? DefaultBusiness(string? roleKey) => roleKey switch
    {
        RoleWorkspaceCatalog.Shipper or RoleWorkspaceCatalog.CargoDriver => BusinessPackageCatalog.FreightDelivery,
        RoleWorkspaceCatalog.Orderer or RoleWorkspaceCatalog.Restaurant or RoleWorkspaceCatalog.FoodDriver
            or RoleWorkspaceCatalog.Operator => BusinessPackageCatalog.FoodDelivery,
        RoleWorkspaceCatalog.Warehouse => BusinessPackageCatalog.OrderWarehouse,
        _ => null
    };

    public static RoleWorkspaceBusinessContext? Resolve(string? roleKey, string? businessCode = null)
    {
        var role = RoleWorkspaceCatalog.Normalize(roleKey);
        if (role is null) return null;
        var supplied = businessCode is not null;
        var business = supplied ? businessCode!.Trim().ToLowerInvariant() : DefaultBusiness(role);
        if (role == RoleWorkspaceCatalog.Operator)
            return business is BusinessPackageCatalog.FoodDelivery or BusinessPackageCatalog.FreightDelivery
                ? new(role, business) : null;
        // 다른 역할은 원래의 전문 업무만 연다. 임의 query로 역할을 다른 업무에 붙이지 않는다.
        return !supplied || string.Equals(business, DefaultBusiness(role), StringComparison.Ordinal)
            ? new(role, DefaultBusiness(role)) : null;
    }

    internal static string Key(string roleKey, string? businessCode)
        => roleKey + "|" + (businessCode ?? DefaultBusiness(roleKey) ?? string.Empty);

    public string Title => RoleKey == RoleWorkspaceCatalog.Operator
        ? BusinessCode == BusinessPackageCatalog.FreightDelivery ? "화물 관리자" : "음식 관리자"
        : RoleWorkspaceCatalog.Find(RoleKey)?.Label ?? "업무";
}
