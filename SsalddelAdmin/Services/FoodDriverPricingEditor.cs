using Ssalddel.Contracts.Admin.Restaurants;

namespace SsalddelAdmin.Services;

/// <summary>화면의 두 기본비 입력을 기존 총액/픽업비 계약에 매핑한다. 요금 계산/저장을 소유하지 않는다.</summary>
public sealed class FoodDriverPricingEditor(음식배달요금정책응답 policy)
{
    public bool SplitEnabled
    {
        get => policy.DriverPickupPayout.HasValue;
        set => policy.DriverPickupPayout = value ? policy.DriverPickupPayout ?? 0m : null;
    }

    public decimal PickupFee
    {
        get => policy.DriverPickupPayout ?? 0m;
        set
        {
            RequireSplit();
            var dropoff = DropoffFee;
            policy.DriverPickupPayout = value;
            policy.DriverBasePayout = value + dropoff;
        }
    }

    public decimal DropoffFee
    {
        get
        {
            RequireSplit();
            return policy.DriverBasePayout - policy.DriverPickupPayout!.Value;
        }
        set
        {
            RequireSplit();
            policy.DriverBasePayout = policy.DriverPickupPayout!.Value + value;
        }
    }

    private void RequireSplit()
    {
        if (!SplitEnabled)
            throw new InvalidOperationException("기존 미분리 기본액의 픽업·전달비는 확정되지 않았습니다.");
    }
}
