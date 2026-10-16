using Microsoft.EntityFrameworkCore;
using 살뜰.Data;
using 살뜰.Services.Storage.Local;
using 살뜰.도메인.공통;
using 살뜰.도메인.운송;
using 살뜰.도메인.차량;
using 살뜰.도메인.화물;
using 살뜰.도메인.화주;

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
    decimal? 최대시간위반분);

public interface I화물배차수락적격성Service
{
    Task<화물배차수락적격성결과> 평가Async(
        string 기사Id,
        화주운송의뢰 후보의뢰,
        IReadOnlyCollection<string> 확인한경고코드,
        CancellationToken cancellationToken = default);
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

    public 화물배차수락적격성Service(
        SsalddelContext db,
        IDriverLocationStore 기사위치Store,
        I차량화물적합성Service 차량적합성Service,
        I기사운송일정구성Service 일정구성Service,
        I운송일정삽입평가Service 일정삽입평가Service)
    {
        _db = db;
        _기사위치Store = 기사위치Store;
        _차량적합성Service = 차량적합성Service;
        _일정구성Service = 일정구성Service;
        _일정삽입평가Service = 일정삽입평가Service;
    }

    public async Task<화물배차수락적격성결과> 평가Async(
        string 기사Id,
        화주운송의뢰 후보의뢰,
        IReadOnlyCollection<string> 확인한경고코드,
        CancellationToken cancellationToken = default)
    {
        var activeTransports = await _db.운송원장
            .AsNoTracking()
            .Where(x => x.기사_운송자 == 기사Id
                        && x.배차업무유형 == 상태값.배차업무유형.용달운송
                        && x.상태 != "인수완료"
                        && x.의뢰Id != 후보의뢰.의뢰Id)
            .ToListAsync(cancellationToken);

        var driver = await _db.용달기사
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.기사Id == 기사Id, cancellationToken);
        var vehicle = string.IsNullOrWhiteSpace(driver?.차량)
            ? null
            : await _db.차량제원.AsNoTracking().FirstOrDefaultAsync(
                x => x.차량코드 == driver.차량 || x.차량명 == driver.차량,
                cancellationToken);
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

