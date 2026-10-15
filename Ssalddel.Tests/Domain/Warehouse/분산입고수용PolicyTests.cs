using Ssalddel.Contracts.Common.Warehouse;
using 살뜰.도메인.창고;

namespace Ssalddel.Tests.Domain.Warehouse;

public sealed class 분산입고수용PolicyTests
{
    [Fact]
    public void 농산물을거부하고공산품을받는조건을명시적상품분류로판정한다()
    {
        var policy = Policy(); policy.DeniedCategoryCodes = [DistributedInboundCodes.Agricultural];
        var goods = Goods(); Assert.True(분산입고수용Policy.EvaluateGoods(policy, goods, 3).IsAllowed);
        goods.CategoryCodes = [DistributedInboundCodes.Agricultural]; goods.IsFood = true;
        Assert.Contains("GoodsCategoryDenied", 분산입고수용Policy.EvaluateGoods(policy, goods, 3).ReasonCodes);
    }

    [Fact]
    public void 허용목록에있어도명시적거부가우선한다()
    {
        var policy = Policy(); policy.DeniedCategoryCodes = [DistributedInboundCodes.Industrial];
        var result = 분산입고수용Policy.EvaluateGoods(policy, Goods(), 1);
        Assert.False(result.IsAllowed); Assert.Contains("GoodsCategoryDenied", result.ReasonCodes);
    }

    [Fact]
    public void 겹치는분류는모두허용되어야하며식품축도별도로만족해야한다()
    {
        var policy = Policy(); policy.AllowFood = true; policy.RequireProcessedFood = true;
        var goods = Goods(); goods.CategoryCodes = [DistributedInboundCodes.Industrial, DistributedInboundCodes.PackagedFood];
        goods.IsFood = true; goods.IsProcessed = true;
        Assert.Contains("GoodsCategoryNotAllowed", 분산입고수용Policy.EvaluateGoods(policy, goods, 1).ReasonCodes);
        policy.AllowedCategoryCodes = [DistributedInboundCodes.Industrial, DistributedInboundCodes.PackagedFood];
        Assert.True(분산입고수용Policy.EvaluateGoods(policy, goods, 1).IsAllowed);
        policy.AllowFood = false; Assert.Contains("FoodNotAllowed", 분산입고수용Policy.EvaluateGoods(policy, goods, 1).ReasonCodes);
    }

    [Fact]
    public void 상품특성을모르면이름이나업체유형으로허용하지않는다()
    {
        var unknown = new DistributedGoodsFacts();
        var result = 분산입고수용Policy.EvaluateGoods(Policy(), unknown, 1);
        Assert.False(result.IsAllowed); Assert.True(result.NeedsReview);
        Assert.Contains("GoodsCategoryUnknown", result.ReasonCodes); Assert.Contains("FoodClassificationUnknown", result.ReasonCodes);
        Assert.Contains("PackagingUnknown", result.ReasonCodes); Assert.Contains("TemperatureUnknown", result.ReasonCodes);
    }

    [Theory]
    [InlineData(null, "ProcessingClassificationUnknown")]
    [InlineData(false, "UnprocessedFoodNotAllowed")]
    public void 가공식품만수용하면가공여부가명확히참이어야한다(bool? processed, string reason)
    {
        var policy = Policy(); policy.AllowFood = true; policy.RequireProcessedFood = true;
        var goods = Goods(); goods.IsFood = true; goods.IsProcessed = processed;
        Assert.Contains(reason, 분산입고수용Policy.EvaluateGoods(policy, goods, 1).ReasonCodes);
    }

    [Fact]
    public void 가공식품분류와비식품선언이충돌하면입고를허용하지않는다()
    {
        var policy = Policy(); policy.AllowedCategoryCodes = [DistributedInboundCodes.PackagedFood];
        var goods = Goods(); goods.CategoryCodes = [DistributedInboundCodes.PackagedFood]; goods.IsProcessed = true;
        Assert.Contains("GoodsClassificationConflict", 분산입고수용Policy.EvaluateGoods(policy, goods, 1).ReasonCodes);
    }

    [Fact]
    public void 밀봉과온도조건을서로독립적으로검사한다()
    {
        var policy = Policy(); policy.RequireSealedPackaging = true;
        policy.AllowedPackagingCodes = [DistributedInboundCodes.Sealed, DistributedInboundCodes.Boxed];
        var goods = Goods(); goods.PackagingCode = DistributedInboundCodes.Boxed; goods.TemperatureCode = DistributedInboundCodes.Frozen;
        var result = 분산입고수용Policy.EvaluateGoods(policy, goods, 1);
        Assert.Contains("PackagingNotAllowed", result.ReasonCodes); Assert.Contains("TemperatureNotAllowed", result.ReasonCodes);
    }

    [Fact]
    public void 재고한개가상자반칸을사용하면확인된비율로공유용량을계산한다()
    {
        var goods = Goods(); goods.CapacityPerItem = .5m;
        var usage = Usage(); usage.Capacity = 8;
        var exact = 분산입고수용Policy.Evaluate(Policy(), goods, 4, usage);
        Assert.True(exact.IsAllowed); Assert.Equal(2, exact.RequiredCapacity);
        Assert.Contains("CapacityExceeded", 분산입고수용Policy.Evaluate(Policy(), goods, 5, usage).ReasonCodes);
    }

