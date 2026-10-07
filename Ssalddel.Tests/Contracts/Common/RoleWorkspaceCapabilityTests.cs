using Ssalddel.Contracts.Common.Versioning;
namespace Ssalddel.Tests.Contracts.Common;

public sealed class RoleWorkspaceCapabilityTests
{
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
