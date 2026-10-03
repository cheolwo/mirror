using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ssalddel.Application.Admin.Restaurants;
using Ssalddel.Contracts.Common.Finance;
using Ssalddel.Controllers.Admin;
using 살뜰.도메인.음식;

namespace Ssalddel.Tests.Application.Admin.Restaurants;

public sealed class 음식배달지급검토Tests
{
    private static readonly DateOnly Start = new(2026, 10, 1);
    private static readonly DateOnly End = new(2026, 10, 7);
    private static FoodDeliveryPayoutPricingPolicy Pricing => new("observed-example.r1", Start, End,
        700m, 700m, 100, 80m, "Floor");
    private static FoodDeliveryPayoutReviewDelivery Delivery(string key, decimal? distance,
        decimal time = 700m, decimal promotion = 300m, string? pickup = null)
        => new(key, Start, distance, "AppDisplayed", "MorningExample", time, promotion, 0m, pickup);

    [Fact]
    public void 명시한_동시픽업만_픽업비를_한번_계산하고_건별금액에포함된할증을_중복하지않는다()
    {
        var result = 음식배달지급검토Policy.건별계산(new(Pricing,
            [Delivery("one", 2.914m, pickup: "pickup-1"), Delivery("two", 0m, pickup: "pickup-1"),
             Delivery("three", 0.608m, pickup: "pickup-1"), Delivery("four", 0m, pickup: "pickup-2")]));
        Assert.Equal(new[] { 4720m, 1700m, 2180m, 2400m }, result.Lines.Select(line => line.GrossPayoutKrw));
        Assert.Equal(11000m, result.TotalGrossPayoutKrw);
        Assert.Equal(2, result.Lines.Count(line => line.PickupFeeKrw == 700m));
        Assert.Equal(Pricing, result.PolicySnapshot);
        Assert.Equal("ReviewOnly", result.StatusCode);
    }

    [Theory]
    [InlineData(700)] [InlineData(1000)] [InlineData(800)] [InlineData(1100)]
    public void 시간대할증은_거리단가와독립이며_관측사례를_동일거리단가로_비교한다(int time)
    {
        var result = 음식배달지급검토Policy.건별계산(new(Pricing, [Delivery("one", 2.999m, time, 0m)]));
        Assert.Equal(2320m, result.Lines[0].DistanceFeeKrw);
        Assert.Equal(1400m + 2320m + time, result.TotalGrossPayoutKrw);
    }

    [Theory]
    [InlineData("0.099", 0)] [InlineData("0.100", 80)] [InlineData("0.199", 80)]
    public void 십진거리의_100m절사_경계를_검증한다(string distance, int fee)
    {
        var value = decimal.Parse(distance, System.Globalization.CultureInfo.InvariantCulture);
        var result = 음식배달지급검토Policy.건별계산(new(Pricing, [Delivery("one", value)]));
        Assert.Equal(fee, result.Lines[0].DistanceFeeKrw);
    }

    [Fact]
    public void 올림과_포함거리와_최소지급보정을_별도검토할수있다()
    {
        var policy = Pricing with { DistanceRoundingCode = "Ceiling", DistanceUnitFeeKrw = 90m };
        var result = 음식배달지급검토Policy.건별계산(new(policy, [Delivery("one", 0.101m)]));
        Assert.Equal(180m, result.Lines[0].DistanceFeeKrw);
        var included = policy with { IncludedDistanceMeters = 1000, MinimumPayoutKrw = 2500m };
        var small = 음식배달지급검토Policy.건별계산(new(included, [Delivery("one", 0.5m, 0m, 0m)]));
        Assert.Equal(0m, small.Lines[0].DistanceFeeKrw);
        Assert.Equal(1100m, small.Lines[0].MinimumAdjustmentKrw);
        Assert.Equal(2500m, small.TotalGrossPayoutKrw);
    }

    [Fact]
    public void 미확인거리와_중복배달과_유효기간외입력은_거부한다()
    {
        Assert.Throws<ArgumentException>(() => 음식배달지급검토Policy.건별계산(new(Pricing, [Delivery("one", null)])));
        Assert.Throws<ArgumentException>(() => 음식배달지급검토Policy.건별계산(new(Pricing, [Delivery("one", 0m), Delivery("one", 1m)])));
        Assert.Throws<ArgumentException>(() => 음식배달지급검토Policy.건별계산(new(Pricing, [Delivery("one", 1m) with { ServiceDate = Start.AddDays(-1) }])));
        Assert.Throws<ArgumentException>(() => 음식배달지급검토Policy.건별계산(new(Pricing,
            [Delivery("one", 1m, pickup: "same"), Delivery("two", 1m, pickup: "same") with { ServiceDate = End }])));
    }

