using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace SsalddelApp.Services;

/// <summary>표시 항목·목록 모드·공개 동네만 저장하며 개인 배송 좌표는 저장하지 않습니다.</summary>
public sealed class NeighborhoodMapPreferenceStore : INeighborhoodMapPreferenceStore
{
    public Task<NeighborhoodMapPreferences?> LoadAsync(string? ownerId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var json = Preferences.Default.Get(Key(ownerId), string.Empty);
            return Task.FromResult(string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<NeighborhoodMapPreferences>(json));
        }
        catch (JsonException) { return Task.FromResult<NeighborhoodMapPreferences?>(null); }
    }

    public Task SaveAsync(string? ownerId, NeighborhoodMapPreferences preferences, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Preferences.Default.Set(Key(ownerId), JsonSerializer.Serialize(preferences));
        return Task.CompletedTask;
    }

    private static string Key(string? ownerId) => "neighborhood-map:v1:" +
        (string.IsNullOrWhiteSpace(ownerId) ? "anonymous" : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ownerId))));
}
