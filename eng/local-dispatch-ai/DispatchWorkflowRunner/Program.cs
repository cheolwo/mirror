using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ssalddel.Tests.Clients;

string[] scenarios = ["same-region", "adjacent-region", "demand-capacity"];
if (args.Length != 1 || args[0] is not ("--mock" or "--actual"))
{
    Console.WriteLine("No inference or workflow executed. --mock verifies three isolated workflows; --actual uses the accepted r49 local model budget.");
    return;
}
var actual = args[0] == "--actual";
var repo = new DirectoryInfo(Directory.GetCurrentDirectory());
while (repo is not null && !File.Exists(Path.Combine(repo.FullName, "Ssalddel", "Ssalddel.csproj"))) repo = repo.Parent;
if (repo is null) throw new InvalidOperationException("Repository root required.");
const string planPath = "docs/AI/Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/workflow-scenarios.implementation.r49.md";
const string orderPath = "docs/AI/Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/workflow-scenarios.r49.work-order.json";
const string acceptedDesign = "d9da9dafa4a4ee1c80be91856d6a9e096ba6041a714e0bbbde60d065fd775423";
using (var order = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(repo.FullName, orderPath))))
{
    if (!order.RootElement.GetProperty("acceptedInCurrentThread").GetBoolean()
        || !order.RootElement.GetProperty("implementationAllowed").GetBoolean()
        || order.RootElement.GetProperty("designSha256").GetString() != acceptedDesign
        || Hash(File.ReadAllBytes(Path.Combine(repo.FullName, planPath))) != acceptedDesign)
        throw new InvalidOperationException("The accepted r49 design and work order must match.");
}
var root = Path.Combine(repo.FullName, "artifacts", "local", "local-dispatch-ai", "workflow-scenarios-r49");
Directory.CreateDirectory(root);
var runId = DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ") + "-" + (actual ? "actual-" : "mock-") + Guid.NewGuid().ToString("N")[..8];
var output = Path.Combine(root, "runs", runId);
Directory.CreateDirectory(output);
string[] sourcePaths = [
    planPath,
    "Ssalddel/Services/Dispatch/AI/FoodDispatchOllamaOptions.cs",
    "Ssalddel/Services/Dispatch/AI/IFoodDispatchCandidateAI.cs",
    "Ssalddel/Services/Dispatch/AI/OllamaFoodDispatchCandidateAI.cs",
    "Ssalddel/Services/Dispatch/Queue/음식배달배차업무정책.cs",
    "Ssalddel/Services/Dispatch/Queue/배차추천후보선정Service.cs",
    "Ssalddel/Services/Dispatch/Queue/배차대기원장전환Service.cs",
    "Ssalddel/Services/Dispatch/Queue/배차대기원장전환Service.Recommendation.cs",
    "Ssalddel/Services/Dispatch/Notification/배차추천알림Service.cs",
    "Ssalddel/Services/Dispatch/Recommendation/FoodDeliveryDriverWorkService.cs",
    "Ssalddel/Controllers/Driver/Food/음식배달기사업무Controller.cs",
    "Ssalddel/Services/Food/EfSsalddelFoodOrderStore.cs",
    "FDriverApp/PageModels/MainPageModel.cs",
    "FDriverApp/PageModels/MainPageModel.FoodNotifications.cs",
    "FDriverApp/Services/FoodDeliveryDriverApiService.cs",
    "FDriverApp/Services/FDriverFoodPushReceiver.cs",
    "eng/local-dispatch-ai/DispatchWorkflowRunner/Program.cs",
    "eng/local-dispatch-ai/DispatchWorkflowRunner/DispatchWorkflowRunner.csproj"
];
var extraPaths = Directory.GetFiles(Path.Combine(repo.FullName, "Ssalddel.Tests", "Services", "Dispatch", "AI"), "FoodDispatchWorkflowScenario*.cs")
    .Concat(Directory.GetFiles(Path.Combine(repo.FullName, "Ssalddel.Tests", "Clients"), "FDriverServerDispatchScenario*.cs"))
    .Select(path => Path.GetRelativePath(repo.FullName, path).Replace('\\', '/'));
sourcePaths = sourcePaths.Concat(extraPaths).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
Dictionary<string, string> Sources() => sourcePaths.ToDictionary(path => path,
    path => Hash(File.ReadAllBytes(Path.Combine(repo.FullName, path))), StringComparer.Ordinal);
