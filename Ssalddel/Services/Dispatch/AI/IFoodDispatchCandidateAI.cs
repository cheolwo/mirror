using 살뜰.Services.Dispatch.Queue;

namespace 살뜰.Services.Dispatch.AI;

public interface IFoodDispatchCandidateAI
{
    bool Enabled { get; }
    Task<FoodDispatchAISelection> SelectAsync(음식배달후보검토결과 trace, CancellationToken cancellationToken = default);
}

public sealed record FoodDispatchAISelection(string? DriverId, string StatusCode);

/// <summary>현재 서버 후보 관문을 줄일 수만 있으며 탈락 후보를 복원하지 않는다.</summary>
public static class FoodDispatchCandidateConstraints
{
    public static IReadOnlyList<음식배달후보검토항목> Eligible(음식배달후보검토결과 trace, int maxCandidates = 30)
    {
        if (trace.선정후보 is null || maxCandidates < 1
            || trace.후보목록.Select(x => x.기사Id).Distinct(StringComparer.Ordinal).Count() != trace.후보목록.Count
            || trace.최종허용기사Ids.Distinct(StringComparer.Ordinal).Count() != trace.최종허용기사Ids.Count)
            return [];

        var allowed = trace.최종허용기사Ids.ToHashSet(StringComparer.Ordinal);
        var baseline = trace.후보목록.FirstOrDefault(x => x.기사Id == trace.선정후보.DriverId
            && x.판정Code == "CandidateAllowed" && allowed.Contains(x.기사Id));
        if (baseline is null || baseline.픽업단계 is null)
            return [];

        var candidates = trace.후보목록.Where(x => x.판정Code == "CandidateAllowed"
            && allowed.Contains(x.기사Id) && x.픽업단계 == baseline.픽업단계
            && (!trace.수요자료최신 || x.기사Id == baseline.기사Id
                || baseline.수요우선순위.HasValue && x.수요우선순위.HasValue
                    && x.수요우선순위.Value <= baseline.수요우선순위.Value));
        // C1 is always the baseline, even when truncating a large candidate set.
        return candidates.OrderBy(x => x.기사Id == baseline.기사Id ? 0 : 1)
            .Take(Math.Min(30, maxCandidates)).ToArray();
    }
}
