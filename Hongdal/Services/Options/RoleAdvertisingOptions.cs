using Hongdal.Contracts.Common.Advertising;

namespace 홍달.Services.Options;

public sealed class RoleAdvertisingOptions
{
    public const string SectionName = "RoleAdvertising";

    public bool Enabled { get; set; }
    public bool AllowOperationalPublishing { get; set; }
    public bool EnforceV0RoleBoundary { get; set; } = true;
    public string[] EnabledProviderCodes { get; set; } =
    [
        RoleAdvertisingProviderCodes.Meta,
        RoleAdvertisingProviderCodes.GoogleAds,
        RoleAdvertisingProviderCodes.LinkedIn,
        RoleAdvertisingProviderCodes.NaverSearchAds
    ];
}
