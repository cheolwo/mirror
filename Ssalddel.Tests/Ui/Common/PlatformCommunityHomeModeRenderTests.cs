using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.HtmlRendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using MudBlazor.Services;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Ui.Common.Areas.App.Components;
using Ssalddel.Ui.Common.Areas.App.Components.Community;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace Ssalddel.Tests.Ui.Common;

public sealed class PlatformCommunityHomeModeRenderTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("/community/boards?from=diagram")]
    public async Task 실제홈은_다이어그램왕복에서_초안과_하위조립수명을_보존한다(string? closeHref)
    {
        await using var fixture = new Fixture(closeHref);
        var publicBoard = fixture.Model.PublicBoard;
        var connectedTools = fixture.Model.ConnectedTools;
        var originalDraft = fixture.Model.Composer.Draft;
        var canvas = fixture.Model.DiagramWorkspace.Canvas;

        var homeHtml = await fixture.RenderAsync();
        var screen = fixture.Screen;
        Assert.Contains("platform-community-forum-header", homeHtml);
        Assert.DoesNotContain("platform-diagram-mode-stage", homeHtml);
        Assert.True(string.IsNullOrWhiteSpace(fixture.Model.PostList.오류메시지), fixture.Model.PostList.오류메시지);

        var diagramHtml = await fixture.ChangeAsync(() =>
        {
            fixture.Model.Composer.Draft.Title = "왕복 후 유지할 초안";
            fixture.Model.DiagramWorkspace.SelectedLedgerTemplateKey = CommunityLedgerTemplateKeys.WarehouseInbound;
            fixture.Model.DiagramWorkspace.SelectedCurrentLedgerId = "ledger-inbound-001";
            canvas.SelectedNodeTitle = "검수/이상";
            canvas.ZoomPercent = 125;
            fixture.Palette.SetDiagramMode(true);
        });
        Assert.Contains("platform-diagram-mode-stage", diagramHtml);
        Assert.Contains("원장 다이어그램 캔버스", diagramHtml);
        Assert.Contains("platform-ledger-flow-node--state-active", diagramHtml);
        Assert.Contains("검수/이상", diagramHtml);
        Assert.DoesNotContain("platform-community-forum-header", diagramHtml);

        var stage = fixture.Stage;
        Assert.Same(PlatformCommunityDiagramCatalog.All, stage.Layers);
        Assert.Same(PlatformDiagramFormNodeCatalog.All, stage.FormNodes);
        Assert.Same(canvas, stage.Canvas);
        Assert.Same(fixture.Model.WarehouseProxy, stage.WarehouseProxy);
        Assert.Same(fixture.Model.DiagramWorkspace.Chat, stage.DiagramChat);

        var returnedHtml = await fixture.ChangeAsync(stage.Close);
        Assert.False(fixture.Palette.IsDiagramMode);
        Assert.Contains("platform-community-forum-header", returnedHtml);
        Assert.DoesNotContain("platform-diagram-mode-stage", returnedHtml);
        Assert.Equal(closeHref, fixture.Navigation.LastUri);

        var reopenedHtml = await fixture.ChangeAsync(() => fixture.Palette.SetDiagramMode(true));
        Assert.Contains("platform-diagram-mode-stage", reopenedHtml);
        Assert.Same(screen, fixture.Screen);
        Assert.Same(stage, fixture.Stage);
        Assert.Same(publicBoard, fixture.Model.PublicBoard);
        Assert.Same(connectedTools, fixture.Model.ConnectedTools);
        Assert.Same(originalDraft, fixture.Model.Composer.Draft);
        Assert.Same(canvas, fixture.Model.DiagramWorkspace.Canvas);
        Assert.Equal("왕복 후 유지할 초안", originalDraft.Title);
        Assert.Equal("검수/이상", canvas.SelectedNodeTitle);
        Assert.Equal(125, canvas.ZoomPercent);
        Assert.Equal("ledger-inbound-001", fixture.Model.DiagramWorkspace.SelectedCurrentLedgerId);
        Assert.All(fixture.Http.Methods, method => Assert.Equal(HttpMethod.Get, method));
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly ServiceProvider provider;
        private readonly HtmlRenderer renderer;
        private HtmlRootComponent rendered;
        public PlatformCommunityHome Screen { get; private set; } = null!;
        public PlatformCommunityHomePageViewModel Model { get; }
        public PlatformDiagramPaletteStateService Palette { get; } = new();
        public TestNavigation Navigation { get; } = new();
        public LocalHttp Http { get; } = new();
        private readonly string? closeHref;

        public Fixture(string? closeHref)
        {
            this.closeHref = closeHref;
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddMudServices();
            services.AddSingleton<IJSRuntime, StaticJs>();
            services.AddSingleton<NavigationManager>(Navigation);
            services.AddSingleton<IPlatformHomeWorkspaceNavigationResolver, UnsupportedPlatformHomeWorkspaceNavigationResolver>();
            services.AddSingleton<IPlatformCommunityNodeNavigationResolver, UnsupportedPlatformCommunityNodeNavigationResolver>();
            var httpClient = new HttpClient(Http) { BaseAddress = new Uri("http://localhost/") };
            var service = new PlatformCommunityService(httpClient, new SsalddelProtectedApiClient(httpClient, null!, new EmptyTokenProvider()));
            services.AddSingleton(service);
            services.AddSingleton(Palette);
            services.AddSingleton<PlatformHomeModeStateService>();
            services.AddSingleton<PlatformCommunityDecorationStateService>();
            services.AddSingleton<PlatformCommunityPostDraftStateService>();
            services.AddSingleton<PlatformCommunityHomePageViewModel>(sp => new(
                new PlatformCommunityPublicBoardViewModel(
                    new CommunityPostComposerViewModel(service, new EmptyDraftStore()),
                    new CommunityPostListPageViewModel(service),
                    new PlatformCommunityHomeShellViewModel(),
                    new PlatformCommunityBoardWorkspaceViewModel(service),
                    new PlatformCommunityPostEngagementViewModel(service),
                    new PlatformCommunityLedgerPickerViewModel(service)),
                new PlatformCommunityConnectedToolsViewModel(
                    new YouTubeFoodCommunityDiscoveryViewModel(new YouTubeFoodCommunityDiscoveryService(httpClient, new EmptyTokenProvider())),
                    new PlatformCommunityDiagramWorkspaceViewModel(),
                    new PlatformCommunityWishFlowViewModel(),
                    new CommunityAuthoringEvidenceChartViewModel(),
                    new PlatformCommunityWarehouseProxyViewModel(sp))));
            provider = services.BuildServiceProvider();
            Model = provider.GetRequiredService<PlatformCommunityHomePageViewModel>();
            renderer = new(provider, provider.GetRequiredService<ILoggerFactory>());
        }

        public PlatformCommunityHome.DiagramStageSurface Stage
            => (PlatformCommunityHome.DiagramStageSurface)typeof(PlatformCommunityHome)
                .GetProperty("DiagramStage", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Screen)!;

        public Task<string> RenderAsync() => renderer.Dispatcher.InvokeAsync(async () =>
        {
            await renderer.RenderComponentAsync<RoleAppProviders>();
            rendered = await renderer.RenderComponentAsync<DisplayHost>(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(DisplayHost.Capture)] = (Action<PlatformCommunityHome>)(screen => Screen = screen),
                [nameof(DisplayHost.CloseHref)] = closeHref
            }));
            return Html();
        });

        public Task<string> ChangeAsync(Action change) => renderer.Dispatcher.InvokeAsync(() =>
        {
            change();
            return Html();
        });

        private string Html() => WebUtility.HtmlDecode(rendered.ToHtmlString());

        public async ValueTask DisposeAsync()
        {
            await renderer.DisposeAsync();
            await provider.DisposeAsync();
        }
    }

    private sealed class DisplayHost : ComponentBase
    {
        [Parameter] public Action<PlatformCommunityHome>? Capture { get; set; }
        [Parameter] public string? CloseHref { get; set; }
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<PlatformCommunityHome>(0);
            builder.AddAttribute(1, nameof(PlatformCommunityHome.CommunityFeedOnly), true);
            builder.AddAttribute(2, nameof(PlatformCommunityHome.UseClassicForumLayout), true);
            builder.AddAttribute(3, nameof(PlatformCommunityHome.DiagramCloseHref), CloseHref);
            builder.AddComponentReferenceCapture(4, component => Capture?.Invoke((PlatformCommunityHome)component));
            builder.CloseComponent();
        }
    }

    private sealed class LocalHttp : HttpMessageHandler
    {
        public List<HttpMethod> Methods { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Methods.Add(request.Method);
            var response = request.RequestUri!.AbsolutePath.EndsWith("/posts", StringComparison.Ordinal)
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new PlatformCommunityPostListResponse()) }
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new PlatformCommunityBoardListResponse()) };
            response.RequestMessage = request;
            return Task.FromResult(response);
        }
    }

    private sealed class EmptyTokenProvider : ISsalddelAccessTokenProvider
    {
        public string? AccessToken => null;
    }

    private sealed class EmptyDraftStore : ICommunityPostComposerDraftStore
    {
        public Task<CommunityPostComposerSnapshot?> LoadAsync(string appKey, CancellationToken cancellationToken = default)
            => Task.FromResult<CommunityPostComposerSnapshot?>(null);
        public Task SaveAsync(string appKey, CommunityPostComposerSnapshot snapshot, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
        public Task ClearAsync(string appKey, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class StaticJs : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => ValueTask.FromResult(default(TValue)!);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            => ValueTask.FromResult(default(TValue)!);
    }

    private sealed class TestNavigation : NavigationManager
    {
        public string? LastUri { get; private set; }
        public TestNavigation() => Initialize("http://localhost/", "http://localhost/community");
        protected override void NavigateToCore(string uri, bool forceLoad) => LastUri = uri;
    }
}
