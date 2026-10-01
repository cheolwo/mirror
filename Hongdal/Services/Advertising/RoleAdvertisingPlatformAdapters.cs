using Hongdal.Contracts.Common.Advertising;

namespace Hongdal.Services.Advertising;

public interface IRoleAdvertisingPlatformAdapter
{
    string ProviderCode { get; }
    bool Supports(RoleAdvertisingRoleProfile role, RoleAdvertisingCampaignDraftRequest request);
    RoleAdvertisingPlatformDraft BuildDraft(RoleAdvertisingRoleProfile role, RoleAdvertisingCampaignDraftRequest request);
}

public abstract class RoleAdvertisingPlatformAdapterBase : IRoleAdvertisingPlatformAdapter
{
    public abstract string ProviderCode { get; }
    public abstract bool Supports(RoleAdvertisingRoleProfile role, RoleAdvertisingCampaignDraftRequest request);
    public abstract RoleAdvertisingPlatformDraft BuildDraft(RoleAdvertisingRoleProfile role, RoleAdvertisingCampaignDraftRequest request);

    protected static IReadOnlyList<string> Merge(params IEnumerable<string>[] sources)
        => sources
            .SelectMany(x => x)
            .Select(x => x?.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    protected static IReadOnlyDictionary<string, IReadOnlyList<string>> CommonHints(
        RoleAdvertisingRoleProfile role,
        RoleAdvertisingCampaignDraftRequest request)
        => new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["locations"] = Merge([request.CountryCode], request.RegionCodes),
            ["languages"] = Merge([request.LanguageCode]),
            ["keywords"] = Merge(role.DefaultKeywordHints, request.KeywordHints),
            ["industries"] = Merge(role.DefaultIndustryHints, request.IndustryHints),
            ["jobFunctions"] = Merge(role.DefaultJobFunctionHints, request.JobFunctionHints)
        };
}

public sealed class MetaRoleAdvertisingPlatformAdapter : RoleAdvertisingPlatformAdapterBase
{
    public override string ProviderCode => RoleAdvertisingProviderCodes.Meta;

    public override bool Supports(RoleAdvertisingRoleProfile role, RoleAdvertisingCampaignDraftRequest request) => true;

    public override RoleAdvertisingPlatformDraft BuildDraft(
        RoleAdvertisingRoleProfile role,
        RoleAdvertisingCampaignDraftRequest request)
        => new(
            ProviderCode,
            "Meta",
            "Marketing API / Conversions API",
            "https://graph.facebook.com",
            "/{graph-version}/act_{ad-account-id}/campaigns",
            request.ObjectiveCode == RoleAdvertisingObjectiveCodes.CommunityJoin ? "Traffic or Leads" : "Leads",
            CommonHints(role, request),
            ["campaign management", "ad set and creative management", "lead or conversion measurement"],
            ["Meta developer app", "Business portfolio and ad account", "ads_management permission and access token"],
            [
                "역할명 자체를 개인 식별자로 보내지 않고 지역·관심 맥락의 광고 문안으로 변환합니다.",
                "Conversions API 연결은 별도 동의·중복 제거·Event 품질 검토 뒤 추가합니다."
            ]);
}

public sealed class GoogleAdsRoleAdvertisingPlatformAdapter : RoleAdvertisingPlatformAdapterBase
{
    public override string ProviderCode => RoleAdvertisingProviderCodes.GoogleAds;

    public override bool Supports(RoleAdvertisingRoleProfile role, RoleAdvertisingCampaignDraftRequest request) => true;

    public override RoleAdvertisingPlatformDraft BuildDraft(
        RoleAdvertisingRoleProfile role,
        RoleAdvertisingCampaignDraftRequest request)
        => new(
            ProviderCode,
            "Google Ads",
            "Google Ads API",
            "https://googleads.googleapis.com",
            "/v{api-version}/customers/{customer-id}/campaigns:mutate",
            "Search",
            CommonHints(role, request),
            ["campaign budget", "search campaign", "ad group and keyword management", "conversion reporting"],
            ["Google Ads manager account", "developer token", "OAuth 2.0 credentials", "permissible use for ad creation and management"],
            [
                "검색어가 드러내는 역할 의도를 우선 사용합니다.",
                "Customer Match와 연락처 업로드는 이번 0.0 모듈 범위에서 제외합니다."
            ]);
}

public sealed class LinkedInRoleAdvertisingPlatformAdapter : RoleAdvertisingPlatformAdapterBase
{
    private static readonly IReadOnlySet<string> SupportedRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        RoleAdvertisingAudienceRoleCodes.GroupPurchaseRepresentative,
        RoleAdvertisingAudienceRoleCodes.ProducerSupplier,
        RoleAdvertisingAudienceRoleCodes.Shipper,
        RoleAdvertisingAudienceRoleCodes.WarehouseOperator
    };

    public override string ProviderCode => RoleAdvertisingProviderCodes.LinkedIn;

    public override bool Supports(RoleAdvertisingRoleProfile role, RoleAdvertisingCampaignDraftRequest request)
        => SupportedRoles.Contains(role.RoleCode);

    public override RoleAdvertisingPlatformDraft BuildDraft(
        RoleAdvertisingRoleProfile role,
        RoleAdvertisingCampaignDraftRequest request)
        => new(
            ProviderCode,
            "LinkedIn",
            "Advertising API",
            "https://api.linkedin.com",
            "/rest/adAccounts/{ad-account-id}/adCampaigns",
            "Sponsored Content or Lead Generation",
            CommonHints(role, request),
            ["targeting facets and entities", "audience counts", "campaign management", "conversion tracking and reporting"],
            ["vetted Advertising API access", "OAuth 2.0 rw_ads scope", "LinkedIn ad account mapped to the app", "versioned API headers"],
            [
                "산업·직무·직급 같은 전문 역할 타기팅에만 우선 사용합니다.",
                "집행 전 Audience Counts API로 최소 300명 이상의 대상 규모를 확인합니다."
            ]);
}

public sealed class NaverSearchAdsRoleAdvertisingPlatformAdapter : RoleAdvertisingPlatformAdapterBase
{
    public override string ProviderCode => RoleAdvertisingProviderCodes.NaverSearchAds;

    public override bool Supports(RoleAdvertisingRoleProfile role, RoleAdvertisingCampaignDraftRequest request)
        => string.Equals(request.CountryCode, "KR", StringComparison.OrdinalIgnoreCase);

    public override RoleAdvertisingPlatformDraft BuildDraft(
        RoleAdvertisingRoleProfile role,
        RoleAdvertisingCampaignDraftRequest request)
        => new(
            ProviderCode,
            "NAVER 검색광고",
            "Search Ads API",
            "https://api.searchad.naver.com",
            "/ncc/campaigns",
            "검색 캠페인",
            CommonHints(role, request),
            ["campaign management", "ad group and keyword management", "statistics reporting"],
            ["NAVER 검색광고 계정", "API license", "secret key", "customer id and signed request headers"],
            [
                "국내 지역명과 역할별 검색어 조합을 우선 사용합니다.",
                "전문 직무 속성 타기팅이 아니라 검색 의도 기반 채널로 취급합니다."
            ]);
}
