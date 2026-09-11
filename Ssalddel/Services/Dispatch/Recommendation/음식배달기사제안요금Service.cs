using Microsoft.EntityFrameworkCore;
using 살뜰.Data;
using 살뜰.Services.Dispatch.Engine;
using 살뜰.Services.Weather;
using 살뜰.도메인.음식;
using 살뜰.도메인.운송;

namespace 살뜰.Services.Dispatch.Recommendation;

public sealed record 음식배달기사제안요금산정결과(
    음식배달기사제안요금판정 요금,
    픽업지기상관측결과 기상,
    string 정책판본,
    DateTime 판정시각Utc);

public interface I음식배달기사제안요금Service
{
    Task<음식배달기사제안요금산정결과> 산정Async(
        운송원장 queue,
        CancellationToken cancellationToken = default);
}

public sealed class 음식배달기사제안요금Service(
    SsalddelContext db,
    I배차추천경로Service routeService,
    I픽업지기상관측Client weatherClient,
    TimeProvider timeProvider) : I음식배달기사제안요금Service
{
    public async Task<음식배달기사제안요금산정결과> 산정Async(
        운송원장 queue,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(queue);

        var policy = await db.음식운영정책
                         .AsNoTracking()
                         .SingleOrDefaultAsync(item => item.Id == 1, cancellationToken)
                     ?? new 음식운영정책
                     {
                         UpdatedAtUtc = DateTime.UnixEpoch
                     };
        var distance = CalculateDistance(queue);
        var weather = await weatherClient.조회Async(
            queue.픽업_위도,
            queue.픽업_경도,
            cancellationToken);
        var price = 음식배달기사제안요금Policy.판정(
            policy,
            distance,
            weather.유효한강수근거있음,
            weather.강수중);
        var revision = string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"{음식배달기사제안요금Policy.기본판본}|weather:{policy.기사기상할증정책판본}|policy:{policy.UpdatedAtUtc.ToUniversalTime():O}");

        return new 음식배달기사제안요금산정결과(
            price,
            weather,
            revision,
            timeProvider.GetUtcNow().UtcDateTime);
    }

    private decimal? CalculateDistance(운송원장 queue)
    {
        if (!queue.픽업_위도.HasValue
            || !queue.픽업_경도.HasValue
            || !queue.하차_위도.HasValue
            || !queue.하차_경도.HasValue)
        {
            return null;
        }

        return routeService.CalculateDistanceKm(
            new 배차경로좌표(queue.픽업_위도.Value, queue.픽업_경도.Value),
            new 배차경로좌표(queue.하차_위도.Value, queue.하차_경도.Value));
    }
}
