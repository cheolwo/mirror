namespace 홍달.Services.Options;

public sealed class NaverMapsOptions
{
    public const string SectionName = "NaverMaps";

    public string BaseUrl { get; set; } = "https://maps.apigw.ntruss.com";
    public string ReverseGeocodingPath { get; set; } = "/map-reversegeocode/v2/gc";
    public string ApplicationName { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
}
