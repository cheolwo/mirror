using System.Net;
using System.Text.Json;
using CargoRegionalTrainingReview;
using Ssalddel.Contracts.Admin.DispatchTraining;

var repoRoot = FindRoot();
var dataRoot = Path.Combine(repoRoot, "artifacts/local/cargo-regional-training-r113/review");
var dataArgument = Array.IndexOf(args, "--data-root");
if (dataArgument >= 0) dataRoot = Path.GetFullPath(args[dataArgument + 1]);
var privateArtifacts = Path.GetFullPath(Path.Combine(repoRoot, "artifacts/local")) + Path.DirectorySeparatorChar;
if (!Path.GetFullPath(dataRoot).StartsWith(privateArtifacts, StringComparison.OrdinalIgnoreCase))
    throw new InvalidOperationException("PrivateArtifactDirectoryRequired");
if (args.Contains("--self-check", StringComparer.Ordinal))
{
    var result = await CargoTrainingSelfChecks.RunAsync(repoRoot);
    Console.WriteLine(JsonSerializer.Serialize(result, CargoTrainingStore.Json));
    return;
}
var planReview = args.Contains("--plan-review", StringComparer.Ordinal);
var store = new CargoTrainingStore(repoRoot, dataRoot, planReview: planReview);
if (args.Contains("--prepare", StringComparer.Ordinal))
{
    Console.WriteLine(JsonSerializer.Serialize(await store.LoadAsync(), CargoTrainingStore.Json));
    return;
}
if (args.Contains("--export-approved", StringComparer.Ordinal))
{
    Console.WriteLine(JsonSerializer.Serialize(await store.ExportApprovedAsync(), CargoTrainingStore.Json));
    return;
}
var compareIndex = Array.IndexOf(args, "--compare-first");
var compareCaseIndex = Array.IndexOf(args, "--compare-case");
if (compareCaseIndex >= 0)
{
    if (compareCaseIndex + 1 >= args.Length || string.IsNullOrWhiteSpace(args[compareCaseIndex + 1])
        || args[compareCaseIndex + 1].StartsWith("--", StringComparison.Ordinal))
        throw CargoTrainingStore.Error("CaseIdRequired");
    if (compareIndex >= 0) throw CargoTrainingStore.Error("ConflictingCompareOptions");
    var caseId = args[compareCaseIndex + 1];
    var w = await store.LoadAsync();
    var selected = w.Cases.SingleOrDefault(c => c.CaseId == caseId) ?? throw CargoTrainingStore.Error("CaseNotFound");
    w = await store.CompareAsync(selected.CaseId, selected.InputSha256);
    var current = w.Cases.Single(c => c.CaseId == caseId);
    Console.WriteLine(JsonSerializer.Serialize(new
    {
        w.DatasetSha256, current.CaseId, current.InputSha256, current.PlanSetSha256,
        current.Ai, current.Engine, humanApprovals = w.Cases.Count(c => c.ReviewCurrent), weightsTrained = false
    }, CargoTrainingStore.Json));
    return;
}
if (compareIndex >= 0)
{
    var count = Math.Clamp(int.Parse(args[compareIndex + 1]), 1, 33);
    var w = await store.LoadAsync();
    foreach (var item in w.Cases.Take(count))
    {
        w = await store.CompareAsync(item.CaseId, item.InputSha256);
        var current = w.Cases.Single(c => c.CaseId == item.CaseId);
        Console.WriteLine(JsonSerializer.Serialize(new { current.CaseId, current.Ai, current.Engine, weightsTrained = false }, CargoTrainingStore.Json));
    }
    return;
}
var port = 5595;
var portIndex = Array.IndexOf(args, "--port");
if (portIndex >= 0) port = int.Parse(args[portIndex + 1]);
if (port is < 1024 or > 65535) throw new InvalidOperationException("LoopbackPortRequired");
var origin = $"http://127.0.0.1:{port}";
var builder = WebApplication.CreateBuilder();
builder.WebHost.UseUrls(origin).UseStaticWebAssets();
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddSingleton<ICargoRegionalTrainingReviewClient>(store);
var app = builder.Build();
app.Use(async (context, next) =>
{
    if (context.Connection.RemoteIpAddress is not { } ip || !IPAddress.IsLoopback(ip)
        || !string.Equals(context.Request.Host.Value, $"127.0.0.1:{port}", StringComparison.OrdinalIgnoreCase)
        || (context.Request.Headers.Origin.Count > 0 && context.Request.Headers.Origin.ToString() != origin))
    {
        context.Response.StatusCode = 403;
        return;
    }
    context.Response.Headers.CacheControl = "no-store";
    context.Response.Headers.XContentTypeOptions = "nosniff";
    await next();
});
app.UseStaticFiles();
app.UseAntiforgery();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.MapGet("/health", () => new { status = "ready", scope = "local-cargo-training-review", operationalDispatchEnabled = false, weightsTrained = false });
Console.WriteLine($"Local cargo training review: {origin}/admin/freight-delivery/dispatch-ai-review/training");
await app.RunAsync();

static string FindRoot()
{
    for (var directory = new DirectoryInfo(Environment.CurrentDirectory); directory is not null; directory = directory.Parent)
        if (File.Exists(Path.Combine(directory.FullName, "Ssalddel/Ssalddel.csproj"))) return directory.FullName;
    throw new InvalidOperationException("RepositoryRootNotFound");
}
