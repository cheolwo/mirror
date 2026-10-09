using Fonts;

namespace FDriverApp.Controls;

// Presentation only: the existing button still owns its text, command and availability.
public static class DriverActionIcon
{
    public static readonly BindableProperty GlyphProperty = BindableProperty.CreateAttached(
        "Glyph", typeof(string), typeof(DriverActionIcon), null, propertyChanged: OnGlyphChanged);

    public static string? GetGlyph(BindableObject target) => (string?)target.GetValue(GlyphProperty);

    public static void SetGlyph(BindableObject target, string? value) => target.SetValue(GlyphProperty, value);

    private static void OnGlyphChanged(BindableObject target, object oldValue, object newValue)
    {
        if (target is not Button button) return;

        if (newValue is not string glyph || string.IsNullOrEmpty(glyph))
        {
            button.ImageSource = null;
            return;
        }

        var icon = new FontImageSource
        {
            FontFamily = FluentUI.FontFamily,
            Glyph = glyph,
            Size = 20,
            FontAutoScalingEnabled = false
        };
        // Follow theme, selection and disabled text colors without subscribing to state events.
        icon.SetBinding(FontImageSource.ColorProperty,
            new Binding(nameof(Button.TextColor), source: button));
        button.ImageSource = icon;
        button.ContentLayout = new Button.ButtonContentLayout(
            Button.ButtonContentLayout.ImagePosition.Left, 8);
    }
}
