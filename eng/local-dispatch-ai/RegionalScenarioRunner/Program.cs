using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Tests.Clients;
using Ssalddel.Tests.Services.Dispatch.AI;
using Ssalddel.Tests.Services.Dispatch.Queue;
using 살뜰.Services.DeliveryZones;
using 살뜰.Services.Dispatch.Coordination;
using 살뜰.Services.Dispatch.AI;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.도메인.배차;

if (args.Length != 3 || args[0] is not ("--mock" or "--actual" or "--actual-smoke") || args[1] != "--dataset")
{
    Console.WriteLine("No scenario or inference executed. Use --mock --dataset <prepared private dataset.json>; then --actual with the same source and dataset.");
    return;
}
var actual = args[0] != "--mock";
var smoke = args[0] == "--actual-smoke";
var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Ssalddel", "Ssalddel.csproj"))) directory = directory.Parent;
var repo = directory?.FullName ?? throw new InvalidOperationException("Repository root required.");
const string designPath = "docs/AI/Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/regional-management.implementation.r50.md";
const string workPath = "docs/AI/Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/regional-management.r50.work-order.json";
const string designHash = "3e2a19fe56f4344404240de0c40040c7d79b8ba5cff311907e59216b360664c2";
using (var work = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(repo, workPath))))
    if (!work.RootElement.GetProperty("acceptedInCurrentThread").GetBoolean()
        || !work.RootElement.GetProperty("implementationAllowed").GetBoolean()
        || work.RootElement.GetProperty("designSha256").GetString() != designHash
        || EvidenceFiles.Hash(File.ReadAllBytes(Path.Combine(repo, designPath))) != designHash)
        throw new InvalidOperationException("Accepted r50 design and work order must match.");
var root = Path.Combine(repo, "artifacts", "local", "local-dispatch-ai", "regional-management-r50");
var datasetPath = Path.GetFullPath(args[2]);
var dataRoot = Path.Combine(root, "datasets") + Path.DirectorySeparatorChar;
if (!datasetPath.StartsWith(dataRoot, StringComparison.OrdinalIgnoreCase) || Path.GetFileName(datasetPath) != "dataset.json")
    throw new InvalidOperationException("Use the fingerprinted private dataset prepared by this runner's exporter.");
using var data = JsonDocument.Parse(File.ReadAllBytes(datasetPath));
using var summary = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(datasetPath)!, "summary.json")));
if (summary.RootElement.GetProperty("datasetSha256").GetString() != EvidenceFiles.Hash(File.ReadAllBytes(datasetPath)))
    throw new InvalidOperationException("Prepared dataset content hash mismatch.");
var datasetFingerprint = data.RootElement.GetProperty("metadata").GetProperty("sourceFingerprint").GetString();
if (new DirectoryInfo(Path.GetDirectoryName(datasetPath)!).Name != datasetFingerprint)
    throw new InvalidOperationException("Dataset fingerprint directory mismatch.");
var metadata = data.RootElement.GetProperty("metadata");
if (!metadata.GetProperty("localVerificationOnly").GetBoolean()
    || metadata.GetProperty("toolSha256").GetString() != EvidenceFiles.Hash(File.ReadAllBytes(Path.Combine(repo,
        "eng/local-dispatch-ai/RegionalScenarioRunner/prepare_regional_dataset.py"))))
    throw new InvalidOperationException("Dataset preparation contract or tool version mismatch.");
var deliveryRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ChatGPT", "배달");
foreach (var source in metadata.GetProperty("sourceFingerprints").EnumerateArray())
{
    var relative = source.GetProperty("relativePath").GetString()!;
    if (relative.Contains("..", StringComparison.Ordinal) || Path.IsPathRooted(relative)
        || !relative.StartsWith("artifacts/local/delivery-history-replay-r54/", StringComparison.Ordinal)
            && !relative.StartsWith("edit-kit/branch-deliveries-all-20261010/", StringComparison.Ordinal))
        throw new InvalidOperationException("Unexpected dataset source reference.");
    var original = Path.Combine(relative.StartsWith("artifacts/", StringComparison.Ordinal) ? repo : deliveryRoot, relative);
    if (EvidenceFiles.Hash(File.ReadAllBytes(original)) != source.GetProperty("sha256").GetString())
        throw new InvalidOperationException("Historical source changed; prepare a new dataset before execution.");
}
var records = data.RootElement.GetProperty("records").EnumerateArray().ToArray();
if (records.Length != 504 || records.Select(r => r.GetProperty("branchId").GetString()).Distinct().Count() != 339
    || records.Any(r => !r.GetProperty("syntheticDestination").GetProperty("notActualCustomerLocation").GetBoolean()))
    throw new InvalidOperationException("Prepared dataset scope or synthetic destination declaration mismatch.");
