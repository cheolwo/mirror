using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Simulation.Application;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;
using Ssalddel.Simulation.Infrastructure;
using Ssalddel.Simulation.Persistence;
using Ssalddel.Simulation.Hosting.Controllers;
using Ssalddel.Simulation.Tests;
using Ssalddel.Unity.Application;
using Ssalddel.Unity.Tests;

var repositoryRoot = FindRepositoryRoot();
var moduleCatalogPath = Path.Combine(repositoryRoot, "eng", "execution-ledgers",
    "evidence-responsibility-module-catalog.json");
var moduleCatalog = JsonSerializer.Deserialize<ModuleCatalogDocument>(
    File.ReadAllText(moduleCatalogPath, Encoding.UTF8), JsonOptions())
    ?? throw new InvalidOperationException("E 책임 모듈 대장을 읽을 수 없습니다.");

var assemblies = new[]
{
    typeof(경영SimulationSession생성Request).Assembly,
    typeof(경영SimulationSessionAggregate).Assembly,
    typeof(LocalSimulationRuntime).Assembly,
    typeof(InMemory경영SimulationSessionStore).Assembly,
    typeof(SimulationSessionSaveStore).Assembly,
    typeof(경영SimulationSessionsController).Assembly,
    typeof(SimulationUnityCodeMetadataTests).Assembly,
    typeof(LastSuccessfulLoadRuntime<,>).Assembly,
    typeof(UnityCodeMetadataTests).Assembly,
}.Distinct().ToArray();

ValidateModuleCatalog(moduleCatalog);
var responsibilities = SsalddelEvidenceResponsibilityReader.Read(assemblies);
var exclusions = SsalddelEvidenceResponsibilityReader.ReadExclusions(assemblies);
var strict = args.Contains("--strict", StringComparer.Ordinal);
var diagnostics = SsalddelEvidenceCoverageValidator.Validate(strict, assemblies)
    .ToArray();
var codeMetadata = SsalddelCodeMetadataReader.Read(assemblies);
var candidates = assemblies.SelectMany(GetLoadableTypes)
    .Where(SsalddelEvidenceCoveragePolicy.IsCandidate)
    .OrderBy(ComponentId, StringComparer.Ordinal)
    .ToArray();
var candidateSet = candidates.ToHashSet();
var mappedTypes = candidates.Concat(responsibilities.Select(item => item.ComponentType))
    .Concat(exclusions.Where(item => item.ComponentType is not null)
        .Select(item => item.ComponentType!))
    .Distinct().OrderBy(ComponentId, StringComparer.Ordinal).ToArray();
var sourceIndex = BuildSourceIndex(repositoryRoot, mappedTypes);

var components = new List<ComponentDocument>();
foreach (var candidate in mappedTypes)
{
    var componentId = ComponentId(candidate);
    var assemblyExclusion = exclusions.SingleOrDefault(item =>
        item.ComponentType is null && item.Assembly == candidate.Assembly);
    var typeExclusion = exclusions.SingleOrDefault(item =>
        item.ComponentType == candidate);
    var exclusion = typeExclusion ?? assemblyExclusion;
    var typeResponsibilities = responsibilities.Where(item =>
        item.ComponentType == candidate && item.ComponentMethod is null).ToArray();
    var primary = typeResponsibilities.SingleOrDefault(item =>
        item.Role == SsalddelEvidenceResponsibilityRole.Primary);
    var secondary = typeResponsibilities.Where(item =>
        item.Role == SsalddelEvidenceResponsibilityRole.Secondary).ToArray();
    var flowMetadata = codeMetadata.Where(item => item.ComponentType == candidate)
        .ToArray();

    components.Add(new ComponentDocument(
        componentId,
        candidateSet.Contains(candidate),
        candidate.Assembly.GetName().Name ?? string.Empty,
        candidate.FullName ?? candidate.Name,
        "Type",
        sourceIndex.GetValueOrDefault(candidate),
        exclusion is not null ? "Excluded"
            : primary is not null ? "Annotated" : "Uncovered",
        primary is null ? null : primary.EvidenceStage.ToString(),
        primary?.Responsibility,
        primary?.Boundary,
        primary?.SubmoduleKey,
        secondary.Select(item => item.EvidenceStage.ToString()).ToArray(),
        secondary.Select(item => item.SubmoduleKey)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal).OrderBy(value => value,
                StringComparer.Ordinal).ToArray(),
        typeResponsibilities.SelectMany(item => item.WorldInteractionIds)
            .Distinct(StringComparer.Ordinal).OrderBy(value => value,
                StringComparer.Ordinal).ToArray(),
        typeResponsibilities.SelectMany(item => item.WorkOrderIds)
            .Distinct(StringComparer.Ordinal).OrderBy(value => value,
                StringComparer.Ordinal).ToArray(),
        flowMetadata.Select(item => item.FeatureKey).Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal).ToArray(),
        flowMetadata.Select(item => item.StepKey)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal).OrderBy(value => value,
                StringComparer.Ordinal).ToArray(),
        exclusion?.Category.ToString(),
        exclusion?.Reason));
}

