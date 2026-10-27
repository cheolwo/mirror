using Ssalddel.Contracts.Common.Dispatch;

namespace 살뜰.Services.Dispatch.Queue;

/// <summary>이미 평가된 후보의 세 정렬 분기를 보존한다. 저장소나 시계를 조회하지 않는다.</summary>
internal static class 음식배달후보순위Policy
{
    internal static 음식배달기사항목? 선택(IReadOnlyList<음식배달기사항목> 평가목록,
        IReadOnlyDictionary<string, 음식배달수요기사판정>? decisions)
    {
        return decisions is not null
            ? 평가목록.OrderBy(x => x.평가.단계)
                .ThenBy(x => decisions.TryGetValue(x.기사.DriverId, out var decision) ? decision.우선순위 : int.MaxValue)
                .ThenByDescending(x => x.추천점수).ThenBy(x => x.평가.차이분)
                .ThenBy(x => x.공간단계).ThenBy(x => x.거리Km)
                .ThenBy(x => x.기사.DriverId, StringComparer.Ordinal).FirstOrDefault()
            : 평가목록.Any(x => x.복귀평가 is { 확인됨: true, 가점: > 0m })
            ? 평가목록.OrderBy(x => x.평가.단계).ThenByDescending(x => x.추천점수)
                .ThenBy(x => x.평가.차이분).ThenBy(x => x.공간단계).ThenBy(x => x.거리Km)
                .ThenBy(x => x.기사.DriverId, StringComparer.Ordinal).FirstOrDefault()
            : Ssalddel.WorkflowRules.음식배달픽업평가Policy.정렬(
                평가목록, x => x.평가, x => x.공간단계, x => x.거리Km, x => x.기사.DriverId).FirstOrDefault();
    }
}
