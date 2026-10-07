using System.Text.Json;
using Microsoft.Extensions.Options;
using Ssalddel.Contracts.Common.PrivacyRetention;

namespace Ssalddel.Services.PrivacyRetention;

public sealed record 개인정보파기대상(string SourceCode, string RecordId, DateTime TerminalAtUtc, string OwnerUserId, long SourceSequence = 0);
public sealed record 개인정보파기Adapter결과(bool Verified, string FailureCode, string[] VerifiedStorageCodes);

public interface I개인정보파기Adapter
{
    string SourceCode { get; }
    Task<IReadOnlyList<개인정보파기대상>> DiscoverAsync(DateTime cutoff, int limit, CancellationToken ct, long afterSequence = 0);
    Task<개인정보파기대상?> FindAsync(string recordId, CancellationToken ct);
    Task<개인정보파기Adapter결과> PurgeAsync(개인정보파기Job job, DateTime now, CancellationToken ct);
}

public interface I개인정보보존조정Service
{
    Task 보존정지Async(string sourceCode, string recordId, string caseId, string reasonCode, DateTime reviewAtUtc, CancellationToken ct = default);
    Task 보존정지해제Async(string sourceCode, string recordId, string caseId, CancellationToken ct = default);
    Task<개인정보보존정지?> 보존정지조회Async(string sourceCode, string recordId, string caseId, CancellationToken ct = default)
        => Task.FromResult<개인정보보존정지?>(null);
    Task<bool> 보존정지해제확인Async(string sourceCode, string recordId, string caseId, CancellationToken ct = default)
        => Task.FromResult(false);
    Task 보존상태적용Async(string sourceCode, string recordId, string caseId, long sequence, bool hold,
        string reasonCode, DateTime? reviewAtUtc, CancellationToken ct = default)
        => throw new NotSupportedException("RetentionIntentFenceUnsupported");
    Task<개인정보보존적용Receipt?> 보존적용조회Async(string sourceCode, string recordId, string caseId, CancellationToken ct = default)
        => Task.FromResult<개인정보보존적용Receipt?>(null);
    Task<개인정보파기결과Dto?> 결과Async(string sourceCode, string recordId, string ownerUserId, CancellationToken ct = default);
    Task<int> 실행Async(CancellationToken ct = default);
}

public interface I개인정보복원차단Service
{
    Task<bool> 복원허용Async(string sourceCode, string recordId, CancellationToken ct = default);
}

