using Ssalddel.Contracts.Shipper.Request;

namespace Ssalddel.Application.Shipper.Request;

public readonly record struct 화주운송기준운임단가(
    string 차량종류,
    decimal 기본운임,
    decimal Km당단가,
    decimal 최소운임,
    string 단가출처);

public readonly record struct 화주운송거리판정(
    decimal DistanceKm,
    bool 직선거리기준,
    string 거리계산방식);

public static class 화주운송기준운임계산기
{
    private static readonly 화주운송기준운임단가[] DefaultRates =
    [
        new("오토바이", 5000m, 1000m, 5000m, "v1.0 기본단가"),
        new("오토바이 퀵", 5000m, 1000m, 5000m, "v1.0 기본단가"),
        new("다마스", 15000m, 1200m, 15000m, "v1.0 기본단가"),
        new("라보", 20000m, 1300m, 20000m, "v1.0 기본단가"),
        new("1톤", 35000m, 1300m, 35000m, "v1.0 기본단가"),
        new("1톤 카고", 35000m, 1300m, 35000m, "v1.0 기본단가"),
        new("1톤 탑차", 35000m, 1350m, 35000m, "v1.0 기본단가"),
        new("1톤 윙바디", 35000m, 1400m, 35000m, "v1.0 기본단가"),
        new("냉장탑차", 35000m, 1450m, 35000m, "v1.0 기본단가"),
        new("1톤 냉장탑", 35000m, 1450m, 35000m, "v1.0 기본단가"),
        new("냉동탑차", 35000m, 1500m, 35000m, "v1.0 기본단가"),
        new("1톤 냉동탑", 35000m, 1500m, 35000m, "v1.0 기본단가"),
        new("1.4톤", 45000m, 1550m, 45000m, "v1.0 기본단가"),
        new("1.4톤 카고", 45000m, 1550m, 45000m, "v1.0 기본단가"),
        new("1.4톤 탑차", 45000m, 1600m, 45000m, "v1.0 기본단가"),
        new("1.4톤 윙바디", 45000m, 1650m, 45000m, "v1.0 기본단가"),
        new("2.5톤", 60000m, 1700m, 60000m, "v1.0 기본단가"),
        new("2.5톤 카고", 60000m, 1700m, 60000m, "v1.0 기본단가"),
        new("2.5톤 탑차", 60000m, 1750m, 60000m, "v1.0 기본단가"),
        new("2.5톤 윙바디", 60000m, 1800m, 60000m, "v1.0 기본단가"),
        new("3.5톤", 80000m, 1850m, 80000m, "v1.0 기본단가"),
        new("3.5톤 카고", 80000m, 1850m, 80000m, "v1.0 기본단가"),
        new("5톤", 100000m, 1900m, 100000m, "v1.0 기본단가"),
        new("5톤 카고", 100000m, 1900m, 100000m, "v1.0 기본단가"),
        new("5톤 탑차", 100000m, 1950m, 100000m, "v1.0 기본단가"),
        new("5톤 윙바디", 100000m, 2000m, 100000m, "v1.0 기본단가")
    ];

    public static 화주운송기준운임견적응답 Calculate(
        화주운송기준운임견적요청 request,
        화주운송기준운임단가 rate,
        decimal distanceKm,
        bool 직선거리기준 = true,
        string 거리계산방식 = "직선거리")
    {
        var roundedDistanceKm = decimal.Round(distanceKm, 2, MidpointRounding.AwayFromZero);
        var waitingFee = NormalizeMoney(request.대기료);
        var manualFee = NormalizeMoney(request.수작업비);
        var surcharge = NormalizeMoney(request.할증);
        var distanceFare = NormalizeMoney(roundedDistanceKm * rate.Km당단가);
        var subtotal = rate.기본운임 + distanceFare + waitingFee + manualFee + surcharge;
        var finalFare = subtotal < rate.최소운임 ? rate.최소운임 : subtotal;

        return new 화주운송기준운임견적응답
        {
            차량종류 = rate.차량종류,
            예상거리Km = roundedDistanceKm,
            기본운임 = rate.기본운임,
            Km당단가 = rate.Km당단가,
            거리운임 = distanceFare,
            최소운임 = rate.최소운임,
            대기료 = waitingFee,
            수작업비 = manualFee,
            할증 = surcharge,
            최종운임 = finalFare,
            직선거리기준 = 직선거리기준,
            거리계산방식 = string.IsNullOrWhiteSpace(거리계산방식) ? "직선거리" : 거리계산방식,
            단가출처 = rate.단가출처,
            경고목록 = BuildWarnings(request, rate, 거리계산방식)
        };
    }

