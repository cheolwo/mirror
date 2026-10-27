using Microsoft.EntityFrameworkCore;
using Ssalddel.Services.Dispatch.Participation;
using 살뜰.도메인.공통;
using 살뜰.도메인.배차;
using 살뜰.도메인.기사;
using 살뜰.도메인.차량;
using 살뜰.도메인.화물;
using 살뜰.도메인.화주;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.Storage.Local;

namespace 살뜰.Services.Dispatch.Coordination;

public sealed partial class 국내화물배차조율입력Factory
{
    private async Task<운송의뢰기사조합평가> EvaluateAsync(
        운송원장 queue,
        화주운송의뢰 request,
        화물요구조건? cargoRequirement,
        국내화물운송기사상태Snapshot state,
        용달기사 driver,
        기사근무? currentShift,
        차량제원? vehicleSpec,
        IReadOnlySet<string> rejectedDriverIds,
        int acceptedTransportCount,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var excluded = new List<string>();
        var evidence = _상태근거 is null ? null
            : await _상태근거.조회Async(queue.의뢰Id, driver.기사Id, now, cancellationToken);
        if (evidence is not null)
            evidence = evidence with { 대기순위기준시각Utc = state.Aging기준시각Utc, 대기순위점수 = state.Aging점수,
                순위관측지문 = 화물배차상태SnapshotService.순위지문(state) };
        if (evidence is not null)
        {
            excluded.AddRange(evidence.차단사유);
            var fare = request.운임구성Id is { } fareId ? await _db.운임구성.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == fareId && x.의뢰Id == request.의뢰Id, cancellationToken) : null;
            if (!evidence.의뢰근거.TryGetValue(queue.의뢰Id, out var sourceHash)
                || sourceHash != 화물배차상태SnapshotService.원장지문(queue, request, fare, cargoRequirement)
                || evidence.기사기본지문 != 화물배차상태SnapshotService.기사원본지문(driver)
                || evidence.실차제원지문 != 화물배차상태SnapshotService.지문(vehicleSpec)
                || 화물배차상태SnapshotService.위치지문(state) != 화물배차상태SnapshotService.위치지문(
                    await _기사상태Store.GetAsync(state.DriverId, cancellationToken)))
                excluded.Add("DispatchEvidence.LoadedSourcesChanged");
        }
        excluded.AddRange(국내화물배차후보금지정책.조합후보금지사유(driver.기사Id, rejectedDriverIds));
        excluded.AddRange(국내화물배차후보금지정책.기사후보금지사유(
            state,
            driver));
        if (_sharedAdmission is not null)
        {
            var participation = await _sharedAdmission.CheckRecommendationAsync(driver.기사Id, queue, cancellationToken);
            if (participation.IsFailed)
                excluded.AddRange(participation.Errors.Select(x => x.Message));
        }

        var pickupPoint = CreatePoint(queue.픽업_위도, queue.픽업_경도);
        var dropoffPoint = CreatePoint(queue.하차_위도, queue.하차_경도);
        var driverPoint = CreatePoint(state.Latitude, state.Longitude);
        if (!_권역복귀상태Cache.TryGetValue(driver.기사Id, out var returnArea))
        {
            returnArea = await _권역복귀Service.상태조회Async(driver.기사Id, driverPoint,
                state.위치기록시각Utc ?? state.위치수신시각Utc, state.AccuracyM);
            _권역복귀상태Cache[driver.기사Id] = returnArea;
        }
        var otherWorkActive = false;
        if (returnArea.정책선택됨 && !_다른업무Cache.TryGetValue(driver.기사Id, out otherWorkActive))
        {
            otherWorkActive = await _db.운송원장.AsNoTracking()
                .AnyAsync(q => q.배차업무유형 != 상태값.배차업무유형.용달운송
                               && q.배차큐단계 == 상태값.배차큐단계.확정
                               && q.상태 != 상태값.배차상태.인수완료
                               && (q.기사_운송자 == driver.기사Id || q.확정기사Id == driver.기사Id));
            _다른업무Cache[driver.기사Id] = otherWorkActive;
        }
        if (pickupPoint is null)
        {
            excluded.Add("상차지 좌표가 없습니다.");
        }

