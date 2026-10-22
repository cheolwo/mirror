using Ssalddel.Contracts.Common.Versioning;

namespace Ssalddel.Tests.Contracts.Common;

public sealed class OverviewParticipationCapabilityR12Tests
{
    [Theory]
    [InlineData(SsalddelPageAppCodes.IntegratedWeb)]
    [InlineData(SsalddelPageAppCodes.Shipper)]
    public void 공개참여내역은익명읽기전용이며업무실행이나개인원장권한을추가하지않는다(string app)
    {
        Assert.True(SsalddelPageCapabilityCatalog.TryResolve(app, "/overview/participation?page=2", out var capability));
        Assert.Equal(PageCapabilityMatchKind.Exact, capability.MatchKind);
        Assert.Equal("/overview/participation", capability.RoutePattern);
        Assert.Equal(PageCapabilityStage.Live, capability.Stage);
        Assert.Equal(PageInteractionBoundary.ReadOnly, capability.Boundary);
        Assert.False(capability.RequiresAuthentication);
        Assert.False(capability.HasExternalEffects);
        Assert.Empty(capability.FeatureKeys);
    }

    [Fact]
    public void 참여내역의등록은미등록하위주소나전체overview영역을공개하지않는다()
    {
        foreach (var app in new[] { SsalddelPageAppCodes.IntegratedWeb, SsalddelPageAppCodes.Shipper })
        {
            Assert.False(SsalddelPageCapabilityCatalog.TryResolve(app, "/overview/participation/private", out _));
            Assert.False(SsalddelPageCapabilityCatalog.TryResolve(app, "/overview/future", out _));
        }
    }
}
