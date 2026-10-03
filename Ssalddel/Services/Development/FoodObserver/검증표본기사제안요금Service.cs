using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using 살뜰.Data;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.Options;
using 살뜰.Services.Weather;
using 살뜰.도메인.공통;
using 살뜰.도메인.기사;
using 살뜰.도메인.음식;
using 살뜰.도메인.운송;

namespace Ssalddel.Services.Development.FoodObserver;

/// <summary>
/// 격리 관찰 호스트의 알려진 합성 주소 쌍에만 적용하는 명시적 거리 입력이다.
/// 외부 API를 호출하지 않으며 운영 경로 실패의 fallback으로 등록하지 않는다.
/// 요금·시간대·할증 규칙은 기존 정책을 그대로 사용한다.
/// </summary>
public sealed class 검증표본기사제안요금Service(
    SsalddelContext db,
    음식배달관찰검증Options observer,
    ISsalddelExecutionModePolicy executionMode,
    TimeProvider timeProvider,
    IOptions<FoodDriverRoutePricingOptions>? routePricingOptions = null) : I음식배달기사제안요금Service
{
    public const string DistanceSourceCode = "FoodObserverSimulationRouteFixture";
    public const string FixtureId = "observer-restaurant-to-customer.r1";
    public const decimal DistanceKm = 0.250m;
    public const string NearDriverId = "food-observer-driver-near";
    public const string FarDriverId = "food-observer-driver-far";

    public static 음식배달관찰RouteFixture ConfiguredFixture => new(
        "Simulation", DistanceSourceCode, FixtureId,
        검증표본좌표Service.RestaurantAddress, 검증표본좌표Service.CustomerAddress,
        37.5880m, 127.0850m, 37.5880m, 127.0870m, DistanceKm,
        IsEstimated: true, ActualMapRequest: false, AllowedDriverIds: [NearDriverId, FarDriverId]);

    public Task<음식배달기사제안요금산정결과> 산정Async(운송원장 queue, CancellationToken cancellationToken = default)
        => throw new ArgumentException("FoodObserverPricingDriverRequired");

    public async Task<음식배달기사제안요금산정결과> 산정Async(
        운송원장 queue, string driverId, CancellationToken cancellationToken = default)
    {
        if (!observer.Enabled || !executionMode.IsSimulation)
            throw new InvalidOperationException("FoodObserverPricingRequiresSimulation");
        ArgumentNullException.ThrowIfNull(queue);
        if (queue.배차업무유형 != 상태값.배차업무유형.음식배달)
            throw new ArgumentException("FoodPricingTaskTypeMismatch");
        if (driverId is not (NearDriverId or FarDriverId))
            throw new ArgumentException("FoodObserverPricingDriverOutsideFixture");
        var fixture = ConfiguredFixture;
        if (queue.픽업_위도 != fixture.PickupLatitude || queue.픽업_경도 != fixture.PickupLongitude
            || queue.하차_위도 != fixture.DropoffLatitude || queue.하차_경도 != fixture.DropoffLongitude)
            throw new ArgumentException("FoodObserverRouteFixtureNotFound");
        var driver = await db.Set<배달기사>().AsNoTracking()
            .SingleOrDefaultAsync(x => x.기사Id == driverId, cancellationToken);
        if (driver is not { 상태: "활동중", 차량: "오토바이" })
            throw new ArgumentException("FoodPricingDriverVehicleRequired");

        var policy = await db.음식운영정책.AsNoTracking().SingleOrDefaultAsync(x => x.Id == 1, cancellationToken)
                     ?? new 음식운영정책 { UpdatedAtUtc = DateTime.UnixEpoch };
        var now = timeProvider.GetUtcNow();
        var band = (routePricingOptions?.Value ?? new FoodDriverRoutePricingOptions()).ResolveBand(now);
        var surcharge = band?.MotorcycleSurchargeKrw ?? 0m;
        // 강수 미확인을 '맑음 관측 성공'으로 바꾸지 않는다.
        var weather = new 픽업지기상관측결과(false, false, "FoodObserverNotObserved",
            null, null, null, DistanceSourceCode, null);
        var price = 음식배달기사제안요금Policy.판정(policy, DistanceKm, false, false, now,
            한시수요할증허용: true, deliveryKey: string.IsNullOrWhiteSpace(queue.의뢰Id) ? "offer" : queue.의뢰Id,
            시간대할증액: surcharge);
        price = price with
        {
            기본요금구성 = price.기본요금구성! with { DistanceBasisCode = DistanceSourceCode }
        };
        // DB 판본 칸(180자) 안에 정책 전체의 hash를 결속하고 계산 상세는 원장 JSON에 보존한다.
        var evidenceHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(new { Fixture = fixture, Policy = policy, Band = band })))).ToLowerInvariant()[..16];
        var revision = $"{음식배달기사제안요금Policy.기본판본}|distance:{DistanceSourceCode}:r1|policy:{evidenceHash}";
        return new(price, weather, revision, now.UtcDateTime)
        {
            기사Id = driverId, 경로옵션Code = FixtureId, 경로차량Code = "Motorcycle",
            산정거리Km = DistanceKm, 거리근거Code = DistanceSourceCode,
            // 표본 거리에 실제 이동시간을 만들어 붙이지 않는다.
            경로소요시간초 = null,
            시간대Code = band?.Code, 시간대시작분Kst = band?.StartMinuteKst,
            시간대종료분Kst = band?.EndMinuteKst, 시간대할증액 = surcharge
        };
    }
}
