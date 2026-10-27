using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Ssalddel.Contracts.Common.Operations;
using Ssalddel.Controllers.Admin.Operations;
using Ssalddel.Services.Commerce;
using Ssalddel.Services.Operations;
using 살뜰.Services.Options;

namespace Ssalddel.Tests.Services.Operations;

public sealed class 화물플랫폼등록준비ServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void 기본값은_무료지만_자료와_기관등록은_미확인이고_영업을_열지_않는다()
    {
        var result = Create().조회();
        Assert.True(result.FreeConnectionConfigured);
        Assert.False(result.IsPreparationComplete);
        Assert.False(result.ActualRegistrationConfirmed);
        Assert.False(result.RequiredAuthorityProceduresConfirmed);
        Assert.False(result.CanActivateOperations);
        Assert.Equal("Unknown", result.PlatformRegistrationStatusCode);
        Assert.Equal("Unknown", result.BrokeragePermitStatusCode);
        Assert.Contains("office", result.MissingRequirementCodes);
        Assert.Contains("violation-review-report", result.MissingRequirementCodes);
        Assert.Equal(new DateOnly(2027, 9, 23), result.RegistrationEffectiveDate);
    }

    [Fact]
    public void 정액정보요금_후보는_확정요금이나_현재건별정산에_적용되지_않는다()
    {
        var usage = new 기사이용료정책Options();
        var result = Create(usage: usage).조회();
        var plan = Assert.IsType<FreightInformationSubscriptionPlanDto>(result.PlannedInformationSubscription);
        Assert.Equal("MonthlyInformationSubscriptionDraft", plan.ModelCode);
        Assert.Equal(new decimal[] { 5000m, 10000m }, plan.CandidateMonthlyAmountsKrw);
        Assert.Null(plan.ConfirmedMonthlyAmountKrw);
        Assert.False(plan.PerShipmentCommissionPlanned);
        Assert.True(plan.RealtimeRecommendationsPlanned);
        Assert.True(plan.PartyFinalDecisionRequired);
        Assert.False(plan.BillingImplemented);
        Assert.False(plan.LegalClassificationConfirmed);
        Assert.True(result.FreeConnectionConfigured);
        Assert.True(usage.무료배차);
        Assert.Equal(0m, usage.기본이용료);
        Assert.Equal(0m, usage.월상한이용료);
        Assert.Equal(0m, usage.월누적이용료계산(0));
        Assert.Equal(0m, usage.월누적이용료계산(100));
        Assert.Contains("information-subscription-classification", result.MissingRequirementCodes);
        Assert.False(result.CanActivateOperations);
    }

    [Theory]
    [InlineData("Prepared", true)]
    [InlineData("Submitted", true)]
    [InlineData("Confirmed", false)]
    public void 정액정보추천은_준비나접수만으로_기관분류확인이_되지_않는다(string status, bool receipt)
    {
        var options = PreparedOptions();
        options.Requirements["information-subscription-classification"] = Evidence(status);
        if (receipt) options.Requirements["information-subscription-classification"].AuthorityReceiptReference = "classification-reply-ref";
        var result = Create(options, ReadyCommerce()).조회();
        Assert.False(result.IsPreparationComplete);
        Assert.False(result.PlannedInformationSubscription!.LegalClassificationConfirmed);
        Assert.Contains("information-subscription-classification", result.MissingRequirementCodes);
        Assert.False(result.CanActivateOperations);
    }

    [Fact]
    public void 정액정보추천의_기관분류확인도_등록허가신고나_영업허용을_대신하지_않는다()
    {
        var result = Create(PreparedOptions(), ReadyCommerce()).조회();
        Assert.True(result.PlannedInformationSubscription!.LegalClassificationConfirmed);
        Assert.True(result.IsPreparationComplete);
        Assert.False(result.ActualRegistrationConfirmed);
        Assert.False(result.RequiredAuthorityProceduresConfirmed);
        Assert.Equal("Unknown", result.BrokeragePermitStatusCode);
        Assert.Null(result.PlannedInformationSubscription.ConfirmedMonthlyAmountKrw);
        Assert.False(result.PlannedInformationSubscription.BillingImplemented);
        Assert.False(result.CanActivateOperations);
    }

    [Fact]
    public void 정액정보추천의_분류회신도_만료나미래검토근거는_보류한다()
    {
        var options = PreparedOptions();
        options.Requirements["information-subscription-classification"].RecheckAtUtc = Now;
        var expired = Create(options, ReadyCommerce()).조회();
        Assert.Equal("Expired", expired.PlannedInformationSubscription!.LegalClassificationStatusCode);
        Assert.False(expired.IsPreparationComplete);
        options.Requirements["information-subscription-classification"].RecheckAtUtc = Now.AddDays(1);
        options.Requirements["information-subscription-classification"].ReviewedAtUtc = Now.AddMinutes(1);
        var future = Create(options, ReadyCommerce()).조회();
        Assert.Equal("Pending", future.PlannedInformationSubscription!.LegalClassificationStatusCode);
        Assert.False(future.IsPreparationComplete);
    }

    [Fact]
    public void 준비자료가_충족되어도_등록_허가_영업허용으로_승격하지_않는다()
    {
        var options = PreparedOptions();
        var result = Create(options, ReadyCommerce()).조회();
        Assert.True(result.IsPreparationComplete);
        Assert.Empty(result.MissingRequirementCodes);
        Assert.False(result.ActualRegistrationConfirmed);
        Assert.Equal("Unknown", result.BrokeragePermitStatusCode);
        Assert.False(result.CanActivateOperations);
    }

    [Theory]
    [InlineData("Prepared")]
    [InlineData("Submitted")]
    public void 접수나_준비는_실제_등록완료가_아니다(string status)
    {
        var options = PreparedOptions();
        options.PlatformRegistration = Evidence(status);
        options.PlatformRegistration.AuthorityReceiptReference = "authority-receipt-ref";
        Assert.False(Create(options, ReadyCommerce()).조회().ActualRegistrationConfirmed);
    }

    [Fact]
    public void 확인상태_문자열만으로_등록완료를_만들지_않는다()
    {
        var options = new 화물플랫폼등록준비Options();
        options.PlatformRegistration.StatusCode = "Confirmed";
        var result = Create(options).조회();
        Assert.False(result.ActualRegistrationConfirmed);
        Assert.Equal("Pending", result.PlatformRegistrationStatusCode);
    }

    [Fact]
    public void 실제_등록확인근거가_있어도_주선허가는_별도다()
    {
        var options = PreparedOptions();
        options.PlatformRegistration = Evidence("Confirmed");
        options.PlatformRegistration.AuthorityReceiptReference = "registration-receipt-ref";
        var result = Create(options, ReadyCommerce()).조회();
        Assert.True(result.ActualRegistrationConfirmed);
        Assert.Equal("Unknown", result.BrokeragePermitStatusCode);
        Assert.False(result.RequiredAuthorityProceduresConfirmed);
        Assert.False(result.CanActivateOperations);
    }

    [Fact]
    public void 플랫폼등록과_요금_약관신고_확인은_각각의_기관근거가_필요하다()
    {
        var options = PreparedOptions();
        options.PlatformRegistration = Evidence("Confirmed");
        options.PlatformRegistration.AuthorityReceiptReference = "registration-receipt";
        options.FeeFiling = Evidence("Submitted");
        options.FeeFiling.AuthorityReceiptReference = "fee-submission-ref";
        options.TermsFiling = Evidence("Confirmed");
        options.TermsFiling.AuthorityReceiptReference = "terms-receipt";
        Assert.False(Create(options, ReadyCommerce()).조회().RequiredAuthorityProceduresConfirmed);
        options.FeeFiling.StatusCode = "Confirmed";
        Assert.True(Create(options, ReadyCommerce()).조회().RequiredAuthorityProceduresConfirmed);
    }

    [Fact]
    public void 만료되거나_미래에_확인했다는_근거는_효력이_없다()
    {
        var options = PreparedOptions();
        options.Requirements["office"].RecheckAtUtc = Now;
        options.Requirements["business-plan"].ReviewedAtUtc = Now.AddMinutes(1);
        var result = Create(options, ReadyCommerce()).조회();
        Assert.False(result.IsPreparationComplete);
        Assert.Contains(result.Requirements, x => x.Code == "office" && x.StatusCode == "Expired");
        Assert.Contains(result.Requirements, x => x.Code == "business-plan" && x.StatusCode == "Pending");
    }

    [Theory]
    [InlineData(false, 0, 0)]
    [InlineData(true, 500, 0)]
    [InlineData(true, 0, 5000)]
    public void 무료운영_설정불일치는_준비완료를_막는다(bool free, int charge, int ceiling)
    {
        var result = Create(PreparedOptions(), ReadyCommerce(), new()
        { 무료배차 = free, 기본이용료 = charge, 월상한이용료 = ceiling }).조회();
        Assert.False(result.FreeConnectionConfigured);
        Assert.False(result.IsPreparationComplete);
        Assert.Contains("free-fee-settings", result.MissingRequirementCodes);
    }

    [Fact]
    public void 조회응답에_비공개_원문이나_검토자_참조를_복제하지_않는다()
    {
        var options = PreparedOptions();
        options.Requirements["office"].EvidenceReference = "private-office-evidence-never-return";
        options.Requirements["office"].ReviewedBy = "private-reviewer-never-return";
        var json = System.Text.Json.JsonSerializer.Serialize(Create(options, ReadyCommerce()).조회());
        Assert.DoesNotContain("private-office-evidence-never-return", json);
        Assert.DoesNotContain("private-reviewer-never-return", json);
        var authorize = Assert.Single(typeof(화물플랫폼등록준비Controller).GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>());
        Assert.Equal("서버관리자전용", authorize.Policy);
        Assert.Equal(FreightPlatformReadinessRoutes.Readiness,
            Assert.Single(typeof(화물플랫폼등록준비Controller).GetCustomAttributes(typeof(RouteAttribute), true).Cast<RouteAttribute>()).Template);
    }

    [Fact]
    public void 포장포함중량과_세치수는_수량이나_부피로_대체할_수_없다()
    {
        var missing = FreightCargoRegistrationPolicy.MissingFields(new("상자", null, null, null, null, "출발지", "도착지"));
        Assert.Equal(new[] { "GrossWeightIncludingPackagingKg", "LengthMm", "WidthMm", "HeightMm" }, missing);
        Assert.Empty(FreightCargoRegistrationPolicy.MissingFields(new("상자", 2.5m, 100, 200, 300, "출발지", "도착지")));
    }

    [Fact]
    public void 빈종류와_출도착지_및_0이하_수치는_등록불가다()
    {
        var missing = FreightCargoRegistrationPolicy.MissingFields(new(" ", 0m, -1, 0, null, null, " "));
        Assert.Equal(7, missing.Count);
    }

    private static 화물플랫폼등록준비Service Create(화물플랫폼등록준비Options? options = null,
        통신판매운영Options? commerce = null, 기사이용료정책Options? usage = null)
        => new(Options.Create(options ?? new()), Options.Create(usage ?? new()),
            Options.Create(commerce ?? new()), new FixedClock());

    private static 화물플랫폼등록준비Options PreparedOptions()
    {
        var options = new 화물플랫폼등록준비Options();
        foreach (var item in Create().조회().Requirements.Where(x => x.Code is not
                     ("free-fee-settings" or "commerce-privacy-review" or "platform-registration" or "brokerage-permit" or "fee-filing" or "terms-filing")))
            options.Requirements[item.Code] = Evidence("Prepared");
        options.Requirements["information-subscription-classification"] = Evidence("Confirmed");
        options.Requirements["information-subscription-classification"].AuthorityReceiptReference = "classification-reply-ref";
        return options;
    }

    private static 화물플랫폼준비근거Options Evidence(string status) => new()
    { StatusCode = status, EvidenceReference = "review-evidence-ref", ReviewedBy = "reviewer-ref", ReviewedAtUtc = Now.AddDays(-1) };

    private static 통신판매운영Options ReadyCommerce() => new()
    { TermsApprovalReference = "terms-ref", PrivacyApprovalReference = "privacy-ref", ExternalProcessingReference = "external-ref", DisputeOperationsReference = "support-ref" };

    private sealed class FixedClock : TimeProvider { public override DateTimeOffset GetUtcNow() => Now; }
}
