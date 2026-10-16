using Ssalddel.Contracts.Common.Dispatch;

namespace 살뜰.Services.Dispatch.Common;

/// <summary>append-only 의사 사건의 개수도 결속하여 같은 시각에 추가된 의사 변경을 놓치지 않습니다.</summary>
internal static class 운영배차수신의사판본
{
    public const string 최초On사유 = "DriverConditionalInitialOn";
    public static string? 계산(IEnumerable<운영배차활동사건Dto> events)
    {
        var intents = events.Where(x => x.사건유형Code == 운영배차사건유형Code.수신의사변경
                && x.주체역할Code == 운영배차주체역할Code.기사)
            .DistinctBy(x => x.사건StableId).OrderBy(기록순서)
            .ThenBy(x => x.사건StableId, StringComparer.Ordinal).ToArray();
        return intents.Length == 0 ? null : $"dispatch-intent-version:{intents.Length}:{intents[^1].사건StableId}";
    }

    public static string? 요청근거(bool conditional, string? expectedToken)
        => conditional ? "initial-on:" + (expectedToken ?? "absent") : null;

    public static DateTimeOffset 기록순서(운영배차활동사건Dto item)
        => item.원장기록시각Utc ?? item.발생시각Utc;
}
