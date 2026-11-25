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
using Ssalddel.Contracts.Food;
using Ssalddel.Services.Community;
using Ssalddel.Services.Food;
using Ssalddel.Services.Outbox;
using 살뜰.Data;
using 살뜰.Services.Options;
using 살뜰.도메인.설정;

/// <summary>
/// R31 한 합성 주문의 이미 존재하는 원장 투영 Outbox 여섯 개만 명시적으로 한 번 처리합니다.
/// 새 업무·새 멱등키·전체 worker·알림/활동/World 투영·게시글 API를 실행하지 않습니다.
/// </summary>
internal sealed class FoodProjectionR31ControlStartupFilter(CargoJourneySettings settings) : IStartupFilter
{
    private const string Route = "/verification/r31/food/projections";
    private const string RunId = "integrated-r108:d1e48b984f5e40368f027925af8c16cc";
    private const string Database = "r108m_d1e48b984f5e40368f027925af8c16cc";
    private const string RunHex = "d1e48b984f5e40368f027925af8c16cc";
    private const string OrderNo = "FOOD-20261029024745747";
    private const string CreateRequestId = "4e5d3abf-bb47-4944-8a3a-6d6e1c621288";
    private static readonly long[] TargetIds = [5, 6, 12, 13, 16, 17];
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use(async (context, nextMiddleware) =>
        {
            if (context.Request.Path != Route) { await nextMiddleware(context); return; }
            if (context.Connection.RemoteIpAddress is not { } remote || !IPAddress.IsLoopback(remote)
                || context.Request.Host.Port != CargoJourneySettings.HostPort || !ValidKey(context))
            { await Error(context, 401, "R31ControlAuthenticationRequired"); return; }
            if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsPost(context.Request.Method))
            { await Error(context, 405, "R31GetOrExplicitPostRequired"); return; }
            if (context.Request.Query.Count != 0 || context.Request.ContentLength is > 4096)
            { await Error(context, 400, "R31BoundedFixedRequestRequired"); return; }

            await Gate.WaitAsync(context.RequestAborted);
            try
            {
                using var scope = context.RequestServices.CreateScope();
                var services = scope.ServiceProvider;
                ValidateConfiguration(services);
                var db = services.GetRequiredService<SsalddelContext>();
                settings.ValidateSqlConnection(db.Database.GetDbConnection().ConnectionString);
                var snapshot = await CaptureAsync(db, context.RequestAborted);
                var attemptPath = Path.Combine(settings.ArtifactRoot, ".private", "r31-food-projection-attempt.json");
                var resultPath = Path.Combine(settings.ArtifactRoot, ".private", "r31-food-projection-result.json");
                var hash = SnapshotHash(snapshot);
                if (HttpMethods.IsGet(context.Request.Method))
                {
                    await context.Response.WriteAsJsonAsync(new
                    {
                        schemaVersion = "r31.food-projection-control.r1", runStableId = RunId,
                        database = Database, orderNo = OrderNo, snapshotSha256 = hash, snapshot,
                        executionAttemptExists = File.Exists(attemptPath), resultExists = File.Exists(resultPath),
                        automaticRetry = false, workersEnabled = false, publicPostCrudCalled = false
                    }, context.RequestAborted);
                    return;
                }

                // A started/uncertain or partially failed call is never repeated, including after restart.
                if (File.Exists(attemptPath))
                {
                    context.Response.StatusCode = 409;
                    await context.Response.WriteAsJsonAsync(new
                    {
                        errorCode = "R31ProjectionAttemptAlreadyExists_ReadCurrentOnly",
                        runStableId = RunId, orderNo = OrderNo, snapshotSha256 = hash, snapshot,
                        resultExists = File.Exists(resultPath), additionalExecution = false
                    }, context.RequestAborted);
                    return;
                }
                var request = await ReadExplicitRequestAsync(context);
                if (request is null || request.OrderNo != OrderNo
                    || request.ExpectedIds is null || !request.ExpectedIds.SequenceEqual(TargetIds)
                    || !string.Equals(request.ExpectedSnapshotSha256, hash, StringComparison.Ordinal))
                { await Error(context, 409, "R31ProjectionSnapshotOrSelectionMismatch"); return; }
                if (snapshot.Targets.Any(x => x.State != OutboxProcessingStatuses.Pending || x.AttemptCount != 0))
                { await Error(context, 409, "R31UnattemptedPendingTargetsRequired"); return; }

                // Capture validates every existing identity/key/payload before the public service is invoked.
                // This immutable file is created before the first mutation and is never removed or replaced.
                await using (var attempt = new FileStream(attemptPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
                    await JsonSerializer.SerializeAsync(attempt, new
                    {
                        status = "StartedOrUncertain", atUtc = DateTime.UtcNow, runStableId = RunId,
                        database = Database, orderNo = OrderNo, snapshotSha256 = hash,
                        targets = snapshot.Targets, automaticRetry = false, explicitRootPost = true
                    }, JsonOptions, context.RequestAborted);

                var completedIds = new List<long>();
                var outbox = services.GetRequiredService<I음식마트원장동기화OutboxService>();
                var store = services.GetRequiredService<ISsalddelFoodOrderStore>();
                string? failureCode = null;
                foreach (var target in snapshot.Targets)
                {
                    context.RequestAborted.ThrowIfCancellationRequested();
                    db.ChangeTracker.Clear();
                    var existing = await db.음식마트원장동기화Outbox.AsNoTracking()
                        .SingleAsync(x => x.Id == target.Id, context.RequestAborted);
                    ValidateTarget(existing, target);
                    if (existing.처리상태 != OutboxProcessingStatuses.Pending || existing.시도횟수 != 0)
                    { failureCode = "R31TargetChangedBeforeSingleInvocation"; break; }
                    var currentOrder = store.GetOrder(OrderNo)
                        ?? throw new InvalidOperationException("R31CurrentFoodOrderUnavailable");
                    if (currentOrder.주문번호 != OrderNo || currentOrder.상태 != 음식주문상태코드.수령확인
                        || currentOrder.배차대기Id != 4)
                        throw new InvalidOperationException("R31CurrentFoodOrderChanged");

                    // Existing key is mandatory. EnqueueAsync returns this exact row, then the product
                    // handler dispatches its existing FoodLedger/TransportLedger payload only.
                    await outbox.음식주문예약후즉시처리Async(currentOrder, existing.변경자, existing.멱등키,
                        context.RequestAborted);
                    db.ChangeTracker.Clear();
                    var after = await db.음식마트원장동기화Outbox.AsNoTracking()
                        .SingleAsync(x => x.Id == target.Id, context.RequestAborted);
                    ValidateTarget(after, target);
                    var count = await db.음식마트원장동기화Outbox.AsNoTracking()
                        .CountAsync(x => x.원천Id == OrderNo, context.RequestAborted);
                    if (count != snapshot.OrderOutboxCount)
                    { failureCode = "R31UnexpectedOutboxCreationObserved_Stop"; break; }
                    if (after.처리상태 != OutboxProcessingStatuses.Succeeded || after.시도횟수 != 1)
                    { failureCode = "R31SingleProjectionProcessingNotSucceeded_NoRetry"; break; }
                    completedIds.Add(target.Id);
                }

                db.ChangeTracker.Clear();
                var final = await CaptureAsync(db, context.RequestAborted);
                if (!snapshot.UnselectedOutboxSha256.Equals(final.UnselectedOutboxSha256, StringComparison.Ordinal))
                    failureCode ??= "R31UnselectedOutboxChanged_Stop";
                if (snapshot.OrderHistoryCount != final.OrderHistoryCount
                    || snapshot.DeliveryAttemptCount != final.DeliveryAttemptCount
                    || snapshot.SettlementCount != final.SettlementCount
                    || snapshot.TransportEventCount != final.TransportEventCount)
                    failureCode ??= "R31CoreBusinessCountsChanged_Stop";
                var result = new
                {
                    status = failureCode is null && completedIds.Count == TargetIds.Length
                        ? "SelectedProductProjectionHandlersCompleted" : "PartialOrFailed_NoAutomaticRetry",
                    atUtc = DateTime.UtcNow, runStableId = RunId, database = Database, orderNo = OrderNo,
                    completedIds, failureCode, before = snapshot, after = final,
                    selectedExistingOutboxesOnly = true, newOutboxAllowed = false,
                    notificationActivityWorldProcessingRequested = false, publicPostCrudCalled = false,
                    workersEnabled = false, independentMongoReadStillRequired = true
                };
                await using (var output = new FileStream(resultPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
                    await JsonSerializer.SerializeAsync(output, result, JsonOptions, context.RequestAborted);
                if (failureCode is not null) context.Response.StatusCode = 409;
                await context.Response.WriteAsJsonAsync(result, context.RequestAborted);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Do not echo raw payload, key, connection string, private actor, or exception message.
                var attempt = Path.Combine(settings.ArtifactRoot, ".private", "r31-food-projection-attempt.json");
                var failure = Path.Combine(settings.ArtifactRoot, ".private", "r31-food-projection-failure.json");
                if (settings.RunStableId == RunId && File.Exists(attempt) && !File.Exists(failure))
                {
                    await using var record = new FileStream(failure, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
                    await JsonSerializer.SerializeAsync(record, new
                    {
                        status = "StoppedOrPartial_NoAutomaticRetry", atUtc = DateTime.UtcNow,
                        runStableId = RunId, orderNo = OrderNo, safeType = exception.GetType().Name,
                        attemptPreserved = true, independentReadRequired = true
                    }, JsonOptions, CancellationToken.None);
                }
                await Error(context, 409, "R31ProjectionControlStopped_NoRetry", exception.GetType().Name);
            }
            finally { Gate.Release(); }
        });
        next(app);
    };

    private bool ValidKey(HttpContext context)
    {
        var supplied = context.Request.Headers["x-r108-key"].ToString();
        return supplied.Length <= 256 && CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(supplied), Encoding.UTF8.GetBytes(settings.AccessKey));
    }

    private void ValidateConfiguration(IServiceProvider services)
    {
        var configuration = services.GetRequiredService<IConfiguration>();
        settings.ValidateEffectiveConfiguration(configuration, services.GetRequiredService<IHostEnvironment>());
        var expectedRoot = Path.GetFullPath(Path.Combine(settings.RepositoryRoot,
            "artifacts", "local", "integrated-workflow-r108", RunHex));
        if (settings.RunStableId != RunId || CargoJourneySettings.DatabaseName != Database
            || !string.Equals(Path.GetFullPath(settings.ArtifactRoot), expectedRoot, StringComparison.OrdinalIgnoreCase)
            || !services.GetRequiredService<ISsalddelExecutionModePolicy>().IsSimulation
            || configuration["MongoDb:Database"] != Database
            || configuration["MongoDb:ConnectionString"] != CargoJourneySettings.MongoConnection)
            throw new InvalidOperationException("R31ExactRunAndSimulationRequired");
    }

    private static async Task<ProjectionSnapshot> CaptureAsync(SsalddelContext db, CancellationToken ct)
    {
        var order = await db.음식주문.AsNoTracking().SingleOrDefaultAsync(x => x.주문번호 == OrderNo, ct);
        if (order is null || order.Id != 1 || order.음식점Id != 101 || order.배차대기Id != 4
            || order.클라이언트요청Id != Guid.Parse(CreateRequestId) || order.상태 != 음식주문상태코드.수령확인
            || order.커뮤니티원장Id != "food-order:" + OrderNo)
            throw new InvalidOperationException("R31FixedCompletedFoodBindingRequired");
        var queue = await db.운송원장.AsNoTracking().SingleAsync(x => x.Id == 4, ct);
        var driverId = await db.Users.AsNoTracking().Where(x => x.UserName == "driver1@ssalddel.local")
            .Select(x => x.Id).SingleAsync(ct);
        if (queue.의뢰Id != OrderNo || queue.원본의뢰Id != OrderNo || queue.확정기사Id != driverId
            || queue.상태 != "인수완료")
            throw new InvalidOperationException("R31FixedFoodTransportRequired");
        var rows = await db.음식마트원장동기화Outbox.AsNoTracking()
            .Where(x => x.원천Id == OrderNo).OrderBy(x => x.Id).ToListAsync(ct);
        var selected = rows.Where(x => TargetIds.Contains(x.Id)).Select(Target).ToArray();
        if (!selected.Select(x => x.Id).SequenceEqual(TargetIds))
            throw new InvalidOperationException("R31ExactSixExistingTargetsRequired");
        foreach (var target in selected) ValidateTarget(rows.Single(x => x.Id == target.Id), target);
        var unselected = rows.Where(x => !TargetIds.Contains(x.Id)).Select(x => new
        { x.Id, state = x.처리상태, attempts = x.시도횟수, payloadSha256 = Sha(x.PayloadJson), keySha256 = Sha(x.멱등키) });
        return new ProjectionSnapshot(RunId, Database, OrderNo, selected, rows.Count,
            Sha(JsonSerializer.Serialize(unselected, JsonOptions)),
            await db.음식주문상태이력.CountAsync(x => x.음식주문Id == order.Id, ct),
            await db.음식배달시도.CountAsync(x => x.주문번호 == OrderNo, ct),
            await db.음식주문기사정산.CountAsync(x => x.주문번호 == OrderNo, ct),
            await db.운송이벤트.CountAsync(x => x.의뢰Id == OrderNo, ct), queue.커뮤니티원장상태);
    }

    private static ProjectionTarget Target(음식마트원장동기화Outbox item)
    {
        using var payload = JsonDocument.Parse(item.PayloadJson);
        var followup = payload.RootElement.GetProperty("driverFollowup");
        return new(item.Id, followup.GetProperty("kind").GetString() ?? "", item.처리상태, item.시도횟수,
            Sha(item.멱등키), Sha(item.PayloadJson));
    }

    private static void ValidateTarget(음식마트원장동기화Outbox item, ProjectionTarget frozen)
    {
        var index = Array.IndexOf(TargetIds, item.Id);
        if (index < 0 || item.Id != frozen.Id || item.동기화유형 != 음식마트원장동기화유형코드.음식주문
            || item.원천Id != OrderNo || !item.멱등키.StartsWith("food-driver-followup:", StringComparison.Ordinal)
            || Sha(item.멱등키) != frozen.KeySha256 || Sha(item.PayloadJson) != frozen.PayloadSha256)
            throw new InvalidOperationException("R31ExistingProjectionIdentityChanged");
        using var document = JsonDocument.Parse(item.PayloadJson);
        var followup = document.RootElement.GetProperty("driverFollowup");
        var kind = followup.GetProperty("kind").GetString();
        var intent = followup.GetProperty("intent");
        var expectedKind = index % 2 == 0 ? "FoodLedger" : "TransportLedger";
        var expectedState = index < 2 ? "기사배정" : index < 4 ? "픽업완료" : "전달완료";
        if (kind != expectedKind || frozen.Kind != expectedKind
            || intent.GetProperty("주문번호").GetString() != OrderNo
            || intent.GetProperty("운송Id").GetInt64() != 4
            || intent.GetProperty("배차의뢰Id").GetString() != OrderNo
            || intent.GetProperty("처리기사Id").GetString() != item.변경자
            || intent.GetProperty("상태").GetString() != expectedState)
            throw new InvalidOperationException("R31ProjectionKindOrSourceMismatch");
    }

    private static async Task<ProcessRequest?> ReadExplicitRequestAsync(HttpContext context)
    {
        // Bound chunked requests too; ContentLength alone is not a body limit.
        var bytes = new byte[4097];
        var length = 0;
        while (length < bytes.Length)
        {
            var read = await context.Request.Body.ReadAsync(bytes.AsMemory(length), context.RequestAborted);
            if (read == 0) break;
            length += read;
        }
        if (length == 0 || length > 4096) return null;
        using var document = JsonDocument.Parse(bytes.AsMemory(0, length), new JsonDocumentOptions { MaxDepth = 4 });
        var value = document.RootElement;
        if (value.ValueKind != JsonValueKind.Object) return null;
        var names = value.EnumerateObject().Select(x => x.Name).ToArray();
        if (names.Length != 3 || names.Distinct(StringComparer.Ordinal).Count() != 3
            || names.Any(x => x is not ("orderNo" or "expectedIds" or "expectedSnapshotSha256"))) return null;
        return value.Deserialize<ProcessRequest>(JsonOptions);
    }

    private static string SnapshotHash(ProjectionSnapshot snapshot) => Sha(JsonSerializer.Serialize(snapshot, JsonOptions));
    private static string Sha(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    private static Task Error(HttpContext context, int status, string code, string? safeType = null)
    {
        context.Response.StatusCode = status;
        return context.Response.WriteAsJsonAsync(new { errorCode = code, safeType, automaticRetry = false });
    }

    private sealed record ProcessRequest(string OrderNo, long[] ExpectedIds, string ExpectedSnapshotSha256);
    private sealed record ProjectionTarget(long Id, string Kind, string State, int AttemptCount, string KeySha256, string PayloadSha256);
    private sealed record ProjectionSnapshot(string RunStableId, string Database, string OrderNo,
        IReadOnlyList<ProjectionTarget> Targets, int OrderOutboxCount, string UnselectedOutboxSha256,
        int OrderHistoryCount, int DeliveryAttemptCount, int SettlementCount, int TransportEventCount,
        string? TransportLedgerState);
}
