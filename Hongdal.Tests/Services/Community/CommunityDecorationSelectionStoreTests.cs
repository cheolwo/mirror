using Hongdal.Ui.Common.Areas.App.Services;
using Microsoft.JSInterop;

namespace Hongdal.Tests.Services.Community;

public sealed class CommunityDecorationSelectionStoreTests
{
    [Fact]
    public async Task 선택한홈테마패키지를_브라우저저장소에서복원한다()
    {
        var jsRuntime = new MemoryJsRuntime();
        var store = new BrowserCommunityDecorationSelectionStore(jsRuntime);
        var expected = new CommunityDecorationSelectionSnapshot(
            "home-theme-scripture-analects-v1",
            true);

        await store.SaveAsync(expected);
        var restored = await store.LoadAsync();

        Assert.Equal(expected, restored);
    }

    [Fact]
    public async Task 시장별꾸미기팩선택을_브라우저저장소에서복원한다()
    {
        var jsRuntime = new MemoryJsRuntime();
        var store = new BrowserCommunityDecorationSelectionStore(jsRuntime);
        var expected = new CommunityDecorationSelectionSnapshot(
            "home-theme-hongdal-default-v1",
            true,
            new Dictionary<string, string>
            {
                ["traditional-market:sample-seongnam"] = "market-theme-seongnam-harvest-v1"
            },
            true);

        await store.SaveAsync(expected);
        var restored = await store.LoadAsync();

        Assert.NotNull(restored);
        Assert.True(restored.IsTraditionalMarketThemeEnabled);
        Assert.Equal(
            "market-theme-seongnam-harvest-v1",
            restored.ActiveTraditionalMarketThemePackByScope?["traditional-market:sample-seongnam"]);
    }

    private sealed class MemoryJsRuntime : IJSRuntime
    {
        private readonly Dictionary<string, string> storage = [];

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            CancellationToken cancellationToken,
            object?[]? args)
        {
            var key = args?[0]?.ToString() ?? string.Empty;
            object? result = null;
            if (identifier == "localStorage.getItem")
            {
                storage.TryGetValue(key, out var value);
                result = value;
            }
            else if (identifier == "localStorage.setItem")
            {
                storage[key] = args?[1]?.ToString() ?? string.Empty;
            }
            else
            {
                throw new InvalidOperationException($"Unsupported JS call: {identifier}");
            }

            return ValueTask.FromResult(result is null ? default! : (TValue)result);
        }
    }
}
