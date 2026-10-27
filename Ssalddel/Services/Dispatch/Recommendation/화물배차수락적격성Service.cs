using Microsoft.EntityFrameworkCore;
using 살뜰.Data;
using 살뜰.Services.Storage.Local;
using 살뜰.도메인.공통;
using 살뜰.도메인.운송;
using 살뜰.도메인.차량;
using 살뜰.도메인.화물;
using 살뜰.도메인.화주;
using System.Text.Json;
using 살뜰.Services.Dispatch.Coordination;

namespace 살뜰.Services.Dispatch.Recommendation;

public static class 화물배차수락오류코드
{
    public const string 차량적합실패 = "FreightVehicleCompatibilityFailed";
    public const string 일정실패 = "FreightScheduleInfeasible";
    public const string 일정근거부족 = "FreightScheduleEvidenceMissing";
    public const string 추천판본불일치 = "FreightRecommendationStale";
    public const string 경고확인필요 = "FreightWarningAcknowledgementRequired";
}

public static class 화물배차수락경고코드
{
    public const string 차량주의사항 = "FreightVehicleAdvisory";
}

public sealed record 화물배차수락적격성결과(
    bool 수락가능,
    string? 오류코드,
    string? 오류메시지,
    IReadOnlyList<string> 경고코드,
    IReadOnlyList<string> 경고메시지,
    IReadOnlyList<string> 권장경로순서,
    decimal? 총소요시간분,
    decimal? 최대시간위반분)
{
    public IReadOnlyList<string> 검증한계 { get; init; } = [];
}

public interface I화물배차수락적격성Service
{
    Task<화물배차수락적격성결과> 평가Async(
        string 기사Id,
        화주운송의뢰 후보의뢰,
        IReadOnlyCollection<string> 확인한경고코드,
        CancellationToken cancellationToken = default);

    Task<화물배차수락적격성결과> 평가Async(
        string 기사Id,
        화주운송의뢰 후보의뢰,
        IReadOnlyCollection<string> 확인한경고코드,
        IReadOnlyDictionary<string, 화물상하차작업시간> 명시작업시간,
        CancellationToken cancellationToken)
        => Task.FromResult(new 화물배차수락적격성결과(false, 화물배차수락오류코드.일정근거부족,
            "명시한 상하차 작업시간의 검증을 지원하지 않는 일정 서비스입니다.", [], [], [], null, null));
}

/// <summary>
/// 추천 화면의 계산값을 신뢰하지 않고 수락 직전에 서버 원장을 다시 읽어
/// 차량·혼적·구간 적재량·전체 시간창을 보수적으로 재검증한다.
/// </summary>
public sealed class 화물배차수락적격성Service : I화물배차수락적격성Service
{
    private static readonly string[] 상차완료상태목록 = ["상차완료", "하차지도착", "운송중"];

    private readonly SsalddelContext _db;
    private readonly IDriverLocationStore _기사위치Store;
    private readonly I차량화물적합성Service _차량적합성Service;
    private readonly I기사운송일정구성Service _일정구성Service;
    private readonly I운송일정삽입평가Service _일정삽입평가Service;
    private readonly I기사실차적재근거조회Service _실차조회;
    private readonly 화물구간적재검증Service _구간검증;

    public 화물배차수락적격성Service(
        SsalddelContext db,
        IDriverLocationStore 기사위치Store,
        I차량화물적합성Service 차량적합성Service,
        I기사운송일정구성Service 일정구성Service,
        I운송일정삽입평가Service 일정삽입평가Service,
        I기사실차적재근거조회Service? 실차조회 = null,
        화물구간적재검증Service? 구간검증 = null)
    {
        _db = db;
        _기사위치Store = 기사위치Store;
        _차량적합성Service = 차량적합성Service;
        _일정구성Service = 일정구성Service;
        _일정삽입평가Service = 일정삽입평가Service;
        _실차조회 = 실차조회 ?? new 기사실차적재근거조회Service(db);
        _구간검증 = 구간검증 ?? new 화물구간적재검증Service(new Ssalddel.Services.LogisticsProcessing.VehicleLoading.차량적재추천Engine());
    }

    public Task<화물배차수락적격성결과> 평가Async(
        string 기사Id,
        화주운송의뢰 후보의뢰,
        IReadOnlyCollection<string> 확인한경고코드,
        CancellationToken cancellationToken = default)
        => 평가CoreAsync(기사Id, 후보의뢰, 확인한경고코드, null, cancellationToken);

