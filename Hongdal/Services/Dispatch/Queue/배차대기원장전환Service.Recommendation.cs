using 홍달.도메인.공통;
using 홍달.도메인.배차;

namespace 홍달.Services.Dispatch.Queue
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

            if (queue.배차노출상태 == 상태값.배차노출상태.추천중
                && !string.IsNullOrWhiteSpace(queue.현재추천대상기사Id)
                && (!queue.추천만료시각.HasValue || queue.추천만료시각 > DateTime.UtcNow))
            {
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

            var changedAtUtc = DateTime.UtcNow;
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

            await _db.SaveChangesAsync(cancellationToken);

            await _recommendationNotificationService.추천알림요청생성Async(
                queue.Id,
                queue.의뢰Id,
                driverId,
                queue.추천라운드,
                cancellationToken);

            await _국내화물운송기사상태Service.추천기록Async(
                driverId,
                queue.추천시작시각.Value,
                cancellationToken);

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

            if (queue.추천라운드 >= _options.최대추천라운드)
            {
                return await 공개배차로전환Async(queue.의뢰Id, cancellationToken);
            }

            var selection = await _candidateSelectionService.다음후보선정Async(queue.의뢰Id, excludeDriverId, cancellationToken);
            if (selection.공개배차전환허용)
            {
                var changedAtUtc = DateTime.UtcNow;
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
    }
}
