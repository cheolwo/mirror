using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Ssalddel.Tests.Services.Dispatch;

if (args.Length != 1 || args[0] != "--local-synthetic")
{
    Console.WriteLine("실행하지 않았습니다. --local-synthetic만 허용합니다. USB/Azure/실제 FCM/Ollama 실행 옵션은 없습니다.");
    return;
}

var root = new DirectoryInfo(Directory.GetCurrentDirectory());
while (root is not null && !File.Exists(Path.Combine(root.FullName, "Ssalddel", "Ssalddel.csproj"))) root = root.Parent;
if (root is null) throw new InvalidOperationException("Hongdal 저장소에서 실행해야 합니다.");
const string planRef = "docs/AI/Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/dispatch-device-cloud-verification.plan.r75.md";
const string orderRef = "docs/AI/Planning/공통/PLAN-OPERATIONS-NEIGHBORHOOD-MICRO-HUB/dispatch-device-cloud-verification.r75.work-order.json";
using (var order = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root.FullName, orderRef))))
{
    var approved = order.RootElement;
    if (approved.GetProperty("status").GetString() != "ReadyToDispatch"
        || approved.GetProperty("planStatus").GetString() != "ApprovedForLocalExecution"
        || approved.GetProperty("planRef").GetString() != planRef
        || approved.GetProperty("planSha256").GetString() != Hash(File.ReadAllBytes(Path.Combine(root.FullName, planRef))))
        throw new InvalidOperationException("승인된 r75 기획 해시와 작업 명세가 일치하지 않습니다.");
}

var evidenceRoot = Path.Combine(root.FullName, "artifacts", "local", "dispatch-verification-r75-implementation");
var runId = DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ") + "-" + Guid.NewGuid().ToString("N")[..8];
var output = Path.Combine(evidenceRoot, "runs", runId);
Directory.CreateDirectory(output);
var sourcePaths = new[]
{
    planRef, orderRef,
    "eng/Ssalddel.DispatchVerificationR75/Program.cs",
    "eng/Ssalddel.DispatchVerificationR75/Ssalddel.DispatchVerificationR75.csproj",
    "Ssalddel.Tests/Services/Dispatch/DispatchWorkflowR75Tests.cs",
    "Ssalddel/Services/Dispatch/Engine/운영체제배차EngineCatalog.cs",
    "Ssalddel/Services/Dispatch/Engine/음식배달배차엔진.cs",
    "Ssalddel/Services/Dispatch/Engine/화물용달배차엔진.cs",
    "Ssalddel/Services/Dispatch/Queue/음식배달배차업무정책.cs",
    "Ssalddel/Services/Dispatch/Queue/용달운송배차업무정책.cs",
    "Ssalddel/Services/Dispatch/Queue/배차추천후보선정Service.cs",
    "Ssalddel/Services/Dispatch/Queue/배차대기원장전환Service.cs",
    "Ssalddel/Services/Dispatch/Queue/배차대기원장전환Service.Recommendation.cs",
    "Ssalddel/Services/Dispatch/Notification/배차추천알림Service.cs",
    "FDriverApp/PageModels/MainPageModel.cs",
    "FDriverApp/PageModels/MainPageModel.FoodNotifications.cs",
    "FDriverApp/Services/FoodDeliveryDriverApiService.cs"
}.Concat(Directory.GetFiles(Path.Combine(root.FullName, "Ssalddel.Tests", "Services", "Dispatch", "AI"), "FoodDispatchWorkflowScenarioFixture*.cs")
    .Concat(Directory.GetFiles(Path.Combine(root.FullName, "Ssalddel.Tests", "Clients"), "FDriverServerDispatchScenario*.cs"))
    .Select(path => Path.GetRelativePath(root.FullName, path).Replace('\\', '/')))
    .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
Dictionary<string, string> SourceHashes() => sourcePaths.ToDictionary(path => path,
    path => Hash(File.ReadAllBytes(Path.Combine(root.FullName, path))), StringComparer.Ordinal);
