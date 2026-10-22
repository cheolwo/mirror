using System.Text.Json;
using FluentResults;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Ssalddel.ApiMetadata;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Application.Warehouse.Events;
using Ssalddel.Contracts.Common.Inventory;
using Ssalddel.Services.LogisticsProcessing.Warehouse;
using 살뜰.Data;
using 살뜰.Services.Audit;
using 살뜰.도메인.창고;

namespace Ssalddel.Application.Warehouse;

public interface I출고인계준비UseCase
{
    Task<Result<출고인계준비목록페이지응답>> 목록Async(출고인계준비목록조회요청 request, CancellationToken cancellationToken);
    Task<Result<출고인계준비상세응답>> 상세Async(long inboundItemId, CancellationToken cancellationToken);
    Task<Result<출고인계준비결과응답>> 완료Async(long inboundItemId, 출고인계준비완료요청 request, 창고작업요청Context context, CancellationToken cancellationToken);
}

[SsalddelApiWorkflow(SsalddelWorkflow.WarehouseFulfillment)]
[SsalddelUseCase("창고 출고 인계 준비", Summary = "포장 완료 재고의 전체 가용수량을 출고예정 원장에 한 번만 기록하며 운송의뢰나 재고 예약은 만들지 않습니다.")]
[SsalddelUseCaseActor(SsalddelActor.WarehouseManager)]
public sealed class 출고인계준비UseCase(
    SsalddelContext db,
    ICurrentUserAccessor currentUserAccessor,
    I사용자행위로그Service activityLogService,
    IPublisher publisher) : I출고인계준비UseCase
{
    public async Task<Result<출고인계준비목록페이지응답>> 목록Async(출고인계준비목록조회요청 request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized<출고인계준비목록페이지응답>();
        var page = Math.Max(0, request.Page); var pageSize = Math.Clamp(request.PageSize, 1, 50);
        var status = 출고인계준비조회상태코드.Normalize(request.Status);
        var query = 접근가능재고Query(userId).AsNoTracking().Where(item => item.가용수량 > 0 && item.상태.StartsWith("포장완료-"));
        query = status switch
        {
            출고인계준비조회상태코드.대기 => query.Where(item => !db.출고예정.Any(plan => plan.입고상품Id == item.Id && plan.상태 != 출고상태.취소)),
            출고인계준비조회상태코드.완료 => query.Where(item => db.출고예정.Any(plan => plan.입고상품Id == item.Id && plan.상태 != 출고상태.취소)),
            _ => query
        };
        if (request.WarehouseId is > 0) query = query.Where(item => item.창고Id == request.WarehouseId.Value);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(item => item.상품명.Contains(search) || item.SKU.Contains(search) || item.옵션명.Contains(search)
                || item.보관위치.Contains(search) || db.입고요청.Any(inbound => inbound.Id == item.입고요청Id && inbound.주문참조번호.Contains(search)));
        }
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await (from item in query
                join warehouse in db.창고.AsNoTracking() on item.창고Id equals warehouse.Id
                let plan = db.출고예정.Where(candidate => candidate.입고상품Id == item.Id && candidate.상태 != 출고상태.취소)
                    .OrderByDescending(candidate => candidate.CreatedAt).ThenByDescending(candidate => candidate.Id).FirstOrDefault()
                orderby item.UpdatedAt, item.Id
                select new 출고인계준비목록항목응답
                {
                    InboundItemId = item.Id, OutboundPlanId = plan == null ? null : plan.Id, WarehouseId = item.창고Id,
                    WarehouseName = warehouse.창고명, ProductName = item.상품명, Sku = item.SKU,
                    HandoffQuantity = plan == null ? item.가용수량 : plan.수량, StorageLocation = item.보관위치,
                    PackagingType = PackagingType(item.상태), IsHandoffReady = plan != null,
                    UpdatedAtUtc = AsUtc(plan == null ? item.UpdatedAt : plan.UpdatedAt)
                }).Skip(page * pageSize).Take(pageSize).ToArrayAsync(cancellationToken);
        return Result.Ok(new 출고인계준비목록페이지응답 { Items = items, TotalCount = totalCount, Page = page, PageSize = pageSize });
    }

    public async Task<Result<출고인계준비상세응답>> 상세Async(long inboundItemId, CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized<출고인계준비상세응답>();
        if (inboundItemId <= 0) return NotFound<출고인계준비상세응답>();
        var row = await (from item in 접근가능재고Query(userId).AsNoTracking()
                join warehouse in db.창고.AsNoTracking() on item.창고Id equals warehouse.Id
                join inbound in db.입고요청.AsNoTracking() on item.입고요청Id equals inbound.Id
                where item.Id == inboundItemId && item.가용수량 > 0 && item.상태.StartsWith("포장완료-")
                select new { item, warehouse.창고명, inbound.보관조건, inbound.주문참조번호 })
            .SingleOrDefaultAsync(cancellationToken);
        if (row is null) return NotFound<출고인계준비상세응답>();
        var plan = await db.출고예정.AsNoTracking().Where(candidate => candidate.입고상품Id == inboundItemId && candidate.상태 != 출고상태.취소)
            .OrderByDescending(candidate => candidate.CreatedAt).ThenByDescending(candidate => candidate.Id).FirstOrDefaultAsync(cancellationToken);
        var histories = await db.재고이력.AsNoTracking().Where(history => history.입고상품Id == inboundItemId && (history.이력유형 == "포장" || history.이력유형 == "출고인계준비"))
            .OrderByDescending(history => history.처리일시).ThenByDescending(history => history.Id)
            .Select(history => new { history.이력유형, history.처리일시, history.메모 }).ToArrayAsync(cancellationToken);
        var packing = histories.FirstOrDefault(history => history.이력유형 == "포장");
        var handoff = histories.FirstOrDefault(history => history.이력유형 == "출고인계준비");
        return Result.Ok(new 출고인계준비상세응답
        {
            InboundItemId = row.item.Id, InboundId = row.item.입고요청Id, OutboundPlanId = plan?.Id,
            WarehouseId = row.item.창고Id, WarehouseName = row.창고명, ProductName = row.item.상품명, Sku = row.item.SKU,
            OptionName = row.item.옵션명, AvailableQuantity = row.item.가용수량, ReservedQuantity = row.item.예약수량,
            DefectiveQuantity = row.item.불량수량, InventoryStatus = row.item.상태, StorageLocation = row.item.보관위치,
            StorageCondition = row.보관조건, OrderReference = row.주문참조번호, PackagingType = PackagingType(row.item.상태),
            PackedAtUtc = AsUtc(packing?.처리일시), PackingMemo = packing?.메모 ?? string.Empty,
            OutboundStatus = plan?.상태 ?? string.Empty, HandoffReadyAtUtc = AsUtc(handoff?.처리일시 ?? plan?.CreatedAt),
            HandoffMemo = handoff?.메모 ?? string.Empty, CanConfirmHandoff = plan is null, UpdatedAtUtc = AsUtc(plan?.UpdatedAt ?? row.item.UpdatedAt)
        });
    }

    public Task<Result<출고인계준비결과응답>> 완료Async(long inboundItemId, 출고인계준비완료요청 request, 창고작업요청Context context, CancellationToken cancellationToken)
        => 창고업무진행알림.저장단위Async(db, committed => 완료저장Async(inboundItemId, request, context, cancellationToken, committed));

    private async Task<Result<출고인계준비결과응답>> 완료저장Async(long inboundItemId, 출고인계준비완료요청 request, 창고작업요청Context context, CancellationToken cancellationToken, Action committed)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!request.PackageSealConfirmed || !request.TransportConditionsConfirmed)
            return Invalid<출고인계준비결과응답>("포장 봉인과 운송 인계 조건 확인을 모두 완료해 주세요.");
        var memo = request.Memo?.Trim() ?? string.Empty;
        if (memo.Length > 400) return Invalid<출고인계준비결과응답>("출고 인계 메모는 400자 이하로 입력해 주세요.");
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized<출고인계준비결과응답>();
        await using var transaction = db.Database.IsRelational() && db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken) : null;
        var row = await (from item in 접근가능재고Query(userId)
                join inbound in db.입고요청 on item.입고요청Id equals inbound.Id
                where item.Id == inboundItemId
                select new { item, inbound }).SingleOrDefaultAsync(cancellationToken);
        if (row is null) return NotFound<출고인계준비결과응답>();
        if (!row.item.상태.StartsWith("포장완료-", StringComparison.Ordinal))
            return Conflict<출고인계준비결과응답>("포장 완료 상태의 재고만 출고 인계 준비를 확정할 수 있습니다.");
        var existing = await db.출고예정.Where(candidate => candidate.입고상품Id == inboundItemId && candidate.상태 != 출고상태.취소)
            .OrderByDescending(candidate => candidate.CreatedAt).ThenByDescending(candidate => candidate.Id).FirstOrDefaultAsync(cancellationToken);
        if (existing is not null)
        {
            if (existing.수량 != request.HandoffQuantity)
                return Conflict<출고인계준비결과응답>("이미 준비 완료된 출고 수량 변경은 별도 출고예정 조정 업무에서 처리해 주세요.");
            return Result.Ok(ToResult(row.item.Id, existing, existing.CreatedAt, true));
        }
        if (request.HandoffQuantity <= 0 || request.HandoffQuantity != row.item.가용수량)
            return Conflict<출고인계준비결과응답>("부분 인계 상태를 만들지 않도록 현재 전체 가용수량과 같은 수량을 확인해 주세요.");

        var now = DateTime.UtcNow;
        var plan = new 출고예정
        {
            주문참조번호 = row.inbound.주문참조번호, 입고상품Id = row.item.Id,
            판매자UserId = row.inbound.판매자UserId, 주문자UserId = row.inbound.주문자UserId,
            출고창고Id = row.item.창고Id, 상품명 = row.item.상품명, SKU = row.item.SKU,
            수량 = request.HandoffQuantity, 상태 = 출고상태.준비중, 입고요청Id = row.item.입고요청Id,
            커뮤니티원장Id = row.inbound.커뮤니티원장Id, 커뮤니티원장템플릿Key = row.inbound.커뮤니티원장템플릿Key,
            커뮤니티원장상태 = row.inbound.커뮤니티원장상태, CreatedAt = now, UpdatedAt = now
        };
        db.출고예정.Add(plan);
        var historyMemo = string.IsNullOrWhiteSpace(memo) ? $"출고 인계 준비 {request.HandoffQuantity}개" : $"출고 인계 준비 {request.HandoffQuantity}개. {memo}";
        db.재고이력.Add(new 재고이력
        {
            입고상품Id = row.item.Id, 이력유형 = "출고인계준비", 변경수량 = 0, 변경후수량 = row.item.가용수량,
            원인유형 = "출고예정", 원인Id = row.item.입고요청Id, 처리UserId = userId, 메모 = historyMemo, 처리일시 = now
        });
        var progress = await 창고업무진행알림.출고Async(db, plan, row.item, "warehouse.ready", userId, now, cancellationToken);
        try { await db.SaveChangesAsync(cancellationToken); }
        catch { 창고업무진행알림.분리(db, progress); throw; }
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            committed();
            // 커밋한 transaction을 먼저 해제하여 감사/Event가 새 저장 경계를 사용하도록 한다.
            await transaction.DisposeAsync();
        }
        await LogAsync(row.item, plan, context, cancellationToken);
        await publisher.Publish(
            new 창고출고인계준비완료됨Event(
                context.UserId,
                context.RoleName,
                row.item.Id,
                plan.Id,
                plan.수량,
                context.Route,
                context.TraceId,
                now,
                context.AppKey,
                plan.주문참조번호,
                plan.입고요청Id,
                plan.커뮤니티원장Id ?? string.Empty),
            cancellationToken);
        return Result.Ok(ToResult(row.item.Id, plan, now, false));
    }

    private IQueryable<입고상품> 접근가능재고Query(string userId)
    {
        var query = db.입고상품.AsQueryable();
        if (string.Equals(currentUserAccessor.Role, 역할명.서버관리자, StringComparison.OrdinalIgnoreCase)) return query;
        return query.Where(item => db.창고.Any(warehouse => warehouse.Id == item.창고Id && warehouse.소유자UserId == userId)
            || db.창고사용자.Any(warehouseUser => warehouseUser.창고Id == item.창고Id && warehouseUser.UserId == userId));
    }
    private async Task LogAsync(입고상품 item, 출고예정 plan, 창고작업요청Context context, CancellationToken cancellationToken)
        => await activityLogService.기록Async(new 사용자행위로그기록
        {
            AppKey = context.AppKey, UserId = context.UserId, UserName = context.UserName, RoleName = context.RoleName,
            ActionType = "WarehouseOutboundHandoff", ActionName = "Prepared", Route = context.Route, TraceId = context.TraceId,
            IsSuccess = true, ClientIp = context.ClientIp, UserAgent = context.UserAgent, OccurredAtUtc = DateTime.UtcNow,
            MetadataJson = JsonSerializer.Serialize(new { inboundItemId = item.Id, outboundPlanId = plan.Id, warehouseId = item.창고Id, handoffQuantity = plan.수량 })
        }, cancellationToken);
    private static 출고인계준비결과응답 ToResult(long itemId, 출고예정 plan, DateTime occurredAt, bool replay) => new()
    {
        InboundItemId = itemId, OutboundPlanId = plan.Id, OutboundStatus = plan.상태, HandoffQuantity = plan.수량,
        NextStep = "별도 운송의뢰 생성에서 하차지·차량·결제 조건 확인", HandoffReadyAtUtc = AsUtc(occurredAt), IdempotentReplay = replay
    };
    private static string PackagingType(string status) => status.StartsWith("포장완료-", StringComparison.Ordinal) ? status[5..] : string.Empty;
    private string? CurrentUserId() { var value = currentUserAccessor.UserId?.Trim(); return string.IsNullOrWhiteSpace(value) ? null : value; }
    private static DateTime AsUtc(DateTime value) => value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);
    private static DateTime? AsUtc(DateTime? value) => value.HasValue ? AsUtc(value.Value) : null;
    private static Result<T> Unauthorized<T>() => Result.Fail<T>(new Error("로그인 사용자 인증 정보가 필요합니다.").WithMetadata("StatusCode", StatusCodes.Status401Unauthorized));
    private static Result<T> NotFound<T>() => Result.Fail<T>(new Error("출고 인계 준비 대상을 찾을 수 없거나 현재 계정의 창고 작업 범위에 없습니다.").WithMetadata("StatusCode", StatusCodes.Status404NotFound));
    private static Result<T> Invalid<T>(string message) => Result.Fail<T>(new Error(message).WithMetadata("StatusCode", StatusCodes.Status400BadRequest));
    private static Result<T> Conflict<T>(string message) => Result.Fail<T>(new Error(message).WithMetadata("StatusCode", StatusCodes.Status409Conflict));
}
