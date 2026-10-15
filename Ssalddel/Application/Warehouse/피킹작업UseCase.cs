using System.Text.Json;
using FluentResults;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Ssalddel.ApiMetadata;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Application.Warehouse.Events;
using Ssalddel.Contracts.Common.ViewSettings;
using Ssalddel.Contracts.Common.Warehouse;
using 살뜰.Data;
using 살뜰.Services.Audit;
using 살뜰.도메인.창고;

namespace Ssalddel.Application.Warehouse;

public interface I피킹작업UseCase
{
    Task<Result<피킹작업목록페이지응답>> 목록Async(
        피킹작업목록조회요청 request,
        CancellationToken cancellationToken);

    Task<Result<피킹작업상세응답>> 상세Async(
        string taskKey,
        CancellationToken cancellationToken);

    Task<Result<피킹작업결과응답>> 시작Async(
        string taskKey,
        창고작업요청Context context,
        CancellationToken cancellationToken);

    Task<Result<피킹작업결과응답>> 완료Async(
        string taskKey,
        피킹작업완료요청 request,
        창고작업요청Context context,
        CancellationToken cancellationToken);
}

[SsalddelApiWorkflow(SsalddelWorkflow.WarehouseFulfillment)]
[SsalddelUseCase(
    "창고 피킹 작업",
    Summary = "접근 가능한 영속 피킹 작업을 조회하고 대기·진행중·완료 상태를 전이합니다.")]
