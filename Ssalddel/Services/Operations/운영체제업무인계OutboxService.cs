using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Services.Outbox;
using 살뜰.Data;
using 살뜰.Services.Options;
using 살뜰.도메인.운영;

namespace 살뜰.Services.Operations;

public sealed class 운영체제업무인계OutboxOptions
{
    public const string SectionName = "OperatingSystemHandoffOutbox";

    public bool Enabled { get; set; }

    public int BatchSize { get; set; } = 100;

    public int IntervalSeconds { get; set; } = 5;
}

public sealed record 운영체제업무인계Outbox전달됨Event(
    long OutboxId,
    string 멱등Key,
    string 인계StableId,
    string 이벤트Type,
    string 출발운영체제Id,
    string 도착운영체제Id,
    string 현재책임운영체제Id,
    long 인계Revision,
    string PayloadJson) : INotification;

public interface I운영체제업무인계OutboxPublisher
{
    Task 발행Async(
        운영체제업무인계Outbox전달됨Event notification,
        CancellationToken cancellationToken = default);
}

public sealed class MediatR운영체제업무인계OutboxPublisher(IPublisher publisher)
    : I운영체제업무인계OutboxPublisher
{
    public Task 발행Async(
        운영체제업무인계Outbox전달됨Event notification,
        CancellationToken cancellationToken = default)
        => publisher.Publish(notification, cancellationToken);
}

public interface I운영체제업무인계OutboxService
{
    Task<int> 대기항목처리Async(
        int take = 100,
        CancellationToken cancellationToken = default);

    Task<bool> 실패항목재시도예약Async(
        long outboxId,
        int expectedAttemptCount,
        CancellationToken cancellationToken = default);
}

[SsalddelCodeMetadata(
    SsalddelCodeFeatureKeys.OperationalLogisticsOs,
    SsalddelCodeLayer.Application,
    "운영체제 업무 인계 Outbox를 내구성 있는 내부 Event로 전달하고 실패 항목을 멱등 재처리한다.",
    Effects = SsalddelCodeEffect.PersistentRead | SsalddelCodeEffect.PersistentWrite,
    FlowOrder = 35,
    StepKey = "application.operating-system-handoff-outbox",
    ExecutionStage = SsalddelCodeExecutionStage.Persistence,
    ReadsFrom = SsalddelCodeDataScope.OperationalState,
    WritesTo = SsalddelCodeDataScope.OperationalState,
    Boundary = "인계 업무 상태나 책임 OS를 변경하지 않는다. 전달은 최소 Event 봉투만 발행하며 구독자는 멱등 키로 중복을 방어한다.")]