public sealed class 개인정보보존조정Service(I개인정보보존Store store, IEnumerable<I개인정보파기Adapter> adapters,
    IOptions<개인정보보존Options> options, TimeProvider clock) : I개인정보보존조정Service, I개인정보복원차단Service
{
    private readonly Dictionary<string, I개인정보파기Adapter> _adapters = adapters.ToDictionary(x => x.SourceCode, StringComparer.Ordinal);
    private 개인정보보존Options Settings => options.Value;
    public Task<bool> 복원허용Async(string sourceCode, string recordId, CancellationToken ct = default)
        => AllowedAsync(sourceCode, recordId, ct);
    private async Task<bool> AllowedAsync(string sourceCode, string recordId, CancellationToken ct)
        => !await store.HasDeletionAsync(sourceCode, recordId, ct);

    public async Task<개인정보보존정지?> 보존정지조회Async(string sourceCode, string recordId, string caseId, CancellationToken ct = default)
    {
        RequireAdapter(sourceCode); RequireKey(caseId);
        var job = await store.GetAsync(개인정보파기Job.Key(sourceCode, recordId), ct);
        return job?.Holds.SingleOrDefault(x => x.CaseId == caseId);
    }

    public async Task<bool> 보존정지해제확인Async(string sourceCode, string recordId, string caseId, CancellationToken ct = default)
    {
        RequireAdapter(sourceCode); RequireKey(caseId);
        var job = await store.GetAsync(개인정보파기Job.Key(sourceCode, recordId), ct);
        return job is not null && job.Holds.All(x => x.CaseId != caseId);
    }

    public async Task<개인정보보존적용Receipt?> 보존적용조회Async(string sourceCode, string recordId, string caseId, CancellationToken ct = default)
    {
        RequireAdapter(sourceCode); RequireKey(caseId);
        var job = await store.GetAsync(개인정보파기Job.Key(sourceCode, recordId), ct);
        return job?.HoldReceipts.SingleOrDefault(x => x.CaseId == caseId);
    }

    public async Task 보존상태적용Async(string sourceCode, string recordId, string caseId, long sequence, bool hold,
        string reasonCode, DateTime? reviewAtUtc, CancellationToken ct = default)
    {
        var adapter = RequireAdapter(sourceCode); RequireKey(caseId);
        if (sequence <= 0) throw new InvalidOperationException("RetentionIntentSequenceInvalid");
        if (hold || !string.IsNullOrEmpty(reasonCode)) RequireKey(reasonCode);
        if (hold && reviewAtUtc is null || !hold && reviewAtUtc is not null)
            throw new InvalidOperationException("RetentionIntentReviewInvalid");
        // Mongo dates have millisecond precision; fingerprint the same canonical value that is persisted.
        var review = reviewAtUtc is { } utc
            ? new DateTime(utc.Ticks - utc.Ticks % TimeSpan.TicksPerMillisecond, DateTimeKind.Utc) : (DateTime?)null;
        var fingerprint = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(new { hold, reasonCode, reviewAtUtc = review })))).ToLowerInvariant();
        var key = 개인정보파기Job.Key(sourceCode, recordId);
        if (await store.GetAsync(key, ct) is null)
        {
            var target = await adapter.FindAsync(recordId, ct) ?? throw new KeyNotFoundException("RetentionSourceNotFound");
            await store.CreateAsync(NewJob(target), ct);
        }
        for (var n = 0; n < 5; n++)
        {
            var job = await store.GetAsync(key, ct) ?? throw new InvalidOperationException("RetentionJobUnavailable");
            var receipt = job.HoldReceipts.SingleOrDefault(x => x.CaseId == caseId);
            if (receipt is not null)
            {
                if (sequence < receipt.Sequence) throw new InvalidOperationException("RetentionIntentStale");
                if (sequence == receipt.Sequence)
                {
                    if (fingerprint != receipt.Fingerprint) throw new InvalidOperationException("RetentionIntentConflict");
                    return; // Includes an already persisted hold whose review date has now passed.
                }
            }
            var now = clock.GetUtcNow().UtcDateTime;
            if (hold)
            {
                if (review <= now || review > now.AddDays(90)) throw new InvalidOperationException("LegalHoldReviewMustBeWithin90Days");
                if (job.StatusCode is 개인정보파기상태Codes.Running or 개인정보파기상태Codes.Completed
                    || await store.HasDeletionAsync(sourceCode, recordId, ct)) throw new InvalidOperationException("DeletionAlreadyStarted");
            }
            var expected = job.Revision;
            job.Holds.RemoveAll(x => x.CaseId == caseId);
            if (hold) job.Holds.Add(new(caseId, reasonCode, review!.Value));
            // The receipt survives release, so an older delayed hold cannot resurrect it.
            job.HoldReceipts.RemoveAll(x => x.CaseId == caseId);
            job.HoldReceipts.Add(new(caseId, sequence, hold, reasonCode, review, fingerprint));
            if (job.Holds.Count > 0)
            {
                job.StatusCode = 개인정보파기상태Codes.Blocked; job.FailureCode = "LegalHold";
                job.NextAttemptAtUtc = job.Holds.Min(x => x.ReviewAtUtc);
            }
            else if (job.StatusCode is not (개인정보파기상태Codes.Running or 개인정보파기상태Codes.Completed))
            {
                job.StatusCode = 개인정보파기상태Codes.Pending; job.FailureCode = string.Empty; job.NextAttemptAtUtc = now;
            }
            if (await store.ReplaceAsync(job, expected, ct)) return;
        }
        throw new InvalidOperationException("RetentionConcurrencyConflict");
    }

    public async Task 보존정지Async(string sourceCode, string recordId, string caseId, string reasonCode, DateTime reviewAtUtc, CancellationToken ct = default)
    {
        RequireKey(caseId); RequireKey(reasonCode);
        var now = clock.GetUtcNow().UtcDateTime;
        if (reviewAtUtc <= now || reviewAtUtc > now.AddDays(90)) throw new InvalidOperationException("LegalHoldReviewMustBeWithin90Days");
        var adapter = RequireAdapter(sourceCode);
        var target = await adapter.FindAsync(recordId, ct) ?? throw new KeyNotFoundException("RetentionSourceNotFound");
        var key = 개인정보파기Job.Key(sourceCode, recordId);
        await store.CreateAsync(NewJob(target), ct);
        for (var n = 0; n < 5; n++)
        {
            var job = await store.GetAsync(key, ct) ?? throw new InvalidOperationException("RetentionJobUnavailable");
            if (job.HoldReceipts.Any(x => x.CaseId == caseId)) throw new InvalidOperationException("RetentionIntentSequenceRequired");
            if (job.StatusCode is 개인정보파기상태Codes.Running or 개인정보파기상태Codes.Completed || await store.HasDeletionAsync(sourceCode, recordId, ct))
                throw new InvalidOperationException("DeletionAlreadyStarted");
            var expected = job.Revision;
            job.Holds.RemoveAll(x => x.CaseId == caseId);
            job.Holds.Add(new(caseId, reasonCode, reviewAtUtc));
            job.StatusCode = 개인정보파기상태Codes.Blocked;
            job.FailureCode = "LegalHold";
            job.NextAttemptAtUtc = reviewAtUtc;
            if (await store.ReplaceAsync(job, expected, ct)) return;
        }
        throw new InvalidOperationException("RetentionConcurrencyConflict");
    }

    public async Task 보존정지해제Async(string sourceCode, string recordId, string caseId, CancellationToken ct = default)
    {
        RequireKey(caseId); RequireAdapter(sourceCode);
        for (var n = 0; n < 5; n++)
        {
            var job = await store.GetAsync(개인정보파기Job.Key(sourceCode, recordId), ct);
            if (job is null) return;
            if (job.HoldReceipts.Any(x => x.CaseId == caseId)) throw new InvalidOperationException("RetentionIntentSequenceRequired");
            var expected = job.Revision;
            if (job.Holds.RemoveAll(x => x.CaseId == caseId) == 0) return;
            if (job.Holds.Count == 0)
            {
                job.StatusCode = 개인정보파기상태Codes.Pending; job.FailureCode = string.Empty;
                job.NextAttemptAtUtc = clock.GetUtcNow().UtcDateTime;
            }
            if (await store.ReplaceAsync(job, expected, ct)) return;
        }
        throw new InvalidOperationException("RetentionConcurrencyConflict");
    }

    public async Task<개인정보파기결과Dto?> 결과Async(string sourceCode, string recordId, string ownerUserId, CancellationToken ct = default)
    {
        var source = await RequireAdapter(sourceCode).FindAsync(recordId, ct);
        if (source is null || string.IsNullOrWhiteSpace(ownerUserId) || source.OwnerUserId != ownerUserId) return null;
        var job = await store.GetAsync(개인정보파기Job.Key(sourceCode, recordId), ct);
        return job is null ? null : new(job.Id, job.SourceCode, job.RecordId, job.StatusCode, job.CompletedAtUtc, job.FailureCode, job.VerifiedStorageCodes);
    }

    public async Task<int> 실행Async(CancellationToken ct = default)
    {
        if (!Settings.Ready) return 0;
        var now = clock.GetUtcNow().UtcDateTime;
        var cutoff = now.AddDays(-Math.Clamp(Settings.OperationalDays, 3, 30));
        foreach (var adapter in _adapters.Values)
        {
            var after = await store.ScanAfterAsync(adapter.SourceCode, ct);
            var targets = await adapter.DiscoverAsync(cutoff, Math.Clamp(Settings.BatchSize, 1, 200), ct, after);
            foreach (var target in targets)
            {
                var scheduled = NewJob(target);
                await store.CreateAsync(scheduled, ct);
                var existing = await store.GetAsync(scheduled.Id, ct);
                if (existing is { DueAtUtc: var due } && due == DateTime.MaxValue)
                {
                    var version = existing.Revision; existing.DueAtUtc = scheduled.DueAtUtc;
                    await store.ReplaceAsync(existing, version, ct);
                }
            }
            // Cycle back after the end; earlier active rows that finish later will be revisited.
            await store.AdvanceScanAsync(adapter.SourceCode, after, targets.Count == 0 ? 0 : targets.Max(x => x.SourceSequence), ct);
        }
        var completed = 0;
        foreach (var job in await store.DueAsync(now, Settings.BatchSize, ct, Math.Clamp(Settings.MaxAttempts, 1, 20)))
        {
            if (job.Holds.Count != 0)
            {
                var version = job.Revision;
                job.StatusCode = 개인정보파기상태Codes.Blocked;
                job.FailureCode = job.Holds.Any(x => x.ReviewAtUtc <= now) ? "LegalHoldReviewOverdue" : "LegalHold";
                job.NextAttemptAtUtc = now.AddDays(1);
                await store.ReplaceAsync(job, version, ct);
                continue;
            }
            if (job.Attempts >= Math.Clamp(Settings.MaxAttempts, 1, 20)) continue;
            var expected = job.Revision;
            job.StatusCode = 개인정보파기상태Codes.Running; job.LeaseToken = Guid.NewGuid().ToString("N");
            job.LeaseUntilUtc = now.AddMinutes(10); job.Attempts++;
            if (!await store.ReplaceAsync(job, expected, ct)) continue;
            try
            {
                var target = await RequireAdapter(job.SourceCode).FindAsync(job.RecordId, ct);
                if (target is null || DueAt(target) > now) throw new InvalidOperationException("TerminalEvidenceUnavailable");
                await store.MarkDeletionAsync(job, now, ct);
                var result = await RequireAdapter(job.SourceCode).PurgeAsync(job, now, ct);
                job.VerifiedStorageCodes = result.VerifiedStorageCodes;
                job.FailureCode = result.FailureCode;
                job.StatusCode = result.Verified ? 개인정보파기상태Codes.Completed : 개인정보파기상태Codes.Blocked;
                if (result.Verified)
                {
                    await store.VerifyDeletionAsync(job.Id, now, ct);
                    job.CompletedAtUtc = now; completed++;
                }
                job.NextAttemptAtUtc = now.AddMinutes(5);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                job.StatusCode = job.Attempts >= Settings.MaxAttempts ? 개인정보파기상태Codes.Blocked : 개인정보파기상태Codes.Retry;
                job.FailureCode = "PurgeFailed:" + ex.GetType().Name; // Never persist arbitrary exception messages or PII.
                job.NextAttemptAtUtc = now.AddSeconds(Math.Min(3600, 30 * Math.Pow(2, job.Attempts)));
            }
            finally { job.LeaseUntilUtc = null; job.LeaseToken = null; }
            await store.ReplaceAsync(job, job.Revision, ct);
        }
        await store.DeleteExpiredEvidenceAsync(now, ct);
        return completed;
    }

    private 개인정보파기Job NewJob(개인정보파기대상 target) => new()
    {
        Id = 개인정보파기Job.Key(target.SourceCode, target.RecordId), SourceCode = target.SourceCode, RecordId = target.RecordId,
        DueAtUtc = DueAt(target), NextAttemptAtUtc = DateTime.MinValue
    };
    private DateTime DueAt(개인정보파기대상 target)
        => target.TerminalAtUtc == DateTime.MaxValue ? DateTime.MaxValue : target.SourceCode switch
        {
            개인정보파기원천Codes.AccessAudit => target.TerminalAtUtc.AddYears(Settings.AuditYears),
            개인정보파기원천Codes.DriverLocation => target.TerminalAtUtc.AddDays(Math.Clamp(Settings.DriverLocationDays, 1, 30)),
            _ => target.TerminalAtUtc.AddDays(Math.Clamp(Settings.OperationalDays, 3, 30))
        };
    private I개인정보파기Adapter RequireAdapter(string sourceCode)
        => _adapters.TryGetValue(sourceCode, out var adapter) ? adapter : throw new InvalidOperationException("RetentionSourceUnsupported");
    private static void RequireKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 160 || value.Any(c => char.IsWhiteSpace(c) || char.IsControl(c)))
            throw new InvalidOperationException("RetentionReferenceInvalid");
    }
}

public sealed class 개인정보파기Worker(IServiceScopeFactory scopes, IOptions<개인정보보존Options> options,
    ILogger<개인정보파기Worker> logger, 살뜰.Services.Options.ISsalddelExecutionModePolicy executionMode) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (executionMode.IsOperational && options.Value.Ready)
                {
                    using var scope = scopes.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<I개인정보보존조정Service>().실행Async(stoppingToken);
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            { logger.LogError("PrivacyPurgeWorkerFailure ErrorType={ErrorType}", ex.GetType().Name); }
            await Task.Delay(TimeSpan.FromSeconds(Math.Clamp(options.Value.ScanIntervalSeconds, 30, 86400)), stoppingToken);
        }
    }
}
