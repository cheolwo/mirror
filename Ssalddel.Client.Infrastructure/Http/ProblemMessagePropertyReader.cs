using System.Text.Json;

namespace Ssalddel.Client.Infrastructure.Http;

/// <summary>메시지 속성의 우선순위만 공유합니다. JSON 파싱·루트 검사·대체 문구는 호출자가 소유합니다.</summary>
public static class ProblemMessagePropertyReader
{
    public static string? Read(JsonElement root)
    {
        foreach (var name in new[] { "detail", "message", "title" })
        {
            if (root.TryGetProperty(name, out var value)
                && value.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(value.GetString()))
            {
                return value.GetString()!;
            }
        }

        return null;
    }
}
