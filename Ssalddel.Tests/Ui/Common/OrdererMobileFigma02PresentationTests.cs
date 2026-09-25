namespace Ssalddel.Tests.Ui.Common;

public sealed class OrdererMobileFigma02PresentationTests
{
    [Theory]
    [InlineData("Home.razor", "02.01")]
    [InlineData("ProducePriceComparison.razor", "02.02A")]
    [InlineData("GroupPurchaseProducts.razor", "02A2.01")]
    [InlineData("GroupPurchaseProductDetail.razor", "02A2.02")]
    [InlineData("GroupPurchaseRecipeUse.razor", "같이주문여정단계.레시피활용")]
    [InlineData("GroupPurchaseOrderModeComparison.razor", "같이주문여정단계.주문방식비교")]
    [InlineData("GroupPurchaseDeliveryScopes.razor", "같이주문여정단계.배송권찾기")]
    [InlineData("GroupPurchaseTogetherOrders.razor", "같이주문여정단계.같이주문목록")]
    [InlineData("GroupPurchaseTogetherOrderDetail.razor", "같이주문여정단계.같이주문상세")]
    [InlineData("GroupPurchaseSupplierRelationship.razor", "같이주문여정단계.공급자관계")]
    [InlineData("GroupPurchaseSupplierMembership.razor", "같이주문여정단계.공급자Membership")]
    [InlineData("GroupPurchaseUrgentHarvestOffer.razor", "같이주문여정단계.긴급수확제안")]
    [InlineData("GroupPurchaseUrgentHarvestReview.razor", "같이주문여정단계.긴급수확검토")]
    [InlineData("GroupPurchaseWishCreate.razor", "02.03")]
    [InlineData("GroupPurchaseWishDetail.razor", "02.04")]
    [InlineData("GroupPurchaseWishEdit.razor", "02.05")]
    [InlineData("GroupPurchaseGroups.razor", "02.06")]
    [InlineData("GroupPurchaseGroupDetail.razor", "02.07")]
    [InlineData("GroupPurchaseImportReview.razor", "02.08")]
    [InlineData("GroupImportReadinessOverview.razor", "02.09")]
    [InlineData("GroupImportReadinessCosts.razor", "02.10")]
    [InlineData("OrderDetail.razor", "02.11")]
    [InlineData("IndividualImportLedger.razor", "02.12")]
    [InlineData("IndividualExportLedger.razor", "02.13")]
    [InlineData("GroupExportLedger.razor", "02.14")]
    public void Figma02화면은_기존MauiRoute에책임코드를고정한다(
        string fileName,
        string screenCode)
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "OrdererApp",
            "Components",
            "Pages",
            fileName));

        Assert.Contains(screenCode, source);
    }

    [Fact]
    public void 주문자MauiShell은_FigmaAppBar와네개하단Navigation을제공한다()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "OrdererApp",
            "Components",
            "Layout",
            "MainLayout.razor"));

        Assert.Contains("orderer-mobile-shell__appbar", source);
        Assert.Contains("orderer-mobile-shell__bottom-nav", source);
        Assert.Contains("음식 · 마트 · 화물 주문", source);
        Assert.Contains(">홈</span>", source);
        Assert.Contains(">음식</span>", source);
        Assert.Contains(">마트</span>", source);
        Assert.Contains(">원장</span>", source);
        Assert.DoesNotContain("같이 주문 후보", source);
        Assert.DoesNotContain("배송권의 같이 주문", source);
    }

    [Fact]
    public void 주문자홈은_Figma바로가기와비구속경계를기존Route로연결한다()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "OrdererApp",
            "Components",
            "Orderer",
            "OrdererMobileHomeScreen.razor"));

        Assert.Contains("OrdererRoutes.Food", source);
        Assert.Contains("OrdererRoutes.Mart", source);
        Assert.Contains("OrdererRoutes.Restaurants", source);
        Assert.Contains("OrdererRoutes.Cargo", source);
        Assert.Contains("OrdererRoutes.Orders", source);
        Assert.Contains("상품을 둘러보는 것만으로 주문되거나 결제되지 않습니다.", source);
        Assert.DoesNotContain("현장 검증", source);
        Assert.DoesNotContain("서버가 확정한", source);
        Assert.Contains("data-orderer-screen=\"@ScreenCode\"", source);
        Assert.DoesNotContain("OrdererRoutes.GroupPurchaseGroups", source);
        Assert.DoesNotContain("OrdererRoutes.GroupPurchaseTogetherOrders", source);
    }

    [Fact]
    public void Figma02A2여정은_버튼Route와읽기Api를_하나의흐름으로연결한다()
    {
        var root = FindRepositoryRoot();
        var screen = File.ReadAllText(Path.Combine(
            root,
            "OrdererApp",
            "Components",
            "GroupPurchase",
            "같이주문탐색여정Screen.razor"));
        var service = File.ReadAllText(Path.Combine(
            root,
            "OrdererApp",
            "Services",
            "같이주문여정ReadService.cs"));

        Assert.Contains("together-journey__map", screen);
        Assert.Contains("RecipeUseFor", screen);
        Assert.Contains("OrderModeComparisonFor", screen);
        Assert.Contains("DeliveryScopeFor", screen);
        Assert.Contains("TogetherOrderDetailFor", screen);
        Assert.Contains("SupplierRelationshipFor", screen);
        Assert.Contains("UrgentHarvestReviewFor", screen);
        Assert.DoesNotContain("25kg 상자 환산", screen);
        Assert.Contains("같이주문수량Presentation.수량(_같이주문상세.총희망수량", screen);
        Assert.Contains("같이주문수량Presentation.선택수량(_같이주문상세.추가필요수량", screen);
        Assert.Contains("원 거래단위를 임의로 kg이나 상자로 바꾸지 않습니다.", screen);
        Assert.Contains("실제 포장 수와 LCL·FCL은 공급자·포워더 회신 전까지 미판정", screen);
        Assert.Contains("order-mode-comparisons/recipe-uses", service);
        Assert.Contains("order-mode-comparisons/preview", service);
        Assert.Contains("group-purchase-auto-groups", service);
    }

    [Fact]
    public void 가격비교는_공개Route를보존하고_주문자흐름에서재료와원함으로이어진다()
    {
        var root = FindRepositoryRoot();
        var ordererPage = File.ReadAllText(Path.Combine(
            root,
            "OrdererApp",
            "Components",
            "Pages",
            "ProducePriceComparison.razor"));
        var publicPage = File.ReadAllText(Path.Combine(
            root,
            "SsalddelApp",
            "Components",
            "Pages",
            "ProduceRegionalPriceComparison.razor"));

        Assert.Contains("OrdererRoutes.GroupPurchaseProducts", ordererPage);
        Assert.Contains("OrdererRoutes.GroupPurchaseWishCreate", ordererPage);
        Assert.Contains("OrdererRoutes.GroupPurchaseGroups", ordererPage);
        Assert.Contains("비구속 수요", ordererPage);
        Assert.Contains("@page \"/information/produce-price-comparison\"", publicPage);
    }

    [Theory]
    [InlineData("IndividualImportLedgerScreen.razor")]
    [InlineData("IndividualExportLedgerScreen.razor")]
    [InlineData("GroupExportLedgerScreen.razor")]
    public void 공용무역원장은_기존소비자를위한Heading기본값을유지한다(string fileName)
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "Ssalddel.Ui.Common",
            "Areas",
            "App",
            "Components",
            "Orderer",
            fileName));

        Assert.Contains("ShowHeading { get; set; } = true", source);
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
