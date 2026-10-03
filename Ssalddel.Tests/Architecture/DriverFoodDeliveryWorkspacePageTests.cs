namespace Ssalddel.Tests.Architecture;

public sealed class DriverFoodDeliveryWorkspacePageTests
{
    [Fact]
    public void 기사앱은_서버음식배달업무공간을_전용경로로연결한다()
    {
        var root = FindRepositoryRoot();
        var service = File.ReadAllText(Path.Combine(root, "DriverApp", "Services", "DriverFoodDeliveryApiService.cs"));
        var routes = File.ReadAllText(Path.Combine(root, "DriverApp", "Services", "DriverRoutes.cs"));
        var registrations = File.ReadAllText(Path.Combine(root, "DriverApp", "Services", "DriverServiceCollectionExtensions.cs"));
        var rootRedirect = File.ReadAllText(Path.Combine(
            root,
            "DriverApp",
            "Components",
            "Pages",
            "RootRedirect.razor"));
        var page = File.ReadAllText(Path.Combine(
            root,
            "DriverApp",
            "Components",
            "Pages",
            "Driver",
            "03_Progress",
            "음식배달업무Page.razor"));

        Assert.Contains("api/v1/driver/food-deliveries", service);
        Assert.Contains("/workspace", service);
        Assert.Contains("\"pickup-complete\"", service);
        Assert.Contains("\"delivery-complete\"", service);
        Assert.Contains("FoodDeliveries = \"/driver/food-deliveries\"", routes);
        Assert.Contains("IDriverFoodDeliveryApiService, DriverFoodDeliveryApiService", registrations);
        Assert.Contains("@page \"/\"", rootRedirect);
        Assert.Contains("NavigateTo(DriverRoutes.HomeSummary", rootRedirect);
        Assert.Contains("@page \"/driver/food-deliveries\"", page);
        Assert.Contains("PickupActionLabel(delivery.ExecutionProfile)", page);
        Assert.Contains("CompletionActionLabel(delivery.ExecutionProfile)", page);
        Assert.Contains("픽업행동명", page);
        Assert.Contains("완료행동명", page);
        Assert.DoesNotContain("Sample", service, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void 음식점앱은_수락응답의_배차인계를_상세화면에표시한다()
    {
        var root = FindRepositoryRoot();
        var page = File.ReadAllText(Path.Combine(
            root,
            "RestaurantDeskApp",
            "Components",
            "Pages",
            "OrderDetail.razor")) + File.ReadAllText(Path.Combine(
            root, "RestaurantDeskApp", "Components", "Pages", "OrderDetail.razor.cs"));
        var service = File.ReadAllText(Path.Combine(
            root,
            "RestaurantDeskApp",
            "Services",
            "음식점주문DeskService.cs"));

        Assert.Contains("DispatchStatusLabel(order.배차상태)", page);
        Assert.Contains("FormatOptionalDate(order.배차요청시각Utc)", page);
        Assert.Contains("주문을 확인해 배차를 요청했습니다", page);
        Assert.Contains("기사 배정 후 조리를 시작해 주세요", page);
        Assert.Contains("item.배차상태 = detail.배차상태", service);
        Assert.Contains("item.배차요청시각Utc = detail.배차요청시각Utc", service);
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
