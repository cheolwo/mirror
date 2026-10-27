using MudBlazor;
using Ssalddel.Contracts.Admin.Restaurants;

namespace SsalddelAdmin.Services;

/// <summary>요금 편집과 리뷰 조회를 조립하며 기존 페이지의 공동 로딩·저장 제한을 유지한다.</summary>
internal sealed class 음식운영PageState
{
    private readonly Func<Task<음식배달요금정책응답>> readPricingPolicy;
    private readonly 음식리뷰운영Reader reviewReader;

    public 음식운영PageState(음식운영Service service)
        : this(
            () => service.배달요금정책조회Async(),
            new(() => service.리뷰운영목록조회Async(), () => service.운영정책조회Async()),
            policy => service.배달요금정책수정Async(policy))
    {
    }

    internal 음식운영PageState(
        Func<Task<음식배달요금정책응답>> readPricingPolicy,
        음식리뷰운영Reader reviewReader,
        Func<음식배달요금정책응답, Task<음식배달요금정책응답>> savePricingPolicy)
    {
        this.readPricingPolicy = readPricingPolicy;
        this.reviewReader = reviewReader;
        Pricing = new(savePricingPolicy);
    }

    public 음식요금편집Session Pricing { get; }
    public 음식리뷰운영Snapshot Reviews { get; private set; } = new(new(), new());
    public bool IsLoading { get; private set; } = true;
    public bool HasLoadedData { get; private set; }
    public bool IsSaving { get; private set; }
    public string? Message { get; private set; }
    public Severity MessageSeverity { get; private set; } = Severity.Info;

    public async Task LoadAsync()
    {
        IsLoading = true;
        HasLoadedData = false;
        Message = null;
        try
        {
            var loadedPricingPolicy = await readPricingPolicy();
            var loadedReviews = await reviewReader.ReadAsync();
            Pricing.Replace(loadedPricingPolicy);
            Reviews = loadedReviews;
            HasLoadedData = true;
        }
        catch (Exception ex)
        {
            MessageSeverity = Severity.Error;
            Message = $"음식 운영 정보를 불러오지 못했습니다: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task SavePricingPolicyAsync()
    {
        if (IsSaving || IsLoading || !HasLoadedData)
        {
            return;
        }

        IsSaving = true;
        Message = null;
        try
        {
            await Pricing.SaveAsync();
            MessageSeverity = Severity.Success;
            Message = "배달 요금 정책을 저장했습니다. 이후 새 제안부터 적용되며 기존 제안 금액은 유지됩니다.";
        }
        catch (Exception ex)
        {
            MessageSeverity = Severity.Error;
            Message = $"정책 저장에 실패했습니다: {ex.Message}";
        }
        finally
        {
            IsSaving = false;
        }
    }
}