    [Fact]
    public void 지역할증과_확인된영거리도_각각보존한다()
    {
        var result = 음식배달지급검토Policy.건별계산(new(Pricing,
            [Delivery("one", 0m, 1000m, 0m) with { RegionSurchargeKrw = 500m }]));
        Assert.Equal(2900m, result.TotalGrossPayoutKrw);
        Assert.Equal(0m, result.Lines[0].DistanceFeeKrw);
    }

    private static FoodDeliverySettlementReviewPolicy SettlementPolicy => new()
    {
        Revision = "synthetic-statement-review.r1", EffectiveFrom = Start, EffectiveThrough = End,
        IncludePeriodRewardsInInsurance = false, RewardInsuranceTreatmentEvidenceRef = "synthetic-review:reward-excluded",
        IncomeTax = Rule(.03m), LocalIncomeTax = Rule(.003m),
        EmploymentInsurance = Rule(.008m, .198m), AccidentInsurance = Rule(.0088m, .198m)
    };
    private static FoodDeliveryDeductionReviewRule Rule(decimal rate, decimal expense = 0m) => new()
    {
        ApplicabilityCode = "Applicable", Rate = rate, ExpenseRatio = expense, EvidenceRef = "synthetic-review:eligibility"
    };
    private static FoodDeliverySettlementReviewRequest Settlement(FoodDeliverySettlementReviewPolicy? policy = null)
        => new(Start, End, 1000000m, 100000m, 130000m, 30000m, policy ?? SettlementPolicy);

    [Fact]
    public void 세전미션과_보상을_구별하고_공제를_기간총액에_한번적용한다()
    {
        var result = 음식배달지급검토Policy.기간계산(Settlement());
        Assert.Equal(1100000m, result.BaseGrossKrw);
        Assert.Equal(1260000m, result.TotalGrossKrw);
        Assert.Equal(new[] { 33000m, 3300m, 7050m, 7760m }, result.Deductions.Select(line => line.AmountKrw!.Value));
        Assert.Equal(5280m, result.RewardTaxKrw);
        Assert.Equal(1203610m, result.NetPayoutKrw);
        Assert.Equal("Estimate", result.StatusCode);
        Assert.Empty(result.PendingCodes);
        Assert.Equal(SettlementPolicy, result.PolicySnapshot);
    }

    [Fact]
    public void 보험에서제외된달성보너스_3만원의_순증액은_29010원이다()
    {
        var request = Settlement();
        var noBonus = 음식배달지급검토Policy.기간계산(request with { MilestoneBonusGrossKrw = 0m });
        var bonus = 음식배달지급검토Policy.기간계산(request);
        Assert.Equal(29010m, bonus.NetPayoutKrw - noBonus.NetPayoutKrw);
        Assert.Equal(noBonus.Deductions, bonus.Deductions);
    }

    [Fact]
    public void 보상_보험포함정책도_명시한판본에서만_비교한다()
    {
        var result = 음식배달지급검토Policy.기간계산(Settlement(SettlementPolicy with { IncludePeriodRewardsInInsurance = true }));
        Assert.Equal(1260000m, result.Deductions[2].BaseKrw);
        Assert.Equal(8080m, result.Deductions[2].AmountKrw);
    }

    [Fact]
    public void 보험적용미확인을_영원공제나_확정수령액으로_표시하지않는다()
    {
        var result = 음식배달지급검토Policy.기간계산(Settlement(SettlementPolicy with { EmploymentInsurance = new() }));
        Assert.Equal("NeedsReview", result.StatusCode);
        Assert.Null(result.Deductions[2].AmountKrw);
        Assert.Null(result.NetPayoutKrw);
        Assert.Null(result.TotalDeductionsKrw);
        Assert.Contains("EmploymentInsuranceEligibilityUnknown", result.PendingCodes);
    }

    [Fact]
    public void 보험보상처리미정은_보상의보험기준을_추측하지않는다()
    {
        var result = 음식배달지급검토Policy.기간계산(Settlement(SettlementPolicy with { IncludePeriodRewardsInInsurance = null }));
        Assert.Null(result.Deductions[2].BaseKrw);
        Assert.Null(result.NetPayoutKrw);
        Assert.Contains("EmploymentInsuranceRewardBasisUnknown", result.PendingCodes);
    }

