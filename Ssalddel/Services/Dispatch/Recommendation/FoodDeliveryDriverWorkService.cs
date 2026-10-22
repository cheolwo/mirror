using Ssalddel.Contracts.Common.Metadata;

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
using Ssalddel.Services.Dispatch.Participation;
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

[SsalddelCodeMetadata(
    SsalddelCodeFeatureKeys.FoodDriverWork,
    SsalddelCodeLayer.Application,
    "기사 수락·거절·도착·중단·픽업·전달의 권한·Revision·현재 상태 전이를 조율한다.",
    ContractType = typeof(I음식배달기사업무Service),
    Effects = SsalddelCodeEffect.PersistentRead | SsalddelCodeEffect.PersistentWrite | SsalddelCodeEffect.NetworkCall | SsalddelCodeEffect.StateMutation | SsalddelCodeEffect.ThirdPartyApiCall | SsalddelCodeEffect.MayIncurExternalCost,
    FlowOrder = 120,
    StepKey = "application.driver-actions",
    DependsOnStepKeys = ["application.driver-action-policy", "contract.driver-followup-port", "contract.active-delivery"],
    ExecutionStage = SsalddelCodeExecutionStage.Confirm,
    ReadsFrom = SsalddelCodeDataScope.OperationalState,
    WritesTo = SsalddelCodeDataScope.OperationalState,
    Boundary = "수락·도착·픽업·전달은 SQL 변경과 후속 예약을 같은 transaction으로 확정한다. 거절의 기존 두 단계 저장은 별도다. 거절·중단의 다음 후보 진행은 새 제안 요금 산정을 통해 외부 경로 API를 호출할 수 있으며 전달·수령·정산·실지급을 합치지 않는다.")]
