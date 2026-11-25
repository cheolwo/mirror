using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using MudBlazor;
using MudBlazor.Services;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.WebApp.Pages;

namespace Ssalddel.Tests.Clients;

/// <summary>실제 페이지의 로그인·복귀 링크 출력입니다. 라우터 바인딩·브라우저 뒤로 가기·실제 로그인과 구분합니다.</summary>
public sealed class WarehouseOutboundLoginReturnTests
{
    [Theory]
    [InlineData("http://fixture.invalid/", "warehouse/general/outbound-plan-review")]
    [InlineData("http://fixture.invalid/roles/05/", "warehouse/general/outbound-plan-review")]
    [InlineData("http://fixture.invalid/roles/05/", "work/outbound/plans")]
    public async Task 출고묶음의기존로그인목적지는유지하고실제로그인화면에서원래업무조회문맥으로복귀시킨다(string appBase, string path)
    {
        const string key = "picking:묶음/A?x=1&next=2+조각";
        var relative = path + "?stage=picking&warehouseId=34"
            + "&periodStartUtc=2026-11-07T00%3A00%3A00.0000000Z&periodEndUtc=2026-11-07T01%3A00%3A00.0000000Z"
            + "&batchPage=2&batchKey=" + Uri.EscapeDataString(key) + "&memberPage=1&planPage=3&outboundPlanId=71";
        var original = new Uri(new Uri(appBase), relative);
        await using var fixture = await Fixture.CreateAsync(appBase, original.AbsoluteUri);

        var warehouse = await fixture.RenderAsync<WarehouseOutboundPlanReviewPage>();
        Assert.Contains("출고예정 검토에는 로그인이 필요", WebUtility.HtmlDecode(warehouse));
        var login = new Uri(original, Link(warehouse, "로그인"));

        Assert.Equal("/login", login.AbsolutePath);
        Assert.Equal(original.Authority, login.Authority);
        var returnUrl = ReturnParameter(login);
        Assert.Equal(original.PathAndQuery, returnUrl);
        Assert.Contains("outboundPlanId=71", returnUrl); Assert.Contains("memberPage=1", returnUrl);
        Assert.Equal(key, QueryValue(new Uri(original.GetLeftPart(UriPartial.Authority) + returnUrl), "batchKey"));

        // 분리 배포의 루트 /login은 역할01로 이동한다. 05에 새 로그인 경로가 있다고 가정하지 않는다.
        var loginBase = new Uri(appBase).AbsolutePath == "/roles/05/" ? "http://fixture.invalid/roles/01/" : "http://fixture.invalid/";
        var actualLogin = new Uri(new Uri(loginBase), "login" + login.Query);
        await using var loginFixture = await Fixture.CreateAsync(loginBase, actualLogin.AbsoluteUri);
        var loginPage = await loginFixture.RenderAsync<LoginPage>(new() { [nameof(LoginPage.ReturnUrl)] = returnUrl });
        Assert.Equal(original.AbsoluteUri, new Uri(actualLogin, Link(loginPage, "이전 화면으로")).AbsoluteUri);
        Assert.Equal(0, fixture.Http.Calls); Assert.Equal(0, loginFixture.Http.Calls);
    }

    [Theory]
    [InlineData("http://fixture.invalid/", "/warehouse/general/outbound-plan-review?stage=packing&outboundPlanId=71", "http://fixture.invalid/warehouse/general/outbound-plan-review?stage=packing&outboundPlanId=71")]
    [InlineData("http://fixture.invalid/roles/05/", "/roles/05/warehouse/general/outbound-plan-review?stage=packing&outboundPlanId=71", "http://fixture.invalid/roles/05/warehouse/general/outbound-plan-review?stage=packing&outboundPlanId=71")]
    [InlineData("http://fixture.invalid/roles/05/", "/warehouse/general/outbound-plan-review?stage=packing&outboundPlanId=71", "http://fixture.invalid/roles/05/warehouse/general/outbound-plan-review?stage=packing&outboundPlanId=71")]
    public async Task 실제로그인복귀검사는기존앱절대경로와공통경로해석을그대로유지한다(string appBase, string returnUrl, string expected)
    {
        await using var fixture = await Fixture.CreateAsync(appBase, new Uri(new Uri(appBase), "login").AbsoluteUri);
        var html = await fixture.RenderAsync<LoginPage>(new() { [nameof(LoginPage.ReturnUrl)] = returnUrl });
        Assert.Equal(expected, Link(html, "이전 화면으로")); Assert.Equal(0, fixture.Http.Calls);
    }

