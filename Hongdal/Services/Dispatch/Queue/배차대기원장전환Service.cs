using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Hongdal;
using Hongdal.Contracts.Common.Operations;
using 홍달.도메인.공통;
using 홍달.도메인.배차;
using 홍달.Services.Dispatch.Engine;
using 홍달.Services.Dispatch.Notification;

namespace 홍달.Services.Dispatch.Queue
{
    public sealed partial class 배차대기원장전환Service : I배차대기원장전환Service
    {
        private readonly HongdalContext _db;
        private readonly 배차큐정책Options _options;
        private readonly I배차추천후보선정Service _candidateSelectionService;
        private readonly I배차추천알림Service _recommendationNotificationService;
        private readonly I국내화물운송기사상태Service _국내화물운송기사상태Service;
        private readonly I음식배달배차흐름Resolver _음식배달배차흐름Resolver;

        public 배차대기원장전환Service(
            HongdalContext db,
            IOptions<배차큐정책Options> options,
            I배차추천후보선정Service candidateSelectionService,
            I배차추천알림Service recommendationNotificationService,
            I국내화물운송기사상태Service 국내화물운송기사상태Service,
            I음식배달배차흐름Resolver 음식배달배차흐름Resolver)
        {
            _db = db;
            _options = options.Value;
            _candidateSelectionService = candidateSelectionService;
            _recommendationNotificationService = recommendationNotificationService;
            _국내화물운송기사상태Service = 국내화물운송기사상태Service;
            _음식배달배차흐름Resolver = 음식배달배차흐름Resolver;
        }

        public async Task<배차대기원장전환결과> 계획배차에서추천으로전환Async(string requestId, CancellationToken cancellationToken = default)
        {
            var queue = await _db.운송원장.FirstOrDefaultAsync(x => x.의뢰Id == requestId, cancellationToken);
            if (queue is null)
            {
                return 대상없음(requestId);
            }

            if (queue.상태 != 상태값.배차대기상태.대기)
            {
                return 대기상태아님(queue);
            }

            if (창고선행작업대기이면전환안됨(queue) is { } blocked)
            {
                return blocked;
            }

            queue.배차큐단계 = 상태값.배차큐단계.배차추천;
            queue.배차노출상태 = 상태값.배차노출상태.추천대기;
            queue.계획배차시도횟수++;
            queue.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync(cancellationToken);

            return await 추천거절후다음후보로진행Async(queue, excludeDriverId: null, cancellationToken);
        }

        public async Task<배차대기원장전환결과> 추천대기처리Async(string requestId, CancellationToken cancellationToken = default)
        {
            var queue = await _db.운송원장.FirstOrDefaultAsync(x => x.의뢰Id == requestId, cancellationToken);
            if (queue is null)
            {
                return 대상없음(requestId);
            }

            if (queue.상태 != 상태값.배차대기상태.대기)
            {
                return 대기상태아님(queue);
            }

            if (창고선행작업대기이면전환안됨(queue) is { } blocked)
            {
                return blocked;
            }

            if (queue.배차큐단계 != 상태값.배차큐단계.배차추천 || queue.배차노출상태 != 상태값.배차노출상태.추천대기)
            {
                return 전환안됨(
                    queue,
                    배차대기원장전환결과코드.단계불일치,
                    "추천대기 상태가 아니라 전환하지 않았습니다.");
            }

            return await 추천거절후다음후보로진행Async(queue, queue.마지막거절기사Id, cancellationToken);
        }

        public async Task<배차대기원장전환결과> 추천시작Async(string requestId, string driverId, int? timeoutSeconds = null, CancellationToken cancellationToken = default)
        {
            var queue = await _db.운송원장.FirstOrDefaultAsync(x => x.의뢰Id == requestId, cancellationToken);
            if (queue is null)
            {
                return 대상없음(requestId, driverId);
            }

            if (queue.상태 != 상태값.배차대기상태.대기)
            {
                return 대기상태아님(queue, driverId);
            }

            if (창고선행작업대기이면전환안됨(queue, driverId) is { } blocked)
            {
                return blocked;
            }

            return await 시작Async(queue, driverId, timeoutSeconds, cancellationToken);
        }

