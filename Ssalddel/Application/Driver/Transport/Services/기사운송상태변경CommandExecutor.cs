using System.Diagnostics;
using System.Text.Json;
using FluentResults;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Contracts.Driver.Transport;
using Ssalddel.Contracts.Common.Versioning;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using 살뜰.Services.Notifications;
using 살뜰.도메인.설정;
using 살뜰.Data;
using 살뜰.도메인.운송;
using 살뜰.도메인.사용자;

namespace Ssalddel.Application.Driver.Transport;

public interface I기사운송상태변경CommandExecutor
{
    Task<Result<기사운송상태변경응답>> 실행Async(
        기사운송상태변경요청 request,
        CancellationToken cancellationToken);
}

public sealed record 기사운송상태변경요청(
    string 기사Id,
    long 운송Id,
    string 참여자Id,
    살뜰역할유형 실행역할,
    string 목표상태,
    string 이벤트명,
    Func<기사운송상태변경Context, INotification> 이벤트생성,
    Action<운송원장, DateTime>? 상태변경전처리 = null,
    Action<INotification>? Commit이후발행수집 = null);

public sealed record 기사운송상태변경Context(
    운송원장 운송,
    string 이전상태,
    DateTime 발생시각Utc,
    string TraceId);

public sealed class 기사운송상태변경CommandExecutor : I기사운송상태변경CommandExecutor
{
    private readonly SsalddelContext _db;
    private readonly I기사운송상태전이Service _상태전이Service;
    private readonly IPublisher _publisher;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly I참여자실행권한검사 _권한검사;
    private readonly ILogger<기사운송상태변경CommandExecutor> _logger;

    public 기사운송상태변경CommandExecutor(
        SsalddelContext db,
        I기사운송상태전이Service 상태전이Service,
        IPublisher publisher,
        ICurrentUserAccessor currentUserAccessor,
        I참여자실행권한검사 권한검사,
        ILogger<기사운송상태변경CommandExecutor> logger)
    {
        _db = db;
        _상태전이Service = 상태전이Service;
        _publisher = publisher;
        _currentUserAccessor = currentUserAccessor;
        _권한검사 = 권한검사;
        _logger = logger;
    }

