using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Ssalddel.CodeMap;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Interior.Contracts;
using Ssalddel.Interior.Domain;
using Ssalddel.Simulation.Application;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;
using Ssalddel.Simulation.Infrastructure;
using Ssalddel.Simulation.Persistence;
using Ssalddel.Simulation.Hosting.Controllers;
using Ssalddel.Unity.Application;

var repositoryRoot = FindRepositoryRoot();
var workArea = ReadOption(args, "--work-area") ?? "simulation-unity";
if (workArea is not ("simulation-unity" or "food-driver")
    || (args.Contains("--work-area", StringComparer.Ordinal) && ReadOption(args, "--work-area") is null))
{
    Console.Error.WriteLine("--work-area는 simulation-unity 또는 food-driver여야 합니다.");
    return 3;
}
var manifestPath = Path.Combine(repositoryRoot, "eng", "work-areas", workArea + ".json");
var manifest = JsonSerializer.Deserialize<WorkAreaManifest>(
    File.ReadAllText(manifestPath, Encoding.UTF8),
    JsonOptions()) ?? throw new InvalidOperationException($"{workArea} manifest를 읽을 수 없습니다.");
if (manifest.Key != workArea)
{
    Console.Error.WriteLine("manifest key와 --work-area가 일치하지 않습니다.");
    return 3;
}
var manifestErrors = CodeMapSourceBindingValidator.ValidateManifest(repositoryRoot, manifest);
if (manifestErrors.Length > 0)
{
    foreach (var diagnostic in manifestErrors)
        Console.Error.WriteLine($"{diagnostic.Severity} {diagnostic.Code} {diagnostic.FeatureKey} {diagnostic.StepKey}: {diagnostic.Message}");
    return 1;
}

var assemblies = workArea == "food-driver" ? new[]
{
    Assembly.Load("Ssalddel"),
    Assembly.Load("Ssalddel.Contracts"),
    Assembly.Load("Ssalddel.Domain"),
    Assembly.Load("Ssalddel.Infrastructure"),
    Assembly.Load("Ssalddel.Ui.Common"),
} : new[]
{
    typeof(I실내공간조립Engine).Assembly,
    typeof(DeterministicInteriorLayoutEngine).Assembly,
    typeof(경영SimulationSession생성Request).Assembly,
    typeof(경영SimulationSessionAggregate).Assembly,
    typeof(경영SimulationSession생명주기Service).Assembly,
    typeof(InMemory경영SimulationSessionStore).Assembly,
    typeof(SimulationSessionSaveStore).Assembly,
    typeof(경영SimulationSessionsController).Assembly,
    typeof(LastSuccessfulLoadRuntime<,>).Assembly,
}.Distinct().ToArray();

var featureKeys = manifest.Features.Select(feature => feature.Key)
    .ToHashSet(StringComparer.Ordinal);
var descriptors = SsalddelCodeMetadataReader.Read(assemblies)
    .Where(descriptor => featureKeys.Contains(descriptor.FeatureKey))
    .ToArray();
var graph = SsalddelCodeMetadataGraphBuilder.Build(descriptors);
var diagnostics = SsalddelCodeMetadataValidator.Validate(graph, requireNavigationFields: true)
    .ToList();

AddManifestDiagnostics(manifest, graph, diagnostics);
AddAuthorityDiagnostics(graph, diagnostics);
var bindingResult = CodeMapSourceBindingValidator.Validate(repositoryRoot, manifest,
    graph.Steps.Select(step => new CodeMapStepIdentity(step.FeatureKey, step.StepKey)).ToArray());
diagnostics.AddRange(bindingResult.Diagnostics);
var sourceIndex = BuildSourceIndex(repositoryRoot, manifest, graph, bindingResult.Bindings, diagnostics);
AddCoverageWarnings(repositoryRoot, manifest, graph, diagnostics);

var document = BuildDocument(repositoryRoot, manifest, graph, sourceIndex, bindingResult.Bindings, diagnostics);
var json = JsonSerializer.Serialize(document, JsonOptions()) + Environment.NewLine;
var markdown = BuildMarkdown(document);
var jsonPath = Path.Combine(repositoryRoot, "docs", "AI", "generated", workArea + "-code-map.json");
var markdownPath = Path.Combine(repositoryRoot, "docs", "AI", "generated", workArea + "-code-map.md");
var hasErrors = diagnostics.Any(item =>
    item.Severity == SsalddelCodeMetadataDiagnosticSeverity.Error);

