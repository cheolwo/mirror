using System.Text.Json;
using Ssalddel.Contracts.Common.Settlement;
using 살뜰.도메인.정산;

namespace Ssalddel.Tests.Domain.Settlement;

public sealed class 활동성과배분PolicyTests
{
    private readonly 활동성과배분Policy _policy = new();

    [Fact]
    public void 미확인_금액과_비율과_가중치는_0으로_추정하지_않는다()
    {
        var terms = Terms(); terms.ReturnRatePercent = null; terms.Weights[0].Weight = null;
        var basis = Basis(); basis.RevenueAmountKrw = null;
        var result = _policy.Calculate(terms, basis, [Work("pick", "worker")]);
        Assert.Equal(NeighborhoodActivityRewardCodes.WaitingForEvidence, result.StatusCode);
        Assert.Contains("ReturnRateMissing", result.ReasonCodes);
        Assert.Contains("RewardWeightMissing", result.ReasonCodes);
        Assert.Contains("RevenueMissing", result.ReasonCodes);
        Assert.Null(result.PoolAmountKrw); Assert.Empty(result.Allocations);
    }

    [Fact]
    public void 기본보수와_기타비용과_유보를_제외한_합의진술을_추가성과금으로_계산한다()
    {
        var terms = Terms(); terms.ReturnRatePercent = 25m;
        var result = _policy.Calculate(terms, Basis(1000, 200, 100, 100), [Work("pick", "worker")]);
        Assert.Equal(NeighborhoodActivityRewardCodes.Ready, result.StatusCode);
        Assert.Equal(600m, result.SurplusAmountKrw); Assert.Equal(150m, result.PoolAmountKrw);
        Assert.Equal(150m, Assert.Single(result.Allocations).AmountKrw);
        Assert.False(result.IsActualBankVerified); Assert.Contains("Simulation", result.Notice);
    }

    [Fact]
    public void 명시적_0수익은_무료나_미확인으로_바뀌지_않고_0원_성과금이다()
    {
        var result = _policy.Calculate(Terms(), Basis(0), [Work("pick", "worker")]);
        Assert.Equal(NeighborhoodActivityRewardCodes.Ready, result.StatusCode);
        Assert.Equal(0m, result.PoolAmountKrw); Assert.Equal(0m, Assert.Single(result.Allocations).AmountKrw);
    }

    [Fact]
    public void 비용이_수익보다_커도_기본보수를_감액하거나_음수성과금으로_만들지_않는다()
    {
        var result = _policy.Calculate(Terms(), Basis(100, 200), [Work("pick", "worker")]);
        Assert.Equal(0m, result.SurplusAmountKrw); Assert.Equal(0m, result.PoolAmountKrw);
    }

    [Fact]
    public void 세사람의_동률에서_잔여1원은_안정ID순서로_분배하고_합계가_풀과_같다()
    {
        var result = _policy.Calculate(Terms(), Basis(2), [Work("c", "c"), Work("b", "b"), Work("a", "a")]);
        Assert.Equal(new[] { "a", "b", "c" }, result.Allocations.Select(x => x.ActorUserId));
        Assert.Equal(new[] { 1m, 1m, 0m }, result.Allocations.Select(x => x.AmountKrw));
        Assert.Equal(result.PoolAmountKrw, result.Allocations.Sum(x => x.AmountKrw));
        Assert.Equal(result.PoolAmountKrw, result.ContributionShares.Sum(x => x.AmountKrw));
    }

    [Fact]
    public void 같은사람의_여러역할은_합산하고_기여내역의_합계도_일치한다()
    {
        var terms = Terms(); terms.Weights = [Weight("Picking", 1), Weight("Storage", 2, NeighborhoodActivityRewardCodes.StorageDay)];
        var works = new[] { Work("pick", "a", 1), Work("store", "a", 2, "Storage", NeighborhoodActivityRewardCodes.StorageDay), Work("other", "b", 1) };
        var result = _policy.Calculate(terms, Basis(12), works);
        Assert.Equal(2, result.Allocations.Count);
        Assert.Equal(10m, result.Allocations.Single(x => x.ActorUserId == "a").AmountKrw);
        Assert.Equal(5m, result.Allocations.Single(x => x.ActorUserId == "a").WeightedUnits);
        foreach (var actor in result.Allocations)
            Assert.Equal(actor.AmountKrw, result.ContributionShares.Where(x => x.Contribution.ActorUserId == actor.ActorUserId).Sum(x => x.AmountKrw));
    }

