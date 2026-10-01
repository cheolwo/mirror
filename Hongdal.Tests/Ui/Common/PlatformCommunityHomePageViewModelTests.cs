using System.Net;
using System.Net.Http.Json;
using Hongdal.Contracts.Common.Content;
using Hongdal.Contracts.Common.Community;
using Hongdal.Ui.Common.Areas.App.Services;
using Hongdal.Ui.Common.Areas.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Hongdal.Tests.Ui.Common;

public sealed class PlatformCommunityHomePageViewModelTests
{
    [Fact]
    public void 홈PageViewModel은_기능별하위ViewModel을조립한다()
    {
        var service = CreateService();
        var composer = new CommunityPostComposerViewModel(service, new EmptyDraftStore());
        var postList = new CommunityPostListPageViewModel(service);
        var shell = new PlatformCommunityHomeShellViewModel();
        var boards = new PlatformCommunityBoardWorkspaceViewModel(service);
        var engagement = new PlatformCommunityPostEngagementViewModel(service);
        var ledgerPicker = new PlatformCommunityLedgerPickerViewModel(service);
        var foodDiscovery = new YouTubeFoodCommunityDiscoveryViewModel(
            new YouTubeFoodCommunityDiscoveryService(new HttpClient(), null!));
        var diagramWorkspace = new PlatformCommunityDiagramWorkspaceViewModel();
        var wishFlow = new PlatformCommunityWishFlowViewModel();
        var evidenceChart = new CommunityAuthoringEvidenceChartViewModel();
        using var services = new ServiceCollection().BuildServiceProvider();
        var warehouseProxy = new PlatformCommunityWarehouseProxyViewModel(services);
        using var page = new PlatformCommunityHomePageViewModel(
            composer,
            postList,
            shell,
            boards,
            engagement,
            ledgerPicker,
            foodDiscovery,
            diagramWorkspace,
            wishFlow,
            evidenceChart,
            warehouseProxy);

        Assert.Same(composer, page.Composer);
        Assert.Same(postList, page.PostList);
        Assert.Same(shell, page.Shell);
        Assert.Same(boards, page.Boards);
        Assert.Same(engagement, page.Engagement);
        Assert.Same(ledgerPicker, page.LedgerPicker);
        Assert.Same(foodDiscovery, page.FoodDiscovery);
        Assert.Same(diagramWorkspace, page.DiagramWorkspace);
        Assert.Same(wishFlow, page.WishFlow);
        Assert.Same(evidenceChart, page.EvidenceChart);
        Assert.Same(warehouseProxy, page.WarehouseProxy);
        Assert.Same(engagement.Journeys, page.ActionJourneys);
    }

    [Fact]
    public void 홈PageViewModel은_글쓰기근거그래프를_초안에적용하고다시불러온다()
    {
        var service = CreateService();
        using var services = new ServiceCollection().BuildServiceProvider();
        using var page = new PlatformCommunityHomePageViewModel(
            new CommunityPostComposerViewModel(service, new EmptyDraftStore()),
            new CommunityPostListPageViewModel(service),
            new PlatformCommunityHomeShellViewModel(),
            new PlatformCommunityBoardWorkspaceViewModel(service),
            new PlatformCommunityPostEngagementViewModel(service),
            new PlatformCommunityLedgerPickerViewModel(service),
            new YouTubeFoodCommunityDiscoveryViewModel(
                new YouTubeFoodCommunityDiscoveryService(new HttpClient(), null!)),
            new PlatformCommunityDiagramWorkspaceViewModel(),
            new PlatformCommunityWishFlowViewModel(),
            new CommunityAuthoringEvidenceChartViewModel(),
            new PlatformCommunityWarehouseProxyViewModel(services));

        page.Composer.Draft.Title = "함께 주문하면 배송비를 줄일 수 있을까";
        page.OpenEvidenceChartTool();
        var evidence = page.EvidenceChart;
        evidence.Title = "주문 수량별 1인 배송비";
        evidence.Claim = "주문 수량이 늘면 1인당 배송비가 낮아질 가능성이 있습니다.";
        evidence.SeriesLabel = "1인당 배송비";
        evidence.Unit = "USD/person";
        evidence.SourceLabel = "작성자 시뮬레이션";
        evidence.ReferenceDate = "2026-07-19";
        evidence.Interpretation = "20명이 함께 주문하는 가정에서 1인당 배송비가 낮게 계산됩니다.";
        evidence.Limitation = "관세, 지역별 최종 배송비와 실제 견적은 포함하지 않은 비교입니다.";
        evidence.Points[0].Label = "개별 주문";
        evidence.Points[0].Value = 18m;
        evidence.Points[1].Label = "20명 공동 주문";
        evidence.Points[1].Value = 7m;

        Assert.True(evidence.Evaluate());
        Assert.True(page.ApplyEvidenceChartToDraft());
        Assert.False(page.IsEvidenceChartToolOpen);
        Assert.Single(CommunityEvidenceChartTextCodec.DecodeAll(page.Composer.Draft.Body));

        page.OpenEvidenceChartTool();

        Assert.True(page.IsEvidenceChartToolOpen);
        Assert.Equal("주문 수량별 1인 배송비", page.EvidenceChart.Title);
        Assert.Equal(7m, page.EvidenceChart.Points[1].Value);

        page.EvidenceChart.Points[1].Value = 6m;
        Assert.True(page.EvidenceChart.Evaluate());
        Assert.True(page.ApplyEvidenceChartToDraft());
        var updated = Assert.Single(
            CommunityEvidenceChartTextCodec.DecodeAll(page.Composer.Draft.Body));
        Assert.Equal(6m, updated.Points[1].Value);
    }

