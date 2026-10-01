using Hongdal.Contracts.Common.Orderer;
using Hongdal.Services.LogisticsProcessing.VehicleLoading;

namespace Hongdal.Services.Orderer;

public interface IDomesticGroupPurchaseVehicleRecommendationService
{
    Task<DomesticGroupPurchaseVehicleRecommendationResponse> PreviewAsync(
        DomesticGroupPurchaseVehicleRecommendationRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class DomesticGroupPurchaseVehicleRecommendationService
    : IDomesticGroupPurchaseVehicleRecommendationService
{
    private readonly I공동구매자동집단화저장소 _autoGroupStore;
    private readonly I차량적재추천Service _loadingRecommendation;

    public DomesticGroupPurchaseVehicleRecommendationService(
        I공동구매자동집단화저장소 autoGroupStore,
        I차량적재추천Service loadingRecommendation)
    {
        _autoGroupStore = autoGroupStore;
        _loadingRecommendation = loadingRecommendation;
    }

    public async Task<DomesticGroupPurchaseVehicleRecommendationResponse> PreviewAsync(
        DomesticGroupPurchaseVehicleRecommendationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRequest(request);

        var warnings = new List<string>();
        var orderSource = await ResolveOrdersAsync(request, warnings, cancellationToken);
        EnsureUniqueOrderKeys(orderSource.Orders);
        var packageMap = BuildPackageMap(request.ProductPackages);
        var productSummaries = new List<DomesticGroupPurchaseProductLoadSummary>();
        var loadingPackages = new List<차량적재포장요구사항>();

        foreach (var productOrders in orderSource.Orders.GroupBy(x => x.ProductKey, StringComparer.OrdinalIgnoreCase))
        {
            if (!packageMap.TryGetValue(productOrders.Key, out var package))
            {
                throw new ArgumentException(
                    $"상품 '{productOrders.Key}'의 외포장 제원이 필요합니다.",
                    nameof(request.ProductPackages));
            }

            var orders = productOrders.ToArray();
            ValidateOrderUnits(orders, package);
            var totalQuantity = orders.Sum(x => x.Quantity);
            var packageCount = request.KeepParticipantPackagesSeparate
                ? SumParticipantPackageCounts(orders, package.UnitsPerPackage)
                : CalculatePackageCount(totalQuantity, package.UnitsPerPackage);
            var packageVolume = CalculatePackageVolumeCbm(package);
            var totalVolume = packageVolume * packageCount;
            var totalWeight = package.PackageGrossWeightKg * packageCount;
            int? productPalletCount = package.PackagesPerPallet is > 0
                ? CalculatePackageCount(packageCount, package.PackagesPerPallet.Value)
                : null;

            productSummaries.Add(new DomesticGroupPurchaseProductLoadSummary
            {
                ProductKey = package.ProductKey.Trim(),
                ProductName = string.IsNullOrWhiteSpace(package.ProductName)
                    ? package.ProductKey.Trim()
                    : package.ProductName.Trim(),
                TotalOrderedQuantity = totalQuantity,
                QuantityUnit = package.QuantityUnit.Trim(),
                OrderCount = orders.Length,
                ParticipantCount = orders.Select(x => x.ParticipantKey).Distinct(StringComparer.Ordinal).Count(),
                PackageCount = packageCount,
                PackageGrossWeightKg = package.PackageGrossWeightKg,
                TotalGrossWeightKg = Round(totalWeight),
                TotalPackageVolumeCbm = Round(totalVolume),
                PalletCount = productPalletCount
            });

            loadingPackages.Add(new 차량적재포장요구사항
            {
                항목키 = package.ProductKey.Trim(),
                항목명 = string.IsNullOrWhiteSpace(package.ProductName)
                    ? package.ProductKey.Trim()
                    : package.ProductName.Trim(),
                포장개수 = packageCount,
                포장길이Mm = package.PackageLengthMm,
                포장폭Mm = package.PackageWidthMm,
                포장높이Mm = package.PackageHeightMm,
                바닥회전가능여부 = package.CanRotateOnFloor,
                적층가능여부 = package.Stackable
            });
        }

        if (productSummaries.Count == 0)
        {
            throw new ArgumentException("차량 추천에 반영할 주문 수량이 없습니다.", nameof(request.Orders));
        }

        WarnAboutUnusedPackageSpecifications(packageMap, productSummaries, warnings);

        var actualWeight = productSummaries.Sum(x => x.TotalGrossWeightKg);
        var rawVolume = productSummaries.Sum(x => x.TotalPackageVolumeCbm);
        var safetyFactor = 1m + request.SafetyMarginRate;
        var plannedWeight = Round(actualWeight * safetyFactor);
        var plannedVolume = Round(rawVolume / request.LoadingEfficiencyRate * safetyFactor);
        var nonStackableFloorArea = CalculateNonStackableFloorArea(
            request.ProductPackages,
            productSummaries,
            request.LoadingEfficiencyRate,
            safetyFactor);
        var palletCount = ResolvePalletCount(request, productSummaries, warnings);
        var temperature = ResolveStrictestTemperature(request.ProductPackages, productSummaries, warnings);

        var loadingRequirement = new 차량적재추천요구사항
        {
            총중량Kg = plannedWeight,
            총부피Cbm = plannedVolume,
            적층불가바닥면적M2 = nonStackableFloorArea > 0 ? nonStackableFloorArea : null,
            총팔레트개수 = palletCount,
            온도조건 = temperature,
            비눈보호필요 = request.RequiresRainProtection,
            리프트필요 = request.RequiresLift,
            측면상하차필요 = request.RequiresSideLoading,
            분할운송허용 = request.AllowSplitTransport,
            포장목록 = loadingPackages
        };

        var analysis = await _loadingRecommendation.추천Async(loadingRequirement, cancellationToken);
        var candidates = analysis.추천후보
            .Take(5)
            .Select((x, index) => ToCandidate(x, index + 1))
            .ToArray();
        var rejectedVehicles = analysis.전체평가
            .Where(x => !x.하드조건적합여부 || !request.AllowSplitTransport && !x.단일운송가능여부)
            .OrderBy(x => x.차량.추천우선순위)
            .Take(5)
            .Select(ToRejectedVehicle)
            .ToArray();

        if (analysis.전체평가.Count == 0)
        {
            warnings.Add("추천에 사용할 차량 제원 기준이 비어 있습니다.");
        }
        else if (candidates.Length == 0)
        {
            warnings.Add("포장 규격과 필수 운송 조건을 만족하는 차량을 찾지 못했습니다.");
        }
        else
        {
            warnings.AddRange(candidates.SelectMany(x => x.VerificationWarnings));
            if (!candidates[0].CanTransportInSingleTrip)
            {
                warnings.Add(
                    $"한 차량에 전량 적재할 수 없어 {candidates[0].VehicleType} 기준 " +
                    $"약 {candidates[0].RecommendedTripCount}회 분할 운송을 제안합니다.");
            }
        }

        warnings.Add("차량 제원은 시스템의 추천 기준값입니다. 실제 배차 전 차량 등록증, 개조 상태와 현장 적재 가능 여부를 확인해야 합니다.");

        var first = candidates.FirstOrDefault();
        return new DomesticGroupPurchaseVehicleRecommendationResponse
        {
            GroupPurchaseCampaignId = request.GroupPurchaseCampaignId,
            AutoGroupId = request.AutoGroupId?.Trim() ?? string.Empty,
            QuantitySourceCode = orderSource.QuantitySourceCode,
            ContainsUnconfirmedDemand = orderSource.ContainsUnconfirmedDemand,
            ParticipantCount = orderSource.Orders.Select(x => x.ParticipantKey).Distinct(StringComparer.Ordinal).Count(),
            OrderCount = orderSource.Orders.Count,
            TotalPackageCount = productSummaries.Sum(x => x.PackageCount),
            ActualGrossWeightKg = Round(actualWeight),
            PlannedWeightWithMarginKg = plannedWeight,
            RawPackageVolumeCbm = Round(rawVolume),
            PlannedLoadingVolumeCbm = plannedVolume,
            NonStackableFloorAreaM2 = nonStackableFloorArea,
            PalletCount = palletCount,
            LoadingEfficiencyRate = request.LoadingEfficiencyRate,
            SafetyMarginRate = request.SafetyMarginRate,
            TemperatureCode = temperature,
            RecommendedVehicleType = first?.VehicleType ?? string.Empty,
            CanTransportInSingleTrip = first?.CanTransportInSingleTrip == true,
            RecommendedTripCount = first?.RecommendedTripCount ?? 0,
            ProductSummaries = productSummaries,
            Candidates = candidates,
            RejectedVehicles = rejectedVehicles,
            CalculationBasis = BuildCalculationBasis(request, orderSource.Orders.Count, productSummaries),
            Warnings = warnings.Distinct(StringComparer.Ordinal).ToArray()
        };
    }

    private async Task<ResolvedOrders> ResolveOrdersAsync(
        DomesticGroupPurchaseVehicleRecommendationRequest request,
        ICollection<string> warnings,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.AutoGroupId))
        {
            return new ResolvedOrders(
                request.Orders.Select((x, index) => NormalizeOrder(x, index)).ToArray(),
                DomesticGroupPurchaseQuantitySourceCodes.ExplicitOrders,
                false);
        }

        var group = await _autoGroupStore.집단조회Async(request.AutoGroupId.Trim(), cancellationToken)
                    ?? throw new KeyNotFoundException("공동구매 자동집단을 찾을 수 없습니다.");
        if (string.IsNullOrWhiteSpace(group.상품키))
        {
            throw new ArgumentException("공동구매 자동집단에 상품키가 없어 포장 제원과 연결할 수 없습니다.");
        }
        var committedOnly = string.Equals(
            request.QuantitySourceCode,
            DomesticGroupPurchaseQuantitySourceCodes.ReservedOrConfirmed,
            StringComparison.OrdinalIgnoreCase);
        var selected = committedOnly
            ? group.수요목록.Where(IsReservedOrConfirmed).ToArray()
            : group.수요목록.ToArray();

        if (selected.Length == 0)
        {
            throw new ArgumentException(
                committedOnly
                    ? "예약 또는 결제 확정된 공동구매 주문이 없습니다. 초기 예측은 all-demand 모드를 사용하세요."
                    : "공동구매 자동집단에 합산할 수요가 없습니다.",
                nameof(request.QuantitySourceCode));
        }

        var containsUnconfirmed = selected.Any(x => !IsReservedOrConfirmed(x));
        if (containsUnconfirmed)
        {
            warnings.Add("관심 표시 단계의 미확정 수요가 포함된 예측입니다. 실제 발주 확정 후 다시 계산해야 합니다.");
        }

        var orders = selected.Select((x, index) => new ResolvedOrder(
            string.IsNullOrWhiteSpace(x.수요Id) ? $"auto-demand-{index + 1}" : x.수요Id.Trim(),
            string.IsNullOrWhiteSpace(x.수요Id) ? $"participant-{index + 1}" : x.수요Id.Trim(),
            group.상품키.Trim(),
            x.희망수량,
            string.IsNullOrWhiteSpace(x.수량단위)
                ? group.수량단위?.Trim() ?? string.Empty
                : x.수량단위.Trim())).ToArray();

        return new ResolvedOrders(
            orders,
            committedOnly
                ? DomesticGroupPurchaseQuantitySourceCodes.ReservedOrConfirmed
                : DomesticGroupPurchaseQuantitySourceCodes.AllDemand,
            containsUnconfirmed);
    }

