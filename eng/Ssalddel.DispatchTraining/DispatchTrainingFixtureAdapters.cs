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

/// <summary>후보 검색의 직선거리만 재사용한다. 실제 경로·ETA·삽입 경로를 인증하지 않는다.</summary>
internal sealed class DispatchTrainingFixtureRoutes : I배차추천경로Service
{
    public decimal? CalculateDistanceKm(배차경로좌표 source, 배차경로좌표 target)
        => StraightLineDeliveryPricingPolicy.CalculateDistanceKm(source.Latitude, source.Longitude, target.Latitude, target.Longitude);
    public Task<배차경로좌표?> ResolveOriginLocationAsync(string id, 용달기사? driver, DriverLocationSnapshot? location, 배차추천검색조건? criteria)
        => Task.FromResult<배차경로좌표?>(location is null ? null : new(location.Latitude, location.Longitude));
    public Task<배차경로좌표?> ResolveRouteAnchorLocationAsync(string id, 용달기사? driver, DriverLocationSnapshot? location)
        => Task.FromResult<배차경로좌표?>(location is null ? null : new(location.Latitude, location.Longitude));
    public Task<배차경로예상결과?> EstimateRouteAsync(배차경로좌표? origin, 배차경로좌표? destination)
        => Task.FromResult<배차경로예상결과?>(null);
    public Task<배차삽입경로예상결과?> EstimateInsertionDelayAsync(배차경로좌표? origin, 배차경로좌표? anchor, 배차경로좌표? pickup, 배차경로좌표? dropoff)
        => Task.FromResult<배차삽입경로예상결과?>(null);
    public Task<배차경로예상결과?> EstimateOrderedRouteAsync(배차경로좌표? origin, IReadOnlyList<배차경로좌표> stops, CancellationToken cancellationToken = default)
        => Task.FromResult<배차경로예상결과?>(null);
}
