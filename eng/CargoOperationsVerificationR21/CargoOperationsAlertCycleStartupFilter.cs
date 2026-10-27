using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Ssalddel.Contracts.Admin.Operations;
using Ssalddel.Domain.Notifications;
using Ssalddel.Services.OperationsAlerts;
using 살뜰.Data;
using 살뜰.Services.Options;

/// <summary>새 r21 DB에서만 합성 관측→확인→해소 재관측을 한 번 명시 실행합니다. 원인 업무·발송·worker를 실행하지 않습니다.</summary>
internal sealed class CargoOperationsAlertCycleStartupFilter(CargoJourneySettings settings) : IStartupFilter
{
    internal const string Route = "/verification/r21/operations-alert-cycle";
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use(async (context, following) =>
        {
            if (context.Request.Path != Route) { await following(context); return; }
            context.Response.Headers.CacheControl = "private, no-store";
            context.Response.Headers.XContentTypeOptions = "nosniff";
            if (!Authorized(context)) { context.Response.StatusCode = 401; return; }
            if (!HttpMethods.IsPost(context.Request.Method)) { context.Response.StatusCode = 405; context.Response.Headers.Allow = "POST"; return; }
            var held = false;
            try
            {
                if (context.Request.Query.Count != 0 || context.Request.ContentLength is null or <= 0 or > 512
                    || context.Request.Headers.ContainsKey("Transfer-Encoding")) throw new InvalidOperationException("R21AlertCycleInputInvalid");
                using var input = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: context.RequestAborted);
                if (input.RootElement.ValueKind != JsonValueKind.Object) throw new InvalidOperationException("R21AlertCycleInputInvalid");
                var fields = input.RootElement.EnumerateObject().ToArray();
                if (fields.Length != 1 || fields[0].Name != "requestIdempotencyKey" || fields[0].Value.ValueKind != JsonValueKind.String
                    || !Guid.TryParseExact(fields[0].Value.GetString(), "D", out var key) || key == Guid.Empty
                    || fields[0].Value.GetString() != key.ToString("D")) throw new InvalidOperationException("R21AlertCycleInputInvalid");
                held = await Gate.WaitAsync(0, context.RequestAborted);
                if (!held) { context.Response.StatusCode = 409; return; }
                var services = context.RequestServices;
                var configuration = services.GetRequiredService<IConfiguration>();
                settings.ValidateEffectiveConfiguration(configuration, services.GetRequiredService<IHostEnvironment>());
                var execution = services.GetRequiredService<ISsalddelExecutionModePolicy>();
                var options = services.GetRequiredService<IOptionsMonitor<OperationsAlertsOptions>>().CurrentValue;
                if (!execution.IsSimulation || execution.IsOperational || CargoJourneySettings.HostPort != 5369
                    || CargoJourneySettings.DatabaseName != "mirror_operations_r21_20261022"
                    || !settings.RunStableId.StartsWith("cargo-operations-r21:", StringComparison.Ordinal)
                    || options.Enabled || options.ExternalSendEnabled || configuration["DriverPayoutBatch:Enabled"] != "false")
                    throw new InvalidOperationException("R21AlertCycleBoundaryMismatch");
                var db = services.GetRequiredService<SsalddelContext>();
                settings.ValidateSqlConnection(db.Database.GetDbConnection().ConnectionString);
                var allowed = new[] { CargoJourneySeed.ShipperId, CargoJourneySeed.DriverId, CargoJourneySeed.WarehouseUserId,
                    CargoJourneySeed.AdminId, R114Seed.SecondDriverId };
                var users = await db.Users.AsNoTracking().Select(x => new { x.Id, x.UserName }).ToArrayAsync(context.RequestAborted);
                if (users.Length != allowed.Length || users.Any(x => x.Id != x.UserName || !allowed.Contains(x.Id, StringComparer.Ordinal)))
                    throw new InvalidOperationException("R21AlertCycleSyntheticAccountsRequired");
                var directory = System.IO.Path.Combine(settings.ArtifactRoot, ".private", "operations-alert-cycle");
                Directory.CreateDirectory(directory);
                var path = System.IO.Path.Combine(directory, Hash(settings.RunStableId) + ".json");
                var finished = System.IO.Path.Combine(directory, Hash(settings.RunStableId) + ".result.json");
                var attempt = new Attempt("r21-alert-cycle-attempt.v1", Hash(settings.RunStableId), Hash(key.ToString("D")));
                if (File.Exists(path))
                {
                    if (JsonSerializer.Deserialize<Attempt>(File.ReadAllBytes(path), Json) != attempt || !File.Exists(finished))
                    { context.Response.StatusCode = 409; await context.Response.WriteAsJsonAsync(new { code = "R21AlertCyclePriorAttemptRequiresReview" }); return; }
                    using var cached = JsonDocument.Parse(File.ReadAllBytes(finished));
                    await context.Response.WriteAsJsonAsync(new { replay = true, result = cached.RootElement.Clone() });
                    return;
                }
                var sourceId = "r21-synthetic:" + Hash(settings.RunStableId);
                var alertId = OperationsAlertCoordinator.Id("monitor", sourceId, "SourceReadFailed");
                if (await db.Set<OperationsAlert>().AnyAsync(x => x.Id == alertId, context.RequestAborted))
                    throw new InvalidOperationException("R21AlertCycleUnexpectedPriorState");
                var outboxBefore = await db.Set<OperationsAlertOutbox>().CountAsync(context.RequestAborted);
                SaveNew(path, attempt); // 미확정 시 새 명령이나 자동 반복으로 알림 상태를 덮지 않습니다.
                var coordinator = services.GetRequiredService<OperationsAlertCoordinator>();
                var now = coordinator.Now;
                await coordinator.ObserveAsync(new("monitor", sourceId, "SourceReadFailed", "Warning", true, now), now, context.RequestAborted);
                var before = (await coordinator.ListAsync("Unresolved", 1, context.RequestAborted)).Items.Single(x => x.Id == alertId);
                await coordinator.AcknowledgeAsync(alertId, CargoJourneySeed.AdminId, new(key, before.Revision), context.RequestAborted);
                var afterAck = (await coordinator.ListAsync("Unresolved", 1, context.RequestAborted)).Items.Single(x => x.Id == alertId);
                await coordinator.ObserveAsync(new("monitor", sourceId, "SourceReadFailed", "Warning", false, coordinator.Now), now, context.RequestAborted);
                var resolved = await coordinator.DetailAsync(alertId, context.RequestAborted);
                var afterClear = await coordinator.ListAsync("Unresolved", 1, context.RequestAborted);
                var outboxAfter = await db.Set<OperationsAlertOutbox>().CountAsync(context.RequestAborted);
                var passed = before.Status == "Open" && afterAck.Status == "Acknowledged"
                    && afterAck.Handling?.StateCode == "AcknowledgedUnresolved" && resolved?.Status == "Resolved"
                    && afterClear.Items.All(x => x.Id != alertId) && outboxBefore == outboxAfter;
                var result = new { schemaVersion = "r21-alert-cycle-result.v1", passed, synthetic = true,
                    manualObservationOnly = true, sourceWasSyntheticObservation = true,
                    openStatus = before.Status, afterAcknowledgementStatus = afterAck.Status,
                    acknowledgedStillUnresolved = afterAck.Handling?.StateCode == "AcknowledgedUnresolved",
                    resolvedStatus = resolved?.Status, unresolvedAfterClear = afterClear.Items.Any(x => x.Id == alertId),
                    nextOperatorReviewAtUtc = afterAck.Handling?.NextOperatorReviewAtUtc,
                    sourceRevalidationAtUtc = afterAck.Handling?.NextRevalidationAtUtc,
                    notificationOutboxDelta = outboxAfter - outboxBefore, automaticWorkersEnabled = false,
                    externalSendEnabled = false, actualBusinessCauseResolved = false, actualTransferExecuted = false };
                SaveNew(finished, result);
                await context.Response.WriteAsJsonAsync(new { replay = false, result });
            }
            catch (JsonException) { context.Response.StatusCode = 400; await context.Response.WriteAsJsonAsync(new { code = "R21AlertCycleInputInvalid" }); }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
            { if (!context.Response.HasStarted) context.Response.StatusCode = 409; }
            catch (Exception error) when (error is not OutOfMemoryException)
            {
                if (context.Response.HasStarted) return;
                context.Response.StatusCode = error is InvalidOperationException ? 409 : 503;
                await context.Response.WriteAsJsonAsync(new { code = "R21AlertCycleRequiresReview", automaticRetry = false }, CancellationToken.None);
            }
            finally { if (held) Gate.Release(); }
        });
        next(app);
    };

    private bool Authorized(HttpContext context)
        => context.Connection.RemoteIpAddress is { } remote && IPAddress.IsLoopback(remote)
            && context.Request.Scheme == "http" && context.Request.Host.Value == CargoJourneySettings.BaseUrl[7..]
            && (context.Request.Headers.Origin.Count == 0 || context.Request.Headers.Origin.Count == 1 && context.Request.Headers.Origin[0] == CargoJourneySettings.BaseUrl)
            && context.Request.Headers.TryGetValue("x-r114-key", out var values) && values.Count == 1
            && values[0] is { Length: > 0 and <= 4096 } key
            && CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(key)), SHA256.HashData(Encoding.UTF8.GetBytes(settings.AccessKey)));
    private static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static void SaveNew<T>(string path, T value)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        JsonSerializer.Serialize(stream, value, Json); stream.Flush(true);
    }
    private sealed record Attempt(string Schema, string RunHash, string CommandHash);
}
