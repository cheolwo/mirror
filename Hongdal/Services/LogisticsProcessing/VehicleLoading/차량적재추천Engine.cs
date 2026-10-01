using Microsoft.EntityFrameworkCore;
using 홍달.도메인.차량;

namespace Hongdal.Services.LogisticsProcessing.VehicleLoading;

public sealed class 차량적재포장요구사항
{
    public string 항목키 { get; set; } = string.Empty;
    public string 항목명 { get; set; } = string.Empty;
    public int 포장개수 { get; set; } = 1;
    public int 포장길이Mm { get; set; }
    public int 포장폭Mm { get; set; }
    public int 포장높이Mm { get; set; }
    public bool 바닥회전가능여부 { get; set; } = true;
    public bool 적층가능여부 { get; set; } = true;
}

public sealed class 차량적재추천요구사항
{
    public decimal? 총중량Kg { get; set; }
    public decimal? 총부피Cbm { get; set; }
    public decimal? 적층불가바닥면적M2 { get; set; }
    public int? 총팔레트개수 { get; set; }
    public string? 온도조건 { get; set; }
    public bool 비눈보호필요 { get; set; }
    public bool 리프트필요 { get; set; }
    public bool 측면상하차필요 { get; set; }
    public bool 장재물 { get; set; }
    public bool 분할운송허용 { get; set; }
    public IReadOnlyList<차량적재포장요구사항> 포장목록 { get; set; } = [];
}

public sealed class 차량적재차량평가
{
    public required 차량제원 차량 { get; init; }
    public decimal 허용중량Kg { get; init; }
    public decimal? 허용부피Cbm { get; init; }
    public decimal? 적재함바닥면적M2 { get; init; }
    public bool 하드조건적합여부 { get; init; }
    public bool 단일운송가능여부 { get; init; }
    public bool 분할운송추천가능여부 { get; init; }
    public int 권장운행횟수 { get; init; } = 1;
    public decimal? 중량사용률Percent { get; init; }
    public decimal? 부피사용률Percent { get; init; }
    public decimal? 팔레트사용률Percent { get; init; }
    public decimal? 바닥면적사용률Percent { get; init; }
    public decimal 미사용용량점수 { get; init; }
    public IReadOnlyList<string> 하드부적합사유 { get; init; } = [];
    public IReadOnlyList<string> 단일운송불가사유 { get; init; } = [];
    public IReadOnlyList<string> 검증경고 { get; init; } = [];
}

public sealed class 차량적재추천분석결과
{
    public IReadOnlyList<차량적재차량평가> 추천후보 { get; init; } = [];
    public IReadOnlyList<차량적재차량평가> 전체평가 { get; init; } = [];
}

public interface I차량적재추천Service
{
    Task<차량적재추천분석결과> 추천Async(
        차량적재추천요구사항 요구사항,
        CancellationToken cancellationToken = default);
}

public sealed class 차량적재추천Service : I차량적재추천Service
{
    private readonly HongdalContext _db;
    private readonly 차량적재추천Engine _engine;

    public 차량적재추천Service(HongdalContext db, 차량적재추천Engine engine)
    {
        _db = db;
        _engine = engine;
    }

    public async Task<차량적재추천분석결과> 추천Async(
        차량적재추천요구사항 요구사항,
        CancellationToken cancellationToken = default)
    {
        var vehicles = await _db.차량제원
            .AsNoTracking()
            .Where(x => x.추천사용여부)
            .Where(x => x.운영권장중량Kg.HasValue || x.최대적재중량Kg > 0)
            .ToListAsync(cancellationToken);

        return _engine.분석(요구사항, vehicles);
    }
}

public sealed class 차량적재추천Engine
{
    public 차량적재추천분석결과 분석(
        차량적재추천요구사항 요구사항,
        IEnumerable<차량제원> 차량목록)
    {
        ArgumentNullException.ThrowIfNull(요구사항);
        ArgumentNullException.ThrowIfNull(차량목록);

        var 전체평가 = 차량목록
            .Select(차량 => 평가(차량, 요구사항))
            .ToArray();

        var 추천후보 = 전체평가
            .Where(x => x.하드조건적합여부)
            .Where(x => x.단일운송가능여부 || 요구사항.분할운송허용)
            .OrderBy(x => x.단일운송가능여부 ? 0 : 1)
            .ThenBy(x => x.권장운행횟수)
            .ThenBy(x => x.미사용용량점수)
            .ThenBy(x => x.차량.추천우선순위)
            .ThenBy(x => x.허용중량Kg)
            .ThenBy(x => x.차량.차량코드, StringComparer.Ordinal)
            .ToArray();

        return new 차량적재추천분석결과
        {
            추천후보 = 추천후보,
            전체평가 = 전체평가
        };
    }

