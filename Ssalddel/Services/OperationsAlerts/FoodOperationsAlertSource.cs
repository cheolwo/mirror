using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.Admin.Food;
using 살뜰.Data;

namespace Ssalddel.Services.OperationsAlerts;

public sealed class FoodOperationsAlertSource(SsalddelContext db, I음식주문운영추적UseCase trace, TimeProvider clock,
    IConfiguration configuration) : IOperationsAlertSource
{
    public string Key => "food";
    public bool RepeatActiveSweep => true;
    private static readonly string[] Codes = ["DispatchLedgerMissing", "DispatchCorrelationMismatch",
        "SharedLedgerOutboxReviewRequired", "DriverNotificationOutboxReviewRequired", "RestaurantPreparationDelay", "DeliveryProgressDelay", "DriverAssignmentDelay"];
    public async Task<IReadOnlyList<OperationsObservation>> ReadChangesAsync(DateTime afterUtc, string afterId, int take, CancellationToken ct)
    {
        if (!configuration.GetValue<bool>("VersionFeatureFlags:FoodDeliveryWorkflow")) return [];
        var recent = clock.GetUtcNow().UtcDateTime.AddDays(-2);
        var ids = await db.음식주문.AsNoTracking().Where(x => (x.UpdatedAt >= recent || (x.상태 != "전달완료" && x.상태 != "취소" && x.상태 != "배달완료"))
            && (x.UpdatedAt > afterUtc || (x.UpdatedAt == afterUtc && string.Compare(x.주문번호, afterId) > 0)))
            .OrderBy(x => x.UpdatedAt).ThenBy(x => x.주문번호).Take(take).Select(x => x.주문번호).ToArrayAsync(ct);
        var result = new List<OperationsObservation>();
        foreach (var id in ids) result.AddRange(await ReadCurrentAsync(id, ct));
        return result;
    }
    public async Task<IReadOnlyList<OperationsObservation>> ReadCurrentAsync(string id, CancellationToken ct)
    {
        if (!configuration.GetValue<bool>("VersionFeatureFlags:FoodDeliveryWorkflow")) throw new InvalidOperationException("FeatureDisabled");
        var row = await trace.조회Async(id, ct) ?? throw new InvalidOperationException("SourceUnavailable");
        // 주의와 자동 회복은 운영자 호출 사유가 아니다. 원장의 확정된 수동 확인 대상만 관찰한다.
        var active = row.생명주기조화.예외Codes.ToHashSet(StringComparer.Ordinal);
        var latestAttempt = row.배달시도목록.OrderByDescending(x => x.시도순번).FirstOrDefault();
        var interruptedAfterPickup = latestAttempt is { 픽업완료시각Utc: not null, 중단시각Utc: not null, 전달완료시각Utc: null };
        return Codes.Select(code => new OperationsObservation(Key, id, code,
            code is "DispatchLedgerMissing" or "DispatchCorrelationMismatch" or "DriverNotificationOutboxReviewRequired"
                ? "Urgent" : "Warning", active.Contains(code), row.최근변경시각Utc))
            .Append(new(Key, id, "AfterPickupRecoveryRequired", "Urgent", interruptedAfterPickup, row.최근변경시각Utc)).ToArray();
    }
}
