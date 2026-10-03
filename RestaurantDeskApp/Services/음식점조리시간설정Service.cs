using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.Maui.Storage;
using RestaurantDeskApp.Options;
using Ssalddel.Contracts.Food;

namespace RestaurantDeskApp.Services;

public sealed class 음식점조리시간설정Service : I음식점조리시간설정Service
{
    private const string PreferencesKey = "restaurant-desk-preparation-time-settings-v1";
    private readonly object _gate = new();
    private 음식점조리시간설정Snapshot _current;

    public 음식점조리시간설정Service(IOptions<RestaurantDeskOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _current = LoadOrDefault(options.Value);
    }

    public 음식점조리시간설정Snapshot 현재조회()
    {
        lock (_gate)
        {
            return Clone(_current);
        }
    }

    public Task 저장Async(
        int 음식점기본조리분,
        IReadOnlyDictionary<string, int> 상품별기본조리분,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(상품별기본조리분);
        cancellationToken.ThrowIfCancellationRequested();

        var normalized = Normalize(음식점기본조리분, 상품별기본조리분);
        Preferences.Default.Set(
            PreferencesKey,
            JsonSerializer.Serialize(new 저장Model
            {
                음식점기본조리분 = normalized.음식점기본조리분,
                상품별기본조리분 = new Dictionary<string, int>(
                    normalized.상품별기본조리분,
                    StringComparer.OrdinalIgnoreCase)
            }));

        lock (_gate)
        {
            _current = normalized;
        }

        return Task.CompletedTask;
    }

    private static 음식점조리시간설정Snapshot LoadOrDefault(RestaurantDeskOptions options)
    {
        try
        {
            var json = Preferences.Default.Get(PreferencesKey, string.Empty);
            if (!string.IsNullOrWhiteSpace(json))
            {
                var stored = JsonSerializer.Deserialize<저장Model>(json);
                if (stored is not null)
                {
                    return Normalize(
                        stored.음식점기본조리분,
                        stored.상품별기본조리분 ?? new Dictionary<string, int>());
                }
            }
        }
        catch (JsonException)
        {
            // 손상된 로컬 설정은 배포 기본값으로 복구한다.
        }

        return Normalize(
            options.DefaultPreparationMinutes,
            options.상품별기본조리분 ?? new Dictionary<string, int>());
    }

    private static 음식점조리시간설정Snapshot Normalize(
        int 음식점기본조리분,
        IReadOnlyDictionary<string, int> 상품별기본조리분)
    {
        var normalized = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in 상품별기본조리분)
        {
            var 상품명 = item.Key?.Trim();
            if (!string.IsNullOrWhiteSpace(상품명))
            {
                normalized[상품명] = 음식점조리시간정책.Clamp(item.Value);
            }
        }

        return new 음식점조리시간설정Snapshot(
            음식점조리시간정책.Clamp(음식점기본조리분),
            normalized);
    }

    private static 음식점조리시간설정Snapshot Clone(음식점조리시간설정Snapshot source)
        => new(
            source.음식점기본조리분,
            new Dictionary<string, int>(
                source.상품별기본조리분,
                StringComparer.OrdinalIgnoreCase));

    private sealed class 저장Model
    {
        public int 음식점기본조리분 { get; set; }

        public Dictionary<string, int>? 상품별기본조리분 { get; set; }
    }
}