foreach (var method in responsibilities.Where(item => item.ComponentMethod is not null))
{
    components.Add(new ComponentDocument(
        method.ComponentId,
        false,
        method.ComponentType.Assembly.GetName().Name ?? string.Empty,
        (method.ComponentType.FullName ?? method.ComponentType.Name) + "." +
        method.ComponentMethod!.Name,
        "Method",
        sourceIndex.GetValueOrDefault(method.ComponentType),
        "Annotated",
        method.EvidenceStage.ToString(),
        method.Responsibility,
        method.Boundary,
        method.SubmoduleKey,
        Array.Empty<string>(),
        Array.Empty<string>(),
        method.WorldInteractionIds.ToArray(),
        method.WorkOrderIds.ToArray(),
        Array.Empty<string>(),
        Array.Empty<string>(),
        null,
        null));
}

var stageDocuments = moduleCatalog.Modules
    .OrderBy(item => ParseStage(item.EvidenceStage))
    .Select(module => new StageDocument(
        module.EvidenceStage,
        module.ManagementSystem,
        module.KoreanName,
        module.TechnicalName,
        components.Count(item => item.PrimaryEvidenceStage == module.EvidenceStage),
        components.Count(item => item.SecondaryEvidenceStages.Contains(
            module.EvidenceStage, StringComparer.Ordinal))))
    .ToArray();
var typeComponents = components.Where(item => item.IsCoverageCandidate).ToArray();
var submoduleDocuments = SsalddelEvidenceSubmoduleDefinitionCatalog.All
    .OrderBy(item => item.EvidenceStage)
    .ThenBy(item => item.SubmoduleKey, StringComparer.Ordinal)
    .Select(item => new SubmoduleDocument(
        item.SubmoduleKey,
        item.EvidenceStage.ToString(),
        item.KoreanName,
        item.TechnicalName,
        item.Responsibility,
        components.Count(component => string.Equals(
            component.PrimarySubmoduleKey, item.SubmoduleKey,
            StringComparison.Ordinal)),
        components.Count(component => component.SecondarySubmoduleKeys.Contains(
            item.SubmoduleKey, StringComparer.Ordinal))))
    .ToArray();
if (submoduleDocuments.Any(item => item.PrimaryCount + item.SecondaryCount == 0))
    throw new InvalidOperationException(
        "E1~E3 하위 모듈에는 최소 한 개의 대표 구성 요소 결속이 필요합니다.");
var document = new EvidenceMapDocument(
    "ssalddel-evidence-responsibility-map.v2",
    "hongdal-simulation-unity-shared",
    moduleCatalog.Revision,
    stageDocuments,
    submoduleDocuments,
    components.OrderBy(item => item.ComponentId, StringComparer.Ordinal).ToArray(),
    new CoverageDocument(
        typeComponents.Length,
        typeComponents.Count(item => item.CoverageState == "Annotated"),
        typeComponents.Count(item => item.CoverageState == "Excluded"),
        typeComponents.Count(item => item.CoverageState == "Uncovered"),
        components.Count(item => item.MemberKind == "Method")),
    diagnostics.Select(item => new DiagnosticDocument(
        item.Severity.ToString(), item.Code, item.ComponentId, item.Message))
        .ToArray());