        if (activeTransports.Count > 0)
        {
            if (IsSingleOnly(후보의뢰, candidateCargo))
            {
                return Block(화물배차수락오류코드.차량적합실패, "단독 운송 또는 혼적 금지 화물은 진행 중 운송과 함께 수락할 수 없습니다.");
            }

            if (IsSensitive(후보의뢰))
            {
                return Block(화물배차수락오류코드.차량적합실패, "파손 주의·냉장·냉동 화물은 현재 진행 중 운송과의 호환 근거가 없어 추가 수락할 수 없습니다.");
            }

            var activeIds = activeTransports.Select(x => x.운송번호).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToArray();
            var activeRequests = await _db.화주운송의뢰.AsNoTracking()
                .Where(x => Enumerable.Contains(activeIds, x.의뢰Id))
                .ToDictionaryAsync(x => x.의뢰Id, StringComparer.Ordinal, cancellationToken);
            var activeCargo = await _db.화물요구조건.AsNoTracking()
                .Where(x => Enumerable.Contains(activeIds, x.의뢰Id))
                .ToDictionaryAsync(x => x.의뢰Id, StringComparer.Ordinal, cancellationToken);

            if (activeTransports.Any(x => activeRequests.TryGetValue(x.운송번호, out var request)
                                          && IsSingleOnly(request, activeCargo.GetValueOrDefault(x.운송번호))))
            {
                return Block(화물배차수락오류코드.차량적합실패, "현재 진행 중인 단독 운송 또는 혼적 금지 화물이 있어 추가 화물을 수락할 수 없습니다.");
            }
            if (activeRequests.Values.Any(IsSensitive))
            {
                return Block(화물배차수락오류코드.차량적합실패, "현재 진행 중인 민감 화물과 새 화물의 혼적 호환 근거가 없습니다.");
            }

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
                        후보의뢰, candidateCargo, x.경로순서)
                })
                .FirstOrDefault(x => x.Capacity.수락가능);
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

            return new(true, null, null, warningCodes, warningMessages, acceptedAttempt.경로순서, acceptedAttempt.총소요시간분, acceptedAttempt.최대시간위반분);
        }

        if (warningCodes.Except(확인한경고코드, StringComparer.Ordinal).Any())
        {
            return Block(
                화물배차수락오류코드.경고확인필요,
                "수락 전에 차량·화물 주의사항을 명시적으로 확인해야 합니다.",
                warningCodes: warningCodes,
                warningMessages: warningMessages);
        }

        return new(true, null, null, warningCodes, warningMessages, [], null, null);
    }

    private static 화물배차수락적격성결과 EvaluateSegmentCapacity(
        차량제원 vehicle,
        IReadOnlyList<운송원장> activeTransports,
        IReadOnlyDictionary<string, 화주운송의뢰> activeRequests,
        IReadOnlyDictionary<string, 화물요구조건> activeCargo,
        화주운송의뢰 candidate,
        화물요구조건? candidateCargo,
        IReadOnlyList<string> routeOrder)
    {
        var requirements = new Dictionary<string, CapacityRequirement>(StringComparer.Ordinal);
        foreach (var transport in activeTransports)
        {
            if (!activeRequests.TryGetValue(transport.운송번호, out var request)
                || !TryCreateRequirement(request, activeCargo.GetValueOrDefault(transport.운송번호), out var requirement))
            {
                return Block(화물배차수락오류코드.일정근거부족, "진행 중 화물의 중량·부피·팔레트 근거가 없어 구간별 적재 여유를 검증할 수 없습니다.");
            }
            requirements[transport.운송번호] = requirement;
        }
        if (!TryCreateRequirement(candidate, candidateCargo, out var candidateRequirement))
        {
            return Block(화물배차수락오류코드.일정근거부족, "새 화물의 중량·부피·팔레트 근거가 없어 구간별 적재 여유를 검증할 수 없습니다.");
        }
        requirements[candidate.의뢰Id] = candidateRequirement;

        var loaded = new HashSet<string>(activeTransports
            .Where(x => 상차완료상태목록.Contains(x.상태, StringComparer.Ordinal))
            .Select(x => x.운송번호), StringComparer.Ordinal);

        foreach (var step in routeOrder)
        {
            var id = requirements.Keys.FirstOrDefault(x => step.EndsWith(x, StringComparison.Ordinal));
            if (id is null) continue;
            if (step.Contains(" 상차 ", StringComparison.Ordinal)) loaded.Add(id);
            if (!Fits(vehicle, loaded.Select(x => requirements[x])))
            {
                return Block(화물배차수락오류코드.차량적합실패, "권장 경로의 일부 구간에서 차량 적재 중량·부피 또는 팔레트 한도를 초과합니다.");
            }
            if (step.Contains(" 하차 ", StringComparison.Ordinal)) loaded.Remove(id);
        }

        return new(true, null, null, [], [], routeOrder, null, null);
    }

    private static bool Fits(차량제원 vehicle, IEnumerable<CapacityRequirement> requirements)
    {
        var list = requirements.ToArray();
        if (list.Sum(x => x.WeightKg) > vehicle.최대적재중량Kg) return false;
        if (vehicle.권장최대CBM.HasValue && list.Sum(x => x.VolumeCbm) > vehicle.권장최대CBM.Value) return false;
        if (vehicle.팔레트적재개수.HasValue && list.Sum(x => x.Pallets) > vehicle.팔레트적재개수.Value) return false;
        return true;
    }

    private static bool TryCreateRequirement(화주운송의뢰 request, 화물요구조건? cargo, out CapacityRequirement requirement)
    {
        var weight = cargo?.화물무게Kg ?? request.화물중량Kg;
        var volume = request.화물부피Cbm;
        var pallets = cargo?.팔레트개수 ?? request.화물팔레트개수;
        if (!weight.HasValue || !volume.HasValue || !pallets.HasValue)
        {
            requirement = default;
            return false;
        }
        requirement = new CapacityRequirement(weight.Value, volume.Value, pallets.Value);
        return true;
    }

    private static bool HasRouteAndWindowEvidence(화주운송의뢰 request)
        => request.픽업_위도.HasValue
           && request.픽업_경도.HasValue
           && request.하차_위도.HasValue
           && request.하차_경도.HasValue
           && request.픽업_시간창_종료일시 != default
           && request.하차_시간창_종료일시.HasValue;

    private static bool IsSingleOnly(화주운송의뢰 request, 화물요구조건? cargo)
    {
        if (cargo is { 독차필수: true } || cargo is { 혼적허용: false }) return true;
        var text = string.Join(' ', new[] { request.운송방식, request.서비스레벨, request.요청사항 }.Where(x => !string.IsNullOrWhiteSpace(x))!);
        return text.Contains("단독", StringComparison.OrdinalIgnoreCase)
               || text.Contains("혼적불가", StringComparison.OrdinalIgnoreCase)
               || text.Contains("경유불가", StringComparison.OrdinalIgnoreCase)
               || text.Contains("묶음불가", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSensitive(화주운송의뢰 request)
        => request.화물파손주의여부
           || (!string.IsNullOrWhiteSpace(request.화물온도조건)
               && !string.Equals(request.화물온도조건, "상온", StringComparison.OrdinalIgnoreCase));

    private static 화물배차수락적격성결과 Block(
        string code,
        string message,
        IReadOnlyList<string>? route = null,
        decimal? totalMinutes = null,
        decimal? maxViolation = null,
        IReadOnlyList<string>? warningCodes = null,
        IReadOnlyList<string>? warningMessages = null)
        => new(false, code, message, warningCodes ?? [], warningMessages ?? [], route ?? [], totalMinutes, maxViolation);

    private readonly record struct CapacityRequirement(decimal WeightKg, decimal VolumeCbm, int Pallets);
}
