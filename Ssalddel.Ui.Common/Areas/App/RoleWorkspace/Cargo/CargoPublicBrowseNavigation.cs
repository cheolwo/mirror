using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;

namespace Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Cargo;

public static class CargoPublicBrowseNavigation
{
    public const string PublicCargo = "/shipper/public-cargo";
    public static string ShipperRequestLogin => RoleWorkspaceNavigation.WithReturn("/workspace-login/shipper", ShipperRequestPageRoutes.Root)!;
    public static string DriverLogin => RoleWorkspaceNavigation.WithReturn("/workspace-login/cargo-driver", "/workspace/cargo-driver")!;

    public static string? LoginReturn(string roleKey, string? candidate)
    {
        var local = RoleWorkspaceNavigation.LocalRoute(candidate);
        if (local is null) return null;
        var path = local.Split('?', 2)[0];
        return path.StartsWith("/workspace/", StringComparison.Ordinal)
            || roleKey == RoleWorkspaceCatalog.Shipper && (path == ShipperRequestPageRoutes.Root || path == CargoNoticeNavigation.Mine)
            ? local : null;
    }
}