var before = SourceHashes();
var results = new List<object>();
var passedCases = new HashSet<string>(StringComparer.Ordinal);
string[] enginePolicyCases = ["food-normal-same-region", "food-normal-adjacent-region", "food-demand-capacity",
    "cargo-engine-recommendation", "small-goods-normal-accept"];
var recoveryCases = DispatchWorkflowR75Tests.ScenarioIds.Except(enginePolicyCases, StringComparer.Ordinal).ToArray();
var allPassed = true;
foreach (var scenarioId in DispatchWorkflowR75Tests.ScenarioIds)
{
    var scenarioOutput = Path.Combine(output, scenarioId);
    Directory.CreateDirectory(scenarioOutput);
    var timer = Stopwatch.StartNew();
    try
    {
        var evidence = await DispatchWorkflowR75Tests.RunScenarioAsync(scenarioId, Path.Combine(scenarioOutput, "workflow.sqlite3"));
        var result = new { scenarioId, passed = evidence.Passed, elapsedSeconds = timer.Elapsed.TotalSeconds, evidence };
        Save(Path.Combine(scenarioOutput, "result.json"), result);
        results.Add(result);
        if (evidence.Passed) passedCases.Add(scenarioId);
        Console.WriteLine(JsonSerializer.Serialize(new { scenarioId, result.passed, evidence.ModelRequests }));
    }
    catch (Exception ex)
    {
        allPassed = false;
        // 합성 계정만 사용한다. 진단 전문은 로컬에 보존하고 콘솔에는 오류 형식만 출력한다.
        File.WriteAllText(Path.Combine(scenarioOutput, "failure.txt"), ex.ToString());
        var failure = new { scenarioId, passed = false, elapsedSeconds = timer.Elapsed.TotalSeconds, exceptionType = ex.GetType().Name };
        Save(Path.Combine(scenarioOutput, "result.json"), failure);
        results.Add(failure);
        Console.WriteLine(JsonSerializer.Serialize(failure));
    }
}
var after = SourceHashes();
var sourcesUnchanged = before.All(x => after[x.Key] == x.Value);
var report = new
{
    schemaVersion = 1, runId, execution = "isolated-local-synthetic-external-ports", syntheticOnly = true,
    approvalWorkOrder = orderRef, sourceHashes = before, sourceHashesUnchanged = sourcesUnchanged,
    actualEngineSelectionEntry = "Production engine catalog and candidate policy; see per-case pass/failure",
    enginePolicyCases, recoveryCases,
    enginePolicyCasesVerified = sourcesUnchanged && enginePolicyCases.All(passedCases.Contains),
    recoveryCasesVerified = sourcesUnchanged && recoveryCases.All(passedCases.Contains),
    directDesignatedDriverOfferUsed = false,
    sqliteRelationalFixture = true, actualLoopbackHttp = true, productionMobilePageModel = true,
    actualMysql = false, actualMongo = false, productionJwt = false, productOperatingHostStarted = false,
    realOllamaInference = false, modelProcessStarted = false, realFcm = false, realSoundHeard = false,
    deviceUiRendered = false, usbUsed = false, azureChanged = false, operationalPayments = false,
    cargoScope = "General cargo: SQL source seed -> production quote/update/postpay commands -> actual engine -> actual accept/replay command. Small goods: production registration -> real straight-line driver policy -> actual engine/accept. No cargo mobile HTTP/photo/completion proof",
    results, allPassed = allPassed && sourcesUnchanged && results.Count == DispatchWorkflowR75Tests.ScenarioIds.Length
};
Save(Path.Combine(output, "result.json"), report);
Save(Path.Combine(evidenceRoot, "latest-" + runId + ".json"), new { runId, report.allPassed, output });
Console.WriteLine(JsonSerializer.Serialize(new { runId, report.allPassed, cases = results.Count, output }));
if (!report.allPassed) Environment.ExitCode = 1;

static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
static void Save(string path, object value)
{
    using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
    JsonSerializer.Serialize(file, value, new JsonSerializerOptions { WriteIndented = true });
}
