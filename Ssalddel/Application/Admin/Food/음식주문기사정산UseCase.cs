using FluentResults;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Ssalddel.ApiMetadata;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Application.Food;
using Ssalddel.Contracts.Admin.Food;
using Ssalddel.Contracts.Food;
using 살뜰.Data;
using 살뜰.Services.Options;
using 살뜰.도메인.공통;
using 살뜰.도메인.음식;

namespace Ssalddel.Application.Admin.Food;

public interface I음식주문기사정산UseCase
{
    Task<Result<FoodDeliveryOrderSettlementDto>> 모의지급검증Async(
        string orderNo, FoodDeliverySimulatedPayoutRequest request, CancellationToken cancellationToken);
}

[SsalddelApiWorkflow(SsalddelWorkflow.FoodDelivery)]
[SsalddelUseCase("음식 주문 기사 정산 모의 지급 검증", Summary = "주문·최종 기사·동결 대금과 명시적 Simulation 공제 시험 입력을 검증합니다. 은행/PG 호출 또는 실입금은 없습니다.")]
[SsalddelUseCaseActor(SsalddelActor.PlatformOperator)]
public sealed class 음식주문기사정산UseCase(
    SsalddelContext db,
    ICurrentUserAccessor currentUser,
    ISsalddelExecutionModePolicy executionMode,
    TimeProvider timeProvider) : I음식주문기사정산UseCase
{
    public async Task<Result<FoodDeliveryOrderSettlementDto>> 모의지급검증Async(
        string orderNo, FoodDeliverySimulatedPayoutRequest request, CancellationToken cancellationToken)
    {
        if (!string.Equals(currentUser.Role, 역할명.서버관리자, StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(currentUser.UserId))
            return Fail("서버관리자만 모의 지급을 검증할 수 있습니다.", StatusCodes.Status403Forbidden);
        if (!executionMode.IsSimulation)
            return Fail("실송금은 연결되지 않았습니다. 모의 지급 검증은 Simulation 모드에서만 가능합니다.", StatusCodes.Status409Conflict);
        var cleanOrderNo = orderNo?.Trim();
        if (string.IsNullOrWhiteSpace(cleanOrderNo) || request is null
            || string.IsNullOrWhiteSpace(request.IdempotencyKey) || request.IdempotencyKey.Trim().Length > 128
            || request.ExpectedSettlementRevision <= 0 || request.ConfirmedGrossAmount <= 0
            || request.OutcomeCode is not ("Succeeded" or "Failed"))
            return Fail("주문번호, 멱등 키, 정산 판본, 세전 금액과 모의 결과를 확인해 주세요.", StatusCodes.Status400BadRequest);
        if (request.ConfirmedDeductionAmount is null || string.IsNullOrWhiteSpace(request.DeductionEvidenceReference))
            return Fail("공제액과 명시적 시험 근거가 없어 수령액은 미확정입니다.", StatusCodes.Status409Conflict);
        if (request.ConfirmedDeductionAmount < 0 || request.ConfirmedDeductionAmount >= request.ConfirmedGrossAmount
            || decimal.Round(request.ConfirmedDeductionAmount.Value, 2) != request.ConfirmedDeductionAmount
            || request.DeductionEvidenceReference.Trim().Length > 500)
            return Fail("공제액은 세전 금액 미만의 유효한 금액이어야 하며 근거는 500자 이하여야 합니다.", StatusCodes.Status400BadRequest);

        var key = request.IdempotencyKey.Trim();
        var evidence = request.DeductionEvidenceReference.Trim();
        var previous = await db.음식주문기사지급검증.AsNoTracking()
            .SingleOrDefaultAsync(x => x.멱등키 == key, cancellationToken);
        if (previous is not null)
        {
            var previousSettlement = await db.음식주문기사정산.AsNoTracking().Include(x => x.지급검증목록)
                .SingleAsync(x => x.Id == previous.정산Id, cancellationToken);
            if (previousSettlement.주문번호 != cleanOrderNo || previous.확인세전대금 != request.ConfirmedGrossAmount
                || previous.확인공제액 != request.ConfirmedDeductionAmount || previous.공제근거참조 != evidence
                || previous.결과Code != request.OutcomeCode)
                return Fail("같은 멱등 키가 다른 모의 지급 입력에 사용되었습니다.", StatusCodes.Status409Conflict);
            return Result.Ok(음식주문기사정산Recorder.ToDto(previousSettlement, true,
                currentExecutionModeCode: executionMode.Mode.ToString()));
        }

        var settlement = await db.음식주문기사정산.Include(x => x.지급검증목록)
            .SingleOrDefaultAsync(x => x.주문번호 == cleanOrderNo, cancellationToken);
        if (settlement is null) return Fail("이 주문의 완료 정산 기록을 찾을 수 없습니다.", StatusCodes.Status404NotFound);
        if (settlement.Revision != request.ExpectedSettlementRevision)
            return Fail("정산이 변경되었습니다. 같은 주문을 다시 조회해 주세요.", StatusCodes.Status409Conflict);
        if (settlement.지급상태Code == 음식주문기사지급상태Code.모의성공)
            return Fail("이 주문의 모의 지급은 이미 성공했습니다. 중복 지급을 만들지 않습니다.", StatusCodes.Status409Conflict);

        var order = await db.음식주문.AsNoTracking().Include(x => x.상태이력)
            .SingleOrDefaultAsync(x => x.Id == settlement.음식주문Id, cancellationToken);
        var transport = await db.운송원장.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == settlement.운송Id, cancellationToken);
        var latestAttempt = await db.음식배달시도.AsNoTracking().Where(x => x.주문번호 == cleanOrderNo)
            .OrderByDescending(x => x.시도순번).FirstOrDefaultAsync(cancellationToken);
        if (order is null || 음식주문상태코드.Normalize(order.상태) != 음식주문상태코드.수령확인
            || latestAttempt is null || latestAttempt.Id != settlement.배달시도Id
            || latestAttempt.중단시각Utc.HasValue || !latestAttempt.전달완료시각Utc.HasValue
            || latestAttempt.기사Id != settlement.기사Id || transport is null
            || transport.배차업무유형 != 상태값.배차업무유형.음식배달
            || transport.확정기사Id != settlement.기사Id)
            return Fail("주문자 수령 확인과 최종 유효 배달 기사 연결을 확인해야 합니다.", StatusCodes.Status409Conflict);
        if (settlement.세전대금 is null || settlement.세전대금 != request.ConfirmedGrossAmount)
            return Fail("확인한 세전 금액이 수락 때 동결한 대금과 일치하지 않습니다.", StatusCodes.Status409Conflict);
        // 이전 실패의 시험 근거도 덮어쓰지 않습니다. 다른 공제 정책은 별도 조정 원장이 필요합니다.
        if (settlement.공제액.HasValue
            && (settlement.공제액 != request.ConfirmedDeductionAmount || settlement.공제근거참조 != evidence))
            return Fail("이 정산의 기존 공제 시험 근거가 다릅니다. 기존 근거를 덮어쓸 수 없습니다.", StatusCodes.Status409Conflict);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        settlement.공제액 = request.ConfirmedDeductionAmount;
        settlement.수령액 = settlement.세전대금 - settlement.공제액;
        settlement.공제근거참조 = evidence;
        settlement.공제근거범위Code = "SimulationFixture";
        settlement.실행모드Code = "Simulation";
        settlement.지급상태Code = request.OutcomeCode == "Succeeded"
            ? 음식주문기사지급상태Code.모의성공 : 음식주문기사지급상태Code.모의실패;
        음식주문기사정산Recorder.상태반영(settlement, order);
        settlement.Revision++;
        settlement.UpdatedAtUtc = now;
        settlement.지급검증목록.Add(new 음식주문기사지급검증
        {
            정산 = settlement,
            지급StableId = 음식주문기사정산Recorder.StableId("food-sim-payment", key),
            멱등키 = key,
            확인세전대금 = request.ConfirmedGrossAmount,
            확인공제액 = request.ConfirmedDeductionAmount.Value,
            모의수령액 = settlement.수령액.Value,
            공제근거참조 = evidence,
            결과Code = request.OutcomeCode,
            검증관리자Id = currentUser.UserId!,
            검증시각Utc = now
        });
        // 단일 SaveChanges transaction + 정산 Revision/멱등 고유 index로 동시 성공과 중복 요청을 차단합니다.
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // 실패한 요청의 추적 상태를 남겨 후속 같은 scope 저장으로 지급 증빙이 섞이지 않게 합니다.
            db.ChangeTracker.Clear();
            return Fail("다른 요청이 먼저 정산을 변경했습니다. 같은 주문을 다시 조회해 주세요.", StatusCodes.Status409Conflict);
        }
        return Result.Ok(음식주문기사정산Recorder.ToDto(settlement,
            currentExecutionModeCode: executionMode.Mode.ToString()));
    }

    private static Result<FoodDeliveryOrderSettlementDto> Fail(string message, int statusCode)
        => Result.Fail<FoodDeliveryOrderSettlementDto>(new Error(message).WithMetadata("StatusCode", statusCode));
}
