using Microsoft.Extensions.Options;
using Ssalddel.Infrastructure.BackgroundJobs;
using Ssalddel.Services.LogisticsProcessing.SalesOrders;
using 살뜰.Services.Options;
using 살뜰.Services.Versioning;

namespace Ssalddel.Tests.Infrastructure.BackgroundJobs;

public sealed class SsalddelBackgroundJobActivationPolicyTests
{
    [Theory]
    [InlineData(
        SsalddelBackgroundWorkloadKeys.FoodDeliveryDispatch,
        VersionFeatureFlagKeys.FoodDeliveryWorkflow)]
    [InlineData(
        SsalddelBackgroundWorkloadKeys.DomesticTransportDispatch,
        VersionFeatureFlagKeys.DomesticTransportWorkflow)]
    [InlineData(
        SsalddelBackgroundWorkloadKeys.CustomsStatusSync,
        VersionFeatureFlagKeys.CustomsAndTradeDataWorkflow)]
    [InlineData(
        SsalddelBackgroundWorkloadKeys.SalesChannelOrderSync,
        VersionFeatureFlagKeys.SalesChannelFulfillmentWorkflow)]
    public void Operational이고_해당Workflow가활성화되면_실행을허용한다(
        string workloadKey,
        string featureKey)
    {
        var policy = CreatePolicy(
            SsalddelExecutionMode.Operational,
            [featureKey],
            salesSyncEnabled: true);

        var result = policy.Evaluate(workloadKey);

        Assert.True(result.IsEnabled);
        Assert.Equal(SsalddelBackgroundWorkloadActivationCodes.Enabled, result.Code);
        Assert.Equal(featureKey, result.FeatureKey);
    }

    [Fact]
    public void Simulation에서는_기능이켜져도_외부효과배치를차단한다()
    {
        var policy = CreatePolicy(
            SsalddelExecutionMode.Simulation,
            [VersionFeatureFlagKeys.DomesticTransportWorkflow],
            salesSyncEnabled: true);

        var result = policy.Evaluate(
            SsalddelBackgroundWorkloadKeys.DomesticTransportDispatch);

        Assert.False(result.IsEnabled);
        Assert.Equal(
            SsalddelBackgroundWorkloadActivationCodes.OperationalModeRequired,
            result.Code);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void 음식과화물은_각자Workflow로만_활성화된다(bool foodEnabled, bool cargoEnabled)
    {
        var features = new List<string>();
        if (foodEnabled) features.Add(VersionFeatureFlagKeys.FoodDeliveryWorkflow);
        if (cargoEnabled) features.Add(VersionFeatureFlagKeys.DomesticTransportWorkflow);
        var policy = CreatePolicy(SsalddelExecutionMode.Operational, features, true);

        var food = policy.Evaluate(SsalddelBackgroundWorkloadKeys.FoodDeliveryDispatch);
        var cargo = policy.Evaluate(SsalddelBackgroundWorkloadKeys.DomesticTransportDispatch);

        Assert.Equal(foodEnabled, food.IsEnabled);
        Assert.Equal(cargoEnabled, cargo.IsEnabled);
        Assert.Equal(VersionFeatureFlagKeys.FoodDeliveryWorkflow, food.FeatureKey);
        Assert.Equal(VersionFeatureFlagKeys.DomesticTransportWorkflow, cargo.FeatureKey);
    }

    [Fact]
    public void Simulation에서는_음식과화물모두켜도_두배치를차단한다()
    {
        var policy = CreatePolicy(SsalddelExecutionMode.Simulation,
            [VersionFeatureFlagKeys.FoodDeliveryWorkflow, VersionFeatureFlagKeys.DomesticTransportWorkflow], true);
        foreach (var workload in new[] { SsalddelBackgroundWorkloadKeys.FoodDeliveryDispatch,
                     SsalddelBackgroundWorkloadKeys.DomesticTransportDispatch })
        {
            var result = policy.Evaluate(workload);
            Assert.False(result.IsEnabled);
            Assert.Equal(SsalddelBackgroundWorkloadActivationCodes.OperationalModeRequired, result.Code);
        }
    }

    [Fact]
    public void Workflow기능이꺼져있으면_Operational에서도_차단한다()
    {
        var policy = CreatePolicy(
            SsalddelExecutionMode.Operational,
            [],
            salesSyncEnabled: true);

        var result = policy.Evaluate(
            SsalddelBackgroundWorkloadKeys.CustomsStatusSync);

        Assert.False(result.IsEnabled);
        Assert.Equal(
            SsalddelBackgroundWorkloadActivationCodes.FeatureDisabled,
            result.Code);
        Assert.Equal(
            VersionFeatureFlagKeys.CustomsAndTradeDataWorkflow,
            result.FeatureKey);
    }

    [Fact]
    public void 판매채널동기화자체설정이꺼져있으면_Workflow가켜져도_차단한다()
    {
        var policy = CreatePolicy(
            SsalddelExecutionMode.Operational,
            [VersionFeatureFlagKeys.SalesChannelFulfillmentWorkflow],
            salesSyncEnabled: false);

        var result = policy.Evaluate(
            SsalddelBackgroundWorkloadKeys.SalesChannelOrderSync);

        Assert.False(result.IsEnabled);
        Assert.Equal(
            SsalddelBackgroundWorkloadActivationCodes.WorkloadDisabled,
            result.Code);
    }

    [Fact]
    public void 등록되지않은작업키는_기본차단한다()
    {
        var policy = CreatePolicy(
            SsalddelExecutionMode.Operational,
            [],
            salesSyncEnabled: true);

        var result = policy.Evaluate("UnknownWorkload");

        Assert.False(result.IsEnabled);
        Assert.Equal(
            SsalddelBackgroundWorkloadActivationCodes.UnknownWorkload,
            result.Code);
        Assert.Empty(result.FeatureKey);
    }

    private static SsalddelBackgroundJobActivationPolicy CreatePolicy(
        SsalddelExecutionMode mode,
        IReadOnlyCollection<string> enabledFeatures,
        bool salesSyncEnabled)
        => new(
            new StaticExecutionModePolicy(mode),
            new StaticFeatureFlagService(enabledFeatures),
            new StaticOptionsMonitor<SalesChannelOrderSyncOptions>(
                new SalesChannelOrderSyncOptions { Enabled = salesSyncEnabled }));

    private sealed class StaticExecutionModePolicy(SsalddelExecutionMode mode)
        : ISsalddelExecutionModePolicy
    {
        public SsalddelExecutionMode Mode { get; } = mode;
        public bool IsSimulation => Mode == SsalddelExecutionMode.Simulation;
        public bool IsOperational => Mode == SsalddelExecutionMode.Operational;
    }

    private sealed class StaticFeatureFlagService(
        IReadOnlyCollection<string> enabledFeatures) : IVersionFeatureFlagService
    {
        private readonly HashSet<string> _enabled = new(
            enabledFeatures,
            StringComparer.OrdinalIgnoreCase);

        public bool IsEnabled(string featureKey) => _enabled.Contains(featureKey);

        public IReadOnlyDictionary<string, bool> GetAll()
            => _enabled.ToDictionary(key => key, _ => true, StringComparer.OrdinalIgnoreCase);
    }

    private sealed class StaticOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue => value;
        public T Get(string? name) => value;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
