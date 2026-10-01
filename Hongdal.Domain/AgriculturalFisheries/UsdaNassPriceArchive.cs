namespace Hongdal.Domain.AgriculturalFisheries;

public static class UsdaNassArchiveStatusCodes
{
    public const string Running = "Running";
    public const string Completed = "Completed";
    public const string Failed = "Failed";
}

public static class HsUsdaMappingReviewStatusCodes
{
    public const string NeedsReview = "NeedsReview";
    public const string InReview = "InReview";
    public const string Reviewed = "Reviewed";
    public const string Rejected = "Rejected";
}

public sealed class UsdaNassPriceCollectionRun
{
    public long Id { get; set; }

    public string RunKey { get; set; } = Guid.NewGuid().ToString("N");

    public string StatusCode { get; set; } = UsdaNassArchiveStatusCodes.Running;

    public int YearFrom { get; set; }

    public string QuerySummary { get; set; } = string.Empty;

    public string SourceUrl { get; set; } = string.Empty;

    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAtUtc { get; set; }

    public DateTime? LatestSourceLoadTimeUtc { get; set; }

    public int FetchedCount { get; set; }

    public int InsertedCount { get; set; }

    public int ExistingCount { get; set; }

    public string ErrorMessage { get; set; } = string.Empty;

    public ICollection<UsdaNassPriceObservation> NewObservations { get; set; } =
        new List<UsdaNassPriceObservation>();
}

public sealed class UsdaNassPriceObservation
{
    public long Id { get; set; }

    public long FirstCollectionRunId { get; set; }

    public UsdaNassPriceCollectionRun? FirstCollectionRun { get; set; }

    public string RecordKey { get; set; } = string.Empty;

    public string SourceDesc { get; set; } = string.Empty;

    public string SectorDesc { get; set; } = string.Empty;

    public string GroupDesc { get; set; } = string.Empty;

    public string CommodityDesc { get; set; } = string.Empty;

    public string ClassDesc { get; set; } = string.Empty;

    public string UtilPracticeDesc { get; set; } = string.Empty;

    public string ProductionPracticeDesc { get; set; } = string.Empty;

    public string StatisticCategoryDesc { get; set; } = string.Empty;

    public string UnitDesc { get; set; } = string.Empty;

    public string ShortDesc { get; set; } = string.Empty;

    public string DomainDesc { get; set; } = string.Empty;

    public string DomainCategoryDesc { get; set; } = string.Empty;

    public string AggregationLevelDesc { get; set; } = string.Empty;

    public string CountryCode { get; set; } = string.Empty;

    public string CountryName { get; set; } = string.Empty;

    public int Year { get; set; }

    public string FrequencyDesc { get; set; } = string.Empty;

    public string BeginCode { get; set; } = string.Empty;

    public string EndCode { get; set; } = string.Empty;

    public string ReferencePeriodDesc { get; set; } = string.Empty;

    public string ValueRaw { get; set; } = string.Empty;

    public decimal? NumericValue { get; set; }

    public bool IsSuppressed { get; set; }

    public string CvPercentRaw { get; set; } = string.Empty;

    public DateTime? SourceLoadTimeUtc { get; set; }

    public string SourceUrl { get; set; } = string.Empty;

    public string RawJson { get; set; } = "{}";

    public DateTime FirstCollectedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime LastSeenAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class HsUsdaCommodityMapping
{
    public long Id { get; set; }

    public string MappingKey { get; set; } = string.Empty;

    public string HsCode6 { get; set; } = string.Empty;

    public string ProductNameKo { get; set; } = string.Empty;

    public string HsDescriptionEn { get; set; } = string.Empty;

    public string UsdaCommodityDesc { get; set; } = string.Empty;

    public string UsdaClassDesc { get; set; } = string.Empty;

    public string UsdaUtilPracticeDesc { get; set; } = string.Empty;

    public string UsdaProductionPracticeDesc { get; set; } = string.Empty;

    public string MatchQualityCode { get; set; } = "Candidate";

    public string ReviewStatusCode { get; set; } = HsUsdaMappingReviewStatusCodes.NeedsReview;

    public string ReviewOwnerUserId { get; set; } = string.Empty;

    public string ReviewNote { get; set; } = string.Empty;

    public string SourceUrl { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
