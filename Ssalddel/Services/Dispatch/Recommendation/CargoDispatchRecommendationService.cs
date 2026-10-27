using Ssalddel.Application.Common.Transport;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Hubs;
using 살뜰.도메인.공통;
using 살뜰.도메인.화물;
using 살뜰.도메인.화주;
using 살뜰.도메인.차량;
using Ssalddel.Application.Driver.Recommendation;
using Ssalddel.Services.Dispatch.Participation;
using 살뜰.Services.Dispatch.Common;
using 살뜰.Services.Dispatch.Queue;
using Microsoft.Extensions.Options;
using Ssalddel.Services.Dispatch.Compliance;
using 살뜰.Services.Dispatch.Coordination;

namespace 살뜰.Services.Dispatch.Recommendation
{
    public sealed partial class 화물배차추천Service : 배차추천Service, I배차추천Service
    {
        private readonly I차량화물적합성Service _적합성Service;
        private readonly 살뜰.Services.Dispatch.Queue.I배차대기원장전환Service _원장전환Service;
        private readonly I기사권역복귀Service _권역복귀Service;
        private readonly ISharedDriverWorkAdmissionService? _sharedAdmission;
        private readonly I소형배송기사제안요금Service? _smallGoodsPricing;
        private readonly I운영배차공통UseCase? _운영배차;
        private readonly I화물후속픽업기준Service _후속픽업;
        private readonly 배차큐정책Options _queueOptions;
        private readonly I신규화물업무자격Guard? _신규업무자격;
        private readonly I기사실차적재근거조회Service _실차적재조회;
        private readonly I국내화물추천재검증Service? _추천재검증;

        public 화물배차추천Service(
            SsalddelContext db,
            IDriverLocationStore driverLocationStore,
            IDriverRejectedRequestStore rejectedRequestStore,
            IDriverRecommendationPushService pushService,
            IDispatchRecommendationLogStore logStore,
            IHubContext<DispatchRecommendationHub> hubContext,
            I배차추천경로Service routeService,
            I배차추천판정Service 판정Service,
            I배차추천평가Service 평가Service,
            I차량화물적합성Service 적합성Service,
            I기사운송일정구성Service 기사운송일정구성Service,
            I운송일정삽입평가Service 운송일정삽입평가Service,
            IOpinetAveragePriceService averagePriceService,
            IGeocodingService geocodingService,
            살뜰.Services.Dispatch.Queue.I배차대기원장전환Service 원장전환Service,
            I기사권역복귀Service 권역복귀Service,
            ISharedDriverWorkAdmissionService? sharedAdmission = null,
            I소형배송기사제안요금Service? smallGoodsPricing = null,
            I운영배차공통UseCase? 운영배차 = null,
            I화물후속픽업기준Service? 후속픽업 = null,
            IOptions<배차큐정책Options>? queueOptions = null,
            I신규화물업무자격Guard? 신규업무자격 = null,
            I기사실차적재근거조회Service? 실차적재조회 = null,
            I국내화물추천재검증Service? 추천재검증 = null)
            : base(
                db,
                driverLocationStore,
                rejectedRequestStore,
                pushService,
                logStore,
                hubContext,
                routeService,
                판정Service,
                평가Service,
                기사운송일정구성Service,
                운송일정삽입평가Service,
                averagePriceService)
        {
            _적합성Service = 적합성Service;
            _원장전환Service = 원장전환Service;
            _권역복귀Service = 권역복귀Service;
            _sharedAdmission = sharedAdmission;
            _smallGoodsPricing = smallGoodsPricing;
            _운영배차 = 운영배차;
            _후속픽업 = 후속픽업 ?? new 화물후속픽업기준Service(db, 기사운송일정구성Service, 운송일정삽입평가Service);
            _queueOptions = queueOptions?.Value ?? new 배차큐정책Options();
            _신규업무자격 = 신규업무자격;
            _실차적재조회 = 실차적재조회 ?? new 기사실차적재근거조회Service(db);
            _추천재검증 = 추천재검증;
        }