var featureArgument = ReadOption(args, "--feature");
if (!string.IsNullOrWhiteSpace(featureArgument))
{
    var feature = document.Features.FirstOrDefault(item => item.Key == featureArgument);
    if (feature is null)
    {
        Console.Error.WriteLine($"기능 키를 찾을 수 없습니다: {featureArgument}");
        return 3;
    }

    Console.Write(BuildFeatureTree(feature));
    return hasErrors ? 1 : 0;
}

if (hasErrors)
{
    foreach (var diagnostic in diagnostics)
    {
        Console.Error.WriteLine($"{diagnostic.Severity} {diagnostic.Code} {diagnostic.FeatureKey} {diagnostic.StepKey}: {diagnostic.Message}");
    }
    return 1;
}

if (args.Contains("--write", StringComparer.Ordinal))
{
    Directory.CreateDirectory(Path.GetDirectoryName(jsonPath)!);
    File.WriteAllText(jsonPath, json, new UTF8Encoding(false));
    File.WriteAllText(markdownPath, markdown, new UTF8Encoding(false));
    Console.WriteLine($"{document.WorkAreaLabel} 코드 지도를 갱신했습니다.");
}
else
{
    var stale = !File.Exists(jsonPath)
        || !File.Exists(markdownPath)
        || File.ReadAllText(jsonPath, Encoding.UTF8) != json
        || File.ReadAllText(markdownPath, Encoding.UTF8) != markdown;
    if (stale)
    {
        Console.Error.WriteLine("코드 지도가 현재 메타데이터와 다릅니다. --write로 갱신하세요.");
        return 2;
    }

    Console.WriteLine($"{document.WorkAreaLabel} 코드 지도가 현재 메타데이터와 일치합니다.");
}

foreach (var diagnostic in diagnostics)
{
    Console.WriteLine($"{diagnostic.Severity} {diagnostic.Code} {diagnostic.FeatureKey} {diagnostic.StepKey}: {diagnostic.Message}");
}

return 0;

static string FindRepositoryRoot()
{
    for (var current = new DirectoryInfo(Directory.GetCurrentDirectory()); current != null; current = current.Parent)
    {
        if (File.Exists(Path.Combine(current.FullName, "AGENTS.md"))
            && Directory.Exists(Path.Combine(current.FullName, "eng", "work-areas")))
        {
            return current.FullName;
        }
    }

    throw new InvalidOperationException("저장소 루트를 찾을 수 없습니다.");
}

static JsonSerializerOptions JsonOptions()
    => new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

static string? ReadOption(IReadOnlyList<string> arguments, string name)
{
    for (var index = 0; index < arguments.Count - 1; index++)
    {
        if (arguments[index] == name) return arguments[index + 1];
    }

    return null;
}

static void AddManifestDiagnostics(
    WorkAreaManifest manifest,
    SsalddelCodeMetadataGraph graph,
    ICollection<SsalddelCodeMetadataDiagnostic> diagnostics)
{
    var knownFeatures = manifest.Features.Select(feature => feature.Key)
        .ToHashSet(StringComparer.Ordinal);
    foreach (var duplicate in manifest.Features.GroupBy(feature => feature.Key, StringComparer.Ordinal)
        .Where(group => string.IsNullOrWhiteSpace(group.Key) || group.Count() > 1))
        diagnostics.Add(Error("CODEMAP022", duplicate.Key, string.Empty, "manifest 기능 키가 비어 있거나 중복되었습니다."));
    foreach (var feature in manifest.Features)
    {
        var actualSteps = graph.Steps.Where(step => step.FeatureKey == feature.Key)
            .Select(step => step.StepKey).ToHashSet(StringComparer.Ordinal);
        foreach (var required in feature.RequiredStepKeys.Where(required => !actualSteps.Contains(required)))
        {
            diagnostics.Add(Error("CODEMAP020", feature.Key, required, "manifest의 필수 단계가 소스 메타데이터에 없습니다."));
        }

        foreach (var dependency in feature.DependsOnFeatures.Where(dependency => !knownFeatures.Contains(dependency)))
        {
            diagnostics.Add(Error("CODEMAP021", feature.Key, string.Empty, $"기능 의존성 '{dependency}'를 찾을 수 없습니다."));
        }
    }
}

