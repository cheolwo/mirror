using System.Globalization;
using Hongdal.Contracts.Common.Community;
using Hongdal.Contracts.Common.Inbound;
using Hongdal.Contracts.Common.Versioning;
using Hongdal.Contracts.Common.Warehouse;
using Hongdal.Ui.Common.Areas.App.Models;
using Hongdal.Ui.Common.Areas.App.Services;
using Hongdal.Ui.Common.Areas.App.ViewModels;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using MudBlazor;

namespace Hongdal.Ui.Common.Areas.App.Components.Community;

public partial class PlatformCommunityHome
{
    private string boardIndexSearchText
    {
        get => Boards.IndexSearchText;
        set => Boards.IndexSearchText = value;
    }

    private PlatformCommunityHomeShellViewModel Shell => ViewModel.Shell;
    private PlatformCommunityBoardWorkspaceViewModel Boards => ViewModel.Boards;
    private PlatformCommunityPostEngagementViewModel Engagement => ViewModel.Engagement;
    private PlatformCommunityLedgerPickerViewModel LedgerPicker => ViewModel.LedgerPicker;
    private PlatformCommunityDiagramWorkspaceViewModel DiagramWorkspace => ViewModel.DiagramWorkspace;
    private PlatformCommunityWishFlowViewModel WishFlow => ViewModel.WishFlow;
    private PlatformCommunityWarehouseProxyViewModel WarehouseProxy => ViewModel.WarehouseProxy;
    private PlatformCommunityDiagramChatViewModel DiagramChat => DiagramWorkspace.Chat;
    private PlatformCommunityDiagramCanvasViewModel DiagramCanvas => DiagramWorkspace.Canvas;
    private List<PlatformCommunityPostLedgerChoiceResponse> myLedgers => LedgerPicker.Items;
    private List<PlatformCommunityBoardResponse> approvedBoards => Boards.ApprovedBoards;
    private List<PlatformCommunityBoardResponse> pendingBoardRequests => Boards.PendingBoardRequests;
    private PlatformCommunityBoardForm boardForm => Boards.Form;
    private Dictionary<long, CommunityPostOpportunityListResponse> postOpportunities => Engagement.Opportunities;
    private HashSet<long> pendingPostParticipationIds => Engagement.PendingPostParticipationIds;
    private Dictionary<long, string> boardReviewMemo => Boards.ReviewMemos;
    private Dictionary<string, string> 원장블록입력값 => DiagramWorkspace.LedgerBlockValues;
    private Dictionary<string, string> diagramFormValues => DiagramWorkspace.FormValues;
    private Dictionary<string, string> 원장Api경로변수값 => DiagramWorkspace.ApiPathParameterValues;
    private Dictionary<string, WorkflowApiEndpointDto> apiEndpointMetadata => DiagramWorkspace.ApiEndpointMetadata;
    private Dictionary<string, bool> featureFlagStates => DiagramWorkspace.FeatureFlagStates;
    private readonly List<원장블록노드> 팔레트원장블록노드목록 = [];
    private List<string> diagramNodeOrder => DiagramCanvas.NodeOrder;
    private List<원장블록연결선> customDiagramEdges => DiagramCanvas.CustomEdges;
    private Dictionary<string, string> diagramEdgeLabels => DiagramCanvas.EdgeLabels;
    private Dictionary<string, DiagramEdgeStyleKind> diagramEdgeStyles => DiagramCanvas.EdgeStyles;
    private static readonly IReadOnlyList<DiagramConnectionHandleKind> DiagramConnectionHandles =
    [
        DiagramConnectionHandleKind.Top,
        DiagramConnectionHandleKind.Right,
        DiagramConnectionHandleKind.Bottom,
        DiagramConnectionHandleKind.Left
    ];
    private const string DiagramLayerStructure = "structure";
    private const string DiagramLayerProcedure = "procedure";
    private const string DiagramLayerRole = "role";
    private const string DiagramLayerState = "state";
    private const string DiagramLayerEvidence = "evidence";
    private const string DiagramLayerRisk = "risk";
    private const string DiagramLayerApi = "api";
    private HashSet<string> hiddenDiagramLayerKeys => DiagramCanvas.HiddenLayerKeys;
    private static readonly IReadOnlyList<다이어그램레이어정의> 다이어그램레이어정의s =
    [
        new(DiagramLayerStructure, "구조", "원장 노드 자체입니다. 캔버스의 기준이므로 항상 표시됩니다.", 10, 10, Icons.Material.Filled.Hub, Color.Primary, true, true),
        new(DiagramLayerProcedure, "절차", "노드 사이의 화살표와 흐름 의미입니다.", 20, 20, Icons.Material.Filled.AccountTree, Color.Secondary, true),
        new(DiagramLayerRole, "역할", "각 노드를 주로 처리하는 참여자 역할입니다.", 30, 30, Icons.Material.Filled.PushPin, Color.Info, true),
        new(DiagramLayerState, "상태", "완료, 현재 단계, 대기 상태입니다.", 40, 40, Icons.Material.Filled.Schedule, Color.Success, true),
        new(DiagramLayerEvidence, "증빙", "필수 확인, 사진, 수령, 정산 근거입니다.", 50, 50, Icons.Material.Filled.FactCheck, Color.Warning, true),
        new(DiagramLayerRisk, "리스크", "신고, 분쟁, 오류, 보류처럼 다른 표시를 덮어야 하는 신호입니다.", 60, 60, Icons.Material.Filled.HelpOutline, Color.Error, true),
        new(DiagramLayerApi, "API", "노드에서 호출 가능한 처리 표면과 기존 API 경로입니다.", 70, 35, Icons.Material.Filled.OpenInNew, Color.Info, true)
    ];
    private CommunityPostComposerViewModel Composer => ViewModel.Composer;
    private CommunityAuthoringEvidenceChartViewModel EvidenceChart => ViewModel.EvidenceChart;
    private YouTubeFoodCommunityDiscoveryViewModel FoodDiscovery => ViewModel.FoodDiscovery;
    private IReadOnlyList<PlatformCommunityPostResponse> posts => ViewModel.PostList.Items;
    private List<IBrowserFile> selectedFiles => Composer.SelectedFiles;
    private CommunityPostComposerDraftViewModel form => Composer.Draft;
    private string selectedLedgerTemplateKey
    {
        get => DiagramWorkspace.SelectedLedgerTemplateKey;
        set => DiagramWorkspace.SelectedLedgerTemplateKey = value;
    }
    private string selectedBoardFilter
    {
        get => ViewModel.PostList.SelectedBoard;
        set => ViewModel.PostList.SelectedBoard = value;
    }

