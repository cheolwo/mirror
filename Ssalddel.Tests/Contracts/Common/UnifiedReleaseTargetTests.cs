using Microsoft.Extensions.Options;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Contracts.Common.Versioning;
using 살뜰.Services.Options;
using 살뜰.Services.Versioning;

namespace Ssalddel.Tests.Contracts.Common;

public sealed class UnifiedReleaseTargetTests
{
    [Fact]
    public void 단일1점0은_전체기존분야를_별도안정키의완성업무로포함한다()
    {
        var target = SsalddelProductRoadmapCatalog.UnifiedReleaseTarget;

        Assert.Equal("unified-app-1.0", target.ReleaseKey);
        Assert.Equal("1.0", target.Version);
        Assert.Equal("통합 앱 1.0", target.DisplayName);
        Assert.Equal("Ssalddel.slnx", target.BuildSolution);
        Assert.Same(SsalddelProductRoadmapCatalog.UnifiedReleaseWorkstreams, target.Workstreams);
        Assert.Equal(8, target.Workstreams.Count);
        Assert.Equal(
            SsalddelProductRoadmapCatalog.All.Select(stage => stage.Version),
            target.Workstreams.Select(workstream => workstream.LegacyVersion));
        Assert.Equal(target.Workstreams.Count, target.Workstreams.Select(item => item.WorkstreamKey).Distinct().Count());
        Assert.All(target.Workstreams, item =>
        {
            Assert.False(string.IsNullOrWhiteSpace(item.WorkstreamKey));
            Assert.False(string.IsNullOrWhiteSpace(item.DisplayName));
            Assert.False(string.IsNullOrWhiteSpace(item.CompletionCriteria));
            Assert.NotEmpty(item.AppCodes);
            Assert.True(item.RequiresIndependentCompletionEvidence);
        });
    }

    [Theory]
    [InlineData("community-foundation", "0.0")]
    [InlineData("individual-order", "0.5")]
    [InlineData("group-purchase", "1.0")]
    [InlineData("supply-readiness", "1.5")]
    [InlineData("cargo-transport", "2.0")]
    [InlineData("warehouse-sales", "2.5")]
    [InlineData("food-delivery", "3.0")]
    [InlineData("mart-logistics", "3.5")]
    public void 각분야의_기존분류코드는_통합목표안에서_호환보존한다(string key, string legacyVersion)
    {
        var workstream = Assert.Single(SsalddelProductRoadmapCatalog.UnifiedReleaseWorkstreams,
            item => item.WorkstreamKey == key);

        Assert.Equal(legacyVersion, workstream.LegacyVersion);
        Assert.Equal(legacyVersion, SsalddelProductRoadmapCatalog.Find(legacyVersion).Version);
    }

    [Fact]
    public void 숫자1점0의_공동주문호환조회와_통합제품목표는_서로다른타입이다()
    {
        SsalddelProductRoadmapStage legacy = SsalddelProductRoadmapCatalog.Find("1.0");
        SsalddelUnifiedReleaseTarget unified = SsalddelProductRoadmapCatalog.UnifiedReleaseTarget;

        Assert.Equal(SsalddelProductRoadmapCatalog.LegacyGroupPurchaseVersion, legacy.Version);
        Assert.Equal("같이 주문·주문자 집단화", legacy.DisplayName);
        Assert.Equal("통합 앱 1.0", unified.DisplayName);
        Assert.Equal(legacy.Version, unified.Version);
        Assert.Equal("문화교통 1.0 · 같이 주문·주문자 집단화", Command기능버전Catalog.Get("1.0").DisplayName);
        Assert.Same(unified, Command기능버전Catalog.UnifiedDeploymentTarget);
    }

    [Fact]
    public void 목표선언은_완료근거나_운영활성화나_현재공개기준의변경이아니다()
    {
        var flags = new VersionFeatureFlagsOptions();
        var service = new VersionFeatureFlagService(new StaticOptionsMonitor(flags));
        var before = service.GetAll().ToDictionary(item => item.Key, item => item.Value);

        var target = SsalddelProductRoadmapCatalog.UnifiedReleaseTarget;

        Assert.False(target.IsCompletionEvidence);
        Assert.False(target.GrantsOperationalActivation);
        Assert.Equal("0.0", SsalddelProductRoadmapCatalog.CurrentDeliveryVersion);
        Assert.Equal("0.0", SsalddelProductRoadmapCatalog.CurrentVersion);
        Assert.Equal("0.0", Command기능버전Catalog.CurrentRelease);
        Assert.True(service.IsEnabled(VersionFeatureFlagKeys.CommunityTrustWorkflow));
        Assert.False(service.IsEnabled(VersionFeatureFlagKeys.DomesticTransportWorkflow));
        Assert.False(service.IsEnabled(VersionFeatureFlagKeys.WarehouseFulfillmentWorkflow));
        Assert.False(service.IsEnabled(VersionFeatureFlagKeys.FoodDeliveryWorkflow));
        Assert.False(service.IsEnabled(VersionFeatureFlagKeys.SsalddelMartWorkflow));
        Assert.False(service.IsEnabled(VersionFeatureFlagKeys.PlatformOperationsControl));
        Assert.Equal(before.OrderBy(item => item.Key), service.GetAll().OrderBy(item => item.Key));
    }

    [Theory]
    [InlineData("shipper-request", "2.0")]
    [InlineData("shipper-warehouse", "2.5")]
    [InlineData("community-group-purchase-public", "1.0")]
    public void 통합목표로_기존페이지의도입분류를_다시쓰지않는다(string pageKey, string legacyVersion)
    {
        var capability = Assert.Single(SsalddelPageCapabilityCatalog.GetAll(), item => item.PageKey == pageKey);
        Assert.Equal(legacyVersion, capability.IntroducedVersion);
    }

    [Theory]
    [InlineData("SsalddelApp/SsalddelApp.csproj")]
    [InlineData("DriverApp/DriverApp.csproj")]
    [InlineData("FDriverApp/FDriverApp.csproj")]
    [InlineData("OrdererApp/OrdererApp.csproj")]
    [InlineData("RestaurantDeskApp/RestaurantDeskApp.csproj")]
    [InlineData("WarehouseManagerApp/WarehouseManagerApp.csproj")]
    [InlineData("SellerApp/SellerApp.csproj")]
    [InlineData("HumanResourcesManagerApp/HumanResourcesManagerApp.csproj")]
    [InlineData("SsalddelAdminApp/SsalddelAdminApp.csproj")]
    public void 단일완성목표의_빌드진입점은_기존전체역할앱을포함한다(string project)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Ssalddel.slnx")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        var solution = File.ReadAllText(Path.Combine(directory.FullName,
            SsalddelProductRoadmapCatalog.UnifiedReleaseTarget.BuildSolution));
        Assert.Contains($"Project Path=\"{project}\"", solution);
    }

    private sealed class StaticOptionsMonitor(VersionFeatureFlagsOptions value) : IOptionsMonitor<VersionFeatureFlagsOptions>
    {
        public VersionFeatureFlagsOptions CurrentValue => value;
        public VersionFeatureFlagsOptions Get(string? name) => value;
        public IDisposable? OnChange(Action<VersionFeatureFlagsOptions, string?> listener) => null;
    }
}