    private static Dictionary<string, DomesticGroupPurchaseProductPackageSpecification> BuildPackageMap(
        IReadOnlyList<DomesticGroupPurchaseProductPackageSpecification> specifications)
    {
        var map = new Dictionary<string, DomesticGroupPurchaseProductPackageSpecification>(StringComparer.OrdinalIgnoreCase);
        foreach (var specification in specifications)
        {
            ValidatePackage(specification);
            var key = specification.ProductKey.Trim();
            if (!map.TryAdd(key, specification))
            {
                throw new ArgumentException($"상품 '{key}'의 포장 제원이 중복되었습니다.", nameof(specifications));
            }
        }

        return map;
    }

    private static void ValidateRequest(DomesticGroupPurchaseVehicleRecommendationRequest request)
    {
        request.Orders ??= [];
        request.ProductPackages ??= [];
        if (request.GroupPurchaseCampaignId == Guid.Empty)
        {
            throw new ArgumentException("공동구매 캠페인 식별자가 필요합니다.", nameof(request));
        }
        if (!string.IsNullOrWhiteSpace(request.AutoGroupId) && request.Orders.Count > 0)
        {
            throw new ArgumentException("자동집단 수량과 직접 입력 주문을 동시에 합산할 수 없습니다.", nameof(request));
        }
        if (string.IsNullOrWhiteSpace(request.AutoGroupId) && request.Orders.Count == 0)
        {
            throw new ArgumentException("자동집단 식별자 또는 주문 목록이 필요합니다.", nameof(request));
        }
        if (!string.IsNullOrWhiteSpace(request.AutoGroupId)
            && !DomesticGroupPurchaseQuantitySourceCodes.All.Contains(request.QuantitySourceCode?.Trim() ?? string.Empty))
        {
            throw new ArgumentException("지원하지 않는 공동구매 수량 집계 범위입니다.", nameof(request.QuantitySourceCode));
        }
        if (request.ProductPackages.Count == 0)
        {
            throw new ArgumentException("상품 외포장 제원이 하나 이상 필요합니다.", nameof(request.ProductPackages));
        }
        if (request.LoadingEfficiencyRate is <= 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(request.LoadingEfficiencyRate), "적재공간 효율은 0 초과 1 이하여야 합니다.");
        }
        if (request.SafetyMarginRate is < 0 or > 0.5m)
        {
            throw new ArgumentOutOfRangeException(nameof(request.SafetyMarginRate), "안전 여유율은 0 이상 0.5 이하여야 합니다.");
        }
        if (request.ExplicitPalletCount is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request.ExplicitPalletCount), "팔레트 수는 0 이상이어야 합니다.");
        }
    }

    private static void ValidatePackage(DomesticGroupPurchaseProductPackageSpecification package)
    {
        if (string.IsNullOrWhiteSpace(package.ProductKey)
            || string.IsNullOrWhiteSpace(package.QuantityUnit))
        {
            throw new ArgumentException("포장 제원에는 상품키와 수량 단위가 필요합니다.", nameof(package));
        }
        if (package.UnitsPerPackage <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(package.UnitsPerPackage), "포장당 상품 수량은 0보다 커야 합니다.");
        }
        if (package.PackageLengthMm <= 0 || package.PackageWidthMm <= 0 || package.PackageHeightMm <= 0)
        {
            throw new ArgumentException("외포장 길이, 폭, 높이는 모두 0보다 커야 합니다.", nameof(package));
        }
        if (package.PackageGrossWeightKg <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(package.PackageGrossWeightKg), "외포장 총중량은 0보다 커야 합니다.");
        }
        if (package.PackagesPerPallet is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(package.PackagesPerPallet), "팔레트당 포장 수는 0보다 커야 합니다.");
        }
    }

    private static ResolvedOrder NormalizeOrder(DomesticGroupPurchaseVehicleOrderItem order, int index)
    {
        if (string.IsNullOrWhiteSpace(order.ProductKey)
            || string.IsNullOrWhiteSpace(order.QuantityUnit)
            || order.Quantity <= 0)
        {
            throw new ArgumentException($"주문 목록 {index + 1}번째 항목의 상품키, 양수 수량과 단위가 필요합니다.");
        }

        var orderKey = string.IsNullOrWhiteSpace(order.OrderKey) ? $"order-{index + 1}" : order.OrderKey.Trim();
        var participantKey = string.IsNullOrWhiteSpace(order.ParticipantKey)
            ? orderKey
            : order.ParticipantKey.Trim();
        return new ResolvedOrder(
            orderKey,
            participantKey,
            order.ProductKey.Trim(),
            order.Quantity,
            order.QuantityUnit.Trim());
    }

    private static void ValidateOrderUnits(
        IEnumerable<ResolvedOrder> orders,
        DomesticGroupPurchaseProductPackageSpecification package)
    {
        var mismatched = orders.FirstOrDefault(x =>
            !string.Equals(x.QuantityUnit, package.QuantityUnit.Trim(), StringComparison.OrdinalIgnoreCase));
        if (mismatched is not null)
        {
            throw new ArgumentException(
                $"상품 '{package.ProductKey}'의 주문 단위({mismatched.QuantityUnit})와 " +
                $"포장 단위({package.QuantityUnit})가 달라 환산할 수 없습니다.");
        }
    }

    private static int SumParticipantPackageCounts(IEnumerable<ResolvedOrder> orders, decimal unitsPerPackage)
    {
        long total = 0;
        foreach (var participantOrders in orders.GroupBy(x => x.ParticipantKey, StringComparer.Ordinal))
        {
            total += CalculatePackageCount(participantOrders.Sum(x => x.Quantity), unitsPerPackage);
            if (total > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(orders), "계산된 포장 수가 허용 범위를 초과했습니다.");
            }
        }

        return (int)total;
    }

    private static void EnsureUniqueOrderKeys(IReadOnlyList<ResolvedOrder> orders)
    {
        var duplicate = orders
            .GroupBy(x => x.OrderKey, StringComparer.Ordinal)
            .FirstOrDefault(x => x.Count() > 1);
        if (duplicate is not null)
        {
            throw new ArgumentException($"주문키 '{duplicate.Key}'가 중복되어 수량을 안전하게 합산할 수 없습니다.");
        }
    }

    private static int CalculatePackageCount(decimal quantity, decimal unitsPerPackage)
    {
        var value = decimal.Ceiling(quantity / unitsPerPackage);
        if (value <= 0 || value > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "계산된 포장 수가 허용 범위를 벗어났습니다.");
        }

        return decimal.ToInt32(value);
    }

    private static decimal CalculatePackageVolumeCbm(DomesticGroupPurchaseProductPackageSpecification package)
        => (package.PackageLengthMm / 1000m)
           * (package.PackageWidthMm / 1000m)
           * (package.PackageHeightMm / 1000m);

    private static decimal CalculateNonStackableFloorArea(
        IReadOnlyList<DomesticGroupPurchaseProductPackageSpecification> packages,
        IReadOnlyList<DomesticGroupPurchaseProductLoadSummary> summaries,
        decimal loadingEfficiency,
        decimal safetyFactor)
    {
        var packageMap = packages.ToDictionary(x => x.ProductKey.Trim(), StringComparer.OrdinalIgnoreCase);
        var rawArea = summaries
            .Where(x => !packageMap[x.ProductKey].Stackable)
            .Sum(x =>
                (packageMap[x.ProductKey].PackageLengthMm / 1000m)
                * (packageMap[x.ProductKey].PackageWidthMm / 1000m)
                * x.PackageCount);
        return rawArea <= 0 ? 0 : Round(rawArea / loadingEfficiency * safetyFactor);
    }

    private static int? ResolvePalletCount(
        DomesticGroupPurchaseVehicleRecommendationRequest request,
        IReadOnlyList<DomesticGroupPurchaseProductLoadSummary> summaries,
        ICollection<string> warnings)
    {
        if (request.ExplicitPalletCount.HasValue)
        {
            return request.ExplicitPalletCount.Value;
        }

        if (summaries.All(x => x.PalletCount.HasValue))
        {
            return summaries.Sum(x => x.PalletCount!.Value);
        }

        if (summaries.Any(x => x.PalletCount.HasValue))
        {
            warnings.Add("일부 상품의 팔레트당 포장 수가 없어 팔레트 적재 조건은 추천에서 제외했습니다.");
        }

        return null;
    }

    private static string ResolveStrictestTemperature(
        IReadOnlyList<DomesticGroupPurchaseProductPackageSpecification> packages,
        IReadOnlyList<DomesticGroupPurchaseProductLoadSummary> summaries,
        ICollection<string> warnings)
    {
        var usedKeys = summaries.Select(x => x.ProductKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var values = packages
            .Where(x => usedKeys.Contains(x.ProductKey.Trim()))
            .Select(x => x.TemperatureCode?.Trim() ?? string.Empty)
            .Where(x => x.Length > 0)
            .ToArray();
        if (values.Any(IsFrozen))
        {
            return "냉동";
        }
        if (values.Any(IsRefrigerated))
        {
            return "냉장";
        }

        var unknown = values.FirstOrDefault(x => !IsAmbient(x));
        if (unknown is not null)
        {
            warnings.Add($"알 수 없는 온도조건 '{unknown}'은 상온으로 계산했습니다.");
        }
        return "상온";
    }

    private static void WarnAboutUnusedPackageSpecifications(
        IReadOnlyDictionary<string, DomesticGroupPurchaseProductPackageSpecification> packageMap,
        IReadOnlyList<DomesticGroupPurchaseProductLoadSummary> summaries,
        ICollection<string> warnings)
    {
        var used = summaries.Select(x => x.ProductKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var unused = packageMap.Keys.Where(x => !used.Contains(x)).ToArray();
        if (unused.Length > 0)
        {
            warnings.Add($"주문이 없는 상품 포장 제원 {unused.Length}건은 계산에서 제외했습니다.");
        }
    }

    private static DomesticGroupPurchaseVehicleCandidateResponse ToCandidate(
        차량적재차량평가 evaluation,
        int rank)
        => new()
        {
            Rank = rank,
            VehicleCode = evaluation.차량.차량코드,
            VehicleType = evaluation.차량.차량명,
            BodyType = evaluation.차량.차체형태,
            LoadBedLengthMm = evaluation.차량.적재함길이Mm,
            LoadBedWidthMm = evaluation.차량.적재함폭Mm,
            LoadBedHeightMm = evaluation.차량.적재함높이Mm,
            AllowedWeightKg = evaluation.허용중량Kg,
            AllowedVolumeCbm = evaluation.허용부피Cbm,
            AllowedPalletCount = evaluation.차량.팔레트적재개수,
            CanTransportInSingleTrip = evaluation.단일운송가능여부,
            RecommendedTripCount = evaluation.권장운행횟수,
            WeightUtilizationPercent = evaluation.중량사용률Percent,
            VolumeUtilizationPercent = evaluation.부피사용률Percent,
            PalletUtilizationPercent = evaluation.팔레트사용률Percent,
            FloorAreaUtilizationPercent = evaluation.바닥면적사용률Percent,
            SingleTripLimitReasons = evaluation.단일운송불가사유,
            VerificationWarnings = evaluation.검증경고,
            Summary = evaluation.단일운송가능여부
                ? $"한 번에 운송 가능 · 중량 {FormatPercent(evaluation.중량사용률Percent)} · 부피 {FormatPercent(evaluation.부피사용률Percent)}"
                : $"약 {evaluation.권장운행횟수}회 분할 운송 · " + string.Join(", ", evaluation.단일운송불가사유)
        };

    private static DomesticGroupPurchaseRejectedVehicleResponse ToRejectedVehicle(차량적재차량평가 evaluation)
        => new()
        {
            VehicleCode = evaluation.차량.차량코드,
            VehicleType = evaluation.차량.차량명,
            Reasons = evaluation.하드부적합사유
                .Concat(evaluation.단일운송불가사유)
                .Distinct(StringComparer.Ordinal)
                .ToArray()
        };

    private static IReadOnlyList<string> BuildCalculationBasis(
        DomesticGroupPurchaseVehicleRecommendationRequest request,
        int orderCount,
        IReadOnlyList<DomesticGroupPurchaseProductLoadSummary> summaries)
    {
        var basis = new List<string>
        {
            $"주문 {orderCount:N0}건을 상품별로 합산해 외포장 {summaries.Sum(x => x.PackageCount):N0}개로 환산했습니다.",
            request.KeepParticipantPackagesSeparate
                ? "참여자별 남은 수량을 서로 합치지 않고 각 참여자 단위로 포장 수를 올림 계산했습니다."
                : "같은 상품의 주문 수량을 먼저 합산한 뒤 포장당 수량으로 나누어 포장 수를 올림 계산했습니다.",
            $"외포장 체적 합계에 적재공간 효율 {request.LoadingEfficiencyRate:P0}와 안전 여유 {request.SafetyMarginRate:P0}를 적용했습니다.",
            "외포장 총중량에는 안전 여유를 더하고 차량의 운영 권장 중량과 비교했습니다.",
            "부분 포장도 완전한 외포장 한 개의 총중량으로 보아 보수적으로 계산했습니다.",
            "각 외포장이 적재함 바닥에서 회전해 들어가는지 확인했으며, 적층 불가 상품은 필요한 바닥면적도 비교했습니다.",
            "분할 운행 횟수는 중량·부피·팔레트·바닥면적 비율 중 가장 큰 값을 올림한 계획값이며 실제 혼합 상차 결과에 따라 늘어날 수 있습니다."
        };

        return basis;
    }

    private static bool IsReservedOrConfirmed(공동구매자동수요응답 demand)
        => string.Equals(demand.수요유형, 공동구매자동수요유형코드.예약결제, StringComparison.OrdinalIgnoreCase)
           || string.Equals(demand.결제상태, 공동구매자동결제상태코드.예약됨, StringComparison.OrdinalIgnoreCase)
           || string.Equals(demand.결제상태, 공동구매자동결제상태코드.결제확정, StringComparison.OrdinalIgnoreCase);

    private static bool IsFrozen(string value)
        => value.Contains("냉동", StringComparison.OrdinalIgnoreCase)
           || value.Contains("frozen", StringComparison.OrdinalIgnoreCase);

    private static bool IsRefrigerated(string value)
        => value.Contains("냉장", StringComparison.OrdinalIgnoreCase)
           || value.Contains("chilled", StringComparison.OrdinalIgnoreCase)
           || value.Contains("refrigerated", StringComparison.OrdinalIgnoreCase);

    private static bool IsAmbient(string value)
        => value.Contains("상온", StringComparison.OrdinalIgnoreCase)
           || value.Contains("ambient", StringComparison.OrdinalIgnoreCase)
           || value.Contains("room", StringComparison.OrdinalIgnoreCase);

    private static string FormatPercent(decimal? value)
        => value.HasValue ? $"{value.Value:0.#}%" : "검증값 없음";

    private static decimal Round(decimal value)
        => decimal.Round(value, 3, MidpointRounding.AwayFromZero);

    private sealed record ResolvedOrder(
        string OrderKey,
        string ParticipantKey,
        string ProductKey,
        decimal Quantity,
        string QuantityUnit);

    private sealed record ResolvedOrders(
        IReadOnlyList<ResolvedOrder> Orders,
        string QuantitySourceCode,
        bool ContainsUnconfirmedDemand);
}
