using MediatR;
using System.Data;
using Ssalddel.Application.Food.Commands;
using Ssalddel.Application.Food.Events;
using Ssalddel.Contracts.Food;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Services.Food;
using Microsoft.EntityFrameworkCore;
using 살뜰.Data;
using 살뜰.Services.Dispatch.Common;

namespace Ssalddel.Application.Food.Handlers;

public sealed class 음식점주문진행변경CommandHandler(
    ISsalddelFoodOrderStore orderStore,
    IPublisher publisher,
    SsalddelContext? db = null) : IRequestHandler<음식점주문진행변경Command, 음식주문응답?>
{
    public async Task<음식주문응답?> Handle(
        음식점주문진행변경Command request,
        CancellationToken cancellationToken)
    {
        Validate(request);

        var changed = await 진행변경과거절기록Async(request, cancellationToken);
        if (changed is null)
        {
            return null;
        }

        if (changed.새로변경됨)
        {
            var history = changed.주문.상태이력
                .OrderByDescending(item => item.전이시각Utc)
                .FirstOrDefault(item => item.클라이언트요청Id == request.Payload.클라이언트요청Id);
            await publisher.Publish(
                new 음식점주문진행변경됨Event(
                    changed.주문,
                    request.처리UserId.Trim(),
                    request.Payload.작업.Trim(),
                    history?.사유 ?? request.Payload.사유.Trim(),
                    DateTime.UtcNow,
                    Guid.NewGuid().ToString("N")),
                cancellationToken);
        }

        return orderStore.GetOrder(request.주문번호) ?? changed.주문;
    }

    private async Task<음식주문변경결과?> 진행변경과거절기록Async(
        음식점주문진행변경Command request,
        CancellationToken cancellationToken)
    {
        if (db is null)
        {
            return 진행변경(request);
        }

        if (!db.Database.IsRelational())
        {
            var changed = 진행변경(request);
            if (changed?.새로변경됨 == true)
            {
                await 거절사건추가Async(changed.주문, request.Payload, cancellationToken);
            }
            return changed;
        }

        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            var changed = 진행변경(request);
            if (changed?.새로변경됨 == true)
            {
                await 거절사건추가Async(changed.주문, request.Payload, cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
            return changed;
        });
    }

    private 음식주문변경결과? 진행변경(음식점주문진행변경Command request)
        => orderStore.음식점진행변경(
            request.주문번호,
            request.Payload,
            request.처리UserId.Trim());

    private async Task 거절사건추가Async(
        음식주문응답 order,
        음식점주문진행변경요청 request,
        CancellationToken cancellationToken)
    {
        if (request.작업.Trim() != 음식점주문진행작업코드.거절)
        {
            return;
        }

        var occurredAt = order.상태이력
            .Where(x => x.클라이언트요청Id == request.클라이언트요청Id)
            .OrderByDescending(x => x.전이시각Utc)
            .Select(x => x.전이시각Utc)
            .FirstOrDefault();
        db!.운영배차활동사건.Add(운영배차활동사건Factory.음식점거절(
            order.음식점Id,
            order.주문번호,
            occurredAt == default ? DateTime.UtcNow : occurredAt,
            request.사유Code));
        await db.SaveChangesAsync(cancellationToken);
    }

    private static void Validate(음식점주문진행변경Command command)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command.주문번호);
        ArgumentNullException.ThrowIfNull(command.Payload);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.처리UserId);

        if (command.Payload.클라이언트요청Id == Guid.Empty)
        {
            throw new ArgumentException("음식점 진행 변경의 클라이언트 요청 ID가 필요합니다.");
        }

        if (!음식점주문진행작업코드.지원여부(command.Payload.작업))
        {
            throw new ArgumentException("지원하지 않는 음식점 주문 진행 작업입니다.");
        }

        if (command.Payload.작업.Trim() == 음식점주문진행작업코드.거절)
        {
            _ = 운영배차음식점거절사유Code.정규화(command.Payload.사유Code);
        }
    }
}