    [Theory]
    [InlineData("/roles/05/warehouse/general/outbound-plan-review?stage=packing&warehouseId=34&batchKey=packing%3Atask%3A33&memberPage=1&outboundPlanId=71")]
    [InlineData("/roles/05/work/outbound/plans?stage=picking&warehouseId=34&batchKey=picking%3Atask%3A33&memberPage=1&outboundPlanId=71")]
    public async Task 실제역할01로그인은창고05의정확한기존두출고경로만조회문맥그대로돌려준다(string returnUrl)
    {
        const string appBase = "http://fixture.invalid/roles/01/";
        await using var fixture = await Fixture.CreateAsync(appBase, appBase + "login");
        var html = await fixture.RenderAsync<LoginPage>(new() { [nameof(LoginPage.ReturnUrl)] = returnUrl });
        Assert.Equal("http://fixture.invalid" + returnUrl, Link(html, "이전 화면으로")); Assert.Equal(0, fixture.Http.Calls);
    }

    [Theory]
    [InlineData("/roles/05/other?stage=picking")]
    [InlineData("/roles/04/warehouse/general/outbound-plan-review?stage=picking")]
    [InlineData("/roles/05/warehouse/general/outbound-plan-review-more?stage=picking")]
    [InlineData("/roles/05/warehouse/general/outbound-plan-review/child?stage=picking")]
    [InlineData("/roles/05/work/outbound/plans-other?stage=picking")]
    [InlineData("/roles/05/warehouse%2Fgeneral/outbound-plan-review?stage=picking")]
    [InlineData("/roles/05/warehouse/%2e%2e/general/outbound-plan-review?stage=picking")]
    [InlineData("/roles/05/warehouse/general/outbound-plan-review%0A?stage=picking")]
    [InlineData("/roles/05/warehouse%252Fgeneral/outbound-plan-review?stage=picking")]
    [InlineData("https://outside.invalid/roles/05/warehouse/general/outbound-plan-review?stage=picking")]
    [InlineData("//outside.invalid/roles/05/warehouse/general/outbound-plan-review?stage=picking")]
    public async Task 역할01의창고출고복귀예외가다른역할이나유사경로및기존거절입력으로넓어지지않는다(string returnUrl)
    {
        const string appBase = "http://fixture.invalid/roles/01/";
        await using var fixture = await Fixture.CreateAsync(appBase, appBase + "login");
        var html = await fixture.RenderAsync<LoginPage>(new() { [nameof(LoginPage.ReturnUrl)] = returnUrl });
        Assert.Equal(appBase + "community/map", Link(html, "이전 화면으로")); Assert.Equal(0, fixture.Http.Calls);
    }

    [Theory]
    [InlineData("https://outside.invalid/warehouse/general/outbound-plan-review?stage=picking")]
    [InlineData("//outside.invalid/warehouse/general/outbound-plan-review?stage=picking")]
    [InlineData("/roles/04/warehouse/general/outbound-plan-review?stage=picking")]
    [InlineData("/roles/05/../04/warehouse/general/outbound-plan-review?stage=picking")]
    public async Task 기존로그인복귀검사는외부주소와다른역할주소를현재창고업무로추정하지않는다(string returnUrl)
    {
        const string appBase = "http://fixture.invalid/roles/05/";
        await using var fixture = await Fixture.CreateAsync(appBase, appBase + "login");
        var html = await fixture.RenderAsync<LoginPage>(new() { [nameof(LoginPage.ReturnUrl)] = returnUrl });
        Assert.Equal(appBase + "community/map", Link(html, "이전 화면으로")); Assert.Equal(0, fixture.Http.Calls);
    }

    [Theory]
    [InlineData("/roles/03/shipper/sales/orders?q=%EC%95%84%EB%AA%AC%EB%93%9C&page=2", true)]
    [InlineData("/roles/03/shipper/sales/orders?scope=domestic&status=reserved", true)]
    [InlineData("/roles/03/shipper/sales/orders/41?q=coffee", false)]
    [InlineData("/roles/03/shipper/sales/orders-extra?q=coffee", false)]
    [InlineData("/roles/04/shipper/sales/orders?q=coffee", false)]
    [InlineData("/roles/03/shipper/sales%2Forders?q=coffee", false)]
    [InlineData("/roles/03/shipper/../shipper/sales/orders?q=coffee", false)]
    [InlineData("//outside.invalid/roles/03/shipper/sales/orders?q=coffee", false)]
    public async Task 역할01로그인은화주03판매목록만조회조건그대로돌려준다(string returnUrl, bool accepted)
    {
        const string origin = "http://fixture.invalid";
        const string appBase = origin + "/roles/01/";
        await using var fixture = await Fixture.CreateAsync(appBase, appBase + "login");
        var html = await fixture.RenderAsync<LoginPage>(new() { [nameof(LoginPage.ReturnUrl)] = returnUrl });
        var expected = new Uri(accepted ? origin + returnUrl : appBase + "community/map");
        Assert.Equal(expected.AbsoluteUri, new Uri(Link(html, "이전 화면으로")).AbsoluteUri);
        Assert.Equal(0, fixture.Http.Calls);
    }

