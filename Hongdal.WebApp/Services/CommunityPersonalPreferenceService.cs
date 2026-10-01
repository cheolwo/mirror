using System.Text.Json;
using Microsoft.JSInterop;

namespace Hongdal.WebApp.Services;

public sealed class CommunityPersonalPreferences
{
    public string PostViewMode { get; set; } = "목록형";
    public bool ShowActivityCountry { get; set; } = true;
    public bool NotifyReplies { get; set; } = true;
    public bool NotifyJourneyChanges { get; set; } = true;
    public bool UseCompactPersonalMenu { get; set; }
}

public sealed class CommunityPersonalPreferenceService(IJSRuntime jsRuntime)
{
    private const string StorageKeyPrefix = "hongdal.community.personal-preferences.v1";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public CommunityPersonalPreferences Current { get; private set; } = new();

    public bool IsLoaded { get; private set; }

    public event Action? Changed;

    public async Task LoadAsync(string? userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var json = await jsRuntime.InvokeAsync<string?>(
                "localStorage.getItem",
                cancellationToken,
                BuildStorageKey(userId));
            Current = string.IsNullOrWhiteSpace(json)
                ? new CommunityPersonalPreferences()
                : JsonSerializer.Deserialize<CommunityPersonalPreferences>(json, JsonOptions)
                  ?? new CommunityPersonalPreferences();
        }
        catch (Exception exception) when (exception is JSException or JsonException)
        {
            Current = new CommunityPersonalPreferences();
        }

        IsLoaded = true;
        Changed?.Invoke();
    }

    public async Task SaveAsync(string? userId, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(Current, JsonOptions);
        await jsRuntime.InvokeVoidAsync(
            "localStorage.setItem",
            cancellationToken,
            BuildStorageKey(userId),
            json);
        IsLoaded = true;
        Changed?.Invoke();
    }

    public async Task ResetAsync(string? userId, CancellationToken cancellationToken = default)
    {
        Current = new CommunityPersonalPreferences();
        await jsRuntime.InvokeVoidAsync(
            "localStorage.removeItem",
            cancellationToken,
            BuildStorageKey(userId));
        IsLoaded = true;
        Changed?.Invoke();
    }

    private static string BuildStorageKey(string? userId)
        => $"{StorageKeyPrefix}:{(string.IsNullOrWhiteSpace(userId) ? "visitor" : userId.Trim())}";
}
