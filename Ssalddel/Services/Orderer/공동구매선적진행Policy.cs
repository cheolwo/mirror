using Ssalddel.Contracts.Common.Orderer;

namespace Ssalddel.Services.Orderer;

internal static class 공동구매선적진행Policy
{
    private static readonly string[] Stages =
    [
        공동구매선적상태코드.문서등록, 공동구매선적상태코드.해외포장완료,
        공동구매선적상태코드.선박항공편적재, 공동구매선적상태코드.운송중,
        공동구매선적상태코드.항만도착, 공동구매선적상태코드.통관진행중,
        공동구매선적상태코드.통관완료, 공동구매선적상태코드.물류대행입고준비,
        공동구매선적상태코드.물류대행입고완료, 공동구매선적상태코드.출품준비,
        공동구매선적상태코드.판매채널출품완료, 공동구매선적상태코드.출고배치준비,
        공동구매선적상태코드.국내창고입고, 공동구매선적상태코드.국내기사상차,
        공동구매선적상태코드.공동주택하차, 공동구매선적상태코드.분배진행중,
        공동구매선적상태코드.완료
    ];

    public static string? LastNormalState(string current, string? recorded, IEnumerable<string> history)
        => Array.IndexOf(Stages, current) >= 0 ? current
            : Array.IndexOf(Stages, recorded) >= 0 ? recorded
            : history.Where(x => Array.IndexOf(Stages, x) >= 0)
                .OrderByDescending(x => Array.IndexOf(Stages, x)).FirstOrDefault();

    public static bool CanAdvance(string current, DateTime? currentAt, string incoming, DateTime incomingAt,
        string? lastNormalState = null)
    {
        if (currentAt.HasValue && incomingAt < currentAt.Value) return false;
        if (current == 공동구매선적상태코드.완료 && incoming != current) return false;
        var currentRank = Array.IndexOf(Stages, current);
        if (currentRank < 0) currentRank = Array.IndexOf(Stages, lastNormalState);
        var incomingRank = Array.IndexOf(Stages, incoming);
        if (incoming == 공동구매선적상태코드.예외) return currentRank != Stages.Length - 1;
        return incomingRank >= 0 && (currentRank < 0 || incomingRank >= currentRank);
    }
}
