using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Ssalddel.Services.Development.MobileFieldTest;
using Ssalddel.Services.Food;
using 살뜰.Data;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.External.Google;
using 살뜰.Services.External.Opinet;
using 살뜰.Services.Options;
using 살뜰.Services.Storage.Local;
using 살뜰.Services.Weather;
using 살뜰.도메인.기사;
using 살뜰.도메인.음식;
using 살뜰.도메인.운송;

internal static class UsbR67SyntheticRegistration
{
    internal static void AddUsbR67SyntheticAdapters(this IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Singleton<IOpinetAveragePriceService, UsbR67SyntheticFuel>());
        services.Replace(ServiceDescriptor.Scoped<I배차추천경로Service, UsbR67SyntheticRoutes>());
        services.Replace(ServiceDescriptor.Scoped<IGeocodingService, UsbR67SyntheticGeocoding>());
        services.Replace(ServiceDescriptor.Scoped<IKakao좌표변환Service, UsbR67SyntheticGeocoding>());
        services.Replace(ServiceDescriptor.Scoped<I음식배달기사제안요금Service, UsbR67SyntheticFoodPricing>());
    }
}

internal sealed class UsbR67SyntheticFuel : IOpinetAveragePriceService
{
    public Task<IReadOnlyList<OpinetAveragePriceItem>> GetAveragePricesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<OpinetAveragePriceItem>>(
            [new() { ProductCode = "UsbR67Synthetic", ProductName = "합성 검증 유가", Price = 1700m }]);
    }
}

/// <summary>허용된 fixture 주소만 해석한다. 실제 주소 검색이나 실제 GPS 증거가 아니다.</summary>
internal sealed class UsbR67SyntheticGeocoding : IGeocodingService, IKakao좌표변환Service
{
    private static (decimal lat, decimal lng)? Resolve(string address) => address switch
    {
        모바일음식배차시험자료.PickupAddress => (37.580450m, 127.087950m),
        모바일음식배차시험자료.DropoffAddress => (37.582000m, 127.090000m),
        "격리 검증 상차 창고" => (37.588m, 127.085m),
        "합성 검증 하차지 도로명 주소" => (37.590m, 127.087m),
        "픽업 장소 1" => (37.588m, 127.085m),
        "전달 장소 1" => (37.590m, 127.087m),
        "전달 장소 2" => (37.584m, 127.094m),
        "전달 장소 3" => (37.592m, 127.092m),
        "픽업 장소 4" => (37.583m, 127.086m),
        "전달 장소 4" => (37.585m, 127.089m),
        "픽업 장소 5" => (37.588m, 127.092m),
        "전달 장소 5" => (37.590m, 127.095m),
        "픽업 장소 6" => (37.594m, 127.082m),
        "전달 장소 6" => (37.597m, 127.085m),
        _ => null
    };
    public Task<(decimal lat, decimal lng)?> GeocodeAsync(string address) => Task.FromResult(Resolve(address));
    public Task<Kakao주소정보?> 주소정보조회Async(string address, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var point = Resolve(address) ?? throw new ArgumentException("UsbR67AddressOutsideFixture");
        return Task.FromResult<Kakao주소정보?>(new(address, address, "합성 시험 지역", "합성 권역", "합성 구역", point.lat, point.lng));
    }
    public Task<(double 위도, double 경도)?> 도로명주소좌표변환Async(string address, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var point = Resolve(address) ?? throw new ArgumentException("UsbR67AddressOutsideFixture");
        return Task.FromResult<(double 위도, double 경도)?>(((double)point.lat, (double)point.lng));
    }
    public Task<Kakao지역정보?> 좌표지역정보조회Async(decimal latitude, decimal longitude, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (latitude is < 37.57m or > 37.60m || longitude is < 127.07m or > 127.10m)
            throw new ArgumentException("UsbR67CoordinatesOutsideFixture");
        return Task.FromResult<Kakao지역정보?>(new("합성 시험 지역", "합성 권역", "합성 구역", "실제 지역 검색 아님"));
    }
}

