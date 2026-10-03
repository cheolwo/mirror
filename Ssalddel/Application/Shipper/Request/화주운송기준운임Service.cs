using FluentResults;
using Ssalddel.Contracts.Shipper.Request;
using 살뜰.Services.Dispatch.Recommendation;

namespace Ssalddel.Application.Shipper.Request;

public interface I화주운송기준운임Service
{
    Task<Result<화주운송기준운임견적응답>> 견적Async(
        화주운송기준운임견적요청 request,
        CancellationToken cancellationToken = default);
}

public sealed class 화주운송기준운임Service : I화주운송기준운임Service
{
    private readonly SsalddelContext _db;
    private readonly I배차추천경로Service _경로Service;

    public 화주운송기준운임Service(SsalddelContext db, I배차추천경로Service 경로Service)
    {
        _db = db;
        _경로Service = 경로Service;
    }

    public async Task<Result<화주운송기준운임견적응답>> 견적Async(
        화주운송기준운임견적요청 request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.차량종류))
        {
            return Result.Fail<화주운송기준운임견적응답>("차량종류는 기준운임 견적에 필요합니다.");
        }

        var distance = await ResolveDistanceAsync(request, cancellationToken);
        if (!distance.HasValue || distance.Value.DistanceKm <= 0m)
        {
            return Result.Fail<화주운송기준운임견적응답>("예상거리Km 또는 상차/하차 좌표가 기준운임 견적에 필요합니다.");
        }
        var resolvedDistance = distance.Value;

        var rate = await ResolveRateAsync(request.차량종류, cancellationToken);
        if (rate is null)
        {
            return Result.Fail<화주운송기준운임견적응답>($"차량종류 '{request.차량종류}'에 대한 차량단가를 찾을 수 없습니다.");
        }

        return Result.Ok(화주운송기준운임계산기.Calculate(
            request,
            rate.Value,
            resolvedDistance.DistanceKm,
            resolvedDistance.직선거리기준,
            resolvedDistance.거리계산방식));
    }

    private async Task<화주운송거리판정?> ResolveDistanceAsync(
        화주운송기준운임견적요청 request,
        CancellationToken cancellationToken)
    {
        if (TryCreatePoint(request.상차위도, request.상차경도, out var pickup)
            && TryCreatePoint(request.하차위도, request.하차경도, out var dropoff))
        {
            var route = await _경로Service.EstimateRouteAsync(pickup, dropoff);
            if (route?.DistanceKm is > 0m)
            {
                return new 화주운송거리판정(
                    route.DistanceKm.Value,
                    직선거리기준: false,
                    string.IsNullOrWhiteSpace(route.계산방식) ? "경로추정" : route.계산방식);
            }
        }

        if (request.예상거리Km.HasValue && request.예상거리Km.Value > 0m)
        {
            return new 화주운송거리판정(request.예상거리Km.Value, true, "입력거리");
        }

        var straightLineDistanceKm = 화주운송기준운임계산기.ResolveStraightLineDistanceKm(request);
        return straightLineDistanceKm.HasValue
            ? new 화주운송거리판정(straightLineDistanceKm.Value, true, "직선거리")
            : null;
    }

    private static bool TryCreatePoint(decimal? latitude, decimal? longitude, out 배차경로좌표 point)
    {
        if (latitude.HasValue && longitude.HasValue)
        {
            point = new 배차경로좌표(latitude.Value, longitude.Value);
            return true;
        }

        point = default!;
        return false;
    }

    private async Task<화주운송기준운임단가?> ResolveRateAsync(string vehicleType, CancellationToken cancellationToken)
    {
        var rates = await _db.차량단가
            .AsNoTracking()
            .Select(x => new 화주운송기준운임단가(
                x.차량종류,
                x.기본운임,
                x.Km당단가,
                x.최소운임,
                단가출처: "차량단가"))
            .ToListAsync(cancellationToken);

        return 화주운송기준운임계산기.FindRate(vehicleType, rates)
            ?? 화주운송기준운임계산기.FindDefaultRate(vehicleType);
    }
}
