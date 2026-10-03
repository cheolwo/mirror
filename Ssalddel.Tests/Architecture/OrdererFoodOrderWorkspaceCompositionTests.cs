namespace Ssalddel.Tests.Architecture;

public sealed class OrdererFoodOrderWorkspaceCompositionTests
{
    [Fact]
    public void 음식주문_루트는_접근상태와_업무영역만_조립한다()
    {
        var componentDirectory = FindComponentDirectory();
        var pagePath = Path.Combine(componentDirectory, "OrdererFoodOrderWorkspace.razor");
        var source = File.ReadAllText(pagePath);

        Assert.True(File.ReadLines(pagePath).Count() <= 50);
        Assert.Contains("<OrdererFoodOrderAccessState", source);
        Assert.Contains("<OrdererFoodOrderLoginPanel", source);
        Assert.Contains("<OrdererFoodOrderHeader", source);
        Assert.Contains("<OrdererFoodOrderSearchPanel", source);
        Assert.Contains("<OrdererFoodOrderListPanel", source);
        Assert.Contains("<OrdererFoodOrderDetailPanel", source);
        Assert.DoesNotContain("<MudAlert", source);
        Assert.DoesNotContain("<MudTextField", source);
        Assert.DoesNotContain("<MudPagination", source);
        Assert.DoesNotContain("<Ssalddel공통로그인Panel", source);
        Assert.DoesNotContain("@foreach", source);
    }

    [Theory]
    [InlineData("OrdererFoodOrderAccessState.razor")]
    [InlineData("OrdererFoodOrderAccessState.razor.css")]
    [InlineData("OrdererFoodOrderHeader.razor")]
    [InlineData("OrdererFoodOrderHeader.razor.css")]
    [InlineData("OrdererFoodOrderLoginPanel.razor")]
    [InlineData("OrdererFoodOrderLoginPanel.razor.css")]
    [InlineData("OrdererFoodOrderSearchPanel.razor")]
    [InlineData("OrdererFoodOrderSearchPanel.razor.css")]
    [InlineData("OrdererFoodOrderListPanel.razor")]
    [InlineData("OrdererFoodOrderListPanel.razor.css")]
    [InlineData("OrdererFoodOrderDetailPanel.razor")]
    [InlineData("OrdererFoodOrderDetailPanel.razor.css")]
    [InlineData("OrdererFoodOrderPresentation.cs")]
    public void 음식주문_상태와화면과표현책임은_전용파일로_존재한다(string fileName)
    {
        var componentPath = Path.Combine(FindComponentDirectory(), fileName);

        Assert.True(File.Exists(componentPath), $"음식 주문 전용 파일이 없습니다: {fileName}");
        Assert.NotEmpty(File.ReadAllText(componentPath));
    }

    [Fact]
    public void 음식주문_루트는_목록callback을_취소와직렬화가있는Controller로연결한다()
    {
        var source = File.ReadAllText(Path.Combine(FindComponentDirectory(), "OrdererFoodOrderWorkspace.razor"));
        var code = File.ReadAllText(Path.Combine(FindComponentDirectory(), "OrdererFoodOrderWorkspace.razor.cs"));

        Assert.Contains("SearchRequested=\"SearchListAsync\"", source);
        Assert.Contains("ReloadRequested=\"ReloadListAsync\"", source);
        Assert.Contains("ResetRequested=\"ResetListAsync\"", source);
        Assert.Contains("PageChanged=\"ChangeListPageAsync\"", source);
        Assert.Contains("RefreshController.목록작업실행Async(token => ViewModel.목록검색Async(token))", code);
        Assert.Contains("RefreshController.목록작업실행Async(token => ViewModel.목록새로고침Async(token))", code);
        Assert.Contains("RefreshController.목록작업실행Async(token => ViewModel.검색조건초기화Async(token))", code);
        Assert.Contains("RefreshController.목록작업실행Async(token => ViewModel.페이지변경Async(page, token))", code);
        Assert.DoesNotContain("@(() => ViewModel.", source);
        Assert.DoesNotContain("@(page => ViewModel.", source);
        Assert.DoesNotContain("SearchRequested=\"ViewModel.", source);
        Assert.DoesNotContain("PageChanged=\"ViewModel.", source);
    }

    [Fact]
    public void 음식주문_선택은_정확한주문번호만조회하고_자동선택하지않는다()
    {
        var viewModelPath = Path.Combine(
            FindRepositoryRoot(),
            "Ssalddel.Ui.Common",
            "Areas",
            "App",
            "ViewModels",
            "음식주문페이지ViewModels.cs");
        var source = File.ReadAllText(viewModelPath);

        Assert.Contains("상세.조회Async(orderNo", source);
        Assert.Contains("상세.조회Async(normalizedOrderNo", source);
        Assert.DoesNotContain("FirstOrDefault", source);
        Assert.DoesNotContain("??=", source);
    }

