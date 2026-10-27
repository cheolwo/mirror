namespace 살뜰.Services.Dispatch.Recommendation;

/// <summary>완성된 삽입 시도의 우선순위와 결과만 계산하며 경로 조회나 시간 누적을 수행하지 않습니다.</summary>
internal static class 운송삽입시도Selector
{
    internal static 운송삽입평가결과 Select(
        IReadOnlyList<운송삽입시도결과> attempts,
        (배차경로좌표? 종료좌표, bool 전체실제경로확인됨, string? 경로옵션) baseline)
    {
        var beneficialRouteChange = attempts
            .Where(x => x.전체완수가능여부
                        && x.경로변경시도여부
                        && x.단순이어가기대비절감분.HasValue
                        && x.단순이어가기대비절감분.Value > 0m)
            .OrderByDescending(x => x.단순이어가기대비절감분 ?? 0m)
            .ThenBy(x => x.총소요시간분 ?? decimal.MaxValue)
            .FirstOrDefault();

        var feasible = attempts
            .Where(x => x.전체완수가능여부)
            .Where(x => !x.경로변경시도여부)
            .OrderBy(x => x.총추가지연분 ?? decimal.MaxValue)
            .ThenByDescending(x => x.총거리Km ?? 0m)
            .FirstOrDefault();

        var fallback = attempts
            .OrderBy(x => x.최대시간위반분 ?? decimal.MaxValue)
            .ThenBy(x => x.총추가지연분 ?? decimal.MaxValue)
            .FirstOrDefault();

        var selected = beneficialRouteChange ?? feasible ?? fallback;
        if (selected is null)
        {
            return new 운송삽입평가결과(false, false, null, false, null, null, null, null, null, ["삽입 시뮬레이션 결과가 없습니다."], [], [], attempts)
            {
                기존일정종료좌표 = baseline.종료좌표,
                기존일정실제경로확인됨 = baseline.전체실제경로확인됨
            };
        }

        return new 운송삽입평가결과(
            beneficialRouteChange is not null || feasible is not null,
            selected.전체완수가능여부,
            selected.삽입인덱스,
            beneficialRouteChange is not null,
            beneficialRouteChange?.단순이어가기대비절감분,
            selected.총소요시간분,
            selected.총거리Km,
            selected.총추가지연분,
            selected.최대시간위반분,
            selected.위반사유,
            selected.경로순서,
            selected.도착예상목록,
            attempts)
        {
            기존일정종료좌표 = baseline.종료좌표,
            후보포함종료좌표 = selected.종료좌표,
            기존일정실제경로확인됨 = baseline.전체실제경로확인됨,
            후보포함실제경로확인됨 = selected.전체실제경로확인됨
                && (baseline.경로옵션 is null || selected.경로옵션 is null
                    || string.Equals(baseline.경로옵션, selected.경로옵션, StringComparison.Ordinal)),
            복귀비교경로옵션 = selected.경로옵션 ?? baseline.경로옵션
        };
    }
}
