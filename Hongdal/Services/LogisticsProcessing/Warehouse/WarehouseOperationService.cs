using Hongdal.Application.CommandProcessing;
using Hongdal.Contracts.Common.Inbound;
using Hongdal.Contracts.Common.Inventory;
using Hongdal.Contracts.Common.Warehouse;
using Hongdal.Services.Community;
using Microsoft.EntityFrameworkCore;
using 홍달.Data;
using 홍달.도메인.배차;
using 홍달.도메인.운송;
using 홍달.도메인.창고;
using 홍달.도메인.화주;
using 홍달.Services.Dispatch.Engine;

namespace Hongdal.Services.LogisticsProcessing.Warehouse;

public sealed class WarehouseOperationService : IWarehouseOperationService
{
    private readonly HongdalContext _db;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly I운송원장Mongo동기화Service _transportLedgerSync;

    public WarehouseOperationService(
        HongdalContext db,
        ICurrentUserAccessor currentUserAccessor,
        I운송원장Mongo동기화Service transportLedgerSync)
    {
        _db = db;
        _currentUserAccessor = currentUserAccessor;
        _transportLedgerSync = transportLedgerSync;
    }

    public async Task<창고목록응답> GetWarehousesAsync(CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        var query = 접근가능창고Query(userId).AsNoTracking();

        var items = await query
            .OrderBy(x => x.창고명)
            .Select(x => new 창고요약응답
            {
                Id = x.Id,
                창고명 = x.창고명,
                소유자UserId = x.소유자UserId,
                소유자유형 = x.소유자유형,
                창고유형 = x.창고유형,
                물류대행지분류 = LogisticsProxySiteTypes.Normalize(x.물류대행지분류),
                주소 = x.주소,
                담당자명 = x.담당자명,
                연락처 = x.연락처,
                위도 = x.위도,
                경도 = x.경도,
                기본창고여부 = x.기본창고여부,
                IsActive = x.IsActive
            })
            .ToArrayAsync(cancellationToken);

        return new 창고목록응답 { Items = items };
    }

