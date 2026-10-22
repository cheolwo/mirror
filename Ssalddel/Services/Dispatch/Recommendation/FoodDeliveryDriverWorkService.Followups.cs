using Ssalddel.Contracts.Common.Metadata;

using FluentResults;
using System.Collections.Concurrent;
using System.Data;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Application.Driver.DispatchAction;
using Ssalddel.Application.Food.Events;
using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Common.Operations;
using 살뜰.Services.Dispatch.Common;
using Ssalddel.Contracts.Food;
using Ssalddel.Services.Community;
using Ssalddel.Services.Food;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using 살뜰.Data;
using 살뜰.Services.Dispatch.Coordination;
using 살뜰.Services.Dispatch.Engine;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.Services.Settlement;
using 살뜰.Services.Storage.Local;
using 살뜰.도메인.공통;
using 살뜰.도메인.음식;
using 살뜰.도메인.운송;
using 살뜰.Services.Notifications;
using 살뜰.도메인.설정;

namespace 살뜰.Services.Dispatch.Recommendation;

[SsalddelCodeMetadata(
    SsalddelCodeFeatureKeys.FoodDriverWork,
    SsalddelCodeLayer.Application,
    "확정된 기사 상태 변경의 원장 동기화·알림·활동·정산 후속을 전달한다.",
    ContractType = typeof(I음식배달기사업무Service),
    Effects = SsalddelCodeEffect.PersistentRead | SsalddelCodeEffect.PersistentWrite | SsalddelCodeEffect.NetworkCall | SsalddelCodeEffect.StateMutation,
    FlowOrder = 140,
    StepKey = "application.driver-followups",
    DependsOnStepKeys = ["application.driver-state-persistence", "contract.driver-followup-intent"],
    ExecutionStage = SsalddelCodeExecutionStage.Projection,
    ReadsFrom = SsalddelCodeDataScope.OperationalState,
    WritesTo = SsalddelCodeDataScope.OperationalState,
    Boundary = "예약과 실제 처리는 별개다. 영속 Outbox가 있으면 재처리하며 기존 fallback·거절·월 정산의 별도 경계를 보존한다. 응답 유실에서 외부 수신의 정확히 한 번 전달을 보장하지 않는다.")]
public sealed partial class 음식배달기사업무Service
{
    private readonly List<Command알림Outbox> _업무진행예약항목 = [];

    private static 음식기사후속의도 후속의도(운송원장 queue, string orderNo, string driverId,
        string status, DateTime occurredAtUtc, string? reason, bool syncLedgers = true, bool activity = true)
        => new(orderNo, queue.Id, queue.의뢰Id, driverId, status, occurredAtUtc, reason, syncLedgers, activity);

    private async Task<음식기사후속의도> 예약후속Async(운송원장 queue, string orderNo, string driverId,
        string status, DateTime occurredAtUtc, string? reason, CancellationToken cancellationToken,
        bool syncLedgers = true, bool activity = true)
    {
        var intent = 후속의도(queue, orderNo, driverId, status, occurredAtUtc, reason, syncLedgers, activity);
        try
        {
            if (_driverFollowups is not null) await _driverFollowups.예약Async(intent, cancellationToken);
            예약업무진행알림(queue, orderNo, driverId, status, occurredAtUtc, intent.EventId);
            return intent;
        }
        catch
        {
            업무진행예약해제();
            throw;
        }
    }

    private void 예약업무진행알림(운송원장 queue, string orderNo, string driverId,
        string status, DateTime occurredAtUtc, string occurrenceKey)
    {
        var stage = status switch
        {
            "기사배정" => "food.assigned",
            "RestaurantArrived" => "food.arrived",
            "픽업완료" => "food.picked-up",
            "전달완료" => "food.delivered",
            _ => null
        };
        if (stage is null || queue.확정기사Id != driverId
            || queue.배차업무유형 != 상태값.배차업무유형.음식배달
            || !운송의뢰배차원천유형.Is음식점주문(queue.원본의뢰유형)
            || (string.IsNullOrWhiteSpace(queue.원본의뢰Id) ? queue.의뢰Id : queue.원본의뢰Id) != orderNo) return;
        var order = _db.음식주문.Local.SingleOrDefault(x => x.주문번호 == orderNo);
        if (order is null) return;
        var row = 업무진행알림Recorder.Reserve(_db, stage, orderNo, occurrenceKey,
            order.주문자UserId, $"/workspace/orderer?selected={Uri.EscapeDataString(orderNo)}", occurredAtUtc);
        if (row is not null) _업무진행예약항목.Add(row);
    }

    private void 업무진행예약해제()
    {
        foreach (var row in _업무진행예약항목)
            if (_db.Entry(row).State == EntityState.Added)
                _db.Entry(row).State = EntityState.Detached;
        _업무진행예약항목.Clear();
    }

