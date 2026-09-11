namespace Ssalddel.Ui.Common.Areas.App.Services;

/// <summary>
/// Web·모바일·Unity가 함께 사용하는 단일 살뜰 서버 주소 계약입니다.
/// </summary>
public sealed class SsalddelEndpointOptions
{
    public const string SectionName = "SsalddelEndpoints";

    public string? ServerBaseAddress { get; set; }
}

public static class SsalddelHttpClientNames
{
    public const string OperationalApi = "Ssalddel.Operational.Api";
    public const string SimulationApi = "Ssalddel.Simulation.Api";
}

/// <summary>
/// 운영 API와 Simulation API를 함께 제공하는 단일 살뜰 서버 주소입니다.
/// </summary>
public static class SsalddelServerEndpoint
{
    public const string ConfigurationKey =
        SsalddelEndpointOptions.SectionName + ":ServerBaseAddress";
    public const string LegacyConfigurationKey = "SsalddelApiBaseAddress";
    public const string LocalDevelopmentBaseAddress = "https://localhost:7117/";
    public const string AndroidEmulatorDebugBaseAddress = "http://10.0.2.2:5104/";

    public static Uri CreateDefaultBaseAddress()
    {
#if DEBUG
        if (OperatingSystem.IsAndroid())
        {
            return new Uri(AndroidEmulatorDebugBaseAddress);
        }
#endif

        return new Uri(LocalDevelopmentBaseAddress);
    }

    public static Uri ResolveConfiguredBaseAddress(
        string? configuredBaseAddress,
        string? legacyConfiguredBaseAddress,
        Uri? fallback = null)
        => ResolveBaseAddress(
            FirstConfiguredValue(configuredBaseAddress, legacyConfiguredBaseAddress),
            fallback);

    public static Uri ResolveBrowserBaseAddress(
        string? configuredBaseAddress,
        string? legacyConfiguredBaseAddress,
        Uri applicationBaseAddress)
    {
        ArgumentNullException.ThrowIfNull(applicationBaseAddress);

        var configured = FirstConfiguredValue(
            configuredBaseAddress,
            legacyConfiguredBaseAddress);
        if (string.Equals(configured, "same-origin", StringComparison.OrdinalIgnoreCase))
        {
            var absoluteApplicationBaseAddress = NormalizeBaseAddress(applicationBaseAddress);
            return new Uri(
                absoluteApplicationBaseAddress.GetLeftPart(UriPartial.Authority) + "/",
                UriKind.Absolute);
        }

        return ResolveBaseAddress(configured, applicationBaseAddress);
    }

    public static Uri ResolveBaseAddress(
        string? configuredBaseAddress,
        Uri? fallback = null)
    {
        if (string.IsNullOrWhiteSpace(configuredBaseAddress))
        {
            return NormalizeBaseAddress(fallback ?? CreateDefaultBaseAddress());
        }

        if (!Uri.TryCreate(configuredBaseAddress.Trim(), UriKind.Absolute, out var configuredUri))
        {
            throw new ArgumentException(
                "The Ssalddel server base address must be an absolute HTTP(S) URI.",
                nameof(configuredBaseAddress));
        }

        return NormalizeBaseAddress(configuredUri);
    }

