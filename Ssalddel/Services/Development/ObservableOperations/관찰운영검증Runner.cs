using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ssalddel.WorkflowRules.Contracts;
using 살뜰.Data;

namespace Ssalddel.Services.Development.ObservableOperations;

/// <summary>
/// 여섯 합성 업무의 완료 Event를 전용 MySQL Outbox에 먼저 남기고 MongoDB·Redis로 전달합니다.
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
    private readonly SemaphoreSlim gate = new(1, 1);
    private volatile bool initialized;

    public async Task<관찰운영검증상태> StartRunAsync(
        string? requestedRunStableId,
        CancellationToken cancellationToken)
    {
        await EnsureInitializedAsync(cancellationToken);
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
            db.Runs.Add(new 관찰운영검증실행Record
            {
                RunStableId = runStableId,
                StatusCode = 관찰운영검증상태Codes.Running,
                AreaStableId = DefaultAreaStableId,
                DurationSeconds = options.DurationSeconds,
                ResumedAtUtc = now,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
                Revision = 1
            });
            db.Cases.AddRange(Definitions(runStableId));
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
            if (run is null) return;
            var elapsed = ElapsedSeconds(run, utcNow);
            var dueCases = await db.Cases.Where(x =>
                    x.RunStableId == run.RunStableId
                    && x.StateCode == 관찰운영검증상태Codes.Scheduled
                    && x.ScheduledOffsetSeconds <= elapsed)
                .OrderBy(x => x.ScheduledOffsetSeconds)
                .ThenBy(x => x.CaseCode)
                .ToArrayAsync(cancellationToken);
            foreach (var item in dueCases)
            {
                item.StateCode = 관찰운영검증상태Codes.Published;
                item.OccurredAtUtc = utcNow;
                item.Revision = 1;
                db.Outbox.Add(new 관찰운영검증OutboxRecord
                {
                    EventStableId = $"observable-operation-completed:{run.RunStableId}:{item.CaseCode}:r1",
                    RunStableId = run.RunStableId,
                    CaseCode = item.CaseCode,
                    EventTypeCode = "ObservableOperationLifecycleCompleted",
                    PayloadJson = JsonSerializer.Serialize(new
                    {
                        item.CaseCode,
                        item.WorkStableId,
                        item.LifecycleStageCode,
                        item.AttentionStateCode,
                        synthetic = true
                    }),
                    StatusCode = 관찰운영검증상태Codes.Pending,
                    CreatedAtUtc = utcNow,
                    NextAttemptAtUtc = utcNow
                });
            }
            if (elapsed >= run.DurationSeconds)
            {
                run.StatusCode = 관찰운영검증상태Codes.Completed;
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

    private async Task PublishPendingAsync(
        string runStableId,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        var pending = await db.Outbox.Where(x =>
                x.RunStableId == runStableId
                && x.StatusCode == 관찰운영검증상태Codes.Pending
                && x.NextAttemptAtUtc <= utcNow)
            .OrderBy(x => x.Id)
            .Take(20)
            .ToArrayAsync(cancellationToken);
        foreach (var message in pending)
        {
            try
            {
                var item = await db.Cases.AsNoTracking().SingleAsync(x =>
                    x.RunStableId == message.RunStableId && x.CaseCode == message.CaseCode,
                    cancellationToken);
                await projectionWriter.UpsertAsync(ToSceneItem(item, utcNow), cancellationToken);
                message.StatusCode = 관찰운영검증상태Codes.Published;
                message.ProcessedAtUtc = utcNow;
                message.LastErrorCode = string.Empty;
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                message.StatusCode = 관찰운영검증상태Codes.Failed;
                message.AttemptCount++;
                message.NextAttemptAtUtc = utcNow.AddSeconds(5);
                message.LastErrorCode = exception.GetType().Name;
            }
        }
        await db.SaveChangesAsync(cancellationToken);
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

    private async Task<관찰운영검증상태> ReadRunAsync(
        string runStableId,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        var run = await db.Runs.AsNoTracking().SingleAsync(x => x.RunStableId == runStableId, cancellationToken);
        var cases = await db.Cases.AsNoTracking().Where(x => x.RunStableId == runStableId)
            .OrderBy(x => x.ScheduledOffsetSeconds).ThenBy(x => x.CaseCode)
            .Select(x => new 관찰운영검증사례상태(
                x.CaseCode,
                x.OperatingSystemId,
                x.WorkStableId,
                x.LifecycleStageCode,
                x.AttentionStateCode,
                x.StateCode,
                x.ScheduledOffsetSeconds,
                x.Revision))
            .ToArrayAsync(cancellationToken);
        var pending = await db.Outbox.AsNoTracking().CountAsync(x =>
            x.RunStableId == runStableId && x.StatusCode == 관찰운영검증상태Codes.Pending,
            cancellationToken);
        var failed = await db.Outbox.AsNoTracking().CountAsync(x =>
            x.RunStableId == runStableId && x.StatusCode == 관찰운영검증상태Codes.Failed,
            cancellationToken);
        return new 관찰운영검증상태(
            run.RunStableId,
            run.StatusCode,
            run.AreaStableId,
            ElapsedSeconds(run, utcNow),
            run.DurationSeconds,
            cases.Count(x => x.StateCode == 관찰운영검증상태Codes.Published),
            pending,
            failed,
            cases);
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
            initialized = true;
        }
        finally
        {
            gate.Release();
        }
    }

    private static async Task EnsureVerificationSchemaAsync(
        관찰운영검증DbContext db,
        CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS `관찰운영검증_실행` (
              `RunStableId` varchar(96) NOT NULL,
              `StatusCode` varchar(24) NOT NULL,
              `AreaStableId` varchar(128) NOT NULL,
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
    }

    private static int ElapsedSeconds(관찰운영검증실행Record run, DateTime utcNow)
    {
        var elapsed = run.ElapsedBeforeResumeSeconds;
        if (run.StatusCode == 관찰운영검증상태Codes.Running && run.ResumedAtUtc.HasValue)
            elapsed += Math.Max(0, (int)(utcNow - run.ResumedAtUtc.Value).TotalSeconds);
        return Math.Min(run.DurationSeconds, elapsed);
    }

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

    private static 관찰운영검증사례Record[] Definitions(string runStableId)
    {
        관찰운영검증사례Record Case(
            string code,
            string os,
            string lifecycle,
            string attention,
            string objectKind,
            string itemKind,
            string role,
            string place,
            int offset,
            params string[] relations)
            => new()
            {
                RunStableId = runStableId,
                CaseCode = code,
                OperatingSystemId = os,
                WorkStableId = $"sample-work:{runStableId}:{code}",
                LifecycleStageCode = lifecycle,
                AttentionStateCode = attention,
                ObjectKindCode = objectKind,
                ItemKind = itemKind,
                RoleCode = role,
                SemanticPlaceStableId = "synthetic-place:" + place,
                RelationStableIdsJson = JsonSerializer.Serialize(relations),
                ScheduledOffsetSeconds = offset
            };

        return
        [
            Case("food-normal", OperationalWorldOperatingSystemIds.FoodDelivery,
                "ReceiptConfirmed", OperationalWorldAttentionStateCodes.Completed,
                "FoodDelivery", OperationalWorldSceneItemKinds.CompletedLifecycle,
                "FoodDeliveryTeam", "food-route-a", 30,
                "synthetic-role:customer", "synthetic-role:restaurant", "synthetic-role:food-driver"),
            Case("food-recovery", OperationalWorldOperatingSystemIds.FoodDelivery,
                "ReceiptConfirmedAfterRedispatch", OperationalWorldAttentionStateCodes.Recovered,
                "FoodDeliveryRecovery", OperationalWorldSceneItemKinds.CompletedLifecycle,
                "FoodDeliveryRecoveryTeam", "food-route-b", 120,
                "synthetic-event:pickup-incident", "synthetic-action:recook", "synthetic-action:redispatch"),
            Case("cargo-normal", OperationalWorldOperatingSystemIds.DomesticCargoTransport,
                "ConsigneeAccepted", OperationalWorldAttentionStateCodes.Completed,
                "CargoTransport", OperationalWorldSceneItemKinds.CompletedLifecycle,
                "CargoDeliveryTeam", "cargo-route-a", 210,
                "synthetic-role:shipper", "synthetic-role:cargo-driver"),
            Case("cargo-recovery", OperationalWorldOperatingSystemIds.DomesticCargoTransport,
                "ConsigneeAcceptedAfterReplan", OperationalWorldAttentionStateCodes.Recovered,
                "CargoTransportRecovery", OperationalWorldSceneItemKinds.CompletedLifecycle,
                "CargoRecoveryTeam", "cargo-route-b", 300,
                "synthetic-event:time-conflict", "synthetic-action:reservation-release", "synthetic-action:replan"),
            Case("warehouse-normal", OperationalWorldOperatingSystemIds.WarehouseCommerceFulfillment,
                "Packed", OperationalWorldAttentionStateCodes.Completed,
                "WarehouseOperation", OperationalWorldSceneItemKinds.WarehouseTask,
                "WarehouseTeam", "warehouse-a", 390,
                "synthetic-action:inbound", "synthetic-action:inspect", "synthetic-action:putaway", "synthetic-action:pick"),
            Case("mart-recovery", OperationalWorldOperatingSystemIds.SsalddelMartUrbanLogistics,
                "ProjectionRecovered", OperationalWorldAttentionStateCodes.Recovered,
                "MartOperationRecovery", OperationalWorldSceneItemKinds.WarehouseActor,
                "MartRecoveryTeam", "mart-a", 480,
                "synthetic-event:follow-up-failed", "synthetic-action:recovery-wait", "synthetic-action:retry")
        ];
    }

    private static OperationalWorldSceneItem ToSceneItem(관찰운영검증사례Record item, DateTime publishedAtUtc)
        => new()
        {
            SnapshotStableId = $"sample-observation:{item.RunStableId}:{item.CaseCode}",
            AreaStableId = DefaultAreaStableId,
            OperatingSystemId = item.OperatingSystemId,
            ItemKind = item.ItemKind,
            RoleCode = item.RoleCode,
            ActivityCode = item.LifecycleStageCode,
            Revision = item.Revision,
            OccurredAtUtc = item.OccurredAtUtc ?? publishedAtUtc,
            PublishedAtUtc = publishedAtUtc,
            ExpiresAtUtc = publishedAtUtc.AddMinutes(30),
            DataPolicyCode = OperationalWorldScenePolicy.OnlineEphemeral,
            LocalStorageAllowed = false,
            ReplayAllowed = false,
            RepresentationDataJson = JsonSerializer.Serialize(new
            {
                item.CaseCode,
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

    private static 관찰운영검증상태 EmptyState()
        => new(string.Empty, 관찰운영검증상태Codes.Idle, DefaultAreaStableId, 0,
            관찰운영검증Options.RequiredDurationSeconds, 0, 0, 0,
            Array.Empty<관찰운영검증사례상태>());
}

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
