using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

// 준비용 R108 관측기입니다. 등록 전에는 동작하지 않으며 health 실행/응답/정책을 대체하지 않습니다.
internal static class R108ReadinessObservationRegistration
{
    private static readonly object OriginalHealthCheckServiceKey = new();

    internal static IServiceCollection AddR108ReadinessObservations(this IServiceCollection services)
    {
        if (services.Any(descriptor => descriptor.IsKeyedService
            && descriptor.ServiceType == typeof(HealthCheckService)
            && ReferenceEquals(descriptor.ServiceKey, OriginalHealthCheckServiceKey)))
            return services;
        var originals = services.Select((descriptor, descriptorIndex) => (Descriptor: descriptor, Index: descriptorIndex))
            .Where(item => !item.Descriptor.IsKeyedService && item.Descriptor.ServiceType == typeof(HealthCheckService))
            .ToArray();
        if (originals.Length != 1)
            return services;

        var (original, index) = originals[0];
        // keyed DI가 원래 type/factory/instance와 수명·Dispose 소유권을 그대로 관리합니다.
        var keyedOriginal = original.ImplementationType is { } implementationType
            ? new ServiceDescriptor(typeof(HealthCheckService), OriginalHealthCheckServiceKey, implementationType, original.Lifetime)
            : original.ImplementationFactory is { } factory
                ? new ServiceDescriptor(typeof(HealthCheckService), OriginalHealthCheckServiceKey,
                    (provider, _) => factory(provider), original.Lifetime)
                : original.ImplementationInstance is HealthCheckService instance
                    ? new ServiceDescriptor(typeof(HealthCheckService), OriginalHealthCheckServiceKey, instance)
                    : null;
        if (keyedOriginal is null) return services;
        services[index] = new ServiceDescriptor(typeof(HealthCheckService), provider =>
            new R108ObservedHealthCheckService(
                provider.GetRequiredKeyedService<HealthCheckService>(OriginalHealthCheckServiceKey),
                provider.GetRequiredService<R108ReadyObservationState>()), original.Lifetime);
        services.Insert(index, keyedOriginal);
        services.TryAddSingleton<R108ReadyObservationState>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, R108ReadinessLoggerProvider>());
        services.AddLogging(logging => logging.AddFilter<R108ReadinessLoggerProvider>(
            R108ReadinessLoggerProvider.Category, LogLevel.Debug));
        services.TryAddEnumerable(ServiceDescriptor.Transient<IStartupFilter, R108ReadinessDiagnosticStartupFilter>());
        return services;
    }
}

internal sealed class R108ReadinessDiagnosticStartupFilter(
    CargoJourneySettings settings, R108ReadyObservationState observations) : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use(async (context, nextMiddleware) =>
        {
            var path = context.Request.Path.Value;
            if (!string.Equals(path, "/health/ready", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(path, "/health/ready/", StringComparison.OrdinalIgnoreCase))
            {
                await nextMiddleware(context);
                return;
            }
            var previous = observations.Current.Value;
            var capture = new R108ReadyRequestCapture();
            observations.Current.Value = capture;
            var watch = Stopwatch.StartNew();
            var completed = false;
            try { await nextMiddleware(context); completed = true; }
            finally
            {
                watch.Stop();
                observations.Current.Value = previous;
                try
                {
                    R108ReadyObservationJournal.TryWrite(settings.ArtifactRoot,
                        capture.Snapshot(context.Response.StatusCode, watch.Elapsed, completed), observations.SessionId);
                }
                catch (Exception) { /* 원래 요청의 결과를 유지합니다. */ }
            }
        });
        next(app);
    };
}

internal sealed class R108ReadyObservationState
{
    internal static string ProcessSessionId { get; } = Guid.NewGuid().ToString("N");
    internal string SessionId => ProcessSessionId;
    internal AsyncLocal<R108ReadyRequestCapture?> Current { get; } = new();
}

// inner는 keyed DI가 소유하므로 이 wrapper가 다시 Dispose하지 않습니다.
internal sealed class R108ObservedHealthCheckService(
    HealthCheckService inner, R108ReadyObservationState observations) : HealthCheckService
{
    public override async Task<HealthReport> CheckHealthAsync(
        Func<HealthCheckRegistration, bool>? predicate, CancellationToken cancellationToken = default)
    {
        var capture = observations.Current.Value;
        var report = await inner.CheckHealthAsync(predicate, cancellationToken);
        try { capture?.ObserveReport(report); }
        catch (Exception) { /* 관측 실패는 원래 report·예외·취소 의미를 바꾸지 않습니다. */ }
        return report;
    }
}

