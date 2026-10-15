namespace 살뜰.도메인.창고;

public sealed class 분산보관합의Record
{
    public string AgreementId { get; set; } = "";
    public string SiteId { get; set; } = "";
    public string LocationId { get; set; } = "";
    public string SpaceOwnerUserId { get; set; } = "";
    public string GoodsOwnerUserId { get; set; } = "";
    public string CreatorUserId { get; set; } = "";
    public string TermsJson { get; set; } = "{}";
    // 새 변경안만으로 현재 유효 계약과 기존 재고 조건을 덮어쓰지 않습니다.
    public string? PendingTermsJson { get; set; }
    public int? PendingTermsVersion { get; set; }
    public bool PendingSpaceOwnerConsented { get; set; }
    public bool PendingGoodsOwnerConsented { get; set; }
    public string HistoryJson { get; set; } = "[]";
    public DateTime StartsAtUtc { get; set; }
    public DateTime EndsAtUtc { get; set; }
    public int TermsVersion { get; set; } = 1;
    public long Revision { get; set; } = 1;
    public bool SpaceOwnerConsented { get; set; }
    public bool GoodsOwnerConsented { get; set; }
    public string StatusCode { get; set; } = "Draft";
}
