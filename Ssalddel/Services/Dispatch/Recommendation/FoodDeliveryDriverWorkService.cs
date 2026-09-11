using FluentResults;
using System.Collections.Concurrent;
using System.Data;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Application.Driver.DispatchAction;
using Ssalddel.Application.Food.Events;
using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Common.Operations;
using 살뜰.Services.Dispatch.Common;
using Ssalddel.Contracts.Food;
using Ssalddel.Services.Community;
using Ssalddel.Services.Food;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using 살뜰.Data;
using 살뜰.Services.Dispatch.Coordination;
using 살뜰.Services.Dispatch.Engine;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.Services.Settlement;
using 살뜰.Services.Storage.Local;
using 살뜰.도메인.공통;
using 살뜰.도메인.음식;
using 살뜰.도메인.운송;

namespace 살뜰.Services.Dispatch.Recommendation;

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
        string? reasonCode,
        CancellationToken cancellationToken = default);

    Task<Result<FoodDeliveryDriverActionResponse>> 픽업완료Async(
        string driverId,
        string offerId,
        CancellationToken cancellationToken = default);

    Task<Result<FoodDeliveryDriverActionResponse>> 가게도착Async(
        string driverId,
        string offerId,
        음식배달가게도착요청 request,
        CancellationToken cancellationToken = default);

    Task<Result<FoodDeliveryDriverActionResponse>> 중단Async(
        string driverId,
        string offerId,
        음식배달중단요청 request,
        CancellationToken cancellationToken = default);

    Task<Result<FoodDeliveryDriverActionResponse>> 전달완료Async(
        string driverId,
        string offerId,
        CancellationToken cancellationToken = default);
}

