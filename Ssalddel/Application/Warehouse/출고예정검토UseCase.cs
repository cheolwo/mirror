using FluentResults;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Ssalddel.ApiMetadata;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Contracts.Common.Inventory;
using 살뜰.Data;
using 살뜰.도메인.공통;
using 살뜰.도메인.창고;

namespace Ssalddel.Application.Warehouse;

public interface I출고예정검토UseCase
{
    Task<Result<출고예정검토목록페이지응답>> 목록Async(출고예정검토목록조회요청 request, CancellationToken cancellationToken);
    Task<Result<출고예정검토상세응답>> 상세Async(long outboundPlanId, CancellationToken cancellationToken);
}

[SsalddelApiWorkflow(SsalddelWorkflow.WarehouseFulfillment)]
[SsalddelUseCase("출고예정 운송 전 검토", Summary = "준비된 출고예정 원장의 포장·수량·출발지 근거와 운송의뢰 입력 필요 항목을 읽기 전용으로 확인합니다.")]
[SsalddelUseCaseActor(SsalddelActor.WarehouseManager)]
public sealed partial class 출고예정검토UseCase(
    SsalddelContext db,
    ICurrentUserAccessor currentUserAccessor) : I출고예정검토UseCase
{
    public async Task<Result<출고예정검토목록페이지응답>> 목록Async(
        출고예정검토목록조회요청 request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized<출고예정검토목록페이지응답>();

        if (PeriodRequestError(request) is { } periodError)
            return Result.Fail<출고예정검토목록페이지응답>(periodError);
        OutboundPeriodResponse? period = null;
        if (request.PeriodStartUtc.HasValue)
        {
            var periodResult = await ReadPeriodAsync(request, userId, cancellationToken);
            if (periodResult.IsFailed)
                return Result.Fail<출고예정검토목록페이지응답>(periodResult.Errors);
            period = periodResult.Value;
        }

        var page = Math.Max(0, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 50);
        var status = 출고예정검토조회상태코드.Normalize(request.Status);
        var query = 접근가능출고Query(userId).AsNoTracking()
            .Where(plan => plan.상태 != 출고상태.취소);

        query = status switch
        {
            출고예정검토조회상태코드.검토대기 => query.Where(plan =>
                plan.상태 == 출고상태.준비중 && (plan.운송의뢰Id == null || plan.운송의뢰Id == string.Empty)),
            출고예정검토조회상태코드.운송연결 => query.Where(plan =>
                plan.운송의뢰Id != null && plan.운송의뢰Id != string.Empty),
            _ => query
        };

        if (request.WarehouseId is > 0)
            query = query.Where(plan => plan.출고창고Id == request.WarehouseId.Value);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            var idMatched = long.TryParse(search, out var planId);
            query = query.Where(plan =>
                (idMatched && plan.Id == planId)
                || plan.주문참조번호.Contains(search)
                || plan.상품명.Contains(search)
                || plan.SKU.Contains(search));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await (from plan in query
                join warehouse in db.창고.AsNoTracking() on plan.출고창고Id equals warehouse.Id
                orderby plan.UpdatedAt descending, plan.Id descending
                select new 출고예정검토목록항목응답
                {
                    OutboundPlanId = plan.Id,
                    InboundItemId = plan.입고상품Id,
                    WarehouseId = plan.출고창고Id,
                    WarehouseName = warehouse.창고명,
                    ProductName = plan.상품명,
                    Sku = plan.SKU,
                    OrderReference = plan.주문참조번호,
                    Quantity = plan.수량,
                    OutboundStatus = plan.상태,
                    TransportRequestId = plan.운송의뢰Id,
                    ReviewStatus = plan.운송의뢰Id != null && plan.운송의뢰Id != string.Empty
                        ? "운송 연결됨"
                        : plan.상태 == 출고상태.준비중 ? "운송 초안 검토" : "상태 확인 필요",
                    UpdatedAtUtc = AsUtc(plan.UpdatedAt)
                })
            .Skip(page * pageSize)
            .Take(pageSize)
            .ToArrayAsync(cancellationToken);

        return Result.Ok(new 출고예정검토목록페이지응답
        {
            Period = period,
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        });
    }

    public async Task<Result<출고예정검토상세응답>> 상세Async(
        long outboundPlanId,
        CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized<출고예정검토상세응답>();
        if (outboundPlanId <= 0) return NotFound<출고예정검토상세응답>();

        var row = await (from plan in 접근가능출고Query(userId).AsNoTracking()
                join warehouse in db.창고.AsNoTracking() on plan.출고창고Id equals warehouse.Id
                where plan.Id == outboundPlanId && plan.상태 != 출고상태.취소
                select new { plan, warehouse })
            .SingleOrDefaultAsync(cancellationToken);
        if (row is null) return NotFound<출고예정검토상세응답>();

        var inventory = row.plan.입고상품Id is > 0
            ? await db.입고상품.AsNoTracking().SingleOrDefaultAsync(
                item => item.Id == row.plan.입고상품Id.Value,
                cancellationToken)
            : null;
        var inbound = inventory is not null
            ? await db.입고요청.AsNoTracking().SingleOrDefaultAsync(
                item => item.Id == inventory.입고요청Id,
                cancellationToken)
            : null;
        var histories = inventory is not null
            ? await db.재고이력.AsNoTracking()
                .Where(history => history.입고상품Id == inventory.Id
                                  && (history.이력유형 == "포장" || history.이력유형 == "출고인계준비"))
                .OrderByDescending(history => history.처리일시)
                .ThenByDescending(history => history.Id)
                .Select(history => new { history.이력유형, history.처리일시, history.메모 })
                .ToArrayAsync(cancellationToken)
            : [];

        var packing = histories.FirstOrDefault(history => history.이력유형 == "포장");
        var handoff = histories.FirstOrDefault(history => history.이력유형 == "출고인계준비");
        var packagingType = ResolvePackagingType(inventory?.상태, packing?.메모);
        var outboundReady = row.plan.상태 == 출고상태.준비중;
        var outboundCompleted = row.plan.상태 == 출고상태.출고완료;
        var inventoryLinked = inventory is not null;
        var packagingReady = inventoryLinked && packing is not null;
        var originReady = row.warehouse.IsActive
                          && !string.IsNullOrWhiteSpace(row.warehouse.창고명)
                          && !string.IsNullOrWhiteSpace(row.warehouse.주소);
        var transportLinked = !string.IsNullOrWhiteSpace(row.plan.운송의뢰Id);
        var transportRequest = transportLinked
            ? await db.화주운송의뢰.AsNoTracking()
                .SingleOrDefaultAsync(item => item.의뢰Id == row.plan.운송의뢰Id, cancellationToken)
            : null;
        var transportLedger = transportLinked
            ? await db.운송원장.AsNoTracking()
                .SingleOrDefaultAsync(item => item.의뢰Id == row.plan.운송의뢰Id, cancellationToken)
            : null;
        var allocation = transportLinked && inventory is not null
            ? await db.운송의뢰상품연결.AsNoTracking()
                .SingleOrDefaultAsync(
                    item => item.운송의뢰Id == row.plan.운송의뢰Id
                            && item.입고상품Id == inventory.Id,
                    cancellationToken)
            : null;
        var assignedDriverId = transportLedger?.확정기사Id?.Trim();
        var assignedDriverVehicle = !string.IsNullOrWhiteSpace(assignedDriverId)
            ? await db.용달기사.AsNoTracking()
                .Where(item => item.기사Id == assignedDriverId && item.상태 == "활동중")
                .OrderByDescending(item => item.UpdatedAt)
                .Select(item => item.차량)
                .FirstOrDefaultAsync(cancellationToken) ?? string.Empty
            : string.Empty;
        var driverAccepted = !string.IsNullOrWhiteSpace(assignedDriverId)
                             && string.Equals(
                                 transportRequest?.배차상태,
                                 상태값.배차상태.배차확정,
                                 StringComparison.Ordinal);
        var vehicleConfirmed = driverAccepted
                               && !string.IsNullOrWhiteSpace(transportRequest?.차량종류)
                               && string.Equals(
                                   transportRequest.차량종류.Trim(),
                                   assignedDriverVehicle.Trim(),
                                   StringComparison.OrdinalIgnoreCase);
        var quantityMatches = inventory is not null
                              && row.plan.수량 > 0
                              && (transportLinked
                                  ? allocation?.할당수량 == row.plan.수량
                                    && (outboundCompleted || inventory.예약수량 >= row.plan.수량)
                                  : row.plan.수량 == inventory.가용수량);
        var handoffStatus = ResolveHandoffStatus(
            row.plan.상태,
            transportLinked,
            transportRequest?.상태,
            transportLedger?.상태,
            assignedDriverId);
        var canStartDraft = outboundReady && inventoryLinked && packagingReady && quantityMatches && originReady && !transportLinked;
        var canCompleteHandoff = outboundReady
                                 && transportLinked
                                 && inventoryLinked
                                 && packagingReady
                                 && quantityMatches
                                 && originReady
                                 && driverAccepted
                                 && vehicleConfirmed;
        var reviewStatus = outboundCompleted
            ? "출고 완료"
            : transportLinked ? "운송 연결됨" : canStartDraft ? "초안 입력 가능" : "원장 보완 필요";

        var checks = new List<출고예정검토항목응답>
        {
            Check("outbound", "출고 원장", outboundReady || outboundCompleted, $"현재 상태: {row.plan.상태}"),
            Check("packing", "포장 근거", packagingReady, packagingReady ? $"{packagingType} 포장 완료 이력 확인" : "포장 완료 이력이 필요합니다."),
            Check("quantity", "수량 정합성", quantityMatches, inventoryLinked ? $"출고 {row.plan.수량:N0}개 · 가용 {inventory!.가용수량:N0}개 · 예약 {inventory.예약수량:N0}개" : "연결된 재고가 없습니다."),
            Check("origin", "출발 창고", originReady, originReady ? $"{row.warehouse.창고명} · 주소 등록 완료" : "활성 창고와 출발지 주소가 필요합니다."),
            InputCheck("destination", "하차지", transportLinked, transportLinked ? "연결된 운송의뢰에서 확인합니다." : "운송의뢰 작성 단계에서 입력합니다."),
            InputCheck("schedule", "희망 일정", transportLinked, transportLinked ? "연결된 운송의뢰에서 확인합니다." : "픽업·도착 희망 시각을 별도로 입력합니다."),
            InputCheck("transport", "운송의뢰", transportLinked, transportLinked ? $"연결됨: {row.plan.운송의뢰Id}" : "이 검토 페이지에서는 운송의뢰를 생성하지 않습니다.")
        };
        if (transportLinked)
        {
            checks.Add(Check(
                "driver",
                "기사 수락",
                driverAccepted,
                driverAccepted ? $"확정 기사: {assignedDriverId}" : "기사 본인의 배차 수락을 기다리고 있습니다."));
            checks.Add(Check(
                "vehicle",
                "등록 차량",
                vehicleConfirmed,
                vehicleConfirmed
                    ? $"요청 {transportRequest!.차량종류} · 등록 {assignedDriverVehicle}"
                    : $"요청 {transportRequest?.차량종류 ?? "-"} · 등록 {(string.IsNullOrWhiteSpace(assignedDriverVehicle) ? "-" : assignedDriverVehicle)}"));
        }

        return Result.Ok(new 출고예정검토상세응답
        {
            OutboundPlanId = row.plan.Id,
            InboundItemId = row.plan.입고상품Id,
            WarehouseId = row.plan.출고창고Id,
            WarehouseName = row.warehouse.창고명,
            PickupAddressConfigured = !string.IsNullOrWhiteSpace(row.warehouse.주소),
            WarehouseActive = row.warehouse.IsActive,
            ProductName = row.plan.상품명,
            Sku = row.plan.SKU,
            OrderReference = row.plan.주문참조번호,
            Quantity = row.plan.수량,
            OutboundStatus = row.plan.상태,
            TransportRequestId = row.plan.운송의뢰Id,
            TransportRequestStatus = transportRequest?.상태 ?? string.Empty,
            DispatchStatus = transportRequest?.배차상태 ?? string.Empty,
            TransportStatus = transportLedger?.상태 ?? string.Empty,
            HandoffStatus = handoffStatus,
            AssignedDriverId = assignedDriverId,
            RequestedVehicleType = transportRequest?.차량종류 ?? string.Empty,
            AssignedDriverVehicle = assignedDriverVehicle,
            DriverAccepted = driverAccepted,
            VehicleConfirmed = vehicleConfirmed,
            CanCompleteHandoff = canCompleteHandoff,
            HandoffCompletedAtUtc = AsUtc(row.plan.출고처리일시),
            DestinationAddress = transportRequest?.하차_도로명주소 ?? string.Empty,
            DestinationAddressDetail = transportRequest?.하차_상세주소 ?? string.Empty,
            AvailableQuantity = inventory?.가용수량,
            ReservedQuantity = inventory?.예약수량,
            DefectiveQuantity = inventory?.불량수량,
            InventoryStatus = inventory?.상태 ?? string.Empty,
            StorageLocation = inventory?.보관위치 ?? string.Empty,
            StorageCondition = inbound?.보관조건 ?? string.Empty,
            PackagingType = packagingType,
            PackedAtUtc = AsUtc(packing?.처리일시),
            HandoffReadyAtUtc = AsUtc(handoff?.처리일시 ?? row.plan.CreatedAt),
            Checks = checks,
            CanStartTransportRequestDraft = canStartDraft,
            ReviewStatus = reviewStatus,
            NextStep = outboundCompleted
                ? $"출고 인계가 완료되었습니다. Warehouse·Driver·Shipper가 같은 의뢰 ID {row.plan.운송의뢰Id}를 다시 조회합니다."
                : transportLinked
                    ? canCompleteHandoff
                        ? "기사와 등록 차량을 현장에서 대조한 뒤 상품 인계를 완료합니다."
                        : "기사 배차 상태를 확인합니다. 이 화면을 다시 열면 같은 출고예정 ID로 최신 상태를 조회합니다."
                : canStartDraft
                    ? "별도 운송의뢰 작성에서 하차지·희망 일정·차량 조건을 입력합니다."
                    : "출고 원장과 포장·수량·출발 창고 정보를 먼저 보완합니다.",
            UpdatedAtUtc = AsUtc(row.plan.UpdatedAt)
        });
    }

    private IQueryable<출고예정> 접근가능출고Query(string userId)
    {
        var query = db.출고예정.AsQueryable();
        if (string.Equals(currentUserAccessor.Role, 역할명.서버관리자, StringComparison.OrdinalIgnoreCase)) return query;
        return query.Where(plan => db.창고.Any(warehouse => warehouse.Id == plan.출고창고Id && warehouse.소유자UserId == userId)
                                   || db.창고사용자.Any(warehouseUser => warehouseUser.창고Id == plan.출고창고Id && warehouseUser.UserId == userId));
    }

    private static 출고예정검토항목응답 Check(string code, string label, bool ready, string summary)
        => new()
        {
            Code = code,
            Label = label,
            Status = ready ? 출고예정검토항목상태코드.확인완료 : 출고예정검토항목상태코드.차단,
            Summary = summary
        };

    private static 출고예정검토항목응답 InputCheck(string code, string label, bool linked, string summary)
        => new()
        {
            Code = code,
            Label = label,
            Status = linked ? 출고예정검토항목상태코드.확인완료 : 출고예정검토항목상태코드.입력필요,
            Summary = summary
        };

    private static string ResolvePackagingType(string? status, string? historyMemo)
    {
        if (status?.StartsWith("포장완료-", StringComparison.Ordinal) == true)
            return status[5..];
        if (string.IsNullOrWhiteSpace(historyMemo)) return "포장";

        var separator = historyMemo.LastIndexOf('/');
        if (separator < 0 || separator == historyMemo.Length - 1) return "포장";
        var type = historyMemo[(separator + 1)..].Split('.', 2)[0].Trim();
        return string.IsNullOrWhiteSpace(type) ? "포장" : type;
    }

    private static string ResolveHandoffStatus(
        string outboundStatus,
        bool transportLinked,
        string? requestStatus,
        string? transportStatus,
        string? assignedDriverId)
    {
        if (string.Equals(outboundStatus, 출고상태.출고완료, StringComparison.Ordinal))
            return "출고 인계 완료";
        if (!transportLinked) return "운송의뢰 전";
        if (!string.IsNullOrWhiteSpace(transportStatus)
            && transportStatus.Contains("완료", StringComparison.Ordinal))
        {
            return "운송 완료";
        }

        if (!string.IsNullOrWhiteSpace(transportStatus)
            && (transportStatus.Contains("운송중", StringComparison.Ordinal)
                || transportStatus.Contains("픽업", StringComparison.Ordinal)
                || transportStatus.Contains("상차", StringComparison.Ordinal)))
        {
            return "기사 운송 진행";
        }

        if (!string.IsNullOrWhiteSpace(assignedDriverId)) return "기사 수락 · 출고 인계 준비";
        if (!string.IsNullOrWhiteSpace(requestStatus)) return "기사 배차 대기";
        return "운송 연결 확인 필요";
    }

    private string? CurrentUserId()
    {
        var value = currentUserAccessor.UserId?.Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static DateTime AsUtc(DateTime value)
        => value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static DateTime? AsUtc(DateTime? value)
        => value.HasValue ? AsUtc(value.Value) : null;

    private static Result<T> Unauthorized<T>()
        => Result.Fail<T>(new Error("로그인 사용자 인증 정보가 필요합니다.")
            .WithMetadata("StatusCode", StatusCodes.Status401Unauthorized));

    private static Result<T> NotFound<T>()
        => Result.Fail<T>(new Error("출고예정 원장을 찾을 수 없거나 현재 계정의 창고 작업 범위에 없습니다.")
            .WithMetadata("StatusCode", StatusCodes.Status404NotFound));
}
