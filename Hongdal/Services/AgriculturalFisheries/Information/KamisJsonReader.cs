using System.Text.Json;

namespace Hongdal.Services.AgriculturalFisheries.Information;

internal static class KamisJsonReader
{
    public static JsonElement ReadDataObject(
        JsonElement root,
        string productClassCode,
        string categoryCode)
    {
        if (!TryGetProperty(root, "data", out var data))
        {
            throw new InvalidOperationException("KAMIS 응답에 data 항목이 없습니다.");
        }

        if (data.ValueKind == JsonValueKind.Object)
        {
            return data;
        }

        if (data.ValueKind == JsonValueKind.Array)
        {
            var first = data.EnumerateArray().FirstOrDefault();
            if (first.ValueKind == JsonValueKind.Object)
            {
                return first;
            }

            var code = first.ValueKind == JsonValueKind.String
                ? first.GetString()
                : first.GetRawText();
            throw new InvalidOperationException(
                $"KAMIS 응답 형식이 올바르지 않습니다. 가격구분={productClassCode}, 부류={categoryCode}, 코드={code}");
        }

        throw new InvalidOperationException("KAMIS 응답의 data 항목 형식이 올바르지 않습니다.");
    }

    public static string ReadString(JsonElement source, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (!TryGetProperty(source, propertyName, out var value))
            {
                continue;
            }

            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString() ?? string.Empty,
                JsonValueKind.Number => value.GetRawText(),
                _ => string.Empty
            };
        }

        return string.Empty;
    }

    public static bool TryGetProperty(
        JsonElement source,
        string propertyName,
        out JsonElement value)
    {
        if (source.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in source.EnumerateObject())
            {
                if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }
}