    public async Task<Result<기사운송상태변경응답>> 실행Async(
        기사운송상태변경요청 request,
        CancellationToken cancellationToken)
    {
        if (!_권한검사.Try검증(_currentUserAccessor.UserId, _currentUserAccessor.Role, request.참여자Id, request.실행역할, out var 권한오류))
        {
            return Result.Fail<기사운송상태변경응답>(권한오류);
        }

        var entity = await _db.운송원장
            .FirstOrDefaultAsync(x => x.Id == request.운송Id && x.기사_운송자 == request.기사Id, cancellationToken);
        if (entity is null)
        {
            return Result.Fail<기사운송상태변경응답>(화물업무실패Factory.생성(
                "CargoTransportNotFound", "운송을 찾을 수 없습니다.", 화물업무실패Factory.목표단계(request.목표상태), 404));
        }

        // 마트 라스트마일은 현재 전표·시도 revision 검증 없이 화물 API로 완료할 수 없습니다.
        if (살뜰.Services.Dispatch.Engine.운송의뢰배차원천유형.Is살뜰마트음식주문(entity.원본의뢰유형))
        {
            return Result.Fail<기사운송상태변경응답>(화물업무실패Factory.생성(
                "MartDriverWorkflowRequired", "살뜰마트 배송 화면에서 현재 전표와 배송 상태를 확인해 주세요.",
                화물업무실패Factory.목표단계(request.목표상태), StatusCodes.Status409Conflict,
                OperatingSystemIds.SsalddelMartUrbanLogistics));
        }

        var requestId = !string.IsNullOrWhiteSpace(entity.의뢰Id) ? entity.의뢰Id : entity.운송번호;
        // Completed outbound rows still bind distributed custody. Use only legacy tables so unmigrated cargo databases remain compatible.
        if (!string.Equals(request.이벤트명, "DistributedCargoCustody", StringComparison.Ordinal)
            && (request.목표상태 is 기사운송상태코드.상차지도착 or 기사운송상태코드.상차완료
                or 기사운송상태코드.하차지도착 or 기사운송상태코드.인수완료)
            && !string.IsNullOrWhiteSpace(requestId)
            && await _db.출고예정.AsNoTracking()
                .Where(x => x.운송의뢰Id == requestId && x.입고상품Id != null)
                .Join(_db.입고상품.AsNoTracking(), x => x.입고상품Id!.Value, x => x.Id, (_, item) => item)
                .AnyAsync(x => x.보관위치.StartsWith("DLOC-"), cancellationToken))
        {
            return Result.Fail<기사운송상태변경응답>(화물업무실패Factory.생성(
                    "DistributedCargoCustodyRequired", "분산 물품은 현재 분산 순회 화면에서 피킹·전달을 확인해 주세요.",
                    화물업무실패Factory.목표단계(request.목표상태), StatusCodes.Status409Conflict,
                    OperatingSystemIds.WarehouseCommerceFulfillment)
                .WithMetadata("Code", "DistributedCargoCustodyRequired"));
        }

        var 이전상태 = entity.상태;
        if (!string.Equals(이전상태, request.목표상태, StringComparison.Ordinal))
        {
            var workflow = new 기사운송요약응답();
            기사운송업무상태Projector.투영(workflow, entity,
                await 기사운송업무상태Projector.사건조회Async(_db, [entity.Id], cancellationToken));
            if (workflow.운송진행보류)
                return Result.Fail<기사운송상태변경응답>(화물업무실패Factory.생성(
                    "CargoTransportHeld", "전체 운송이 보류되었습니다. 현재 예외 처리 상태를 확인해 주세요.",
                    OperatingSystemLifecycleStageIds.CargoInterruptionRecovery));
        }
        var now = DateTime.UtcNow;
        var 상태변경 = _상태전이Service.상태변경(entity, request.목표상태, now);
        if (상태변경.IsFailed)
        {
            return Result.Fail<기사운송상태변경응답>(상태변경.Errors.Select(error => 화물업무실패Factory.생성(
                "CargoTransportStateChanged", error.Message, 화물업무실패Factory.목표단계(request.목표상태), 409)));
        }

        request.상태변경전처리?.Invoke(entity, now);
        await 화주원장상태반영Async(entity, now, cancellationToken);

        Command알림Outbox? progressNotification = null;
        if (!string.IsNullOrWhiteSpace(requestId)
            && !string.Equals(이전상태, entity.상태, StringComparison.Ordinal))
        {
            _db.운송이벤트.Add(new 운송이벤트
            {
                의뢰Id = requestId,
                이벤트타입 = 운송이벤트유형.기사운송상태변경,
                이벤트시각 = now,
                메타데이터 = JsonSerializer.Serialize(new
                {
                    previousState = 이전상태,
                    targetState = entity.상태,
                    completionProjectionToken = string.Equals(
                        entity.상태,
                        기사운송상태코드.인수완료,
                        StringComparison.Ordinal)
                        ? Guid.NewGuid().ToString("N")
                        : string.Empty,
                    cargoCloseoutR69 = request.이벤트명 == nameof(운송인수완료됨Event)
                        ? CargoCompletionRecoveryMarker.Create(entity, now) : null
                })
            });
            if (CargoWorkflowProgressNotifications.StageForState(entity.상태) is { } progressStage)
            {
                var source = await _db.화주운송의뢰.FirstOrDefaultAsync(x => x.의뢰Id == requestId, cancellationToken);
                if (source is not null)
                    progressNotification = await CargoWorkflowProgressNotifications.ReserveAsync(_db, progressStage, source, entity.Id,
                        request.기사Id, now, Activity.Current?.TraceId.ToString() ?? string.Empty, cancellationToken);
            }
        }

        try { await _db.SaveChangesAsync(cancellationToken); }
        catch
        {
            CargoWorkflowProgressNotifications.DiscardUncommitted(_db, progressNotification);
            throw;
        }
        if (!string.Equals(이전상태, entity.상태, StringComparison.Ordinal))
        {
            if (request.Commit이후발행수집 is { } collect)
                collect(request.이벤트생성(new(entity, 이전상태, now, Activity.Current?.TraceId.ToString() ?? string.Empty)));
            else await PublishAfterCommitAsync(request, entity, 이전상태, now, cancellationToken);
        }

        return Result.Ok(new 기사운송상태변경응답
        {
            Id = entity.Id,
            운송번호 = entity.운송번호,
            상태 = entity.상태,
            UpdatedAt = entity.UpdatedAt
        });
    }

    private async Task 화주원장상태반영Async(
        운송원장 entity,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var requestId = !string.IsNullOrWhiteSpace(entity.의뢰Id)
            ? entity.의뢰Id
            : entity.운송번호;
        if (string.IsNullOrWhiteSpace(requestId))
        {
            return;
        }

        var shipperRequest = await _db.화주운송의뢰
            .FirstOrDefaultAsync(x => x.의뢰Id == requestId, cancellationToken);
        if (shipperRequest is null)
        {
            return;
        }

        shipperRequest.배차상태 = entity.상태 switch
        {
            기사운송상태코드.상차완료 => 상태값.배차상태.상차완료,
            기사운송상태코드.인수완료 => 상태값.배차상태.인수완료,
            _ => entity.상태
        };
        shipperRequest.UpdatedAt = now;
    }

    private async Task PublishAfterCommitAsync(
        기사운송상태변경요청 request,
        운송원장 entity,
        string 이전상태,
        DateTime now,
        CancellationToken cancellationToken)
    {
        try
        {
            var context = new 기사운송상태변경Context(
                entity,
                이전상태,
                now,
                Activity.Current?.TraceId.ToString() ?? string.Empty);
            await _publisher.Publish(request.이벤트생성(context), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "{EventName} 사후처리 이벤트 발행 중 예외가 발생했습니다. TransportId={TransportId}",
                request.이벤트명,
                entity.Id);
        }
    }
}
