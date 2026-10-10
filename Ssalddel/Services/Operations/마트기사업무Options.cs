using Microsoft.Extensions.Options;
using 살뜰.Services.Options;
using 살뜰.Services.Versioning;

namespace Ssalddel.Services.Operations;

public sealed class 마트기사업무Options
{
    public const string SectionName = "MartDriverWorkflow";
    public bool Enabled { get; set; }
}

public interface I마트기사업무Gate { bool Enabled { get; } }

/// <summary>r58은 명시적인 Simulation 검증 전용입니다. 운영 기본 OFF와 기존 세 기능 관문을 보존합니다.</summary>
public sealed class 마트기사업무Gate(
    IOptions<마트기사업무Options> options,
    IOptions<SsalddelExecutionOptions> execution,
    IVersionFeatureFlagService features) : I마트기사업무Gate
{
    public bool Enabled => options.Value.Enabled
        && execution.Value.Mode == SsalddelExecutionMode.Simulation && !execution.Value.DevelopmentReadOnly
        && features.IsEnabled(VersionFeatureFlagKeys.SsalddelMartWorkflow)
        && features.IsEnabled(VersionFeatureFlagKeys.FoodDeliveryWorkflow)
        && features.IsEnabled(VersionFeatureFlagKeys.WarehouseFulfillmentWorkflow);
}
