namespace 살뜰.도메인.기사;

/// <summary>공식기관 적격 판정이 아닌 기사·실차별 검토 원장. 비공개 근거와 이력은 persistence에서 암호화한다.</summary>
public sealed class 운송자격Record
{
    public string DriverId { get; set; } = "";
    public int Revision { get; set; }
    public string Status { get; set; } = "Pending";
    public string PolicyVersion { get; set; } = "";
    public string VehicleKind { get; set; } = "";
    public string VehicleProfileValue { get; set; } = "";
    public string PrivateDetailsJson { get; set; } = "{}";
    public string HistoryJson { get; set; } = "[]";
    public DateTime SubmittedAtUtc { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public DateTime? RecheckAtUtc { get; set; }
    public string? ReviewerId { get; set; }
}
