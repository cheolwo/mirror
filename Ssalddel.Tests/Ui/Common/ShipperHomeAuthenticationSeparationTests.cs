using System.Net;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using MudBlazor.Services;
using Ssalddel.Ui.Common.Areas.App.Components.Auth;
using Ssalddel.Ui.Common.Areas.App.Components.Shipper;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace Ssalddel.Tests.Ui.Common;

public sealed class ShipperHomeAuthenticationSeparationTests
{
    [Fact]
    public async Task 명시적화주인증모드는_로그인과공개이동만표시하고_업무를표시하지않는다()
    {
        await using var fixture = new RenderFixture(Snapshot(false));
        var html = await fixture.RenderAsync();

        AssertAuthenticationOnly(html);
        Assert.Contains("공개 커뮤니티", html);
        Assert.DoesNotContain("검증 화주 업무 홈", html);
        Assert.DoesNotContain("운송 의뢰 작성", html);
    }

    [Fact]
    public async Task 로그인성공후에는_같은화주홈의업무와기존목적지를표시한다()
    {
        await using var fixture = new RenderFixture(Snapshot(true));
        fixture.State.SessionAuthenticated = true;

        var html = await fixture.RenderAsync();

        AssertWorkspaceOnly(html);
        Assert.Contains("합성 업무 진입 안내", html);
        Assert.Contains("합성 전문 도구", html);
        Assert.Contains("/shipper/request", html);
        Assert.Contains("검증 화주 업무 홈", html);
    }

    [Fact]
    public async Task 호스트세션이로그아웃이면_이전인증업무스냅샷이남아도로그인만표시한다()
    {
        await using var fixture = new RenderFixture(Snapshot(true));
        fixture.State.SessionAuthenticated = false;

        var html = await fixture.RenderAsync();

        AssertAuthenticationOnly(html);
        Assert.DoesNotContain("합성 화주", html);
        Assert.DoesNotContain("FOOD-SYNTHETIC-REQUEST", html);
    }

    [Fact]
    public async Task 같은컴포넌트에서_로그인후업무로복귀하고_로그아웃즉시인증모드로돌아간다()
    {
        await using var fixture = new RenderFixture(Snapshot(false));
        AssertAuthenticationOnly(await fixture.RenderAsync());

        fixture.Client.Current = Snapshot(true);
        var loggedIn = await fixture.SetSessionAndRefreshAsync(true);
        AssertWorkspaceOnly(loggedIn);
        Assert.Contains("합성 업무 진입 안내", loggedIn);

        // The previously read server snapshot deliberately remains authenticated.
        // The host session must hide it before the next dashboard request finishes.
        var loggedOut = await fixture.SetSessionAsync(false);
        AssertAuthenticationOnly(loggedOut);
    }

    [Fact]
    public async Task 인증모드는_이전업무경고와조회오류를숨기고_로그인자체오류는유지한다()
    {
        const string workWarning = "합성 이전 의뢰 조회 경고";
        const string workFailure = "합성 업무 조회 실패";
        const string authenticationError = "합성 인증 실패";
        await using var fixture = new RenderFixture(Snapshot(true) with { Warnings = [workWarning] });
        fixture.State.SessionAuthenticated = false;
        fixture.State.CredentialError = authenticationError;

        var anonymousHtml = await fixture.RenderAsync();
        AssertAuthenticationOnly(anonymousHtml);
        Assert.Contains(authenticationError, anonymousHtml);
        Assert.DoesNotContain(workWarning, anonymousHtml);

        fixture.Client.Failure = new IOException(workFailure);
        var failedLookupHtml = await fixture.RefreshAsync();
        AssertAuthenticationOnly(failedLookupHtml);
        Assert.Contains(authenticationError, failedLookupHtml);
        Assert.DoesNotContain(workWarning, failedLookupHtml);
        Assert.DoesNotContain(workFailure, failedLookupHtml);

        fixture.Client.Failure = null;
        var authenticatedHtml = await fixture.SetSessionAndRefreshAsync(true);
        AssertWorkspaceOnly(authenticatedHtml);
        Assert.Contains(workWarning, authenticatedHtml);
        Assert.DoesNotContain(authenticationError, authenticatedHtml);
    }