var sourcePaths = new List<string> { designPath,
    "Ssalddel/Extensions/ServiceCollectionExtensions.DomainServices.Operations.cs",
    "Ssalddel/Services/Dispatch/Queue/QueueOptions.cs",
    "Ssalddel/Services/Dispatch/Queue/음식배달후보검토결과.cs",
    "Ssalddel/Services/Dispatch/Queue/음식배달수요참여Service.cs",
    "Ssalddel/Services/Dispatch/Queue/배차추천후보선정Service.cs",
    "Ssalddel/Services/Dispatch/Queue/배차대기원장전환Service.cs",
    "Ssalddel/Services/Dispatch/Queue/배차대기원장전환Service.Recommendation.cs",
    "Ssalddel/Services/Dispatch/Notification/배차추천알림Service.cs",
    "Ssalddel/Services/Dispatch/Recommendation/FoodDeliveryDriverWorkService.cs",
    "Ssalddel/Controllers/Driver/Food/음식배달기사업무Controller.cs",
    "Ssalddel/Services/Food/EfSsalddelFoodOrderStore.cs",
    "FDriverApp/Services/FoodDeliveryDriverApiService.cs",
    "FDriverApp/Services/FDriverFoodPushReceiver.cs",
    "Ssalddel/Services/Dispatch/Recommendation/멀티배차조합Service.cs",
    "Ssalddel/Services/Dispatch/Recommendation/픽업하차경로최적화Service.cs",
    "Ssalddel.Domain/배차/음식배달수요참여Policy.cs" };
foreach (var (folder, pattern) in new[] {
    ("Ssalddel/Services/Dispatch/AI", "*.cs"), ("Ssalddel/Services/Dispatch/Queue", "음식배달배차업무정책*.cs"),
    ("Ssalddel/Services/Dispatch/Recommendation", "음식권역복귀일정평가Service*.cs"),
    ("Ssalddel.Tests/Services/Dispatch/AI", "FoodDispatchWorkflowScenarioFixture*.cs"),
    ("Ssalddel.Tests/Services/Dispatch/Queue", "음식배달배차업무정책Tests*.cs"),
    ("Ssalddel.Tests/Clients", "FDriverServerDispatchScenario*.cs"),
    ("FDriverApp/PageModels", "MainPageModel*.cs"),
    ("eng/local-dispatch-ai/RegionalScenarioRunner", "*.cs"),
    ("eng/local-dispatch-ai/RegionalScenarioRunner", "*.csproj"),
    ("eng/local-dispatch-ai/RegionalScenarioRunner", "*.py") })
    sourcePaths.AddRange(Directory.GetFiles(Path.Combine(repo, folder), pattern)
        .Select(p => Path.GetRelativePath(repo, p).Replace('\\', '/')));