    [Fact]
    public void 같은값이어도서로다른용량단위를자동환산하지않는다()
    {
        var goods = Goods(); goods.CapacityUnit = "개";
        Assert.Contains("CapacityUnitMismatch", 분산입고수용Policy.Evaluate(Policy(), goods, 1, Usage()).ReasonCodes);
        goods = Goods(); var usage = Usage(); usage.CapacityUnit = "개";
        Assert.Contains("ExistingCapacityUnitMismatch", 분산입고수용Policy.Evaluate(Policy(), goods, 1, usage).ReasonCodes);
    }

    [Fact]
    public void 기존재고용량이미확인이면영으로간주하지않는다()
    {
        var usage = Usage(); usage.IsKnown = false;
        var result = 분산입고수용Policy.Evaluate(Policy(), Goods(), 1, usage);
        Assert.False(result.IsAllowed); Assert.True(result.NeedsReview); Assert.Contains("ExistingCapacityUnknown", result.ReasonCodes);
    }

    [Fact]
    public void 중량과부피제한이있으면상품과기존재고의실측근거가모두필요하다()
    {
        var policy = Policy(); policy.MaximumWeightKg = 10; policy.MaximumVolumeM3 = 1;
        var goods = Goods();
        var missing = 분산입고수용Policy.Evaluate(policy, goods, 1, Usage());
        Assert.Contains("GoodsWeightUnknown", missing.ReasonCodes); Assert.Contains("GoodsVolumeUnknown", missing.ReasonCodes);
        goods.WeightKgPerItem = 2; goods.VolumeM3PerItem = .2m;
        missing = 분산입고수용Policy.Evaluate(policy, goods, 1, Usage());
        Assert.Contains("ExistingWeightUnknown", missing.ReasonCodes); Assert.Contains("ExistingVolumeUnknown", missing.ReasonCodes);
        var usage = Usage(); usage.WeightKg = 9; usage.VolumeM3 = .9m;
        var exceeded = 분산입고수용Policy.Evaluate(policy, goods, 1, usage);
        Assert.Contains("WeightExceeded", exceeded.ReasonCodes); Assert.Contains("VolumeExceeded", exceeded.ReasonCodes);
    }

    [Fact]
    public void 수량및용량수치오류는허용이나예외누출로바꾸지않는다()
    {
        Assert.Contains("InvalidInboundQuantity", 분산입고수용Policy.EvaluateGoods(Policy(), Goods(), 0).ReasonCodes);
        var goods = Goods(); goods.CapacityPerItem = decimal.MaxValue;
        Assert.Contains("CapacityOverflow", 분산입고수용Policy.EvaluateGoods(Policy(), goods, 2).ReasonCodes);
        var usage = Usage(); usage.Capacity = -1;
        Assert.Contains("InvalidExistingCapacity", 분산입고수용Policy.Evaluate(Policy(), Goods(), 1, usage).ReasonCodes);
    }

    [Fact]
    public void 일정과보관기간그리고이번출입시간확인을각각검사한다()
    {
        var start = new DateTime(2026, 10, 12, 9, 0, 0, DateTimeKind.Utc);
        var policy = Policy(); policy.MaximumStorageDays = 7;
        Assert.Empty(분산입고수용Policy.EvaluateSchedule(policy, start, start.AddHours(1), start.AddDays(7), true));
        Assert.Contains("StoragePeriodExceeded", 분산입고수용Policy.EvaluateSchedule(policy, start, start.AddHours(1), start.AddDays(8), true));
        Assert.Contains("AccessHoursConfirmationRequired", 분산입고수용Policy.EvaluateSchedule(policy, start, start.AddHours(1), start.AddDays(2), false));
        Assert.Contains("InvalidInboundSchedule", 분산입고수용Policy.EvaluateSchedule(policy, start, start, start.AddDays(2), true));
    }

    [Fact]
    public void 빈정책이나알수없는코드와영용량을유효설정으로저장하지않는다()
    {
        var policy = new DistributedSiteAcceptancePolicy { AllowedCategoryCodes = ["farm"], MaximumCapacity = 0 };
        var errors = 분산입고수용Policy.Validate(policy);
        Assert.Contains("InvalidPolicyCategories", errors); Assert.Contains("InvalidPolicyCapacity", errors);
        Assert.Contains("InvalidPolicyAccessHours", errors); Assert.NotEmpty(분산입고수용Policy.Validate(null));
    }

    private static DistributedSiteAcceptancePolicy Policy() => new()
    {
        AllowedCategoryCodes = [DistributedInboundCodes.Industrial], AllowedPackagingCodes = [DistributedInboundCodes.Sealed],
        AllowedTemperatureCodes = [DistributedInboundCodes.Ambient], CapacityUnit = "상자", MaximumCapacity = 10,
        AccessHours = "오전 10시~12시, 사전 연락"
    };
    private static DistributedGoodsFacts Goods() => new()
    {
        CategoryCodes = [DistributedInboundCodes.Industrial], IsFood = false, PackagingCode = DistributedInboundCodes.Sealed,
        TemperatureCode = DistributedInboundCodes.Ambient, CapacityUnit = "상자", CapacityPerItem = 1
    };
    private static DistributedCapacityUsage Usage() => new() { IsKnown = true, CapacityUnit = "상자", Capacity = 0 };
}
