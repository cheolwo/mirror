using Hongdal.Contracts.Common.CollectiveProcurement;

namespace Hongdal.Services.CollectiveProcurement;

public interface ICollectiveProcurementEconomicsEngine
{
    CollectiveProcurementAssessmentResponse Evaluate(
        CollectiveProcurementAssessmentRequest request,
        DateTimeOffset evaluatedAtUtc);
}

public sealed class CollectiveProcurementEconomicsEngine : ICollectiveProcurementEconomicsEngine
{
    private const int MaximumCalculationPoints = 10_000;
    private const int MaximumResponseScenarios = 200;

    public CollectiveProcurementAssessmentResponse Evaluate(
        CollectiveProcurementAssessmentRequest request,
        DateTimeOffset evaluatedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(request);
        Validate(request);

        var quantities = BuildCalculationQuantities(request);
        var scenarios = quantities
            .Select(quantity => EvaluateQuantity(request, quantity))
            .OrderBy(scenario => scenario.Quantity)
            .ToArray();
        var viable = scenarios.Where(scenario => scenario.EconomicallyViable).ToArray();
        var minimumViable = viable.FirstOrDefault();
        var recommended = viable
            .OrderBy(scenario => scenario.EstimatedUnitLandedCost)
            .ThenBy(scenario => scenario.Quantity)
            .FirstOrDefault();
        var current = request.CurrentCommittedQuantity > 0
            ? EvaluateQuantity(request, request.CurrentCommittedQuantity)
            : null;
        var potential = request.CurrentPotentialQuantity > 0
            ? EvaluateQuantity(request, request.CurrentPotentialQuantity)
            : null;

        var benefitPool = Math.Max(0m, recommended?.TotalExpectedBenefit ?? 0m);
        var proposedBenefit = request.BenefitPositions.Sum(position => position.ProposedBenefitAmount);
        var allocationWithinPool = proposedBenefit <= benefitPool;
        var allMinimumsMet = request.BenefitPositions.All(position =>
            position.ProposedBenefitAmount >= position.MinimumAcceptableBenefitAmount);
        var concentrationWithinPolicy = IsWithinConcentrationPolicy(request, proposedBenefit);
        var warnings = BuildWarnings(
            request,
            evaluatedAtUtc,
            current,
            potential,
            minimumViable,
            recommended,
            benefitPool,
            proposedBenefit,
            allMinimumsMet,
            concentrationWithinPolicy);

        return new CollectiveProcurementAssessmentResponse
        {
            CurrencyCode = request.CurrencyCode.Trim().ToUpperInvariant(),
            QuantityUnit = request.QuantityUnit.Trim(),
            CurrentCommittedQuantity = request.CurrentCommittedQuantity,
            CurrentPotentialQuantity = request.CurrentPotentialQuantity,
            MinimumOrderQuantity = request.MinimumOrderQuantity,
            MaximumSafeQuantity = request.MaximumSafeQuantity,
            MinimumViableQuantity = minimumViable?.Quantity,
            RecommendedQuantity = recommended?.Quantity,
            AdditionalQuantityToMinimumViable = minimumViable is null
                ? null
                : Math.Max(0m, minimumViable.Quantity - request.CurrentCommittedQuantity),
            AdditionalQuantityToRecommended = recommended is null
                ? null
                : Math.Max(0m, recommended.Quantity - request.CurrentCommittedQuantity),
            BenefitPoolAmount = RoundMoney(benefitPool),
            TotalProposedBenefitAmount = RoundMoney(proposedBenefit),
            UnallocatedBenefitAmount = RoundMoney(benefitPool - proposedBenefit),
            AllocationWithinBenefitPool = allocationWithinPool,
            AllParticipantsMeetPrivateMinimums = allMinimumsMet,
            BenefitConcentrationWithinAgreedPolicy = concentrationWithinPolicy,
            BenefitAgreementReady = recommended is not null
                                    && allocationWithinPool
                                    && allMinimumsMet
                                    && concentrationWithinPolicy,
            CurrentQuantityEconomicallyViable = current?.EconomicallyViable == true,
            CurrentCommittedScenario = current,
            CurrentPotentialScenario = potential,
            MinimumViableScenario = minimumViable,
            RecommendedScenario = recommended,
            CandidateScenarios = BuildResponseScenarios(
                scenarios,
                request,
                minimumViable,
                recommended,
                current,
                potential),
            Warnings = warnings
        };
    }