static void AddAuthorityDiagnostics(
    SsalddelCodeMetadataGraph graph,
    ICollection<SsalddelCodeMetadataDiagnostic> diagnostics)
{
    foreach (var step in graph.Steps)
    {
        var assemblyName = step.ComponentType.Assembly.GetName().Name ?? string.Empty;
        if ((step.WritesTo & SsalddelCodeDataScope.SharedPublicData) != 0)
        {
            diagnostics.Add(Error("CODEMAP030", step.FeatureKey, step.StepKey, "공유 공공데이터는 읽기 전용입니다."));
        }

        if ((assemblyName.StartsWith("Ssalddel.Simulation", StringComparison.Ordinal)
                || assemblyName.StartsWith("Ssalddel.Unity", StringComparison.Ordinal))
            && (step.WritesTo & SsalddelCodeDataScope.OperationalState) != 0)
        {
            diagnostics.Add(Error("CODEMAP031", step.FeatureKey, step.StepKey, "Simulation·Unity는 운영 상태를 쓸 수 없습니다."));
        }

        if (assemblyName == "Ssalddel.Unity"
            && (step.WritesTo & ~SsalddelCodeDataScope.ClientPresentation) != 0)
        {
            diagnostics.Add(Error("CODEMAP032", step.FeatureKey, step.StepKey, "Unity는 ClientPresentation 이외의 상태를 쓸 수 없습니다."));
        }

        if (step.Layer == SsalddelCodeLayer.Contract
            && (step.Effects != SsalddelCodeEffect.None || step.WritesTo != SsalddelCodeDataScope.None))
        {
            diagnostics.Add(Error("CODEMAP033", step.FeatureKey, step.StepKey, "Contract 단계는 부수효과나 상태 쓰기를 가질 수 없습니다."));
        }
    }
}