    public static Uri NormalizeBaseAddress(Uri baseAddress)
    {
        ArgumentNullException.ThrowIfNull(baseAddress);

        var isHttp = string.Equals(baseAddress.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            || string.Equals(baseAddress.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);

        if (!baseAddress.IsAbsoluteUri
            || !isHttp
            || !string.IsNullOrEmpty(baseAddress.Query)
            || !string.IsNullOrEmpty(baseAddress.Fragment))
        {
            throw new ArgumentException(
                "The Ssalddel server base address must be an absolute HTTP(S) URI without a query or fragment.",
                nameof(baseAddress));
        }

        var builder = new UriBuilder(baseAddress);
        if (!builder.Path.EndsWith("/", StringComparison.Ordinal))
        {
            builder.Path += "/";
        }

        return builder.Uri;
    }

    private static string? FirstConfiguredValue(
        string? configuredBaseAddress,
        string? legacyConfiguredBaseAddress)
        => !string.IsNullOrWhiteSpace(configuredBaseAddress)
            ? configuredBaseAddress
            : legacyConfiguredBaseAddress;
}

/// <summary>운영 API 논리 client의 source 호환 이름입니다.</summary>
[Obsolete("Use SsalddelServerEndpoint. Operational and Simulation APIs share one server address.")]
public static class SsalddelOperationalApiEndpoint
{
    public const string ConfigurationKey = SsalddelServerEndpoint.ConfigurationKey;
    public const string LegacyConfigurationKey = SsalddelServerEndpoint.LegacyConfigurationKey;
    public const string LocalDevelopmentBaseAddress = SsalddelServerEndpoint.LocalDevelopmentBaseAddress;
    public const string AndroidEmulatorDebugBaseAddress = SsalddelServerEndpoint.AndroidEmulatorDebugBaseAddress;

    public static Uri CreateDefaultBaseAddress() => SsalddelServerEndpoint.CreateDefaultBaseAddress();
    public static Uri ResolveConfiguredBaseAddress(string? value, string? legacy, Uri? fallback = null)
        => SsalddelServerEndpoint.ResolveConfiguredBaseAddress(value, legacy, fallback);
    public static Uri ResolveBrowserBaseAddress(string? value, string? legacy, Uri appBase)
        => SsalddelServerEndpoint.ResolveBrowserBaseAddress(value, legacy, appBase);
    public static Uri ResolveBaseAddress(string? value, Uri? fallback = null)
        => SsalddelServerEndpoint.ResolveBaseAddress(value, fallback);
    public static Uri NormalizeBaseAddress(Uri value)
        => SsalddelServerEndpoint.NormalizeBaseAddress(value);
}

/// <summary>
/// 원격 가상 세션도 같은 살뜰 서버 주소를 사용합니다.
/// </summary>
public static class SsalddelSimulationApiEndpoint
{
    public const string ConfigurationKey =
        SsalddelServerEndpoint.ConfigurationKey;

    public static Uri ResolveRequiredBaseAddress(string? configuredBaseAddress)
    {
        if (string.IsNullOrWhiteSpace(configuredBaseAddress))
        {
            throw new InvalidOperationException(
                "Ssalddel Simulation API base address is required for RemoteHost mode.");
        }

        if (!Uri.TryCreate(configuredBaseAddress.Trim(), UriKind.Absolute, out var configuredUri))
        {
            throw new ArgumentException(
                "The Ssalddel Simulation API base address must be an absolute HTTP(S) URI.",
                nameof(configuredBaseAddress));
        }

        return SsalddelServerEndpoint.NormalizeBaseAddress(configuredUri);
    }
}

/// <summary>
/// 이전 클라이언트 호환용 이름입니다. 새 코드에서는 운영·Simulation 주소를 명시적으로 선택합니다.
/// </summary>
[Obsolete("Use SsalddelServerEndpoint.")]
public static class SsalddelApiEndpoint
{
    public const string ConfigurationKey = SsalddelServerEndpoint.LegacyConfigurationKey;
    public const string LocalDevelopmentBaseAddress =
        SsalddelServerEndpoint.LocalDevelopmentBaseAddress;
    public const string AndroidEmulatorDebugBaseAddress =
        SsalddelServerEndpoint.AndroidEmulatorDebugBaseAddress;

    public static Uri CreateDefaultBaseAddress()
        => SsalddelServerEndpoint.CreateDefaultBaseAddress();

    public static Uri ResolveBaseAddress(string? configuredBaseAddress, Uri? fallback = null)
        => SsalddelServerEndpoint.ResolveBaseAddress(configuredBaseAddress, fallback);

    public static Uri NormalizeBaseAddress(Uri baseAddress)
        => SsalddelServerEndpoint.NormalizeBaseAddress(baseAddress);
}