    [Fact]
    public void 다이어그램Workspace는_운송흐름을_커뮤니티초안으로전환한다()
    {
        var viewModel = new PlatformCommunityDiagramWorkspaceViewModel();

        var draft = viewModel.CreateCommunityDraft(
        [
            new("상차 요청", "운송", "출발지에서 화물을 싣습니다.", "delivery"),
            new("운송", "운송", "도착지까지 이동합니다.", "delivery"),
            new("하차 확인", "운송", "수령 증빙을 확인합니다.", "delivery")
        ],
        [
            new("상차 요청", "운송", "배차 후 출발"),
            new("운송", "하차 확인", "도착 후 인계")
        ],
        ["일반", "운송 실무", "생활 원장", "신고/분쟁"],
        "홍달",
        "화주");

        Assert.Equal(CommunityLedgerTemplateKeys.CargoTransport, draft.LedgerTemplateKey);
        Assert.Equal("운송 실무", draft.Category);
        Assert.Contains("상차 요청 -> 운송", draft.Body);
        Assert.Contains("기본 원장 초안", draft.Body);
        Assert.False(draft.IsReportBoardPost);
        Assert.Equal(draft.LedgerTemplateKey, viewModel.SelectedLedgerTemplateKey);
    }

    [Fact]
    public void 다이어그램Workspace는_분쟁신호를_신고초안으로보호한다()
    {
        var viewModel = new PlatformCommunityDiagramWorkspaceViewModel();

        var draft = viewModel.CreateCommunityDraft(
        [
            new("분쟁 신고", "검토", "거래 분쟁을 확인합니다.", "review")
        ],
        [],
        ["일반", "생활 원장", "신고/분쟁"],
        "홍달",
        "참여자");

        Assert.Equal("신고/분쟁", draft.Category);
        Assert.True(draft.IsReportBoardPost);
        Assert.Empty(draft.WorkflowTag);
        Assert.Empty(draft.RoleTag);
    }

    [Fact]
    public void 다이어그램Workspace는_국제업무초안의_맥락을구성한다()
    {
        var viewModel = new PlatformCommunityDiagramWorkspaceViewModel();

        var draft = viewModel.CreateWorkDraft(
            WorkCommunityDraftKind.InternationalCoordination,
            "수입 업무 앱",
            "관세사",
            "통관 담당");

        Assert.Equal("국제 소통", draft.Category);
        Assert.Equal("통관·무역 데이터", draft.WorkflowTag);
        Assert.Equal("통관 담당", draft.RoleTag);
        Assert.Contains("관련 국가/지역", draft.Body);
        Assert.Contains("관세사", draft.Title);
    }

    [Fact]
    public void 게시글상호작용ViewModel은_게시글별입력과펼침상태를분리한다()
    {
        var viewModel = new PlatformCommunityPostEngagementViewModel(CreateService());

        var first = viewModel.GetCommentForm(10);
        var same = viewModel.GetCommentForm(10);
        var other = viewModel.GetCommentForm(20);
        viewModel.ToggleComments(10);

        Assert.Same(first, same);
        Assert.NotSame(first, other);
        Assert.True(viewModel.IsCommentsExpanded(10));
        Assert.False(viewModel.IsCommentsExpanded(20));
    }

