using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using Ssalddel.Contracts.Common.Versioning;
using Ssalddel.WorkflowRules.Contracts;
using 살뜰.Data;

namespace Ssalddel.Services.Development.ObservableOperations;

/// <summary>
/// 네 공간형 OS의 정상·회복 여덟 사례를 단계별 Event로 전용 MySQL Outbox에 먼저 남기고
/// 같은 SnapshotStableId의 증가 revision으로 MongoDB·Redis에 전달합니다.
/// 실제 역할 API, 결제, 알림 또는 운영 DB에 효과를 만들지 않습니다.
/// </summary>
public sealed class 관찰운영검증Runner(
    IDbContextFactory<관찰운영검증DbContext> contexts,
    IServiceScopeFactory scopes,
    I관찰운영검증ProjectionWriter projectionWriter,
    I관찰운영검증RunStateWriter stateWriter,
    관찰운영검증Options options,
    TimeProvider timeProvider,
    ILogger<관찰운영검증Runner> logger) : BackgroundService
{
    public const string DefaultAreaStableId = "area:kr-seoul-jungnang-myeonmok";
    internal const string TimelineFixtureRevision = "observable-operations-lifecycle.r4";
    internal const int VerificationSchemaVersion = 2;
    internal const int ExpectedCaseCount = 8;
    internal const int ExpectedStepCount = 77;
    private readonly SemaphoreSlim gate = new(1, 1);
    private volatile bool initialized;
    private string fixtureStatusCode = 관찰운영검증Fixture상태Codes.NotConfigured;
    private string fixturePackStableId = string.Empty;
    private string fixtureHashSha256 = string.Empty;
    private string fixtureRevision = string.Empty;

    public async Task<관찰운영검증상태> StartRunAsync(
        string? requestedRunStableId,
        CancellationToken cancellationToken)
    {
        await EnsureInitializedAsync(cancellationToken);
        if (!string.Equals(fixtureStatusCode, 관찰운영검증Fixture상태Codes.Seeded, StringComparison.Ordinal))
            throw new InvalidOperationException("ObservableOperationsRestaurantFixtureRequired");
        await gate.WaitAsync(cancellationToken);
        try
        {
            await using var db = await contexts.CreateDbContextAsync(cancellationToken);
            var active = await db.Runs.AsNoTracking().AnyAsync(
                x => x.StatusCode == 관찰운영검증상태Codes.Running
                     || x.StatusCode == 관찰운영검증상태Codes.Paused,
                cancellationToken);
            if (active) throw new InvalidOperationException("ObservableOperationsRunAlreadyActive");

            var runStableId = NormalizeRunStableId(requestedRunStableId);
            if (await db.Runs.AnyAsync(x => x.RunStableId == runStableId, cancellationToken))
                throw new InvalidOperationException("ObservableOperationsRunStableIdAlreadyExists");

            var now = timeProvider.GetUtcNow().UtcDateTime;
            db.Runs.Add(CreateRunRecord(
                runStableId,
                options.DurationSeconds,
                fixturePackStableId,
                fixtureHashSha256,
                fixtureRevision,
                now));
            var definitions = Definitions(runStableId);
            db.Cases.AddRange(definitions.Cases);
            db.Steps.AddRange(definitions.Steps);
            await db.SaveChangesAsync(cancellationToken);
            return await ReadRunAsync(runStableId, now, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<관찰운영검증상태> ReadLatestAsync(CancellationToken cancellationToken)
    {
        await EnsureInitializedAsync(cancellationToken);
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        var runStableId = await db.Runs.AsNoTracking()
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => x.RunStableId)
            .FirstOrDefaultAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(runStableId)
            ? EmptyState()
            : await ReadRunAsync(runStableId, timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
    }

    public Task<관찰운영검증상태> PauseAsync(CancellationToken cancellationToken)
        => ChangeActiveRunAsync(관찰운영검증상태Codes.Paused, cancellationToken);

    public Task<관찰운영검증상태> ResumeAsync(CancellationToken cancellationToken)
        => ChangeActiveRunAsync(관찰운영검증상태Codes.Running, cancellationToken);

    public async Task<관찰운영검증상태> RetryFailedOutboxAsync(CancellationToken cancellationToken)
    {
        await EnsureInitializedAsync(cancellationToken);
        await gate.WaitAsync(cancellationToken);
        try
        {
            await using var db = await contexts.CreateDbContextAsync(cancellationToken);
            var run = await db.Runs.OrderByDescending(x => x.CreatedAtUtc)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new InvalidOperationException("ObservableOperationsRunNotFound");
            var now = timeProvider.GetUtcNow().UtcDateTime;
            var failed = await db.Outbox.Where(x =>
                    x.RunStableId == run.RunStableId
                    && x.StatusCode == 관찰운영검증상태Codes.Failed)
                .ToArrayAsync(cancellationToken);
            foreach (var item in failed)
            {
                item.StatusCode = 관찰운영검증상태Codes.Pending;
                item.NextAttemptAtUtc = now;
                item.LastErrorCode = string.Empty;
            }
            run.UpdatedAtUtc = now;
            run.Revision++;
            await db.SaveChangesAsync(cancellationToken);
            await PublishPendingAsync(run.RunStableId, now, cancellationToken);
            return await ReadRunAsync(run.RunStableId, now, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task ProcessDueAsync(DateTime utcNow, CancellationToken cancellationToken)
    {
        await EnsureInitializedAsync(cancellationToken);
        await gate.WaitAsync(cancellationToken);
        try
        {
            await using var db = await contexts.CreateDbContextAsync(cancellationToken);
            var run = await db.Runs.OrderByDescending(x => x.CreatedAtUtc)
                .FirstOrDefaultAsync(x => x.StatusCode == 관찰운영검증상태Codes.Running, cancellationToken);
            if (run is null)
            {
                var pendingRunStableId = await db.Outbox.AsNoTracking()
                    .Where(x => x.StatusCode == 관찰운영검증상태Codes.Pending
                                && x.NextAttemptAtUtc <= utcNow)
                    .OrderBy(x => x.Id)
                    .Select(x => x.RunStableId)
                    .FirstOrDefaultAsync(cancellationToken);
                if (!string.IsNullOrWhiteSpace(pendingRunStableId))
                    await PublishPendingAsync(pendingRunStableId, utcNow, cancellationToken);
                return;
            }
            var elapsed = ElapsedSeconds(run, utcNow);
            var dueSteps = await db.Steps.Where(x =>
                    x.RunStableId == run.RunStableId
                    && x.StateCode == 관찰운영검증상태Codes.Scheduled
                    && x.ScheduledOffsetMilliseconds <= elapsed * 1000)
                .OrderBy(x => x.ScheduledOffsetMilliseconds)
                .ThenBy(x => x.CaseCode)
                .ThenBy(x => x.StepSequence)
                .ToArrayAsync(cancellationToken);
            var dueCaseCodes = dueSteps.Select(x => x.CaseCode).Distinct(StringComparer.Ordinal).ToList();
            var caseRecords = await db.Cases.Where(x =>
                    x.RunStableId == run.RunStableId && dueCaseCodes.Contains(x.CaseCode))
                .ToDictionaryAsync(x => x.CaseCode, StringComparer.Ordinal, cancellationToken);
            var finalSequences = dueCaseCodes.Count == 0
                ? new Dictionary<string, int>(StringComparer.Ordinal)
                : (await db.Steps.AsNoTracking().Where(x =>
                            x.RunStableId == run.RunStableId && dueCaseCodes.Contains(x.CaseCode))
                        .Select(x => new { x.CaseCode, x.StepSequence })
                        .ToArrayAsync(cancellationToken))
                    .GroupBy(x => x.CaseCode, StringComparer.Ordinal)
                    .ToDictionary(group => group.Key, group => group.Max(x => x.StepSequence), StringComparer.Ordinal);
            foreach (var step in dueSteps)
            {
                step.StateCode = 관찰운영검증상태Codes.Published;
                step.OccurredAtUtc = utcNow;
                var item = caseRecords[step.CaseCode];
                item.LifecycleStageCode = step.LifecycleStageCode;
                item.AttentionStateCode = step.AttentionStateCode;
                item.ObjectKindCode = step.ObjectKindCode;
                item.ItemKind = step.ItemKind;
                item.RoleCode = step.RoleCode;
                item.SemanticPlaceStableId = step.SemanticPlaceStableId;
                item.RelationStableIdsJson = step.RelationStableIdsJson;
                item.StateCode = step.StepSequence == finalSequences[step.CaseCode]
                    ? 관찰운영검증상태Codes.Published
                    : 관찰운영검증상태Codes.Active;
                item.OccurredAtUtc = utcNow;
                item.Revision = step.Revision;
                var sceneItem = ToSceneItem(step, utcNow);
                db.Outbox.Add(CreateOutbox(step, sceneItem, utcNow));
            }
            if (elapsed >= run.DurationSeconds)
            {
                run.ElapsedBeforeResumeSeconds = run.DurationSeconds;
                run.ResumedAtUtc = null;
            }
            run.UpdatedAtUtc = utcNow;
            run.Revision++;
            await db.SaveChangesAsync(cancellationToken);
            await PublishPendingAsync(run.RunStableId, utcNow, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await EnsureInitializedAsync(stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(options.PollIntervalMilliseconds));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await ProcessDueAsync(timeProvider.GetUtcNow().UtcDateTime, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "관찰 운영 검증 실행기 순회 실패. 다음 주기에 재시도합니다.");
            }
        }
    }

    private async Task<관찰운영검증상태> ChangeActiveRunAsync(
        string targetStatus,
        CancellationToken cancellationToken)
    {
        await EnsureInitializedAsync(cancellationToken);
        await gate.WaitAsync(cancellationToken);
        try
        {
            await using var db = await contexts.CreateDbContextAsync(cancellationToken);
            var run = await db.Runs.OrderByDescending(x => x.CreatedAtUtc)
                .FirstOrDefaultAsync(x =>
                    x.StatusCode == 관찰운영검증상태Codes.Running
                    || x.StatusCode == 관찰운영검증상태Codes.Paused,
                    cancellationToken)
                ?? throw new InvalidOperationException("ObservableOperationsActiveRunNotFound");
            var now = timeProvider.GetUtcNow().UtcDateTime;
            if (targetStatus == 관찰운영검증상태Codes.Paused)
            {
                if (run.StatusCode != 관찰운영검증상태Codes.Running)
                    throw new InvalidOperationException("ObservableOperationsRunAlreadyPaused");
                run.ElapsedBeforeResumeSeconds = ElapsedSeconds(run, now);
                run.ResumedAtUtc = null;
            }
            else
            {
                if (run.StatusCode != 관찰운영검증상태Codes.Paused)
                    throw new InvalidOperationException("ObservableOperationsRunNotPaused");
                run.ResumedAtUtc = now;
            }
            run.StatusCode = targetStatus;
            run.UpdatedAtUtc = now;
            run.Revision++;
            await db.SaveChangesAsync(cancellationToken);
            var state = await ReadRunAsync(run.RunStableId, now, cancellationToken);
            await stateWriter.WriteAsync(state, cancellationToken);
            return state;
        }
        finally
        {
            gate.Release();
        }
    }

    internal async Task PublishPendingAsync(
        string runStableId,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        var nonTerminal = await db.Outbox.Where(x =>
                x.RunStableId == runStableId
                && (x.StatusCode == 관찰운영검증상태Codes.Pending
                    || x.StatusCode == 관찰운영검증상태Codes.Failed))
            .OrderBy(x => x.Id)
            .Take(500)
            .ToArrayAsync(cancellationToken);
        var caseQueues = nonTerminal
            .GroupBy(x => x.CaseCode, StringComparer.Ordinal)
            .OrderBy(group => group.Min(x => x.Id));
        foreach (var caseQueue in caseQueues)
        {
            foreach (var message in caseQueue
                         .OrderBy(x => ReadOutboxRevision(x.PayloadJson))
                         .ThenBy(x => x.Id))
            {
                // 같은 사례에서 앞 revision이 실패했거나 재시도 시간이 오지 않았으면 뒤 revision을 게시하지 않습니다.
                if (message.StatusCode == 관찰운영검증상태Codes.Failed
                    || message.NextAttemptAtUtc > utcNow)
                    break;
                try
                {
                    var item = JsonSerializer.Deserialize<OperationalWorldSceneItem>(message.PayloadJson);
                    if (item is null || string.IsNullOrWhiteSpace(item.SnapshotStableId))
                    {
                        // r1 volume에서 남아 있을 수 있는 완료 Event도 유실시키지 않습니다.
                        var legacy = await db.Cases.AsNoTracking().SingleAsync(x =>
                            x.RunStableId == message.RunStableId && x.CaseCode == message.CaseCode,
                            cancellationToken);
                        item = ToLegacySceneItem(legacy, message.CreatedAtUtc);
                    }
                    await projectionWriter.UpsertAsync(item, cancellationToken);
                    message.StatusCode = 관찰운영검증상태Codes.Published;
                    message.ProcessedAtUtc = utcNow;
                    message.LastErrorCode = string.Empty;
                }
                catch (InvalidOperationException exception) when (
                    !cancellationToken.IsCancellationRequested
                    && string.Equals(exception.Message,
                        "ObservableOperationsProjectionRevisionStale", StringComparison.Ordinal))
                {
                    // Mongo에 이미 더 높은 revision이 있으면 과거 Outbox를 실패로 영구 잔존시키지 않습니다.
                    message.StatusCode = 관찰운영검증상태Codes.Superseded;
                    message.AttemptCount++;
                    message.ProcessedAtUtc = utcNow;
                    message.LastErrorCode = "ObservableOperationsProjectionRevisionStale";
                }
                catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
                {
                    message.StatusCode = 관찰운영검증상태Codes.Failed;
                    message.AttemptCount++;
                    message.NextAttemptAtUtc = utcNow.AddSeconds(5);
                    message.LastErrorCode = exception.GetType().Name;
                    break;
                }
            }
        }
        await db.SaveChangesAsync(cancellationToken);
        await TryCompleteRunAsync(runStableId, utcNow, cancellationToken);
        var state = await ReadRunAsync(runStableId, utcNow, cancellationToken);
        try
        {
            await stateWriter.WriteAsync(state, cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "관찰 운영 Redis 단기 상태 쓰기 실패. MySQL 원장과 Outbox는 유지됩니다.");
        }
    }

    internal async Task<bool> TryCompleteRunAsync(
        string runStableId,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        var run = await db.Runs.SingleOrDefaultAsync(x => x.RunStableId == runStableId, cancellationToken);
        if (run is null) return false;
        if (run.StatusCode == 관찰운영검증상태Codes.Completed) return true;
        if (run.StatusCode != 관찰운영검증상태Codes.Running
            && run.StatusCode != 관찰운영검증상태Codes.Paused)
            return false;
        if (ElapsedSeconds(run, utcNow) < run.DurationSeconds) return false;

        var cases = await db.Cases.AsNoTracking().Where(x => x.RunStableId == runStableId)
            .ToArrayAsync(cancellationToken);
        var steps = await db.Steps.AsNoTracking().Where(x => x.RunStableId == runStableId)
            .ToArrayAsync(cancellationToken);
        var outbox = await db.Outbox.AsNoTracking().Where(x => x.RunStableId == runStableId)
            .Select(x => new { x.CaseCode, x.StatusCode, x.PayloadJson })
            .ToArrayAsync(cancellationToken);
        if (cases.Length != ExpectedCaseCount
            || steps.Length != ExpectedStepCount
            || outbox.Length != ExpectedStepCount
            || cases.Any(x => x.StateCode != 관찰운영검증상태Codes.Published)
            || steps.Any(x => x.StateCode != 관찰운영검증상태Codes.Published)
            || outbox.Any(item =>
                item.StatusCode != 관찰운영검증상태Codes.Published
                && item.StatusCode != 관찰운영검증상태Codes.Superseded))
            return false;

        var expectedStepKeys = steps
            .Select(item => (item.CaseCode, item.Revision))
            .ToHashSet();
        var outboxWithRevision = outbox
            .Select(item => new
            {
                item.CaseCode,
                item.StatusCode,
                Revision = ReadOutboxRevision(item.PayloadJson)
            })
            .ToArray();
        var actualOutboxKeys = outboxWithRevision
            .Select(item => (item.CaseCode, item.Revision))
            .ToHashSet();
        if (actualOutboxKeys.Count != ExpectedStepCount
            || !actualOutboxKeys.SetEquals(expectedStepKeys))
            return false;

        var finalSteps = steps.GroupBy(x => x.CaseCode, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(x => x.Revision).First(),
                StringComparer.Ordinal);
        if (finalSteps.Count != ExpectedCaseCount
            || cases.Any(item =>
                !finalSteps.TryGetValue(item.CaseCode, out var final)
                || final.Revision != item.Revision
                || !string.Equals(final.OperatingSystemId, item.OperatingSystemId, StringComparison.Ordinal)
                || !string.Equals(final.WorkStableId, item.WorkStableId, StringComparison.Ordinal)
                || !string.Equals(final.LifecycleStageCode, item.LifecycleStageCode, StringComparison.Ordinal)
                || !string.Equals(final.AttentionStateCode, item.AttentionStateCode, StringComparison.Ordinal)
                || !string.Equals(final.ObjectKindCode, item.ObjectKindCode, StringComparison.Ordinal)
                || !string.Equals(final.ItemKind, item.ItemKind, StringComparison.Ordinal)
                || !string.Equals(final.RoleCode, item.RoleCode, StringComparison.Ordinal)
                || !string.Equals(final.SemanticPlaceStableId, item.SemanticPlaceStableId, StringComparison.Ordinal)
                || !string.Equals(final.RelationStableIdsJson, item.RelationStableIdsJson, StringComparison.Ordinal))
            || outboxWithRevision.Any(item =>
                !finalSteps.TryGetValue(item.CaseCode, out var final)
                || (item.Revision == final.Revision
                    && item.StatusCode != 관찰운영검증상태Codes.Published)
                || (item.StatusCode == 관찰운영검증상태Codes.Superseded
                    && item.Revision >= final.Revision)))
            return false;

        run.StatusCode = 관찰운영검증상태Codes.Completed;
        run.ElapsedBeforeResumeSeconds = run.DurationSeconds;
        run.ResumedAtUtc = null;
        run.UpdatedAtUtc = utcNow;
        run.Revision++;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static long ReadOutboxRevision(string payloadJson)
    {
        try
        {
            return JsonSerializer.Deserialize<OperationalWorldSceneItem>(payloadJson)?.Revision ?? 0;
        }
        catch (JsonException)
        {
            return 0;
        }
    }

    internal async Task<관찰운영검증상태> ReadRunAsync(
        string runStableId,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        var run = await db.Runs.AsNoTracking().SingleAsync(x => x.RunStableId == runStableId, cancellationToken);
        var caseRows = await db.Cases.AsNoTracking().Where(x => x.RunStableId == runStableId)
            .OrderBy(x => x.ScheduledOffsetSeconds).ThenBy(x => x.CaseCode)
            .ToArrayAsync(cancellationToken);
        var stepRows = await db.Steps.AsNoTracking().Where(x => x.RunStableId == runStableId)
            .Select(x => new { x.CaseCode, x.StepSequence, x.StateCode })
            .ToArrayAsync(cancellationToken);
        var stepCounts = stepRows.GroupBy(x => x.CaseCode, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => new
                {
                    Total = group.Count(),
                    Current = group.Where(x => x.StateCode == 관찰운영검증상태Codes.Published)
                        .Select(x => x.StepSequence).DefaultIfEmpty(0).Max()
                },
                StringComparer.Ordinal);
        var cases = caseRows.Select(x =>
        {
            stepCounts.TryGetValue(x.CaseCode, out var count);
            return new 관찰운영검증사례상태(
                x.CaseCode,
                x.OperatingSystemId,
                x.WorkStableId,
                x.LifecycleStageCode,
                x.AttentionStateCode,
                x.StateCode,
                x.ScheduledOffsetSeconds,
                x.Revision,
                count?.Current ?? 0,
                count?.Total ?? 0);
        }).ToArray();
        var pending = await db.Outbox.AsNoTracking().CountAsync(x =>
            x.RunStableId == runStableId && x.StatusCode == 관찰운영검증상태Codes.Pending,
            cancellationToken);
        var failed = await db.Outbox.AsNoTracking().CountAsync(x =>
            x.RunStableId == runStableId && x.StatusCode == 관찰운영검증상태Codes.Failed,
            cancellationToken);
        var lineageValueCount = new[]
        {
            run.FixturePackStableId,
            run.FixtureHashSha256,
            run.FixtureRevision
        }.Count(value => !string.IsNullOrWhiteSpace(value));
        var boundPack = lineageValueCount == 3
            ? await db.FixturePacks.AsNoTracking().SingleOrDefaultAsync(
                item => item.FixturePackStableId == run.FixturePackStableId,
                cancellationToken)
            : null;
        var runFixtureStatusCode = FixtureLineageStatus(run, boundPack, utcNow);
        return new 관찰운영검증상태(
            run.RunStableId,
            run.StatusCode,
            run.AreaStableId,
            ElapsedSeconds(run, utcNow),
            run.DurationSeconds,
            cases.Count(x => x.StateCode == 관찰운영검증상태Codes.Published),
            stepRows.Count(x => x.StateCode == 관찰운영검증상태Codes.Published),
            stepRows.Length,
            pending,
            failed,
            runFixtureStatusCode,
            run.FixturePackStableId,
            run.FixtureHashSha256,
            run.FixtureRevision,
            cases);
    }

    internal static string FixtureLineageStatus(
        관찰운영검증실행Record run,
        관찰운영검증Fixture묶음Record? pack,
        DateTime utcNow)
    {
        var lineageValues = new[]
        {
            run.FixturePackStableId,
            run.FixtureHashSha256,
            run.FixtureRevision
        };
        var valueCount = lineageValues.Count(value => !string.IsNullOrWhiteSpace(value));
        if (valueCount == 0) return 관찰운영검증Fixture상태Codes.UnboundLegacyRun;
        if (valueCount != lineageValues.Length
            || pack is null
            || run.FixtureHashSha256.Length != 64
            || pack.InputHashSha256.Length != 64)
            return 관찰운영검증Fixture상태Codes.InvalidRunFixtureLineage;

        var expectedPackStableId = "fixture-pack:sagajeong-restaurants:" + run.FixtureHashSha256[..16];
        var lineageIsSafe = string.Equals(run.FixturePackStableId, expectedPackStableId, StringComparison.Ordinal)
            && string.Equals(pack.FixturePackStableId, run.FixturePackStableId, StringComparison.Ordinal)
            && string.Equals(pack.FixtureHashSha256, run.FixtureHashSha256, StringComparison.Ordinal)
            && string.Equals(pack.FixtureRevision, run.FixtureRevision, StringComparison.Ordinal)
            && string.Equals(pack.FixtureRevision, 관찰운영검증FixtureFactory.FixtureRevision, StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(pack.SourceRevision)
            && string.Equals(pack.DeterministicSeed, 관찰운영검증FixtureFactory.DeterministicSeed, StringComparison.Ordinal)
            && string.Equals(pack.SourceKindCode, 관찰운영검증FixtureFactory.SourceKindCode, StringComparison.Ordinal)
            && string.Equals(pack.EnvironmentCode, 관찰운영검증FixtureFactory.EnvironmentCode, StringComparison.Ordinal)
            && string.Equals(pack.GeneratorVersion, 관찰운영검증FixtureFactory.GeneratorVersion, StringComparison.Ordinal)
            && !pack.DistributionApproved
            && !pack.OperationalEffectsAllowed
            && pack.CreatedAtUtc <= utcNow
            && pack.ExpiresAtUtc == pack.CreatedAtUtc.AddDays(30);
        if (!lineageIsSafe)
            return 관찰운영검증Fixture상태Codes.InvalidRunFixtureLineage;
        return pack.ExpiresAtUtc <= utcNow
            ? 관찰운영검증Fixture상태Codes.ExpiredRunFixture
            : 관찰운영검증Fixture상태Codes.Seeded;
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (initialized) return;
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (initialized) return;
            // 전용 빈 DB에서도 인증/미들웨어가 필요한 최소 호스트가 실행되도록 기존 schema를 먼저 준비합니다.
            await using (var scope = scopes.CreateAsyncScope())
            {
                var applicationDb = scope.ServiceProvider.GetRequiredService<SsalddelContext>();
                await applicationDb.Database.MigrateAsync(cancellationToken);
            }
            await using var db = await contexts.CreateDbContextAsync(cancellationToken);
            await EnsureVerificationSchemaAsync(db, cancellationToken);
            await EnsureRestaurantFixtureAsync(db, cancellationToken);
            initialized = true;
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task EnsureRestaurantFixtureAsync(
        관찰운영검증DbContext verificationDb,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.RestaurantDirectoryPath))
        {
            fixtureStatusCode = 관찰운영검증Fixture상태Codes.NotConfigured;
            return;
        }

        var seed = await 관찰운영검증FixtureFactory.ReadAsync(
            options.RestaurantDirectoryPath,
            timeProvider.GetUtcNow().UtcDateTime,
            cancellationToken);
        if (seed is null)
        {
            fixtureStatusCode = 관찰운영검증Fixture상태Codes.InputNotFound;
            logger.LogWarning(
                "사가정 음식점 directory 입력을 찾지 못해 음식점 Fixture를 합성하지 않습니다. Path={Path}",
                options.RestaurantDirectoryPath);
            return;
        }
        if (!string.Equals(seed.Pack.InputHashSha256, options.RestaurantDirectorySha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("ObservableOperationsRestaurantFixtureHashMismatch");
        if (!string.Equals(seed.Pack.SourceRevision, options.RestaurantDirectoryRevision, StringComparison.Ordinal))
            throw new InvalidOperationException("ObservableOperationsRestaurantFixtureRevisionMismatch");

        await using var scope = scopes.CreateAsyncScope();
        var applicationDb = scope.ServiceProvider.GetRequiredService<SsalddelContext>();
        await 관찰운영검증FixtureFactory.PersistAsync(
            seed,
            applicationDb,
            verificationDb,
            timeProvider.GetUtcNow().UtcDateTime,
            cancellationToken);
        fixtureStatusCode = 관찰운영검증Fixture상태Codes.Seeded;
        fixturePackStableId = seed.Pack.FixturePackStableId;
        fixtureHashSha256 = seed.Pack.FixtureHashSha256;
        fixtureRevision = seed.Pack.FixtureRevision;
    }

    private static async Task EnsureVerificationSchemaAsync(
        관찰운영검증DbContext db,
        CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS `관찰운영검증_Schema` (
              `SingletonId` tinyint NOT NULL,
              `SchemaVersion` int NOT NULL,
              `UpdatedAtUtc` datetime(6) NOT NULL,
              PRIMARY KEY (`SingletonId`)
            ) CHARACTER SET=utf8mb4;
            """, cancellationToken);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS `관찰운영검증_실행` (
              `RunStableId` varchar(96) NOT NULL,
              `StatusCode` varchar(24) NOT NULL,
              `AreaStableId` varchar(128) NOT NULL,
              `FixturePackStableId` varchar(128) NOT NULL DEFAULT '',
              `FixtureHashSha256` varchar(64) NOT NULL DEFAULT '',
              `FixtureRevision` varchar(48) NOT NULL DEFAULT '',
              `DurationSeconds` int NOT NULL,
              `ElapsedBeforeResumeSeconds` int NOT NULL,
              `ResumedAtUtc` datetime(6) NULL,
              `CreatedAtUtc` datetime(6) NOT NULL,
              `UpdatedAtUtc` datetime(6) NOT NULL,
              `Revision` bigint NOT NULL,
              PRIMARY KEY (`RunStableId`),
              KEY `IX_관찰운영검증_실행_CreatedAtUtc` (`CreatedAtUtc`)
            ) CHARACTER SET=utf8mb4;
            """, cancellationToken);
        await EnsureRunFixtureLineageColumnAsync(db, """
            ALTER TABLE `관찰운영검증_실행`
            ADD COLUMN `FixturePackStableId` varchar(128) NOT NULL DEFAULT '';
            """, cancellationToken);
        await EnsureRunFixtureLineageColumnAsync(db, """
            ALTER TABLE `관찰운영검증_실행`
            ADD COLUMN `FixtureHashSha256` varchar(64) NOT NULL DEFAULT '';
            """, cancellationToken);
        await EnsureRunFixtureLineageColumnAsync(db, """
            ALTER TABLE `관찰운영검증_실행`
            ADD COLUMN `FixtureRevision` varchar(48) NOT NULL DEFAULT '';
            """, cancellationToken);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS `관찰운영검증_사례` (
              `RunStableId` varchar(96) NOT NULL,
              `CaseCode` varchar(64) NOT NULL,
              `OperatingSystemId` varchar(64) NOT NULL,
              `WorkStableId` varchar(196) NOT NULL,
              `LifecycleStageCode` varchar(96) NOT NULL,
              `AttentionStateCode` varchar(32) NOT NULL,
              `ObjectKindCode` varchar(64) NOT NULL,
              `ItemKind` varchar(64) NOT NULL,
              `RoleCode` varchar(64) NOT NULL,
              `SemanticPlaceStableId` varchar(160) NOT NULL,
              `RelationStableIdsJson` json NOT NULL,
              `StateCode` varchar(24) NOT NULL,
              `ScheduledOffsetSeconds` int NOT NULL,
              `Revision` bigint NOT NULL,
              `OccurredAtUtc` datetime(6) NULL,
              PRIMARY KEY (`RunStableId`, `CaseCode`),
              KEY `IX_관찰운영검증_사례_Run_Offset` (`RunStableId`, `ScheduledOffsetSeconds`)
            ) CHARACTER SET=utf8mb4;
            """, cancellationToken);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS `관찰운영검증_Outbox` (
              `Id` bigint NOT NULL AUTO_INCREMENT,
              `EventStableId` varchar(220) NOT NULL,
              `RunStableId` varchar(96) NOT NULL,
              `CaseCode` varchar(64) NOT NULL,
              `EventTypeCode` varchar(64) NOT NULL,
              `PayloadJson` json NOT NULL,
              `StatusCode` varchar(24) NOT NULL,
              `AttemptCount` int NOT NULL,
              `CreatedAtUtc` datetime(6) NOT NULL,
              `NextAttemptAtUtc` datetime(6) NOT NULL,
              `ProcessedAtUtc` datetime(6) NULL,
              `LastErrorCode` varchar(96) NOT NULL,
              PRIMARY KEY (`Id`),
              UNIQUE KEY `IX_관찰운영검증_Outbox_EventStableId` (`EventStableId`),
              KEY `IX_관찰운영검증_Outbox_Status_Next` (`StatusCode`, `NextAttemptAtUtc`)
            ) CHARACTER SET=utf8mb4;
            """, cancellationToken);
        // r1 volume의 기존 세 표를 보존한 채 r2 단계/Fixture 표만 추가하는 멱등 업그레이드입니다.
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS `관찰운영검증_단계` (
              `RunStableId` varchar(96) NOT NULL,
              `CaseCode` varchar(64) NOT NULL,
              `StepSequence` int NOT NULL,
              `OperatingSystemId` varchar(64) NOT NULL,
              `WorkStableId` varchar(196) NOT NULL,
              `LifecycleStageCode` varchar(96) NOT NULL,
              `AttentionStateCode` varchar(32) NOT NULL,
              `ObjectKindCode` varchar(64) NOT NULL,
              `ItemKind` varchar(64) NOT NULL,
              `RoleCode` varchar(64) NOT NULL,
              `SemanticPlaceStableId` varchar(160) NOT NULL,
              `RelationStableIdsJson` json NOT NULL,
              `ExpectedStateCode` varchar(96) NOT NULL,
              `FailureOrRecoveryCode` varchar(96) NOT NULL,
              `ReturnStateCode` varchar(96) NOT NULL,
              `SourceKindCode` varchar(48) NOT NULL,
              `FixtureRevision` varchar(48) NOT NULL,
              `StepHashSha256` varchar(64) NOT NULL,
              `StateCode` varchar(24) NOT NULL,
              `ScheduledOffsetMilliseconds` int NOT NULL,
              `Revision` bigint NOT NULL,
              `OccurredAtUtc` datetime(6) NULL,
              PRIMARY KEY (`RunStableId`, `CaseCode`, `StepSequence`),
              UNIQUE KEY `IX_관찰운영검증_단계_Run_Case_Revision` (`RunStableId`, `CaseCode`, `Revision`),
              KEY `IX_관찰운영검증_단계_Run_Offset` (`RunStableId`, `ScheduledOffsetMilliseconds`)
            ) CHARACTER SET=utf8mb4;
            """, cancellationToken);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS `관찰운영검증_Fixture묶음` (
              `FixturePackStableId` varchar(128) NOT NULL,
              `FixtureRevision` varchar(48) NOT NULL,
              `FixtureHashSha256` varchar(64) NOT NULL,
              `InputHashSha256` varchar(64) NOT NULL,
              `SourceRevision` varchar(96) NOT NULL,
              `DeterministicSeed` varchar(128) NOT NULL,
              `SourceKindCode` varchar(48) NOT NULL,
              `EnvironmentCode` varchar(48) NOT NULL,
              `DistributionApproved` tinyint(1) NOT NULL,
              `OperationalEffectsAllowed` tinyint(1) NOT NULL,
              `GeneratorVersion` varchar(48) NOT NULL,
              `CreatedAtUtc` datetime(6) NOT NULL,
              `ExpiresAtUtc` datetime(6) NOT NULL,
              PRIMARY KEY (`FixturePackStableId`)
            ) CHARACTER SET=utf8mb4;
            """, cancellationToken);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS `관찰운영검증_Fixture결속` (
              `FixturePackStableId` varchar(128) NOT NULL,
              `FixtureObjectStableId` varchar(196) NOT NULL,
              `ObjectKindCode` varchar(64) NOT NULL,
              `RestaurantProfileId` bigint NOT NULL,
              `MenuId` bigint NULL,
              `PublicBusinessObservationStableId` varchar(196) NOT NULL,
              `LocationAnchorStableId` varchar(196) NOT NULL,
              `BuildingStableId` varchar(160) NOT NULL,
              `SemanticPlaceStableId` varchar(160) NOT NULL,
              `BindingPurposeCode` varchar(64) NOT NULL,
              `AffiliationCode` varchar(64) NOT NULL,
              `DisplayDisclosureCode` varchar(96) NOT NULL,
              `ScenarioOrderAllowed` tinyint(1) NOT NULL,
              `ActualOrderAllowed` tinyint(1) NOT NULL,
              `DistributionApproved` tinyint(1) NOT NULL,
              `ReviewStatusCode` varchar(48) NOT NULL,
              `Revision` bigint NOT NULL,
              `BindingHashSha256` varchar(64) NOT NULL,
              PRIMARY KEY (`FixturePackStableId`, `FixtureObjectStableId`),
              KEY `IX_관찰운영검증_Fixture결속_RestaurantProfileId` (`RestaurantProfileId`),
              KEY `IX_관찰운영검증_Fixture결속_MenuId` (`MenuId`)
            ) CHARACTER SET=utf8mb4;
            """, cancellationToken);
        await db.Database.ExecuteSqlRawAsync($"""
            INSERT INTO `관찰운영검증_Schema` (`SingletonId`, `SchemaVersion`, `UpdatedAtUtc`)
            VALUES (1, {VerificationSchemaVersion}, UTC_TIMESTAMP(6))
            ON DUPLICATE KEY UPDATE
              `SchemaVersion` = GREATEST(`SchemaVersion`, VALUES(`SchemaVersion`)),
              `UpdatedAtUtc` = VALUES(`UpdatedAtUtc`);
            """, cancellationToken);
    }

    private static async Task EnsureRunFixtureLineageColumnAsync(
        관찰운영검증DbContext db,
        string sql,
        CancellationToken cancellationToken)
    {
        try
        {
            await db.Database.ExecuteSqlRawAsync(sql, cancellationToken);
        }
        catch (MySqlException exception) when (exception.Number == 1060)
        {
            // 기존/신규 v2 volume 모두 같은 초기화 경로를 사용하므로 이미 있는 열은 그대로 보존합니다.
        }
    }

    private static int ElapsedSeconds(관찰운영검증실행Record run, DateTime utcNow)
    {
        var elapsed = run.ElapsedBeforeResumeSeconds;
        if (run.StatusCode == 관찰운영검증상태Codes.Running && run.ResumedAtUtc.HasValue)
            elapsed += Math.Max(0, (int)(utcNow - run.ResumedAtUtc.Value).TotalSeconds);
        return Math.Min(run.DurationSeconds, elapsed);
    }

    internal static 관찰운영검증실행Record CreateRunRecord(
        string runStableId,
        int durationSeconds,
        string fixturePackStableId,
        string fixtureHashSha256,
        string fixtureRevision,
        DateTime utcNow)
        => new()
        {
            RunStableId = runStableId,
            StatusCode = 관찰운영검증상태Codes.Running,
            AreaStableId = DefaultAreaStableId,
            FixturePackStableId = fixturePackStableId,
            FixtureHashSha256 = fixtureHashSha256,
            FixtureRevision = fixtureRevision,
            DurationSeconds = durationSeconds,
            ResumedAtUtc = utcNow,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow,
            Revision = 1
        };

    private static string NormalizeRunStableId(string? requested)
    {
        if (string.IsNullOrWhiteSpace(requested))
            return "observable-operations-run:" + Guid.NewGuid().ToString("N");
        var value = requested.Trim();
        if (value.Length is < 8 or > 96
            || value.Any(character => !(char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or ':' or '.')))
            throw new ArgumentException("ObservableOperationsRunStableIdInvalid", nameof(requested));
        return value;
    }

    internal static 관찰운영검증TimelineDefinitions Definitions(string runStableId)
    {
        var cases = new List<관찰운영검증사례Record>();
        var steps = new List<관찰운영검증단계Record>();

        void AddCase(
            string code,
            string operatingSystemId,
            string objectKindCode,
            string itemKind,
            string roleCode,
            string[] relationStableIds,
            params 관찰운영검증StepDefinition[] definitions)
        {
            if (definitions.Length == 0) throw new InvalidOperationException("ObservableOperationsTimelineStepsRequired");
            var workStableId = $"sample-work:{runStableId}:{code}";
            foreach (var pair in definitions.Select((definition, index) => (definition, sequence: index + 1)))
            {
                var definition = pair.definition;
                var hash = StepHash(
                    code,
                    pair.sequence,
                    operatingSystemId,
                    objectKindCode,
                    itemKind,
                    roleCode,
                    OperationalWorldSceneSourceKinds.VerificationSample,
                    definition,
                    relationStableIds);
                steps.Add(new 관찰운영검증단계Record
                {
                    RunStableId = runStableId,
                    CaseCode = code,
                    StepSequence = pair.sequence,
                    OperatingSystemId = operatingSystemId,
                    WorkStableId = workStableId,
                    LifecycleStageCode = definition.LifecycleStageCode,
                    AttentionStateCode = definition.AttentionStateCode,
                    ObjectKindCode = objectKindCode,
                    ItemKind = itemKind,
                    RoleCode = roleCode,
                    SemanticPlaceStableId = "synthetic-place:" + definition.PlaceCode,
                    RelationStableIdsJson = JsonSerializer.Serialize(relationStableIds),
                    ExpectedStateCode = definition.ExpectedStateCode,
                    FailureOrRecoveryCode = definition.FailureOrRecoveryCode,
                    ReturnStateCode = definition.ReturnStateCode,
                    SourceKindCode = OperationalWorldSceneSourceKinds.VerificationSample,
                    FixtureRevision = TimelineFixtureRevision,
                    StepHashSha256 = hash,
                    ScheduledOffsetMilliseconds = definition.OffsetSeconds * 1000,
                    Revision = pair.sequence
                });
            }

            var first = steps.First(item => item.CaseCode == code);
            var final = steps.Last(item => item.CaseCode == code);
            cases.Add(new 관찰운영검증사례Record
            {
                RunStableId = runStableId,
                CaseCode = code,
                OperatingSystemId = operatingSystemId,
                WorkStableId = workStableId,
                LifecycleStageCode = first.LifecycleStageCode,
                AttentionStateCode = first.AttentionStateCode,
                ObjectKindCode = objectKindCode,
                ItemKind = itemKind,
                RoleCode = roleCode,
                SemanticPlaceStableId = first.SemanticPlaceStableId,
                RelationStableIdsJson = first.RelationStableIdsJson,
                ScheduledOffsetSeconds = final.ScheduledOffsetMilliseconds / 1000
            });
        }

        static 관찰운영검증StepDefinition Step(
            int offset,
            string lifecycle,
            string expected,
            string place,
            string attention = OperationalWorldAttentionStateCodes.Active,
            string failureOrRecovery = "",
            string returnState = "")
            => new(offset, lifecycle, expected, attention, place, failureOrRecovery, returnState);

        AddCase("food-normal", OperationalWorldOperatingSystemIds.FoodDelivery,
            "FoodDelivery", OperationalWorldSceneItemKinds.CompletedLifecycle, "FoodDeliveryTeam",
            ["synthetic-role:customer", "synthetic-role:restaurant", "synthetic-role:food-driver"],
            Step(5, OperatingSystemLifecycleStageIds.FoodOrder, "OrderRequested", "food-customer-a"),
            Step(15, OperatingSystemLifecycleStageIds.FoodRestaurantResponse, "RestaurantAccepted", "food-restaurant-a"),
            Step(25, OperatingSystemLifecycleStageIds.FoodCooking, "Cooking", "food-restaurant-a"),
            Step(35, OperatingSystemLifecycleStageIds.FoodCooking, "ReadyForPickup", "food-restaurant-a"),
            Step(45, OperatingSystemLifecycleStageIds.FoodDispatch, "DriverAssigned", "food-driver-wait-a"),
            Step(55, OperatingSystemLifecycleStageIds.FoodPickup, "PickedUp", "food-restaurant-a"),
            Step(65, OperatingSystemLifecycleStageIds.FoodDelivery, "Delivered", "food-customer-a"),
            Step(75, OperatingSystemLifecycleStageIds.FoodDelivery, "ReceiptConfirmed", "food-customer-a"),
            Step(85, OperatingSystemLifecycleStageIds.FoodDelivery, "DriverReturned", "food-driver-wait-a",
                OperationalWorldAttentionStateCodes.Completed, returnState: "DriverAvailable"));

        AddCase("food-recovery", OperationalWorldOperatingSystemIds.FoodDelivery,
            "FoodDeliveryRecovery", OperationalWorldSceneItemKinds.CompletedLifecycle, "FoodDeliveryRecoveryTeam",
            ["synthetic-event:pickup-incident", "synthetic-action:recook", "synthetic-action:redispatch"],
            Step(20, OperatingSystemLifecycleStageIds.FoodOrder, "OrderRequested", "food-customer-b"),
            Step(30, OperatingSystemLifecycleStageIds.FoodRestaurantResponse, "RestaurantAccepted", "food-restaurant-b"),
            Step(40, OperatingSystemLifecycleStageIds.FoodCooking, "Cooking", "food-restaurant-b"),
            Step(50, OperatingSystemLifecycleStageIds.FoodCooking, "ReadyForPickup", "food-restaurant-b"),
            Step(60, OperatingSystemLifecycleStageIds.FoodDispatch, "DriverAssigned", "food-driver-wait-b"),
            Step(70, OperatingSystemLifecycleStageIds.FoodPickup, "PickedUp", "food-restaurant-b"),
            Step(80, OperatingSystemLifecycleStageIds.FoodInterruptionRecovery, "DriverInterrupted", "food-route-b",
                OperationalWorldAttentionStateCodes.RecoveryPending, "PickupIncident"),
            Step(95, OperatingSystemLifecycleStageIds.FoodCooking, "Recooking", "food-restaurant-b",
                OperationalWorldAttentionStateCodes.Recovered, "RecookStarted"),
            Step(110, OperatingSystemLifecycleStageIds.FoodDispatch, "Redispatched", "food-driver-wait-b",
                OperationalWorldAttentionStateCodes.Recovered, "DriverRedispatched"),
            Step(125, OperatingSystemLifecycleStageIds.FoodPickup, "RepickedUp", "food-restaurant-b",
                OperationalWorldAttentionStateCodes.Recovered),
            Step(140, OperatingSystemLifecycleStageIds.FoodDelivery, "Delivered", "food-customer-b",
                OperationalWorldAttentionStateCodes.Recovered),
            Step(150, OperatingSystemLifecycleStageIds.FoodDelivery, "ReceiptConfirmed", "food-customer-b",
                OperationalWorldAttentionStateCodes.Recovered),
            Step(160, OperatingSystemLifecycleStageIds.FoodInterruptionRecovery, "DriverReturned", "food-driver-wait-b",
                OperationalWorldAttentionStateCodes.Recovered, returnState: "DriverAvailable"));

        AddCase("cargo-normal", OperationalWorldOperatingSystemIds.DomesticCargoTransport,
            "CargoTransport", OperationalWorldSceneItemKinds.CargoHandoff, "CargoDeliveryTeam",
            ["synthetic-role:shipper", "synthetic-role:cargo-driver", "synthetic-role:consignee"],
            Step(110, OperatingSystemLifecycleStageIds.CargoRequest, "TransportRequested", "cargo-origin-a"),
            Step(125, OperatingSystemLifecycleStageIds.CargoTermsAgreement, "TermsAgreed", "cargo-origin-a"),
            Step(140, OperatingSystemLifecycleStageIds.CargoDispatch, "DriverAssigned", "cargo-driver-wait-a"),
            Step(155, OperatingSystemLifecycleStageIds.CargoPickup, "Loaded", "cargo-origin-a"),
            Step(170, OperatingSystemLifecycleStageIds.CargoTransport, "InTransit", "cargo-route-a"),
            Step(185, OperatingSystemLifecycleStageIds.CargoDropoff, "Unloaded", "cargo-destination-a"),
            Step(200, OperatingSystemLifecycleStageIds.CargoDropoff, "ConsigneeAccepted", "cargo-destination-a"),
            Step(215, OperatingSystemLifecycleStageIds.CargoEvidenceSettlement, "ProofReturned", "cargo-driver-wait-a",
                OperationalWorldAttentionStateCodes.Completed, returnState: "DriverAvailable"));

        AddCase("cargo-recovery", OperationalWorldOperatingSystemIds.DomesticCargoTransport,
            "CargoTransportRecovery", OperationalWorldSceneItemKinds.CargoHandoff, "CargoRecoveryTeam",
            ["synthetic-event:transport-interruption", "synthetic-action:cargo-protection", "synthetic-action:replan"],
            Step(150, OperatingSystemLifecycleStageIds.CargoRequest, "TransportRequested", "cargo-origin-b"),
            Step(165, OperatingSystemLifecycleStageIds.CargoTermsAgreement, "TermsAgreed", "cargo-origin-b"),
            Step(180, OperatingSystemLifecycleStageIds.CargoDispatch, "DriverAssigned", "cargo-driver-wait-b"),
            Step(195, OperatingSystemLifecycleStageIds.CargoPickup, "Loaded", "cargo-origin-b"),
            Step(210, OperatingSystemLifecycleStageIds.CargoTransport, "InTransit", "cargo-route-b"),
            Step(225, OperatingSystemLifecycleStageIds.CargoInterruptionRecovery, "TransportInterrupted", "cargo-route-b",
                OperationalWorldAttentionStateCodes.RecoveryPending, "TransportInterrupted"),
            Step(240, OperatingSystemLifecycleStageIds.CargoInterruptionRecovery, "CargoProtected", "cargo-safe-stop-b",
                OperationalWorldAttentionStateCodes.RecoveryPending, "CargoProtectionHold"),
            Step(255, OperatingSystemLifecycleStageIds.CargoDispatch, "Redispatched", "cargo-safe-stop-b",
                OperationalWorldAttentionStateCodes.Recovered, "RouteReplanned"),
            Step(270, OperatingSystemLifecycleStageIds.CargoTransport, "TransportResumed", "cargo-route-b",
                OperationalWorldAttentionStateCodes.Recovered),
            Step(285, OperatingSystemLifecycleStageIds.CargoDropoff, "ConsigneeAccepted", "cargo-destination-b",
                OperationalWorldAttentionStateCodes.Recovered),
            Step(300, OperatingSystemLifecycleStageIds.CargoEvidenceSettlement, "ProofReturned", "cargo-driver-wait-b",
                OperationalWorldAttentionStateCodes.Recovered, returnState: "DriverAvailable"));

        AddCase(WarehouseLifecycleValidationCaseCodes.Normal,
            OperationalWorldOperatingSystemIds.WarehouseCommerceFulfillment,
            "WarehouseOperation", OperationalWorldSceneItemKinds.WarehouseTask, "WarehouseTeam",
            ["synthetic-role:warehouse-worker", "synthetic-object:pallet-a"],
            Step(250, OperatingSystemLifecycleStageIds.WarehouseInboundPlan, "InboundExpected", "warehouse-inbound-a"),
            Step(265, OperatingSystemLifecycleStageIds.WarehouseReceiving, "Receiving", "warehouse-inbound-a"),
            Step(280, OperatingSystemLifecycleStageIds.WarehouseInspection, "Inspecting", "warehouse-inspection-a"),
            Step(295, OperatingSystemLifecycleStageIds.WarehousePutAwayInventory, "PutAwayCompleted", "warehouse-storage-a"),
            Step(310, OperatingSystemLifecycleStageIds.WarehouseOutboundAllocation, "OutboundAllocated", "warehouse-storage-a"),
            Step(325, OperatingSystemLifecycleStageIds.WarehousePicking, "Picking", "warehouse-picking-a"),
            Step(340, OperatingSystemLifecycleStageIds.WarehousePacking, "Packed", "warehouse-packing-a"),
            Step(355, OperatingSystemLifecycleStageIds.WarehouseOutboundHandoff, "OutboundReady", "warehouse-outbound-a",
                OperationalWorldAttentionStateCodes.Completed, returnState: "WorkerAvailable"));

        AddCase(WarehouseLifecycleValidationCaseCodes.QuantityMismatchRecovery,
            OperationalWorldOperatingSystemIds.WarehouseCommerceFulfillment,
            "WarehouseOperationRecovery", OperationalWorldSceneItemKinds.WarehouseTask, "WarehouseRecoveryTeam",
            ["synthetic-event:quantity-mismatch", "synthetic-action:unit-hold", "synthetic-action:recount"],
            Step(290, OperatingSystemLifecycleStageIds.WarehouseInboundPlan, "InboundExpected", "warehouse-inbound-b"),
            Step(305, OperatingSystemLifecycleStageIds.WarehouseReceiving, "Receiving", "warehouse-inbound-b"),
            Step(320, OperatingSystemLifecycleStageIds.WarehouseInspection, "QuantityMismatchDetected", "warehouse-inspection-b",
                OperationalWorldAttentionStateCodes.RecoveryPending, "QuantityMismatch"),
            Step(335, OperatingSystemLifecycleStageIds.WarehouseExceptionRecovery, "UnitProtected", "warehouse-hold-b",
                OperationalWorldAttentionStateCodes.RecoveryPending, "ProtectedUnitHold"),
            Step(350, OperatingSystemLifecycleStageIds.WarehouseInspection, "Recounted", "warehouse-inspection-b",
                OperationalWorldAttentionStateCodes.Recovered, "ApprovedQuantityApplied"),
            Step(365, OperatingSystemLifecycleStageIds.WarehousePutAwayInventory, "PutAwayCompleted", "warehouse-storage-b",
                OperationalWorldAttentionStateCodes.Recovered),
            Step(380, OperatingSystemLifecycleStageIds.WarehouseOutboundAllocation, "OutboundAllocated", "warehouse-storage-b",
                OperationalWorldAttentionStateCodes.Recovered),
            Step(395, OperatingSystemLifecycleStageIds.WarehousePicking, "Picking", "warehouse-picking-b",
                OperationalWorldAttentionStateCodes.Recovered),
            Step(410, OperatingSystemLifecycleStageIds.WarehousePacking, "Packed", "warehouse-packing-b",
                OperationalWorldAttentionStateCodes.Recovered),
            Step(425, OperatingSystemLifecycleStageIds.WarehouseOutboundHandoff, "OutboundReady", "warehouse-outbound-b",
                OperationalWorldAttentionStateCodes.Recovered, returnState: "WorkerAvailable"));

        AddCase("mart-normal", OperationalWorldOperatingSystemIds.SsalddelMartUrbanLogistics,
            "MartOperation", OperationalWorldSceneItemKinds.WarehouseActor, "MartTeam",
            ["synthetic-role:mart-worker", "synthetic-object:customer-order-a", "synthetic-handoff:food-delivery-a"],
            Step(390, OperatingSystemLifecycleStageIds.MartSupplyAgreement, "SupplyAgreementSelected", "mart-a"),
            Step(405, OperatingSystemLifecycleStageIds.MartReplenishmentOrder, "InboundOrdered", "mart-a"),
            Step(420, OperatingSystemLifecycleStageIds.MartInboundReceiving, "ReceivedAndInspected", "mart-inbound-a"),
            Step(435, OperatingSystemLifecycleStageIds.MartPutawayInventory, "Stocked", "mart-storage-a"),
            Step(450, OperatingSystemLifecycleStageIds.MartCustomerOrderAllocation, "OrderAllocated", "mart-storage-a"),
            Step(465, OperatingSystemLifecycleStageIds.MartPickingPacking, "PickedAndPacked", "mart-packing-a"),
            Step(480, OperatingSystemLifecycleStageIds.MartLastMileHandoff, "FoodDeliveryChildHandedOff", "mart-handoff-a"),
            Step(495, OperatingSystemLifecycleStageIds.MartCompletionRecovery, "Completed", "mart-a",
                OperationalWorldAttentionStateCodes.Completed, returnState: "WorkerAvailable"));

        AddCase("mart-recovery", OperationalWorldOperatingSystemIds.SsalddelMartUrbanLogistics,
            "MartOperationRecovery", OperationalWorldSceneItemKinds.WarehouseActor, "MartRecoveryTeam",
            ["synthetic-event:stockout", "synthetic-action:replacement-or-hold", "synthetic-action:retry"],
            Step(430, OperatingSystemLifecycleStageIds.MartSupplyAgreement, "SupplyAgreementSelected", "mart-b"),
            Step(445, OperatingSystemLifecycleStageIds.MartReplenishmentOrder, "InboundOrdered", "mart-b"),
            Step(460, OperatingSystemLifecycleStageIds.MartInboundReceiving, "ReceivedAndInspected", "mart-inbound-b"),
            Step(475, OperatingSystemLifecycleStageIds.MartPutawayInventory, "Stocked", "mart-storage-b"),
            Step(490, OperatingSystemLifecycleStageIds.MartCustomerOrderAllocation, "StockoutDetected", "mart-storage-b",
                OperationalWorldAttentionStateCodes.RecoveryPending, "Stockout"),
            Step(505, OperatingSystemLifecycleStageIds.MartCompletionRecovery, "ReplacementOrHold", "mart-hold-b",
                OperationalWorldAttentionStateCodes.RecoveryPending, "ReplacementOrHold"),
            Step(520, OperatingSystemLifecycleStageIds.MartCustomerOrderAllocation, "StockAllocatedAfterRetry", "mart-storage-b",
                OperationalWorldAttentionStateCodes.Recovered, "RetrySucceeded"),
            Step(535, OperatingSystemLifecycleStageIds.MartPickingPacking, "PickedAndPacked", "mart-packing-b",
                OperationalWorldAttentionStateCodes.Recovered),
            Step(550, OperatingSystemLifecycleStageIds.MartLastMileHandoff, "FoodDeliveryChildHandedOff", "mart-handoff-b",
                OperationalWorldAttentionStateCodes.Recovered),
            Step(565, OperatingSystemLifecycleStageIds.MartCompletionRecovery, "CompletedAfterRecovery", "mart-b",
                OperationalWorldAttentionStateCodes.Recovered, returnState: "WorkerAvailable"));

        return new 관찰운영검증TimelineDefinitions(cases, steps);
    }

    internal static OperationalWorldSceneItem ToSceneItem(관찰운영검증단계Record item, DateTime publishedAtUtc)
        => new()
        {
            SnapshotStableId = $"sample-observation:{item.RunStableId}:{item.CaseCode}",
            AreaStableId = DefaultAreaStableId,
            OperatingSystemId = item.OperatingSystemId,
            ItemKind = item.ItemKind,
            RoleCode = item.RoleCode,
            ActivityCode = item.ExpectedStateCode,
            Revision = item.Revision,
            OccurredAtUtc = item.OccurredAtUtc ?? publishedAtUtc,
            PublishedAtUtc = publishedAtUtc,
            ExpiresAtUtc = publishedAtUtc.AddMinutes(30),
            DataPolicyCode = OperationalWorldScenePolicy.OnlineEphemeral,
            LocalStorageAllowed = false,
            ReplayAllowed = false,
            RepresentationDataJson = JsonSerializer.Serialize(new
            {
                caseCode = item.CaseCode,
                stepSequence = item.StepSequence,
                expectedStateCode = item.ExpectedStateCode,
                failureOrRecoveryCode = item.FailureOrRecoveryCode,
                returnStateCode = item.ReturnStateCode,
                stepHashSha256 = item.StepHashSha256,
                fixtureRevision = item.FixtureRevision,
                synthetic = true,
                exactPositionIncluded = false,
                personalDataIncluded = false
            }),
            WorkStableId = item.WorkStableId,
            LifecycleStageCode = item.LifecycleStageCode,
            AttentionStateCode = item.AttentionStateCode,
            ObjectKindCode = item.ObjectKindCode,
            SemanticPlaceStableId = item.SemanticPlaceStableId,
            RelationStableIds = JsonSerializer.Deserialize<string[]>(item.RelationStableIdsJson) ?? Array.Empty<string>(),
            SourceKindCode = OperationalWorldSceneSourceKinds.VerificationSample,
            ScenarioRunStableId = item.RunStableId
        };

    internal static 관찰운영검증OutboxRecord CreateOutbox(
        관찰운영검증단계Record step,
        OperationalWorldSceneItem sceneItem,
        DateTime createdAtUtc)
        => new()
        {
            EventStableId = $"observable-operation-step:{step.RunStableId}:{step.CaseCode}:s{step.StepSequence}:r{step.Revision}",
            RunStableId = step.RunStableId,
            CaseCode = step.CaseCode,
            EventTypeCode = "ObservableOperationLifecycleStepProjected",
            PayloadJson = JsonSerializer.Serialize(sceneItem),
            StatusCode = 관찰운영검증상태Codes.Pending,
            CreatedAtUtc = createdAtUtc,
            NextAttemptAtUtc = createdAtUtc
        };

    private static OperationalWorldSceneItem ToLegacySceneItem(
        관찰운영검증사례Record item,
        DateTime publishedAtUtc)
        => new()
        {
            SnapshotStableId = $"sample-observation:{item.RunStableId}:{item.CaseCode}",
            AreaStableId = DefaultAreaStableId,
            OperatingSystemId = item.OperatingSystemId,
            ItemKind = item.ItemKind,
            RoleCode = item.RoleCode,
            ActivityCode = item.LifecycleStageCode,
            Revision = Math.Max(1, item.Revision),
            OccurredAtUtc = item.OccurredAtUtc ?? publishedAtUtc,
            PublishedAtUtc = publishedAtUtc,
            ExpiresAtUtc = publishedAtUtc.AddMinutes(30),
            DataPolicyCode = OperationalWorldScenePolicy.OnlineEphemeral,
            LocalStorageAllowed = false,
            ReplayAllowed = false,
            RepresentationDataJson = JsonSerializer.Serialize(new
            {
                caseCode = item.CaseCode,
                synthetic = true,
                legacyCompletionSample = true,
                exactPositionIncluded = false,
                personalDataIncluded = false
            }),
            WorkStableId = item.WorkStableId,
            LifecycleStageCode = item.LifecycleStageCode,
            AttentionStateCode = item.AttentionStateCode,
            ObjectKindCode = item.ObjectKindCode,
            SemanticPlaceStableId = item.SemanticPlaceStableId,
            RelationStableIds = JsonSerializer.Deserialize<string[]>(item.RelationStableIdsJson) ?? Array.Empty<string>(),
            SourceKindCode = OperationalWorldSceneSourceKinds.VerificationSample,
            ScenarioRunStableId = item.RunStableId
        };

    private static string StepHash(
        string caseCode,
        int sequence,
        string operatingSystemId,
        string objectKindCode,
        string itemKind,
        string roleCode,
        string sourceKindCode,
        관찰운영검증StepDefinition definition,
        IReadOnlyList<string> relations)
    {
        var canonical = string.Join('|',
            TimelineFixtureRevision,
            caseCode,
            sequence.ToString(System.Globalization.CultureInfo.InvariantCulture),
            operatingSystemId,
            objectKindCode,
            itemKind,
            roleCode,
            sourceKindCode,
            definition.OffsetSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture),
            definition.LifecycleStageCode,
            definition.ExpectedStateCode,
            definition.AttentionStateCode,
            definition.PlaceCode,
            definition.FailureOrRecoveryCode,
            definition.ReturnStateCode,
            string.Join('\u001f', relations));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    private 관찰운영검증상태 EmptyState()
        => new(string.Empty, 관찰운영검증상태Codes.Idle, DefaultAreaStableId, 0,
            관찰운영검증Options.RequiredDurationSeconds, 0, 0, 0, 0, 0,
            fixtureStatusCode, fixturePackStableId, fixtureHashSha256, fixtureRevision,
            Array.Empty<관찰운영검증사례상태>());
}

internal sealed record 관찰운영검증TimelineDefinitions(
    IReadOnlyList<관찰운영검증사례Record> Cases,
    IReadOnlyList<관찰운영검증단계Record> Steps);

internal sealed record 관찰운영검증StepDefinition(
    int OffsetSeconds,
    string LifecycleStageCode,
    string ExpectedStateCode,
    string AttentionStateCode,
    string PlaceCode,
    string FailureOrRecoveryCode,
    string ReturnStateCode);

public interface I관찰운영검증UseCase
{
    Task<관찰운영검증상태> StartAsync(string? runStableId, CancellationToken cancellationToken);
    Task<관찰운영검증상태> ReadAsync(CancellationToken cancellationToken);
    Task<관찰운영검증상태> PauseAsync(CancellationToken cancellationToken);
    Task<관찰운영검증상태> ResumeAsync(CancellationToken cancellationToken);
    Task<관찰운영검증상태> RetryAsync(CancellationToken cancellationToken);
}

public sealed class 관찰운영검증UseCase(관찰운영검증Runner runner) : I관찰운영검증UseCase
{
    public Task<관찰운영검증상태> StartAsync(string? runStableId, CancellationToken cancellationToken)
        => runner.StartRunAsync(runStableId, cancellationToken);
    public Task<관찰운영검증상태> ReadAsync(CancellationToken cancellationToken)
        => runner.ReadLatestAsync(cancellationToken);
    public Task<관찰운영검증상태> PauseAsync(CancellationToken cancellationToken)
        => runner.PauseAsync(cancellationToken);
    public Task<관찰운영검증상태> ResumeAsync(CancellationToken cancellationToken)
        => runner.ResumeAsync(cancellationToken);
    public Task<관찰운영검증상태> RetryAsync(CancellationToken cancellationToken)
        => runner.RetryFailedOutboxAsync(cancellationToken);
}
