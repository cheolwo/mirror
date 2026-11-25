using Ssalddel.WebApp.Services;

namespace Ssalddel.Tests.WebApp;

public sealed class WarehouseBatchMenuSelectionTests
{
    private const string CanonicalPath = "/warehouse/general/outbound-plan-review";
    private static readonly string[] AppBases = ["https://local.invalid/", "https://local.invalid/roles/05/"];

    [Theory]
    [InlineData("review", "1 출고 검토 묶음")]
    [InlineData("picking", "2 피킹 묶음")]
    [InlineData("packing", "3 포장 묶음")]
    [InlineData("handoff", "4 운송 인계 묶음")]
    public void 공정과조회조건의순서에관계없이실제메뉴에서해당단계하나만선택한다(string stage, string label)
    {
        foreach (var appBase in AppBases)
        foreach (var query in new[]
        {
            $"?stage={stage}&warehouseId=34&batchPage=2&batchKey=packing%3Atask%3A33&memberPage=1&planPage=3",
            $"?warehouseId=34&periodStartUtc=2026-11-07T00%3A00%3A00.0000000Z&stage={stage}&outboundPlanId=81#members"
        })
        {
            var current = Address(appBase, CanonicalPath + query);
            var webMenus = WebNavigationCatalog.GetBusinessItems("warehouse")
                .Where(item => WarehouseBatchMenuSelection.IsBatchMenu(item.Href)).ToArray();
            Assert.Equal(5, webMenus.Length);
            var selectedWeb = Assert.Single(webMenus, item => WarehouseBatchMenuSelection.IsActive(appBase, current, item.Href));
            Assert.Equal(label, selectedWeb.Title);

            var hostMenus = RoleWebAppDescriptor.FromAssembly(RoleWebAppDescriptor.WarehouseAssemblyName)
                .NavigationItems.Where(item => WarehouseBatchMenuSelection.IsBatchMenu(item.Path)).ToArray();
            Assert.Equal(4, hostMenus.Length);
            var selectedHost = Assert.Single(hostMenus, item => WarehouseBatchMenuSelection.IsActive(appBase, current, item.Path));
            Assert.Equal(label, selectedHost.Label);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("?outboundPlanId=81")]
    [InlineData("?warehouseId=34&searchText=stage%3Dpicking#stage=packing")]
    public void 공정이없는기존주소는일반출고검토만선택한다(string suffix)
    {
        foreach (var appBase in AppBases)
        {
            var current = Address(appBase, CanonicalPath + suffix);
            var selected = Assert.Single(AllMenuPaths(), path => WarehouseBatchMenuSelection.IsActive(appBase, current, path));
            Assert.Equal(CanonicalPath, selected);
        }
    }

    [Theory]
    [InlineData("?stage=")]
    [InlineData("?stage")]
    [InlineData("?stage=unknown")]
    [InlineData("?stage=PICKING")]
    [InlineData("?stage=picking&stage=picking")]
    [InlineData("?stage=picking&stage=packing")]
    [InlineData("?stage=picking&STAGE=packing")]
    [InlineData("?stage=picking&stage=")]
    public void 잘못되거나중복된공정은아무메뉴도선택하지않는다(string query)
    {
        foreach (var appBase in AppBases)
        {
            var current = Address(appBase, CanonicalPath + query);
            Assert.DoesNotContain(AllMenuPaths(), path => WarehouseBatchMenuSelection.IsActive(appBase, current, path));
        }
    }

    [Fact]
    public void 인코딩된단일공정과관계없는추가조건도같은메뉴로선택한다()
    {
        foreach (var appBase in AppBases)
        {
            var current = Address(appBase, CanonicalPath + "?batchPage=3&%73tage=%70icking&search=packing&filter=a&filter=b#stage=packing");
            var selected = Assert.Single(AllMenuPaths(), path => WarehouseBatchMenuSelection.IsActive(appBase, current, path));
            Assert.Equal(CanonicalPath + "?stage=picking", selected);
        }
    }

    [Theory]
    [InlineData("/warehouse/general/outbound-plan-review-more?stage=picking")]
    [InlineData("/warehouse/general/outbound-plan-review/history?stage=picking")]
    [InlineData("/warehouse/general/inventory?stage=picking")]
    [InlineData("/warehouse/work/picking-batch?stage=picking")]
    [InlineData("/driver/me?stage=picking")]
    public void 같은공정문자열이있어도다른업무경로는선택하지않는다(string path)
    {
        foreach (var appBase in AppBases)
            Assert.DoesNotContain(AllMenuPaths(), menu => WarehouseBatchMenuSelection.IsActive(appBase, Address(appBase, path), menu));
    }

    [Theory]
    [InlineData("https://local.invalid/roles/05/", "https://local.invalid/roles/04/warehouse/general/outbound-plan-review?stage=picking")]
    [InlineData("https://local.invalid/roles/05/", "https://local.invalid/warehouse/general/outbound-plan-review?stage=picking")]
    [InlineData("https://local.invalid/", "https://local.invalid/roles/05/warehouse/general/outbound-plan-review?stage=picking")]
    [InlineData("https://local.invalid/", "https://other.invalid/warehouse/general/outbound-plan-review?stage=picking")]
    [InlineData("invalid", "https://local.invalid/warehouse/general/outbound-plan-review?stage=picking")]
    [InlineData("https://local.invalid/", "relative-path")]
    public void 현재앱의기준경로를벗어난주소를선택하지않는다(string appBase, string current)
    {
        Assert.DoesNotContain(AllMenuPaths(), menu => WarehouseBatchMenuSelection.IsActive(appBase, current, menu));
    }

    [Theory]
    [InlineData("?stage=unknown")]
    [InlineData("?stage=picking&stage=picking")]
    public void 잘못된메뉴공정도유효한현재단계로대체하지않는다(string suffix)
    {
        Assert.False(WarehouseBatchMenuSelection.IsActive(AppBases[0], Address(AppBases[0], CanonicalPath + "?stage=picking"), CanonicalPath + suffix));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("/warehouse/general/outbound-plan-review", true)]
    [InlineData("/warehouse/general/outbound-plan-review?stage=picking", true)]
    [InlineData("/warehouse/general/outbound-plan-review?outboundPlanId=81#review", true)]
    [InlineData("/warehouse/general/outbound-plan-review-more", false)]
    [InlineData("/warehouse/work/picking-batch", false)]
    public void 정확히같은출고경로의메뉴에만별도선택규칙을적용한다(string? path, bool expected)
        => Assert.Equal(expected, WarehouseBatchMenuSelection.IsBatchMenu(path));

    [Fact]
    public void 창고모바일출고의실제메뉴는조회문맥을제외한기존업무경로로비교한다()
    {
        var host = RoleWebAppDescriptor.FromAssembly(RoleWebAppDescriptor.WarehouseAssemblyName);
        var outbound = Assert.Single(host.MobileNavigationItems, item => item.Label == "출고");
        Assert.True(WarehouseBatchMenuSelection.IsBatchMenu(outbound.Path));
        Assert.Equal(CanonicalPath, WarehouseBatchMenuSelection.PathWithoutQuery(outbound.Path));
        Assert.Equal(CanonicalPath, WarehouseBatchMenuSelection.PathWithoutQuery(CanonicalPath + "?stage=packing#members"));
        Assert.Equal("/driver/me", WarehouseBatchMenuSelection.PathWithoutQuery("/driver/me"));
    }

    private static string[] AllMenuPaths()
        => WebNavigationCatalog.GetBusinessItems("warehouse")
            .Where(item => WarehouseBatchMenuSelection.IsBatchMenu(item.Href)).Select(item => item.Href).ToArray();

    private static string Address(string appBase, string path) => new Uri(new Uri(appBase), path.TrimStart('/')).AbsoluteUri;
}
