using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.Food;
using Ssalddel.Contracts.Food;
using 살뜰.도메인.음식;

namespace Ssalddel.Application.Driver.Food;

public sealed partial class FoodDeliveryDriverWorkspaceUseCase
{
    /// <summary>완료 원장의 UTC 시각을 한국 날짜로 묶습니다. 수령 확인·후속 공제·모의 지급일로 귀속을 옮기지 않습니다.</summary>
    public async Task<FoodDeliveryDailySettlementDto> GetDailySettlementAsync(
        string driverId, DateOnly? completionDateKst, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(driverId))
            throw new ArgumentException("기사 인증 정보가 없습니다.", nameof(driverId));

        var now = _timeProvider.GetUtcNow();
        var date = completionDateKst ?? DateOnly.FromDateTime(now.UtcDateTime.AddHours(9));
        if (date == DateOnly.MinValue || date == DateOnly.MaxValue)
            throw new ArgumentException("조회 가능한 한국 날짜를 입력해 주세요.", nameof(completionDateKst));
        var startUtc = DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue).AddHours(-9), DateTimeKind.Utc);
        var endUtc = startUtc.AddDays(1);

        // 전달 완료 transaction에서 저장한 고유 주문 정산이 권위입니다. 제안/현재 운송이나 최근40건을 합산하지 않습니다.
        var settlements = await _db.음식주문기사정산.AsNoTracking()
            .Include(x => x.지급검증목록)
            .Where(x => x.기사Id == driverId
                        && x.전달완료시각Utc >= startUtc
                        && x.전달완료시각Utc < endUtc)
            .OrderBy(x => x.전달완료시각Utc).ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);
        var orders = settlements.Select(x => 음식주문기사정산Recorder.ToDto(
            x, currentExecutionModeCode: _executionMode.Mode.ToString())).ToArray();

        return new FoodDeliveryDailySettlementDto
        {
            DriverId = driverId,
            CompletionDateKst = date,
            PeriodStartAtUtc = startUtc,
            PeriodEndAtUtc = endUtc,
            CompletedOrderCount = orders.Length,
            ReceiptConfirmedOrderCount = orders.Count(x => x.ReceiptConfirmedAtUtc.HasValue),
            AwaitingReceiptOrderCount = orders.Count(x => !x.ReceiptConfirmedAtUtc.HasValue),
            MissingGrossAmountCount = orders.Count(x => !x.GrossAmount.HasValue),
            UnconfirmedDeductionCount = orders.Count(x => !x.DeductionAmount.HasValue),
            UnconfirmedNetAmountCount = orders.Count(x => !x.NetAmount.HasValue),
            KnownGrossAmountTotal = orders.Sum(x => x.GrossAmount ?? 0m),
            GrossAmountTotal = orders.All(x => x.GrossAmount.HasValue) ? orders.Sum(x => x.GrossAmount!.Value) : null,
            DeductionAmountTotal = orders.All(x => x.DeductionAmount.HasValue) ? orders.Sum(x => x.DeductionAmount!.Value) : null,
            NetAmountTotal = orders.All(x => x.NetAmount.HasValue) ? orders.Sum(x => x.NetAmount!.Value) : null,
            SimulationSucceededOrderCount = orders.Count(x => x.PayoutStatusCode == 음식주문기사지급상태Code.모의성공),
            SimulationFailedOrderCount = orders.Count(x => x.PayoutStatusCode == 음식주문기사지급상태Code.모의실패),
            SimulationSucceededNetAmountTotal = orders
                .Where(x => x.PayoutStatusCode == 음식주문기사지급상태Code.모의성공)
                .All(x => x.NetAmount.HasValue)
                ? orders.Where(x => x.PayoutStatusCode == 음식주문기사지급상태Code.모의성공).Sum(x => x.NetAmount!.Value)
                : null,
            // 기존 원장은 Simulation 증빙만 보유합니다. 모의 성공을 실제 지급으로 승격하지 않습니다.
            ActualTransferCompletedOrderCount = 0,
            ActualTransferAmountTotal = null,
            ServerExecutionModeCode = _executionMode.Mode.ToString(),
            UpdatedAtUtc = now.UtcDateTime,
            OrderSettlements = orders
        };
    }
}
