using System.Data;
using System.Text.Json;
using FluentResults;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Contracts.Admin.Transport;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Fulfillment;
using Ssalddel.Contracts.Shipper.Request;
using 살뜰.Services.Options;
using 살뜰.도메인.결제;
using 살뜰.도메인.공통;
using 살뜰.도메인.운송;

namespace Ssalddel.Application.Shipper.Request;

public sealed class 관리자운송의뢰취소환불CommandHandler
    : IRequestHandler<관리자운송의뢰취소환불Command, Result<화주운송의뢰응답>>
{
    private readonly SsalddelContext _db;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly ISsalddelExecutionModePolicy _executionModePolicy;

    public 관리자운송의뢰취소환불CommandHandler(
        SsalddelContext db,
        ICurrentUserAccessor currentUserAccessor,
        ISsalddelExecutionModePolicy executionModePolicy)
    {
        _db = db;
        _currentUserAccessor = currentUserAccessor;
        _executionModePolicy = executionModePolicy;
    }

    public async Task<Result<화주운송의뢰응답>> Handle(
        관리자운송의뢰취소환불Command request,
        CancellationToken cancellationToken)
    {
        if (!주문자권한검사.IsServerAdmin(_currentUserAccessor))
        {
            return Result.Fail<화주운송의뢰응답>("서버관리자만 취소 또는 환불 상태를 기록할 수 있습니다.");
        }

        if (!_executionModePolicy.IsSimulation)
        {
            return Result.Fail<화주운송의뢰응답>(
                "Operational 모드에서는 외부 결제 취소·환불 연동이 준비되기 전 이 작업을 실행할 수 없습니다.");
        }

        var confirmationError = 관리자운송의뢰취소환불정책.명시적확인오류(
            request.RequestId,
            request.확인의뢰Id,
            request.사유);
        if (confirmationError is not null)
        {
            return Result.Fail<화주운송의뢰응답>(confirmationError);
        }

        var requestId = request.RequestId.Trim();
        var entity = await _db.화주운송의뢰
            .FirstOrDefaultAsync(x => x.의뢰Id == requestId, cancellationToken);
        if (entity is null)
        {
            return Result.Fail<화주운송의뢰응답>("의뢰를 찾을 수 없습니다.");
        }
        if (await _db.운송원장.AsNoTracking().AnyAsync(x => x.의뢰Id == requestId && x.일반회원배송전용, cancellationToken))
            return Result.Fail<화주운송의뢰응답>(new Error("일반 회원 배송은 전용 취소 또는 지원 사건 복구에서 물품 인수와 수행 책임을 먼저 확인해 주세요.")
                .WithMetadata("ErrorCode", "MemberDeliveryRecoveryRequired").WithMetadata("StatusCode", 409));

        // 같은 source/current를 읽는 처리처 선택과 직렬화하여 확인 뒤 재접수가 끼어들지 않게 합니다.
        // 일반 운송 취소의 기존 경로는 유지하고 모의 수행만 전용 취소 확인을 요구합니다.
        await using var fulfillmentTransaction = entity.모의처리처수행 && _db.Database.IsRelational()
            ? await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken) : null;
        if (entity.모의처리처수행)
        {
            await _db.Entry(entity).ReloadAsync(cancellationToken);
            var current = await _db.NeighborhoodFulfillments.AsNoTracking()
                .SingleOrDefaultAsync(x => x.SourceKind == FulfillmentSourceKinds.NeighborhoodDelivery && x.RequestId == requestId, cancellationToken);
            var attempt = current is null ? null : await _db.NeighborhoodFulfillmentAttempts.AsNoTracking()
                .SingleOrDefaultAsync(x => x.SourceKind == FulfillmentSourceKinds.NeighborhoodDelivery && x.AttemptId == current.AttemptId && x.RequestId == requestId, cancellationToken);
            var hasAttempts = await _db.NeighborhoodFulfillmentAttempts.AsNoTracking()
                .AnyAsync(x => x.SourceKind == FulfillmentSourceKinds.NeighborhoodDelivery && x.RequestId == requestId, cancellationToken);
            var unsent = current is null
                && !hasAttempts
                && !await _db.운송원장.AsNoTracking().AnyAsync(x => x.의뢰Id == requestId || x.원본의뢰Id == requestId, cancellationToken);
            // 자동 재선정 대기 중인 이전 실패만으로 원 의뢰를 종료하지 않습니다.
            // 전용 Coordinator가 현재 수행의 취소를 확인한 뒤에만 관리자 취소를 허용합니다.
            var verifiedClosed = current?.IsAutomatic == true
                ? current.StateCode == AutoFulfillmentStates.Cancelled
                    && (attempt?.StateCode is AutoFulfillmentStates.Cancelled or AutoFulfillmentStates.Failed
                        || string.IsNullOrEmpty(current.AttemptId) && !hasAttempts)
                : attempt?.StateCode is NeighborhoodFulfillmentStates.Cancelled or NeighborhoodFulfillmentStates.Failed;
            if (!unsent && !verifiedClosed)
                return Result.Fail<화주운송의뢰응답>(new Error("처리처 수행이 남아 있거나 결과를 확인할 수 없습니다. 배송 상세에서 처리처 취소 결과를 먼저 확인해 주세요.")
                    .WithMetadata("ErrorCode", "FulfillmentCancellationRequired").WithMetadata("StatusCode", 409));
        }

        if (string.Equals(entity.상태, 상태값.의뢰상태.취소, StringComparison.OrdinalIgnoreCase)
            && string.Equals(entity.배차상태, 상태값.배차상태.취소, StringComparison.OrdinalIgnoreCase)
            && (string.Equals(entity.결제상태, 상태값.결제상태.결제취소, StringComparison.OrdinalIgnoreCase)
                || string.Equals(entity.결제상태, 상태값.결제상태.환불됨, StringComparison.OrdinalIgnoreCase)))
        {
            return Result.Ok(화주운송의뢰매퍼.To응답(entity));
        }

        var decision = 관리자운송의뢰취소환불정책.평가(
            entity.상태,
            entity.결제상태,
            entity.정산상태,
            entity.배차상태);
        if (!decision.처리가능)
        {
            return Result.Fail<화주운송의뢰응답>(decision.안내문구);
        }

        var payments = await _db.결제
            .Where(x => x.의뢰Id == requestId || x.대상Id == requestId)
            .ToListAsync(cancellationToken);
        var refundRequired = decision.환불상태기록필요
            || payments.Any(x =>
                x.공통결제상태 == 결제공통정의.결제상태.승인완료
                || string.Equals(x.결제상태, 상태값.결제상태.결제완료, StringComparison.OrdinalIgnoreCase));

        var ledgers = await _db.운송원장
            .Where(x => x.의뢰Id == requestId || x.원본의뢰Id == requestId)
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        entity.상태 = 상태값.의뢰상태.취소;
        entity.결제상태 = refundRequired
            ? 상태값.결제상태.환불됨
            : 상태값.결제상태.결제취소;
        entity.정산상태 = 운임정산상태.정산취소.ToString();
        entity.배차상태 = 상태값.배차상태.취소;
        entity.UpdatedAt = now;

        foreach (var payment in payments)
        {
            payment.결제상태 = refundRequired
                ? 상태값.결제상태.환불됨
                : 상태값.결제상태.결제취소;
            payment.공통결제상태 = refundRequired
                ? 결제공통정의.결제상태.환불완료
                : 결제공통정의.결제상태.취소완료;
            payment.취소일시 = now;
        }

        foreach (var ledger in ledgers)
        {
            ledger.상태 = 상태값.배차상태.취소;
            ledger.배차큐단계 = 상태값.배차큐단계.종료;
            ledger.배차노출상태 = 상태값.배차노출상태.종료;
            ledger.현재추천대상기사Id = null;
            ledger.추천만료시각 = null;
            ledger.UpdatedAt = now;
        }

        _db.운송이벤트.Add(new 운송이벤트
        {
            의뢰Id = requestId,
            이벤트타입 = 운송이벤트유형.관리자취소환불상태기록,
            이벤트시각 = now,
            메타데이터 = JsonSerializer.Serialize(new
            {
                실행모드 = "Simulation",
                처리유형 = refundRequired ? "취소및환불" : "의뢰취소",
                관리자Id = _currentUserAccessor.UserId ?? "unknown",
                사유 = request.사유.Trim()
            })
        });

        await _db.SaveChangesAsync(cancellationToken);
        if (fulfillmentTransaction is not null) await fulfillmentTransaction.CommitAsync(cancellationToken);
        return Result.Ok(화주운송의뢰매퍼.To응답(entity));
    }
}
