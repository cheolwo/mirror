using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Education;
using Ssalddel.Contracts.Common.Orderer;
using Ssalddel.Services.Community;
using Ssalddel.Services.Education;
using Ssalddel.Services.Orderer;
using 살뜰.Services.Options;

namespace Ssalddel.LifecycleProbe;

internal static class Program
{
    private const string QueueCollection = "education_field_experience_submissions";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly string[] SourcePaths =
    [
        "Ssalddel/Services/Education/교육기관제출대기열.cs",
        "Ssalddel/Services/Education/교육기관제출Worker.cs",
        "Ssalddel/Services/Education/현장체험활동UseCase.cs",
        "Ssalddel/Services/Community/CommunityLedgerStore.cs",
        "Ssalddel/Services/Orderer/공동구매해외선적추적저장소.cs",
        "Ssalddel/Services/Orderer/공동구매선적진행Policy.cs"
    ];

    private static async Task<int> Main(string[] args)
    {
        string root;
        string reportPath;
        try
        {
            root = FindRepositoryRoot();
            if (args.Length == 1 && args[0] == "--help")
            {
                Console.WriteLine("dotnet run --project eng/Ssalddel.LifecycleProbe -- [--report artifacts/local/<directory>/<name>.json]");
                return 0;
            }
            if (args.Length != 0 && (args.Length != 2 || args[0] != "--report" || string.IsNullOrWhiteSpace(args[1])))
                throw new ArgumentException("Invalid command line.");
            reportPath = ResolveReportPath(root, args.Length == 0
                ? "artifacts/local/os-lifecycle-hardening-r24/mongo-probe.json" : args[1]);
        }
        catch (Exception ex)
        {
            Console.WriteLine(JsonSerializer.Serialize(new { status = "Failed", code = "PROBE_INPUT", exceptionType = ex.GetType().Name }, JsonOptions));
            return 1;
        }

        var report = new ProbeReport();
        foreach (var path in SourcePaths)
        {
            var fullPath = Path.Combine(root, path);
            report.Sources.Add(new(path, File.Exists(fullPath)
                ? Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(fullPath))).ToLowerInvariant() : null));
        }
        var databaseName = "r24-lifecycle-" + Guid.NewGuid().ToString("N");
        report.DatabaseName = databaseName;
        // No appsettings, user secrets, production database or external endpoint is read.
        var client = new MongoClient(new MongoClientSettings
        {
            Server = new MongoServerAddress("127.0.0.1", 27027),
            ServerSelectionTimeout = TimeSpan.FromSeconds(8),
            ConnectTimeout = TimeSpan.FromSeconds(5)
        });
        var database = client.GetDatabase(databaseName);
        var options = Options.Create(new MongoDbOptions { Database = databaseName });
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        try
        {
            await RunProbeAsync("education-queue", report, () => ProbeQueueAsync(client, database, options, report, timeout.Token));
            await RunProbeAsync("education-worker", report, () => ProbeWorkerAsync(client, database, options, report, timeout.Token));
            await RunProbeAsync("shipment", report, () => ProbeShipmentAsync(client, options, report, timeout.Token));
        }
        finally
        {
            try
            {
                // Only the exact name allocated by this run is eligible for deletion.
                if (!databaseName.StartsWith("r24-lifecycle-", StringComparison.Ordinal)
                    || !Guid.TryParseExact(databaseName["r24-lifecycle-".Length..], "N", out _))
                    throw new InvalidOperationException("Database ownership check failed.");
                using var cleanupTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                await client.DropDatabaseAsync(databaseName, cleanupTimeout.Token);
                report.CleanupSucceeded = true;
            }
            catch (Exception ex)
            {
                report.Errors.Add(new("PROBE_CLEANUP", ex.GetType().Name));
            }
        }

        report.CompletedAtUtc = DateTime.UtcNow;
        report.Status = report.Errors.Count == 0 && report.Checks.All(x => x.Passed) && report.CleanupSucceeded ? "Passed" : "Failed";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
            await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(report, JsonOptions));
        }
        catch (Exception ex)
        {
            report.Errors.Add(new("PROBE_REPORT_WRITE", ex.GetType().Name));
            report.Status = "Failed";
        }
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            report.Status, checkCount = report.Checks.Count, passedCount = report.Checks.Count(x => x.Passed),
            report.CleanupSucceeded, reportPath, report.Errors
        }, JsonOptions));
        return report.Status == "Passed" ? 0 : 1;
    }

    private static async Task RunProbeAsync(string name, ProbeReport report, Func<Task> action)
    {
        try { await action(); }
        catch (ProbeAssertionException) { report.Errors.Add(new("PROBE_ASSERTION:" + name, nameof(ProbeAssertionException))); }
        catch (Exception ex) { report.Errors.Add(new("PROBE_EXECUTION:" + name, ex.GetType().Name,
            (ex as MongoCommandException)?.Code, (ex as MongoCommandException)?.CodeName)); }
    }

    private static async Task ProbeQueueAsync(IMongoClient client, IMongoDatabase database,
        IOptions<MongoDbOptions> options, ProbeReport report, CancellationToken ct)
    {
        var queue = new Mongo교육기관제출대기열(client, options);
        const string id = "queue-receipt";
        const string ledgerId = "queue-ledger";
        var reserved = await queue.예약Async(id, ledgerId, 교육기관제출방식.이메일, null, "teacher@example.invalid", ct);
        var indexDefinitions = await (await database.GetCollection<BsonDocument>(QueueCollection).Indexes.ListAsync(ct)).ToListAsync(ct);
        Check(report, "EDU-Q-00", indexDefinitions.Any(index => index["name"] == "_id_" && index["key"].AsBsonDocument.Contains("_id"))
            && indexDefinitions.Any(index => index["name"] == "ix_education_submission_pending")
            && indexDefinitions.Any(index => index["name"] == "ix_education_submission_ledger"),
            "Submission uses the native unique ID index and creates supported pending/ledger indexes on Mongo 8.");
        Check(report, "EDU-Q-01", reserved.상태 == 교육기관제출상태.전송대기, "New reservation is pending.");
        var first = await queue.다음작업확보Async(ct);
        Check(report, "EDU-Q-02", first?.제출Id == id && first.시도횟수 == 1 && !first.전송완료, "First claim has no send receipt.");
        await queue.완료Async(id, 교육기관제출상태.전송완료, ct);
        var completed = (await queue.원장별조회Async(ledgerId, ct)).Single();
        Check(report, "EDU-Q-03", completed.전송완료시각Utc.HasValue, "Send receipt is persisted.");
        var repeated = await queue.예약Async(id, ledgerId, 교육기관제출방식.이메일, null, "teacher@example.invalid", ct);
        Check(report, "EDU-Q-04", repeated.상태 == completed.상태 && repeated.전송완료시각Utc == completed.전송완료시각Utc,
            "Reservation replay preserves completed status and receipt.");
        var rejected = false;
        try { await queue.예약Async(id, "different-ledger", 교육기관제출방식.이메일, null, "teacher@example.invalid", ct); }
        catch (InvalidOperationException) { rejected = true; }
        Check(report, "EDU-Q-05", rejected && (await queue.원장별조회Async("different-ledger", ct)).Count == 0,
            "Same submission ID cannot move to another ledger.");
        await queue.실패Async(id, "controlled-projection-failure", false, 5, ct);
        var failedProjection = (await queue.원장별조회Async(ledgerId, ct)).Single();
        Check(report, "EDU-Q-06", failedProjection.상태 == 교육기관제출상태.전송완료
            && failedProjection.전송완료시각Utc == completed.전송완료시각Utc && failedProjection.마지막오류 == "controlled-projection-failure",
            "Projection failure preserves receipt and schedules projection recovery.");
        await MakeDueAsync(database, id, ct);
        var freshQueue = new Mongo교육기관제출대기열(client, options);
        var next = await freshQueue.다음작업확보Async(ct);
        Check(report, "EDU-Q-07", next?.제출Id == id && next.전송완료 && next.시도횟수 == 2,
            "Fresh queue instance claims persisted receipt as AlreadySent.");
        await freshQueue.실패Async(id, "controlled-legacy-projection-failure", false, 5, ct);
        // Model a persisted pre-field completed document, retaining its send receipt.
        var legacyUpdate = await database.GetCollection<BsonDocument>(QueueCollection).UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", id),
            Builders<BsonDocument>.Update.Unset("원장반영완료"), cancellationToken: ct);
        Require(legacyUpdate.MatchedCount == 1);
        await MakeDueAsync(database, id, ct);
        var legacy = await new Mongo교육기관제출대기열(client, options).다음작업확보Async(ct);
        var legacyReceipt = (await freshQueue.원장별조회Async(ledgerId, ct)).Single();
        Check(report, "EDU-Q-10", legacy?.제출Id == id && legacy.전송완료 && legacy.시도횟수 == 3
            && legacyReceipt.전송완료시각Utc == completed.전송완료시각Utc,
            "Completed receipt lacking the new projection flag remains claimable as AlreadySent.");
        await freshQueue.원장반영완료Async(id, ct);
        await MakeDueAsync(database, id, ct);
        Check(report, "EDU-Q-08", await freshQueue.다음작업확보Async(ct) is null, "Projected completion is no longer claimable.");
        await freshQueue.실패Async(id, "late-failure", false, 5, ct);
        var terminalReplay = await freshQueue.예약Async(id, ledgerId, 교육기관제출방식.이메일, null, "teacher@example.invalid", ct);
        Check(report, "EDU-Q-09", terminalReplay.상태 == 교육기관제출상태.전송완료
            && terminalReplay.전송완료시각Utc == completed.전송완료시각Utc && terminalReplay.마지막오류 is null,
            "Late failure and reservation replay preserve projected terminal result.");
    }

    private static async Task ProbeWorkerAsync(IMongoClient client, IMongoDatabase database,
        IOptions<MongoDbOptions> options, ProbeReport report, CancellationToken ct)
    {
        var queue = new Mongo교육기관제출대기열(client, options);
        var store = new Mongo커뮤니티원장저장소(client, options);
        var useCase = new 현장체험활동UseCase(store, queue);
        var recovery = await ReadySubmissionAsync(useCase, ct);
        var faultStore = new ProjectionFaultLedgerStore(store) { FailNextProjection = true };
        var sender = new CountingSender();
        using (var provider = WorkerServices(faultStore, sender))
        using (var worker = CreateWorker(queue, provider))
        {
            await ProcessWorkerAsync(worker, ct);
            var receipt = (await queue.원장별조회Async(recovery.LedgerId, ct)).Single();
            var pending = await store.원장조회Async(recovery.LedgerId, ct);
            Check(report, "EDU-W-01", sender.Calls == 1 && receipt.전송완료시각Utc.HasValue
                && receipt.상태 == 교육기관제출상태.전송완료 && pending?.상태 == 현장체험활동상태.제출대기,
                "Actual worker persists send receipt before controlled ledger projection failure.");
            await MakeDueAsync(database, recovery.SubmissionId, ct);
            await ProcessWorkerAsync(worker, ct);
            var recovered = await store.원장조회Async(recovery.LedgerId, ct);
            Check(report, "EDU-W-02", sender.Calls == 1 && recovered?.상태 == 현장체험활동상태.학교심사중,
                "Actual worker recovers actual Mongo ledger without a second sender call.");
            Check(report, "EDU-W-03", await queue.다음작업확보Async(ct) is null,
                "Recovered worker submission is acknowledged and no longer claimable.");
        }
        foreach (var recognized in new[] { true, false })
        {
            var ready = await ReadySubmissionAsync(useCase, ct);
            var decision = await useCase.학교결정Async(ready.LedgerId, new()
            {
                출석인정여부 = recognized, 결정기관명 = "Probe School", 결정자표시명 = "Probe Teacher"
            }, "probe-teacher", "probe-school", false, ct);
            Check(report, "EDU-W-DECISION-" + recognized, decision.IsSuccess, "Actual use case saves school decision before worker processing.");
            var before = await store.원장조회Async(ready.LedgerId, ct);
            var decisionSender = new CountingSender();
            using var provider = WorkerServices(store, decisionSender);
            using var worker = CreateWorker(queue, provider);
            await ProcessWorkerAsync(worker, ct);
            var after = await store.원장조회Async(ready.LedgerId, ct);
            Check(report, "EDU-W-PRESERVE-" + recognized, after?.상태 == before?.상태 && after?.Revision == before?.Revision
                && decisionSender.Calls == 1 && after?.상태 == (recognized ? 현장체험활동상태.출석인정 : 현장체험활동상태.출석미인정),
                "Actual worker leaves school decision and ledger revision intact.");
            var submission = (await queue.원장별조회Async(ready.LedgerId, ct)).Single();
            Check(report, "EDU-W-ACK-" + recognized, submission.전송완료시각Utc.HasValue && await queue.다음작업확보Async(ct) is null,
                "Decision-preserving processing still acknowledges the submission.");
        }
    }

    private static async Task<(string LedgerId, string SubmissionId)> ReadySubmissionAsync(현장체험활동UseCase useCase, CancellationToken ct)
    {
        var start = DateTimeOffset.UtcNow.AddHours(-2);
        var created = await useCase.생성Async(new()
        {
            제목 = "Lifecycle probe " + Guid.NewGuid().ToString("N"), 학생표시명 = "Probe Student",
            학교식별Key = "probe-school", 학교명 = "Probe School", 보호자UserId = "probe-guardian",
            보호자표시명 = "Probe Guardian", 활동목표 = "Controlled lifecycle verification", 활동장소 = "Local probe",
            시작예정시각 = start, 종료예정시각 = start.AddHours(1), 계획활동 = ["Observation"],
            학교담당이메일 = "teacher@example.invalid"
        }, "probe-student", ct);
        Require(created.IsSuccess);
        var id = created.Value.원장Id;
        Require((await useCase.활동기록Async(id, new()
        {
            활동명 = "Observation", 활동내용 = "Controlled evidence", 수행역할 = "Observer",
            시작시각 = start, 종료시각 = start.AddHours(1), 확인자표시명 = "Probe Supervisor"
        }, "probe-student", ct)).IsSuccess);
        Require((await useCase.보호자승인Async(id, new() { 승인여부 = true, 보호자표시명 = "Probe Guardian" }, "probe-guardian", ct)).IsSuccess);
        var submitted = await useCase.학교제출Async(id, new() { 전송방식 = 교육기관제출방식.이메일 }, "probe-student", ct);
        Require(submitted.IsSuccess);
        return (id, submitted.Value.제출목록.Single().제출Id);
    }

    private static async Task ProbeShipmentAsync(IMongoClient client, IOptions<MongoDbOptions> options,
        ProbeReport report, CancellationToken ct)
    {
        var store = new Mongo공동구매해외선적추적저장소(client, options);
        var t = new DateTime(2026, 10, 6, 0, 0, 0, DateTimeKind.Utc);
        const string doc = "PROBE-SHIPMENT";
        var input = ShipmentInput(doc, 공동구매선적상태코드.국내창고입고, t.AddMinutes(10), "current-warehouse");
        input.이벤트목록 =
        [
            ShipmentEvent(공동구매선적상태코드.문서등록, t, "initial"),
            ShipmentEvent(공동구매선적상태코드.국내창고입고, t.AddMinutes(10), "warehouse")
        ];
        var initial = await store.UpsertAsync(input, "probe", ct);
        Check(report, "SHIP-01", initial.이벤트목록.Count == 2, "Actual Upsert creates initial history.");
        var old = await store.AppendEventAsync(doc, AppendEvent(공동구매선적상태코드.통관진행중, t.AddMinutes(5), "old-append"), "probe", ct);
        Check(report, "SHIP-02", SameProjection(initial, old!) && old!.이벤트목록.Count == 3,
            "Older Append event is retained without current projection regression.");
        var metadata = ShipmentInput(doc, 공동구매선적상태코드.통관진행중, t.AddMinutes(4), "old-upsert-location");
        metadata.상품요약 = "metadata-before-complete";
        metadata.이벤트목록 = [ShipmentEvent(공동구매선적상태코드.통관진행중, t.AddMinutes(4), "old-upsert")];
        var merged = await store.UpsertAsync(metadata, "probe", ct);
        Check(report, "SHIP-03", SameProjection(initial, merged) && merged.상품요약 == metadata.상품요약 && merged.이벤트목록.Count == 4,
            "Older Upsert merges provenance and metadata while preserving current projection.");
        var completed = (await store.AppendEventAsync(doc, AppendEvent(공동구매선적상태코드.완료, t.AddMinutes(20), "completed"), "probe", ct))!;
        Check(report, "SHIP-04", completed.현재상태코드 == 공동구매선적상태코드.완료, "Shipment advances to Completed.");
        var customs = (await store.AppendEventAsync(doc, AppendEvent(공동구매선적상태코드.통관진행중, t.AddMinutes(30), "newer-customs"), "probe", ct))!;
        var exception = (await store.AppendEventAsync(doc, AppendEvent(공동구매선적상태코드.예외, t.AddMinutes(40), "newer-exception"), "probe", ct))!;
        Check(report, "SHIP-05", SameProjection(completed, customs) && SameProjection(completed, exception)
            && exception.이벤트목록.Count == 7, "Newer customs and exception after Completed remain history only.");
        var terminalMetadata = ShipmentInput(doc, 공동구매선적상태코드.통관진행중, t.AddMinutes(50), "terminal-upsert-location");
        terminalMetadata.상품요약 = "metadata-after-complete";
        terminalMetadata.이벤트목록 = [ShipmentEvent(공동구매선적상태코드.통관진행중, t.AddMinutes(50), "terminal-upsert")];
        var terminal = await store.UpsertAsync(terminalMetadata, "probe", ct);
        Check(report, "SHIP-06", SameProjection(completed, terminal) && terminal.상품요약 == terminalMetadata.상품요약
            && terminal.이벤트목록.Count == 8 && new[] { "initial", "warehouse", "old-append", "old-upsert", "completed", "newer-customs", "newer-exception", "terminal-upsert" }
                .All(reference => terminal.이벤트목록.Any(x => x.증빙참조 == reference)),
            "Completed Upsert preserves projection and all provenance while applying metadata.");
        var duplicate = await store.UpsertAsync(terminalMetadata, "probe", ct);
        Check(report, "SHIP-07", SameProjection(completed, duplicate) && duplicate.이벤트목록.Count == 8,
            "Repeated exact Upsert event does not duplicate full provenance.");

        const string concurrentDoc = "PROBE-CONCURRENT";
        var seed = ShipmentInput(concurrentDoc, 공동구매선적상태코드.국내창고입고, t.AddMinutes(10), "concurrent-seed");
        seed.이벤트목록 = [ShipmentEvent(공동구매선적상태코드.국내창고입고, t.AddMinutes(10), "concurrent-initial")];
        await store.UpsertAsync(seed, "probe", ct);
        var writes = new[]
        {
            AppendEvent(공동구매선적상태코드.통관진행중, t.AddMinutes(30), "concurrent-customs"),
            AppendEvent(공동구매선적상태코드.예외, t.AddMinutes(40), "concurrent-exception"),
            AppendEvent(공동구매선적상태코드.완료, t.AddMinutes(50), "concurrent-completed")
        };
        // Completed has the latest event time; arrival order is deliberately unconstrained.
        await Task.WhenAll(writes.Select(x => new Mongo공동구매해외선적추적저장소(client, options)
            .AppendEventAsync(concurrentDoc, x, "probe", ct)));
        var final = (await store.GetBy문서관리번호Async(concurrentDoc, ct))!;
        Check(report, "SHIP-08", final.현재상태코드 == 공동구매선적상태코드.완료
            && final.마지막단계시각Utc == t.AddMinutes(50) && final.현재위치요약 == "concurrent-completed",
            "Concurrent distinct events retain latest Completed projection.");
        Check(report, "SHIP-09", final.이벤트목록.Count == 4
            && writes.All(x => final.이벤트목록.Count(e => e.증빙참조 == x.증빙참조) == 1),
            "Concurrent atomic event writes retain every distinct event once.");

        const string recoveryDoc = "PROBE-EXCEPTION-RECOVERY";
        var recoverySeed = ShipmentInput(recoveryDoc, 공동구매선적상태코드.국내기사상차, t.AddMinutes(10), "carrier-pickup");
        recoverySeed.이벤트목록 = [ShipmentEvent(공동구매선적상태코드.국내기사상차, t.AddMinutes(10), "carrier-pickup")];
        await store.UpsertAsync(recoverySeed, "probe", ct);
        var exceptional = (await store.AppendEventAsync(recoveryDoc,
            AppendEvent(공동구매선적상태코드.예외, t.AddMinutes(20), "carrier-exception"), "probe", ct))!;
        var collection = client.GetDatabase(options.Value.Database)
            .GetCollection<공동구매해외선적추적문서>("orderer_group_purchase_overseas_shipments");
        var exceptionalDocument = await collection.Find(x => x.문서관리번호정규화 == recoveryDoc).SingleAsync(ct);
        Check(report, "SHIP-10", exceptional.현재상태코드 == 공동구매선적상태코드.예외
            && exceptionalDocument.마지막정상상태코드 == 공동구매선적상태코드.국내기사상차,
            "Exception preserves the persisted last normal carrier-pickup baseline.");
        var backward = (await store.AppendEventAsync(recoveryDoc,
            AppendEvent(공동구매선적상태코드.문서등록, t.AddMinutes(30), "backward-after-exception"), "probe", ct))!;
        var backwardDocument = await collection.Find(x => x.문서관리번호정규화 == recoveryDoc).SingleAsync(ct);
        Check(report, "SHIP-11", SameProjection(exceptional, backward) && backward.이벤트목록.Count == 3
            && backwardDocument.마지막정상상태코드 == 공동구매선적상태코드.국내기사상차,
            "Newer DocumentRegistered after Exception is history only and retains the normal baseline.");
        var resumed = (await store.AppendEventAsync(recoveryDoc,
            AppendEvent(공동구매선적상태코드.완료, t.AddMinutes(40), "completed-after-exception"), "probe", ct))!;
        var resumedDocument = await collection.Find(x => x.문서관리번호정규화 == recoveryDoc).SingleAsync(ct);
        Check(report, "SHIP-12", resumed.현재상태코드 == 공동구매선적상태코드.완료
            && resumed.이벤트목록.Count == 4 && resumedDocument.마지막정상상태코드 == 공동구매선적상태코드.완료,
            "A later Completed event advances from Exception without losing history.");
    }

    private static bool SameProjection(공동구매해외선적추적Dto left, 공동구매해외선적추적Dto right)
        => left.현재상태코드 == right.현재상태코드 && left.현재위치요약 == right.현재위치요약 && left.마지막단계시각Utc == right.마지막단계시각Utc;

    private static 공동구매해외선적추적저장요청 ShipmentInput(string doc, string state, DateTime at, string location) => new()
    {
        공동구매Id = "probe-group", 주문자집단배송권키 = "probe-scope", 주문자집단배송권명 = "Probe Scope",
        상품요약 = "Probe goods", 문서관리번호 = doc, 운송문서번호 = doc + "-BL",
        현재상태코드 = state, 마지막단계시각Utc = at, 현재위치요약 = location
    };
    private static 공동구매해외선적추적이벤트Dto ShipmentEvent(string state, DateTime at, string reference) => new()
    {
        이벤트코드 = state, 표시명 = reference, 위치요약 = reference, 발생시각Utc = at,
        출처주체코드 = "probe", 증빙참조 = reference
    };
    private static 공동구매해외선적추적이벤트추가요청 AppendEvent(string state, DateTime at, string reference) => new()
    {
        이벤트코드 = state, 표시명 = reference, 위치요약 = reference, 발생시각Utc = at,
        출처주체코드 = "probe", 증빙참조 = reference
    };

    private static async Task MakeDueAsync(IMongoDatabase database, string id, CancellationToken ct)
    {
        var result = await database.GetCollection<BsonDocument>(QueueCollection).UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", id),
            Builders<BsonDocument>.Update.Set("다음시도시각Utc", DateTime.UtcNow.AddSeconds(-5)), cancellationToken: ct);
        Require(result.MatchedCount == 1);
    }
    private static ServiceProvider WorkerServices(I커뮤니티원장저장소 store, CountingSender sender)
        => new ServiceCollection().AddSingleton(store).AddSingleton<I교육기관제출전송Service>(sender).BuildServiceProvider();
    private static 교육기관제출Worker CreateWorker(I교육기관제출대기열 queue, ServiceProvider services)
        => new(queue, services.GetRequiredService<IServiceScopeFactory>(), new StaticOptions(), NullLogger<교육기관제출Worker>.Instance);
    private static Task ProcessWorkerAsync(교육기관제출Worker worker, CancellationToken ct)
    {
        // Invoke one internal processing batch without starting a hosted worker or altering product visibility.
        var method = typeof(교육기관제출Worker).GetMethod("ProcessPendingAsync", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(nameof(교육기관제출Worker), "ProcessPendingAsync");
        return (Task)method.Invoke(worker, [new 교육기관제출Options(), ct])!;
    }
    private static void Check(ProbeReport report, string id, bool passed, string assertion)
    {
        report.Checks.Add(new(id, passed, assertion));
        Require(passed);
    }
    private static void Require(bool condition)
    {
        if (!condition) throw new ProbeAssertionException();
    }
    private static string FindRepositoryRoot()
    {
        foreach (var candidate in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
            for (var dir = new DirectoryInfo(candidate); dir is not null; dir = dir.Parent)
                if (File.Exists(Path.Combine(dir.FullName, "Directory.Build.props"))
                    && File.Exists(Path.Combine(dir.FullName, "Ssalddel", "Ssalddel.csproj"))) return dir.FullName;
        throw new DirectoryNotFoundException("Repository root not found.");
    }
    private static string ResolveReportPath(string root, string input)
    {
        if (input.StartsWith(@"\\", StringComparison.Ordinal) || input.Contains("://", StringComparison.Ordinal))
            throw new ArgumentException("Report must be a local artifact path.");
        var full = Path.GetFullPath(input, root);
        var allowed = Path.GetFullPath(Path.Combine(root, "artifacts", "local")) + Path.DirectorySeparatorChar;
        if (!full.StartsWith(allowed, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(Path.GetExtension(full), ".json", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Report must be a JSON file beneath artifacts/local.");
        for (var path = full; path is not null; path = Path.GetDirectoryName(path))
            if ((File.Exists(path) || Directory.Exists(path)) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new ArgumentException("Report path cannot traverse a link.");
        if (Directory.Exists(full)) throw new ArgumentException("Report path is a directory.");
        return full;
    }

    private sealed class ProjectionFaultLedgerStore(I커뮤니티원장저장소 inner) : I커뮤니티원장저장소
    {
        public bool FailNextProjection { get; set; }
        public Task<커뮤니티원장Dto> 원장저장Async(커뮤니티원장저장요청 request, string updatedBy, CancellationToken ct = default)
            => inner.원장저장Async(request, updatedBy, ct);
        public Task<커뮤니티원장Dto?> 원장조회Async(string id, CancellationToken ct = default) => inner.원장조회Async(id, ct);
        public Task<IReadOnlyList<커뮤니티원장Dto>> 원장목록조회Async(커뮤니티원장조회조건 query, CancellationToken ct = default)
            => inner.원장목록조회Async(query, ct);
        public Task<커뮤니티원장Dto?> 원장상태변경Async(커뮤니티원장상태변경요청 request, string updatedBy, CancellationToken ct = default)
        {
            if (FailNextProjection)
            {
                FailNextProjection = false;
                throw new InvalidOperationException("controlled-ledger-projection-failure");
            }
            return inner.원장상태변경Async(request, updatedBy, ct);
        }
    }
    private sealed class CountingSender : I교육기관제출전송Service
    {
        public int Calls { get; private set; }
        public Task<교육기관제출전송결과> 전송Async(교육기관제출작업 work, 커뮤니티원장Dto ledger, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(교육기관제출전송결과.완료());
        }
    }
    private sealed class StaticOptions : IOptionsMonitor<교육기관제출Options>
    {
        public 교육기관제출Options CurrentValue { get; } = new();
        public 교육기관제출Options Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<교육기관제출Options, string?> listener) => null;
    }
    private sealed class ProbeAssertionException : Exception { }
    private sealed record ProbeCheck(string Id, bool Passed, string Assertion);
    private sealed record ProbeError(string Code, string ExceptionType, int? DatabaseCode = null, string? DatabaseCodeName = null);
    private sealed record SourceHash(string Path, string? Sha256);
    private sealed class ProbeReport
    {
        public int SchemaVersion { get; } = 1;
        public string Status { get; set; } = "Running";
        public string MongoEndpoint { get; } = "127.0.0.1:27027";
        public string DatabaseName { get; set; } = "";
        public DateTime StartedAtUtc { get; } = DateTime.UtcNow;
        public DateTime? CompletedAtUtc { get; set; }
        public bool CleanupSucceeded { get; set; }
        public List<ProbeCheck> Checks { get; } = [];
        public List<ProbeError> Errors { get; } = [];
        public List<SourceHash> Sources { get; } = [];
        public string[] Boundaries { get; } =
        [
            "Actual local Mongo persistence and product services are exercised; server HTTP, APK UI and device validation are outside this probe.",
            "The sender is a counting fake; no SMTP or institution API is invoked.",
            "The worker internal batch is invoked by reflection without starting BackgroundService.",
            "One controlled wrapper fails ledger projection once; successful reads and projection retries use the real Mongo ledger store.",
            "Only this run's randomly allocated database is modified and deleted in finally; no app configuration or credentials are read.",
            "Due timestamps are changed directly only in this run's queue collection to avoid waiting for backoff.",
            "Source hashes bind the executed source snapshot; hashes do not prove semantic branch coverage.",
            "Concurrent shipment Completed carries the latest occurrence time; arbitrarily older terminal events are not forced to replace newer projection."
        ];
    }
}
