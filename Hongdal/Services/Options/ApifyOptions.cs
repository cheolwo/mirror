namespace 홍달.Services.Options;

public sealed class ApifyOptions
{
    public const string SectionName = "Apify";

    /// <summary>
    /// 외부 비용이 발생하는 Apify Actor 실행을 명시적으로 허용합니다.
    /// </summary>
    public bool Enabled { get; set; }

    public string ApiToken { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = "https://api.apify.com/v2/";

    public int TimeoutSeconds { get; set; } = 150;

    /// <summary>
    /// 개별 Adapter가 요청할 수 있는 호출당 최대 비용의 전역 상한입니다.
    /// </summary>
    public decimal MaxTotalChargeUsd { get; set; } = 2m;

    /// <summary>
    /// 서버가 실행해도 되는 Actor ID의 명시적 허용 목록입니다.
    /// </summary>
    public string[] AllowedActorIds { get; set; } = [];
}
