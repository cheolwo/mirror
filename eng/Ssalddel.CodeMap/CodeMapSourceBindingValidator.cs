using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Ssalddel.Contracts.Common.Metadata;

namespace Ssalddel.CodeMap;

public static class CodeMapSourceBindingValidator
{
    public static SsalddelCodeMetadataDiagnostic[] ValidateManifest(string repositoryRoot, WorkAreaManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        if (manifest.Key != "food-driver") return Array.Empty<SsalddelCodeMetadataDiagnostic>();
        var diagnostics = new List<SsalddelCodeMetadataDiagnostic>();
        void Error(string code, string featureKey, string message) => diagnostics.Add(new(
            SsalddelCodeMetadataDiagnosticSeverity.Error, code, featureKey, string.Empty, message));
        var roots = manifest.SourceRoots ?? Array.Empty<string>();
        if (roots.Length == 0 || roots.Distinct(StringComparer.OrdinalIgnoreCase).Count() != roots.Length)
            Error("CODEMAP023", string.Empty, "음식 기사 sourceRoots에는 중복 없는 명시 소스 범위가 필요합니다.");
        foreach (var root in roots)
        {
            if (!IsRepositoryRelative(root)
                || (manifest.ExcludedRoots ?? Array.Empty<string>()).Any(excluded => root.Split('/').Contains(excluded, StringComparer.OrdinalIgnoreCase) || Within(root, excluded)))
            {
                Error("CODEMAP023", string.Empty, $"허용 소스 범위는 제외 범위 밖의 저장소 상대 경로여야 합니다: {root}");
                continue;
            }
            var fullPath = Path.Combine(repositoryRoot, root);
            try
            {
                if ((!Directory.Exists(fullPath) && !(File.Exists(fullPath) && IsSourceExtension(root)))
                    || ContainsReparsePoint(repositoryRoot, root))
                    Error("CODEMAP023", string.Empty, $"소스 범위는 존재하는 디렉터리·소스 파일이며 reparse 경로가 아니어야 합니다: {root}");
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                Error("CODEMAP023", string.Empty, $"소스 범위를 확인할 수 없습니다: {root}");
            }
        }
        var features = manifest.Features ?? Array.Empty<WorkAreaFeature>();
        if (features.Length == 0)
            Error("CODEMAP024", string.Empty, "음식 기사 features와 각 기능의 필수 단계가 필요합니다.");
        var featureKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var feature in features)
        {
            if (feature is null)
            {
                Error("CODEMAP024", string.Empty, "기능 항목은 null일 수 없습니다.");
                continue;
            }
            if (string.IsNullOrWhiteSpace(feature.Key) || !featureKeys.Add(feature.Key))
                Error("CODEMAP022", feature.Key, "manifest 기능 키가 비어 있거나 중복되었습니다.");
            var required = feature.RequiredStepKeys ?? Array.Empty<string>();
            if (required.Length == 0 || required.Any(string.IsNullOrWhiteSpace)
                || required.Distinct(StringComparer.Ordinal).Count() != required.Length)
                Error("CODEMAP024", feature.Key, "각 기능의 requiredStepKeys에는 중복 없는 비어 있지 않은 필수 단계가 필요합니다.");
        }
        return diagnostics.ToArray();
    }

    // Limits explicit typed source selection to the reflected component's project and namespace.
    public static bool MatchesReflectedSource(string sourcePath, string content, string assemblyName, string componentNamespace, string typeName)
    {
        if (!sourcePath.StartsWith(assemblyName + "/", StringComparison.OrdinalIgnoreCase)) return false;
        if (sourcePath.EndsWith(".razor", StringComparison.OrdinalIgnoreCase))
        {
            if (Path.GetFileNameWithoutExtension(sourcePath) != typeName) return false;
            var declared = Regex.Match(content, @"(?m)^\s*@namespace\s+(?<name>[\p{L}\p{N}_.]+)\s*$", RegexOptions.CultureInvariant);
            var effectiveNamespace = declared.Success ? declared.Groups["name"].Value
                : Path.GetDirectoryName(sourcePath)?.Replace('\\', '/').Replace('/', '.');
            return effectiveNamespace == componentNamespace;
        }
        if (!Regex.IsMatch(content, @"\b(class|interface|struct|record)\s+" + Regex.Escape(typeName) + @"\b", RegexOptions.CultureInvariant)) return false;
        return string.IsNullOrEmpty(componentNamespace)
            ? !Regex.IsMatch(content, @"\bnamespace\s+", RegexOptions.CultureInvariant)
            : Regex.IsMatch(content, @"\bnamespace\s+" + Regex.Escape(componentNamespace) + @"\s*[;{]", RegexOptions.CultureInvariant);
    }

    public static CodeMapSourceBindingValidationResult Validate(
        string repositoryRoot,
        WorkAreaManifest manifest,
        IReadOnlyCollection<CodeMapStepIdentity> knownSteps)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(knownSteps);
        var root = Path.GetFullPath(repositoryRoot);
        var diagnostics = new List<SsalddelCodeMetadataDiagnostic>();
        var bindings = new List<ResolvedCodeMapSourceBinding>();
        var identities = knownSteps.Select(step => (step.FeatureKey, step.StepKey)).ToHashSet();
        foreach (var feature in manifest.Features ?? Array.Empty<WorkAreaFeature>())
        {
            if (feature is null)
            {
                diagnostics.Add(new(SsalddelCodeMetadataDiagnosticSeverity.Error, "CODEMAP050", string.Empty,
                    string.Empty, "기능 항목은 null일 수 없습니다."));
                continue;
            }
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var binding in feature.SourceBindings ?? Array.Empty<CodeMapSourceBinding>())
            {
                if (binding is null)
                {
                    diagnostics.Add(new(SsalddelCodeMetadataDiagnosticSeverity.Error, "CODEMAP050", feature.Key,
                        string.Empty, "소스 결속 항목은 null일 수 없습니다."));
                    continue;
                }
                void Error(string code, string message) => diagnostics.Add(new(
                    SsalddelCodeMetadataDiagnosticSeverity.Error, code, feature.Key, binding.Key, message));

                var relatedStepKeys = binding.RelatedStepKeys ?? Array.Empty<string>();
                var references = binding.Sources ?? Array.Empty<CodeMapSourceReference>();
                var before = diagnostics.Count;
                if (string.IsNullOrWhiteSpace(binding.Key) || !keys.Add(binding.Key)
                    || identities.Contains((feature.Key, binding.Key)))
                    Error("CODEMAP050", "소스 결속 키는 비어 있지 않고 같은 기능의 단계·결속과 중복되지 않아야 합니다.");
                if (string.IsNullOrWhiteSpace(binding.Responsibility) || string.IsNullOrWhiteSpace(binding.Boundary))
                    Error("CODEMAP051", "소스 결속에는 책임과 경계가 필요합니다.");
                if (relatedStepKeys.Length == 0
                    || relatedStepKeys.Distinct(StringComparer.Ordinal).Count() != relatedStepKeys.Length
                    || relatedStepKeys.Any(key => !identities.Contains((feature.Key, key))))
                    Error("CODEMAP052", "관련 단계는 같은 기능에 존재하는 중복 없는 reflection 단계여야 합니다.");
                if (references.Length == 0)
                    Error("CODEMAP053", "소스 결속에는 명시적인 파일과 심볼이 필요합니다.");

                var sources = new List<ResolvedCodeMapSourceReference>();
                var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var sourceKeys = new HashSet<(string Path, string Symbol)>();
                foreach (var source in references)
                {
                    if (source is null)
                    {
                        Error("CODEMAP053", "소스 항목은 null일 수 없습니다.");
                        continue;
                    }
                    if (!IsAllowedPath(source.Path, manifest))
                    {
                        Error("CODEMAP054", $"소스는 허용 범위 안의 저장소 상대 .cs/.razor/.xaml 경로여야 합니다: {source.Path}");
                        continue;
                    }
                    if (string.IsNullOrWhiteSpace(source.Symbol) || source.Symbol.Contains('\n') || source.Symbol.Contains('\r')
                        || !sourceKeys.Add((source.Path.ToUpperInvariant(), source.Symbol)))
                    {
                        Error("CODEMAP053", $"소스 심볼은 한 줄의 비어 있지 않은 중복 없는 anchor여야 합니다: {source.Path}");
                        continue;
                    }
                    var fullPath = Path.Combine(root, source.Path);
                    if (!File.Exists(fullPath))
                    {
                        Error("CODEMAP054", $"소스 파일이 없습니다: {source.Path}");
                        continue;
                    }
                    string content;
                    try
                    {
                        if (ContainsReparsePoint(root, source.Path))
                        {
                            Error("CODEMAP054", $"소스 파일이나 부모 경로가 reparse 경로입니다: {source.Path}");
                            continue;
                        }
                        content = File.ReadAllText(fullPath, new UTF8Encoding(false, true));
                    }
                    catch (Exception error) when (error is IOException or UnauthorizedAccessException or DecoderFallbackException)
                    {
                        Error("CODEMAP054", $"UTF-8 소스 파일을 읽을 수 없습니다: {source.Path}");
                        continue;
                    }
                    files[source.Path] = content;
                    var position = content.IndexOf(source.Symbol, StringComparison.Ordinal);
                    if (position < 0)
                    {
                        Error("CODEMAP055", $"소스 심볼을 찾을 수 없습니다: {source.Path} · {source.Symbol}");
                        continue;
                    }
                    sources.Add(new()
                    {
                        Path = source.Path,
                        Symbol = source.Symbol,
                        Line = 1 + content.AsSpan(0, position).Count('\n'),
                    });
                }

                foreach (var source in references.Where(source => source?.Path?.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase) == true))
                {
                    if (!files.TryGetValue(source.Path, out var xaml)) continue;
                    if (!files.TryGetValue(source.Path + ".cs", out var code))
                    {
                        Error("CODEMAP056", $"XAML과 같은 결속에 코드부를 명시해야 합니다: {source.Path}.cs");
                        continue;
                    }
                    try
                    {
                        using var reader = XmlReader.Create(new StringReader(xaml), new XmlReaderSettings
                        {
                            DtdProcessing = DtdProcessing.Prohibit,
                            XmlResolver = null,
                        });
                        var document = XDocument.Load(reader);
                        var className = document.Root?.Attribute(XName.Get("Class", "http://schemas.microsoft.com/winfx/2009/xaml"))?.Value;
                        var separator = className?.LastIndexOf('.') ?? -1;
                        if (separator <= 0
                            || !Regex.IsMatch(code, @"\bnamespace\s+" + Regex.Escape(className![..separator]) + @"\s*[;{]", RegexOptions.CultureInvariant)
                            || !Regex.IsMatch(code, @"\bpartial\s+class\s+" + Regex.Escape(className![(separator + 1)..]) + @"\b", RegexOptions.CultureInvariant))
                            Error("CODEMAP056", $"XAML x:Class와 코드부의 namespace/partial class가 일치하지 않습니다: {source.Path}");
                    }
                    catch (XmlException)
                    {
                        Error("CODEMAP056", $"XAML을 읽을 수 없거나 DTD가 포함되어 있습니다: {source.Path}");
                    }
                }

                if (diagnostics.Count == before)
                    bindings.Add(new()
                    {
                        FeatureKey = feature.Key,
                        Key = binding.Key,
                        RelatedStepKeys = relatedStepKeys.ToArray(),
                        Responsibility = binding.Responsibility,
                        Boundary = binding.Boundary,
                        Sources = sources.ToArray(),
                    });
            }
        }
        return new() { Bindings = bindings.ToArray(), Diagnostics = diagnostics.ToArray() };
    }

    private static bool IsAllowedPath(string path, WorkAreaManifest manifest)
    {
        if (!IsRepositoryRelative(path)) return false;
        var segments = path.Split('/');
        if (!IsSourceExtension(path)) return false;
        if ((manifest.ExcludedRoots ?? Array.Empty<string>()).Any(excluded => segments.Contains(excluded, StringComparer.OrdinalIgnoreCase)
            || Within(path, excluded))) return false;
        return (manifest.SourceRoots ?? Array.Empty<string>()).Any(allowed => Within(path, allowed));
    }

    internal static bool ContainsReparsePoint(string repositoryRoot, string relativePath)
    {
        var current = Path.GetFullPath(repositoryRoot);
        foreach (var segment in relativePath.Split('/'))
        {
            current = Path.Combine(current, segment);
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) return true;
        }
        return false;
    }

    private static bool IsRepositoryRelative(string path)
        => !string.IsNullOrWhiteSpace(path) && !Path.IsPathRooted(path) && !path.Contains('\\') && !path.Contains(':')
            && !path.Split('/').Any(segment => segment is "" or "." or "..");

    private static bool IsSourceExtension(string path)
        => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".razor", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase);

    private static bool Within(string path, string root)
        => !string.IsNullOrWhiteSpace(root)
            && (path.Equals(root.TrimEnd('/'), StringComparison.OrdinalIgnoreCase)
                || path.StartsWith(root.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase));
}
