using System.Data;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MySqlConnector;
using Ssalddel.Infrastructure.Persistence.AgriculturalFisheries;
using Ssalddel.Infrastructure.Persistence.PublicData;
using Ssalddel.Infrastructure.Persistence.TraditionalMarkets;
using 살뜰.Data;

// 전용 하네스의 schema 준비/관측만 소유한다. 제품 readiness와 migration은 변경하지 않는다.
internal static class R108SchemaReadiness
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    internal static string Mode => Environment.GetEnvironmentVariable("R108_SCHEMA_MODE") switch
    {
        null or "" or "CurrentModel" => "CurrentModel",
        "MigrationFirst" => "MigrationFirst",
        _ => throw new InvalidOperationException("R108SchemaModeInvalid")
    };

    internal static async Task PrepareAsync(IServiceProvider services, CargoJourneySettings settings)
    {
        if (Mode == "CurrentModel") return;
        ValidateFreshPreparation(settings);
        var attemptPath = Path.Combine(settings.ArtifactRoot, "schema-migration-attempt.json");
        await using var scope = services.CreateAsyncScope();
        var contexts = ResolveContexts(scope.ServiceProvider, settings);
        if (File.Exists(attemptPath))
        {
            using var previous = JsonDocument.Parse(await File.ReadAllTextAsync(attemptPath));
            ValidateBinding(previous.RootElement, settings);
            if (previous.RootElement.GetProperty("state").GetString() != "Succeeded")
                throw new InvalidOperationException("R108SchemaAttemptFailedOrInterruptedPreserveDatabase");
            // 성공한 같은 실행만 재개하며 새 migration이 생겼으면 자동 적용하지 않는다.
            foreach (var (_, db) in contexts)
                if ((await db.Database.GetPendingMigrationsAsync()).Any())
                    throw new InvalidOperationException("R108SuccessfulSchemaNowHasPendingMigrations");
            return;
        }

        if (Environment.GetEnvironmentVariable("R108_CONFIRM_FRESH_EMPTY_SCHEMA") != "1")
            throw new InvalidOperationException("R108FreshEmptySchemaConfirmationRequired");
        if (File.Exists(Path.Combine(settings.ArtifactRoot, "initialized.json"))
            || File.Exists(Path.Combine(settings.ArtifactRoot, "ready.json")))
            throw new InvalidOperationException("R108MigrationMustPrecedeSeedAndReady");
        if (await CountSchemaObjectsAsync(contexts[0].Db) != 0)
            throw new InvalidOperationException("R108MigrationRequiresEmptySchema");

        var attempt = new SchemaAttempt
        {
            Database = CargoJourneySettings.DatabaseName, RunStableId = settings.RunStableId,
            StartedAtUtc = DateTimeOffset.UtcNow, State = "Started"
        };
        // 적용 전에 CreateNew로 시도를 고정한다. 중단·실패 상태를 덮어 재시도하지 않는다.
        await using (var stream = new FileStream(attemptPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            await JsonSerializer.SerializeAsync(stream, attempt, JsonOptions);

        try
        {
            foreach (var (name, db) in contexts)
            {
                var progress = new SchemaContextResult { Name = name, State = "Started" };
                attempt.Contexts.Add(progress);
                await WriteAsync(attemptPath, attempt);
                try
                {
                    await ObserveMigrationsAsync(db, progress, before: true);
                    if (progress.KnownMigrations is null or 0)
                        throw new InvalidOperationException("R108MigrationCatalogMustNotBeEmpty");
                    await WriteAsync(attemptPath, attempt);
                    await db.Database.MigrateAsync();
                    await ObserveMigrationsAsync(db, progress, before: false);
                    if (progress.PendingAfter != 0 || progress.AppliedAfter != progress.KnownMigrations)
                        throw new InvalidOperationException("R108MigrationCompletedWithPendingMigrations");
                    progress.State = "Succeeded";
                }
                catch (Exception exception)
                {
                    progress.State = "Failed";
                    progress.Error = SafeError.From(exception);
                    // 첫 pending ID는 실패한 SQL의 정확한 migration ID와 같다고 가정하지 않는다.
                    try { await ObserveMigrationsAsync(db, progress, before: false); }
                    catch (Exception observationError) { progress.ObservationError = SafeError.From(observationError); }
                    throw;
                }
                finally { await WriteAsync(attemptPath, attempt); }
            }
            attempt.State = "Succeeded";
            attempt.FinishedAtUtc = DateTimeOffset.UtcNow;
            await WriteAsync(attemptPath, attempt);
        }
        catch (Exception exception)
        {
            attempt.State = "Failed";
            attempt.Error = SafeError.From(exception);
            attempt.FinishedAtUtc = DateTimeOffset.UtcNow;
            await WriteAsync(attemptPath, attempt);
            await CaptureReadinessAsync(services, settings, "readiness-after-schema-failure.json");
            throw new InvalidOperationException("R108SchemaMigrationFailedPreserveDatabase", exception);
        }
    }

    internal static async Task<object> InspectAsync(IServiceProvider services, CargoJourneySettings settings)
    {
        await using var scope = services.CreateAsyncScope();
        var contexts = ResolveContexts(scope.ServiceProvider, settings);
        var observations = new List<SchemaContextResult>();
        foreach (var (name, db) in contexts)
        {
            var result = new SchemaContextResult { Name = name, State = "ReadOnlyObservation" };
            try { await ObserveMigrationsAsync(db, result, before: false); }
            catch (Exception exception) { result.State = "ObservationFailed"; result.Error = SafeError.From(exception); }
            observations.Add(result);
        }
        var health = await CaptureReadinessAsync(services, settings, "readiness-diagnostic.json");
        var observation = new
        {
            schemaVersion = "integrated-workflow-r108.schema-inspection.r1",
            database = CargoJourneySettings.DatabaseName, settings.RunStableId, mode = Mode,
            atUtc = DateTimeOffset.UtcNow, contexts = observations, health,
            schemaChanged = false, seedExecuted = false, operationalMigrationVerified = false
        };
        await WriteAsync(Path.Combine(settings.ArtifactRoot, "schema-inspection.json"), observation);
        return observation;
    }

    internal static async Task<SafeHealthReport> CaptureReadinessAsync(
        IServiceProvider services, CargoJourneySettings settings, string fileName)
    {
        SafeHealthReport safe;
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
            var report = await services.GetRequiredService<HealthCheckService>().CheckHealthAsync(timeout.Token);
            safe = new SafeHealthReport
            {
                Status = report.Status.ToString(), AtUtc = DateTimeOffset.UtcNow,
                Entries = report.Entries.Select(entry => new SafeHealthEntry
                {
                    Name = entry.Key, Status = entry.Value.Status.ToString(),
                    Tags = entry.Value.Tags.ToArray(),
                    PendingMigrations = entry.Value.Data
                        .Where(item => item.Key.EndsWith("PendingMigrations", StringComparison.Ordinal) && item.Value is int)
                        .ToDictionary(item => item.Key, item => (int)item.Value),
                    TransientStateProvider = entry.Value.Data.TryGetValue("transientStateProvider", out var provider)
                        && provider is string and ("Memory" or "Redis") ? (string)provider : null,
                    MongoDatabaseMatchesOwnedRun = entry.Value.Data.TryGetValue("mongoDatabase", out var database)
                        ? Equals(database, CargoJourneySettings.DatabaseName) : null,
                    Error = entry.Value.Exception is { } exception ? SafeError.From(exception) : null
                }).ToArray()
            };
        }
        catch (Exception exception)
        {
            safe = new SafeHealthReport { Status = "ObservationFailed", AtUtc = DateTimeOffset.UtcNow,
                Error = SafeError.From(exception) };
        }
        await WriteAsync(Path.Combine(settings.ArtifactRoot, fileName), new
        {
            schemaVersion = "integrated-workflow-r108.readiness.r1", settings.RunStableId,
            database = CargoJourneySettings.DatabaseName, report = safe,
            productHealthPolicyChanged = false, operationalReadinessClaimed = false
        });
        return safe;
    }

    private static (string Name, DbContext Db)[] ResolveContexts(IServiceProvider services, CargoJourneySettings settings)
    {
        (string, DbContext)[] contexts =
        [
            ("mysql-main", services.GetRequiredService<SsalddelContext>()),
            ("mysql-traditional-markets", services.GetRequiredService<TraditionalMarketDbContext>()),
            ("mysql-agricultural-fisheries", services.GetRequiredService<AgriculturalFisheriesDbContext>()),
            ("mysql-public-data-ingestion", services.GetRequiredService<PublicDataIngestionDbContext>())
        ];
        foreach (var (_, db) in contexts) settings.ValidateSqlConnection(db.Database.GetDbConnection().ConnectionString);
        return contexts;
    }

    private static void ValidateFreshPreparation(CargoJourneySettings settings)
    {
        var suffix = CargoJourneySettings.DatabaseGuid;
        var expected = Path.GetFullPath(Path.Combine(settings.RepositoryRoot, "artifacts", "local", "integrated-workflow-r108", suffix));
        if (!CargoJourneySettings.DatabaseName.StartsWith(CargoJourneySettings.MigrationFirstDatabasePrefix, StringComparison.Ordinal)
            || !Regex.IsMatch(suffix, "^[a-f0-9]{32}$")
            || !string.Equals(expected, settings.ArtifactRoot, StringComparison.OrdinalIgnoreCase)
            || settings.RunStableId != "integrated-r108:" + suffix)
            throw new InvalidOperationException("R108SchemaOwnedGuidBindingRequired");
        using var preparation = JsonDocument.Parse(File.ReadAllText(Path.Combine(settings.ArtifactRoot, "preparation.json")));
        ValidateBinding(preparation.RootElement, settings);
        if (preparation.RootElement.GetProperty("schemaMode").GetString() != "MigrationFirst"
            || preparation.RootElement.GetProperty("status").GetString() != "FreshDedicatedDatabasePrepared"
            || preparation.RootElement.GetProperty("outputRoot").GetString() != settings.ArtifactRoot)
            throw new InvalidOperationException("R108MigrationFirstPreparationRequired");
    }

    private static void ValidateBinding(JsonElement value, CargoJourneySettings settings)
    {
        // 기록은 이 하네스 전용이다. 기존 원본 DB를 migration 대상으로 승격하지 않는다.
        if (value.GetProperty("database").GetString() != CargoJourneySettings.DatabaseName
            || value.GetProperty("runStableId").GetString() != settings.RunStableId)
            throw new InvalidOperationException("R108SchemaRecordBindingMismatch");
    }

    private static async Task<long> CountSchemaObjectsAsync(DbContext db)
    {
        var connection = db.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere) await connection.OpenAsync();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT (SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE()) + (SELECT COUNT(*) FROM information_schema.ROUTINES WHERE ROUTINE_SCHEMA = DATABASE()) + (SELECT COUNT(*) FROM information_schema.EVENTS WHERE EVENT_SCHEMA = DATABASE())";
            return Convert.ToInt64(await command.ExecuteScalarAsync());
        }
        finally { if (openedHere) await connection.CloseAsync(); }
    }

    private static async Task ObserveMigrationsAsync(DbContext db, SchemaContextResult result, bool before)
    {
        var applied = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
        var pending = (await db.Database.GetPendingMigrationsAsync()).ToArray();
        result.KnownMigrations = db.Database.GetMigrations().Count();
        if (before)
        {
            result.AppliedBefore = applied.Length; result.PendingBefore = pending.Length;
            result.FirstPendingBefore = pending.FirstOrDefault();
        }
        else
        {
            result.AppliedAfter = applied.Length; result.PendingAfter = pending.Length;
            result.LastAppliedAfter = applied.LastOrDefault(); result.FirstPendingAfter = pending.FirstOrDefault();
        }
    }

    private static async Task WriteAsync<T>(string path, T value)
    {
        var temporary = path + ".writing";
        await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(value, JsonOptions));
        File.Move(temporary, path, overwrite: true);
    }

    private sealed class SchemaAttempt
    {
        public string SchemaVersion { get; init; } = "integrated-workflow-r108.schema-attempt.r1";
        [System.Text.Json.Serialization.JsonPropertyName("database")] public required string Database { get; init; }
        [System.Text.Json.Serialization.JsonPropertyName("runStableId")] public required string RunStableId { get; init; }
        [System.Text.Json.Serialization.JsonPropertyName("state")] public required string State { get; set; }
        public DateTimeOffset StartedAtUtc { get; init; }
        public DateTimeOffset? FinishedAtUtc { get; set; }
        public List<SchemaContextResult> Contexts { get; } = [];
        public SafeError? Error { get; set; }
        public bool OperationalMigrationVerified => false;
        public bool FailedOrInterruptedAttemptMayAutomaticallyResume => false;
    }

    private sealed class SchemaContextResult
    {
        public required string Name { get; init; }
        public required string State { get; set; }
        public int? KnownMigrations { get; set; }
        public int? AppliedBefore { get; set; }
        public int? PendingBefore { get; set; }
        public string? FirstPendingBefore { get; set; }
        public int? AppliedAfter { get; set; }
        public int? PendingAfter { get; set; }
        public string? LastAppliedAfter { get; set; }
        public string? FirstPendingAfter { get; set; }
        public bool FailureMigrationExactIdVerified => false;
        public SafeError? Error { get; set; }
        public SafeError? ObservationError { get; set; }
    }

    internal sealed class SafeHealthReport
    {
        public required string Status { get; init; }
        public DateTimeOffset AtUtc { get; init; }
        public SafeHealthEntry[] Entries { get; init; } = [];
        public SafeError? Error { get; init; }
    }

    internal sealed class SafeHealthEntry
    {
        public required string Name { get; init; }
        public required string Status { get; init; }
        public string[] Tags { get; init; } = [];
        public Dictionary<string, int> PendingMigrations { get; init; } = [];
        public string? TransientStateProvider { get; init; }
        public bool? MongoDatabaseMatchesOwnedRun { get; init; }
        public SafeError? Error { get; init; }
    }

    internal sealed record SafeError(string Type, int? MySqlNumber, string? SqlState, string? HarnessCode)
    {
        internal static SafeError From(Exception exception)
        {
            MySqlException? mysql = null;
            for (Exception? current = exception; current is not null; current = current.InnerException)
                if (current is MySqlException sql) { mysql = sql; break; }
            return new(exception.GetType().FullName ?? exception.GetType().Name, mysql?.Number,
                mysql?.SqlState, exception is InvalidOperationException
                    && Regex.IsMatch(exception.Message, "^R108[A-Za-z0-9]+$") ? exception.Message : null);
        }
    }
}
