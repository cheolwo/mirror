using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Services.LogisticsProcessing.VehicleLoading;

namespace Ssalddel.Application.Shipper.Request;

public interface I차량추천Service
{
    Task<차량추천응답> 추천Async(차량추천요청 request, CancellationToken cancellationToken);
}

public sealed class 차량추천Service : I차량추천Service
{
    private readonly I차량적재추천Service _loadingRecommendation;

    public 차량추천Service(I차량적재추천Service loadingRecommendation)
    {
        _loadingRecommendation = loadingRecommendation;
    }

    public async Task<차량추천응답> 추천Async(
        차량추천요청 request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var normalized = 화물적재입력Policy.계산(화물적재입력Policy.입력생성(request));
        var warnings = normalized.경고.Concat(normalized.오류).ToList();
        var reasons = new List<string>();
        var estimatedCbm = normalized.유효부피Cbm;
        AddCalculationReasons(request, estimatedCbm, reasons);
        if (!normalized.유효여부) return EmptyResponse(normalized, reasons, warnings);

        var loadingRequirement = BuildLoadingRequirement(normalized);
        var analysis = await _loadingRecommendation.추천Async(loadingRequirement, cancellationToken);

        if (analysis.전체평가.Count == 0)
        {
            warnings.Add("추천에 사용할 차량 제원 기준이 비어 있습니다.");
            return EmptyResponse(normalized, reasons, warnings);
        }

        var matches = analysis.추천후보
            .Where(x => x.단일운송가능여부)
            .Take(5)
            .ToArray();

        if (matches.Length == 0)
        {
            warnings.Add("입력한 조건으로 한 번에 운송할 수 있는 차량 제원을 찾지 못했습니다.");
            var nearestSplit = analysis.전체평가
                .Where(x => x.하드조건적합여부)
                .OrderBy(x => x.권장운행횟수)
                .ThenBy(x => x.미사용용량점수)
                .FirstOrDefault();
            if (nearestSplit is not null && nearestSplit.권장운행횟수 > 1)
            {
                warnings.Add(
                    $"{nearestSplit.차량.차량명} 기준 약 {nearestSplit.권장운행횟수}회 분할 운송이 필요합니다.");
            }

            return EmptyResponse(normalized, reasons, warnings);
        }

        foreach (var warning in matches.SelectMany(x => x.검증경고).Distinct(StringComparer.Ordinal))
        {
            warnings.Add(warning);
        }

        var candidates = matches
            .Select((match, index) => ToCandidate(match, index + 1))
            .ToArray();

        return new 차량추천응답
        {
            적재조건 = normalized,
            추천차량종류 = candidates[0].차량종류,
            추정화물부피Cbm = estimatedCbm,
            추천사유 = reasons,
            경고목록 = warnings.Distinct(StringComparer.Ordinal).ToArray(),
            후보목록 = candidates
        };
    }

    private static 차량적재추천요구사항 BuildLoadingRequirement(화물적재입력결과 normalized)
    {
        var input = normalized.입력;
        var packages = new List<차량적재포장요구사항>();
        if (input.길이Mm.HasValue || input.폭Mm.HasValue || input.높이Mm.HasValue)
        {
            packages.Add(new 차량적재포장요구사항
            {
                항목키 = "shipper-cargo",
                항목명 = string.IsNullOrWhiteSpace(input.화물종류) ? "화물" : input.화물종류.Trim(),
                포장개수 = normalized.적용수량,
                포장길이Mm = input.길이Mm.GetValueOrDefault(),
                포장폭Mm = input.폭Mm.GetValueOrDefault(),
                포장높이Mm = input.높이Mm.GetValueOrDefault(),
                바닥회전가능여부 = normalized.바닥회전가능여부,
                적층가능여부 = normalized.적층가능여부
            });
        }

        return new 차량적재추천요구사항
        {
            총중량Kg = input.총중량Kg,
            총부피Cbm = normalized.유효부피Cbm,
            적층불가바닥면적M2 = normalized.비적층바닥면적M2,
            총팔레트개수 = input.팔레트개수,
            온도조건 = input.온도조건,
            비눈보호필요 = 화물적재입력Policy.요청문구포함(input, "비") || 화물적재입력Policy.요청문구포함(input, "방수"),
            리프트필요 = 화물적재입력Policy.요청문구포함(input, "리프트"),
            측면상하차필요 = 화물적재입력Policy.요청문구포함(input, "측면"),
            장재물 = 화물적재입력Policy.요청문구포함(input, "장재물"),
            분할운송허용 = false,
            포장목록 = packages
        };
    }

