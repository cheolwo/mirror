using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using 살뜰.도메인.공통;
using 살뜰.도메인.배차;
using 살뜰.도메인.기사;
using 살뜰.도메인.차량;
using 살뜰.도메인.화물;
using 살뜰.도메인.화주;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.Storage.Local;

namespace 살뜰.Services.Dispatch.Coordination;

public interface I국내화물배차조율입력Factory
{
    Task<국내화물배차조율입력> 생성Async(
        국내화물배차조율입력요청 request,
        CancellationToken cancellationToken = default);
}

public sealed partial class 국내화물배차조율입력Factory : I국내화물배차조율입력Factory
{
    private readonly SsalddelContext _db;
    private readonly I국내화물운송기사상태Store _기사상태Store;
    private readonly IDriverRejectedRequestStore _거절Store;
    private readonly I배차추천경로Service _경로Service;
    private readonly I차량화물적합성Service _적합성Service;
    private readonly I배차추천판정Service _판정Service;
    private readonly I배차추천평가Service _평가Service;
    private readonly I기사운송일정구성Service _기사운송일정구성Service;
    private readonly I운송일정삽입평가Service _운송일정삽입평가Service;
    private readonly I운송의뢰수익묶음Service _수익묶음Service;
    private readonly 배차큐정책Options _options;
    private readonly 국내화물배차AI정책Options _aiPolicyOptions;
    private readonly I기사권역복귀Service _권역복귀Service;
    private readonly Dictionary<string, 기사권역복귀상태> _권역복귀상태Cache = new(StringComparer.Ordinal);
    private readonly Dictionary<string, bool> _다른업무Cache = new(StringComparer.Ordinal);

    public 국내화물배차조율입력Factory(
        SsalddelContext db,
        I국내화물운송기사상태Store 기사상태Store,
        IDriverRejectedRequestStore 거절Store,
        I배차추천경로Service 경로Service,
        I차량화물적합성Service 적합성Service,
        I배차추천판정Service 판정Service,
        I배차추천평가Service 평가Service,
        I기사운송일정구성Service 기사운송일정구성Service,
        I운송일정삽입평가Service 운송일정삽입평가Service,
        I운송의뢰수익묶음Service 수익묶음Service,
        IOptions<배차큐정책Options> options,
        IOptions<국내화물배차AI정책Options> aiPolicyOptions,
        I기사권역복귀Service 권역복귀Service)
    {
        _db = db;
        _기사상태Store = 기사상태Store;
        _거절Store = 거절Store;
        _경로Service = 경로Service;
        _적합성Service = 적합성Service;
        _판정Service = 판정Service;
        _평가Service = 평가Service;
        _기사운송일정구성Service = 기사운송일정구성Service;
        _운송일정삽입평가Service = 운송일정삽입평가Service;
        _수익묶음Service = 수익묶음Service;
        _options = options.Value;
        _aiPolicyOptions = aiPolicyOptions.Value;
        _권역복귀Service = 권역복귀Service;
    }

    public async Task<국내화물배차조율입력> 생성Async(
        국내화물배차조율입력요청 request,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        _권역복귀상태Cache.Clear();
        _다른업무Cache.Clear();
        var queues = await LoadQueuesAsync(request, now, cancellationToken);
        var requestMap = await LoadRequestMapAsync(queues.Select(x => x.의뢰Id), cancellationToken);
        var cargoMap = await LoadCargoMapAsync(queues.Select(x => x.의뢰Id), cancellationToken);
        var driverStates = await LoadDriverStatesAsync(request, cancellationToken);
        var driverMap = await LoadDriverMapAsync(driverStates, request, cancellationToken);
        var currentShiftMap = await LoadCurrentShiftMapAsync(driverMap.Keys, now, cancellationToken);
        var vehicleSpecMap = await LoadVehicleSpecMapAsync(driverMap.Values, cancellationToken);
        var acceptedTransportCounts = await LoadAcceptedTransportCountsAsync(driverMap.Keys, cancellationToken);
        var candidateQueues = FilterCandidateQueues(queues, requestMap, now);
        var candidateDriverStates = FilterCandidateDriverStates(
            driverStates,
            driverMap);

        var requestInputs = candidateQueues
            .Where(x => requestMap.ContainsKey(x.의뢰Id))
            .Select(x => ToRequestInput(x, requestMap[x.의뢰Id]))
            .ToArray();
        var driverInputs = candidateDriverStates
            .Where(x => driverMap.ContainsKey(x.DriverId))
            .Select(x => ToDriverInput(x, driverMap[x.DriverId], GetAcceptedTransportCount(acceptedTransportCounts, x.DriverId)))
            .ToArray();

        var evaluations = await BuildEvaluationsAsync(
            candidateQueues,
            requestMap,
            cargoMap,
            candidateDriverStates,
            driverMap,
            currentShiftMap,
            vehicleSpecMap,
            acceptedTransportCounts,
            now,
            cancellationToken);
        var revenueBundles = BuildRevenueBundles(requestInputs, evaluations);

        return new 국내화물배차조율입력(
            now,
            Math.Max(1, request.기사당최대추천건수),
            requestInputs,
            driverInputs,
            evaluations,
            revenueBundles,
            new 국내화물기사배정AI정책(
                request.목표기사건당지급액 ?? _aiPolicyOptions.목표기사건당지급액,
                request.기사목표지급액미달패널티배수 ?? _aiPolicyOptions.기사목표지급액미달패널티배수,
                request.기사목표지급액초과패널티배수 ?? _aiPolicyOptions.기사목표지급액초과패널티배수));
    }