public sealed partial class 음식배달기사업무Service : I음식배달기사업무Service
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
    private readonly I음식기사후속OutboxService? _driverFollowups;
    private readonly I운송원장Mongo동기화Service _transportLedgerSync;
    private readonly I음식점주문실시간알림Service _restaurantNotification;
    private readonly I기사월정산Service _settlementService;
    private readonly IPublisher _publisher;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly ILogger<음식배달기사업무Service> _logger;
    private readonly I운영배차공통UseCase _수신의사;
    private readonly Ssalddel.Services.Fulfillment.I자동수행Coordinator? _fulfillment;
    private readonly bool _memberDeliveryEnabled;
    private readonly Ssalddel.Services.DeliveryExperience.음식배달경험Service? _experience;
    private readonly 살뜰.Services.Options.ISsalddelExecutionModePolicy? _experienceExecution;
    private readonly ISharedDriverWorkAdmissionService? _sharedAdmission;

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
        ILogger<음식배달기사업무Service> logger,
        I운영배차공통UseCase 수신의사,
        Ssalddel.Services.Fulfillment.I자동수행Coordinator? fulfillment = null,
        Microsoft.Extensions.Options.IOptions<Ssalddel.Services.Community.NeighborhoodMemberDeliveryOptions>? memberDelivery = null,
        Ssalddel.Services.DeliveryExperience.음식배달경험Service? experience = null,
        살뜰.Services.Options.ISsalddelExecutionModePolicy? experienceExecution = null,
        I음식기사후속OutboxService? driverFollowups = null,
        ISharedDriverWorkAdmissionService? sharedAdmission = null)
    {
        _db = db;
        _locationStore = locationStore;
        _routeService = routeService;
        _queueTransitionService = queueTransitionService;
        _deliveryScopeStore = deliveryScopeStore;
        _foodOrderStore = foodOrderStore;
        _foodLedgerOutbox = foodLedgerOutbox;
        _driverFollowups = driverFollowups ?? foodLedgerOutbox as I음식기사후속OutboxService;
        _transportLedgerSync = transportLedgerSync;
        _restaurantNotification = restaurantNotification;
        _settlementService = settlementService;
        _publisher = publisher;
        _currentUserAccessor = currentUserAccessor;
        _logger = logger;
        _수신의사 = 수신의사;
        _fulfillment = fulfillment;
        _memberDeliveryEnabled = memberDelivery?.Value.Enabled == true;
        _experience = experience;
        _experienceExecution = experienceExecution;
        _sharedAdmission = sharedAdmission;
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

        using var sharedLease = _sharedAdmission is null ? null
            : await _sharedAdmission.EnterAsync(driverId, cancellationToken);
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
                if (_sharedAdmission is not null)
                {
                    var admission = await _sharedAdmission.CheckAsync(driverId, normalizedIds, cancellationToken);
                    if (admission.IsFailed) return Result.Fail<List<FoodDeliveryAssignment>>(admission.Errors);
                }
                if (_memberDeliveryEnabled && await Ssalddel.Services.Community.NeighborhoodMemberWorkExclusion.LockAndHasMemberWorkAsync(_db, driverId, cancellationToken))
                    return Result.Fail<List<FoodDeliveryAssignment>>(new Error("일반 회원 배송을 마친 뒤 다른 배차를 수락할 수 있습니다.")
                        .WithMetadata("StatusCode", 409).WithMetadata("ErrorCode", "MemberWorkInProgress"));
                if (!살뜰.도메인.배차.운영배차수신상태Policy.신규배차수신허용(
                        await _수신의사.수신상태조회Async(driverId, cancellationToken)))
                {
                    return Result.Fail<List<FoodDeliveryAssignment>>(new Error(
                        "신규 배차 수신 또는 서버의 배차 조건을 확인해 주세요. 현재 수행 중인 배달은 유지됩니다.")
                        .WithMetadata("StatusCode", StatusCodes.Status409Conflict)
                        .WithMetadata("ErrorCode", "FoodDelivery.DispatchReceivingOff"));
                }
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
                        order.상태 is 음식주문상태코드.조리중 or 음식주문상태코드.픽업대기
                            && order.상태이력.Any(x => x.다음상태 == 음식주문상태코드.주문확인)
                            ? order.상태 : 음식주문상태코드.기사배정,
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
                    await 예약후속Async(queue, order.주문번호, driverId, "기사배정", changedAtUtc,
                        requireBundle ? "기사가 묶음 배달을 수락했습니다." : "기사가 배달을 수락했습니다.", cancellationToken);
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
            업무진행예약해제();
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
            if (!await _db.음식주문.AsNoTracking().AnyAsync(x => x.주문번호 == assignment.OrderNo
                    && x.모의처리처수행, cancellationToken))
                await ApplySettlementAsync(driverId, acceptedAtUtc, cancellationToken);
            await _deliveryScopeStore.Remove운송의뢰Async(assignment.Queue.의뢰Id, cancellationToken);
            await 처리후속Async(후속의도(assignment.Queue, assignment.OrderNo, driverId, "기사배정", assignment.Queue.UpdatedAt,
                requireBundle ? "기사가 묶음 배달을 수락했습니다." : "기사가 배달을 수락했습니다."), cancellationToken);
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
        if (queue is null || 운송의뢰배차원천유형.Is살뜰마트음식주문(queue.원본의뢰유형))
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
            var followup = 후속의도(queue, orderNo, driverId, "Rejected", order.UpdatedAt,
                "기사가 제안을 거절해 다른 기사 배차를 계속합니다.", activity: false);
            if (_driverFollowups is not null) await _driverFollowups.예약Async(followup, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
            await 처리후속Async(followup, cancellationToken);
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
                var followup = await 예약후속Async(queue, order.주문번호, driverId, "RestaurantArrived", now,
                    null, cancellationToken, syncLedgers: false);
                var saved = await SaveTransactionAsync(transaction, cancellationToken);
                if (saved.IsFailed) return Result.Fail<FoodDeliveryStateChange>(saved.Errors);
                return Result.Ok(new FoodDeliveryStateChange(
                    queue,
                    order.주문번호,
                    Response(offerId, order.주문번호, DriverWorkOfferStatus.MovingToPickup, "가게 도착을 기록했습니다.", locationAudit, attempt),
                    true, Followup: followup));
            }

            await transaction.RollbackAsync(cancellationToken);
            return Result.Ok(new FoodDeliveryStateChange(
                queue,
                order.주문번호,
                Response(offerId, order.주문번호, DriverWorkOfferStatus.MovingToPickup, "이미 가게 도착이 기록됐습니다.", locationAudit, attempt),
                false));
        });
        if (result.IsFailed) return Result.Fail<FoodDeliveryDriverActionResponse>(result.Errors);
        if (result.Value.Changed && result.Value.Followup is { } arrivalFollowup)
            await 처리후속Async(arrivalFollowup, cancellationToken);
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
            // 중단 후 큐의 확정기사는 해제된다. 원래 기사·제안·요청에 결속된 완료 시도만 재전송한다.
            var completedAttempt = await _db.음식배달시도.FirstOrDefaultAsync(x =>
                x.기사Id == driverId && x.제안Id == offerId && x.주문번호 == order.주문번호
                && x.마지막요청Id == request.클라이언트요청Id && x.중단시각Utc.HasValue, cancellationToken);
            if (completedAttempt is not null)
            {
                var memo = string.IsNullOrWhiteSpace(request.메모) ? null : request.메모.Trim();
                if (completedAttempt.중단사유Code != reasonCode || completedAttempt.중단메모 != memo)
                    return Result.Fail<FoodDeliveryStateChange>("같은 요청 ID에 다른 배달 중단 내용이 있습니다.");
                await transaction.RollbackAsync(cancellationToken);
                return Result.Ok(new FoodDeliveryStateChange(queue, order.주문번호,
                    Response(offerId, order.주문번호, "Interrupted", "이미 배달 중단이 처리됐습니다.", attempt: completedAttempt), false));
            }
            if (!string.Equals(queue.확정기사Id, driverId, StringComparison.Ordinal))
                return Result.Fail<FoodDeliveryStateChange>("확정된 배달 기사만 중단을 요청할 수 있습니다.");

            var now = DateTime.UtcNow;
            var attempt = await 현재시도조회또는추정Async(queue, order, driverId, cancellationToken);
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
                if (픽업준비됨(order))
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
                var cookingMinutes = order.적용조리분 is > 0
                    ? order.적용조리분.Value
                    : order.플랫폼참고조리분 is > 0 ? order.플랫폼참고조리분.Value : 20;
                order.적용조리분 = cookingMinutes;
                // 재조리를 요청한 사실만 기록한다. 새 유효 기사 배정 뒤 음식점이 실제로 시작한다.
                order.조리예상완료시각Utc = null;
            }

            var nextState = postPickup
                ? 음식주문상태코드.주문확인
                : 픽업준비됨(order)
                    ? 음식주문상태코드.픽업대기
                    : 음식주문현재조리Policy.계산(order).CookingStartedAtUtc.HasValue
                        ? 음식주문상태코드.조리중
                        : 음식주문상태코드.주문확인;
            ApplyFoodOrderState(order, nextState, 음식주문배차상태코드.배차대기,
                postPickup ? 음식주문현재조리Policy.재조리대기이력사유 : $"픽업 전 배달 중단 · {reasonCode}", now);
            배차재추천상태Policy.적용(queue, driverId, now);
            _db.운영배차활동사건.Add(운영배차활동사건Factory.중단(driverId, offerId, order.주문번호, queue.추천라운드, now, attempt.시도StableId, reasonCode, responsibility));
            _db.운영배차활동사건.Add(운영배차활동사건Factory.균형반환(driverId, offerId, order.주문번호, queue.추천라운드, now, attempt.시도StableId, responsibility));
            var followup = await 예약후속Async(queue, order.주문번호, driverId,
                postPickup ? "DeliveryInterruptedRecook" : "DeliveryInterrupted", now,
                postPickup ? "픽업 후 배달 중단으로 재조리를 요청했습니다. 새 기사 배정 뒤 음식점이 조리를 시작합니다. 추가 비용은 운영 검토가 필요합니다." : "배달 중단 후 재배차를 진행합니다.", cancellationToken);
            var saved = await SaveTransactionAsync(transaction, cancellationToken);
            return saved.IsFailed
                ? Result.Fail<FoodDeliveryStateChange>(saved.Errors)
                : Result.Ok(new FoodDeliveryStateChange(queue, order.주문번호,
                    Response(offerId, order.주문번호, "Interrupted", postPickup ? "배달을 중단하고 재조리·재배차를 요청했습니다." : "배달을 중단하고 재배차를 시작했습니다.", attempt: attempt), true, postPickup, followup));
        });

        if (result.IsFailed) return Result.Fail<FoodDeliveryDriverActionResponse>(result.Errors);
        if (result.Value.Changed)
        {
            try { await _queueTransitionService.추천대기처리Async(offerId, cancellationToken); }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested) { _logger.LogWarning(ex, "배달 중단 후 즉시 재배차에 실패했습니다. 영속 대기열에서 재시도합니다. OfferId={OfferId}", offerId); }
            if (result.Value.Followup is { } interruptedFollowup)
                await 처리후속Async(interruptedFollowup, cancellationToken);
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
                && currentOrderState is not (음식주문상태코드.기사배정 or 음식주문상태코드.픽업대기))
            {
                return Result.Fail<FoodDeliveryStateChange>("기사 배정이 완료된 주문만 픽업 완료할 수 있습니다.");
            }

            if (nextOrderState == 음식주문상태코드.픽업완료
                && !픽업준비됨(order))
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
                await Ssalddel.Application.Food.음식주문기사정산Recorder.완료기록Async(
                    _db, order, queue, attempt, cancellationToken);
            }

            if (_experience is not null)
                await _experience.완료근거추가Async(order, attempt,
                    nextOrderState == 음식주문상태코드.픽업완료 ? "PickupCompleted" : "DeliveryCompleted", cancellationToken);
            var followup = await 예약후속Async(queue, order.주문번호, driverId, nextOrderState,
                changedAtUtc, reason, cancellationToken);
            var saveResult = await SaveTransactionAsync(transaction, cancellationToken);
            return saveResult.IsFailed
                ? Result.Fail<FoodDeliveryStateChange>(saveResult.Errors)
                : Result.Ok(new FoodDeliveryStateChange(
                    queue,
                    order.주문번호,
                    Response(offerId, order.주문번호, responseState, $"{reason} 처리했습니다.", locationAudit, attempt),
                    true, Followup: followup));
        });
        if (transactionResult.IsFailed)
        {
            return Result.Fail<FoodDeliveryDriverActionResponse>(transactionResult.Errors);
        }

        var stateChange = transactionResult.Value;
        if (stateChange.Changed)
        {
            if (stateChange.Followup is { } followup)
                await 처리후속Async(followup, cancellationToken);
        }

        return Result.Ok(stateChange.Response);
    }

    private sealed record FoodDeliveryAssignment(운송원장 Queue, string OrderNo);

    private sealed record FoodDeliveryStateChange(
        운송원장 Queue,
        string OrderNo,
        FoodDeliveryDriverActionResponse Response,
        bool Changed,
        bool RequiresRecook = false,
        음식기사후속의도? Followup = null);

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