    [Fact]
    public async Task 인증모드의초기대기는_업무조회설명없이로그인준비만표시한다()
    {
        var completion = new TaskCompletionSource<ShipperHomeDashboardSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var fixture = new RenderFixture(Snapshot(false));
        fixture.Client.PendingLoad = completion.Task;

        var pendingHtml = await fixture.BeginRenderAsync();
        Assert.Contains("로그인 화면을 준비하고 있습니다.", pendingHtml);
        Assert.DoesNotContain("업무 경계를 확인하고 있습니다.", pendingHtml);
        Assert.DoesNotContain("로그인 상태와 0.0 기능 플래그", pendingHtml);
        Assert.DoesNotContain("shipper-home__metrics", pendingHtml);
        Assert.DoesNotContain("shipper-home__workflows", pendingHtml);
        Assert.DoesNotContain("합성 업무 진입 안내", pendingHtml);

        completion.SetResult(Snapshot(false));
        var completedHtml = await fixture.WaitForInitializationAsync();
        AssertAuthenticationOnly(completedHtml);
    }

    [Fact]
    public async Task 기본공개웹소비자는_익명업무설명과기존로그인이동을유지한다()
    {
        await using var fixture = new RenderFixture(Snapshot(false));
        fixture.State.SeparateAuthentication = false;
        fixture.State.CredentialsProvided = false;

        var html = await fixture.RenderAsync();

        Assert.Contains("shipper-home__workflows", html);
        Assert.Contains("shipper-home__metrics", html);
        Assert.Contains("/login?returnUrl=%2Fshipper%2Fworkspace", html);
        Assert.Contains("로그인·선택 회원가입", html);
        Assert.DoesNotContain("type=\"password\"", html);
    }

    [Fact]
    public async Task 익명credential내용이없으면_분리옵션만으로공개허브를막지않는다()
    {
        await using var fixture = new RenderFixture(Snapshot(false));
        fixture.State.CredentialsProvided = false;

        var html = await fixture.RenderAsync();

        Assert.Contains("shipper-home__workflows", html);
        Assert.Contains("공개 커뮤니티", html);
        Assert.DoesNotContain("data-display-mode=\"authentication\"", html);
    }

    private static void AssertAuthenticationOnly(string html)
    {
        Assert.Contains("data-display-mode=\"authentication\"", html);
        Assert.Contains("type=\"password\"", html);
        Assert.Contains("화주 로그인", html);
        Assert.DoesNotContain("shipper-home__metrics", html);
        Assert.DoesNotContain("shipper-home__workflows", html);
        Assert.DoesNotContain("shipper-home__latest", html);
        Assert.DoesNotContain("합성 업무 진입 안내", html);
        Assert.DoesNotContain("합성 전문 도구", html);
    }

    private static void AssertWorkspaceOnly(string html)
    {
        Assert.Contains("data-display-mode=\"workspace\"", html);
        Assert.Contains("shipper-home__metrics", html);
        Assert.Contains("shipper-home__workflows", html);
        Assert.DoesNotContain("type=\"password\"", html);
    }

    private static ShipperHomeDashboardSnapshot Snapshot(bool authenticated)
        => new(
            authenticated,
            authenticated ? "합성 화주" : "방문자",
            true,
            new Dictionary<string, bool>
            {
                [ShipperHomeFeatureKeys.DomesticTransport] = true,
                [ShipperHomeFeatureKeys.WarehouseFulfillment] = true
            },
            authenticated
                ? new("FOOD-SYNTHETIC-REQUEST", "접수", "결제대기", "배차대기", DateTime.UnixEpoch)
                : null,
            authenticated ? 7 : 0,
            authenticated ? 3 : 0,
            authenticated ? 5 : 0,
            []);

    // Actual shared Razor and the existing credential component are rendered.
    // The synthetic dashboard and JS/navigation adapters replace external boundaries.
    private sealed class RenderFixture : IAsyncDisposable
    {
        private readonly ServiceProvider provider;
        private readonly HtmlRenderer renderer;
        private Microsoft.AspNetCore.Components.Web.HtmlRendering.HtmlRootComponent rendered;

        public DashboardClient Client { get; }
        public DisplayState State { get; } = new();

        public RenderFixture(ShipperHomeDashboardSnapshot snapshot)
        {
            Client = new() { Current = snapshot };
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddMudServices();
            services.AddSingleton<IJSRuntime, NoopJsRuntime>();
            services.AddSingleton<NavigationManager, TestNavigationManager>();
            services.AddSingleton(new ShipperHomePageViewModel(Client));
            services.AddSingleton(State);
            provider = services.BuildServiceProvider();
            renderer = new(provider, provider.GetRequiredService<ILoggerFactory>());
        }

        public Task<string> RenderAsync()
            => renderer.Dispatcher.InvokeAsync(async () =>
            {
                rendered = await renderer.RenderComponentAsync<DisplayHost>();
                return Html();
            });

        public Task<string> BeginRenderAsync()
            => renderer.Dispatcher.InvokeAsync(() =>
            {
                rendered = renderer.BeginRenderingComponent<DisplayHost>();
                return Html();
            });

