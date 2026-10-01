using System.Globalization;
using Hongdal.Contracts.Common.Community;
using Hongdal.Contracts.Common.Inbound;
using Hongdal.Contracts.Common.Versioning;
using Hongdal.Contracts.Common.Warehouse;
using Hongdal.Ui.Common.Areas.App.Models;
using Hongdal.Ui.Common.Areas.App.Services;
using Hongdal.Ui.Common.Areas.App.ViewModels;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using MudBlazor;

namespace Hongdal.Ui.Common.Areas.App.Components.Community;

public partial class PlatformCommunityHome
{
    private static 다이어그램레이어정의? FindDiagramLayer(string key)
        => 다이어그램레이어정의s.FirstOrDefault(layer => string.Equals(layer.Key, key, StringComparison.OrdinalIgnoreCase));

    private bool IsDiagramLayerVisible(string key)
    {
        var layer = FindDiagramLayer(key);
        return DiagramCanvas.IsLayerVisible(key, layer?.IsLocked == true);
    }

    private string BuildDiagramCanvasClass(string baseClass)
    {
        var hiddenClasses = 다이어그램레이어정의s
            .Where(layer => !IsDiagramLayerVisible(layer.Key))
            .Select(layer => $"platform-ledger-flow-diagram--hide-layer-{layer.Key}");

        return string.Join(' ', hiddenClasses.Prepend(baseClass));
    }

    private static IReadOnlyList<CommunityLedgerTemplateResponse> LedgerTemplates => CommunityLedgerTemplateCatalog.All;

    private IReadOnlyList<PlatformHomeWorkspaceProfile> UnifiedWorkspaces
        => Workspaces.Count > 0 ? Workspaces : PlatformHomeWorkspaceCatalog.DefaultWorkspaces;

    private CommunityLedgerTemplateResponse SelectedLedgerTemplate
        => CommunityLedgerTemplateCatalog.Find(selectedLedgerTemplateKey);

    private 현재원장컨텍스트? 선택현재원장
        => string.IsNullOrWhiteSpace(선택현재원장Id)
            ? null
            : 현재원장스냅샷목록.FirstOrDefault(ledger =>
                string.Equals(ledger.Id, 선택현재원장Id, StringComparison.OrdinalIgnoreCase));

}
