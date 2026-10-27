using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

/// <summary>같은 격리 실행의 소스를 고정한다. 소스 내용·자격값을 복사하지 않고 경로와 해시만 보존한다.</summary>
internal sealed class CargoSourceBinding
{
    internal const string SchemaVersion = "cargo-operations-r21.source-binding.v1";
    private const string Project = "eng/CargoOperationsVerificationR21/CargoOperationsVerificationR21.csproj";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private static readonly HashSet<string> ExcludedDirectories = new(StringComparer.OrdinalIgnoreCase)
        { "bin", "obj", ".git", ".vs", ".private", "private", "artifacts", ".codex-build", "node_modules", "__pycache__" };
    private static readonly HashSet<string> SourceExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".cs", ".csproj", ".props", ".targets", ".razor", ".cshtml", ".js", ".css", ".json", ".py", ".ps1" };
    private readonly string root;
    internal SourceManifest Manifest { get; }

    private CargoSourceBinding(string root, SourceManifest manifest) { this.root = root; Manifest = manifest; }

    internal static CargoSourceBinding Bind(CargoJourneySettings settings)
    {
        var sources = Snapshot(settings.RepositoryRoot);
        var proposed = new SourceManifest(SchemaVersion, settings.RunStableId, CargoJourneySettings.DatabaseName, Fingerprint(sources), sources);
        var path = Path.Combine(settings.ArtifactRoot, "source-binding.json");
        try
        {
            using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            JsonSerializer.Serialize(file, proposed, Json); file.Flush(true);
        }
        catch (IOException) when (File.Exists(path)) { }
        var stored = JsonSerializer.Deserialize<SourceManifest>(File.ReadAllBytes(path), Json)
            ?? throw new InvalidOperationException("R21SourceBindingMissing");
        if (stored.SchemaVersion != SchemaVersion || stored.RunStableId != settings.RunStableId
            || stored.Database != CargoJourneySettings.DatabaseName || stored.ManifestSha256 != proposed.ManifestSha256
            || stored.Sources is null || !stored.Sources.SequenceEqual(sources))
            throw new InvalidOperationException("R21SourceChangedWithinRun");
        return new(settings.RepositoryRoot, stored);
    }

    internal void AssertCurrent()
    {
        var current = Snapshot(root);
        if (Fingerprint(current) != Manifest.ManifestSha256 || !Manifest.Sources.SequenceEqual(current))
            throw new InvalidOperationException("R21SourceChangedWithinRun");
    }

    private static SourceFile[] Snapshot(string root)
    {
        var files = new HashSet<string>(StringComparer.Ordinal);
        var projects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string Full(string path)
        {
            var full = Path.GetFullPath(path);
            var relative = Path.GetRelativePath(root, full).Replace('\\', '/');
            if (Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith("../", StringComparison.Ordinal))
                throw new InvalidOperationException("R21SourceOutsideRepository");
            return full;
        }
        void Add(string path)
        {
            var full = Full(path);
            if (!File.Exists(full)) throw new InvalidOperationException("R21SourceMissing");
            var relative = Path.GetRelativePath(root, full).Replace('\\', '/');
            if (relative.Split('/').Any(part => ExcludedDirectories.Contains(part) && !string.Equals(part, "artifacts", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("R21ExcludedSource");
            files.Add(relative);
        }
        void Visit(string project)
        {
            project = Full(project);
            if (!projects.Add(project)) return;
            Add(project);
            var directory = Path.GetDirectoryName(project)!;
            var pending = new Stack<string>(); pending.Push(directory);
            while (pending.TryPop(out var current))
            {
                foreach (var child in Directory.EnumerateDirectories(current))
                    if (!ExcludedDirectories.Contains(Path.GetFileName(child))
                        && (File.GetAttributes(child) & FileAttributes.ReparsePoint) == 0) pending.Push(child);
                foreach (var file in Directory.EnumerateFiles(current))
                {
                    var name = Path.GetFileName(file);
                    if (SourceExtensions.Contains(Path.GetExtension(file)) && !name.Contains(".Local.", StringComparison.OrdinalIgnoreCase)
                        && !name.EndsWith(".Local.json", StringComparison.OrdinalIgnoreCase)
                        && !(Path.GetExtension(file).Equals(".json", StringComparison.OrdinalIgnoreCase)
                            && name.Contains("secret", StringComparison.OrdinalIgnoreCase))) Add(file);
                }
            }
            var xml = XDocument.Load(project);
            foreach (var item in xml.Descendants().Where(item => item.Name.LocalName is "ProjectReference" or "Compile" or "EmbeddedResource"))
            {
                var include = item.Attribute("Include")?.Value;
                if (string.IsNullOrWhiteSpace(include)) continue;
                if (include.IndexOfAny(['$', '*', '?', ';']) >= 0) throw new InvalidOperationException("R21SourceIncludeUnsupported");
                var full = Full(Path.Combine(directory, include.Replace('\\', Path.DirectorySeparatorChar)));
                if (item.Name.LocalName == "ProjectReference") Visit(full); else Add(full);
            }
        }
        Visit(Path.Combine(root, Project.Replace('/', Path.DirectorySeparatorChar)));
        Add(Path.Combine(root, "eng", "verification", "cargo-ai-device-r114.py"));
        Add(Path.Combine(root, "eng", "local-dispatch-ai", "cargo-regional-training", "legal-dong-regions.json"));
        foreach (var name in new[] { "Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props", "NuGet.Config", "global.json" })
            if (File.Exists(Path.Combine(root, name))) Add(Path.Combine(root, name));
        return files.Order(StringComparer.Ordinal).Select(path => new SourceFile(path,
            Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(Path.Combine(root, path)))))).ToArray();
    }

    private static string Fingerprint(IReadOnlyList<SourceFile> files)
        => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Concat(files.Select(file => file.Path + "\t" + file.Sha256 + "\n")))));

    internal sealed record SourceFile(string Path, string Sha256);
    internal sealed record SourceManifest(string SchemaVersion, string RunStableId, string Database, string ManifestSha256, SourceFile[] Sources);
}

/// <summary>과거 추천 증거를 다시 읽을 때에도 변경된 소스로 현재 검증을 주장하지 않는다.</summary>
internal sealed class CargoSourceBindingStartupFilter(CargoSourceBinding binding) : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use(async (context, following) =>
        {
            if (context.Request.Path.StartsWithSegments("/verification/r114")
                || context.Request.Path.StartsWithSegments("/verification/r21"))
            {
                try { binding.AssertCurrent(); }
                catch (Exception error) when (error is InvalidOperationException or IOException or UnauthorizedAccessException)
                {
                    context.Response.StatusCode = 503;
                    context.Response.Headers.CacheControl = "private, no-store";
                    await context.Response.WriteAsJsonAsync(new { code = "R21SourceChangedOrUnavailable", verificationHeld = true }, context.RequestAborted);
                    return;
                }
            }
            await following(context);
        });
        next(app);
    };
}
