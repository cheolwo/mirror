using 살뜰.도메인.공통;
using 살뜰.도메인.배차;
using 살뜰.Services.Dispatch.Common;
using Microsoft.EntityFrameworkCore;

namespace 살뜰.Services.Dispatch.Queue
{
    public sealed partial class 배차대기원장전환Service
    {
        private async Task<배차대기원장전환결과> 시작Async(
            운송원장 queue,
            string driverId,
            int? timeoutSeconds,
            CancellationToken cancellationToken,
            배차추천후보선정결과? selection = null)
        {
            if (!Ssalddel.Services.Community.생활배송배차Policy.자동추천가능(queue))
                return 전환안됨(queue, 배차대기원장전환결과코드.추천준비안됨, "선택한 방식에서는 자동 추천을 시작할 수 없습니다.");
            if (queue.상태 != 상태값.배차대기상태.대기)
            {
                var blocked = 대기상태아님(queue, driverId);
                return selection is null
                    ? blocked
                    : await 감사기록후반환Async(
                        queue,
                        selection,
                        배차엔진후속전환.전환없음,
                        blocked,
                        cancellationToken);
            }

            if (queue.배차업무유형 == 상태값.배차업무유형.용달운송
                && !await 운영배차신규수신Guard.허용Async(_운영배차, driverId, cancellationToken))
                return 전환안됨(queue, 배차대기원장전환결과코드.추천준비안됨, "신규 배차 수신 상태를 확인해 주세요.", driverId);
            var cargoBefore = queue.배차업무유형 == 상태값.배차업무유형.용달운송
                ? 음식배달선정원장판본.생성(queue) : null;

            if (queue.배차노출상태 == 상태값.배차노출상태.추천중
                && !string.IsNullOrWhiteSpace(queue.현재추천대상기사Id)
                && (!queue.추천만료시각.HasValue || queue.추천만료시각 > DateTime.UtcNow))
            {
                if (_sharedAdmission is not null)
                {
                    var participation = await _sharedAdmission.CheckRecommendationAsync(
                        queue.현재추천대상기사Id, queue, cancellationToken);
                    if (participation.IsFailed)
                        return 전환안됨(queue, 배차대기원장전환결과코드.추천준비안됨,
                            participation.Errors[0].Message, driverId);
                }
                if (queue.배차업무유형 == 상태값.배차업무유형.음식배달 && queue.추천만료시각.HasValue)
                    await _recommendationNotificationService.추천알림요청생성Async(queue.Id, queue.의뢰Id,
                        queue.현재추천대상기사Id, queue.추천라운드, cancellationToken);
                var blocked = 전환안됨(
                    queue,
                    배차대기원장전환결과코드.이미추천중,
                    "아직 만료되지 않은 추천중 상태라 새 추천을 시작하지 않았습니다.",
                    driverId);
                return selection is null
                    ? blocked
                    : await 감사기록후반환Async(
                        queue,
                        selection,
                        배차엔진후속전환.전환없음,
                        blocked,
                        cancellationToken);
            }

            // 직접 지정에서도 첫 추천의 수집 판단을 우회하지 않는다.
            if (selection is null && await 음식배달수집판정Async(queue, cancellationToken) is { } collecting)
                return collecting;

            if (selection is null && queue.배차업무유형 == 상태값.배차업무유형.용달운송)
            {
                var target = _화물대상후보 is null ? null
                    : await _화물대상후보.대상후보조회Async(queue, driverId, cancellationToken);
                if (target is null)
                    return 전환안됨(queue, 배차대기원장전환결과코드.추천준비안됨, "현재 후보 근거가 확인되지 않은 기사에게 추천할 수 없습니다.", driverId);
                selection = 배차추천후보선정결과.선정됨(target);
            }

            // 직접 기사 지정도 현재 후보 선정을 통과한다. 이미 선정한 자동 흐름과 유효한 기존 제안은 재평가하지 않는다.
            if (selection is null && (_foodDemandParticipationEnabled || _candidateAI?.Enabled == true || _dispatchTiming?.Enabled == true)
                && queue.배차업무유형 == 상태값.배차업무유형.음식배달)
            {
                var before = 음식배달선정원장판본.생성(queue);
                selection = await _candidateSelectionService.다음후보선정Async(
                    queue.의뢰Id, 제외기사Id: null, cancellationToken);
                if (!await 음식배달선정후현재원장확인Async(queue, before, cancellationToken))
                    return 전환안됨(queue, 배차대기원장전환결과코드.추천준비안됨,
                        "FoodDispatchAI.QueueChanged", driverId);
                if (selection is null || selection.상태값 != 배차추천후보선정상태.선정됨
                    || !string.Equals(selection.후보?.DriverId, driverId, StringComparison.Ordinal))
                {
                    return 전환안됨(queue, 배차대기원장전환결과코드.추천준비안됨,
                        "현재 배차 참여 순서에 맞는 기사에게만 새 제안을 시작할 수 있습니다.", driverId);
                }
            }

            if (queue.배차업무유형 == 상태값.배차업무유형.음식배달
                && _음식배달기사추천기록Service is null)
            {
                var blocked = 전환안됨(queue, 배차대기원장전환결과코드.배차구성오류,
                    "음식 배달 기사 추천 기록 서비스가 구성되지 않아 제안을 생성하지 않았습니다.", driverId);
                return selection is null ? blocked : await 감사기록후반환Async(
                    queue, selection, 배차엔진후속전환.전환없음, blocked, cancellationToken);
            }

            var beforePricing = _db.Entry(queue).CurrentValues.Clone();
            try
            {
                if (_smallGoodsPricing is not null)
                {
                    var pricing = await _smallGoodsPricing.준비Async(queue, cancellationToken);
                    if (pricing.IsFailed)
                    {
                        _db.Entry(queue).CurrentValues.SetValues(beforePricing);
                        var blocked = 전환안됨(queue, 배차대기원장전환결과코드.배차구성오류,
                            pricing.Errors[0].Message, driverId);
                        return selection is null ? blocked : await 감사기록후반환Async(
                            queue, selection, 배차엔진후속전환.전환없음, blocked, cancellationToken);
                    }
                }
                await 음식배달제안요금동결Async(queue, driverId, cancellationToken);
            }
            catch (Exception ex) when (ex is ArgumentException or Ssalddel.Application.Pricing.AgreedRateTableException)
            {
                var blocked = 전환안됨(queue, 배차대기원장전환결과코드.배차구성오류,
                    "배달료 계산에 필요한 거리 또는 요금 정책이 유효하지 않아 제안을 생성하지 않았습니다.", driverId);
                return selection is null ? blocked : await 감사기록후반환Async(
                    queue, selection, 배차엔진후속전환.전환없음, blocked, cancellationToken);
            }

            // 후보·AI·요금 계산을 기다리는 동안 변경된 업무 선택도 실제 제안 저장과 알림 전에 다시 확인합니다.
            if (_sharedAdmission is not null)
            {
                var participation = await _sharedAdmission.CheckRecommendationAsync(driverId, queue, cancellationToken);
                if (participation.IsFailed)
                {
                    _db.Entry(queue).CurrentValues.SetValues(beforePricing);
                    var blocked = 전환안됨(queue, 배차대기원장전환결과코드.추천준비안됨,
                        participation.Errors[0].Message, driverId);
                    return selection is null ? blocked : await 감사기록후반환Async(
                        queue, selection, 배차엔진후속전환.전환없음, blocked, cancellationToken);
                }
            }

            var changedAtUtc = DateTime.UtcNow;
            if (queue.배차업무유형 == 상태값.배차업무유형.용달운송)
            {
                var currentQueue = await _db.운송원장.AsNoTracking().SingleOrDefaultAsync(x => x.의뢰Id == queue.의뢰Id, cancellationToken);
                var basis = selection?.후보?.화물수행근거;
                var activeWithoutBasis = basis is null && _options.화물수행종료기준사용
                    && await _db.운송원장.AsNoTracking().AnyAsync(x => (x.기사_운송자 == driverId || x.확정기사Id == driverId)
                        && x.배차큐단계 == 상태값.배차큐단계.확정 && x.상태 != 상태값.배차상태.인수완료, cancellationToken);
                if (currentQueue is null || cargoBefore != 음식배달선정원장판본.생성(currentQueue)
                    || cargoBefore != 음식배달선정원장판본.생성(queue)
                    || activeWithoutBasis || (basis is not null && (_화물후속픽업 is null
                        || !await _화물후속픽업.현재근거확인Async(driverId, basis, cancellationToken)))
                    || !await 운영배차신규수신Guard.허용Async(_운영배차, driverId, cancellationToken))
                {
                    _db.Entry(queue).CurrentValues.SetValues(beforePricing);
                    return 전환안됨(queue, 배차대기원장전환결과코드.추천준비안됨, "기사의 수신 상태 또는 기존 수행 근거가 변경되었습니다.", driverId);
                }
            }
            queue.배차큐단계 = 상태값.배차큐단계.배차추천;
            queue.배차노출상태 = 상태값.배차노출상태.추천중;
            queue.현재추천대상기사Id = driverId;
            queue.추천라운드 = queue.추천라운드 + 1;
            queue.추천시작시각 = changedAtUtc;
            var ttl = TimeSpan.FromSeconds(timeoutSeconds ?? _options.추천유지시간초);
            queue.추천만료시각 = queue.추천시작시각.Value.Add(ttl);
            queue.UpdatedAt = changedAtUtc;

            var result = 전환됨(
                queue,
                배차대기원장전환결과코드.추천시작됨,
                "배차대기를 특정 기사 추천중 상태로 전환했습니다.",
                driverId);
            배차판단감사추가(
                queue,
                selection,
                배차엔진후속전환.추천시작,
                result.결과코드,
                changedAtUtc);
            _db.운영배차활동사건.Add(운영배차활동사건Factory.유효제안(
                driverId,
                queue.의뢰Id,
                queue.추천라운드,
                changedAtUtc));

            await _db.SaveChangesAsync(cancellationToken);

            await _recommendationNotificationService.추천알림요청생성Async(
                queue.Id,
                queue.의뢰Id,
                driverId,
                queue.추천라운드,
                cancellationToken);

            if (queue.배차업무유형 == 상태값.배차업무유형.음식배달)
            {
                await _음식배달기사추천기록Service!.추천기록Async(
                    driverId,
                    queue.추천시작시각.Value,
                    cancellationToken);
            }
            else if (queue.배차업무유형 == 상태값.배차업무유형.용달운송)
            {
                await _국내화물운송기사상태Service.추천기록Async(
                    driverId,
                    queue.추천시작시각.Value,
                    cancellationToken);
            }

            return result;
        }

