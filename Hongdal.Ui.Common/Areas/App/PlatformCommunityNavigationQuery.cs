namespace Hongdal.Ui.Common.Areas.App;

internal static class PlatformCommunityNavigationQuery
{
    public static string Build(string path, IReadOnlyDictionary<string, string?> values)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(values);

        var query = string.Join(
            "&",
            values
                .Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
                .Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value!)}"));

        return string.IsNullOrWhiteSpace(query) ? path : $"{path}?{query}";
    }
}