    public Task<화물배차수락적격성결과> 평가Async(
        string 기사Id,
        화주운송의뢰 후보의뢰,
        IReadOnlyCollection<string> 확인한경고코드,
        IReadOnlyDictionary<string, 화물상하차작업시간> 명시작업시간,
        CancellationToken cancellationToken)
        => 명시작업시간 is null
            ? Task.FromResult(Block(화물배차수락오류코드.일정근거부족, "명시한 상하차 작업시간 자료가 없습니다."))
            : 평가CoreAsync(기사Id, 후보의뢰, 확인한경고코드, 명시작업시간, cancellationToken);

    private async Task<화물배차수락적격성결과> 평가CoreAsync(
        string 기사Id,
        화주운송의뢰 후보의뢰,
        IReadOnlyCollection<string> 확인한경고코드,
        IReadOnlyDictionary<string, 화물상하차작업시간>? 명시작업시간,
        CancellationToken cancellationToken)
    {
        var activeTransports = await _db.운송원장
            .AsNoTracking()
            .Where(x => (x.기사_운송자 == 기사Id || x.확정기사Id == 기사Id)
                        && x.배차큐단계 == 상태값.배차큐단계.확정
                        && x.상태 != "인수완료"
                        && x.의뢰Id != 후보의뢰.의뢰Id)
            .ToListAsync(cancellationToken);
        if (await 화물후속픽업기준Service.반납책임있음Async(_db, 기사Id, cancellationToken))
            return Block("DriverWork.ReturnCustodyPending", "기존 물품의 반환 책임을 마친 뒤 새 화물을 수락할 수 있습니다.");
        if (activeTransports.Any(x => string.IsNullOrWhiteSpace(x.의뢰Id)
                || x.배차업무유형 != 상태값.배차업무유형.용달운송
                || x.상태 is "취소" or "반환대기"
                || !string.IsNullOrWhiteSpace(x.확정기사Id) && x.확정기사Id != 기사Id
                || !string.IsNullOrWhiteSpace(x.기사_운송자) && x.기사_운송자 != 기사Id)
            || activeTransports.Select(x => x.의뢰Id).Distinct(StringComparer.Ordinal).Count() != activeTransports.Count)
            return Block(화물배차수락오류코드.일정근거부족, "진행 중 업무의 의뢰·기사·수행 책임을 명확히 확인해야 합니다.");

        var actualVehicle = await _실차조회.조회Async(기사Id, cancellationToken);
        var vehicle = actualVehicle.유효제원;
        var candidateCargo = await _db.화물요구조건
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.의뢰Id == 후보의뢰.의뢰Id, cancellationToken);

        if (activeTransports.Count > 0 && vehicle is null)
        {
            return Block(
                화물배차수락오류코드.일정근거부족,
                "이미 진행 중인 운송이 있어 추가 수락 전 차량 제원과 적재 여유를 확인해야 합니다.");
        }

        var vehicleResult = _차량적합성Service.판정(vehicle, 후보의뢰, candidateCargo);
        if (!vehicleResult.적합여부)
        {
            return Block(
                화물배차수락오류코드.차량적합실패,
                string.Join(" ", vehicleResult.부적합사유));
        }

        var warningCodes = new List<string>();
        var warningMessages = new List<string>();
        if (vehicle is null)
        {
            warningCodes.Add(화물배차수락경고코드.차량주의사항);
            warningMessages.Add("차량 제원이 등록되지 않아 첫 단독 운송의 적재 조건을 완전히 검증하지 못했습니다.");
        }
        if (vehicle is not null && vehicleResult.경고.Length > 0)
        {
            warningCodes.Add(화물배차수락경고코드.차량주의사항);
            warningMessages.AddRange(vehicleResult.경고);
        }