Dictionary<string, string> Sources()
{
    var values = sourcePaths.Distinct().Order(StringComparer.Ordinal).ToDictionary(p => p,
        p => EvidenceFiles.Hash(File.ReadAllBytes(Path.Combine(repo, p))), StringComparer.Ordinal);
    values.Add("private-prepared-dataset.json", EvidenceFiles.Hash(File.ReadAllBytes(datasetPath)));
    return values;
}
var before = Sources();
var mockEvidence = actual ? MatchingMock(root, before) : null;
var runId = DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ") + (actual ? "-actual-" : "-mock-") + Guid.NewGuid().ToString("N")[..8];
var output = Path.Combine(root, "runs", runId); Directory.CreateDirectory(output);
var results = new List<CaseResult>();
string[] districts = ["중랑구", "동대문구", "광진구"];
string[] families = ["shortage", "surplus", "long-wait", "multi"];
var abort = false;
for (var index = 0; index < districts.Length && !abort; index++)
{
    var district = districts[index];
    var districtRecords = records.Where(r => r.GetProperty("district").GetString() == district).ToArray();
    var anchor = districtRecords.First(r => r.GetProperty("historical").GetProperty("menuItems").GetArrayLength() > 0);
    var nearby = districtRecords.Where(r => r.GetProperty("branchId").GetString() != anchor.GetProperty("branchId").GetString()
            && r.GetProperty("historical").GetProperty("menuItems").GetArrayLength() > 0)
        .OrderBy(r => Distance(anchor, r)).GroupBy(r => r.GetProperty("branchId").GetString()).Select(g => g.First()).Take(2).ToArray();
    var selected = new[] { anchor }.Concat(nearby).ToArray();
    var workflowAnchor = anchor;
    string? reviewedBundleKey = null;
    string[] reviewedRecordIds = [];
    foreach (var family in families)
    {
        var caseId = $"district-{index + 1}-{family}";
        var dir = Path.Combine(output, caseId); Directory.CreateDirectory(dir);
        var useActual = actual && index == 0 && family == "multi";
        using var handler = new RegionalModelTransport(useActual, root, runId, caseId, dir);
        using var http = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        try
        {
            var now = DateTimeOffset.UtcNow;
            var input = Demand(family, now, index);
            var demand = new 음식배달수요참여Policy().평가(input);
            var sensitivity = new 음식배달수요참여Policy().평가(input, new() { 시간당목표처리건수 = 4m });
            var request = new 멀티배차조합요청(selected.Select((r, j) => Job(r, $"synthetic-order-{j + 1}", now)).ToArray());
            var service = new 음식권역배차관리Service(new NoOperationalDemand(),
                new 음식멀티배차조합Service(new RegionalGeometricRoutes()),
                new Ollama음식권역배차AI(http, Options.Create(new 음식권역배차OllamaOptions { Enabled = true, TimeoutSeconds = 90 })), TimeProvider.System);
            var review = await service.검토자료Async(demand, request);
            if (handler.Calls != 1 || review.AIStatusCode != "Applied" || review.OperationalApplied || !review.RequiresCurrentGates)
                throw new InvalidOperationException("Regional preview did not preserve selection boundaries.");
            if (family == "shortage" && demand.추가참여인원 <= 0
                || family == "surplus" && (demand.추가참여인원 != 0 || review.제안허용기사Ids.Any(id => id is "synthetic-new-old" or "synthetic-new-recent"))
                || family == "long-wait" && review.제안허용기사Ids.FirstOrDefault() != "synthetic-new-old")
                throw new InvalidOperationException("Demand or waiting priority assertion failed.");
            var evidence = new { district, family, review, input, sensitivityAtFourPerHour = sensitivity,
                sourceRecordIds = selected.Select(r => r.GetProperty("recordId").GetString()),
                syntheticOrdersDriversAndTimes = true, distanceBasis = "GeometricPreviewNotNavigationApi",
                reviewStatus = "HumanReviewPending", weightsTrained = false };
            EvidenceFiles.Save(Path.Combine(dir, "evidence.json"), evidence);
            if (family == "multi" && review.선택조합 is not null)
            {
                reviewedBundleKey = review.선택조합.조합키;
                reviewedRecordIds = review.선택조합.의뢰Ids.Select(id => selected[int.Parse(id["synthetic-order-".Length..]) - 1]
                    .GetProperty("recordId").GetString()!).ToArray();
                workflowAnchor = selected.First(r => r.GetProperty("recordId").GetString() == reviewedRecordIds[0]);
                // Menu-free historical records remain preview-only. Never infer an order menu.
                if (workflowAnchor.GetProperty("historical").GetProperty("menuItems").GetArrayLength() == 0)
                    throw new InvalidOperationException("Selected workflow member has no confirmed per-order menu.");
            }
            results.Add(new(caseId, true, useActual, handler.Calls, review.AIStatusCode));
        }
        catch (Exception ex) { RecordFailure(caseId, dir, useActual, handler.Calls, ex); if (actual) { abort = true; break; } }
    }
    if (abort) break;
    foreach (var anchorCase in new[] { "valid", "late", "missingproof" })
    {
        var id = $"district-{index + 1}-arrival-{anchorCase}";
        var dir = Path.Combine(output, id); Directory.CreateDirectory(dir);
        try
        {
            var point = Point(anchor, "pickup");
            var evidence = await 음식배달배차업무정책Tests.RunArrivalAnchorLocalScenarioAsync(point.Latitude, point.Longitude, anchorCase);
            if (!evidence.ExpectedPassed || evidence.ActualMapApiCalled) throw new InvalidOperationException("Arrival anchor assertion failed.");
            EvidenceFiles.Save(Path.Combine(dir, "evidence.json"), new { district, sourceRecordId = anchor.GetProperty("recordId").GetString(), evidence });
            results.Add(new(id, true, false, 0, anchorCase));
        }
        catch (Exception ex) { RecordFailure(id, dir, false, 0, ex); if (actual) { abort = true; break; } }
    }
    if (abort) break;
    foreach (var workflowFamily in new[] { "same-region", "demand-capacity" })
    {
    var workflowActual = actual && workflowFamily == "same-region" && (!smoke || index == 0);
    var workflowId = $"district-{index + 1}-http-mobile-{workflowFamily}";
    var workflowDir = Path.Combine(output, workflowId); Directory.CreateDirectory(workflowDir);
    using var workflowHandler = new RegionalModelTransport(workflowActual, root, runId, workflowId, workflowDir);
    using var workflowHttp = new HttpClient(workflowHandler) { Timeout = Timeout.InfiniteTimeSpan };
    try
    {
        var menu = workflowAnchor.GetProperty("historical").GetProperty("menuItems")[0].GetProperty("name").GetString()!;
        var pickup = workflowAnchor.GetProperty("pickup"); var destination = workflowAnchor.GetProperty("syntheticDestination");
        var seed = new FoodDispatchRegionalSeed(workflowAnchor.GetProperty("recordId").GetString()!, workflowAnchor.GetProperty("publicStoreName").GetString()!,
            workflowAnchor.GetProperty("publicPickupAddress").GetString()!, pickup.GetProperty("latitude").GetDecimal(), pickup.GetProperty("longitude").GetDecimal(),
            destination.GetProperty("latitude").GetDecimal(), destination.GetProperty("longitude").GetDecimal(), menu, 9000m, true);
        var workflow = await FDriverServerDispatchScenarioTests.RunScenarioAsync(workflowFamily, workflowHttp, Path.Combine(workflowDir, "workflow.sqlite3"), seed);
        if (!workflow.CustomerReceived || workflowFamily == "same-region" && (workflowHandler.Calls != 1 || !workflow.Applied)
            || workflowFamily == "demand-capacity" && (workflowHandler.Calls != 0 || !workflow.DemandSnapshotCurrent || workflow.DemandDecision is null))
            throw new InvalidOperationException("Regional HTTP/mobile flow failed.");
        EvidenceFiles.Save(Path.Combine(workflowDir, "evidence.json"), new { district, seed, workflow, reviewedBundleKey, reviewedRecordIds,
            selectedPreviewMemberExecutedSeparately = true, atomicMultiOrderAcceptanceVerified = false,
            menuBasis = "ConfirmedHistoricalNameWithSyntheticQuantityOneAndSyntheticUnitPrice9000",
            realCustomerLocationsUsed = 0, actualDeviceVerified = false, actualFcmVerified = false });
        results.Add(new(workflowId, true, workflowActual, workflowHandler.Calls, workflow.Reason));
    }
    catch (Exception ex) { RecordFailure(workflowId, workflowDir, workflowActual, workflowHandler.Calls, ex); if (actual) { abort = true; break; } }
    }
}
var unchanged = before.OrderBy(p => p.Key).SequenceEqual(Sources().OrderBy(p => p.Key));
var allPassed = !abort && results.Count == 27 && results.All(r => r.Passed) && unchanged;
EvidenceFiles.Save(Path.Combine(output, "result.json"), new { schemaVersion = 1, runId, mode = actual ? "actual-representatives-plus-explicit-mocks" : "mock",
    designSha256 = designHash, representativeSmokeOnly = smoke, datasetFingerprint, sourceHashes = before, sourceHashesUnchanged = unchanged, matchingMockEvidence = mockEvidence,
    cases = results, allPassed, actualModelRequests = results.Where(r => r.ActualInference).Sum(r => r.ModelRequests),
    mockModelRequests = results.Where(r => !r.ActualInference).Sum(r => r.ModelRequests),
    humanReviewStatus = "Pending", externalNotificationsDelivered = 0, actualAndroidVerified = false, realCustomerWrites = 0,
    regionalPreviewOperationalApplied = false, weightsTrained = false, priorBudgetsReused = false });