internal sealed class R108ReadinessLoggerProvider(R108ReadyObservationState observations) : ILoggerProvider
{
    internal const string Category = "Microsoft.Extensions.Diagnostics.HealthChecks.DefaultHealthCheckService";
    public ILogger CreateLogger(string categoryName)
        => categoryName == Category ? new SafeLogger(observations) : NullLogger.Instance;
    public void Dispose() { }

    private sealed class SafeLogger(R108ReadyObservationState observations) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => level >= LogLevel.Debug && level != LogLevel.None;
        public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            // formatter/Description/Message/임의 Data를 읽거나 출력하지 않습니다.
            try
            {
                if (observations.Current.Value is not { } capture || eventId.Id is not (103 or 104 or 105)
                    || state is not IEnumerable<KeyValuePair<string, object?>> pairs) return;
                var selected = new Dictionary<string, object?>(StringComparer.Ordinal);
                foreach (var pair in pairs)
                {
                    if (pair.Key is "HealthCheckName" or "HealthStatus" or "ElapsedMilliseconds"
                        || R108ReadyRequestCapture.PendingKeys.Contains(pair.Key))
                        if (!selected.TryAdd(pair.Key, pair.Value)) return;
                }
                if (!selected.TryGetValue("HealthCheckName", out var value) || value is not string name
                    || name is not ("persistence" or "simulation-persistence")) return;
                capture.Observe(name, eventId.Id, selected, exception?.GetType().FullName);
            }
            catch (Exception) { /* 진단 제공자의 실패가 원래 로그/요청을 실패시키지 않습니다. */ }
        }
    }
}

internal sealed class R108ReadyRequestCapture
{
    internal static readonly string[] PendingKeys =
    [
        "mysql-mainPendingMigrations", "mysql-traditional-marketsPendingMigrations",
        "mysql-agricultural-fisheriesPendingMigrations", "mysql-public-data-ingestionPendingMigrations"
    ];
    private static readonly string[] PendingLabels =
        ["mysql-main", "mysql-traditional-markets", "mysql-agricultural-fisheries", "mysql-public-data-ingestion"];
    private readonly object gate = new();
    private readonly Dictionary<string, CheckCapture> checks = new(StringComparer.Ordinal);
    private readonly string requestId = Guid.NewGuid().ToString("N");
    private bool healthReportObserved;
    private string? healthReportStatus;

    internal void ObserveReport(HealthReport report)
    {
        lock (gate)
        {
            healthReportObserved = true;
            healthReportStatus = Enum.IsDefined(report.Status) ? report.Status.ToString() : null;
            foreach (var name in new[] { "persistence", "simulation-persistence" })
            {
                if (!report.Entries.TryGetValue(name, out var entry)) continue;
                if (!checks.TryGetValue(name, out var check)) checks.Add(name, check = new CheckCapture(name));
                check.Status = Enum.IsDefined(entry.Status) ? entry.Status.ToString() : null;
                var milliseconds = entry.Duration.TotalMilliseconds;
                check.DurationMs = double.IsFinite(milliseconds) && milliseconds >= 0 ? Math.Round(milliseconds, 2) : null;
                check.ExceptionType = entry.Exception?.GetType().FullName;
                if (name == "persistence")
                    check.Pending = PendingKeys.Select((key, index) =>
                    {
                        var present = entry.Data.TryGetValue(key, out var value);
                        var valid = present && value is int count && count >= 0;
                        return new SafePendingRead(PendingLabels[index], present, valid, valid ? (int)value! : null);
                    }).ToArray();
            }
        }
    }