    private static CollectiveProcurementQuantityScenarioResponse EvaluateQuantity(
        CollectiveProcurementAssessmentRequest request,
        decimal quantity)
    {
        var tier = request.SupplierPriceTiers
            .OrderBy(item => item.MinimumQuantity)
            .LastOrDefault(item => item.MinimumQuantity <= quantity)
            ?? request.SupplierPriceTiers.OrderBy(item => item.MinimumQuantity).First();
        var goodsCost = tier.UnitPrice * quantity;
        var nonPercentageCosts = request.CostComponents.Sum(component => component.ModelCode switch
        {
            CollectiveProcurementCostModelCodes.Fixed => component.Amount,
            CollectiveProcurementCostModelCodes.PerUnit => component.Amount * quantity,
            CollectiveProcurementCostModelCodes.CapacityStep =>
                Math.Ceiling(quantity / component.CapacityQuantity!.Value) * component.Amount,
            CollectiveProcurementCostModelCodes.PercentOfSubtotal => 0m,
            _ => throw new InvalidOperationException($"지원하지 않는 비용 계산 방식입니다: {component.ModelCode}")
        });
        var baseSubtotal = goodsCost + nonPercentageCosts;
        var percentageCosts = request.CostComponents
            .Where(component => string.Equals(
                component.ModelCode,
                CollectiveProcurementCostModelCodes.PercentOfSubtotal,
                StringComparison.OrdinalIgnoreCase))
            .Sum(component => baseSubtotal * component.Amount / 100m);
        var subtotal = baseSubtotal + percentageCosts;
        var riskReserve = subtotal * request.RiskReservePercent / 100m;
        var estimatedTotal = subtotal + riskReserve;
        var comparisonTotal = request.ComparisonUnitPrice * quantity;
        var benefit = comparisonTotal - estimatedTotal;
        var savingsPercent = comparisonTotal == 0m ? 0m : benefit / comparisonTotal * 100m;
        var meetsMinimum = quantity >= request.MinimumOrderQuantity;
        var withinMaximum = quantity <= request.MaximumSafeQuantity;
        var meetsTarget = savingsPercent >= request.TargetSavingsPercent;

        return new CollectiveProcurementQuantityScenarioResponse
        {
            Quantity = quantity,
            ComparisonTotalCost = RoundMoney(comparisonTotal),
            EstimatedTotalCost = RoundMoney(estimatedTotal),
            EstimatedUnitLandedCost = RoundUnitPrice(estimatedTotal / quantity),
            TotalExpectedBenefit = RoundMoney(benefit),
            SavingsPercent = Math.Round(savingsPercent, 2, MidpointRounding.AwayFromZero),
            MeetsMinimumOrderQuantity = meetsMinimum,
            WithinMaximumSafeQuantity = withinMaximum,
            MeetsTargetSavings = meetsTarget,
            EconomicallyViable = meetsMinimum && withinMaximum && meetsTarget
        };
    }

