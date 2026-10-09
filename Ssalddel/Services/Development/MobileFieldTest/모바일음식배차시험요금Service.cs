using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using 살뜰.Data;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.Options;
using 살뜰.Services.Weather;
using 살뜰.도메인.기사;
using 살뜰.도메인.음식;
using 살뜰.도메인.운송;

namespace Ssalddel.Services.Development.MobileFieldTest;

public sealed class 모바일음식배차시험요금Service(
    SsalddelContext db, 모바일음식배차시험Guard guard, 모바일음식배차시험자료 fixtures,
    TimeProvider timeProvider, IOptions<FoodDriverRoutePricingOptions> routePricingOptions) : I음식배달기사제안요금Service
{
    public Task<음식배달기사제안요금산정결과> 산정Async(운송원장 queue, CancellationToken cancellationToken = default)
        => throw new ArgumentException("MobileFoodDispatchPricingDriverRequired");

    public async Task<음식배달기사제안요금산정결과> 산정Async(
        운송원장 queue, string driverId, CancellationToken cancellationToken = default)
    {
        guard.EnsureEnabled();
        ArgumentNullException.ThrowIfNull(queue);
        if (!fixtures.IsReady || driverId != fixtures.Accounts!.DriverId)
            throw new ArgumentException("MobileFoodDispatchDriverOutsideFixture");
        if (!await db.Users.AsNoTracking().AnyAsync(x => x.Id == driverId
            && x.UserName == 모바일음식배차시험자료.DriverUserName, cancellationToken))
            throw new ArgumentException("MobileFoodDispatchDriverOutsideFixture");
        if (!await fixtures.IsFixtureQueueAsync(db, queue, cancellationToken))
            throw new ArgumentException("MobileFoodDispatchRouteOutsideFixture");
        var driver = await db.Set<배달기사>().AsNoTracking().SingleOrDefaultAsync(x => x.기사Id == driverId, cancellationToken);
        if (driver is not { 상태: "활동중", 차량: "오토바이" } || driver.메모 != 모바일음식배차시험자료.DriverMemo)
            throw new ArgumentException("FoodPricingDriverVehicleRequired");
        var policy = await db.음식운영정책.AsNoTracking().SingleOrDefaultAsync(x => x.Id == 1, cancellationToken)
                     ?? new 음식운영정책 { UpdatedAtUtc = DateTime.UnixEpoch };
        var now = timeProvider.GetUtcNow();
        var band = routePricingOptions.Value.ResolveBand(now);
        var surcharge = band?.MotorcycleSurchargeKrw ?? 0m;
        var route = 모바일음식배차시험자료.ConfiguredRoute;
        var price = 음식배달기사제안요금Policy.판정(policy, route.DistanceKm, false, false, now,
            한시수요할증허용: true, deliveryKey: queue.의뢰Id, 시간대할증액: surcharge);
        price = price with { 기본요금구성 = price.기본요금구성! with { DistanceBasisCode = route.SourceCode } };
        var hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(
            new { Fixture = route, Policy = policy, Band = band }))).ToLowerInvariant()[..16];
        // 고정 자료의 IsEstimated/ActualMapRequest도 hash에 포함하며 원장 판본에 의미를 남긴다.
        var revision = $"{음식배달기사제안요금Policy.기본판본}|{route.SourceCode}:r1|IsEstimated=true|ActualMapRequest=false|evidence:{hash}";
        var weather = new 픽업지기상관측결과(false, false, "MobileFieldTestNotObserved", null, null, null, route.SourceCode, null);
        return new(price, weather, revision, now.UtcDateTime)
        {
            기사Id = driverId, 경로옵션Code = route.FixtureId, 경로차량Code = "Motorcycle",
            산정거리Km = route.DistanceKm, 거리근거Code = route.SourceCode, 경로소요시간초 = null,
            시간대Code = band?.Code, 시간대시작분Kst = band?.StartMinuteKst,
            시간대종료분Kst = band?.EndMinuteKst, 시간대할증액 = surcharge
        };
    }
}