        {
            var activeIds = activeTransports.Select(x => x.의뢰Id).Distinct(StringComparer.Ordinal).ToArray();
            var activeRequests = await _db.화주운송의뢰.AsNoTracking()
                .Where(x => Enumerable.Contains(activeIds, x.의뢰Id))
                .ToDictionaryAsync(x => x.의뢰Id, StringComparer.Ordinal, cancellationToken);
            var activeCargo = await _db.화물요구조건.AsNoTracking()
                .Where(x => Enumerable.Contains(activeIds, x.의뢰Id))
                .ToDictionaryAsync(x => x.의뢰Id, StringComparer.Ordinal, cancellationToken);

            var mixReasons = 화물구간적재검증Service.혼적차단사유(activeRequests.Values
                .Select(x => new 화물구간적재작업(x, activeCargo.GetValueOrDefault(x.의뢰Id)))
                .Append(new(후보의뢰, candidateCargo)).ToArray());
            if (mixReasons.Count > 0)
                return Block(화물배차수락오류코드.차량적합실패, string.Join(" ", mixReasons));

            if (!_기사위치Store.TryGetLatest(기사Id, out var currentLocation)
                || !HasRouteAndWindowEvidence(후보의뢰)
                || activeRequests.Count != activeIds.Length
                || activeRequests.Values.Any(x => !HasRouteAndWindowEvidence(x)))
            {
                return Block(
                    화물배차수락오류코드.일정근거부족,
                    "진행 중 운송을 포함한 현재 위치·좌표·시간창 근거가 모두 있어야 추가 수락할 수 있습니다.");
            }

            var plan = await _일정구성Service.구성Async(
                기사Id,
                new 배차경로좌표(currentLocation.Latitude, currentLocation.Longitude),
                cancellationToken);
            // 추천이 만들어진 과거 기준시각 대신 현재 수행 시작 시각으로 약속을 다시 확인한다.
            plan = plan with { 기준시각 = DateTime.UtcNow };
            IReadOnlyDictionary<string, 화물상하차작업시간>? handling = 명시작업시간;
            if (handling is null)
            {
                var queue = await _db.운송원장.AsNoTracking().FirstOrDefaultAsync(x => x.의뢰Id == 후보의뢰.의뢰Id, cancellationToken);
                if (!string.IsNullOrWhiteSpace(queue?.추천판정근거Json))
                {
                    try { handling = JsonSerializer.Deserialize<국내화물추천저장근거>(queue.추천판정근거Json)?.수행계획?.상하차작업시간; }
                    catch (JsonException) { return Block(화물배차수락오류코드.추천판본불일치, "추천 수행계획 근거를 읽을 수 없어 다시 추천받아야 합니다."); }
                }
            }
            var handlingSources = activeRequests.Values.Append(후보의뢰).ToArray();
            if (handling is not null || handlingSources.Any(x => x.픽업_작업시간분.HasValue || x.하차_작업시간분.HasValue))
            {
                handling = 국내화물배차조율입력Factory.작업시간입력결합(handlingSources, handling, out var conflicts);
                if (conflicts.Length > 0)
                    return Block(화물배차수락오류코드.추천판본불일치, "저장된 상하차 작업시간과 추천 계획이 다릅니다. 최신 추천을 확인해 주세요.");
            }
            if (handling is not null)
            {
                bool TimeConfirmed(string id, string stage) => handling.TryGetValue(id, out var time) && time is not null
                    && (stage == "pickup" ? time.상차분 : time.하차분) is >= 0m;
                if (!TimeConfirmed(후보의뢰.의뢰Id, "pickup") || !TimeConfirmed(후보의뢰.의뢰Id, "dropoff")
                    || plan.항목목록.Any(x => !TimeConfirmed(x.의뢰Id, x.단계유형)))
                    return Block(화물배차수락오류코드.일정근거부족, "후보와 진행 중 운송의 남은 상하차 작업시간을 모두 확인해야 합니다.");
                plan = plan with
                {
                    상하차작업시간 = handling,
                    항목목록 = plan.항목목록.Select(x => x with { 작업시간분 = handling.GetValueOrDefault(x.의뢰Id) is { } times
                        ? x.단계유형 == "pickup" ? times.상차분 : times.하차분 : null }).ToArray()
                };
            }
            var schedule = await _일정삽입평가Service.평가Async(plan, 후보의뢰, cancellationToken);
            if (!schedule.삽입가능여부 || !schedule.전체완수가능여부 || !schedule.후보포함실제경로확인됨)
            {
                return Block(
                    화물배차수락오류코드.일정실패,
                    schedule.위반사유.Length == 0
                        ? "기존 운송과 새 운송의 시간창을 모두 지킬 수 없습니다."
                        : string.Join(" ", schedule.위반사유),
                    schedule.권장경로순서,
                    schedule.총소요시간분,
                    schedule.최대시간위반분);
            }

            var feasibleCapacityAttempt = schedule.시도목록
                .Where(x => x.전체완수가능여부 && x.전체실제경로확인됨)
                .OrderBy(x => x.총추가지연분 ?? decimal.MaxValue)
                .ThenBy(x => x.총소요시간분 ?? decimal.MaxValue)
                .Select(x => new
                {
                    Attempt = x,
                    Capacity = EvaluateSegmentCapacity(
                        vehicle!, activeTransports, activeRequests, activeCargo,
                        후보의뢰, candidateCargo, x.도착예상목록)
                })
                .FirstOrDefault(x => x.Capacity.적합여부);
            if (feasibleCapacityAttempt is null)
            {
                return Block(
                    화물배차수락오류코드.차량적합실패,
                    "시간창을 지킬 수 있는 경로 중 차량 적재 한도를 지키는 경로가 없습니다.",
                    schedule.권장경로순서,
                    schedule.총소요시간분,
                    schedule.최대시간위반분);
            }

            var acceptedAttempt = feasibleCapacityAttempt.Attempt;

            if (warningCodes.Except(확인한경고코드, StringComparer.Ordinal).Any())
            {
                return Block(
                    화물배차수락오류코드.경고확인필요,
                    "수락 전에 차량·화물 주의사항을 명시적으로 확인해야 합니다.",
                    acceptedAttempt.경로순서,
                    acceptedAttempt.총소요시간분,
                    acceptedAttempt.최대시간위반분,
                    warningCodes,
                    warningMessages);
            }

            return new(true, null, null, warningCodes, warningMessages, acceptedAttempt.경로순서, acceptedAttempt.총소요시간분, acceptedAttempt.최대시간위반분)
            {
                검증한계 = handling is null || !handling.TryGetValue(후보의뢰.의뢰Id, out var times) || !times.확인됨
                    || plan.항목목록.Any(x => x.작업시간분 is null)
                    ? ["상하차 작업시간이 일부 미확인입니다. 구형 일정의 0분 가정은 확인된 작업시간이 아닙니다."] : []
            };
        }

    }

    private 화물구간적재검증결과 EvaluateSegmentCapacity(
        차량제원 vehicle,
        IReadOnlyList<운송원장> activeTransports,
        IReadOnlyDictionary<string, 화주운송의뢰> activeRequests,
        IReadOnlyDictionary<string, 화물요구조건> activeCargo,
        화주운송의뢰 candidate,
        화물요구조건? candidateCargo,
        IReadOnlyList<운송일정도착예상항목> arrivals)
    {
        if (activeTransports.Any(x => !activeRequests.ContainsKey(x.의뢰Id)))
            return new(false, false, "FreightSegment.EvidenceMissing", ["진행 중 운송의 화물 원장을 확인하지 못했습니다."], []);
        var jobs = activeTransports.Select(x => new 화물구간적재작업(activeRequests[x.의뢰Id],
            activeCargo.GetValueOrDefault(x.의뢰Id), 상차완료상태목록.Contains(x.상태, StringComparer.Ordinal)))
            .Append(new(candidate, candidateCargo)).ToArray();
        var typed = arrivals.Select(x => new 기사운송일정항목(x.의뢰Id, x.단계유형, x.주소,
            null, null, x.시간창종료일시, x.순서, null, false, x.의뢰Id == candidate.의뢰Id)).ToArray();
        return _구간검증.평가(vehicle, jobs, typed);
    }

    private static bool HasRouteAndWindowEvidence(화주운송의뢰 request)
        => request.픽업_위도.HasValue
           && request.픽업_경도.HasValue
           && request.하차_위도.HasValue
           && request.하차_경도.HasValue
           && request.픽업_시간창_종료일시 != default
           && request.하차_시간창_종료일시.HasValue;

    private static 화물배차수락적격성결과 Block(
        string code,
        string message,
        IReadOnlyList<string>? route = null,
        decimal? totalMinutes = null,
        decimal? maxViolation = null,
        IReadOnlyList<string>? warningCodes = null,
        IReadOnlyList<string>? warningMessages = null)
        => new(false, code, message, warningCodes ?? [], warningMessages ?? [], route ?? [], totalMinutes, maxViolation);

}
