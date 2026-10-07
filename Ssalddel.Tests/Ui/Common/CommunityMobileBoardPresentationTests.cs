using Ssalddel.Contracts.Common.Community;
using Ssalddel.Ui.Common.Areas.App.Models;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;

namespace Ssalddel.Tests.Ui.Common;

public sealed class CommunityMobileBoardPresentationTests
{
    [Fact]
    public void 생활모드는_Figma01의_네게시판을_실제catalogKey에연결한다()
    {
        var boards = CommunityMobileBoardPresentation.LifeBoards;

        Assert.Equal(4, boards.Count);
        Assert.Equal(
            [
                CommunityBoardKeys.Vow,
                CommunityBoardKeys.FreeLife,
                CommunityBoardKeys.RegionalCulture,
                CommunityBoardKeys.InformationPrices
            ],
            boards.Select(board => board.BoardKey).ToArray());
        Assert.Equal(
            "농수산물 가격",
            boards.Single(board => board.BoardKey == CommunityBoardKeys.InformationPrices).DisplayName);
        Assert.All(boards, board => Assert.NotNull(CommunityBoardCatalog.Find(board.BoardKey)));
    }

    [Fact]
    public void 공공데이터묶음은_네전용게시판을_주기성목록으로연결한다()
    {
        var boards = CommunityMobileBoardPresentation.PublicDataBoards;

        Assert.Equal(
            [
                CommunityBoardKeys.PeriodicDataKamis,
                CommunityBoardKeys.PeriodicDataMfds,
                CommunityBoardKeys.PeriodicDataUsda,
                CommunityBoardKeys.PeriodicDataCustomsImportUnitPrice
            ],
            boards.Select(board => board.BoardKey));
        Assert.All(boards, board =>
        {
            Assert.True(CommunityPeriodicDataBoardCatalog.IsDataBoard(board.BoardKey));
            Assert.Contains("boardKey=", board.Href, StringComparison.Ordinal);
            Assert.Contains(
                Uri.EscapeDataString(CommunityPeriodicPostTopicCatalog.PeriodicListFilter),
                board.Href,
                StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void 업무모드는_다섯탐색묶음으로_열여섯업무단위게시판을빠짐없이보여준다()
    {
        var groups = CommunityMobileBoardPresentation.WorkGroups;
        var boardKeys = groups
            .Where(group => !group.IsCrossCutting)
            .SelectMany(group => group.BoardKeys)
            .ToArray();

        Assert.Equal(5, groups.Count);
        Assert.Equal(
            ["01A.08", "01A.09", "01A.10", "01A.11", "01A.12"],
            groups.Select(group => group.ScreenCode));
        Assert.Equal(16, boardKeys.Length);
        Assert.Equal(16, boardKeys.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.True(
            CommunityActivityBoardCatalog.Boards
                .Select(board => board.Key)
                .ToHashSet(StringComparer.OrdinalIgnoreCase)
                .SetEquals(boardKeys));

        var crossCutting = Assert.Single(groups, group => group.IsCrossCutting);
        Assert.Equal("ledger", crossCutting.Key);
        Assert.Empty(crossCutting.BoardKeys);
    }

    [Theory]
    [InlineData("CommunityHomePage.razor", "<CommunityMobileBoardDirectoryScreen")]
    [InlineData("CommunityBoardDirectoryPage.razor", "@page \"/community/boards/directory\"")]
    [InlineData("CommunityBoardPage.razor", "공개 커뮤니티 · 01A.02")]
    [InlineData("CommunityPostDetailPage.razor", "공개 커뮤니티 · 01A.03")]
    [InlineData("CommunityPostComposePage.razor", "공개 커뮤니티 · 01A.04")]
    [InlineData("CommunityRecommendedPostsPage.razor", "공개 커뮤니티 · 01A.05")]
    [InlineData("CommunityBoardManagementPage.razor", "공개 커뮤니티 · 01A.06")]
    [InlineData("CommunityWorkBoardPage.razor", "<CommunityMobileWorkBoardScreen")]
    [InlineData("CommunityPersonalPage.razor", "@page \"/community/me\"")]
    [InlineData("CommunityIndividualOrdersPage.razor", "@page \"/community/orders\"")]
    public void MauiCommunity화면은_전용모바일Shell을사용한다(
        string fileName,
        string responsibilityMarker)
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "SsalddelApp",
            "Components",
            "Pages",
            fileName));

        Assert.Contains("@layout CommunityMobileLayout", source);
        Assert.Contains(responsibilityMarker, source);
    }

    [Fact]
    public void 모바일Shell은_FigmaAppBar와하단Navigation을제공한다()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "SsalddelApp",
            "Components",
            "Layout",
            "CommunityMobileLayout.razor"));

