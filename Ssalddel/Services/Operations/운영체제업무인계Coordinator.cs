using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Contracts.Common.Operations;
using Ssalddel.Contracts.Common.Versioning;
using 살뜰.도메인.공통;
using 살뜰.도메인.운영;

namespace 살뜰.Services.Operations;

public interface I운영체제업무인계Coordinator
{
    Task<운영체제업무인계Dto> 요청Async(
        운영체제업무인계생성요청 요청,
        CancellationToken cancellationToken = default);

    Task<운영체제업무인계Dto> 결정Async(
        string 인계StableId,
        운영체제업무인계결정요청 요청,
        CancellationToken cancellationToken = default);

    Task<운영체제업무인계Dto?> 조회Async(
        string 인계StableId,
        CancellationToken cancellationToken = default);
}

[SsalddelCodeMetadata(
    SsalddelCodeFeatureKeys.OperationalLogisticsOs,
    SsalddelCodeLayer.Application,
    "출발 운영 OS의 책임을 유지한 채 도착 운영 OS의 명시적 수락·거절·보류를 영속 원장과 Outbox에 기록한다.",
    Effects = SsalddelCodeEffect.PersistentRead | SsalddelCodeEffect.PersistentWrite,
    FlowOrder = 30,
    StepKey = "application.operating-system-handoff-coordinator",
    ExecutionStage = SsalddelCodeExecutionStage.Confirm,
    ReadsFrom = SsalddelCodeDataScope.OperationalState,
    WritesTo = SsalddelCodeDataScope.OperationalState,
    Boundary = "수락 전에는 출발 OS가 책임을 유지한다. 상대 OS 내부 Service·Engine·Store를 직접 호출하지 않고 인계 원장과 Outbox만 기록한다.")]
