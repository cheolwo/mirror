using System.Text.Json;

namespace 살뜰.Services.Dispatch.AI;

/// <summary>
/// 이미 받은 Ollama HTTP 응답을 제한된 크기로 읽고 chat 봉투만 검사합니다.
/// 요청·기한·재시도·fallback과 content 안의 후보/계획/행동 판정은 각 어댑터가 소유합니다.
/// </summary>
internal static class OllamaChatResponseReader
{
    internal const int MaxResponseBytes = 64 * 1024;

    internal static async Task<OllamaChatResponse> ReadAsync(
        HttpResponseMessage response, string expectedModel, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode) return new(null, "HttpFailure");
        if (response.Content.Headers.ContentLength > MaxResponseBytes) return new(null, "ResponseTooLarge");
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        while (buffer.Length <= MaxResponseBytes)
        {
            var read = await stream.ReadAsync(chunk.AsMemory(0,
                Math.Min(chunk.Length, MaxResponseBytes + 1 - (int)buffer.Length)), cancellationToken);
            if (read == 0) break;
            buffer.Write(chunk, 0, read);
        }
        if (buffer.Length > MaxResponseBytes) return new(null, "ResponseTooLarge");
        using var envelope = JsonDocument.Parse(buffer.ToArray(), new JsonDocumentOptions { MaxDepth = 16 });
        RejectDuplicateProperties(envelope.RootElement);
        var root = envelope.RootElement;
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("model", out var model) || model.ValueKind != JsonValueKind.String || model.GetString() != expectedModel
            || !root.TryGetProperty("done", out var done) || done.ValueKind != JsonValueKind.True
            || !root.TryGetProperty("message", out var message) || message.ValueKind != JsonValueKind.Object
            || !message.TryGetProperty("role", out var role) || role.ValueKind != JsonValueKind.String || role.GetString() != "assistant"
            || !message.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.String
            || message.TryGetProperty("tool_calls", out var calls) && calls.ValueKind != JsonValueKind.Null
                && (calls.ValueKind != JsonValueKind.Array || calls.GetArrayLength() != 0))
            return new(null, "InvalidResponse");
        return new(content.GetString(), null);
    }

    // content의 JSON도 같은 문법 검사를 재사용하되 스키마·허용 토큰 판정은 호출자가 수행합니다.
    internal static void RejectDuplicateProperties(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new JsonException("Duplicate JSON property.");
                RejectDuplicateProperties(property.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
            foreach (var item in value.EnumerateArray()) RejectDuplicateProperties(item);
    }
}

internal sealed record OllamaChatResponse(string? Content, string? ErrorCode);
