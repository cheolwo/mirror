using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Ssalddel.Tests.Services.Dispatch.Queue;

if (args.Length != 1 || args[0] != "--actual")
{
    Console.WriteLine("No inference or dispatch executed. Use --actual for the accepted r48 isolated application verification.");
    return;
}
var repo = new DirectoryInfo(Directory.GetCurrentDirectory());
while (repo is not null && !File.Exists(Path.Combine(repo.FullName, "Ssalddel", "Ssalddel.csproj"))) repo = repo.Parent;
if (repo is null) throw new InvalidOperationException("Repository root required.");
var root = Path.Combine(repo.FullName, "artifacts", "local", "local-dispatch-ai", "candidate-application-r48");
Directory.CreateDirectory(root);
var attempts = Path.Combine(root, "attempts");
Directory.CreateDirectory(attempts);
var lockPath = Path.Combine(attempts, "reservation.lock");
using var lease = new FileStream(lockPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
var runId = DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ") + "-" + Guid.NewGuid().ToString("N")[..8];
string? reservation = null;
try
{
    for (var i = 1; i <= 2; i++)
    {
        var path = Path.Combine(attempts, $"attempt-{i:000}.json");
        try
        {
            using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            JsonSerializer.Serialize(stream, new { runId, reservedAtUtc = DateTime.UtcNow, maxAttempts = 2,
                includesFailedAndInterrupted = true, isolatedLocalApplicationOnly = true });
            reservation = path;
            break;
        }
        catch (IOException) when (File.Exists(path)) { }
    }
    if (reservation is null) throw new InvalidOperationException("r48 cumulative inference budget exhausted.");
    var output = Path.Combine(root, "runs", runId);
    Directory.CreateDirectory(output);
    var sourcePaths = new[]
    {
        "Ssalddel/Services/Dispatch/AI/FoodDispatchOllamaOptions.cs",
        "Ssalddel/Services/Dispatch/AI/IFoodDispatchCandidateAI.cs",
        "Ssalddel/Services/Dispatch/AI/OllamaFoodDispatchCandidateAI.cs",
        "Ssalddel/Services/Dispatch/Queue/음식배달배차업무정책.cs",
        "Ssalddel/Services/Dispatch/Queue/배차추천후보선정Service.cs",
        "Ssalddel/Services/Dispatch/Queue/배차대기원장전환Service.cs",
        "Ssalddel/Services/Dispatch/Queue/배차대기원장전환Service.Recommendation.cs",
        "Ssalddel.Tests/Services/Dispatch/Queue/FoodDispatchOllamaApplicationTests.cs"
    };
    Dictionary<string, string> Sources() => sourcePaths.ToDictionary(x => x, x =>
        Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(Path.Combine(repo.FullName, x)))));
    var before = Sources();
    using var http = new HttpClient(new HttpClientHandler { UseProxy = false, AllowAutoRedirect = false,
        UseCookies = false }) { Timeout = Timeout.InfiniteTimeSpan };
    var stopwatch = Stopwatch.StartNew();
    try
    {
        var evidence = await 음식배달배차업무정책Tests.RunOllamaApplicationAsync(http,
            Path.Combine(output, "dispatch.sqlite3"));
        var after = Sources();
        if (before.Any(pair => after[pair.Key] != pair.Value)) throw new InvalidOperationException("Source changed during verification.");
        var result = new { schemaVersion = 1, runId, synthetic = true, model = "qwen3:1.7b",
            elapsedSeconds = stopwatch.Elapsed.TotalSeconds, sourceHashes = before, evidence,
            actualRecommendationService = true, isolatedSqliteSavedAndRequeried = true,
            externalNotificationsSent = 0, realCustomerWrites = 0, driverAcceptance = false,
            weightsTrained = false, actualOperatingServerStarted = false };
        using var saved = new FileStream(Path.Combine(output, "result.json"), FileMode.CreateNew);
        JsonSerializer.Serialize(saved, result, new JsonSerializerOptions { WriteIndented = true });
        Console.WriteLine(JsonSerializer.Serialize(new { runId, evidence.ModelRequests, evidence.AISelectionApplied,
            evidence.RecommendedDriverId, evidence.FrozenFee, evidence.RecommendationRound, output }));
    }
    catch (Exception ex)
    {
        using var failed = new FileStream(Path.Combine(output, "failure.json"), FileMode.CreateNew);
        JsonSerializer.Serialize(failed, new { runId, exceptionType = ex.GetType().Name,
            elapsedSeconds = stopwatch.Elapsed.TotalSeconds, attemptConsumed = true });
        throw;
    }
}
finally
{
    lease.Dispose();
    File.Delete(lockPath);
}
