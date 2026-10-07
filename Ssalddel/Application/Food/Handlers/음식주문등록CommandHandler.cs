using Ssalddel.Application.Food.Commands;
using Ssalddel.Application.Food.Events;
using Ssalddel.Contracts.Food;
using Ssalddel.Services.Food;
using MediatR;
using Microsoft.EntityFrameworkCore;
using 살뜰.Data;
using 살뜰.Services.Dispatch.Common;

namespace Ssalddel.Application.Food.Handlers;

public sealed class 음식주문등록CommandHandler(
    ISsalddelFoodOrderStore orderStore,
    I음식주문메뉴검증Service menuValidationService,
    IPublisher publisher,
    SsalddelContext? db = null,
    Ssalddel.Services.Commerce.I통신판매거래Guard? commerce = null) : IRequestHandler<음식주문등록Command, 음식주문응답>
{
    public async Task<음식주문응답> Handle(음식주문등록Command request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request.Payload);
        cancellationToken.ThrowIfCancellationRequested();
        ValidateIdentity(request.Payload);
        // 접수된 동일 요청은 이후의 메뉴 변경과 무관하게 원래 원장으로 응답합니다.
        var existing = orderStore.접수주문조회(request.Payload.주문자UserId, request.Payload.클라이언트요청Id);
        if (existing is not null) return existing;
        Validate(request.Payload);
        var canonicalRequest = await menuValidationService.서버기준요청생성Async(
            request.Payload,
            cancellationToken);

        if (commerce is not null)
            await commerce.음식주문요구Async(canonicalRequest.주문자UserId, canonicalRequest.음식점Id,
                request.Payload.CommerceProtection, canonicalRequest.클라이언트요청Id.ToString("N"), cancellationToken);
        var saveResult = await 등록과음식점제안기록Async(canonicalRequest, cancellationToken);
        var order = saveResult.주문;

        if (saveResult.새로생성됨)
        {
            await publisher.Publish(
                new 음식주문등록됨Event(order, DateTime.UtcNow, Guid.NewGuid().ToString("N")),
                cancellationToken);
        }

        return orderStore.GetOrder(order.주문번호) ?? order;
    }

    private async Task<음식주문저장결과> 등록과음식점제안기록Async(
        음식주문등록요청 request,
        CancellationToken cancellationToken)
    {
        if (db is null)
        {
            return orderStore.멱등등록(request);
        }

        if (!db.Database.IsRelational())
        {
            var saved = orderStore.멱등등록(request);
            if (saved.새로생성됨)
            {
                db.운영배차활동사건.Add(운영배차활동사건Factory.음식점유효주문제안(
                    saved.주문.음식점Id,
                    saved.주문.주문번호,
                    saved.주문.CreatedAt));
                await db.SaveChangesAsync(cancellationToken);
            }
            return saved;
        }

        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var saved = orderStore.멱등등록(request);
            if (saved.새로생성됨)
            {
                db.운영배차활동사건.Add(운영배차활동사건Factory.음식점유효주문제안(
                    saved.주문.음식점Id,
                    saved.주문.주문번호,
                    saved.주문.CreatedAt));
                await db.SaveChangesAsync(cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
            return saved;
        });
    }

    private static void ValidateIdentity(음식주문등록요청 request)
    {
        if (request.클라이언트요청Id == Guid.Empty)
        {
            throw new 음식주문입력확인Exception("클라이언트요청Id가 필요합니다.");
        }

        if (string.IsNullOrWhiteSpace(request.주문자UserId))
        {
            throw new 음식주문입력확인Exception("주문자UserId가 필요합니다.");
        }
    }

    private static void Validate(음식주문등록요청 request)
    {
        if (request.음식점Id <= 0)
            throw new 음식주문입력확인Exception("음식점Id가 필요합니다.");

        if (request.상품목록 is null || request.상품목록.Count == 0)
        {
            throw new 음식주문입력확인Exception("상품목록이 필요합니다.");
        }

        if (request.상품목록.Any(x => x is null || x.메뉴Id is null or <= 0 || x.수량 <= 0))
        {
            throw new 음식주문입력확인Exception("공개 메뉴 ID와 수량을 확인해 주세요.");
        }

        if (request.수령인정보 is null)
            throw new 음식주문입력확인Exception("수령인 정보를 입력해 주세요.");

        if (string.IsNullOrWhiteSpace(request.수령인정보.수령인명))
        {
            throw new 음식주문입력확인Exception("수령인 이름이 필요합니다.");
        }

        if (string.IsNullOrWhiteSpace(request.수령인정보.연락처))
        {
            throw new 음식주문입력확인Exception("수령인 연락처가 필요합니다.");
        }

        if (string.IsNullOrWhiteSpace(request.수령인정보.주소))
        {
            throw new 음식주문입력확인Exception("수령지 주소가 필요합니다.");
        }
    }
}
