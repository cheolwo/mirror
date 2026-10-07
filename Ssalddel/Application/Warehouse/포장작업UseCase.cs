using System.Text.Json;
using FluentResults;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Ssalddel.ApiMetadata;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Application.Warehouse.Events;
using Ssalddel.Contracts.Common.Inventory;
using 살뜰.Data;
using 살뜰.Services.Audit;
using 살뜰.도메인.창고;

namespace Ssalddel.Application.Warehouse;

public interface I포장작업UseCase
{
    Task<Result<포장작업목록페이지응답>> 목록Async(포장작업목록조회요청 request, CancellationToken cancellationToken);
    Task<Result<포장작업상세응답>> 상세Async(long inboundItemId, CancellationToken cancellationToken);
    Task<Result<포장작업결과응답>> 완료Async(long inboundItemId, 포장작업완료요청 request, 창고작업요청Context context, CancellationToken cancellationToken);
}

[SsalddelApiWorkflow(SsalddelWorkflow.WarehouseFulfillment)]
[SsalddelUseCase("창고 포장 작업", Summary = "적재 완료 재고의 전체 가용수량을 확인하고 출고 준비용 포장 완료 사실을 한 번만 기록합니다.")]
[SsalddelUseCaseActor(SsalddelActor.WarehouseManager)]
public sealed class 포장작업UseCase(
    SsalddelContext db,
    ICurrentUserAccessor currentUserAccessor,
    I사용자행위로그Service activityLogService,
    IPublisher publisher) : I포장작업UseCase
{
    public async Task<Result<포장작업목록페이지응답>> 목록Async(포장작업목록조회요청 request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized<포장작업목록페이지응답>();
        var page = Math.Max(0, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 50);
        var status = 포장작업조회상태코드.Normalize(request.Status);
        var query = 접근가능재고Query(userId).AsNoTracking().Where(item => item.가용수량 > 0);
        query = status switch
        {
            포장작업조회상태코드.대기 => query.Where(item => item.상태 == "적재완료"),
            포장작업조회상태코드.완료 => query.Where(item => item.상태.StartsWith("포장완료-")),
            _ => query.Where(item => item.상태 == "적재완료" || item.상태.StartsWith("포장완료-"))
        };
        if (request.WarehouseId is > 0) query = query.Where(item => item.창고Id == request.WarehouseId.Value);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(item => item.상품명.Contains(search) || item.SKU.Contains(search) || item.옵션명.Contains(search)
                || item.보관위치.Contains(search)
                || db.입고요청.Any(inbound => inbound.Id == item.입고요청Id && inbound.주문참조번호.Contains(search)));
        }
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await (from item in query
                join warehouse in db.창고.AsNoTracking() on item.창고Id equals warehouse.Id
                orderby item.UpdatedAt, item.Id
                select new 포장작업목록항목응답
                {
                    InboundItemId = item.Id, WarehouseId = item.창고Id, WarehouseName = warehouse.창고명,
                    ProductName = item.상품명, Sku = item.SKU, AvailableQuantity = item.가용수량,
                    InventoryStatus = item.상태, StorageLocation = item.보관위치,
                    CanPack = item.상태 == "적재완료", UpdatedAtUtc = AsUtc(item.UpdatedAt)
                }).Skip(page * pageSize).Take(pageSize).ToArrayAsync(cancellationToken);
        return Result.Ok(new 포장작업목록페이지응답 { Items = items, TotalCount = totalCount, Page = page, PageSize = pageSize });
    }

    public async Task<Result<포장작업상세응답>> 상세Async(long inboundItemId, CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized<포장작업상세응답>();
        if (inboundItemId <= 0) return NotFound<포장작업상세응답>();
        var row = await (from item in 접근가능재고Query(userId).AsNoTracking()
                join warehouse in db.창고.AsNoTracking() on item.창고Id equals warehouse.Id
                join inbound in db.입고요청.AsNoTracking() on item.입고요청Id equals inbound.Id
                where item.Id == inboundItemId && item.가용수량 > 0
                    && (item.상태 == "적재완료" || item.상태.StartsWith("포장완료-"))
                select new { item, warehouse.창고명, inbound.보관조건, inbound.주문참조번호 })
            .SingleOrDefaultAsync(cancellationToken);
        if (row is null) return NotFound<포장작업상세응답>();
        var histories = await db.재고이력.AsNoTracking()
            .Where(history => history.입고상품Id == inboundItemId && (history.이력유형 == "적재" || history.이력유형 == "포장"))
            .OrderByDescending(history => history.처리일시).ThenByDescending(history => history.Id)
            .Select(history => new { history.이력유형, history.처리일시, history.메모 }).ToArrayAsync(cancellationToken);
        var putAway = histories.FirstOrDefault(history => history.이력유형 == "적재");
        var packing = histories.FirstOrDefault(history => history.이력유형 == "포장");
        return Result.Ok(new 포장작업상세응답
        {
            InboundItemId = row.item.Id, InboundId = row.item.입고요청Id, WarehouseId = row.item.창고Id,
            WarehouseName = row.창고명, ProductName = row.item.상품명, Sku = row.item.SKU, OptionName = row.item.옵션명,
            AvailableQuantity = row.item.가용수량, ReservedQuantity = row.item.예약수량, DefectiveQuantity = row.item.불량수량,
            InventoryStatus = row.item.상태, StorageLocation = row.item.보관위치, StorageCondition = row.보관조건,
            OrderReference = row.주문참조번호, PutAwayAtUtc = AsUtc(putAway?.처리일시), PutAwayMemo = putAway?.메모 ?? string.Empty,
            PackedAtUtc = AsUtc(packing?.처리일시), PackingMemo = packing?.메모 ?? string.Empty,
            PackingType = PackagingType(row.item.상태), CanPack = row.item.상태 == "적재완료", UpdatedAtUtc = AsUtc(row.item.UpdatedAt)
        });
    }

    public async Task<Result<포장작업결과응답>> 완료Async(long inboundItemId, 포장작업완료요청 request, 창고작업요청Context context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!request.InventoryConfirmed || !request.PackageLabelConfirmed)
            return Invalid<포장작업결과응답>("표시 재고와 포장 표찰 확인을 모두 완료해 주세요.");
        var type = request.PackagingType?.Trim() ?? string.Empty;
        if (!포장유형코드.IsValid(type)) return Invalid<포장작업결과응답>("지원하는 포장 유형을 선택해 주세요.");
        var memo = request.Memo?.Trim() ?? string.Empty;
        if (memo.Length > 400) return Invalid<포장작업결과응답>("포장 메모는 400자 이하로 입력해 주세요.");
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized<포장작업결과응답>();
        var item = await 접근가능재고Query(userId).FirstOrDefaultAsync(candidate => candidate.Id == inboundItemId, cancellationToken);
        if (item is null) return NotFound<포장작업결과응답>();
        if (item.상태.StartsWith("포장완료-", StringComparison.Ordinal))
        {
            var existing = await db.재고이력.AsNoTracking().Where(history => history.입고상품Id == item.Id && history.이력유형 == "포장")
                .OrderByDescending(history => history.처리일시).ThenByDescending(history => history.Id).FirstOrDefaultAsync(cancellationToken);
            if (!string.Equals(PackagingType(item.상태), type, StringComparison.Ordinal) || existing?.변경후수량 != request.PackagingQuantity)
                return Conflict<포장작업결과응답>("이미 포장 완료된 재고의 수량·유형 변경은 별도 재포장 작업에서 처리해 주세요.");
            return Result.Ok(ToResult(item, request.PackagingQuantity, type, existing.처리일시, true));
        }
        if (창고재고공정Policy.포장차단사유(item.상태, request.PackagingQuantity, item.가용수량) is { } blocked)
            return Conflict<포장작업결과응답>(blocked);

        var inbound = await db.입고요청.AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == item.입고요청Id, cancellationToken);
        var now = DateTime.UtcNow;
        item.상태 = $"포장완료-{type}";
        item.UpdatedAt = now;
        var historyMemo = string.IsNullOrWhiteSpace(memo) ? $"포장 {request.PackagingQuantity}개 / {type}" : $"포장 {request.PackagingQuantity}개 / {type}. {memo}";
        db.재고이력.Add(new 재고이력 { 입고상품Id = item.Id, 이력유형 = "포장", 변경수량 = 0, 변경후수량 = request.PackagingQuantity, 원인유형 = "포장작업", 원인Id = item.입고요청Id, 처리UserId = userId, 메모 = historyMemo, 처리일시 = now });
        db.재고이동.Add(new 재고이동 { 창고Id = item.창고Id, 입고상품Id = item.Id, 상품명 = item.상품명, SKU = item.SKU, 이동유형 = "포장", 수량 = request.PackagingQuantity, 입고요청Id = item.입고요청Id, 처리UserId = userId, 메모 = historyMemo, 발생일시 = now });
        await db.SaveChangesAsync(cancellationToken);
        await LogAsync(item, request.PackagingQuantity, type, context, cancellationToken);
        await publisher.Publish(
            new 창고포장완료됨Event(
                context.UserId,
                context.RoleName,
                item.Id,
                request.PackagingQuantity,
                context.Route,
                context.TraceId,
                now,
                context.AppKey,
                item.입고요청Id,
                item.창고Id,
                inbound?.출고예정Id,
                inbound?.주문참조번호 ?? string.Empty,
                item.상품명,
                item.SKU,
                type,
                item.보관위치,
                item.커뮤니티원장Id ?? inbound?.커뮤니티원장Id ?? string.Empty),
            cancellationToken);
        return Result.Ok(ToResult(item, request.PackagingQuantity, type, now, false));
    }

    private IQueryable<입고상품> 접근가능재고Query(string userId)
    {
        var query = db.입고상품.AsQueryable();
        if (string.Equals(currentUserAccessor.Role, 역할명.서버관리자, StringComparison.OrdinalIgnoreCase)) return query;
        return query.Where(item => db.창고.Any(warehouse => warehouse.Id == item.창고Id && warehouse.소유자UserId == userId)
            || db.창고사용자.Any(warehouseUser => warehouseUser.창고Id == item.창고Id && warehouseUser.UserId == userId));
    }
    private async Task LogAsync(입고상품 item, int quantity, string type, 창고작업요청Context context, CancellationToken cancellationToken)
        => await activityLogService.기록Async(new 사용자행위로그기록
        {
            AppKey = context.AppKey, UserId = context.UserId, UserName = context.UserName, RoleName = context.RoleName,
            ActionType = "WarehousePacking", ActionName = "Completed", Route = context.Route, TraceId = context.TraceId,
            IsSuccess = true, ClientIp = context.ClientIp, UserAgent = context.UserAgent, OccurredAtUtc = DateTime.UtcNow,
            MetadataJson = JsonSerializer.Serialize(new { inboundItemId = item.Id, warehouseId = item.창고Id, packagingQuantity = quantity, packagingType = type })
        }, cancellationToken);
    private static 포장작업결과응답 ToResult(입고상품 item, int quantity, string type, DateTime occurredAt, bool replay) => new()
    {
        InboundItemId = item.Id, InventoryStatus = item.상태, PackagingQuantity = quantity, PackagingType = type,
        NextStep = "출고 인계 작업에서 운송·인수 조건 확인", PackedAtUtc = AsUtc(occurredAt), IdempotentReplay = replay
    };
    private static string PackagingType(string status) => status.StartsWith("포장완료-", StringComparison.Ordinal) ? status[5..] : string.Empty;
    private string? CurrentUserId() { var value = currentUserAccessor.UserId?.Trim(); return string.IsNullOrWhiteSpace(value) ? null : value; }
    private static DateTime AsUtc(DateTime value) => value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);
    private static DateTime? AsUtc(DateTime? value) => value.HasValue ? AsUtc(value.Value) : null;
    private static Result<T> Unauthorized<T>() => Result.Fail<T>(new Error("로그인 사용자 인증 정보가 필요합니다.").WithMetadata("StatusCode", StatusCodes.Status401Unauthorized));
    private static Result<T> NotFound<T>() => Result.Fail<T>(new Error("포장 대상을 찾을 수 없거나 현재 계정의 창고 작업 범위에 없습니다.").WithMetadata("StatusCode", StatusCodes.Status404NotFound));
    private static Result<T> Invalid<T>(string message) => Result.Fail<T>(new Error(message).WithMetadata("StatusCode", StatusCodes.Status400BadRequest));
    private static Result<T> Conflict<T>(string message) => Result.Fail<T>(new Error(message).WithMetadata("StatusCode", StatusCodes.Status409Conflict));
}