    public static decimal? ResolveDistanceKm(화주운송기준운임견적요청 request)
    {
        if (request.예상거리Km.HasValue && request.예상거리Km.Value > 0m)
        {
            return request.예상거리Km.Value;
        }

        return ResolveStraightLineDistanceKm(request);
    }

    public static decimal? ResolveStraightLineDistanceKm(화주운송기준운임견적요청 request)
    {
        if (request.상차위도.HasValue
            && request.상차경도.HasValue
            && request.하차위도.HasValue
            && request.하차경도.HasValue)
        {
            return CalculateStraightLineKm(
                request.상차위도.Value,
                request.상차경도.Value,
                request.하차위도.Value,
                request.하차경도.Value);
        }

        return null;
    }

    public static 화주운송기준운임단가? FindDefaultRate(string vehicleType)
        => FindRate(vehicleType, DefaultRates);

    public static 화주운송기준운임단가? FindRate(
        string vehicleType,
        IEnumerable<화주운송기준운임단가> rates)
    {
        var normalized = NormalizeVehicleType(vehicleType);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        var normalizedRates = rates
            .Select(x => new { Rate = x, Normalized = NormalizeVehicleType(x.차량종류) })
            .Where(x => !string.IsNullOrWhiteSpace(x.Normalized))
            .ToArray();

        var exact = normalizedRates.FirstOrDefault(x => x.Normalized == normalized);
        if (exact is not null)
        {
            return exact.Rate;
        }

        var specificAlias = normalizedRates
            .Where(x => normalized.Contains(x.Normalized, StringComparison.Ordinal))
            .OrderByDescending(x => x.Normalized.Length)
            .FirstOrDefault();
        if (specificAlias is not null)
        {
            return specificAlias.Rate;
        }

        var generic = normalizedRates
            .Where(x => x.Normalized.Contains(normalized, StringComparison.Ordinal))
            .OrderBy(x => x.Rate.기본운임)
            .ThenBy(x => x.Rate.Km당단가)
            .ThenBy(x => x.Normalized.Length)
            .FirstOrDefault();

        return generic is null ? null : generic.Rate;
    }

    private static string NormalizeVehicleType(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim()
                .Replace(" ", string.Empty, StringComparison.Ordinal)
                .Replace("-", string.Empty, StringComparison.Ordinal)
                .Replace("_", string.Empty, StringComparison.Ordinal)
                .ToUpperInvariant();

    private static decimal NormalizeMoney(decimal? value)
        => value.HasValue
            ? decimal.Round(Math.Max(0m, value.Value), 0, MidpointRounding.AwayFromZero)
            : 0m;

    private static IReadOnlyList<string> BuildWarnings(
        화주운송기준운임견적요청 request,
        화주운송기준운임단가 rate,
        string 거리계산방식)
    {
        var warnings = new List<string>();
        if (string.Equals(rate.단가출처, "차량단가", StringComparison.Ordinal))
        {
            return BuildDistanceWarnings(request, 거리계산방식, warnings);
        }

        warnings.Add($"차량단가 테이블에서 '{request.차량종류}' 단가를 찾지 못해 {rate.단가출처}를 적용했습니다.");
        return BuildDistanceWarnings(request, 거리계산방식, warnings);
    }

    private static IReadOnlyList<string> BuildDistanceWarnings(
        화주운송기준운임견적요청 request,
        string 거리계산방식,
        List<string> warnings)
    {
        var hasCoordinates = request.상차위도.HasValue
            && request.상차경도.HasValue
            && request.하차위도.HasValue
            && request.하차경도.HasValue;
        if (hasCoordinates
            && !string.Equals(거리계산방식, "Directions5", StringComparison.Ordinal)
            && !string.Equals(거리계산방식, "Directions5-구간합산", StringComparison.Ordinal))
        {
            warnings.Add($"Directions5 실제 운행거리를 확보하지 못해 {거리계산방식} 기준으로 산정했습니다.");
        }

        return warnings;
    }

    private static decimal CalculateStraightLineKm(
        decimal pickupLat,
        decimal pickupLng,
        decimal dropoffLat,
        decimal dropoffLng)
    {
        const double earthRadiusKm = 6371.0088;

        var lat1 = ToRadians((double)pickupLat);
        var lat2 = ToRadians((double)dropoffLat);
        var deltaLat = ToRadians((double)(dropoffLat - pickupLat));
        var deltaLng = ToRadians((double)(dropoffLng - pickupLng));

        var a = Math.Sin(deltaLat / 2d) * Math.Sin(deltaLat / 2d)
            + Math.Cos(lat1) * Math.Cos(lat2) * Math.Sin(deltaLng / 2d) * Math.Sin(deltaLng / 2d);
        var c = 2d * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1d - a));
        return (decimal)(earthRadiusKm * c);
    }

    private static double ToRadians(double degrees)
        => degrees * Math.PI / 180d;
}