Console.WriteLine(JsonSerializer.Serialize(new { runId, allPassed, cases = results.Count,
    actualModelRequests = results.Where(r => r.ActualInference).Sum(r => r.ModelRequests), output }));
if (!allPassed) Environment.ExitCode = 1;

void RecordFailure(string id, string dir, bool inferred, int calls, Exception ex)
{
    File.WriteAllText(Path.Combine(dir, "failure.txt"), ex.ToString());
    results.Add(new(id, false, inferred, calls, ex.GetType().Name));
    Console.WriteLine(JsonSerializer.Serialize(new { caseId = id, passed = false, type = ex.GetType().Name }));
}
static decimal Distance(JsonElement a, JsonElement b) => new RegionalGeometricRoutes().CalculateDistanceKm(Point(a, "pickup"), Point(b, "pickup"))!.Value;
static 배차경로좌표 Point(JsonElement r, string field) => new(r.GetProperty(field).GetProperty("latitude").GetDecimal(), r.GetProperty(field).GetProperty("longitude").GetDecimal());
static 픽업하차경로작업 Job(JsonElement r, string id, DateTimeOffset now)
{
    var pickup = Point(r, "pickup"); var dropoff = Point(r, "syntheticDestination");
    var pickupKey = 음식배달권정책.판정(new(pickup.Latitude, pickup.Longitude), null).배달권키;
    var dropoffKey = 음식배달권정책.판정(new(dropoff.Latitude, dropoff.Longitude), null).배달권키;
    return new(id, "public-store-reference", pickup, now.UtcDateTime.AddMinutes(15), "synthetic-destination", dropoff,
        now.UtcDateTime.AddMinutes(45), now.UtcDateTime.AddMinutes(5), 30m, pickupKey, dropoffKey);
}
static 음식배달수요참여입력 Demand(string family, DateTimeOffset now, int region)
{
    음식배달수요기사입력 Driver(string id, bool active, int wait, int assigned) => new()
    {
        기사Id = id, 기존활동여부 = active, 운행시작시각Utc = now.AddHours(-2), 대기시작시각Utc = now.AddMinutes(-wait),
        마지막관측시각Utc = now, 남은수행시간분 = active && family == "shortage" ? 20m : 0m, 최근시간창배차수 = assigned
    };
    return new() { 권역Key = $"synthetic-region-{region + 1}", 판정시각Utc = now, 수요관측시각Utc = now,
        대기주문수 = family == "shortage" ? 12 : family == "multi" ? 4 : 1,
        최근시간창신규주문수 = family == "shortage" ? 8 : family == "multi" ? 2 : 0,
        기사목록 = [Driver("synthetic-active-older", true, 15, 1), Driver("synthetic-active-recent", true, 5, 2),
            Driver("synthetic-new-old", false, family == "long-wait" ? 45 : 10, 0), Driver("synthetic-new-recent", false, 1, 0)] };
}
static object MatchingMock(string root, Dictionary<string, string> sources)
{
    var runs = Path.Combine(root, "runs");
    foreach (var path in Directory.Exists(runs) ? Directory.GetFiles(runs, "result.json", SearchOption.AllDirectories).OrderDescending() : Enumerable.Empty<string>())
    {
        using var doc = JsonDocument.Parse(File.ReadAllBytes(path)); var r = doc.RootElement;
        if (!r.TryGetProperty("mode", out var mode) || mode.GetString() != "mock" || !r.GetProperty("allPassed").GetBoolean()
            || !r.GetProperty("sourceHashesUnchanged").GetBoolean()) continue;
        var h = r.GetProperty("sourceHashes");
        if (h.EnumerateObject().Count() != sources.Count || sources.Any(p => !h.TryGetProperty(p.Key, out var v) || v.GetString() != p.Value)) continue;
        return new { runId = r.GetProperty("runId").GetString(), reportSha256 = EvidenceFiles.Hash(File.ReadAllBytes(path)), currentSourcesAndDatasetMatch = true };
    }
    throw new InvalidOperationException("Current source and dataset require a successful --mock run before actual inference.");
}
internal sealed record CaseResult(string CaseId, bool Passed, bool ActualInference, int ModelRequests, string Status);
