using MediatR;
using Microsoft.EntityFrameworkCore;
using FluentResults;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Data;
using System.Text.Json;
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
using 살뜰.Services.Dispatch.Common;
using 살뜰.Services.Dispatch.Queue;
using Ssalddel.Application.Driver.Transport;
using Ssalddel.Services.Dispatch.Compliance;
using 살뜰.Services.Dispatch.Coordination;

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
    private readonly I운영배차공통UseCase? _운영배차;
    private readonly I화물후속픽업기준Service? _후속픽업;
    private readonly IDriverLocationStore? _기사위치;
    private readonly 배차큐정책Options _queueOptions;
    private readonly ICargoAcceptanceRecoveryService _acceptanceRecovery;
    private readonly I신규화물업무자격Guard? _신규업무자격;
    private readonly I화물배차상태SnapshotService? _상태근거;
    private readonly I국내화물추천재검증Service? _추천재검증;

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
        I소형배송기사제안요금Service? smallGoodsPricing = null,
        I운영배차공통UseCase? 운영배차 = null,
        I화물후속픽업기준Service? 후속픽업 = null,
        IDriverLocationStore? 기사위치 = null,
        Microsoft.Extensions.Options.IOptions<배차큐정책Options>? queueOptions = null,
        ICargoAcceptanceRecoveryService? acceptanceRecovery = null,
        I신규화물업무자격Guard? 신규업무자격 = null,
        I화물배차상태SnapshotService? 상태근거 = null,
        I국내화물추천재검증Service? 추천재검증 = null)
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
        _운영배차 = 운영배차;
        _후속픽업 = 후속픽업;
        _기사위치 = 기사위치;
        _queueOptions = queueOptions?.Value ?? new 배차큐정책Options();
        _신규업무자격 = 신규업무자격;
        _상태근거 = 상태근거;
        _추천재검증 = 추천재검증;
        _acceptanceRecovery = acceptanceRecovery ?? new CargoAcceptanceRecoveryService(db, 연속배차UseCase,
            Microsoft.Extensions.Options.Options.Create(new CargoAcceptanceRecoveryOptions()), TimeProvider.System,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<CargoAcceptanceRecoveryService>.Instance,
            recommendationRefresh: 추천재검증);
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

        if (_추천재검증 is not null) await _추천재검증.갱신Async(request.기사Id, cancellationToken);
        using var sharedLease = _sharedAdmission is null ? null
            : await _sharedAdmission.EnterAsync(request.기사Id, cancellationToken);
        var driverGate = 기사별수락Gate.GetOrAdd(request.기사Id, static _ => new SemaphoreSlim(1, 1));
        await driverGate.WaitAsync(cancellationToken);
        var driverGateHeld = true;
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
                if (persisted.Result.IsSuccess)
                {
                    driverGate.Release();
                    driverGateHeld = false;
                    sharedLease?.Dispose(); // 수락 transaction 뒤의 후속 갱신이 같은 기사 잠금을 다시 획득한다.
                    await ResumeAcceptanceAsync(request, cancellationToken);
                }
                return persisted.Result;
            }

            driverGate.Release();
            driverGateHeld = false;
            sharedLease?.Dispose();
            await PublishAfterCommitAsync(request, persisted, cancellationToken);
            return persisted.Result;
        }
        finally
        {
            if (driverGateHeld) driverGate.Release();
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

        if (queue.배차업무유형 != 상태값.배차업무유형.용달운송)
            return new(Conflict(화물배차수락오류코드.차량적합실패, "화물 기사 수락 경로에서는 용달 운송 의뢰만 처리할 수 있습니다."));
        if (기존수락본인업무인가(queue, request.기사Id))
            return new(Result.Ok(new 배차수락결과(request.RequestId, "이미 수락된 운송입니다.")));

        var qualification = await 신규화물업무자격검사.평가Async(_신규업무자격, request.기사Id, cancellationToken);
        if (!qualification.CanReceiveNewWork)
            return new(Conflict(qualification.ReasonCode, qualification.Message));

        var dispatchRequest = await _db.화주운송의뢰.FirstOrDefaultAsync(x => x.의뢰Id == request.RequestId, cancellationToken);
        if (dispatchRequest is null)
        {
            return new(Result.Fail<배차수락결과>("운송의뢰 데이터를 찾을 수 없습니다."));
        }

        if (!신규화물업무자격검사.화물정보완료(_신규업무자격, dispatchRequest))
            return new(Conflict("FreightCargo.RequiredInformationMissing", "화물 종류·포장 포함 중량·가로·세로·높이와 출발·도착지를 먼저 확인해 주세요."));

        var settlementReadiness = 화주운송배차진입정책.판정(dispatchRequest);
        if (!settlementReadiness.가능)
        {
            return new(Conflict(화주운송배차진입정책.진입불가오류코드, settlementReadiness.사유));
        }

        if (!await 운영배차신규수신Guard.허용Async(_운영배차, request.기사Id, cancellationToken))
            return new(Conflict("FreightDispatchReceivingOff", "신규 배차 수신 또는 서버의 배차 조건을 확인해 주세요. 현재 운송은 유지됩니다."));
        var beforeActive = await 현재업무판본Async(request.기사Id, cancellationToken);
        화물후속픽업기준결과? activeBasis = null;
        if (_queueOptions.화물수행종료기준사용 && beforeActive.Length > 0)
        {
            if (_후속픽업 is null || _기사위치 is null || !_기사위치.TryGetLatest(request.기사Id, out var location)
                || !기사위치신선도정책.유효한가(location.ReceivedAtUtc, DateTime.UtcNow, _queueOptions.기사위치유효시간분))
                return new(Conflict(화물배차수락오류코드.일정근거부족, "진행 중 업무의 현재 위치와 전체 완료 근거를 다시 확인해 주세요."));
            activeBasis = await _후속픽업.조회Async(request.기사Id, new(location.Latitude, location.Longitude), DateTime.UtcNow, cancellationToken);
            if (!activeBasis.확인됨 || !activeBasis.활성업무있음)
                return new(Conflict(화물배차수락오류코드.일정근거부족, "기존 전체 업무의 완료 예상시각과 마지막 목적지를 확인할 수 없습니다."));
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

        국내화물배차판정근거? decisionEvidence = null;
        if (!string.IsNullOrWhiteSpace(queue.추천판정근거Json))
        {
            국내화물추천저장근거? savedEvidence;
            try { savedEvidence = JsonSerializer.Deserialize<국내화물추천저장근거>(queue.추천판정근거Json); }
            catch (JsonException) { return new(Conflict(화물배차수락오류코드.추천판본불일치, "추천 근거를 읽을 수 없습니다. 최신 추천을 다시 확인해 주세요.")); }
            decisionEvidence = savedEvidence?.판정근거;
            if (savedEvidence?.수행계획 is not { 확인됨: true, 가능여부: true } savedPlan
                || string.IsNullOrWhiteSpace(savedEvidence.조율입력지문) || savedPlan.입력지문 != savedEvidence.조율입력지문
                || savedPlan.기사Id != request.기사Id || !savedPlan.의뢰Ids.Contains(request.RequestId, StringComparer.Ordinal)
                || decisionEvidence is null || _상태근거 is null || request.ExpectedRecommendationRound is null
                || !decisionEvidence.기사근거.ContainsKey(request.기사Id)
                || !decisionEvidence.의뢰근거.ContainsKey(request.RequestId)
                || !await _상태근거.현재근거확인Async(decisionEvidence, cancellationToken))
                return new(Conflict(화물배차수락오류코드.추천판본불일치, "추천 이후 화물·기사·보수 근거가 변경되었습니다. 최신 추천을 확인해 주세요."));
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
        var currentActive = await 현재업무판본Async(request.기사Id, cancellationToken);
        var finalQualification = await 신규화물업무자격검사.평가Async(_신규업무자격, request.기사Id, cancellationToken);
        if (!finalQualification.CanReceiveNewWork)
            return new(Conflict(finalQualification.ReasonCode, finalQualification.Message));
        if (qualification.Revision != finalQualification.Revision)
            return new(Conflict("Qualification.RevisionChanged", "자격 검토 상태가 변경되었습니다. 최신 상태를 확인한 뒤 다시 수락해 주세요."));
        if (!Enumerable.SequenceEqual(beforeActive, currentActive)
            || (decisionEvidence is not null && !await _상태근거!.현재근거확인Async(decisionEvidence, cancellationToken))
            || (activeBasis is not null && !await _후속픽업!.현재근거확인Async(request.기사Id, activeBasis, cancellationToken))
            || !await 운영배차신규수신Guard.허용Async(_운영배차, request.기사Id, cancellationToken)
            || !(배차응답가능정책.추천수락가능(queue, request.기사Id, DateTime.UtcNow) || 배차응답가능정책.공개배차수락가능(queue)))
            return new(Conflict("FreightDispatchConditionsChanged", "수락 검증 중 수신 상태나 기존 업무가 변경되었거나 제안이 만료되었습니다. 최신 제안을 확인해 주세요."));
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
        // 배정과 같은 transaction에 후속 의도를 남겨 commit 직후 프로세스가 끝나도 재개합니다.
        if (_db.Database.IsRelational()) _db.운송이벤트.Add(CargoAcceptanceRecoveryMarker.Create(queue, now));
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

        if (_db.Database.IsRelational())
            await ResumeAcceptanceAsync(request, cancellationToken);
        else try
        {
            await _연속배차UseCase.시간약속잠금Async(request.기사Id, dispatchRequest, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "배차 수락 뒤 화물 시간 약속 잠금에 실패했습니다. RequestId={RequestId}", request.RequestId);
        }

        if (!_db.Database.IsRelational() && _추천재검증 is not null)
        {
            try { await _추천재검증.갱신Async(request.기사Id, cancellationToken); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            { _logger.LogWarning(ex, "배차 수락 뒤 본인 추천 재검증이 대기 중입니다. RequestId={RequestId}", request.RequestId); }
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

    private async Task ResumeAcceptanceAsync(배차수락Command request, CancellationToken cancellationToken)
    {
        try { await _acceptanceRecovery.ProcessRequestAsync(request.RequestId, request.기사Id, cancellationToken); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception)
        {
            // 수락 성공을 후속 실패로 바꾸지 않습니다. 같은 수락의 영속 의도는 재조회/worker가 재개합니다.
            _logger.LogWarning("배차 수락 후속이 대기 중입니다. RequestId={RequestId}", request.RequestId);
        }
    }

    private sealed record AcceptPersistenceResult(
        Result<배차수락결과> Result,
        운송원장? Queue = null,
        화주운송의뢰? DispatchRequest = null,
        DateTime AcceptedAtUtc = default);

    private sealed record 현재업무판본(string Id, string State, string? Driver, string Assigned,
        DateTime? RowVersion, DateTime UpdatedAt, DateTime? Pickup, DateTime? Dropoff);

    private static bool 기존수락본인업무인가(운송원장 queue, string driverId)
        => queue.배차큐단계 == 상태값.배차큐단계.확정
           && (queue.확정기사Id == driverId || queue.기사_운송자 == driverId)
           && (string.IsNullOrWhiteSpace(queue.확정기사Id) || queue.확정기사Id == driverId)
           && (string.IsNullOrWhiteSpace(queue.기사_운송자) || queue.기사_운송자 == driverId)
           && queue.상태 is 상태값.배차대기상태.확정 or 기사운송상태코드.배차확정
               or 기사운송상태코드.이동중 or 기사운송상태코드.상차지도착 or 기사운송상태코드.상차완료
               or 기사운송상태코드.운송중 or 기사운송상태코드.하차지도착 or 기사운송상태코드.인수완료;

    private Task<현재업무판본[]> 현재업무판본Async(string driverId, CancellationToken ct)
        => _db.운송원장.AsNoTracking().Where(x => (x.확정기사Id == driverId || x.기사_운송자 == driverId)
            && x.배차큐단계 == 상태값.배차큐단계.확정 && x.상태 != 상태값.배차상태.인수완료)
            .OrderBy(x => x.의뢰Id).Select(x => new 현재업무판본(x.의뢰Id, x.상태, x.확정기사Id, x.기사_운송자,
                x.RowVersion, x.UpdatedAt, x.출발_픽업, x.도착)).ToArrayAsync(ct);

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
