using 살뜰.Services.Options;

namespace Ssalddel.Services.PrivacySupport;

/// <summary>사건의 영속 의도와 원장의 실제 hold를 대조한 뒤에만 완료 판본을 저장합니다.</summary>
public sealed class 보호지원보존Service(I보호지원Store store, I보호지원보존연결 retention, TimeProvider clock)
{
    public async Task<보호지원Record> 조정Async(string caseId, CancellationToken ct = default)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var row = await store.조회Async(caseId, ct) ?? throw new KeyNotFoundException("SupportCaseNotFound");
            if (row.RetentionIntent is not { } intent) return row;
            if (row.PurgeStateCode == "claimed" || row.AssignedAdminUserId != intent.ActorUserId
                || row.Kind != "transaction-dispute" || intent.Sequence < 1) throw new InvalidOperationException("RetentionIntentBindingMismatch");
            if (intent.Action is not ("hold-record" or "release-hold")) throw new InvalidOperationException("RetentionIntentActionInvalid");
            var hold = intent.Action == "hold-record";
            if (hold && intent.ReviewAtUtc is null) throw new InvalidOperationException("RetentionIntentReviewMissing");
            var applied = await retention.보존적용조회Async(row.CaseId, row.SourceKind, row.SourceId, ct);
            if (!Matches(applied, intent, hold))
            {
                try { await retention.보존상태적용Async(row.CaseId, row.SourceKind, row.SourceId, intent.Sequence, hold,
                    intent.LegalBasis, intent.ReviewAtUtc, ct); }
                catch (InvalidOperationException)
                {
                    // 다른 worker가 새 의도를 적용했다면 최신 사건을 읽습니다. 오래된 외부 쓰기는 fence에서 거절됩니다.
                    var latest = await store.조회Async(caseId, ct);
                    if (latest?.RetentionIntent is not { } live || live.Sequence != intent.Sequence) continue;
                    throw;
                }
            }
            applied = await retention.보존적용조회Async(row.CaseId, row.SourceKind, row.SourceId, ct);
            if (!Matches(applied, intent, hold)) throw new InvalidOperationException("RetentionReceiptUnverified");
            if (hold)
            {
                if (!SameTime(await retention.보존정지조회Async(row.CaseId, row.SourceKind, row.SourceId, ct), intent.ReviewAtUtc))
                    throw new InvalidOperationException("RetentionHoldUnverified");
            }
            else if (!await retention.보존정지해제확인Async(row.CaseId, row.SourceKind, row.SourceId, ct))
                throw new InvalidOperationException("RetentionReleaseUnverified");
            row.SourceHoldActive = hold; row.SourceHoldReviewAtUtc = hold ? intent.ReviewAtUtc : null;
            var now = clock.GetUtcNow().UtcDateTime;
            row.SourceHoldLegalBasis = intent.LegalBasis;
            row.Evidence.Add(new(intent.ActorUserId, "[" + intent.Action + ":legal-basis] " + intent.LegalBasis, now));
            row.History.Add(new(intent.Action, "assigned-admin", intent.ActorUserId, now));
            row.Receipts.Add(new(intent.RequestId, intent.ActorUserId, intent.Fingerprint));
            row.RetentionIntent = null; row.UpdatedAtUtc = now;
            var expected = row.Revision++;
            if (await store.교체Async(row, expected, ct)) return row;
            // 원장 변경 뒤 사건 CAS가 충돌해도 같은 의도의 실제 hold를 다시 읽어 이어갑니다.
        }
        throw new InvalidOperationException("RetentionCompletionConcurrencyConflict");
    }

    public async Task<int> 대기조정Async(CancellationToken ct = default)
    {
        var completed = 0;
        foreach (var row in await store.보존조정후보Async(100, ct))
        {
            try { if ((await 조정Async(row.CaseId, ct)).RetentionIntent is null) completed++; }
            catch (InvalidOperationException) { /* 불명확한 한 사건은 pending으로 남기고 다음 사건을 대조합니다. */ }
            catch (KeyNotFoundException) { /* 실제 원장 확인이 되지 않은 의도를 완료로 표시하지 않습니다. */ }
        }
        return completed;
    }

    private static bool Matches(보호지원보존적용? receipt, 보호지원보존Intent intent, bool hold) => receipt is not null
        && receipt.Sequence == intent.Sequence && receipt.Hold == hold && SameTime(receipt.ReviewAtUtc, hold ? intent.ReviewAtUtc : null);
    private static bool SameTime(DateTime? one, DateTime? two) => one.HasValue == two.HasValue
        && (!one.HasValue || new DateTimeOffset(one.Value).ToUnixTimeMilliseconds() == new DateTimeOffset(two!.Value).ToUnixTimeMilliseconds());
}

public sealed class 보호지원보존Worker(IServiceScopeFactory scopes, ISsalddelExecutionModePolicy executionMode,
    ILogger<보호지원보존Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (executionMode.IsOperational)
            {
                try
                {
                    using var scope = scopes.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<보호지원보존Service>().대기조정Async(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception ex) { logger.LogError("보존 상태 대조를 완료하지 못했습니다. 오류 종류: {ErrorType}", ex.GetType().Name); }
            }
            try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}
