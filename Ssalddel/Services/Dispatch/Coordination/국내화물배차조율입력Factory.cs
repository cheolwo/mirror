using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ssalddel.Services.Dispatch.Participation;
using Ssalddel.Services.Dispatch.Compliance;
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
    private readonly ISharedDriverWorkAdmissionService? _sharedAdmission;
    private readonly I신규화물업무자격Guard? _신규업무자격;
    private readonly I기사실차적재근거조회Service? _실차제원;
    private readonly I화물배차상태SnapshotService? _상태근거;
    private readonly TimeProvider _clock;
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
        I기사권역복귀Service 권역복귀Service,
        ISharedDriverWorkAdmissionService? sharedAdmission = null,
        I신규화물업무자격Guard? 신규업무자격 = null,
        I기사실차적재근거조회Service? 실차제원 = null,
        I화물배차상태SnapshotService? 상태근거 = null,
        TimeProvider? clock = null)
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
        _sharedAdmission = sharedAdmission;
        _신규업무자격 = 신규업무자격;
        _실차제원 = 실차제원;
        _상태근거 = 상태근거;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<국내화물배차조율입력> 생성Async(
        국내화물배차조율입력요청 request,
        CancellationToken cancellationToken = default)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
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
        candidateQueues = candidateQueues.Where(x => requestMap.TryGetValue(x.의뢰Id, out var cargoRequest)
            && 신규화물업무자격검사.화물정보완료(_신규업무자격, cargoRequest)).ToArray();
        var candidateDriverStates = FilterCandidateDriverStates(
            driverStates,
            driverMap);
        var qualifiedDriverStates = new List<국내화물운송기사상태Snapshot>();
        foreach (var state in candidateDriverStates)
            if ((await 신규화물업무자격검사.평가Async(_신규업무자격, state.DriverId, cancellationToken)).CanReceiveNewWork)
                qualifiedDriverStates.Add(state);
        candidateDriverStates = qualifiedDriverStates.ToArray();

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
        var driverIds = candidateDriverStates.Select(x => x.DriverId).Distinct().ToArray();
        var activeIds = await _db.운송원장.AsNoTracking()
            .Where(x => (Enumerable.Contains(driverIds, x.확정기사Id!) || Enumerable.Contains(driverIds, x.기사_운송자!))
                && x.배차큐단계 == 상태값.배차큐단계.확정 && x.상태 != 상태값.배차상태.인수완료)
            .Select(x => x.의뢰Id).Distinct().ToArrayAsync(cancellationToken);
        var activeRequests = await _db.화주운송의뢰.AsNoTracking().Where(x => Enumerable.Contains(activeIds, x.의뢰Id))
            .ToArrayAsync(cancellationToken);
        var handlingTimes = 작업시간입력결합(requestMap.Values.Concat(activeRequests), request.상하차작업시간,
            out var handlingConflicts);

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
                request.기사목표지급액초과패널티배수 ?? _aiPolicyOptions.기사목표지급액초과패널티배수))
        {
            상하차작업시간 = handlingTimes,
            판정근거 = new()
            {
                기준시각Utc = now, 조회시작시각Utc = now, 조회완료시각Utc = _clock.GetUtcNow().UtcDateTime,
                조회범위 = "Bounded waiting queues and active drivers; sequential SQL and external state reads",
                원자적조회여부 = false,
                입력지문 = evaluations.Count > 0 && evaluations.All(x => x.판정근거?.입력지문 is not null)
                    ? 화물배차상태SnapshotService.지문(evaluations.Select(x => new
                        { x.의뢰Id, x.기사Id, x.판정근거!.입력지문, x.판정근거.순위관측지문 }).ToArray()) : null,
                순위관측지문 = evaluations.Count > 0 && evaluations.All(x => x.판정근거?.순위관측지문 is not null)
                    ? 화물배차상태SnapshotService.지문(evaluations.Select(x => new { x.의뢰Id, x.기사Id, x.판정근거!.순위관측지문 }).ToArray()) : null,
                미확인사유 = evaluations.SelectMany(x => x.판정근거?.미확인사유 ?? ["DispatchEvidence.LegacyInputUnknown"])
                    .Concat(handlingConflicts.Length > 0 ? ["CargoPlan.HandlingInputConflict"] : Array.Empty<string>()).Distinct(StringComparer.Ordinal).ToArray(),
                차단사유 = evaluations.SelectMany(x => x.판정근거?.차단사유 ?? []).Distinct(StringComparer.Ordinal).ToArray()
            }
        };
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

    internal static IReadOnlyDictionary<string, 화물상하차작업시간> 작업시간입력결합(
        IEnumerable<화주운송의뢰> requests,
        IReadOnlyDictionary<string, 화물상하차작업시간>? supplied,
        out string[] conflicts)
    {
        var result = new Dictionary<string, 화물상하차작업시간>(StringComparer.Ordinal);
        var mismatches = new List<string>();
        foreach (var row in requests.GroupBy(x => x.의뢰Id).Select(x => x.First()))
        {
            var stored = Ssalddel.Application.Shipper.Request.화주운송의뢰매퍼.ReadHandlingTime(row);
            var hasStored = stored.상차분.HasValue || stored.하차분.HasValue;
            if (supplied is not null && supplied.TryGetValue(row.의뢰Id, out var explicitTime))
            {
                // 구형 검토 요청의 명시 입력은 유지하되 저장된 입력과 충돌하면 확인된 시간으로 쓰지 않는다.
                if (hasStored && explicitTime != stored) { result[row.의뢰Id] = new(null, null); mismatches.Add(row.의뢰Id); }
                else result[row.의뢰Id] = hasStored ? stored : explicitTime;
            }
            else result[row.의뢰Id] = stored;
        }
        conflicts = mismatches.ToArray();
        return result;
    }
}
