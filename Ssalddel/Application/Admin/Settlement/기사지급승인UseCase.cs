using System.Text.Json;
using FluentResults;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Ssalddel.ApiMetadata;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Application.Driver.Settlement;
using Ssalddel.Contracts.Admin.Settlement;
using Ssalddel.Contracts.Shipper.Request;
using 살뜰.Data;
using 살뜰.Services.Options;
using 살뜰.도메인.공통;
using 살뜰.도메인.기사;
using 살뜰.도메인.설정;
using 살뜰.도메인.운송;
using 살뜰.도메인.화주;

namespace Ssalddel.Application.Admin.Settlement;

public interface I기사지급승인UseCase
{
    Task<Result<기사지급승인응답>> 승인Async(
        기사지급승인요청 request,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<기사지급승인응답>>> 목록조회Async(
        int? year,
        int? month,
        string? driverId,
        CancellationToken cancellationToken = default);
}

[SsalddelApiWorkflow(SsalddelWorkflow.DomesticTransport)]
[SsalddelUseCase(
    "기사 운송대금 지급 승인",
    Summary = "완료 운송의 기사 지급 조건을 재검증하고 멱등 승인과 지급 Outbox를 기록합니다. 실제 송금 완료는 별도 Provider 결과로만 기록합니다.")]
[SsalddelUseCaseActor(SsalddelActor.PlatformOperator)]
public sealed class 기사지급승인UseCase : I기사지급승인UseCase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly SsalddelContext _db;
    private readonly ICurrentUserAccessor _currentUser;
    private readonly ISsalddelExecutionModePolicy _executionMode;
    private readonly TimeProvider _timeProvider;

    public 기사지급승인UseCase(
        SsalddelContext db,
        ICurrentUserAccessor currentUser,
        ISsalddelExecutionModePolicy executionMode,
        TimeProvider timeProvider)
    {
        _db = db;
        _currentUser = currentUser;
        _executionMode = executionMode;
        _timeProvider = timeProvider;
    }

    public async Task<Result<기사지급승인응답>> 승인Async(
        기사지급승인요청 request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!IsServerAdmin())
        {
            return 상태실패<기사지급승인응답>(
                "서버관리자만 기사 지급을 승인할 수 있습니다.",
                StatusCodes.Status403Forbidden);
        }

        var validationError = Validate(request);
        if (validationError is not null)
        {
            return 상태실패<기사지급승인응답>(
                validationError,
                StatusCodes.Status400BadRequest);
        }

        var idempotencyKey = request.IdempotencyKey.Trim();
        var existing = await _db.기사운송대금지급요청
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.멱등키 == idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            if (existing.운송Id != request.TransportId
                || !string.Equals(
                    existing.의뢰Id,
                    request.ConfirmedRequestId.Trim(),
                    StringComparison.Ordinal)
                || existing.지급예정금액 != request.ConfirmedExpectedPayoutAmount
                || !string.Equals(
                    existing.통화코드,
                    request.CurrencyCode.Trim(),
                    StringComparison.OrdinalIgnoreCase))
            {
                return 상태실패<기사지급승인응답>(
                    "같은 멱등 키가 다른 기사 지급 승인 자료에 이미 사용되었습니다.",
                    StatusCodes.Status409Conflict);
            }

