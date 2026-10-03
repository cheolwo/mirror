namespace FDriverApp.Controls;

/// <summary>Map creation, tile loading and authentication are separate facts.</summary>
public sealed class NaverMapReadiness
{
    private bool _hasLoadedTiles;
    private bool _hasInternet = true;
    private string? _authenticationErrorCode;

    public bool IsConfigured { get; }
    public NaverMapReadinessStatus Status { get; private set; }
    public string? Message => Status switch
    {
        NaverMapReadinessStatus.ConfigurationMissing => "지도를 사용할 수 없습니다. 운영자에게 문의해 주세요.",
        NaverMapReadinessStatus.Loading => "지도를 불러오는 중입니다.",
        NaverMapReadinessStatus.Offline => "인터넷 연결을 확인해 주세요. 지도 정보가 최신이 아닐 수 있습니다.",
        NaverMapReadinessStatus.AuthenticationFailed when _authenticationErrorCode == "429" =>
            "지도를 사용할 수 없습니다. 잠시 후 다시 시도하거나 운영자에게 문의해 주세요.",
        NaverMapReadinessStatus.AuthenticationFailed => "지도를 사용할 수 없습니다. 운영자에게 문의해 주세요.",
        NaverMapReadinessStatus.LoadUnavailable => "지도를 불러오지 못했습니다. 인터넷 연결을 확인하고 다시 시도해 주세요.",
        _ => null
    };

    public NaverMapReadiness(string? sdkKeyId)
    {
        IsConfigured = IsUsableKeyId(sdkKeyId);
        Status = IsConfigured ? NaverMapReadinessStatus.Loading : NaverMapReadinessStatus.ConfigurationMissing;
    }

    public static bool IsUsableKeyId(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        !value.Trim().StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase) &&
        !value.Trim().StartsWith("REPLACE_", StringComparison.OrdinalIgnoreCase);

    public void SetInternetAvailable(bool available)
    {
        _hasInternet = available;
        if (!IsConfigured || Status == NaverMapReadinessStatus.AuthenticationFailed)
        {
            return;
        }

        Status = !available ? NaverMapReadinessStatus.Offline :
            _hasLoadedTiles ? NaverMapReadinessStatus.Ready : NaverMapReadinessStatus.Loading;
    }

    public void OnTilesLoaded()
    {
        if (!IsConfigured || Status == NaverMapReadinessStatus.AuthenticationFailed)
        {
            return;
        }

        _hasLoadedTiles = true;
        Status = _hasInternet ? NaverMapReadinessStatus.Ready : NaverMapReadinessStatus.Offline;
    }

    public void OnAuthenticationFailed(string? errorCode)
    {
        if (!IsConfigured)
        {
            return;
        }

        // Store only a documented code, never SDK exception text or credentials.
        _authenticationErrorCode = errorCode is "401" or "429" or "800" ? errorCode : null;
        Status = NaverMapReadinessStatus.AuthenticationFailed;
    }

    public void OnLoadTimeout()
    {
        if (Status == NaverMapReadinessStatus.Loading)
        {
            Status = NaverMapReadinessStatus.LoadUnavailable;
        }
    }
}

public enum NaverMapReadinessStatus
{
    ConfigurationMissing,
    Loading,
    Offline,
    AuthenticationFailed,
    LoadUnavailable,
    Ready
}
