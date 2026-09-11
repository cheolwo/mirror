using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Ssalddel.Simulation.Application;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;
using Ssalddel.Simulation.Hosting;

var repositoryRoot = FindRepositoryRoot();
var builder = Host.CreateApplicationBuilder(args);
builder.Configuration
    .AddJsonFile(
        Path.Combine(repositoryRoot, "Ssalddel", "appsettings.json"),
        optional: false)
    .AddJsonFile(
        Path.Combine(repositoryRoot, "Ssalddel", "appsettings.Local.json"),
        optional: true)
    .AddEnvironmentVariables()
    .AddCommandLine(args);

var requiresWorldDerivationDatabase = args.Any(argument =>
    string.Equals(argument, "--migrate-simulation-world-database", StringComparison.OrdinalIgnoreCase)
    || string.Equals(argument, "--build-pyeongchang-world-derived", StringComparison.OrdinalIgnoreCase)
    || string.Equals(argument, "--build-pyeongchang-synty-landscape", StringComparison.OrdinalIgnoreCase)
    || string.Equals(argument, "--assemble-pyeongchang-landscape-graph", StringComparison.OrdinalIgnoreCase)
    || string.Equals(argument, "--build-pyeongchang-wi-spatial-bindings", StringComparison.OrdinalIgnoreCase)
    || string.Equals(argument, "--render-pyeongchang-area-set-status", StringComparison.OrdinalIgnoreCase)
    || string.Equals(argument, "--assemble-pyeongchang-world-business-rules", StringComparison.OrdinalIgnoreCase));
requiresWorldDerivationDatabase = requiresWorldDerivationDatabase || args.Any(argument =>
    string.Equals(argument, "--plan-pyeongchang-world-ui", StringComparison.OrdinalIgnoreCase));
if (requiresWorldDerivationDatabase)
    builder.Configuration["SimulationWorldDerivationDatabase:Enabled"] = "true";
var landscapeGrammarArgument = args.FirstOrDefault(argument =>
    argument.StartsWith("--landscape-grammar=", StringComparison.OrdinalIgnoreCase));
if (landscapeGrammarArgument != null)
    builder.Configuration["SimulationWorldDerivationDatabase:LandscapeGrammarManifestPath"] =
        landscapeGrammarArgument["--landscape-grammar=".Length..];

if (args.Contains("--migrate-simulation-session-database",
        StringComparer.OrdinalIgnoreCase))
{
    builder.Configuration["SimulationSessionDatabase:Enabled"] = "true";
}

if (args.Contains("--build-pyeongchang-world-derived", StringComparer.OrdinalIgnoreCase))
    builder.Configuration["SimulationSharedPublicData:Enabled"] = "true";

if (args.Length == 0 || args.Contains("--help", StringComparer.OrdinalIgnoreCase))
{
    Console.WriteLine(
        "Ssalddel Simulation 유지보수 CLI\n"
        + "  --migrate-simulation-session-database\n"
        + "  --migrate-simulation-world-database\n"
        + "  --build-pyeongchang-world-derived\n"
        + "  --build-pyeongchang-synty-landscape\n"
        + "  --assemble-pyeongchang-landscape-graph\n"
        + "  --build-pyeongchang-wi-spatial-bindings\n"
        + "  --render-pyeongchang-area-set-status\n"
        + "  --interpret-pyeongchang-building-type-demo\n"
        + "  --assemble-pyeongchang-world-business-rules\n"
        + "  --plan-pyeongchang-world-ui\n"
        + "각 명령의 필수 --key=value 인수는 README와 오류 메시지에서 확인하세요.");
    return;
}

builder.Services.AddSsalddelSimulationModule(
    builder.Configuration,
    builder.Environment);

using var host = builder.Build();

if (args.Contains("--migrate-simulation-session-database",
        StringComparer.OrdinalIgnoreCase))
{
    var factory = host.Services.GetRequiredService<IDbContextFactory<
        Ssalddel.Simulation.Persistence.SimulationSessionDbContext>>();
    await using var sessionDb = await factory.CreateDbContextAsync();
    await sessionDb.Database.MigrateAsync(CancellationToken.None);
    Console.WriteLine("SimulationSessionDBMigration완료");
    return;
}

if (args.Contains("--migrate-simulation-world-database", StringComparer.OrdinalIgnoreCase))
{
    await using var scope = host.Services.CreateAsyncScope();
    var worldDb = scope.ServiceProvider.GetRequiredService<
        Ssalddel.Simulation.Persistence.SimulationWorld파생DbContext>();
    await worldDb.Database.MigrateAsync(CancellationToken.None);
    Console.WriteLine("SimulationWorld파생DBMigration완료");
    return;
}

