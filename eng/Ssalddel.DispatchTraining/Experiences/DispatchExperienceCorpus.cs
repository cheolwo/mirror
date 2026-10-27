using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Ssalddel.DispatchTraining.Experiences;

/// <summary>명시적으로 지정한 가상 훈련 원문과 사람 검토만 로컬에 누적한다. 업무 API나 모델은 호출하지 않는다.</summary>
public sealed class DispatchExperienceCorpus
{
    private const int MaximumBytes = 8 * 1024 * 1024;
    private const string ManifestSchema = "dispatch-experience-snapshot.v1";
    private const string ReviewSchema = "dispatch-experience-review.v1";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly string root;

    public DispatchExperienceCorpus(string corpusRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(corpusRoot);
        root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(corpusRoot));
        RejectLinks(root);
    }

    public async Task<DispatchExperienceImportResult> ImportAsync(string sessionPath, CancellationToken cancellationToken = default)
    {
        var source = await ReadBytesAsync(Path.GetFullPath(sessionPath), cancellationToken);
        var state = ParseState(source);
        var sha = Hash(source);
        var projection = DispatchExperienceProjection.Project(state, sha);
        await using var lease = await AcquireAsync(cancellationToken);
        var existing = await LoadSnapshotsAsync(cancellationToken);
        await LoadReviewsAsync(existing, cancellationToken);
        var matching = existing.SingleOrDefault(x => x.Manifest.RunId == state.RunId && x.Manifest.Revision == state.Revision);
        if (matching is not null)
        {
            if (matching.Manifest.SourceSha256 != sha) throw Error("SourceRevisionConflict");
            return new(state.RunId!, state.Revision, sha, true, projection.Orders.Count);
        }
        ValidateRunLinks(existing.Where(x => x.Manifest.RunId == state.RunId).Select(x => x.Projection).Append(projection));
        var ordersBytes = JsonSerializer.SerializeToUtf8Bytes(projection, Json);
        var manifest = new SnapshotManifest(ManifestSchema, DispatchExperienceProjection.Version, "sha256/raw-source-bytes.v1",
            state.RunId!, state.Revision, sha, source.Length, Hash(ordersBytes), projection.Orders.Count, true, false, false, false);
        var parent = Path.Combine(root, "runs", DispatchExperienceProjection.RunKey(state.RunId!));
        CreateDirectory(parent);
        var destination = Path.Combine(parent, state.Revision.ToString(CultureInfo.InvariantCulture));
        RejectLinks(destination);
        var pending = Path.Combine(root, ".pending-" + Guid.NewGuid().ToString("N"));
        CreateDirectory(pending);
        try
        {
            await WriteNewAsync(Path.Combine(pending, "source.json"), source, cancellationToken);
            await WriteNewAsync(Path.Combine(pending, "orders.json"), ordersBytes, cancellationToken);
            await WriteNewAsync(Path.Combine(pending, "manifest.json"), JsonSerializer.SerializeToUtf8Bytes(manifest, Json), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            RejectLinks(destination);
            Directory.Move(pending, destination);
        }
        finally { DeletePending(pending); }
        return new(state.RunId!, state.Revision, sha, false, projection.Orders.Count);
    }

    public Task<DispatchExperienceSummary> ReadAsync(CancellationToken cancellationToken = default)
        => ReadAndPublishAsync(null, cancellationToken);

    /// <summary>조회와 그 판본의 로컬 보고서 게시를 같은 누적 기록 잠금 안에서 순서대로 수행한다.</summary>
    internal async Task<DispatchExperienceSummary> ReadAndPublishAsync(
        Func<DispatchExperienceSummary, CancellationToken, Task>? publish, CancellationToken cancellationToken = default)
    {
        await using var lease = await AcquireAsync(cancellationToken);
        var snapshots = await LoadSnapshotsAsync(cancellationToken);
        var reviews = await LoadReviewsAsync(snapshots, cancellationToken);
        var latest = snapshots.GroupBy(x => x.Manifest.RunId, StringComparer.Ordinal)
            .Select(x => x.MaxBy(s => s.Manifest.Revision)!).OrderBy(x => x.Manifest.RunId, StringComparer.Ordinal).ToArray();
        var orders = latest.SelectMany(x => x.Projection.Orders).Select(order =>
        {
            var review = reviews.LastOrDefault(x => x.Record.Command.ExperienceId == order.ExperienceId)?.Record;
            return review is null ? order : order with
            {
                ReviewRevision = review.Revision, ReviewOutcome = review.Command.Outcome,
                ReviewSnapshotSha256 = review.Command.SnapshotSha256,
                ReviewCurrent = review.Command.SnapshotSha256 == order.SnapshotSha256
            };
        }).ToArray();
        var summary = new DispatchExperienceSummary
        {
            SnapshotCount = snapshots.Count, RunCount = latest.Length, Orders = orders,
            TotalSlotCount = latest.Sum(x => x.Projection.TotalSlotCount),
            ScheduledSlotCount = latest.Sum(x => x.Projection.ScheduledSlotCount),
            SkippedSlotCount = latest.Sum(x => x.Projection.SkippedSlotCount),
            UnlinkedDecisionCount = latest.Sum(x => x.Projection.UnlinkedDecisionCount),
            UnlinkedIncidentCount = latest.Sum(x => x.Projection.UnlinkedIncidentCount),
            UnlinkedEventCount = latest.Sum(x => x.Projection.UnlinkedEventCount)
        };
        if (publish is not null) await publish(summary, cancellationToken);
        return summary;
    }

    public async Task<DispatchExperienceReviewResult> ReviewAsync(DispatchExperienceReviewCommand command,
        CancellationToken cancellationToken = default)
    {
        ValidateCommand(command);
        await using var lease = await AcquireAsync(cancellationToken);
        var snapshots = await LoadSnapshotsAsync(cancellationToken);
        var reviews = await LoadReviewsAsync(snapshots, cancellationToken);
        var payloadSha = Hash(JsonSerializer.SerializeToUtf8Bytes(command, Json));
        var previous = reviews.SingleOrDefault(x => x.Record.Command.RequestId == command.RequestId);
        if (previous is not null)
        {
            if (previous.Record.PayloadSha256 != payloadSha) throw Error("RequestConflict");
            return new(previous.Record.Revision, true);
        }
        var latest = snapshots.GroupBy(x => x.Manifest.RunId, StringComparer.Ordinal).Select(x => x.MaxBy(s => s.Manifest.Revision)!)
            .SelectMany(x => x.Projection.Orders).SingleOrDefault(x => x.ExperienceId == command.ExperienceId)
            ?? throw Error("ExperienceNotFound");
        if (latest.SnapshotSha256 != command.SnapshotSha256) throw Error("SnapshotChanged");
        var revision = reviews.LastOrDefault(x => x.Record.Command.ExperienceId == command.ExperienceId)?.Record.Revision ?? 0;
        if (revision != command.ExpectedReviewRevision) throw Error("RevisionConflict");
        var record = new ReviewRecord(ReviewSchema, reviews.Count + 1, reviews.LastOrDefault()?.Sha256,
            payloadSha, revision + 1, DateTimeOffset.UtcNow, command);
        var path = Path.Combine(root, "reviews", command.RequestId.ToString("N") + ".json");
        CreateDirectory(Path.GetDirectoryName(path)!);
        var pending = Path.Combine(root, ".pending-review-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            await WriteNewAsync(pending, JsonSerializer.SerializeToUtf8Bytes(record, Json), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            RejectLinks(path);
            File.Move(pending, path, overwrite: false);
        }
        finally { if (File.Exists(pending)) { RejectLinks(pending); File.Delete(pending); } }
        return new(revision + 1, false);
    }

    private async Task<List<LoadedSnapshot>> LoadSnapshotsAsync(CancellationToken ct)
    {
        var folder = Path.Combine(root, "runs");
        RejectLinks(folder);
        List<LoadedSnapshot> snapshots = [];
        if (!Directory.Exists(folder)) return snapshots;
        foreach (var run in Directory.EnumerateDirectories(folder).Order(StringComparer.Ordinal))
        {
            RejectLinks(run);
            foreach (var revision in Directory.EnumerateDirectories(run).Order(StringComparer.Ordinal))
            {
                ct.ThrowIfCancellationRequested();
                RejectLinks(revision);
                var manifest = Parse<SnapshotManifest>(await ReadBytesAsync(Path.Combine(revision, "manifest.json"), ct));
                if (manifest.Schema != ManifestSchema || manifest.ProjectionVersion != DispatchExperienceProjection.Version
                    || manifest.HashVersion != "sha256/raw-source-bytes.v1" || string.IsNullOrWhiteSpace(manifest.RunId)
                    || manifest.Revision < 0 || !manifest.Synthetic || manifest.TrainingEligible || manifest.WeightsTrained
                    || manifest.OperationalDispatchEnabled || !IsHash(manifest.SourceSha256) || !IsHash(manifest.OrdersSha256)
                    || Path.GetFileName(run) != DispatchExperienceProjection.RunKey(manifest.RunId)
                    || Path.GetFileName(revision) != manifest.Revision.ToString(CultureInfo.InvariantCulture))
                    throw Error("SnapshotManifestInvalid");
                var source = await ReadBytesAsync(Path.Combine(revision, "source.json"), ct);
                if (source.Length != manifest.SourceBytes || Hash(source) != manifest.SourceSha256) throw Error("SourceCorrupt");
                var state = ParseState(source);
                if (state.RunId != manifest.RunId || state.Revision != manifest.Revision) throw Error("SnapshotIdentityChanged");
                var projection = DispatchExperienceProjection.Project(state, manifest.SourceSha256);
                var bytes = await ReadBytesAsync(Path.Combine(revision, "orders.json"), ct, MaximumBytes * 2);
                if (Hash(bytes) != manifest.OrdersSha256 || projection.Orders.Count != manifest.OrderCount
                    || !bytes.AsSpan().SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(projection, Json)))
                    throw Error("ProjectionCorrupt");
                snapshots.Add(new(manifest, projection));
            }
        }
        if (snapshots.GroupBy(x => (x.Manifest.RunId, x.Manifest.Revision)).Any(x => x.Count() != 1)) throw Error("DuplicateSnapshot");
        foreach (var run in snapshots.GroupBy(x => x.Manifest.RunId, StringComparer.Ordinal)) ValidateRunLinks(run.Select(x => x.Projection));
        return snapshots;
    }

    private async Task<List<LoadedReview>> LoadReviewsAsync(IReadOnlyList<LoadedSnapshot> snapshots, CancellationToken ct)
    {
        var folder = Path.Combine(root, "reviews");
        RejectLinks(folder);
        List<LoadedReview> rows = [];
        if (!Directory.Exists(folder)) return rows;
        foreach (var path in Directory.EnumerateFiles(folder))
        {
            var bytes = await ReadBytesAsync(path, ct);
            var record = Parse<ReviewRecord>(bytes);
            ValidateCommand(record.Command);
            if (record.Schema != ReviewSchema || record.Sequence < 1 || record.Revision < 1
                || record.PayloadSha256 != Hash(JsonSerializer.SerializeToUtf8Bytes(record.Command, Json))
                || Path.GetFileName(path) != record.Command.RequestId.ToString("N") + ".json") throw Error("ReviewCorrupt");
            rows.Add(new(record, Hash(bytes)));
        }
        rows = rows.OrderBy(x => x.Record.Sequence).ToList();
        Dictionary<string, long> revisions = new(StringComparer.Ordinal);
        HashSet<Guid> requests = [];
        string? previous = null;
        for (var i = 0; i < rows.Count; i++)
        {
            var item = rows[i]; var record = item.Record; var command = record.Command;
            var expected = revisions.GetValueOrDefault(command.ExperienceId);
            if (record.Sequence != i + 1 || record.PreviousSha256 != previous || record.Revision != expected + 1
                || command.ExpectedReviewRevision != expected || !requests.Add(command.RequestId)
                || !snapshots.Any(s => s.Manifest.SourceSha256 == command.SnapshotSha256
                    && s.Projection.Orders.Any(o => o.ExperienceId == command.ExperienceId))) throw Error("ReviewCorrupt");
            revisions[command.ExperienceId] = record.Revision; previous = item.Sha256;
        }
        return rows;
    }

    private static void ValidateRunLinks(IEnumerable<DispatchExperienceSnapshot> snapshots)
    {
        var orders = snapshots.SelectMany(x => x.Orders).ToArray();
        foreach (var group in orders.GroupBy(x => x.OrderIndex))
            if (group.Select(x => x.ClientRequestId).Distinct().Count() != 1
                || group.Select(x => x.CanonicalOrderId).Where(x => x is not null).Distinct(StringComparer.Ordinal).Count() > 1)
                throw Error("RunOrderLinkChanged");
        foreach (var group in orders.Where(x => x.CanonicalOrderId is not null).GroupBy(x => x.CanonicalOrderId, StringComparer.Ordinal))
            if (group.Select(x => x.OrderIndex).Distinct().Count() != 1) throw Error("DuplicateCanonicalOrder");
    }

    private static void ValidateCommand(DispatchExperienceReviewCommand command)
    {
        if (command is null || command.RequestId == Guid.Empty || command.ExperienceId is null
            || !command.ExperienceId.StartsWith("exp-", StringComparison.Ordinal) || !IsHash(command.ExperienceId[4..])
            || !IsHash(command.SnapshotSha256) || command.ExpectedReviewRevision < 0
            || command.Outcome is not ("approve" or "correct" or "defer") || command.Note?.Length > 1000)
            throw Error("InvalidReviewCommand");
    }

    private async Task<FileStream> AcquireAsync(CancellationToken ct)
    {
        CreateDirectory(root);
        var path = Path.Combine(root, ".corpus.lock");
        for (var attempt = 0; ; attempt++)
        {
            ct.ThrowIfCancellationRequested(); RejectLinks(path);
            try { return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException) when (attempt < 80) { await Task.Delay(25, ct); }
            catch (IOException) { throw Error("CorpusBusy"); }
        }
    }

    private static DispatchTrainingSessionState ParseState(byte[] bytes)
    {
        var state = Parse<DispatchTrainingSessionState>(bytes);
        try { DispatchTrainingSessionStore.Validate(state); }
        catch (Exception e) when (e is InvalidDataException or NullReferenceException or ArgumentException)
        { throw Error("SessionInvalid"); }
        if (state.LastObservation.Orders is null || state.LastObservation.Orders.Any(x => x is null)
            || state.Decisions.Any(x => x is null || x.Candidates is null)
            || state.Incidents.Any(x => x is null) || state.Events.Any(x => x is null)) throw Error("SessionInvalid");
        return state;
    }

    private static T Parse<T>(byte[] bytes)
    {
        try
        {
            using var doc = JsonDocument.Parse(bytes);
            CheckDuplicateProperties(doc.RootElement);
            return JsonSerializer.Deserialize<T>(bytes, Json) ?? throw Error("JsonInvalid");
        }
        catch (JsonException) { throw Error("JsonInvalid"); }
    }

    private static void CheckDuplicateProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw Error("DuplicateJsonProperty");
                CheckDuplicateProperties(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var item in element.EnumerateArray()) CheckDuplicateProperties(item);
    }

    private static async Task<byte[]> ReadBytesAsync(string path, CancellationToken ct, int maximum = MaximumBytes)
    {
        RejectLinks(path);
        await using var file = new FileStream(path, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete, 16384, FileOptions.Asynchronous);
        if (file.Length > maximum) throw Error("InputTooLarge");
        using var buffer = new MemoryStream();
        var chunk = new byte[16384];
        while (true)
        {
            var read = await file.ReadAsync(chunk, ct);
            if (read == 0) break;
            if (buffer.Length + read > maximum) throw Error("InputTooLarge");
            buffer.Write(chunk, 0, read);
        }
        return buffer.ToArray();
    }

    private static async Task WriteNewAsync(string path, byte[] bytes, CancellationToken ct)
    {
        RejectLinks(path);
        await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None,
            16384, FileOptions.Asynchronous | FileOptions.WriteThrough);
        await file.WriteAsync(bytes, ct); await file.FlushAsync(ct); file.Flush(true);
    }
    private static bool IsHash(string? value) => value is { Length: 64 } && value.All(x => x is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static string Hash(byte[] bytes) => DispatchExperienceProjection.Hash(bytes);
    private static DispatchExperienceException Error(string code) => DispatchExperienceProjection.Error(code);
    private static void CreateDirectory(string path) { RejectLinks(path); Directory.CreateDirectory(path); RejectLinks(path); }
    private static void RejectLinks(string path)
    {
        for (var current = Path.GetFullPath(path); !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
            if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw Error("LinkedPathRejected");
    }
    private void DeletePending(string path)
    {
        if (!Directory.Exists(path)) return;
        if (Path.GetDirectoryName(Path.GetFullPath(path)) != root || !Path.GetFileName(path).StartsWith(".pending-", StringComparison.Ordinal))
            throw Error("PendingPathInvalid");
        RejectLinks(path);
        foreach (var item in Directory.EnumerateFiles(path)) { RejectLinks(item); File.Delete(item); }
        Directory.Delete(path, recursive: false);
    }

    private sealed record SnapshotManifest(string Schema, string ProjectionVersion, string HashVersion, string RunId,
        long Revision, string SourceSha256, long SourceBytes, string OrdersSha256, int OrderCount,
        bool Synthetic, bool TrainingEligible, bool WeightsTrained, bool OperationalDispatchEnabled);
    private sealed record LoadedSnapshot(SnapshotManifest Manifest, DispatchExperienceSnapshot Projection);
    private sealed record ReviewRecord(string Schema, long Sequence, string? PreviousSha256, string PayloadSha256,
        long Revision, DateTimeOffset RecordedAtUtc, DispatchExperienceReviewCommand Command);
    private sealed record LoadedReview(ReviewRecord Record, string Sha256);
}