        public async Task<배차대기원장전환결과> 추천거절처리Async(string requestId, string driverId, CancellationToken cancellationToken = default)
        {
            var queue = await _db.운송원장.FirstOrDefaultAsync(x => x.의뢰Id == requestId, cancellationToken);
            if (queue is null)
            {
                return 대상없음(requestId, driverId);
            }

            if (queue.상태 != 상태값.배차대기상태.대기)
            {
                return 대기상태아님(queue, driverId);
            }

            // only process if the driver was indeed the current recommended
            if (!string.Equals(queue.현재추천대상기사Id, driverId, StringComparison.Ordinal))
            {
                return 전환안됨(
                    queue,
                    배차대기원장전환결과코드.현재추천기사불일치,
                    "현재 추천 대상 기사가 아니라 거절 전환하지 않았습니다.",
                    driverId);
            }

            queue.배차큐단계 = 상태값.배차큐단계.배차추천;
            queue.배차노출상태 = 상태값.배차노출상태.추천거절;
            queue.마지막거절기사Id = driverId;
            queue.현재추천대상기사Id = null;
            queue.추천시작시각 = null;
            queue.추천만료시각 = null;
            queue.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync(cancellationToken);

            return await 추천거절후다음후보로진행Async(queue, driverId, cancellationToken);
        }

        public async Task<배차대기원장전환결과> 추천만료처리Async(string requestId, CancellationToken cancellationToken = default)
        {
            var queue = await _db.운송원장.FirstOrDefaultAsync(x => x.의뢰Id == requestId, cancellationToken);
            if (queue is null)
            {
                return 대상없음(requestId);
            }

            if (queue.상태 != 상태값.배차대기상태.대기)
            {
                return 대기상태아님(queue);
            }

            if (!queue.추천만료시각.HasValue || queue.추천만료시각 > DateTime.UtcNow)
            {
                return 전환안됨(
                    queue,
                    배차대기원장전환결과코드.만료전,
                    "추천 만료 시각 전이라 만료 처리하지 않았습니다.",
                    queue.현재추천대상기사Id);
            }

            var expiredDriverId = queue.현재추천대상기사Id;

            queue.배차큐단계 = 상태값.배차큐단계.배차추천;
            queue.배차노출상태 = 상태값.배차노출상태.추천만료;
            queue.현재추천대상기사Id = null;
            queue.추천시작시각 = null;
            queue.추천만료시각 = null;
            queue.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync(cancellationToken);

            return await 추천거절후다음후보로진행Async(queue, expiredDriverId, cancellationToken);
        }

        public async Task<배차대기원장전환결과> 공개배차로전환Async(string requestId, CancellationToken cancellationToken = default)
        {
            var queue = await _db.운송원장.FirstOrDefaultAsync(x => x.의뢰Id == requestId, cancellationToken);
            if (queue is null)
            {
                return 대상없음(requestId);
            }

            if (queue.상태 != 상태값.배차대기상태.대기)
            {
                return 대기상태아님(queue);
            }

            공개배차상태적용(queue, DateTime.UtcNow);

            await _db.SaveChangesAsync(cancellationToken);

            return 전환됨(
                queue,
                배차대기원장전환결과코드.공개배차전환됨,
                "배차대기를 공개배차 상태로 전환했습니다.");
        }

