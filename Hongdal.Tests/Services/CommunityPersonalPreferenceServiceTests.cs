using Hongdal.WebApp.Services;
using Microsoft.JSInterop;

namespace Hongdal.Tests.Services;

public sealed class CommunityPersonalPreferenceServiceTests
{
    [Fact]
    public async Task Preferences_AreStoredSeparatelyForEachUser()
    {
        var jsRuntime = new MemoryJsRuntime();
        var first = new CommunityPersonalPreferenceService(jsRuntime);
        first.Current.PostViewMode = "카드형";
        first.Current.NotifyReplies = false;

        await first.SaveAsync("user-1");

        var restored = new CommunityPersonalPreferenceService(jsRuntime);
        await restored.LoadAsync("user-1");
        var otherUser = new CommunityPersonalPreferenceService(jsRuntime);
        await otherUser.LoadAsync("user-2");

        Assert.Equal("카드형", restored.Current.PostViewMode);
        Assert.False(restored.Current.NotifyReplies);
        Assert.Equal("목록형", otherUser.Current.PostViewMode);
        Assert.True(otherUser.Current.NotifyReplies);
    }

    [Fact]
    public async Task Reset_RemovesStoredPreferencesAndRestoresDefaults()
    {
        var jsRuntime = new MemoryJsRuntime();
        var service = new CommunityPersonalPreferenceService(jsRuntime);
        service.Current.ShowActivityCountry = false;
        await service.SaveAsync(null);

        await service.ResetAsync(null);

        var restored = new CommunityPersonalPreferenceService(jsRuntime);
        await restored.LoadAsync(null);
        Assert.True(restored.Current.ShowActivityCountry);
        Assert.Equal("목록형", restored.Current.PostViewMode);
    }

    private sealed class MemoryJsRuntime : IJSRuntime
    {
        private readonly Dictionary<string, string> _storage = [];

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            CancellationToken cancellationToken,
            object?[]? args)
        {
            var key = args?[0]?.ToString() ?? string.Empty;
            object? result = null;
            switch (identifier)
            {
                case "localStorage.getItem":
                    _storage.TryGetValue(key, out var value);
                    result = value;
                    break;
                case "localStorage.setItem":
                    _storage[key] = args?[1]?.ToString() ?? string.Empty;
                    break;
                case "localStorage.removeItem":
                    _storage.Remove(key);
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported JS call: {identifier}");
            }

            return ValueTask.FromResult(result is null ? default! : (TValue)result);
        }
    }
}