    private static void AddCalculationReasons(
        차량추천요청 request,
        decimal? estimatedCbm,
        ICollection<string> reasons)
    {
        if (estimatedCbm.HasValue)
        {
            reasons.Add($"화물 부피 {estimatedCbm.Value:0.###}cbm와 차량별 권장 적재 CBM을 비교했습니다.");
        }

        if (request.화물중량Kg.HasValue)
        {
            reasons.Add($"총 화물 중량 {request.화물중량Kg.Value:0.###}kg을 운영 권장 중량과 비교했습니다.");
        }

        if (request.팔레트개수.HasValue)
        {
            reasons.Add($"팔레트 {request.팔레트개수.Value}개 적재 가능 여부를 확인했습니다.");
        }

        if (!string.IsNullOrWhiteSpace(request.화물온도조건))
        {
            reasons.Add($"온도조건 {request.화물온도조건.Trim()} 기준으로 후보를 제한했습니다.");
        }

        if (request.화물길이Mm.HasValue || request.화물폭Mm.HasValue || request.화물높이Mm.HasValue)
        {
            reasons.Add("개별 외포장 치수와 바닥 회전 가능 여부를 차량 적재함 제원과 비교했습니다.");
        }

        reasons.Add("차량 추천 우선순위보다 실제 적재 여유가 적은 차량을 먼저 배치했습니다.");
    }

    private static 차량추천후보응답 ToCandidate(차량적재차량평가 match, int priority)
        => new()
        {
            차량코드 = match.차량.차량코드,
            차량종류 = match.차량.차량명,
            우선순위 = priority,
            적재가능중량Kg = match.허용중량Kg,
            적재가능부피Cbm = match.허용부피Cbm,
            적재가능팔레트개수 = match.차량.팔레트적재개수,
            단일운송가능여부 = match.단일운송가능여부,
            권장운행횟수 = match.권장운행횟수,
            중량사용률Percent = match.중량사용률Percent,
            부피사용률Percent = match.부피사용률Percent,
            팔레트사용률Percent = match.팔레트사용률Percent,
            설명 = BuildDescription(match)
        };

    private static string BuildDescription(차량적재차량평가 match)
    {
        var parts = new List<string>
        {
            $"권장중량 {match.허용중량Kg:0.###}kg"
        };

        if (match.허용부피Cbm.HasValue)
        {
            parts.Add($"적재부피 {match.허용부피Cbm.Value:0.###}cbm");
        }
        if (match.차량.팔레트적재개수.HasValue)
        {
            parts.Add($"팔레트 {match.차량.팔레트적재개수.Value}개");
        }
        if (match.중량사용률Percent.HasValue)
        {
            parts.Add($"중량 사용률 {match.중량사용률Percent.Value:0.#}%");
        }
        if (match.부피사용률Percent.HasValue)
        {
            parts.Add($"부피 사용률 {match.부피사용률Percent.Value:0.#}%");
        }
        if (match.차량.냉동가능)
        {
            parts.Add("냉동 가능");
        }
        else if (match.차량.냉장가능)
        {
            parts.Add("냉장 가능");
        }

        return string.Join(" / ", parts);
    }

    private static 차량추천응답 EmptyResponse(
        화물적재입력결과 normalized,
        IReadOnlyList<string> reasons,
        IReadOnlyList<string> warnings)
        => new()
        {
            적재조건 = normalized,
            추천차량종류 = string.Empty,
            추정화물부피Cbm = normalized.유효부피Cbm,
            추천사유 = reasons,
            경고목록 = warnings,
            후보목록 = []
        };
}
