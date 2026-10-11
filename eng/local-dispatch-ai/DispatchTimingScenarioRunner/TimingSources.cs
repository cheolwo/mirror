using System.Buffers.Binary;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

internal sealed class TimingSources
{
    public const string DesignSha256 = "a895d005306e9f077173bb235226ec28e6c6afd1e0efce690fae167d1e48a0be";
    public const string FormatVersion = "dispatch-timing-raw/v1";
    public const string SplitRule = "sha256-branch-first8-u32-mod100-70-15-15";
    private const string DatasetHash = "6a9c5e01bcc9dd84e2f30353c6cc3d88d5d9aa9853eed441b5bca0158ffa8bff";
    private const string Plan = "docs/AI/Planning/공통/PLAN-OPERATIONS-DISPATCH-CORE/";
    private readonly SortedDictionary<string, string> paths = new(StringComparer.Ordinal);
    public SortedDictionary<string, string> Hashes { get; } = new(StringComparer.Ordinal);
    public required IReadOnlyList<JsonElement> Records { get; init; }
    public required string DatasetSha256 { get; init; }
    public string Fingerprint { get; private set; } = "";
    public string OutputDirectory { get; private set; } = "";

    public static TimingSources Open(string datasetArgument)
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Ssalddel/Ssalddel.csproj"))) directory = directory.Parent;
        var repo = directory?.FullName ?? throw new InvalidOperationException("RepositoryRootRequired");
        var delivery = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ChatGPT", "배달");
        var datasetPath = SafePath(datasetArgument, Path.Combine(repo, "artifacts/local/local-dispatch-ai/regional-management-r50/datasets"));
        if (Path.GetFileName(datasetPath) != "dataset.json") throw new InvalidOperationException("PreparedDatasetRequired");
        var bytes = File.ReadAllBytes(datasetPath);
        if (Hash(bytes) != DatasetHash) throw new InvalidOperationException("ApprovedDatasetMismatch");
        using var data = JsonDocument.Parse(bytes);
        var metadata = data.RootElement.GetProperty("metadata");
        using var summary = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(datasetPath)!, "summary.json")));
        if (summary.RootElement.GetProperty("datasetSha256").GetString() != DatasetHash
            || new DirectoryInfo(Path.GetDirectoryName(datasetPath)!).Name != metadata.GetProperty("sourceFingerprint").GetString()
            || !metadata.GetProperty("localVerificationOnly").GetBoolean()
            || metadata.GetProperty("realCustomerLocationsUsed").GetInt32() != 0
            || metadata.GetProperty("realDriverLocationsUsed").GetInt32() != 0
            || metadata.GetProperty("historicalValuesAreCurrentPrices").GetBoolean()
            || metadata.GetProperty("visitMenuSummariesAllocated").GetBoolean())
            throw new InvalidOperationException("DatasetContractMismatch");
        var records = data.RootElement.GetProperty("records").EnumerateArray().Select(x => x.Clone()).ToArray();
        if (records.Length != 504 || records.Select(x => x.GetProperty("branchId").GetString()).Distinct().Count() != 339
            || records.Select(x => x.GetProperty("recordId").GetString()).Distinct().Count() != 504)
            throw new InvalidOperationException("DatasetScopeMismatch");
        foreach (var row in records)
            if (!Regex.IsMatch(row.GetProperty("recordId").GetString()!, "^replay-record-[0-9a-f]{64}$")
                || !Regex.IsMatch(row.GetProperty("branchId").GetString()!, "^replay-branch-[0-9a-f]{64}$"))
                throw new InvalidOperationException("SourceIdentityMismatch");
        using (var work = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(repo, Plan + "batch-timing.r52.work-order.json"))))
        {
            var w = work.RootElement;
            if (!w.GetProperty("acceptedInCurrentThread").GetBoolean() || !w.GetProperty("implementationAllowed").GetBoolean()
                || w.GetProperty("designSha256").GetString() != DesignSha256
                || w.GetProperty("sourceDatasetSha256").GetString() != DatasetHash
                || w.GetProperty("modelCallsAllowed").GetInt32() != 0 || w.GetProperty("weightTrainingAllowed").GetBoolean()
                || w.GetProperty("operationalChangesAllowed").GetBoolean())
                throw new InvalidOperationException("AcceptedWorkOrderRequired");
        }
        var result = new TimingSources { Records = records, DatasetSha256 = DatasetHash };
        result.Pin(Path.GetRelativePath(repo, datasetPath).Replace('\\', '/'), datasetPath, DatasetHash);
        var summaryPath = Path.Combine(Path.GetDirectoryName(datasetPath)!, "summary.json");
        result.Pin(Path.GetRelativePath(repo, summaryPath).Replace('\\', '/'), summaryPath);
        result.Pin(Plan + "batch-timing.implementation.r52.md", Path.Combine(repo, Plan + "batch-timing.implementation.r52.md"), DesignSha256);
        var fingerprints = metadata.GetProperty("sourceFingerprints").EnumerateArray().ToArray();
        if (fingerprints.Length != 29) throw new InvalidOperationException("HistoricalSourceCoverageMismatch");
        foreach (var source in fingerprints)
        {
            var name = source.GetProperty("relativePath").GetString()!;
            if (name.Contains('\\') || name.Contains(':') || name.Split('/').Any(x => x is ".." or "." or "")
                || !(name.StartsWith("artifacts/local/delivery-history-replay-r54/", StringComparison.Ordinal)
                    || name.StartsWith("edit-kit/branch-deliveries-all-20261010/", StringComparison.Ordinal)))
                throw new InvalidOperationException("UnexpectedHistoricalSource");
            var boundary = name.StartsWith("edit-kit/", StringComparison.Ordinal) ? delivery : repo;
            result.Pin(name, SafePath(Path.Combine(boundary, name), boundary), source.GetProperty("sha256").GetString());
        }
        const string ledger = "edit-kit/branch-deliveries-all-20261010/delivery-count-ledger.json";
        result.Pin(ledger, SafePath(Path.Combine(delivery, ledger), delivery), metadata.GetProperty("sourceLedgerSha256").GetString());
        const string exporter = "eng/local-dispatch-ai/RegionalScenarioRunner/prepare_regional_dataset.py";
        result.Pin(exporter, Path.Combine(repo, exporter), metadata.GetProperty("toolSha256").GetString());
        foreach (var name in new[] { "Ssalddel.Domain/배차/FoodDispatchTimingPolicy.cs", "Ssalddel.Contracts/Common/Dispatch/FoodDispatchTimingContracts.cs" })
            result.Pin(name, Path.Combine(repo, name));
        foreach (var pattern in new[] { "*.cs", "*.csproj" })
            foreach (var path in Directory.GetFiles(Path.Combine(repo, "eng/local-dispatch-ai/DispatchTimingScenarioRunner"), pattern))
                result.Pin(Path.GetRelativePath(repo, path).Replace('\\', '/'), path);
        result.Fingerprint = Hash(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { sourceHashes = result.Hashes, fixedSeed = 52, formatVersion = FormatVersion },
            new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping })));
        result.OutputDirectory = SafePath(Path.Combine(repo, "artifacts/local/local-dispatch-ai/dispatch-timing-r52/raw", result.Fingerprint), repo);
        var start = new ProcessStartInfo("git") { WorkingDirectory = repo, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in new[] { "check-ignore", "-q", "--", result.OutputDirectory }) start.ArgumentList.Add(arg);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("GitIgnoreCheckFailed");
        process.WaitForExit();
        if (process.ExitCode != 0) throw new InvalidOperationException("OutputMustBeGitIgnored");
        result.VerifyUnchanged();
        return result;
    }

    private void Pin(string key, string path, string? expected = null)
    {
        var hash = Hash(File.ReadAllBytes(path));
        if (expected is not null && hash != expected) throw new InvalidOperationException("PinnedSourceChanged");
        if (paths.TryGetValue(key, out var prior) && prior != path) throw new InvalidOperationException("SourceCollision");
        paths[key] = path; Hashes[key] = hash;
    }

    public void VerifyUnchanged()
    {
        foreach (var item in paths)
            if (Hash(File.ReadAllBytes(item.Value)) != Hashes[item.Key]) throw new InvalidOperationException("SourceChangedDuringPreparation");
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
        for (var part = new DirectoryInfo(Path.GetDirectoryName(full)!); part is not null; part = part.Parent)
            if (part.Exists && (part.Attributes & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException("LinkPathForbidden");
        if ((File.Exists(full) || Directory.Exists(full)) && (File.GetAttributes(full) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidOperationException("LinkPathForbidden");
        return full;
    }

    public static void WriteImmutable(string path, byte[] bytes)
    {
        SafePath(path, Path.GetDirectoryName(path)!);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        SafePath(path, Path.GetDirectoryName(path)!);
        if (File.Exists(path))
        {
            if (!File.ReadAllBytes(path).AsSpan().SequenceEqual(bytes)) throw new InvalidOperationException("ExistingOutputDifferent");
            return;
        }
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        stream.Write(bytes); stream.Flush(true);
    }
}