var before = Sources();
// Actual inference is only allowed after the same harness passed all primary mock workflows.
var mockPreflight = actual ? FindMatchingMockEvidence(root, before, scenarios) : null;
var results = new List<object>();
var allPassed = true;
foreach (var scenarioId in scenarios)
{
    var scenarioOutput = Path.Combine(output, scenarioId);
    Directory.CreateDirectory(scenarioOutput);
    using var handler = new WorkflowModelHandler(actual, root, runId, scenarioId, scenarioOutput);
    using var modelHttp = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
    var timer = Stopwatch.StartNew();
    try
    {
        var evidence = await FDriverServerDispatchScenarioTests.RunScenarioAsync(scenarioId, modelHttp,
            Path.Combine(scenarioOutput, "workflow.sqlite3"));
        var expectedCalls = scenarioId == "demand-capacity" ? 0 : 1;
        if (handler.Calls != expectedCalls) throw new InvalidOperationException("Unexpected model requests for the scenario's current eligible candidate set.");
        var result = new { scenarioId, passed = true, elapsedSeconds = timer.Elapsed.TotalSeconds,
            modelRequests = handler.Calls, evidence };
        Save(Path.Combine(scenarioOutput, "result.json"), result);
        results.Add(result);
        Console.WriteLine(JsonSerializer.Serialize(new { scenarioId, passed = true, modelRequests = handler.Calls }));
    }
    catch (Exception ex)
    {
        allPassed = false;
        // Keep the full exception locally for diagnosis, never pass it to the model.
        File.WriteAllText(Path.Combine(scenarioOutput, "failure.txt"), ex.ToString());
        var failure = new { scenarioId, passed = false, elapsedSeconds = timer.Elapsed.TotalSeconds,
            modelRequests = handler.Calls, exceptionType = ex.GetType().Name };
        Save(Path.Combine(scenarioOutput, "result.json"), failure);
        results.Add(failure);
        Console.WriteLine(JsonSerializer.Serialize(failure));
        // Do not consume more actual inference attempts after a broken harness or exhausted budget.
        if (actual) break;
    }
}
var after = Sources();
var unchanged = before.All(pair => after[pair.Key] == pair.Value);
Save(Path.Combine(output, "result.json"), new { schemaVersion = 1, runId, synthetic = true,
    inference = actual ? "actual-local-ollama" : "mock-http-response", model = "qwen3:1.7b",
    designSha256 = acceptedDesign, sourceHashes = before, sourceHashesUnchanged = unchanged,
    matchingMockPreflight = mockPreflight,
    scenarios = results, allPassed = allPassed && unchanged && results.Count == scenarios.Length,
    isolatedSqliteHarness = true, loopbackHttpHarness = true,
    loopbackHttpVerified = allPassed && unchanged && results.Count == scenarios.Length,
    fixtureAuthenticationOnly = true,
    externalNotificationsDelivered = 0, realCustomerWrites = 0, actualAndroidBannerVerified = false,
    actualFcmDeliveryVerified = false, productOperatingHostStarted = false,
    weightsTrained = false, priorBudgetsReused = false });
Console.WriteLine(JsonSerializer.Serialize(new { runId, allPassed = allPassed && unchanged && results.Count == scenarios.Length, output }));
if (!allPassed || !unchanged || results.Count != scenarios.Length) Environment.ExitCode = 1;

static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
static void Save(string path, object value)
{
    using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
    JsonSerializer.Serialize(stream, value, new JsonSerializerOptions { WriteIndented = true });
}
static object FindMatchingMockEvidence(string root, Dictionary<string, string> sources, string[] scenarioIds)
{
    var runs = Path.Combine(root, "runs");
    foreach (var directory in Directory.GetDirectories(runs).OrderDescending(StringComparer.Ordinal))
    {
        var path = Path.Combine(directory, "result.json");
        if (!File.Exists(path)) continue;
        var bytes = File.ReadAllBytes(path);
        try
        {
            using var document = JsonDocument.Parse(bytes);
            var saved = document.RootElement;
            if (saved.GetProperty("inference").GetString() != "mock-http-response"
                || !saved.GetProperty("allPassed").GetBoolean()
                || !saved.GetProperty("sourceHashesUnchanged").GetBoolean()) continue;
            var hashes = saved.GetProperty("sourceHashes");
            if (hashes.EnumerateObject().Count() != sources.Count
                || sources.Any(pair => !hashes.TryGetProperty(pair.Key, out var value) || value.GetString() != pair.Value)) continue;
            var cases = saved.GetProperty("scenarios").EnumerateArray().ToArray();
            if (cases.Length != scenarioIds.Length || cases.Select(item => item.GetProperty("scenarioId").GetString())
                .Order(StringComparer.Ordinal).SequenceEqual(scenarioIds.Order(StringComparer.Ordinal)) is false) continue;
            if (cases.Any(item => !item.GetProperty("passed").GetBoolean()
                || item.GetProperty("modelRequests").GetInt32() != (item.GetProperty("scenarioId").GetString() == "demand-capacity" ? 0 : 1))) continue;
            return new { runId = saved.GetProperty("runId").GetString(), reportSha256 = Hash(bytes),
                sourceHashesMatched = true, allThreePrimaryMockWorkflowsPassed = true };
        }
        catch (JsonException) { }
        catch (KeyNotFoundException) { }
        catch (InvalidOperationException) { }
    }
    throw new InvalidOperationException("Run --mock successfully against the current source before --actual. No model call was started.");
}

