using System.Text.Json;
using System.Text;
using System.Security.Cryptography;
using Microsoft.JSInterop;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.WebApp.Services;

/// <summary>레이어·목록 선택·공개 동네만 저장합니다. 배송 번호, 주소, 연락처, 경로는 저장하지 않습니다.</summary>
public sealed class NeighborhoodMapPreferenceStore(IJSRuntime js) : INeighborhoodMapPreferenceStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static string Key(string? owner) => "ssalddel.neighborhood-map.preferences.v1:" + (owner is null ? "visitor" : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(owner))).ToLowerInvariant());
    public async Task<NeighborhoodMapPreferences?> LoadAsync(string? ownerId, CancellationToken cancellationToken = default)
    {
        var value = await js.InvokeAsync<string?>("localStorage.getItem", cancellationToken, Key(ownerId));
        if (string.IsNullOrWhiteSpace(value)) return null;
        try { var saved = JsonSerializer.Deserialize<NeighborhoodMapPreferences>(value, Json); return saved is null ? null : Clean(saved); }
        catch (JsonException) { return null; }
    }
    public Task SaveAsync(string? ownerId, NeighborhoodMapPreferences preferences, CancellationToken cancellationToken = default)
        => js.InvokeVoidAsync("localStorage.setItem", cancellationToken, Key(ownerId), JsonSerializer.Serialize(Clean(preferences), Json)).AsTask();
    private static NeighborhoodMapPreferences Clean(NeighborhoodMapPreferences value) => new(
        NeighborhoodMapNavigation.ParseLayers(NeighborhoodMapNavigation.SerializeLayers(value.Layers ?? [])),
        NeighborhoodMapNavigation.View(value.ViewMode), value.RegionKey?.Length <= 100 ? value.RegionKey : null);
}
