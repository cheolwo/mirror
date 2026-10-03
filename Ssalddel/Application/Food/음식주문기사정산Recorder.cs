using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Food;
using 살뜰.Data;
using 살뜰.도메인.공통;
using 살뜰.도메인.음식;
using 살뜰.도메인.운송;

namespace Ssalddel.Application.Food;

/// <summary>호출자가 소유한 전달 완료 transaction에 정산 대금을 함께 추가합니다. 새 요율로 재계산하지 않습니다.</summary>
public static class 음식주문기사정산Recorder
{
    public static async Task 완료기록Async(
        SsalddelContext db,
        음식주문 order,
        운송원장 queue,
        음식배달시도 attempt,
        CancellationToken cancellationToken)
    {
        if (queue.배차업무유형 != 상태값.배차업무유형.음식배달
            || 음식주문상태코드.Normalize(order.상태) is not (음식주문상태코드.전달완료 or 음식주문상태코드.수령확인)
            || queue.배차큐단계 != 상태값.배차큐단계.종료
            || attempt.주문번호 != order.주문번호
            || attempt.제안Id != queue.의뢰Id
            || attempt.기사Id != queue.확정기사Id
            || attempt.중단시각Utc.HasValue
            || !attempt.전달완료시각Utc.HasValue)
        {
            throw new InvalidOperationException("최종 유효 음식 배달 완료만 정산에 연결할 수 있습니다.");
        }

        var existing = await db.음식주문기사정산.SingleOrDefaultAsync(
            x => x.주문번호 == order.주문번호,
            cancellationToken);
        if (existing is not null)
        {
            if (existing.배달시도StableId != attempt.시도StableId || existing.기사Id != attempt.기사Id)
                throw new InvalidOperationException("주문에 다른 배달 시도의 정산이 이미 기록되어 있습니다.");
            return;
        }

        var hasFrozenQuote = queue.기사지급예정액 is > 0
                             && queue.기사제안요금판정시각Utc.HasValue
                             && !string.IsNullOrWhiteSpace(queue.기사제안요금정책판본)
                             && HasFrozenEvidence(queue);
        var settlement = new 음식주문기사정산
        {
            정산StableId = StableId("food-settlement", order.주문번호),
            음식주문Id = order.Id,
            주문번호 = order.주문번호,
            음식점명 = order.음식점명,
            배달시도Id = attempt.Id,
            배달시도 = attempt,
            배달시도StableId = attempt.시도StableId,
            운송Id = queue.Id,
            기사Id = attempt.기사Id,
            세전대금 = hasFrozenQuote ? queue.기사지급예정액 : null,
            요금정책판본 = queue.기사제안요금정책판본 ?? string.Empty,
            요금계산근거Json = queue.기사제안요금계산근거Json ?? string.Empty,
            전달완료시각Utc = attempt.전달완료시각Utc.Value,
            CreatedAtUtc = attempt.전달완료시각Utc.Value,
            UpdatedAtUtc = attempt.전달완료시각Utc.Value
        };
        상태반영(settlement, order);
        db.음식주문기사정산.Add(settlement);
    }

    public static void 상태반영(음식주문기사정산 settlement, 음식주문 order)
    {
        if (음식주문상태코드.Normalize(order.상태) == 음식주문상태코드.수령확인)
            settlement.수령확인시각Utc ??= order.상태이력
                .Where(x => 음식주문상태코드.Normalize(x.다음상태) == 음식주문상태코드.수령확인)
                .OrderBy(x => x.전이시각Utc)
                .Select(x => (DateTime?)x.전이시각Utc)
                .FirstOrDefault() ?? order.UpdatedAt;

        (settlement.정산상태Code, settlement.보류사유) = settlement.세전대금 is null
            ? (음식주문기사정산상태Code.요금근거없음, "수락 때 동결한 배달료와 계산 근거를 확인해야 합니다.")
            : settlement.수령확인시각Utc is null
                ? (음식주문기사정산상태Code.수령확인대기, "주문자 수령 확인을 기다리고 있습니다.")
                : settlement.공제액 is null
                    ? (음식주문기사정산상태Code.공제확인대기, "기간 공제·배분 근거가 없어 수령액은 미확정입니다.")
                    : (음식주문기사정산상태Code.모의검증준비, string.Empty);
    }

