using System.Text;
using System.Text.Json;
using Ssalddel.Contracts.Admin.DispatchTraining;
using static CargoRegionalTrainingReview.CargoTrainingStore;

namespace CargoRegionalTrainingReview;

/// <summary>검토 이력의 파일 입출력과 인스턴스 간 잠금·영속화를 담당한다.</summary>
internal sealed class CargoTrainingEventLog(string outputDirectory)
{
    public List<CargoTrainingEvent> Read()
    {
        var path = Path.Combine(outputDirectory, "review-events.jsonl");
        if (!File.Exists(path)) return [];
        try
        {
            return File.ReadLines(path, Encoding.UTF8).Select(line => JsonSerializer.Deserialize<CargoTrainingEvent>(line, Json) ?? throw Error("HistoryCorrupt")).ToList();
        }
        catch (JsonException) { throw Error("HistoryCorrupt"); }
    }

    public void Append(CargoTrainingEvent item)
    {
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(item, Json) + "\n");
        using var file = new FileStream(Path.Combine(outputDirectory, "review-events.jsonl"), FileMode.Append, FileAccess.Write, FileShare.Read);
        file.Write(bytes); file.Flush(flushToDisk: true);
    }

    public async Task<FileStream> AcquireAsync(CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try { return new FileStream(Path.Combine(outputDirectory, ".review.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException) when (attempt < 40) { await Task.Delay(25, ct); }
            catch (IOException) { throw Error("ReviewBusy"); }
        }
    }

    public static void WriteImmutable(string path, byte[] bytes)
    {
        try { using var f = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read); f.Write(bytes); f.Flush(true); }
        catch (IOException) when (File.Exists(path))
        {
            if (!File.ReadAllBytes(path).AsSpan().SequenceEqual(bytes)) throw Error("ImmutableArtifactChanged");
        }
    }
}

// 속성 순서와 이름은 기존 review-events.jsonl 직렬화 계약을 유지한다.
internal sealed record CargoTrainingEvent(string Kind, string DatasetSha256, string CaseId, string InputSha256,
    string? CommandSha256, CargoTrainingReview? Review, CargoTrainingDecision? Decision);
