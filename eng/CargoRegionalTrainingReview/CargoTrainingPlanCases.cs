using System.Text.Json;
using Ssalddel.Contracts.Admin.DispatchTraining;
using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Services.LogisticsProcessing.VehicleLoading;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.Storage.Local;
using 살뜰.도메인.기사;
using 살뜰.도메인.차량;
using 살뜰.도메인.화주;

namespace CargoRegionalTrainingReview;

/// <summary>명시적인 가상 시간·차량·화물로 기존 일정/구간 적재 검사기를 실행한다. 운영 상태 조회가 아니다.</summary>
public static class CargoTrainingPlanCases
{
    private static readonly DateTime Clock = new(2026, 10, 19, 0, 0, 0, DateTimeKind.Utc);
    public static IReadOnlyList<string> SourcePaths { get; } = CargoTrainingCases.PolicySourcePaths.Concat(new[]
    {
        "eng/CargoRegionalTrainingReview/CargoTrainingPlanCases.cs",
        "eng/CargoRegionalTrainingReview/CargoTrainingOllama.cs",
        "eng/CargoRegionalTrainingReview/CargoTrainingStore.cs",
        "Ssalddel.Contracts/Admin/DispatchTraining/CargoRegionalTrainingDtos.cs",
        "Ssalddel.Contracts/Shipper/Request/화물적재입력Policy.cs",
        "Ssalddel/Services/Dispatch/Recommendation/화물구간적재검증Service.cs",
        "Ssalddel/Services/Dispatch/Recommendation/차량화물적합성Service.cs",
        "Ssalddel/Services/Dispatch/Recommendation/운송일정삽입평가Service.cs",
        "Ssalddel/Services/Dispatch/Recommendation/운송일정삽입평가Service.Mapping.cs",
        "Ssalddel/Services/Dispatch/Recommendation/운송일정삽입평가Service.Simulation.cs",
        "Ssalddel/Services/Dispatch/Recommendation/운송일정삽입평가Service.Attempts.cs",
        "Ssalddel/Services/Dispatch/Recommendation/배차추천모델.cs",
        "Ssalddel/Services/LogisticsProcessing/VehicleLoading/차량적재추천Engine.cs"
    }).Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray();

    public static List<CargoTrainingCase> Build(IReadOnlyList<CargoTrainingCase> legacy)
    {
        var regions = legacy.Select(x => x.PickupRegion).DistinctBy(x => x.LegalDongCode).ToArray();
        var scenarios = new[]
        {
            (Code: "compatible", Title: "두 주문의 상하차를 함께 수행", Text: "상하차 시간과 구간 적재를 함께 확인합니다. 같은 기사에게 두 주문을 맡길 수 있는지 판단해 주세요."),
            (Code: "overloaded", Title: "각각 가능해도 함께 싣기에는 무거운 주문", Text: "단건은 가능하지만 두 화물이 겹쳐 실리는 구간은 중량 한도를 넘습니다. 단건 대안과 조합을 구분합니다."),
            (Code: "deadline", Title: "작업시간을 포함하면 약속을 넘는 조합", Text: "이동만 계산하지 않습니다. 상하차 작업을 끝내는 시점까지 약속을 지킬 수 있는지 확인합니다."),
            (Code: "missing_handling", Title: "상하차 작업시간을 확인하지 못한 조합", Text: "한 주문의 작업시간이 없습니다. 누락을 0분으로 바꾸지 않고 정보 부족으로 남깁니다."),
            (Code: "missing_all_handling", Title: "작업시간 확인 전에는 배차를 보류", Text: "단건과 조합 모두 작업시간 근거가 없습니다. 모델에 가능 후보가 있는 것처럼 전달하지 않습니다.")
        };
        var result = new List<CargoTrainingCase>();
        foreach (var (code, title, text) in scenarios)
        {
            var index = result.Count;
            var region = regions[index % regions.Length];
            var item = new CargoTrainingCase
            {
                CaseId = "cargo-plan-r76-" + code, FamilyId = "plan_" + code, Split = "review",
                Title = title, Situation = text, TaskType = "CargoPlan", Source = "synthetic",
                PickupRegion = region, DropoffRegion = regions[(index + 1) % regions.Length],
                WeightKg = code == "overloaded" ? 6 : 3, VolumeLiters = 10, PickupDeadlineMinutes = 60,
                Fare = 5000
            };
            var joint = CreatePlan(code, joint: true);
            var single = CreatePlan(code, joint: false);
            item = item with
            {
                Candidates = [Candidate("P1", joint, region, 25), Candidate("P2", single, region, 25)]
            };
            item = item with { PlanSetSha256 = PlanSetFingerprint(item) };
            item = item with { InputSha256 = CargoTrainingCases.InputFingerprint(item) };
            result.Add(item with
            {
                Engine = new CargoTrainingDecision
                {
                    Source = "plan_checks_only", Status = "not_run", InputSha256 = item.InputSha256,
                    ReasonCodes = ["synthetic_plan_checks", "assignment_selection_not_executed"]
                }
            });
        }
        return result;
    }

