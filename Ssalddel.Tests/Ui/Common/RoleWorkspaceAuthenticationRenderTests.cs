using System.Net;
using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.HtmlRendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using MudBlazor.Services;
using Ssalddel.Contracts.Food;
using Ssalddel.Ui.Common.Areas.App.Models;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Tests.Ui.Common;

public sealed class RoleWorkspaceAuthenticationRenderTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 익명과_401은_로그인안내만_렌더하고_지도와_업무를_숨긴다(bool serverUnauthorized)
    {
        await using var fixture = new Fixture(serverUnauthorized);
        fixture.Adapter.StatusCode = serverUnauthorized ? 401 : null;

        var html = await fixture.RenderAsync();

        AssertAuthenticationOnly(html);
        Assert.Contains("/workspace-login/food-driver?returnUrl=%2Fworkspace%2Ffood-driver%3Fselected%3Dwork-a", html);
        Assert.Equal(serverUnauthorized, fixture.Model.IsAuthenticated);
        Assert.Equal(serverUnauthorized, fixture.Model.RequiresLogin);
        var initialHides = fixture.Map.Hides;
        await fixture.AfterRenderAsync();
        Assert.Equal(initialHides + 1, fixture.Map.Hides);
        Assert.Equal(0, fixture.Map.Renders);
        Assert.Equal(0, fixture.Adapter.Commands);
    }

    [Fact]
    public async Task 정상로그인에서는_기존지도와_현재업무를_렌더한다()
    {
        await using var fixture = new Fixture(true);
        var html = await fixture.RenderAsync();

        AssertWorkspaceOnly(html);
        var initialHides = fixture.Map.Hides;
        await fixture.AfterRenderAsync();
        Assert.Equal(1, fixture.Map.Renders);
        Assert.Equal(initialHides, fixture.Map.Hides);
        Assert.Equal(0, fixture.Adapter.Commands);
    }

    [Fact]
    public async Task 같은화면에서_로그아웃하면_지도를숨기고_로그인복원후_다시그린다()
    {
        await using var fixture = new Fixture(true);
        await fixture.RenderAsync();
        await fixture.AfterRenderAsync();
        var initialHides = fixture.Map.Hides;

        var signedOut = await fixture.SignOutAsync();
        AssertAuthenticationOnly(signedOut);
        await fixture.AfterRenderAsync();
        Assert.Equal(1, fixture.Map.Renders);
        Assert.Equal(initialHides + 1, fixture.Map.Hides);

        var restored = await fixture.RestoreAsync();
        AssertWorkspaceOnly(restored);
        await fixture.AfterRenderAsync();
        Assert.Equal(2, fixture.Map.Renders);
        Assert.Equal(initialHides + 1, fixture.Map.Hides);
        Assert.Equal(0, fixture.Adapter.Commands);
    }

    [Fact]
    public async Task 세션표시가남은_401도_이미그린지도를숨기고_재로그인후_복원한다()
    {
        await using var fixture = new Fixture(true);
        await fixture.RenderAsync();
        await fixture.AfterRenderAsync();
        var initialHides = fixture.Map.Hides;
        fixture.Adapter.StatusCode = 401;

        var expired = await fixture.RefreshAsync();
        Assert.True(fixture.Model.IsAuthenticated);
        Assert.True(fixture.Model.RequiresLogin);
        AssertAuthenticationOnly(expired);
        await fixture.AfterRenderAsync();
        Assert.Equal(initialHides + 1, fixture.Map.Hides);

        fixture.Adapter.StatusCode = null;
        AssertWorkspaceOnly(await fixture.RestoreAsync());
        await fixture.AfterRenderAsync();
        Assert.Equal(2, fixture.Map.Renders);
        Assert.Equal(0, fixture.Adapter.Commands);
    }

    [Fact]
    public async Task 초기로그인복원중에는_로그인버튼과_업무를_꺼내지않는다()
    {
        await using var fixture = new Fixture(false);
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Access.Initialize = (_, token) => pending.Task.WaitAsync(token);

        var loading = await fixture.BeginRenderAsync();
        Assert.Contains("로그인 상태를 확인하고 있습니다.", loading);
        Assert.DoesNotContain("/workspace-login/", loading);
        AssertNoWorkspace(loading);
        var initialHides = fixture.Map.Hides;
        await fixture.AfterRenderAsync();
        Assert.Equal(initialHides + 1, fixture.Map.Hides);

        fixture.Access.Initialize = (_, _) => Task.CompletedTask;
        pending.SetResult();
        var anonymous = await fixture.WaitForInitializationAsync();
        AssertAuthenticationOnly(anonymous);
    }

    [Fact]
    public async Task 금지403은_로그인오류로바꾸지않고_역할선택으로_안내한다()
    {
        await using var fixture = new Fixture(true);
        fixture.Adapter.StatusCode = 403;
        var html = await fixture.RenderAsync();

        Assert.Contains("역할 선택으로", html);
        Assert.Contains("역할 권한을 확인해 주세요.", html);
        Assert.DoesNotContain("로그인이 필요합니다", html);
        Assert.DoesNotContain("/workspace-login/", html);
        AssertNoWorkspace(html);
        var initialHides = fixture.Map.Hides;
        await fixture.AfterRenderAsync();
        Assert.Equal(initialHides + 1, fixture.Map.Hides);
    }

    private static void AssertAuthenticationOnly(string html)
    {
        Assert.Contains("로그인이 필요합니다", html);
        Assert.Matches(@"<a\b(?=[^>]*\bclass=""is-primary"")(?=[^>]*\bhref=""[^""]*/workspace-login/food-driver\?returnUrl=[^""]*"")[^>]*>로그인</a>", html);
        AssertNoWorkspace(html);
        Assert.DoesNotContain("새로고침", html);
        Assert.DoesNotContain("지도 다시 시도", html);
    }

    private static void AssertNoWorkspace(string html)
    {
        Assert.DoesNotContain("role-workspace-map-canvas", html);
        Assert.DoesNotContain("role-map-workspace__mode", html);
        Assert.DoesNotContain("role-map-workspace__settings", html);
        Assert.DoesNotContain("role-map-workspace__card\"", html);
        Assert.DoesNotContain("비공개 현재 배달", html);
        Assert.DoesNotContain("비공개 전달 주소", html);
        Assert.DoesNotContain("고객 전달 완료", html);
    }

    private static void AssertWorkspaceOnly(string html)
    {
        Assert.Contains("role-workspace-map-canvas", html);
        Assert.Contains("role-map-workspace__mode", html);
        Assert.Contains("role-map-workspace__settings", html);
        Assert.Contains("비공개 현재 배달", html);
        Assert.Contains("비공개 전달 주소", html);
        Assert.Contains("고객 전달 완료", html);
        Assert.DoesNotContain("로그인이 필요합니다", html);
        Assert.DoesNotContain("/workspace-login/", html);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly HtmlRenderer _renderer;
        private HtmlRootComponent _rendered;
        private RoleMapWorkspace? _screen;
        public Access Access { get; }
        public Adapter Adapter { get; } = new();
        public MapHost Map { get; } = new();
        public RoleWorkspaceViewModel Model { get; }

        public Fixture(bool authenticated)
        {
            Access = new(authenticated);
            Model = new([Adapter], Access, new RoleWorkspaceState());
            var services = new ServiceCollection();
            services.AddLogging(); services.AddMudServices();
            services.AddSingleton(Model);
            services.AddSingleton<NavigationManager, TestNavigation>();
            services.AddSingleton<INeighborhoodMapHost>(Map);
            services.AddSingleton<IJSRuntime, StaticRenderJs>();
            _provider = services.BuildServiceProvider();
            _renderer = new(_provider, _provider.GetRequiredService<ILoggerFactory>());
        }

        private ParameterView Parameters => ParameterView.FromDictionary(new Dictionary<string, object?>
        {
            [nameof(DisplayHost.Capture)] = (Action<RoleMapWorkspace>)(component => _screen = component)
        });
        private string Html() => WebUtility.HtmlDecode(_rendered.ToHtmlString());
        public Task<string> RenderAsync() => _renderer.Dispatcher.InvokeAsync(async () =>
        {
            _rendered = await _renderer.RenderComponentAsync<DisplayHost>(Parameters);
            return Html();
        });
        public Task<string> BeginRenderAsync() => _renderer.Dispatcher.InvokeAsync(() =>
        {
            _rendered = _renderer.BeginRenderingComponent<DisplayHost>(Parameters);
            return Html();
        });
        public async Task<string> WaitForInitializationAsync()
        {
            await _rendered.QuiescenceTask;
            return await _renderer.Dispatcher.InvokeAsync(Html);
        }
        public Task<string> RefreshAsync() => _renderer.Dispatcher.InvokeAsync(async () =>
        {
            await Model.RefreshAsync(); return Html();
        });
        public Task<string> SignOutAsync() => _renderer.Dispatcher.InvokeAsync(async () =>
        {
            await Model.SignOutAsync(); return Html();
        });
        public Task<string> RestoreAsync() => _renderer.Dispatcher.InvokeAsync(async () =>
        {
            Access.Change(true); await Model.RefreshAsync(); return Html();
        });
        // HtmlRenderer는 after-render를 자동 실행하지 않으므로 실제로 장착된 컴포넌트의
        // 해당 수명 메서드를 renderer dispatcher에서 호출해 지도 host 흐름도 검증합니다.
        public Task AfterRenderAsync() => _renderer.Dispatcher.InvokeAsync(async () =>
        {
            var method = typeof(RoleMapWorkspace).GetMethod("OnAfterRenderAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
            await (Task)method.Invoke(_screen!, [false])!;
        });
        public async ValueTask DisposeAsync()
        {
            await _renderer.DisposeAsync();
            await _provider.DisposeAsync();
        }
    }

    private sealed class DisplayHost : ComponentBase
    {
        [Parameter] public Action<RoleMapWorkspace>? Capture { get; set; }
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<RoleMapWorkspace>(0);
            builder.AddAttribute(1, nameof(RoleMapWorkspace.RoleKey), "food-driver");
            builder.AddAttribute(2, nameof(RoleMapWorkspace.SelectedId), "work-a");
            builder.AddAttribute(3, nameof(RoleMapWorkspace.LoginHref), "/workspace-login/food-driver");
            builder.AddComponentReferenceCapture(4, component => Capture?.Invoke((RoleMapWorkspace)component));
            builder.CloseComponent();
        }
    }

    private sealed class Adapter : IRoleWorkspaceAdapter
    {
        public string RoleKey => "food-driver";
        public int? StatusCode { get; set; }
        public int Commands { get; private set; }
        public Task<RoleWorkspaceSnapshot> LoadAsync(string? selectedId, CancellationToken cancellationToken)
        {
            if (StatusCode is { } status)
                throw new RoleWorkspaceAccessException(status, status == 401 ? "로그인한 뒤 업무를 확인해 주세요." : "역할 권한을 확인해 주세요.");
            // 익명에서도 이전 snapshot 후보를 반환해 UI가 세션을 우선하는지 확인합니다.
            return Task.FromResult(new RoleWorkspaceSnapshot(RoleKey,
                [new("work-a", "비공개 현재 배달", "전달 중", Subtitle: "비공개 전달 주소",
                    Actions: [new(음식배달가능행동Ids.기사전달완료, "고객 전달 완료")], IsCurrent: true)], SelectedId: "work-a"));
        }
        public Task PerformAsync(string itemId, string actionKey, Guid requestId, CancellationToken cancellationToken)
        { Commands++; return Task.CompletedTask; }
        public void Clear() { }
    }

    private sealed class Access(bool authenticated) : IRoleWorkspaceAccess
    {
        private RoleWorkspaceIdentity _identity = new(authenticated ? "owner-a" : null, 1, authenticated);
        public event Action? Changed;
        public Func<string, CancellationToken, Task> Initialize { get; set; } = (_, _) => Task.CompletedTask;
        public RoleWorkspaceIdentity GetIdentity(string roleKey) => _identity;
        public Task EnsureInitializedAsync(string roleKey, CancellationToken cancellationToken = default) => Initialize(roleKey, cancellationToken);
        public void Change(bool signedIn)
        { _identity = new(signedIn ? "owner-a" : null, _identity.Revision + 1, signedIn); Changed?.Invoke(); }
        public Task SignInAsync(string roleKey, string name, string password, CancellationToken cancellationToken = default)
        { Change(true); return Task.CompletedTask; }
        public Task SignOutAsync(string roleKey, CancellationToken cancellationToken = default)
        { Change(false); return Task.CompletedTask; }
    }

    private sealed class MapHost : INeighborhoodMapHost
    {
        public int Renders { get; private set; }
        public int Hides { get; private set; }
        public Task<NeighborhoodMapHostStatus> RenderAsync(string id, NeighborhoodMapRenderState state, Func<string, Task> selected, CancellationToken cancellationToken = default)
        { Renders++; return Task.FromResult(new NeighborhoodMapHostStatus("ready")); }
        public Task HideAsync(CancellationToken cancellationToken = default)
        { Hides++; return Task.CompletedTask; }
    }
    private sealed class StaticRenderJs : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => throw new NotSupportedException();
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => InvokeAsync<TValue>(identifier, args);
    }
    private sealed class TestNavigation : NavigationManager
    {
        public TestNavigation() => Initialize("http://localhost/", "http://localhost/workspace/food-driver?selected=work-a");
        protected override void NavigateToCore(string uri, bool forceLoad) { }
    }
}
