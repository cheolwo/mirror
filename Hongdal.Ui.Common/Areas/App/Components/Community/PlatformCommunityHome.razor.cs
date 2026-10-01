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
    [Parameter]
    public bool BoardIndexOnly { get; set; }

    [Parameter]
    public bool ListOnly { get; set; }

    [Parameter]
    public string? InitialBoard { get; set; }

    [Parameter]
    public bool StartInComposeMode { get; set; }

    [Parameter]
    public string AppName { get; set; } = "Hongdal";

    [Parameter]
    public string RoleLabel { get; set; } = "플랫폼 구성원";

    [Parameter]
    public string AppKey { get; set; } = "platform";

    [Parameter]
    public IReadOnlyList<PlatformHomeQuickAction> QuickActions { get; set; } = [];

    [Parameter]
    public IReadOnlyList<PlatformHomeWorkspaceProfile> Workspaces { get; set; } = [];

    [Parameter]
    public IReadOnlyList<HongdalCardinalNavigationOption> CardinalNavigationOptions { get; set; } = [];

    [Parameter]
    public bool ShowPrajnaUpayaNavigator { get; set; }

    [Parameter]
    public string UpayaLabel { get; set; } = "방편";

    [Parameter]
    public string PrajnaLabel { get; set; } = "정보";

    [Parameter]
    public string? UpayaHref { get; set; }

    [Parameter]
    public string? PrajnaHref { get; set; }

    [Parameter]
    public bool BaguaCenterOpensCommunity { get; set; }

    [Parameter]
    public bool BaguaCenterShowsTaegeuk { get; set; } = true;

    [Parameter]
    public string? BaguaCenterSymbol { get; set; } = "☶";

    [Parameter]
    public string BaguaCenterTrigramName { get; set; } = "간";

    [Parameter]
    public string BaguaCenterDestinationLabel { get; set; } = "커뮤니티";

    [Parameter]
    public bool UseBaguaRoleTransitionPages { get; set; }

    [Parameter]
    public string? BaguaPerspectiveRoleCode { get; set; }

    [Parameter]
    public bool CanManageCommunityPosts { get; set; }

    [Parameter]
    public bool ShowRealtimeBest { get; set; }

    [Parameter]
    public bool UseCompactHomeSummary { get; set; }

    [Parameter]
    public RenderFragment? CommunityModeContent { get; set; }

    [Parameter]
    public RenderFragment? WorkModeContent { get; set; }

    [Parameter]
    public string? QueryLedgerTemplateKey { get; set; }

    [Parameter]
    public string? QueryDiagramMode { get; set; }

    [Parameter]
    public long? QueryPostId { get; set; }

    [Parameter]
    public string? QuerySeedPostTitle { get; set; }

    [Parameter]
    public string? QueryBoardName { get; set; }

}
