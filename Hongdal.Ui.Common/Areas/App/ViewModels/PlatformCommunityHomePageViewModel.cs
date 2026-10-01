using System.Net;
using CommunityToolkit.Mvvm.ComponentModel;
using Hongdal.Contracts.Common.Community;
using Hongdal.Contracts.Common.Metadata;
using Hongdal.Ui.Common.Areas.App.Services;
using Microsoft.AspNetCore.Components.Forms;

namespace Hongdal.Ui.Common.Areas.App.ViewModels;

[HongdalCommunityV0Module(
    HongdalCommunityV0ModuleKeys.Ui,
    HongdalModuleKind.ClientFeature,
    "커뮤니티 홈에서 게시판·글 목록·작성·참여·원장·다이어그램 하위 ViewModel을 조립",
    ReleaseStage = HongdalCommunityV0ReleaseStages.IndependentExecution,
    Boundary = "기본 진입은 게시판과 글이며 후속 업무 도구는 사용자 행동과 기능 플래그 뒤에서만 엽니다.")]
public sealed class PlatformCommunityHomePageViewModel : PageViewModelBase
{
    private bool _isEvidenceChartToolOpen;

    public PlatformCommunityHomePageViewModel(
        CommunityPostComposerViewModel composer,
        CommunityPostListPageViewModel postList,
        PlatformCommunityHomeShellViewModel shell,
        PlatformCommunityBoardWorkspaceViewModel boards,
        PlatformCommunityPostEngagementViewModel engagement,
        PlatformCommunityLedgerPickerViewModel ledgerPicker,
        YouTubeFoodCommunityDiscoveryViewModel foodDiscovery,
        PlatformCommunityDiagramWorkspaceViewModel diagramWorkspace,
        PlatformCommunityWishFlowViewModel wishFlow,
        CommunityAuthoringEvidenceChartViewModel evidenceChart,
        PlatformCommunityWarehouseProxyViewModel warehouseProxy)
    {
        Composer = 하위ViewModel등록(composer, 수명소유: true);
        PostList = 하위ViewModel등록(postList, 수명소유: true);
        Shell = 하위ViewModel등록(shell, 수명소유: true);
        Boards = 하위ViewModel등록(boards, 수명소유: true);
        Engagement = 하위ViewModel등록(engagement, 수명소유: true);
        LedgerPicker = 하위ViewModel등록(ledgerPicker, 수명소유: true);
        FoodDiscovery = 하위ViewModel등록(foodDiscovery, 수명소유: true);
        DiagramWorkspace = 하위ViewModel등록(diagramWorkspace, 수명소유: true);
        WishFlow = 하위ViewModel등록(wishFlow);
        EvidenceChart = 하위ViewModel등록(evidenceChart, 수명소유: true);
        WarehouseProxy = 하위ViewModel등록(warehouseProxy, 수명소유: true);
    }

    public CommunityPostComposerViewModel Composer { get; }
    public CommunityPostListPageViewModel PostList { get; }
    public PlatformCommunityHomeShellViewModel Shell { get; }
    public PlatformCommunityBoardWorkspaceViewModel Boards { get; }
    public PlatformCommunityPostEngagementViewModel Engagement { get; }
    public PlatformCommunityLedgerPickerViewModel LedgerPicker { get; }
    public YouTubeFoodCommunityDiscoveryViewModel FoodDiscovery { get; }
    public PlatformCommunityDiagramWorkspaceViewModel DiagramWorkspace { get; }
    public PlatformCommunityWishFlowViewModel WishFlow { get; }
    public CommunityAuthoringEvidenceChartViewModel EvidenceChart { get; }
    public PlatformCommunityWarehouseProxyViewModel WarehouseProxy { get; }
    public CommunityPostJourneyCollectionViewModel ActionJourneys => Engagement.Journeys;

    public bool IsEvidenceChartToolOpen
    {
        get => _isEvidenceChartToolOpen;
        private set => SetProperty(ref _isEvidenceChartToolOpen, value);
    }

    public void Configure(string appKey, string defaultRoleTag)
    {
        Composer.Configure(appKey, defaultRoleTag);
        PostList.Configure(appKey);
    }

    public void OpenEvidenceChartTool()
    {
        EvidenceChart.PrepareFromDraft(Composer.Draft.Title, Composer.Draft.Body);
        Composer.Open();
        IsEvidenceChartToolOpen = true;
        Composer.SetStatus(
            "주장을 뒷받침할 수치와 출처, 기준일, 해석과 한계를 함께 정리합니다.",
            CommunityComposerMessageKind.Info);
    }

    public void CloseEvidenceChartTool()
        => IsEvidenceChartToolOpen = false;

    public bool ApplyEvidenceChartToDraft()
    {
        var result = EvidenceChart.ApplyToDraft(Composer.Draft);
        if (!result.Succeeded)
        {
            Composer.SetStatus(result.Message, CommunityComposerMessageKind.Warning);
            return false;
        }

        Composer.Open();
        IsEvidenceChartToolOpen = false;
        Composer.SetStatus(
            $"{result.Message} 게시 뒤에도 같은 데이터로 그래프와 요약 통계가 표시됩니다.",
            CommunityComposerMessageKind.Success);
        return true;
    }

    public void ResetEvidenceChartTool()
    {
        EvidenceChart.Reset();
        IsEvidenceChartToolOpen = false;
    }

    protected override async Task 불러오기Async(
        bool 새로고침,
        CancellationToken cancellationToken)
    {
        var postsLoaded = 새로고침
            ? await PostList.새로고침Async(cancellationToken)
            : await PostList.초기화Async(cancellationToken);
        if (!postsLoaded)
        {
            throw new InvalidOperationException(
                PostList.오류메시지 ?? "커뮤니티 게시글 목록을 불러오지 못했습니다.");
        }
    }
}