    private string selectedForumListFilter
    {
        get => ViewModel.PostList.SelectedListFilter;
        set => ViewModel.PostList.SelectedListFilter = value;
    }

    private CommunityPostViewMode communityPostViewMode
    {
        get => ViewModel.PostList.ViewMode;
        set => ViewModel.PostList.ViewMode = value;
    }

    private string communityPostSearchText
    {
        get => ViewModel.PostList.SearchText;
        set => ViewModel.PostList.SearchText = value;
    }
    private string? pendingLedgerId
    {
        get => LedgerPicker.PendingLedgerId;
        set => LedgerPicker.PendingLedgerId = value;
    }
    private long? selectedForumPostId
    {
        get => ViewModel.PostList.SelectedPostId;
        set => ViewModel.PostList.SelectedPostId = value;
    }
    private string? selectedForumSeedPostTitle
    {
        get => Engagement.SelectedSeedPostTitle;
        set => Engagement.SelectedSeedPostTitle = value;
    }
    private string? 선택현재원장Id
    {
        get => DiagramWorkspace.SelectedCurrentLedgerId;
        set => DiagramWorkspace.SelectedCurrentLedgerId = value;
    }
    private string? 선택원장블록노드제목
    {
        get => DiagramCanvas.SelectedNodeTitle;
        set => DiagramCanvas.SelectedNodeTitle = value;
    }
    private string? connectionStartNodeTitle
    {
        get => DiagramCanvas.ConnectionStartNodeTitle;
        set => DiagramCanvas.ConnectionStartNodeTitle = value;
    }
    private string? diagramConnectionMessage
    {
        get => DiagramCanvas.ConnectionMessage;
        set => DiagramCanvas.ConnectionMessage = value;
    }
    private string? selectedDiagramEdgeId
    {
        get => DiagramCanvas.SelectedEdgeId;
        set => DiagramCanvas.SelectedEdgeId = value;
    }
    private 원장블록노드? nodeDetailPanelNode;
    private bool isDiagramEdgeOptionDockCollapsed
    {
        get => DiagramCanvas.IsEdgeOptionDockCollapsed;
        set => DiagramCanvas.IsEdgeOptionDockCollapsed = value;
    }
    private DiagramHandleDrag? activeDiagramHandleDrag
    {
        get => DiagramCanvas.ActiveHandleDrag;
        set => DiagramCanvas.ActiveHandleDrag = value;
    }
    private DiagramDragPoint? diagramDragPointer
    {
        get => DiagramCanvas.DragPointer;
        set => DiagramCanvas.DragPointer = value;
    }
    private DiagramSnapshotDto? sharedLedgerDiagramSnapshot
    {
        get => DiagramWorkspace.SharedLedgerDiagramSnapshot;
        set => DiagramWorkspace.SharedLedgerDiagramSnapshot = value;
    }
    private bool diagramHandleDragMoved
    {
        get => DiagramCanvas.HandleDragMoved;
        set => DiagramCanvas.HandleDragMoved = value;
    }
    private bool suppressNextDiagramHandleClick
    {
        get => DiagramCanvas.SuppressNextHandleClick;
        set => DiagramCanvas.SuppressNextHandleClick = value;
    }
    private string newConnectionLabel
    {
        get => DiagramCanvas.NewConnectionLabel;
        set => DiagramCanvas.NewConnectionLabel = value;
    }
    private string 원함입력
    {
        get => WishFlow.Wish;
        set => WishFlow.Wish = value;
    }
    private string 원함조건입력
    {
        get => WishFlow.Condition;
        set => WishFlow.Condition = value;
    }
    private CommunityLedgerFlowAnalysisResponse? 원함분석결과
    {
        get => WishFlow.Analysis;
        set => WishFlow.Analysis = value;
    }
    private long? editingPostId
    {
        get => Composer.EditingPostId;
        set => Composer.EditingPostId = value;
    }
    private bool isLoading
    {
        get => Shell.IsLoading;
        set => Shell.IsLoading = value;
    }
    private bool isBoardLoading
    {
        get => Boards.IsLoading;
        set => Boards.IsLoading = value;
    }
    private bool isApiEndpointMetadataLoading
    {
        get => DiagramWorkspace.IsApiEndpointMetadataLoading;
        set => DiagramWorkspace.IsApiEndpointMetadataLoading = value;
    }
    private bool isMyLedgersLoading
    {
        get => LedgerPicker.IsLoading;
        set => LedgerPicker.IsLoading = value;
    }
    private bool isLedgerPickerOpen
    {
        get => LedgerPicker.IsPickerOpen;
        set => LedgerPicker.IsPickerOpen = value;
    }
    private bool isLedgerDetailOpen
    {
        get => LedgerPicker.IsDetailOpen;
        set => LedgerPicker.IsDetailOpen = value;
    }
    private bool isOrderLedgerHierarchyOpen
    {
        get => LedgerPicker.IsHierarchyOpen;
        set => LedgerPicker.IsHierarchyOpen = value;
    }
    private bool isLedgerDetailLoading
    {
        get => LedgerPicker.IsDetailLoading;
        set => LedgerPicker.IsDetailLoading = value;
    }
    private bool ledgerDetailOpenedFromHierarchy
    {
        get => LedgerPicker.DetailOpenedFromHierarchy;
        set => LedgerPicker.DetailOpenedFromHierarchy = value;
    }
    private bool isLedgerSharingSaving
    {
        get => LedgerPicker.IsSharingSaving;
        set => LedgerPicker.IsSharingSaving = value;
    }
    private bool isSharedLedgerReusing
    {
        get => LedgerPicker.IsSharedLedgerReusing;
        set => LedgerPicker.IsSharedLedgerReusing = value;
    }
    private bool isSavingBoardRequest
    {
        get => Boards.IsSavingRequest;
        set => Boards.IsSavingRequest = value;
    }
    private bool isWorkMode
    {
        get => Shell.IsWorkMode;
        set => Shell.IsWorkMode = value;
    }
    private bool isCompactHomeSummary
    {
        get => Shell.IsCompactHomeSummary;
        set => Shell.IsCompactHomeSummary = value;
    }
    private bool isComposeOpen
    {
        get => Composer.IsOpen;
        set
        {
            Composer.IsOpen = value;
            if (!value)
            {
                ViewModel.CloseEvidenceChartTool();
            }
        }
    }