    [Fact]
    public void 음식주문_만료상태는_개인화면대신_기존로그인폼과복구안내로연결한다()
    {
        var directory = FindComponentDirectory();
        var source = File.ReadAllText(Path.Combine(directory, "OrdererFoodOrderWorkspace.razor"));
        var code = File.ReadAllText(Path.Combine(directory, "OrdererFoodOrderWorkspace.razor.cs"));
        var login = File.ReadAllText(Path.Combine(directory, "OrdererFoodOrderLoginPanel.razor"));

        Assert.Contains("!Authentication.로그인됨 || ViewModel.재로그인필요", source);
        Assert.Contains("SessionExpired=\"ViewModel.재로그인필요\"", source);
        Assert.Contains("ViewModel.개인주문조회가능 && OrderSelected.HasDelegate", code);
        Assert.Contains("SessionExpired ? \"로그인이 만료되었습니다. 다시 로그인해 주세요.\"", login);
    }

    [Fact]
    public void 음식주문_화면은_수령안내를유지하고_사용자에게불필요한기술설명을제외한다()
    {
        var componentDirectory = FindComponentDirectory();
        var header = File.ReadAllText(Path.Combine(componentDirectory, "OrdererFoodOrderHeader.razor"));
        var detail = File.ReadAllText(Path.Combine(componentDirectory, "OrdererFoodOrderDetailPanel.razor"));

        Assert.Contains("@Authentication.현재사용자표시", header);
        Assert.Contains("OnClick=\"LogoutRequested\"", header);
        Assert.DoesNotContain("영속", header);
        Assert.DoesNotContain("샘플", header);
        Assert.Contains("SsalddelSensitiveDisclosureList", detail);
        Assert.Contains("수령지와 연락처는 주문한 계정에서 필요할 때만 펼쳐 확인", detail);
        Assert.Contains("음식을 받은 뒤 수령 확인을 눌러 주세요", detail);
        Assert.DoesNotContain("DriverLocationCoordinates", detail);
        Assert.DoesNotContain("위치 정확도", detail);
        Assert.DoesNotContain("EXACT ORDER", detail);
    }

    [Fact]
    public void 음식주문_상세는_기사전달완료와주문자수령확인을_분리한다()
    {
        var componentDirectory = FindComponentDirectory();
        var detail = File.ReadAllText(Path.Combine(componentDirectory, "OrdererFoodOrderDetailPanel.razor"));
        var client = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "Ssalddel.Ui.Common",
            "Areas",
            "App",
            "Services",
            "음식주문Client.cs"));

        Assert.Contains("1. 기사 전달 완료", detail);
        Assert.Contains("2. 주문자 수령 확인", detail);
        Assert.Contains("ReceiptConfirmationRequested", detail);
        Assert.Contains("/receipt-confirmation", client);
    }

    [Fact]
    public void 음식주문_상세는_배차실패를_자동취소로오인하지않도록안내한다()
    {
        var componentDirectory = FindComponentDirectory();
        var detail = File.ReadAllText(Path.Combine(componentDirectory, "OrdererFoodOrderDetailPanel.razor"));
        var presentation = File.ReadAllText(Path.Combine(componentDirectory, "OrdererFoodOrderPresentation.cs"));

        Assert.Contains("NeedsDeliveryRecovery", detail);
        Assert.Contains("DeliveryRecoveryGuide", detail);
        Assert.Contains("주문 취소나 환불이 자동 확정되는 것은 아니며", presentation);
        Assert.Contains("추천만료", presentation);
    }

    [Fact]
    public void 음식주문_화면은_좁은폭에서_로그인과목록과상세를_단일열로전환한다()
    {
        var componentDirectory = FindComponentDirectory();
        var workspaceCss = File.ReadAllText(Path.Combine(componentDirectory, "OrdererFoodOrderWorkspace.razor.css"));
        var loginCss = File.ReadAllText(Path.Combine(componentDirectory, "OrdererFoodOrderLoginPanel.razor.css"));
        var searchCss = File.ReadAllText(Path.Combine(componentDirectory, "OrdererFoodOrderSearchPanel.razor.css"));
        var listCss = File.ReadAllText(Path.Combine(componentDirectory, "OrdererFoodOrderListPanel.razor.css"));
        var detailCss = File.ReadAllText(Path.Combine(componentDirectory, "OrdererFoodOrderDetailPanel.razor.css"));

        Assert.Contains("@media (max-width: 1100px)", workspaceCss);
        Assert.Contains("grid-template-columns: 1fr", workspaceCss);
        Assert.Contains("@media (max-width: 720px)", loginCss);
        Assert.Contains("grid-template-columns: 1fr", loginCss);
        Assert.Contains("min-height: 48px", loginCss);
        Assert.Contains("@media (max-width: 720px)", searchCss);
        Assert.Contains("grid-template-columns: 1fr", searchCss);
        Assert.Contains("@media (max-width: 720px)", listCss);
        Assert.Contains("grid-template-columns: 1fr", listCss);
        Assert.Contains("@media (max-width: 720px)", detailCss);
        Assert.Contains("position: static", detailCss);
        Assert.Contains("flex-direction: column", detailCss);
    }

    private static string FindComponentDirectory()
        => Path.Combine(
            FindRepositoryRoot(),
            "Ssalddel.Ui.Common",
            "Areas",
            "App",
            "Components",
            "Food");

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
