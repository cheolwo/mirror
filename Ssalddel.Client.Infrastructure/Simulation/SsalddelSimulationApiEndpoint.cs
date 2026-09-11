namespace Ssalddel.Client.Infrastructure.Simulation;

/// <summary>
/// 운영·Simulation API를 함께 제공하는 단일 살뜰 서버 주소입니다.
/// </summary>
public static class SsalddelSimulationApiEndpoint
{
    public const string ConfigurationKey =
        "SsalddelEndpoints:ServerBaseAddress";

    public static Uri ResolveRequiredBaseAddress(string? configuredBaseAddress)
    {
        if (string.IsNullOrWhiteSpace(configuredBaseAddress))
        {
            throw new InvalidOperationException(
                "Ssalddel server base address is required for RemoteHost mode.");
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

        var isHttp = string.Equals(
                baseAddress.Scheme,
                Uri.UriSchemeHttp,
                StringComparison.OrdinalIgnoreCase)
            || string.Equals(
                baseAddress.Scheme,
                Uri.UriSchemeHttps,
                StringComparison.OrdinalIgnoreCase);

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
}