    public static bool IsSelectable(CargoTrainingCandidate candidate)
        => candidate.Eligible && (candidate.Plan is null || candidate.Plan is
        {
            Verified: true, Feasible: true, Source: "synthetic", TimingEvidence: "synthetic_explicit_schedule_checked",
            LoadingEvidence: "shared_segment_service_checked", TotalMinutes: >= 0, TotalDistanceKm: >= 0
        } plan && plan.PlanSha256 == PlanFingerprint(plan) && plan.Stops.Count > 0 && plan.Segments.Count > 0
          && plan.Stops.All(x => (x.Kind is "pickup" or "dropoff") && x.ArrivalMinutes is >= 0
              && x.HandlingMinutes is >= 0 && x.FinishMinutes is >= 0 && x.DeadlineMinutes is >= 0
              && x.FinishMinutes <= x.DeadlineMinutes));

    public static string PlanFingerprint(CargoTrainingPlan plan) => CargoTrainingStore.Hash(
        JsonSerializer.SerializeToUtf8Bytes(plan with { PlanSha256 = "" }, CargoTrainingStore.Json));

    public static string PlanSetFingerprint(CargoTrainingCase item) => CargoTrainingStore.Hash(
        JsonSerializer.SerializeToUtf8Bytes(item.Candidates.OrderBy(x => x.Id, StringComparer.Ordinal)
            .Select(x => new { x.Id, PlanId = x.Plan?.PlanId, PlanSha256 = x.Plan?.PlanSha256 }), CargoTrainingStore.Json));

    public static void Verify(CargoTrainingCase item)
    {
        if (item.Candidates.All(x => x.Plan is null)) return;
        if (item.Source != "synthetic" || item.PlanSetSha256 != PlanSetFingerprint(item)
            || item.Candidates.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() != item.Candidates.Count
            || item.Candidates.Any(x => x.Plan is null || x.Plan.PlanSha256 != PlanFingerprint(x.Plan)
                || x.Eligible && !IsSelectable(x))) throw CargoTrainingStore.Error("PlanEvidenceChanged");
    }

    private static CargoTrainingCandidate Candidate(string id, CargoTrainingPlan plan, CargoTrainingRegion region, decimal wait)
        => new()
        {
            Id = id, Vehicle = "가상 실차", CurrentRegion = region, AvailableRegion = region, Plan = plan,
            Eligible = plan.Verified && plan.Feasible, Eligibility = plan.ReasonCode,
            ExclusionReasons = plan.Verified && plan.Feasible ? [] : [plan.ReasonCode],
            CapacityKg = 10, CapacityLiters = 1000, IdleWaitMinutes = wait,
            TravelAfterAvailableMinutes = plan.Stops.FirstOrDefault()?.ArrivalMinutes,
            PickupEtaMinutes = plan.Stops.FirstOrDefault()?.ArrivalMinutes,
            CommitmentsFeasible = plan.Verified && plan.Feasible, LocationAgeMinutes = 1
        };

