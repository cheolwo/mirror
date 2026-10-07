namespace Ssalddel.Tests.Architecture;

public sealed class ShipperRequestAuthoringCompositionTests
{
    [Theory]
    [InlineData("Ssalddel.WebApp", "Pages/ShipperRequestCargoPage.razor", "/shipper/request/cargo", "<ShipperRequestCargoScreen")]
    [InlineData("Ssalddel.WebApp", "Pages/ShipperRequestTransportPage.razor", "/shipper/request/transport", "<ShipperRequestTransportScreen")]
    [InlineData("Ssalddel.WebApp", "Pages/ShipperRequestProcedurePage.razor", "/shipper/request/procedure", "<ShipperRequestProcedureScreen")]
    [InlineData("Ssalddel.WebApp", "Pages/ShipperRequestReviewPage.razor", "/shipper/request/review", "<ShipperRequestReviewScreen")]
    [InlineData("SsalddelApp", "Components/Pages/ShipperRequestCargoPage.razor", "/shipper/request/cargo", "<ShipperRequestCargoScreen")]
    [InlineData("SsalddelApp", "Components/Pages/ShipperRequestTransportPage.razor", "/shipper/request/transport", "<ShipperRequestTransportScreen")]
    [InlineData("SsalddelApp", "Components/Pages/ShipperRequestProcedurePage.razor", "/shipper/request/procedure", "<ShipperRequestProcedureScreen")]
    [InlineData("SsalddelApp", "Components/Pages/ShipperRequestReviewPage.razor", "/shipper/request/review", "<ShipperRequestReviewScreen")]
    public void Web과모바일단계Route는_같은공용Screen을조립한다(
        string project,
        string relativePath,
        string route,
        string screenMarkup)
    {
        var path = ProjectFile(project, relativePath);
        var source = File.ReadAllText(path);

        Assert.Contains($"@page \"{route}\"", source);
        Assert.Contains(screenMarkup, source);
        Assert.Contains("ShipperRequestNavigationContext.Parse", source);
        Assert.DoesNotContain(File.ReadLines(path), line => line.Trim().Equals("try", StringComparison.Ordinal));
        Assert.DoesNotContain("IShipperOperationsService", source);
        Assert.DoesNotContain("ICommunityPostClient", source);
        if (project == "Ssalddel.WebApp")
        {
            Assert.Contains(route == "/shipper/request/cargo"
                ? "RootBackHref=\"@(RouteContext.ReturnPath ?? RouteContext.RootPath)\""
                : "RootBackHref=\"@RouteContext.RootPath\"", source);
        }
    }

    [Fact]
    public void WebRoot는_같은네Screen의AdaptiveComposition만조립한다()
    {
        var route = File.ReadAllText(ProjectFile("Ssalddel.WebApp", "Pages/ShipperRequestPage.razor"));
        var screen = File.ReadAllText(ComponentFile("ShipperRequestAuthoringScreen.razor"));

        Assert.Contains("<ShipperRequestAuthoringScreen", route);
        Assert.Contains("<ShipperRequestCargoScreen", screen);
        Assert.Contains("<ShipperRequestTransportScreen", screen);
        Assert.Contains("<ShipperRequestProcedureScreen", screen);
        Assert.Contains("<ShipperRequestReviewScreen", screen);
        Assert.DoesNotContain("@page ", screen);
    }

    [Fact]
    public void 모바일Root는_화물단계로문맥을보존해호환이동한다()
    {
        var source = File.ReadAllText(ProjectFile(
            "SsalddelApp",
            "Components/Pages/ShipperRequestWizard.razor"));

        Assert.Contains("ShipperRequestNavigationContext.Parse", source);
        Assert.Contains("ShipperRequestAuthoringStep.Cargo", source);
        Assert.Contains("replace: true", source);
        Assert.DoesNotContain("Ssalddel운송모델작성Panel", source);
    }

    [Fact]
    public void 공용Screen은_플랫폼서비스와Route를소유하지않는다()
    {
        foreach (var fileName in new[]
                 {
                     "ShipperRequestCargoScreen.razor",
                     "ShipperRequestTransportScreen.razor",
                     "ShipperRequestProcedureScreen.razor",
                     "ShipperRequestReviewScreen.razor"
                 })
        {
            var source = File.ReadAllText(ComponentFile(fileName));
            Assert.DoesNotContain("@page ", source);
            Assert.DoesNotContain("Ssalddel.WebApp", source);
            Assert.DoesNotContain("SsalddelApp", source);
            Assert.DoesNotContain("IShipperOperationsService", source);
            Assert.DoesNotContain("ICommunityPostClient", source);
        }
    }

    [Fact]
    public void 공용Screen의문자열Parameter는_식으로전달해literal노출을막는다()
    {
        var frame = File.ReadAllText(ComponentFile("ShipperRequestScreenFrame.razor"));
        var authoring = File.ReadAllText(ComponentFile("ShipperRequestAuthoringScreen.razor"));
        var webRoute = File.ReadAllText(ProjectFile("Ssalddel.WebApp", "Pages/ShipperRequestPage.razor"));

        Assert.Contains("RootBackHref=\"@RootBackHref\"", frame);
        Assert.Contains("RootBackLabel=\"@RootBackLabel\"", frame);
        Assert.Contains("StatusMessage=\"@StatusMessage\"", authoring);
        Assert.Contains("RegistrationBoundaryMessage=\"@RegistrationBoundaryMessage\"", authoring);
        Assert.Contains("BulkHref=\"@ShipperRequestPageRoutes.Bulk\"", webRoute);
        Assert.Contains("AutoSaveMessage=\"@PageModel.AutoSaveMessage\"", webRoute);
    }

    [Fact]
    public void 과거ModeMultiplexer와복합Panel은_제거한다()
    {
        Assert.False(File.Exists(ProjectFile(
            "Ssalddel.WebApp",
            "Pages/ShipperRequestStepPage.razor")));
        Assert.False(File.Exists(ComponentFile("Ssalddel운송모델작성Panel.razor")));
    }

    private static string ComponentFile(string fileName)
        => Path.Combine(
            FindRepositoryRoot(),
            "Ssalddel.Ui.Common",
            "Areas",
            "App",
            "Components",
            "Transport",
            fileName);

    private static string ProjectFile(string project, string relativePath)
        => Path.Combine(
            FindRepositoryRoot(),
            project,
            relativePath.Replace('/', Path.DirectorySeparatorChar));

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
