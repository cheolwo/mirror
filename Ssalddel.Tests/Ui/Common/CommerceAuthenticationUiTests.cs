using System.Net;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using MudBlazor.Services;
using Ssalddel.Ui.Common.Areas.App.Components;
using Ssalddel.Ui.Common.Areas.App.Components.Commerce;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace Ssalddel.Tests.Ui.Common;

public sealed class CommerceAuthenticationUiTests
{
    [Fact]
    public async Task 익명_로그인화면은_업무입력을_렌더하거나_보호요청을_제출하지않는다()
    {
        var auth = new AuthService();
        var result = await RenderAsync(auth, "/commerce/disputes?sourceKind=food-order&sourceId=FOOD-42");
        Assert.Contains("type=\"password\"", result.Html);
        Assert.Contains("이전 화면으로", result.Html);
        Assert.Contains("/mobile/commerce/disputes?sourceKind=food-order&amp;sourceId=FOOD-42", result.RawHtml);
        Assert.DoesNotContain("문제 내용", result.Html);
        Assert.DoesNotContain("요청 접수", result.Html);
        Assert.Null(result.Navigation.Target);
        Assert.Equal(1, auth.RestoreCalls);
        Assert.Equal(0, auth.LoginCalls);
        Assert.Equal(0, result.Commerce.Commands);
        Assert.Empty(result.Commerce.Rights);
    }

    [Theory]
    [InlineData("/commerce/privacy/case-42", "http://localhost/mobile/commerce/privacy/case-42")]
    [InlineData("/commerce/disputes?sourceKind=food-order&sourceId=FOOD-42", "http://localhost/mobile/commerce/disputes?sourceKind=food-order&sourceId=FOOD-42")]
    [InlineData("//outside.example/commerce/privacy", "http://localhost/mobile/commerce/notices")]
    [InlineData("/commerce/privacy/%252foutside.example", "http://localhost/mobile/commerce/notices")]
    public async Task 복원된인증은_앱기준경로내_원래업무로만_복귀하고_자동접수하지않는다(string route, string expected)
    {
        var auth = new AuthService { Authenticated = true };
        var result = await RenderAsync(auth, route);
        Assert.Equal(expected, result.Navigation.Target);
        Assert.Equal(0, auth.LoginCalls);
        Assert.Equal(0, result.Commerce.Commands);
        Assert.Empty(result.Commerce.Rights);
    }

    private static async Task<RenderResult> RenderAsync(AuthService auth, string returnUrl)
    {
        var services = new ServiceCollection();
        var nav = new TestNavigation();
        services.AddLogging(); services.AddMudServices(); services.AddCommerceUiFixture();
        services.AddSingleton<IJSRuntime, NoopJs>(); services.AddSingleton<NavigationManager>(nav);
        services.AddTransient(_ => new 주문자앱인증ViewModel(auth));
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            await renderer.RenderComponentAsync<RoleAppProviders>();
            var page = await renderer.RenderComponentAsync<OrdererCommerceAuthenticationPage>(ParameterView.FromDictionary(
                new Dictionary<string, object?> { [nameof(OrdererCommerceAuthenticationPage.ReturnUrl)] = returnUrl }));
            var raw = page.ToHtmlString();
            return new RenderResult(WebUtility.HtmlDecode(raw), raw, nav, (CommerceUiFixture)provider.GetRequiredService<Ssalddel.Ui.Common.Areas.App.Services.Commerce.I통신판매보호Client>());
        });
    }

    private sealed record RenderResult(string Html, string RawHtml, TestNavigation Navigation, CommerceUiFixture Commerce);
    private sealed class AuthService : I주문자앱인증Service
    {
        public bool Authenticated { get; init; }
        public int RestoreCalls, LoginCalls;
        public Task<주문자앱인증결과> 복원Async(CancellationToken cancellationToken = default)
        {
            RestoreCalls++;
            return Task.FromResult(new 주문자앱인증결과(Authenticated ? new(true, "owner", "계정") : 주문자앱세션상태.익명));
        }
        public Task<주문자앱인증결과> 로그인Async(string name, string password, CancellationToken cancellationToken = default)
        { LoginCalls++; return Task.FromResult(new 주문자앱인증결과(new(true, "owner", "계정"))); }
        public Task 로그아웃Async(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
    private sealed class TestNavigation : NavigationManager
    {
        public string? Target { get; private set; }
        public TestNavigation() => Initialize("http://localhost/mobile/", "http://localhost/mobile/login");
        protected override void NavigateToCore(string uri, bool forceLoad) => Target = uri;
        protected override void NavigateToCore(string uri, NavigationOptions options) => Target = uri;
    }
    private sealed class NoopJs : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => ValueTask.FromResult(default(TValue)!);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => ValueTask.FromResult(default(TValue)!);
    }
}
