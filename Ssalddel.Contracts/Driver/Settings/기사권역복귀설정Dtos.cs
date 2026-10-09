namespace Ssalddel.Contracts.Driver.Settings;

/// <summary>현재 GPS와 별도로 보관하는 기사 본인의 비공개 복귀 기준점입니다.</summary>
public sealed class 기사권역복귀설정
{
    public long Revision { get; set; }
    public string Mode { get; set; } = "off";
    public string? 기준위치명 { get; set; }
    public decimal? 기준위도 { get; set; }
    public decimal? 기준경도 { get; set; }
    public decimal 진입반경Km { get; set; } = 7m;
    public decimal 해제반경Km { get; set; } = 6m;
}

public sealed class 기사권역복귀설정수정요청
{
    public long ExpectedRevision { get; set; }
    public 기사권역복귀설정? 설정 { get; set; }
}

public sealed class 기사권역복귀설정응답
{
    public string DriverId { get; set; } = string.Empty;
    public 기사권역복귀설정 설정 { get; set; } = new();
    public bool 활성여부 { get; set; }
    public DateTime? 위치판정시각Utc { get; set; }
}
