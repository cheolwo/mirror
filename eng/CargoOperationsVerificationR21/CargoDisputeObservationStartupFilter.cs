using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Ssalddel.Services.PrivacySupport;
using 살뜰.Data;
using 살뜰.Services.Options;
using 살뜰.도메인.공통;

/// <summary>r21 전용 합성 원천의 실제 Mongo 분쟁 판정만 읽습니다. 지급 준비나 업무 상태를 변경하지 않습니다.</summary>
internal sealed class CargoDisputeObservationStartupFilter(CargoJourneySettings settings) : IStartupFilter
{
    internal const string Path = "/verification/r21/payout-dispute";
    private const string RegionHash = "737d57de3bc595aaafaba81b61044f61d76902c0bf3d2391899ac471837a973b";
    private static readonly Regex MarkerPattern = new(
        "R114_AI_LEGAL_DONG/pickup=([0-9]{10});dropoff=([0-9]{10})(?:;|\\s|$)", RegexOptions.CultureInvariant);
    private static readonly string[] AllowedDrivers = [CargoJourneySeed.DriverId, R114Seed.SecondDriverId];

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use(async (context, nextMiddleware) =>
        {
            if (context.Request.Path != Path) { await nextMiddleware(context); return; }
            context.Response.Headers.CacheControl = "private, no-store";
            context.Response.Headers.XContentTypeOptions = "nosniff";
            if (!Authorized(context)) { context.Response.StatusCode = StatusCodes.Status401Unauthorized; return; }
            if (!HttpMethods.IsGet(context.Request.Method))
            {
                context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
                context.Response.Headers.Allow = "GET";
                return;
            }
            try
            {
                if (context.Request.Query.Count != 1 || !context.Request.Query.TryGetValue("requestId", out var values)
                    || values.Count != 1 || values[0] is not { } requestId
                    || !Guid.TryParseExact(requestId, "D", out var id) || id == Guid.Empty
                    || requestId != id.ToString("D") || context.Request.ContentLength is > 0
                    || context.Request.Headers.ContainsKey("Transfer-Encoding"))
                    throw Failure("R21ObservationInputInvalid", StatusCodes.Status400BadRequest);

                var services = context.RequestServices;
                var configuration = services.GetRequiredService<IConfiguration>();
                settings.ValidateEffectiveConfiguration(configuration, services.GetRequiredService<IHostEnvironment>());
                var execution = services.GetRequiredService<ISsalddelExecutionModePolicy>();
                var mongo = services.GetRequiredService<IOptions<MongoDbOptions>>().Value;
                if (!execution.IsSimulation || execution.IsOperational
                    || CargoJourneySettings.HostPort != 5369 || CargoJourneySettings.DatabaseName != "mirror_operations_r21_20261022"
                    || configuration["MongoDb:ConnectionString"] != CargoJourneySettings.MongoConnection
                    || mongo.Database != CargoJourneySettings.DatabaseName
                    || !settings.RunStableId.StartsWith("cargo-operations-r21:", StringComparison.Ordinal))
                    throw Failure("R21ObservationBoundaryMismatch", StatusCodes.Status503ServiceUnavailable);

                var db = services.GetRequiredService<SsalddelContext>();
                settings.ValidateSqlConnection(db.Database.GetDbConnection().ConnectionString);
                var request = await db.화주운송의뢰.AsNoTracking().Where(x => x.의뢰Id == requestId)
                    .Select(x => new { x.의뢰Id, x.화주Id, x.주문자UserId, x.화물종류, x.요청사항, x.화물설명 })
                    .SingleOrDefaultAsync(context.RequestAborted);
                var transport = await db.운송원장.AsNoTracking().Where(x => x.의뢰Id == requestId)
                    .Select(x => new { x.의뢰Id, x.화주Id, x.배차업무유형, x.확정기사Id, x.기사_운송자, x.메모 })
                    .SingleOrDefaultAsync(context.RequestAborted);
                if (request is null || transport is null
                    || request.의뢰Id != requestId || transport.의뢰Id != requestId
                    || request.화주Id != CargoJourneySeed.ShipperId || request.주문자UserId != CargoJourneySeed.ShipperId
                    || transport.화주Id != CargoJourneySeed.ShipperId || transport.배차업무유형 != 상태값.배차업무유형.용달운송
                    || request.화물종류?.Contains("R114", StringComparison.Ordinal) != true
                    || !AllowedDriver(transport.확정기사Id) || !AllowedDriver(transport.기사_운송자)
                    || transport.확정기사Id is { Length: > 0 } confirmed && transport.기사_운송자 is { Length: > 0 } actual && confirmed != actual)
                    throw Failure("R21SyntheticSourceNotConfirmed", StatusCodes.Status404NotFound);

                ValidateMarker([request.요청사항, request.화물설명, transport.메모]);
                var expectedUsers = new[] { CargoJourneySeed.ShipperId, CargoJourneySeed.DriverId,
                    CargoJourneySeed.WarehouseUserId, CargoJourneySeed.AdminId, R114Seed.SecondDriverId };
                var accounts = await db.Users.AsNoTracking().Select(x => new { x.Id, x.UserName }).ToArrayAsync(context.RequestAborted);
                if (accounts.Length != expectedUsers.Length || accounts.Any(x => x.Id != x.UserName
                    || !expectedUsers.Contains(x.Id, StringComparer.Ordinal)))
                    throw Failure("R21SyntheticPartiesNotConfirmed", StatusCodes.Status503ServiceUnavailable);

                var store = services.GetRequiredService<I보호지원Store>();
                var query = services.GetRequiredService<I화물분쟁지급보류Query>();
                if (store.GetType() != typeof(Mongo보호지원Store) || query.GetType() != typeof(보호지원화물지급보류Query))
                    throw Failure("R21DisputeReaderNotCanonical", StatusCodes.Status503ServiceUnavailable);
                var decision = await query.조회Async(requestId, context.RequestAborted);
                await context.Response.WriteAsJsonAsync(new
                {
                    schemaVersion = "r21-payout-dispute-observation.v1", synthetic = true, observationOnly = true,
                    sourceVerified = true, executionMode = "Simulation", storeType = store.GetType().Name, queryType = query.GetType().Name,
                    hold = decision.Hold, code = decision.Code, observedAtUtc = DateTimeOffset.UtcNow,
                    payoutReadinessEvaluated = false, businessMutation = false, actualTransferExecuted = false
                }, context.RequestAborted);
            }
            catch (ObservationException failure)
            {
                context.Response.StatusCode = failure.Status;
                await context.Response.WriteAsJsonAsync(new { code = failure.Code, observationOnly = true }, CancellationToken.None);
            }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
            { if (!context.Response.HasStarted) context.Response.StatusCode = StatusCodes.Status409Conflict; }
            catch (Exception error) when (error is not OutOfMemoryException)
            {
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                await context.Response.WriteAsJsonAsync(new { code = "R21ObservationUnavailable", observationOnly = true }, CancellationToken.None);
            }
        });
        next(app);
    };

    private bool Authorized(HttpContext context)
    {
        var origin = $"http://127.0.0.1:{CargoJourneySettings.HostPort}";
        if (context.Connection.RemoteIpAddress is not { } remote || !IPAddress.IsLoopback(remote)
            || context.Request.Scheme != "http" || context.Request.Host.Value != "127.0.0.1:5369"
            || context.Request.Headers.Origin.Count > 0 && (context.Request.Headers.Origin.Count != 1 || context.Request.Headers.Origin[0] != origin)
            || !context.Request.Headers.TryGetValue("x-r114-key", out var supplied) || supplied.Count != 1
            || supplied[0] is not { Length: > 0 and <= 4096 } key || string.IsNullOrWhiteSpace(settings.AccessKey)) return false;
        return CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(key)),
            SHA256.HashData(Encoding.UTF8.GetBytes(settings.AccessKey)));
    }

    private static bool AllowedDriver(string? driver) => string.IsNullOrEmpty(driver) || AllowedDrivers.Contains(driver, StringComparer.Ordinal);

    private void ValidateMarker(string?[] sources)
    {
        var tags = sources.Where(x => x is not null).SelectMany(x => MarkerPattern.Matches(x!).Cast<Match>())
            .Select(x => (Pickup: x.Groups[1].Value, Dropoff: x.Groups[2].Value)).Distinct().ToArray();
        if (tags.Length != 1) throw Failure("R21SyntheticSourceNotConfirmed", StatusCodes.Status404NotFound);
        var bytes = File.ReadAllBytes(System.IO.Path.Combine(settings.RepositoryRoot,
            "eng/local-dispatch-ai/cargo-regional-training/legal-dong-regions.json"));
        if (!Convert.ToHexString(SHA256.HashData(bytes)).Equals(RegionHash, StringComparison.OrdinalIgnoreCase))
            throw Failure("R21RegionProjectionChanged", StatusCodes.Status503ServiceUnavailable);
        using var projection = JsonDocument.Parse(bytes);
        var codes = projection.RootElement.GetProperty("regions").EnumerateArray()
            .Select(x => x.GetProperty("legalDongCode").GetString()).ToHashSet(StringComparer.Ordinal);
        if (codes.Count != 15 || !codes.Contains(tags[0].Pickup) || !codes.Contains(tags[0].Dropoff))
            throw Failure("R21SyntheticSourceNotConfirmed", StatusCodes.Status404NotFound);
    }

    private static ObservationException Failure(string code, int status) => new(code, status);
    private sealed class ObservationException(string code, int status) : Exception
    {
        public string Code { get; } = code;
        public int Status { get; } = status;
    }
}
