using Hongdal.Contracts.Common.Advertising;
using Microsoft.Extensions.Options;
using 홍달.Services.Options;

namespace Hongdal.Services.Advertising;

public interface IRoleAdvertisingCampaignPlanner
{
    Task<RoleAdvertisingCampaignPlan> BuildPlanAsync(
        RoleAdvertisingCampaignDraftRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class RoleAdvertisingCampaignPlanner : IRoleAdvertisingCampaignPlanner
{
    private readonly IRoleAdvertisingAudienceCatalog _audienceCatalog;
    private readonly IReadOnlyDictionary<string, IRoleAdvertisingPlatformAdapter> _adapters;
    private readonly RoleAdvertisingOptions _options;
    private readonly IHongdalExecutionModePolicy _executionModePolicy;

    public RoleAdvertisingCampaignPlanner(
        IRoleAdvertisingAudienceCatalog audienceCatalog,
        IEnumerable<IRoleAdvertisingPlatformAdapter> adapters,
        IOptions<RoleAdvertisingOptions> options,
        IHongdalExecutionModePolicy executionModePolicy)
    {
        _audienceCatalog = audienceCatalog;
        _adapters = adapters.ToDictionary(x => x.ProviderCode, StringComparer.OrdinalIgnoreCase);
        _options = options.Value;
        _executionModePolicy = executionModePolicy;
    }

    public Task<RoleAdvertisingCampaignPlan> BuildPlanAsync(
        RoleAdvertisingCampaignDraftRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var issues = Validate(request);
        var role = _audienceCatalog.Find(request.AudienceRoleCode);
        if (role is null)
        {
            issues.Add(Error("UnknownAudienceRole", $"지원하지 않는 광고 역할 코드입니다: {request.AudienceRoleCode}"));
            return Task.FromResult(CreateBlockedPlan(request, null, issues));
        }

        if (_options.EnforceV0RoleBoundary && !role.IsCurrentV0Role)
        {
            issues.Add(Error(
                "FutureRoleBlocked",
                $"{role.DisplayName} 광고는 1.0 이후 역할이므로 현재 0.0 외부 광고 계획에서 차단됩니다."));
        }

        var normalized = Normalize(request, role);
        var requestedProviders = normalized.PreferredProviderCodes.Count > 0
            ? normalized.PreferredProviderCodes
            : role.RecommendedProviderCodes;
        var enabledProviders = new HashSet<string>(
            _options.EnabledProviderCodes ?? [],
            StringComparer.OrdinalIgnoreCase);
        var drafts = new List<RoleAdvertisingPlatformDraft>();

        foreach (var providerCode in requestedProviders.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!_adapters.TryGetValue(providerCode, out var adapter))
            {
                issues.Add(Error("UnknownProvider", $"지원하지 않는 광고 플랫폼 코드입니다: {providerCode}"));
                continue;
            }

            if (!enabledProviders.Contains(providerCode))
            {
                issues.Add(Warning("ProviderDisabled", $"{providerCode} Adapter가 설정에서 비활성화되어 계획에서 제외됐습니다."));
                continue;
            }

            if (!adapter.Supports(role, normalized))
            {
                issues.Add(Warning(
                    "ProviderNotSuitableForRole",
                    $"{providerCode}는 {role.DisplayName} 역할 또는 {normalized.CountryCode} 지역에 적합하지 않아 제외됐습니다."));
                continue;
            }

            drafts.Add(adapter.BuildDraft(role, normalized));
        }

        if (drafts.Count == 0 && !issues.Any(x => x.Severity == RoleAdvertisingIssueSeverities.Error))
        {
            issues.Add(Error("NoProviderDraft", "현재 역할과 지역에 사용할 수 있는 광고 플랫폼 계획이 없습니다."));
        }

        var hasErrors = issues.Any(x => x.Severity == RoleAdvertisingIssueSeverities.Error);
        var apiGateOpen = !hasErrors
            && _executionModePolicy.IsOperational
            && _options.Enabled
            && _options.AllowOperationalPublishing;
        var status = ResolveExecutionStatus(hasErrors);

        return Task.FromResult(new RoleAdvertisingCampaignPlan(
            normalized.CampaignKey,
            role,
            normalized.ObjectiveCode,
            status,
            apiGateOpen,
            hasErrors ? [] : drafts,
            issues));
    }

    private List<RoleAdvertisingValidationIssue> Validate(RoleAdvertisingCampaignDraftRequest request)
    {
        var issues = new List<RoleAdvertisingValidationIssue>();

        if (string.IsNullOrWhiteSpace(request.CampaignKey))
        {
            issues.Add(Error("CampaignKeyRequired", "내부 캠페인 키가 필요합니다."));
        }

        if (!IsAbsoluteHttpUrl(request.LandingPageUrl))
        {
            issues.Add(Error("LandingPageUrlInvalid", "역할별 landing page의 절대 HTTP 또는 HTTPS URL이 필요합니다."));
        }

        if (request.DailyBudget <= 0)
        {
            issues.Add(Error("DailyBudgetInvalid", "일일 예산은 0보다 커야 합니다."));
        }

        if (request.CountryCode.Trim().Length != 2)
        {
            issues.Add(Error("CountryCodeInvalid", "국가 코드는 ISO 3166-1 alpha-2 형식이어야 합니다."));
        }

        if (request.CurrencyCode.Trim().Length != 3)
        {
            issues.Add(Error("CurrencyCodeInvalid", "통화 코드는 ISO 4217의 3자리 형식이어야 합니다."));
        }

        if (string.IsNullOrWhiteSpace(request.Headline) || string.IsNullOrWhiteSpace(request.Body))
        {
            issues.Add(Error("CreativeTextRequired", "광고 제목과 본문 초안이 모두 필요합니다."));
        }

        if (request.TracksConversion && !IsAbsoluteHttpUrl(request.ConsentNoticeUrl))
        {
            issues.Add(Error(
                "ConsentNoticeRequired",
                "전환을 측정하려면 광고·분석 데이터 처리 안내 URL이 필요합니다."));
        }

        if (request.IsEmploymentRelated && string.IsNullOrWhiteSpace(request.ComplianceReviewReference))
        {
            issues.Add(Error(
                "EmploymentComplianceReviewRequired",
                "채용 또는 일자리 성격의 광고는 플랫폼별 특별 광고 정책 검토 참조가 필요합니다."));
        }

        return issues;
    }

    private RoleAdvertisingCampaignDraftRequest Normalize(
        RoleAdvertisingCampaignDraftRequest request,
        RoleAdvertisingRoleProfile role)
        => new()
        {
            CampaignKey = request.CampaignKey.Trim(),
            AudienceRoleCode = role.RoleCode,
            ObjectiveCode = string.IsNullOrWhiteSpace(request.ObjectiveCode)
                ? role.DefaultObjectiveCode
                : request.ObjectiveCode.Trim(),
            LandingPageUrl = request.LandingPageUrl.Trim(),
            CountryCode = request.CountryCode.Trim().ToUpperInvariant(),
            RegionCodes = Clean(request.RegionCodes),
            LanguageCode = request.LanguageCode.Trim().ToLowerInvariant(),
            DailyBudget = request.DailyBudget,
            CurrencyCode = request.CurrencyCode.Trim().ToUpperInvariant(),
            Headline = request.Headline.Trim(),
            Body = request.Body.Trim(),
            KeywordHints = Clean(request.KeywordHints),
            IndustryHints = Clean(request.IndustryHints),
            JobFunctionHints = Clean(request.JobFunctionHints),
            PreferredProviderCodes = Clean(request.PreferredProviderCodes),
            TracksConversion = request.TracksConversion,
            ConsentNoticeUrl = request.ConsentNoticeUrl?.Trim(),
            IsEmploymentRelated = request.IsEmploymentRelated,
            ComplianceReviewReference = request.ComplianceReviewReference?.Trim()
        };

    private string ResolveExecutionStatus(bool hasErrors)
    {
        if (hasErrors)
        {
            return RoleAdvertisingExecutionStatuses.ValidationBlocked;
        }

        if (_executionModePolicy.IsSimulation)
        {
            return RoleAdvertisingExecutionStatuses.SimulationPreview;
        }

        return !_options.Enabled || !_options.AllowOperationalPublishing
            ? RoleAdvertisingExecutionStatuses.ConfigurationDisabled
            : RoleAdvertisingExecutionStatuses.OperationalDraftReady;
    }

    private static RoleAdvertisingCampaignPlan CreateBlockedPlan(
        RoleAdvertisingCampaignDraftRequest request,
        RoleAdvertisingRoleProfile? role,
        IReadOnlyList<RoleAdvertisingValidationIssue> issues)
        => new(
            request.CampaignKey.Trim(),
            role,
            request.ObjectiveCode.Trim(),
            RoleAdvertisingExecutionStatuses.ValidationBlocked,
            false,
            [],
            issues);

    private static IReadOnlyList<string> Clean(IEnumerable<string>? values)
        => (values ?? [])
            .Select(x => x?.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static bool IsAbsoluteHttpUrl(string? value)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private static RoleAdvertisingValidationIssue Error(string code, string message)
        => new(RoleAdvertisingIssueSeverities.Error, code, message);

    private static RoleAdvertisingValidationIssue Warning(string code, string message)
        => new(RoleAdvertisingIssueSeverities.Warning, code, message);
}
