using 살뜰.Data;
using 살뜰.Services.Versioning;

namespace Ssalddel.Services.Operations;

public interface IMartLastMileDispatchReadinessPolicy
{
    Task<MartLastMileDispatchReadiness> EvaluateAsync(string orderReference, CancellationToken cancellationToken);
}

public sealed record MartLastMileDispatchReadiness(bool IsReady, string Code, string Message)
{
    public static MartLastMileDispatchReadiness Hold(string code, string message) => new(false, code, message);
}

public static class MartLastMileDispatchReadinessCodes
{
    public const string FeatureDisabled = "MartLastMileFeatureDisabled";
    public const string SourceNotBound = "MartLastMileSourceNotBound";
    public const string ConsumerBindingUnavailable = "MartLastMileConsumerBindingUnavailable";
    public const string PreparationNotReady = "MartLastMilePreparationNotReady";
    public const string Ready = "MartLastMileReady";
}

/// <summary>공통 창고 포장을 마트 기사 배차로 오분류하거나 연결되지 않은 자식 업무를 수락하지 않습니다.</summary>
public sealed class MartLastMileDispatchReadinessPolicy(
    SsalddelContext db,
    IVersionFeatureFlagService features,
    I마트라스트마일원천Adapter? sourceAdapter = null,
    I마트기사업무Gate? consumerGate = null) : IMartLastMileDispatchReadinessPolicy
{
    private readonly I마트라스트마일원천Adapter _sourceAdapter = sourceAdapter ?? new 마트라스트마일원천Adapter(db);

    public async Task<MartLastMileDispatchReadiness> EvaluateAsync(string orderReference, CancellationToken cancellationToken)
    {
        if (!features.IsEnabled(VersionFeatureFlagKeys.SsalddelMartWorkflow)
            || !features.IsEnabled(VersionFeatureFlagKeys.FoodDeliveryWorkflow)
            || !features.IsEnabled(VersionFeatureFlagKeys.WarehouseFulfillmentWorkflow))
            return MartLastMileDispatchReadiness.Hold(MartLastMileDispatchReadinessCodes.FeatureDisabled,
                "마트 라스트마일 연결 기능이 비활성화되어 기사 인계를 보류했습니다. 창고 포장 결과는 유지됩니다.");

        var source = await _sourceAdapter.조회Async(orderReference, cancellationToken);
        if (!source.원천결속됨) return SourceNotBound();

        // 기존 2/3인자 호출은 기본 보류를 유지합니다. 새 전용 consumer의 Simulation 관문을 명시해야 합니다.
        // 같은 주문번호의 음식주문이나 포장 문자열만으로 기사 소비자 결속을 인정하지 않습니다.
        if (consumerGate?.Enabled == true)
            return source.픽업준비완료
                ? new(true, MartLastMileDispatchReadinessCodes.Ready, "마트 전용 기사 업무의 Simulation 연결을 준비했습니다.")
                : MartLastMileDispatchReadiness.Hold(MartLastMileDispatchReadinessCodes.PreparationNotReady,
                    "현재 전체 피킹·포장 근거를 확인할 때까지 기사 인계를 보류했습니다.");
        return MartLastMileDispatchReadiness.Hold(MartLastMileDispatchReadinessCodes.ConsumerBindingUnavailable,
            "마트 포장 주문을 수행·완료할 기사 연결이 아직 지원되지 않아 인계를 보류했습니다. 창고 포장 결과는 유지됩니다.");
    }

    private static MartLastMileDispatchReadiness SourceNotBound()
        => MartLastMileDispatchReadiness.Hold(MartLastMileDispatchReadinessCodes.SourceNotBound,
            "실제 마트 주문과 같은 판매자·주문자의 출고 연결을 확인하지 못해 기사 인계를 보류했습니다.");
}