    [Fact]
    public void 보험미적용에는_판정근거가필요하고_미적용은영원으로구분한다()
    {
        var rule = new FoodDeliveryDeductionReviewRule { ApplicabilityCode = "NotApplicable", EvidenceRef = "synthetic:exempt" };
        var result = 음식배달지급검토Policy.기간계산(Settlement(SettlementPolicy with { EmploymentInsurance = rule }));
        Assert.Equal(0m, result.Deductions[2].AmountKrw);
        Assert.NotNull(result.NetPayoutKrw);
        Assert.Throws<ArgumentException>(() => 음식배달지급검토Policy.기간계산(Settlement(SettlementPolicy with
            { EmploymentInsurance = rule with { EvidenceRef = "" } })));
    }

    [Fact]
    public void 최소보험료는_기간에한번만적용하고_보수영원에는_부과하지않는다()
    {
        var policy = SettlementPolicy with { EmploymentInsurance = Rule(.008m, .198m) with { MinimumKrw = 2440m } };
        var request = new FoodDeliverySettlementReviewRequest(Start, End, 100000m, 0m, 0m, 0m, policy);
        Assert.Equal(2440m, 음식배달지급검토Policy.기간계산(request).Deductions[2].AmountKrw);
        Assert.Equal(0m, 음식배달지급검토Policy.기간계산(request with { DeliveryGrossKrw = 0m }).Deductions[2].AmountKrw);
    }

    [Fact]
    public void 보상세액의_원단위절사를_기본세액십원절사와_구분한다()
    {
        var request = Settlement() with { PeriodPromotionGrossKrw = 1001m, MilestoneBonusGrossKrw = 0m };
        Assert.Equal(33m, 음식배달지급검토Policy.기간계산(request).RewardTaxKrw);
    }

    [Fact]
    public void 잘못된비율과_유효기간밖과_보험근거누락은_거부한다()
    {
        Assert.Throws<ArgumentException>(() => 음식배달지급검토Policy.기간계산(Settlement(SettlementPolicy with { IncomeTax = Rule(3m) })));
        Assert.Throws<ArgumentException>(() => 음식배달지급검토Policy.기간계산(Settlement() with { PeriodEnd = End.AddDays(1) }));
        Assert.Throws<ArgumentException>(() => 음식배달지급검토Policy.기간계산(Settlement(SettlementPolicy with { RewardInsuranceTreatmentEvidenceRef = "" })));
        Assert.Throws<ArgumentException>(() => 음식배달지급검토Policy.기간계산(Settlement() with { MissionGrossKrw = -1m }));
    }

    [Fact]
    public void 공제가총액을초과하면_음수수령액을_확정하지않는다()
    {
        var policy = SettlementPolicy with { EmploymentInsurance = Rule(.008m, .198m) with { MinimumKrw = 10000m } };
        var result = 음식배달지급검토Policy.기간계산(new(Start, End, 100m, 0m, 0m, 0m, policy));
        Assert.Null(result.NetPayoutKrw);
        Assert.Contains("DeductionsExceedGross", result.PendingCodes);
    }

    [Fact]
    public void 직렬화된동결판본은_현재정책변경과관계없이_같은계산을재현한다()
    {
        var request = Settlement();
        var frozen = JsonSerializer.Deserialize<FoodDeliverySettlementReviewRequest>(JsonSerializer.Serialize(request))!;
        var result = 음식배달지급검토Policy.기간계산(frozen);
        Assert.Equal(음식배달지급검토Policy.기간계산(request).NetPayoutKrw, result.NetPayoutKrw);
        Assert.Equal(request.Policy, result.PolicySnapshot);
    }

    [Fact]
    public void 검토UseCase는_미확인거리를_400오류로_반환한다()
    {
        var useCase = new 음식배달지급검토UseCase();
        var result = useCase.건별계산(new(Pricing, [Delivery("one", null)]));
        Assert.True(result.IsFailed);
        Assert.Equal(400, result.Errors[0].Metadata["StatusCode"]);
    }

    [Theory]
    [InlineData(nameof(음식배달요금정책Controller.기사지급검토), "payout-preview")]
    [InlineData(nameof(음식배달요금정책Controller.기사정산검토), "settlement-preview")]
    public void 기존관리자전용요금경로에_저장없는검토행동을추가한다(string action, string path)
    {
        var controller = typeof(음식배달요금정책Controller);
        Assert.Equal("서버관리자전용", controller.GetCustomAttribute<AuthorizeAttribute>()!.Policy);
        Assert.Equal("api/v1/admin/food-delivery-pricing-policy", controller.GetCustomAttribute<RouteAttribute>()!.Template);
        Assert.Equal(path, controller.GetMethod(action)!.GetCustomAttribute<HttpPostAttribute>()!.Template);
    }
}
