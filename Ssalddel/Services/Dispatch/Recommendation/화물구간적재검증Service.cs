using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Services.LogisticsProcessing.VehicleLoading;
using 살뜰.도메인.차량;
using 살뜰.도메인.화물;
using 살뜰.도메인.화주;

namespace 살뜰.Services.Dispatch.Recommendation;

public sealed record 화물구간적재작업(화주운송의뢰 의뢰, 화물요구조건? 요구조건 = null, bool 이미상차됨 = false);
public sealed record 화물구간적재근거(int 순서, string 의뢰Id, string 단계유형,
    IReadOnlyList<string> 적재의뢰Ids, decimal 총중량Kg, decimal 총부피Cbm, int 팔레트개수,
    decimal 비적층바닥면적M2, IReadOnlyList<string> 입력지문);
public sealed record 화물구간적재검증결과(bool 확인됨, bool 적합여부, string 사유Code,
    IReadOnlyList<string> 사유, IReadOnlyList<화물구간적재근거> 구간목록);

/// <summary>기존 수락 직전 적재 검사의 순수 공통 구현. 경로를 생성하거나 배차를 확정하지 않는다.</summary>
public sealed class 화물구간적재검증Service(차량적재추천Engine engine)
{
    /// <summary>기존 수락의 혼적 정책이다. 물리 적재 가능 여부나 단독 운송 적합성과는 별개다.</summary>
    public static IReadOnlyList<string> 혼적차단사유(IReadOnlyList<화물구간적재작업> jobs)
    {
        if (jobs.Count <= 1) return [];
        var reasons = new List<string>();
        foreach (var job in jobs)
        {
            var request = job.의뢰;
            var cargo = job.요구조건;
            var text = string.Join(' ', new[] { request.운송방식, request.서비스레벨, request.요청사항 }
                .Where(x => !string.IsNullOrWhiteSpace(x))!);
            if (cargo is { 독차필수: true } or { 혼적허용: false }
                || new[] { "단독", "독차", "혼적불가", "경유불가", "묶음불가" }.Any(x => text.Contains(x, StringComparison.OrdinalIgnoreCase)))
                reasons.Add("단독 운송 또는 혼적 금지 화물은 다른 운송과 조합할 수 없습니다.");
            var temperature = 차량화물적합성Service.NormalizeLoadingInput(request, cargo).입력.온도조건;
            if (request.화물파손주의여부 || !string.IsNullOrWhiteSpace(temperature) && !string.Equals(temperature.Trim(), "상온", StringComparison.OrdinalIgnoreCase))
                reasons.Add("파손 주의·냉장·냉동 등 민감 화물의 혼적 호환 근거가 없습니다.");
        }
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    public 화물구간적재검증결과 평가(차량제원? vehicle, IReadOnlyList<화물구간적재작업> jobs,
        IReadOnlyList<기사운송일정항목> typedStops)
    {
        ArgumentNullException.ThrowIfNull(jobs);
        ArgumentNullException.ThrowIfNull(typedStops);
        var segments = new List<화물구간적재근거>();
        화물구간적재검증결과 Fail(bool known, string code, params string[] reasons) => new(known, false, code, reasons, segments);
        if (vehicle is null) return Fail(false, "FreightSegment.VehicleMissing", "구간 적재를 검증할 차량 제원이 없습니다.");
        if (jobs.Any(x => x.의뢰 is null || string.IsNullOrWhiteSpace(x.의뢰.의뢰Id))
            || jobs.Select(x => x.의뢰.의뢰Id).Distinct(StringComparer.Ordinal).Count() != jobs.Count)
            return Fail(false, "FreightSegment.InvalidJobs", "화물 의뢰 식별값이 없거나 중복됐습니다.");
        var map = jobs.ToDictionary(x => x.의뢰.의뢰Id, StringComparer.Ordinal);
        var normalized = jobs.ToDictionary(x => x.의뢰.의뢰Id,
            x => 차량화물적합성Service.NormalizeLoadingInput(x.의뢰, x.요구조건), StringComparer.Ordinal);
        if (normalized.Values.Any(x => !x.유효여부))
            return Fail(false, "FreightSegment.InvalidInput", normalized.Values.SelectMany(x => x.오류).ToArray());
        if (normalized.Values.Any(x => x.입력.총중량Kg is null || x.유효부피Cbm is null || x.입력.팔레트개수 is null
            || !x.적층가능여부 && x.비적층바닥면적M2 is null))
            return Fail(false, "FreightSegment.EvidenceMissing", "각 화물의 중량·부피·팔레트 및 비적층 면적 근거가 필요합니다.");
        // 합산 요약에서 온도 문구·장비·포장별 조건이 소실되지 않도록 개별 요구도 같은 엔진으로 확인한다.
        foreach (var job in jobs)
        {
            var check = engine.평가(vehicle, 차량화물적합성Service.BuildLoadingRequirement(job.의뢰, job.요구조건, normalized[job.의뢰.의뢰Id]));
            if (!check.단일운송가능여부)
            {
                var reasons = check.하드부적합사유.Concat(check.단일운송불가사유).ToArray();
                var known = !reasons.Any(x => x.Contains("검증 불가", StringComparison.Ordinal));
                return Fail(known, known ? "FreightSegment.IndividualIncompatible" : "FreightSegment.EvidenceMissing", reasons);
            }
        }
        if (typedStops.Any(x => !map.ContainsKey(x.의뢰Id) || x.단계유형 is not ("pickup" or "dropoff"))
            || typedStops.Select(x => x.순서).Distinct().Count() != typedStops.Count)
            return Fail(false, "FreightSegment.InvalidStops", "알 수 없는 화물·단계 또는 중복 순서가 있습니다.");
        foreach (var job in jobs)
        {
            var stops = typedStops.Where(x => x.의뢰Id == job.의뢰.의뢰Id).ToArray();
            if (stops.Count(x => x.단계유형 == "pickup") != (job.이미상차됨 ? 0 : 1)
                || stops.Count(x => x.단계유형 == "dropoff") != 1)
                return Fail(false, "FreightSegment.IncompleteStops", "각 화물의 남은 상차·하차 절차가 정확히 한 번씩 필요합니다.");
        }
        var loaded = jobs.Where(x => x.이미상차됨).Select(x => x.의뢰.의뢰Id).ToHashSet(StringComparer.Ordinal);
        화물구간적재검증결과? Check(int sequence, string id, string stage)
        {
            var ids = loaded.OrderBy(x => x, StringComparer.Ordinal).ToArray();
            var inputs = ids.Select(x => normalized[x]).ToArray();
            var requirements = ids.Select(x => 차량화물적합성Service.BuildLoadingRequirement(map[x].의뢰, map[x].요구조건, normalized[x])).ToArray();
            var pallets = inputs.Sum(x => (long)x.입력.팔레트개수!.Value);
            if (pallets > int.MaxValue) return Fail(true, "FreightSegment.CapacityExceeded", "합산 팔레트 수가 표현 가능한 차량 상한을 초과합니다.");
            var total = new 차량적재추천요구사항
            {
                총중량Kg = inputs.Sum(x => x.입력.총중량Kg!.Value), 총부피Cbm = inputs.Sum(x => x.유효부피Cbm!.Value),
                총팔레트개수 = (int)pallets,
                적층불가바닥면적M2 = inputs.Sum(x => x.비적층바닥면적M2 ?? 0m),
                비눈보호필요 = requirements.Any(x => x.비눈보호필요), 리프트필요 = requirements.Any(x => x.리프트필요),
                측면상하차필요 = requirements.Any(x => x.측면상하차필요), 장재물 = requirements.Any(x => x.장재물),
                온도조건 = requirements.Any(x => x.온도조건 == "냉동") ? "냉동" : requirements.Any(x => x.온도조건 == "냉장") ? "냉장" : "상온",
                포장목록 = requirements.SelectMany(x => x.포장목록).ToArray(), 분할운송허용 = false
            };
            segments.Add(new(sequence, id, stage, ids, total.총중량Kg.Value, total.총부피Cbm.Value,
                total.총팔레트개수.Value, total.적층불가바닥면적M2.Value, inputs.Select(x => x.입력지문).ToArray()));
            var check = engine.평가(vehicle, total);
            var reasons = check.하드부적합사유.Concat(check.단일운송불가사유).ToArray();
            var known = !reasons.Any(x => x.Contains("검증 불가", StringComparison.Ordinal));
            return check.단일운송가능여부 ? null : Fail(known,
                known ? "FreightSegment.CapacityExceeded" : "FreightSegment.EvidenceMissing", reasons);
        }
        if (Check(-1, "", "initial") is { } initialFailure) return initialFailure;
        foreach (var stop in typedStops.OrderBy(x => x.순서))
        {
            if (stop.단계유형 == "pickup")
            {
                if (!loaded.Add(stop.의뢰Id)) return Fail(false, "FreightSegment.DuplicatePickup", "이미 적재한 화물을 다시 상차할 수 없습니다.");
                if (Check(stop.순서, stop.의뢰Id, stop.단계유형) is { } failure) return failure;
            }
            else
            {
                if (!loaded.Contains(stop.의뢰Id)) return Fail(false, "FreightSegment.DropoffBeforePickup", "화물을 상차하기 전에 하차할 수 없습니다.");
                if (Check(stop.순서, stop.의뢰Id, stop.단계유형) is { } failure) return failure;
                loaded.Remove(stop.의뢰Id);
            }
        }
        return loaded.Count == 0 ? new(true, true, "FreightSegment.Verified", [], segments)
            : Fail(false, "FreightSegment.Unfinished", "하차되지 않은 화물이 남아 있습니다.");
    }
}
