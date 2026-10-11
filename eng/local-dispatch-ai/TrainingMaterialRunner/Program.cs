using System.Text;
using System.Text.Json;

if (args.Length != 3 || args[0] != "--prepare" || args[1] != "--dataset" || !Path.IsPathFullyQualified(args[2]))
{
    Console.WriteLine("No data or model execution. Use --prepare --dataset <absolute r50 dataset.json path>.");
    return;
}

try
{
    var sources = TrainingSources.Open(args[2]);
    var cases = await new TrainingCases(sources.Records).GenerateAsync();
    if (cases.Count != 4041 || cases.Select(x => x.caseId).Distinct(StringComparer.Ordinal).Count() != cases.Count)
        throw new InvalidOperationException("TrainingCaseCoverageMismatch");
    sources.VerifyUnchanged();
    var raw = Encoding.UTF8.GetBytes(string.Join('\n', cases.Select(x => JsonSerializer.Serialize(x))) + "\n");
    var manifest = new
    {
        schemaVersion = 1, formatVersion = TrainingSources.FormatVersion,
        datasetSha256 = sources.DatasetSha256, sourceHashes = sources.Hashes,
        rawSha256 = TrainingSources.Hash(raw), caseCount = cases.Count,
        modelCalls = 0, weightsTrained = false, humanApprovals = 0,
        generatorFingerprint = sources.Fingerprint, designSha256 = TrainingSources.DesignSha256,
        sourceHashesUnchanged = true, splitRule = "sha256-branch-first8-u32-mod100-70-15-15", fixedSeed = 51
    };
    var output = sources.OutputDirectory;
    TrainingSources.WriteImmutable(Path.Combine(output, "raw.jsonl"), raw);
    TrainingSources.WriteImmutable(Path.Combine(output, "raw.manifest.json"),
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }) + "\n"));
    sources.VerifyUnchanged();
    Console.WriteLine(JsonSerializer.Serialize(new { output, rawCases = cases.Count, modelCalls = 0,
        humanApprovals = 0, weightsTrained = false, eligibleForTraining = 0 }));
}
catch (Exception ex)
{
    // Paths, source contents and customer fields are never echoed on a preparation failure.
    var code = System.Text.RegularExpressions.Regex.IsMatch(ex.Message, "^[A-Za-z][A-Za-z0-9]{0,100}$") ? ex.Message : "UnclassifiedFailure";
    Console.Error.WriteLine(JsonSerializer.Serialize(new { error = "TrainingPreparationFailed", type = ex.GetType().Name, code }));
    Environment.ExitCode = 1;
}
