using Hongdal.WebApp.Services;

namespace Hongdal.Tests.WebApp;

public sealed class WebExperienceCatalogTests
{
    [Fact]
    public void Roles_HaveUniqueKeysAndRequiredExperienceGroups()
    {
        var roles = WebExperienceCatalog.Roles;

        Assert.Equal(roles.Count, roles.Select(role => role.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Contains(roles, role => role.Key == "global-supplier");
        Assert.Contains(roles, role => role.Key == "shipper-seller");
        Assert.Contains(roles, role => role.Key == "driver");
        Assert.Contains(roles, role => role.Key == "warehouse");
        Assert.Contains(roles, role => role.Key == "community-orderer");
    }

    [Fact]
    public void Roles_ExposeUsableStartAndScreenRoutes()
    {
        foreach (var role in WebExperienceCatalog.Roles)
        {
            Assert.StartsWith("/", role.StartHref);
            Assert.StartsWith("/images/role-previews/", role.ImageUrl);
            Assert.True(role.Screens.Count >= 4);
            Assert.All(role.Screens, screen => Assert.StartsWith("/", screen.Href));
        }
    }

    [Fact]
    public void Find_ReturnsRequestedRoleOrDefault()
    {
        Assert.Equal("driver", WebExperienceCatalog.Find("DRIVER").Key);
        Assert.Same(WebExperienceCatalog.DefaultRole, WebExperienceCatalog.Find("unknown"));
    }
}
