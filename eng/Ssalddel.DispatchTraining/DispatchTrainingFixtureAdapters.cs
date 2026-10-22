using System.Text.RegularExpressions;
using FluentResults;
using Ssalddel.Application.Shipper.Request;
using Ssalddel.Contracts.Shipper.Request;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.External.Google;
using 살뜰.Services.Storage.Local;
using 살뜰.도메인.기사;
using 살뜰.도메인.운송;

namespace Ssalddel.DispatchTraining;

/// <summary>정해진 합성 주소만 좌표로 바꾼다. 다른 주소를 실주소처럼 추정하거나 외부로 전송하지 않는다.</summary>
internal sealed class DispatchTrainingGeocoding : IGeocodingService
{
    private static readonly Regex Address = new("\\A가상 (픽업|전달) ([0-9]{2})\\z", RegexOptions.CultureInvariant);

    public Task<(decimal lat, decimal lng)?> GeocodeAsync(string address)
    {
        var match = Address.Match(address ?? string.Empty);
        if (!match.Success || !int.TryParse(match.Groups[2].Value, out var index) || index is < 1 or > 99)
            return Task.FromResult<(decimal lat, decimal lng)?>(null);
        // 실제 건물과 연결하지 않는 작은 합성 격자다. 등록·견적·Handler 모두 같은 이름을 재조회한다.
        var row = (index - 1) % 10;
        var column = (index - 1) / 10;
        var pickup = match.Groups[1].Value == "픽업";
        return Task.FromResult<(decimal lat, decimal lng)?>(
            (37.58m + row * 0.0005m + (pickup ? 0m : 0.005m),
             127.08m + column * 0.0005m + (pickup ? 0m : 0.005m)));
    }
}

/// <summary>외부 경로를 사용하지 않는 합성 견적이다. 픽업 1천원+전달 1천원+직선 100m당 100원이다.</summary>
internal sealed class DispatchTrainingFareService : I화주운송기준운임Service
{
    public const string Source = "DispatchTrainingSynthetic.Pickup1000.Dropoff1000.Per100m100";

    public Task<Result<화주운송기준운임견적응답>> 견적Async(화주운송기준운임견적요청 request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.차량종류 != DispatchTrainingSeed.VehicleName
            || request.상차위도 is null || request.상차경도 is null || request.하차위도 is null || request.하차경도 is null)
            return Task.FromResult(Result.Fail<화주운송기준운임견적응답>("DispatchTrainingFareInputInvalid"));
        decimal distance;
        try
        {
            distance = StraightLineDeliveryPricingPolicy.CalculateDistanceKm(request.상차위도.Value,
                request.상차경도.Value, request.하차위도.Value, request.하차경도.Value);
        }
        catch (ArgumentException)
        {
            return Task.FromResult(Result.Fail<화주운송기준운임견적응답>("DispatchTrainingFareCoordinatesInvalid"));
        }
        if (distance <= 0m || distance > 10m)
            return Task.FromResult(Result.Fail<화주운송기준운임견적응답>("DispatchTrainingFareOutsideFixtureRegion"));
        // 고객 견적의 기존 반올림·요금 항목 계산기를 재사용한다. 실제 상품·회사 요율이 아니다.
        var result = 화주운송기준운임계산기.Calculate(request, new(DispatchTrainingSeed.VehicleName, 2000m, 1000m, 2000m, Source),
            distance, true, "SyntheticStraightLine");
        result.경고목록 = ["합성 요율: 픽업 1,000원 + 전달 1,000원 + 직선 100m당 100원. 실제 상품·회사 운임이 아닙니다.",
            "주행거리·실제 ETA가 아니며 고객 견적 계산기의 소수 둘째 자리 거리 반올림을 사용합니다."];
        return Task.FromResult(Result.Ok(result));
    }
}

/// <summary>격리 훈련의 합성 격자·고정 속도로만 구간을 만든다. 현실 도로·ETA·차량 적격성 확인이 아니다.</summary>
internal sealed class DispatchTrainingFixtureRoutes : I배차추천경로Service
{
    internal const string Source = "DispatchTrainingSynthetic.StraightLine.LegSum.r1";
    internal const string RouteOption = "DispatchTrainingSyntheticOnly";
    internal const decimal DefaultSpeedKph = 20m;
    private readonly decimal speedKph;

