using 살뜰.Services.Dispatch.Queue;

namespace 살뜰.Services.Dispatch.AI;

public interface ICargoDispatchCandidateAI
{
    bool Enabled { get; }
    Task<CargoDispatchAISelection> SelectAsync(용달운송후보검토결과 trace, CancellationToken cancellationToken = default);
}

public sealed record CargoDispatchAISelection(string? DriverId, string StatusCode);

/// <summary>이미 검증된 화물 후보만 줄인다. 현재 규칙 후보보다 대기 가점이 낮은 선택으로 공정성을 완화하지 않는다.</summary>
public static class CargoDispatchCandidateConstraints
{
    public static IReadOnlyList<용달운송후보검토항목> Eligible(용달운송후보검토결과 trace, int maxCandidates = 30)
    {
        if (trace.선정후보 is null || maxCandidates < 1
            || trace.후보목록.Any(x => string.IsNullOrWhiteSpace(x.기사Id))
            || trace.후보목록.Select(x => x.기사Id).Distinct(StringComparer.Ordinal).Count() != trace.후보목록.Count
            || trace.최종허용기사Ids.Distinct(StringComparer.Ordinal).Count() != trace.최종허용기사Ids.Count)
            return [];
        var allowed = trace.최종허용기사Ids.ToHashSet(StringComparer.Ordinal);
        bool Verified(용달운송후보검토항목 item) => item.판정Code == "CandidateAllowed" && allowed.Contains(item.기사Id)
            && item.서버후보 is { } candidate && candidate.DriverId == item.기사Id
            && item.추천점수.HasValue && item.추천점수 == candidate.추천점수
            && item.거리Km is >= 0m && item.대기점수 is >= 0m && item.픽업도착예상시각Utc.HasValue;
        var baseline = trace.후보목록.FirstOrDefault(x => x.기사Id == trace.선정후보.DriverId && Verified(x));
        if (baseline is null) return [];
        return trace.후보목록.Where(x => Verified(x) && x.대기점수 >= baseline.대기점수)
            .OrderBy(x => x.기사Id == baseline.기사Id ? 0 : 1).Take(Math.Min(30, maxCandidates)).ToArray();
    }
}
