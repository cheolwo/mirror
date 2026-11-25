using Microsoft.Maui.Storage;

namespace RestaurantDeskApp.Services;

/// <summary>음식점 앱의 기기 로컬 설정입니다. 서버나 다른 기기의 설정을 변경하지 않습니다.</summary>
public sealed class Maui음식점전표출력설정Store : I음식점전표출력설정Store
{
    private const string PreferencesKey = "restaurant-desk-receipt-copy-count-v1";

    public int? Read()
        => Preferences.Default.ContainsKey(PreferencesKey)
            ? Preferences.Default.Get(PreferencesKey, 음식점전표출력설정Service.DefaultCopies)
            : null;

    public void Write(int 출력장수) => Preferences.Default.Set(PreferencesKey, 출력장수);
}
