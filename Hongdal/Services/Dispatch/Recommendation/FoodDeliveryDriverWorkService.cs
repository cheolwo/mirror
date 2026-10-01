using FluentResults;
using Hongdal.Application.CommandProcessing;
using Hongdal.Application.Driver.DispatchAction;
using Hongdal.Contracts.Common.Drivers;
using Hongdal.Contracts.Common.Operations;
using Hongdal.Contracts.Food;
using Hongdal.Services.Community;
using Hongdal.Services.Food;
using Microsoft.EntityFrameworkCore;
using 홍달.Data;
using 홍달.Services.Dispatch.Coordination;
using 홍달.Services.Dispatch.Queue;
using 홍달.Services.Settlement;
using 홍달.Services.Storage.Local;
using 홍달.도메인.공통;
using 홍달.도메인.음식;
using 홍달.도메인.운송;

namespace 홍달.Services.Dispatch.Recommendation;

public interface I음식배달기사업무Service
{
    Task<IReadOnlyList<DriverWorkOfferDto>> 제안조회Async(
        string driverId,
        CancellationToken cancellationToken = default);

    Task<Result<FoodDeliveryDriverActionResponse>> 수락Async(
        string driverId,
        string offerId,
        CancellationToken cancellationToken = default);

    Task<Result<FoodDeliveryDriverActionResponse>> 묶음수락Async(
        string driverId,
        IReadOnlyList<string> offerIds,
        CancellationToken cancellationToken = default);

    Task<Result<FoodDeliveryDriverActionResponse>> 거절Async(
        string driverId,
        string offerId,
        CancellationToken cancellationToken = default);

    Task<Result<FoodDeliveryDriverActionResponse>> 픽업완료Async(
        string driverId,
        string offerId,
        CancellationToken cancellationToken = default);

    Task<Result<FoodDeliveryDriverActionResponse>> 전달완료Async(
        string driverId,
        string offerId,
        CancellationToken cancellationToken = default);
}

public sealed class 음식배달기사업무Service : I음식배달기사업무Service
{
    private const decimal 묶음픽업최대거리Km = 1.5m;
    private const decimal 묶음전달최대거리Km = 3m;
    private static readonly TimeSpan 묶음조리완료최대차이 = TimeSpan.FromMinutes(12);

    private readonly HongdalContext _db;
    private readonly IDriverLocationStore _locationStore;
    private readonly I배차추천경로Service _routeService;
    private readonly I배차대기원장전환Service _queueTransitionService;
    private readonly I배달권실행공간Store _deliveryScopeStore;
    private readonly IHongdalFoodOrderStore _foodOrderStore;
    private readonly I음식마트원장Mongo동기화Service _foodLedgerSync;
    private readonly I운송원장Mongo동기화Service _transportLedgerSync;
    private readonly I기사월정산Service _settlementService;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly ILogger<음식배달기사업무Service> _logger;

    public 음식배달기사업무Service(
        HongdalContext db,
        IDriverLocationStore locationStore,
        I배차추천경로Service routeService,
        I배차대기원장전환Service queueTransitionService,
        I배달권실행공간Store deliveryScopeStore,
        IHongdalFoodOrderStore foodOrderStore,
        I음식마트원장Mongo동기화Service foodLedgerSync,
        I운송원장Mongo동기화Service transportLedgerSync,
        I기사월정산Service settlementService,
        ICurrentUserAccessor currentUserAccessor,
        ILogger<음식배달기사업무Service> logger)
    {
        _db = db;
        _locationStore = locationStore;
        _routeService = routeService;
        _queueTransitionService = queueTransitionService;
        _deliveryScopeStore = deliveryScopeStore;
        _foodOrderStore = foodOrderStore;
        _foodLedgerSync = foodLedgerSync;
        _transportLedgerSync = transportLedgerSync;
        _settlementService = settlementService;
        _currentUserAccessor = currentUserAccessor;
        _logger = logger;
    }

    public async Task<IReadOnlyList<DriverWorkOfferDto>> 제안조회Async(
        string driverId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(driverId))
        {
            return [];
        }

