namespace 살뜰.Services.Dispatch.Coordination;

public interface I국내화물배차조율Service
{
    국내화물배차조율결과 조율(국내화물배차조율입력 input);
}

public sealed partial class 국내화물배차조율Service : I국내화물배차조율Service
{
    private readonly I국내화물기사배정AIService _기사배정AIService;
    private readonly I배차AI판단근거조회Service _판단근거조회Service;

    public 국내화물배차조율Service()
        : this(new 규칙기반국내화물기사배정AIService(), new 규칙기반배차AI판단근거조회Service())
    {
    }

    public 국내화물배차조율Service(I국내화물기사배정AIService 기사배정AIService)
        : this(기사배정AIService, new 규칙기반배차AI판단근거조회Service())
    {
    }

    public 국내화물배차조율Service(
        I국내화물기사배정AIService 기사배정AIService,
        I배차AI판단근거조회Service 판단근거조회Service)
    {
        _기사배정AIService = 기사배정AIService;
        _판단근거조회Service = 판단근거조회Service;
    }

    public 국내화물배차조율결과 조율(국내화물배차조율입력 input)
    {
        var driverAssignmentPolicy = input.기사배정AI정책;
        var maxPerDriver = Math.Max(1, input.기사당최대추천건수);
        var driverCapacityMap = input.기사후보목록
            .Where(x => !string.IsNullOrWhiteSpace(x.기사Id))
            .GroupBy(x => x.기사Id, StringComparer.Ordinal)
            .ToDictionary(
                x => x.Key,
                _ => maxPerDriver,
                StringComparer.Ordinal);
        var rawRequestIds = input.운송의뢰목록
            .Select(x => x.의뢰Id)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var driverIds = driverCapacityMap
            .Where(x => x.Value > 0)
            .Select(x => x.Key)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var driverAssignmentEvidence = _판단근거조회Service.조회(new 배차AI판단근거요청(
            "국내화물운송OS:기사배정",
            rawRequestIds,
            driverIds,
            input.운송의뢰목록
                .Select(x => x.배달권키)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.Ordinal)
                .ToArray(),
            input.수익묶음후보목록?
                .Where(x => x.묶음가능여부)
                .Select(x => x.묶음크기)
                .DefaultIfEmpty(1)
                .Max() ?? 1,
            null,
            driverAssignmentPolicy?.목표기사건당지급액,
            ["기사", "배정", "지급액", "Aging", "시간창"]));
        var revenueBundleCostBenefitMap = BuildRevenueBundleCostBenefitMap(input.수익묶음후보목록);
        var rawCandidates = input.조합평가목록
            .Where(x => x.추천가능여부)
            .Where(x => rawRequestIds.Contains(x.의뢰Id, StringComparer.Ordinal))
            .Where(x => driverIds.Contains(x.기사Id, StringComparer.Ordinal))
            .Select(x => ApplyRevenueBundlePriority(x, revenueBundleCostBenefitMap))
            .Select(x => _기사배정AIService.표시보정(x, driverAssignmentPolicy))
            .ToArray();
        var platformSelection = SelectPlatformFirstRequests(
            rawRequestIds,
            input.수익묶음후보목록,
            driverCapacityMap.Values.Sum(x => Math.Max(0, x)),
            rawCandidates.Select(x => x.의뢰Id).ToHashSet(StringComparer.Ordinal));
        var requestIds = platformSelection.선별의뢰Ids;
        var strategy = SelectStrategy(requestIds.Count, driverCapacityMap);
        var candidates = rawCandidates
            .Where(x => requestIds.Contains(x.의뢰Id, StringComparer.Ordinal))
            .ToArray();

