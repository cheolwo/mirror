using Ssalddel.Contracts.Admin.Restaurants;

namespace SsalddelAdmin.Services;

/// <summary>현재 편집 정책과 픽업·전달 입력의 수명을 유지하고 저장된 서버 응답으로 교체한다.</summary>
internal sealed class 음식요금편집Session
{
    private readonly Func<음식배달요금정책응답, Task<음식배달요금정책응답>> save;

    public 음식요금편집Session(Func<음식배달요금정책응답, Task<음식배달요금정책응답>> save)
    {
        this.save = save;
        DriverPricing = new(Policy);
    }

    public 음식배달요금정책응답 Policy { get; private set; } = new();
    public FoodDriverPricingEditor DriverPricing { get; private set; }

    public void Replace(음식배달요금정책응답 policy)
    {
        Policy = policy;
        DriverPricing = new(policy);
    }

    public async Task SaveAsync()
    {
        var savedPolicy = await save(Policy);
        Replace(savedPolicy);
    }
}
