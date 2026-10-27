namespace Ssalddel.Tests.Architecture;

public sealed class OrdererGroupPurchasePageCompositionTests
{
    private static readonly string[] BusinessComponents =
    [
        "<GroupPurchaseProductCatalogPanel",
        "<GroupPurchaseProductEvidencePanel",
        "<GroupPurchaseDemandPanel",
        "<GroupPurchaseProductAnalysisPanel",
        "<GroupPurchaseTradeReadinessEvidencePanel",
        "<GroupPurchaseShipmentTrackingPanel"
    ];

    [Fact]
    public void 공동구매_페이지는_목록_상세_Action_후속조회를_각_route로_분리한다()
    {
        var repositoryRoot = FindRepositoryRoot();
        var pagesRoot = Path.Combine(repositoryRoot, "OrdererApp", "Components", "Pages");

        AssertPage(
            pagesRoot,
            "GroupPurchaseHome.razor",
            "@page \"/group-purchase\"",
            expectedBusinessComponent: null);
        AssertPage(
            pagesRoot,
            "GroupPurchasePractice.razor",
            "@page \"/group-purchase/practice\"",
            expectedBusinessComponent: null);
        AssertPage(
            pagesRoot,
            "GroupPurchaseProducts.razor",
            "@page \"/group-purchase/products\"",
            "<GroupPurchaseProductCatalogPanel");
        AssertPage(
            pagesRoot,
            "GroupPurchaseProductDetail.razor",
            "@page \"/group-purchase/products/{ProductId}\"",
            "<GroupPurchaseProductEvidencePanel");
        AssertPage(
            pagesRoot,
            "GroupPurchaseDemandCreate.razor",
            "@page \"/group-purchase/demands/new/{ProductId}\"",
            "<GroupPurchaseDemandPanel");
        AssertPage(
            pagesRoot,
            "GroupPurchaseImportReview.razor",
            "@page \"/group-purchase/import-review/{ProductId}\"",
            "<GroupPurchaseProductAnalysisPanel");
        AssertPage(
            pagesRoot,
            "GroupPurchaseShipments.razor",
            "@page \"/group-purchase/shipments\"",
            "<GroupPurchaseShipmentTrackingPanel");

        Assert.False(File.Exists(Path.Combine(pagesRoot, "GroupPurchaseIntent.razor")));
    }