        protected override async Task<bool> IsDrivingAsync(string driverId)
        {
            var driver = await _db.용달기사.AsNoTracking().FirstOrDefaultAsync(x => x.기사Id == driverId);
            return string.Equals(driver?.운행상태, 상태값.기사운행상태.운행중, StringComparison.Ordinal);
        }

        public override async Task<IReadOnlyList<DispatchRecommendationDto>> GetDrivingRecommendationsAsync(string driverId, 배차추천검색조건? criteria = null)
        {
            return await GetRecommendationsCoreAsync(driverId, criteria, isDriving: true);
        }

        public override async Task<IReadOnlyList<DispatchRecommendationDto>> GetIdleRecommendationsAsync(string driverId, 배차추천검색조건? criteria = null)
        {
            return await GetRecommendationsCoreAsync(driverId, criteria, isDriving: false);
        }

        private async Task<IReadOnlyList<DispatchRecommendationDto>> GetRecommendationsCoreAsync(string driverId, 배차추천검색조건? criteria, bool isDriving)
        {
            if (!(await 신규화물업무자격검사.평가Async(_신규업무자격, driverId)).CanReceiveNewWork) return [];
            if (!await 운영배차신규수신Guard.허용Async(_운영배차, driverId, CancellationToken.None)) return [];
            await 추천만료정리Async(driverId);
            if (_추천재검증 is not null) await _추천재검증.갱신Async(driverId);

            var driver = await _db.용달기사.AsNoTracking().FirstOrDefaultAsync(x => x.기사Id == driverId);
            _driverLocationStore.TryGetLatest(driverId, out DriverLocationSnapshot? currentLocation);
            var actualLocation = currentLocation is null ? null
                : new 배차경로좌표(currentLocation.Latitude, currentLocation.Longitude);
            var returnArea = await _권역복귀Service.상태조회Async(driverId, actualLocation,
                currentLocation?.RecordedAtUtc, currentLocation?.AccuracyM);
            // 화물 일정에 포함되지 않은 다른 업무가 있으면 전체 수행 끝점으로 인증하지 않는다.
            var otherWorkActive = returnArea.정책선택됨 && await _db.운송원장.AsNoTracking()
                .AnyAsync(q => q.배차업무유형 != 상태값.배차업무유형.용달운송
                               && q.배차큐단계 == 상태값.배차큐단계.확정
                               && q.상태 != 상태값.배차상태.인수완료
                               && (q.기사_운송자 == driverId || q.확정기사Id == driverId));
            var rejectedRequestIds = await _rejectedRequestStore.GetRejectedRequestIdsAsync(driverId);
            var rejectedRequestIdSet = rejectedRequestIds.Count > 0
                ? new HashSet<string>(rejectedRequestIds, StringComparer.Ordinal)
                : null;

            var originLocation = isDriving
                ? await _routeService.ResolveRouteAnchorLocationAsync(driverId, driver, currentLocation)
                : await _routeService.ResolveOriginLocationAsync(driverId, driver, currentLocation, criteria);
            화물후속픽업기준결과? pickupBasis = null;
            if (_queueOptions.화물수행종료기준사용)
            {
                var hasWork = await _db.운송원장.AsNoTracking().AnyAsync(x => (x.확정기사Id == driverId || x.기사_운송자 == driverId)
                    && x.배차큐단계 == 상태값.배차큐단계.확정 && x.상태 != 상태값.배차상태.인수완료);
                if (hasWork && (actualLocation is null || currentLocation is null
                        || !기사위치신선도정책.유효한가(currentLocation.ReceivedAtUtc, DateTime.UtcNow, _queueOptions.기사위치유효시간분))) return [];
                if (actualLocation is not null)
                {
                    pickupBasis = await _후속픽업.조회Async(driverId, actualLocation, DateTime.UtcNow);
                    if (!pickupBasis.확인됨) return [];
                    if (pickupBasis.활성업무있음) originLocation = pickupBasis.기준좌표;
                }
            }
            var routeAnchorLocation = isDriving ? originLocation : null;
            var currentSchedulePlan = pickupBasis?.기존계획 ?? (isDriving || returnArea.정책선택됨
                ? await _기사운송일정구성Service.구성Async(driverId,
                    returnArea.정책선택됨 ? actualLocation : originLocation)
                : null);
            var returnDestination = await ResolveReturnDestinationAsync(driverId, driver);
            var fuelPricePerLiter = await ResolveFuelPricePerLiterAsync();
            var fuelEfficiencyKmPerLiter = ResolveFuelEfficiencyKmPerLiter(driver?.차량);

            var items = await _db.운송원장
                .Where(q => q.배차업무유형 == 상태값.배차업무유형.용달운송
                            && q.상태 == 상태값.배차대기상태.대기
                            && q.배차큐단계 == 상태값.배차큐단계.배차추천
                            && q.배차노출상태 == 상태값.배차노출상태.추천중
                            && q.현재추천대상기사Id == driverId)
                .ToListAsync();

            if (_sharedAdmission is not null)
            {
                for (var index = items.Count - 1; index >= 0; index--)
                {
                    var participation = await _sharedAdmission.CheckRecommendationAsync(driverId, items[index], CancellationToken.None);
                    if (participation.IsFailed) items.RemoveAt(index);
                }
            }

            if (_smallGoodsPricing is not null)
            {
                for (var index = items.Count - 1; index >= 0; index--)
                    if ((await _smallGoodsPricing.조회검증Async(items[index], CancellationToken.None)).IsFailed) items.RemoveAt(index);
            }

            var requestIds = items.Select(q => q.의뢰Id).Distinct().ToList();
            var directPickingIds = await 기사제안표시근거Service.직접피킹의뢰Async(_db, requestIds);
            var requestMap = await LoadRequestMapAsync(requestIds);
            items.RemoveAll(x => !requestMap.TryGetValue(x.의뢰Id, out var cargoRequest)
                || !신규화물업무자격검사.화물정보완료(_신규업무자격, cargoRequest));
            var driverFareMap = await LoadDriverFareMapAsync(requestMap.Values);
            var cargoMap = await LoadCargoMapAsync(requestIds);
            var vehicleSpec = await LoadVehicleSpecAsync(driverId);

            var hasSearchCriteria = criteria is not null && criteria.RadiusKm > 0;
            var searchLatitude = hasSearchCriteria ? criteria!.Latitude : originLocation?.Latitude ?? currentLocation?.Latitude;
            var searchLongitude = hasSearchCriteria ? criteria!.Longitude : originLocation?.Longitude ?? currentLocation?.Longitude;
            var radiusKm = hasSearchCriteria ? criteria!.RadiusKm : (decimal?)null;

            var filtered = items
                .Where(q => rejectedRequestIdSet == null || !rejectedRequestIdSet.Contains(q.의뢰Id))
                .Select(q => new
                {
                    Item = q,
                    Request = requestMap.TryGetValue(q.의뢰Id, out var request) ? request : null,
                    CargoRequirement = cargoMap.TryGetValue(q.의뢰Id, out var cargoRequirement) ? cargoRequirement : null,
                    CandidateDistanceKm = searchLatitude.HasValue && searchLongitude.HasValue && q.픽업_위도.HasValue && q.픽업_경도.HasValue
                        ? (double?)_routeService.CalculateDistanceKm(
                            new 배차경로좌표(searchLatitude.Value, searchLongitude.Value),
                            new 배차경로좌표(q.픽업_위도.Value, q.픽업_경도.Value))
                        : (double?)null
                })
                .OrderBy(x => x.CandidateDistanceKm.HasValue ? 0 : 1)
                .ThenBy(x => x.CandidateDistanceKm ?? double.MaxValue)
                .ThenBy(x => x.Item.CreatedAt)
                .Where(x => !radiusKm.HasValue || !x.CandidateDistanceKm.HasValue || x.CandidateDistanceKm.Value <= (double)radiusKm.Value)
                .Take(returnArea.활성여부 ? 40 : 10)
                .ToList();

            var result = new List<DispatchRecommendationDto>(filtered.Count);
            foreach (var x in filtered)
            {
                var request = x.Request;
                var cargo = x.CargoRequirement;
                var pickupPoint = CreatePoint(x.Item.픽업_위도, x.Item.픽업_경도);
                var dropoffPoint = CreatePoint(x.Item.하차_위도, x.Item.하차_경도);
                var fit = request is null ? null : _적합성Service.판정(vehicleSpec, request, cargo);
                var scheduleEvaluation = request is not null && currentSchedulePlan is not null
                    ? await _운송일정삽입평가Service.평가Async(currentSchedulePlan, request)
                    : null;
                var insertionResult = await _routeService.EstimateInsertionDelayAsync(originLocation, routeAnchorLocation, pickupPoint, dropoffPoint);
                배차경로예상결과? routeFromOriginToPickup;
                if (pickupBasis is { 활성업무있음: true, RouteOption: not null })
                {
                    try { routeFromOriginToPickup = await _routeService.EstimateRouteAsync(originLocation, pickupPoint, pickupBasis.RouteOption, CancellationToken.None); }
                    catch (Exception) { continue; } // 경로 제공 실패는 새 후보의 근거 미확인이다.
                }
                else routeFromOriginToPickup = await _routeService.EstimateRouteAsync(originLocation, pickupPoint);
                var routeFromPickupToDropoff = await _routeService.EstimateRouteAsync(pickupPoint, dropoffPoint);
                var routeFromOriginToDropoff = await _routeService.EstimateRouteAsync(originLocation, dropoffPoint);
                var routeFromDropoffToReturn = await _routeService.EstimateRouteAsync(dropoffPoint, returnDestination.좌표);
                var routeFromOriginToReturn = await _routeService.EstimateRouteAsync(originLocation, returnDestination.좌표);

                var candidateDurationMinutes = SumMinutes(routeFromOriginToPickup?.Duration, routeFromPickupToDropoff?.Duration);
                var directDurationMinutes = routeFromOriginToDropoff?.Duration?.TotalMinutes;
                var additionalDelayMinutes = scheduleEvaluation?.총추가지연분
                    ?? insertionResult?.기존배송지연분
                    ?? (candidateDurationMinutes.HasValue && directDurationMinutes.HasValue
                    ? Math.Max(0m, candidateDurationMinutes.Value - (decimal)directDurationMinutes.Value)
                    : (decimal?)null);

                decimal? routeAnchorDistanceKm = null;
                if (routeAnchorLocation is not null && pickupPoint is not null)
                {
                    routeAnchorDistanceKm = Math.Round(_routeService.CalculateDistanceKm(routeAnchorLocation, pickupPoint) ?? 0m, 2);
                }

                var routeAnchorRoute = pickupBasis is { 활성업무있음: true }
                    ? routeFromOriginToPickup : await _routeService.EstimateRouteAsync(routeAnchorLocation, pickupPoint);
                var pickupArrivalMinutes = routeAnchorRoute?.Duration?.TotalMinutes ?? routeFromOriginToPickup?.Duration?.TotalMinutes;
                if (pickupBasis is { 활성업무있음: true })
                {
                    if (!pickupArrivalMinutes.HasValue || !pickupBasis.가용예상시각Utc.HasValue
                        || routeFromOriginToPickup is not { 실제경로여부: true, DistanceKm: >= 0m, Duration: not null }
                        || routeFromOriginToPickup.Duration.Value < TimeSpan.Zero || routeFromOriginToPickup.RouteOption != pickupBasis.RouteOption
                        || scheduleEvaluation is not { 삽입가능여부: true, 전체완수가능여부: true, 후보포함실제경로확인됨: true }
                        || !scheduleEvaluation.시도목록.Any(a => a.시도유형 == "하차후이어가기" && a.전체완수가능여부 && a.전체실제경로확인됨)) continue;
                    pickupArrivalMinutes += (pickupBasis.가용예상시각Utc.Value - DateTime.UtcNow).TotalMinutes;
                }
                decimal? pickupWindowSlackMinutes = null;
                var pickupWindowEnd = request?.픽업_시간창_종료일시;
                if (pickupWindowEnd.HasValue && pickupArrivalMinutes.HasValue)
                {
                    pickupWindowSlackMinutes = (decimal)((pickupWindowEnd.Value - DateTime.UtcNow).TotalMinutes - pickupArrivalMinutes.Value);
                }
                if (pickupBasis is { 활성업무있음: true } && (!pickupWindowSlackMinutes.HasValue || pickupWindowSlackMinutes < 0m)) continue;

                var 판단결과 = _판정Service.판정(request, additionalDelayMinutes, pickupWindowSlackMinutes, routeAnchorDistanceKm, fit, scheduleEvaluation);

                var emptyDistanceKm = routeFromOriginToPickup?.DistanceKm;
                var cargoDistanceKm = routeFromPickupToDropoff?.DistanceKm;
                var returnDistanceKm = routeFromDropoffToReturn?.DistanceKm;
                var directReturnDistanceKm = routeFromOriginToReturn?.DistanceKm;
                var totalEmptyDistanceKm = SumDistance(emptyDistanceKm, returnDistanceKm);
                var returnDetourDistanceKm = returnDistanceKm.HasValue && directReturnDistanceKm.HasValue
                    ? Math.Round(returnDistanceKm.Value - directReturnDistanceKm.Value, 2)
                    : (decimal?)null;
                var totalDistanceKm = SumDistance(emptyDistanceKm, cargoDistanceKm, returnDistanceKm);
                var tollFare = SumMoney(routeFromOriginToPickup?.TollFare, routeFromPickupToDropoff?.TollFare);
                var estimatedFuelCost = EstimateFuelCost(totalDistanceKm, fuelEfficiencyKmPerLiter, fuelPricePerLiter);
                var offerPricing = 소형배송기사제안요금Service.TryReadPricing(x.Item);
                driverFareMap.TryGetValue(request?.운임구성Id ?? 0, out var driverFare);
                var payout = 기사제안표시근거Service.화물보수(x.Item, request, driverFare, offerPricing);
                var estimatedRevenue = payout.Amount;
                var estimatedTotalCost = SumMoney(tollFare, estimatedFuelCost);
                var estimatedExtraProfit = estimatedRevenue.HasValue && estimatedTotalCost.HasValue
                    ? estimatedRevenue.Value - estimatedTotalCost.Value
                    : (decimal?)null;

                var 평가결과 = _평가Service.평가(
                    request,
                    판단결과,
                    scheduleEvaluation,
                    estimatedExtraProfit,
                    additionalDelayMinutes,
                    routeAnchorDistanceKm,
                    candidateDurationMinutes,
                    pickupWindowSlackMinutes,
                    returnArea.정책선택됨 ? null : returnDetourDistanceKm,
                    !returnArea.정책선택됨 && returnDestination.복귀지기준사용됨,
                    returnArea.정책선택됨 ? null : returnDestination.출처);
                var returnEvaluation = returnArea.정책선택됨
                    ? await _권역복귀Service.평가Async(returnArea,
                        scheduleEvaluation?.기존일정종료좌표,
                        scheduleEvaluation?.후보포함종료좌표,
                        !otherWorkActive && scheduleEvaluation is
                        {
                            기존일정실제경로확인됨: true,
                            후보포함실제경로확인됨: true
                        },
                        // 선택 경로의 구간 적재 검증이 연결되기 전에는 기존 화물이 없는 단독 후보만 인증한다.
                        화물권역복귀적재근거Policy.단독후보확인됨(vehicleSpec, request, cargo)
                            && currentSchedulePlan is { 항목목록.Count: 0 }
                            && request is not null && fit?.적합여부 == true && 판단결과.차량적합여부
                            && scheduleEvaluation is { 삽입가능여부: true, 전체완수가능여부: true }
                            && (!pickupWindowSlackMinutes.HasValue || pickupWindowSlackMinutes.Value >= 0m),
                        scheduleEvaluation?.복귀비교경로옵션)
                    : null;

                var recommendation = new DispatchRecommendationDto
                {
                    WorkKind = SharedDriverWorkAdmissionService.ResolveWorkKind(x.Item),
                    AssignedActivities = 기사제안표시근거Service.운송작업(directPickingIds.Contains(x.Item.의뢰Id)),
                    Pricing = offerPricing,
                    기사지급예정운임 = payout.Amount,
                    기사지급예정운임근거 = payout.Source,
                    의뢰Id = x.Item.의뢰Id,
                    화물종류 = request?.화물종류 ?? x.Item.픽업_도로명주소,
                    픽업지 = x.Item.픽업_도로명주소,
                    하차지 = x.Item.하차_도로명주소,
                    픽업_위도 = x.Item.픽업_위도,
                    픽업_경도 = x.Item.픽업_경도,
                    하차_위도 = x.Item.하차_위도,
                    하차_경도 = x.Item.하차_경도,
                    직선거리Km = x.CandidateDistanceKm.HasValue ? Math.Round((decimal)x.CandidateDistanceKm.Value, 2) : null,
                    픽업거리Km = emptyDistanceKm.HasValue ? Math.Round(emptyDistanceKm.Value, 2) : null,
                    공차거리Km = totalEmptyDistanceKm.HasValue ? Math.Round(totalEmptyDistanceKm.Value, 2) : null,
                    운송거리Km = cargoDistanceKm.HasValue ? Math.Round(cargoDistanceKm.Value, 2) : null,
                    복귀예상거리Km = returnDistanceKm.HasValue ? Math.Round(returnDistanceKm.Value, 2) : null,
                    지금바로복귀거리Km = directReturnDistanceKm.HasValue ? Math.Round(directReturnDistanceKm.Value, 2) : null,
                    복귀우회증가거리Km = returnDetourDistanceKm,
                    총공차거리Km = totalEmptyDistanceKm.HasValue ? Math.Round(totalEmptyDistanceKm.Value, 2) : null,
                    주행거리Km = totalDistanceKm.HasValue ? Math.Round(totalDistanceKm.Value, 2) : null,
                    예상톨비 = tollFare.HasValue ? Math.Round(tollFare.Value, 0) : null,
                    예상연료비 = estimatedFuelCost.HasValue ? Math.Round(estimatedFuelCost.Value, 0) : null,
                    예상총비용 = estimatedTotalCost.HasValue ? Math.Round(estimatedTotalCost.Value, 0) : null,
                    예상수익 = estimatedRevenue.HasValue ? Math.Round(estimatedRevenue.Value, 0) : null,
                    예상추가순이익 = estimatedExtraProfit.HasValue ? Math.Round(estimatedExtraProfit.Value, 0) : null,
                    분당추가수익 = estimatedExtraProfit.HasValue && candidateDurationMinutes.HasValue && candidateDurationMinutes.Value > 0
                        ? Math.Round(estimatedExtraProfit.Value / candidateDurationMinutes.Value, 0)
                        : null,
                    추천유형 = 판단결과.추천유형,
                    추가예상시간분 = candidateDurationMinutes.HasValue ? Math.Round(candidateDurationMinutes.Value, 0) : null,
                    기존배송지연분 = additionalDelayMinutes.HasValue ? Math.Round(additionalDelayMinutes.Value, 0) : null,
                    기존경로거리Km = insertionResult?.기존경로거리Km,
                    삽입경로거리Km = insertionResult?.삽입경로거리Km,
                    기존경로소요시간분 = insertionResult?.기존경로소요시간분,
                    삽입경로소요시간분 = insertionResult?.삽입경로소요시간분,
                    삽입추가톨비 = insertionResult?.삽입추가톨비,
                    추천점수 = 평가결과.추천점수 + (returnEvaluation?.가점 ?? 0m),
                    추천사유 = returnEvaluation is null ? 평가결과.추천사유
                        : $"{평가결과.추천사유} · {returnEvaluation.사유}",
                    일정삽입가능여부 = scheduleEvaluation?.삽입가능여부 ?? !isDriving,
                    전체일정완수가능여부 = scheduleEvaluation?.전체완수가능여부 ?? true,
                    최적삽입인덱스 = scheduleEvaluation?.최적삽입인덱스,
                    경로변경이점여부 = scheduleEvaluation?.경로변경이점여부 ?? false,
                    경로변경절감분 = scheduleEvaluation?.경로변경절감분,
                    권장경로순서 = scheduleEvaluation?.권장경로순서.ToArray() ?? Array.Empty<string>(),
                    최대시간위반분 = scheduleEvaluation?.최대시간위반분,
                    일정위반사유 = scheduleEvaluation?.위반사유 ?? Array.Empty<string>(),
                    복귀지기준추천여부 = !returnArea.정책선택됨 && returnDestination.복귀지기준사용됨,
                    복귀지출처 = returnArea.정책선택됨 ? null : returnDestination.출처,
                    복귀추천사유 = returnEvaluation?.사유 ?? 평가결과.복귀추천사유,
                    권역복귀정책선택됨 = returnArea.정책선택됨 ? true : null,
                    권역복귀활성여부 = returnArea.정책선택됨 ? returnArea.활성여부 : null,
                    권역복귀확인됨 = returnEvaluation?.확인됨,
                    권역복귀가점 = returnEvaluation?.가점,
                    전체일정기존복귀거리Km = returnEvaluation?.기존복귀거리Km,
                    전체일정후보복귀거리Km = returnEvaluation?.후보복귀거리Km,
                    배지 = returnEvaluation is { 가점: > 0m }
                        ? 평가결과.배지.Append("권역 복귀 방향").ToArray() : 평가결과.배지,
                    경고 = fit is null ? 평가결과.경고 : 평가결과.경고.Concat(fit.경고).ToArray(),
                    차량적합여부 = fit?.적합여부 ?? true,
                    차량부적합사유 = fit?.부적합사유 ?? Array.Empty<string>(),
                    차량경고 = fit?.경고 ?? Array.Empty<string>(),
                    추천시작시각 = x.Item.추천시작시각,
                    추천만료시각 = x.Item.추천만료시각,
                    추천라운드 = x.Item.추천라운드,
                    상태 = x.Item.상태,
                    배차상태 = 상태값.배차상태.대기
                };

                화물운송조건Projector.투영(recommendation, request);
                DispatchRecommendationRequestTypeClassifier.ApplyTo(
                    recommendation,
                    x.Item.원본의뢰유형,
                    x.Item.공동구매도착지유형코드,
                    x.Item.공동구매기사세대배송여부,
                    x.Item.공동구매세대배송건수);
                if (생활배송기사정보공개Policy.생활배송인가(request?.클라이언트요청Id))
                    생활배송기사정보공개Policy.추천정보가림(recommendation);
                result.Add(recommendation);
            }

            if (!(await 신규화물업무자격검사.평가Async(_신규업무자격, driverId)).CanReceiveNewWork
                || !await 운영배차신규수신Guard.허용Async(_운영배차, driverId, CancellationToken.None)
                || (pickupBasis is not null && !await _후속픽업.현재근거확인Async(driverId, pickupBasis))) return [];
            return returnArea.활성여부
                ? result.OrderByDescending(x => x.추천점수 ?? decimal.MinValue)
                    .ThenBy(x => x.픽업거리Km ?? decimal.MaxValue).Take(10).ToArray()
                : result;
        }

    }
}