        var now = DateTime.UtcNow;
        var queues = await _db.운송원장
            .AsNoTracking()
            .Where(x => x.배차업무유형 == 상태값.배차업무유형.음식배달
                        && x.상태 != 상태값.배차상태.인수완료
                        && ((x.상태 == 상태값.배차대기상태.대기
                             && x.배차큐단계 == 상태값.배차큐단계.배차추천
                             && x.배차노출상태 == 상태값.배차노출상태.추천중
                             && x.현재추천대상기사Id == driverId
                             && x.추천만료시각.HasValue
                             && x.추천만료시각 > now)
                            || (x.확정기사Id == driverId
                                && x.배차큐단계 == 상태값.배차큐단계.확정)))
            .OrderBy(x => x.추천만료시각)
            .ThenBy(x => x.CreatedAt)
            .Take(12)
            .ToListAsync(cancellationToken);
        if (queues.Count == 0)
        {
            return [];
        }

        var orderNos = queues
            .Select(ResolveOrderNo)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var orders = await _db.음식주문
            .AsNoTracking()
            .Include(x => x.상품목록)
            .Where(x => orderNos.Contains(x.주문번호))
            .ToDictionaryAsync(x => x.주문번호, StringComparer.Ordinal, cancellationToken);

        _locationStore.TryGetLatest(driverId, out var driverLocation);
        return queues
            .Where(queue => orders.ContainsKey(ResolveOrderNo(queue)))
            .Select(queue => ToOffer(queue, orders[ResolveOrderNo(queue)], driverLocation))
            .ToArray();
    }

    public async Task<Result<FoodDeliveryDriverActionResponse>> 수락Async(
        string driverId,
        string offerId,
        CancellationToken cancellationToken = default)
        => await 수락목록Async(driverId, [offerId], requireBundle: false, cancellationToken);

    public async Task<Result<FoodDeliveryDriverActionResponse>> 묶음수락Async(
        string driverId,
        IReadOnlyList<string> offerIds,
        CancellationToken cancellationToken = default)
        => await 수락목록Async(driverId, offerIds, requireBundle: true, cancellationToken);

    private async Task<Result<FoodDeliveryDriverActionResponse>> 수락목록Async(
        string driverId,
        IReadOnlyList<string>? offerIds,
        bool requireBundle,
        CancellationToken cancellationToken)
    {
        var executionBoundary = CollectiveActionDispatchBoundaryPolicy.Evaluate(
            DispatchConfirmationBoundaryRequest.ForDriverSelfAcceptance(
                _currentUserAccessor.UserId,
                driverId));
        if (!executionBoundary.CanConfirmDispatch)
        {
            return Result.Fail<FoodDeliveryDriverActionResponse>(
                "플랫폼의 후보 정보만으로 배차를 확정할 수 없습니다. 참여 기사 본인의 수락이 필요합니다.");
        }

        var normalizedIds = (offerIds ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.Ordinal)
            .Take(4)
            .ToArray();
        if (normalizedIds.Length == 0)
        {
            return Result.Fail<FoodDeliveryDriverActionResponse>("수락할 음식 배달 제안이 없습니다.");
        }

        if (requireBundle && normalizedIds.Length < 2)
        {
            return Result.Fail<FoodDeliveryDriverActionResponse>("묶음 배달은 서로 다른 제안 두 건 이상이 필요합니다.");
        }

        if (normalizedIds.Length > 3)
        {
            return Result.Fail<FoodDeliveryDriverActionResponse>("한 번에 최대 세 건까지 묶음 수락할 수 있습니다.");
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var assignments = new List<(운송원장 Queue, 음식주문 Order)>(normalizedIds.Length);
        foreach (var id in normalizedIds)
        {
            var loaded = await LoadForActionAsync(id, cancellationToken);
            if (loaded is null)
            {
                return Result.Fail<FoodDeliveryDriverActionResponse>($"{id} 음식 배달 제안을 찾을 수 없습니다.");
            }

            var (queue, order) = loaded.Value;
            if (!배차응답가능정책.추천수락가능(queue, driverId, DateTime.UtcNow))
            {
                return Result.Fail<FoodDeliveryDriverActionResponse>($"{id} 제안은 이미 만료되었거나 다른 기사에게 배정되었습니다.");
            }

            if (음식주문상태코드.Normalize(order.상태) is 음식주문상태코드.취소 or 음식주문상태코드.전달완료)
            {
                return Result.Fail<FoodDeliveryDriverActionResponse>($"{id} 주문은 취소되었거나 이미 전달 완료되었습니다.");
            }

            assignments.Add((queue, order));
        }

        if (requireBundle && !묶음동선가능(assignments))
        {
            return Result.Fail<FoodDeliveryDriverActionResponse>(
                "조리 완료 시각 또는 픽업·전달 동선이 묶음 배달 기준을 벗어났습니다. 목록을 새로고침해 주세요.");
        }

        var now = DateTime.UtcNow;
        foreach (var (queue, order) in assignments)
        {
            queue.상태 = 상태값.배차대기상태.확정;
            queue.배차큐단계 = 상태값.배차큐단계.확정;
            queue.배차노출상태 = 상태값.배차노출상태.확정;
            queue.확정기사Id = driverId;
            queue.기사_운송자 = driverId;
            queue.현재추천대상기사Id = null;
            queue.추천시작시각 = null;
            queue.추천만료시각 = null;
            queue.UpdatedAt = now;

            ApplyFoodOrderState(
                order,
                음식주문상태코드.기사배정,
                음식주문배차상태코드.기사배정,
                requireBundle ? "F드라이버 묶음 배차 수락" : "F드라이버 배차 수락",
                now);
        }

        var saveResult = await SaveTransactionAsync(transaction, cancellationToken);
        if (saveResult.IsFailed)
        {
            return saveResult.ToResult<FoodDeliveryDriverActionResponse>();
        }

        foreach (var (queue, order) in assignments)
        {
            await ApplySettlementAsync(driverId, now, cancellationToken);
            await _deliveryScopeStore.Remove운송의뢰Async(queue.의뢰Id, cancellationToken);
            await SyncLedgersAsync(queue, order.주문번호, driverId, cancellationToken);
        }

        return Result.Ok(new FoodDeliveryDriverActionResponse
        {
            OfferId = assignments.Count == 1
                ? assignments[0].Queue.의뢰Id
                : $"bundle:{string.Join(':', assignments.Select(x => x.Queue.의뢰Id))}",
            OrderIds = assignments.Select(x => x.Order.주문번호).ToArray(),
            Status = DriverWorkOfferStatus.Accepted,
            Message = assignments.Count == 1
                ? "음식 배달 제안을 수락했습니다."
                : $"묶음 배달 {assignments.Count}건을 한 번에 확정했습니다."
        });
    }

    public async Task<Result<FoodDeliveryDriverActionResponse>> 거절Async(
        string driverId,
        string offerId,
        CancellationToken cancellationToken = default)
    {
        var queue = await _db.운송원장
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.의뢰Id == offerId
                                      && x.배차업무유형 == 상태값.배차업무유형.음식배달,
                cancellationToken);
        if (queue is null)
        {
            return Result.Fail<FoodDeliveryDriverActionResponse>("음식 배달 제안을 찾을 수 없습니다.");
        }

        if (!배차응답가능정책.추천거절가능(queue, driverId, DateTime.UtcNow))
        {
            return Result.Fail<FoodDeliveryDriverActionResponse>("거절할 수 있는 활성 제안이 아닙니다.");
        }

        var transition = await _queueTransitionService.추천거절처리Async(offerId, driverId, cancellationToken);
        if (!transition.전환여부)
        {
            return Result.Fail<FoodDeliveryDriverActionResponse>(transition.메시지);
        }

        var orderNo = ResolveOrderNo(queue);
        var order = await _db.음식주문.FirstOrDefaultAsync(x => x.주문번호 == orderNo, cancellationToken);
        if (order is not null)
        {
            order.배차상태 = 음식주문배차상태코드.배차대기;
            order.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }

        return Result.Ok(Response(offerId, orderNo, DriverWorkOfferStatus.Rejected, "음식 배달 제안을 거절했습니다."));
    }

    public Task<Result<FoodDeliveryDriverActionResponse>> 픽업완료Async(
        string driverId,
        string offerId,
        CancellationToken cancellationToken = default)
        => 진행상태변경Async(
            driverId,
            offerId,
            음식주문상태코드.픽업완료,
            음식주문배차상태코드.기사배정,
            상태값.배차상태.상차완료,
            DriverWorkOfferStatus.MovingToDropoff,
            "음식점 픽업 완료",
            cancellationToken);

    public Task<Result<FoodDeliveryDriverActionResponse>> 전달완료Async(
        string driverId,
        string offerId,
        CancellationToken cancellationToken = default)
        => 진행상태변경Async(
            driverId,
            offerId,
            음식주문상태코드.전달완료,
            음식주문배차상태코드.배달완료,
            상태값.배차상태.인수완료,
            DriverWorkOfferStatus.Completed,
            "고객 전달 완료",
            cancellationToken);

    private async Task<Result<FoodDeliveryDriverActionResponse>> 진행상태변경Async(
        string driverId,
        string offerId,
        string nextOrderState,
        string nextDispatchState,
        string nextTransportState,
        string responseState,
        string reason,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var loaded = await LoadForActionAsync(offerId, cancellationToken);
        if (loaded is null)
        {
            return Result.Fail<FoodDeliveryDriverActionResponse>("음식 배달 업무를 찾을 수 없습니다.");
        }

        var (queue, order) = loaded.Value;
        if (!string.Equals(queue.확정기사Id, driverId, StringComparison.Ordinal)
            || queue.배차큐단계 is not (상태값.배차큐단계.확정 or 상태값.배차큐단계.종료))
        {
            return Result.Fail<FoodDeliveryDriverActionResponse>("이 음식 배달을 진행할 수 있는 기사가 아닙니다.");
        }

        var currentOrderState = 음식주문상태코드.Normalize(order.상태);
        if (string.Equals(currentOrderState, nextOrderState, StringComparison.Ordinal))
        {
            return Result.Ok(Response(offerId, order.주문번호, responseState, $"이미 {reason} 상태입니다."));
        }

        if (nextOrderState == 음식주문상태코드.픽업완료
            && currentOrderState != 음식주문상태코드.기사배정)
        {
            return Result.Fail<FoodDeliveryDriverActionResponse>("기사 배정이 완료된 주문만 픽업 완료할 수 있습니다.");
        }

        if (nextOrderState == 음식주문상태코드.전달완료
            && currentOrderState != 음식주문상태코드.픽업완료)
        {
            return Result.Fail<FoodDeliveryDriverActionResponse>("픽업 완료된 주문만 고객 전달 완료할 수 있습니다.");
        }

        var now = DateTime.UtcNow;
        ApplyFoodOrderState(order, nextOrderState, nextDispatchState, reason, now);
        queue.상태 = nextTransportState;
        queue.UpdatedAt = now;
        if (nextOrderState == 음식주문상태코드.픽업완료)
        {
            queue.출발_픽업 ??= now;
        }
        else
        {
            queue.도착 ??= now;
            queue.배차큐단계 = 상태값.배차큐단계.종료;
            queue.배차노출상태 = 상태값.배차노출상태.종료;
        }

        var saveResult = await SaveTransactionAsync(transaction, cancellationToken);
        if (saveResult.IsFailed)
        {
            return saveResult.ToResult<FoodDeliveryDriverActionResponse>();
        }

        await SyncLedgersAsync(queue, order.주문번호, driverId, cancellationToken);
        return Result.Ok(Response(offerId, order.주문번호, responseState, $"{reason} 처리했습니다."));
    }

    private async Task<(운송원장 Queue, 음식주문 Order)?> LoadForActionAsync(
        string offerId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(offerId))
        {
            return null;
        }

        var queue = await _db.운송원장
            .FirstOrDefaultAsync(x => x.의뢰Id == offerId
                                      && x.배차업무유형 == 상태값.배차업무유형.음식배달,
                cancellationToken);
        if (queue is null)
        {
            return null;
        }

        var orderNo = ResolveOrderNo(queue);
        var order = await _db.음식주문
            .Include(x => x.상태이력)
            .FirstOrDefaultAsync(x => x.주문번호 == orderNo, cancellationToken);
        return order is null ? null : (queue, order);
    }

    private async Task<Result> SaveTransactionAsync(
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction,
        CancellationToken cancellationToken)
    {
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result.Ok();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogInformation(ex, "음식 배달 상태 변경 중 동시성 충돌이 발생했습니다.");
            return Result.Fail("다른 기사 또는 운영자가 먼저 상태를 변경했습니다. 목록을 새로고침해 주세요.");
        }
        catch (DbUpdateException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogWarning(ex, "음식 배달 상태 저장에 실패했습니다.");
            return Result.Fail("음식 배달 상태를 저장하지 못했습니다.");
        }
    }

    private async Task SyncLedgersAsync(
        운송원장 queue,
        string orderNo,
        string updatedBy,
        CancellationToken cancellationToken)
    {
        try
        {
            await _transportLedgerSync.운송실행투영동기화Async(queue, updatedBy, cancellationToken);
            var order = _foodOrderStore.GetOrder(orderNo);
            if (order is not null)
            {
                await _foodLedgerSync.음식주문동기화Async(order, updatedBy, cancellationToken);
            }
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "음식 배달 확정 후 원장 동기화에 실패했습니다. OrderNo={OrderNo}", orderNo);
        }
    }

    private async Task ApplySettlementAsync(
        string driverId,
        DateTime acceptedAtUtc,
        CancellationToken cancellationToken)
    {
        try
        {
            await _settlementService.배차확정반영Async(driverId, acceptedAtUtc, cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "음식 배달 확정 후 기사 월정산 반영에 실패했습니다. DriverId={DriverId}", driverId);
        }
    }

    private bool 묶음동선가능(IReadOnlyList<(운송원장 Queue, 음식주문 Order)> assignments)
    {
        for (var firstIndex = 0; firstIndex < assignments.Count - 1; firstIndex++)
        {
            for (var secondIndex = firstIndex + 1; secondIndex < assignments.Count; secondIndex++)
            {
                var first = assignments[firstIndex];
                var second = assignments[secondIndex];
                if (first.Order.조리예상완료시각Utc.HasValue
                    && second.Order.조리예상완료시각Utc.HasValue
                    && (first.Order.조리예상완료시각Utc.Value - second.Order.조리예상완료시각Utc.Value).Duration()
                    > 묶음조리완료최대차이)
                {
                    return false;
                }

                var firstPickup = CreatePoint(first.Queue.픽업_위도, first.Queue.픽업_경도);
                var secondPickup = CreatePoint(second.Queue.픽업_위도, second.Queue.픽업_경도);
                var firstDropoff = CreatePoint(first.Queue.하차_위도, first.Queue.하차_경도);
                var secondDropoff = CreatePoint(second.Queue.하차_위도, second.Queue.하차_경도);
                if (firstPickup is null || secondPickup is null || firstDropoff is null || secondDropoff is null
                    || (_routeService.CalculateDistanceKm(firstPickup, secondPickup) ?? decimal.MaxValue) > 묶음픽업최대거리Km
                    || (_routeService.CalculateDistanceKm(firstDropoff, secondDropoff) ?? decimal.MaxValue) > 묶음전달최대거리Km)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private DriverWorkOfferDto ToOffer(
        운송원장 queue,
        음식주문 order,
        DriverLocationSnapshot? driverLocation)
    {
        var pickup = CreatePoint(queue.픽업_위도, queue.픽업_경도);
        var dropoff = CreatePoint(queue.하차_위도, queue.하차_경도);
        var deliveryDistance = pickup is not null && dropoff is not null
            ? _routeService.CalculateDistanceKm(pickup, dropoff)
            : null;
        var pickupDistance = driverLocation is not null && pickup is not null
            ? _routeService.CalculateDistanceKm(
                new 배차경로좌표(driverLocation.Latitude, driverLocation.Longitude),
                pickup)
            : null;
        var status = ResolveOfferStatus(queue, order);
        var menu = order.상품목록.Count == 0
            ? "음식 주문"
            : string.Join(", ", order.상품목록.Take(2).Select(x => $"{x.상품명} {x.수량}개"));
        var reason = pickupDistance.HasValue
            ? $"음식점까지 {pickupDistance.Value:0.0}km · 픽업 준비 {FormatReadyTime(order.조리예상완료시각Utc)}"
            : $"픽업 준비 {FormatReadyTime(order.조리예상완료시각Utc)}";

        return new DriverWorkOfferDto(
            queue.의뢰Id,
            기사앱식별자.FoodDeliveryDriverApp,
            기사도메인구분.음식배달,
            기사업무유형코드.음식배달,
            menu,
            $"{order.음식점명} 픽업 · {queue.하차_도로명주소} 전달",
            new DriverWorkStopDto(
                string.IsNullOrWhiteSpace(order.음식점명) ? "음식점" : order.음식점명,
                JoinAddress(queue.픽업_도로명주소, queue.픽업_상세주소),
                (double)(queue.픽업_위도 ?? 0m),
                (double)(queue.픽업_경도 ?? 0m),
                ToOffset(order.조리예상완료시각Utc)),
            new DriverWorkStopDto(
                "고객 주소",
                JoinAddress(queue.하차_도로명주소, queue.하차_상세주소),
                (double)(queue.하차_위도 ?? 0m),
                (double)(queue.하차_경도 ?? 0m),
                ToOffset(order.조리예상완료시각Utc?.AddMinutes(42))),
            CalculateDriverPayout(deliveryDistance),
            deliveryDistance.HasValue ? (double)deliveryDistance.Value : null,
            reason,
            status,
            ToOffset(queue.추천만료시각),
            [order.주문번호]);
    }

    private static void ApplyFoodOrderState(
        음식주문 order,
        string nextState,
        string nextDispatchState,
        string reason,
        DateTime now)
    {
        var previous = 음식주문상태코드.Normalize(order.상태);
        order.상태 = nextState;
        order.배차상태 = nextDispatchState;
        order.UpdatedAt = now;
        order.상태이력.Add(new 음식주문상태이력
        {
            이전상태 = previous,
            다음상태 = nextState,
            사유 = reason,
            전이시각Utc = now
        });
    }

    private static string ResolveOrderNo(운송원장 queue)
        => string.IsNullOrWhiteSpace(queue.원본의뢰Id) ? queue.의뢰Id : queue.원본의뢰Id;

    private static string ResolveOfferStatus(운송원장 queue, 음식주문 order)
    {
        var foodState = 음식주문상태코드.Normalize(order.상태);
        if (foodState == 음식주문상태코드.전달완료)
        {
            return DriverWorkOfferStatus.Completed;
        }

        if (foodState == 음식주문상태코드.픽업완료)
        {
            return DriverWorkOfferStatus.MovingToDropoff;
        }

        return queue.배차큐단계 == 상태값.배차큐단계.확정
            ? DriverWorkOfferStatus.MovingToPickup
            : DriverWorkOfferStatus.Recommended;
    }

    private static 배차경로좌표? CreatePoint(decimal? latitude, decimal? longitude)
        => latitude.HasValue && longitude.HasValue
            ? new 배차경로좌표(latitude.Value, longitude.Value)
            : null;

    private static decimal CalculateDriverPayout(decimal? deliveryDistanceKm)
    {
        const decimal minimumPayout = 2500m;
        const decimal includedDistanceKm = 1m;
        const decimal perAdditionalKm = 900m;
        var distance = Math.Max(0m, deliveryDistanceKm ?? 0m);
        return Math.Max(minimumPayout, minimumPayout + Math.Max(0m, distance - includedDistanceKm) * perAdditionalKm);
    }

    private static DateTimeOffset? ToOffset(DateTime? value)
        => value.HasValue
            ? new DateTimeOffset(DateTime.SpecifyKind(value.Value, DateTimeKind.Utc))
            : null;

    private static string FormatReadyTime(DateTime? value)
        => value.HasValue ? $"{value.Value.ToLocalTime():HH:mm}" : "시간 미정";

    private static string JoinAddress(string primary, string detail)
        => string.IsNullOrWhiteSpace(detail) ? primary : $"{primary} {detail}";

    private static FoodDeliveryDriverActionResponse Response(
        string offerId,
        string orderNo,
        string status,
        string message)
        => new()
        {
            OfferId = offerId,
            OrderIds = [orderNo],
            Status = status,
            Message = message
        };
}
