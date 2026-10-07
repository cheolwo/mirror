using System.Text.Json.Nodes;
using 살뜰.도메인.운송;

namespace Ssalddel.Application.Driver.Transport;

public interface I운송증빙첨부JsonWriter
{
    void 추가(운송원장 entity, 운송증빙첨부 attachment);
}

public sealed class 운송증빙첨부JsonWriter : I운송증빙첨부JsonWriter
{
    public void 추가(운송원장 entity, 운송증빙첨부 attachment)
    {
        var attachments = ParseAttachments(entity.첨부_json);
        var objectName = attachment.ObjectName?.Trim();
        if ((attachment.Kind is "pickup-complete-photo" or "dropoff-complete-photo")
            && !string.IsNullOrWhiteSpace(objectName)
            && attachments.Any(existing => existing is JsonObject item
                && item["kind"] is JsonValue kind && kind.TryGetValue<string>(out var recordedKind)
                && string.Equals(recordedKind, attachment.Kind, StringComparison.Ordinal)
                && item["objectName"] is JsonValue name && name.TryGetValue<string>(out var recordedName)
                && string.Equals(recordedName?.Trim(), objectName, StringComparison.Ordinal)))
        {
            // 응답 유실 뒤 동일 완료 사진 재전송은 최초 증빙·시각·메타데이터를 유지한다.
            return;
        }
        var node = new JsonObject
        {
            ["kind"] = attachment.Kind,
            ["objectName"] = objectName,
            ["url"] = attachment.Url?.Trim(),
            ["uploadedBy"] = attachment.UploadedBy,
            ["recordedAtUtc"] = attachment.RecordedAtUtc
        };

        foreach (var item in attachment.Metadata)
        {
            node[item.Key] = ToJsonNode(item.Value);
        }

        attachments.Add(node);
        entity.첨부_json = attachments.ToJsonString();
    }

    private static JsonArray ParseAttachments(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        try
        {
            return JsonNode.Parse(value) as JsonArray ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static JsonNode? ToJsonNode(object? value)
    {
        return value switch
        {
            null => null,
            string text => JsonValue.Create(text),
            bool flag => JsonValue.Create(flag),
            DateTime dateTime => JsonValue.Create(dateTime),
            decimal number => JsonValue.Create(number),
            int number => JsonValue.Create(number),
            long number => JsonValue.Create(number),
            double number => JsonValue.Create(number),
            _ => JsonValue.Create(value.ToString())
        };
    }
}

public sealed record 운송증빙첨부(
    string Kind,
    string? ObjectName,
    string? Url,
    string UploadedBy,
    DateTime RecordedAtUtc,
    IReadOnlyDictionary<string, object?> Metadata);
