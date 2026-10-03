using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using 살뜰.도메인.기사;
using 살뜰.도메인.공통;
using 살뜰.Data;
using 살뜰.Services.Dispatch.Engine;
using 살뜰.Services.Options;
using 살뜰.Services.Weather;
using 살뜰.도메인.음식;
using 살뜰.도메인.운송;

namespace 살뜰.Services.Dispatch.Recommendation;

public sealed record 음식배달기사제안요금산정결과(
    음식배달기사제안요금판정 요금,
    픽업지기상관측결과 기상,
    string 정책판본,
    DateTime 판정시각Utc)
{
    public string? 기사Id { get; init; }
    public string? 경로옵션Code { get; init; }
    public int? 시간대시작분Kst { get; init; }
    public int? 시간대종료분Kst { get; init; }
    public string? 시간대Code { get; init; }
    public decimal 시간대할증액 { get; init; }
    public double? 경로소요시간초 { get; init; }
    public decimal? 산정거리Km { get; init; }
    public string 거리근거Code { get; init; } = "Unknown";
    public string 경로차량Code { get; init; } = "Unknown";
}

public interface I음식배달기사제안요금Service
{
    Task<음식배달기사제안요금산정결과> 산정Async(
        운송원장 queue,
        CancellationToken cancellationToken = default);
    Task<음식배달기사제안요금산정결과> 산정Async(운송원장 queue, string driverId, CancellationToken cancellationToken = default)
        => 산정Async(queue, cancellationToken);
}

public sealed class 음식배달기사제안요금Service(
    SsalddelContext db,
    I배차추천경로Service routeService,
    I픽업지기상관측Client weatherClient,
    TimeProvider timeProvider,
    ISsalddelExecutionModePolicy executionMode,
    IOptions<FoodDriverRoutePricingOptions>? routePricingOptions = null) : I음식배달기사제안요금Service
{
    public async Task<음식배달기사제안요금산정결과> 산정Async(
        운송원장 queue,
        CancellationToken cancellationToken = default)
        => await CalculateAsync(queue, null, "Car", null, cancellationToken);

    public async Task<음식배달기사제안요금산정결과> 산정Async(운송원장 queue, string driverId, CancellationToken cancellationToken = default)
    {
        EnsureFoodQueue(queue);
        var driver = await db.Set<배달기사>().AsNoTracking().SingleOrDefaultAsync(d => d.기사Id == driverId, cancellationToken);
        var vehicle = driver?.차량?.Trim() switch
        {
            "자동차" or "승용차" or "Car" => "Car",
            "오토바이" or "이륜차" or "Motorcycle" => "Motorcycle",
            _ => throw new ArgumentException("FoodPricingDriverVehicleRequired")
        };
        var options = routePricingOptions?.Value ?? new FoodDriverRoutePricingOptions();
        var option = vehicle == "Car" ? options.CarRouteOption : options.MotorcycleRouteOption;
        if (vehicle == "Motorcycle" && option != "traavoidcaronly")
            throw new ArgumentException("FoodPricingMotorcycleRouteOptionInvalid");
        return await CalculateAsync(queue, driverId, vehicle, option, cancellationToken);
    }

    private async Task<음식배달기사제안요금산정결과> CalculateAsync(운송원장 queue, string? driverId, string vehicle, string? option, CancellationToken cancellationToken)
    {
        EnsureFoodQueue(queue);

        var policy = await db.음식운영정책
                         .AsNoTracking()
                         .SingleOrDefaultAsync(item => item.Id == 1, cancellationToken)
                     ?? new 음식운영정책
                     {
                         UpdatedAtUtc = DateTime.UnixEpoch
                     };
        var now = timeProvider.GetUtcNow();
        var band = (routePricingOptions?.Value ?? new FoodDriverRoutePricingOptions()).ResolveBand(now);
        if (vehicle == "Car" && band?.CarRouteOption is not null) option = band.CarRouteOption;
        var route = await CalculateDistanceAsync(queue, option, cancellationToken);
        var distance = route.DistanceKm!.Value;
        var weather = await weatherClient.조회Async(
            queue.픽업_위도,
            queue.픽업_경도,
            cancellationToken);
        var timeSurcharge = vehicle == "Car" ? band?.CarSurchargeKrw ?? 0m : band?.MotorcycleSurchargeKrw ?? 0m;
        var price = 음식배달기사제안요금Policy.판정(
            policy,
            distance,
            weather.유효한강수근거있음,
            weather.강수중,
            now,
            executionMode.IsSimulation,
            string.IsNullOrWhiteSpace(queue.의뢰Id) ? "offer" : queue.의뢰Id,
            시간대할증액: timeSurcharge);
        var revision = string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"{음식배달기사제안요금Policy.기본판본}|distance:naver-directions5-vehicle.r2:{vehicle}:{route.RouteOption}|time-band:{band?.Code ?? "None"}:{timeSurcharge}|weather:{policy.기사기상할증정책판본}|demand:food-demand-surcharge.r1:{policy.기사한시수요할증Revision}|policy:{policy.UpdatedAtUtc.ToUniversalTime():O}");

        return new 음식배달기사제안요금산정결과(
            price,
            weather,
            revision,
            now.UtcDateTime)
        {
            산정거리Km = distance,
            거리근거Code = vehicle == "Car" ? "NaverDirections5CarRouteEstimate" : "NaverDirections5CarOnlyAvoidanceEstimate",
            경로차량Code = vehicle,
            기사Id = driverId,
            경로옵션Code = route.RouteOption,
            경로소요시간초 = route.Duration?.TotalSeconds,
            시간대Code = band?.Code,
            시간대시작분Kst = band?.StartMinuteKst,
            시간대종료분Kst = band?.EndMinuteKst,
            시간대할증액 = timeSurcharge
        };
    }

    private static void EnsureFoodQueue(운송원장 queue)
    {
        ArgumentNullException.ThrowIfNull(queue);
        if (queue.배차업무유형 != 상태값.배차업무유형.음식배달)
            throw new ArgumentException("FoodPricingTaskTypeMismatch");
    }

    private async Task<배차경로예상결과> CalculateDistanceAsync(운송원장 queue, string? option, CancellationToken cancellationToken)
    {
        if (!queue.픽업_위도.HasValue
            || !queue.픽업_경도.HasValue
            || !queue.하차_위도.HasValue
            || !queue.하차_경도.HasValue)
        {
            throw new ArgumentException("FoodPricingCoordinatesRequired");
        }

        if (queue.픽업_위도 is < -90m or > 90m || queue.하차_위도 is < -90m or > 90m
            || queue.픽업_경도 is < -180m or > 180m || queue.하차_경도 is < -180m or > 180m)
            throw new ArgumentException("FoodPricingCoordinatesInvalid");

        cancellationToken.ThrowIfCancellationRequested();
        var origin = new 배차경로좌표(queue.픽업_위도.Value, queue.픽업_경도.Value);
        var goal = new 배차경로좌표(queue.하차_위도.Value, queue.하차_경도.Value);
        var route = option is null
            ? await routeService.EstimateRouteAsync(origin, goal, cancellationToken)
            : await routeService.EstimateRouteAsync(origin, goal, option, cancellationToken);
        if (route is not { 실제경로여부: true, 계산방식: "Directions5", DistanceKm: >= 0m and <= 1000m })
            throw new ArgumentException("FoodPricingRouteUnavailable");

        if (option is not null && route.RouteOption != option)
            throw new ArgumentException("FoodPricingRouteOptionMismatch");
        return route;
    }
}
