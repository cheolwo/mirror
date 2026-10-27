using 살뜰.도메인.기사;
using 살뜰.Services.Dispatch.Recommendation;

namespace 살뜰.Services.Dispatch.Coordination;

// 외부 평가가 끝난 값만 조립한다. 근거 재검사와 조회 완료 시각은 Factory가 소유한다.
internal static class 국내화물조합평가Composer
{
    public static decimal 점수계산(
        배차추천평가결과 evaluation,
        decimal agingScore,
        기사권역복귀평가? returnEvaluation,
        decimal? estimatedCost,
        decimal? estimatedProfit,
        복귀부담평가 returnBurden)
    {
        var score = (evaluation.추천점수 ?? 0m) + agingScore + (returnEvaluation?.가점 ?? 0m);
        if (estimatedCost.HasValue)
        {
            score -= Math.Clamp(estimatedCost.Value / 5000m, 0m, 20m);
        }

        if (estimatedProfit.HasValue)
        {
            score += Math.Clamp(estimatedProfit.Value / 3000m, -20m, 30m);
        }

        if (returnBurden.부담점수 > 0m)
        {
            score -= returnBurden.부담점수;
        }

        if (returnBurden.보너스점수 > 0m)
        {
            score += returnBurden.보너스점수;
        }

        return score;
    }

    public static 운송의뢰기사조합평가 조립(
        국내화물조합평가자료 input,
        decimal score,
        IReadOnlyList<string> excluded)
    {
        if (excluded.Count > 0)
        {
            score = Math.Min(score, 0m);
        }

        var reason = input.Aging점수 > 0m
            ? $"{input.추천평가.추천사유} · 기사대기보정 +{input.Aging점수:0}"
            : input.추천평가.추천사유;
        if (!string.IsNullOrWhiteSpace(input.복귀부담.사유))
        {
            reason = $"{reason} · {input.복귀부담.사유}";
        }
        if (input.권역복귀평가 is not null)
            reason = $"{reason} · {input.권역복귀평가.사유}";

        var badges = input.추천평가.배지.ToList();
        if (input.권역복귀평가 is { 가점: > 0m }) badges.Add("권역 복귀 방향");
        if (input.복귀부담.퇴근시간대부담여부)
        {
            badges.Add("퇴근복귀부담");
        }

        if (input.복귀부담.복귀콜선호 == 기사복귀선호코드.복귀우선 && input.복귀부담.보너스점수 > 0m)
        {
            badges.Add("복귀콜선호");
        }
        else if (input.복귀부담.복귀콜선호 == 기사복귀선호코드.수익우선 && input.복귀부담.부담점수 > 0m)
        {
            badges.Add("수익우선");
        }

        return new 운송의뢰기사조합평가(
            input.의뢰Id,
            input.기사Id,
            excluded.Count == 0,
            input.상차지거리Km.HasValue ? Math.Round(input.상차지거리Km.Value, 2) : null,
            input.상차지이동시간분,
            input.화물운송시간분,
            input.총예상시간분,
            input.총예상거리Km,
            input.예상톨비,
            input.예상운임,
            input.예상총비용,
            input.예상순이익,
            input.일정평가?.삽입가능여부 ?? input.현재수락운송건수 == 0,
            input.일정평가?.전체완수가능여부 ?? true,
            input.일정평가?.최적삽입인덱스,
            input.일정평가?.경로변경이점여부 ?? false,
            input.일정평가?.경로변경절감분,
            input.일정평가?.총추가지연분,
            input.동일배달권여부,
            input.인접배달권여부,
            input.하차후복귀거리Km.HasValue ? Math.Round(input.하차후복귀거리Km.Value, 2) : null,
            Math.Round(input.복귀부담.부담점수, 2),
            input.복귀부담.퇴근시간대부담여부,
            Math.Round(score, 2),
            reason,
            badges.Distinct(StringComparer.Ordinal).ToArray(),
            input.차량적합성.경고.Concat(input.추천평가.경고).Distinct(StringComparer.Ordinal).ToArray(),
            excluded.Distinct(StringComparer.Ordinal).ToArray());
    }
}

internal sealed record 국내화물조합평가자료(
    string 의뢰Id,
    string 기사Id,
    decimal Aging점수,
    int 현재수락운송건수,
    decimal? 상차지거리Km,
    decimal? 상차지이동시간분,
    decimal? 화물운송시간분,
    decimal? 총예상시간분,
    decimal? 총예상거리Km,
    decimal? 예상톨비,
    decimal? 예상운임,
    decimal? 예상총비용,
    decimal? 예상순이익,
    운송삽입평가결과? 일정평가,
    bool 동일배달권여부,
    bool 인접배달권여부,
    decimal? 하차후복귀거리Km,
    복귀부담평가 복귀부담,
    기사권역복귀평가? 권역복귀평가,
    배차추천평가결과 추천평가,
    차량화물적합성결과 차량적합성);