        if (dropoffPoint is null)
        {
            excluded.Add("하차지 좌표가 없습니다.");
        }

        if (driverPoint is null)
        {
            excluded.Add("기사 현재 위치가 없습니다.");
        }

        decimal? pickupDistanceKm = null;
        decimal? totalDistanceKm = null;
        decimal? pickupMinutes = null;
        decimal? cargoMinutes = null;
        decimal? totalMinutes = null;
        decimal? tollFare = null;
        decimal? returnDistanceKm = null;
        운송삽입평가결과? scheduleEvaluation = null;
        if (driverPoint is not null && pickupPoint is not null)
        {
            pickupDistanceKm = _경로Service.CalculateDistanceKm(driverPoint, pickupPoint);
            if (pickupDistanceKm.HasValue && !상차접근허용(state, pickupDistanceKm.Value, request, now))
            {
                excluded.Add("기사 허용 반경 또는 상차 시간창 기준을 넘습니다.");
            }
        }

        var fit = _적합성Service.판정(vehicleSpec, request, cargoRequirement);
        if (!fit.적합여부)
        {
            excluded.AddRange(fit.부적합사유);
        }

        if (excluded.Count == 0 && driverPoint is not null && pickupPoint is not null && dropoffPoint is not null)
        {
            var toPickup = await _경로Service.EstimateRouteAsync(driverPoint, pickupPoint);
            var pickupToDropoff = await _경로Service.EstimateRouteAsync(pickupPoint, dropoffPoint);
            pickupMinutes = ToMinutes(toPickup?.Duration);
            cargoMinutes = ToMinutes(pickupToDropoff?.Duration);
            totalMinutes = Sum(pickupMinutes, cargoMinutes);
            totalDistanceKm = Sum(toPickup?.DistanceKm, pickupToDropoff?.DistanceKm);
            tollFare = Sum(toPickup?.TollFare, pickupToDropoff?.TollFare);
            var returnPoint = ResolveReturnPoint(driver, currentShift);
            if (returnPoint is not null)
            {
                var returnRoute = await _경로Service.EstimateRouteAsync(dropoffPoint, returnPoint);
                returnDistanceKm = returnRoute?.DistanceKm ?? _경로Service.CalculateDistanceKm(dropoffPoint, returnPoint);
            }

            if (acceptedTransportCount > 0 || returnArea.정책선택됨)
            {
                var schedulePlan = await _기사운송일정구성Service.구성Async(driver.기사Id, driverPoint);
                scheduleEvaluation = await _운송일정삽입평가Service.평가Async(schedulePlan, request);
            }
        }

        decimal? pickupWindowSlackMinutes = null;
        if (request.픽업_시간창_종료일시 > now && pickupMinutes.HasValue)
        {
            pickupWindowSlackMinutes = (decimal)(request.픽업_시간창_종료일시 - now).TotalMinutes - pickupMinutes.Value;
        }