    public 차량적재차량평가 평가(차량제원 차량, 차량적재추천요구사항 요구사항)
    {
        ArgumentNullException.ThrowIfNull(차량);
        ArgumentNullException.ThrowIfNull(요구사항);

        var 하드부적합사유 = new List<string>();
        var 단일운송불가사유 = new List<string>();
        var 검증경고 = new List<string>();

        ValidateCapabilities(차량, 요구사항, 하드부적합사유);
        ValidatePackageDimensions(차량, 요구사항.포장목록, 하드부적합사유, 검증경고);

        var 허용중량Kg = Math.Max(0, 차량.운영권장중량Kg ?? 차량.최대적재중량Kg);
        var 허용부피Cbm = ResolveAllowedCbm(차량);
        var 적재함바닥면적M2 = CalculateFloorArea(차량);
        var 운행횟수비율 = new List<decimal>();

        var 중량사용률 = AddCapacityRatio(
            요구사항.총중량Kg,
            허용중량Kg,
            "중량",
            "kg",
            단일운송불가사유,
            검증경고,
            운행횟수비율);

        var 부피사용률 = AddCapacityRatio(
            요구사항.총부피Cbm,
            허용부피Cbm,
            "부피",
            "cbm",
            단일운송불가사유,
            검증경고,
            운행횟수비율);

        var 팔레트사용률 = AddCapacityRatio(
            요구사항.총팔레트개수,
            차량.팔레트적재개수,
            "팔레트",
            "개",
            단일운송불가사유,
            검증경고,
            운행횟수비율);

        var 바닥면적사용률 = AddCapacityRatio(
            요구사항.적층불가바닥면적M2,
            적재함바닥면적M2,
            "적층 불가 화물 바닥면적",
            "m²",
            단일운송불가사유,
            검증경고,
            운행횟수비율);

        var 권장운행횟수 = 운행횟수비율.Count == 0
            ? 1
            : 운행횟수비율.Max(CeilingToInt);
        var 하드조건적합 = 하드부적합사유.Count == 0;
        var 단일운송가능 = 하드조건적합 && 단일운송불가사유.Count == 0;

        return new 차량적재차량평가
        {
            차량 = 차량,
            허용중량Kg = 허용중량Kg,
            허용부피Cbm = 허용부피Cbm,
            적재함바닥면적M2 = 적재함바닥면적M2,
            하드조건적합여부 = 하드조건적합,
            단일운송가능여부 = 단일운송가능,
            분할운송추천가능여부 = 하드조건적합 && !단일운송가능 && 요구사항.분할운송허용,
            권장운행횟수 = Math.Max(1, 권장운행횟수),
            중량사용률Percent = 중량사용률,
            부피사용률Percent = 부피사용률,
            팔레트사용률Percent = 팔레트사용률,
            바닥면적사용률Percent = 바닥면적사용률,
            미사용용량점수 = CalculateUnusedCapacityScore(
                권장운행횟수,
                중량사용률,
                부피사용률,
                팔레트사용률,
                바닥면적사용률),
            하드부적합사유 = 하드부적합사유,
            단일운송불가사유 = 단일운송불가사유,
            검증경고 = 검증경고
        };
    }

    private static void ValidateCapabilities(
        차량제원 차량,
        차량적재추천요구사항 요구사항,
        ICollection<string> reasons)
    {
        var temperature = 요구사항.온도조건?.Trim();
        if (IsFrozen(temperature) && !차량.냉동가능)
        {
            reasons.Add("냉동 운송 불가");
        }
        else if (IsRefrigerated(temperature) && !차량.냉장가능 && !차량.냉동가능)
        {
            reasons.Add("냉장 운송 불가");
        }

        if (요구사항.비눈보호필요 && !차량.비눈보호가능)
        {
            reasons.Add("비·눈 보호 불가");
        }

        if (요구사항.리프트필요 && !차량.리프트가능)
        {
            reasons.Add("리프트 상하차 불가");
        }

        if (요구사항.측면상하차필요 && !차량.측면상하차가능)
        {
            reasons.Add("측면 상하차 불가");
        }

        if (요구사항.장재물 && !차량.장재물유리)
        {
            reasons.Add("장재물 운송 부적합");
        }
    }

    private static void ValidatePackageDimensions(
        차량제원 차량,
        IReadOnlyList<차량적재포장요구사항> packages,
        ICollection<string> reasons,
        ICollection<string> warnings)
    {
        foreach (var package in packages.Where(x => x.포장개수 > 0))
        {
            if (package.포장길이Mm <= 0 && package.포장폭Mm <= 0 && package.포장높이Mm <= 0)
            {
                warnings.Add($"{ResolvePackageLabel(package)} 포장 치수가 없어 개별 포장 적재 여부를 검증하지 못했습니다.");
                continue;
            }

            if (package.포장길이Mm <= 0 || package.포장폭Mm <= 0 || package.포장높이Mm <= 0)
            {
                warnings.Add($"{ResolvePackageLabel(package)} 포장 치수 일부가 없어 입력된 축만 검증했습니다.");
            }

            var heightFits = package.포장높이Mm <= 0
                             || !차량.적재함높이Mm.HasValue
                             || package.포장높이Mm <= 차량.적재함높이Mm.Value;
            var hasBothBaseDimensions = package.포장길이Mm > 0 && package.포장폭Mm > 0;
            var baseFits = (package.포장길이Mm <= 0 || package.포장길이Mm <= 차량.적재함길이Mm)
                           && (package.포장폭Mm <= 0 || package.포장폭Mm <= 차량.적재함폭Mm);
            var rotatedBaseFits = hasBothBaseDimensions
                                  && package.바닥회전가능여부
                                  && package.포장폭Mm <= 차량.적재함길이Mm
                                  && package.포장길이Mm <= 차량.적재함폭Mm;

            if (!heightFits || (!baseFits && !rotatedBaseFits))
            {
                var vehicleHeight = 차량.적재함높이Mm.HasValue
                    ? 차량.적재함높이Mm.Value.ToString()
                    : "개방형";
                reasons.Add(
                    $"{ResolvePackageLabel(package)} 포장 규격 " +
                    $"{package.포장길이Mm}×{package.포장폭Mm}×{package.포장높이Mm}mm가 " +
                    $"적재함 {차량.적재함길이Mm}×{차량.적재함폭Mm}×{vehicleHeight}mm에 맞지 않음");
            }
        }
    }

