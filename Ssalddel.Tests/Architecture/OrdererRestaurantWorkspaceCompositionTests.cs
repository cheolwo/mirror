using Ssalddel.Ui.Common.Areas.App.Components.Food;

namespace Ssalddel.Tests.Architecture;

public sealed class OrdererRestaurantWorkspaceCompositionTests
{
    [Fact]
    public void 주문자_음식점_루트는_접근상태와_화면영역만_조립한다()
    {
        var componentDirectory = FindComponentDirectory();
        var pagePath = Path.Combine(componentDirectory, "OrdererRestaurantWorkspace.razor");
        var source = File.ReadAllText(pagePath);

        Assert.True(File.ReadLines(pagePath).Count() <= 60);
        Assert.Contains("<OrdererRestaurantAccessState", source);
        Assert.Contains("<OrdererRestaurantSearchPanel", source);
        Assert.Contains("<OrdererRestaurantResultList", source);
        Assert.Contains("<OrdererRestaurantDetailPanel", source);
        Assert.Contains("else if (ViewModel.인증화면표시)", source);
        Assert.Contains("<OrdererRestaurantAuthenticationWorkspace", source);
        Assert.DoesNotContain("<MudSelect", source);
        Assert.DoesNotContain("<MudTextField", source);
        Assert.DoesNotContain("<MudPagination", source);
        Assert.DoesNotContain("<article", source);
        Assert.DoesNotContain("@foreach", source);
    }

    [Theory]
    [InlineData("OrdererRestaurantAccessState.razor")]
    [InlineData("OrdererRestaurantAccessState.razor.css")]
    [InlineData("OrdererRestaurantSearchPanel.razor")]
    [InlineData("OrdererRestaurantSearchPanel.razor.css")]
    [InlineData("OrdererRestaurantResultList.razor")]
    [InlineData("OrdererRestaurantResultList.razor.css")]
    [InlineData("OrdererRestaurantDetailPanel.razor")]
    [InlineData("OrdererRestaurantDetailPanel.razor.css")]
    [InlineData("OrdererFoodOrderComposer.razor")]
    [InlineData("OrdererFoodOrderComposer.razor.css")]
    [InlineData("OrdererRestaurantAuthenticationWorkspace.razor")]
    [InlineData("OrdererRestaurantAuthenticationWorkspace.razor.css")]
    [InlineData("OrdererRestaurantPresentation.cs")]
    public void 음식점_화면과_표현책임은_전용파일로_존재한다(string fileName)
    {
        var componentPath = Path.Combine(FindComponentDirectory(), fileName);

        Assert.True(File.Exists(componentPath), $"음식점 전용 파일이 없습니다: {fileName}");
        Assert.NotEmpty(File.ReadAllText(componentPath));
    }

    [Theory]
    [InlineData(null, "거리 기준 없음")]
    [InlineData(7.25, "기준점 7.25km")]
    public void 거리표현은_공개기준점과_값없음을_구분한다(double? distanceKm, string expected)
    {
        var actual = OrdererRestaurantPresentation.DistanceLabel(
            distanceKm.HasValue ? (decimal)distanceKm.Value : null);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void 음식점_화면은_좁은폭에서_단일열과_비고정상세로_전환한다()
    {
        var componentDirectory = FindComponentDirectory();
        var workspaceCss = File.ReadAllText(Path.Combine(componentDirectory, "OrdererRestaurantWorkspace.razor.css"));
        var searchCss = File.ReadAllText(Path.Combine(componentDirectory, "OrdererRestaurantSearchPanel.razor.css"));
        var listCss = File.ReadAllText(Path.Combine(componentDirectory, "OrdererRestaurantResultList.razor.css"));
        var detailCss = File.ReadAllText(Path.Combine(componentDirectory, "OrdererRestaurantDetailPanel.razor.css"));

        Assert.Contains("@media (max-width: 1000px)", workspaceCss);
        Assert.Contains("grid-template-columns: 1fr", workspaceCss);
        Assert.Contains("@media (max-width: 700px)", searchCss);
        Assert.Contains("@media (max-width: 700px)", listCss);
        Assert.Contains("position: static", detailCss);
        Assert.Contains("grid-template-columns: 1fr", detailCss);
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
