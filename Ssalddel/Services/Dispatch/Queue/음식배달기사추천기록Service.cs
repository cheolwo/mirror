using Ssalddel.Contracts.Common.Drivers;
using 살뜰.Services.Storage.Local;

namespace 살뜰.Services.Dispatch.Queue;

// 공용 실행 인덱스는 재사용하되 음식 기사 추천의 대기 점수만 갱신한다.
public sealed class 음식배달기사추천기록Service(
    I국내화물운송기사상태Store store) : I음식배달기사추천기록Service
{
    public async Task 추천기록Async(
        string driverId,
        DateTime 추천시각Utc,
        CancellationToken cancellationToken = default)
    {
        var existing = await store.GetAsync(driverId, cancellationToken);
        if (existing is null
            || !string.Equals(existing.AppKey, 기사앱식별자.FoodDeliveryDriverApp, StringComparison.Ordinal))
        {
            return;
        }

        var basis = 추천시각Utc.Kind switch
        {
            DateTimeKind.Utc => 추천시각Utc,
            DateTimeKind.Local => 추천시각Utc.ToUniversalTime(),
            _ => DateTime.SpecifyKind(추천시각Utc, DateTimeKind.Utc)
        };
        await store.UpsertAsync(existing with
        {
            Aging기준시각Utc = basis,
            Aging점수 = 0m,
            마지막추천시각Utc = basis,
            마지막후보없음시각Utc = null,
            후보없음횟수 = 0
        }, cancellationToken);
    }
}
