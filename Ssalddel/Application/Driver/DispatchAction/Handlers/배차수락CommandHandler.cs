using MediatR;
using Microsoft.EntityFrameworkCore;
using FluentResults;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Data;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Application.Shipper.Request;
using Ssalddel.Contracts.Common.Hr;
using Ssalddel.Contracts.Common.Operations;
using Ssalddel.Services.Dispatch.Participation;
using 살뜰.도메인.공통;
using 살뜰.도메인.운송;
using 살뜰.도메인.화주;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.Dispatch.Continuity;

namespace Ssalddel.Application.Driver.DispatchAction;

public sealed class 배차수락CommandHandler : IRequestHandler<배차수락Command, Result<배차수락결과>>
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> 기사별수락Gate = new(StringComparer.Ordinal);

    private readonly SsalddelContext _db;
    private readonly IPublisher _publisher;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly I참여자실행권한검사 _권한검사;
    private readonly IWorkRelationshipSnapshotCollector _relationshipSnapshotCollector;
    private readonly I화물배차수락적격성Service _수락적격성Service;
    private readonly I화물연속배차UseCase _연속배차UseCase;
    private readonly ILogger<배차수락CommandHandler> _logger;
    private readonly bool _memberDeliveryEnabled;
    private readonly ISharedDriverWorkAdmissionService? _sharedAdmission;
    private readonly I소형배송기사제안요금Service? _smallGoodsPricing;

    public 배차수락CommandHandler(
        SsalddelContext db,
        IPublisher publisher,
        ICurrentUserAccessor currentUserAccessor,
        I참여자실행권한검사 권한검사,
        IWorkRelationshipSnapshotCollector relationshipSnapshotCollector,
        I화물배차수락적격성Service 수락적격성Service,
        I화물연속배차UseCase 연속배차UseCase,
        ILogger<배차수락CommandHandler> logger,
        Microsoft.Extensions.Options.IOptions<Ssalddel.Services.Community.NeighborhoodMemberDeliveryOptions>? memberDelivery = null,
        ISharedDriverWorkAdmissionService? sharedAdmission = null,
        I소형배송기사제안요금Service? smallGoodsPricing = null)
    {
        _db = db;
        _publisher = publisher;
        _currentUserAccessor = currentUserAccessor;
        _권한검사 = 권한검사;
        _relationshipSnapshotCollector = relationshipSnapshotCollector;
        _수락적격성Service = 수락적격성Service;
        _연속배차UseCase = 연속배차UseCase;
        _logger = logger;
        _memberDeliveryEnabled = memberDelivery?.Value.Enabled == true;
        _sharedAdmission = sharedAdmission;
        _smallGoodsPricing = smallGoodsPricing;
    }

    public async Task<Result<배차수락결과>> Handle(배차수락Command request, CancellationToken cancellationToken)
    {
        if (!_권한검사.Try검증(_currentUserAccessor.UserId, _currentUserAccessor.Role, request.참여자Id, request.실행역할, out var 권한오류))
        {
            return Result.Fail<배차수락결과>(권한오류);
        }

        var executionBoundary = CollectiveActionDispatchBoundaryPolicy.Evaluate(
            DispatchConfirmationBoundaryRequest.ForDriverSelfAcceptance(
                _currentUserAccessor.UserId,
                request.기사Id));
        if (!executionBoundary.CanConfirmDispatch)
        {
            return Result.Fail<배차수락결과>(
                "플랫폼의 후보 정보만으로 배차를 확정할 수 없습니다. 참여 기사 본인의 수락이 필요합니다.");
        }

        using var sharedLease = _sharedAdmission is null ? null
            : await _sharedAdmission.EnterAsync(request.기사Id, cancellationToken);
        var driverGate = 기사별수락Gate.GetOrAdd(request.기사Id, static _ => new SemaphoreSlim(1, 1));
        await driverGate.WaitAsync(cancellationToken);
        try
        {
            AcceptPersistenceResult persisted;
            var attempt = 0;
            try
            {
                persisted = await _db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
                {
                    // 실패한 트랜잭션의 추적 값으로 다음 시도를 확정하지 않는다.
                    if (attempt++ > 0)
                    {
                        _db.ChangeTracker.Clear();
                    }

                    return await PersistAcceptanceAsync(request, cancellationToken);
                });
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(ex, "배차 수락 중 동시성 충돌이 발생했습니다. RequestId={RequestId} DriverId={DriverId}", request.RequestId, request.기사Id);
                return Result.Fail<배차수락결과>("다른 기사에 의해 이미 수락되었습니다.");
            }
            catch (DbUpdateException ex)
            {
                _logger.LogWarning(ex, "배차 수락 저장 중 DB 예외가 발생했습니다. RequestId={RequestId} DriverId={DriverId}", request.RequestId, request.기사Id);
                return Result.Fail<배차수락결과>("수락 처리 중 오류가 발생했습니다. 잠시 후 다시 시도해 주세요.");
            }

            if (persisted.Queue is null || persisted.DispatchRequest is null)
            {
                return persisted.Result;
            }

            await PublishAfterCommitAsync(request, persisted, cancellationToken);
            return persisted.Result;
        }
        finally
        {
            driverGate.Release();
        }
    }

    private async Task<AcceptPersistenceResult> PersistAcceptanceAsync(
        배차수락Command request,
        CancellationToken cancellationToken)
    {
        await using var tx = _db.Database.IsRelational()
            ? await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            : null;

        if (_sharedAdmission is not null)
            await _sharedAdmission.LockAsync(request.기사Id, cancellationToken);

        if (_memberDeliveryEnabled && await Ssalddel.Services.Community.NeighborhoodMemberWorkExclusion.LockAndHasMemberWorkAsync(_db, request.기사Id, cancellationToken))
            return new(Conflict("MemberWorkInProgress", "일반 회원 배송을 마친 뒤 다른 배차를 수락할 수 있습니다."));

        var queue = await _db.운송원장.FirstOrDefaultAsync(x => x.의뢰Id == request.RequestId, cancellationToken);
        if (queue is null)
        {
            return new(Result.Fail<배차수락결과>("배차대기 데이터를 찾을 수 없습니다."));
        }
        if (queue.일반회원배송전용)
            return new(Conflict("MemberDeliveryDedicatedRouteRequired", "일반 회원 배송은 해당 회원에게 전달된 배송 제안에서 수락해 주세요."));

        var dispatchRequest = await _db.화주운송의뢰.FirstOrDefaultAsync(x => x.의뢰Id == request.RequestId, cancellationToken);
        if (dispatchRequest is null)
        {
            return new(Result.Fail<배차수락결과>("운송의뢰 데이터를 찾을 수 없습니다."));
        }

        var settlementReadiness = 화주운송배차진입정책.판정(dispatchRequest);
        if (!settlementReadiness.가능)
        {
            return new(Conflict(화주운송배차진입정책.진입불가오류코드, settlementReadiness.사유));
        }

        if (queue.배차업무유형 != 상태값.배차업무유형.용달운송)
        {
            return new(Conflict(
                화물배차수락오류코드.차량적합실패,
                "화물 기사 수락 경로에서는 용달 운송 의뢰만 처리할 수 있습니다."));
        }

        if (queue.상태 == 상태값.배차대기상태.확정
            && string.Equals(queue.확정기사Id ?? queue.기사_운송자, request.기사Id, StringComparison.Ordinal))
        {
            return new(Result.Ok(new 배차수락결과(request.RequestId, "이미 수락된 운송입니다.")));
        }

        if (_sharedAdmission is not null)
        {
            var admission = await _sharedAdmission.CheckAsync(request.기사Id, [request.RequestId], cancellationToken);
            if (admission.IsFailed) return new(Result.Fail<배차수락결과>(admission.Errors));
        }

        if (await Ssalddel.Application.Pricing.AgreedCargoAcceptancePolicy.CheckAsync(_db, dispatchRequest, request.기사Id, cancellationToken) is { } rateError)
            return new(Conflict(rateError, "선택한 사전 합의 요금표의 기사·업무·원장 근거를 다시 확인해 주세요."));

        if (queue.생활배송배차방식 is not null && (!queue.생활배송수락준비완료 || request.ExpectedRecommendationRound is null))
            return new(Conflict("DispatchChoiceNotReady", "현재 배송 연결과 배차 판본을 확인해 주세요."));
        if (request.ExpectedRecommendationRound.HasValue
            && request.ExpectedRecommendationRound.Value != queue.추천라운드)
        {
            return new(Conflict(
                화물배차수락오류코드.추천판본불일치,
                "추천 판본이 변경되었습니다. 최신 추천을 다시 확인해 주세요."));
        }

        var reservationValidation = await _연속배차UseCase.수락예약검증Async(
            request.기사Id,
            request.RequestId,
            request.ReservationId,
            request.ExpectedReservationRevision,
            cancellationToken);
        if (!reservationValidation.유효)
        {
            return new(Conflict(
                reservationValidation.오류Code ?? "FreightReservationInvalid",
                reservationValidation.오류메시지 ?? "다음 콜 예약을 수락할 수 없습니다."));
        }

        var now = DateTime.UtcNow;
        var canAcceptRecommendation = 배차응답가능정책.추천수락가능(queue, request.기사Id, now);
        var canAcceptPublic = 배차응답가능정책.공개배차수락가능(queue);

        if (!canAcceptRecommendation && !canAcceptPublic)
        {
            return new(Result.Fail<배차수락결과>("수락 가능한 배차가 아닙니다."));
        }

        var eligibility = await _수락적격성Service.평가Async(
            request.기사Id,
            dispatchRequest,
            request.AcknowledgedWarningCodes,
            cancellationToken);
        if (!eligibility.수락가능)
        {
            return new(Conflict(
                eligibility.오류코드 ?? 화물배차수락오류코드.일정실패,
                eligibility.오류메시지 ?? "현재 운송 조건으로 수락할 수 없습니다.",
                eligibility.경고코드,
                eligibility.경고메시지));
        }

        if (_smallGoodsPricing is not null)
        {
            var payout = await _smallGoodsPricing.수락검증Async(queue, request.기사Id, cancellationToken);
            if (payout.IsFailed) return new(Result.Fail<배차수락결과>(payout.Errors));
        }

        // 고객 청구 운임은 기사 보수가 아니다. 기존 약정/직접 합의 또는 소형 배송의
        // 검증된 제안으로 지급액이 정해진 뒤에만 새 유상 업무를 확정한다.
        var driverFare = await _db.운임구성.SingleOrDefaultAsync(
            x => x.Id == dispatchRequest.운임구성Id && x.의뢰Id == request.RequestId, cancellationToken);
        if (driverFare?.기사지급예정운임 is not > 0
            || decimal.Truncate(driverFare.기사지급예정운임.Value) != driverFare.기사지급예정운임.Value)
            return new(Conflict("FreightDriverPayoutAgreementRequired", "기사 보수가 아직 확정되지 않았습니다. 화주와 운임을 합의한 뒤 최신 제안에서 수락해 주세요."));
        if (queue.기사지급예정액.HasValue && queue.기사지급예정액 != driverFare.기사지급예정운임)
            return new(Conflict("FreightDriverPayoutAgreementChanged", "제시한 기사 보수와 현재 합의가 다릅니다. 최신 제안을 확인해 주세요."));
        queue.기사지급예정액 ??= driverFare.기사지급예정운임;

        if (queue.생활배송배차방식 is not null) queue.생활배송선택판본++;
        queue.상태 = 상태값.배차대기상태.확정;
        queue.배차큐단계 = 상태값.배차큐단계.확정;
        queue.배차노출상태 = 상태값.배차노출상태.확정;
        queue.운송번호 = string.IsNullOrWhiteSpace(queue.운송번호) ? request.RequestId : queue.운송번호;
        queue.의뢰Id = string.IsNullOrWhiteSpace(queue.의뢰Id) ? request.RequestId : queue.의뢰Id;
        queue.화주Id = string.IsNullOrWhiteSpace(queue.화주Id) ? dispatchRequest.화주Id : queue.화주Id;
        queue.기사_운송자 = request.기사Id;
        queue.확정기사Id = request.기사Id;
        queue.픽업_도로명주소 = dispatchRequest.픽업_도로명주소;
        queue.픽업_상세주소 = dispatchRequest.픽업_상세주소;
        queue.픽업_위도 = dispatchRequest.픽업_위도;
        queue.픽업_경도 = dispatchRequest.픽업_경도;
        queue.하차_도로명주소 = dispatchRequest.하차_도로명주소;
        queue.하차_상세주소 = dispatchRequest.하차_상세주소;
        queue.하차_위도 = dispatchRequest.하차_위도;
        queue.하차_경도 = dispatchRequest.하차_경도;
        queue.출발지 = dispatchRequest.픽업_도로명주소;
        queue.도착지 = dispatchRequest.하차_도로명주소;
        queue.운임 = dispatchRequest.최종운임;
        queue.첨부_json = string.IsNullOrWhiteSpace(queue.첨부_json) ? "[]" : queue.첨부_json;
        queue.현재추천대상기사Id = null;
        queue.추천시작시각 = null;
        queue.추천만료시각 = null;
        dispatchRequest.배차상태 = 상태값.배차상태.배차확정;
        dispatchRequest.UpdatedAt = now;
        queue.UpdatedAt = now;

        // 생활 배송 지도는 후처리 알림/Mongo 로그 대신 같은 SQL 저장의 현재 배정 근거를 사용합니다.
        var assignmentEvent = Ssalddel.Services.Community.NeighborhoodDeliveryLocationPolicy.CreateAssignmentEvent(dispatchRequest, queue, now);
        if (assignmentEvent is not null) _db.운송이벤트.Add(assignmentEvent);
        await _db.SaveChangesAsync(cancellationToken);
        await _연속배차UseCase.수락완료Async(request.기사Id, request.RequestId, cancellationToken);
        if (tx is not null)
        {
            await tx.CommitAsync(cancellationToken);
        }

        return new(Result.Ok(new 배차수락결과(request.RequestId, "수락되었습니다.")), queue, dispatchRequest, now);
    }

    private async Task PublishAfterCommitAsync(
        배차수락Command request,
        AcceptPersistenceResult persisted,
        CancellationToken cancellationToken)
    {
        var queue = persisted.Queue!;
        var dispatchRequest = persisted.DispatchRequest!;

        try
        {
            await _연속배차UseCase.시간약속잠금Async(request.기사Id, dispatchRequest, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "배차 수락 뒤 화물 시간 약속 잠금에 실패했습니다. RequestId={RequestId}", request.RequestId);
        }

        _relationshipSnapshotCollector.Add(new WorkRelationshipSnapshotRecordRequest
        {
            WorkDomain = WorkRelationshipDomains.Dispatch,
            WorkProcess = WorkRelationshipProcesses.DriverAssignment,
            ActionCode = "DispatchAccepted",
            ActionLabel = "배차 수락",
            RelatedEntityType = "TransportRequest",
            RelatedEntityId = request.RequestId,
            RelatedDisplayLabel = $"운송 의뢰 {request.RequestId}",
            CounterpartyUserId = dispatchRequest.화주Id,
            CounterpartyRoleCode = "Shipper",
            PrivacyLevel = WorkRelationshipPrivacyCodes.ConnectionRequestEligible,
            Memo = "기사와 화주가 실제 배차 수락 업무에서 만난 친구 후보 기록입니다."
        });

        try
        {
            await _publisher.Publish(
                new 배차수락됨Event(
                    request.기사Id,
                    dispatchRequest.화주Id,
                    request.RequestId,
                    queue.상태,
                    dispatchRequest.배차상태,
                    dispatchRequest.결제상태,
                    persisted.AcceptedAtUtc,
                    System.Diagnostics.Activity.Current?.TraceId.ToString() ?? string.Empty),
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "배차수락 사후처리 이벤트 발행 중 예외가 발생했습니다. RequestId={RequestId}", request.RequestId);
        }

    }

    private sealed record AcceptPersistenceResult(
        Result<배차수락결과> Result,
        운송원장? Queue = null,
        화주운송의뢰? DispatchRequest = null,
        DateTime AcceptedAtUtc = default);

    private static Result<배차수락결과> Conflict(
        string errorCode,
        string message,
        IReadOnlyList<string>? warningCodes = null,
        IReadOnlyList<string>? warningMessages = null)
        => Result.Fail<배차수락결과>(
            new Error(message)
                .WithMetadata("StatusCode", 409)
                .WithMetadata("ErrorCode", errorCode)
                .WithMetadata("WarningCodes", warningCodes ?? [])
                .WithMetadata("WarningMessages", warningMessages ?? []));
}