    private static CargoTrainingPlan CreatePlan(string scenario, bool joint)
    {
        // 이 수치들은 모델/운영 기본값이 아니라 이 사례에만 고정한 합성 입력이다.
        var a = Request("A", scenario == "overloaded" ? 6m : 3m, 0m, 1m);
        var b = Request("B", scenario == "overloaded" ? 6m : 3m, 2m, 3m);
        if (scenario == "deadline") b.픽업_시간창_종료일시 = Clock.AddMinutes(4);
        var jobs = joint ? new[] { a, b } : new[] { a };
        var vehicle = new 차량제원
        {
            차량코드 = "synthetic-r76", 차량명 = "사례용 확인 차량", 차체형태 = "탑차", 적재함길이Mm = 1000,
            적재함폭Mm = 1000, 적재함높이Mm = 1000, 최대적재중량Kg = 10, 운영권장중량Kg = 10,
            권장최대CBM = 1m, 팔레트적재개수 = 0, 비눈보호가능 = true, CreatedAt = Clock, UpdatedAt = Clock
        };
        var times = jobs.ToDictionary(x => x.의뢰Id, x => new 화물상하차작업시간(
            scenario == "missing_all_handling" || scenario == "missing_handling" && x.의뢰Id == "B" ? null : 2m, 2m), StringComparer.Ordinal);
        기사운송일정항목[] existing = joint ? [Stop(a, "pickup", 0, times), Stop(a, "dropoff", 1, times)] : [];
        var schedule = new 기사운송일정계획("synthetic-driver", Clock, new 배차경로좌표(0m, -1m), existing)
        { 상하차작업시간 = times };
        var insertion = new 운송일정삽입평가Service(new SyntheticRouteService()).평가Async(schedule, joint ? b : a).GetAwaiter().GetResult();
        // 기존 삽입기가 실제 열거한 시도 중 상차가 연속되는 비교 후보를 사용한다. 전체 최적해 주장이 아니다.
        var attempt = joint ? insertion.시도목록.First(x => x.도착예상목록.OrderBy(s => s.순서).Take(2).All(s => s.단계유형 == "pickup"))
            : insertion.시도목록.First();
        var stops = attempt.도착예상목록.OrderBy(x => x.순서)
            .Select(x => Stop(jobs.Single(j => j.의뢰Id == x.의뢰Id), x.단계유형, x.순서, times)).ToArray();
        var loading = new 화물구간적재검증Service(new 차량적재추천Engine()).평가(vehicle,
            jobs.Select(x => new 화물구간적재작업(x)).ToArray(), stops);
        var mixedLoadBlocked = 화물구간적재검증Service.혼적차단사유(jobs.Select(x => new 화물구간적재작업(x)).ToArray()).Count > 0;
        var timingKnown = times.Values.All(x => x.확인됨) && attempt.전체실제경로확인됨;
        var timingFeasible = timingKnown && attempt.전체완수가능여부;
        var proof = new
        {
            source = "synthetic", revision = "cargo-plan-r76-v1", clock = Clock,
            routeProvider = "synthetic-fixed-3min-1km-per-leg", vehicle,
            jobs = jobs.Select(x => new { x.의뢰Id, x.화물중량Kg, x.화물입력부피Cbm, x.화물수량,
                x.화물길이Mm, x.화물폭Mm, x.화물높이Mm, x.화물팔레트개수, x.화물바닥회전가능여부,
                x.화물적층가능여부, x.운송방식, x.서비스레벨, x.요청사항, x.픽업_시간창_종료일시, x.하차_시간창_종료일시 }),
            times, order = stops.Select(x => new { x.의뢰Id, x.단계유형, x.순서 })
        };
        var plan = new CargoTrainingPlan
        {
            PlanId = "synthetic-r76-" + scenario + (joint ? "-joint" : "-single"), DriverId = "synthetic-driver",
            InputSha256 = CargoTrainingStore.Hash(JsonSerializer.SerializeToUtf8Bytes(proof, CargoTrainingStore.Json)),
            Verified = loading.확인됨 && timingKnown, Feasible = !mixedLoadBlocked && loading.적합여부 && timingFeasible,
            ReasonCode = mixedLoadBlocked ? "CargoPlan.MixedLoadNotAllowed" : !loading.확인됨 || !loading.적합여부 ? loading.사유Code : !timingKnown ? "CargoPlan.HandlingTimeMissing"
                : !timingFeasible ? "CargoPlan.DeadlineExceeded" : "CargoPlan.Verified",
            TimingEvidence = timingKnown ? "synthetic_explicit_schedule_checked" : "missing_handling_time",
            LoadingEvidence = "shared_segment_service_checked", RequestIds = jobs.Select(x => x.의뢰Id).ToList(),
            Stops = attempt.도착예상목록.OrderBy(x => x.순서).Select(x =>
            {
                var handling = stops.Single(s => s.순서 == x.순서).작업시간분;
                decimal? arrival = timingKnown && x.예상도착시각 is { } eta ? (decimal)(eta - Clock).TotalMinutes : null;
                return new CargoTrainingPlanStop(x.의뢰Id, x.단계유형, x.순서, arrival, handling,
                    arrival + handling, x.시간창종료일시 is { } end ? (decimal)(end - Clock).TotalMinutes : null);
            }).ToList(),
            Segments = loading.구간목록.Select(x => new CargoTrainingLoadSegment(x.순서, x.의뢰Id, x.단계유형,
                x.적재의뢰Ids, x.총중량Kg, x.총부피Cbm, x.팔레트개수, x.비적층바닥면적M2, x.입력지문)).ToList(),
            TotalMinutes = timingKnown ? attempt.총소요시간분 : null,
            TotalDistanceKm = attempt.총거리Km,
            AgreedDriverCompensation = null
        };
        return plan with { PlanSha256 = PlanFingerprint(plan) };
    }

