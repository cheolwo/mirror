global using Microsoft.Maui.Storage;

namespace Microsoft.Maui.Storage;

// MAUI 저장 경계만 대체하고 운행 프로필 정책은 실제 제품 소스를 실행한다.
public static class Preferences
{
    private static readonly AsyncLocal<Store?> Current = new();
    public static Store Default => Current.Value ??= new Store();

    public sealed class Store
    {
        private readonly Dictionary<string, string> _values = [];
        public string Get(string key, string fallback) => _values.GetValueOrDefault(key, fallback);
        public void Set(string key, string value) => _values[key] = value;
    }
}
