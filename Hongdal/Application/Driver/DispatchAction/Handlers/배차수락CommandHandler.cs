using MediatR;
using Microsoft.EntityFrameworkCore;
using FluentResults;
using Microsoft.Extensions.Logging;
using Hongdal.Application.CommandProcessing;
using Hongdal.Contracts.Common.Hr;
using Hongdal.Contracts.Common.Operations;
using 홍달.도메인.공통;

namespace Hongdal.Application.Driver.DispatchAction;

public sealed class 배차수락CommandHandler : IRequestHandler<배차수락Command, Result<배차수락결과>>
{
    private readonly HongdalContext _db;
    private readonly IPublisher _publisher;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly I참여자실행권한검사 _권한검사;
    private readonly IWorkRelationshipSnapshotCollector _relationshipSnapshotCollector;
    private readonly ILogger<배차수락CommandHandler> _logger;

    public 배차수락CommandHandler(
        HongdalContext db,
        IPublisher publisher,
        ICurrentUserAccessor currentUserAccessor,
        I참여자실행권한검사 권한검사,
        IWorkRelationshipSnapshotCollector relationshipSnapshotCollector,
        ILogger<배차수락CommandHandler> logger)
    {
        _db = db;
        _publisher = publisher;
        _currentUserAccessor = currentUserAccessor;
        _권한검사 = 권한검사;
        _relationshipSnapshotCollector = relationshipSnapshotCollector;
        _logger = logger;
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

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        var queue = await _db.운송원장.FirstOrDefaultAsync(x => x.의뢰Id == request.RequestId, cancellationToken);
        if (queue is null)
        {
            return Result.Fail<배차수락결과>("배차대기 데이터를 찾을 수 없습니다.");
        }

        var dispatchRequest = await _db.화주운송의뢰.FirstOrDefaultAsync(x => x.의뢰Id == request.RequestId, cancellationToken);
        if (dispatchRequest is null)
        {
            return Result.Fail<배차수락결과>("운송의뢰 데이터를 찾을 수 없습니다.");
        }

        if (dispatchRequest.결제상태 != 상태값.결제상태.결제완료)
        {
            return Result.Fail<배차수락결과>("결제완료 의뢰만 수락할 수 있습니다.");
        }

        var now = DateTime.UtcNow;
        var canAcceptRecommendation = 배차응답가능정책.추천수락가능(queue, request.기사Id, now);
        var canAcceptPublic = 배차응답가능정책.공개배차수락가능(queue);

        if (!canAcceptRecommendation && !canAcceptPublic)
        {
            return Result.Fail<배차수락결과>("수락 가능한 배차가 아닙니다.");
        }

        queue.상태 = 상태값.배차대기상태.확정;
        queue.배차큐단계 = 상태값.배차큐단계.확정;
        queue.배차노출상태 = 상태값.배차노출상태.확정;
        queue.확정기사Id = request.기사Id;
        queue.현재추천대상기사Id = null;
        queue.추천시작시각 = null;
        queue.추천만료시각 = null;
        dispatchRequest.배차상태 = 상태값.배차상태.배차확정;
        dispatchRequest.UpdatedAt = now;
        queue.UpdatedAt = now;

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
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

        _relationshipSnapshotCollector.Add(new WorkRelationshipSnapshotRecordRequest
        {
            WorkDomain = WorkRelationshipDomains.Dispatch,
            WorkProcess = WorkRelationshipProcesses.DriverAssignment,
            ActionCode = "DispatchAccepted",
            ActionLabel = "Dispatch accepted",
            RelatedEntityType = "TransportRequest",
            RelatedEntityId = request.RequestId,
            RelatedDisplayLabel = $"Transport request {request.RequestId}",
            CounterpartyUserId = dispatchRequest.화주Id,
            CounterpartyRoleCode = "Shipper",
            PrivacyLevel = "ActorVisibleAnonymized",
            Memo = "The driver accepted a dispatch request, creating a work relationship context."
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
                    now,
                    System.Diagnostics.Activity.Current?.TraceId.ToString() ?? string.Empty),
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "배차수락 사후처리 이벤트 발행 중 예외가 발생했습니다. RequestId={RequestId}", request.RequestId);
        }

        return Result.Ok(new 배차수락결과(request.RequestId, "수락되었습니다."));
    }
}
