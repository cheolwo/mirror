using FluentResults;
using Hongdal.Application.CommandProcessing;
using Hongdal.Contracts.Common.VehicleLoading;
using Hongdal.Contracts.Common.Warehouse;
using Hongdal.Services.Community;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using 홍달.Data;
using 홍달.도메인.운송;
using 홍달.도메인.창고;

namespace Hongdal.Services.LogisticsProcessing.VehicleLoading;

public interface ILoadingPerspectiveReadService
{
    Task<Result<상차관점페이지응답>> QueryAsync(
        string perspectiveCode,
        string? communityLedgerId,
        상차관점목록조회요청 request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 출고예정과 운송 실행 투영을 결합해 상차 업무를 구성하고,
/// 주문·판매·창고·운송·공동원장의 실제 사용자 관계로 읽기 범위를 제한합니다.
/// </summary>
public sealed class LoadingPerspectiveReadService(
    HongdalContext db,
    ICurrentUserAccessor currentUserAccessor,
    I커뮤니티원장저장소 communityLedgerStore) : ILoadingPerspectiveReadService
{
    private static readonly IReadOnlyList<string> LoadingCompletedTransportStates =
    [
        "상차완료",
        "운송중",
        "하차지도착",
        "하차완료",
        "인수완료"
    ];

    private static readonly IReadOnlyList<string> LoadingWaitingTransportStates =
    [
        "배차대기",
        "매칭중",
        "배차확정",
        "이동중",
        "상차중"
    ];

    public async Task<Result<상차관점페이지응답>> QueryAsync(
        string perspectiveCode,
        string? communityLedgerId,
        상차관점목록조회요청 request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var userId = Clean(currentUserAccessor.UserId);
        if (userId is null)
        {
            return Failure("로그인 사용자를 확인할 수 없습니다.", StatusCodes.Status401Unauthorized);
        }

        var perspective = Clean(perspectiveCode);
        var ledgerId = Clean(communityLedgerId);
        if (!SupportedPerspective(perspective))
        {
            return Failure("지원하지 않는 상차 관점입니다.", StatusCodes.Status400BadRequest);
        }

        if (string.Equals(perspective, 상차업무관점코드.공동원장, StringComparison.OrdinalIgnoreCase))
        {
            var ledgerAccess = await CheckLedgerAccessAsync(ledgerId, userId, cancellationToken);
            if (ledgerAccess.IsFailed)
            {
                return Result.Fail<상차관점페이지응답>(ledgerAccess.Errors);
            }
        }

        var query = db.출고예정
            .AsNoTracking()
            .Where(item => item.상태 != 출고상태코드.취소
                           && item.운송의뢰Id != null
                           && item.운송의뢰Id != string.Empty
                           && db.운송원장.Any(transport =>
                               transport.의뢰Id == item.운송의뢰Id
                               || transport.원본의뢰Id == item.운송의뢰Id));
        query = ApplyPerspective(query, perspective!, ledgerId, userId);
        query = ApplyFilters(query, request);
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            query = ApplyStatus(query, request.Status.Trim());
        }

        var page = Math.Max(0, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 200);
        var totalCount = await query.CountAsync(cancellationToken);
        var outbounds = await Sort(query, request)
            .Skip(Skip(page, pageSize))
            .Take(pageSize)
            .ToArrayAsync(cancellationToken);
        var warehouses = await LoadWarehousesAsync(outbounds, cancellationToken);
        var transports = await LoadTransportsAsync(outbounds, cancellationToken);

        return Result.Ok(new 상차관점페이지응답
        {
            Items = outbounds
                .Select(outbound => ToResponse(
                    outbound,
                    warehouses.GetValueOrDefault(outbound.출고창고Id),
                    FindTransport(transports, outbound.운송의뢰Id),
                    perspective!,
                    ledgerId))
                .Where(item => item is not null)
                .Cast<상차관점항목응답>()
                .ToArray(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        });
    }

    private IQueryable<출고예정> ApplyPerspective(
        IQueryable<출고예정> query,
        string perspective,
        string? ledgerId,
        string userId)
        => perspective.ToLowerInvariant() switch
        {
            상차업무관점코드.주문자 => query.Where(item => item.주문자UserId == userId),
            상차업무관점코드.판매자 => query.Where(item => item.판매자UserId == userId),
            상차업무관점코드.창고관리자 => query.Where(item =>
                db.창고.Any(warehouse =>
                    warehouse.Id == item.출고창고Id && warehouse.소유자UserId == userId)
                || db.창고사용자.Any(warehouseUser =>
                    warehouseUser.창고Id == item.출고창고Id && warehouseUser.UserId == userId)),
            상차업무관점코드.운송담당자 => query.Where(item => db.운송원장.Any(transport =>
                (transport.의뢰Id == item.운송의뢰Id || transport.원본의뢰Id == item.운송의뢰Id)
                && (transport.화주Id == userId
                    || transport.현재추천대상기사Id == userId
                    || transport.확정기사Id == userId))),
            상차업무관점코드.공동원장 => query.Where(item =>
                item.커뮤니티원장Id == ledgerId
                || db.운송원장.Any(transport =>
                    (transport.의뢰Id == item.운송의뢰Id || transport.원본의뢰Id == item.운송의뢰Id)
                    && transport.커뮤니티원장Id == ledgerId)),
            _ => query.Where(_ => false)
        };

    private IQueryable<출고예정> ApplyStatus(IQueryable<출고예정> query, string status)
    {
        if (string.Equals(status, 상차작업상태코드.대기, StringComparison.OrdinalIgnoreCase))
        {
            return query.Where(item => db.운송원장.Any(transport =>
                (transport.의뢰Id == item.운송의뢰Id || transport.원본의뢰Id == item.운송의뢰Id)
                && LoadingWaitingTransportStates.Contains(transport.상태)));
        }

        if (string.Equals(status, 상차작업상태코드.도착, StringComparison.OrdinalIgnoreCase))
        {
            return query.Where(item => db.운송원장.Any(transport =>
                (transport.의뢰Id == item.운송의뢰Id || transport.원본의뢰Id == item.운송의뢰Id)
                && transport.상태 == "상차지도착"));
        }

        if (string.Equals(status, 상차작업상태코드.완료, StringComparison.OrdinalIgnoreCase))
        {
            return query.Where(item => db.운송원장.Any(transport =>
                (transport.의뢰Id == item.운송의뢰Id || transport.원본의뢰Id == item.운송의뢰Id)
                && LoadingCompletedTransportStates.Contains(transport.상태)));
        }

        return query.Where(item => db.운송원장.Any(transport =>
            (transport.의뢰Id == item.운송의뢰Id || transport.원본의뢰Id == item.운송의뢰Id)
            && transport.상태 == status));
    }

    private static IQueryable<출고예정> ApplyFilters(
        IQueryable<출고예정> query,
        상차관점목록조회요청 request)
    {
        if (request.WarehouseId is > 0)
        {
            query = query.Where(item => item.출고창고Id == request.WarehouseId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            var idMatched = long.TryParse(search, out var outboundId);
            query = query.Where(item =>
                (idMatched && item.Id == outboundId)
                || item.주문참조번호.Contains(search)
                || item.상품명.Contains(search)
                || item.SKU.Contains(search)
                || (item.운송의뢰Id != null && item.운송의뢰Id.Contains(search)));
        }

        return query;
    }

    private static IOrderedQueryable<출고예정> Sort(
        IQueryable<출고예정> query,
        상차관점목록조회요청 request)
    {
        var descending = request.SortDescending;
        return request.SortBy?.Trim() switch
        {
            nameof(상차관점항목응답.출고예정Id) => descending ? query.OrderByDescending(item => item.Id) : query.OrderBy(item => item.Id),
            nameof(상차관점항목응답.출고창고Id) => descending ? query.OrderByDescending(item => item.출고창고Id).ThenByDescending(item => item.Id) : query.OrderBy(item => item.출고창고Id).ThenBy(item => item.Id),
            nameof(상차관점항목응답.주문참조번호) => descending ? query.OrderByDescending(item => item.주문참조번호).ThenByDescending(item => item.Id) : query.OrderBy(item => item.주문참조번호).ThenBy(item => item.Id),
            nameof(상차관점항목응답.상품명) => descending ? query.OrderByDescending(item => item.상품명).ThenByDescending(item => item.Id) : query.OrderBy(item => item.상품명).ThenBy(item => item.Id),
            nameof(상차관점항목응답.생성시각Utc) => descending ? query.OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id) : query.OrderBy(item => item.CreatedAt).ThenBy(item => item.Id),
            _ => query.OrderByDescending(item => item.UpdatedAt).ThenByDescending(item => item.Id)
        };
    }

    private async Task<Result> CheckLedgerAccessAsync(
        string? ledgerId,
        string userId,
        CancellationToken cancellationToken)
    {
        if (ledgerId is null)
        {
            return FailureResult("공동 원장 ID가 필요합니다.", StatusCodes.Status400BadRequest);
        }

        var ledger = await communityLedgerStore.원장조회Async(ledgerId, cancellationToken);
        if (ledger is null)
        {
            return FailureResult("공동 원장을 찾을 수 없습니다.", StatusCodes.Status404NotFound);
        }

        return 주문원장역할별조회Service.직접접근가능(ledger, userId)
            ? Result.Ok()
            : FailureResult("현재 사용자는 이 공동 원장의 참여자가 아닙니다.", StatusCodes.Status403Forbidden);
    }

    private async Task<IReadOnlyDictionary<long, 창고>> LoadWarehousesAsync(
        IReadOnlyCollection<출고예정> outbounds,
        CancellationToken cancellationToken)
    {
        var ids = outbounds.Select(item => item.출고창고Id).Distinct().ToList();
        return ids.Count == 0
            ? new Dictionary<long, 창고>()
            : await db.창고.AsNoTracking()
                .Where(item => ids.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, cancellationToken);
    }

    private async Task<IReadOnlyDictionary<string, 운송원장>> LoadTransportsAsync(
        IReadOnlyCollection<출고예정> outbounds,
        CancellationToken cancellationToken)
    {
        var requestIds = outbounds
            .Select(item => item.운송의뢰Id)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (requestIds.Count == 0)
        {
            return new Dictionary<string, 운송원장>(StringComparer.OrdinalIgnoreCase);
        }

        var items = await db.운송원장.AsNoTracking()
            .Where(item => requestIds.Contains(item.의뢰Id) || requestIds.Contains(item.원본의뢰Id))
            .OrderByDescending(item => item.UpdatedAt)
            .ToArrayAsync(cancellationToken);
        var result = new Dictionary<string, 운송원장>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
        {
            if (!string.IsNullOrWhiteSpace(item.의뢰Id))
            {
                result.TryAdd(item.의뢰Id, item);
            }

            if (!string.IsNullOrWhiteSpace(item.원본의뢰Id))
            {
                result.TryAdd(item.원본의뢰Id, item);
            }
        }

        return result;
    }

    private static 운송원장? FindTransport(
        IReadOnlyDictionary<string, 운송원장> transports,
        string? requestId)
        => string.IsNullOrWhiteSpace(requestId) ? null : transports.GetValueOrDefault(requestId);

    private static 상차관점항목응답? ToResponse(
        출고예정 outbound,
        창고? warehouse,
        운송원장? transport,
        string perspective,
        string? requestedLedgerId)
    {
        if (transport is null)
        {
            return null;
        }

        var loadingCompleted = LoadingCompletedTransportStates.Contains(
            transport.상태,
            StringComparer.OrdinalIgnoreCase);
        var loadingArrived = string.Equals(transport.상태, "상차지도착", StringComparison.OrdinalIgnoreCase);
        var communityLedgerId = string.Equals(perspective, 상차업무관점코드.공동원장, StringComparison.OrdinalIgnoreCase)
            ? requestedLedgerId
            : Clean(outbound.커뮤니티원장Id) ?? Clean(transport.커뮤니티원장Id);
        return new 상차관점항목응답
        {
            상차작업Id = $"{transport.Id}:{outbound.Id}",
            출고예정Id = outbound.Id,
            운송원장Id = transport.Id,
            운송의뢰Id = outbound.운송의뢰Id ?? transport.의뢰Id,
            운송번호 = transport.운송번호,
            관계코드 = perspective,
            조회근거 = AccessBasis(perspective),
            상차상태 = loadingCompleted
                ? 상차작업상태코드.완료
                : loadingArrived ? 상차작업상태코드.도착 : 상차작업상태코드.대기,
            운송상태 = transport.상태,
            상차가능여부 = loadingArrived,
            상차완료여부 = loadingCompleted,
            상차완료일시 = loadingCompleted ? transport.출발_픽업 : null,
            주문참조번호 = outbound.주문참조번호,
            주문자UserId = outbound.주문자UserId,
            판매자UserId = outbound.판매자UserId,
            화주UserId = transport.화주Id,
            확정기사UserId = Clean(transport.확정기사Id),
            출고창고Id = outbound.출고창고Id,
            출고창고명 = warehouse?.창고명 ?? string.Empty,
            상차주소 = First(transport.픽업_도로명주소, warehouse?.주소),
            상차상세주소 = transport.픽업_상세주소,
            하차주소 = transport.하차_도로명주소,
            하차상세주소 = transport.하차_상세주소,
            상품명 = outbound.상품명,
            SKU = outbound.SKU,
            수량 = outbound.수량,
            출고묶음Id = outbound.출고묶음Id,
            공동원장Id = communityLedgerId,
            공동원장템플릿Key = string.Equals(outbound.커뮤니티원장Id, communityLedgerId, StringComparison.OrdinalIgnoreCase)
                ? outbound.커뮤니티원장템플릿Key
                : transport.커뮤니티원장템플릿Key,
            생성시각Utc = outbound.CreatedAt,
            수정시각Utc = outbound.UpdatedAt > transport.UpdatedAt ? outbound.UpdatedAt : transport.UpdatedAt
        };
    }

    private static string AccessBasis(string perspective)
        => perspective switch
        {
            상차업무관점코드.주문자 => "주문자연결",
            상차업무관점코드.판매자 => "판매자연결",
            상차업무관점코드.창고관리자 => "출고창고담당",
            상차업무관점코드.운송담당자 => "운송원장담당",
            상차업무관점코드.공동원장 => "공동원장참여",
            _ => string.Empty
        };

    private static bool SupportedPerspective(string? perspective)
        => perspective is not null
           && new[]
           {
               상차업무관점코드.주문자,
               상차업무관점코드.판매자,
               상차업무관점코드.창고관리자,
               상차업무관점코드.운송담당자,
               상차업무관점코드.공동원장
           }.Contains(perspective, StringComparer.OrdinalIgnoreCase);

    private static string First(params string?[] values)
        => values.Select(Clean).FirstOrDefault(value => value is not null) ?? string.Empty;

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static int Skip(int page, int pageSize)
        => (int)Math.Min((long)page * pageSize, int.MaxValue);

    private static Result FailureResult(string message, int statusCode)
        => Result.Fail(new Error(message).WithMetadata("StatusCode", statusCode));

    private static Result<상차관점페이지응답> Failure(string message, int statusCode)
        => Result.Fail<상차관점페이지응답>(new Error(message).WithMetadata("StatusCode", statusCode));
}
