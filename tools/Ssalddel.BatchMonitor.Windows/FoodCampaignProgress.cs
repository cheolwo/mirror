using System.IO;
using System.Text.Json;

namespace Ssalddel.BatchMonitor.Windows;

internal sealed class FoodCampaignProgress
{
    private const int SchemaVersion = 1;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly string path;
    private CampaignFile progress = new(SchemaVersion, [], new(StringComparer.Ordinal));

    private FoodCampaignProgress(string path) => this.path = Path.GetFullPath(path);

    public string? Warning { get; private set; }
    public string? LastStartedStageId => progress.LastStartedStageId;
    public IReadOnlyList<FoodCampaignStage> UnlockedStages => [FoodCampaignStage.All[0], .. progress.ClearedStageIds
        .SelectMany(id => FoodCampaignStage.NextOptions(FoodCampaignStage.Find(id)!))
        .DistinctBy(stage => stage.Id)];

    public static FoodCampaignProgress Load(string? path = null)
    {
        var directory = Environment.GetEnvironmentVariable("SSALDDEL_BATCH_MONITOR_CAMPAIGN_DIRECTORY");
        if (string.IsNullOrWhiteSpace(directory))
        {
            directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Ssalddel", "BatchMonitor");
        }

        var profile = new FoodCampaignProgress(path ?? Path.Combine(directory, "food-campaign-v1.json"));
        try
        {
            profile.progress = profile.Read();
        }
        catch (Exception error) when (IsStorageError(error))
        {
            profile.Warning = "스테이지 기록을 읽지 못했습니다. 기존 저장 파일을 확인해 주세요.";
        }

        return profile;
    }

    public bool IsUnlocked(FoodCampaignStage stage) => IsUnlocked(stage.Id, progress);

    public bool IsCleared(FoodCampaignStage stage) => progress.ClearedStageIds.Contains(stage.Id, StringComparer.Ordinal);

    public bool RecordStageStart(FoodCampaignStage stage) =>
        Update(stage.Id, latest => latest with { LastStartedStageId = stage.Id });

    public bool RecordResult(FoodManagementRound round)
    {
        if (round.CampaignStage is not { } stage ||
            round.CampaignResult is not { } result ||
            round.CampaignReplay() is not { } replay)
            return false;

        return Update(stage.Id, latest =>
        {
            if (result.Cleared && !latest.ClearedStageIds.Contains(stage.Id, StringComparer.Ordinal))
                latest.ClearedStageIds.Add(stage.Id);

            latest.Replays[stage.Id] = JsonSerializer.SerializeToElement(replay, JsonOptions);
            return latest;
        });
    }

    private bool Update(string stageId, Func<CampaignFile, CampaignFile> update)
    {
        var temporary = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var fileLock = new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
            var latest = Read();
            if (!IsUnlocked(stageId, latest))
            {
                Warning = "이 스테이지의 이전 통과 기록을 확인하지 못했습니다.";
                return false;
            }

            latest = update(latest);
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, latest, JsonOptions);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporary, path, overwrite: true);
            progress = latest;
            Warning = null;
            return true;
        }
        catch (Exception error) when (IsStorageError(error))
        {
            Warning = "스테이지 기록을 저장하지 못했습니다.";
            try
            {
                File.Delete(temporary);
            }
            catch (Exception cleanupError) when (IsStorageError(cleanupError))
            {
                // The existing profile remains available.
            }

            return false;
        }
    }

    private CampaignFile Read()
    {
        CampaignFile? saved;
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
            saved = JsonSerializer.Deserialize<CampaignFile>(stream, JsonOptions);
        }
        catch (FileNotFoundException)
        {
            return new CampaignFile(SchemaVersion, [], new(StringComparer.Ordinal));
        }
        catch (DirectoryNotFoundException)
        {
            return new CampaignFile(SchemaVersion, [], new(StringComparer.Ordinal));
        }

        if (saved is null || saved.Version != SchemaVersion ||
            saved.ClearedStageIds is null || saved.Replays is null ||
            !HasValidClears(saved.ClearedStageIds) ||
            (saved.LastStartedStageId is not null && !IsUnlocked(saved.LastStartedStageId, saved)))
            throw new JsonException("Invalid food campaign progress.");

        return saved;
    }

    private static bool HasValidClears(IEnumerable<string> stageIds)
    {
        var cleared = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in stageIds)
        {
            if (FoodCampaignStage.Find(id) is not { } stage ||
                (stage.ParentId is { } parent && !cleared.Contains(parent)) || !cleared.Add(id))
                return false;
        }

        return true;
    }

    private static bool IsUnlocked(string stageId, CampaignFile saved) =>
        FoodCampaignStage.Find(stageId) is { } stage &&
        (stage.ParentId is null || saved.ClearedStageIds.Contains(stage.ParentId, StringComparer.Ordinal));

    private static bool IsStorageError(Exception error) =>
        error is IOException or UnauthorizedAccessException or JsonException;

    private sealed record CampaignFile(int Version, List<string> ClearedStageIds, Dictionary<string, JsonElement> Replays,
        string? LastStartedStageId = null);
}
