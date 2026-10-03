using MudBlazor;

namespace Ssalddel.Ui.Common.Areas.App.Theme;

public enum 역할앱시각역할
{
    주문자,
    음식점,
    관리자
}

/// <summary>
/// 역할별 업무는 유지하며 같은 색상·글자·표면 문법을 공유합니다.
/// 네이티브 배달 앱은 같은 팔레트를 XAML 리소스로 사용합니다.
/// 시각 기준: docs/Architecture/RoleAppVisualDesignStandard.md.
/// </summary>
public static class RoleAppTheme
{
    public static MudTheme Create(역할앱시각역할 role)
    {
        var palette = role switch
        {
            역할앱시각역할.음식점 => CreateRestaurantPalette(),
            역할앱시각역할.관리자 => CreateAdminPalette(),
            _ => CreateOrdererPalette()
        };

        return new MudTheme
        {
            PaletteLight = palette,
            Typography = CreateTypography(),
            LayoutProperties = new LayoutProperties
            {
                DefaultBorderRadius = "12px"
            }
        };
    }

    private static PaletteLight CreateOrdererPalette() => CreateBasePalette(
        primary: "#0F766E",
        secondary: "#586B78",
        appbar: "#FFFFFF",
        appbarText: "#172B36",
        background: "#F5F7FA",
        divider: "#DCE5E9");

    private static PaletteLight CreateRestaurantPalette() => CreateBasePalette(
        primary: "#0F766E",
        secondary: "#586B78",
        appbar: "#FFFFFF",
        appbarText: "#172B36",
        background: "#F5F7FA",
        divider: "#DCE5E9");

    private static PaletteLight CreateAdminPalette() => CreateBasePalette(
        primary: "#0F766E",
        secondary: "#586B78",
        appbar: "#FFFFFF",
        appbarText: "#172B36",
        background: "#F5F7FA",
        divider: "#DCE5E9");

    private static PaletteLight CreateBasePalette(
        string primary,
        string secondary,
        string appbar,
        string appbarText,
        string background,
        string divider) => new()
    {
        Primary = primary,
        PrimaryContrastText = "#FFFFFF",
        Secondary = secondary,
        AppbarBackground = appbar,
        AppbarText = appbarText,
        Background = background,
        Surface = "#FFFFFF",
        BackgroundGray = "#F5F7FA",
        TextPrimary = "#172B36",
        TextSecondary = "#586B78",
        Divider = divider,
        LinesDefault = divider,
        LinesInputs = "#788C97",
        Success = "#166534",
        Warning = "#92400E",
        Error = "#B42318",
        Info = "#1D4ED8"
    };

    private static Typography CreateTypography() => new()
    {
        Default = new DefaultTypography
        {
            FontFamily = ["Pretendard", "SUIT", "Noto Sans KR", "Apple SD Gothic Neo", "Malgun Gothic", "sans-serif"],
            FontSize = "1rem", LineHeight = "1.5", LetterSpacing = "0"
        },
        H4 = new H4Typography { FontSize = "1.5rem", FontWeight = "600", LineHeight = "1.35", LetterSpacing = "-.02em" },
        H5 = new H5Typography { FontSize = "1.375rem", FontWeight = "600", LineHeight = "1.4", LetterSpacing = "-.02em" },
        H6 = new H6Typography { FontSize = "1.125rem", FontWeight = "600", LineHeight = "1.45", LetterSpacing = "-.01em" },
        Subtitle1 = new Subtitle1Typography { FontSize = "1rem", FontWeight = "600", LineHeight = "1.5", LetterSpacing = "0" },
        Subtitle2 = new Subtitle2Typography { FontSize = ".875rem", FontWeight = "600", LineHeight = "1.45", LetterSpacing = "0" },
        Body1 = new Body1Typography { FontSize = "1rem", LineHeight = "1.5", LetterSpacing = "0" },
        Body2 = new Body2Typography { FontSize = ".875rem", LineHeight = "1.5", LetterSpacing = "0" },
        Caption = new CaptionTypography { FontSize = ".875rem", LineHeight = "1.45", LetterSpacing = "0" },
        Button = new ButtonTypography { FontSize = ".875rem", FontWeight = "600", LetterSpacing = "0", TextTransform = "none" }
    };
}
