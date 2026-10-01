using System.Text.Json;
using Microsoft.JSInterop;

namespace Hongdal.Ui.Common.Areas.App.Services;

public sealed record CommunityDecorationSelectionSnapshot(
    string ActiveHomeThemePackKey,
    bool IsHomeThemeEnabled,
    IReadOnlyDictionary<string, string>? ActiveTraditionalMarketThemePackByScope = null,
    bool IsTraditionalMarketThemeEnabled = true);

public interface ICommunityDecorationSelectionStore
{
    Task<CommunityDecorationSelectionSnapshot?> LoadAsync(
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        CommunityDecorationSelectionSnapshot snapshot,
        CancellationToken cancellationToken = default);
}

public sealed class BrowserCommunityDecorationSelectionStore(IJSRuntime jsRuntime)
    : ICommunityDecorationSelectionStore
{
    private const string StorageKey = "hongdal.community.decoration-selection.v1";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<CommunityDecorationSelectionSnapshot?> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var json = await jsRuntime.InvokeAsync<string?>(
                "localStorage.getItem",
                cancellationToken,
                StorageKey);
            return string.IsNullOrWhiteSpace(json)
                ? null
                : JsonSerializer.Deserialize<CommunityDecorationSelectionSnapshot>(json, JsonOptions);
        }
        catch (Exception exception) when (exception is JSException or JsonException)
        {
            return null;
        }
    }

    public Task SaveAsync(
        CommunityDecorationSelectionSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return jsRuntime.InvokeVoidAsync(
                "localStorage.setItem",
                cancellationToken,
                StorageKey,
                JsonSerializer.Serialize(snapshot, JsonOptions))
            .AsTask();
    }
}
