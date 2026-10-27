namespace Ssalddel.Tests.Ui.Common;

public sealed class ShipperMobileFigma03PresentationTests
{
    [Theory]
    [InlineData("Home.razor", "03.01")]
    [InlineData("ShipperRequestWizard.razor", "03.02")]
    [InlineData("ShipperRequestCargoPage.razor", "03.03")]
    [InlineData("ShipperRequestTransportPage.razor", "03.04")]
    [InlineData("ShipperRequestProcedurePage.razor", "03.05")]
    [InlineData("ShipperRequestReviewPage.razor", "03.06")]
    [InlineData("ShipperRequestDetail.razor", "03.07")]
    [InlineData("ShipperRequestPaymentPage.razor", "03.08")]
    [InlineData("ShipperRequestTimelinePage.razor", "03.09")]
    [InlineData("ShipperRequestProofsPage.razor", "03.10")]
    [InlineData("ShipperBulkImport.razor", "03.11")]
    [InlineData("InboundDashboard.razor", "03.12")]
    [InlineData("CustomsHsReviews.razor", "03.13")]
    [InlineData("FclLclPlanner.razor", "03.14")]
    [InlineData("SalesChannels.razor", "03.15")]
    [InlineData("ProductListings.razor", "03.16")]
    [InlineData("OrderFulfillment.razor", "03.17")]
    [InlineData("WarehouseWorkspace.razor", "03.18")]
    public void Figma03의_열여덟화면은_기존MauiRoute에책임코드를고정한다(
        string fileName,
        string screenCode)
    {
        var source = ReadAppPage(fileName);

        Assert.Contains(screenCode, source);
        Assert.Contains("@layout ShipperMobileLayout", source);
    }

    [Fact]
    public void 화주MauiShell은_FigmaAppBar와네개하단Navigation을제공한다()
    {
        var source = Read(
            "SsalddelApp",
            "Components",
            "Layout",
            "ShipperMobileLayout.razor");

        Assert.Contains("shipper-mobile-shell__appbar", source);
        Assert.Contains("shipper-mobile-shell__bottom-nav", source);
        Assert.Contains("화주 · 판매자", source);
        Assert.Contains(">홈</span>", source);
        Assert.Contains(">의뢰</span>", source);
        Assert.Contains(">입고</span>", source);
        Assert.Contains(">판매</span>", source);
    }

    [Fact]
    public void 화주홈은_실제공용Dashboard를Compact표현으로재사용한다()
    {
        var source = Read(
            "SsalddelApp",
            "Components",
            "Shared",
            "ShipperHomeAppShell.razor");

        Assert.Contains("<ShipperHomeScreen", source);
        Assert.Contains("CompactPresentation=\"true\"", source);
        Assert.Contains("ScreenCode=\"@ScreenCode\"", source);
        Assert.Contains("ShipperHomeAppLoginPanel", source);
        Assert.DoesNotContain("ActiveRequestCount =", source);
    }

    [Fact]
    public void 운송의뢰시작은_단건과일괄등록을기존Route로분리한다()
    {
        var source = ReadAppPage("ShipperRequestWizard.razor");

        Assert.Contains("ShipperRequestNavigationContext.Parse", source);
        Assert.Contains("ShipperRequestAuthoringStep.Cargo", source);
        Assert.Contains("ShipperRoutes.RequestBulk", source);
        Assert.Contains("replace: true", source);
        Assert.Contains("배차·계약·결제는 실행되지 않으며", source);
    }

    [Theory]
    [InlineData("ShipperRequestScreenFrame.razor", "STEP {(int)Step + 1} · TRANSPORT REQUEST")]
    [InlineData("ShipperRequestDetailScreenFrame.razor", "<h1>@Title</h1>")]
    public void 공용운송Screen은_화면코드계약과사용자제목을유지한다(
        string fileName,
        string defaultEyebrow)
    {
        var source = Read(
            "Ssalddel.Ui.Common",
            "Areas",
            "App",
            "Components",
            "Transport",
            fileName);

        Assert.Contains("string? ScreenCode", source);
        Assert.Contains(defaultEyebrow, source);
    }

    private static string ReadAppPage(string fileName)
        => Read("SsalddelApp", "Components", "Pages", fileName);

    private static string Read(params string[] segments)
        => File.ReadAllText(Path.Combine(new[] { FindRepositoryRoot() }.Concat(segments).ToArray()));

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