if (args.Contains("--build-pyeongchang-world-derived", StringComparer.OrdinalIgnoreCase))
{
    await using var scope = host.Services.CreateAsyncScope();
    var pipeline = scope.ServiceProvider.GetRequiredService<
        Ssalddel.Simulation.Persistence.평창군공간파생Pipeline>();
    var tileManifestArgument = args.FirstOrDefault(argument =>
        argument.StartsWith("--tile-manifest=", StringComparison.OrdinalIgnoreCase));
    var spatialArtifactManifestArgument = args.FirstOrDefault(argument =>
        argument.StartsWith("--spatial-artifact-manifest=", StringComparison.OrdinalIgnoreCase));
    var result = await pipeline.실행Async(
        tileManifestArgument?["--tile-manifest=".Length..],
        spatialArtifactManifestArgument?["--spatial-artifact-manifest=".Length..],
        CancellationToken.None);
    Console.WriteLine(
        $"평창군공간파생완료:상태={result.상태코드};새실행본={result.새실행본저장여부};" +
        $"원본건축물={result.건축물수};대표건축물={result.대표건축물수};대표군={result.대표군수};" +
        $"표현제외건축물={result.표현제외건축물수};원본공개사업장={result.공개사업장수};" +
        $"대표공개사업장={result.대표공개사업장수};" +
        $"연결={result.사업장건물연결수};미배치건축물={result.미배치건축물수};" +
        $"Unity변환Profile={result.Unity공간변환Profile수};Unity타일Manifest={result.Unity타일Manifest수};" +
        $"Unity산출물={result.Unity산출물수};" +
        $"파생실행={result.파생실행고유식별자};출력SHA256={result.출력해시SHA256}");
    return;
}

if (args.Contains("--build-pyeongchang-synty-landscape", StringComparer.OrdinalIgnoreCase))
{
    await using var scope = host.Services.CreateAsyncScope();
    var spatialBuildArgument = args.FirstOrDefault(argument =>
        argument.StartsWith("--spatial-build=", StringComparison.OrdinalIgnoreCase));
    if (spatialBuildArgument == null)
        throw new InvalidOperationException("--spatial-build is required.");
    var spatialBuildStableId = spatialBuildArgument["--spatial-build=".Length..];
    var spatialReader = scope.ServiceProvider.GetRequiredService<ISimulationWorld공간실행Reader>();
    var spatialBuild = await spatialReader.조회Async(spatialBuildStableId, CancellationToken.None)
        ?? throw new InvalidOperationException(
            SimulationWorldSynty경관JobShell.SpatialBuildNotFoundCode);
    var shell = scope.ServiceProvider.GetRequiredService<SimulationWorldSynty경관JobShell>();
    var request = new SimulationWorldSynty경관Job요청
    {
        JobStableId = "synty-job:pyeongchang:" + spatialBuild.OutputHashSha256[..16] + ":pc-high:v2",
        SpatialBuildStableId = spatialBuild.BuildStableId,
        SpatialOutputHashSha256 = spatialBuild.OutputHashSha256,
        AreaSetStableId = spatialBuild.AreaSetStableId,
        ScopeKindCode = SimulationWorldSynty범위Codes.영역묶음,
        ScopeStableId = spatialBuild.AreaSetStableId,
        LandscapeRuleRevision = "pyeongchang-synty-landscape.v2",
        VisualCatalogRevision = "legal-dong-scenic-catalog.v2",
        UrpProfileCatalogRevision = "urp-pyeongchang-four-pack.v2",
        Seed = 51760,
        TargetPlatformCode = SimulationWorldSynty대상플랫폼Codes.PC,
        QualityTierCode = "PC-High",
    };
    var result = await shell.실행Async(request, CancellationToken.None);
    Console.WriteLine(
        $"평창군Synty경관Job완료:상태={result.StatusCode};새실행본={result.Inserted};" +
        $"그래픽계획={result.GraphicsPlanCount};시각배치={result.VisualPlacementCount};" +
        $"배치거부={result.RejectionCount};시각실행={result.VisualBuildStableId};" +
        $"출력SHA256={result.OutputHashSha256}");
    return;
}