sealed class WorkflowModelHandler(bool actual, string root, string runId, string scenarioId, string output) : HttpMessageHandler
{
    public int Calls { get; private set; }
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (Calls != 0) throw new InvalidOperationException("Only one model request is permitted per primary scenario.");
        if (request.Method != HttpMethod.Post || request.RequestUri != new Uri("http://127.0.0.1:12681/api/chat"))
            throw new InvalidOperationException("Only the accepted local Ollama endpoint is permitted.");
        var requestBytes = await request.Content!.ReadAsByteArrayAsync(ct);
        using (var json = JsonDocument.Parse(requestBytes))
        {
            if (json.RootElement.GetProperty("model").GetString() != "qwen3:1.7b"
                || json.RootElement.GetProperty("stream").GetBoolean())
                throw new InvalidOperationException("Unexpected model configuration.");
        }
        if (actual) Reserve();
        Calls++;
        File.WriteAllBytes(Path.Combine(output, "model-request.json"), requestBytes);
        HttpResponseMessage response;
        if (actual)
        {
            using var client = new HttpClient(new HttpClientHandler { UseProxy = false, AllowAutoRedirect = false, UseCookies = false })
                { Timeout = Timeout.InfiniteTimeSpan };
            using var forwarded = new HttpRequestMessage(request.Method, request.RequestUri)
                { Content = new ByteArrayContent(requestBytes) };
            forwarded.Content.Headers.ContentType = new("application/json");
            response = await client.SendAsync(forwarded, HttpCompletionOption.ResponseHeadersRead, ct);
            // The product client rejects more than 64 KiB. Preserve a bounded response and that rejection.
            await using var body = await response.Content.ReadAsStreamAsync(ct);
            var buffer = new byte[65537];
            var length = 0;
            while (length < buffer.Length)
            {
                var read = await body.ReadAsync(buffer.AsMemory(length), ct);
                if (read == 0) break;
                length += read;
            }
            response.Content.Dispose();
            response.Content = new ByteArrayContent(buffer.AsSpan(0, length).ToArray());
        }
        else response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(new { model = "qwen3:1.7b", done = true,
                message = new { role = "assistant", content = "{\"candidateToken\":\"C1\"}" } }), Encoding.UTF8, "application/json")
        };
        var responseBytes = await response.Content.ReadAsByteArrayAsync(ct);
        File.WriteAllBytes(Path.Combine(output, "model-response.json"), responseBytes);
        Save(Path.Combine(output, "model-exchange.json"), new { actual, status = (int)response.StatusCode,
            requestSha256 = Hash(requestBytes), responseSha256 = Hash(responseBytes),
            responseBytes = responseBytes.Length, responsePossiblyTruncated = responseBytes.Length == 65537 });
        return response;
    }

    private void Reserve()
    {
        var attempts = Path.Combine(root, "attempts");
        Directory.CreateDirectory(attempts);
        var path = Path.Combine(attempts, "reservation.lock");
        using var lease = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        try
        {
            for (var i = 1; i <= 4; i++)
            {
                var reservation = Path.Combine(attempts, $"attempt-{i:000}.json");
                try
                {
                    Save(reservation, new { runId, scenarioId, reservedAtUtc = DateTime.UtcNow,
                        cumulativeMaximum = 4, includesFailedAndInterrupted = true, previousBudgetsReused = false });
                    return;
                }
                catch (IOException) when (File.Exists(reservation)) { }
            }
            throw new InvalidOperationException("r49 cumulative inference budget exhausted.");
        }
        finally { lease.Dispose(); File.Delete(path); }
    }

    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static void Save(string path, object value)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        JsonSerializer.Serialize(stream, value, new JsonSerializerOptions { WriteIndented = true });
    }
}
