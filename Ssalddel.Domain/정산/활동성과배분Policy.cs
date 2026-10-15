using System.Numerics;
using Ssalddel.Contracts.Common.Settlement;

namespace 살뜰.도메인.정산;

/// <summary>
/// 사전 합의 가중치와 서버가 확인한 실제 수행 기여를 계산합니다. DB·시계·외부 API를 사용하지 않습니다.
/// Ready는 Simulation 당사자 진술의 계산 준비이며 동의·최종 배송·실재원·실제 지급 인증이 아닙니다.
/// </summary>
public sealed class 활동성과배분Policy
{
    public const string CurrentCalculationVersion = "neighborhood-activity-reward/v1";
    public const int MaximumContributions = 1024;
    public const int MaximumWeightTerms = 64;
    public const decimal MaximumMoneyKrw = 1_000_000_000_000m;
    public const decimal MaximumQuantity = 1_000_000_000m;
    public const decimal MaximumWeight = 1_000_000m;
    public const decimal MaximumTotalWeightedUnits = 1_000_000_000_000_000m;
    private const decimal Scale = 1_000_000m;
    private const decimal ScoreScale = 1_000_000_000_000m;

    public NeighborhoodActivityRewardPreviewDto Calculate(
        NeighborhoodActivityRewardPolicyDto? policy,
        NeighborhoodActivityRewardFinancialBasisDto? financialBasis,
        IReadOnlyList<NeighborhoodActivityRewardContributionDto>? contributions)
    {
        var invalid = new SortedSet<string>(StringComparer.Ordinal);
        var missing = new SortedSet<string>(StringComparer.Ordinal);
        if (policy is null) missing.Add("RewardPolicyMissing");
        if (financialBasis is null) missing.Add("FinancialBasisMissing");
        if (policy is null || financialBasis is null)
            return Result(policy, NeighborhoodActivityRewardCodes.WaitingForEvidence, missing);

        if (!Identifier(policy.PolicyVersion)) missing.Add("PolicyVersionMissing");
        Number(policy.ReturnRatePercent, 100m, 6, "ReturnRate", missing, invalid);
        if (string.IsNullOrWhiteSpace(financialBasis.BasisKind)) missing.Add("FinancialBasisKindMissing");
        else if (financialBasis.BasisKind != NeighborhoodActivityRewardCodes.SimulationPartyStatement)
            invalid.Add("FinancialBasisKindInvalid");
        if (!Identifier(financialBasis.BasisReference)) missing.Add("FinancialBasisReferenceMissing");
        Number(financialBasis.RevenueAmountKrw, MaximumMoneyKrw, 0, "Revenue", missing, invalid);
        Number(financialBasis.BasicWorkCostAmountKrw, MaximumMoneyKrw, 0, "BasicWorkCost", missing, invalid);
        Number(financialBasis.OtherCostAmountKrw, MaximumMoneyKrw, 0, "OtherCost", missing, invalid);
        Number(financialBasis.ReserveAmountKrw, MaximumMoneyKrw, 0, "Reserve", missing, invalid);

        var weights = new Dictionary<(string Kind, string Unit), decimal>();
        var terms = policy.Weights;
        if (terms is null || terms.Count == 0) missing.Add("RewardWeightsMissing");
        else if (terms.Count > MaximumWeightTerms) invalid.Add("RewardWeightLimitExceeded");
        else
            foreach (var term in terms)
            {
                if (term is null || !Identifier(term.Kind) || !Unit(term.UnitCode))
                { invalid.Add("RewardWeightIdentityInvalid"); continue; }
                var key = (term.Kind, term.UnitCode);
                if (!weights.TryAdd(key, term.Weight ?? 0m)) invalid.Add("RewardWeightDuplicate");
                Number(term.Weight, MaximumWeight, 6, "RewardWeight", missing, invalid);
            }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        var origins = new HashSet<(string Kind, string Id)>();
        var normalized = new List<ScoredContribution>();
        if (contributions is null || contributions.Count == 0) missing.Add("ContributionsMissing");
        else if (contributions.Count > MaximumContributions) invalid.Add("ContributionLimitExceeded");
        else
            foreach (var contribution in contributions)
            {
                if (contribution is null || !Identifier(contribution.ContributionId)
                    || !Identifier(contribution.ActorUserId) || !Identifier(contribution.Kind)
                    || !Identifier(contribution.OriginKind) || !Identifier(contribution.OriginId)
                    || !Unit(contribution.UnitCode) || contribution.OriginRevision < 0)
                { invalid.Add("ContributionIdentityInvalid"); continue; }
                if (!ids.Add(contribution.ContributionId)) invalid.Add("ContributionDuplicate");
                // 동일 정본을 다른 ID·수행자·판본·종류로 다시 보상하지 않습니다.
                if (!origins.Add((contribution.OriginKind, contribution.OriginId))) invalid.Add("ContributionOriginDuplicate");
                if (contribution.Quantity <= 0 || !ValidNumber(contribution.Quantity, MaximumQuantity, 6))
                { invalid.Add("ContributionQuantityInvalid"); continue; }
                if (contribution.EvidenceReferences is null || contribution.EvidenceReferences.Count == 0)
                    missing.Add("ContributionEvidenceMissing");
                else if (contribution.EvidenceReferences.Count > 64
                    || contribution.EvidenceReferences.Any(x => !Identifier(x, 512))
                    || contribution.EvidenceReferences.Distinct(StringComparer.Ordinal).Count() != contribution.EvidenceReferences.Count)
                    invalid.Add("ContributionEvidenceInvalid");
                if (!weights.TryGetValue((contribution.Kind, contribution.UnitCode), out var weight))
                { missing.Add("ContributionWeightMissing"); continue; }
                if (!ValidNumber(weight, MaximumWeight, 6)) continue;
                // 최대 소수 6자리 입력을 정수로 바꿔 곱하므로 decimal 중간 반올림·overflow가 없습니다.
                var score = Scaled(contribution.Quantity) * Scaled(weight);
                normalized.Add(new(Clone(contribution), weight, score));
            }

        var totalScore = normalized.Aggregate(BigInteger.Zero, (sum, item) => sum + item.Score);
        if (totalScore > new BigInteger(MaximumTotalWeightedUnits) * new BigInteger(ScoreScale))
            invalid.Add("TotalWeightedUnitsLimitExceeded");
        if (invalid.Count > 0) return Result(policy, NeighborhoodActivityRewardCodes.InvalidInput, invalid.Concat(missing));
        if (missing.Count > 0) return Result(policy, NeighborhoodActivityRewardCodes.WaitingForEvidence, missing);

        var surplus = Math.Max(0m, financialBasis.RevenueAmountKrw!.Value - financialBasis.BasicWorkCostAmountKrw!.Value
            - financialBasis.OtherCostAmountKrw!.Value - financialBasis.ReserveAmountKrw!.Value);
        var unrounded = surplus * policy.ReturnRatePercent!.Value / 100m;
        var pool = decimal.Floor(unrounded);
        if (pool > 0 && totalScore == 0)
            return Result(policy, NeighborhoodActivityRewardCodes.WaitingForEvidence, ["EligibleWeightMissing"]);

        var actors = normalized.GroupBy(x => x.Contribution.ActorUserId, StringComparer.Ordinal)
            .OrderBy(x => x.Key, StringComparer.Ordinal)
            .Select(group => new Share(group.Key, group.Aggregate(BigInteger.Zero, (sum, item) => sum + item.Score)))
            .ToArray();
        Allocate(new BigInteger(pool), actors);
        var result = Result(policy, NeighborhoodActivityRewardCodes.Ready, []);
        result.SurplusAmountKrw = surplus;
        result.UnroundedPoolAmountKrw = unrounded;
        result.PoolAmountKrw = pool;
        result.FractionalPoolRemainderKrw = unrounded - pool;
        result.Allocations = actors.Select(actor => new NeighborhoodActivityRewardAllocationDto
        {
            ActorUserId = actor.Id, WeightedUnits = Units(actor.Score), AmountKrw = (decimal)actor.Amount,
            ContributionIds = normalized.Where(x => x.Contribution.ActorUserId == actor.Id)
                .Select(x => x.Contribution.ContributionId).OrderBy(x => x, StringComparer.Ordinal).ToArray()
        }).ToArray();
        var contributionShares = new List<NeighborhoodActivityRewardContributionShareDto>();
        foreach (var actor in actors)
        {
            var items = normalized.Where(x => x.Contribution.ActorUserId == actor.Id)
                .OrderBy(x => x.Contribution.ContributionId, StringComparer.Ordinal).ToArray();
            var shares = items.Select(x => new Share(x.Contribution.ContributionId, x.Score)).ToArray();
            Allocate(actor.Amount, shares);
            for (var i = 0; i < items.Length; i++)
                contributionShares.Add(new()
                {
                    Contribution = items[i].Contribution, AgreedWeight = items[i].Weight,
                    WeightedUnits = Units(items[i].Score), AmountKrw = (decimal)shares[i].Amount
                });
        }
        result.ContributionShares = contributionShares.OrderBy(x => x.Contribution.ContributionId, StringComparer.Ordinal).ToArray();
        return result;
    }

