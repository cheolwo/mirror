using System.Collections.Concurrent;

namespace SsalddelAdminApp.Services;

/// <summary>
/// 실제 관리자 인증 소스가 사용하는 기존 일곱 키를 프로세스 메모리에만 보관합니다.
/// 장치 SecureStorage와 앱 재시작 뒤 복구를 검증하는 구현은 아닙니다.
/// </summary>
internal static class SecureStorage
{
    public static MemoryStorage Default { get; } = new();

    public sealed class MemoryStorage
    {
        private readonly ConcurrentDictionary<string, string> values = new(StringComparer.Ordinal);

        public Task<string?> GetAsync(string key)
        {
            values.TryGetValue(key, out var value);
            return Task.FromResult(value);
        }

        public Task SetAsync(string key, string value)
        {
            values[key] = value;
            return Task.CompletedTask;
        }

        public bool Remove(string key) => values.TryRemove(key, out _);
    }
}
