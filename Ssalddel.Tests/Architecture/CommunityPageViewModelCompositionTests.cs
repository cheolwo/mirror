using Microsoft.Extensions.DependencyInjection;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace Ssalddel.Tests.Architecture;

public sealed class CommunityPageViewModelCompositionTests
{
    [Theory]
    [InlineData(
        "Community/Community전체FeedScreen.razor",
        "Community전체FeedViewModel")]
    [InlineData(
        "Community/CommunityMobileBoardDirectoryScreen.razor",
        "CommunityMobileBoardDirectoryViewModel")]
    [InlineData(
        "Community/CommunityMobileWorkBoardScreen.razor",
        "CommunityMobileBoardDirectoryViewModel")]
    [InlineData(
        "Community/CommunityBoardListScreen.razor",
        "CommunityBoardPageViewModel")]
    [InlineData(
        "Information/RegionalCultureSpecialtyBrowse.razor",
        "지역문화특산물목록PageViewModel")]
    [InlineData(
        "Information/RegionalCultureSpecialtyDetail.razor",
        "지역문화특산물상세PageViewModel")]
    public void Community화면은_DI_PageViewModel패턴을사용한다(
        string relativePath,
        string viewModelName)
    {
        var source = RepositorySource.Read(
            "Ssalddel.Ui.Common",
            "Areas",
            "App",
            "Components",
            relativePath.Replace('/', Path.DirectorySeparatorChar));

        Assert.Contains($"@inherits MvvmComponentBase<{viewModelName}>", source);
        Assert.DoesNotContain($"new {viewModelName}", source);
    }

    [Theory]
    [InlineData(typeof(Community전체FeedViewModel))]
    [InlineData(typeof(CommunityMobileBoardDirectoryViewModel))]
    [InlineData(typeof(CommunityBoardPageViewModel))]
    [InlineData(typeof(지역문화특산물목록PageViewModel))]
    [InlineData(typeof(지역문화특산물상세PageViewModel))]
    public void CommunityPageViewModel은_실제공통DI_module에자기형식으로Transient등록된다(Type viewModelType)
    {
        var services = new ServiceCollection();
        Assert.Same(services, services.AddCommunityPlatformUiModule());
        var descriptor = Assert.Single(services, item => item.ServiceType == viewModelType);
        Assert.Equal(ServiceLifetime.Transient, descriptor.Lifetime);
        Assert.Equal(viewModelType, descriptor.ImplementationType);
        Assert.Null(descriptor.ImplementationFactory);
    }
}
