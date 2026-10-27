using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ssalddel;
using Ssalddel.Contracts.Common.Drivers;
using 살뜰.도메인.기사;
using 살뜰.도메인.화물;
using 살뜰.Services.Storage.Local;
using 살뜰.Services.Dispatch.Common;
using 살뜰.Services.Dispatch.Recommendation;
using Ssalddel.Services.Dispatch.Participation;
using Ssalddel.Services.Dispatch.Compliance;
using 살뜰.Services.Dispatch.AI;

namespace 살뜰.Services.Dispatch.Queue
{
    public sealed partial class 용달운송배차업무정책 : I배차업무정책, I용달운송대상후보Service
    {
        private readonly SsalddelContext _db;
        private readonly IDriverLocationStore _driverLocationStore;
        private readonly I국내화물운송기사상태Store _국내화물운송기사상태Store;
        private readonly 배차큐정책Options _options;
        private readonly I배차추천경로Service _routeService;
        private readonly I차량화물적합성Service _적합성Service;
        private readonly I배차추천판정Service _판정Service;
        private readonly I배차추천평가Service _평가Service;
        private readonly IDriverRejectedRequestStore _rejectedRequestStore;
        private readonly I용달운송후보검토Observer? _observer;
        private readonly I운영배차공통UseCase? _운영배차;
        private readonly I화물후속픽업기준Service _후속픽업;
        private readonly I운송일정삽입평가Service _일정평가;
        private readonly ISharedDriverWorkAdmissionService? _sharedAdmission;
        private readonly I신규화물업무자격Guard? _신규업무자격;
        private readonly I기사실차적재근거조회Service _실차조회;
        private readonly ICargoDispatchCandidateAI? _candidateAI;

        public 용달운송배차업무정책(
            SsalddelContext db,
            IDriverLocationStore driverLocationStore,
            I국내화물운송기사상태Store 국내화물운송기사상태Store,
            IOptions<배차큐정책Options> options,
            I배차추천경로Service routeService,
            I차량화물적합성Service 적합성Service,
            I배차추천판정Service 판정Service,
            I배차추천평가Service 평가Service,
            IDriverRejectedRequestStore rejectedRequestStore,
            I용달운송후보검토Observer? observer = null,
            I운영배차공통UseCase? 운영배차 = null,
            I화물후속픽업기준Service? 후속픽업 = null,
            I운송일정삽입평가Service? 일정평가 = null,
            ISharedDriverWorkAdmissionService? sharedAdmission = null,
            I신규화물업무자격Guard? 신규업무자격 = null,
            I기사실차적재근거조회Service? 실차조회 = null,
            ICargoDispatchCandidateAI? candidateAI = null)
        {
            _db = db;
            _driverLocationStore = driverLocationStore;
            _국내화물운송기사상태Store = 국내화물운송기사상태Store;
            _options = options.Value;
            _routeService = routeService;
            _적합성Service = 적합성Service;
            _판정Service = 판정Service;
            _평가Service = 평가Service;
            _rejectedRequestStore = rejectedRequestStore;
            _observer = observer;
            _운영배차 = 운영배차;
            _일정평가 = 일정평가 ?? new 운송일정삽입평가Service(routeService);
            _후속픽업 = 후속픽업 ?? new 화물후속픽업기준Service(db, new 기사운송일정구성Service(db), _일정평가);
            _sharedAdmission = sharedAdmission;
            _신규업무자격 = 신규업무자격;
            _실차조회 = 실차조회 ?? new 기사실차적재근거조회Service(db);
            _candidateAI = candidateAI;
        }

        public int 배차업무유형 => 살뜰.도메인.공통.상태값.배차업무유형.용달운송;

        public async Task<배차추천후보?> 대상후보조회Async(살뜰.도메인.운송.운송원장 queue, string driverId, CancellationToken ct = default)
        {
            var review = await 후보검토Async(queue, null, ct);
            var candidate = review.후보목록.FirstOrDefault(x => x.기사Id == driverId && x.판정Code == "CandidateAllowed")?.서버후보;
            if (candidate is null || !await 운영배차신규수신Guard.허용Async(_운영배차, driverId, ct)
                || (candidate.화물수행근거 is { } basis && !await _후속픽업.현재근거확인Async(driverId, basis, ct))) return null;
            return candidate;
        }