if (args.Contains("--assemble-pyeongchang-landscape-graph", StringComparer.OrdinalIgnoreCase))
{
    await using var scope = host.Services.CreateAsyncScope();
    var shell = scope.ServiceProvider.GetRequiredService<
        SimulationWorldAreaSetLandscapeGraphJobShell>();
    var results = await shell.BuildAsync(CancellationToken.None);
    Console.WriteLine(
        $"평창군경관Graph조립완료:Graph={results.Count};" +
        $"준비={results.Count(item => item.StatusCode == SimulationWorldLandscapeCompositionCodes.Available)};" +
        $"대기={results.Count(item => item.StatusCode == SimulationWorldLandscapeCompositionCodes.WaitingForSpatialArtifact)};" +
        $"미해결={results.Sum(item => item.Unresolved.Length)}");
    foreach (var result in results)
        Console.WriteLine(
            $"경관Graph:{result.LandscapeGraphStableId};상태={result.StatusCode};" +
            $"타일={result.TileRefs.Length};" +
            $"Node={result.Nodes.Length};Edge={result.Edges.Length};" +
            $"배치={result.Placements.Length};외부연결={result.ExternalConnectorStubs.Length};" +
            $"SHA256={result.GraphHashSha256}");
    return;
}

if (args.Contains("--render-pyeongchang-area-set-status", StringComparer.OrdinalIgnoreCase))
{
    await using var scope = host.Services.CreateAsyncScope();
    var service = scope.ServiceProvider.GetRequiredService<
        SimulationWorldAreaSetLandscapeGraphService>();
    var areaSet = await service.ReadAreaSetAsync(
        PyeongchangAreaSetStableIds.AreaSet, CancellationToken.None)
        ?? throw new InvalidOperationException("PyeongchangAreaSetNotFound");
    var graphs = new List<SimulationWorldLandscapeGraphResponse>();
    foreach (var descriptor in areaSet.LandscapeGraphs)
        graphs.Add(await service.ReadGraphAsync(
            descriptor.LandscapeGraphStableId, CancellationToken.None)
            ?? throw new InvalidOperationException(
                "PyeongchangLandscapeGraphNotFound:" + descriptor.LandscapeGraphStableId));
    var outputArgument = args.FirstOrDefault(argument =>
        argument.StartsWith("--area-set-status-output=", StringComparison.OrdinalIgnoreCase));
    var outputPath = outputArgument?["--area-set-status-output=".Length..]
                     ?? "eng/world-seedbeds/area-sets/pyeongchang-farm-hub-town.v1/generated/area-set-status.md";
    // dotnet run은 프로세스 작업 경로와 ContentRootPath를 Server 폴더로 맞춘다.
    // 기본 상대 경로는 한 단계 위 저장소 루트를 기준으로 해석한다.
    var fullOutputPath = Path.IsPathRooted(outputPath)
        ? Path.GetFullPath(outputPath)
        : Path.GetFullPath(outputPath, repositoryRoot);
    Directory.CreateDirectory(Path.GetDirectoryName(fullOutputPath)!);
    await File.WriteAllTextAsync(
        fullOutputPath,
        SimulationWorldAreaSetStatusMarkdownRenderer.Render(areaSet, graphs),
        new System.Text.UTF8Encoding(false),
        CancellationToken.None);
    Console.WriteLine("평창AreaSet상태문서생성완료:" + fullOutputPath);
    return;
}

if (args.Contains("--build-pyeongchang-wi-spatial-bindings", StringComparer.OrdinalIgnoreCase))
{
    await using var scope = host.Services.CreateAsyncScope();
    var shell = scope.ServiceProvider.GetRequiredService<SimulationWorld상호작용GraphJobShell>();
    var result = await shell.BuildAsync(PyeongchangAreaSetStableIds.AreaSet, CancellationToken.None);
    var readinessStore = scope.ServiceProvider.GetRequiredService<
        ISimulationWorld상호작용GraphReadinessStore>();
    var stored = await readinessStore.ReadLatestAsync(
        PyeongchangAreaSetStableIds.AreaSet, CancellationToken.None)
        ?? throw new InvalidOperationException("PyeongchangWiSpatialReadbackMissing");
    if (!string.Equals(result.BindingCatalogHashSha256,
            stored.BindingCatalogHashSha256, StringComparison.Ordinal)
        || !string.Equals(result.AreaSetDefinitionHashSha256,
            stored.AreaSetDefinitionHashSha256, StringComparison.Ordinal))
        throw new InvalidOperationException("PyeongchangWiSpatialReadbackMismatch");
    Console.WriteLine(
        $"평창WI공간Graph연결완료:상태={result.OverallStatusCode};" +
        $"폐루프={result.Bindings.Count(item => item.SpatialClosedLoop)};" +
        $"전체WI={result.Bindings.Length};DB재조회=일치;" +
        $"대장SHA256={result.BindingCatalogHashSha256}");
    return;
}

