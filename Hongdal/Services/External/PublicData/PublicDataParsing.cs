using System.Text.Json;
using System.Xml.Linq;

namespace 홍달.Services.External.PublicData;

internal static class PublicDataParsing
{
    public static IReadOnlyList<Dictionary<string, string?>> ReadItems(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return [];
        }

        var trimmed = body.TrimStart();
        return trimmed.StartsWith("{", StringComparison.Ordinal) || trimmed.StartsWith("[", StringComparison.Ordinal)
            ? ReadJsonItems(body)
            : ReadXmlItems(body);
    }

    public static int? ReadTotalCount(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        var trimmed = body.TrimStart();
        if (trimmed.StartsWith("{", StringComparison.Ordinal) || trimmed.StartsWith("[", StringComparison.Ordinal))
        {
            using var doc = JsonDocument.Parse(body);
            var value = FindFirstJsonString(doc.RootElement, "totalCount", "totalCnt", "total");
            return TryInt(value);
        }

        var xml = XDocument.Parse(body);
        var text = xml.Descendants()
            .FirstOrDefault(x => IsAny(x.Name.LocalName, "totalCount", "totalCnt", "total"))
            ?.Value;
        return TryInt(text);
    }

    public static string? ReadResultCode(string body)
        => ReadFirstDocumentValue(body, "resultCode", "returnReasonCode");

    public static string? ReadResultMessage(string body)
        => ReadFirstDocumentValue(body, "resultMsg", "resultMessage", "returnAuthMsg", "errMsg");

    public static string? FirstValue(Dictionary<string, string?> item, params string[] keys)
    {
        foreach (var key in keys)
        {
            var match = item.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(match.Value))
            {
                return match.Value;
            }
        }

        return null;
    }

    public static int? FirstInt(Dictionary<string, string?> item, params string[] keys)
    {
        return TryInt(FirstValue(item, keys));
    }

    public static decimal? FirstDecimal(Dictionary<string, string?> item, params string[] keys)
    {
        return TryDecimal(FirstValue(item, keys));
    }

    public static IReadOnlyList<KeyValuePair<string, decimal>> NumericValues(Dictionary<string, string?> item)
    {
        return item
            .Select(x => new KeyValuePair<string, decimal?>(x.Key, TryDecimal(x.Value)))
            .Where(x => x.Value.HasValue)
            .Select(x => new KeyValuePair<string, decimal>(x.Key, x.Value!.Value))
            .ToArray();
    }

    private static IReadOnlyList<Dictionary<string, string?>> ReadXmlItems(string body)
    {
        var doc = XDocument.Parse(body);
        var nodes = doc.Descendants()
            .Where(x => string.Equals(x.Name.LocalName, "item", StringComparison.OrdinalIgnoreCase)
                || string.Equals(x.Name.LocalName, "juso", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        return nodes
            .Select(node => node.Elements()
                .GroupBy(x => x.Name.LocalName, StringComparer.OrdinalIgnoreCase)
                .ToDictionary<IGrouping<string, XElement>, string, string?>(
                    x => x.Key,
                    x => x.First().Value,
                    StringComparer.OrdinalIgnoreCase))
            .ToArray();
    }

    private static IReadOnlyList<Dictionary<string, string?>> ReadJsonItems(string body)
    {
        using var doc = JsonDocument.Parse(body);
        var arrays = new List<JsonElement>();
        CollectArrays(doc.RootElement, arrays);

        var firstObjectArray = arrays.FirstOrDefault(array =>
            array.ValueKind == JsonValueKind.Array
            && array.EnumerateArray().Any(element => element.ValueKind == JsonValueKind.Object));

        if (firstObjectArray.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return firstObjectArray.EnumerateArray()
            .Where(element => element.ValueKind == JsonValueKind.Object)
            .Select(ReadJsonObject)
            .ToArray();
    }

    private static Dictionary<string, string?> ReadJsonObject(JsonElement element)
    {
        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in element.EnumerateObject())
        {
            result[property.Name] = property.Value.ValueKind switch
            {
                JsonValueKind.String => property.Value.GetString(),
                JsonValueKind.Number => property.Value.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Null => null,
                _ => property.Value.GetRawText()
            };
        }

        return result;
    }

    private static void CollectArrays(JsonElement element, List<JsonElement> arrays)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            arrays.Add(element);
            foreach (var child in element.EnumerateArray())
            {
                CollectArrays(child, arrays);
            }
            return;
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var property in element.EnumerateObject())
        {
            CollectArrays(property.Value, arrays);
        }
    }

    private static string? FindFirstJsonString(JsonElement element, params string[] names)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (names.Any(name => string.Equals(name, property.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    return property.Value.ValueKind == JsonValueKind.String
                        ? property.Value.GetString()
                        : property.Value.GetRawText();
                }

                var child = FindFirstJsonString(property.Value, names);
                if (!string.IsNullOrWhiteSpace(child))
                {
                    return child;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in element.EnumerateArray())
            {
                var value = FindFirstJsonString(child, names);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
        }

        return null;
    }

    private static string? ReadFirstDocumentValue(string body, params string[] names)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        var trimmed = body.TrimStart();
        if (trimmed.StartsWith("{", StringComparison.Ordinal) || trimmed.StartsWith("[", StringComparison.Ordinal))
        {
            using var doc = JsonDocument.Parse(body);
            return FindFirstJsonString(doc.RootElement, names);
        }

        var xml = XDocument.Parse(body);
        return xml.Descendants()
            .FirstOrDefault(element => names.Any(name =>
                string.Equals(element.Name.LocalName, name, StringComparison.OrdinalIgnoreCase)))
            ?.Value;
    }

    private static bool IsAny(string value, params string[] candidates)
    {
        return candidates.Any(candidate => string.Equals(value, candidate, StringComparison.OrdinalIgnoreCase));
    }

    private static int? TryInt(string? value)
    {
        return int.TryParse(value, out var result) ? result : null;
    }

    private static decimal? TryDecimal(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Replace(",", string.Empty).Trim();
        return decimal.TryParse(normalized, out var result) ? result : null;
    }
}