public sealed class 운영체제업무인계Coordinator(
    SsalddelContext db,
    TimeProvider timeProvider) : I운영체제업무인계Coordinator
{
    private const int 최대상태사본문자수 = 32_768;

    public async Task<운영체제업무인계Dto> 요청Async(
        운영체제업무인계생성요청 요청,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(요청);
        요청검증(요청);

        var sourceOs = OperatingSystemIds.Normalize(요청.출발운영체제Id);
        var targetOs = OperatingSystemIds.Normalize(요청.도착운영체제Id);
        if (sourceOs == targetOs)
        {
            throw new ArgumentException("출발 OS와 도착 OS는 서로 달라야 합니다.", nameof(요청));
        }

        var idempotencyKey = 요청.클라이언트요청Id.ToString("N");
        var existing = await db.운영체제업무인계
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.출발운영체제Id == sourceOs
                        && item.생성멱등Key == idempotencyKey,
                cancellationToken);
        if (existing is not null)
        {
            동일생성요청검증(existing, 요청, targetOs);
            return ToDto(existing);
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (요청.만료시각Utc.Kind != DateTimeKind.Utc || 요청.만료시각Utc <= now)
        {
            throw new ArgumentException("인계 만료시각은 현재보다 이후인 UTC 시각이어야 합니다.", nameof(요청));
        }

        var entity = new 운영체제업무인계
        {
            인계StableId = $"os-handoff:{요청.클라이언트요청Id:N}",
            생성멱등Key = idempotencyKey,
            출발운영체제Id = sourceOs,
            도착운영체제Id = targetOs,
            현재책임운영체제Id = sourceOs,
            출발업무유형Code = 요청.출발업무유형Code.Trim(),
            출발업무StableId = 요청.출발업무StableId.Trim(),
            출발업무Revision = 요청.출발업무Revision,
            인계계약Code = 요청.인계계약Code.Trim(),
            인계계약Revision = 요청.인계계약Revision.Trim(),
            최소상태사본Json = 정규화Json(요청.최소상태사본Json),
            공개범위Code = 요청.공개범위Code.Trim(),
            상태Code = 운영체제업무인계상태Codes.요청됨,
            Revision = 1,
            요청시각Utc = now,
            만료시각Utc = 요청.만료시각Utc,
            CreatedAt = now,
            UpdatedAt = now
        };

        var outbox = Outbox생성(
            entity,
            "OperatingSystemHandoffRequested",
            $"handoff:{entity.인계StableId}:requested:r{entity.Revision}",
            now);
        db.운영체제업무인계.Add(entity);
        db.운영체제업무인계Outbox.Add(outbox);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            db.Entry(entity).State = EntityState.Detached;
            db.Entry(outbox).State = EntityState.Detached;
            var concurrent = await db.운영체제업무인계
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    item => item.출발운영체제Id == sourceOs
                            && item.생성멱등Key == idempotencyKey,
                    cancellationToken);
            if (concurrent is null)
            {
                throw;
            }

            동일생성요청검증(concurrent, 요청, targetOs);
            return ToDto(concurrent);
        }

        return ToDto(entity);
    }

    public async Task<운영체제업무인계Dto> 결정Async(
        string 인계StableId,
        운영체제업무인계결정요청 요청,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(인계StableId);
        ArgumentNullException.ThrowIfNull(요청);
        if (요청.클라이언트요청Id == Guid.Empty)
        {
            throw new ArgumentException("결정 요청 ID가 필요합니다.", nameof(요청));
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(요청.응답운영체제Id);

        var decisionCode = 요청.결정Code.Trim();
        if (!운영체제업무인계결정Codes.IsKnown(decisionCode))
        {
            throw new ArgumentException("알 수 없는 인계 결정입니다.", nameof(요청));
        }

        var entity = await db.운영체제업무인계.SingleOrDefaultAsync(
            item => item.인계StableId == 인계StableId.Trim(),
            cancellationToken) ?? throw new KeyNotFoundException("운영체제 업무 인계 요청을 찾을 수 없습니다.");
        var respondingOs = OperatingSystemIds.Normalize(요청.응답운영체제Id);
        if (respondingOs != entity.도착운영체제Id)
        {
            throw new InvalidOperationException("도착 운영 OS만 인계 요청을 수락·거절·보류할 수 있습니다.");
        }
        var requestId = 요청.클라이언트요청Id.ToString("N");

        if (entity.마지막응답요청Id == requestId)
        {
            동일결정요청검증(entity, 요청, decisionCode);
            return ToDto(entity);
        }

        if (운영체제업무인계상태Codes.Is종결(entity.상태Code))
        {
            throw new InvalidOperationException($"이미 종결된 인계는 다시 결정할 수 없습니다. Status={entity.상태Code}");
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (entity.만료시각Utc <= now)
        {
            entity.상태Code = 운영체제업무인계상태Codes.만료됨;
            entity.Revision += 1;
            entity.UpdatedAt = now;
            db.운영체제업무인계Outbox.Add(Outbox생성(
                entity,
                "OperatingSystemHandoffExpired",
                $"handoff:{entity.인계StableId}:expired:r{entity.Revision}",
                now));
            await db.SaveChangesAsync(cancellationToken);
            return ToDto(entity);
        }

        if (요청.예상Revision != entity.Revision)
        {
            throw new DbUpdateConcurrencyException(
                $"인계 revision이 변경되었습니다. Expected={요청.예상Revision}, Actual={entity.Revision}");
        }

        var targetWorkId = 요청.도착업무StableId.Trim();
        var reasonCode = 요청.사유Code.Trim();
        if (decisionCode == 운영체제업무인계결정Codes.수락 && string.IsNullOrWhiteSpace(targetWorkId))
        {
            throw new ArgumentException("인계를 수락하려면 도착 업무 Stable ID가 필요합니다.", nameof(요청));
        }

        if (decisionCode != 운영체제업무인계결정Codes.수락 && string.IsNullOrWhiteSpace(reasonCode))
        {
            throw new ArgumentException("인계를 거절하거나 보류하려면 사유 코드가 필요합니다.", nameof(요청));
        }

        entity.상태Code = decisionCode switch
        {
            운영체제업무인계결정Codes.수락 => 운영체제업무인계상태Codes.수락됨,
            운영체제업무인계결정Codes.거절 => 운영체제업무인계상태Codes.거절됨,
            _ => 운영체제업무인계상태Codes.보류됨
        };
        entity.현재책임운영체제Id = decisionCode == 운영체제업무인계결정Codes.수락
            ? entity.도착운영체제Id
            : entity.출발운영체제Id;
        entity.도착업무StableId = targetWorkId;
        entity.마지막응답요청Id = requestId;
        entity.마지막응답결정Code = decisionCode;
        entity.응답사유Code = reasonCode;
        entity.응답시각Utc = now;
        entity.Revision += 1;
        entity.UpdatedAt = now;

        var outbox = Outbox생성(
            entity,
            decisionCode switch
            {
                운영체제업무인계결정Codes.수락 => "OperatingSystemHandoffAccepted",
                운영체제업무인계결정Codes.거절 => "OperatingSystemHandoffRejected",
                _ => "OperatingSystemHandoffHeld"
            },
            $"handoff:{entity.인계StableId}:decision:{requestId}",
            now);
        db.운영체제업무인계Outbox.Add(outbox);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            var concurrent = await db.운영체제업무인계
                .AsNoTracking()
                .SingleAsync(item => item.인계StableId == 인계StableId.Trim(), cancellationToken);
            if (concurrent.마지막응답요청Id != requestId)
            {
                throw;
            }

            동일결정요청검증(concurrent, 요청, decisionCode);
            return ToDto(concurrent);
        }

        return ToDto(entity);
    }

    public async Task<운영체제업무인계Dto?> 조회Async(
        string 인계StableId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(인계StableId);
        var entity = await db.운영체제업무인계
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.인계StableId == 인계StableId.Trim(), cancellationToken);
        return entity is null ? null : ToDto(entity);
    }

    private static void 요청검증(운영체제업무인계생성요청 요청)
    {
        if (요청.클라이언트요청Id == Guid.Empty)
            throw new ArgumentException("인계 요청 ID가 필요합니다.", nameof(요청));
        ArgumentException.ThrowIfNullOrWhiteSpace(요청.출발운영체제Id);
        ArgumentException.ThrowIfNullOrWhiteSpace(요청.도착운영체제Id);
        ArgumentException.ThrowIfNullOrWhiteSpace(요청.출발업무유형Code);
        ArgumentException.ThrowIfNullOrWhiteSpace(요청.출발업무StableId);
        ArgumentException.ThrowIfNullOrWhiteSpace(요청.인계계약Code);
        ArgumentException.ThrowIfNullOrWhiteSpace(요청.인계계약Revision);
        if (요청.출발업무Revision < 0)
            throw new ArgumentException("출발 업무 revision은 음수일 수 없습니다.", nameof(요청));
        if (요청.공개범위Code != 운영체제업무인계공개범위Codes.최소필요업무자료)
            throw new ArgumentException("인계에는 최소 필요 업무 자료 공개 범위만 사용할 수 있습니다.", nameof(요청));
        _ = 정규화Json(요청.최소상태사본Json);
    }

    private static string 정규화Json(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 최대상태사본문자수)
            throw new ArgumentException($"최소 상태 사본은 비어 있지 않은 {최대상태사본문자수:N0}자 이하 JSON이어야 합니다.");
        using var document = JsonDocument.Parse(value);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("최소 상태 사본은 JSON object여야 합니다.");
        return document.RootElement.GetRawText();
    }

    private static void 동일생성요청검증(
        운영체제업무인계 existing,
        운영체제업무인계생성요청 요청,
        string targetOs)
    {
        if (existing.도착운영체제Id != targetOs
            || existing.출발업무유형Code != 요청.출발업무유형Code.Trim()
            || existing.출발업무StableId != 요청.출발업무StableId.Trim()
            || existing.출발업무Revision != 요청.출발업무Revision
            || existing.인계계약Code != 요청.인계계약Code.Trim()
            || existing.인계계약Revision != 요청.인계계약Revision.Trim()
            || existing.최소상태사본Json != 정규화Json(요청.최소상태사본Json)
            || existing.공개범위Code != 요청.공개범위Code.Trim()
            || existing.만료시각Utc != 요청.만료시각Utc)
        {
            throw new InvalidOperationException("같은 인계 요청 ID를 다른 내용으로 다시 사용할 수 없습니다.");
        }
    }

    private static void 동일결정요청검증(
        운영체제업무인계 existing,
        운영체제업무인계결정요청 요청,
        string decisionCode)
    {
        if (existing.마지막응답결정Code != decisionCode
            || existing.도착업무StableId != 요청.도착업무StableId.Trim()
            || existing.응답사유Code != 요청.사유Code.Trim())
        {
            throw new InvalidOperationException("같은 결정 요청 ID를 다른 내용으로 다시 사용할 수 없습니다.");
        }
    }

    private static 운영체제업무인계Outbox Outbox생성(
        운영체제업무인계 entity,
        string eventType,
        string idempotencyKey,
        DateTime now)
        => new()
        {
            멱등Key = idempotencyKey,
            인계StableId = entity.인계StableId,
            이벤트Type = eventType,
            PayloadJson = JsonSerializer.Serialize(new
            {
                entity.인계StableId,
                entity.출발운영체제Id,
                entity.도착운영체제Id,
                entity.현재책임운영체제Id,
                entity.출발업무유형Code,
                entity.출발업무StableId,
                entity.출발업무Revision,
                entity.도착업무StableId,
                entity.인계계약Code,
                entity.인계계약Revision,
                entity.상태Code,
                entity.응답사유Code,
                entity.Revision
            }),
            처리상태Code = 운영체제업무인계Outbox상태Codes.대기,
            CreatedAt = now,
            UpdatedAt = now
        };

    internal static 운영체제업무인계Dto ToDto(운영체제업무인계 entity)
        => new()
        {
            인계StableId = entity.인계StableId,
            출발운영체제Id = entity.출발운영체제Id,
            도착운영체제Id = entity.도착운영체제Id,
            현재책임운영체제Id = entity.현재책임운영체제Id,
            출발업무유형Code = entity.출발업무유형Code,
            출발업무StableId = entity.출발업무StableId,
            출발업무Revision = entity.출발업무Revision,
            도착업무StableId = entity.도착업무StableId,
            인계계약Code = entity.인계계약Code,
            인계계약Revision = entity.인계계약Revision,
            공개범위Code = entity.공개범위Code,
            상태Code = entity.상태Code,
            응답사유Code = entity.응답사유Code,
            Revision = entity.Revision,
            요청시각Utc = entity.요청시각Utc,
            만료시각Utc = entity.만료시각Utc,
            응답시각Utc = entity.응답시각Utc
        };
}
