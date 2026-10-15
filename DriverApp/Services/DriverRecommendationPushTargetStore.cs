using System.Text.Json;
using Microsoft.Maui.Storage;

namespace DriverApp.Services;

/// <summary>재실행 복귀용 최소 식별자만 기기 보안 저장소에 보관합니다.</summary>
public sealed class DriverRecommendationPushTargetStore : IDriverRecommendationPushTargetStore
{
    private const string Key = "ssalddel.driver.recommendationTarget.v1";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<DriverRecommendationPushTarget?> LoadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var json = await SecureStorage.Default.GetAsync(Key);
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            var target = JsonSerializer.Deserialize<DriverRecommendationPushTarget>(json, JsonOptions);
            if (target is null || !DriverRecommendationPushHintParser.ValidId(target.OwnerId)
                || target.Hint is null || !DriverRecommendationPushHintParser.ValidId(target.Hint.RequestId))
                throw new JsonException();
            return target;
        }
        catch (JsonException)
        {
            SecureStorage.Default.Remove(Key);
            return null;
        }
    }

    public async Task SaveAsync(DriverRecommendationPushTarget target, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await SecureStorage.Default.SetAsync(Key, JsonSerializer.Serialize(target, JsonOptions));
        cancellationToken.ThrowIfCancellationRequested();
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SecureStorage.Default.Remove(Key);
        return Task.CompletedTask;
    }
}