    private static IReadOnlyList<decimal> BuildCalculationQuantities(
        CollectiveProcurementAssessmentRequest request)
    {
        decimal pointCountValue;
        try
        {
            pointCountValue = Math.Ceiling(request.MaximumSafeQuantity / request.QuantityIncrement);
        }
        catch (OverflowException)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                $"최대 안전 물량과 계산 증분으로 만든 계산 구간은 {MaximumCalculationPoints:N0}개 이하여야 합니다.");
        }

        if (pointCountValue > MaximumCalculationPoints)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                $"최대 안전 물량과 계산 증분으로 만든 계산 구간은 {MaximumCalculationPoints:N0}개 이하여야 합니다.");
        }

        var pointCount = decimal.ToInt32(pointCountValue);

        var quantities = new HashSet<decimal>();
        for (var index = 1; index <= pointCount; index++)
        {
            var quantity = request.QuantityIncrement * index;
            quantities.Add(Math.Min(quantity, request.MaximumSafeQuantity));
        }

        AddQuantity(quantities, request.CurrentCommittedQuantity, request.MaximumSafeQuantity);
        AddQuantity(quantities, request.CurrentPotentialQuantity, request.MaximumSafeQuantity);
        AddQuantity(quantities, request.MinimumOrderQuantity, request.MaximumSafeQuantity);
        AddQuantity(quantities, request.MaximumSafeQuantity, request.MaximumSafeQuantity);
        foreach (var quantity in request.CandidateQuantities)
        {
            AddQuantity(quantities, quantity, request.MaximumSafeQuantity);
        }

        foreach (var tier in request.SupplierPriceTiers)
        {
            AddQuantity(quantities, tier.MinimumQuantity, request.MaximumSafeQuantity);
        }

        return quantities.OrderBy(quantity => quantity).ToArray();
    }

    private static IReadOnlyList<CollectiveProcurementQuantityScenarioResponse> BuildResponseScenarios(
        IReadOnlyList<CollectiveProcurementQuantityScenarioResponse> scenarios,
        CollectiveProcurementAssessmentRequest request,
        CollectiveProcurementQuantityScenarioResponse? minimumViable,
        CollectiveProcurementQuantityScenarioResponse? recommended,
        CollectiveProcurementQuantityScenarioResponse? current,
        CollectiveProcurementQuantityScenarioResponse? potential)
    {
        if (scenarios.Count <= MaximumResponseScenarios)
        {
            return scenarios;
        }

        var selected = new Dictionary<decimal, CollectiveProcurementQuantityScenarioResponse>();
        var stride = (int)Math.Ceiling((decimal)scenarios.Count / (MaximumResponseScenarios - 10));
        for (var index = 0; index < scenarios.Count; index += stride)
        {
            selected[scenarios[index].Quantity] = scenarios[index];
        }

        foreach (var quantity in request.CandidateQuantities
                     .Append(request.MinimumOrderQuantity)
                     .Append(request.MaximumSafeQuantity))
        {
            var scenario = scenarios.FirstOrDefault(item => item.Quantity == quantity);
            if (scenario is not null)
            {
                selected[scenario.Quantity] = scenario;
            }
        }

        foreach (var scenario in new[] { minimumViable, recommended, current, potential })
        {
            if (scenario is not null)
            {
                selected[scenario.Quantity] = scenario;
            }
        }

        return selected.Values.OrderBy(item => item.Quantity).ToArray();
    }

    private static bool IsWithinConcentrationPolicy(
        CollectiveProcurementAssessmentRequest request,
        decimal totalProposedBenefit)
    {
        if (!request.MaximumSingleParticipantBenefitSharePercent.HasValue
            || totalProposedBenefit <= 0m)
        {
            return true;
        }

        return request.BenefitPositions.All(position =>
            position.ProposedBenefitAmount / totalProposedBenefit * 100m
            <= request.MaximumSingleParticipantBenefitSharePercent.Value);
    }

    private static IReadOnlyList<string> BuildWarnings(
        CollectiveProcurementAssessmentRequest request,
        DateTimeOffset evaluatedAtUtc,
        CollectiveProcurementQuantityScenarioResponse? current,
        CollectiveProcurementQuantityScenarioResponse? potential,
        CollectiveProcurementQuantityScenarioResponse? minimumViable,
        CollectiveProcurementQuantityScenarioResponse? recommended,
        decimal benefitPool,
        decimal proposedBenefit,
        bool allMinimumsMet,
        bool concentrationWithinPolicy)
    {
        var warnings = new List<string>();
        if (minimumViable is null)
        {
            warnings.Add("설정한 최대 안전 물량 안에서 목표 절감률을 충족하는 구간을 찾지 못했습니다.");
        }
        else if (current?.EconomicallyViable != true)
        {
            warnings.Add($"현재 확정 물량에서 경제성이 성립하지 않습니다. 최소 {minimumViable.Quantity:0.####} {request.QuantityUnit.Trim()}가 필요합니다.");
        }

        if (potential?.EconomicallyViable == true && current?.EconomicallyViable != true)
        {
            warnings.Add("예약·관심 물량이 확정되면 목표 경제성에 도달할 수 있습니다.");
        }

        if (proposedBenefit > benefitPool)
        {
            warnings.Add("참여자들이 제안한 편익 합계가 권장 물량에서 만들어지는 전체 편익을 초과합니다.");
        }

        if (!allMinimumsMet)
        {
            warnings.Add("한 명 이상의 참여자가 비공개로 설정한 최소 편익 조건을 충족하지 못했습니다.");
        }

        if (!concentrationWithinPolicy)
        {
            warnings.Add("한 참여자의 제안 편익 비중이 참여자들이 설정한 집중 상한을 초과합니다.");
        }

        if (request.SupplierPriceTiers.Any(tier => tier.ValidUntilUtc < evaluatedAtUtc)
            || request.CostComponents.Any(component => component.ValidUntilUtc < evaluatedAtUtc))
        {
            warnings.Add("유효기간이 지난 가격 또는 비용 근거가 포함되어 있습니다. 최신 견적을 확인해야 합니다.");
        }

        if (recommended is not null && request.BenefitPositions.Count == 0)
        {
            warnings.Add("상호이익 합의를 위해 참여자별 제안 편익과 비공개 최소조건을 입력해야 합니다.");
        }

        return warnings;
    }

    private static void Validate(CollectiveProcurementAssessmentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CurrencyCode) || request.CurrencyCode.Trim().Length != 3)
        {
            throw new ArgumentException("통화 코드는 ISO 4217 세 글자로 입력해야 합니다.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.QuantityUnit))
        {
            throw new ArgumentException("물량 단위를 입력해야 합니다.", nameof(request));
        }

        if (request.CurrentCommittedQuantity < 0m
            || request.CurrentPotentialQuantity < request.CurrentCommittedQuantity)
        {
            throw new ArgumentException("잠재 물량은 확정 물량보다 작을 수 없습니다.", nameof(request));
        }

        if (request.MinimumOrderQuantity <= 0m
            || request.MaximumSafeQuantity < request.MinimumOrderQuantity)
        {
            throw new ArgumentException("최대 안전 물량은 최소 주문 물량 이상이어야 합니다.", nameof(request));
        }

        if (request.QuantityIncrement <= 0m)
        {
            throw new ArgumentException("물량 계산 증분은 0보다 커야 합니다.", nameof(request));
        }

        if (request.ComparisonUnitPrice <= 0m)
        {
            throw new ArgumentException("경제성 비교 기준 단가는 0보다 커야 합니다.", nameof(request));
        }

        if (request.TargetSavingsPercent is < 0m or > 100m
            || request.RiskReservePercent is < 0m or > 100m)
        {
            throw new ArgumentException("목표 절감률과 위험 예비율은 0% 이상 100% 이하여야 합니다.", nameof(request));
        }

        if (request.MaximumSingleParticipantBenefitSharePercent is <= 0m or > 100m)
        {
            throw new ArgumentException("개별 참여자 편익 비중 상한은 0% 초과 100% 이하여야 합니다.", nameof(request));
        }

        if (request.SupplierPriceTiers.Count == 0
            || request.SupplierPriceTiers.Any(tier => tier.MinimumQuantity < 0m || tier.UnitPrice <= 0m))
        {
            throw new ArgumentException("유효한 공급 가격 구간을 하나 이상 입력해야 합니다.", nameof(request));
        }

        if (request.SupplierPriceTiers
            .GroupBy(tier => tier.MinimumQuantity)
            .Any(group => group.Count() > 1))
        {
            throw new ArgumentException("같은 최소 물량을 가진 공급 가격 구간을 중복 입력할 수 없습니다.", nameof(request));
        }

        if (request.CandidateQuantities.Any(quantity => quantity <= 0m))
        {
            throw new ArgumentException("비교 후보 물량은 0보다 커야 합니다.", nameof(request));
        }

        foreach (var component in request.CostComponents)
        {
            if (string.IsNullOrWhiteSpace(component.Code)
                || !CollectiveProcurementCostModelCodes.All.Contains(component.ModelCode)
                || component.Amount < 0m)
            {
                throw new ArgumentException("비용 항목의 코드, 계산 방식과 금액을 확인해야 합니다.", nameof(request));
            }

            if (string.Equals(component.ModelCode, CollectiveProcurementCostModelCodes.CapacityStep, StringComparison.OrdinalIgnoreCase)
                && component.CapacityQuantity is null or <= 0m)
            {
                throw new ArgumentException("구간 반복 비용에는 0보다 큰 단위 처리량이 필요합니다.", nameof(request));
            }

            if (string.Equals(component.ModelCode, CollectiveProcurementCostModelCodes.PercentOfSubtotal, StringComparison.OrdinalIgnoreCase)
                && component.Amount > 100m)
            {
                throw new ArgumentException("비율 비용은 100%를 초과할 수 없습니다.", nameof(request));
            }
        }

        if (request.CostComponents
            .GroupBy(component => component.Code, StringComparer.OrdinalIgnoreCase)
            .Any(group => group.Count() > 1))
        {
            throw new ArgumentException("비용 항목 코드는 중복될 수 없습니다.", nameof(request));
        }

        if (request.BenefitPositions.Any(position =>
                string.IsNullOrWhiteSpace(position.ParticipantReferenceCode)
                || !CollectiveProcurementBenefitKindCodes.All.Contains(position.BenefitKindCode)
                || position.ProposedBenefitAmount < 0m
                || position.MinimumAcceptableBenefitAmount < 0m))
        {
            throw new ArgumentException("참여자 편익 제안과 비공개 최소조건을 확인해야 합니다.", nameof(request));
        }

        if (request.BenefitPositions
            .GroupBy(position => position.ParticipantReferenceCode, StringComparer.OrdinalIgnoreCase)
            .Any(group => group.Count() > 1))
        {
            throw new ArgumentException("한 참여자에 대한 편익 조건을 중복 입력할 수 없습니다.", nameof(request));
        }
    }

    private static void AddQuantity(ISet<decimal> quantities, decimal quantity, decimal maximum)
    {
        if (quantity > 0m && quantity <= maximum)
        {
            quantities.Add(quantity);
        }
    }

    private static decimal RoundMoney(decimal value)
        => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static decimal RoundUnitPrice(decimal value)
        => Math.Round(value, 4, MidpointRounding.AwayFromZero);
}
