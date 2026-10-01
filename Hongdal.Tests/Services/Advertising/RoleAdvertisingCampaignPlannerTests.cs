using Hongdal.Contracts.Common.Advertising;
using Hongdal.Services.Advertising;
using Microsoft.Extensions.Options;
using 홍달.Services.Options;

namespace Hongdal.Tests.Services.Advertising;

public sealed class RoleAdvertisingCampaignPlannerTests
{
    [Fact]
    public async Task Simulation에서는_공동구매수요자_플랫폼별_초안만_만든다()
    {
        var planner = CreatePlanner(HongdalExecutionMode.Simulation);

        var result = await planner.BuildPlanAsync(ValidRequest(RoleAdvertisingAudienceRoleCodes.GroupPurchaseBuyer));

        Assert.Equal(RoleAdvertisingExecutionStatuses.SimulationPreview, result.ExecutionStatus);
        Assert.False(result.ProviderApiCallGateOpen);
        Assert.Equal(RoleAdvertisingObjectiveCodes.QualifiedLead, result.ObjectiveCode);
        Assert.Equal(3, result.PlatformDrafts.Count);
        Assert.Contains(result.PlatformDrafts, x => x.ProviderCode == RoleAdvertisingProviderCodes.Meta);
        Assert.Contains(result.PlatformDrafts, x => x.ProviderCode == RoleAdvertisingProviderCodes.GoogleAds);
        Assert.Contains(result.PlatformDrafts, x => x.ProviderCode == RoleAdvertisingProviderCodes.NaverSearchAds);
        Assert.DoesNotContain(result.PlatformDrafts, x => x.ProviderCode == RoleAdvertisingProviderCodes.LinkedIn);
        Assert.DoesNotContain(result.Issues, x => x.Severity == RoleAdvertisingIssueSeverities.Error);
    }

    [Fact]
    public async Task 생산자공급자는_LinkedIn_직무산업_초안을_선택할수있다()
    {
        var planner = CreatePlanner(HongdalExecutionMode.Simulation);
        var request = ValidRequest(RoleAdvertisingAudienceRoleCodes.ProducerSupplier);
        request.PreferredProviderCodes = [RoleAdvertisingProviderCodes.LinkedIn];

        var result = await planner.BuildPlanAsync(request);

        var draft = Assert.Single(result.PlatformDrafts);
        Assert.Equal(RoleAdvertisingProviderCodes.LinkedIn, draft.ProviderCode);
        Assert.Contains("Food Production", draft.TargetingHints["industries"]);
        Assert.Contains("Business Development", draft.TargetingHints["jobFunctions"]);
        Assert.Contains(draft.Notes, x => x.Contains("300명", StringComparison.Ordinal));
    }

    [Fact]
    public async Task 현재_0점0에서는_기사_광고를_차단한다()
    {
        var planner = CreatePlanner(HongdalExecutionMode.Simulation);

        var result = await planner.BuildPlanAsync(ValidRequest(RoleAdvertisingAudienceRoleCodes.CargoDriver));

        Assert.Equal(RoleAdvertisingExecutionStatuses.ValidationBlocked, result.ExecutionStatus);
        Assert.Empty(result.PlatformDrafts);
        Assert.Contains(result.Issues, x => x.Code == "FutureRoleBlocked");
    }

    [Fact]
    public async Task 채용성_광고는_정책검토참조가_필요하다()
    {
        var planner = CreatePlanner(HongdalExecutionMode.Simulation);
        var request = ValidRequest(RoleAdvertisingAudienceRoleCodes.ProducerSupplier);
        request.IsEmploymentRelated = true;

        var result = await planner.BuildPlanAsync(request);

        Assert.Equal(RoleAdvertisingExecutionStatuses.ValidationBlocked, result.ExecutionStatus);
        Assert.Contains(result.Issues, x => x.Code == "EmploymentComplianceReviewRequired");
    }

    [Fact]
    public async Task Operational이어도_광고설정이_꺼져있으면_API_gate가_열리지않는다()
    {
        var planner = CreatePlanner(HongdalExecutionMode.Operational);

        var result = await planner.BuildPlanAsync(ValidRequest(RoleAdvertisingAudienceRoleCodes.CommunityMember));

        Assert.Equal(RoleAdvertisingExecutionStatuses.ConfigurationDisabled, result.ExecutionStatus);
        Assert.False(result.ProviderApiCallGateOpen);
        Assert.NotEmpty(result.PlatformDrafts);
    }

    private static RoleAdvertisingCampaignPlanner CreatePlanner(
        HongdalExecutionMode mode,
        RoleAdvertisingOptions? advertisingOptions = null)
        => new(
            new RoleAdvertisingAudienceCatalog(),
            [
                new MetaRoleAdvertisingPlatformAdapter(),
                new GoogleAdsRoleAdvertisingPlatformAdapter(),
                new LinkedInRoleAdvertisingPlatformAdapter(),
                new NaverSearchAdsRoleAdvertisingPlatformAdapter()
            ],
            Options.Create(advertisingOptions ?? new RoleAdvertisingOptions()),
            new HongdalExecutionModePolicy(Options.Create(new HongdalExecutionOptions { Mode = mode })));

    private static RoleAdvertisingCampaignDraftRequest ValidRequest(string roleCode)
        => new()
        {
            CampaignKey = $"role-{roleCode.ToLowerInvariant()}-kr-001",
            AudienceRoleCode = roleCode,
            LandingPageUrl = "https://hongdal.example/roles/join",
            CountryCode = "KR",
            RegionCodes = ["KR-11"],
            LanguageCode = "ko",
            DailyBudget = 30_000,
            CurrencyCode = "KRW",
            Headline = "함께 필요한 일을 공개하고 연결해 보세요",
            Body = "역할에 맞는 게시판에서 조건을 확인하고 직접 참여 의사를 남길 수 있습니다.",
            TracksConversion = true,
            ConsentNoticeUrl = "https://hongdal.example/privacy/ads"
        };
}
