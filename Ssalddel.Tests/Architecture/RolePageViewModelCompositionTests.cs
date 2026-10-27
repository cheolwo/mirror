namespace Ssalddel.Tests.Architecture;

public sealed class RolePageViewModelCompositionTests
{
    [Theory]
    [InlineData("OrdererApp/ViewModels/주문자PageViewModelBase.cs", "PageViewModelBase")]
    [InlineData("SsalddelApp/ViewModels/Shipper/화주PageViewModelBase.cs", "PageViewModelBase")]
    [InlineData("DriverApp/ViewModels/Driver/기사PageViewModelBase.cs", "PageViewModelBase")]
    public void 역할별_PageViewModelBase는_공통페이지수명주기를사용한다(
        string relativePath,
        string commonBaseName)
    {
        var source = Read(relativePath);

        Assert.Contains($": {commonBaseName}", source);
    }

    [Fact]
    public void 창고는_기존페이지Base와_공통조립수명을유지한다()
    {
        var source = Read(
            "WarehouseManagerApp/ViewModels/Warehouse/창고PageViewModelBase.cs");

        Assert.Contains(": 조립ViewModelBase", source);
        Assert.Contains("하위ViewModel등록", source);
    }

    [Theory]
    [InlineData(
        "OrdererApp/Components/Pages/GroupPurchaseProducts.razor",
        "주문자재료후보PageViewModel")]
    [InlineData(
        "OrdererApp/Components/Pages/GroupPurchaseWishCreate.razor",
        "주문자의향등록PageViewModel")]
    [InlineData(
        "SsalddelApp/Components/Pages/ProductListings.razor",
        "ProductListingsPageViewModel")]
    [InlineData(
        "SsalddelApp/Components/Pages/OrderFulfillment.razor",
        "OrderFulfillmentPageViewModel")]
    [InlineData(
        "DriverApp/Components/Pages/Driver/Home/기사홈Page.razor",
        "기사홈PageViewModel")]
    [InlineData(
        "DriverApp/Components/Pages/Driver/03_Progress/상차Page.razor",
        "기사상차PageViewModel")]
    [InlineData(
        "DriverApp/Components/Pages/Driver/03_Progress/하차Page.razor",
        "기사하차PageViewModel")]
    [InlineData(
        "DriverApp/Components/Pages/Driver/01_Work/운행시작Page.razor",
        "기사운행시작PageViewModel")]
    [InlineData(
        "DriverApp/Components/Pages/Driver/04_Reservation/예약Page.razor",
        "기사예약PageViewModel")]
    [InlineData(
        "DriverApp/Components/Pages/Driver/05_Settlement/월정산Page.razor",
        "기사월정산PageViewModel")]
    [InlineData(
        "DriverApp/Components/Pages/Driver/05_Settlement/계좌정보Page.razor",
        null)]
    [InlineData(
        "DriverApp/Components/Pages/Driver/05_Settlement/이용료안내Page.razor",
        "기사이용료안내PageViewModel")]
    [InlineData(
        "DriverApp/Components/Pages/Driver/06_Notification/알림함Page.razor",
        "기사알림함PageViewModel")]
    [InlineData(
        "DriverApp/Components/Pages/Driver/06_Notification/푸시설정Page.razor",
        null)]
    [InlineData(
        "DriverApp/Components/Pages/Driver/02_Recommendation/탐색캠페인Page.razor",
        "기사탐색캠페인PageViewModel")]
    [InlineData(
        "WarehouseManagerApp/Components/Pages/WorkBoard.razor",
        "창고작업보드PageViewModel")]
    [InlineData(
        "WarehouseManagerApp/Components/Pages/ExpectedInbounds.razor",
        "창고입고예정조회PageViewModel")]
    public void 대표_역할화면은_업무조회가필요할때만_DI_PageViewModel을조립한다(
        string relativePath,
        string? viewModelName)
    {
        var source = Read(relativePath);

        // 계좌·휴대폰 알림은 미연결 상태를 안내하며 무관한 정산/알림 조회를 시작하지 않는다.
        if (viewModelName is null)
        {
            Assert.DoesNotContain("MvvmComponentBase<", source);
            Assert.DoesNotContain("@inject", source);
            return;
        }

        Assert.Contains($"MvvmComponentBase<{viewModelName}>", source);
        Assert.DoesNotContain($"new {viewModelName}", source);
    }

    [Fact]
    public void 역할별_대표PageViewModel은_역할Base를사용한다()
    {
        Assert.Contains(
            ": 주문자PageViewModelBase",
            Read("OrdererApp/ViewModels/주문자공동구매PageViewModels.cs"));
        Assert.Contains(
            ": 화주PageViewModelBase",
            Read("SsalddelApp/ViewModels/Shipper/ProductListingsPageViewModels.cs"));
        Assert.Contains(
            ": 화주PageViewModelBase",
            Read("SsalddelApp/ViewModels/Shipper/OrderFulfillmentPageViewModels.cs"));
        Assert.Contains(
            ": 기사PageViewModelBase",
            Read("DriverApp/ViewModels/Driver/Home/기사홈PageViewModel.cs"));
        Assert.Contains(
            ": 기사PageViewModelBase",
            Read("DriverApp/ViewModels/Driver/Transport/기사상차PageViewModel.cs"));
        Assert.Contains(
            ": 기사PageViewModelBase",
            Read("DriverApp/ViewModels/Driver/Transport/기사하차PageViewModel.cs"));
        Assert.Contains(
            ": 기사PageViewModelBase",
            Read("DriverApp/ViewModels/Driver/Work/기사운행시작PageViewModel.cs"));
        Assert.Contains(
            ": 기사PageViewModelBase",
            Read("DriverApp/ViewModels/Driver/Reservation/기사예약PageViewModel.cs"));
        Assert.Contains(
            ": 기사정산PageViewModelBase",
            Read("DriverApp/ViewModels/Driver/Settlement/기사정산PageViewModels.cs"));
        Assert.Contains(
            ": 기사알림PageViewModelBase",
            Read("DriverApp/ViewModels/Driver/Notification/기사알림PageViewModels.cs"));
    }

    [Theory]
    [InlineData("DriverApp/Components/Pages/Driver/01_Work/운행시작Page.razor")]
    [InlineData("DriverApp/Components/Pages/Driver/04_Reservation/예약Page.razor")]
    [InlineData("DriverApp/Components/Pages/Driver/05_Settlement/월정산Page.razor")]
    [InlineData("DriverApp/Components/Pages/Driver/05_Settlement/계좌정보Page.razor")]
    [InlineData("DriverApp/Components/Pages/Driver/05_Settlement/이용료안내Page.razor")]
    [InlineData("DriverApp/Components/Pages/Driver/06_Notification/알림함Page.razor")]
    [InlineData("DriverApp/Components/Pages/Driver/06_Notification/푸시설정Page.razor")]
    public void 정렬된_기사Route는_Api와_SampleService를_직접주입하지않는다(string relativePath)
    {
        var source = Read(relativePath);

        Assert.DoesNotContain("@inject IDriver", source);
        Assert.DoesNotContain("@inject 기사", source);
    }

    private static string Read(string relativePath)
        => File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            relativePath.Replace('/', Path.DirectorySeparatorChar)));

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
