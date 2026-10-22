using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MudBlazor.Services;
using Ssalddel.Contracts.Admin.BusinessPackages;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;

namespace Ssalddel.Tests.Ui.Common;

public sealed class RoleSelectionBusinessR9Tests
{
    [Fact]
    public void 화물세역할과_음식네역할_생활창고를_기존업무코드로_구분한다()
    {
        var groups = RoleWorkspaceHomeCatalog.Groups;
        Assert.Equal(new[] { BusinessPackageCatalog.FreightDelivery, BusinessPackageCatalog.FoodDelivery, "other" }, groups.Select(group => group.Key));
        Assert.Equal(new[] { "화물 운송", "음식 배달", "생활·창고" }, groups.Select(group => group.Label));
        Assert.Equal(new[] { RoleWorkspaceCatalog.Shipper, RoleWorkspaceCatalog.CargoDriver, RoleWorkspaceCatalog.Operator }, groups[0].Entries.Select(entry => entry.RoleKey));
        Assert.Equal(new[] { RoleWorkspaceCatalog.Orderer, RoleWorkspaceCatalog.Restaurant, RoleWorkspaceCatalog.FoodDriver, RoleWorkspaceCatalog.Operator }, groups[1].Entries.Select(entry => entry.RoleKey));
        Assert.Equal(new[] { RoleWorkspaceCatalog.Community, RoleWorkspaceCatalog.Warehouse }, groups[2].Entries.Select(entry => entry.RoleKey));
        Assert.Equal(RoleWorkspaceCatalog.Roles.Select(role => role.Key).Order(),
            groups.SelectMany(group => group.Entries).Select(entry => entry.RoleKey).Distinct().Order());
    }

    [Fact]
    public async Task 관리자만_같은역할경로에서_업무를구분하고_다른역할링크는_보존한다()
    {
        var html = await RenderAsync();
        var entries = Links(html);
        Assert.Equal(9, entries.Count);
        var administrators = entries.Where(entry => Attribute(entry, "data-role") == RoleWorkspaceCatalog.Operator).ToArray();
        Assert.Equal(2, administrators.Length);
        Assert.Contains(administrators, entry => Attribute(entry, "href") == "http://localhost/preview/workspace/operator?business=freight-delivery");
        Assert.Contains(administrators, entry => Attribute(entry, "href") == "http://localhost/preview/workspace/operator?business=food-delivery");
        foreach (var role in RoleWorkspaceCatalog.Roles.Where(role => role.Key != RoleWorkspaceCatalog.Operator))
            Assert.Contains(entries, entry => Attribute(entry, "data-role") == role.Key
                && Attribute(entry, "href") == "http://localhost/preview" + role.Href);
        Assert.DoesNotContain("<form", html);
    }

    [Theory]
    [InlineData(null, BusinessPackageCatalog.FoodDelivery)]
    [InlineData(BusinessPackageCatalog.FoodDelivery, BusinessPackageCatalog.FoodDelivery)]
    [InlineData(BusinessPackageCatalog.FreightDelivery, BusinessPackageCatalog.FreightDelivery)]
    public async Task 운영자선택은_해당업무의_관리자하나만_강조한다(string? selectedBusiness, string expectedBusiness)
    {
        var html = await RenderAsync(new Dictionary<string, object?>
        {
            [nameof(RoleSelectionHome.SelectedRoleKey)] = RoleWorkspaceCatalog.Operator,
            [nameof(RoleSelectionHome.SelectedBusinessCode)] = selectedBusiness
        });
        var selected = Assert.Single(Links(html).Where(link => Attribute(link, "aria-current") == "page"));
        Assert.Equal(RoleWorkspaceCatalog.Operator, Attribute(selected, "data-role"));
        Assert.Equal(expectedBusiness, Attribute(selected, "data-business"));
        Assert.Contains("is-selected", Attribute(selected, "class"));
    }

    [Fact]
    public async Task 기존고유역할_선택과_법적서비스안내를_유지한다()
    {
        var html = await RenderAsync(new Dictionary<string, object?>
        {
            [nameof(RoleSelectionHome.SelectedRoleKey)] = RoleWorkspaceCatalog.Shipper,
            [nameof(RoleSelectionHome.SelectedBusinessCode)] = BusinessPackageCatalog.FoodDelivery,
            [nameof(RoleSelectionHome.ShowQuickIntroduction)] = true
        });
        var selected = Assert.Single(Links(html).Where(link => Attribute(link, "aria-current") == "page"));
        Assert.Equal(RoleWorkspaceCatalog.Shipper, Attribute(selected, "data-role"));
        Assert.Equal(BusinessPackageCatalog.FreightDelivery, Attribute(selected, "data-business"));
        Assert.Contains("Mirror는 통신판매중개자이며 상품·서비스 거래의 당사자가 아닙니다.", html);
        Assert.Contains("http://localhost/preview/commerce/notices", html);
        Assert.Contains("http://localhost/preview/services", html);
        Assert.Contains("aria-labelledby=\"role-group-freight-delivery\"", html);
        Assert.Contains("aria-labelledby=\"role-group-food-delivery\"", html);
        Assert.Contains("aria-labelledby=\"role-group-other\"", html);
    }

    [Fact]
    public async Task 독립호스트에서도_업무관리자링크는_통합앱루트를_사용한다()
    {
        var html = await RenderAsync(new Dictionary<string, object?>
        {
            [nameof(RoleSelectionHome.IntegratedRoot)] = true
        }, "http://localhost/roles/01/");
        Assert.Contains("http://localhost/workspace/operator?business=freight-delivery", html);
        Assert.Contains("http://localhost/workspace/operator?business=food-delivery", html);
        Assert.DoesNotContain("http://localhost/roles/01/workspace/", html);
    }

    private static string Attribute(string tag, string name)
        => Regex.Match(tag, "(?:^|\\s)" + Regex.Escape(name) + "=\"([^\"]*)\"").Groups[1].Value;

    private static IReadOnlyList<string> Links(string html)
        => Regex.Matches(html, "<a\\s[^>]*data-role=\"[^\"]+\"[^>]*>")
            .Select(match => match.Value).ToArray();

    private static async Task<string> RenderAsync(Dictionary<string, object?>? parameters = null, string baseUri = "http://localhost/preview/")
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMudServices();
        services.AddSingleton<NavigationManager>(new TestNavigation(baseUri));
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<RoleSelectionHome>(ParameterView.FromDictionary(parameters ?? []));
            return WebUtility.HtmlDecode(output.ToHtmlString());
        });
    }

    private sealed class TestNavigation : NavigationManager
    {
        public TestNavigation(string baseUri) => Initialize(baseUri, baseUri);
        protected override void NavigateToCore(string uri, bool forceLoad) { }
    }
}
