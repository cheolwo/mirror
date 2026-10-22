namespace Ssalddel.Contracts.Common.Home;

/// <summary>공개 홈에 제공하는 누적 화물 완료 기록 집계입니다. 수령·정산·실지급 또는 운영 감사의 증명이 아닙니다.</summary>
public sealed class HomeParticipationSummaryResponse
{
    public string PeriodCode { get; set; } = "all-time";
    public DateTimeOffset AsOfUtc { get; set; }
    public string ExecutionMode { get; set; } = "";
    public string StatusCode { get; set; } = "recorded";
    /// <summary>확정 이익의 공개 집계 정본이 연결되지 않아 현재는 null입니다.</summary>
    public decimal? ConfirmedPlatformProfitKrw { get; set; }
    /// <summary>예정 보수·수령 진술과 구분한 실지급 정본이 연결되지 않아 현재는 null입니다.</summary>
    public decimal? ConfirmedSharedPayoutKrw { get; set; }
    public int? CompletedFreightTransportCount { get; set; }
    /// <summary>완료 기록에 결속된 화주 Identity 계정 수이며 가입자·온라인 인원 수가 아닙니다.</summary>
    public int? FreightShipperAccountCount { get; set; }
    /// <summary>완료 기록의 현재 확정 기사와 수행 기사 결속이 일치하는 Identity 계정 수입니다.</summary>
    public int? FreightDriverAccountCount { get; set; }
    public bool HasIncompleteParticipantBindings { get; set; }
}