/// <summary>근거리 fixture 좌표의 직선 합성 추정만 반환한다. 실제경로여부는 항상 false다.</summary>
internal sealed class UsbR67SyntheticRoutes : I배차추천경로Service
{
    public decimal? CalculateDistanceKm(배차경로좌표 source, 배차경로좌표 target)
        => StraightLineDeliveryPricingPolicy.CalculateDistanceKm(source.Latitude, source.Longitude, target.Latitude, target.Longitude);
    public Task<배차경로좌표?> ResolveOriginLocationAsync(string id, 용달기사? driver, DriverLocationSnapshot? location, 배차추천검색조건? criteria)
        => Task.FromResult<배차경로좌표?>(location is null ? null : new(location.Latitude, location.Longitude));
    public Task<배차경로좌표?> ResolveRouteAnchorLocationAsync(string id, 용달기사? driver, DriverLocationSnapshot? location)
        => Task.FromResult<배차경로좌표?>(location is null ? null : new(location.Latitude, location.Longitude));
    public Task<배차경로예상결과?> EstimateRouteAsync(배차경로좌표? origin, 배차경로좌표? destination)
    {
        if (origin is null || destination is null) return Task.FromResult<배차경로예상결과?>(null);
        var distance = CalculateDistanceKm(origin, destination);
        return Task.FromResult<배차경로예상결과?>(distance is null ? null : new(distance, TimeSpan.FromMinutes((double)distance.Value * 3), 0m, "UsbR67SyntheticStraightLine", false));
    }
    public async Task<배차경로예상결과?> EstimateRouteAsync(배차경로좌표? origin, 배차경로좌표? destination, string routeOption, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = await EstimateRouteAsync(origin, destination);
        return result is null ? null : result with { RouteOption = routeOption };
    }
    public Task<배차삽입경로예상결과?> EstimateInsertionDelayAsync(배차경로좌표? origin, 배차경로좌표? anchor, 배차경로좌표? pickup, 배차경로좌표? dropoff)
        => Task.FromResult<배차삽입경로예상결과?>(null);
    public Task<배차경로예상결과?> EstimateOrderedRouteAsync(배차경로좌표? origin, IReadOnlyList<배차경로좌표> stops, CancellationToken cancellationToken = default)
        => Task.FromResult<배차경로예상결과?>(null);
}

/// <summary>실제 경로 관문을 위조하지 않는 전용 합성 요금 adapter. 계산은 기존 요금 Policy가 소유한다.</summary>
internal sealed class UsbR67SyntheticFoodPricing(SsalddelContext db, CargoJourneySettings settings,
    TimeProvider clock, IOptions<FoodDriverRoutePricingOptions> routeOptions) : I음식배달기사제안요금Service
{
    public Task<음식배달기사제안요금산정결과> 산정Async(운송원장 queue, CancellationToken cancellationToken = default)
        => throw new ArgumentException("UsbR67FoodDriverRequired");
    public async Task<음식배달기사제안요금산정결과> 산정Async(운송원장 queue, string driverId, CancellationToken cancellationToken = default)
    {
        settings.ValidateSqlConnection(db.Database.GetDbConnection().ConnectionString);
        if (!await db.Users.AsNoTracking().AnyAsync(x => x.Id == driverId && x.UserName == "driver1@ssalddel.local", cancellationToken)
            || !await db.배달기사.AsNoTracking().AnyAsync(x => x.기사Id == driverId && x.차량 == "오토바이" && x.메모.Contains("usb-r67"), cancellationToken)
            || queue.픽업_도로명주소 != 모바일음식배차시험자료.PickupAddress
            || queue.하차_도로명주소 != 모바일음식배차시험자료.DropoffAddress
            || queue.픽업_위도 != 37.580450m || queue.픽업_경도 != 127.087950m
            || queue.하차_위도 != 37.582000m || queue.하차_경도 != 127.090000m
            || !await db.음식주문.AsNoTracking().AnyAsync(x => x.주문번호 == queue.의뢰Id && x.음식점Id == 101 && !x.모의처리처수행, cancellationToken))
            throw new ArgumentException("UsbR67FoodPricingOutsideFixture");
        var policy = await db.음식운영정책.AsNoTracking().SingleOrDefaultAsync(x => x.Id == 1, cancellationToken)
            ?? new 음식운영정책 { UpdatedAtUtc = DateTime.UnixEpoch };
        var now = clock.GetUtcNow();
        var band = routeOptions.Value.ResolveBand(now);
        var distance = StraightLineDeliveryPricingPolicy.CalculateDistanceKm(37.580450m, 127.087950m, 37.582000m, 127.090000m);
        var price = 음식배달기사제안요금Policy.판정(policy, distance, false, false, now, true, queue.의뢰Id,
            시간대할증액: band?.MotorcycleSurchargeKrw ?? 0m, 거리근거Code: "UsbR67SyntheticStraightLine");
        return new(price, new(false, false, "UsbR67WeatherNotObserved", null, null, null, "Synthetic", null),
            "usb-r67.synthetic-pricing.r1|ActualMapRequest=false", now.UtcDateTime)
        {
            기사Id = driverId, 경로차량Code = "Motorcycle", 경로옵션Code = "UsbR67SyntheticStraightLine",
            산정거리Km = distance, 거리근거Code = "UsbR67SyntheticStraightLine",
            시간대Code = band?.Code, 시간대할증액 = band?.MotorcycleSurchargeKrw ?? 0m
        };
    }
}