if (args.Contains("--interpret-pyeongchang-building-type-demo", StringComparer.OrdinalIgnoreCase))
{
    await using var scope = host.Services.CreateAsyncScope();
    var spatialBuildArgument = args.FirstOrDefault(argument =>
        argument.StartsWith("--spatial-build=", StringComparison.OrdinalIgnoreCase));
    if (spatialBuildArgument == null)
        throw new InvalidOperationException("--spatial-build is required.");
    var pipeline = scope.ServiceProvider.GetRequiredService<SimulationWorld건물종류DemoPipeline>();
    var result = await pipeline.실행Async(
        spatialBuildArgument["--spatial-build=".Length..], CancellationToken.None);
    Console.WriteLine(
        $"평창군건물종류Demo완료:규칙대장신규={result.RuleCatalogInserted};" +
        $"해석실행신규={result.InterpretationInserted};건물종류={result.Presentations.Count};" +
        $"해석실행={result.InterpretationStableId};출력SHA256={result.OutputHashSha256}");
    foreach (var item in result.Presentations)
        Console.WriteLine(
            $"건물종류Demo:{item.BuildingCategoryCode};대표원본={item.RepresentedRecordCount};" +
            $"시험상태={item.FixtureSimulationStateCode};기본구성={item.DefaultCompositionKey};" +
            $"동적의도={item.DynamicIntentBundleKey}");
    return;
}

if (args.Contains("--assemble-pyeongchang-world-business-rules", StringComparer.OrdinalIgnoreCase))
{
    await using var scope = host.Services.CreateAsyncScope();
    var spatialBuildArgument = args.FirstOrDefault(argument =>
        argument.StartsWith("--spatial-build=", StringComparison.OrdinalIgnoreCase));
    if (spatialBuildArgument == null)
        throw new InvalidOperationException("--spatial-build is required.");
    var shell = scope.ServiceProvider.GetRequiredService<SimulationWorld업무규칙집결JobShell>();
    var result = await shell.실행Async(
        spatialBuildArgument["--spatial-build=".Length..], CancellationToken.None);
    Console.WriteLine(
        $"평창군업무Simulation규칙집결완료:신규={result.Inserted};시설={result.FacilityCount};" +
        $"기능={result.CapabilityCount};규칙={result.RuleCount};연결={result.BindingCount};" +
        $"Scenario규칙묶음={result.ScenarioRuleSetCount};대장={result.CatalogRevision};" +
        $"SHA256={result.CatalogHashSha256}");
    return;
}

if (args.Contains("--plan-pyeongchang-world-ui", StringComparer.OrdinalIgnoreCase))
{
    await using var scope = host.Services.CreateAsyncScope();
    var ruleCatalogArgument = args.FirstOrDefault(argument =>
        argument.StartsWith("--business-rule-catalog=", StringComparison.OrdinalIgnoreCase));
    if (ruleCatalogArgument == null)
        throw new InvalidOperationException("--business-rule-catalog is required.");
    var shell = scope.ServiceProvider.GetRequiredService<SimulationWorldUI기획JobShell>();
    var result = await shell.실행Async(
        ruleCatalogArgument["--business-rule-catalog=".Length..], CancellationToken.None);
    Console.WriteLine(
        $"평창군SimulationWorldUI기획완료:신규={result.Inserted};화면영역={result.SurfaceCount};" +
        $"정보항목={result.InformationItemCount};상태표현={result.StatePresentationCount};" +
        $"행동후보={result.ActionCandidateCount};규칙연결={result.RuleBindingCount};" +
        $"대장={result.CatalogRevision};SHA256={result.CatalogHashSha256}");
    return;
}

Console.Error.WriteLine(
    "Simulation 유지보수 명령이 필요합니다. README의 지원 명령을 확인하세요.");
Environment.ExitCode = 2;

static string FindRepositoryRoot()
{
    for (var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
         directory is not null;
         directory = directory.Parent)
    {
        if (File.Exists(Path.Combine(directory.FullName, "Ssalddel.slnx")))
            return directory.FullName;
    }

    throw new DirectoryNotFoundException("Ssalddel 저장소 루트를 찾지 못했습니다.");
}
