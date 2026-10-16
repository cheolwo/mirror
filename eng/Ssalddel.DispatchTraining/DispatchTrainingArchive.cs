using System.Security.Cryptography;
using System.Text.Json;
using System.Net.NetworkInformation;
using Ssalddel.Contracts.Admin.DispatchTraining;

namespace Ssalddel.DispatchTraining;

/// <summary>명시적인 로컬 CLI에서만 호출하는 훈련 기록 보존입니다. DB·업무·배차를 취소하거나 변경하지 않습니다.</summary>
public static class DispatchTrainingArchive
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    /// <summary>호스트는 세션 조회 또는 서버 기동 전에 취득하고 프로세스가 끝날 때까지 유지합니다.</summary>
    public static DispatchTrainingHostLease AcquireHostLease(string sessionJournalPath)
    {
        var store = new DispatchTrainingSessionStore(sessionJournalPath);
        var directory = Path.GetDirectoryName(store.JournalPath)!;
        Directory.CreateDirectory(directory);
        RejectLinks(directory);
        var leasePath = store.JournalPath + ".host.lock";
        RejectLinks(leasePath);
        try
        {
            // 파일의 존재가 아니라 열린 배타 핸들이 소유권입니다. 종료된 프로세스의 파일은 재사용합니다.
            return new(new FileStream(leasePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None));
        }
        catch (IOException error)
        {
            throw new IOException("DispatchTrainingHostLeaseUnavailable: 실행 중인 훈련 호스트를 먼저 종료하거나 기록 경로를 확인해 주세요.", error);
        }
    }

    /// <summary>관련 파일은 같은 폴더의 JSON만 보존합니다. 원문 복사·해시 대조 뒤 세션 제어 기록만 새 Idle로 교체합니다.</summary>
    public static async Task<DispatchTrainingArchiveResult> ArchiveAsync(string sessionJournalPath,
        IReadOnlyList<string>? relatedJournalPaths = null, CancellationToken ct = default, int hostPort = 5324)
    {
        RejectListeningHost(hostPort);
        var store = new DispatchTrainingSessionStore(sessionJournalPath);
        using var lease = AcquireHostLease(store.JournalPath);
        RejectListeningHost(hostPort);
        var directory = Path.GetDirectoryName(store.JournalPath)!;
        if (!File.Exists(store.JournalPath)) throw new FileNotFoundException("보존할 훈련 세션 기록이 없습니다.", store.JournalPath);
        var sources = new List<string> { store.JournalPath };
        foreach (var candidate in relatedJournalPaths ?? [])
        {
            var source = Path.GetFullPath(candidate);
            if (!string.Equals(Path.GetDirectoryName(source), directory, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(Path.GetExtension(source), ".json", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("관련 훈련 기록은 세션과 같은 폴더의 JSON 파일이어야 합니다.", nameof(relatedJournalPaths));
            RejectLinks(source);
            if (!sources.Contains(source, StringComparer.OrdinalIgnoreCase)) sources.Add(source);
        }
        if (sources.Count > 8) throw new ArgumentException("보존할 훈련 기록 파일이 너무 많습니다.", nameof(relatedJournalPaths));
        if (sources.Any(x => string.Equals(Path.GetFileName(x), "manifest.json", StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("manifest.json은 보존 보고서 전용 이름입니다.", nameof(relatedJournalPaths));

        var id = $"{DateTime.UtcNow:yyyyMMddTHHmmssfffZ}-{Guid.NewGuid():N}";
        var archiveDirectory = Path.Combine(directory, "archive", id);
        RejectLinks(archiveDirectory);
        Directory.CreateDirectory(archiveDirectory);
        RejectLinks(archiveDirectory);
        List<DispatchTrainingArchiveFile> files = [];
        List<string> missing = [];
        foreach (var source in sources)
        {
            ct.ThrowIfCancellationRequested();
            if (!File.Exists(source)) { missing.Add(Path.GetFileName(source)); continue; }
            files.Add(await PreserveAsync(source, Path.Combine(archiveDirectory, Path.GetFileName(source)), ct));
        }
        var manifestPath = Path.Combine(archiveDirectory, "manifest.json");
        var manifest = new DispatchTrainingArchiveManifest
        {
            ArchiveId = id, ArchivedAtUtc = DateTime.UtcNow, Files = files, MissingRelatedFileNames = missing,
            ResetStatusCode = "OriginalsPreserved"
        };
        await WriteManifestAsync(manifestPath, manifest, ct);
        ct.ThrowIfCancellationRequested();

        var idle = new DispatchTrainingSessionState
        {
            Revision = 1, EventSequence = 1, PhaseCode = DispatchTrainingPhaseCodes.Idle,
            LastObservation = new() { CanonicalReadSucceeded = false },
            Events = [new(1, DateTime.UtcNow, "PreviousSessionArchived", DetailCode: id)]
        };
        await store.SaveAsync(idle, ct);
        // 교체 뒤 보고서 쓰기가 실패해도 보존 원문과 실제 session 재조회로 결과를 확인할 수 있습니다.
        manifest = manifest with { ResetStatusCode = "IdleConfirmed", IdleRevision = idle.Revision };
        await WriteManifestAsync(manifestPath, manifest, CancellationToken.None);
        return new(id, archiveDirectory, files, missing, DispatchTrainingPhaseCodes.Idle);
    }

    private static async Task<DispatchTrainingArchiveFile> PreserveAsync(string source, string target, CancellationToken ct)
    {
        RejectLinks(source);
        RejectLinks(target);
        await using var original = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read);
        await using (var backup = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None,
            16384, FileOptions.WriteThrough))
        {
            await original.CopyToAsync(backup, ct);
            await backup.FlushAsync(ct);
            backup.Flush(flushToDisk: true);
        }
        original.Position = 0;
        var sourceHash = Convert.ToHexString(await SHA256.HashDataAsync(original, ct));
        await using var preserved = new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.Read);
        var backupHash = Convert.ToHexString(await SHA256.HashDataAsync(preserved, ct));
        if (!string.Equals(sourceHash, backupHash, StringComparison.Ordinal) || original.Length != preserved.Length)
            throw new IOException("훈련 기록 보존 사본이 원문과 다릅니다. 세션을 초기화하지 않았습니다.");
        return new(Path.GetFileName(source), sourceHash, original.Length);
    }

    private static async Task WriteManifestAsync(string path, DispatchTrainingArchiveManifest manifest, CancellationToken ct)
    {
        RejectLinks(path);
        var temporary = path + $".{Guid.NewGuid():N}.tmp";
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 16384, FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, manifest, Json, ct);
                await stream.FlushAsync(ct);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, path, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static void RejectLinks(string path)
    {
        for (var current = path; !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
            if ((File.Exists(current) || Directory.Exists(current))
                && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("훈련 기록 보존 경로의 링크 또는 junction은 사용할 수 없습니다.");
    }

    private static void RejectListeningHost(int port)
    {
        // 0은 외부 호스트를 사용하지 않는 자체 검증에서만 전달합니다. CLI는 기본 5324를 유지합니다.
        if (port == 0) return;
        ArgumentOutOfRangeException.ThrowIfNegative(port);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(port, 65535);
        if (IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners().Any(x => x.Port == port))
            throw new IOException("DispatchTrainingHostIsListening: 실행 중인 훈련 호스트를 먼저 종료해 주세요.");
    }

    private sealed record DispatchTrainingArchiveManifest
    {
        public int SchemaVersion { get; init; } = 1;
        public string ArchiveId { get; init; } = "";
        public DateTime ArchivedAtUtc { get; init; }
        public IReadOnlyList<DispatchTrainingArchiveFile> Files { get; init; } = [];
        public IReadOnlyList<string> MissingRelatedFileNames { get; init; } = [];
        public string ResetStatusCode { get; init; } = "OriginalsPreserved";
        public long? IdleRevision { get; init; }
        public bool DatabaseMutated => false;
        public bool OrdersCancelled => false;
        public bool PreviousWorkReconciled => false;
    }
}

public sealed class DispatchTrainingHostLease : IDisposable
{
    private FileStream? _stream;
    internal DispatchTrainingHostLease(FileStream stream) => _stream = stream;
    public void Dispose() => Interlocked.Exchange(ref _stream, null)?.Dispose();
}

public sealed record DispatchTrainingArchiveFile(string FileName, string Sha256, long Length);
public sealed record DispatchTrainingArchiveResult(string ArchiveId, string ArchiveDirectory,
    IReadOnlyList<DispatchTrainingArchiveFile> Files, IReadOnlyList<string> MissingRelatedFileNames, string NewPhaseCode)
{
    public bool DatabaseMutated => false;
    public bool OrdersCancelled => false;
    public bool PreviousWorkReconciled => false;
}