        var additionalDelayMinutes = scheduleEvaluation?.총추가지연분;
        if (scheduleEvaluation is not null && (!scheduleEvaluation.삽입가능여부 || !scheduleEvaluation.전체완수가능여부))
            excluded.Add("FreightScheduleInfeasible");
        if (pickupWindowSlackMinutes < 0m || request.픽업_시간창_종료일시 <= now)
            excluded.Add("FreightPickupWindowExpired");
        var decision = _판정Service.판정(request, additionalDelayMinutes, pickupWindowSlackMinutes, pickupDistanceKm, fit, scheduleEvaluation);
        var requestDeliveryScope = 국내화물배달권정책.판정(pickupPoint, queue.픽업_도로명주소);
        var driverDeliveryScope = 국내화물배달권정책.판정(driverPoint, driver.주_활동지역);
        var sameDeliveryScope = string.Equals(requestDeliveryScope.배달권키, driverDeliveryScope.배달권키, StringComparison.Ordinal);
        var adjacentDeliveryScope = !sameDeliveryScope && 국내화물배달권정책.인접배달권여부(requestDeliveryScope, driverDeliveryScope);
        var estimatedRevenue = ResolveEstimatedRevenue(request);
        var estimatedCost = EstimateCost(totalDistanceKm, tollFare);
        var estimatedProfit = estimatedRevenue.HasValue && estimatedCost.HasValue
            ? estimatedRevenue.Value - estimatedCost.Value
            : (decimal?)null;
        var returnBurden = returnArea.정책선택됨
            ? new 복귀부담평가(0m, 0m, false, 기사복귀선호코드.Normalize(state.복귀콜선호), null)
            : 시간대별복귀부담정책.평가(now, returnDistanceKm, state.복귀콜선호);
        var returnEvaluation = returnArea.정책선택됨
            ? await _권역복귀Service.평가Async(returnArea,
                scheduleEvaluation?.기존일정종료좌표,
                scheduleEvaluation?.후보포함종료좌표,
                !otherWorkActive && scheduleEvaluation is
                {
                    기존일정실제경로확인됨: true,
                    후보포함실제경로확인됨: true
                },
                // 다른 적재 가능 시도의 끝점으로 바꾸지 않고, 선택 경로의 구간 적재 근거가 없으면 가점을 보류한다.
                화물권역복귀적재근거Policy.단독후보확인됨(vehicleSpec, request, cargoRequirement)
                    && acceptedTransportCount == 0
                    && excluded.Count == 0 && fit.적합여부 && decision.차량적합여부
                    && scheduleEvaluation is { 삽입가능여부: true, 전체완수가능여부: true }
                    && (!pickupWindowSlackMinutes.HasValue || pickupWindowSlackMinutes.Value >= 0m),
                scheduleEvaluation?.복귀비교경로옵션)
            : null;
        var evaluation = _평가Service.평가(
            request,
            decision,
            scheduleEvaluation,
            estimatedProfit,
            additionalDelayMinutes,
            pickupDistanceKm,
            totalMinutes,
            pickupWindowSlackMinutes,
            null,
            false,
            null);

        var score = 국내화물조합평가Composer.점수계산(
            evaluation, state.Aging점수, returnEvaluation, estimatedCost, estimatedProfit, returnBurden);

        if (evidence is not null && !await _상태근거!.현재근거확인Async(evidence, cancellationToken))
            excluded.Add("DispatchEvidence.ChangedDuringEvaluation");

        return 국내화물조합평가Composer.조립(new(
            queue.의뢰Id,
            driver.기사Id,
            state.Aging점수,
            acceptedTransportCount,
            pickupDistanceKm,
            pickupMinutes,
            cargoMinutes,
            totalMinutes,
            totalDistanceKm,
            tollFare,
            estimatedRevenue,
            estimatedCost,
            estimatedProfit,
            scheduleEvaluation,
            sameDeliveryScope,
            adjacentDeliveryScope,
            returnDistanceKm,
            returnBurden,
            returnEvaluation,
            evaluation,
            fit), score, excluded) with
        {
            판정근거 = evidence ?? new()
            {
                기준시각Utc = now, 조회시작시각Utc = now, 조회완료시각Utc = _clock.GetUtcNow().UtcDateTime,
                조회범위 = "Legacy factory dependencies; unbound state evidence", 원자적조회여부 = false,
                미확인사유 = ["DispatchEvidence.LegacyInputUnknown"]
            }
        };
    }

    private bool 상차접근허용(국내화물운송기사상태Snapshot state, decimal distanceKm, 화주운송의뢰 request, DateTime now)
    {
        var baseRadius = Math.Max(1m, _options.기사후보검색반경Km);
        if (distanceKm <= baseRadius)
        {
            return true;
        }

        var driverAllowedRadius = state.상차접근허용반경Km ?? baseRadius;
        var maxRadius = Math.Max(baseRadius, _options.원거리상차접근최대반경Km);
        var allowedRadius = Math.Min(driverAllowedRadius, maxRadius);
        if (distanceKm > allowedRadius)
        {
            return false;
        }

        if (request.픽업_시간창_종료일시 <= now)
        {
            return false;
        }

        var speed = Math.Max(1m, _options.원거리상차평균속도KmH);
        var estimatedMinutes = distanceKm / speed * 60m;
        var remainingMinutes = (decimal)(request.픽업_시간창_종료일시 - now).TotalMinutes;
        return remainingMinutes >= estimatedMinutes + Math.Max(0m, _options.원거리상차도착여유분);
    }
}
