namespace FDriverApp.Controls;

/// <summary>내 정보의 메뉴 한 행을 표시하며 실제 버튼이 접근성과 입력을 소유합니다.</summary>
public partial class DriverProfileMenuRow : ContentView
{
    public static readonly BindableProperty TextProperty = BindableProperty.Create(
        nameof(Text), typeof(string), typeof(DriverProfileMenuRow), string.Empty);
    public static readonly BindableProperty GlyphProperty = BindableProperty.Create(
        nameof(Glyph), typeof(string), typeof(DriverProfileMenuRow), string.Empty);

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public string Glyph
    {
        get => (string)GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    public event EventHandler? Clicked;

    public DriverProfileMenuRow() => InitializeComponent();

    private void OnMenuClicked(object? sender, EventArgs args) => Clicked?.Invoke(this, args);
}