    private bool isBaguaNavigatorOpen
    {
        get => Shell.IsBaguaNavigatorOpen;
        set => Shell.IsBaguaNavigatorOpen = value;
    }
    private string? statusMessage
    {
        get => Shell.StatusMessage;
        set => Shell.StatusMessage = value;
    }
    private string? myLedgerLoadMessage
    {
        get => LedgerPicker.LoadMessage;
        set => LedgerPicker.LoadMessage = value;
    }
    private string? ledgerDetailErrorMessage
    {
        get => LedgerPicker.DetailErrorMessage;
        set => LedgerPicker.DetailErrorMessage = value;
    }
    private 커뮤니티원장공개설정Response? ledgerSharingSettings
    {
        get => LedgerPicker.SharingSettings;
        set => LedgerPicker.SharingSettings = value;
    }
    private PlatformCommunityPostLedgerContextResponse? ledgerDetailContext
    {
        get => LedgerPicker.DetailContext;
        set => LedgerPicker.DetailContext = value;
    }
    private PlatformCommunityPostLedgerContextResponse? orderLedgerHierarchyContext
    {
        get => LedgerPicker.HierarchyContext;
        set => LedgerPicker.HierarchyContext = value;
    }
    private string? 원장전송결과메시지
    {
        get => DiagramWorkspace.LedgerSubmissionMessage;
        set => DiagramWorkspace.LedgerSubmissionMessage = value;
    }
    private Severity statusSeverity
    {
        get => Shell.StatusKind switch
        {
            CommunityComposerMessageKind.Success => Severity.Success,
            CommunityComposerMessageKind.Warning => Severity.Warning,
            CommunityComposerMessageKind.Error => Severity.Error,
            _ => Severity.Info
        };
        set => Shell.StatusKind = value switch
        {
            Severity.Success => CommunityComposerMessageKind.Success,
            Severity.Warning => CommunityComposerMessageKind.Warning,
            Severity.Error => CommunityComposerMessageKind.Error,
            _ => CommunityComposerMessageKind.Info
        };
    }
    private Severity diagramConnectionSeverity
    {
        get => DiagramCanvas.ConnectionMessageKind switch
        {
            CommunityComposerMessageKind.Success => Severity.Success,
            CommunityComposerMessageKind.Warning => Severity.Warning,
            CommunityComposerMessageKind.Error => Severity.Error,
            _ => Severity.Info
        };
        set => DiagramCanvas.ConnectionMessageKind = value switch
        {
            Severity.Success => CommunityComposerMessageKind.Success,
            Severity.Warning => CommunityComposerMessageKind.Warning,
            Severity.Error => CommunityComposerMessageKind.Error,
            _ => CommunityComposerMessageKind.Info
        };
    }
    private Severity 원장전송결과Severity
    {
        get => DiagramWorkspace.LedgerSubmissionMessageKind switch
        {
            CommunityComposerMessageKind.Success => Severity.Success,
            CommunityComposerMessageKind.Warning => Severity.Warning,
            CommunityComposerMessageKind.Error => Severity.Error,
            _ => Severity.Info
        };
        set => DiagramWorkspace.LedgerSubmissionMessageKind = value switch
        {
            Severity.Success => CommunityComposerMessageKind.Success,
            Severity.Warning => CommunityComposerMessageKind.Warning,
            Severity.Error => CommunityComposerMessageKind.Error,
            _ => CommunityComposerMessageKind.Info
        };
    }
    private bool isDisposed;
}
