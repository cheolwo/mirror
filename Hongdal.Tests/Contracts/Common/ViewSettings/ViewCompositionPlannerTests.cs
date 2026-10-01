using Hongdal.Contracts.Common.ViewSettings;

namespace Hongdal.Tests.Contracts.Common.ViewSettings;

public sealed class ViewCompositionPlannerTests
{
    [Fact]
    public void BuildPlan_HidesOptionalItemsDisabledByAdmin()
    {
        var plan = ViewCompositionPlanner.BuildPlan(
            CreateCatalog(),
            "HongdalApp",
            "shipper",
            ViewCompositionSurfaceCode.PrimaryNavigation,
            [
                new ViewCompositionPolicyOverride(
                    "HongdalApp",
                    "shipper",
                    ViewCompositionSurfaceCode.PrimaryNavigation,
                    "shipper.sales-channels",
                    PolicyEnabled: false)
            ]);

        Assert.DoesNotContain(plan.Items, x => x.ItemKey == "shipper.sales-channels");
        Assert.Contains(plan.Items, x => x.ItemKey == "shipper.request");
    }

    [Fact]
    public void BuildPlan_KeepsRequiredItemsEvenWhenAdminDisablesThem()
    {
        var plan = ViewCompositionPlanner.BuildPlan(
            CreateCatalog(),
            "HongdalApp",
            "shipper",
            ViewCompositionSurfaceCode.PrimaryNavigation,
            [
                new ViewCompositionPolicyOverride(
                    "HongdalApp",
                    "shipper",
                    ViewCompositionSurfaceCode.PrimaryNavigation,
                    "shipper.request",
                    PolicyEnabled: false)
            ],
            includeHidden: true);

        var request = Assert.Single(plan.Items, x => x.ItemKey == "shipper.request");
        Assert.True(request.IsRequired);
        Assert.False(request.PolicyEnabled);
        Assert.True(request.EffectiveVisible);
    }

    [Fact]
    public void BuildPlan_AppliesDashboardWidgetOrderAndSpans()
    {
        var plan = ViewCompositionPlanner.BuildPlan(
            CreateCatalog(),
            "HongdalAdmin",
            "admin",
            ViewCompositionSurfaceCode.Dashboard,
            [
                new ViewCompositionPolicyOverride(
                    "HongdalAdmin",
                    "admin",
                    ViewCompositionSurfaceCode.Dashboard,
                    "admin.dispatch-health",
                    PolicyEnabled: true,
                    SortOrder: 5,
                    ColumnSpan: 8,
                    RowSpan: 2)
            ]);

        var first = Assert.Single(plan.Items, x => x.ItemKey == "admin.dispatch-health");
        Assert.Equal(5, first.SortOrder);
        Assert.Equal(8, first.ColumnSpan);
        Assert.Equal(2, first.RowSpan);
    }

    [Fact]
    public void BuildPlan_FiltersByRoleAndSurface()
    {
        var plan = ViewCompositionPlanner.BuildPlan(
            CreateCatalog(),
            "HongdalApp",
            "shipper",
            ViewCompositionSurfaceCode.ProfileMenu);

        Assert.All(plan.Items, x =>
        {
            Assert.Equal("shipper", x.RoleName);
            Assert.Equal(ViewCompositionSurfaceCode.ProfileMenu, x.Surface);
        });
        Assert.DoesNotContain(plan.Items, x => x.RoleName == "admin");
    }

    private static IReadOnlyList<ViewCompositionCatalogItem> CreateCatalog()
    {
        return
        [
            new(
                "HongdalApp",
                "shipper",
                ViewCompositionSurfaceCode.PrimaryNavigation,
                "shipper.request",
                ViewCompositionItemKindCode.View,
                "Request shipment",
                "/shipper/request",
                "add_box",
                "",
                IsRequired: true,
                DefaultPolicyEnabled: true,
                SortOrder: 10),
            new(
                "HongdalApp",
                "shipper",
                ViewCompositionSurfaceCode.PrimaryNavigation,
                "shipper.sales-channels",
                ViewCompositionItemKindCode.View,
                "Sales channels",
                "/shipper/sales/channels",
                "storefront",
                "",
                IsRequired: false,
                DefaultPolicyEnabled: true,
                SortOrder: 20),
            new(
                "HongdalApp",
                "shipper",
                ViewCompositionSurfaceCode.ProfileMenu,
                "shipper.view-settings",
                ViewCompositionItemKindCode.View,
                "View settings",
                "/shipper/settings/views",
                "settings",
                "",
                IsRequired: false,
                DefaultPolicyEnabled: true,
                SortOrder: 10),
            new(
                "HongdalAdmin",
                "admin",
                ViewCompositionSurfaceCode.Dashboard,
                "admin.dispatch-health",
                ViewCompositionItemKindCode.Widget,
                "Dispatch health",
                "",
                "monitoring",
                "AdminDispatchHealthWidget",
                IsRequired: false,
                DefaultPolicyEnabled: true,
                SortOrder: 20,
                ColumnSpan: 4,
                RowSpan: 1),
            new(
                "HongdalAdmin",
                "admin",
                ViewCompositionSurfaceCode.ProfileMenu,
                "admin.view-policies",
                ViewCompositionItemKindCode.View,
                "View policies",
                "/view-policies",
                "settings",
                "",
                IsRequired: true,
                DefaultPolicyEnabled: true,
                SortOrder: 10)
        ];
    }
}
