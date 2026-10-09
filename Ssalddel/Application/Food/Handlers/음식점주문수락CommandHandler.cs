using Ssalddel.Application.Food.Commands;
using Ssalddel.Application.Food.Events;
using Ssalddel.Contracts.Food;
using Ssalddel.Services.Food;
using MediatR;
using Microsoft.EntityFrameworkCore;
using 살뜰.Data;
using 살뜰.Services.Dispatch.Common;
using Ssalddel.Contracts.Common.Fulfillment;
using Ssalddel.Services.Fulfillment;

namespace Ssalddel.Application.Food.Handlers;

public sealed class 음식점주문수락CommandHandler(
    ISsalddelFoodOrderStore orderStore,
    IPublisher publisher,
    SsalddelContext? db = null,
    I음식배차요청OutboxService? dispatchOutbox = null,
    I음식점조리시간Service? preparationTimeService = null,
    I자동수행Coordinator? fulfillment = null) : IRequestHandler<음식점주문수락Command, 음식주문응답?>
{
    public async Task<음식주문응답?> Handle(음식점주문수락Command request, CancellationToken cancellationToken)
    {
        Validate(request);

        var actorUserId = NormalizeUserId(request.처리UserId)
            ?? throw new ArgumentException("인증된 음식점 처리 사용자 ID가 필요합니다.");
        var before = orderStore.GetOrder(request.주문번호);
        if (db is not null && fulfillment?.Enabled != true
            && await db.음식주문.AsNoTracking().AnyAsync(x => x.주문번호 == request.주문번호 && x.모의처리처수행, cancellationToken))
            throw new InvalidOperationException("자동 수행 기능이 중지되어 식당 확인과 수행 예약을 시작할 수 없습니다. 기존 주문 기록은 유지합니다.");
        var preparationDecision = preparationTimeService is null || before is null
            ? null
            : await preparationTimeService.주문수락값결정Async(
                request.주문번호,
                before.음식점Id,
                request.Payload.조리예상분,
                request.Payload.즉시픽업가능여부,
                DateTime.UtcNow,
                cancellationToken);
        if (preparationDecision is not null)
        {
            request.Payload.조리예상분 = preparationDecision.적용조리분;
        }
        var eventId = Guid.NewGuid().ToString("N");
        var accepted = db is not null
            ? await AcceptAndRecordAsync(request, actorUserId, eventId, preparationDecision, cancellationToken)
            : orderStore.음식점수락멱등(request.주문번호, request.Payload, actorUserId);
        if (accepted is null)
        {
            return null;
        }

        if (accepted.새로변경됨)
        {
            await publisher.Publish(
                new 음식점주문수락됨Event(
                    accepted.주문,
                    actorUserId,
                    DateTime.UtcNow,
                    eventId),
                cancellationToken);
        }

        var response = orderStore.GetOrder(request.주문번호) ?? accepted.주문;
        if (fulfillment is not null && response.AutoFulfillment is not null && !response.AutoFulfillment.ExecutionPaused)
            response.AutoFulfillment = await fulfillment.상태Async(new(FulfillmentSourceKinds.FoodOrder, request.주문번호), cancellationToken);
        return response;
    }

    private async Task<음식주문변경결과?> AcceptAndRecordAsync(
        음식점주문수락Command request,
        string actorUserId,
        string eventId,
        음식조리시간결정? preparationDecision,
        CancellationToken cancellationToken)
    {
        if (!db!.Database.IsRelational())
        {
            var inMemory = orderStore.음식점수락멱등(request.주문번호, request.Payload, actorUserId);
            if (inMemory?.새로변경됨 == true)
            {
                if (dispatchOutbox is not null)
                {
                    await dispatchOutbox.예약Async(inMemory.주문, actorUserId, eventId, cancellationToken);
                }
                db.운영배차활동사건.Add(운영배차활동사건Factory.음식점수락(
                    inMemory.주문.음식점Id,
                    inMemory.주문.주문번호,
                    inMemory.주문.음식점수락시각Utc ?? DateTime.UtcNow));
                if (preparationDecision is not null && preparationTimeService is not null)
                {
                    await preparationTimeService.주문결정기록Async(request.주문번호, preparationDecision, cancellationToken);
                }
                await db.SaveChangesAsync(cancellationToken);
            }
            return inMemory;
        }

        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var accepted = orderStore.음식점수락멱등(request.주문번호, request.Payload, actorUserId);
            if (accepted?.새로변경됨 == true)
            {
                var automatic = await db.음식주문.AsNoTracking().AnyAsync(x => x.주문번호 == request.주문번호
                    && x.모의처리처수행, cancellationToken);
                if (automatic)
                {
                    if (fulfillment is null) throw new InvalidOperationException("자동 수행 요청의 Coordinator를 사용할 수 없습니다.");
                    await fulfillment.예약Async(new(FulfillmentSourceKinds.FoodOrder, request.주문번호),
                        request.Payload.클라이언트요청Id, cancellationToken);
                }
                else if (dispatchOutbox is not null)
                {
                    await dispatchOutbox.예약Async(accepted.주문, actorUserId, eventId, cancellationToken);
                }
                db.운영배차활동사건.Add(운영배차활동사건Factory.음식점수락(
                    accepted.주문.음식점Id,
                    accepted.주문.주문번호,
                    accepted.주문.음식점수락시각Utc ?? DateTime.UtcNow));
                if (preparationDecision is not null && preparationTimeService is not null)
                {
                    await preparationTimeService.주문결정기록Async(request.주문번호, preparationDecision, cancellationToken);
                }
                await db.SaveChangesAsync(cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
            return accepted;
        });
    }

    private static void Validate(음식점주문수락Command command)
    {
        if (string.IsNullOrWhiteSpace(command.주문번호))
        {
            throw new ArgumentException("주문번호가 필요합니다.");
        }

        if (command.Payload is null)
        {
            throw new ArgumentException("음식점 수락 요청 본문이 필요합니다.");
        }

        if (command.Payload.클라이언트요청Id == Guid.Empty)
        {
            throw new ArgumentException("음식점 주문 수락의 클라이언트 요청 ID가 필요합니다.");
        }
    }

    private static string? NormalizeUserId(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