    public async Task<창고요약응답> CreateWarehouseAsync(창고저장요청 request, CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        var entity = new 창고
        {
            소유자UserId = userId,
            창고명 = request.창고명.Trim(),
            소유자유형 = string.IsNullOrWhiteSpace(request.소유자유형) ? 창고소유자유형.주문자 : request.소유자유형.Trim(),
            창고유형 = string.IsNullOrWhiteSpace(request.창고유형) ? 홍달.도메인.창고.창고유형.가상창고 : request.창고유형.Trim(),
            물류대행지분류 = LogisticsProxySiteTypes.Normalize(request.물류대행지분류),
            주소 = request.주소.Trim(),
            담당자명 = request.담당자명.Trim(),
            연락처 = request.연락처.Trim(),
            위도 = request.위도,
            경도 = request.경도,
            기본창고여부 = request.기본창고여부,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.창고.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        return new 창고요약응답
        {
            Id = entity.Id,
            창고명 = entity.창고명,
            소유자UserId = entity.소유자UserId,
            소유자유형 = entity.소유자유형,
            창고유형 = entity.창고유형,
            물류대행지분류 = LogisticsProxySiteTypes.Normalize(entity.물류대행지분류),
            주소 = entity.주소,
            담당자명 = entity.담당자명,
            연락처 = entity.연락처,
            위도 = entity.위도,
            경도 = entity.경도,
            기본창고여부 = entity.기본창고여부,
            IsActive = entity.IsActive
        };
    }

    public async Task<창고요약응답> UpdateWarehouseAsync(
        long warehouseId,
        창고저장요청 request,
        CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        var entity = await 접근가능창고Query(userId)
            .FirstOrDefaultAsync(x => x.Id == warehouseId, cancellationToken)
            ?? throw new InvalidOperationException("창고를 찾을 수 없거나 접근할 수 없습니다.");

        entity.창고명 = request.창고명.Trim();
        entity.소유자유형 = string.IsNullOrWhiteSpace(request.소유자유형) ? entity.소유자유형 : request.소유자유형.Trim();
        entity.창고유형 = string.IsNullOrWhiteSpace(request.창고유형) ? entity.창고유형 : request.창고유형.Trim();
        entity.물류대행지분류 = LogisticsProxySiteTypes.Normalize(request.물류대행지분류);
        entity.주소 = request.주소.Trim();
        entity.담당자명 = request.담당자명.Trim();
        entity.연락처 = request.연락처.Trim();
        entity.위도 = request.위도;
        entity.경도 = request.경도;
        entity.기본창고여부 = request.기본창고여부;
        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return ToWarehouseResponse(entity);
    }

    public async Task DeleteWarehouseAsync(long warehouseId, CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        var entity = await 접근가능창고Query(userId)
            .FirstOrDefaultAsync(x => x.Id == warehouseId, cancellationToken)
            ?? throw new InvalidOperationException("창고를 찾을 수 없거나 접근할 수 없습니다.");

        if (await _db.입고요청.AnyAsync(x => x.창고Id == warehouseId, cancellationToken)
            || await _db.입고상품.AnyAsync(x => x.창고Id == warehouseId, cancellationToken))
        {
            throw new InvalidOperationException("입고 또는 재고 이력이 있는 창고는 삭제할 수 없습니다.");
        }

        var users = await _db.창고사용자.Where(x => x.창고Id == warehouseId).ToArrayAsync(cancellationToken);
        _db.창고사용자.RemoveRange(users);
        _db.창고.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<창고사용자목록응답> GetWarehouseUsersAsync(long warehouseId, CancellationToken cancellationToken)
    {
        await 창고접근확인Async(warehouseId, RequireUserId(), cancellationToken);
        var items = await _db.창고사용자.AsNoTracking()
            .Where(x => x.창고Id == warehouseId)
            .OrderByDescending(x => x.IsPrimary)
            .ThenBy(x => x.역할명)
            .Select(x => new 창고사용자항목응답
            {
                Id = x.Id,
                창고Id = x.창고Id,
                UserId = x.UserId,
                사용자명 = x.UserId,
                역할명 = x.역할명,
                IsPrimary = x.IsPrimary
            })
            .ToArrayAsync(cancellationToken);

        return new 창고사용자목록응답 { Items = items };
    }

    public async Task<창고사용자항목응답> AddWarehouseUserAsync(long warehouseId, 창고사용자저장요청 request, CancellationToken cancellationToken)
    {
        await 창고접근확인Async(warehouseId, RequireUserId(), cancellationToken);
        var entity = new 창고사용자
        {
            창고Id = warehouseId,
            UserId = request.UserId.Trim(),
            역할명 = request.역할명.Trim(),
            IsPrimary = request.IsPrimary,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.창고사용자.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        return new 창고사용자항목응답
        {
            Id = entity.Id,
            창고Id = entity.창고Id,
            UserId = entity.UserId,
            사용자명 = entity.UserId,
            역할명 = entity.역할명,
            IsPrimary = entity.IsPrimary
        };
    }

    public async Task<창고사용자항목응답> UpdateWarehouseUserAsync(
        long warehouseId,
        long warehouseUserId,
        창고사용자저장요청 request,
        CancellationToken cancellationToken)
    {
        await 창고접근확인Async(warehouseId, RequireUserId(), cancellationToken);
        var entity = await _db.창고사용자
            .FirstOrDefaultAsync(x => x.Id == warehouseUserId && x.창고Id == warehouseId, cancellationToken)
            ?? throw new InvalidOperationException("창고 사용자를 찾을 수 없습니다.");

        entity.UserId = request.UserId.Trim();
        entity.역할명 = request.역할명.Trim();
        entity.IsPrimary = request.IsPrimary;
        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return ToWarehouseUserResponse(entity);
    }

    public async Task DeleteWarehouseUserAsync(
        long warehouseId,
        long warehouseUserId,
        CancellationToken cancellationToken)
    {
        await 창고접근확인Async(warehouseId, RequireUserId(), cancellationToken);
        var entity = await _db.창고사용자
            .FirstOrDefaultAsync(x => x.Id == warehouseUserId && x.창고Id == warehouseId, cancellationToken)
            ?? throw new InvalidOperationException("창고 사용자를 찾을 수 없습니다.");
        _db.창고사용자.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<입고요청목록응답> GetInboundsAsync(CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        var entities = await 접근가능입고Query(userId)
            .AsNoTracking()
            .Where(x => x.상태 != 입고상태.취소)
            .OrderByDescending(x => x.CreatedAt)
            .ToArrayAsync(cancellationToken);
        var expectedOutbounds = await LoadExpectedOutboundsAsync(entities, cancellationToken);
        var items = entities
            .Select(entity => ToInboundResponse(
                entity,
                expectedOutbounds.GetValueOrDefault(entity.Id)))
            .ToArray();

        return new 입고요청목록응답 { Items = items };
    }

    public async Task<입고요청페이지응답> QueryInboundsAsync(
        입고요청목록조회요청 request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var page = Math.Max(0, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 200);
        var query = 접근가능입고Query(RequireUserId())
            .AsNoTracking()
            .Where(x => x.상태 != 입고상태.취소);

        if (request.WarehouseId is > 0)
        {
            query = query.Where(x => x.창고Id == request.WarehouseId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = request.Status.Trim();
            query = query.Where(x => x.상태 == status);
        }

        if (!string.IsNullOrWhiteSpace(request.FlowType))
        {
            var flowType = 입고흐름유형코드.Normalize(request.FlowType);
            query = query.Where(x => x.입고흐름유형 == flowType);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            var idMatched = long.TryParse(search, out var inboundId);
            query = query.Where(x =>
                (idMatched && x.Id == inboundId)
                || x.공급처코드.Contains(search)
                || x.공급처명.Contains(search)
                || x.원주문참조번호.Contains(search)
                || x.주문참조번호.Contains(search)
                || x.계약번호.Contains(search)
                || _db.출고예정.Any(outbound =>
                    outbound.입고요청Id == x.Id
                    && (outbound.상품명.Contains(search) || outbound.SKU.Contains(search))));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var descending = request.SortDescending;
        var sortedQuery = request.SortBy?.Trim() switch
        {
            nameof(입고요청항목응답.Id) => descending
                ? query.OrderByDescending(x => x.Id)
                : query.OrderBy(x => x.Id),
            nameof(입고요청항목응답.창고Id) => descending
                ? query.OrderByDescending(x => x.창고Id).ThenByDescending(x => x.Id)
                : query.OrderBy(x => x.창고Id).ThenBy(x => x.Id),
            nameof(입고요청항목응답.공급처코드) => descending
                ? query.OrderByDescending(x => x.공급처코드).ThenByDescending(x => x.Id)
                : query.OrderBy(x => x.공급처코드).ThenBy(x => x.Id),
            nameof(입고요청항목응답.공급처명) => descending
                ? query.OrderByDescending(x => x.공급처명).ThenByDescending(x => x.Id)
                : query.OrderBy(x => x.공급처명).ThenBy(x => x.Id),
            nameof(입고요청항목응답.원주문참조번호) => descending
                ? query.OrderByDescending(x => x.원주문참조번호).ThenByDescending(x => x.Id)
                : query.OrderBy(x => x.원주문참조번호).ThenBy(x => x.Id),
            nameof(입고요청항목응답.상태) => descending
                ? query.OrderByDescending(x => x.상태).ThenByDescending(x => x.Id)
                : query.OrderBy(x => x.상태).ThenBy(x => x.Id),
            nameof(입고요청항목응답.예정도착일) => descending
                ? query.OrderByDescending(x => x.예정도착일).ThenByDescending(x => x.Id)
                : query.OrderBy(x => x.예정도착일).ThenBy(x => x.Id),
            _ => query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
        };

        var skip = (int)Math.Min((long)page * pageSize, int.MaxValue);
        var entities = await sortedQuery
            .Skip(skip)
            .Take(pageSize)
            .ToArrayAsync(cancellationToken);
        var expectedOutbounds = await LoadExpectedOutboundsAsync(entities, cancellationToken);

        return new 입고요청페이지응답
        {
            Items = entities
                .Select(entity => ToInboundResponse(
                    entity,
                    expectedOutbounds.GetValueOrDefault(entity.Id)))
                .ToArray(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<입고요청항목응답> CreateInboundAsync(입고요청저장요청 request, CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        await 창고접근확인Async(request.창고Id, userId, cancellationToken);
        var contract = (request.계약정보 ?? 입고계약스냅샷.Default(request.공급처명)).Normalize();
        if (string.IsNullOrWhiteSpace(contract.계약상대방명))
        {
            contract.계약상대방명 = request.공급처명.Trim();
        }

        var entity = new 입고요청
        {
            창고Id = request.창고Id,
            입고흐름유형 = 입고흐름유형코드.Normalize(request.입고흐름유형),
            입고생성경로 = string.IsNullOrWhiteSpace(request.입고생성경로)
                ? BuildInboundSourceLabel(request.입고흐름유형)
                : request.입고생성경로.Trim(),
            계약선행여부 = request.계약선행여부,
            자동생성여부 = request.자동생성여부,
            주문Id = request.주문Id,
            주문참조번호 = request.주문참조번호.Trim(),
            주문자UserId = userId,
            판매자UserId = request.판매자UserId.Trim(),
            출고예정Id = request.출고예정Id,
            운송의뢰Id = string.IsNullOrWhiteSpace(request.운송의뢰Id) ? null : request.운송의뢰Id.Trim(),
            공급처코드 = request.공급처코드.Trim(),
            공급처명 = request.공급처명.Trim(),
            원주문참조번호 = request.원주문참조번호.Trim(),
            예정도착일 = request.예정도착일,
            비고 = request.비고.Trim(),
            계약번호 = contract.계약번호,
            계약유형 = contract.계약유형,
            계약상대방명 = contract.계약상대방명,
            정산방식 = contract.정산방식,
            판매수수료율 = contract.판매수수료율,
            보관료일단가 = contract.보관료일단가,
            통관필요여부 = contract.통관필요여부,
            계약시작일 = contract.계약시작일,
            계약종료일 = contract.계약종료일,
            계약메모 = contract.계약메모,
            상태 = 입고상태.예정,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.입고요청.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        return new 입고요청항목응답
        {
            Id = entity.Id,
            창고Id = entity.창고Id,
            커뮤니티원장Id = entity.커뮤니티원장Id,
            커뮤니티원장템플릿Key = entity.커뮤니티원장템플릿Key,
            커뮤니티원장상태 = entity.커뮤니티원장상태,
            입고흐름유형 = entity.입고흐름유형,
            입고생성경로 = entity.입고생성경로,
            계약선행여부 = entity.계약선행여부,
            자동생성여부 = entity.자동생성여부,
            주문Id = entity.주문Id,
            주문참조번호 = entity.주문참조번호,
            주문자UserId = entity.주문자UserId,
            판매자UserId = entity.판매자UserId,
            출고예정Id = entity.출고예정Id,
            운송의뢰Id = entity.운송의뢰Id,
            공급처코드 = entity.공급처코드,
            공급처명 = entity.공급처명,
            원주문참조번호 = entity.원주문참조번호,
            상태 = entity.상태,
            예정도착일 = entity.예정도착일,
            입고완료일시 = entity.입고완료일시,
            계약정보 = CreateContractSnapshot(entity)
        };
    }

    public async Task<입고요청항목응답> UpdateInboundAsync(
        long inboundId,
        입고요청저장요청 request,
        CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        var entity = await 접근가능입고Query(userId)
            .FirstOrDefaultAsync(x => x.Id == inboundId, cancellationToken)
            ?? throw new InvalidOperationException("입고요청을 찾을 수 없거나 접근할 수 없습니다.");
        if (entity.상태 != 입고상태.예정)
        {
            throw new InvalidOperationException("입고 예정 상태에서만 입고요청을 수정할 수 있습니다.");
        }

        await 창고접근확인Async(request.창고Id, userId, cancellationToken);
        var contract = (request.계약정보 ?? 입고계약스냅샷.Default(request.공급처명)).Normalize();
        if (string.IsNullOrWhiteSpace(contract.계약상대방명))
        {
            contract.계약상대방명 = request.공급처명.Trim();
        }

        entity.창고Id = request.창고Id;
        entity.입고흐름유형 = 입고흐름유형코드.Normalize(request.입고흐름유형);
        entity.입고생성경로 = string.IsNullOrWhiteSpace(request.입고생성경로)
            ? BuildInboundSourceLabel(request.입고흐름유형)
            : request.입고생성경로.Trim();
        entity.계약선행여부 = request.계약선행여부;
        entity.자동생성여부 = request.자동생성여부;
        entity.주문Id = request.주문Id;
        entity.주문참조번호 = request.주문참조번호.Trim();
        entity.판매자UserId = request.판매자UserId.Trim();
        entity.출고예정Id = request.출고예정Id;
        entity.운송의뢰Id = string.IsNullOrWhiteSpace(request.운송의뢰Id) ? null : request.운송의뢰Id.Trim();
        entity.공급처코드 = request.공급처코드.Trim();
        entity.공급처명 = request.공급처명.Trim();
        entity.원주문참조번호 = request.원주문참조번호.Trim();
        entity.예정도착일 = request.예정도착일;
        entity.비고 = request.비고.Trim();
        entity.계약번호 = contract.계약번호;
        entity.계약유형 = contract.계약유형;
        entity.계약상대방명 = contract.계약상대방명;
        entity.정산방식 = contract.정산방식;
        entity.판매수수료율 = contract.판매수수료율;
        entity.보관료일단가 = contract.보관료일단가;
        entity.통관필요여부 = contract.통관필요여부;
        entity.계약시작일 = contract.계약시작일;
        entity.계약종료일 = contract.계약종료일;
        entity.계약메모 = contract.계약메모;
        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return ToInboundResponse(entity);
    }

    public async Task CancelInboundAsync(long inboundId, CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        var entity = await 접근가능입고Query(userId)
            .FirstOrDefaultAsync(x => x.Id == inboundId, cancellationToken)
            ?? throw new InvalidOperationException("입고요청을 찾을 수 없거나 접근할 수 없습니다.");
        if (entity.상태 != 입고상태.예정)
        {
            throw new InvalidOperationException("입고 예정 상태에서만 입고요청을 취소할 수 있습니다.");
        }

        entity.상태 = 입고상태.취소;
        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<입고상품목록응답> CompleteInboundAsync(long inboundId, 입고완료요청 request, CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        var inbound = await 접근가능입고Query(userId)
            .FirstOrDefaultAsync(x => x.Id == inboundId, cancellationToken)
            ?? throw new InvalidOperationException("입고요청을 찾을 수 없거나 접근할 수 없습니다.");

        inbound.상태 = 입고상태.입고완료;
        inbound.입고완료일시 = DateTime.UtcNow;
        inbound.UpdatedAt = DateTime.UtcNow;

        var createdItems = new List<입고상품>();
        foreach (var item in request.Items)
        {
            var inboundItem = new 입고상품
            {
                입고요청Id = inbound.Id,
                창고Id = inbound.창고Id,
                커뮤니티원장Id = inbound.커뮤니티원장Id,
                커뮤니티원장템플릿Key = inbound.커뮤니티원장템플릿Key,
                커뮤니티원장상태 = inbound.커뮤니티원장상태,
                소유자UserId = inbound.주문자UserId,
                판매자UserId = inbound.판매자UserId,
                상품명 = item.상품명.Trim(),
                SKU = item.SKU.Trim(),
                옵션명 = item.옵션명.Trim(),
                입고수량 = item.입고수량,
                가용수량 = Math.Max(0, item.입고수량 - item.불량수량),
                예약수량 = 0,
                불량수량 = item.불량수량,
                보관위치 = item.보관위치.Trim(),
                계약번호 = inbound.계약번호,
                계약유형 = inbound.계약유형,
                계약상대방명 = inbound.계약상대방명,
                정산방식 = inbound.정산방식,
                판매수수료율 = inbound.판매수수료율,
                보관료일단가 = inbound.보관료일단가,
                통관필요여부 = inbound.통관필요여부,
                계약시작일 = inbound.계약시작일,
                계약종료일 = inbound.계약종료일,
                계약메모 = inbound.계약메모,
                상태 = "보관중",
                입고완료일시 = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            createdItems.Add(inboundItem);
            _db.입고상품.Add(inboundItem);
        }

        await _db.SaveChangesAsync(cancellationToken);

        foreach (var item in createdItems)
        {
            _db.재고이력.Add(new 재고이력
            {
                입고상품Id = item.Id,
                이력유형 = "입고",
                변경수량 = item.가용수량,
                변경후수량 = item.가용수량,
                원인유형 = "입고완료",
                원인Id = inbound.Id,
                처리UserId = userId,
                메모 = "입고완료로 재고 생성",
                처리일시 = DateTime.UtcNow
            });

            _db.재고이동.Add(new 재고이동
            {
                창고Id = item.창고Id,
                입고상품Id = item.Id,
                상품명 = item.상품명,
                SKU = item.SKU,
                이동유형 = 재고이동유형.입고,
                수량 = item.가용수량,
                주문Id = inbound.주문Id,
                주문참조번호 = string.IsNullOrWhiteSpace(inbound.주문참조번호) ? inbound.원주문참조번호 : inbound.주문참조번호,
                출고예정Id = inbound.출고예정Id,
                입고요청Id = inbound.Id,
                운송의뢰Id = inbound.운송의뢰Id,
                처리UserId = userId,
                메모 = "입고완료로 재고 증가",
                발생일시 = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync(cancellationToken);

        return new 입고상품목록응답
        {
            Items = createdItems.Select(item => new 입고상품항목응답
            {
                Id = item.Id,
                입고요청Id = item.입고요청Id,
                창고Id = item.창고Id,
                커뮤니티원장Id = item.커뮤니티원장Id,
                커뮤니티원장템플릿Key = item.커뮤니티원장템플릿Key,
                커뮤니티원장상태 = item.커뮤니티원장상태,
                소유자UserId = item.소유자UserId,
                판매자UserId = item.판매자UserId,
                상품명 = item.상품명,
                SKU = item.SKU,
                옵션명 = item.옵션명,
                입고수량 = item.입고수량,
                가용수량 = item.가용수량,
                불량수량 = item.불량수량,
                보관위치 = item.보관위치,
                상태 = item.상태,
                입고완료일시 = item.입고완료일시,
                계약정보 = CreateContractSnapshot(item)
            }).ToArray()
        };
    }

    public async Task<재고목록응답> GetInventoryAsync(CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        var warehouseNames = _db.창고.AsNoTracking();
        var items = await 접근가능재고Query(userId)
            .AsNoTracking()
            .OrderByDescending(x => x.UpdatedAt)
            .Select(x => new 재고항목응답
            {
                입고상품Id = x.Id,
                창고Id = x.창고Id,
                커뮤니티원장Id = x.커뮤니티원장Id,
                커뮤니티원장템플릿Key = x.커뮤니티원장템플릿Key,
                커뮤니티원장상태 = x.커뮤니티원장상태,
                창고명 = warehouseNames.Where(w => w.Id == x.창고Id).Select(w => w.창고명).FirstOrDefault() ?? string.Empty,
                소유자UserId = x.소유자UserId,
                판매자UserId = x.판매자UserId,
                상품명 = x.상품명,
                SKU = x.SKU,
                옵션명 = x.옵션명,
                가용수량 = x.가용수량,
                예약수량 = x.예약수량,
                상태 = x.상태,
                보관위치 = x.보관위치,
                계약정보 = CreateContractSnapshot(x)
            })
            .ToArrayAsync(cancellationToken);

        return new 재고목록응답 { Items = items };
    }

    public async Task<창고작업결과응답> InspectInboundItemAsync(long inboundItemId, 입고검수요청 request, CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        var item = await 접근가능재고Query(userId)
            .FirstOrDefaultAsync(x => x.Id == inboundItemId, cancellationToken)
            ?? throw new InvalidOperationException("입고상품을 찾을 수 없거나 접근할 수 없습니다.");

        if (request.검수수량 <= 0)
        {
            throw new InvalidOperationException("검수수량은 1 이상이어야 합니다.");
        }

        if (request.불량수량 < 0 || request.불량수량 > request.검수수량)
        {
            throw new InvalidOperationException("불량수량은 검수수량보다 클 수 없습니다.");
        }

        var now = DateTime.UtcNow;
        var previousAvailable = item.가용수량;
        var previousDefect = item.불량수량;
        item.입고수량 = request.검수수량;
        item.불량수량 = request.불량수량;
        item.가용수량 = Math.Max(0, request.검수수량 - request.불량수량 - item.예약수량);
        item.상태 = request.불량수량 > 0 ? "검수완료-불량포함" : "검수완료";
        item.UpdatedAt = now;

        _db.재고이력.Add(new 재고이력
        {
            입고상품Id = item.Id,
            이력유형 = "입고검수",
            변경수량 = item.가용수량 - previousAvailable,
            변경후수량 = item.가용수량,
            원인유형 = "입고검수",
            원인Id = item.입고요청Id,
            처리UserId = userId,
            메모 = $"검수 {request.검수수량}, 불량 {request.불량수량}. {request.검수메모}".Trim(),
            처리일시 = now
        });

        if (item.불량수량 != previousDefect)
        {
            _db.재고이동.Add(CreateInventoryMovement(item, "입고검수", Math.Abs(item.불량수량 - previousDefect), userId, request.검수메모, now));
        }

        await _db.SaveChangesAsync(cancellationToken);
        return ToWarehouseWorkResult(item, "입고검수", userId, now, request.검수메모);
    }

    public async Task<창고작업결과응답> PutAwayInventoryItemAsync(long inboundItemId, 적재위치배정요청 request, CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        var item = await 접근가능재고Query(userId)
            .FirstOrDefaultAsync(x => x.Id == inboundItemId, cancellationToken)
            ?? throw new InvalidOperationException("입고상품을 찾을 수 없거나 접근할 수 없습니다.");

        if (string.IsNullOrWhiteSpace(request.보관위치))
        {
            throw new InvalidOperationException("보관위치는 필수입니다.");
        }

        var now = DateTime.UtcNow;
        var previousLocation = item.보관위치;
        item.보관위치 = request.보관위치.Trim();
        item.상태 = "적재완료";
        item.UpdatedAt = now;

        var memo = $"보관위치 {previousLocation} -> {item.보관위치}. {request.적재메모}".Trim();
        _db.재고이력.Add(new 재고이력
        {
            입고상품Id = item.Id,
            이력유형 = "적재",
            변경수량 = 0,
            변경후수량 = item.가용수량,
            원인유형 = "적재위치배정",
            원인Id = item.입고요청Id,
            처리UserId = userId,
            메모 = memo,
            처리일시 = now
        });
        _db.재고이동.Add(CreateInventoryMovement(item, "적재", item.가용수량, userId, memo, now));

        await _db.SaveChangesAsync(cancellationToken);
        return ToWarehouseWorkResult(item, "적재", userId, now, request.적재메모);
    }

    public async Task<창고작업결과응답> PackInventoryItemAsync(long inboundItemId, 포장작업요청 request, CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        var item = await 접근가능재고Query(userId)
            .FirstOrDefaultAsync(x => x.Id == inboundItemId, cancellationToken)
            ?? throw new InvalidOperationException("입고상품을 찾을 수 없거나 접근할 수 없습니다.");

        if (request.포장수량 <= 0 || request.포장수량 > item.가용수량 + item.예약수량)
        {
            throw new InvalidOperationException("포장수량이 재고 수량 범위를 벗어났습니다.");
        }

        var now = DateTime.UtcNow;
        var packageType = string.IsNullOrWhiteSpace(request.포장유형) ? "일반포장" : request.포장유형.Trim();
        item.상태 = $"포장완료-{packageType}";
        item.UpdatedAt = now;

        var memo = $"포장 {request.포장수량}개 / {packageType}. {request.포장메모}".Trim();
        _db.재고이력.Add(new 재고이력
        {
            입고상품Id = item.Id,
            이력유형 = "포장",
            변경수량 = 0,
            변경후수량 = item.가용수량,
            원인유형 = "포장작업",
            원인Id = item.입고요청Id,
            처리UserId = userId,
            메모 = memo,
            처리일시 = now
        });
        _db.재고이동.Add(CreateInventoryMovement(item, "포장", request.포장수량, userId, memo, now));

        await _db.SaveChangesAsync(cancellationToken);
        return ToWarehouseWorkResult(item, "포장", userId, now, request.포장메모);
    }

    public async Task<Hongdal.Contracts.Shipper.Request.화주운송의뢰응답> CreateReconsignmentRequestAsync(재고운송의뢰생성요청 request, CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        var item = await 접근가능재고Query(userId)
            .FirstOrDefaultAsync(x => x.Id == request.입고상품Id, cancellationToken)
            ?? throw new InvalidOperationException("입고상품을 찾을 수 없거나 접근할 수 없습니다.");

        if (item.가용수량 < request.요청수량 || request.요청수량 <= 0)
        {
            throw new InvalidOperationException("가용수량보다 많은 수량을 재위탁할 수 없습니다.");
        }

        var warehouse = await _db.창고.FirstOrDefaultAsync(x => x.Id == item.창고Id, cancellationToken)
            ?? throw new InvalidOperationException("창고 정보를 찾을 수 없습니다.");

        var now = DateTime.UtcNow;
        var requestId = Guid.NewGuid().ToString();
        var shipRequest = new 화주운송의뢰
        {
            의뢰Id = requestId,
            화주Id = item.판매자UserId,
            주문자UserId = userId,
            화물종류 = string.IsNullOrWhiteSpace(request.화물종류) ? item.상품명 : request.화물종류.Trim(),
            화물설명 = $"입고상품 재위탁: {item.상품명}",
            화물수량 = request.요청수량,
            화물중량Kg = null,
            화물부피Cbm = null,
            화물파손주의여부 = false,
            화물온도조건 = "상온",
            운송방식 = "재위탁",
            차량종류 = request.차량종류.Trim(),
            결제수단 = Hongdal.Contracts.Shipper.Request.결제수단.별도정산.ToString(),
            정산시점 = Hongdal.Contracts.Shipper.Request.정산시점.운송완료후정산.ToString(),
            증빙방식 = Hongdal.Contracts.Shipper.Request.증빙방식.없음.ToString(),
            수납주체 = Hongdal.Contracts.Shipper.Request.수납주체.플랫폼.ToString(),
            정산상태 = Hongdal.Contracts.Shipper.Request.운임정산상태.청구대기.ToString(),
            정산메모 = "입고상품 재위탁 운송",
            결제예정금액 = null,
            픽업_도로명주소 = warehouse.주소,
            픽업_상세주소 = item.보관위치,
            픽업_연락처_이름 = warehouse.담당자명,
            픽업_연락처_전화번호 = warehouse.연락처,
            픽업_시간창_시작일시 = now,
            픽업_시간창_종료일시 = now.AddDays(1),
            하차_도로명주소 = request.하차지주소.Trim(),
            하차_상세주소 = request.하차지상세주소.Trim(),
            하차_연락처_이름 = userId,
            하차_연락처_전화번호 = warehouse.연락처,
            하차_시간창_시작일시 = now,
            하차_시간창_종료일시 = now.AddDays(1),
            서비스레벨 = "일반",
            요청사항 = $"재위탁 출고 상품 SKU: {item.SKU}",
            클라이언트요청Id = $"reconsignment-{item.Id}-{now:yyyyMMddHHmmss}",
            상태 = 상태값.의뢰상태.생성됨,
            결제상태 = 상태값.결제상태.결제대기,
            배차상태 = 상태값.배차상태.미시작,
            CreatedAt = now,
            UpdatedAt = now
        };

        item.가용수량 -= request.요청수량;
        item.예약수량 += request.요청수량;
        item.상태 = item.가용수량 == 0 ? "재위탁대기" : item.상태;
        item.UpdatedAt = now;

        _db.화주운송의뢰.Add(shipRequest);
        _db.운송의뢰상품연결.Add(new 운송의뢰상품연결
        {
            운송의뢰Id = shipRequest.의뢰Id,
            입고상품Id = item.Id,
            할당수량 = request.요청수량,
            CreatedAt = now
        });

        var transportProjection = new 운송원장
        {
            운송번호 = shipRequest.의뢰Id,
            의뢰Id = shipRequest.의뢰Id,
            화주Id = shipRequest.화주Id,
            배차업무유형 = 상태값.배차업무유형.용달운송,
            원본의뢰유형 = 운송의뢰배차원천유형.창고출고연계운송,
            원본의뢰Id = item.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
            픽업_도로명주소 = shipRequest.픽업_도로명주소,
            픽업_상세주소 = shipRequest.픽업_상세주소,
            픽업_위도 = shipRequest.픽업_위도,
            픽업_경도 = shipRequest.픽업_경도,
            하차_도로명주소 = shipRequest.하차_도로명주소,
            하차_상세주소 = shipRequest.하차_상세주소,
            하차_위도 = shipRequest.하차_위도,
            하차_경도 = shipRequest.하차_경도,
            상태 = 상태값.배차대기상태.대기,
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.운송원장.Add(transportProjection);

        _db.재고이력.Add(new 재고이력
        {
            입고상품Id = item.Id,
            이력유형 = 재고이동유형.예약,
            변경수량 = -request.요청수량,
            변경후수량 = item.가용수량,
            원인유형 = "재위탁운송생성",
            처리UserId = userId,
            메모 = $"재위탁 운송의뢰 생성: {shipRequest.의뢰Id}",
            처리일시 = now
        });

        _db.재고이동.Add(new 재고이동
        {
            창고Id = item.창고Id,
            입고상품Id = item.Id,
            상품명 = item.상품명,
            SKU = item.SKU,
            이동유형 = 재고이동유형.예약,
            수량 = request.요청수량,
            운송의뢰Id = shipRequest.의뢰Id,
            처리UserId = userId,
            메모 = $"재위탁 운송의뢰 생성: {shipRequest.의뢰Id}",
            발생일시 = now
        });

        await Hongdal.Application.Shipper.Request.화주운송의뢰매퍼.UpsertCargoRequirementAsync(_db, shipRequest, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        await _transportLedgerSync.화주운송의뢰동기화Async(shipRequest, userId, cancellationToken);

        return Hongdal.Application.Shipper.Request.화주운송의뢰매퍼.To응답(shipRequest);
    }

    private static 창고요약응답 ToWarehouseResponse(창고 entity)
        => new()
        {
            Id = entity.Id,
            창고명 = entity.창고명,
            소유자UserId = entity.소유자UserId,
            소유자유형 = entity.소유자유형,
            창고유형 = entity.창고유형,
            물류대행지분류 = LogisticsProxySiteTypes.Normalize(entity.물류대행지분류),
            주소 = entity.주소,
            담당자명 = entity.담당자명,
            연락처 = entity.연락처,
            위도 = entity.위도,
            경도 = entity.경도,
            기본창고여부 = entity.기본창고여부,
            IsActive = entity.IsActive
        };

    private static 창고사용자항목응답 ToWarehouseUserResponse(창고사용자 entity)
        => new()
        {
            Id = entity.Id,
            창고Id = entity.창고Id,
            UserId = entity.UserId,
            사용자명 = entity.UserId,
            역할명 = entity.역할명,
            IsPrimary = entity.IsPrimary
        };

    private static 입고요청항목응답 ToInboundResponse(
        입고요청 entity,
        출고예정? expectedOutbound = null)
        => new()
        {
            Id = entity.Id,
            창고Id = entity.창고Id,
            커뮤니티원장Id = entity.커뮤니티원장Id,
            커뮤니티원장템플릿Key = entity.커뮤니티원장템플릿Key,
            커뮤니티원장상태 = entity.커뮤니티원장상태,
            입고흐름유형 = entity.입고흐름유형,
            입고생성경로 = entity.입고생성경로,
            계약선행여부 = entity.계약선행여부,
            자동생성여부 = entity.자동생성여부,
            주문Id = entity.주문Id,
            주문참조번호 = entity.주문참조번호,
            주문자UserId = entity.주문자UserId,
            판매자UserId = entity.판매자UserId,
            출고예정Id = entity.출고예정Id,
            운송의뢰Id = entity.운송의뢰Id,
            공급처코드 = entity.공급처코드,
            공급처명 = entity.공급처명,
            예정상품명 = expectedOutbound?.상품명 ?? string.Empty,
            예정SKU = expectedOutbound?.SKU ?? string.Empty,
            예정수량 = expectedOutbound?.수량,
            원주문참조번호 = entity.원주문참조번호,
            상태 = entity.상태,
            예정도착일 = entity.예정도착일,
            입고완료일시 = entity.입고완료일시,
            계약정보 = CreateContractSnapshot(entity)
        };

    private async Task<IReadOnlyDictionary<long, 출고예정>> LoadExpectedOutboundsAsync(
        IReadOnlyCollection<입고요청> inbounds,
        CancellationToken cancellationToken)
    {
        var inboundIds = inbounds.Select(entity => entity.Id).Distinct().ToArray();
        if (inboundIds.Length == 0)
        {
            return new Dictionary<long, 출고예정>();
        }

        var outbounds = await _db.출고예정
            .AsNoTracking()
            .Where(outbound => outbound.입고요청Id.HasValue
                               && inboundIds.Contains(outbound.입고요청Id.Value))
            .OrderBy(outbound => outbound.Id)
            .ToArrayAsync(cancellationToken);

        return outbounds
            .GroupBy(outbound => outbound.입고요청Id!.Value)
            .ToDictionary(group => group.Key, group => group.First());
    }

    private string RequireUserId()
    {
        var userId = _currentUserAccessor.UserId?.Trim();
        return !string.IsNullOrWhiteSpace(userId)
            ? userId
            : throw new InvalidOperationException("로그인 사용자를 확인할 수 없습니다.");
    }

    private IQueryable<창고> 접근가능창고Query(string userId)
        => _db.창고.Where(warehouse =>
            warehouse.소유자UserId == userId
            || _db.창고사용자.Any(user =>
                user.창고Id == warehouse.Id && user.UserId == userId));

    private IQueryable<입고요청> 접근가능입고Query(string userId)
        => _db.입고요청.Where(inbound =>
            inbound.주문자UserId == userId
            || _db.창고.Any(warehouse =>
                warehouse.Id == inbound.창고Id && warehouse.소유자UserId == userId)
            || _db.창고사용자.Any(user =>
                user.창고Id == inbound.창고Id && user.UserId == userId));

    private IQueryable<입고상품> 접근가능재고Query(string userId)
        => _db.입고상품.Where(item =>
            item.소유자UserId == userId
            || item.판매자UserId == userId
            || _db.창고.Any(warehouse =>
                warehouse.Id == item.창고Id && warehouse.소유자UserId == userId)
            || _db.창고사용자.Any(user =>
                user.창고Id == item.창고Id && user.UserId == userId));

    private async Task 창고접근확인Async(
        long warehouseId,
        string userId,
        CancellationToken cancellationToken)
    {
        if (!await 접근가능창고Query(userId).AnyAsync(
                warehouse => warehouse.Id == warehouseId,
                cancellationToken))
        {
            throw new InvalidOperationException("창고를 찾을 수 없거나 접근할 수 없습니다.");
        }
    }

    private static 재고이동 CreateInventoryMovement(입고상품 item, string movementType, int quantity, string userId, string memo, DateTime occurredAt)
    {
        return new 재고이동
        {
            창고Id = item.창고Id,
            입고상품Id = item.Id,
            상품명 = item.상품명,
            SKU = item.SKU,
            이동유형 = movementType,
            수량 = quantity,
            입고요청Id = item.입고요청Id,
            처리UserId = userId,
            메모 = memo,
            발생일시 = occurredAt
        };
    }

    private static 창고작업결과응답 ToWarehouseWorkResult(입고상품 item, string workType, string userId, DateTime processedAt, string memo)
    {
        return new 창고작업결과응답
        {
            입고상품Id = item.Id,
            창고Id = item.창고Id,
            작업유형 = workType,
            상태 = item.상태,
            보관위치 = item.보관위치,
            가용수량 = item.가용수량,
            불량수량 = item.불량수량,
            처리UserId = userId,
            처리일시 = processedAt,
            메모 = memo
        };
    }

    private static 입고계약스냅샷 CreateContractSnapshot(입고요청 inbound)
        => new 입고계약스냅샷
        {
            계약번호 = inbound.계약번호,
            계약유형 = inbound.계약유형,
            계약상대방명 = inbound.계약상대방명,
            정산방식 = inbound.정산방식,
            판매수수료율 = inbound.판매수수료율,
            보관료일단가 = inbound.보관료일단가,
            통관필요여부 = inbound.통관필요여부,
            계약시작일 = inbound.계약시작일,
            계약종료일 = inbound.계약종료일,
            계약메모 = inbound.계약메모
        }.Normalize();

    private static string BuildInboundSourceLabel(string? flowType)
        => 입고흐름유형코드.Normalize(flowType) switch
        {
            입고흐름유형코드.현장임시입고 => "창고 관리자 수기 등록",
            입고흐름유형코드.주문자동입고예정 => "주문/구매 흐름 자동 생성",
            _ => "계약 DB 기반 등록"
        };

    private static 입고계약스냅샷 CreateContractSnapshot(입고상품 item)
        => new 입고계약스냅샷
        {
            계약번호 = item.계약번호,
            계약유형 = item.계약유형,
            계약상대방명 = item.계약상대방명,
            정산방식 = item.정산방식,
            판매수수료율 = item.판매수수료율,
            보관료일단가 = item.보관료일단가,
            통관필요여부 = item.통관필요여부,
            계약시작일 = item.계약시작일,
            계약종료일 = item.계약종료일,
            계약메모 = item.계약메모
        }.Normalize();
}