var json = JsonSerializer.Serialize(document, JsonOptions()) + Environment.NewLine;
var markdown = BuildMarkdown(document);
var jsonPath = Path.Combine(repositoryRoot, "docs", "AI", "generated",
    "evidence-responsibility-code-map.json");
var markdownPath = Path.Combine(repositoryRoot, "docs", "AI", "generated",
    "evidence-responsibility-code-map.md");

if (args.Contains("--write", StringComparer.Ordinal))
{
    Directory.CreateDirectory(Path.GetDirectoryName(jsonPath)!);
    File.WriteAllText(jsonPath, json, new UTF8Encoding(false));
    File.WriteAllText(markdownPath, markdown, new UTF8Encoding(false));
    Console.WriteLine($"E 책임 코드 지도를 갱신했습니다: Candidates={document.Coverage.CandidateTypeCount};Annotated={document.Coverage.AnnotatedTypeCount};Excluded={document.Coverage.ExcludedTypeCount};Uncovered={document.Coverage.UncoveredTypeCount};Methods={document.Coverage.AnnotatedMethodCount}");
}
else
{
    var stale = !File.Exists(jsonPath) || !File.Exists(markdownPath)
        || File.ReadAllText(jsonPath, Encoding.UTF8) != json
        || File.ReadAllText(markdownPath, Encoding.UTF8) != markdown;
    if (stale)
    {
        Console.Error.WriteLine("E 책임 코드 지도가 현재 소스와 다릅니다. --write로 갱신하세요.");
        return 2;
    }
    Console.WriteLine("E 책임 코드 지도가 현재 소스와 일치합니다.");
}

foreach (var diagnostic in diagnostics)
    Console.WriteLine($"{diagnostic.Severity} {diagnostic.Code} {diagnostic.ComponentId}: {diagnostic.Message}");
return diagnostics.Any(item =>
    item.Severity == SsalddelEvidenceCoverageDiagnosticSeverity.Error) ? 1 : 0;