    private static string Link(string html, string text)
    {
        var anchor = Assert.Single(Regex.Matches(html, "<a\\b[^>]*>(.*?)</a>", RegexOptions.Singleline).Cast<Match>()
            .Where(match => WebUtility.HtmlDecode(Regex.Replace(match.Groups[1].Value, "<[^>]*>", string.Empty)).Trim() == text));
        return WebUtility.HtmlDecode(Regex.Match(anchor.Value, "\\bhref=\"([^\"]*)\"").Groups[1].Value);
    }
    private static string ReturnParameter(Uri uri)
    {
        var values = QueryValues(uri, "returnUrl"); return Assert.Single(values);
    }
    private static string QueryValue(Uri uri, string name) => Assert.Single(QueryValues(uri, name));
    private static IReadOnlyList<string> QueryValues(Uri uri, string name)
        => uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2)).Where(pair => WebUtility.UrlDecode(pair[0]) == name)
            .Select(pair => pair.Length == 2 ? WebUtility.UrlDecode(pair[1]) : string.Empty).ToArray();

    private sealed class Fixture : IAsyncDisposable
    {
        public RejectHttp Http { get; } = new();
        public NavigationStub Navigation { get; }
        private readonly ServiceProvider services;
        private readonly HtmlRenderer renderer;
        private Fixture(string appBase, string current)
        {
            Navigation = new(appBase, current);
            var registrations = new ServiceCollection(); registrations.AddLogging(); registrations.AddMudServices();
            registrations.AddSingleton<IJSRuntime, NoJavaScript>(); registrations.AddSingleton<NavigationManager>(Navigation);
            registrations.AddSingleton<IClientSessionGuard, ClientSessionGuard>();
            registrations.AddSingleton(new HttpClient(Http, false) { BaseAddress = new Uri(appBase) });
            // Tests 프로젝트의 source-link 동명 형식 대신 실제 제품 페이지가 주입받는 형식을 등록한다.
            var authType = typeof(WarehouseOutboundPlanReviewPage).GetProperty("AuthSession", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.PropertyType
                ?? throw new MissingMemberException(nameof(WarehouseOutboundPlanReviewPage), "AuthSession");
            registrations.AddSingleton(authType, provider => Activator.CreateInstance(authType,
                provider.GetRequiredService<HttpClient>(), provider.GetRequiredService<IJSRuntime>(), provider.GetRequiredService<IClientSessionGuard>())
                ?? throw new InvalidOperationException("실제 제품 인증 세션을 만들지 못했습니다."));
            services = registrations.BuildServiceProvider();
            renderer = new(services, services.GetRequiredService<ILoggerFactory>());
        }
        public static async Task<Fixture> CreateAsync(string appBase, string current)
        {
            var fixture = new Fixture(appBase, current);
            try
            {
                await fixture.RenderAsync<Providers>(); return fixture;
            }
            catch { await fixture.DisposeAsync(); throw; }
        }
        public Task<string> RenderAsync<T>(Dictionary<string, object?>? parameters = null) where T : IComponent
            => renderer.Dispatcher.InvokeAsync(async () => (await renderer.RenderComponentAsync<T>(
                ParameterView.FromDictionary(parameters ?? new()))).ToHtmlString());
        public async ValueTask DisposeAsync() { await renderer.DisposeAsync(); await services.DisposeAsync(); Http.Dispose(); }
    }
    private sealed class RejectHttp : HttpMessageHandler
    {
        public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        { Calls++; throw new InvalidOperationException("로그인 링크 시험은 HTTP를 실행하지 않습니다."); }
    }
    private sealed class NavigationStub : NavigationManager
    {
        public NavigationStub(string appBase, string current) => Initialize(appBase, current);
        public void At(string uri) => Uri = ToAbsoluteUri(uri).AbsoluteUri;
        protected override void NavigateToCore(string uri, bool forceLoad) => throw new InvalidOperationException("정적 링크 시험은 탐색하지 않습니다.");
    }
    private sealed class NoJavaScript : IJSRuntime
    {
        public ValueTask<T> InvokeAsync<T>(string identifier, object?[]? args) => ValueTask.FromResult(default(T)!);
        public ValueTask<T> InvokeAsync<T>(string identifier, CancellationToken token, object?[]? args) => InvokeAsync<T>(identifier, args);
    }
    private sealed class Providers : ComponentBase
    {
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<MudThemeProvider>(0); builder.CloseComponent();
            builder.OpenComponent<MudPopoverProvider>(1); builder.CloseComponent();
            builder.OpenComponent<MudDialogProvider>(2); builder.CloseComponent();
            builder.OpenComponent<MudSnackbarProvider>(3); builder.CloseComponent();
        }
    }
}