    private static 화주운송의뢰 Request(string id, decimal weight, decimal pickup, decimal dropoff) => new()
    {
        의뢰Id = id, 화물종류 = "포장 상자", 화물수량 = 1, 화물길이Mm = 200, 화물폭Mm = 200, 화물높이Mm = 200,
        화물중량Kg = weight, 화물부피Cbm = .01m, 화물입력부피Cbm = .01m, 화물팔레트개수 = 0,
        화물적재계산판본 = 화물적재입력Policy.CurrentRevision, 화물부피근거Code = "UserProvided", 화물바닥회전가능여부 = true, 화물적층가능여부 = true,
        픽업_위도 = 0, 픽업_경도 = pickup, 하차_위도 = 0, 하차_경도 = dropoff,
        픽업_시간창_시작일시 = Clock, 픽업_시간창_종료일시 = Clock.AddMinutes(60),
        하차_시간창_시작일시 = Clock, 하차_시간창_종료일시 = Clock.AddMinutes(90)
    };

    private static 기사운송일정항목 Stop(화주운송의뢰 job, string kind, int sequence,
        IReadOnlyDictionary<string, 화물상하차작업시간> times) => new(job.의뢰Id, kind, "",
        new 배차경로좌표(0m, kind == "pickup" ? job.픽업_경도!.Value : job.하차_경도!.Value), Clock,
        kind == "pickup" ? job.픽업_시간창_종료일시 : job.하차_시간창_종료일시, sequence, null, false, true)
    { 작업시간분 = kind == "pickup" ? times[job.의뢰Id].상차분 : times[job.의뢰Id].하차분 };

    private sealed class SyntheticRouteService : I배차추천경로Service
    {
        public Task<배차경로좌표?> ResolveOriginLocationAsync(string id, 용달기사? driver, DriverLocationSnapshot? location, 배차추천검색조건? criteria) => Task.FromResult<배차경로좌표?>(null);
        public Task<배차경로좌표?> ResolveRouteAnchorLocationAsync(string id, 용달기사? driver, DriverLocationSnapshot? location) => Task.FromResult<배차경로좌표?>(null);
        // 실제경로여부는 이 격리 provider의 합성 경로 계약만 뜻한다. 내보내기에는 실제 경로로 표시하지 않는다.
        public Task<배차경로예상결과?> EstimateRouteAsync(배차경로좌표? origin, 배차경로좌표? destination)
            => Task.FromResult<배차경로예상결과?>(new(1m, TimeSpan.FromMinutes(3), null, "synthetic", true) { RouteOption = "synthetic-fixed-leg" });
        public Task<배차경로예상결과?> EstimateOrderedRouteAsync(배차경로좌표? origin, IReadOnlyList<배차경로좌표> stops, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<배차삽입경로예상결과?> EstimateInsertionDelayAsync(배차경로좌표? origin, 배차경로좌표? anchor, 배차경로좌표? pickup, 배차경로좌표? dropoff) => throw new NotSupportedException();
        public decimal? CalculateDistanceKm(배차경로좌표 source, 배차경로좌표 target) => null;
    }
}
