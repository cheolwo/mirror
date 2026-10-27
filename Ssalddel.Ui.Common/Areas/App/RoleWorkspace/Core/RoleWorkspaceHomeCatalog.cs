using Ssalddel.Contracts.Admin.BusinessPackages;

namespace Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;

/// <summary>홈의 업무별 진입을 조립합니다. 역할 자격과 업무 원장은 변경하지 않습니다.</summary>
public static class RoleWorkspaceHomeCatalog
{
    public static IReadOnlyList<RoleWorkspaceHomeGroup> Groups { get; } = Array.AsReadOnly<RoleWorkspaceHomeGroup>(
    [
        new(BusinessPackageCatalog.FreightDelivery, "화물 운송", Array.AsReadOnly<RoleWorkspaceHomeEntry>(
        [
            Entry(RoleWorkspaceCatalog.Shipper),
            Entry(RoleWorkspaceCatalog.CargoDriver),
            Entry(RoleWorkspaceCatalog.FreightBroker),
            Entry(RoleWorkspaceCatalog.Operator, "화물 관리자", "운송 진행·배차·예외 확인", BusinessPackageCatalog.FreightDelivery)
        ])),
        new(BusinessPackageCatalog.FoodDelivery, "음식 배달", Array.AsReadOnly<RoleWorkspaceHomeEntry>(
        [
            Entry(RoleWorkspaceCatalog.Orderer),
            Entry(RoleWorkspaceCatalog.Restaurant),
            Entry(RoleWorkspaceCatalog.FoodDriver),
            Entry(RoleWorkspaceCatalog.Operator, "음식 관리자", "음식 주문·배달·예외 확인", BusinessPackageCatalog.FoodDelivery)
        ])),
        new("other", "생활·창고", Array.AsReadOnly<RoleWorkspaceHomeEntry>(
        [
            Entry(RoleWorkspaceCatalog.Community),
            Entry(RoleWorkspaceCatalog.Warehouse)
        ]))
    ]);

    private static RoleWorkspaceHomeEntry Entry(string roleKey, string? label = null, string? description = null, string? business = null)
    {
        var role = RoleWorkspaceCatalog.Find(roleKey)
            ?? throw new InvalidOperationException($"Unknown role '{roleKey}'.");
        return new(role.Key, label ?? role.Label, description ?? role.Description, role.Icon,
            business is null ? role.Href : role.Href + "?business=" + business);
    }
}

public sealed record RoleWorkspaceHomeGroup(string Key, string Label, IReadOnlyList<RoleWorkspaceHomeEntry> Entries);

public sealed record RoleWorkspaceHomeEntry(string RoleKey, string Label, string Description, string Icon, string Href);