            return Result.Ok(await ToResponseAsync(existing, true, cancellationToken));
        }

        var transport = await _db.운송원장
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == request.TransportId, cancellationToken);
        if (transport is null)
        {
            return 상태실패<기사지급승인응답>(
                "완료 운송 원장을 찾을 수 없습니다.",
                StatusCodes.Status404NotFound);
        }

        if (!transport.도착.HasValue)
        {
            return 상태실패<기사지급승인응답>(
                "운송 완료가 확인된 뒤에만 지급을 승인할 수 있습니다.",
                StatusCodes.Status409Conflict);
        }

        var requestId = request.ConfirmedRequestId.Trim();
        var sourceRequest = await _db.화주운송의뢰
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.의뢰Id == requestId, cancellationToken);
        if (sourceRequest is null
            || (!string.Equals(transport.의뢰Id, requestId, StringComparison.Ordinal)
                && !string.Equals(transport.운송번호, requestId, StringComparison.Ordinal)))
        {
            return 상태실패<기사지급승인응답>(
                "확인한 의뢰 ID가 운송 원장의 원천 의뢰와 일치하지 않습니다.",
                StatusCodes.Status409Conflict);
        }

        var fare = await _db.운임구성
            .AsNoTracking()
            .Where(x => x.의뢰Id == requestId)
            .OrderByDescending(x => x.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        var driverId = transport.확정기사Id ?? transport.기사_운송자 ?? string.Empty;
        if (string.IsNullOrWhiteSpace(driverId))
        {
            return 상태실패<기사지급승인응답>(
                "확정 기사를 확인할 수 없습니다.",
                StatusCodes.Status409Conflict);
        }

        var account = await _db.Set<기사정산계좌>()
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.기사Id == driverId, cancellationToken);
        var hasOpenAbnormalTransportIncident = await _db.비정상운송사건
            .AsNoTracking()
            .AnyAsync(
                x => x.운송의뢰Id == requestId
                     && (x.정산보류적용여부
                         || x.상태Code == 비정상운송사건상태Codes.운영검토대기),
                cancellationToken);
        var readiness = 기사지급준비UseCase.준비상태판정(
            sourceRequest,
            fare?.기사지급예정운임,
            account is not null,
            string.Equals(
                account?.확인상태,
                기사정산계좌확인상태.확인완료,
                StringComparison.Ordinal),
            hasOpenAbnormalTransportIncident);
        if (!readiness.IsReady)
        {
            return 상태실패<기사지급승인응답>(
                readiness.Message,
                StatusCodes.Status409Conflict);
        }

        if (fare!.기사지급예정운임 != request.ConfirmedExpectedPayoutAmount)
        {
            return 상태실패<기사지급승인응답>(
                "확인한 지급 예정 금액이 현재 운임 구성과 일치하지 않습니다.",
                StatusCodes.Status409Conflict);
        }

        var duplicateTransport = await _db.기사운송대금지급요청
            .AsNoTracking()
            .AnyAsync(x => x.운송Id == transport.Id, cancellationToken);
        if (duplicateTransport)
        {
            return 상태실패<기사지급승인응답>(
                "이 운송에는 이미 다른 기사 지급 승인 기록이 있습니다.",
                StatusCodes.Status409Conflict);
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var entity = new 기사운송대금지급요청
        {
            운송Id = transport.Id,
            운송번호 = transport.운송번호,
            의뢰Id = requestId,
            기사Id = driverId,
            지급예정금액 = request.ConfirmedExpectedPayoutAmount,
            통화코드 = request.CurrencyCode.Trim().ToUpperInvariant(),
            멱등키 = idempotencyKey,
            상태코드 = 기사지급요청상태코드.승인됨,
            승인관리자Id = _currentUser.UserId ?? "unknown",
            승인사유 = request.ApprovalReason.Trim(),
            실행모드코드 = _executionMode.Mode.ToString(),
            승인일시Utc = now,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        _db.기사운송대금지급요청.Add(entity);
        var outbox = new 기사지급Outbox
        {
            기사지급요청 = entity,
            멱등키 = idempotencyKey,
            PayloadJson = JsonSerializer.Serialize(
                new 기사지급OutboxPayload(
                    entity.멱등키,
                    entity.운송Id,
                    entity.의뢰Id,
                    entity.기사Id,
                    entity.지급예정금액,
                    entity.통화코드),
                JsonOptions),
            처리상태 = 기사지급Outbox상태코드.대기,
            다음시도시각Utc = now,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        _db.기사지급Outbox.Add(outbox);
        await _db.SaveChangesAsync(cancellationToken);

        return Result.Ok(ToResponse(entity, outbox, false));
    }

    public async Task<Result<IReadOnlyList<기사지급승인응답>>> 목록조회Async(
        int? year,
        int? month,
        string? driverId,
        CancellationToken cancellationToken = default)
    {
        if (!IsServerAdmin())
        {
            return 상태실패<IReadOnlyList<기사지급승인응답>>(
                "서버관리자만 기사 지급 승인 이력을 조회할 수 있습니다.",
                StatusCodes.Status403Forbidden);
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var targetYear = year ?? now.Year;
        var targetMonth = month ?? now.Month;
        if (targetYear is < 2000 or > 2100 || targetMonth is < 1 or > 12)
        {
            return 상태실패<IReadOnlyList<기사지급승인응답>>(
                "year와 month가 올바르지 않습니다.",
                StatusCodes.Status400BadRequest);
        }

        var startUtc = new DateTime(targetYear, targetMonth, 1, 0, 0, 0, DateTimeKind.Utc);
        var endUtc = startUtc.AddMonths(1);
        var query = _db.기사운송대금지급요청
            .AsNoTracking()
            .Where(x => x.승인일시Utc >= startUtc && x.승인일시Utc < endUtc);
        if (!string.IsNullOrWhiteSpace(driverId))
        {
            var normalizedDriverId = driverId.Trim();
            query = query.Where(x => x.기사Id == normalizedDriverId);
        }

        var requests = await query
            .OrderByDescending(x => x.승인일시Utc)
            .ToArrayAsync(cancellationToken);
        var requestIds = requests.Select(x => x.Id).ToList();
        var outboxes = await _db.기사지급Outbox
            .AsNoTracking()
            .Where(x => requestIds.Contains(x.기사지급요청Id))
            .ToDictionaryAsync(x => x.기사지급요청Id, cancellationToken);
        return Result.Ok<IReadOnlyList<기사지급승인응답>>(
            requests.Select(x => ToResponse(x, outboxes.GetValueOrDefault(x.Id), false)).ToArray());
    }

    private async Task<기사지급승인응답> ToResponseAsync(
        기사운송대금지급요청 entity,
        bool idempotentReplay,
        CancellationToken cancellationToken)
    {
        var outbox = await _db.기사지급Outbox
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.기사지급요청Id == entity.Id, cancellationToken);
        return ToResponse(entity, outbox, idempotentReplay);
    }

    private static 기사지급승인응답 ToResponse(
        기사운송대금지급요청 entity,
        기사지급Outbox? outbox,
        bool idempotentReplay)
        => new()
        {
            PayoutRequestId = entity.Id,
            TransportId = entity.운송Id,
            TransportNumber = entity.운송번호,
            RequestId = entity.의뢰Id,
            DriverId = entity.기사Id,
            ExpectedPayoutAmount = entity.지급예정금액,
            CurrencyCode = entity.통화코드,
            StatusCode = entity.상태코드,
            ExecutionModeCode = entity.실행모드코드,
            ApprovedBy = entity.승인관리자Id,
            ApprovedAtUtc = entity.승인일시Utc,
            SimulationVerifiedAtUtc = entity.Simulation검증일시Utc,
            OutboxStatusCode = outbox?.처리상태 ?? string.Empty,
            OutboxAttemptCount = outbox?.시도횟수 ?? 0,
            NextAttemptAtUtc = outbox?.다음시도시각Utc,
            LastResultCode = entity.마지막처리코드,
            LastResultMessage = entity.마지막처리메시지,
            IsIdempotentReplay = idempotentReplay,
            IsActualTransferCompleted = false
        };

    private static string? Validate(기사지급승인요청 request)
    {
        if (request.TransportId <= 0)
        {
            return "운송 ID가 올바르지 않습니다.";
        }

        if (string.IsNullOrWhiteSpace(request.ConfirmedRequestId)
            || string.IsNullOrWhiteSpace(request.IdempotencyKey)
            || string.IsNullOrWhiteSpace(request.CurrencyCode)
            || string.IsNullOrWhiteSpace(request.ApprovalReason))
        {
            return "확인 의뢰 ID, 멱등 키, 통화, 승인 사유가 필요합니다.";
        }

        if (request.IdempotencyKey.Trim().Length > 128)
        {
            return "멱등 키는 128자 이하여야 합니다.";
        }

        return request.ConfirmedExpectedPayoutAmount <= 0m
            ? "확인한 지급 예정 금액은 0보다 커야 합니다."
            : null;
    }

    private bool IsServerAdmin()
        => string.Equals(_currentUser.Role, 역할명.서버관리자, StringComparison.OrdinalIgnoreCase);

    private static Result<T> 상태실패<T>(string message, int statusCode)
        => Result.Fail<T>(new Error(message).WithMetadata("StatusCode", statusCode));
}

public sealed record 기사지급OutboxPayload(
    string IdempotencyKey,
    long TransportId,
    string RequestId,
    string DriverId,
    decimal ExpectedPayoutAmount,
    string CurrencyCode);
