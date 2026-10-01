namespace Hongdal.Domain.AgriculturalFisheries;

public static class KamisArchiveStatusCodes
{
    public const string Running = "Running";
    public const string Completed = "Completed";
    public const string Failed = "Failed";
}

public sealed class KamisPriceCollectionRun
{
    public long Id { get; set; }

    public string RunKey { get; set; } = Guid.NewGuid().ToString("N");

    public string StatusCode { get; set; } = KamisArchiveStatusCodes.Running;

    public DateOnly RequestedDate { get; set; }

    public DateOnly? LatestSurveyDate { get; set; }

    public string QuerySummary { get; set; } = string.Empty;

    public string SourceUrl { get; set; } = string.Empty;

    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAtUtc { get; set; }

    public int FetchedCount { get; set; }

    public int InsertedCount { get; set; }

    public int UpdatedCount { get; set; }

    public int ExistingCount { get; set; }

    public string ErrorMessage { get; set; } = string.Empty;

    public ICollection<KamisPriceObservation> NewObservations { get; set; } =
        new List<KamisPriceObservation>();
}

public sealed class KamisPriceObservation
{
    public long Id { get; set; }

    public long FirstCollectionRunId { get; set; }

    public KamisPriceCollectionRun? FirstCollectionRun { get; set; }

    public string RecordKey { get; set; } = string.Empty;

    public string ProductClassCode { get; set; } = string.Empty;

    public string ProductClassName { get; set; } = string.Empty;

    public string CategoryCode { get; set; } = string.Empty;

    public string CategoryName { get; set; } = string.Empty;

    public string CountryCode { get; set; } = string.Empty;

    public string CountryName { get; set; } = string.Empty;

    public DateOnly RequestedDate { get; set; }

    public DateOnly SurveyDate { get; set; }

    public string FrequencyCode { get; set; } = "Daily";

    public string ItemName { get; set; } = string.Empty;

    public string ItemCode { get; set; } = string.Empty;

    public string KindName { get; set; } = string.Empty;

    public string KindCode { get; set; } = string.Empty;

    public string RankName { get; set; } = string.Empty;

    public string RankCode { get; set; } = string.Empty;

    public string Unit { get; set; } = string.Empty;

    public string PriceRaw { get; set; } = string.Empty;

    public decimal? PriceKrw { get; set; }

    public string PreviousDayLabel { get; set; } = string.Empty;

    public decimal? PreviousDayPriceKrw { get; set; }

    public string OneWeekAgoLabel { get; set; } = string.Empty;

    public decimal? OneWeekAgoPriceKrw { get; set; }

    public string TwoWeeksAgoLabel { get; set; } = string.Empty;

    public decimal? TwoWeeksAgoPriceKrw { get; set; }

    public string OneMonthAgoLabel { get; set; } = string.Empty;

    public decimal? OneMonthAgoPriceKrw { get; set; }

    public string OneYearAgoLabel { get; set; } = string.Empty;

    public decimal? OneYearAgoPriceKrw { get; set; }

    public string NormalYearLabel { get; set; } = string.Empty;

    public decimal? NormalYearPriceKrw { get; set; }

    public bool IsPriceMissing { get; set; }

    public string SourceUrl { get; set; } = string.Empty;

    public string RawJson { get; set; } = "{}";

    public DateTime FirstCollectedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime LastSeenAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