    public DispatchTrainingFixtureRoutes() : this(DefaultSpeedKph) { }
    internal DispatchTrainingFixtureRoutes(decimal speedKph)
    {
        if (speedKph is < 1m or > 120m) throw new ArgumentOutOfRangeException(nameof(speedKph));
        this.speedKph = speedKph;
    }

    public decimal? CalculateDistanceKm(배차경로좌표 source, 배차경로좌표 target)
        => InFixture(source) && InFixture(target)
            ? StraightLineDeliveryPricingPolicy.CalculateDistanceKm(source.Latitude, source.Longitude, target.Latitude, target.Longitude)
            : null;
    public Task<배차경로좌표?> ResolveOriginLocationAsync(string id, 용달기사? driver, DriverLocationSnapshot? location, 배차추천검색조건? criteria)
        => Task.FromResult(Point(location));
    public Task<배차경로좌표?> ResolveRouteAnchorLocationAsync(string id, 용달기사? driver, DriverLocationSnapshot? location)
        => Task.FromResult(Point(location));
    public Task<배차경로예상결과?> EstimateRouteAsync(배차경로좌표? origin, 배차경로좌표? destination)
        => Task.FromResult(Leg(origin, destination));
    public Task<배차경로예상결과?> EstimateRouteAsync(배차경로좌표? origin, 배차경로좌표? destination, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Leg(origin, destination));
    }

    public async Task<배차삽입경로예상결과?> EstimateInsertionDelayAsync(배차경로좌표? origin, 배차경로좌표? anchor, 배차경로좌표? pickup, 배차경로좌표? dropoff)
    {
        if (!InFixture(origin) || !InFixture(pickup) || !InFixture(dropoff) || anchor is not null && !InFixture(anchor)) return null;
        var baseline = Leg(origin, anchor ?? dropoff)!;
        var inserted = await EstimateOrderedRouteAsync(origin, anchor is null ? [pickup!, dropoff!] : [pickup!, anchor, dropoff!]);
        if (inserted is null) return null;
        var baseMinutes = (decimal)baseline.Duration!.Value.TotalMinutes;
        var insertedMinutes = (decimal)inserted.Duration!.Value.TotalMinutes;
        return new(baseline.DistanceKm, inserted.DistanceKm, baseMinutes, insertedMinutes,
            Math.Max(0m, insertedMinutes - baseMinutes), 0m);
    }

    public Task<배차경로예상결과?> EstimateOrderedRouteAsync(배차경로좌표? origin, IReadOnlyList<배차경로좌표> stops, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!InFixture(origin) || stops is null || stops.Count == 0) return Task.FromResult<배차경로예상결과?>(null);
        decimal distance = 0m;
        long ticks = 0;
        var current = origin;
        foreach (var stop in stops)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var leg = Leg(current, stop);
            // 미확보 중간 구간을 건너뛰거나 시작~끝 직선으로 축약하지 않는다.
            if (leg is null) return Task.FromResult<배차경로예상결과?>(null);
            distance += leg.DistanceKm!.Value;
            ticks = checked(ticks + leg.Duration!.Value.Ticks);
            current = stop;
        }
        return Task.FromResult<배차경로예상결과?>(Result(distance, TimeSpan.FromTicks(ticks)));
    }

    private 배차경로예상결과? Leg(배차경로좌표? origin, 배차경로좌표? destination)
    {
        if (!InFixture(origin) || !InFixture(destination)) return null;
        var distance = CalculateDistanceKm(origin!, destination!)!.Value;
        var ticks = decimal.ToInt64(decimal.Round(distance / speedKph * TimeSpan.TicksPerHour, 0, MidpointRounding.AwayFromZero));
        return Result(distance, TimeSpan.FromTicks(ticks));
    }

    private 배차경로예상결과 Result(decimal distance, TimeSpan duration)
        // 제품 DTO의 true는 이 시험에서 각 leg를 제공하는 mock 값이다. 현실 실측/길찾기 확인으로 내보내지 않는다.
        => new(distance, duration, 0m, FormattableString.Invariant($"{Source}.Speed{speedKph}Kph"), true) { RouteOption = RouteOption };
    private static bool InFixture(배차경로좌표? point)
        => point is { Latitude: >= 37.58m and <= 37.59m, Longitude: >= 127.08m and <= 127.09m };
    private static 배차경로좌표? Point(DriverLocationSnapshot? location)
    {
        var point = location is null ? null : new 배차경로좌표(location.Latitude, location.Longitude);
        return InFixture(point) ? point : null;
    }
}