        var requestScopeMap = input.운송의뢰목록
            .Where(x => !string.IsNullOrWhiteSpace(x.의뢰Id))
            .GroupBy(x => x.의뢰Id, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.First().배달권키, StringComparer.Ordinal);
        var driverScopeMap = input.기사후보목록
            .Where(x => !string.IsNullOrWhiteSpace(x.기사Id))
            .GroupBy(x => x.기사Id, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.First().배달권키, StringComparer.Ordinal);
        var remainingDriverCapacityMap = driverCapacityMap
            .ToDictionary(x => x.Key, x => Math.Max(0, x.Value), StringComparer.Ordinal);
        var bundleMatchedCandidates = AssignPlatformBundlesByDriverPerspective(
            platformSelection.선별묶음후보목록,
            driverIds,
            candidates,
            strategy,
            remainingDriverCapacityMap,
            revenueBundleCostBenefitMap,
            driverAssignmentPolicy,
            driverAssignmentEvidence);
        var bundleAssignedRequestIds = bundleMatchedCandidates
            .Select(x => x.의뢰Id)
            .ToHashSet(StringComparer.Ordinal);
        var remainingRequestIds = requestIds
            .Where(x => !bundleAssignedRequestIds.Contains(x))
            .ToArray();
        var remainingDriverIds = driverIds
            .Where(x => remainingDriverCapacityMap.TryGetValue(x, out var capacity) && capacity > 0)
            .ToArray();
        var remainingCandidates = candidates
            .Where(x => !bundleAssignedRequestIds.Contains(x.의뢰Id))
            .Where(x => remainingDriverIds.Contains(x.기사Id, StringComparer.Ordinal))
            .ToArray();
        var fallbackMatchedCandidates = OptimizeByDeliveryScope(
            remainingRequestIds,
            remainingDriverIds,
            remainingCandidates,
            strategy with { 기사용량 = remainingDriverCapacityMap },
            requestScopeMap,
            driverScopeMap,
            revenueBundleCostBenefitMap,
            _기사배정AIService,
            driverAssignmentPolicy);
        var matchedCandidates = bundleMatchedCandidates
            .Concat(fallbackMatchedCandidates)
            .ToArray();
        var driverAssignmentCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var proposals = matchedCandidates
            .Select((candidate, index) =>
            {
                driverAssignmentCounts.TryGetValue(candidate.기사Id, out var driverCount);
                driverAssignmentCounts[candidate.기사Id] = driverCount + 1;
                return new 국내화물배차제안(
                    index + 1,
                    candidate.의뢰Id,
                    candidate.기사Id,
                    driverCount + 1,
                    candidate.추천점수,
                    candidate.예상총비용,
                    candidate.예상운임,
                    candidate.예상순이익,
                    candidate.추천사유,
                    candidate.배지)
                {
                    판정근거 = candidate.판정근거
                };
            })
            .ToArray();
        var assignedRequests = proposals.Select(x => x.의뢰Id).ToHashSet(StringComparer.Ordinal);

        var excluded = input.조합평가목록
            .Where(x => !x.추천가능여부)
            .Select(x => new 국내화물배차제외(x.의뢰Id, x.기사Id, x.제외사유))
            .ToArray();

        var holds = input.운송의뢰목록
            .Where(x => !assignedRequests.Contains(x.의뢰Id))
            .Select(x => new 국내화물배차보류(
                x.의뢰Id,
                input.조합평가목록.Any(c => string.Equals(c.의뢰Id, x.의뢰Id, StringComparison.Ordinal))
                    ? "추천 가능한 기사 용량이 부족하거나 더 높은 점수의 의뢰가 먼저 배정되었습니다."
                    : "평가 가능한 기사 후보가 없습니다."))
            .ToArray();

        return new 국내화물배차조율결과(
            input.기준시각Utc,
            proposals,
            excluded,
            holds,
            Sum(proposals.Select(x => x.예상총비용)),
            Sum(proposals.Select(x => x.예상운임)),
            Sum(proposals.Select(x => x.예상순이익)),
            strategy.이름,
            strategy.가용기사운송의뢰비율)
        {
            판정근거 = input.판정근거
        };
    }

    private static decimal? Sum(IEnumerable<decimal?> values)
    {
        decimal sum = 0m;
        var hasValue = false;
        foreach (var value in values)
        {
            if (!value.HasValue)
            {
                continue;
            }

            hasValue = true;
            sum += value.Value;
        }

        return hasValue ? sum : null;
    }

}
