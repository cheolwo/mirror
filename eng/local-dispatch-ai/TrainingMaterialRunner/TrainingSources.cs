using System.Buffers.Binary;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

internal sealed class TrainingSources
{
    public const string DesignSha256 = "4604b837968a7e303d3e43d916f28b7c8d520d5c75caf8d82d70947a3b8b44f8";
    public const string FormatVersion = "dispatch-training-raw/v1";
    private const string ApprovedDatasetSha256 = "6a9c5e01bcc9dd84e2f30353c6cc3d88d5d9aa9853eed441b5bca0158ffa8bff";
    private const string PlanFolder = "docs/AI/Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/";
    private readonly SortedDictionary<string, string> paths = new(StringComparer.Ordinal);
    public SortedDictionary<string, string> Hashes { get; } = new(StringComparer.Ordinal);
    public required IReadOnlyList<JsonElement> Records { get; init; }
    public required string DatasetSha256 { get; init; }
    public string Fingerprint { get; private set; } = "";
    public string OutputDirectory { get; private set; } = "";

    public static TrainingSources Open(string datasetArgument)
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Ssalddel/Ssalddel.csproj"))) directory = directory.Parent;
        var repo = directory?.FullName ?? throw new InvalidOperationException("RepositoryRootRequired");
        var delivery = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ChatGPT", "배달");
        var dataRoot = Path.Combine(repo, "artifacts/local/local-dispatch-ai/regional-management-r50/datasets");
        var datasetPath = SafePath(datasetArgument, dataRoot);
        if (Path.GetFileName(datasetPath) != "dataset.json") throw new InvalidOperationException("PreparedDatasetRequired");
        var payload = File.ReadAllBytes(datasetPath);
        if (Hash(payload) != ApprovedDatasetSha256) throw new InvalidOperationException("ApprovedDatasetMismatch");
        using var data = JsonDocument.Parse(payload);
        using var summary = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(datasetPath)!, "summary.json")));
        var metadata = data.RootElement.GetProperty("metadata");
        if (summary.RootElement.GetProperty("datasetSha256").GetString() != Hash(payload)
            || new DirectoryInfo(Path.GetDirectoryName(datasetPath)!).Name != metadata.GetProperty("sourceFingerprint").GetString()
            || !metadata.GetProperty("localVerificationOnly").GetBoolean()
            || metadata.GetProperty("realCustomerLocationsUsed").GetInt32() != 0
            || metadata.GetProperty("realDriverLocationsUsed").GetInt32() != 0
            || metadata.GetProperty("historicalValuesAreCurrentPrices").GetBoolean()
            || metadata.GetProperty("visitMenuSummariesAllocated").GetBoolean())
            throw new InvalidOperationException("DatasetContractMismatch");
        var records = data.RootElement.GetProperty("records").EnumerateArray().Select(x => x.Clone()).ToArray();
        if (records.Length != 504 || records.Select(x => x.GetProperty("branchId").GetString()).Distinct().Count() != 339
            || records.Select(x => x.GetProperty("recordId").GetString()).Distinct().Count() != 504
            || records.Any(x => !x.GetProperty("syntheticDestination").GetProperty("notActualCustomerLocation").GetBoolean())
            || records.Sum(x => x.GetProperty("historical").GetProperty("menuItems").GetArrayLength()) != 488)
            throw new InvalidOperationException("DatasetScopeMismatch");
        foreach (var record in records)
        {
            if (!Regex.IsMatch(record.GetProperty("recordId").GetString()!, "^replay-record-[0-9a-f]{64}$")
                || !Regex.IsMatch(record.GetProperty("branchId").GetString()!, "^replay-branch-[0-9a-f]{64}$"))
                throw new InvalidOperationException("SourceIdentityMismatch");
            foreach (var point in new[] { "pickup", "syntheticDestination" })
            {
                var p = record.GetProperty(point);
                if (p.GetProperty("latitude").GetDecimal() is < 33 or > 39 || p.GetProperty("longitude").GetDecimal() is < 124 or > 132)
                    throw new InvalidOperationException("SourceCoordinateMismatch");
            }
        }
        var result = new TrainingSources { Records = records, DatasetSha256 = Hash(payload) };
        result.Pin(Path.GetRelativePath(repo, datasetPath).Replace('\\', '/'), datasetPath);
        result.Pin(Path.GetRelativePath(repo, Path.Combine(Path.GetDirectoryName(datasetPath)!, "summary.json")).Replace('\\', '/'),
            Path.Combine(Path.GetDirectoryName(datasetPath)!, "summary.json"));
        using (var work = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(repo, PlanFolder + "training-materials.r51.work-order.json"))))
        {
            if (!work.RootElement.GetProperty("acceptedInCurrentThread").GetBoolean()
                || !work.RootElement.GetProperty("implementationAllowed").GetBoolean()
                || work.RootElement.GetProperty("designSha256").GetString() != DesignSha256
                || work.RootElement.GetProperty("datasetSha256").GetString() != ApprovedDatasetSha256
                || work.RootElement.GetProperty("modelCallsAllowed").GetInt32() != 0
                || work.RootElement.GetProperty("weightTrainingAllowed").GetBoolean()
                || work.RootElement.GetProperty("operationalChangesAllowed").GetBoolean())
                throw new InvalidOperationException("AcceptedWorkOrderRequired");
        }
        result.Pin(PlanFolder + "training-materials.implementation.r51.md", Path.Combine(repo, PlanFolder + "training-materials.implementation.r51.md"), DesignSha256);
        var sourceRows = metadata.GetProperty("sourceFingerprints").EnumerateArray().ToArray();
        if (sourceRows.Length != 29) throw new InvalidOperationException("HistoricalSourceCoverageMismatch");
        foreach (var source in sourceRows)
        {
            var relative = source.GetProperty("relativePath").GetString()!;
            if (relative.Contains('\\') || relative.Contains(':') || relative.Split('/').Contains("..")
                || !(relative.StartsWith("artifacts/local/delivery-history-replay-r54/", StringComparison.Ordinal)
                    || relative.StartsWith("edit-kit/branch-deliveries-all-20261010/", StringComparison.Ordinal)))
                throw new InvalidOperationException("UnexpectedHistoricalSource");
            var boundary = relative.StartsWith("artifacts/", StringComparison.Ordinal) ? repo : delivery;
            result.Pin(relative, SafePath(Path.Combine(boundary, relative), boundary), source.GetProperty("sha256").GetString());
        }
        const string ledger = "edit-kit/branch-deliveries-all-20261010/delivery-count-ledger.json";
        result.Pin(ledger, SafePath(Path.Combine(delivery, ledger), delivery), metadata.GetProperty("sourceLedgerSha256").GetString());
        const string exporter = "eng/local-dispatch-ai/RegionalScenarioRunner/prepare_regional_dataset.py";
        result.Pin(exporter, Path.Combine(repo, exporter), metadata.GetProperty("toolSha256").GetString());
        foreach (var relative in new[] {
            "Ssalddel.Domain/배차/음식배달수요참여Policy.cs", "Ssalddel.Contracts/Common/Dispatch/음식배달수요참여Contracts.cs",
            "Ssalddel/Services/Dispatch/AI/음식권역배차관리Service.cs", "Ssalddel/Services/Dispatch/AI/Ollama음식권역배차AI.cs",
            "Ssalddel/Services/Dispatch/AI/음식권역배차OllamaOptions.cs", "Ssalddel/Services/Dispatch/AI/FoodDispatchOllamaOptions.cs",
            "Ssalddel/Services/Dispatch/Recommendation/멀티배차조합Service.cs", "Ssalddel/Services/Dispatch/Recommendation/픽업하차경로최적화Service.cs",
            "Ssalddel/Services/Dispatch/Recommendation/음식권역복귀일정평가Service.cs", "Ssalddel/Services/Dispatch/Queue/QueueOptions.cs",
            "Ssalddel/Services/Food/음식주문현재조리Policy.cs", "Ssalddel/Services/Options/FoodDriverRoutePricingOptions.cs",
            "Ssalddel/Services/Dispatch/Queue/음식배달후보검토결과.cs", "Ssalddel/Services/Dispatch/Queue/음식배달수요참여Service.cs" })
            result.Pin(relative, Path.Combine(repo, relative));
        foreach (var (folder, pattern) in new[] {
            ("eng/local-dispatch-ai/TrainingMaterialRunner", "*.cs"), ("eng/local-dispatch-ai/TrainingMaterialRunner", "*.csproj"),
            ("Ssalddel/Services/Dispatch/Queue", "음식배달배차업무정책*.cs"),
            ("Ssalddel.Tests/Services/Dispatch/Queue", "음식배달배차업무정책Tests*.cs"),
            ("Ssalddel/Services/Dispatch/Coordination", "*판단근거*.cs"),
            ("Ssalddel/Services/Dispatch/Coordination", "음식배달권정책.cs"),
            ("Ssalddel/Services/Dispatch/Coordination", "국내행정구역배달권Catalog*.cs"),
            ("Ssalddel/Services/DeliveryZones", "*.cs") })
            foreach (var path in Directory.GetFiles(Path.Combine(repo, folder), pattern))
                result.Pin(Path.GetRelativePath(repo, path).Replace('\\', '/'), path);
        result.Fingerprint = Hash(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { sourceHashes = result.Hashes, fixedSeed = 51, formatVersion = FormatVersion },
            new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping })));
        result.OutputDirectory = SafePath(Path.Combine(repo, "artifacts/local/local-dispatch-ai/training-materials-r51/raw", result.Fingerprint), repo);
        var check = new ProcessStartInfo("git") { WorkingDirectory = repo, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in new[] { "check-ignore", "-q", "--", Path.GetRelativePath(repo, result.OutputDirectory).Replace('\\', '/') }) check.ArgumentList.Add(arg);
        using var process = Process.Start(check) ?? throw new InvalidOperationException("GitIgnoreCheckFailed");
        process.WaitForExit();
        if (process.ExitCode != 0) throw new InvalidOperationException("OutputMustBeGitIgnored");
        result.VerifyUnchanged();
        return result;
    }

    private void Pin(string key, string path, string? expected = null)
    {
        var hash = Hash(File.ReadAllBytes(path));
        if (expected is not null && expected != hash) throw new InvalidOperationException("PinnedSourceChanged");
        if (paths.TryGetValue(key, out var previous) && previous != path) throw new InvalidOperationException("SourceCollision");
        paths[key] = path; Hashes[key] = hash;
    }

    public void VerifyUnchanged()
    {
        foreach (var pair in paths)
            if (Hash(File.ReadAllBytes(pair.Value)) != Hashes[pair.Key]) throw new InvalidOperationException("SourceChangedDuringPreparation");
    }

    public static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    public static string Split(string branchId)
    {
        var bucket = BinaryPrimitives.ReadUInt32BigEndian(SHA256.HashData(Encoding.UTF8.GetBytes(branchId))) % 100;
        return bucket < 70 ? "train" : bucket < 85 ? "validation" : "test";
    }

    public static string SafePath(string path, string boundary)
    {
        var full = Path.GetFullPath(path); var root = Path.GetFullPath(boundary);
        if (!full.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("PathOutsideBoundary");
        for (var item = new DirectoryInfo(Path.GetDirectoryName(full)!); item is not null; item = item.Parent)
            if (item.Exists && (item.Attributes & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException("LinkPathForbidden");
        if ((File.Exists(full) || Directory.Exists(full)) && (File.GetAttributes(full) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidOperationException("LinkPathForbidden");
        return full;
    }

    public static void WriteImmutable(string path, byte[] payload)
    {
        SafePath(path, Path.GetDirectoryName(path)!);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        SafePath(path, Path.GetDirectoryName(path)!);
        if (File.Exists(path))
        {
            if (!File.ReadAllBytes(path).AsSpan().SequenceEqual(payload)) throw new InvalidOperationException("ExistingOutputDifferent");
            return;
        }
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        stream.Write(payload); stream.Flush(true);
    }
}
