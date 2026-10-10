using System.Xml.Linq;

namespace Ssalddel.Tests.Clients;

/// <summary>소스 조립 검사이며 실제 Android 화면 렌더 증거를 대신하지 않습니다.</summary>
public sealed class FDriverMartDeliveryCompositionTests
{
    [Fact]
    public void ProfileMenuRouteAndDependencyRegistrationBindAnIndependentNativePage()
    {
        var menu = Read("FDriverApp/Pages/MainPage.xaml");
        Assert.Contains("Text=\"살뜰마트 배송\"", menu); Assert.Contains("OnMartDeliveriesClicked", menu);
        Assert.Contains("OpenProfileSettingsAsync(살뜰마트배송Page.RouteName)", Read("FDriverApp/Pages/MainPage.xaml.cs"));
        Assert.Contains("Routing.RegisterRoute(살뜰마트배송Page.RouteName, typeof(살뜰마트배송Page))", Read("FDriverApp/AppShell.xaml.cs"));
        var di = Read("FDriverApp/MauiProgram.cs");
        Assert.Contains("AddSingleton<I살뜰마트배송ApiClient, 살뜰마트배송ApiClient>", di);
        Assert.Contains("AddSingleton<I살뜰마트배송PendingStore, 살뜰마트배송SecurePendingStore>", di);
        Assert.Contains("AddSingleton<FDriverApp.PageModels.살뜰마트배송PageViewModel>", di);
        Assert.Contains("AddTransient<살뜰마트배송Page>", di);
        Assert.Contains("food-delivery-workspace", Read("FDriverApp/AppShell.xaml"));
    }

    [Fact]
    public void PageBindsStageActionsAndRecovery_WithoutHardwareOrFoodCooking()
    {
        var text = Read("FDriverApp/Pages/살뜰마트배송Page.xaml");
        var xml = XDocument.Parse(text);
        Assert.Equal("ContentPage", xml.Root!.Name.LocalName);
        foreach (var command in new[] { "AcceptCommand", "RejectCommand", "PickupCommand", "DeliverCommand", "CheckResultCommand", "RetryCommand" })
            Assert.Contains("{Binding " + command + "}", text);
        Assert.Contains("{StaticResource DriverCardStyle}", text);
        Assert.Contains("IsPassword=\"True\"", text); Assert.Contains("MaxLength=\"512\"", text);
        Assert.DoesNotContain("Camera", text); Assert.DoesNotContain("Scanner", text); Assert.DoesNotContain("Print", text);
        Assert.DoesNotContain("재조리", text); Assert.DoesNotContain("음식점 도착", text);
        Assert.Contains("_model.Deactivate()", Read("FDriverApp/Pages/살뜰마트배송Page.xaml.cs"));
        Assert.Contains("?focus=profile", Read("FDriverApp/Pages/살뜰마트배송Page.xaml.cs"));
    }

    [Fact]
    public void ClientAndPendingStoreDoNotReuseFoodSourceOrPersistRawCodes()
    {
        var client = Read("FDriverApp/Services/살뜰마트배송ApiClient.cs");
        Assert.Contains("api/v1/driver/mart-deliveries", client); Assert.DoesNotContain("api/v1/driver/food-deliveries", client);
        var store = Read("FDriverApp/Services/살뜰마트배송SecurePendingStore.cs");
        Assert.Contains("SecureStorage.Default", store); Assert.Contains("ssalddel.fdriver.mart-pending.v1", store);
        var record = client[client.IndexOf("public sealed record 살뜰마트배송미확인요청", StringComparison.Ordinal)..client.IndexOf("public interface I살뜰마트배송PendingStore", StringComparison.Ordinal)];
        Assert.Contains("CodeHash", record); Assert.DoesNotContain("string Code,", record);
        Assert.DoesNotContain("Address", record); Assert.DoesNotContain("AccessToken", record);
        Assert.DoesNotContain("Console.", client); Assert.DoesNotContain("Logger", client);
    }

    private static string Read(string relative)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "FDriverApp"))) directory = directory.Parent;
        return File.ReadAllText(Path.Combine((directory ?? throw new DirectoryNotFoundException("저장소를 찾지 못했습니다.")).FullName, relative));
    }
}
