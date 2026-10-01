using System.Globalization;

namespace Hongdal.Ui.Common.Areas.App.Services;

public sealed record CommunityReadingThemePresentation(
    string PackKey,
    string Title,
    string Symbol,
    string BackgroundColor,
    string SurfaceColor,
    string AccentColor,
    string AccentRgb,
    string TextColor,
    string MutedColor,
    string BorderColor,
    string OnAccentColor,
    bool IsCustomized)
{
    public string CssVariables
        => string.Join(
            ';',
            $"--community-reading-background:{BackgroundColor}",
            $"--community-reading-surface:{SurfaceColor}",
            $"--community-reading-accent:{AccentColor}",
            $"--community-reading-accent-rgb:{AccentRgb}",
            $"--community-reading-text:{TextColor}",
            $"--community-reading-muted:{MutedColor}",
            $"--community-reading-border:{BorderColor}",
            $"--community-reading-on-accent:{OnAccentColor}",
            $"--platform-home-accent:{AccentColor}",
            $"--platform-home-accent-rgb:{AccentRgb}",
            $"--platform-home-border:{BorderColor}",
            $"--platform-home-surface:{SurfaceColor}",
            $"--platform-home-surface-soft:{BackgroundColor}");

    public static CommunityReadingThemePresentation Create(
        PlatformCommunityDecorationStateService decorationState)
    {
        ArgumentNullException.ThrowIfNull(decorationState);

        var isEnabled = decorationState.IsHomeThemeEnabled;
        var theme = isEnabled
            ? decorationState.ActiveHomeTheme
            : PlatformCommunityDecorationStateService.CreateDefaultHomeTheme();
        var product = isEnabled
            ? decorationState.Products.FirstOrDefault(candidate =>
                candidate.IsHomeTheme &&
                string.Equals(
                    candidate.PackKey,
                    decorationState.ActiveHomeThemePackKey,
                    StringComparison.OrdinalIgnoreCase))
            : null;
        var packKey = product?.PackKey ?? PlatformCommunityDecorationStateService.DefaultHomeThemePackKey;

        return new(
            packKey,
            product?.Title ?? "Hongdal basic",
            product?.ScriptureSource?.Symbol ?? "H",
            theme.PreviewBackground,
            theme.Labels.Color,
            theme.AccentColor,
            ToRgb(theme.AccentColor),
            theme.InnerCommunity.Color,
            theme.InnerStore.Color,
            theme.Frame.Color,
            theme.Labels.Color,
            isEnabled && !string.Equals(
                packKey,
                PlatformCommunityDecorationStateService.DefaultHomeThemePackKey,
                StringComparison.OrdinalIgnoreCase));
    }

    private static string ToRgb(string hexColor)
    {
        if (hexColor.Length != 7 || hexColor[0] != '#')
        {
            return "37, 99, 235";
        }

        return string.Join(
            ", ",
            byte.Parse(hexColor.AsSpan(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            byte.Parse(hexColor.AsSpan(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            byte.Parse(hexColor.AsSpan(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
    }
}