        private async Task<배차대기원장전환결과> 추천거절후다음후보로진행Async(운송원장 queue, string? excludeDriverId, CancellationToken cancellationToken)
        {
            if (queue.상태 != 상태값.배차대기상태.대기)
            {
                return 대기상태아님(queue, excludeDriverId);
            }

            if (queue.배차큐단계 == 상태값.배차큐단계.공개배차)
            {
                return 전환안됨(
                    queue,
                    배차대기원장전환결과코드.단계불일치,
                    "이미 공개배차 단계라 다음 추천 후보를 찾지 않았습니다.",
                    excludeDriverId);
            }

            if (queue.배차업무유형 == 상태값.배차업무유형.용달운송
                && queue.생활배송배차방식 is null && queue.추천라운드 >= _options.최대추천라운드)
            {
                return await 공개배차로전환Async(queue.의뢰Id, cancellationToken);
            }

            if (await 음식배달수집판정Async(queue, cancellationToken) is { } collecting)
                return collecting;

            var before = 음식배달선정원장판본.생성(queue);
            var selection = await _candidateSelectionService.다음후보선정Async(queue.의뢰Id, excludeDriverId, cancellationToken);
            if (!await 음식배달선정후현재원장확인Async(queue, before, cancellationToken))
                return 전환안됨(queue, 배차대기원장전환결과코드.추천준비안됨,
                    "FoodDispatchAI.QueueChanged", excludeDriverId);
            if (selection.공개배차전환허용)
            {
                var changedAtUtc = DateTime.UtcNow;
                if (queue.배차업무유형 == 상태값.배차업무유형.음식배달 || queue.생활배송배차방식 is Ssalddel.Contracts.Common.Community.NeighborhoodDispatchModes.Automatic or Ssalddel.Contracts.Common.Community.NeighborhoodDispatchModes.Hybrid)
                {
                    음식배달후보재탐색대기상태적용(queue, changedAtUtc);
                    var retryResult = 배차대기원장전환결과.전환됨(
                        queue.의뢰Id,
                        배차대기원장전환결과코드.음식배달후보재탐색대기,
                        queue.생활배송배차방식 == Ssalddel.Contracts.Common.Community.NeighborhoodDispatchModes.Automatic ? "자동 추천에 적격 기사가 없어 재탐색을 기다립니다. 배차 방식을 변경할 수 있습니다."
                            : queue.생활배송배차방식 == Ssalddel.Contracts.Common.Community.NeighborhoodDispatchModes.Hybrid ? "추천 후보 재탐색을 기다리는 동안 공개 콜도 계속 열어 둡니다."
                            : "현재 적격 음식배달 기사가 없어 공개배차로 전환하지 않고 재탐색을 기다립니다.",
                        excludeDriverId);
                    배차판단감사추가(
                        queue,
                        selection,
                        배차엔진후속전환.재추천대기,
                        retryResult.결과코드,
                        changedAtUtc);
                    await _db.SaveChangesAsync(cancellationToken);
                    return retryResult;
                }

                공개배차상태적용(queue, changedAtUtc);
                var result = 배차대기원장전환결과.전환됨(
                    queue.의뢰Id,
                    배차대기원장전환결과코드.후보없음,
                    "추천 후보가 없어 공개배차로 전환했습니다.",
                    excludeDriverId);
                배차판단감사추가(
                    queue,
                    selection,
                    배차엔진후속전환.공개배차전환,
                    result.결과코드,
                    changedAtUtc);
                await _db.SaveChangesAsync(cancellationToken);
                return result;
            }

            if (selection.상태값 == 배차추천후보선정상태.선정됨)
            {
                return await 시작Async(
                    queue,
                    selection.후보!.DriverId,
                    null,
                    cancellationToken,
                    selection);
            }

            var blocked = selection.상태값 switch
            {
                배차추천후보선정상태.준비안됨 => 전환안됨(
                    queue,
                    배차대기원장전환결과코드.추천준비안됨,
                    selection.사유,
                    excludeDriverId),
                배차추천후보선정상태.잘못된입력 => 전환안됨(
                    queue,
                    배차대기원장전환결과코드.후보선정입력오류,
                    selection.사유,
                    excludeDriverId),
                배차추천후보선정상태.구성오류 => 전환안됨(
                    queue,
                    배차대기원장전환결과코드.배차구성오류,
                    selection.사유,
                    excludeDriverId),
                _ => 전환안됨(
                    queue,
                    배차대기원장전환결과코드.배차구성오류,
                    "알 수 없는 배차 후보 선정 결과라 공개배차로 전환하지 않았습니다.",
                    excludeDriverId)
            };
            return await 감사기록후반환Async(
                queue,
                selection,
                배차엔진후속전환.보류,
                blocked,
                cancellationToken);
        }

        private async Task<bool> 음식배달선정후현재원장확인Async(
            운송원장 queue, 음식배달선정원장판본 before, CancellationToken cancellationToken)
        {
            if ((_candidateAI?.Enabled != true && _dispatchTiming?.Enabled != true)
                || before.업무유형 != 상태값.배차업무유형.음식배달)
                return true;
            var entry = _db.Entry(queue);
            await entry.ReloadAsync(cancellationToken);
            return entry.State != EntityState.Detached
                && before == 음식배달선정원장판본.생성(queue)
                && 배차실행인덱스재구성정책.미처리운송의뢰인가(queue, DateTime.UtcNow);
        }
    }
}
