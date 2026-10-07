using Microsoft.Extensions.Options;
using Ssalddel.Services.LogisticsProcessing.SalesOrders;
using 살뜰.Services.Options;
using 살뜰.Services.Versioning;

namespace Ssalddel.Infrastructure.BackgroundJobs;

public static class SsalddelBackgroundWorkloadKeys
{
    public const string FoodDeliveryDispatch = nameof(FoodDeliveryDispatch);
    public const string DomesticTransportDispatch = nameof(DomesticTransportDispatch);
    public const string CustomsStatusSync = nameof(CustomsStatusSync);
    public const string SalesChannelOrderSync = nameof(SalesChannelOrderSync);
}

public static class SsalddelBackgroundWorkloadActivationCodes
{
    public const string Enabled = nameof(Enabled);
    public const string OperationalModeRequired = nameof(OperationalModeRequired);
    public const string FeatureDisabled = nameof(FeatureDisabled);
    public const string WorkloadDisabled = nameof(WorkloadDisabled);
    public const string UnknownWorkload = nameof(UnknownWorkload);
}

public readonly record struct SsalddelBackgroundWorkloadActivation(
    bool IsEnabled,
    string Code,
    string FeatureKey);

public interface ISsalddelBackgroundJobActivationPolicy
{
    SsalddelBackgroundWorkloadActivation Evaluate(string workloadKey);
}

/// <summary>
/// 외부 조회나 업무 상태 변경을 일으키는 자동 작업의 공통 실행 경계입니다.
/// Job 등록 여부와 무관하게 실행 직전에 Operational 모드와 해당 workflow 공개 상태를 다시 확인합니다.
/// </summary>
public sealed class SsalddelBackgroundJobActivationPolicy : ISsalddelBackgroundJobActivationPolicy
{
    private readonly ISsalddelExecutionModePolicy _executionMode;
    private readonly IVersionFeatureFlagService _featureFlags;
    private readonly IOptionsMonitor<SalesChannelOrderSyncOptions> _salesOrderSyncOptions;

    public SsalddelBackgroundJobActivationPolicy(
        ISsalddelExecutionModePolicy executionMode,
        IVersionFeatureFlagService featureFlags,
        IOptionsMonitor<SalesChannelOrderSyncOptions> salesOrderSyncOptions)
    {
        _executionMode = executionMode;
        _featureFlags = featureFlags;
        _salesOrderSyncOptions = salesOrderSyncOptions;
    }

    public SsalddelBackgroundWorkloadActivation Evaluate(string workloadKey)
    {
        var featureKey = ResolveFeatureKey(workloadKey);
        if (string.IsNullOrWhiteSpace(featureKey))
        {
            return new(
                IsEnabled: false,
                SsalddelBackgroundWorkloadActivationCodes.UnknownWorkload,
                string.Empty);
        }

        if (!_executionMode.IsOperational)
        {
            return new(
                IsEnabled: false,
                SsalddelBackgroundWorkloadActivationCodes.OperationalModeRequired,
                featureKey);
        }

        if (!_featureFlags.IsEnabled(featureKey))
        {
            return new(
                IsEnabled: false,
                SsalddelBackgroundWorkloadActivationCodes.FeatureDisabled,
                featureKey);
        }

        if (string.Equals(
                workloadKey,
                SsalddelBackgroundWorkloadKeys.SalesChannelOrderSync,
                StringComparison.Ordinal)
            && !_salesOrderSyncOptions.CurrentValue.Enabled)
        {
            return new(
                IsEnabled: false,
                SsalddelBackgroundWorkloadActivationCodes.WorkloadDisabled,
                featureKey);
        }

        return new(
            IsEnabled: true,
            SsalddelBackgroundWorkloadActivationCodes.Enabled,
            featureKey);
    }

    private static string ResolveFeatureKey(string workloadKey)
        => workloadKey switch
        {
            SsalddelBackgroundWorkloadKeys.FoodDeliveryDispatch =>
                VersionFeatureFlagKeys.FoodDeliveryWorkflow,
            SsalddelBackgroundWorkloadKeys.DomesticTransportDispatch =>
                VersionFeatureFlagKeys.DomesticTransportWorkflow,
            SsalddelBackgroundWorkloadKeys.CustomsStatusSync =>
                VersionFeatureFlagKeys.CustomsAndTradeDataWorkflow,
            SsalddelBackgroundWorkloadKeys.SalesChannelOrderSync =>
                VersionFeatureFlagKeys.SalesChannelFulfillmentWorkflow,
            _ => string.Empty
        };
}
