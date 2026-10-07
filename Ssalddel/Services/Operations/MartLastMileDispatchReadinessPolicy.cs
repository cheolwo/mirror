using Microsoft.EntityFrameworkCore;
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
}

/// <summary>공통 창고 포장을 마트 기사 배차로 오분류하거나 연결되지 않은 자식 업무를 수락하지 않습니다.</summary>
public sealed class MartLastMileDispatchReadinessPolicy(
    SsalddelContext db,
    IVersionFeatureFlagService features) : IMartLastMileDispatchReadinessPolicy
{
    public async Task<MartLastMileDispatchReadiness> EvaluateAsync(string orderReference, CancellationToken cancellationToken)
    {
        if (!features.IsEnabled(VersionFeatureFlagKeys.SsalddelMartWorkflow)
            || !features.IsEnabled(VersionFeatureFlagKeys.FoodDeliveryWorkflow)
            || !features.IsEnabled(VersionFeatureFlagKeys.WarehouseFulfillmentWorkflow))
            return MartLastMileDispatchReadiness.Hold(MartLastMileDispatchReadinessCodes.FeatureDisabled,
                "마트 라스트마일 연결 기능이 비활성화되어 기사 인계를 보류했습니다. 창고 포장 결과는 유지됩니다.");

        if (string.IsNullOrWhiteSpace(orderReference)) return SourceNotBound();
        var orderRef = orderReference.Trim();
        var orders = await db.마트주문.AsNoTracking()
            .Where(order => order.주문참조번호 == orderRef)
            .Take(2).ToListAsync(cancellationToken);
        if (orders.Count != 1 || string.IsNullOrWhiteSpace(orders[0].판매자UserId)
            || string.IsNullOrWhiteSpace(orders[0].주문자UserId))
            return SourceNotBound();

        var source = orders[0];
        var outbounds = await db.출고예정.AsNoTracking()
            .Where(plan => plan.주문참조번호 == orderRef)
            .Select(plan => new { plan.판매자UserId, plan.주문자UserId })
            .ToListAsync(cancellationToken);
        if (outbounds.Count == 0 || outbounds.Any(plan => plan.판매자UserId != source.판매자UserId
            || plan.주문자UserId != source.주문자UserId))
            return SourceNotBound();

        // 현재 등록된 음식기사 consumer는 음식주문과 음식배달시도를 요구하며
        // SsalddelMartPackedOrder의 원본·시도·완료 결과를 결속하는 adapter는 없습니다.
        // 같은 주문번호의 음식주문 행만으로 다른 업무 원천의 결속을 인정하지 않습니다.
        return MartLastMileDispatchReadiness.Hold(MartLastMileDispatchReadinessCodes.ConsumerBindingUnavailable,
            "마트 포장 주문을 수행·완료할 기사 연결이 아직 지원되지 않아 인계를 보류했습니다. 창고 포장 결과는 유지됩니다.");
    }

    private static MartLastMileDispatchReadiness SourceNotBound()
        => MartLastMileDispatchReadiness.Hold(MartLastMileDispatchReadinessCodes.SourceNotBound,
            "실제 마트 주문과 같은 판매자·주문자의 출고 연결을 확인하지 못해 기사 인계를 보류했습니다.");
}
