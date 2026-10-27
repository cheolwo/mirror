namespace Ssalddel.Tests.Architecture;

public sealed class PlatformCommunityHomeCompositionTests
{
    [Fact]
    public void 커뮤니티_홈_루트는_경로와_화면_mode만_조립한다()
    {
        var componentDirectory = FindComponentDirectory();
        var pagePath = Path.Combine(componentDirectory, "PlatformCommunityHome.razor");
        var source = File.ReadAllText(pagePath);

        Assert.True(File.ReadLines(pagePath).Count() <= 340);
        Assert.Contains("<PlatformCommunityHomeHero", source);
        Assert.Contains("<PlatformCommunityHomeDiagramStage", source);
        Assert.Contains("<PlatformCommunityHomeFeed", source);
        Assert.Contains("<PlatformCommunityHomeComposer", source);
        Assert.Contains("<PlatformCommunityHomeWorkspace", source);
        Assert.DoesNotContain("<PlatformCommunityDiagramToolbar", source);
        Assert.DoesNotContain("<PlatformCommunityPostComposer", source);
        Assert.DoesNotContain("<PlatformCommunityBoardManagementPanel", source);
        Assert.DoesNotContain("platform-ledger-diagram-workbench", source);
    }

    [Theory]
    [InlineData("PlatformCommunityHomeHero.razor")]
    [InlineData("PlatformCommunityHomeFeed.razor")]
    [InlineData("PlatformCommunityHomeComposer.razor")]
    [InlineData("PlatformCommunityHomeDiagramStage.razor")]
    [InlineData("PlatformCommunityHome.DiagramStageSurface.razor.cs")]
    [InlineData("PlatformCommunityHomeLedgerDraft.razor")]
    [InlineData("PlatformCommunityHome.LedgerDraftSurface.razor.cs")]
    [InlineData("PlatformCommunityHomeWorkspace.razor")]
    [InlineData("PlatformCommunityHome.WorkspaceSurface.razor.cs")]
    [InlineData("CommunityWorkspaceScreen.razor")]
    [InlineData("CommunityBoardManagementScreen.razor")]
    [InlineData("CommunityLedgerDraftScreen.razor")]
    [InlineData("CommunityPostComposeScreen.razor")]
    [InlineData("CommunityRecommendedPostListScreen.razor")]
    [InlineData("CommunityRecommendedPostDetailScreen.razor")]
    [InlineData("CommunityPostDetailScreen.razor")]
    public void 커뮤니티_홈_화면_책임은_전용_파일로_존재한다(string fileName)
    {
        var componentPath = Path.Combine(FindComponentDirectory(), fileName);

        Assert.True(File.Exists(componentPath), $"커뮤니티 홈 전용 파일이 없습니다: {fileName}");
        Assert.NotEmpty(File.ReadAllText(componentPath));
    }

    [Fact]
    public void 글쓰기전용mode는_홈피드와업무도구없이_작성기만조립한다()
    {
        var componentDirectory = FindComponentDirectory();
        var home = File.ReadAllText(Path.Combine(componentDirectory, "PlatformCommunityHome.razor"));
        var workspace = File.ReadAllText(Path.Combine(componentDirectory, "PlatformCommunityHomeWorkspace.razor"));
        var composer = File.ReadAllText(Path.Combine(componentDirectory, "PlatformCommunityHomeComposer.razor"));

        Assert.Contains("else if (ComposeOnly)", home);
        Assert.Contains("<PlatformCommunityHomeComposer Context=\"@Workspace\"", home);
        Assert.Contains("Surface == CommunityWorkspaceSurfaceKind.Full", workspace);
        Assert.Contains("<PlatformCommunityHomeComposer Context=\"@Context\"", workspace);
        Assert.Contains("CommunityWorkspaceSurfaceKind.Full", workspace);
        Assert.Contains("CommunityWorkspaceSurfaceKind.BoardManagement", workspace);
        Assert.Contains("CommunityWorkspaceSurfaceKind.LedgerDraft", workspace);
        Assert.Contains("CommunityWorkspaceSurfaceKind.Hub", workspace);

        var workspaceRouteScreen = File.ReadAllText(Path.Combine(
            componentDirectory,
            "CommunityWorkspaceScreen.razor"));
        Assert.Contains("WorkspaceSection=\"CommunityWorkspaceSurfaceKind.Hub\"", workspaceRouteScreen);
        Assert.DoesNotContain("<PlatformCommunityPostComposer", workspace);
        Assert.Contains("<PlatformCommunityPostComposer", composer);
        Assert.DoesNotContain("<PlatformCommunityHomeFeed", composer);
        Assert.DoesNotContain("<PlatformCommunityWorkPanel", composer);
        Assert.DoesNotContain("<PlatformCommunityBoardManagementPanel", composer);

        var composerFlow = File.ReadAllText(Path.Combine(
            componentDirectory,
            "PlatformCommunityHome.CommunityComposer.razor.cs"));
        var postCommands = File.ReadAllText(Path.Combine(
            componentDirectory,
            "PlatformCommunityHome.PostCommands.razor.cs"));
        Assert.Contains("Navigation.NavigateTo(EffectiveComposeCloseHref)", composerFlow);
        Assert.Contains("CommunityPageRoutes.PostDetailFor", postCommands);
    }

    [Fact]
    public void 전문원장표현과레이어카탈로그는_화면owner없이_계산하고_기존화면어댑터로연결한다()
    {
        var componentDirectory = FindComponentDirectory();
        var ledgerFacade = File.ReadAllText(Path.Combine(componentDirectory, "PlatformCommunityHome.LedgerStatePresentation.razor.cs"));
        var projection = File.ReadAllText(Path.Combine(componentDirectory, "PlatformCommunityLedgerStatePresentation.cs"));
        var catalog = File.ReadAllText(Path.Combine(componentDirectory, "PlatformCommunityDiagramCatalog.cs"));
        var state = File.ReadAllText(Path.Combine(componentDirectory, "PlatformCommunityHome.State.razor.cs"));
        var surface = File.ReadAllText(Path.Combine(componentDirectory, "PlatformCommunityHome.DiagramStageSurface.razor.cs"));

        Assert.Contains("PlatformCommunityLedgerStatePresentation.ResolveActiveNodeIndex", ledgerFacade);
        Assert.Contains("PlatformCommunityLedgerStatePresentation.FindNodeIndex", ledgerFacade);
        Assert.Contains("internal static class PlatformCommunityLedgerStatePresentation", projection);
        Assert.Contains("ContainsAny(stateLabel", projection);
        Assert.DoesNotContain("PlatformCommunityHome", projection);
        Assert.DoesNotContain("Navigation", projection);
        Assert.DoesNotContain("owner", projection);
        Assert.Contains("internal static class PlatformCommunityDiagramCatalog", catalog);
        Assert.DoesNotContain("PlatformCommunityHome", catalog);
        Assert.Contains("=> PlatformCommunityDiagramCatalog.All;", state);
        Assert.Contains("Layers => PlatformCommunityDiagramCatalog.All;", surface);
        Assert.Contains("public void Close() => owner.CloseDiagramSurface();", surface);
        Assert.Contains("owner.BuildDesktopDiagramPresentation", surface);
        Assert.Contains("owner.BuildMobileDiagramSteps", surface);
    }

    private static string FindComponentDirectory()
        => Path.Combine(
            FindRepositoryRoot(),
            "Ssalddel.Ui.Common",
            "Areas",
            "App",
            "Components",
            "Community");

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
