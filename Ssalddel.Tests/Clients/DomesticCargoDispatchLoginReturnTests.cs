using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.HtmlRendering.Infrastructure;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web.HtmlRendering;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using MudBlazor;
using MudBlazor.Services;
using SsalddelAdmin.Components.Pages;
using SsalddelAdmin.Services;

namespace Ssalddel.Tests.Clients;

public sealed class DomesticCargoDispatchLoginReturnTests
{
    private const string Canonical = "/admin/freight-delivery/dispatch-ai-review";

    [Fact]
    public async Task 조회401의실제로그인링크는현재허용된같은검토화면으로복귀한다()
    {
        await using var fixture = new Fixture();
        var html = await fixture.RenderAsync();
        var href = Assert.Single(Regex.Matches(html, "href=\"([^\"]+)\"").Cast<Match>()
            .Select(match => WebUtility.HtmlDecode(match.Groups[1].Value))
            .Where(value => value.StartsWith("/login?returnUrl=", StringComparison.Ordinal)));
        fixture.AssertOnlyReads(1);
        AssertReturn(href);

        var login = new AdminLogin { ReturnUrl = Uri.UnescapeDataString(href.Split("returnUrl=")[1]) };
        fixture.Inject(login);
        typeof(AdminLogin).GetMethod("OnInitialized", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(login, null);
        Assert.Equal(Canonical, fixture.Navigation.LastTarget);
        fixture.AssertOnlyReads(1); // 기존 세션 복귀는 새 로그인이나 업무 쓰기를 만들지 않습니다.
    }

    [Fact]
    public async Task 미로그인초기진입도같은현재주소를보존하고업무를조회하지않는다()
    {
        await using var fixture = new Fixture(authenticated: false);
        await fixture.RenderAsync();
        AssertReturn(Assert.IsType<string>(fixture.Navigation.LastTarget));
        fixture.AssertOnlyReads(0);
    }

    [Fact]
    public async Task 조회403은기존권한거부를유지하고로그인복귀로우회하지않는다()
    {
        await using var fixture = new Fixture(status: HttpStatusCode.Forbidden);
        var html = await fixture.RenderAsync();
        Assert.Contains("현재 계정에는 배차 검토 권한이 없습니다.", html);
        Assert.DoesNotContain("/login?returnUrl=", html);
        Assert.Null(fixture.Navigation.LastTarget);
        fixture.AssertOnlyReads(1);
    }

    private static void AssertReturn(string href)
    {
        Assert.StartsWith("/login?returnUrl=", href);
        var route = Uri.UnescapeDataString(href.Split("returnUrl=")[1]);
        Assert.Equal(Canonical, route);
        Assert.True(AdminV1NavigationPolicy.IsAllowedRoute(route));
        Assert.False(AdminV1NavigationPolicy.IsAllowedRoute("/dispatch/ai-review"));
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public HttpStub Http { get; }
        public NavigationStub Navigation { get; } = new();
        private readonly ServiceProvider services;
        private readonly StaticHtmlRenderer renderer;
        public Fixture(bool authenticated = true, HttpStatusCode status = HttpStatusCode.Unauthorized)
        {
            Http = new(status);
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AdminData:UseMemory"] = "false",
                ["AdminAuth:DevelopmentBootstrap:Enabled"] = authenticated.ToString(),
                ["AdminAuth:DevelopmentBootstrap:AccessToken"] = "synthetic-login-return-only",
                ["AdminAuth:DevelopmentBootstrap:Roles:0"] = "서버관리자"
            }).Build();
            var collection = new ServiceCollection(); collection.AddLogging(); collection.AddMudServices();
            collection.AddSingleton<IConfiguration>(config); collection.AddSingleton<IWebHostEnvironment, EnvironmentStub>();
            collection.AddSingleton<IJSRuntime, JavaScriptStub>(); collection.AddSingleton<NavigationManager>(Navigation);
            collection.AddSingleton<관리자인증세션Service>();
            collection.AddSingleton(new HttpClient(Http, false) { BaseAddress = new Uri("http://fixture.invalid/") });
            collection.AddSingleton<DomesticCargoDispatchAIReviewAdminService>(); collection.AddSingleton<관리자인증Service>();
            services = collection.BuildServiceProvider(); renderer = new(services, services.GetRequiredService<ILoggerFactory>());
        }
        public void Inject(object component)
        {
            foreach (var property in component.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(property => property.GetCustomAttribute<InjectAttribute>() is not null))
                property.SetValue(component, services.GetRequiredService(property.PropertyType));
        }
        public void AssertOnlyReads(int expected)
        {
            Assert.Equal(expected, Http.Attempts); Assert.Equal(expected, Http.Reads);
            Assert.Equal(0, Http.Posts); Assert.Equal(0, Http.UnexpectedRequests);
        }
        public Task<string> RenderAsync() => renderer.Dispatcher.InvokeAsync(async () =>
        {
            var providers = renderer.BeginRenderingComponent(new Providers(), ParameterView.Empty); await providers.QuiescenceTask;
            var page = new DomesticCargoDispatchAIReview(); Inject(page);
            var result = renderer.BeginRenderingComponent(page, ParameterView.Empty); await result.QuiescenceTask;
            return WebUtility.HtmlDecode(result.ToHtmlString());
        });
        public async ValueTask DisposeAsync() { await renderer.DisposeAsync(); await services.DisposeAsync(); Http.Dispose(); }
    }
    private sealed class HttpStub(HttpStatusCode status) : HttpMessageHandler
    {
        public int Attempts { get; private set; }
        public int Reads { get; private set; }
        public int Posts { get; private set; }
        public int UnexpectedRequests { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Attempts++; if (request.Method == HttpMethod.Post) Posts++;
            if (request.Method != HttpMethod.Get
                || request.RequestUri?.AbsoluteUri != "http://fixture.invalid/api/v1/admin/dispatch/domestic-cargo-ai-review"
                || request.Headers.Authorization?.Parameter != "synthetic-login-return-only")
            {
                UnexpectedRequests++; return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            }
            Reads++; return Task.FromResult(new HttpResponseMessage(status));
        }
    }
    private sealed class NavigationStub : NavigationManager
    {
        public string? LastTarget { get; private set; }
        public NavigationStub() => Initialize("http://fixture.invalid/", "http://fixture.invalid" + Canonical);
        protected override void NavigateToCore(string uri, bool forceLoad) => LastTarget = uri;
    }
    private sealed class JavaScriptStub : IJSRuntime
    {
        public ValueTask<T> InvokeAsync<T>(string identifier, object?[]? args) => ValueTask.FromResult(default(T)!);
        public ValueTask<T> InvokeAsync<T>(string identifier, CancellationToken token, object?[]? args) => InvokeAsync<T>(identifier, args);
    }
    private sealed class EnvironmentStub : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "SyntheticLoginReturnTests";
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
    private sealed class Providers : ComponentBase
    {
        protected override void BuildRenderTree(RenderTreeBuilder builder) { builder.OpenComponent<MudPopoverProvider>(0); builder.CloseComponent(); }
    }
}