    internal void Observe(string name, int eventId, IReadOnlyDictionary<string, object?> fields, string? exceptionType)
    {
        lock (gate)
        {
            if (!checks.TryGetValue(name, out var check)) checks.Add(name, check = new CheckCapture(name));
            if (eventId == 105)
            {
                if (name != "persistence") return;
                check.DataEventObserved = true;
                check.Pending = PendingKeys.Select((key, index) =>
                {
                    var present = fields.TryGetValue(key, out var value);
                    var valid = present && value is int count && count >= 0;
                    return new SafePendingRead(PendingLabels[index], present, valid, valid ? (int)value! : null);
                }).ToArray();
                return;
            }
            check.CompletionEvents = Math.Min(check.CompletionEvents + 1, 32);
            check.Status = fields.TryGetValue("HealthStatus", out var status) && status is HealthStatus typed
                && Enum.IsDefined(typed) ? typed.ToString() : eventId == 104 ? "Unhealthy" : null;
            check.DurationMs = fields.TryGetValue("ElapsedMilliseconds", out var elapsed) && elapsed is double milliseconds
                && double.IsFinite(milliseconds) && milliseconds >= 0 ? Math.Round(milliseconds, 2) : null;
            check.ExceptionType = exceptionType;
        }
    }

    internal SafeReadyRequest Snapshot(int responseStatus, TimeSpan duration, bool nextCompleted)
    {
        lock (gate)
            return new("r108-observed-health-request.r4", DateTimeOffset.UtcNow, requestId, responseStatus,
                Math.Round(duration.TotalMilliseconds, 2), nextCompleted, checks.Values.Any(x => x.CompletionEvents > 0),
                checks.Values.OrderBy(x => x.Name, StringComparer.Ordinal).Select(x => new SafeCheckObservation(
                    x.Name, x.Status, x.DurationMs, x.ExceptionType, x.CompletionEvents, x.DataEventObserved, x.Pending)).ToArray())
            {
                HealthReportObserved = healthReportObserved,
                HealthReportStatus = healthReportStatus
            };
    }

    private sealed class CheckCapture(string name)
    {
        internal string Name { get; } = name;
        internal string? Status { get; set; }
        internal double? DurationMs { get; set; }
        internal string? ExceptionType { get; set; }
        internal int CompletionEvents { get; set; }
        internal bool DataEventObserved { get; set; }
        internal SafePendingRead[] Pending { get; set; } = [];
    }
}

internal sealed record SafePendingRead(string Name, bool KeyPresent, bool NonnegativeInteger, int? PendingMigrations);
internal sealed record SafeCheckObservation(string Name, string? Status, double? DurationMs, string? ExceptionType,
    int CompletionEvents, bool DataEventObserved, IReadOnlyList<SafePendingRead> PendingReads);
internal sealed record SafeReadyRequest(string SchemaVersion, DateTimeOffset AtUtc, string RequestId, int ResponseStatus,
    double DurationMs, bool NextCompleted, bool HealthCheckCompletionEventsObserved, IReadOnlyList<SafeCheckObservation> Checks)
{
    public bool HealthReportObserved { get; init; }
    public string? HealthReportStatus { get; init; }
}

internal static class R108ReadyObservationJournal
{
    private static readonly object FileGate = new();
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    internal static bool TryWrite(string artifactRoot, SafeReadyRequest record, string? sessionId = null)
    {
        try
        {
            sessionId ??= R108ReadyObservationState.ProcessSessionId;
            if (!Guid.TryParseExact(sessionId, "N", out _)) return false;
            var failed = !record.NextCompleted || record.ResponseStatus >= 400
                || (record.HealthReportObserved && record.HealthReportStatus != "Healthy");
            var healthy = record.NextCompleted && record.ResponseStatus is >= 200 and < 300
                && record.HealthReportObserved && record.HealthReportStatus == "Healthy";
            var category = failed ? "nonhealthy" : healthy ? "healthy" : "unobserved";
            var limit = failed ? 16 : healthy ? 2 : 4;
            var directory = Path.Combine(artifactRoot, ".private", "readiness-request-journal", "r4-" + sessionId);
            lock (FileGate)
            {
                Directory.CreateDirectory(directory);
                if (Directory.EnumerateFiles(directory, $"ready-{category}-*.json").Take(limit).Count() >= limit)
                    return false;
                var path = Path.Combine(directory,
                    $"ready-{category}-{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfffZ}-{record.RequestId}.json");
                using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
                JsonSerializer.Serialize(stream, record, JsonOptions);
                return true;
            }
        }
        catch (Exception) { return false; }
    }
}