public sealed class 음식배달기사업무Service : I음식배달기사업무Service
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> 수락Gates = new(StringComparer.Ordinal);
    private const decimal 묶음픽업최대거리Km = 1.5m;
    private const decimal 묶음전달최대거리Km = 3m;
    private static readonly TimeSpan 묶음조리완료최대차이 = TimeSpan.FromMinutes(12);

    private readonly SsalddelContext _db;
    private readonly IDriverLocationStore _locationStore;
    private readonly I배차추천경로Service _routeService;
    private readonly I배차대기원장전환Service _queueTransitionService;
    private readonly I음식배달권실행공간Store _deliveryScopeStore;
    private readonly ISsalddelFoodOrderStore _foodOrderStore;
    private readonly I음식마트원장동기화OutboxService _foodLedgerOutbox;
    private readonly I운송원장Mongo동기화Service _transportLedgerSync;
    private readonly I음식점주문실시간알림Service _restaurantNotification;
    private readonly I기사월정산Service _settlementService;
    private readonly IPublisher _publisher;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly ILogger<음식배달기사업무Service> _logger;

    public 음식배달기사업무Service(
        SsalddelContext db,
        IDriverLocationStore locationStore,
        I배차추천경로Service routeService,
        I배차대기원장전환Service queueTransitionService,
        I음식배달권실행공간Store deliveryScopeStore,
        ISsalddelFoodOrderStore foodOrderStore,
        I음식마트원장동기화OutboxService foodLedgerOutbox,
        I운송원장Mongo동기화Service transportLedgerSync,
        I음식점주문실시간알림Service restaurantNotification,
        I기사월정산Service settlementService,
        IPublisher publisher,
        ICurrentUserAccessor currentUserAccessor,
        ILogger<음식배달기사업무Service> logger)
    {
        _db = db;
        _locationStore = locationStore;
        _routeService = routeService;
        _queueTransitionService = queueTransitionService;
        _deliveryScopeStore = deliveryScopeStore;
        _foodOrderStore = foodOrderStore;
        _foodLedgerOutbox = foodLedgerOutbox;
        _transportLedgerSync = transportLedgerSync;
        _restaurantNotification = restaurantNotification;
        _settlementService = settlementService;
        _publisher = publisher;
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
            .Where(x => Enumerable.Contains(orderNos, x.주문번호))
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

        if (normalizedIds.Length > 음식배달기사활성업무Policy.MaxActiveDeliveries)
        {
            return 활성업무상한초과<FoodDeliveryDriverActionResponse>(0, normalizedIds.Length);
        }

        var gate = 수락Gates.GetOrAdd(driverId, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        Result<List<FoodDeliveryAssignment>> transactionResult;
        try
        {
            var executionStrategy = _db.Database.CreateExecutionStrategy();
            transactionResult = await executionStrategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _db.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    cancellationToken);
                var currentActiveDeliveries = await _db.운송원장
                    .CountAsync(x => x.배차업무유형 == 상태값.배차업무유형.음식배달
                                     && x.상태 != 상태값.배차상태.인수완료
                                     && x.배차큐단계 == 상태값.배차큐단계.확정
                                     && (x.확정기사Id == driverId || x.기사_운송자 == driverId),
                        cancellationToken);
                if (!음식배달기사활성업무Policy.수락가능한가(
                        currentActiveDeliveries,
                        normalizedIds.Length))
                {
                    return 활성업무상한초과<List<FoodDeliveryAssignment>>(
                        currentActiveDeliveries,
                        normalizedIds.Length);
                }
                var assignments = new List<(운송원장 Queue, 음식주문 Order)>(normalizedIds.Length);
                foreach (var id in normalizedIds)
                {
                    var loaded = await LoadForActionAsync(id, cancellationToken);
                    if (loaded is null)
                    {
                        return Result.Fail<List<FoodDeliveryAssignment>>($"{id} 음식 배달 제안을 찾을 수 없습니다.");
                    }

                    var (queue, order) = loaded.Value;
                    if (!배차응답가능정책.추천수락가능(queue, driverId, DateTime.UtcNow))
                    {
                        return Result.Fail<List<FoodDeliveryAssignment>>(
                            $"{id} 제안은 이미 만료되었거나 다른 기사에게 배정되었습니다.");
                    }

                    if (음식주문상태코드.Normalize(order.상태) is
                        음식주문상태코드.거절 or
                        음식주문상태코드.취소 or
                        음식주문상태코드.전달완료 or
                        음식주문상태코드.수령확인)
                    {
                        return Result.Fail<List<FoodDeliveryAssignment>>(
                            $"{id} 주문은 취소되었거나 이미 전달 완료되었습니다.");
                    }

                    assignments.Add((queue, order));
                }

                if (requireBundle && !묶음동선가능(assignments))
                {
                    return Result.Fail<List<FoodDeliveryAssignment>>(
                        "조리 완료 시각 또는 픽업·전달 동선이 묶음 배달 기준을 벗어났습니다. 목록을 새로고침해 주세요.");
                }

                var changedAtUtc = DateTime.UtcNow;
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
                    queue.UpdatedAt = changedAtUtc;

                    ApplyFoodOrderState(
                        order,
                        음식주문상태코드.기사배정,
                        음식주문배차상태코드.기사배정,
                        requireBundle ? "F드라이버 묶음 배차 수락" : "F드라이버 배차 수락",
                        changedAtUtc);
                    var attempt = await 수락시도생성Async(queue, order, driverId, changedAtUtc, cancellationToken);
                    _db.운영배차활동사건.Add(운영배차활동사건Factory.수락(
                        driverId,
                        queue.의뢰Id,
                        order.주문번호,
                        queue.추천라운드,
                        changedAtUtc,
                        attempt.시도StableId));
                }

                var saveResult = await SaveTransactionAsync(transaction, cancellationToken);
                return saveResult.IsFailed
                    ? Result.Fail<List<FoodDeliveryAssignment>>(saveResult.Errors)
                    : Result.Ok(assignments
                        .Select(assignment => new FoodDeliveryAssignment(
                            assignment.Queue,
                            assignment.Order.주문번호))
                        .ToList());
            });
        }
        finally
        {
            gate.Release();
        }
        if (transactionResult.IsFailed)
        {
            return Result.Fail<FoodDeliveryDriverActionResponse>(transactionResult.Errors);
        }

        var completedAssignments = transactionResult.Value;
        var acceptedAtUtc = DateTime.UtcNow;
        foreach (var assignment in completedAssignments)
        {
            await ApplySettlementAsync(driverId, acceptedAtUtc, cancellationToken);
            await _deliveryScopeStore.Remove운송의뢰Async(assignment.Queue.의뢰Id, cancellationToken);
            await SyncLedgersAsync(assignment.Queue, assignment.OrderNo, driverId, cancellationToken);
            await NotifyRestaurantAsync(
                assignment.OrderNo,
                requireBundle ? "기사가 묶음 배달을 수락했습니다." : "기사가 배달을 수락했습니다.",
                cancellationToken);
            await PublishCommunityActivityAsync(
                assignment.Queue.의뢰Id,
                assignment.OrderNo,
                "기사배정",
                assignment.Queue.UpdatedAt,
                cancellationToken);
        }

        return Result.Ok(new FoodDeliveryDriverActionResponse
        {
            OfferId = completedAssignments.Count == 1
                ? completedAssignments[0].Queue.의뢰Id
                : $"bundle:{string.Join(':', completedAssignments.Select(x => x.Queue.의뢰Id))}",
            OrderIds = completedAssignments.Select(x => x.OrderNo).ToArray(),
            Status = DriverWorkOfferStatus.Accepted,
            Message = completedAssignments.Count == 1
                ? "음식 배달 제안을 수락했습니다."
                : $"묶음 배달 {completedAssignments.Count}건을 한 번에 확정했습니다."
        });
    }

    private static Result<T> 활성업무상한초과<T>(int currentActiveDeliveries, int requestedDeliveries)
        => Result.Fail<T>(new Error(음식배달기사활성업무Policy.초과안내(
                currentActiveDeliveries,
                requestedDeliveries))
            .WithMetadata("StatusCode", StatusCodes.Status409Conflict)
            .WithMetadata("ErrorCode", 음식배달기사활성업무Policy.LimitExceededErrorCode));

    public async Task<Result<FoodDeliveryDriverActionResponse>> 거절Async(
        string driverId,
        string offerId,
        string? reasonCode,
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

        string normalizedReasonCode;
        try
        {
            normalizedReasonCode = 운영배차거절사유Code.정규화(reasonCode);
        }
        catch (ArgumentException exception)
        {
            return Result.Fail<FoodDeliveryDriverActionResponse>(exception.Message);
        }

        var transition = await _queueTransitionService.추천거절처리Async(
            offerId,
            driverId,
            normalizedReasonCode,
            cancellationToken);
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
            await SyncFoodLedgerAsync(orderNo, driverId, cancellationToken);
            await NotifyRestaurantAsync(
                orderNo,
                "기사가 제안을 거절해 다른 기사 배차를 계속합니다.",
                cancellationToken);
        }

        return Result.Ok(Response(offerId, orderNo, DriverWorkOfferStatus.Rejected, "음식 배달 제안을 거절했습니다."));
    }

    public async Task<Result<FoodDeliveryDriverActionResponse>> 가게도착Async(
        string driverId,
        string offerId,
        음식배달가게도착요청 request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.클라이언트요청Id == Guid.Empty)
            return Result.Fail<FoodDeliveryDriverActionResponse>("가게 도착 클라이언트 요청 ID가 필요합니다.");

        var strategy = _db.Database.CreateExecutionStrategy();
        var result = await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
            var loaded = await LoadForActionAsync(offerId, cancellationToken);
            if (loaded is null) return Result.Fail<FoodDeliveryStateChange>("음식 배달 업무를 찾을 수 없습니다.");
            var (queue, order) = loaded.Value;
            if (!string.Equals(queue.확정기사Id, driverId, StringComparison.Ordinal))
                return Result.Fail<FoodDeliveryStateChange>("확정된 배달 기사만 가게 도착을 기록할 수 있습니다.");

            var now = DateTime.UtcNow;
            var attempt = await 현재시도조회또는추정Async(queue, order, driverId, cancellationToken);
            if (attempt.마지막요청Id == request.클라이언트요청Id && attempt.가게도착시각Utc.HasValue)
            {
                await transaction.RollbackAsync(cancellationToken);
                var duplicateAudit = FoodDeliveryLocationAudit.Evaluate(
                    _locationStore.TryGetLatest(driverId, out var duplicateLatest) ? duplicateLatest : null,
                    queue.픽업_위도,
                    queue.픽업_경도,
                    now);
                return Result.Ok(new FoodDeliveryStateChange(
                    queue,
                    order.주문번호,
                    Response(offerId, order.주문번호, DriverWorkOfferStatus.MovingToPickup, "이미 가게 도착이 기록됐습니다.", duplicateAudit, attempt),
                    false));
            }
            if (request.예상시도Revision.HasValue && request.예상시도Revision.Value != attempt.Revision)
                return Result.Fail<FoodDeliveryStateChange>("배달 시도가 다른 요청에서 먼저 변경됐습니다.");

            var locationAudit = FoodDeliveryLocationAudit.Evaluate(
                _locationStore.TryGetLatest(driverId, out var latest) ? latest : null,
                queue.픽업_위도,
                queue.픽업_경도,
                now);
            if (!attempt.가게도착시각Utc.HasValue)
            {
                attempt.가게도착시각Utc = now;
                attempt.상태Code = 음식배달시도상태Code.가게도착;
                attempt.마지막요청Id = request.클라이언트요청Id;
                attempt.Revision++;
                attempt.UpdatedAtUtc = now;
                _db.운송이벤트.Add(위치감사사건(queue, driverId, "RestaurantArrival", locationAudit, now, attempt.시도StableId));
                var saved = await SaveTransactionAsync(transaction, cancellationToken);
                if (saved.IsFailed) return Result.Fail<FoodDeliveryStateChange>(saved.Errors);
                return Result.Ok(new FoodDeliveryStateChange(
                    queue,
                    order.주문번호,
                    Response(offerId, order.주문번호, DriverWorkOfferStatus.MovingToPickup, "가게 도착을 기록했습니다.", locationAudit, attempt),
                    true));
            }

            await transaction.RollbackAsync(cancellationToken);
            return Result.Ok(new FoodDeliveryStateChange(
                queue,
                order.주문번호,
                Response(offerId, order.주문번호, DriverWorkOfferStatus.MovingToPickup, "이미 가게 도착이 기록됐습니다.", locationAudit, attempt),
                false));
        });
        if (result.IsFailed) return Result.Fail<FoodDeliveryDriverActionResponse>(result.Errors);
        if (result.Value.Changed)
            await PublishCommunityActivityAsync(offerId, result.Value.OrderNo, "RestaurantArrived", DateTime.UtcNow, cancellationToken);
        return Result.Ok(result.Value.Response);
    }

    public async Task<Result<FoodDeliveryDriverActionResponse>> 중단Async(
        string driverId,
        string offerId,
        음식배달중단요청 request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.클라이언트요청Id == Guid.Empty)
            return Result.Fail<FoodDeliveryDriverActionResponse>("배달 중단 클라이언트 요청 ID가 필요합니다.");
        if (request.메모?.Trim().Length > 500)
            return Result.Fail<FoodDeliveryDriverActionResponse>("배달 중단 메모는 500자 이하여야 합니다.");
        string reasonCode;
        try { reasonCode = 음식배달중단사유Code.정규화(request.사유Code); }
        catch (ArgumentException ex) { return Result.Fail<FoodDeliveryDriverActionResponse>(ex.Message); }

        var strategy = _db.Database.CreateExecutionStrategy();
        var result = await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            var loaded = await LoadForActionAsync(offerId, cancellationToken);
            if (loaded is null) return Result.Fail<FoodDeliveryStateChange>("음식 배달 업무를 찾을 수 없습니다.");
            var (queue, order) = loaded.Value;
            if (!string.Equals(queue.확정기사Id, driverId, StringComparison.Ordinal))
                return Result.Fail<FoodDeliveryStateChange>("확정된 배달 기사만 중단을 요청할 수 있습니다.");

            var now = DateTime.UtcNow;
            var attempt = await 현재시도조회또는추정Async(queue, order, driverId, cancellationToken);
            if (attempt.마지막요청Id == request.클라이언트요청Id && attempt.중단시각Utc.HasValue)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result.Ok(new FoodDeliveryStateChange(queue, order.주문번호,
                    Response(offerId, order.주문번호, "Interrupted", "이미 배달 중단이 처리됐습니다.", attempt: attempt), false));
            }
            if (request.예상시도Revision.HasValue && request.예상시도Revision.Value != attempt.Revision)
                return Result.Fail<FoodDeliveryStateChange>("배달 시도가 다른 요청에서 먼저 변경됐습니다.");
            if (attempt.중단시각Utc.HasValue || attempt.전달완료시각Utc.HasValue)
                return Result.Fail<FoodDeliveryStateChange>("이미 종료된 배달 시도입니다.");

            var postPickup = attempt.픽업완료시각Utc.HasValue
                             || 음식주문상태코드.Normalize(order.상태) == 음식주문상태코드.픽업완료;
            if (reasonCode == 음식배달중단사유Code.조리지연)
            {
                if (postPickup || !attempt.가게도착시각Utc.HasValue)
                    return Result.Fail<FoodDeliveryStateChange>("조리 지연 중단은 픽업 전에 가게 도착을 기록한 경우만 가능합니다.");
                if (!attempt.표시준비예정시각Utc.HasValue || now < attempt.표시준비예정시각Utc.Value.AddMinutes(10))
                    return Result.Fail<FoodDeliveryStateChange>("표시된 준비 예정 시각을 10분 초과한 뒤 조리 지연으로 중단할 수 있습니다.");
                if (픽업준비됨(order, attempt.수락시각Utc))
                    return Result.Fail<FoodDeliveryStateChange>("이미 픽업 준비가 완료된 주문은 조리 지연으로 중단할 수 없습니다.");
            }

            var responsibility = await 초기책임판정Async(driverId, reasonCode, now, cancellationToken);
            attempt.상태Code = 음식배달시도상태Code.중단;
            attempt.중단시각Utc = now;
            attempt.중단사유Code = reasonCode;
            attempt.중단메모 = string.IsNullOrWhiteSpace(request.메모) ? null : request.메모.Trim();
            attempt.책임Code = responsibility;
            attempt.마지막요청Id = request.클라이언트요청Id;
            attempt.Revision++;
            attempt.UpdatedAtUtc = now;
            if (postPickup)
            {
                attempt.재조리요청StableId = $"recook:{attempt.시도StableId}";
                attempt.재조리요청시각Utc = now;
                order.조리예상완료시각Utc = now.AddMinutes(order.적용조리분 ?? order.플랫폼참고조리분 ?? 20);
            }

            var nextState = postPickup
                ? 음식주문상태코드.조리중
                : 픽업준비됨(order, attempt.수락시각Utc)
                    ? 음식주문상태코드.픽업대기
                    : 음식주문상태코드.조리중;
            ApplyFoodOrderState(order, nextState, 음식주문배차상태코드.배차대기,
                postPickup ? "픽업 후 배달 중단 · 재조리·재배차" : $"픽업 전 배달 중단 · {reasonCode}", now);
            배차재추천상태Policy.적용(queue, driverId, now);
            _db.운영배차활동사건.Add(운영배차활동사건Factory.중단(driverId, offerId, order.주문번호, queue.추천라운드, now, attempt.시도StableId, reasonCode, responsibility));
            _db.운영배차활동사건.Add(운영배차활동사건Factory.균형반환(driverId, offerId, order.주문번호, queue.추천라운드, now, attempt.시도StableId, responsibility));
            var saved = await SaveTransactionAsync(transaction, cancellationToken);
            return saved.IsFailed
                ? Result.Fail<FoodDeliveryStateChange>(saved.Errors)
                : Result.Ok(new FoodDeliveryStateChange(queue, order.주문번호,
                    Response(offerId, order.주문번호, "Interrupted", postPickup ? "배달을 중단하고 재조리·재배차를 시작했습니다." : "배달을 중단하고 재배차를 시작했습니다.", attempt: attempt), true, postPickup));
        });

        if (result.IsFailed) return Result.Fail<FoodDeliveryDriverActionResponse>(result.Errors);
        if (result.Value.Changed)
        {
            try { await _queueTransitionService.추천대기처리Async(offerId, cancellationToken); }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested) { _logger.LogWarning(ex, "배달 중단 후 즉시 재배차에 실패했습니다. 영속 대기열에서 재시도합니다. OfferId={OfferId}", offerId); }
            await SyncLedgersAsync(result.Value.Queue, result.Value.OrderNo, driverId, cancellationToken);
            await NotifyRestaurantAsync(
                result.Value.OrderNo,
                result.Value.RequiresRecook
                    ? "픽업 후 배달 중단으로 재조리와 재배차를 진행합니다."
                    : "배달 중단 후 재배차를 진행합니다.",
                cancellationToken);
            await PublishCommunityActivityAsync(
                offerId,
                result.Value.OrderNo,
                result.Value.RequiresRecook ? "DeliveryInterruptedRecook" : "DeliveryInterrupted",
                DateTime.UtcNow,
                cancellationToken);
        }
        return Result.Ok(result.Value.Response);
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
        var executionStrategy = _db.Database.CreateExecutionStrategy();
        var transactionResult = await executionStrategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
            var loaded = await LoadForActionAsync(offerId, cancellationToken);
            if (loaded is null)
            {
                return Result.Fail<FoodDeliveryStateChange>("음식 배달 업무를 찾을 수 없습니다.");
            }

            var (queue, order) = loaded.Value;
            if (!string.Equals(queue.확정기사Id, driverId, StringComparison.Ordinal)
                || queue.배차큐단계 is not (상태값.배차큐단계.확정 or 상태값.배차큐단계.종료))
            {
                return Result.Fail<FoodDeliveryStateChange>("이 음식 배달을 진행할 수 있는 기사가 아닙니다.");
            }

            var attempt = await 현재시도조회또는추정Async(queue, order, driverId, cancellationToken);
            var currentOrderState = 음식주문상태코드.Normalize(order.상태);
            _locationStore.TryGetLatest(driverId, out var latestLocation);
            var locationAudit = FoodDeliveryLocationAudit.Evaluate(
                latestLocation,
                nextOrderState == 음식주문상태코드.픽업완료 ? queue.픽업_위도 : queue.하차_위도,
                nextOrderState == 음식주문상태코드.픽업완료 ? queue.픽업_경도 : queue.하차_경도,
                DateTime.UtcNow);
            if (string.Equals(currentOrderState, nextOrderState, StringComparison.Ordinal))
            {
                return Result.Ok(new FoodDeliveryStateChange(
                    queue,
                    order.주문번호,
                    Response(offerId, order.주문번호, responseState, $"이미 {reason} 상태입니다.", locationAudit, attempt),
                    false));
            }

            if (nextOrderState == 음식주문상태코드.픽업완료
                && currentOrderState != 음식주문상태코드.기사배정)
            {
                return Result.Fail<FoodDeliveryStateChange>("기사 배정이 완료된 주문만 픽업 완료할 수 있습니다.");
            }

            if (nextOrderState == 음식주문상태코드.픽업완료
                && !order.상태이력.Any(history => history.다음상태 == 음식주문상태코드.픽업대기
                                                      || history.사유.StartsWith("음식점 픽업 준비 완료", StringComparison.Ordinal)))
            {
                return Result.Fail<FoodDeliveryStateChange>("음식점이 픽업 준비를 완료한 주문만 픽업할 수 있습니다.");
            }

            if (nextOrderState == 음식주문상태코드.전달완료
                && currentOrderState != 음식주문상태코드.픽업완료)
            {
                return Result.Fail<FoodDeliveryStateChange>("픽업 완료된 주문만 고객 전달 완료할 수 있습니다.");
            }

            var changedAtUtc = DateTime.UtcNow;
            ApplyFoodOrderState(order, nextOrderState, nextDispatchState, reason, changedAtUtc);
            _db.운송이벤트.Add(new 운송이벤트
            {
                의뢰Id = queue.의뢰Id,
                이벤트타입 = 운송이벤트유형.음식배달위치감사,
                이벤트시각 = changedAtUtc,
                메타데이터 = JsonSerializer.Serialize(new
                {
                    Action = nextOrderState,
                    DriverId = driverId,
                    locationAudit.Code,
                    locationAudit.DistanceKm,
                    locationAudit.DriverLocationAtUtc,
                    locationAudit.DriverLatitude,
                    locationAudit.DriverLongitude,
                    locationAudit.TargetLatitude,
                    locationAudit.TargetLongitude,
                    Policy = "AdvisoryEvidenceOnly"
                })
            });
            queue.상태 = nextTransportState;
            queue.UpdatedAt = changedAtUtc;
            if (nextOrderState == 음식주문상태코드.픽업완료)
            {
                queue.출발_픽업 ??= changedAtUtc;
                attempt.가게도착시각Utc ??= changedAtUtc;
                attempt.픽업완료시각Utc = changedAtUtc;
                attempt.현장대기초 = Math.Max(0, (int)(changedAtUtc - attempt.가게도착시각Utc.Value).TotalSeconds);
                attempt.상태Code = 음식배달시도상태Code.픽업완료;
                attempt.Revision++;
                attempt.UpdatedAtUtc = changedAtUtc;
            }
            else
            {
                queue.도착 ??= changedAtUtc;
                queue.배차큐단계 = 상태값.배차큐단계.종료;
                queue.배차노출상태 = 상태값.배차노출상태.종료;
                attempt.전달완료시각Utc = changedAtUtc;
                attempt.상태Code = 음식배달시도상태Code.전달완료;
                attempt.Revision++;
                attempt.UpdatedAtUtc = changedAtUtc;
                _db.운영배차활동사건.Add(운영배차활동사건Factory.완료(
                    driverId,
                    queue.의뢰Id,
                    order.주문번호,
                    queue.추천라운드,
                    changedAtUtc,
                    attempt.시도StableId));
            }

            var saveResult = await SaveTransactionAsync(transaction, cancellationToken);
            return saveResult.IsFailed
                ? Result.Fail<FoodDeliveryStateChange>(saveResult.Errors)
                : Result.Ok(new FoodDeliveryStateChange(
                    queue,
                    order.주문번호,
                    Response(offerId, order.주문번호, responseState, $"{reason} 처리했습니다.", locationAudit, attempt),
                    true));
        });
        if (transactionResult.IsFailed)
        {
            return Result.Fail<FoodDeliveryDriverActionResponse>(transactionResult.Errors);
        }

        var stateChange = transactionResult.Value;
        if (stateChange.Changed)
        {
            await SyncLedgersAsync(stateChange.Queue, stateChange.OrderNo, driverId, cancellationToken);
            await NotifyRestaurantAsync(stateChange.OrderNo, reason, cancellationToken);
            await PublishCommunityActivityAsync(
                stateChange.Queue.의뢰Id,
                stateChange.OrderNo,
                nextOrderState,
                stateChange.Queue.UpdatedAt,
                cancellationToken);
        }

        return Result.Ok(stateChange.Response);
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

    private async Task<음식배달시도> 수락시도생성Async(
        운송원장 queue,
        음식주문 order,
        string driverId,
        DateTime acceptedAtUtc,
        CancellationToken cancellationToken)
    {
        var previousAttempts = await _db.음식배달시도
            .Where(x => x.주문번호 == order.주문번호)
            .OrderByDescending(x => x.시도순번)
            .ToListAsync(cancellationToken);
        var sequence = previousAttempts.Count == 0 ? 1 : previousAttempts[0].시도순번 + 1;
        var followsPreparationDelay = previousAttempts.Any(x =>
            x.상태Code == 음식배달시도상태Code.중단
            && x.중단사유Code == 음식배달중단사유Code.조리지연);
        var displayedReadyAtUtc = order.조리예상완료시각Utc;
        if (followsPreparationDelay)
        {
            var minimumReadyAtUtc = acceptedAtUtc.AddMinutes(10);
            if (!displayedReadyAtUtc.HasValue || displayedReadyAtUtc.Value < minimumReadyAtUtc)
            {
                displayedReadyAtUtc = minimumReadyAtUtc;
            }
        }

        var attempt = new 음식배달시도
        {
            시도StableId = 배달시도StableId(order.주문번호, sequence),
            주문번호 = order.주문번호,
            제안Id = queue.의뢰Id,
            기사Id = driverId,
            추천라운드 = queue.추천라운드,
            시도순번 = sequence,
            상태Code = 음식배달시도상태Code.수락,
            Revision = 1,
            수락시각Utc = acceptedAtUtc,
            표시준비예정시각Utc = displayedReadyAtUtc,
            조리지연재배차여부 = followsPreparationDelay,
            CreatedAtUtc = acceptedAtUtc,
            UpdatedAtUtc = acceptedAtUtc
        };
        _db.음식배달시도.Add(attempt);
        return attempt;
    }

    private async Task<음식배달시도> 현재시도조회또는추정Async(
        운송원장 queue,
        음식주문 order,
        string driverId,
        CancellationToken cancellationToken)
    {
        var attempt = await _db.음식배달시도
            .Where(x => x.주문번호 == order.주문번호
                        && x.제안Id == queue.의뢰Id
                        && x.기사Id == driverId)
            .OrderByDescending(x => x.시도순번)
            .FirstOrDefaultAsync(cancellationToken);
        if (attempt is not null)
        {
            return attempt;
        }

        var sequence = (await _db.음식배달시도
            .Where(x => x.주문번호 == order.주문번호)
            .MaxAsync(x => (int?)x.시도순번, cancellationToken) ?? 0) + 1;
        var acceptedAtUtc = order.상태이력
            .Where(x => x.다음상태 == 음식주문상태코드.기사배정)
            .OrderByDescending(x => x.전이시각Utc)
            .Select(x => x.전이시각Utc)
            .FirstOrDefault();
        if (acceptedAtUtc == default)
        {
            acceptedAtUtc = queue.UpdatedAt == default ? DateTime.UtcNow : queue.UpdatedAt;
        }

        attempt = new 음식배달시도
        {
            시도StableId = 배달시도StableId(order.주문번호, sequence),
            주문번호 = order.주문번호,
            제안Id = queue.의뢰Id,
            기사Id = driverId,
            추천라운드 = queue.추천라운드,
            시도순번 = sequence,
            상태Code = 음식주문상태코드.Normalize(order.상태) == 음식주문상태코드.픽업완료
                ? 음식배달시도상태Code.픽업완료
                : 음식배달시도상태Code.수락,
            Revision = 1,
            수락시각Utc = DateTime.SpecifyKind(acceptedAtUtc, DateTimeKind.Utc),
            표시준비예정시각Utc = order.조리예상완료시각Utc,
            픽업완료시각Utc = 음식주문상태코드.Normalize(order.상태) == 음식주문상태코드.픽업완료
                ? queue.출발_픽업
                : null,
            유산추정여부 = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        _db.음식배달시도.Add(attempt);
        return attempt;
    }

    private async Task<string> 초기책임판정Async(
        string driverId,
        string reasonCode,
        DateTime occurredAtUtc,
        CancellationToken cancellationToken)
    {
        if (reasonCode == 음식배달중단사유Code.조리지연)
        {
            return 운영배차책임Code.음식점;
        }

        if (reasonCode is 음식배달중단사유Code.사고
            or 음식배달중단사유Code.배터리부족
            or 음식배달중단사유Code.배달수단고장
            or 음식배달중단사유Code.위험기상)
        {
            return 운영배차책임Code.보호대상;
        }

        if (reasonCode != 음식배달중단사유Code.개인긴급)
        {
            return 운영배차책임Code.미확정;
        }

        var timeZone = 운영배차단기지표Calculator.대한민국시간대조회();
        var localDate = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(occurredAtUtc, DateTimeKind.Utc), timeZone));
        var localStart = localDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var localEnd = localDate.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var startUtc = TimeZoneInfo.ConvertTimeToUtc(localStart, timeZone);
        var endUtc = TimeZoneInfo.ConvertTimeToUtc(localEnd, timeZone);
        var alreadyProtectedToday = await _db.음식배달시도.AnyAsync(x =>
            x.기사Id == driverId
            && x.중단사유Code == 음식배달중단사유Code.개인긴급
            && x.중단시각Utc >= startUtc
            && x.중단시각Utc < endUtc
            && x.책임Code == 운영배차책임Code.보호대상,
            cancellationToken);
        return alreadyProtectedToday ? 운영배차책임Code.미확정 : 운영배차책임Code.보호대상;
    }

    private static bool 픽업준비됨(음식주문 order, DateTime acceptedAtUtc)
    {
        var assignment = order.상태이력
            .Where(x => x.다음상태 == 음식주문상태코드.기사배정
                        && x.전이시각Utc >= acceptedAtUtc.AddSeconds(-1))
            .OrderBy(x => x.전이시각Utc)
            .FirstOrDefault();
        if (assignment?.이전상태 == 음식주문상태코드.픽업대기)
        {
            return true;
        }

        return order.상태이력.Any(x =>
            x.전이시각Utc >= acceptedAtUtc
            && (x.다음상태 == 음식주문상태코드.픽업대기
                || x.사유.StartsWith("음식점 픽업 준비 완료", StringComparison.Ordinal)));
    }

    private static 운송이벤트 위치감사사건(
        운송원장 queue,
        string driverId,
        string action,
        FoodDeliveryLocationAuditResult locationAudit,
        DateTime occurredAtUtc,
        string attemptId)
        => new()
        {
            의뢰Id = queue.의뢰Id,
            이벤트타입 = 운송이벤트유형.음식배달위치감사,
            이벤트시각 = occurredAtUtc,
            메타데이터 = JsonSerializer.Serialize(new
            {
                Action = action,
                DriverId = driverId,
                DeliveryAttemptId = attemptId,
                locationAudit.Code,
                locationAudit.DistanceKm,
                locationAudit.DriverLocationAtUtc,
                locationAudit.DriverLatitude,
                locationAudit.DriverLongitude,
                locationAudit.TargetLatitude,
                locationAudit.TargetLongitude,
                Policy = "AdvisoryEvidenceOnly"
            })
        };

    private static string 배달시도StableId(string orderNo, int sequence)
    {
        var hash = Convert.ToHexString(SHA256.HashData(
                Encoding.UTF8.GetBytes($"{orderNo}|{sequence}")))
            .ToLowerInvariant();
        return $"food-delivery-attempt:{hash}";
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

    private sealed record FoodDeliveryAssignment(운송원장 Queue, string OrderNo);

    private sealed record FoodDeliveryStateChange(
        운송원장 Queue,
        string OrderNo,
        FoodDeliveryDriverActionResponse Response,
        bool Changed,
        bool RequiresRecook = false);

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
                await _foodLedgerOutbox.음식주문예약후즉시처리Async(
                    order,
                    updatedBy,
                    BuildFoodLedgerIdempotencyKey(order, updatedBy),
                    cancellationToken);
            }
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "음식 배달 확정 후 원장 동기화에 실패했습니다. OrderNo={OrderNo}", orderNo);
        }
    }

    private async Task SyncFoodLedgerAsync(
        string orderNo,
        string updatedBy,
        CancellationToken cancellationToken)
    {
        try
        {
            var order = _foodOrderStore.GetOrder(orderNo);
            if (order is not null)
            {
                await _foodLedgerOutbox.음식주문예약후즉시처리Async(
                    order,
                    updatedBy,
                    BuildFoodLedgerIdempotencyKey(order, updatedBy),
                    cancellationToken);
            }
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "음식 주문 상태 변경 후 원장 동기화에 실패했습니다. OrderNo={OrderNo}", orderNo);
        }
    }

    private static string BuildFoodLedgerIdempotencyKey(
        음식주문응답 order,
        string updatedBy)
    {
        var latestTransition = order.상태이력
            .OrderByDescending(x => x.전이시각Utc)
            .FirstOrDefault()
            ?.전이시각Utc
            .Ticks ?? 0L;
        return $"food-driver:{order.주문번호}:{order.상태}:{order.배차상태}:{updatedBy}:{latestTransition}";
    }

    private async Task NotifyRestaurantAsync(
        string orderNo,
        string reason,
        CancellationToken cancellationToken)
    {
        try
        {
            var order = _foodOrderStore.GetOrder(orderNo);
            if (order is not null)
            {
                await _restaurantNotification.주문상태변경알림발송Async(order, reason, cancellationToken);
            }
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                ex,
                "음식 배달 상태 변경 후 음식점 실시간 알림에 실패했습니다. OrderNo={OrderNo}",
                orderNo);
        }
    }

    private async Task PublishCommunityActivityAsync(
        string deliveryId,
        string orderNo,
        string status,
        DateTime occurredAtUtc,
        CancellationToken cancellationToken)
    {
        try
        {
            await _publisher.Publish(
                new 음식배달인계상태변경됨Event(
                    deliveryId,
                    orderNo,
                    status,
                    occurredAtUtc,
                    $"food-delivery:{deliveryId}:{status}:{occurredAtUtc.Ticks}"),
                cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                ex,
                "음식 배달 상태 변경 후 커뮤니티 활동 신호 발행에 실패했습니다. OrderNo={OrderNo}, Status={Status}",
                orderNo,
                status);
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
        var isRecommended = status == DriverWorkOfferStatus.Recommended;
        var dropoffAddress = isRecommended
            ? ToApproximateAddress(queue.하차_도로명주소)
            : JoinAddress(queue.하차_도로명주소, queue.하차_상세주소);
        var dropoffLatitude = isRecommended
            ? ToApproximateCoordinate(queue.하차_위도)
            : queue.하차_위도;
        var dropoffLongitude = isRecommended
            ? ToApproximateCoordinate(queue.하차_경도)
            : queue.하차_경도;

        return new DriverWorkOfferDto(
            queue.의뢰Id,
            기사앱식별자.FoodDeliveryDriverApp,
            기사도메인구분.음식배달,
            기사업무유형코드.음식배달,
            menu,
            $"{order.음식점명} 픽업 · {dropoffAddress} 전달",
            new DriverWorkStopDto(
                string.IsNullOrWhiteSpace(order.음식점명) ? "음식점" : order.음식점명,
                JoinAddress(queue.픽업_도로명주소, queue.픽업_상세주소),
                (double)(queue.픽업_위도 ?? 0m),
                (double)(queue.픽업_경도 ?? 0m),
                ToOffset(order.조리예상완료시각Utc)),
            new DriverWorkStopDto(
                isRecommended ? "고객 전달 권역" : "고객 주소",
                dropoffAddress,
                (double)(dropoffLatitude ?? 0m),
                (double)(dropoffLongitude ?? 0m),
                ToOffset(order.조리예상완료시각Utc?.AddMinutes(42))),
            queue.기사지급예정액 ?? CalculateDriverPayout(deliveryDistance),
            deliveryDistance.HasValue ? (double)deliveryDistance.Value : null,
            reason,
            status,
            ToOffset(queue.추천만료시각),
            [order.주문번호],
            운송실행프로필Factory.Create(queue),
            isRecommended
                ? null
                : new DriverWorkRecipientDto(
                    order.수령인명,
                    order.수령인연락처,
                    order.수령요청사항,
                    order.주문자본인수령여부),
            queue.기사기상할증액 ?? 0m,
            queue.기사기상할증적용여부,
            queue.기사제안요금정책판본,
            ToOffset(queue.픽업지기상기준시각Utc));
    }

    private static void ApplyFoodOrderState(
        음식주문 order,
        string nextState,
        string nextDispatchState,
        string reason,
        DateTime now)
    {
        var previous = 음식주문상태코드.Normalize(order.상태);
        음식배달업무상태전이Guard.허용확인(previous, nextState);
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
        if (foodState is 음식주문상태코드.전달완료 or 음식주문상태코드.수령확인)
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

    internal static string ToApproximateAddress(string address)
    {
        var parts = address.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var nonNumericParts = parts
            .Where(part => !part.Any(char.IsDigit))
            .Take(2)
            .ToArray();
        return nonNumericParts.Length switch
        {
            0 => "상세 위치는 수락 후 공개",
            1 => $"{nonNumericParts[0]} 인근",
            _ => $"{nonNumericParts[0]} {nonNumericParts[1]} 인근"
        };
    }

    internal static decimal? ToApproximateCoordinate(decimal? coordinate)
        => coordinate.HasValue
            ? Math.Round(coordinate.Value, 2, MidpointRounding.AwayFromZero)
            : null;

    private static FoodDeliveryDriverActionResponse Response(
        string offerId,
        string orderNo,
        string status,
        string message,
        FoodDeliveryLocationAuditResult? locationAudit = null,
        음식배달시도? attempt = null)
        => new()
        {
            OfferId = offerId,
            OrderIds = [orderNo],
            Status = status,
            Message = message,
            LocationAuditCode = locationAudit?.Code ?? string.Empty,
            LocationDistanceKm = locationAudit?.DistanceKm,
            DeliveryAttemptId = attempt?.시도StableId ?? string.Empty,
            AttemptRevision = attempt?.Revision,
            OccurredAtUtc = attempt?.UpdatedAtUtc
        };
}
