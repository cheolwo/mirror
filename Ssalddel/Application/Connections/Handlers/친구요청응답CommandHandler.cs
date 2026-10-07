using FluentResults;
using MediatR;
using System.Data;
using System.Text.Json;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Application.Connections.Commands;
using 살뜰.도메인.사용자;
using 살뜰.도메인.설정;

namespace Ssalddel.Application.Connections.Handlers;

public sealed class 친구요청응답CommandHandler : IRequestHandler<친구요청응답Command, Result<Unit>>
{
    private readonly SsalddelContext _db;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public 친구요청응답CommandHandler(SsalddelContext db, ICurrentUserAccessor currentUserAccessor)
    {
        _db = db;
        _currentUserAccessor = currentUserAccessor;
    }

    public async Task<Result<Unit>> Handle(친구요청응답Command request, CancellationToken cancellationToken)
    {
        var attempt = 0;
        return await _db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            if (attempt++ > 0) _db.ChangeTracker.Clear();
            await using var transaction = _db.Database.IsRelational()
                ? await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
                : null;
            var result = await PersistAsync(request, cancellationToken);
            if (result.IsSuccess && transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            return result;
        });
    }

    private async Task<Result<Unit>> PersistAsync(친구요청응답Command request, CancellationToken cancellationToken)
    {
        var entity = await _db.친구요청
            .FirstOrDefaultAsync(x => x.Id == request.친구요청Id, cancellationToken);

        if (entity is null)
        {
            return Result.Fail<Unit>("친구 요청을 찾을 수 없습니다.");
        }

        if (!string.Equals(entity.대상자참여자Id, _currentUserAccessor.UserId, StringComparison.Ordinal))
        {
            return Result.Fail<Unit>("요청 수신자만 응답할 수 있습니다.");
        }

        if (entity.상태 != 친구요청상태.대기)
        {
            return Result.Fail<Unit>("이미 처리된 친구 요청입니다.");
        }

        var actorUserId = _currentUserAccessor.UserId!;
        if (request.수락 && request.공개동의 is not null
            && !string.Equals(request.공개동의.동의자참여자Id, actorUserId, StringComparison.Ordinal))
        {
            return Result.Fail<Unit>("연락처 공개 동의는 본인 명의로만 기록할 수 있습니다.");
        }

        var now = DateTimeOffset.UtcNow;

        if (request.수락)
        {
            entity.상태 = 친구요청상태.수락;
            entity.응답일시 = now;

            if (request.공개동의 is not null)
            {
                var consent = await _db.연락처공개동의
                    .FirstOrDefaultAsync(x => x.친구요청Id == entity.Id && x.동의자참여자Id == actorUserId, cancellationToken);

                if (consent is null)
                {
                    consent = new 연락처공개동의
                    {
                        친구요청Id = entity.Id,
                        동의자참여자Id = actorUserId,
                        동의일시 = now
                    };
                    _db.연락처공개동의.Add(consent);
                }

                consent.프로필공개 = request.공개동의.프로필공개;
                consent.업체명공개 = request.공개동의.업체명공개;
                consent.이메일공개 = request.공개동의.이메일공개;
                consent.전화번호공개 = request.공개동의.전화번호공개;
                consent.카카오채널공개 = request.공개동의.카카오채널공개;
                consent.판매채널공개 = request.공개동의.판매채널공개;
                consent.제공목적 = request.공개동의.제공목적;
                consent.철회일시 = null;
            }
        }
        else
        {
            entity.상태 = 친구요청상태.거절;
            entity.응답일시 = now;
            entity.거절사유 = request.거절사유?.Trim();
        }

        _db.Command알림Outbox.Add(new Command알림Outbox
        {
            CommandName = nameof(친구요청응답CommandHandler),
            // 기존 Outbox consumer 호환을 위해 EventName은 유지한다.
            EventName = "인연연결요청응답됨",
            FeatureName = "Connection",
            Target = "Participant",
            PayloadJson = JsonSerializer.Serialize(new
            {
                entity.Id,
                entity.요청자참여자Id,
                entity.대상자참여자Id,
                상태 = entity.상태.ToString(),
                entity.응답일시,
                entity.거절사유
            }),
            Status = "Pending",
            TraceId = System.Diagnostics.Activity.Current?.TraceId.ToString() ?? string.Empty,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);

        return Result.Ok(Unit.Value);
    }
}
