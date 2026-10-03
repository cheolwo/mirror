using FluentResults;
using Ssalddel.Contracts.Common.Finance;
using 살뜰.도메인.음식;

namespace Ssalddel.Application.Admin.Restaurants;

public interface I음식배달지급검토UseCase
{
    Result<FoodDeliveryPayoutPricingReviewResponse> 건별계산(FoodDeliveryPayoutPricingReviewRequest request);
    Result<FoodDeliverySettlementReviewResponse> 기간계산(FoodDeliverySettlementReviewRequest request);
}

// 저장소·지급 서비스에 의존하지 않으며 검토 입력을 운영 정책으로 적용하지 않는다.
public sealed class 음식배달지급검토UseCase : I음식배달지급검토UseCase
{
    public Result<FoodDeliveryPayoutPricingReviewResponse> 건별계산(FoodDeliveryPayoutPricingReviewRequest request)
        => Calculate(() => 음식배달지급검토Policy.건별계산(request));

    public Result<FoodDeliverySettlementReviewResponse> 기간계산(FoodDeliverySettlementReviewRequest request)
        => Calculate(() => 음식배달지급검토Policy.기간계산(request));

    private static Result<T> Calculate<T>(Func<T> calculate)
    {
        try { return Result.Ok(calculate()); }
        catch (ArgumentException error)
        {
            return Result.Fail<T>(new Error(error.Message).WithMetadata("StatusCode", 400));
        }
    }
}
