using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Ui.Common.Areas.App.Components.Community.Exchange;
using Ssalddel.Ui.Common.Areas.App.Models;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace Ssalddel.Tests.Ui.Common;

public sealed class NeighborhoodMapWorkToolsR22RenderTests
{
    [Fact]
    public async Task 선택업무가_없으면_등록은_바로보이고_독립도구는_기본접힘으로_기존화면에_연결한다()
    {
        var html = await RenderAsync();
        var tools = Tools(html);
        Assert.DoesNotContain(" open", tools);
        Assert.Contains("data-neighborhood-start", html);
        Assert.DoesNotContain("data-neighborhood-start", tools);
        Assert.Contains("주변 보기", html); Assert.Contains("내 할 일", html); Assert.Contains("표시 설정", html);
        Assert.Contains("교류 글 올리기", tools); Assert.Contains("보관공간 등록", tools); Assert.Contains("물건 배송 요청", tools);
        Assert.Contains("/roles/01/community/exchange/write?returnUrl=", tools);
        Assert.Contains("/roles/01/community/exchange/spaces/new?returnUrl=", tools);
        Assert.Contains("/roles/01/community/exchange/deliveries/new?returnUrl=", tools);
        Assert.Contains("/roles/01/community/exchange/deliveries?returnUrl=", tools);
        Assert.Contains("/roles/01/community/exchange/spaces?mine=true&returnUrl=", tools);
        Assert.DoesNotContain("<form", tools);
    }

    [Fact]
    public async Task 진행중협업의_필수행동과_취소는_도구밖에_남고_모든독립도구는_같은선택문맥으로_복귀한다()
    {
        var html = await RenderAsync("work", "work-one");
        var tools = Tools(html);
        var home = NeighborhoodMapNavigation.Href(["offer"], "list", "seoul-a", "work", "work-one");
        Assert.DoesNotContain("data-neighborhood-start", html);
        Assert.Contains("물품 나눔 1", html); Assert.Contains("완료 확인 요청", html); Assert.Contains("협업 취소", html);
        Assert.Matches("<button[^>]*data-neighborhood-recovery[^>]*>협업 취소</button>", html);
        Assert.DoesNotContain("완료 확인 요청", tools); Assert.DoesNotContain("협업 취소", tools);
        Assert.Contains("목록으로", html);
        Assert.Equal(5, Regex.Matches(tools, "returnUrl=" + Regex.Escape(Uri.EscapeDataString(home))).Count);
        Assert.Contains("&region=seoul-a", tools);
    }

    [Fact]
    public async Task 명시적등록패널_URL은_기존등록선택과_지도로의복귀를_유지한다()
    {
        var html = await RenderAsync("register");
        Assert.Contains("무엇을 등록할까요?", html);
        Assert.Contains("data-neighborhood-start", html);
        Assert.Contains(Uri.EscapeDataString(NeighborhoodMapNavigation.Href(["offer"], "list", "seoul-a", "register")), html);
    }

    [Fact]
    public async Task 로그인과_조회실패복구는_접힌업무도구밖에_보인다()
    {
        var anonymous = await RenderAsync("work", "work-one", authenticated: false);
        Assert.Contains("로그인이 필요해요", anonymous); Assert.Contains("로그인하기", anonymous);
        Assert.Contains("내 배송은", anonymous); Assert.Contains("href=\"/login\"", anonymous);
        Assert.DoesNotContain("로그인", Tools(anonymous));
        var failed = await RenderAsync("work", "work-one", failWork: true);
        Assert.Contains("새로고침", failed); Assert.DoesNotContain("새로고침", Tools(failed));
    }

    private static string Tools(string html)
        => Regex.Match(html, "<details class=\"neighborhood-map-work-tools\"[^>]*>.*?</details>", RegexOptions.Singleline).Value;

