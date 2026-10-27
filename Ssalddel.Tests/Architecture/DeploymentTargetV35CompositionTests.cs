using Ssalddel.Application.CommandProcessing;
using Ssalddel.Contracts.Common.Versioning;

namespace Ssalddel.Tests.Architecture;

public sealed class DeploymentTargetV35CompositionTests
{
    [Fact]
    public void 제품로드맵은_기존공개기준을보존하고_전체완성목표를_통합1점0으로묶는다()
    {
        Assert.Equal(
            SsalddelProductRoadmapCatalog.FoundationVersion,
            SsalddelProductRoadmapCatalog.CurrentDeliveryVersion);
        Assert.Equal(
            SsalddelProductRoadmapCatalog.UnifiedReleaseVersion,
            SsalddelProductRoadmapCatalog.DeploymentTargetVersion);
        Assert.Equal(
            SsalddelProductRoadmapCatalog.DeploymentTargetVersion,
            Command기능버전Catalog.DeploymentTarget);
        Assert.Same(
            SsalddelProductRoadmapCatalog.UnifiedReleaseTarget,
            Command기능버전Catalog.UnifiedDeploymentTarget);
    }

    [Theory]
    [InlineData("OrdererApp/OrdererApp.csproj")]
    [InlineData("SsalddelApp/SsalddelApp.csproj")]
    [InlineData("DriverApp/DriverApp.csproj")]
    [InlineData("WarehouseManagerApp/WarehouseManagerApp.csproj")]
    [InlineData("RestaurantDeskApp/RestaurantDeskApp.csproj")]
    [InlineData("FDriverApp/FDriverApp.csproj")]
    [InlineData("Ssalddel/Ssalddel.csproj")]
    [InlineData("Ssalddel.WebApp/Ssalddel.WebApp.csproj")]
    [InlineData("Ssalddel.Tests/Ssalddel.Tests.csproj")]
    public void 삼점오_solution은_전체역할과_검증프로젝트를조립한다(string projectPath)
    {
        var solution = Read("Ssalddel.v3.5.slnx");

        Assert.Contains($"Project Path=\"{projectPath}\"", solution);
    }

    [Fact]
    public void Release_CI는_삼점오를빌드하고_마트배포bundle을생성한다()
    {
        var workflow = Read(".github/workflows/release-readiness.yml");

        Assert.Contains("dotnet workload restore Ssalddel.v3.5.slnx", workflow);
        Assert.Contains("dotnet restore Ssalddel.v3.5.slnx", workflow);
        Assert.Contains("dotnet build Ssalddel.v3.5.slnx", workflow);
        Assert.Contains("compose.mart-v35.override.yaml", workflow);
        Assert.Contains("mart-v35-deployment-${{ github.run_number }}", workflow);
        Assert.DoesNotContain("Package Azure 1.0 deployment bundle", workflow);
    }

    [Fact]
    public void 삼점오배포script는_공통rollback절차에_마트Profile을전달한다()
    {
        var profileScript = Read("deploy/azure-vm/deploy-preview-profile.sh");
        var martScript = Read("deploy/azure-vm/deploy-mart-v35.sh");

        Assert.Contains("mart-v35", profileScript);
        Assert.Contains("deploy-preview-profile.sh", martScript);
        Assert.Contains("mart-v35", martScript);
    }

    private static string Read(string relativePath)
        => File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            relativePath.Replace('/', Path.DirectorySeparatorChar)));

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Ssalddel.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Ssalddel 저장소 루트를 찾지 못했습니다.");
    }
}
