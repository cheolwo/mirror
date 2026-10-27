using Ssalddel.Contracts.Common.Versioning;
namespace Ssalddel.Tests.Contracts.Common;

public sealed class RoleWorkspaceCapabilityTests
{
    [Theory]
    [InlineData(SsalddelPageAppCodes.IntegratedWeb)]
    [InlineData(SsalddelPageAppCodes.Shipper)]
    public void Recommendation_alert_preferences_require_authentication_and_only_persist_own_settings(string app)
    {
        Assert.True(SsalddelPageCapabilityCatalog.TryResolve(app, "/driver/notifications/settings", out var settings));
        Assert.True(settings.RequiresAuthentication);
        Assert.Equal(PageInteractionBoundary.PlatformPersistence, settings.Boundary);
        Assert.Equal(PageCapabilityStage.Beta, settings.Stage);
    }

    [Theory]
    [InlineData(SsalddelPageAppCodes.IntegratedWeb)]
    [InlineData(SsalddelPageAppCodes.Shipper)]
    public void Current_driver_work_is_authenticated_read_only_without_new_execution_permission(string app)
    {
        Assert.True(SsalddelPageCapabilityCatalog.TryResolve(app, "/driver/current-work", out var current));
        Assert.Equal(PageInteractionBoundary.ReadOnly, current.Boundary);
        Assert.Equal(PageCapabilityStage.Beta, current.Stage);
        Assert.True(current.RequiresAuthentication);
    }

    [Theory]
    [InlineData(SsalddelPageAppCodes.IntegratedWeb)]
    [InlineData(SsalddelPageAppCodes.Shipper)]
    public void Role_workspace_preserves_food_community_and_authentication_boundaries(string app)
    {
        Assert.True(SsalddelPageCapabilityCatalog.TryResolve(app, "/workspace/community", out var community));
        Assert.Equal(PageInteractionBoundary.ReadOnly, community.Boundary);
        Assert.Contains("CommunityTrustWorkflow", community.FeatureKeys);
        Assert.True(SsalddelPageCapabilityCatalog.TryResolve(app, "/workspace/food-driver", out var food));
        Assert.Contains("FoodDeliveryWorkflow", food.FeatureKeys);
        Assert.True(SsalddelPageCapabilityCatalog.TryResolve(app, "/workspace-login/operator", out var login));
        Assert.False(login.RequiresAuthentication);
        Assert.Empty(login.FeatureKeys);
        Assert.True(SsalddelPageCapabilityCatalog.TryResolve(app, "/workspace/cargo-driver/transports/12/pickup", out var cargo));
        Assert.DoesNotContain("FoodDeliveryWorkflow", cargo.FeatureKeys);
    }
}