    private static void Allocate(BigInteger pool, Share[] shares)
    {
        var total = shares.Aggregate(BigInteger.Zero, (sum, item) => sum + item.Score);
        if (total.IsZero) return;
        foreach (var share in shares)
            share.Amount = BigInteger.DivRem(pool * share.Score, total, out share.Remainder);
        var remaining = pool - shares.Aggregate(BigInteger.Zero, (sum, item) => sum + item.Amount);
        foreach (var share in shares.OrderByDescending(x => x.Remainder).ThenBy(x => x.Id, StringComparer.Ordinal))
        {
            if (remaining.IsZero) break;
            share.Amount++; remaining--;
        }
    }

    private static void Number(decimal? value, decimal maximum, int scale, string name,
        ISet<string> missing, ISet<string> invalid)
    {
        if (value is null) missing.Add(name + "Missing");
        else if (!ValidNumber(value.Value, maximum, scale)) invalid.Add(name + "Invalid");
    }

    private static bool ValidNumber(decimal value, decimal maximum, int scale)
        => value >= 0 && value <= maximum && decimal.Round(value, scale) == value;
    private static bool Identifier(string? value, int maximum = 191)
        => !string.IsNullOrWhiteSpace(value) && value == value.Trim() && value.Length <= maximum;
    private static bool Unit(string value)
        => value is NeighborhoodActivityRewardCodes.Unit or NeighborhoodActivityRewardCodes.StorageDay;
    private static BigInteger Scaled(decimal value) => new(value * Scale);
    private static decimal Units(BigInteger score) => (decimal)score / ScoreScale;