    public static FoodDeliveryOrderSettlementDto ToDto(음식주문기사정산 item, bool replay = false, string? currentExecutionModeCode = null)
    {
        var latest = item.지급검증목록.OrderByDescending(x => x.검증시각Utc).ThenByDescending(x => x.Id).FirstOrDefault();
        return new FoodDeliveryOrderSettlementDto
        {
            SettlementId = item.정산StableId,
            OrderNo = item.주문번호,
            RestaurantName = item.음식점명,
            DeliveryAttemptId = item.배달시도StableId,
            DriverId = item.기사Id,
            GrossAmount = item.세전대금,
            DeductionAmount = item.공제액,
            NetAmount = item.수령액,
            PricingPolicyRevision = item.요금정책판본,
            SettlementStatusCode = item.정산상태Code,
            PayoutStatusCode = item.지급상태Code,
            HoldReason = item.보류사유,
            DeductionEvidenceReference = item.공제근거참조,
            DeductionEvidenceScopeCode = item.공제근거범위Code,
            ExecutionModeCode = string.IsNullOrEmpty(item.실행모드Code)
                ? currentExecutionModeCode ?? "Unknown" : item.실행모드Code,
            ServerExecutionModeCode = string.IsNullOrWhiteSpace(currentExecutionModeCode)
                ? "Unknown" : currentExecutionModeCode,
            CompletedAtUtc = item.전달완료시각Utc,
            ReceiptConfirmedAtUtc = item.수령확인시각Utc,
            SimulationPaymentId = latest?.지급StableId ?? string.Empty,
            SimulationVerifiedAtUtc = latest?.검증시각Utc,
            IsActualTransferCompleted = false,
            IsIdempotentReplay = replay,
            Revision = item.Revision,
            SimulationPayments = item.지급검증목록.OrderBy(x => x.검증시각Utc).ThenBy(x => x.Id)
                .Select(x => new FoodDeliverySimulatedPaymentDto
                {
                    PaymentId = x.지급StableId,
                    OutcomeCode = x.결과Code,
                    Amount = x.모의수령액,
                    VerifiedAtUtc = x.검증시각Utc,
                    ExecutionModeCode = "Simulation",
                    IsActualTransferCompleted = false
                }).ToArray()
        };
    }

    public static string StableId(string prefix, string value)
        => $"{prefix}:{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant()}";

    private static bool HasFrozenEvidence(운송원장 queue)
    {
        var evidence = queue.기사제안요금계산근거Json;
        if (string.IsNullOrWhiteSpace(evidence)) return false;
        try
        {
            using var document = JsonDocument.Parse(evidence);
            var root = document.RootElement;
            return root.ValueKind == JsonValueKind.Object
                   && root.TryGetProperty("요금", out var pricing)
                   && pricing.ValueKind == JsonValueKind.Object
                   && pricing.TryGetProperty("기사지급예정액", out var amount)
                   && amount.ValueKind == JsonValueKind.Number
                   && amount.TryGetDecimal(out var gross)
                   && gross == queue.기사지급예정액
                   && root.TryGetProperty("정책판본", out var revision)
                   && revision.ValueKind == JsonValueKind.String
                   && revision.GetString() == queue.기사제안요금정책판본
                   && (!root.TryGetProperty("기사Id", out var driver)
                       || driver.ValueKind == JsonValueKind.Null
                       || (driver.ValueKind == JsonValueKind.String && driver.GetString() == queue.확정기사Id));
        }
        catch (JsonException) { return false; }
    }
}