    private static async Task<string> RenderAsync(string? panel = null, string? target = null, bool authenticated = true, bool failWork = false)
    {
        var fixture = new Fixture(authenticated, failWork);
        var services = new ServiceCollection(); services.AddLogging();
        services.AddCommerceUiFixture();
        services.AddSingleton<NavigationManager, Navigation>();
        services.AddSingleton<INeighborhoodMapHost, MapHost>();
        services.AddSingleton<ISsalddel현재사용자Context>(fixture);
        services.AddSingleton<INeighborhoodExchangeMapClient>(fixture);
        services.AddSingleton<INeighborhoodMapPreferenceStore>(fixture);
        services.AddTransient<NeighborhoodExchangeMapViewModel>();
        services.AddSingleton<INeighborhoodCollaborationClient>(fixture);
        services.AddSingleton<NeighborhoodCollaborationDraftSession>();
        services.AddTransient<NeighborhoodCollaborationQueryViewModel>();
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<NeighborhoodExchangeMapPage>(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(NeighborhoodExchangeMapPage.Layers)] = "offer", [nameof(NeighborhoodExchangeMapPage.View)] = "list",
                [nameof(NeighborhoodExchangeMapPage.Region)] = "seoul-a", [nameof(NeighborhoodExchangeMapPage.Panel)] = panel,
                [nameof(NeighborhoodExchangeMapPage.Target)] = target
            }));
            return WebUtility.HtmlDecode(output.ToHtmlString());
        });
        Assert.Equal(0, fixture.Writes);
        return html;
    }
    private sealed class Navigation : NavigationManager
    {
        public Navigation() => Initialize("http://localhost/roles/01/", "http://localhost/roles/01/community/map");
        protected override void NavigateToCore(string uri, bool forceLoad) { }
    }
    private sealed class MapHost : INeighborhoodMapHost
    {
        public Task<NeighborhoodMapHostStatus> RenderAsync(string elementId, NeighborhoodMapRenderState state, Func<string, Task> selected, CancellationToken ct) => Task.FromResult(NeighborhoodMapHostStatus.Unavailable);
        public Task HideAsync(CancellationToken ct = default) => Task.CompletedTask;
    }
    private sealed class Fixture(bool authenticated, bool failWork) : ISsalddel현재사용자Context, INeighborhoodExchangeMapClient, INeighborhoodMapPreferenceStore, INeighborhoodCollaborationClient
    {
        public int Writes { get; private set; }
        public 현재사용자Snapshot 현재사용자 => authenticated ? new("owner", "이웃", []) : 현재사용자Snapshot.익명;
        public Task<NeighborhoodExchangeRegionListResponse> RegionsAsync(CancellationToken ct) => Task.FromResult(new NeighborhoodExchangeRegionListResponse { Items = [new() { RegionKey = "seoul-a", DisplayName = "동네 1", Latitude = 37.580, Longitude = 127.087 }] });
        public Task<NeighborhoodExchangeMapResponse> MapAsync(string? intent, CancellationToken ct) => Task.FromResult(new NeighborhoodExchangeMapResponse());
        public Task<PlatformCommunityPostListResponse> PostsAsync(string? region, string? intent, int page, CancellationToken ct) => Task.FromResult(new PlatformCommunityPostListResponse());
        public Task<NeighborhoodMapDeliveryPage> MineAsync(int page, CancellationToken ct) => Task.FromResult(new NeighborhoodMapDeliveryPage([], false));
        public Task<NeighborhoodDeliveryMapResponse?> DeliveryMapAsync(string id, bool route, CancellationToken ct) => Task.FromResult<NeighborhoodDeliveryMapResponse?>(null);
        public Task<NeighborhoodMapPreferences?> LoadAsync(string? owner, CancellationToken ct = default) => Task.FromResult<NeighborhoodMapPreferences?>(null);
        public Task SaveAsync(string? owner, NeighborhoodMapPreferences value, CancellationToken ct = default) => Task.CompletedTask;
        public Task<NeighborhoodCollaborationListResponse> MineAsync(string scope, int page, CancellationToken ct) => Task.FromResult(new NeighborhoodCollaborationListResponse());
        public Task<NeighborhoodCollaborationResponse?> ReadAsync(string id, CancellationToken ct) => failWork ? Task.FromException<NeighborhoodCollaborationResponse?>(new InvalidOperationException()) : Task.FromResult<NeighborhoodCollaborationResponse?>(new()
        {
            StableId = id, SourceTitle = "물품 나눔 1", StatusCode = NeighborhoodCollaborationStates.InProgress, Kind = NeighborhoodCollaborationKinds.Goods,
            AllowedActions = [NeighborhoodCollaborationActions.ProposeCompletion, NeighborhoodCollaborationActions.Cancel], IsRequester = true
        });
        private Task<NeighborhoodCollaborationResponse?> Write() { Writes++; throw new InvalidOperationException("Unexpected write"); }
        public Task<NeighborhoodCollaborationResponse?> CreateAsync(NeighborhoodCollaborationCreateRequest request, CancellationToken ct) => Write();
        public Task<NeighborhoodCollaborationResponse?> CommandAsync(string id, NeighborhoodCollaborationCommandRequest request, CancellationToken ct) => Write();
        public Task<NeighborhoodCollaborationResponse?> ReceiptAsync(Guid request, string? id, CancellationToken ct) => Write();
        public Task<IReadOnlyList<NeighborhoodCollaborationOpportunityResponse>> OpportunitiesAsync(long id, CancellationToken ct) => Task.FromResult<IReadOnlyList<NeighborhoodCollaborationOpportunityResponse>>([]);
        public Task<IReadOnlyList<NeighborhoodCollaborationPublicHistoryResponse>> PublicHistoryAsync(long id, CancellationToken ct) => Task.FromResult<IReadOnlyList<NeighborhoodCollaborationPublicHistoryResponse>>([]);
    }
}
