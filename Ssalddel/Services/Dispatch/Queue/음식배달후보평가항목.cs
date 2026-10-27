using Ssalddel.Contracts.Common.Dispatch;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.Storage.Local;

namespace 살뜰.Services.Dispatch.Queue;

/// <summary>한 후보 검토 안에서 확인한 계산과 근거. 운영 상태나 학습 승인 자료가 아니다.</summary>
internal sealed record 음식배달기사항목(
    국내화물운송기사상태Snapshot 기사,
    decimal 거리Km,
    int 공간단계,
    string 공간설명,
    Ssalddel.WorkflowRules.음식배달픽업평가 평가,
    기사권역복귀평가? 복귀평가 = null)
{
    public string 픽업시각설명 => 평가.설명;
    public decimal 추천점수 => 평가.추천점수 + (복귀평가 is { 확인됨: true } ? 복귀평가.가점 : 0m);
}

internal static class 음식배달후보검토기록
{
    internal static void 검토기록(List<음식배달후보검토항목> records, string driverId, int spaceStage,
        string spaceDescription, string code, 국내화물운송기사상태Snapshot? state,
        Ssalddel.WorkflowRules.음식배달픽업평가? evaluation,
        IReadOnlyDictionary<string, 음식배달수요기사판정>? decisions, decimal? distance = null,
        음식후속픽업기준결과? basis = null, decimal? travelMinutes = null, string? routeBasis = null)
    {
        var decision = decisions is not null && decisions.TryGetValue(driverId, out var found) ? found : null;
        var item = new 음식배달후보검토항목(driverId, spaceStage, spaceDescription, code, distance,
            evaluation?.단계, evaluation?.차이분, evaluation?.추천점수, state?.Aging점수,
            decision?.우선순위, decision?.사유Code)
        {
            위치기준Code = basis is null ? null : basis.확인됨
                ? basis.활성업무있음 ? "ActiveScheduleEnd" : "CurrentLocation" : "UnverifiedActiveSchedule",
            기존수행완료예상시각Utc = basis is { 활성업무있음: true, 확인됨: true } ? basis.가용예상시각Utc : null,
            픽업예상소요분 = travelMinutes,
            경로근거Code = routeBasis
        };
        var index = records.FindIndex(x => string.Equals(x.기사Id, driverId, StringComparison.Ordinal));
        if (index < 0) records.Add(item);
        else records[index] = item;
    }

    internal static void 판정갱신(List<음식배달후보검토항목> records, string driverId, string code)
    {
        var index = records.FindIndex(x => string.Equals(x.기사Id, driverId, StringComparison.Ordinal));
        if (index >= 0) records[index] = records[index] with { 판정Code = code };
    }

    internal static void 점수갱신(List<음식배달후보검토항목> records, string driverId, decimal score)
    {
        var index = records.FindIndex(x => string.Equals(x.기사Id, driverId, StringComparison.Ordinal));
        if (index >= 0) records[index] = records[index] with { 추천점수 = score };
    }

}
