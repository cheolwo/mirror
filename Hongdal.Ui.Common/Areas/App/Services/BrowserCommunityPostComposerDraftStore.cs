using System.Text.Json;
using Hongdal.Ui.Common.Areas.App.ViewModels;
using Microsoft.JSInterop;

namespace Hongdal.Ui.Common.Areas.App.Services;

public sealed class BrowserCommunityPostComposerDraftStore(IJSRuntime jsRuntime)
    : ICommunityPostComposerDraftStore
{
    public async Task<CommunityPostComposerSnapshot?> LoadAsync(
        string appKey,
        CancellationToken cancellationToken = default)
    {
        var json = await jsRuntime.InvokeAsync<string?>(
            "localStorage.getItem",
            cancellationToken,
            StorageKey(appKey));
        return string.IsNullOrWhiteSpace(json)
            ? null
            : JsonSerializer.Deserialize<CommunityPostComposerSnapshot>(json);
    }

    public Task SaveAsync(
        string appKey,
        CommunityPostComposerSnapshot snapshot,
        CancellationToken cancellationToken = default)
        => jsRuntime.InvokeVoidAsync(
                "localStorage.setItem",
                cancellationToken,
                StorageKey(appKey),
                JsonSerializer.Serialize(snapshot))
            .AsTask();

    public Task ClearAsync(
        string appKey,
        CancellationToken cancellationToken = default)
        => jsRuntime.InvokeVoidAsync(
                "localStorage.removeItem",
                cancellationToken,
                StorageKey(appKey))
            .AsTask();

    private static string StorageKey(string appKey)
        => $"hongdal.community.compose-draft.{Uri.EscapeDataString(appKey)}";
}
