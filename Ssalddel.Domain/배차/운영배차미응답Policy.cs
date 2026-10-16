using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Common.Metadata;

namespace 살뜰.도메인.배차;

/// <summary>앱 표시가 확인된 제안의 연속 만료만 집계한다. 운행·기존 배정·다른 서버 제한을 바꾸지 않는다.</summary>
[SsalddelCodeMetadata(SsalddelCodeFeatureKeys.OperationalDispatchCore, SsalddelCodeLayer.Domain,
    "앱 표시가 확인된 세 번째 연속 미응답과 기사의 명시 재개를 원장 사건으로 판정한다.",
    StepKey = "domain.operational-dispatch-no-response", FlowOrder = 26,
    ExecutionStage = SsalddelCodeExecutionStage.Preview, Effects = SsalddelCodeEffect.None,
    ReadsFrom = SsalddelCodeDataScope.OperationalState, WritesTo = SsalddelCodeDataScope.None,
    Boundary = "신규 수신만 중지한다. 사람의 읽음·귀책·운행 종료·기존 배정 취소를 확정하지 않는다.")]
public static class 운영배차미응답Policy
{
    public const int 자동중지기준횟수 = 3;

    public static void 반영(운영배차수신상태Dto 상태, IEnumerable<운영배차활동사건Dto> 사건목록)
    {
        var events = 사건목록.Where(x => x.주체Id == 상태.주체Id)
            .DistinctBy(x => x.사건StableId)
            .OrderBy(x => x.발생시각Utc)
            // 같은 시각의 재개는 이미 발생한 만료보다 뒤에 반영한다.
            .ThenBy(x => 명시재개(x) ? 1 : 0)
            .ThenBy(x => x.사건StableId, StringComparer.Ordinal).ToArray();
        var receipts = events.Where(표시확인).ToArray();
        var count = 0;
        DateTimeOffset? pausedAt = null;
        DateTimeOffset? resumedAt = null;
        var countedOffers = new HashSet<(string, string)>();
        foreach (var item in events)
        {
            if (명시재개(item))
            {
                count = 0;
                pausedAt = null;
                resumedAt = item.발생시각Utc;
            }
            else if (item.주체역할Code == 운영배차주체역할Code.기사
                     && item.제안유효성Code == 운영배차제안유효성Code.유효
                     && item.사건유형Code is 운영배차사건유형Code.수락 or 운영배차사건유형Code.거절)
            {
                count = 0;
                // 이미 자동 중지됐으면 수락/거절 사건만으로 재개하지 않는다.
            }
            else if (item.사건유형Code == 운영배차사건유형Code.무응답
                     && item.주체역할Code == 운영배차주체역할Code.기사
                     && item.제안유효성Code == 운영배차제안유효성Code.유효
                     && item.사유Code == 운영배차미응답Code.표시제안만료
                     && !string.IsNullOrWhiteSpace(item.제안Id)
                     && !countedOffers.Contains((item.제안Id, item.상태값Code))
                     && receipts.Any(x => x.제안Id == item.제안Id && x.상태값Code == item.상태값Code
                         && x.발생시각Utc < item.발생시각Utc
                         && (!resumedAt.HasValue || x.발생시각Utc > resumedAt.Value)))
            {
                countedOffers.Add((item.제안Id!, item.상태값Code));
                count = Math.Min(자동중지기준횟수, count + 1);
                if (count == 자동중지기준횟수) pausedAt ??= item.발생시각Utc;
            }
        }

        상태.연속미응답횟수 = count;
        상태.미응답자동중지여부 = pausedAt.HasValue;
        상태.미응답자동중지시각Utc = pausedAt;
    }

    private static bool 표시확인(운영배차활동사건Dto item)
        => item.사건유형Code == 운영배차사건유형Code.제안표시확인
           && item.주체역할Code == 운영배차주체역할Code.기사
           && item.제안유효성Code == 운영배차제안유효성Code.유효
           && item.사유Code == 운영배차미응답Code.앱표시확인;

    private static bool 명시재개(운영배차활동사건Dto item)
        => item.사건유형Code == 운영배차사건유형Code.수신의사변경
           && item.주체역할Code == 운영배차주체역할Code.기사
           && item.상태값Code == 운영배차수신의사Code.On
           && item.사유Code == 운영배차미응답Code.기사명시재개;
}