        Assert.Contains("community-mobile-shell__appbar", source);
        Assert.Contains("community-mobile-shell__bottom-nav", source);
        Assert.Contains("공개 커뮤니티", source);
        Assert.Contains("내 정보", source);
        Assert.Contains("내 글", source);
        Assert.Contains("내 주문", source);
        Assert.Contains("CommunityPageRoutes.IndividualOrders", source);
        Assert.Contains("지역 문화", source);
        Assert.Contains("농수산물 가격", source);
        Assert.DoesNotContain("업무 게시판", source);
        Assert.DoesNotContain("공공데이터 게시판", source);
        Assert.Contains("CommunityPageRoutes.Compose", source);
    }

    [Fact]
    public void MauiApp은_역할선택홈에서_여덟역할로진입하고_개별주문에서_주문자App원장으로이어진다()
    {
        var root = FindRepositoryRoot();
        var mainPageSource = File.ReadAllText(Path.Combine(root, "SsalddelApp", "MainPage.xaml"));
        var unifiedHomeSource = File.ReadAllText(Path.Combine(
            root, "SsalddelApp", "Components", "Pages", "UnifiedHome.razor"));
        var roleWorkspaceSource = File.ReadAllText(Path.Combine(
            root, "SsalddelApp", "Components", "Pages", "RoleWorkspacePage.razor"));
        var communityHomeSource = File.ReadAllText(Path.Combine(
            root,
            "SsalddelApp",
            "Components",
            "Pages",
            "CommunityHomePage.razor"));
        var individualOrdersSource = File.ReadAllText(Path.Combine(
            root,
            "SsalddelApp",
            "Components",
            "Pages",
            "CommunityIndividualOrdersPage.razor"));
        var journeySource = File.ReadAllText(Path.Combine(
            root,
            "Ssalddel.Ui.Common",
            "Areas",
            "App",
            "Components",
            "Community",
            "CommunityIndividualOrderJourneyPanel.razor"));

        Assert.Contains("StartPath=\"/\"", mainPageSource);
        Assert.Contains("@page \"/\"", unifiedHomeSource);
        Assert.Contains("<Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core.RoleSelectionHome", unifiedHomeSource);
        Assert.Contains("@page \"/workspace/{RoleKey}\"", roleWorkspaceSource);
        Assert.Equal(
            ["community", "orderer", "restaurant", "food-driver", "shipper", "cargo-driver", "warehouse", "operator"],
            RoleWorkspaceCatalog.Roles.Select(role => role.Key).ToArray());
        Assert.All(RoleWorkspaceCatalog.Roles, role => Assert.Equal("/workspace/" + role.Key, role.Href));
        Assert.Contains("<CommunityIndividualOrderJourneyPanel", communityHomeSource);
        Assert.Contains("<CommunityIndividualOrderListScreen", individualOrdersSource);
        Assert.Contains("0.0 둘러보기 → 0.5 내 주문 → 1.0 함께 주문", journeySource);
        Assert.Contains("별도로 동의한 주문만", journeySource);
        Assert.Contains("결제·계약·수입·운송을 자동 실행하지 않습니다", journeySource);
    }

    [Fact]
    public void Maui기존게시판인덱스도_주요네게시판catalog를공유한다()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "Ssalddel.Ui.Common",
            "Areas",
            "App",
            "Components",
            "Community",
            "PlatformCommunityHome.PostMetadata.razor.cs"));

        Assert.Contains("CommunityBoardCatalog.FeaturedBoards", source);
        Assert.Contains("board.DisplayName", source);
    }

    [Fact]
    public void Maui공통홈과기존게시판모음Route는_새01디렉터리로연결된다()
    {
        var root = FindRepositoryRoot();
        var communityHomeSource = File.ReadAllText(Path.Combine(
            root,
            "SsalddelApp",
            "Components",
            "Pages",
            "CommunityHomePage.razor"));
        var boardDirectorySource = File.ReadAllText(Path.Combine(
            root,
            "SsalddelApp",
            "Components",
            "Pages",
            "CommunityBoardDirectoryPage.razor"));

        Assert.Contains("@page \"/community\"", communityHomeSource);
        Assert.Contains("@page \"/community/boards/directory\"", boardDirectorySource);
        Assert.Contains("<CommunityMobileBoardDirectoryScreen", boardDirectorySource);

        var roleNeutralHomeSource = File.ReadAllText(Path.Combine(
            root,
            "SsalddelApp",
            "Components",
            "Pages",
            "RoleNeutralHome.razor"));
        Assert.Contains("href=\"@CommunityPageRoutes.Home\"", roleNeutralHomeSource);

        var warehouseHomeSource = File.ReadAllText(Path.Combine(
            root,
            "SsalddelApp",
            "Components",
            "Pages",
            "WarehouseManagerRoleHome.razor"));
        Assert.Contains("CommunityDirectoryHref=\"@CommunityPageRoutes.Home\"", warehouseHomeSource);
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
