using Hongdal.Ui.Common.Areas.App.Services;

namespace Hongdal.Tests.Services.Community;

public sealed class CommunityReadingThemePresentationTests
{
    [Fact]
    public void 경전패키지를적용하면_게시판읽기테마도같은패키지를사용한다()
    {
        var service = new PlatformCommunityDecorationStateService();
        var scriptureProduct = service.Products.First(product => product.ScriptureSource is not null);

        var applied = service.ApplyProduct(scriptureProduct);
        var presentation = CommunityReadingThemePresentation.Create(service);

        Assert.True(applied);
        Assert.Equal(scriptureProduct.PackKey, presentation.PackKey);
        Assert.Equal(scriptureProduct.Title, presentation.Title);
        Assert.Equal(scriptureProduct.ScriptureSource!.Symbol, presentation.Symbol);
        Assert.Equal(scriptureProduct.HomeTheme!.AccentColor, presentation.AccentColor);
        Assert.True(presentation.IsCustomized);
        Assert.Contains("--platform-home-accent", presentation.CssVariables);
    }

    [Fact]
    public void 홈테마를끄면_게시판읽기테마는기본값으로돌아간다()
    {
        var service = new PlatformCommunityDecorationStateService();
        var scriptureProduct = service.Products.First(product => product.ScriptureSource is not null);
        service.ApplyProduct(scriptureProduct);

        service.SetTargetEnabled(CommunityDecorationTarget.HomeNavigatorTheme, false);
        var presentation = CommunityReadingThemePresentation.Create(service);

        Assert.Equal(PlatformCommunityDecorationStateService.DefaultHomeThemePackKey, presentation.PackKey);
        Assert.False(presentation.IsCustomized);
        Assert.Equal("H", presentation.Symbol);
    }
}
