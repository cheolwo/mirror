using Ssalddel.Contracts.Common.Community;

namespace Ssalddel.Ui.Common.Areas.App.Services;

public static class NeighborhoodMapNavigation
{
    public const string Home = "/community/map";
    public const string Offer = "offer";
    public const string Need = "need";
    public const string Mine = "mine-active";
    public const string PublicData = "public-data";
    public const string Storage = "storage";
    public static readonly IReadOnlyList<string> LayerCodes = [Offer, Need, Mine, PublicData, Storage];
    public static readonly IReadOnlyList<string> DefaultLayers = [Offer, Need, Mine];
    public static IReadOnlyList<string> ParseLayers(string value)
        => value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(LayerCodes.Contains).Distinct(StringComparer.Ordinal).ToArray();
    public static string SerializeLayers(IEnumerable<string> layers)
    {
        var selected = layers.ToHashSet(StringComparer.Ordinal);
        var value = string.Join(',', LayerCodes.Where(selected.Contains));
        return value.Length == 0 ? "none" : value;
    }
    public static string View(string? value) => value == "list" ? "list" : "map";
    public static string Href(IEnumerable<string> layers, string view, string? region, string? panelKind = null, string? panelId = null, string? panelRelated = null)
        => $"{Home}?layers={Uri.EscapeDataString(SerializeLayers(layers))}&view={View(view)}"
            + (string.IsNullOrWhiteSpace(region) ? "" : $"&region={Uri.EscapeDataString(region)}")
            + PanelQuery(panelKind, panelId, panelRelated);

    public static string? PanelKind(string? value)
        => value is "post" or "work" or "space" or "delivery" or "mine" or "register" or "spaces" ? value : null;
    public static string? PanelTarget(string? value)
        => !string.IsNullOrEmpty(value) && value.Length <= 128
            && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_') ? value : null;
    private static string PanelQuery(string? kind, string? id, string? related)
        => PanelKind(kind) is { } panel ? "&panel=" + panel
            + (PanelTarget(id) is { } target && panel is "post" or "work" or "space" or "delivery" ? "&target=" + Uri.EscapeDataString(target) : "")
            + (panel == "space" && PanelTarget(id) is not null && PanelTarget(related) is { } context ? "&related=" + Uri.EscapeDataString(context) : "") : "";
    public static string PanelHref(string baseHref, string? kind, string? id = null, string? related = null)
    {
        var href = SafeReturn(baseHref) ?? Home;
        var query = ParseQuery(href);
        return Href(query.TryGetValue("layers", out var layers) ? ParseLayers(layers) : DefaultLayers,
            query.GetValueOrDefault("view") ?? "map", query.GetValueOrDefault("region"), kind, id,
            related ?? (kind == "space" && query.GetValueOrDefault("panel") == kind && query.GetValueOrDefault("target") == id ? query.GetValueOrDefault("related") : null));
    }
    public static string RelatedHref(string baseHref, string spaceId, string collaborationId)
        => PanelHref(baseHref, "space", spaceId, collaborationId);
    public static string WithReturn(string route, string returnHref)
        => route + (route.Contains('?') ? "&" : "?") + "returnUrl=" + Uri.EscapeDataString(SafeReturn(returnHref) ?? Home);
    public static string? ReturnToPanel(string? returnHref, string kind, string id)
        => SafeReturn(returnHref) is { } safe ? PanelHref(safe, kind, id) : null;
    private static Dictionary<string, string> ParseQuery(string href)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var start = href.IndexOf('?');
        if (start < 0) return result;
        foreach (var part in href[(start + 1)..].Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var pieces = part.Split('=', 2);
            try { result[Uri.UnescapeDataString(pieces[0])] = pieces.Length == 2 ? Uri.UnescapeDataString(pieces[1]) : ""; }
            catch (UriFormatException) { }
        }
        return result;
    }
    public static string? SafeReturn(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        if (!Uri.TryCreate(value, UriKind.Relative, out _) || value.Contains('\\') || value.Contains('#')
            || value.Any(char.IsControl) || !value.StartsWith(Home, StringComparison.Ordinal)) return null;
        var path = value.Split('?')[0];
        if (path != Home || value.Contains("%2f", StringComparison.OrdinalIgnoreCase)
            || value.Contains("%5c", StringComparison.OrdinalIgnoreCase) || value.Contains("..", StringComparison.Ordinal)) return null;
        return value;
    }
    public static string WriteHref(string? region, string returnHref)
        => NeighborhoodExchange.Write + "?returnUrl=" + Uri.EscapeDataString(SafeReturn(returnHref) ?? Home)
            + (string.IsNullOrWhiteSpace(region) ? "" : "&region=" + Uri.EscapeDataString(region));
}
