using Hongdal.Ui.Common.Areas.BackOffice.ViewModels;

namespace Hongdal.Tests.Ui.BackOffice;

public sealed class AdminPageCatalogPageViewModelTests
{
    [Fact]
    public async Task 초기화는_카탈로그와_운영요약을_함께구성한다()
    {
        var client = new RecordingClient(
        [
            Page("admin-home", "admin", "Hongdal Admin", "admin", "관리", "관리 홈",
                AdminPageExecutionMode.ReadOnly, AdminPageReviewState.Verified,
                AdminPageNavigationState.Primary, desktopVerified: true, mobileVerified: true),
            Page("community-home", "web", "Hongdal Web", "community", "커뮤니티", "커뮤니티 홈",
                AdminPageExecutionMode.Simulation, AdminPageReviewState.NeedsReview,
                AdminPageNavigationState.Contextual, desktopVerified: true, mobileVerified: false)
        ]);
        using var viewModel = CreateViewModel(client);

        Assert.True(await viewModel.초기화Async());

        Assert.Equal(2, viewModel.List.Items.Count);
        Assert.Equal(2, viewModel.List.Summary.TotalCount);
        Assert.Equal(1, viewModel.List.Summary.PrimaryNavigationCount);
        Assert.Equal(1, viewModel.List.Summary.SimulationCount);
        Assert.Equal(1, viewModel.List.Summary.NeedsAttentionCount);
        Assert.Equal(1, viewModel.List.Summary.FullyVerifiedCount);
        Assert.NotNull(viewModel.Detail.SelectedPage);
        Assert.Equal(2, viewModel.AppOptions.Count);
    }

    [Fact]
    public async Task 앱_검색어_검토필요_조건을_즉시조합한다()
    {
        var client = new RecordingClient(
        [
            Page("community-home", "web", "Hongdal Web", "community", "커뮤니티", "커뮤니티 홈"),
            Page("warehouse-inbound", "warehouse", "창고 Manager", "inbound", "입고", "입고 예정",
                AdminPageExecutionMode.Simulation, AdminPageReviewState.NeedsReview,
                AdminPageNavigationState.Contextual, desktopVerified: true, mobileVerified: false),
            Page("warehouse-picking", "warehouse", "창고 Manager", "outbound", "출고", "마트 피킹")
        ]);
        using var viewModel = CreateViewModel(client);
        await viewModel.초기화Async();

        viewModel.AppFilter = "warehouse";
        viewModel.SearchText = "입고";
        viewModel.NeedsAttentionOnly = true;

        Assert.Equal("warehouse-inbound", Assert.Single(viewModel.List.Items).PageKey);
        Assert.Equal("warehouse-inbound", viewModel.Detail.SelectedPage?.PageKey);

        viewModel.ResetFilters();
        Assert.Equal(3, viewModel.List.Items.Count);
    }

    [Fact]
    public async Task 비관리자는_관리메타데이터를_저장할수없다()
    {
        var client = new RecordingClient([Page("community-home", "web", "Hongdal Web", "community", "커뮤니티", "커뮤니티 홈")]);
        using var viewModel = CreateViewModel(client);
        await viewModel.초기화Async();
        viewModel.Detail.AdminNote = "검토 기록";

        var saved = await viewModel.SaveSelectedAsync(canManage: false, reviewer: "방문자");

        Assert.False(saved);
        Assert.Equal(0, client.UpdateCount);
        Assert.Equal(AdminPageCatalogMessageKind.Warning, viewModel.MessageKind);
    }

    [Fact]
    public async Task 관리자는_검토_노출_화면검증_메타데이터를_갱신한다()
    {
        var client = new RecordingClient(
        [
            Page("community-home", "web", "Hongdal Web", "community", "커뮤니티", "커뮤니티 홈",
                AdminPageExecutionMode.ReadOnly, AdminPageReviewState.NeedsReview,
                AdminPageNavigationState.Contextual, desktopVerified: true, mobileVerified: false)
        ]);
        using var viewModel = CreateViewModel(client);
        await viewModel.초기화Async();
        viewModel.Detail.ReviewState = AdminPageReviewState.Verified;
        viewModel.Detail.NavigationState = AdminPageNavigationState.Primary;
        viewModel.Detail.MobileVerified = true;
        viewModel.Detail.AdminNote = "데스크톱과 모바일 핵심 흐름 확인";

        var saved = await viewModel.SaveSelectedAsync(canManage: true, reviewer: "운영자");

        Assert.True(saved);
        Assert.Equal(1, client.UpdateCount);
        Assert.Equal(AdminPageReviewState.Verified, client.LastRequest?.ReviewState);
        Assert.Equal("운영자", viewModel.Detail.SelectedPage?.LastReviewer);
        Assert.Equal(0, viewModel.List.Summary.NeedsAttentionCount);
        Assert.Equal(1, viewModel.List.Summary.FullyVerifiedCount);
        Assert.Equal(AdminPageCatalogMessageKind.Success, viewModel.MessageKind);
    }

    private static AdminPageCatalogPageViewModel CreateViewModel(IAdminPageCatalogClient client)
        => new(client, new AdminPageCatalogListViewModel(), new AdminPageCatalogDetailViewModel());

    private static AdminManagedPageSnapshot Page(
        string pageKey,
        string appKey,
        string appName,
        string areaKey,
        string areaName,
        string title,
        AdminPageExecutionMode executionMode = AdminPageExecutionMode.ReadOnly,
        AdminPageReviewState reviewState = AdminPageReviewState.Verified,
        AdminPageNavigationState navigationState = AdminPageNavigationState.Contextual,
        bool desktopVerified = true,
        bool mobileVerified = true)
        => new(
            pageKey,
            appKey,
            appName,
            areaKey,
            areaName,
            title,
            $"/{pageKey}",
            $"/{pageKey}",
            $"Pages/{title}.razor",
            $"{title} 운영 목적",
            "운영자",
            ["사용자"],
            AdminPageLifecycle.Active,
            executionMode,
            reviewState,
            navigationState,
            RouteDeclared: true,
            DesktopVerified: desktopVerified,
            MobileVerified: mobileVerified,
            RequiresAuthentication: false,
            HasExternalEffects: false,
            LastReviewedAt: null,
            LastReviewer: null,
            AdminNote: string.Empty);

    private sealed class RecordingClient(IReadOnlyList<AdminManagedPageSnapshot> pages)
        : IAdminPageCatalogClient
    {
        private readonly List<AdminManagedPageSnapshot> _pages = [.. pages];

        public int UpdateCount { get; private set; }
        public AdminPageCatalogUpdateRequest? LastRequest { get; private set; }

        public Task<IReadOnlyList<AdminManagedPageSnapshot>> GetPagesAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<AdminManagedPageSnapshot>>([.. _pages]);

        public Task<AdminManagedPageSnapshot> UpdatePageAsync(
            AdminPageCatalogUpdateRequest request,
            CancellationToken cancellationToken = default)
        {
            UpdateCount++;
            LastRequest = request;
            var index = _pages.FindIndex(item => item.PageKey == request.PageKey);
            var updated = _pages[index] with
            {
                ReviewState = request.ReviewState,
                NavigationState = request.NavigationState,
                DesktopVerified = request.DesktopVerified,
                MobileVerified = request.MobileVerified,
                AdminNote = request.AdminNote,
                LastReviewer = request.Reviewer,
                LastReviewedAt = new DateTimeOffset(2026, 7, 18, 6, 0, 0, TimeSpan.Zero)
            };
            _pages[index] = updated;
            return Task.FromResult(updated);
        }
    }
}