    [Fact]
    public void 소수_수량과_가중치도_정확한_비율로_계산한다()
    {
        var terms = Terms(); terms.Weights[0].Weight = 0.000001m;
        var result = _policy.Calculate(terms, Basis(7), [Work("a", "a", 0.000001m), Work("b", "b", 0.000002m)]);
        Assert.Equal(new[] { 2m, 5m }, result.Allocations.Select(x => x.AmountKrw));
        Assert.Equal(0.000000000001m, result.Allocations[0].WeightedUnits);
    }

    [Fact]
    public void 풀의_1원미만은_별도기록하고_사람별_배분은_정수원이다()
    {
        var terms = Terms(); terms.ReturnRatePercent = 33.333333m;
        var result = _policy.Calculate(terms, Basis(10), [Work("a", "a"), Work("b", "b")]);
        Assert.Equal(3.3333333m, result.UnroundedPoolAmountKrw);
        Assert.Equal(3m, result.PoolAmountKrw); Assert.Equal(0.3333333m, result.FractionalPoolRemainderKrw);
        Assert.All(result.Allocations, x => Assert.Equal(decimal.Truncate(x.AmountKrw), x.AmountKrw));
    }

    [Fact]
    public void 입력순서와_근거순서가_바뀌어도_결과가_결정적이다()
    {
        var terms = Terms(); terms.Weights = [Weight("Picking", 1), Weight("Inbound", 2)];
        var works = new[] { Work("c", "b"), Work("a", "a", 2, "Inbound"), Work("b", "a") };
        works[0].EvidenceReferences = ["photo:z", "photo:a"];
        var first = _policy.Calculate(terms, Basis(13), works);
        terms.Weights = terms.Weights.Reverse().ToArray(); works[0].EvidenceReferences = ["photo:a", "photo:z"];
        var second = _policy.Calculate(terms, Basis(13), works.Reverse().ToArray());
        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(second));
    }

    [Fact]
    public void 동일기여ID를_두번_계산하지_않는다()
    {
        var result = _policy.Calculate(Terms(), Basis(), [Work("same", "a"), Work("same", "b")]);
        Assert.Equal(NeighborhoodActivityRewardCodes.InvalidInput, result.StatusCode);
        Assert.Contains("ContributionDuplicate", result.ReasonCodes); Assert.Empty(result.Allocations);
    }

    [Fact]
    public void 다른ID와_다른판본을_붙여도_동일정본_수행을_중복배분하지_않는다()
    {
        var first = Work("first", "a"); var second = Work("second", "b");
        second.OriginId = first.OriginId; second.OriginRevision++;
        var result = _policy.Calculate(Terms(), Basis(), [first, second]);
        Assert.Contains("ContributionOriginDuplicate", result.ReasonCodes);
        Assert.Equal(NeighborhoodActivityRewardCodes.InvalidInput, result.StatusCode);
    }

    [Fact]
    public void 사진이나_수행근거가_없으면_배분대기다()
    {
        var work = Work("pick", "worker"); work.EvidenceReferences = [];
        var result = _policy.Calculate(Terms(), Basis(), [work]);
        Assert.Equal(NeighborhoodActivityRewardCodes.WaitingForEvidence, result.StatusCode);
        Assert.Contains("ContributionEvidenceMissing", result.ReasonCodes);
    }

    [Fact]
    public void 합의하지_않은_작업단위의_가중치를_기본1로_채우지_않는다()
    {
        var result = _policy.Calculate(Terms(), Basis(), [Work("store", "worker", 1, "Picking", NeighborhoodActivityRewardCodes.StorageDay)]);
        Assert.Equal(NeighborhoodActivityRewardCodes.WaitingForEvidence, result.StatusCode);
        Assert.Contains("ContributionWeightMissing", result.ReasonCodes);
    }

    [Fact]
    public void 양수풀이_있어도_가중치가_전부0이면_누구에게도_자동배분하지_않는다()
    {
        var terms = Terms(); terms.Weights[0].Weight = 0;
        var result = _policy.Calculate(terms, Basis(), [Work("pick", "worker")]);
        Assert.Equal(NeighborhoodActivityRewardCodes.WaitingForEvidence, result.StatusCode);
        Assert.Contains("EligibleWeightMissing", result.ReasonCodes);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void 비율범위를_자동보정하지_않는다(decimal rate)
    {
        var terms = Terms(); terms.ReturnRatePercent = rate;
        Assert.Contains("ReturnRateInvalid", _policy.Calculate(terms, Basis(), [Work("pick", "worker")]).ReasonCodes);
    }

    [Fact]
    public void 금액상한과_원미만_입력은_거절한다()
    {
        var basis = Basis(decimal.MaxValue);
        Assert.Contains("RevenueInvalid", _policy.Calculate(Terms(), basis, [Work("pick", "worker")]).ReasonCodes);
        basis = Basis(0.1m);
        Assert.Contains("RevenueInvalid", _policy.Calculate(Terms(), basis, [Work("pick", "worker")]).ReasonCodes);
    }

    [Fact]
    public void 수량과_가중치의_상한과_소수정밀도를_검사한다()
    {
        var terms = Terms(); terms.Weights[0].Weight = decimal.MaxValue;
        Assert.Contains("RewardWeightInvalid", _policy.Calculate(terms, Basis(), [Work("pick", "worker")]).ReasonCodes);
        Assert.Contains("ContributionQuantityInvalid", _policy.Calculate(Terms(), Basis(), [Work("pick", "worker", decimal.MaxValue)]).ReasonCodes);
        Assert.Contains("ContributionQuantityInvalid", _policy.Calculate(Terms(), Basis(), [Work("pick", "worker", 0.0000001m)]).ReasonCodes);
    }

    [Fact]
    public void 최대허용수치도_중간곱셈_overflow없이_합계가_일치한다()
    {
        var terms = Terms(); terms.Weights[0].Weight = 활동성과배분Policy.MaximumWeight;
        var result = _policy.Calculate(terms, Basis(활동성과배분Policy.MaximumMoneyKrw),
            [Work("pick", "worker", 활동성과배분Policy.MaximumQuantity)]);
        Assert.Equal(NeighborhoodActivityRewardCodes.Ready, result.StatusCode);
        Assert.Equal(활동성과배분Policy.MaximumMoneyKrw, Assert.Single(result.Allocations).AmountKrw);
    }

    [Fact]
    public void 누적가중수량상한을_넘기면_반올림하거나_잘라내지_않는다()
    {
        var terms = Terms(); terms.Weights[0].Weight = 활동성과배분Policy.MaximumWeight;
        var result = _policy.Calculate(terms, Basis(),
            [Work("a", "a", 활동성과배분Policy.MaximumQuantity), Work("b", "b", 활동성과배분Policy.MaximumQuantity)]);
        Assert.Equal(NeighborhoodActivityRewardCodes.InvalidInput, result.StatusCode);
        Assert.Contains("TotalWeightedUnitsLimitExceeded", result.ReasonCodes);
    }

    [Fact]
    public void 운영확정이나_입금인증으로_경제입력을_승격하지_않는다()
    {
        var basis = Basis(); basis.BasisKind = "BankVerified";
        var result = _policy.Calculate(Terms(), basis, [Work("pick", "worker")]);
        Assert.Equal(NeighborhoodActivityRewardCodes.InvalidInput, result.StatusCode);
        Assert.Contains("FinancialBasisKindInvalid", result.ReasonCodes); Assert.False(result.IsActualBankVerified);
    }

    [Fact]
    public void 입력객체를_바꾸어도_완료한_계산근거는_변하지_않는다()
    {
        var work = Work("pick", "worker");
        var result = _policy.Calculate(Terms(), Basis(), [work]);
        work.ActorUserId = "changed"; work.EvidenceReferences = [];
        Assert.Equal("worker", Assert.Single(result.ContributionShares).Contribution.ActorUserId);
        Assert.NotEmpty(result.ContributionShares[0].Contribution.EvidenceReferences);
    }

    private static NeighborhoodActivityRewardPolicyDto Terms() => new()
    {
        PolicyVersion = "agreed-v1", ReturnRatePercent = 100m, Weights = [Weight("Picking", 1m)]
    };
    private static NeighborhoodActivityRewardWeightDto Weight(string kind, decimal weight,
        string unit = NeighborhoodActivityRewardCodes.Unit) => new() { Kind = kind, UnitCode = unit, Weight = weight };
    private static NeighborhoodActivityRewardFinancialBasisDto Basis(decimal revenue = 100m, decimal basic = 0m,
        decimal other = 0m, decimal reserve = 0m) => new()
    {
        BasisKind = NeighborhoodActivityRewardCodes.SimulationPartyStatement, BasisReference = "agreed-input-v1",
        RevenueAmountKrw = revenue, BasicWorkCostAmountKrw = basic, OtherCostAmountKrw = other, ReserveAmountKrw = reserve
    };
    private static NeighborhoodActivityRewardContributionDto Work(string id, string actor, decimal quantity = 1m,
        string kind = "Picking", string unit = NeighborhoodActivityRewardCodes.Unit) => new()
    {
        ContributionId = id, ActorUserId = actor, Kind = kind, UnitCode = unit, Quantity = quantity,
        OriginKind = "InventoryHistory", OriginId = "origin-" + id, OriginRevision = 1, EvidenceReferences = ["receipt:" + id]
    };
}
