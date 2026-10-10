using Ssalddel.Application.Driver.Food;
using Ssalddel.Contracts.Driver.Food;
using Ssalddel.Contracts.Food;

namespace Ssalddel.Services.Development.MobileFieldTest;

/// <summary>기존 조회 결과를 유지하고 실제 활성화된 시험 작업자의 안내만 정정한다.</summary>
public sealed class 모바일음식배차시험기사조회UseCase(
    FoodDeliveryDriverWorkspaceUseCase inner, 모바일음식배차시험Guard guard,
    모바일음식배차시험자료 fixtures) : IFoodDeliveryDriverWorkspaceUseCase
{
    public async Task<FoodDeliveryDriverCurrentWorkDto> GetCurrentWorkAsync(string driverId, CancellationToken cancellationToken)
    {
        guard.EnsureEnabled();
        var result = await inner.GetCurrentWorkAsync(driverId, cancellationToken);
        if (fixtures.IsReady && driverId == fixtures.Accounts!.DriverId)
        {
            result.DispatchAutomationEnabled = true;
            result.DispatchAutomationNotice = "내부 시험 자료로 기사 추천을 확인합니다. 실제 지도·운행 확인은 별도입니다.";
        }
        return result;
    }

    public async Task<FoodDeliveryDriverWorkspaceDto> GetAsync(string driverId, CancellationToken cancellationToken)
    {
        guard.EnsureEnabled();
        var result = await inner.GetAsync(driverId, cancellationToken);
        if (fixtures.IsReady && driverId == fixtures.Accounts!.DriverId)
        {
            result.DispatchAutomationEnabled = true;
            result.DispatchAutomationNotice = "내부 시험 자료로 기사 추천을 확인합니다. 실제 지도·운행 확인은 별도입니다.";
        }
        return result;
    }

    public Task<FoodDeliveryDailySettlementDto> GetDailySettlementAsync(string driverId,
        DateOnly? completionDateKst, CancellationToken cancellationToken)
        => inner.GetDailySettlementAsync(driverId, completionDateKst, cancellationToken);

    public Task<FoodDeliveryCompletedDeliveryDetailDto?> GetCompletedDeliveryDetailAsync(string driverId,
        string settlementId, CancellationToken cancellationToken)
        => inner.GetCompletedDeliveryDetailAsync(driverId, settlementId, cancellationToken);
}
