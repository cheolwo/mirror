using System.Collections.Concurrent;

namespace SsalddelAdminApp.Services;

/// <summary>브라우저 렌더용 인증 어댑터입니다. Android SecureStorage 검증을 의미하지 않습니다.</summary>
internal static class SecureStorage
{
    public static MemoryStorage Default { get; } = new();
    internal sealed class MemoryStorage
    {
        private readonly ConcurrentDictionary<string, string> values = new(StringComparer.Ordinal);
        public Task<string?> GetAsync(string key) => Task.FromResult(values.TryGetValue(key, out var value) ? value : null);
        public Task SetAsync(string key, string value) { values[key] = value; return Task.CompletedTask; }
        public bool Remove(string key) => values.TryRemove(key, out _);
    }
}
