using System.Text.Json;
using System.Globalization;
using System.Security.Cryptography;
using Ssalddel.Contracts.Admin.DispatchTraining;

namespace Ssalddel.DispatchTraining;

/// <summary>훈련 제어와 요청 식별자만 보관합니다. 운송 업무의 상태 정본은 기존 API입니다.</summary>
public sealed class DispatchTrainingSessionStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly string _path;
    public string JournalPath => _path;

    public DispatchTrainingSessionStore(string journalPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(journalPath);
        _path = Path.GetFullPath(journalPath);
        if (!string.Equals(Path.GetExtension(_path), ".json", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("훈련 기록은 JSON 파일이어야 합니다.", nameof(journalPath));
        RejectLinks(_path);
    }

    public async Task<DispatchTrainingSessionState?> LoadAsync(CancellationToken ct = default)
    {
        RejectLinks(_path);
        if (!File.Exists(_path)) return null;
        await using var stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var state = await JsonSerializer.DeserializeAsync<DispatchTrainingSessionState>(stream, Json, ct)
            ?? throw new InvalidDataException("훈련 기록이 비어 있습니다. 기존 기록을 초기화하지 않았습니다.");
        Validate(state);
        return state;
    }

    public async Task SaveAsync(DispatchTrainingSessionState state, CancellationToken ct = default)
    {
        Validate(state);
        RejectLinks(_path);
        var directory = Path.GetDirectoryName(_path)!;
        Directory.CreateDirectory(directory);
        RejectLinks(_path);
        var temporary = Path.Combine(directory, $".{Path.GetFileName(_path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 16384, FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, state, Json, ct);
                await stream.FlushAsync(ct);
                stream.Flush(flushToDisk: true);
            }
            ct.ThrowIfCancellationRequested();
            RejectLinks(_path);
            File.Move(temporary, _path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    /// <summary>새 회차 진입 전에 디스크의 Completed 원문을 동결합니다. CLI archive·업무 상태 변경과 별개입니다.</summary>
    public async Task<DispatchTrainingCompletedRunSnapshot> PreserveCompletedRunAsync(string runId,
        long expectedRevision, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(runId) || runId.Length > 128
            || runId.Any(x => !char.IsAsciiLetterOrDigit(x) && x is not ('-' or '_')) || expectedRevision < 0)
            throw new InvalidDataException("완료 회차의 안전한 식별자 또는 revision이 필요합니다.");
        RejectLinks(_path);
        await using var original = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var state = await JsonSerializer.DeserializeAsync<DispatchTrainingSessionState>(original, Json, ct)
            ?? throw new InvalidDataException("완료 회차 기록이 비어 있습니다.");
        Validate(state);
        if (state.PhaseCode != DispatchTrainingPhaseCodes.Completed || state.RunId != runId || state.Revision != expectedRevision)
            throw new InvalidDataException("디스크의 완료 회차와 시작 요청이 다릅니다. 기존 상태를 유지합니다.");
        original.Position = 0;
        var digest = Convert.ToHexString(await SHA256.HashDataAsync(original, ct));
        var target = Path.Combine(Path.GetDirectoryName(_path)!, "completed-runs", runId,
            expectedRevision.ToString(CultureInfo.InvariantCulture) + ".json");
        RejectLinks(target);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        RejectLinks(target);
        if (File.Exists(target))
        {
            await VerifyAsync(target);
            return new(runId, expectedRevision, target, digest, Reused: true);
        }
        var temporary = target + $".{Guid.NewGuid():N}.tmp";
        try
        {
            original.Position = 0;
            await using (var preserved = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 16384, FileOptions.WriteThrough))
            {
                await original.CopyToAsync(preserved, ct);
                await preserved.FlushAsync(ct);
                preserved.Flush(flushToDisk: true);
            }
            await VerifyAsync(temporary);
            ct.ThrowIfCancellationRequested();
            RejectLinks(target);
            var reused = false;
            try { File.Move(temporary, target, overwrite: false); }
            catch (IOException) when (File.Exists(target))
            {
                await VerifyAsync(target);
                reused = true;
            }
            await VerifyAsync(target);
            return new(runId, expectedRevision, target, digest, reused);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }

        async Task VerifyAsync(string path)
        {
            RejectLinks(path);
            await using var saved = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var hash = Convert.ToHexString(await SHA256.HashDataAsync(saved, ct));
            if (saved.Length != original.Length || !string.Equals(hash, digest, StringComparison.Ordinal))
                throw new InvalidDataException("완료 회차 동결 파일이 원문과 다릅니다. 덮어쓰거나 새 회차를 시작하지 않았습니다.");
        }
    }

    private static void RejectLinks(string path)
    {
        for (var current = path; !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
        {
            if ((File.Exists(current) || Directory.Exists(current))
                && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("훈련 기록 경로의 링크 또는 junction은 사용할 수 없습니다.");
        }
    }

    internal static void Validate(DispatchTrainingSessionState state)
    {
        if (state.SchemaVersion != 1 || state.Revision < 0 || state.EventSequence < 0)
            throw new InvalidDataException("지원하지 않거나 손상된 훈련 기록입니다. 자동 초기화하지 않았습니다.");
        if (state.PhaseCode is not (DispatchTrainingPhaseCodes.Idle or DispatchTrainingPhaseCodes.Running
            or DispatchTrainingPhaseCodes.Paused or DispatchTrainingPhaseCodes.Draining
            or DispatchTrainingPhaseCodes.Completed or DispatchTrainingPhaseCodes.Failed))
            throw new InvalidDataException("훈련 단계가 유효하지 않습니다.");
        if (state.Slots is null || state.Commands is null || state.Events is null || state.Incidents is null
            || state.Decisions is null || state.LastObservation is null)
            throw new InvalidDataException("훈련 기록에 필수 항목이 없습니다.");
        if (state.RunId is not null && (state.StartedAtUtc is null || state.GenerationEndsAtUtc is null
            || state.Slots.Count != DispatchTrainingScenario.MaximumOrders))
            throw new InvalidDataException("훈련 일정이 손상되었습니다.");
        if (state.Slots.Select(x => x.Index).Distinct().Count() != state.Slots.Count
            || state.Slots.Any(x => x.Index < 0 || x.Index >= DispatchTrainingScenario.MaximumOrders || x.ClientRequestId == Guid.Empty))
            throw new InvalidDataException("훈련 주문 식별자가 손상되었습니다.");
        if (state.RunId is null && (state.PhaseCode != DispatchTrainingPhaseCodes.Idle || state.Slots.Count != 0)
            || state.RunId is not null && (string.IsNullOrWhiteSpace(state.RunId)
                || state.GenerationEndsAtUtc != state.StartedAtUtc!.Value.AddSeconds(DispatchTrainingScenario.DurationSeconds)
                || state.Slots.Any(x => x.DueAtUtc != DispatchTrainingScenario.DueAt(state.StartedAtUtc.Value, x.Index)
                    || x.ClientRequestId != DispatchTrainingScenario.OrderRequestId(state.RunId, x.Index))))
            throw new InvalidDataException("훈련 회차와 일정의 연결이 손상되었습니다.");
        if (state.Slots.Any(x => x.StatusCode is not ("Scheduled" or "Submitting" or "Confirmed" or "Unknown" or "FailedReview" or "Skipped")
            || x.StatusCode == "Confirmed" && string.IsNullOrWhiteSpace(x.CanonicalOrderId)))
            throw new InvalidDataException("훈련 접수 결과가 손상되었습니다.");
        if (state.Commands.Select(x => x.ClientRequestId).Distinct().Count() != state.Commands.Count
            || state.Commands.Any(x => x.ClientRequestId == Guid.Empty))
            throw new InvalidDataException("훈련 제어 요청 식별자가 손상되었습니다.");
        if (state.Events.Zip(state.Events.Skip(1), (a, b) => b.Sequence > a.Sequence).Any(x => !x)
            || (state.Events.Count > 0 && state.Events[^1].Sequence != state.EventSequence))
            throw new InvalidDataException("훈련 사건 순번이 손상되었습니다.");
    }
}

public sealed class DispatchTrainingSessionState
{
    public int SchemaVersion { get; set; } = 1;
    public string? RunId { get; set; }
    public long Revision { get; set; }
    public long EventSequence { get; set; }
    public string PhaseCode { get; set; } = DispatchTrainingPhaseCodes.Idle;
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? GenerationEndsAtUtc { get; set; }
    public List<DispatchTrainingOrderSlot> Slots { get; set; } = [];
    public List<DispatchTrainingCommandReceipt> Commands { get; set; } = [];
    public List<DispatchTrainingJournalEvent> Events { get; set; } = [];
    public List<DispatchTrainingIncidentDto> Incidents { get; set; } = [];
    public List<DispatchTrainingDecisionDto> Decisions { get; set; } = [];
    public DispatchTrainingTickResult LastObservation { get; set; } = new();
}

public sealed class DispatchTrainingOrderSlot
{
    public int Index { get; set; }
    public DateTime DueAtUtc { get; set; }
    public Guid ClientRequestId { get; set; }
    public string StatusCode { get; set; } = "Scheduled";
    public string? CanonicalOrderId { get; set; }
    public string? LastErrorCode { get; set; }
}

public sealed class DispatchTrainingCommandReceipt
{
    public Guid ClientRequestId { get; set; }
    public string Fingerprint { get; set; } = "";
    public string CommandCode { get; set; } = "";
    public string? RunId { get; set; }
    public string OutcomeCode { get; set; } = "Pending";
    public int SupportTargetCount { get; set; }
}

public sealed record DispatchTrainingJournalEvent(long Sequence, DateTime AtUtc, string KindCode,
    int? OrderIndex = null, Guid? ClientRequestId = null, string? DetailCode = null);

public sealed record DispatchTrainingCompletedRunSnapshot(string RunId, long Revision, string Path, string Sha256, bool Reused);