    private IReadOnlyList<운송의뢰수익묶음후보> BuildRevenueBundles(
        IReadOnlyList<운송의뢰조율입력> requestInputs,
        IReadOnlyList<운송의뢰기사조합평가> evaluations)
    {
        if (requestInputs.Count == 0)
        {
            return [];
        }

        var lowestCostByRequest = evaluations
            .Where(x => x.추천가능여부)
            .Select(x => new
            {
                x.의뢰Id,
                Cost = x.예상총비용 ?? EstimateCost(x.총예상거리Km, x.예상톨비)
            })
            .Where(x => x.Cost.HasValue)
            .GroupBy(x => x.의뢰Id, StringComparer.Ordinal)
            .ToDictionary(
                x => x.Key,
                x => x.Min(v => v.Cost!.Value),
                StringComparer.Ordinal);

        var targets = requestInputs
            .Select(x => new 운송의뢰수익묶음대상(
                x.의뢰Id,
                x.배달권키,
                x.최종운임,
                lowestCostByRequest.TryGetValue(x.의뢰Id, out var cost) ? cost : null,
                x.상차좌표,
                x.하차좌표,
                x.상차시간창종료Utc,
                멀티배차허용: true))
            .ToArray();

        return _수익묶음Service.묶음생성(new 운송의뢰수익묶음요청(
            targets,
            최대묶음크기: Math.Max(1, _aiPolicyOptions.수익묶음최대묶음크기),
            단건후보포함: true,
            최대묶음수: Math.Max(_aiPolicyOptions.수익묶음최대묶음수최소값, requestInputs.Count * 2),
            최대조합탐색크기: Math.Max(1, _aiPolicyOptions.수익묶음최대조합탐색크기),
            거리원가기준Km당: _aiPolicyOptions.수익묶음거리원가기준Km당,
            묶음최소예상순이익: _aiPolicyOptions.수익묶음최소예상순이익,
            목표건당플랫폼순이익: _aiPolicyOptions.목표건당플랫폼순이익,
            목표건당플랫폼순이익미달차단: _aiPolicyOptions.목표건당플랫폼순이익미달차단,
            목표수익미달패널티배수: _aiPolicyOptions.목표수익미달패널티배수,
            목표수익회귀보너스배수: _aiPolicyOptions.목표수익회귀보너스배수,
            목표수익초과보너스배수: _aiPolicyOptions.목표수익초과보너스배수,
            목표수익초과보너스상한: _aiPolicyOptions.목표수익초과보너스상한,
            멀티묶음기본보너스: _aiPolicyOptions.멀티묶음기본보너스,
            추가묶음건당보너스: _aiPolicyOptions.추가묶음건당보너스,
            멀티묶음원가보정비율: _aiPolicyOptions.멀티묶음원가보정비율,
            묶음추가건당원가보정감소폭: _aiPolicyOptions.묶음추가건당원가보정감소폭,
            멀티묶음최소원가보정비율: _aiPolicyOptions.멀티묶음최소원가보정비율,
            같은배달권보너스: _aiPolicyOptions.같은배달권보너스,
            인접배달권보너스: _aiPolicyOptions.인접배달권보너스,
            외부배달권패널티: _aiPolicyOptions.외부배달권패널티,
            상차지근접권장Km: _aiPolicyOptions.상차지근접권장Km,
            상차지근접보너스: _aiPolicyOptions.상차지근접보너스,
            상차지분산패널티Km당: _aiPolicyOptions.상차지분산패널티Km당,
            하차지근접권장Km: _aiPolicyOptions.하차지근접권장Km,
            하차지근접보너스: _aiPolicyOptions.하차지근접보너스,
            하차지분산패널티Km당: _aiPolicyOptions.하차지분산패널티Km당,
            상차시간창권장차이분: _aiPolicyOptions.상차시간창권장차이분,
            상차시간창근접보너스: _aiPolicyOptions.상차시간창근접보너스,
            상차시간창차이패널티분당: _aiPolicyOptions.상차시간창차이패널티분당));
    }
}
