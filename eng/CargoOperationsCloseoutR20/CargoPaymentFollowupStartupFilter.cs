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
using Ssalddel.Application.Shipper.Payment;
using Ssalddel.Application.Shipper.Payment.Events;
using Ssalddel.Services.Outbox;
using 살뜰.Data;
using 살뜰.Services.Payments;
using 살뜰.Services.Options;
using 살뜰.도메인.결제;
using 살뜰.도메인.공통;

/// <summary>작업자가 명시한 합성 결제 한 건의 기존 Outbox를 한 번 발행한다. Worker와 실제 결제는 켜지 않는다.</summary>
internal sealed class CargoPaymentFollowupStartupFilter(CargoJourneySettings settings) : IStartupFilter
{
    internal const string Route = "/verification/r20/payment-followup";
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly Regex Marker = new("R114_AI_LEGAL_DONG/pickup=[0-9]{10};dropoff=[0-9]{10}(?:;|\\s|$)", RegexOptions.CultureInvariant);

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use(async (context, following) =>
        {
            if (context.Request.Path != Route) { await following(context); return; }
            context.Response.Headers.CacheControl = "private, no-store";
            context.Response.Headers.XContentTypeOptions = "nosniff";
            if (!Authorized(context)) { context.Response.StatusCode = 401; return; }
            if (!HttpMethods.IsPost(context.Request.Method))
            { context.Response.StatusCode = 405; context.Response.Headers.Allow = "POST"; return; }
            var held = false;
            try
            {
                var ct = context.RequestAborted;
                if (context.Request.Query.Count != 0 || context.Request.ContentLength is not (> 0 and <= 1024)
                    || context.Request.Headers.ContainsKey("Transfer-Encoding")
                    || !System.Net.Http.Headers.MediaTypeHeaderValue.TryParse(context.Request.ContentType, out var type)
                    || !string.Equals(type.MediaType, "application/json", StringComparison.OrdinalIgnoreCase))
                    throw Fail("R20FollowupInputInvalid", 400);
                using var input = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: ct);
                if (input.RootElement.ValueKind != JsonValueKind.Object) throw Fail("R20FollowupInputInvalid", 400);
                var fields = input.RootElement.EnumerateObject().ToArray();
                if (fields.Length != 2 || fields.Select(x => x.Name).Distinct(StringComparer.Ordinal).Count() != 2
                    || !input.RootElement.TryGetProperty("requestId", out var id) || id.ValueKind != JsonValueKind.String
                    || !input.RootElement.TryGetProperty("requestIdempotencyKey", out var key) || key.ValueKind != JsonValueKind.String)
                    throw Fail("R20FollowupInputInvalid", 400);
                var requestId = id.GetString()!; var commandKey = key.GetString()!;
                if (!Guid.TryParseExact(requestId, "D", out var parsedId) || parsedId == Guid.Empty || requestId != parsedId.ToString("D")
                    || !Guid.TryParseExact(commandKey, "D", out var parsedKey) || parsedKey == Guid.Empty || commandKey != parsedKey.ToString("D"))
                    throw Fail("R20FollowupInputInvalid", 400);
                held = await Gate.WaitAsync(0, ct);
                if (!held) throw Fail("R20FollowupBusy", 409);
                await ProcessAsync(context, requestId, commandKey, ct);
            }
            catch (FollowupException failure) { await ReplyAsync(context, failure.Status, failure.Code, false, false, false); }
            catch (JsonException) { await ReplyAsync(context, 400, "R20FollowupInputInvalid", false, false, false); }
            catch (Exception error) when (error is not OutOfMemoryException)
            { await ReplyAsync(context, 409, "R20FollowupResultRequiresReview", null, null, context.Items.ContainsKey("r20-payment-followup-started")); }
            finally { if (held) Gate.Release(); }
        });
        next(app);
    };

    private async Task ProcessAsync(HttpContext context, string requestId, string commandKey, CancellationToken ct)
    {
        var services = context.RequestServices;
        var configuration = services.GetRequiredService<IConfiguration>();
        settings.ValidateEffectiveConfiguration(configuration, services.GetRequiredService<IHostEnvironment>());
        var execution = services.GetRequiredService<ISsalddelExecutionModePolicy>();
        if (!execution.IsSimulation || execution.IsOperational || CargoJourneySettings.HostPort != 5368
            || CargoJourneySettings.DatabaseName != "mirror_operations_r20r2_20261022"
            || configuration["MongoDb:Database"] != CargoJourneySettings.DatabaseName
            || configuration["TransportPrepayment:Enabled"] != "true"
            || configuration["DriverPayoutBatch:Enabled"] != "false") throw Fail("R20FollowupBoundaryMismatch", 503);
        var db = services.GetRequiredService<SsalddelContext>();
        settings.ValidateSqlConnection(db.Database.GetDbConnection().ConnectionString);
        var request = await db.화주운송의뢰.AsNoTracking().SingleOrDefaultAsync(x => x.의뢰Id == requestId, ct);
        if (request is null || request.화주Id != CargoJourneySeed.ShipperId || request.주문자UserId != CargoJourneySeed.ShipperId
            || request.모의처리처수행 || request.화물종류?.Contains("R114", StringComparison.Ordinal) != true
            || !new[] { request.요청사항, request.화물설명 }.Any(value => value is not null && Marker.IsMatch(value))
            || request.정산시점 != "선결제" || request.결제상태 != 상태값.결제상태.결제완료)
            throw Fail("R20FollowupSyntheticSourceNotConfirmed", 404);
        var allowedUsers = new[] { CargoJourneySeed.ShipperId, CargoJourneySeed.DriverId, CargoJourneySeed.WarehouseUserId,
            CargoJourneySeed.AdminId, R114Seed.SecondDriverId };
        var users = await db.Users.AsNoTracking().Select(x => new { x.Id, x.UserName }).ToArrayAsync(ct);
        if (users.Length != allowedUsers.Length || users.Any(x => x.Id != x.UserName || !allowedUsers.Contains(x.Id, StringComparer.Ordinal)))
            throw Fail("R20FollowupSyntheticPartiesNotConfirmed", 503);
        var payments = await db.결제.AsNoTracking().Where(x => x.의뢰Id == requestId).ToArrayAsync(ct);
        if (payments.Length != 1) throw Fail("R20FollowupPaymentRequiresReview", 409);
        var payment = payments[0];
        if (payment.화주Id != CargoJourneySeed.ShipperId || payment.대상Id != requestId
            || payment.결제대상유형 != 결제공통정의.결제대상유형.용달운송의뢰
            || payment.PG사 != "FakePG" || payment.결제제공자 != 결제공통정의.결제제공자.FakePG
            || payment.결제상태 != 상태값.결제상태.결제완료 || payment.공통결제상태 != 결제공통정의.결제상태.승인완료
            || payment.취소일시 is not null || payment.승인일시 is null || payment.통화 != "KRW" || payment.결제금액 <= 0
            || 배송료결제준비근거Policy.현재근거검증(payment, request).IsFailed)
            throw Fail("R20FollowupFakePaymentNotConfirmed", 409);
        var outbox = await db.결제승인완료Outbox.AsNoTracking().SingleOrDefaultAsync(x => x.결제레코드Id == payment.Id, ct);
        if (outbox is null || outbox.결제Id != payment.결제Id || outbox.대상Id != requestId
            || outbox.결제대상유형 != payment.결제대상유형 || outbox.결제제공자 != payment.결제제공자
            || outbox.결제금액 != payment.결제금액 || outbox.통화 != payment.통화)
            throw Fail("R20FollowupOutboxNotConfirmed", 409);
        var payload = JsonSerializer.Deserialize<결제승인완료Event>(outbox.PayloadJson, Json);
        if (payload is null || payload.결제레코드Id != payment.Id || payload.결제Id != payment.결제Id
            || payload.대상Id != requestId || payload.결제대상유형 != payment.결제대상유형
            || payload.결제제공자 != payment.결제제공자 || payload.결제금액 != payment.결제금액 || payload.통화 != payment.통화)
            throw Fail("R20FollowupOutboxPayloadMismatch", 409);

        var directory = System.IO.Path.Combine(settings.ArtifactRoot, ".private", "payment-followup");
        Directory.CreateDirectory(directory);
        var path = System.IO.Path.Combine(directory, Hash(requestId) + ".json");
        var attempt = new Attempt("r20-payment-followup-command.v1", Hash(settings.RunStableId), Hash(requestId), Hash(commandKey), payment.Id, outbox.Id);
        if (File.Exists(path) && JsonSerializer.Deserialize<Attempt>(File.ReadAllBytes(path), Json) != attempt)
            throw Fail("R20FollowupIdempotencyConflict", 409);
        if (outbox.처리상태 == OutboxProcessingStatuses.Succeeded)
        {
            if (!File.Exists(path)) SaveAttempt(path, attempt);
            var queue = await QueueConfirmedAsync(db, requestId, ct);
            await ReplyAsync(context, queue ? 200 : 409, queue ? "R20FollowupAlreadySucceeded" : "R20FollowupQueueRequiresReview", true, queue, false);
            return;
        }
        // Started가 남거나 이전 시도가 있는 Pending/Processing/Failed는 자동 재발행하지 않는다.
        if (File.Exists(path) || outbox.처리상태 != OutboxProcessingStatuses.Pending || outbox.시도횟수 != 0 || outbox.마지막시도시각 is not null)
            throw Fail("R20FollowupPriorAttemptRequiresReview", 409);
        var now = DateTime.UtcNow;
        var retryCutoff = now - OutboxProcessingPolicy.RetryDelay;
        var leaseCutoff = now - OutboxProcessingPolicy.LeaseTimeout;
        var eligible = await db.결제승인완료Outbox.AsNoTracking().Where(x =>
                x.처리상태 == OutboxProcessingStatuses.Pending && (x.시도횟수 == 0 || x.UpdatedAt <= retryCutoff)
                || x.처리상태 == OutboxProcessingStatuses.Processing && x.UpdatedAt <= leaseCutoff)
            .OrderBy(x => x.CreatedAt).Select(x => x.Id).Take(2).ToArrayAsync(ct);
        if (eligible.Length != 1 || eligible[0] != outbox.Id) throw Fail("R20FollowupOtherOutboxRequiresReview", 409);
        ct.ThrowIfCancellationRequested();
        // 새 제품 원장 없이, 발행 호출 전에 결과 미확정 경계를 비공개 파일에 내구성 있게 남긴다.
        SaveAttempt(path, attempt);
        ct.ThrowIfCancellationRequested();
        context.Items["r20-payment-followup-started"] = true;
        var processed = await services.GetRequiredService<I결제승인완료OutboxService>().대기이벤트발행Async(take: 1, cancellationToken: ct);
        db.ChangeTracker.Clear();
        var succeeded = await db.결제승인완료Outbox.AsNoTracking().AnyAsync(x => x.Id == outbox.Id
            && x.결제레코드Id == payment.Id && x.처리상태 == OutboxProcessingStatuses.Succeeded, ct);
        var queueConfirmed = succeeded && await QueueConfirmedAsync(db, requestId, ct);
        await ReplyAsync(context, processed == 1 && queueConfirmed ? 200 : 409,
            processed == 1 && queueConfirmed ? "R20FollowupSucceeded" : "R20FollowupResultRequiresReview", succeeded, queueConfirmed, true);
    }

    private static Task<bool> QueueConfirmedAsync(SsalddelContext db, string requestId, CancellationToken ct)
        => db.운송원장.AsNoTracking().AnyAsync(x => x.의뢰Id == requestId && x.화주Id == CargoJourneySeed.ShipperId
            && x.배차업무유형 == 상태값.배차업무유형.용달운송, ct);

    private bool Authorized(HttpContext context)
    {
        if (context.Connection.RemoteIpAddress is not { } remote || !IPAddress.IsLoopback(remote)
            || context.Request.Scheme != "http" || context.Request.Host.Value != "127.0.0.1:5368"
            || context.Request.Headers.Origin.Count > 0 && (context.Request.Headers.Origin.Count != 1 || context.Request.Headers.Origin[0] != CargoJourneySettings.BaseUrl)
            || !context.Request.Headers.TryGetValue("x-r114-key", out var supplied) || supplied.Count != 1
            || supplied[0] is not { Length: > 0 and <= 4096 } key || string.IsNullOrWhiteSpace(settings.AccessKey)) return false;
        return CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(key)), SHA256.HashData(Encoding.UTF8.GetBytes(settings.AccessKey)));
    }
    private static async Task ReplyAsync(HttpContext context, int status, string code, bool? succeeded, bool? queueConfirmed, bool called)
    {
        if (context.Response.HasStarted) return;
        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(new { schemaVersion = "r20-payment-followup-result.v1", code,
            synthetic = true, executionMode = "Simulation", followupSucceeded = succeeded, dispatchQueueConfirmed = queueConfirmed,
            followupCallStarted = called, verificationHeld = status != 200, automaticWorkersEnabled = false,
            actualCollection = false, actualTransferExecuted = false }, CancellationToken.None);
    }
    private static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static void SaveAttempt(string path, Attempt attempt)
    {
        using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        JsonSerializer.Serialize(file, attempt, Json); file.Flush(true);
    }
    private static FollowupException Fail(string code, int status) => new(code, status);
    private sealed record Attempt(string Schema, string RunHash, string RequestHash, string KeyHash, long PaymentId, long OutboxId);
    private sealed class FollowupException(string code, int status) : Exception
    { internal string Code { get; } = code; internal int Status { get; } = status; }
}