    private static decimal? AddCapacityRatio(
        decimal? required,
        decimal? capacity,
        string label,
        string unit,
        ICollection<string> reasons,
        ICollection<string> warnings,
        ICollection<decimal> tripRatios)
    {
        if (!required.HasValue || required.Value <= 0)
        {
            return null;
        }

        if (!capacity.HasValue || capacity.Value <= 0)
        {
            warnings.Add($"차량의 {label} 적재 기준이 없어 해당 조건을 검증하지 못했습니다.");
            return null;
        }

        var ratio = required.Value / capacity.Value;
        tripRatios.Add(ratio);
        if (ratio > 1m)
        {
            reasons.Add($"{label} 초과({required.Value:0.###}{unit} > {capacity.Value:0.###}{unit})");
        }

        return decimal.Round(ratio * 100m, 1, MidpointRounding.AwayFromZero);
    }

    private static decimal? AddCapacityRatio(
        int? required,
        int? capacity,
        string label,
        string unit,
        ICollection<string> reasons,
        ICollection<string> warnings,
        ICollection<decimal> tripRatios)
        => AddCapacityRatio(
            required.HasValue ? (decimal?)required.Value : null,
            capacity.HasValue ? (decimal?)capacity.Value : null,
            label,
            unit,
            reasons,
            warnings,
            tripRatios);

    private static int CeilingToInt(decimal value)
    {
        if (value <= 1m)
        {
            return 1;
        }

        return value >= int.MaxValue
            ? int.MaxValue
            : decimal.ToInt32(decimal.Ceiling(value));
    }

    private static decimal CalculateUnusedCapacityScore(
        int tripCount,
        params decimal?[] utilizationPercentages)
    {
        var known = utilizationPercentages
            .Where(x => x.HasValue)
            .Select(x => Math.Min(100m, x!.Value / Math.Max(1, tripCount)))
            .ToArray();

        if (known.Length == 0)
        {
            return 100m;
        }

        return decimal.Round(
            known.Average(x => 100m - x),
            3,
            MidpointRounding.AwayFromZero);
    }

    private static decimal? CalculateFloorArea(차량제원 차량)
    {
        if (차량.적재함길이Mm <= 0 || 차량.적재함폭Mm <= 0)
        {
            return null;
        }

        return decimal.Round(
            (차량.적재함길이Mm / 1000m) * (차량.적재함폭Mm / 1000m),
            3,
            MidpointRounding.AwayFromZero);
    }

    public static decimal? ResolveAllowedCbm(차량제원 차량)
    {
        decimal? physicalCbm = null;
        if (차량.적재함길이Mm > 0 && 차량.적재함폭Mm > 0 && 차량.적재함높이Mm.GetValueOrDefault() > 0)
        {
            physicalCbm = decimal.Round(
                (차량.적재함길이Mm / 1000m)
                * (차량.적재함폭Mm / 1000m)
                * (차량.적재함높이Mm!.Value / 1000m),
                3,
                MidpointRounding.AwayFromZero);
        }

        if (차량.권장최대CBM is > 0)
        {
            return physicalCbm.HasValue
                ? decimal.Min(physicalCbm.Value, 차량.권장최대CBM.Value)
                : decimal.Round(차량.권장최대CBM.Value, 3, MidpointRounding.AwayFromZero);
        }

        return physicalCbm;
    }

    private static bool IsFrozen(string? value)
        => value is not null
           && (value.Contains("냉동", StringComparison.OrdinalIgnoreCase)
               || value.Contains("frozen", StringComparison.OrdinalIgnoreCase));

    private static bool IsRefrigerated(string? value)
        => value is not null
           && (value.Contains("냉장", StringComparison.OrdinalIgnoreCase)
               || value.Contains("chilled", StringComparison.OrdinalIgnoreCase)
               || value.Contains("refrigerated", StringComparison.OrdinalIgnoreCase));

    private static string ResolvePackageLabel(차량적재포장요구사항 package)
        => !string.IsNullOrWhiteSpace(package.항목명)
            ? package.항목명.Trim()
            : !string.IsNullOrWhiteSpace(package.항목키)
                ? package.항목키.Trim()
                : "화물";
}
