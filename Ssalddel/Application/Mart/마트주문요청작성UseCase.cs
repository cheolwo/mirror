using FluentResults;
using Microsoft.EntityFrameworkCore;
using Ssalddel.ApiMetadata;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Contracts.Mart;
using 살뜰.Data;
using 살뜰.도메인.마트;

namespace Ssalddel.Application.Mart;

public interface I마트주문요청작성UseCase
{
    Task<Result<마트주문요청응답>> 등록Async(
        마트주문요청등록요청 request,
        CancellationToken cancellationToken);

    Task<Result<마트주문요청응답>> 수량변경Async(
        Guid orderRequestId,
        마트주문요청수량변경요청 request,
        CancellationToken cancellationToken);

    Task<Result<마트주문요청응답>> 철회Async(
        Guid orderRequestId,
        마트주문요청철회요청 request,
        CancellationToken cancellationToken);
}

[SsalddelApiWorkflow(SsalddelWorkflow.SsalddelMart)]
[SsalddelUseCase(
    "마트 주문 요청 작성",
    Summary = "공개 상품 한 건의 비구속 주문 의향을 서버 검증과 사용자별 요청 ID 멱등성으로 저장하고, 주문 확정 전에는 본인이 수량을 변경하거나 철회할 수 있게 하며 재고·결제·출고 원장은 변경하지 않습니다.")]
