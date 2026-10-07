using System.Net;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MudBlazor.Services;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;

namespace Ssalddel.Tests.Ui.Common;

public sealed class RoleSelectionHomeRenderTests
{
    [Fact]
    public async Task 여덟역할이_전용앱이아닌_같은통합앱의_지도에_연결된다()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMudServices();
        services.AddSingleton<NavigationManager, TestNavigation>();
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<RoleSelectionHome>();
            return WebUtility.HtmlDecode(output.ToHtmlString());
        });
        foreach (var key in new[] { "community", "orderer", "restaurant", "food-driver", "shipper", "cargo-driver", "warehouse", "operator" })
            Assert.Contains("http://localhost/preview/workspace/" + key, html);
        Assert.DoesNotContain("/roles/0", html);
        Assert.Contains("음식기사", html);
        Assert.Contains("화물기사", html);
        Assert.DoesNotContain("<form", html);
    }

    [Theory]
    [InlineData("https://other.example/path")]
    [InlineData("//other.example/path")]
    [InlineData("/\\other.example/path")]
    [InlineData("/%2Fother.example/path")]
    [InlineData("/%5Cother.example/path")]
    [InlineData("/workspace/../login")]
    public void 독립업무_링크는_외부_복귀_경로로_바꾸지_않는다(string route)
        => Assert.Null(RoleWorkspaceNavigation.WithReturn(route, "/workspace/orderer"));

    [Fact]
    public async Task 독립01_역할선택은_통합앱_루트로_연결한다()
    {
        var services = new ServiceCollection();
        services.AddLogging(); services.AddMudServices();
        services.AddSingleton<NavigationManager>(new TestNavigation("http://localhost/roles/01/"));
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<RoleSelectionHome>(ParameterView.FromDictionary(
                new Dictionary<string, object?> { [nameof(RoleSelectionHome.IntegratedRoot)] = true }));
            return WebUtility.HtmlDecode(output.ToHtmlString());
        });
        Assert.Contains("http://localhost/workspace/food-driver", html);
        Assert.DoesNotContain("http://localhost/roles/01/workspace/", html);
    }

    private sealed class TestNavigation : NavigationManager
    {
        public TestNavigation() : this("http://localhost/preview/") { }
        public TestNavigation(string baseUri) => Initialize(baseUri, baseUri);
        protected override void NavigateToCore(string uri, bool forceLoad) { }
    }
}
