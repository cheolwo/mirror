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
using Ssalddel.Application.Pricing;
using Ssalddel.Contracts.Common.Pricing;
using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Common.Finance;

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
    public AgreedRateTableSnapshotDto? 합의요금표 { get; init; }
    public DriverOfferPricingDto? Pricing { get; init; }
    public decimal? 경로거리Km { get; init; }
}

public interface I음식배달기사제안요금Service
{
    Task<음식배달기사제안요금산정결과> 산정Async(
        운송원장 queue,
        CancellationToken cancellationToken = default);
    Task<음식배달기사제안요금산정결과> 산정Async(운송원장 queue, string driverId, CancellationToken cancellationToken = default)
        => 산정Async(queue, cancellationToken);
    Task<음식배달기사제안요금산정결과> 수행조건산정Async(운송원장 queue, string vehicle, CancellationToken cancellationToken = default)
        => throw new ArgumentException("FoodFulfillmentExplicitVehiclePricingRequired");
}

public sealed class 음식배달기사제안요금Service(
    SsalddelContext db,
    I배차추천경로Service routeService,
    I픽업지기상관측Client weatherClient,
    TimeProvider timeProvider,
    ISsalddelExecutionModePolicy executionMode,
    IOptions<FoodDriverRoutePricingOptions>? routePricingOptions = null,
    IAgreedRateTableUseCase? agreedRates = null,
    IOptions<StraightLineDriverPricingOptions>? straightLineOptions = null) : I음식배달기사제안요금Service
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

    public Task<음식배달기사제안요금산정결과> 수행조건산정Async(운송원장 queue, string vehicle, CancellationToken cancellationToken = default)
    {
        if (vehicle != "Motorcycle") throw new ArgumentException("FoodFulfillmentVehicleUnsupported");
        var option = (routePricingOptions?.Value ?? new FoodDriverRoutePricingOptions()).MotorcycleRouteOption;
        if (option != "traavoidcaronly") throw new ArgumentException("FoodPricingMotorcycleRouteOptionInvalid");
        return CalculateAsync(queue, null, vehicle, option, cancellationToken);
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
        var agreement = agreedRates is not null && driverId is not null && !string.IsNullOrWhiteSpace(queue.원본의뢰Id)
            ? await agreedRates.SelectedAsync(AgreedRateBusinessKinds.FoodDelivery, queue.원본의뢰Id, driverId, vehicle, cancellationToken)
            : null;
        var band = (routePricingOptions?.Value ?? new FoodDriverRoutePricingOptions()).ResolveBand(now);
        if (vehicle == "Car" && band?.CarRouteOption is not null) option = band.CarRouteOption;
        var route = await CalculateDistanceAsync(queue, option, cancellationToken);
        var useStraightLine = straightLineOptions?.Value.Enabled == true && agreement is null;
        var distance = useStraightLine
            ? StraightLineDeliveryPricingPolicy.CalculateDistanceKm(queue.픽업_위도!.Value, queue.픽업_경도!.Value,
                queue.하차_위도!.Value, queue.하차_경도!.Value)
            : route.DistanceKm!.Value;
        FoodDeliveryPayoutPricingPolicy? straightPolicy = null;
        if (useStraightLine)
        {
            var day = DateOnly.FromDateTime(now.UtcDateTime);
            straightPolicy = new(StraightLineDeliveryPricingPolicy.Revision, day, day, policy.기사픽업지급액 ?? 0m,
                policy.기사기본지급액 - (policy.기사픽업지급액 ?? 0m), 1000,
                StraightLineDriverPricingOptions.RatePerKmKrw, StraightLineDeliveryPricingPolicy.RoundingCode, 0, policy.기사최소지급액);
        }
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
            시간대할증액: timeSurcharge,
            합의계산정책: agreement?.FoodPricing,
            기본거리계산정책: straightPolicy,
            거리근거Code: useStraightLine ? StraightLineDeliveryPricingPolicy.DistanceBasisCode : "RouteEstimate");
        var distanceRevision = useStraightLine
            ? $"{StraightLineDeliveryPricingPolicy.Revision}:1000:ProportionalAwayFromZero:included0"
            : $"naver-directions5-vehicle.r2:{vehicle}:{route.RouteOption}";
        var revision = string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"{음식배달기사제안요금Policy.기본판본}|distance:{distanceRevision}|time-band:{band?.Code ?? "None"}:{timeSurcharge}|weather:{policy.기사기상할증정책판본}|demand:food-demand-surcharge.r1:{policy.기사한시수요할증Revision}|policy:{policy.UpdatedAtUtc.ToUniversalTime():O}");
        if (agreement is not null) revision += $"|agreement:{agreement.AgreementId}:{agreement.Revision}:{agreement.TermsDigest}";
        if (useStraightLine) revision += $"|eta-route:naver-directions5-vehicle.r2:{vehicle}:{route.RouteOption}";
        var components = price.기본요금구성!;

        return new 음식배달기사제안요금산정결과(
            price,
            weather,
            revision,
            now.UtcDateTime)
        {
            산정거리Km = distance,
            거리근거Code = useStraightLine ? StraightLineDeliveryPricingPolicy.DistanceBasisCode
                : vehicle == "Car" ? "NaverDirections5CarRouteEstimate" : "NaverDirections5CarOnlyAvoidanceEstimate",
            경로거리Km = route.DistanceKm,
            경로차량Code = vehicle,
            기사Id = driverId,
            경로옵션Code = route.RouteOption,
            경로소요시간초 = route.Duration?.TotalSeconds,
            시간대Code = band?.Code,
            시간대시작분Kst = band?.StartMinuteKst,
            시간대종료분Kst = band?.EndMinuteKst,
            시간대할증액 = timeSurcharge,
            합의요금표 = agreement,
            Pricing = useStraightLine ? new DriverOfferPricingDto
            {
                DistanceBasisCode = StraightLineDeliveryPricingPolicy.DistanceBasisCode,
                RawDistanceMeters = distance * 1000m,
                RatePerKmKrw = StraightLineDriverPricingOptions.RatePerKmKrw,
                IncludedDistanceMeters = 0,
                RoundingCode = StraightLineDeliveryPricingPolicy.RoundingCode,
                DistanceFeeKrw = components.DistanceFeeKrw,
                BaseFeeKrw = components.PickupFeeKrw + components.DropoffFeeKrw,
                MinimumPayoutKrw = price.기본계산정책!.MinimumPayoutKrw,
                SurchargeKrw = price.기상할증액 + price.한시수요할증액 + price.시간대할증액,
                TotalPayoutKrw = price.기사지급예정액,
                PolicyRevision = revision
            } : null
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
