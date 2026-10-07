using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;

namespace 살뜰.Services.Audit;

/// <summary>감사 저장 경계에는 요청 원문 대신 승인된 진단 필드만 남깁니다.</summary>
public static class 사용자행위로그보호Policy
{
    private static readonly HashSet<string> NumericKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "statusCode", "page", "pageSize", "take", "skip", "documentId", "entityId", "policyId",
        "inboundItemId", "warehouseId", "outboundPlanId", "packagingQuantity", "handoffQuantity"
    };
    private static readonly HashSet<string> IdentifierKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "traceId", "requestId", "orderId", "pageId", "viewKey", "countryCode", "method", "packagingType"
    };
    private static readonly HashSet<string> BooleanKeys = new(StringComparer.OrdinalIgnoreCase)
    { "enabled", "isVisible", "refresh" };

    public static IReadOnlyDictionary<string, string> SafeQuery(IQueryCollection query)
        => query.Where(x => NumericKeys.Contains(x.Key) && x.Value.Count == 1
                            && long.TryParse(x.Value[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            .ToDictionary(x => x.Key, x => x.Value[0]!, StringComparer.OrdinalIgnoreCase);

    public static string SafeMetadata(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        try
        {
            using var document = JsonDocument.Parse(value);
            if (document.RootElement.ValueKind != JsonValueKind.Object) return "{}";
            var fields = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var field in document.RootElement.EnumerateObject())
            {
                if (NumericKeys.Contains(field.Name) && field.Value.TryGetInt64Value(out var number)) fields[field.Name] = number;
                else if (BooleanKeys.Contains(field.Name) && field.Value.ValueKind is JsonValueKind.True or JsonValueKind.False)
                    fields[field.Name] = field.Value.GetBoolean();
                else if (IdentifierKeys.Contains(field.Name) && field.Value.ValueKind == JsonValueKind.String)
                {
                    var text = field.Value.GetString() ?? "";
                    if (text.Length is > 0 and <= 128 && Regex.IsMatch(text, @"^[\p{L}\p{N}_:.\-]+$")) fields[field.Name] = text;
                }
                else if (field.Name.Equals("query", StringComparison.OrdinalIgnoreCase) && field.Value.ValueKind == JsonValueKind.Object)
                {
                    var query = new Dictionary<string, long>();
                    foreach (var item in field.Value.EnumerateObject())
                        if (NumericKeys.Contains(item.Name) && long.TryParse(item.Value.ToString(), out var n)) query[item.Name] = n;
                    fields[field.Name] = query;
                }
                else if (field.Name.Equals("routeTemplate", StringComparison.OrdinalIgnoreCase) && field.Value.ValueKind == JsonValueKind.String)
                {
                    var route = field.Value.GetString() ?? "";
                    if (route.Length <= 256 && Regex.IsMatch(route, @"^/?[\p{L}\p{N}/{}_:.*?\-]+$")) fields[field.Name] = route;
                }
            }
            return JsonSerializer.Serialize(fields);
        }
        catch (JsonException) { return "{}"; }
    }

    public static string SafeError(string? message)
        => string.IsNullOrWhiteSpace(message) ? string.Empty : "오류 상세는 감사 로그에 저장하지 않습니다.";
}

internal static class 사용자행위로그JsonValueExtensions
{
    public static bool TryGetInt64Value(this JsonElement element, out long value)
    {
        value = 0;
        return element.ValueKind == JsonValueKind.Number && element.TryGetInt64(out value);
    }
}
