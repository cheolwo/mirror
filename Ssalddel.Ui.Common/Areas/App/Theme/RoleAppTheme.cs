using MudBlazor;

namespace Ssalddel.Ui.Common.Areas.App.Theme;

public enum 역할앱시각역할
{
    주문자,
    음식점,
    관리자
}

/// <summary>
/// 역할마다 강조색은 달리하되 같은 정보 밀도와 상태 색상 문법을 공유합니다.
/// 네이티브 배달 앱은 같은 팔레트를 XAML 리소스로 사용합니다.
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
            LayoutProperties = new LayoutProperties
            {
                DefaultBorderRadius = "14px"
            }
        };
    }

    private static PaletteLight CreateOrdererPalette() => CreateBasePalette(
        primary: "#0F766E",
        secondary: "#2563EB",
        appbar: "#FCFFFD",
        appbarText: "#173B57",
        background: "#EEECE5",
        divider: "#DCE8E3");

    private static PaletteLight CreateRestaurantPalette() => CreateBasePalette(
        primary: "#9A4D22",
        secondary: "#677A32",
        appbar: "#FFFDF9",
        appbarText: "#3F2F23",
        background: "#F7F2EA",
        divider: "#EADFD1");

    private static PaletteLight CreateAdminPalette() => CreateBasePalette(
        primary: "#173B57",
        secondary: "#0F766E",
        appbar: "#FCFFFD",
        appbarText: "#173B57",
        background: "#F3F7F5",
        divider: "#DCE8E3");

    private static PaletteLight CreateBasePalette(
        string primary,
        string secondary,
        string appbar,
        string appbarText,
        string background,
        string divider) => new()
    {
        Primary = primary,
        Secondary = secondary,
        AppbarBackground = appbar,
        AppbarText = appbarText,
        Background = background,
        Surface = "#FFFFFF",
        TextPrimary = "#0F172A",
        TextSecondary = "#64748B",
        Divider = divider,
        Success = "#0F9F6E",
        Warning = "#D97706",
        Error = "#DC2626",
        Info = "#2563EB"
    };
}