public sealed class 운영체제업무인계OutboxService(
    SsalddelContext db,
    I운영체제업무인계OutboxPublisher publisher,
    TimeProvider timeProvider,
    ILogger<운영체제업무인계OutboxService> logger)
    : I운영체제업무인계OutboxService
{
    public async Task<int> 대기항목처리Async(
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var leaseCutoff = now - OutboxProcessingPolicy.LeaseTimeout;
        var candidateIds = await db.운영체제업무인계Outbox
            .AsNoTracking()
            .Where(item =>
                item.처리상태Code == 운영체제업무인계Outbox상태Codes.대기
                || (item.처리상태Code == 운영체제업무인계Outbox상태Codes.재시도대기
                    && (item.다음처리시각Utc == null || item.다음처리시각Utc <= now))
                || (item.처리상태Code == 운영체제업무인계Outbox상태Codes.처리중
                    && item.UpdatedAt <= leaseCutoff))
            .OrderBy(item => item.CreatedAt)
            .Select(item => item.Id)
            .Take(Math.Clamp(take, 1, 500))
            .ToListAsync(cancellationToken);

        var claimedCount = 0;
        foreach (var candidateId in candidateIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var claim = await ClaimAsync(candidateId, cancellationToken);
            if (claim is null)
            {
                continue;
            }

            claimedCount++;
            try
            {
                var handoff = await db.운영체제업무인계
                    .AsNoTracking()
                    .SingleOrDefaultAsync(
                        item => item.인계StableId == claim.인계StableId,
                        cancellationToken)
                    ?? throw new InvalidOperationException("Outbox가 참조하는 운영체제 업무 인계 원장을 찾을 수 없습니다.");

                using var payload = JsonDocument.Parse(claim.PayloadJson);
                var payloadRoot = payload.RootElement;
                if (payloadRoot.ValueKind != JsonValueKind.Object)
                {
                    throw new InvalidOperationException("운영체제 업무 인계 Outbox payload는 JSON object여야 합니다.");
                }

                var payloadHandoffStableId = RequiredString(
                    payloadRoot,
                    nameof(운영체제업무인계.인계StableId));
                if (!string.Equals(payloadHandoffStableId, handoff.인계StableId, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("운영체제 업무 인계 Outbox와 원장의 고유 식별자가 일치하지 않습니다.");
                }

                await publisher.발행Async(
                    new 운영체제업무인계Outbox전달됨Event(
                        claim.Id,
                        claim.멱등Key,
                        claim.인계StableId,
                        claim.이벤트Type,
                        RequiredString(payloadRoot, nameof(운영체제업무인계.출발운영체제Id)),
                        RequiredString(payloadRoot, nameof(운영체제업무인계.도착운영체제Id)),
                        RequiredString(payloadRoot, nameof(운영체제업무인계.현재책임운영체제Id)),
                        RequiredInt64(payloadRoot, nameof(운영체제업무인계.Revision)),
                        claim.PayloadJson),
                    cancellationToken);

                await CompleteAsync(claim, cancellationToken);
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                await FailAsync(claim, exception, cancellationToken);
            }
        }

        return claimedCount;
    }

    public async Task<bool> 실패항목재시도예약Async(
        long outboxId,
        int expectedAttemptCount,
        CancellationToken cancellationToken = default)
    {
        if (outboxId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(outboxId));
        }

        var item = await db.운영체제업무인계Outbox
            .SingleOrDefaultAsync(candidate => candidate.Id == outboxId, cancellationToken);
        if (item is null)
        {
            return false;
        }

        if (item.처리시도수 != expectedAttemptCount)
        {
            throw new DbUpdateConcurrencyException("후속 처리 상태가 이미 변경되었습니다. 목록을 새로 조회해 주세요.");
        }

        if (item.처리상태Code != 운영체제업무인계Outbox상태Codes.실패)
        {
            throw new InvalidOperationException("자동 재시도가 끝나 운영자 확인이 필요한 인계 항목만 다시 예약할 수 있습니다.");
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        item.처리상태Code = 운영체제업무인계Outbox상태Codes.재시도대기;
        item.다음처리시각Utc = now;
        item.UpdatedAt = now;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<OutboxClaim?> ClaimAsync(
        long outboxId,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var leaseCutoff = now - OutboxProcessingPolicy.LeaseTimeout;

        if (db.Database.IsRelational())
        {
            var updated = await db.운영체제업무인계Outbox
                .Where(item => item.Id == outboxId
                    && (item.처리상태Code == 운영체제업무인계Outbox상태Codes.대기
                        || (item.처리상태Code == 운영체제업무인계Outbox상태Codes.재시도대기
                            && (item.다음처리시각Utc == null || item.다음처리시각Utc <= now))
                        || (item.처리상태Code == 운영체제업무인계Outbox상태Codes.처리중
                            && item.UpdatedAt <= leaseCutoff)))
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(
                            item => item.처리상태Code,
                            운영체제업무인계Outbox상태Codes.처리중)
                        .SetProperty(item => item.처리시도수, item => item.처리시도수 + 1)
                        .SetProperty(item => item.다음처리시각Utc, (DateTime?)null)
                        .SetProperty(item => item.UpdatedAt, now),
                    cancellationToken);
            if (updated == 0)
            {
                return null;
            }

            return await LoadClaimAsync(outboxId, cancellationToken);
        }

        var tracked = await db.운영체제업무인계Outbox
            .SingleOrDefaultAsync(item => item.Id == outboxId, cancellationToken);
        if (tracked is null || !CanClaim(tracked, now, leaseCutoff))
        {
            return null;
        }

        tracked.처리상태Code = 운영체제업무인계Outbox상태Codes.처리중;
        tracked.처리시도수 += 1;
        tracked.다음처리시각Utc = null;
        tracked.UpdatedAt = now;
        await db.SaveChangesAsync(cancellationToken);
        return ToClaim(tracked);
    }

    private Task<OutboxClaim> LoadClaimAsync(long outboxId, CancellationToken cancellationToken)
        => db.운영체제업무인계Outbox
            .AsNoTracking()
            .Where(item => item.Id == outboxId)
            .Select(item => new OutboxClaim(
                item.Id,
                item.멱등Key,
                item.인계StableId,
                item.이벤트Type,
                item.PayloadJson,
                item.처리시도수))
            .SingleAsync(cancellationToken);

    private async Task CompleteAsync(
        OutboxClaim claim,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (db.Database.IsRelational())
        {
            await db.운영체제업무인계Outbox
                .Where(item => item.Id == claim.Id
                    && item.처리상태Code == 운영체제업무인계Outbox상태Codes.처리중
                    && item.처리시도수 == claim.처리시도수)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(item => item.처리상태Code, 운영체제업무인계Outbox상태Codes.완료)
                        .SetProperty(item => item.다음처리시각Utc, (DateTime?)null)
                        .SetProperty(item => item.UpdatedAt, now),
                    cancellationToken);
            return;
        }

        var item = await db.운영체제업무인계Outbox.SingleAsync(
            candidate => candidate.Id == claim.Id,
            cancellationToken);
        if (item.처리상태Code != 운영체제업무인계Outbox상태Codes.처리중
            || item.처리시도수 != claim.처리시도수)
        {
            return;
        }

        item.처리상태Code = 운영체제업무인계Outbox상태Codes.완료;
        item.다음처리시각Utc = null;
        item.UpdatedAt = now;
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task FailAsync(
        OutboxClaim claim,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var retry = OutboxProcessingPolicy.CanRetry(claim.처리시도수);
        var status = retry
            ? 운영체제업무인계Outbox상태Codes.재시도대기
            : 운영체제업무인계Outbox상태Codes.실패;
        var nextAttemptAt = retry ? now + OutboxProcessingPolicy.RetryDelay : (DateTime?)null;

        if (db.Database.IsRelational())
        {
            await db.운영체제업무인계Outbox
                .Where(item => item.Id == claim.Id
                    && item.처리상태Code == 운영체제업무인계Outbox상태Codes.처리중
                    && item.처리시도수 == claim.처리시도수)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(item => item.처리상태Code, status)
                        .SetProperty(item => item.다음처리시각Utc, nextAttemptAt)
                        .SetProperty(item => item.UpdatedAt, now),
                    cancellationToken);
        }
        else
        {
            var item = await db.운영체제업무인계Outbox.SingleAsync(
                candidate => candidate.Id == claim.Id,
                cancellationToken);
            if (item.처리상태Code == 운영체제업무인계Outbox상태Codes.처리중
                && item.처리시도수 == claim.처리시도수)
            {
                item.처리상태Code = status;
                item.다음처리시각Utc = nextAttemptAt;
                item.UpdatedAt = now;
                await db.SaveChangesAsync(cancellationToken);
            }
        }

        logger.LogWarning(
            exception,
            "운영체제 업무 인계 Outbox 전달 실패. OutboxId={OutboxId}, HandoffStableId={HandoffStableId}, Attempt={Attempt}, WillRetry={WillRetry}",
            claim.Id,
            claim.인계StableId,
            claim.처리시도수,
            retry);
    }

    private static bool CanClaim(
        운영체제업무인계Outbox item,
        DateTime now,
        DateTime leaseCutoff)
        => item.처리상태Code == 운영체제업무인계Outbox상태Codes.대기
           || (item.처리상태Code == 운영체제업무인계Outbox상태Codes.재시도대기
               && (item.다음처리시각Utc == null || item.다음처리시각Utc <= now))
           || (item.처리상태Code == 운영체제업무인계Outbox상태Codes.처리중
               && item.UpdatedAt <= leaseCutoff);

    private static string RequiredString(JsonElement payload, string propertyName)
    {
        if (!payload.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(property.GetString()))
        {
            throw new InvalidOperationException($"운영체제 업무 인계 Outbox payload에 {propertyName} 값이 없습니다.");
        }

        return property.GetString()!;
    }

    private static long RequiredInt64(JsonElement payload, string propertyName)
    {
        if (!payload.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.Number
            || !property.TryGetInt64(out var value))
        {
            throw new InvalidOperationException($"운영체제 업무 인계 Outbox payload에 {propertyName} 값이 없습니다.");
        }

        return value;
    }

    private static OutboxClaim ToClaim(운영체제업무인계Outbox item)
        => new(
            item.Id,
            item.멱등Key,
            item.인계StableId,
            item.이벤트Type,
            item.PayloadJson,
            item.처리시도수);

    private sealed record OutboxClaim(
        long Id,
        string 멱등Key,
        string 인계StableId,
        string 이벤트Type,
        string PayloadJson,
        int 처리시도수);
}

public sealed class 운영체제업무인계OutboxWorker(
    IServiceScopeFactory scopeFactory,
    ISsalddelExecutionModePolicy executionMode,
    Microsoft.Extensions.Options.IOptions<운영체제업무인계OutboxOptions> options,
    ILogger<운영체제업무인계OutboxWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var settings = options.Value;
            if (executionMode.IsOperational && settings.Enabled)
            {
                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var service = scope.ServiceProvider
                        .GetRequiredService<I운영체제업무인계OutboxService>();
                    await service.대기항목처리Async(settings.BatchSize, stoppingToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    logger.LogError(exception, "운영체제 업무 인계 Outbox 처리 주기가 실패했습니다.");
                }
            }

            var intervalSeconds = Math.Clamp(settings.IntervalSeconds, 5, 3600);
            await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
        }
    }
}
