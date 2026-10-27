using System.Text.Json;
using Ssalddel.Contracts.Admin.DispatchTraining;
using static CargoRegionalTrainingReview.CargoTrainingStore;

namespace CargoRegionalTrainingReview;

/// <summary>검토 자료의 기준선·출처 판본과 불변 파일을 준비하고 변경을 검증한다.</summary>
internal sealed class CargoTrainingDatasetEvidence
{
    private readonly string repoRoot;
    private readonly string regionPath;
    private readonly string initialSourceFingerprint;
    private readonly byte[] datasetBytes;
    private readonly byte[] sourceManifestBytes;
    private readonly string archivePath;
    private readonly string archiveSha256;
    private readonly bool planReview;
    private IReadOnlyList<string> SourcePaths => planReview ? CargoTrainingPlanCases.SourcePaths : CargoTrainingCases.PolicySourcePaths;

    public CargoTrainingWorkspace Baseline { get; }
    public string OutputDirectory { get; }

    public CargoTrainingDatasetEvidence(string repoRoot, string outputRoot, bool planReview)
    {
        this.repoRoot = Path.GetFullPath(repoRoot);
        var fullOutputRoot = Path.GetFullPath(outputRoot);
        this.planReview = planReview;
        regionPath = Path.Combine(this.repoRoot, "eng/local-dispatch-ai/cargo-regional-training/legal-dong-regions.json");
        using var regions = JsonDocument.Parse(File.ReadAllBytes(regionPath));
        archivePath = Path.GetFullPath(Path.Combine(this.repoRoot, regions.RootElement.GetProperty("source").GetProperty("archivePath").GetString()!));
        archiveSha256 = FileHash(archivePath);
        initialSourceFingerprint = CurrentPolicyFingerprint();
        var cases = CargoTrainingCases.Build(regionPath);
        if (planReview) cases.AddRange(CargoTrainingPlanCases.Build(cases));
        foreach (var item in cases) CargoTrainingPlanCases.Verify(item);
        var bytes = System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { policy = initialSourceFingerprint, regionHash = FileHash(regionPath), cases }, Json));
        var datasetSha = Hash(bytes);
        OutputDirectory = Path.Combine(fullOutputRoot, datasetSha);
        Directory.CreateDirectory(OutputDirectory);
        Baseline = new CargoTrainingWorkspace
        {
            DatasetId = planReview ? "cargo-plan-training-r76" : "cargo-regional-training-r67", DatasetSha256 = datasetSha,
            PolicySha256 = initialSourceFingerprint, RegionSourceSha256 = FileHash(regionPath), Cases = cases
        };
        datasetBytes = JsonSerializer.SerializeToUtf8Bytes(Baseline, Json);
        CargoTrainingEventLog.WriteImmutable(Path.Combine(OutputDirectory, "dataset.json"), datasetBytes);
        var sourceRefs = SourcePaths.Select(p => new { path = p.Replace('\\', '/'), sha256 = FileHash(Path.Combine(this.repoRoot, p)) });
        sourceManifestBytes = JsonSerializer.SerializeToUtf8Bytes(new
        {
            schema = "cargo-training-provenance.v1", datasetSha256 = datasetSha, policySha256 = initialSourceFingerprint,
            regionSourceSha256 = Baseline.RegionSourceSha256, sources = sourceRefs,
            runtimeAssemblies = RuntimeAssemblies(),
            dataSource = "official_legal_dong_codes_and_synthetic_order_driver_conditions",
            spatialAdjacencyVerified = false, actualTravelTimeVerified = false, productionGuardsExecuted = false,
            source = "synthetic", planChecksExecuted = planReview,
            planChecks = planReview ? "existing_insertion_schedule_and_shared_segment_service_with_synthetic_provider" : null,
            weightsTrained = false, humanApprovals = 0, operationalDispatchEnabled = false
        }, Json);
        CargoTrainingEventLog.WriteImmutable(Path.Combine(OutputDirectory, "sources.json"), sourceManifestBytes);
    }

    public void VerifySources()
    {
        if (CurrentPolicyFingerprint() != initialSourceFingerprint) throw Error("SourceChanged");
        if (FileHash(archivePath) != archiveSha256) throw Error("RegionArchiveChanged");
        if (!File.ReadAllBytes(Path.Combine(OutputDirectory, "dataset.json")).AsSpan().SequenceEqual(datasetBytes)) throw Error("DatasetChanged");
        if (!File.ReadAllBytes(Path.Combine(OutputDirectory, "sources.json")).AsSpan().SequenceEqual(sourceManifestBytes)) throw Error("SourceManifestChanged");
    }

    private string CurrentPolicyFingerprint() => Hash(JsonSerializer.SerializeToUtf8Bytes(new
    {
        sourceFingerprint = planReview ? Hash(JsonSerializer.SerializeToUtf8Bytes(SourcePaths.Select(p => new { path = p, sha256 = FileHash(Path.Combine(repoRoot, p)) }), Json))
            : CargoTrainingCases.PolicyFingerprint(repoRoot), runtimeAssemblies = RuntimeAssemblies()
    }, Json));

    private static object[] RuntimeAssemblies() => new[]
    {
        typeof(CargoTrainingStore).Assembly, typeof(살뜰.Services.Dispatch.Coordination.국내화물배차조율Service).Assembly,
        typeof(CargoTrainingWorkspace).Assembly
    }.Select(a => (object)new { name = a.GetName().Name, sha256 = FileHash(a.Location) }).ToArray();
}