[SsalddelUseCaseActor(SsalddelActor.Orderer)]
public sealed class 마트주문요청작성UseCase(
    SsalddelContext db,
    ICurrentUserAccessor currentUserAccessor) : I마트주문요청작성UseCase
{
    public async Task<Result<마트주문요청응답>> 등록Async(
        마트주문요청등록요청 request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var userId = currentUserAccessor.UserId?.Trim();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return 마트주문요청Results.Unauthorized<마트주문요청응답>();
        }

        if (request.클라이언트요청Id == Guid.Empty)
        {
            return 마트주문요청Results.BadRequest<마트주문요청응답>("마트 주문 요청 ID를 확인해 주세요.");
        }

        if (request.공개상품Id <= 0)
        {
            return 마트주문요청Results.BadRequest<마트주문요청응답>("주문 요청할 공개 상품 ID를 확인해 주세요.");
        }

        if (request.수량 is < 1 or > 100)
        {
            return 마트주문요청Results.BadRequest<마트주문요청응답>("주문 요청 수량은 1개 이상 100개 이하로 입력해 주세요.");
        }

        var existing = await FindByClientRequestAsync(userId, request.클라이언트요청Id, cancellationToken);
        if (existing is not null)
        {
            return existing.공개상품Id == request.공개상품Id && existing.수량 == request.수량
                ? Result.Ok(마트주문요청Mapper.ToResponse(existing))
                : 마트주문요청Results.Conflict<마트주문요청응답>(
                    "같은 요청 ID를 다른 상품이나 수량에 다시 사용할 수 없습니다.");
        }

        if (!마트주문요청안내.유효한확인(request))
        {
            return 마트주문요청Results.BadRequest<마트주문요청응답>(
                "현재 마트 주문 요청 안내를 확인하고 비구속 저장에 동의해 주세요.");
        }

        var product = await db.마트공개상품
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == request.공개상품Id && item.공개여부, cancellationToken);
        if (product is null)
        {
            return 마트주문요청Results.ProductNotFound<마트주문요청응답>();
        }

        if (!product.판매허용여부 || product.판매가능수량 <= 0)
        {
            return 마트주문요청Results.Conflict<마트주문요청응답>(
                "현재 판매 가능한 상품이 아닙니다. 공개 상품 상태를 다시 확인해 주세요.");
        }

        if (request.수량 > product.판매가능수량)
        {
            return 마트주문요청Results.Conflict<마트주문요청응답>(
                $"현재 표시 가능한 수량은 {product.판매가능수량:N0}개입니다. 수량을 줄여 다시 요청해 주세요.");
        }

        var now = DateTime.UtcNow;
        var orderRequest = new 마트주문요청
        {
            Id = Guid.NewGuid(),
            요청자UserId = userId,
            클라이언트요청Id = request.클라이언트요청Id,
            공개상품Id = product.Id,
            상품명Snapshot = product.상품명.Trim(),
            판매단위Snapshot = product.판매단위.Trim(),
            단가Snapshot = product.판매가,
            수량 = request.수량,
            합계Snapshot = product.판매가 * request.수량,
            통화 = "KRW",
            제출시판매가능수량 = product.판매가능수량,
            재고기준시각Utc = product.재고기준시각Utc,
            상태코드 = 마트주문요청상태코드.제출됨,
            비구속주문요청확인 = true,
            안내버전 = 마트주문요청안내.현재버전,
            CreatedAtUtc = now
        };
        db.마트주문요청.Add(orderRequest);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            db.Entry(orderRequest).State = EntityState.Detached;
            var concurrentlyCreated = await FindByClientRequestAsync(
                userId,
                request.클라이언트요청Id,
                cancellationToken);
            if (concurrentlyCreated is null)
            {
                throw;
            }

            return concurrentlyCreated.공개상품Id == request.공개상품Id
                   && concurrentlyCreated.수량 == request.수량
                ? Result.Ok(마트주문요청Mapper.ToResponse(concurrentlyCreated))
                : 마트주문요청Results.Conflict<마트주문요청응답>(
                    "같은 요청 ID를 다른 상품이나 수량에 다시 사용할 수 없습니다.");
        }

        return Result.Ok(마트주문요청Mapper.ToResponse(orderRequest));
    }

    public async Task<Result<마트주문요청응답>> 수량변경Async(
        Guid orderRequestId,
        마트주문요청수량변경요청 request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var userId = currentUserAccessor.UserId?.Trim();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return 마트주문요청Results.Unauthorized<마트주문요청응답>();
        }

        if (orderRequestId == Guid.Empty)
        {
            return 마트주문요청Results.BadRequest<마트주문요청응답>(
                "변경할 마트 주문 요청 ID를 확인해 주세요.");
        }

        if (request.수량 is < 1 or > 100)
        {
            return 마트주문요청Results.BadRequest<마트주문요청응답>(
                "주문 요청 수량은 1개 이상 100개 이하로 입력해 주세요.");
        }

        if (!request.비구속주문요청확인
            || !string.Equals(request.안내버전, 마트주문요청안내.현재버전, StringComparison.Ordinal))
        {
            return 마트주문요청Results.BadRequest<마트주문요청응답>(
                "현재 마트 주문 요청 안내를 확인하고 비구속 변경에 동의해 주세요.");
        }

        var orderRequest = await FindOwnedAsync(orderRequestId, userId, cancellationToken);
        if (orderRequest is null)
        {
            return 마트주문요청Results.NotFound<마트주문요청응답>();
        }

        if (!string.Equals(orderRequest.상태코드, request.기대상태코드, StringComparison.Ordinal))
        {
            return 마트주문요청Results.Conflict<마트주문요청응답>(
                "마트 주문 요청 상태가 먼저 변경되었습니다. 최신 요청을 다시 조회해 주세요.");
        }

        if (!string.Equals(orderRequest.상태코드, 마트주문요청상태코드.제출됨, StringComparison.Ordinal))
        {
            return 마트주문요청Results.Conflict<마트주문요청응답>(
                "철회되었거나 주문 확정이 시작된 요청의 수량은 변경할 수 없습니다.");
        }

        var product = await db.마트공개상품
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.Id == orderRequest.공개상품Id && item.공개여부,
                cancellationToken);
        if (product is null)
        {
            return 마트주문요청Results.ProductNotFound<마트주문요청응답>();
        }

        if (!product.판매허용여부 || product.판매가능수량 <= 0)
        {
            return 마트주문요청Results.Conflict<마트주문요청응답>(
                "현재 판매 가능한 상품이 아닙니다. 공개 상품 상태를 다시 확인해 주세요.");
        }

        if (request.수량 > product.판매가능수량)
        {
            return 마트주문요청Results.Conflict<마트주문요청응답>(
                $"현재 표시 가능한 수량은 {product.판매가능수량:N0}개입니다. 수량을 줄여 다시 요청해 주세요.");
        }

        if (db.Database.IsRelational())
        {
            var originalQuantity = orderRequest.수량;
            var originalPrice = orderRequest.단가Snapshot;
            var changed = await db.마트주문요청.Where(x => x.Id == orderRequestId && x.요청자UserId == userId
                    && x.상태코드 == 마트주문요청상태코드.제출됨 && x.수량 == originalQuantity && x.단가Snapshot == originalPrice)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.상품명Snapshot, product.상품명.Trim())
                    .SetProperty(x => x.판매단위Snapshot, product.판매단위.Trim()).SetProperty(x => x.단가Snapshot, product.판매가)
                    .SetProperty(x => x.수량, request.수량).SetProperty(x => x.합계Snapshot, product.판매가 * request.수량)
                    .SetProperty(x => x.제출시판매가능수량, product.판매가능수량).SetProperty(x => x.재고기준시각Utc, product.재고기준시각Utc)
                    .SetProperty(x => x.비구속주문요청확인, true).SetProperty(x => x.안내버전, 마트주문요청안내.현재버전), cancellationToken);
            if (changed != 1) return 마트주문요청Results.Conflict<마트주문요청응답>("주문 확정 또는 수량 변경이 먼저 처리되었습니다. 원 요청을 다시 확인해 주세요.");
            var saved = await db.마트주문요청.AsNoTracking().SingleAsync(x => x.Id == orderRequestId && x.요청자UserId == userId, cancellationToken);
            db.Entry(orderRequest).State = EntityState.Detached;
            return Result.Ok(마트주문요청Mapper.ToResponse(saved));
        }
        // 비관계형 시험 저장소의 기존 계약을 유지합니다. 주문 확정 인계는 관계형 저장소에서만 열립니다.
        orderRequest.상품명Snapshot = product.상품명.Trim();
        orderRequest.판매단위Snapshot = product.판매단위.Trim();
        orderRequest.단가Snapshot = product.판매가;
        orderRequest.수량 = request.수량;
        orderRequest.합계Snapshot = product.판매가 * request.수량;
        orderRequest.제출시판매가능수량 = product.판매가능수량;
        orderRequest.재고기준시각Utc = product.재고기준시각Utc;
        orderRequest.비구속주문요청확인 = true;
        orderRequest.안내버전 = 마트주문요청안내.현재버전;
        await db.SaveChangesAsync(cancellationToken);

        return Result.Ok(마트주문요청Mapper.ToResponse(orderRequest));
    }

    public async Task<Result<마트주문요청응답>> 철회Async(
        Guid orderRequestId,
        마트주문요청철회요청 request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var userId = currentUserAccessor.UserId?.Trim();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return 마트주문요청Results.Unauthorized<마트주문요청응답>();
        }

        if (orderRequestId == Guid.Empty)
        {
            return 마트주문요청Results.BadRequest<마트주문요청응답>(
                "철회할 마트 주문 요청 ID를 확인해 주세요.");
        }

        var orderRequest = await FindOwnedAsync(orderRequestId, userId, cancellationToken);
        if (orderRequest is null)
        {
            return 마트주문요청Results.NotFound<마트주문요청응답>();
        }

        if (string.Equals(orderRequest.상태코드, 마트주문요청상태코드.철회됨, StringComparison.Ordinal))
        {
            return Result.Ok(마트주문요청Mapper.ToResponse(orderRequest));
        }

        if (!string.Equals(orderRequest.상태코드, request.기대상태코드, StringComparison.Ordinal))
        {
            return 마트주문요청Results.Conflict<마트주문요청응답>(
                "마트 주문 요청 상태가 먼저 변경되었습니다. 최신 요청을 다시 조회해 주세요.");
        }

        if (orderRequest.상태코드 != 마트주문요청상태코드.제출됨)
            return 마트주문요청Results.Conflict<마트주문요청응답>("주문 확정이 시작된 요청은 여기에서 철회할 수 없습니다. 연결된 전문 주문에서 확인해 주세요.");

        if (db.Database.IsRelational())
        {
            var changed = await db.마트주문요청.Where(x => x.Id == orderRequestId && x.요청자UserId == userId
                    && x.상태코드 == 마트주문요청상태코드.제출됨 && x.수량 == orderRequest.수량 && x.단가Snapshot == orderRequest.단가Snapshot)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.상태코드, 마트주문요청상태코드.철회됨), cancellationToken);
            if (changed != 1) return 마트주문요청Results.Conflict<마트주문요청응답>("주문 확정 또는 수량 변경이 먼저 처리되었습니다. 원 요청을 다시 확인해 주세요.");
            var saved = await db.마트주문요청.AsNoTracking().SingleAsync(x => x.Id == orderRequestId && x.요청자UserId == userId, cancellationToken);
            db.Entry(orderRequest).State = EntityState.Detached;
            return Result.Ok(마트주문요청Mapper.ToResponse(saved));
        }

        orderRequest.상태코드 = 마트주문요청상태코드.철회됨;
        await db.SaveChangesAsync(cancellationToken);
        return Result.Ok(마트주문요청Mapper.ToResponse(orderRequest));
    }

    private Task<마트주문요청?> FindOwnedAsync(
        Guid orderRequestId,
        string userId,
        CancellationToken cancellationToken)
        => db.마트주문요청.SingleOrDefaultAsync(
            item => item.Id == orderRequestId && item.요청자UserId == userId,
            cancellationToken);

    private Task<마트주문요청?> FindByClientRequestAsync(
        string userId,
        Guid clientRequestId,
        CancellationToken cancellationToken)
        => db.마트주문요청
            .AsNoTracking()
            .SingleOrDefaultAsync(item =>
                item.요청자UserId == userId
                && item.클라이언트요청Id == clientRequestId,
                cancellationToken);
}
