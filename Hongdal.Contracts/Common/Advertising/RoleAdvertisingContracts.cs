namespace Hongdal.Contracts.Common.Advertising;

public static class RoleAdvertisingProviderCodes
{
    public const string Meta = "Meta";
    public const string GoogleAds = "GoogleAds";
    public const string LinkedIn = "LinkedIn";
    public const string NaverSearchAds = "NaverSearchAds";

    public static IReadOnlySet<string> All { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Meta,
        GoogleAds,
        LinkedIn,
        NaverSearchAds
    };
}

public static class RoleAdvertisingAudienceRoleCodes
{
    public const string CommunityMember = "CommunityMember";
    public const string GroupPurchaseBuyer = "GroupPurchaseBuyer";
    public const string GroupPurchaseRepresentative = "GroupPurchaseRepresentative";
    public const string ProducerSupplier = "ProducerSupplier";
    public const string Shipper = "Shipper";
    public const string WarehouseOperator = "WarehouseOperator";
    public const string CargoDriver = "CargoDriver";
    public const string FoodDeliveryDriver = "FoodDeliveryDriver";

    public static IReadOnlySet<string> CurrentV0 { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        CommunityMember,
        GroupPurchaseBuyer,
        GroupPurchaseRepresentative,
        ProducerSupplier
    };
}

public static class RoleAdvertisingObjectiveCodes
{
    public const string CommunityJoin = "CommunityJoin";
    public const string QualifiedLead = "QualifiedLead";
    public const string SupplyProposal = "SupplyProposal";
    public const string RoleApplication = "RoleApplication";
}

public static class RoleAdvertisingIssueSeverities
{
    public const string Error = "Error";
    public const string Warning = "Warning";
}

public static class RoleAdvertisingExecutionStatuses
{
    public const string ValidationBlocked = "ValidationBlocked";
    public const string SimulationPreview = "SimulationPreview";
    public const string ConfigurationDisabled = "ConfigurationDisabled";
    public const string OperationalDraftReady = "OperationalDraftReady";
}

public sealed class RoleAdvertisingCampaignDraftRequest
{
    public string CampaignKey { get; set; } = string.Empty;
    public string AudienceRoleCode { get; set; } = RoleAdvertisingAudienceRoleCodes.CommunityMember;
    public string ObjectiveCode { get; set; } = string.Empty;
    public string LandingPageUrl { get; set; } = string.Empty;
    public string CountryCode { get; set; } = "KR";
    public IReadOnlyList<string> RegionCodes { get; set; } = [];
    public string LanguageCode { get; set; } = "ko";
    public decimal DailyBudget { get; set; }
    public string CurrencyCode { get; set; } = "KRW";
    public string Headline { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public IReadOnlyList<string> KeywordHints { get; set; } = [];
    public IReadOnlyList<string> IndustryHints { get; set; } = [];
    public IReadOnlyList<string> JobFunctionHints { get; set; } = [];
    public IReadOnlyList<string> PreferredProviderCodes { get; set; } = [];
    public bool TracksConversion { get; set; } = true;
    public string? ConsentNoticeUrl { get; set; }
    public bool IsEmploymentRelated { get; set; }
    public string? ComplianceReviewReference { get; set; }
}

public sealed record RoleAdvertisingRoleProfile(
    string RoleCode,
    string DisplayName,
    string DefaultObjectiveCode,
    string LandingPagePurpose,
    string PrimarySuccessMetric,
    bool IsCurrentV0Role,
    IReadOnlyList<string> RecommendedProviderCodes,
    IReadOnlyList<string> DefaultKeywordHints,
    IReadOnlyList<string> DefaultIndustryHints,
    IReadOnlyList<string> DefaultJobFunctionHints);

public sealed record RoleAdvertisingValidationIssue(
    string Severity,
    string Code,
    string Message);

public sealed record RoleAdvertisingPlatformDraft(
    string ProviderCode,
    string ProviderName,
    string ApiProductName,
    string ApiBaseUrl,
    string CampaignResourceTemplate,
    string RecommendedCampaignType,
    IReadOnlyDictionary<string, IReadOnlyList<string>> TargetingHints,
    IReadOnlyList<string> RequiredCapabilities,
    IReadOnlyList<string> AccountPrerequisites,
    IReadOnlyList<string> Notes);

public sealed record RoleAdvertisingCampaignPlan(
    string CampaignKey,
    RoleAdvertisingRoleProfile? RoleProfile,
    string ObjectiveCode,
    string ExecutionStatus,
    bool ProviderApiCallGateOpen,
    IReadOnlyList<RoleAdvertisingPlatformDraft> PlatformDrafts,
    IReadOnlyList<RoleAdvertisingValidationIssue> Issues);
