namespace HongdalApp.Options;

public sealed class CoupangWingOptions
{
    public const string SectionName = "CoupangWing";

    public string BaseUrl { get; set; } = "https://api-gateway.coupang.com/";

    public string AccessKey { get; set; } = string.Empty;

    public string SecretKey { get; set; } = string.Empty;

    public string VendorId { get; set; } = string.Empty;
}