    [Fact]
    public void 원장선택ViewModel은_범위와검색어를함께적용한다()
    {
        var viewModel = new PlatformCommunityLedgerPickerViewModel(CreateService());
        viewModel.ReplaceItems(
        [
            new PlatformCommunityPostLedgerChoiceResponse
            {
                원장Id = "mine-1",
                제목 = "공동수입 준비",
                원장템플릿명 = "공동수입",
                내접근원장여부 = true,
                수정시각Utc = new DateTime(2026, 7, 17, 1, 0, 0, DateTimeKind.Utc)
            },
            new PlatformCommunityPostLedgerChoiceResponse
            {
                원장Id = "shared-1",
                제목 = "동네 공동구매",
                원장템플릿명 = "공동구매",
                내접근원장여부 = false,
                수정시각Utc = new DateTime(2026, 7, 17, 2, 0, 0, DateTimeKind.Utc)
            }
        ]);

        viewModel.Scope = "공개 원장";
        viewModel.SearchText = "공동구매";

        var item = Assert.Single(viewModel.FilteredItems);
        Assert.Equal("shared-1", item.원장Id);
    }

    [Fact]
    public void 게시판신청Draft는_필수값과제출후초기화를관리한다()
    {
        var form = new PlatformCommunityBoardForm
        {
            Title = "해외 생활",
            RequestedBy = "작성자",
            RequestReason = "국가별 생활 정보를 나누기 위해",
            Description = "설명"
        };

        Assert.True(form.IsValid);

        form.ResetAfterSubmit();

        Assert.False(form.IsValid);
        Assert.Equal("작성자", form.RequestedBy);
        Assert.Empty(form.Title);
        Assert.Empty(form.Description);
        Assert.Empty(form.RequestReason);
    }

    [Fact]
    public async Task 음식영상발견ViewModel은_국가와후보유형을함께필터한다()
    {
        IReadOnlyList<YouTube음식커뮤니티공유후보Dto> candidates =
        [
            CreateFoodCandidate(1, "고추장", "KR", YouTube상품후보유형코드.식재료),
            CreateFoodCandidate(2, "메이플 쿠키", "CA", YouTube상품후보유형코드.포장상품),
            CreateFoodCandidate(3, "집된장", "KR", YouTube상품후보유형코드.포장상품)
        ];
        var httpClient = new HttpClient(
            new JsonResponseHandler<IReadOnlyList<YouTube음식커뮤니티공유후보Dto>>(candidates))
        {
            BaseAddress = new Uri("https://hongdal.test/")
        };
        var viewModel = new YouTubeFoodCommunityDiscoveryViewModel(
            new YouTubeFoodCommunityDiscoveryService(httpClient, new EmptyAccessTokenProvider()));

        await viewModel.LoadAsync(forceRefresh: false);
        viewModel.SelectCountry("KR");
        viewModel.SelectCandidateType(YouTube상품후보유형코드.포장상품);

        var item = Assert.Single(viewModel.VisibleItems);
        Assert.Equal("집된장", item.상품명);
        Assert.Null(viewModel.ErrorMessage);
    }

    private static PlatformCommunityService CreateService()
        => new(new HttpClient(), null!);

    private static YouTube음식커뮤니티공유후보Dto CreateFoodCandidate(
        long id,
        string productName,
        string countryCode,
        string candidateType)
        => new(
            id,
            productName,
            null,
            countryCode,
            candidateType,
            null,
            "영상 내 소개",
            $"video-{id}",
            $"{productName} 만드는 법",
            new DateTime(2026, 7, 17, 0, 0, 0, DateTimeKind.Utc).AddMinutes(id),
            null,
            $"https://www.youtube.com/watch?v=video-{id}",
            $"channel-{id}",
            $"채널 {id}",
            countryCode);

    private sealed class JsonResponseHandler<T>(T responseValue) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(responseValue),
                RequestMessage = request
            });
    }

    private sealed class EmptyAccessTokenProvider : IHongdalAccessTokenProvider
    {
        public string? AccessToken => null;
    }

    private sealed class EmptyDraftStore : ICommunityPostComposerDraftStore
    {
        public Task<CommunityPostComposerSnapshot?> LoadAsync(
            string appKey,
            CancellationToken cancellationToken = default)
            => Task.FromResult<CommunityPostComposerSnapshot?>(null);

        public Task SaveAsync(
            string appKey,
            CommunityPostComposerSnapshot snapshot,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task ClearAsync(
            string appKey,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
