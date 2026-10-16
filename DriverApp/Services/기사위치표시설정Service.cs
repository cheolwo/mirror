using Microsoft.Maui.Storage;

namespace DriverApp.Services;

/// <summary>이 화물 기사 앱의 기기에만 지도 표시 선택을 보관합니다. 배차·위치 송신 설정과 무관합니다.</summary>
public sealed class 기사위치표시설정Service
{
    public const string DirectionArrowPreferenceKey = "driver.map.show_direction_arrow";
    private readonly IPreferences _preferences;

    public 기사위치표시설정Service(IPreferences preferences)
    {
        _preferences = preferences;
        방향화살표표시 = preferences.Get(DirectionArrowPreferenceKey, false);
    }

    public bool 방향화살표표시 { get; private set; }
    public event Action? Changed;

    public void SetDirectionArrow(bool enabled)
    {
        if (방향화살표표시 == enabled) return;
        _preferences.Set(DirectionArrowPreferenceKey, enabled);
        방향화살표표시 = enabled;
        Changed?.Invoke();
    }
}
