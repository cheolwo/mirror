namespace HumanResourcesManagerApp.ViewModels;

internal static class 인사Api경로
{
    public static string Query(string path, params (string Name, string? Value)[] values)
    {
        var query = values
            .Where(x => !string.IsNullOrWhiteSpace(x.Value))
            .Select(x => $"{Uri.EscapeDataString(x.Name)}={Uri.EscapeDataString(x.Value!.Trim())}")
            .ToArray();

        return query.Length == 0 ? path : $"{path}?{string.Join('&', query)}";
    }
}