    private static NeighborhoodActivityRewardPreviewDto Result(NeighborhoodActivityRewardPolicyDto? policy,
        string status, IEnumerable<string> reasons)
        => new()
        {
            StatusCode = status, CalculationVersion = CurrentCalculationVersion,
            PolicyVersion = policy?.PolicyVersion ?? "", ReasonCodes = reasons.Distinct(StringComparer.Ordinal)
                .OrderBy(x => x, StringComparer.Ordinal).ToArray(), IsActualBankVerified = false
        };

    private static NeighborhoodActivityRewardContributionDto Clone(NeighborhoodActivityRewardContributionDto value)
        => new()
        {
            ContributionId = value.ContributionId, ActorUserId = value.ActorUserId, Kind = value.Kind,
            UnitCode = value.UnitCode, Quantity = value.Quantity, OriginKind = value.OriginKind,
            OriginId = value.OriginId, OriginRevision = value.OriginRevision,
            EvidenceReferences = value.EvidenceReferences?.OrderBy(x => x, StringComparer.Ordinal).ToArray() ?? []
        };
    private sealed record ScoredContribution(NeighborhoodActivityRewardContributionDto Contribution, decimal Weight, BigInteger Score);
    private sealed class Share(string id, BigInteger score)
    {
        public string Id { get; } = id;
        public BigInteger Score { get; } = score;
        public BigInteger Amount;
        public BigInteger Remainder;
    }
}