        public async Task<배차추천후보?> 다음후보선정Async(살뜰.도메인.운송.운송원장 queue, string? 제외기사Id = null, CancellationToken cancellationToken = default)
        {
            var source = _candidateAI?.Enabled == true ? await AI입력판본Async(queue, cancellationToken) : null;
            var review = await 후보검토Async(queue, 제외기사Id, cancellationToken);
            // 선택과 같은 계산을 관찰한다. 로컬 관찰 실패가 실제 추천 결과를 바꾸지 않는다.
            try { _observer?.OnReviewed(queue.의뢰Id, review); }
            catch (Exception) { /* 관찰자는 업무 실행의 선행 조건이 아니다. */ }
            if (_candidateAI?.Enabled != true || review.선정후보 is null) return review.선정후보;
            if (source is null) return null;
            return await CargoDispatchCandidateSelection.SelectAsync(review, _candidateAI, async ct =>
            {
                var current = await 후보검토Async(queue, 제외기사Id, ct);
                // 저장소 오류는 전파한다. 미확인·변경된 의뢰로 과거 AI 선택을 적용하지 않는다.
                return source == await AI입력판본Async(queue, ct) ? current : null;
            }, cancellationToken);
        }

        /// <summary>기존 후보 순회와 점수 계산을 한 번 실행한다. 배정·원장·기사 상태는 변경하지 않는다.</summary>
        public async Task<용달운송후보검토결과> 후보검토Async(살뜰.도메인.운송.운송원장 queue, string? 제외기사Id = null, CancellationToken cancellationToken = default)
        {
            var reviewed = new List<용달운송후보검토항목>();
            용달운송후보검토결과 Result(배차추천후보? selected = null, string? stopCode = null)
                => new(selected, reviewed.ToArray(), reviewed.Where(x => x.판정Code == "CandidateAllowed")
                    .Select(x => x.기사Id).ToArray(), stopCode);
            void Exclude(국내화물운송기사상태Snapshot state, string code, decimal? distance = null)
                => reviewed.Add(new(state.DriverId, code, distance, null, null, null, null));

            var request = await _db.화주운송의뢰.AsNoTracking().FirstOrDefaultAsync(x => x.의뢰Id == queue.의뢰Id, cancellationToken);
            if (request is null)
            {
                return Result(stopCode: "RequestNotFound");
            }

            if (!신규화물업무자격검사.화물정보완료(_신규업무자격, request))
                return Result(stopCode: "FreightCargo.RequiredInformationMissing");

            var cargoRequirement = await _db.화물요구조건.AsNoTracking().FirstOrDefaultAsync(x => x.의뢰Id == queue.의뢰Id, cancellationToken);
            var pickupPoint = queue.픽업_위도.HasValue && queue.픽업_경도.HasValue
                ? new 배차경로좌표(queue.픽업_위도.Value, queue.픽업_경도.Value)
                : null;

            if (pickupPoint is null)
            {
                return Result(stopCode: "PickupCoordinatesMissing");
            }

            var candidateStates = await _국내화물운송기사상태Store.위치반경조회Async(
                pickupPoint.Latitude,
                pickupPoint.Longitude,
                Math.Max(1m, _options.기사후보검색반경Km),
                Math.Clamp(_options.기사후보최대조회수, 1, 500),
                cancellationToken);
            candidateStates = await 원거리지원후보병합Async(candidateStates, pickupPoint, request, cancellationToken);
            if (candidateStates.Count == 0)
            {
                return Result(stopCode: "NoCandidatesInSearchScope");
            }

            var rejectedDriverIds = await _rejectedRequestStore.GetRejectedDriverIdsAsync(queue.의뢰Id, cancellationToken);
            var candidateDriverIds = candidateStates
                .Select(x => x.DriverId)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var vehicles = await _db.용달기사.AsNoTracking()
                .Where(x => Enumerable.Contains(candidateDriverIds, x.기사Id) && x.상태 == "활동중")
                .ToDictionaryAsync(x => x.기사Id, StringComparer.Ordinal, cancellationToken);
            var now = DateTime.UtcNow;

            배차추천후보? bestCandidate = null;
            decimal bestDistance = decimal.MaxValue;

            foreach (var osState in candidateStates)
            {
                if (string.Equals(
                        osState.AppKey,
                        기사앱식별자.FoodDeliveryDriverApp,
                        StringComparison.Ordinal))
                {
                    Exclude(osState, "DriverAppMismatch");
                    continue;
                }

                if (!vehicles.TryGetValue(osState.DriverId, out var driver))
                {
                    Exclude(osState, "ActiveVehicleMissing");
                    continue;
                }

                var qualification = await 신규화물업무자격검사.평가Async(_신규업무자격, driver.기사Id, cancellationToken);
                if (!qualification.CanReceiveNewWork)
                {
                    Exclude(osState, qualification.ReasonCode);
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(제외기사Id) && string.Equals(driver.기사Id, 제외기사Id, StringComparison.Ordinal))
                {
                    Exclude(osState, "DriverExcluded");
                    continue;
                }

                if (rejectedDriverIds.Contains(driver.기사Id))
                {
                    Exclude(osState, "DriverRejected");
                    continue;
                }

                if (!string.Equals(osState.운행상태, 상태값.기사운행상태.운행중, StringComparison.OrdinalIgnoreCase))
                {
                    Exclude(osState, "DriverNotDriving");
                    continue;
                }

                DriverLocationSnapshot location;
                if (!await 운영배차신규수신Guard.허용Async(_운영배차, driver.기사Id, cancellationToken))
                { Exclude(osState, "DispatchReceivingUnavailable"); continue; }
                if (_sharedAdmission is not null
                    && (await _sharedAdmission.CheckRecommendationAsync(driver.기사Id, queue, cancellationToken)).IsFailed)
                { Exclude(osState, "DriverWorkAdmissionBlocked"); continue; }
                if (osState.Latitude.HasValue
                    && osState.Longitude.HasValue
                    && 기사위치신선도정책.유효한가(
                        osState.위치수신시각Utc,
                        now,
                        _options.기사위치유효시간분))
                {
                    location = new DriverLocationSnapshot(
                        driver.기사Id,
                        osState.Latitude.Value,
                        osState.Longitude.Value,
                        osState.AccuracyM,
                        osState.운행상태,
                        osState.위치기록시각Utc ?? osState.위치수신시각Utc!.Value,
                        osState.위치수신시각Utc!.Value);
                }
                else if (!_driverLocationStore.TryGetLatest(driver.기사Id, out location)
                    || !기사위치신선도정책.유효한가(
                        location.ReceivedAtUtc,
                        now,
                        _options.기사위치유효시간분))
                {
                    Exclude(osState, "DriverLocationUnavailableOrStale");
                    continue;
                }

                var currentPoint = new 배차경로좌표(location.Latitude, location.Longitude);
                화물후속픽업기준결과? basis = _options.화물수행종료기준사용
                    ? await _후속픽업.조회Async(driver.기사Id, currentPoint, now, cancellationToken) : null;
                if (basis is { 확인됨: false }) { Exclude(osState, basis.사유Code); continue; }
                var origin = basis?.기준좌표 ?? currentPoint;
                DateTime? pickupArrival = null;
                decimal? travelMinutes = null;
                decimal? pickupSlack = null;
                운송삽입평가결과? schedule = null;
                var distanceKm = _routeService.CalculateDistanceKm(origin, pickupPoint);
                if (basis is { 활성업무있음: true })
                {
                    배차경로예상결과? route;
                    try { route = await _routeService.EstimateRouteAsync(origin, pickupPoint, basis.RouteOption!, cancellationToken); }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                    catch { Exclude(osState, "ActiveSchedulePickupRouteUnverified"); continue; }
                    if (route is not { 실제경로여부: true, DistanceKm: >= 0m, Duration: not null }
                        || route.Duration.Value < TimeSpan.Zero || route.RouteOption != basis.RouteOption
                        || !basis.가용예상시각Utc.HasValue || basis.기존계획 is null)
                    { Exclude(osState, "ActiveSchedulePickupRouteUnverified"); continue; }
                    distanceKm = route.DistanceKm;
                    pickupArrival = basis.가용예상시각Utc.Value.Add(route.Duration.Value);
                    if (request.픽업_시간창_종료일시 == default || pickupArrival > request.픽업_시간창_종료일시)
                    { Exclude(osState, "ActiveSchedulePickupLate", distanceKm); continue; }
                    travelMinutes = (decimal)(pickupArrival.Value - now).TotalMinutes;
                    pickupSlack = (decimal)(request.픽업_시간창_종료일시 - pickupArrival.Value).TotalMinutes;
                    try { schedule = await _일정평가.평가Async(basis.기존계획, request, cancellationToken); }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                    catch { Exclude(osState, "ActiveScheduleCandidateUnverified"); continue; }
                    if (!schedule.삽입가능여부 || !schedule.전체완수가능여부 || !schedule.후보포함실제경로확인됨
                        || !schedule.시도목록.Any(x => x.시도유형 == "하차후이어가기" && x.전체완수가능여부 && x.전체실제경로확인됨))
                    { Exclude(osState, "ActiveScheduleCandidateInfeasible"); continue; }
                }
                else
                {
                    var idle = await 대기기사약속검토Async(driver.기사Id, origin, pickupPoint, request, now, cancellationToken);
                    if (idle.제외Code is not null) { Exclude(osState, idle.제외Code, idle.거리Km); continue; }
                    distanceKm = idle.거리Km;
                    pickupArrival = idle.픽업도착시각Utc;
                    travelMinutes = idle.픽업소요분;
                    pickupSlack = idle.픽업여유분;
                    schedule = idle.일정;
                }
                if (basis is not null && !await _후속픽업.현재근거확인Async(driver.기사Id, basis, cancellationToken))
                { Exclude(osState, "ActiveScheduleChanged"); continue; }
                if (!distanceKm.HasValue)
                {
                    Exclude(osState, "DistanceUnavailable");
                    continue;
                }

                if (!상차접근허용(osState, distanceKm.Value, request))
                {
                    Exclude(osState, "OutsideApproachAllowance", distanceKm);
                    continue;
                }

                var vehicleSpec = (await _실차조회.조회Async(driver.기사Id, cancellationToken)).유효제원;
                var fit = _적합성Service.판정(vehicleSpec, request, cargoRequirement);
                if (!fit.적합여부)
                {
                    Exclude(osState, "VehicleIncompatible", distanceKm);
                    continue;
                }

                var 판단결과 = _판정Service.판정(request, schedule?.총추가지연분, pickupSlack, distanceKm, fit, schedule);
                var 평가결과 = _평가Service.평가(
                    request,
                    판단결과,
                    schedule,
                    null,
                    schedule?.총추가지연분,
                    distanceKm,
                    travelMinutes,
                    pickupSlack,
                    null,
                    false,
                    null);

                var agingScore = 기사대기Aging점수정책.계산(osState.Aging기준시각Utc, now);
                var score = (평가결과.추천점수 ?? 0m) + agingScore;
                var reason = agingScore > 0m
                    ? $"{평가결과.추천사유} · 기사대기보정 +{agingScore:0}"
                    : 평가결과.추천사유;
                reviewed.Add(new(driver.기사Id, "CandidateAllowed", distanceKm, 평가결과.추천점수,
                    agingScore, score, reason)
                {
                    픽업기준Code = basis?.사유Code ?? "LegacyCurrentLocation",
                    기존수행완료예상시각Utc = basis is { 활성업무있음: true } ? basis.가용예상시각Utc : null,
                    픽업도착예상시각Utc = pickupArrival,
                    서버후보 = new 배차추천후보(driver.기사Id, score, reason) { 화물수행근거 = basis }
                });

                if (bestCandidate is null
                    || score > bestCandidate.추천점수
                    || (score == bestCandidate.추천점수 && distanceKm.Value < bestDistance))
                {
                    bestCandidate = new 배차추천후보(driver.기사Id, score, reason) { 화물수행근거 = basis };
                    bestDistance = distanceKm.Value;
                }
            }

            if (bestCandidate is not null && (!await 운영배차신규수신Guard.허용Async(_운영배차, bestCandidate.DriverId, cancellationToken)
                || (bestCandidate.화물수행근거 is { } latestBasis
                    && !await _후속픽업.현재근거확인Async(bestCandidate.DriverId, latestBasis, cancellationToken))))
                return Result(stopCode: "CandidateChangedBeforeReturn");
            return Result(bestCandidate, bestCandidate is null ? "NoEligibleCandidate" : null);
        }

    }
}
