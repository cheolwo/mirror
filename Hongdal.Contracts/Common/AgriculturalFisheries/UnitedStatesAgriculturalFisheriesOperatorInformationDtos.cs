namespace Hongdal.Contracts.Common.AgriculturalFisheries;

public static class 미국농어업경영체정보분야Codes
{
    public const string Agriculture = "Agriculture";
    public const string Aquaculture = "Aquaculture";
    public const string WildCaptureFisheries = "WildCaptureFisheries";
    public const string LocalFoodDistribution = "LocalFoodDistribution";
    public const string MeatPoultryEggProcessing = "MeatPoultryEggProcessing";
    public const string SeafoodProcessingAndShipping = "SeafoodProcessingAndShipping";
}

public static class 미국농어업경영체정보기록유형Codes
{
    public const string AggregateStatistics = "AggregateStatistics";
    public const string CertifiedOperationDirectory = "CertifiedOperationDirectory";
    public const string VoluntaryBusinessDirectory = "VoluntaryBusinessDirectory";
    public const string InspectedEstablishmentDirectory = "InspectedEstablishmentDirectory";
    public const string CertifiedShipperDirectory = "CertifiedShipperDirectory";
    public const string PermitSummaryDirectory = "PermitSummaryDirectory";
    public const string ConfidentialAdministrativeRecords =
        "ConfidentialAdministrativeRecords";
}

public static class 미국농어업경영체정보공개범위Codes
{
    public const string PublicAggregateOnly = "PublicAggregateOnly";
    public const string PublicBusinessDirectory = "PublicBusinessDirectory";
    public const string PublicRegionalPermitSummary = "PublicRegionalPermitSummary";
    public const string RestrictedIndividualRecords = "RestrictedIndividualRecords";
}

public static class 미국농어업경영체정보접근방식Codes
{
    public const string Api = "Api";
    public const string CsvDownload = "CsvDownload";
    public const string DynamicSearch = "DynamicSearch";
    public const string PdfDocument = "PdfDocument";
    public const string WebDirectory = "WebDirectory";
    public const string Restricted = "Restricted";
}

public static class 미국농어업경영체정보통합상태Codes
{
    public const string IntegratedAggregateApi = "IntegratedAggregateApi";
    public const string BulkIntegrationCandidate = "BulkIntegrationCandidate";
    public const string OfficialLookupOnly = "OfficialLookupOnly";
    public const string MetadataCataloged = "MetadataCataloged";
    public const string DoNotIngest = "DoNotIngest";
}

public sealed class 미국농어업경영체정보원천조회요청
{
    public string? SearchText { get; init; }

    public string? SectorCode { get; init; }

    public string? RecordTypeCode { get; init; }

    public string? PublicAccessCode { get; init; }

    public string? IntegrationStatusCode { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 50;
}

public sealed class 미국농어업경영체정보원천증거
{
    public string SourceTitle { get; init; } = string.Empty;

    public string SourceUrl { get; init; } = string.Empty;

    public DateOnly ReviewedOn { get; init; }
}

public sealed class 미국농어업경영체정보원천항목
{
    public string SourceKey { get; init; } = string.Empty;

    public string AgencyName { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public IReadOnlyList<string> SectorCodes { get; init; } = [];

    public string RecordTypeCode { get; init; } = string.Empty;

    public string PublicAccessCode { get; init; } = string.Empty;

    public string GeographicScope { get; init; } = string.Empty;

    public IReadOnlyList<string> AccessModeCodes { get; init; } = [];

    public string IntegrationStatusCode { get; init; } = string.Empty;

    public string OfficialUrl { get; init; } = string.Empty;

    public string? MachineReadableAccessUrl { get; init; }

    public string UpdateCycle { get; init; } = string.Empty;

    public bool CanDiscoverBusinesses { get; init; }

    public bool CanConfirmProgramStatus { get; init; }

    public bool CanVerifyTransactionAuthority { get; init; }

    public bool CanAutoInvite { get; init; }

    public bool CanAutoSelectForOperations { get; init; }

    public bool ContainsPotentialPersonalData { get; init; }

    public bool IsComprehensiveRegistry { get; init; }

    public bool RequiresLiveRecheck { get; init; } = true;

    public IReadOnlyList<string> PublicFieldExamples { get; init; } = [];

    public IReadOnlyList<string> AllowedPlatformUses { get; init; } = [];

    public IReadOnlyList<string> ProhibitedPlatformUses { get; init; } = [];

    public IReadOnlyList<string> Limitations { get; init; } = [];

    public IReadOnlyList<미국농어업경영체정보원천증거> Evidence { get; init; } = [];
}

public sealed class 미국농어업경영체정보원천조회응답
{
    public bool Success { get; init; } = true;

    public string MarketCode { get; init; } = "US";

    public DateOnly SnapshotReviewedOn { get; init; }

    public bool HasUnifiedPublicOperatorRegistry { get; init; }

    public bool IndividualOperationRecordsGenerallyConfidential { get; init; } = true;

    public bool DiscoveryOnly { get; init; } = true;

    public string Summary { get; init; } = string.Empty;

    public int Page { get; init; }

    public int PageSize { get; init; }

    public int TotalCount { get; init; }

    public IReadOnlyList<string> AvailableSectorCodes { get; init; } = [];

    public IReadOnlyList<string> AvailableRecordTypeCodes { get; init; } = [];

    public IReadOnlyList<string> AvailablePublicAccessCodes { get; init; } = [];

    public IReadOnlyList<string> AvailableIntegrationStatusCodes { get; init; } = [];

    public IReadOnlyList<string> Notices { get; init; } = [];

    public IReadOnlyList<미국농어업경영체정보원천항목> Items { get; init; } = [];
}