static Dictionary<(string FeatureKey, string StepKey), string?> BuildSourceIndex(
    string repositoryRoot,
    WorkAreaManifest manifest,
    SsalddelCodeMetadataGraph graph,
    IReadOnlyCollection<ResolvedCodeMapSourceBinding> sourceBindings,
    ICollection<SsalddelCodeMetadataDiagnostic> diagnostics)
{
    var files = EnumerateSourceFiles(repositoryRoot, manifest).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    var contents = files.ToDictionary(
        path => path,
        path => File.ReadAllText(path, Encoding.UTF8),
        StringComparer.OrdinalIgnoreCase);
    var featureFieldNames = typeof(SsalddelCodeFeatureKeys)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field.IsLiteral && field.FieldType == typeof(string))
        .ToDictionary(
            field => (string)field.GetRawConstantValue()!,
            field => field.Name,
            StringComparer.Ordinal);
    var result = new Dictionary<(string FeatureKey, string StepKey), string?>();

    foreach (var step in graph.Steps)
    {
        var typeName = step.ComponentType.Name.Split('`')[0];
        var declaration = new Regex(@"\b(class|interface|struct|record)\s+" + Regex.Escape(typeName) + @"\b", RegexOptions.CultureInvariant);
        bool MatchesSource(string path, string content) => manifest.Key == "simulation-unity"
            ? declaration.IsMatch(content)
            : CodeMapSourceBindingValidator.MatchesReflectedSource(
                Path.GetRelativePath(repositoryRoot, path).Replace('\\', '/'), content,
                step.ComponentType.Assembly.GetName().Name ?? string.Empty, step.ComponentType.Namespace ?? string.Empty, typeName);
        var candidates = contents.Where(pair => MatchesSource(pair.Key, pair.Value)).Select(pair => pair.Key).ToArray();
        if (featureFieldNames.TryGetValue(step.FeatureKey, out var fieldName))
        {
            var preferred = candidates.Where(path => contents[path].Contains(
                "SsalddelCodeFeatureKeys." + fieldName,
                StringComparison.Ordinal)).ToArray();
            if (preferred.Length == 1) candidates = preferred;
        }

        // 같은 partial 타입의 여러 기능/단계는 선언된 StepKey로 구분한다.
        if (candidates.Length > 1 && !string.IsNullOrWhiteSpace(step.StepKey))
        {
            var exactSteps = candidates.Where(path => Regex.IsMatch(contents[path],
                @"\bStepKey\s*=\s*""" + Regex.Escape(step.StepKey) + @"""",
                RegexOptions.CultureInvariant)).ToArray();
            if (exactSteps.Length == 1) candidates = exactSteps;
        }

        // Explicit bindings disambiguate only this reflected type; a native host never replaces an API component.
        var explicitSources = sourceBindings.Where(binding => binding.FeatureKey == step.FeatureKey
                && binding.RelatedStepKeys.Contains(step.StepKey, StringComparer.Ordinal))
            .SelectMany(binding => binding.Sources).Select(source => Path.Combine(repositoryRoot, source.Path))
            .Where(path => contents.TryGetValue(path, out var content)
                && MatchesSource(path, content))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (explicitSources.Length == 1) candidates = explicitSources;
        else if (explicitSources.Length > 1)
            diagnostics.Add(Error("CODEMAP057", step.FeatureKey, step.StepKey, "같은 reflection 단계의 명시 소스가 여러 파일로 충돌합니다."));

        foreach (var reference in step.SourceCodeRefs.Concat(step.SharedRuleRefs))
        {
            var segments = reference.Split('/');
            if (Path.IsPathRooted(reference) || reference.Contains('\\') || reference.Contains(':')
                || segments.Any(segment => segment is "" or "." or "..")
                || !reference.EndsWith(".cs", StringComparison.Ordinal)
                || !File.Exists(Path.Combine(repositoryRoot, reference)))
                diagnostics.Add(Error("CODEMAP042", step.FeatureKey, step.StepKey,
                    $"출처는 존재하는 저장소 상대 C# 파일이어야 합니다: {reference}"));
        }

        string? relativePath = candidates.Length == 1
            ? Path.GetRelativePath(repositoryRoot, candidates[0]).Replace('\\', '/')
            : null;
        result[(step.FeatureKey, step.StepKey)] = relativePath;
        if (relativePath is null)
        {
            if (manifest.Key == "food-driver" && manifest.Features.Any(feature => feature.Key == step.FeatureKey
                && feature.RequiredStepKeys.Contains(step.StepKey, StringComparer.Ordinal)))
                diagnostics.Add(Error("CODEMAP058", step.FeatureKey, step.StepKey, "필수 단계의 소스 파일을 하나로 결정하지 못했습니다."));
            else diagnostics.Add(Warning("CODEMAP101", step.FeatureKey, step.StepKey, "소스 파일을 하나로 결정하지 못했습니다."));
        }
    }

    return result;
}

static void AddCoverageWarnings(
    string repositoryRoot,
    WorkAreaManifest manifest,
    SsalddelCodeMetadataGraph graph,
    ICollection<SsalddelCodeMetadataDiagnostic> diagnostics)
{
    var annotated = graph.Steps.Select(step => step.ComponentType.Name.Split('`')[0])
        .ToHashSet(StringComparer.Ordinal);
    var eligible = new Regex(
        @"public\s+(?:sealed\s+|static\s+|partial\s+|abstract\s+)*(?:class|interface|record)\s+(?<name>[\p{L}\p{N}_]+(?:Controller|Service|Store|Coordinator|JobShell|Mapper|Runtime))\b",
        RegexOptions.CultureInvariant);
    var allFiles = EnumerateSourceFiles(repositoryRoot, manifest).ToArray();
    foreach (var sourceRoot in manifest.SourceRoots)
    {
        var sourceRootPath = Path.GetFullPath(Path.Combine(repositoryRoot, sourceRoot));
        var fullRoot = sourceRootPath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var missing = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var file in allFiles.Where(path => Path.GetFullPath(path).StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase)
            || (manifest.Key == "food-driver" && Path.GetFullPath(path).Equals(sourceRootPath, StringComparison.OrdinalIgnoreCase))))
        {
            var text = File.ReadAllText(file, Encoding.UTF8);
            foreach (Match match in eligible.Matches(text))
            {
                var typeName = match.Groups["name"].Value;
                if (!annotated.Contains(typeName)) missing.Add(typeName);
            }
        }

        if (missing.Count > 0)
        {
            var examples = string.Join(", ", missing.Take(8));
            diagnostics.Add(Warning(
                "CODEMAP100",
                manifest.Key,
                sourceRoot,
                $"기준 기능 밖의 탐색 후보 {missing.Count}개: {examples}{(missing.Count > 8 ? ", ..." : string.Empty)}"));
        }
    }
}

static IEnumerable<string> EnumerateSourceFiles(string repositoryRoot, WorkAreaManifest manifest)
{
    foreach (var root in manifest.SourceRoots)
    {
        var fullRoot = Path.Combine(repositoryRoot, root);
        if (manifest.Key == "food-driver" && File.Exists(fullRoot))
        {
            yield return fullRoot;
            continue;
        }
        if (!Directory.Exists(fullRoot)) continue;
        var paths = manifest.Key == "simulation-unity"
            ? Directory.EnumerateFiles(fullRoot, "*.cs", SearchOption.AllDirectories)
            : Directory.EnumerateFiles(fullRoot, "*", new EnumerationOptions
            {
                RecurseSubdirectories = true,
                AttributesToSkip = FileAttributes.ReparsePoint,
                IgnoreInaccessible = false,
            });
        foreach (var path in paths
            .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                || (manifest.Key != "simulation-unity" && path.EndsWith(".razor", StringComparison.OrdinalIgnoreCase))))
        {
            var segments = Path.GetRelativePath(fullRoot, path)
                .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var relative = Path.GetRelativePath(repositoryRoot, path).Replace('\\', '/');
            if (!segments.Any(segment => (manifest.ExcludedRoots ?? Array.Empty<string>()).Contains(segment, StringComparer.OrdinalIgnoreCase))
                && (manifest.Key == "simulation-unity" || !(manifest.ExcludedRoots ?? Array.Empty<string>()).Any(excluded =>
                    relative.Equals(excluded, StringComparison.OrdinalIgnoreCase)
                    || relative.StartsWith(excluded.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase))))
            {
                yield return path;
            }
        }
    }
}

static CodeMapDocument BuildDocument(
    string repositoryRoot,
    WorkAreaManifest manifest,
    SsalddelCodeMetadataGraph graph,
    IReadOnlyDictionary<(string FeatureKey, string StepKey), string?> sourceIndex,
    IReadOnlyCollection<ResolvedCodeMapSourceBinding> sourceBindings,
    IEnumerable<SsalddelCodeMetadataDiagnostic> diagnostics)
{
    var features = manifest.Features.Select(feature => new CodeMapFeature
    {
        Key = feature.Key,
        Label = feature.Label,
        DependsOnFeatures = feature.DependsOnFeatures,
        SourceBindings = feature.SourceBindings is not { Length: > 0 } ? null
            : sourceBindings.Where(binding => binding.FeatureKey == feature.Key).ToArray(),
        Steps = graph.Steps.Where(step => step.FeatureKey == feature.Key)
            .Select(step => new CodeMapStep
            {
                FlowOrder = step.FlowOrder,
                StepKey = step.StepKey,
                DependsOnStepKeys = step.DependsOnStepKeys.ToArray(),
                ExecutionStage = step.ExecutionStage.ToString(),
                Layer = step.Layer.ToString(),
                ComponentType = step.ComponentType.FullName ?? step.ComponentType.Name,
                ContractType = manifest.Key == "simulation-unity" ? null : step.ContractType?.FullName,
                Assembly = step.ComponentType.Assembly.GetName().Name ?? string.Empty,
                SourcePath = sourceIndex[(step.FeatureKey, step.StepKey)],
                SourceLine = manifest.Key == "simulation-unity" ? null
                    : FindSourceLine(repositoryRoot, sourceIndex[(step.FeatureKey, step.StepKey)], step, sourceBindings),
                Responsibility = step.Responsibility,
                Effects = FlagNames(step.Effects),
                ReadsFrom = FlagNames(step.ReadsFrom),
                WritesTo = FlagNames(step.WritesTo),
                Boundary = step.Boundary,
                SourceCodeRefs = step.SourceCodeRefs.Count == 0 ? null : step.SourceCodeRefs.ToArray(),
                ReuseKind = step.ReuseKind.Length == 0 ? null : step.ReuseKind,
                SharedRuleRefs = step.SharedRuleRefs.Count == 0 ? null : step.SharedRuleRefs.ToArray(),
                Adaptation = step.Adaptation.Length == 0 ? null : step.Adaptation,
            }).ToArray(),
    }).ToArray();

    return new CodeMapDocument
    {
        SchemaVersion = "ssalddel-code-map.v1",
        WorkAreaKey = manifest.Key,
        WorkAreaVersion = manifest.Version,
        WorkAreaLabel = manifest.Key == "simulation-unity" ? "Simulation·Unity"
            : string.IsNullOrWhiteSpace(manifest.Label) ? "음식 기사" : manifest.Label,
        Features = features,
        Diagnostics = diagnostics
            .OrderBy(item => item.Severity)
            .ThenBy(item => item.Code, StringComparer.Ordinal)
            .ThenBy(item => item.FeatureKey, StringComparer.Ordinal)
            .ThenBy(item => item.StepKey, StringComparer.Ordinal)
            .Select(item => new CodeMapDiagnostic
            {
                Severity = item.Severity.ToString(),
                Code = item.Code,
                FeatureKey = item.FeatureKey,
                StepKey = item.StepKey,
                Message = item.Message,
            }).ToArray(),
    };
}

static string[] FlagNames<T>(T value) where T : struct, Enum
{
    var text = value.ToString();
    return text == "None" ? Array.Empty<string>() : text.Split(", ", StringSplitOptions.RemoveEmptyEntries);
}

static int? FindSourceLine(
    string repositoryRoot,
    string? sourcePath,
    SsalddelCodeMetadataDescriptor step,
    IReadOnlyCollection<ResolvedCodeMapSourceBinding> sourceBindings)
{
    if (sourcePath is null) return null;
    var explicitSource = sourceBindings.Where(binding => binding.FeatureKey == step.FeatureKey
            && binding.RelatedStepKeys.Contains(step.StepKey, StringComparer.Ordinal))
        .SelectMany(binding => binding.Sources).FirstOrDefault(source => source.Path == sourcePath);
    if (explicitSource is not null) return explicitSource.Line;
    var content = File.ReadAllText(Path.Combine(repositoryRoot, sourcePath), Encoding.UTF8);
    var match = Regex.Match(content, @"\bStepKey\s*=\s*""" + Regex.Escape(step.StepKey) + @"""", RegexOptions.CultureInvariant);
    if (!match.Success)
        match = Regex.Match(content, @"\b(class|interface|struct|record)\s+"
            + Regex.Escape(step.ComponentType.Name.Split('`')[0]) + @"\b", RegexOptions.CultureInvariant);
    return match.Success ? 1 + content.AsSpan(0, match.Index).Count('\n') : 1;
}

static string BuildMarkdown(CodeMapDocument document)
{
    var builder = new StringBuilder();
    var extended = document.WorkAreaKey != "simulation-unity";
    builder.Append("# ").Append(document.WorkAreaLabel).AppendLine(" 코드 탐색 트리");
    builder.AppendLine();
    builder.Append("> 이 문서는 `SsalddelCodeMetadataAttribute`와 `eng/work-areas/")
        .Append(document.WorkAreaKey).AppendLine(".json`에서 자동 생성된다. 직접 수정하지 않는다.");
    if (extended)
    {
        builder.AppendLine();
        builder.AppendLine("소스 결속은 검토한 파일·심볼의 탐색 연결이다. reflection 단계와 분리하며 실제 호출·실행 성공이나 운영 검증을 인증하지 않는다. MAUI 앱 assembly를 로드하지 않는다.");
    }
    builder.AppendLine();
    builder.AppendLine("```text");
    builder.AppendLine(document.WorkAreaLabel);
    for (var index = 0; index < document.Features.Length; index++)
    {
        var feature = document.Features[index];
        var lastFeature = index == document.Features.Length - 1;
        builder.Append(lastFeature ? "└─ " : "├─ ").Append(feature.Label).Append(" [").Append(feature.Key).AppendLine("]");
        for (var stepIndex = 0; stepIndex < feature.Steps.Length; stepIndex++)
        {
            var step = feature.Steps[stepIndex];
            builder.Append(lastFeature ? "   " : "│  ")
                .Append(stepIndex == feature.Steps.Length - 1 ? "└─ " : "├─ ")
                .Append(step.FlowOrder.ToString("D3")).Append(' ')
                .Append(step.StepKey).Append(" · ").Append(step.Layer).Append(" · ")
                .Append(step.ExecutionStage).AppendLine();
        }
        if (feature.SourceBindings is { Length: > 0 })
            foreach (var binding in feature.SourceBindings)
                builder.Append(lastFeature ? "   " : "│  ")
                    .Append("  소스 결속 · ").AppendLine(binding.Key);
    }
    builder.AppendLine("```");
    builder.AppendLine();
    builder.Append("기능 하나만 보려면 `dotnet run --project eng/Ssalddel.CodeMap -- ")
        .Append(extended ? "--work-area " + document.WorkAreaKey + " " : string.Empty)
        .AppendLine("--feature <기능키>`를 사용한다.");
    builder.AppendLine();
    foreach (var feature in document.Features)
    {
        builder.Append("## ").Append(feature.Label).Append(" (`").Append(feature.Key).AppendLine("`)");
        builder.AppendLine();
        if (feature.DependsOnFeatures.Length > 0)
        {
            builder.Append("선행 기능: ").AppendLine(string.Join(", ", feature.DependsOnFeatures.Select(value => "`" + value + "`")));
            builder.AppendLine();
        }

        foreach (var step in feature.Steps)
        {
            builder.Append("- **").Append(step.FlowOrder.ToString("D3")).Append(' ')
                .Append(step.StepKey).Append("** — ");
            if (step.SourcePath is not null)
            {
                builder.Append('[').Append(step.ComponentType.Split('.').Last()).Append("](")
                    .Append("../../../").Append(step.SourcePath)
                    .Append(step.SourceLine is { } line ? "#L" + line : string.Empty).Append(')');
            }
            else
            {
                builder.Append('`').Append(step.ComponentType).Append('`');
            }
            builder.Append(" · ").Append(step.Responsibility).AppendLine();
            builder.Append("  - 계층/단계: `").Append(step.Layer).Append(" / ").Append(step.ExecutionStage).AppendLine("`");
            builder.Append("  - 읽기/쓰기: `").Append(JoinOrNone(step.ReadsFrom)).Append(" → ").Append(JoinOrNone(step.WritesTo)).AppendLine("`");
            builder.Append("  - 부수효과: `").Append(JoinOrNone(step.Effects)).AppendLine("`");
            builder.Append("  - 경계: ").AppendLine(step.Boundary);
            if (extended)
            {
                if (step.ContractType is not null)
                    builder.Append("  - 주 계약: `").Append(step.ContractType).AppendLine("`");
                builder.Append("  - 먼저 읽을 단계: ").AppendLine(JoinOrNone(step.DependsOnStepKeys));
            }
            if (step.ReuseKind is not null)
            {
                builder.Append("  - 재사용 종류: `").Append(step.ReuseKind).AppendLine("`");
                foreach (var reference in step.SourceCodeRefs ?? Array.Empty<string>())
                    builder.Append("  - 원천 코드: [").Append(Path.GetFileName(reference))
                        .Append("](../../../").Append(reference).AppendLine(")");
                foreach (var reference in step.SharedRuleRefs ?? Array.Empty<string>())
                    builder.Append("  - 공유 규칙: [").Append(Path.GetFileName(reference))
                        .Append("](../../../").Append(reference).AppendLine(")");
                builder.Append("  - 변형/제외: ").AppendLine(step.Adaptation);
            }
        }
        if (feature.SourceBindings is { Length: > 0 })
        {
            builder.AppendLine();
            builder.AppendLine("### 소스 결속");
            builder.AppendLine();
            foreach (var binding in feature.SourceBindings)
            {
                builder.Append("- **").Append(binding.Key).Append("** · ").AppendLine(binding.Responsibility);
                builder.Append("  - 관련 단계(탐색): ").AppendLine(string.Join(", ", binding.RelatedStepKeys.Select(key => "`" + key + "`")));
                builder.Append("  - 경계: ").AppendLine(binding.Boundary);
                foreach (var source in binding.Sources)
                    builder.Append("  - [").Append(source.Path).Append(':').Append(source.Line)
                        .Append("](../../../").Append(source.Path).Append("#L").Append(source.Line)
                        .Append(") · `").Append(source.Symbol).AppendLine("`");
            }
        }
        builder.AppendLine();
    }

    var errors = document.Diagnostics.Count(item => item.Severity == "Error");
    var warnings = document.Diagnostics.Count(item => item.Severity == "Warning");
    builder.AppendLine("## 진단 요약");
    builder.AppendLine();
    builder.Append("- 오류: ").AppendLine(errors.ToString());
    builder.Append("- 경고: ").AppendLine(warnings.ToString());
    builder.AppendLine("- 일반 공개 타입의 미표기는 경고이며, 필수 단계·권위 위반·오래된 생성 파일만 검증을 차단한다.");
    return builder.ToString();
}

static string BuildFeatureTree(CodeMapFeature feature)
{
    var builder = new StringBuilder();
    builder.Append(feature.Label).Append(" [").Append(feature.Key).AppendLine("]");
    for (var index = 0; index < feature.Steps.Length; index++)
    {
        var step = feature.Steps[index];
        builder.Append(index == feature.Steps.Length - 1 ? "└─ " : "├─ ")
            .Append(step.FlowOrder.ToString("D3")).Append(' ')
            .Append(step.StepKey).Append(" · ").Append(step.ComponentType).AppendLine();
        if (step.ReuseKind is not null)
        {
            builder.Append("   재사용: ").AppendLine(step.ReuseKind);
            builder.Append("   원천: ").AppendLine(string.Join(" | ", step.SourceCodeRefs ?? Array.Empty<string>()));
            builder.Append("   공유 규칙: ").AppendLine(string.Join(" | ", step.SharedRuleRefs ?? Array.Empty<string>()));
            builder.Append("   변형/제외: ").AppendLine(step.Adaptation);
        }
        if (step.SourceLine is not null)
        {
            builder.Append("   소스: ").Append(step.SourcePath).Append(':').AppendLine(step.SourceLine.ToString());
            builder.Append("   먼저 읽을 단계: ").AppendLine(JoinOrNone(step.DependsOnStepKeys));
            if (step.ContractType is not null) builder.Append("   주 계약: ").AppendLine(step.ContractType);
            builder.Append("   경계: ").AppendLine(step.Boundary);
        }
    }
    if (feature.SourceBindings is { Length: > 0 })
    {
        builder.AppendLine("소스 결속(정적 탐색 연결; reflection 단계와 별도)");
        foreach (var binding in feature.SourceBindings)
        {
            builder.Append("- ").Append(binding.Key).Append(" · ").AppendLine(binding.Responsibility);
            builder.Append("  관련 단계: ").AppendLine(string.Join(", ", binding.RelatedStepKeys));
            builder.Append("  경계: ").AppendLine(binding.Boundary);
            foreach (var source in binding.Sources)
                builder.Append("  ").Append(source.Path).Append(':').Append(source.Line).Append(" · ").AppendLine(source.Symbol);
        }
    }
    return builder.ToString();
}

static string JoinOrNone(string[] values) => values.Length == 0 ? "None" : string.Join(" | ", values);

static SsalddelCodeMetadataDiagnostic Error(string code, string featureKey, string stepKey, string message)
    => new(SsalddelCodeMetadataDiagnosticSeverity.Error, code, featureKey, stepKey, message);

static SsalddelCodeMetadataDiagnostic Warning(string code, string featureKey, string stepKey, string message)
    => new(SsalddelCodeMetadataDiagnosticSeverity.Warning, code, featureKey, stepKey, message);

internal sealed class CodeMapDocument
{
    [JsonIgnore]
    public string WorkAreaLabel { get; set; } = string.Empty;
    public string SchemaVersion { get; set; } = string.Empty;
    public string WorkAreaKey { get; set; } = string.Empty;
    public string WorkAreaVersion { get; set; } = string.Empty;
    public CodeMapFeature[] Features { get; set; } = Array.Empty<CodeMapFeature>();
    public CodeMapDiagnostic[] Diagnostics { get; set; } = Array.Empty<CodeMapDiagnostic>();
}

internal sealed class CodeMapFeature
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string[] DependsOnFeatures { get; set; } = Array.Empty<string>();
    public CodeMapStep[] Steps { get; set; } = Array.Empty<CodeMapStep>();
    public ResolvedCodeMapSourceBinding[]? SourceBindings { get; set; }
}

internal sealed class CodeMapStep
{
    public string[]? SourceCodeRefs { get; set; }
    public string? ReuseKind { get; set; }
    public string[]? SharedRuleRefs { get; set; }
    public string? Adaptation { get; set; }
    public int FlowOrder { get; set; }
    public string StepKey { get; set; } = string.Empty;
    public string[] DependsOnStepKeys { get; set; } = Array.Empty<string>();
    public string ExecutionStage { get; set; } = string.Empty;
    public string Layer { get; set; } = string.Empty;
    public string ComponentType { get; set; } = string.Empty;
    public string? ContractType { get; set; }
    public string Assembly { get; set; } = string.Empty;
    public string? SourcePath { get; set; }
    public int? SourceLine { get; set; }
    public string Responsibility { get; set; } = string.Empty;
    public string[] Effects { get; set; } = Array.Empty<string>();
    public string[] ReadsFrom { get; set; } = Array.Empty<string>();
    public string[] WritesTo { get; set; } = Array.Empty<string>();
    public string Boundary { get; set; } = string.Empty;
}

internal sealed class CodeMapDiagnostic
{
    public string Severity { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string FeatureKey { get; set; } = string.Empty;
    public string StepKey { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