static string FindRepositoryRoot()
{
    for (var current = new DirectoryInfo(Directory.GetCurrentDirectory());
         current is not null; current = current.Parent)
    {
        if (File.Exists(Path.Combine(current.FullName, "AGENTS.md")) &&
            Directory.Exists(Path.Combine(current.FullName, "eng", "work-areas")))
            return current.FullName;
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

static IReadOnlyList<Type> GetLoadableTypes(Assembly assembly)
{
    try { return assembly.GetTypes(); }
    catch (ReflectionTypeLoadException exception)
    { return exception.Types.OfType<Type>().ToArray(); }
}

static string ComponentId(Type type)
    => (type.Assembly.GetName().Name ?? string.Empty) + ":" +
       (type.FullName ?? type.Name);

static int ParseStage(string stage)
    => Enum.TryParse<SsalddelEvidenceStage>(stage, out var parsed)
        ? (int)parsed : int.MaxValue;

static void ValidateModuleCatalog(ModuleCatalogDocument catalog)
{
    var expected = Enumerable.Range(1, 10).Select(value => "E" + value).ToArray();
    var actual = catalog.Modules.OrderBy(item => ParseStage(item.EvidenceStage))
        .Select(item => item.EvidenceStage).ToArray();
    if (!actual.SequenceEqual(expected, StringComparer.Ordinal))
        throw new InvalidOperationException("E 책임 모듈 대장은 E1~E10을 각각 하나씩 가져야 합니다.");
    foreach (var definition in SsalddelEvidenceStageDefinitionCatalog.All)
    {
        var source = catalog.Modules.Single(item => item.EvidenceStage ==
            definition.EvidenceStage.ToString());
        if (source.ManagementSystem != definition.ManagementSystem ||
            source.KoreanName != definition.KoreanName ||
            source.TechnicalName != definition.TechnicalName)
            throw new InvalidOperationException(
                $"E 책임 모듈 대장과 C# 투영이 다릅니다: {source.EvidenceStage}");
    }
    var submodules = SsalddelEvidenceSubmoduleDefinitionCatalog.All;
    if (submodules.Count != 16 || submodules.Select(item => item.SubmoduleKey)
            .Distinct(StringComparer.Ordinal).Count() != submodules.Count)
        throw new InvalidOperationException(
            "E1~E3 하위 모듈 카탈로그는 중복 없는 16개 정의를 가져야 합니다.");
    var expectedSubmoduleCountByStage = new Dictionary<
        SsalddelEvidenceStage, int>
    {
        [SsalddelEvidenceStage.E1] = 5,
        [SsalddelEvidenceStage.E2] = 6,
        [SsalddelEvidenceStage.E3] = 5,
    };
    foreach (var expectedCount in expectedSubmoduleCountByStage)
        if (submodules.Count(item => item.EvidenceStage ==
                                     expectedCount.Key) != expectedCount.Value)
            throw new InvalidOperationException(
                $"{expectedCount.Key} 하위 모듈은 {expectedCount.Value}개여야 합니다.");
}

static Dictionary<Type, string?> BuildSourceIndex(
    string repositoryRoot,
    IReadOnlyList<Type> types)
{
    var assemblyNames = types.Select(value => value.Assembly.GetName().Name)
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .ToHashSet(StringComparer.Ordinal);
    var roots = Directory.EnumerateDirectories(repositoryRoot, "Ssalddel*")
        .Where(path => assemblyNames.Contains(Path.GetFileName(path)))
        .ToArray();
    var files = roots.SelectMany(root => Directory.EnumerateFiles(root, "*.cs",
            SearchOption.AllDirectories))
        .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
        .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
        .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}artifacts{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
        .ToArray();
    var typesByName = types.GroupBy(type => type.Name.Split('`')[0],
            StringComparer.Ordinal)
        .ToDictionary(group => group.Key, group => group.ToArray(),
            StringComparer.Ordinal);
    var matchesByType = types.ToDictionary(type => type,
        _ => new List<string>());
    var declaration = new Regex(
        @"\b(?:class|interface|struct|record)\s+(?<name>[\p{L}\p{N}_]+)\b",
        RegexOptions.CultureInvariant);
    foreach (var file in files)
    {
        var content = File.ReadAllText(file, Encoding.UTF8);
        foreach (Match match in declaration.Matches(content))
        {
            if (!typesByName.TryGetValue(match.Groups["name"].Value,
                    out var namedTypes)) continue;
            foreach (var type in namedTypes) matchesByType[type].Add(file);
        }
    }
    var result = new Dictionary<Type, string?>();
    foreach (var type in types)
    {
        var matches = matchesByType[type].Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        result[type] = matches.Length == 1
            ? Path.GetRelativePath(repositoryRoot, matches[0]).Replace('\\', '/')
            : null;
    }
    return result;
}

static string BuildMarkdown(EvidenceMapDocument document)
{
    var builder = new StringBuilder();
    builder.AppendLine("# E 책임 코드 지도");
    builder.AppendLine();
    builder.AppendLine("> 이 문서는 C# E 책임 Attribute와 현재 E 책임 모듈 대장에서 자동 생성된다. 직접 수정하지 않는다.");
    builder.AppendLine();
    builder.AppendLine($"- 후보 타입: `{document.Coverage.CandidateTypeCount}`");
    builder.AppendLine($"- 책임 지정: `{document.Coverage.AnnotatedTypeCount}`");
    builder.AppendLine($"- 사유 있는 제외: `{document.Coverage.ExcludedTypeCount}`");
    builder.AppendLine($"- 미분류: `{document.Coverage.UncoveredTypeCount}`");
    builder.AppendLine($"- 메서드 책임: `{document.Coverage.AnnotatedMethodCount}`");
    builder.AppendLine();
    builder.AppendLine("## E 단계별 책임");
    builder.AppendLine();
    builder.AppendLine("| E | G | 모듈 | 대표 | 보조 |");
    builder.AppendLine("| --- | --- | --- | ---: | ---: |");
    foreach (var stage in document.Stages)
        builder.AppendLine($"| `{stage.EvidenceStage}` | `{stage.ManagementSystem}` | `{stage.TechnicalName}` {stage.KoreanName} | {stage.PrimaryCount} | {stage.SecondaryCount} |");
    builder.AppendLine();
    builder.AppendLine("## E1~E3 사람용 하위 모듈");
    builder.AppendLine();
    builder.AppendLine("> 하위 모듈은 E 단계를 추가하거나 증거를 승격하지 않는다. 넓은 E1~E3 책임을 사람이 탐색 가능한 묶음으로 나눈다.");
    builder.AppendLine();
    builder.AppendLine("| E | 하위 모듈 | 안정 key | 책임 | 대표 결속 | 보조 결속 |");
    builder.AppendLine("| --- | --- | --- | --- | ---: | ---: |");
    foreach (var submodule in document.Submodules)
        builder.AppendLine($"| `{submodule.EvidenceStage}` | `{submodule.TechnicalName}` {submodule.KoreanName} | `{submodule.SubmoduleKey}` | {submodule.Responsibility} | {submodule.PrimaryCount} | {submodule.SecondaryCount} |");
    builder.AppendLine();
    builder.AppendLine("### 아직 하위 모듈을 지정하지 않은 기존 책임");
    builder.AppendLine();
    foreach (var stage in new[] { "E1", "E2", "E3" })
    {
        var count = document.Components.Count(item =>
            item.PrimaryEvidenceStage == stage &&
            string.IsNullOrWhiteSpace(item.PrimarySubmoduleKey));
        builder.AppendLine($"- `{stage}`: `{count}`개");
    }
    builder.AppendLine();
    builder.AppendLine("## 미분류 후보");
    builder.AppendLine();
    var uncovered = document.Components.Where(item =>
        item.MemberKind == "Type" && item.CoverageState == "Uncovered").ToArray();
    if (uncovered.Length == 0) builder.AppendLine("미분류 후보가 없다.");
    else
    {
        builder.AppendLine("| 구성 요소 | 소스 |");
        builder.AppendLine("| --- | --- |");
        foreach (var item in uncovered)
            builder.AppendLine($"| `{item.ComponentName}` | `{item.SourcePath ?? "미확인"}` |");
    }
    builder.AppendLine();
    builder.AppendLine("## 분류된 구성 요소");
    builder.AppendLine();
    builder.AppendLine("| 구성 요소 | 대표 E | 하위 모듈 | 보조 E | WI | 상태 |");
    builder.AppendLine("| --- | --- | --- | --- | --- | --- |");
    foreach (var item in document.Components.Where(item =>
                 item.CoverageState != "Uncovered"))
        builder.AppendLine($"| `{item.ComponentName}` | `{item.PrimaryEvidenceStage ?? "-"}` | `{item.PrimarySubmoduleKey ?? ""}` | `{string.Join(",", item.SecondaryEvidenceStages)}` | `{string.Join(",", item.WorldInteractionIds)}` | `{item.CoverageState}` |");
    return builder.ToString();
}

sealed record ModuleCatalogDocument(string Revision, ModuleDocument[] Modules);
sealed record ModuleDocument(string EvidenceStage, string ManagementSystem,
    string KoreanName, string TechnicalName);
sealed record EvidenceMapDocument(string SchemaVersion, string RepositoryKey,
    string ModuleCatalogRevision, StageDocument[] Stages,
    SubmoduleDocument[] Submodules, ComponentDocument[] Components,
    CoverageDocument Coverage,
    DiagnosticDocument[] Diagnostics);
sealed record StageDocument(string EvidenceStage, string ManagementSystem,
    string KoreanName, string TechnicalName, int PrimaryCount,
    int SecondaryCount);
sealed record SubmoduleDocument(string SubmoduleKey, string EvidenceStage,
    string KoreanName, string TechnicalName, string Responsibility,
    int PrimaryCount, int SecondaryCount);
sealed record ComponentDocument(string ComponentId, bool IsCoverageCandidate,
    string AssemblyName,
    string ComponentName, string MemberKind, string? SourcePath,
    string CoverageState, string? PrimaryEvidenceStage,
    string? Responsibility, string? Boundary,
    string? PrimarySubmoduleKey, string[] SecondaryEvidenceStages,
    string[] SecondarySubmoduleKeys, string[] WorldInteractionIds,
    string[] WorkOrderIds, string[] FeatureKeys, string[] StepKeys,
    string? ExclusionCategory, string? ExclusionReason);
sealed record CoverageDocument(int CandidateTypeCount, int AnnotatedTypeCount,
    int ExcludedTypeCount, int UncoveredTypeCount, int AnnotatedMethodCount);
sealed record DiagnosticDocument(string Severity, string Code,
    string ComponentId, string Message);