    private async Task 처리후속Async(음식기사후속의도 intent, CancellationToken cancellationToken)
    {
        if (_driverFollowups is not null)
        {
            try { await _driverFollowups.즉시처리Async(intent, cancellationToken); }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "음식 기사 후속 즉시 처리 실패. 영속 Outbox에서 재시도합니다. OrderNo={OrderNo}", intent.주문번호);
            }
            return;
        }

        // 이전 직접 생성 테스트/호환 adapter 경로입니다. 제품 DI는 영속 후속 포트를 명시적으로 연결합니다.
        if (intent.원장동기화)
        {
            if (intent.상태 == "Rejected") await SyncFoodLedgerAsync(intent.주문번호, intent.처리기사Id, cancellationToken);
            else
            {
                var queue = await _db.운송원장.AsNoTracking().SingleAsync(x => x.Id == intent.운송Id, cancellationToken);
                await SyncLedgersAsync(queue, intent.주문번호, intent.처리기사Id, cancellationToken);
            }
        }
        if (intent.음식점알림사유 is { } reason)
            await NotifyRestaurantAsync(intent.주문번호, reason, cancellationToken);
        if (intent.활동발행)
            await PublishCommunityActivityAsync(intent.배차의뢰Id, intent.주문번호, intent.상태, intent.발생시각Utc, cancellationToken);
    }
    private async Task SyncLedgersAsync(
        운송원장 queue,
        string orderNo,
        string updatedBy,
        CancellationToken cancellationToken)
    {
        try
        {
            await _transportLedgerSync.운송실행투영동기화Async(queue, updatedBy, cancellationToken);
            var order = _foodOrderStore.GetOrder(orderNo);
            if (order is not null)
            {
                await _foodLedgerOutbox.음식주문예약후즉시처리Async(
                    order,
                    updatedBy,
                    BuildFoodLedgerIdempotencyKey(order, updatedBy),
                    cancellationToken);
            }
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "음식 배달 확정 후 원장 동기화에 실패했습니다. OrderNo={OrderNo}", orderNo);
        }
    }

    private async Task SyncFoodLedgerAsync(
        string orderNo,
        string updatedBy,
        CancellationToken cancellationToken)
    {
        try
        {
            var order = _foodOrderStore.GetOrder(orderNo);
            if (order is not null)
            {
                await _foodLedgerOutbox.음식주문예약후즉시처리Async(
                    order,
                    updatedBy,
                    BuildFoodLedgerIdempotencyKey(order, updatedBy),
                    cancellationToken);
            }
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "음식 주문 상태 변경 후 원장 동기화에 실패했습니다. OrderNo={OrderNo}", orderNo);
        }
    }

    private static string BuildFoodLedgerIdempotencyKey(
        음식주문응답 order,
        string updatedBy)
    {
        var latestTransition = order.상태이력
            .OrderByDescending(x => x.전이시각Utc)
            .FirstOrDefault()
            ?.전이시각Utc
            .Ticks ?? 0L;
        return $"food-driver:{order.주문번호}:{order.상태}:{order.배차상태}:{updatedBy}:{latestTransition}";
    }

    private async Task NotifyRestaurantAsync(
        string orderNo,
        string reason,
        CancellationToken cancellationToken)
    {
        try
        {
            var order = _foodOrderStore.GetOrder(orderNo);
            if (order is not null)
            {
                await _restaurantNotification.주문상태변경알림발송Async(order, reason, cancellationToken);
            }
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                ex,
                "음식 배달 상태 변경 후 음식점 실시간 알림에 실패했습니다. OrderNo={OrderNo}",
                orderNo);
        }
    }

    private async Task PublishCommunityActivityAsync(
        string deliveryId,
        string orderNo,
        string status,
        DateTime occurredAtUtc,
        CancellationToken cancellationToken)
    {
        try
        {
            await _publisher.Publish(
                new 음식배달인계상태변경됨Event(
                    deliveryId,
                    orderNo,
                    status,
                    occurredAtUtc,
                    $"food-delivery:{deliveryId}:{status}:{occurredAtUtc.Ticks}"),
                cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                ex,
                "음식 배달 상태 변경 후 커뮤니티 활동 신호 발행에 실패했습니다. OrderNo={OrderNo}, Status={Status}",
                orderNo,
                status);
        }
    }

    private async Task ApplySettlementAsync(
        string driverId,
        DateTime acceptedAtUtc,
        CancellationToken cancellationToken)
    {
        try
        {
            await _settlementService.배차확정반영Async(driverId, acceptedAtUtc, cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "음식 배달 확정 후 기사 월정산 반영에 실패했습니다. DriverId={DriverId}", driverId);
        }
    }

}