        public async Task<배차대기원장전환결과> 실행주체확정결과동기화Async(
            string requestId,
            DispatchConfirmationBoundaryRequest confirmation,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(confirmation);

            var boundaryDecision = CollectiveActionDispatchBoundaryPolicy.Evaluate(confirmation);
            var driverId = confirmation.SelectedDriverParticipantId?.Trim() ?? string.Empty;
            if (!boundaryDecision.CanConfirmDispatch)
            {
                return 배차대기원장전환결과.전환안됨(
                    requestId,
                    배차대기원장전환결과코드.실행결정권한없음,
                    "플랫폼 후보 정보는 배차 확정 권한이 없습니다. 참여자의 실행 결정이 필요합니다.",
                    driverId);
            }

            var queue = await _db.운송원장.FirstOrDefaultAsync(x => x.의뢰Id == requestId, cancellationToken);
            if (queue is null)
            {
                return 대상없음(requestId, driverId);
            }

            if (queue.상태 != 상태값.배차대기상태.확정
                || !string.Equals(queue.확정기사Id, driverId, StringComparison.Ordinal))
            {
                return 배차대기원장전환결과.전환안됨(
                    requestId,
                    배차대기원장전환결과코드.실행결정불일치,
                    "참여자의 수락 결과와 현재 배차 확정 상태가 일치하지 않습니다.",
                    driverId);
            }

            queue.배차큐단계 = 상태값.배차큐단계.확정;
            queue.배차노출상태 = 상태값.배차노출상태.확정;
            queue.현재추천대상기사Id = null;
            queue.추천시작시각 = null;
            queue.추천만료시각 = null;
            queue.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync(cancellationToken);

            return 전환됨(
                queue,
                배차대기원장전환결과코드.확정됨,
                "참여자의 실행 결정을 배차 원장에 동기화했습니다.",
                driverId);
        }

        public async Task<배차대기원장전환결과> 배차수락취소처리Async(string requestId, string driverId, string? reason = null, CancellationToken cancellationToken = default)
        {
            var queue = await _db.운송원장.FirstOrDefaultAsync(x => x.의뢰Id == requestId, cancellationToken);
            if (queue is null)
            {
                return 대상없음(requestId, driverId);
            }

            var assignedToDriver = string.Equals(queue.확정기사Id, driverId, StringComparison.Ordinal)
                                   || string.Equals(queue.현재추천대상기사Id, driverId, StringComparison.Ordinal);
            if ((queue.확정기사Id is not null || queue.현재추천대상기사Id is not null) && !assignedToDriver)
            {
                return 전환안됨(
                    queue,
                    배차대기원장전환결과코드.현재추천기사불일치,
                    "확정 또는 추천 대상 기사가 아니라 수락 취소하지 않았습니다.",
                    driverId);
            }

            var dispatchRequest = await _db.화주운송의뢰.FirstOrDefaultAsync(x => x.의뢰Id == requestId, cancellationToken);
            if (dispatchRequest is not null)
            {
                dispatchRequest.배차상태 = 상태값.배차상태.매칭중;
                dispatchRequest.UpdatedAt = DateTime.UtcNow;
            }

            queue.상태 = 상태값.배차대기상태.대기;
            queue.배차큐단계 = 상태값.배차큐단계.배차추천;
            queue.배차노출상태 = 상태값.배차노출상태.추천대기;
            queue.마지막거절기사Id = driverId;
            queue.확정기사Id = null;
            queue.현재추천대상기사Id = null;
            queue.추천시작시각 = null;
            queue.추천만료시각 = null;
            queue.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync(cancellationToken);

            return await 추천거절후다음후보로진행Async(queue, driverId, cancellationToken);
        }

        private 배차대기원장전환결과? 창고선행작업대기이면전환안됨(운송원장 queue, string? driverId = null)
        {
            if (queue.배차업무유형 != 상태값.배차업무유형.음식배달)
            {
                return null;
            }

            var flow = _음식배달배차흐름Resolver.Resolve(queue);
            if (flow.배차시작가능)
            {
                return null;
            }

            return 전환안됨(
                queue,
                배차대기원장전환결과코드.창고선행작업대기,
                flow.배차시작조건,
                driverId);
        }
    }
}
