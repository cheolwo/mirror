namespace 살뜰.Services.Options;

/// <summary>새 기사 제안용 직선거리 정책. 고객 청구액과 기존 약정은 변경하지 않습니다.</summary>
public sealed class StraightLineDriverPricingOptions
{
    public const string SectionName = "StraightLineDriverPricing";
    public bool Enabled { get; set; }
    public const decimal RatePerKmKrw = 1000m;
    public string SmallGoodsPolicyRevision { get; set; } = string.Empty;
    public DateTime? SmallGoodsEffectiveFromUtc { get; set; }
    public DateTime? SmallGoodsEffectiveUntilUtc { get; set; }
    public decimal? SmallGoodsBaseFeeKrw { get; set; }
    public decimal? SmallGoodsMinimumPayoutKrw { get; set; }
    public decimal SmallGoodsWorkFeeKrw { get; set; }
    public decimal SmallGoodsSurchargeKrw { get; set; }
}
