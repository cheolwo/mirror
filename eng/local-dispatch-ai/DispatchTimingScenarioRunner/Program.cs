using System.Text;
using System.Text.Json;

if (args.Length != 3 || args[0] != "--prepare" || args[1] != "--dataset" || !Path.IsPathFullyQualified(args[2]))
{
    Console.WriteLine("No data, dispatch or model execution. Use --prepare --dataset <absolute r50 dataset.json path>.");
    return;
}

try
{
    var sources = TimingSources.Open(args[2]);
    var rows = TimingCases.Generate(sources.Records);
    if (rows.Count != 4032 || rows.Select(x => x.caseId).Distinct(StringComparer.Ordinal).Count() != rows.Count)
        throw new InvalidOperationException("TimingCaseCoverageMismatch");
    sources.VerifyUnchanged();
    var raw = Encoding.UTF8.GetBytes(string.Join('\n', rows.Select(x => JsonSerializer.Serialize(x))) + "\n");
    var manifest = new
    {
        schemaVersion = 1, formatVersion = TimingSources.FormatVersion,
        datasetSha256 = sources.DatasetSha256, sourceHashes = sources.Hashes,
        rawSha256 = TimingSources.Hash(raw), caseCount = rows.Count,
        modelCalls = 0, weightsTrained = false, humanApprovals = 0, operationalWrites = 0,
        generatorFingerprint = sources.Fingerprint, designSha256 = TimingSources.DesignSha256,
        sourceHashesUnchanged = true, splitRule = TimingSources.SplitRule, fixedSeed = 52
    };
    TimingSources.WriteImmutable(Path.Combine(sources.OutputDirectory, "raw.jsonl"), raw);
    TimingSources.WriteImmutable(Path.Combine(sources.OutputDirectory, "raw.manifest.json"),
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }) + "\n"));
    sources.VerifyUnchanged();
    Console.WriteLine(JsonSerializer.Serialize(new { output = sources.OutputDirectory, rawCases = rows.Count,
        modelCalls = 0, weightsTrained = false, humanApprovals = 0, operationalWrites = 0, eligibleForTraining = 0 }));
}
catch (Exception ex)
{
    var code = System.Text.RegularExpressions.Regex.IsMatch(ex.Message, "^[A-Za-z][A-Za-z0-9]{0,100}$")
        ? ex.Message : "UnclassifiedFailure";
    Console.Error.WriteLine(JsonSerializer.Serialize(new { error = "TimingPreparationFailed", type = ex.GetType().Name, code }));
    Environment.ExitCode = 1;
}
