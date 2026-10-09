using System.Net;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Pricing;

namespace Ssalddel.Tests.Ui.Common;

public sealed class AgreedRateTableRenderTests
{
    [Theory]
    [InlineData("restaurant")]
    [InlineData("food-driver")]
    [InlineData("shipper")]
    [InlineData("cargo-driver")]
    public async Task 지원역할의_독립페이지는_빈목록과_새제안_복귀를_표시한다(string role)
    {
        var api = new RateUiApi { Get = (_, _, _) => Task.FromResult<object>(Array.Empty<Ssalddel.Contracts.Common.Pricing.AgreedRateTableDto>()) };
        var html = await Render(api, new RateUiAccess(), role, "//evil.example");
        Assert.Contains("받은 제안", html); Assert.Contains("내가 보낸 제안", html); Assert.Contains("새 요금표 제안", html);
        Assert.Contains($"href=\"/workspace/{role}\"", html); Assert.DoesNotContain("evil.example", html); Assert.Empty(api.Posts);
        Assert.DoesNotContain("이 업무에 요금표 선택", html);
    }
    [Fact]
    public async Task 인증안내는_비공개요금표와_제안양식을_함께_표시하지않는다()
    {
        var api = new RateUiApi(); var html = await Render(api, new RateUiAccess { Identity = new(null, 0, false) }, "restaurant", "/workspace/restaurant");
        Assert.Contains("로그인", html); Assert.DoesNotContain("새 요금표 제안", html); Assert.DoesNotContain("받은 제안", html); Assert.Empty(api.Gets);
    }
    [Fact]
    public async Task 조회실패는_정상빈목록으로_표시하지않고_재조회를_제공한다()
    {
        var api = new RateUiApi { Get = (_, _, _) => throw new HttpRequestException("failure") };
        var html = await Render(api, new RateUiAccess(), "restaurant", null);
        Assert.Contains("role=\"alert\"", html); Assert.Contains("최신 요금표 다시 조회", html);
        Assert.DoesNotContain("현재 확인할 요금표가 없습니다", html); Assert.Empty(api.Posts);
    }
    [Theory]
    [InlineData("/login?returnUrl=%2Fagreed-rates", true)]
    [InlineData("javascript:alert(1)", false)]
    public async Task 네이티브_로그인복귀는_로컬경로만_허용한다(string login, bool valid)
    {
        var html = await Render(new RateUiApi(), new RateUiAccess { Identity = new(null, 0, false) }, "restaurant", null, login);
        Assert.Equal(valid, html.Contains("href=\"/login?returnUrl=%2Fagreed-rates\"")); Assert.DoesNotContain("javascript:", html);
        if (!valid) Assert.Contains("/workspace/restaurant/login", html);
    }
    private static async Task<string> Render(RateUiApi api, RateUiAccess access, string role, string? returnHref, string? login = null)
    {
        var services = new ServiceCollection(); services.AddLogging();
        services.AddSingleton<IRoleWorkspaceApi>(api); services.AddSingleton<IRoleWorkspaceAccess>(access);
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<AgreedRateTableScreen>(ParameterView.FromDictionary(new Dictionary<string, object?>
            { [nameof(AgreedRateTableScreen.RoleKey)] = role, [nameof(AgreedRateTableScreen.ReturnHref)] = returnHref,
                [nameof(AgreedRateTableScreen.LoginReturnHref)] = login }));
            return WebUtility.HtmlDecode(output.ToHtmlString());
        });
    }
}