[SsalddelUseCaseActor(SsalddelActor.WarehouseManager)]
public sealed class 피킹작업UseCase(
    SsalddelContext db,
    ICurrentUserAccessor currentUserAccessor,
    I사용자행위로그Service activityLogService,
    IPublisher publisher) : I피킹작업UseCase
{
    public async Task<Result<피킹작업목록페이지응답>> 목록Async(
        피킹작업목록조회요청 request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var userId = CurrentUserId();
        if (userId is null)
        {
            return Unauthorized<피킹작업목록페이지응답>();
        }

        var page = Math.Max(0, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 50);
        var status = 피킹작업조회상태코드.Normalize(request.Status);
        var query = 접근가능피킹작업Query(userId).AsNoTracking();
        if (request.WarehouseId is > 0)
        {
            query = query.Where(task => task.창고Id == request.WarehouseId.Value);
        }

        if (!string.Equals(status, 피킹작업조회상태코드.전체, StringComparison.Ordinal))
        {
            query = query.Where(task => task.상태 == status);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(task =>
                task.작업Key.Contains(search)
                || task.주문참조번호.Contains(search)
                || task.상품명.Contains(search)
                || task.SKU.Contains(search)
                || (task.적재대코드 != null && task.적재대코드.Contains(search))
                || (task.보관위치코드 != null && task.보관위치코드.Contains(search)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(task => task.상태 == 피킹포장작업상태.진행중 ? 0 : task.상태 == 피킹포장작업상태.대기 ? 1 : 2)
            .ThenBy(task => task.적재대코드)
            .ThenBy(task => task.Id)
            .Skip(page * pageSize)
            .Take(pageSize)
            .Select(task => new 피킹작업목록항목응답
            {
                TaskKey = task.작업Key,
                WarehouseId = task.창고Id,
                WarehouseName = task.창고명,
                ProductName = task.상품명,
                Sku = task.SKU,
                Quantity = task.수량,
                RackCode = task.적재대코드 ?? task.보관위치코드 ?? string.Empty,
                Status = task.상태,
                WorkerDisplayName = task.작업자표시명,
                UpdatedAtUtc = AsUtc(task.UpdatedAt)
            })
            .ToArrayAsync(cancellationToken);

        return Result.Ok(new 피킹작업목록페이지응답
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        });
    }

    public async Task<Result<피킹작업상세응답>> 상세Async(
        string taskKey,
        CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();
        if (userId is null)
        {
            return Unauthorized<피킹작업상세응답>();
        }

        var normalizedKey = NormalizeTaskKey(taskKey);
        if (normalizedKey is null)
        {
            return NotFound<피킹작업상세응답>();
        }

        var task = await 접근가능피킹작업Query(userId)
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.작업Key == normalizedKey, cancellationToken);
        return task is null
            ? NotFound<피킹작업상세응답>()
            : Result.Ok(ToDetail(task));
    }

    public async Task<Result<피킹작업결과응답>> 시작Async(
        string taskKey,
        창고작업요청Context context,
        CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();
        if (userId is null)
        {
            return Unauthorized<피킹작업결과응답>();
        }

        var task = await FindTrackedAsync(userId, taskKey, cancellationToken);
        if (task is null)
        {
            return NotFound<피킹작업결과응답>();
        }

        if (string.Equals(task.상태, 피킹포장작업상태.진행중, StringComparison.Ordinal))
        {
            return Result.Ok(ToResult(task, true));
        }

        if (!string.Equals(task.상태, 피킹포장작업상태.대기, StringComparison.Ordinal))
        {
            return Conflict<피킹작업결과응답>("대기 상태의 피킹 작업만 시작할 수 있습니다.");
        }

        var now = WarehouseActivityCompletionEvidence.NormalizeUtc(DateTime.UtcNow);
        task.상태 = 피킹포장작업상태.진행중;
        task.시작일시Utc = now;
        task.UpdatedAt = now;
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            return Conflict<피킹작업결과응답>("작업 상태가 다른 요청에서 변경되었습니다. 현재 작업을 다시 조회해 주세요.");
        }
        await LogAsync("Started", task, context, cancellationToken);
        return Result.Ok(ToResult(task, false));
    }

    public async Task<Result<피킹작업결과응답>> 완료Async(
        string taskKey,
        피킹작업완료요청 request,
        창고작업요청Context context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!request.ProductConfirmed || !request.QuantityConfirmed)
        {
            return Invalid<피킹작업결과응답>("상품과 전체 수량 확인을 모두 완료해 주세요.");
        }

        var rackCode = request.RackCode?.Trim() ?? string.Empty;
        if (rackCode.Length is 0 or > 120)
        {
            return Invalid<피킹작업결과응답>("적재대 확인 코드는 1자 이상 120자 이하로 입력해 주세요.");
        }

        var userId = CurrentUserId();
        if (userId is null)
        {
            return Unauthorized<피킹작업결과응답>();
        }

        var task = await FindTrackedAsync(userId, taskKey, cancellationToken);
        if (task is null)
        {
            return NotFound<피킹작업결과응답>();
        }

        var expectedRackCode = ExpectedRackCode(task);
        if (string.IsNullOrWhiteSpace(expectedRackCode))
        {
            return Conflict<피킹작업결과응답>("이 피킹 작업에는 서버 적재대 확인 코드가 없어 완료할 수 없습니다.");
        }

        if (!string.Equals(expectedRackCode, rackCode, StringComparison.OrdinalIgnoreCase))
        {
            return Invalid<피킹작업결과응답>("입력한 적재대 코드가 서버 피킹 작업과 일치하지 않습니다.");
        }

        if (string.Equals(task.상태, 피킹포장작업상태.완료, StringComparison.Ordinal))
        {
            return Result.Ok(ToResult(task, true));
        }

        if (!string.Equals(task.상태, 피킹포장작업상태.진행중, StringComparison.Ordinal))
        {
            return Conflict<피킹작업결과응답>("진행중 상태의 피킹 작업만 완료할 수 있습니다.");
        }

        var now = WarehouseActivityCompletionEvidence.NormalizeUtc(DateTime.UtcNow);
        task.상태 = 피킹포장작업상태.완료;
        task.완료일시Utc = now;
        task.완료근거Json = WarehouseActivityCompletionEvidence.Capture(task, userId, now);
        task.UpdatedAt = now;
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            return Conflict<피킹작업결과응답>("작업 상태가 다른 요청에서 변경되었습니다. 현재 작업을 다시 조회해 주세요.");
        }
        await LogAsync("Completed", task, context, cancellationToken);
        await publisher.Publish(
            new 창고피킹완료됨Event(
                context.UserId,
                context.RoleName,
                task.작업Key,
                task.창고Id,
                task.수량,
                context.Route,
                context.TraceId,
                now,
                context.AppKey,
                task.입고상품Id,
                task.출고예정Id,
                task.주문참조번호,
                task.라인Key,
                task.상품명,
                task.SKU,
                expectedRackCode,
                task.묶음바코드 ?? string.Empty,
                task.커뮤니티원장Id ?? string.Empty),
            cancellationToken);
        return Result.Ok(ToResult(task, false));
    }

    private IQueryable<피킹포장작업> 접근가능피킹작업Query(string userId)
    {
        var query = db.피킹포장작업.Where(task => task.작업유형 == 피킹포장작업유형.피킹);
        if (string.Equals(currentUserAccessor.Role, 역할명.서버관리자, StringComparison.OrdinalIgnoreCase))
        {
            return query;
        }

        return query.Where(task =>
            task.작업자UserId == userId
            || db.창고.Any(warehouse =>
                warehouse.Id == task.창고Id && warehouse.소유자UserId == userId)
            || db.창고사용자.Any(warehouseUser =>
                warehouseUser.창고Id == task.창고Id && warehouseUser.UserId == userId));
    }

    private async Task<피킹포장작업?> FindTrackedAsync(
        string userId,
        string taskKey,
        CancellationToken cancellationToken)
    {
        var normalizedKey = NormalizeTaskKey(taskKey);
        return normalizedKey is null
            ? null
            : await 접근가능피킹작업Query(userId)
                .FirstOrDefaultAsync(task => task.작업Key == normalizedKey, cancellationToken);
    }

    private async Task LogAsync(
        string actionName,
        피킹포장작업 task,
        창고작업요청Context context,
        CancellationToken cancellationToken)
        => await activityLogService.기록Async(new 사용자행위로그기록
        {
            AppKey = context.AppKey,
            UserId = context.UserId,
            UserName = context.UserName,
            RoleName = context.RoleName,
            ActionType = "WarehousePickingTask",
            ActionName = actionName,
            Route = context.Route,
            TraceId = context.TraceId,
            IsSuccess = true,
            ClientIp = context.ClientIp,
            UserAgent = context.UserAgent,
            OccurredAtUtc = DateTime.UtcNow,
            MetadataJson = JsonSerializer.Serialize(new
            {
                taskKey = task.작업Key,
                warehouseId = task.창고Id,
                quantity = task.수량,
                status = task.상태
            })
        }, cancellationToken);

    private static 피킹작업상세응답 ToDetail(피킹포장작업 task)
        => new()
        {
            TaskKey = task.작업Key,
            ProcessingMode = task.처리방식,
            Status = task.상태,
            WarehouseId = task.창고Id,
            WarehouseName = task.창고명,
            WorkerDisplayName = task.작업자표시명,
            OrderReference = task.주문참조번호,
            LineKey = task.라인Key,
            ProductName = task.상품명,
            Sku = task.SKU,
            Quantity = task.수량,
            RackCode = task.적재대코드 ?? string.Empty,
            StorageLocationCode = task.보관위치코드 ?? string.Empty,
            BundleBarcode = task.묶음바코드 ?? string.Empty,
            AssignmentReason = task.할당사유 ?? string.Empty,
            NextStep = ResolveNextStep(task),
            CanStart = string.Equals(task.상태, 피킹포장작업상태.대기, StringComparison.Ordinal),
            CanComplete = string.Equals(task.상태, 피킹포장작업상태.진행중, StringComparison.Ordinal),
            StartedAtUtc = AsUtc(task.시작일시Utc),
            CompletedAtUtc = AsUtc(task.완료일시Utc),
            UpdatedAtUtc = AsUtc(task.UpdatedAt)
        };

    private static 피킹작업결과응답 ToResult(피킹포장작업 task, bool idempotentReplay)
        => new()
        {
            TaskKey = task.작업Key,
            Status = task.상태,
            Quantity = task.수량,
            NextStep = ResolveNextStep(task),
            StartedAtUtc = AsUtc(task.시작일시Utc),
            CompletedAtUtc = AsUtc(task.완료일시Utc),
            IdempotentReplay = idempotentReplay
        };

    private static string ResolveNextStep(피킹포장작업 task)
        => !string.IsNullOrWhiteSpace(task.다음작업Key)
            ? $"포장 작업 {task.다음작업Key} 인계 대기"
            : string.Equals(task.상태, 피킹포장작업상태.완료, StringComparison.Ordinal)
                ? "후속 출고 단계 확인"
                : "피킹 작업 완료 필요";

    private static string ExpectedRackCode(피킹포장작업 task)
        => (task.적재대코드 ?? task.보관위치코드 ?? string.Empty).Trim();

    private string? CurrentUserId()
    {
        var userId = currentUserAccessor.UserId?.Trim();
        return string.IsNullOrWhiteSpace(userId) ? null : userId;
    }

    private static string? NormalizeTaskKey(string? taskKey)
    {
        var normalized = taskKey?.Trim();
        return string.IsNullOrWhiteSpace(normalized) || normalized.Length > 120 ? null : normalized;
    }

    private static DateTime AsUtc(DateTime value)
        => value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static DateTime? AsUtc(DateTime? value)
        => value.HasValue ? AsUtc(value.Value) : null;

    private static Result<T> Unauthorized<T>()
        => Result.Fail<T>(new Error("로그인 사용자 인증 정보가 필요합니다.")
            .WithMetadata("StatusCode", StatusCodes.Status401Unauthorized));

    private static Result<T> NotFound<T>()
        => Result.Fail<T>(new Error("피킹 작업을 찾을 수 없거나 현재 계정의 창고 작업 범위에 없습니다.")
            .WithMetadata("StatusCode", StatusCodes.Status404NotFound));

    private static Result<T> Invalid<T>(string message)
        => Result.Fail<T>(new Error(message)
            .WithMetadata("StatusCode", StatusCodes.Status400BadRequest));

    private static Result<T> Conflict<T>(string message)
        => Result.Fail<T>(new Error(message)
            .WithMetadata("StatusCode", StatusCodes.Status409Conflict));
}
