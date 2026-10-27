using Ssalddel.Application.CommandProcessing;
using Ssalddel.Contracts.Common.Versioning;

namespace Ssalddel.Tests.CommandProcessing;

public sealed class Command기능버전CatalogTests
{
    [Fact]
    public void Command기능버전은_제품로드맵의현재버전과문화교통이름을재사용한다()
    {
        Assert.Equal(
            SsalddelProductRoadmapCatalog.CurrentVersion,
            Command기능버전Catalog.CurrentRelease);
        Assert.Equal(
            SsalddelProductRoadmapCatalog.DeploymentTargetVersion,
            Command기능버전Catalog.DeploymentTarget);
        Assert.Equal("1.0", Command기능버전Catalog.DeploymentTarget);
        Assert.Equal("통합 앱 1.0", Command기능버전Catalog.UnifiedDeploymentTarget.DisplayName);

        var current = Command기능버전Catalog.Get(null);
        var foundation = Command기능버전Catalog.Get(
            SsalddelProductRoadmapCatalog.FoundationVersion);

        Assert.True(current.IsCurrentRelease);
        Assert.Equal(
            SsalddelProductRoadmapCatalog
                .Find(SsalddelProductRoadmapCatalog.FoundationVersion)
                .FullDisplayName,
            current.DisplayName);
        Assert.Equal(
            "문화교통 0.0 · 커뮤니티·공공데이터 기반",
            foundation.DisplayName);
    }

    [Fact]
    public void 운송Command는_과거1점0이아닌_2점0으로분류한다()
    {
        Assert.All(
            Command기능대상Catalog.DriverCommands,
            command => Assert.Equal(
                SsalddelProductRoadmapCatalog.TransportVersion,
                command.Version));
        Assert.All(
            Command기능정책Catalog.All,
            policy => Assert.Equal(
                SsalddelProductRoadmapCatalog.FoundationVersion,
                policy.Version));
    }
}