        public async Task<string> WaitForInitializationAsync()
        {
            await rendered.QuiescenceTask;
            return await renderer.Dispatcher.InvokeAsync(Html);
        }

        public Task<string> RefreshAsync()
            => renderer.Dispatcher.InvokeAsync(async () =>
            {
                await State.Screen!.RefreshAsync();
                return Html();
            });

        public Task<string> SetSessionAndRefreshAsync(bool authenticated)
            => renderer.Dispatcher.InvokeAsync(async () =>
            {
                State.SessionAuthenticated = authenticated;
                State.Host!.Refresh();
                await State.Screen!.RefreshAsync();
                return Html();
            });

        public Task<string> SetSessionAsync(bool authenticated)
            => renderer.Dispatcher.InvokeAsync(() =>
            {
                State.SessionAuthenticated = authenticated;
                State.Host!.Refresh();
                return Html();
            });

        private string Html() => WebUtility.HtmlDecode(rendered.ToHtmlString());

        public async ValueTask DisposeAsync()
        {
            await renderer.DisposeAsync();
            await provider.DisposeAsync();
        }
    }

    private sealed class DisplayState
    {
        public bool SeparateAuthentication { get; set; } = true;
        public bool? SessionAuthenticated { get; set; }
        public bool CredentialsProvided { get; set; } = true;
        public string? CredentialError { get; set; }
        public DisplayHost? Host { get; set; }
        public ShipperHomeScreen? Screen { get; set; }
    }

    private sealed class DisplayHost : ComponentBase
    {
        [Inject] public DisplayState State { get; set; } = null!;

        protected override void OnInitialized() => State.Host = this;

        public void Refresh() => StateHasChanged();

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<ShipperHomeScreen>(0);
            builder.AddAttribute(1, nameof(ShipperHomeScreen.HomeTitle), "검증 화주 업무 홈");
            builder.AddAttribute(2, nameof(ShipperHomeScreen.SeparateAnonymousAuthentication), State.SeparateAuthentication);
            builder.AddAttribute(3, nameof(ShipperHomeScreen.SessionAuthenticated), State.SessionAuthenticated);
            builder.AddAttribute(4, nameof(ShipperHomeScreen.LoginHref), "/login?returnUrl=%2Fshipper%2Fworkspace");
            builder.AddAttribute(5, nameof(ShipperHomeScreen.TransportHref), "/shipper/request");
            builder.AddAttribute(6, nameof(ShipperHomeScreen.AuthenticatedIntro), (RenderFragment)(content => content.AddMarkupContent(0, "<div>합성 업무 진입 안내</div>")));
            builder.AddAttribute(7, nameof(ShipperHomeScreen.AdditionalTools), (RenderFragment)(content => content.AddMarkupContent(0, "<div>합성 전문 도구</div>")));
            if (State.CredentialsProvided)
            {
                builder.AddAttribute(8, nameof(ShipperHomeScreen.AnonymousContent), (RenderFragment)(content =>
                {
                    content.OpenComponent<Ssalddel공통로그인Panel>(0);
                    content.AddAttribute(1, nameof(Ssalddel공통로그인Panel.제목), "화주 로그인");
                    content.AddAttribute(2, nameof(Ssalddel공통로그인Panel.소셜로그인표시), false);
                    content.AddAttribute(3, nameof(Ssalddel공통로그인Panel.상태메시지), State.CredentialError);
                    content.CloseComponent();
                }));
            }

            builder.AddComponentReferenceCapture(9, component => State.Screen = (ShipperHomeScreen)component);
            builder.CloseComponent();
        }
    }

    private sealed class DashboardClient : IShipperHomeDashboardClient
    {
        public required ShipperHomeDashboardSnapshot Current { get; set; }
        public Task<ShipperHomeDashboardSnapshot>? PendingLoad { get; set; }
        public Exception? Failure { get; set; }
        public Task<ShipperHomeDashboardSnapshot> LoadAsync(CancellationToken cancellationToken = default)
            => Failure is not null
                ? Task.FromException<ShipperHomeDashboardSnapshot>(Failure)
                : PendingLoad ?? Task.FromResult(Current);
    }

    private sealed class NoopJsRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => ValueTask.FromResult(default(TValue)!);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            => ValueTask.FromResult(default(TValue)!);
    }

    private sealed class TestNavigationManager : NavigationManager
    {
        public TestNavigationManager() => Initialize("http://localhost/", "http://localhost/shipper");
        protected override void NavigateToCore(string uri, bool forceLoad) { }
    }
}
