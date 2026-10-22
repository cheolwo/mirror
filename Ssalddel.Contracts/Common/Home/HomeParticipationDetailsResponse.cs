namespace Ssalddel.Contracts.Common.Home;

/// <summary>공개 누적 집계와 도착 시각 기준 월별 집계만 제공합니다. 개별 운송·계정이나 자금 원문은 포함하지 않습니다.</summary>
public sealed class HomeParticipationDetailsResponse
{
    public HomeParticipationSummaryResponse Summary { get; set; } = new();
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 12;
    public bool HasMore { get; set; }
    public string MonthlyPeriodBasisCode { get; set; } = "arrival-month-kst";
    /// <summary>기록이 있는 월만 최신순으로 제공합니다. 월별 distinct 계정 수의 합은 누적 계정 수가 아닙니다.</summary>
    public IReadOnlyList<HomeParticipationMonthSummary> MonthlyItems { get; set; } = [];
}

public sealed class HomeParticipationMonthSummary
{
    public string MonthCode { get; set; } = "";
    public int CompletedFreightTransportCount { get; set; }
    public int FreightShipperAccountCount { get; set; }
    public int FreightDriverAccountCount { get; set; }
    public bool HasIncompleteParticipantBindings { get; set; }
}