    [Fact]
    public void 체험공동구매는_가상참여자와무저장경계를_공용화면에고정한다()
    {
        var repositoryRoot = FindRepositoryRoot();
        var screen = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "Ssalddel.Ui.Common",
            "Areas",
            "App",
            "Components",
            "Orderer",
            "GroupPurchasePracticeScreen.razor"));
        var webPage = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "Ssalddel.WebApp",
            "Pages",
            "CommunityGroupPurchasePracticePage.razor"));
        var server = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "Ssalddel",
            "Services",
            "Orderer",
            "공동구매체험Service.cs"));

        Assert.Contains("실제 사람이 아닌", screen);
        Assert.Contains("가상 이웃", screen);
        Assert.Contains("실제 비구속 수요를 새로 확인하기", screen);
        Assert.DoesNotContain("비구속수요저장Async", screen, StringComparison.Ordinal);
        Assert.Contains("@page \"/community/group-purchase/practice\"", webPage);
        Assert.Contains("서버저장여부 = false", server);
        Assert.Contains("외부효과발생여부 = false", server);
        Assert.Contains("실제 주문·결제·계약", server);
    }

    [Fact]
    public void 공동구매_route와_responsive_frame은_공용계층에_있다()
    {
        var repositoryRoot = FindRepositoryRoot();
        var routePath = Path.Combine(
            repositoryRoot,
            "Ssalddel.Contracts",
            "Common",
            "Orderer",
            "GroupPurchasePageRoutes.cs");
        var framePath = Path.Combine(
            repositoryRoot,
            "Ssalddel.Ui.Common",
            "Areas",
            "App",
            "Components",
            "Orderer",
            "GroupPurchaseScreenFrame.razor");
        var frameCssPath = framePath + ".css";

        Assert.True(File.Exists(routePath));
        Assert.True(File.Exists(framePath));
        Assert.True(File.Exists(frameCssPath));
        Assert.Contains("ProductDetailTemplate", File.ReadAllText(routePath));
        Assert.Contains("DemandCreateTemplate", File.ReadAllText(routePath));
        Assert.Contains("@media (max-width: 600px)", File.ReadAllText(frameCssPath));
        Assert.Contains("min-height: 44px", File.ReadAllText(frameCssPath));
    }

    [Fact]
    public void 비구속_수요_Action은_결제와_보조흐름을_기본_페이지에서_분리한다()
    {
        var repositoryRoot = FindRepositoryRoot();
        var demandPanelPath = Path.Combine(
            repositoryRoot,
            "OrdererApp",
            "Components",
            "GroupPurchase",
            "GroupPurchaseDemandPanel.razor");
        var demandPagePath = Path.Combine(
            repositoryRoot,
            "OrdererApp",
            "Components",
            "Pages",
            "GroupPurchaseDemandCreate.razor");
        var demandPanelPage = File.ReadAllText(demandPanelPath);
        var demandPanel = string.Join("\n", demandPanelPage, File.ReadAllText(Path.Combine(
            repositoryRoot, "OrdererApp", "Components", "GroupPurchase", "GroupPurchaseDemandSession.cs")));
        var demandPage = File.ReadAllText(demandPagePath);

        Assert.Contains("GroupPurchaseDemandSession", demandPanelPage);
        Assert.DoesNotContain("<GroupPurchasePaymentSchedule", demandPanel, StringComparison.Ordinal);
        Assert.Contains("ShowFlowSummary", demandPanel);
        Assert.Contains("WithdrawDemandAsync", demandPanel);
        Assert.Contains("ShipmentTrackingService.ListGroupsAsync", demandPanel);
        Assert.True(
            demandPanelPage.LastIndexOf("WithdrawDemandAsync", StringComparison.Ordinal)
            < demandPanelPage.LastIndexOf("ShipmentTrackingService.ListGroupsAsync", StringComparison.Ordinal));
        Assert.Contains("철회한 뒤 서버 원장을 다시 확인했습니다.", demandPanel);
        Assert.Contains("ShowFlowSummary=\"false\"", demandPage);
        Assert.DoesNotContain("<GroupPurchaseProductAnalysisPanel", demandPage);
        Assert.DoesNotContain("<GroupPurchaseShipmentTrackingPanel", demandPage);
    }

    [Fact]
    public void 재료목록은_카드한번클릭으로_미리보기와_비구속저장을_이어가고_카드에서철회한다()
    {
        var repositoryRoot = FindRepositoryRoot();
        var page = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "OrdererApp",
            "Components",
            "Pages",
            "GroupPurchaseProducts.razor"));
        var catalog = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "OrdererApp",
            "Components",
            "GroupPurchase",
            "GroupPurchaseProductCatalogPanel.razor"));
        var viewModel = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "Ssalddel.Ui.Common",
            "Areas",
            "App",
            "ViewModels",
            "OrdererIngredientCardAutoGroupingViewModel.cs"));
        var pageViewModel = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "OrdererApp",
            "ViewModels",
            "주문자공동구매PageViewModels.cs"));

        Assert.Contains("GroupRequested=\"ViewModel.JoinGroupAsync\"", page);
        Assert.Contains("GroupWithdrawRequested=\"ViewModel.WithdrawGroupAsync\"", page);
        Assert.Contains("AutoGrouping.JoinAsync(product)", pageViewModel);
        Assert.Contains("AutoGrouping.WithdrawAsync(product)", pageViewModel);
        Assert.DoesNotContain("private Task JoinGroupAsync", page);
        Assert.Contains("이 재료로 집단화", catalog);
        Assert.Contains("참여 철회", catalog);
        Assert.Contains("수요배치미리보기Async", viewModel);
        Assert.Contains("비구속수요저장Async", viewModel);
        Assert.Contains("비구속수요철회Async", viewModel);
        Assert.True(
            viewModel.IndexOf("수요배치미리보기Async", StringComparison.Ordinal)
            < viewModel.IndexOf("비구속수요저장Async", StringComparison.Ordinal));
        Assert.DoesNotContain("예약결제금액 =", viewModel, StringComparison.Ordinal);
    }

    [Fact]
    public void 같이수입_검토는_분석컴포넌트_안에서만_공식근거를_조립한다()
    {
        var repositoryRoot = FindRepositoryRoot();
        var analysisPanelPath = Path.Combine(
            repositoryRoot,
            "OrdererApp",
            "Components",
            "GroupPurchase",
            "GroupPurchaseProductAnalysisPanel.razor");
        var importReviewPagePath = Path.Combine(
            repositoryRoot,
            "OrdererApp",
            "Components",
            "Pages",
            "GroupPurchaseImportReview.razor");
        var analysisPanel = File.ReadAllText(analysisPanelPath);
        var importReviewPage = File.ReadAllText(importReviewPagePath);

        Assert.Contains("<GroupPurchaseTradeReadinessEvidencePanel", analysisPanel);
        Assert.Contains("<GroupPurchaseProductAnalysisPanel", importReviewPage);
        Assert.DoesNotContain("<GroupPurchaseTradeReadinessEvidencePanel", importReviewPage);
    }

    [Theory]
    [InlineData("GroupPurchaseShipmentTrackingPanel.razor")]
    [InlineData("GroupPurchaseProductCatalogPanel.razor")]
    [InlineData("GroupPurchaseProductEvidencePanel.razor")]
    [InlineData("GroupPurchaseProductAnalysisPanel.razor")]
    [InlineData("GroupPurchaseTradeReadinessEvidencePanel.razor")]
    [InlineData("HsCandidateList.razor")]
    [InlineData("GroupPurchaseDemandPanel.razor")]
    [InlineData("GroupPurchaseDemandFlowSummary.razor")]
    [InlineData("GroupPurchasePaymentSchedule.razor")]
    public void 공동구매_하위_책임은_별도_컴포넌트로_존재한다(string fileName)
    {
        var repositoryRoot = FindRepositoryRoot();
        var componentPath = Path.Combine(
            repositoryRoot,
            "OrdererApp",
            "Components",
            "GroupPurchase",
            fileName);

        Assert.True(File.Exists(componentPath), $"공동구매 하위 컴포넌트가 없습니다: {fileName}");
        Assert.NotEmpty(File.ReadAllText(componentPath));
    }

    private static void AssertPage(
        string pagesRoot,
        string fileName,
        string route,
        string? expectedBusinessComponent)
    {
        var pagePath = Path.Combine(pagesRoot, fileName);
        var source = File.ReadAllText(pagePath);

        Assert.True(File.ReadLines(pagePath).Count() <= 100, $"{fileName}이 route 조립 책임을 넘어섰습니다.");
        Assert.Contains(route, source);
        Assert.Contains("<GroupPurchaseScreenFrame", source);
        Assert.DoesNotContain("IGroupPurchaseShipmentTrackingService", source);
        Assert.DoesNotContain("RegisterDemandAsync", source);
        Assert.DoesNotContain("ResolveDeliveryScopesAsync", source);
        Assert.DoesNotContain("SimulateImportUnitPriceAsync", source);

        foreach (var component in BusinessComponents)
        {
            if (string.Equals(component, expectedBusinessComponent, StringComparison.Ordinal))
            {
                Assert.Contains(component, source);
            }
            else
            {
                Assert.DoesNotContain(component, source);
            }
        }
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Ssalddel.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Ssalddel 저장소 루트를 찾지 못했습니다.");
    }
}
