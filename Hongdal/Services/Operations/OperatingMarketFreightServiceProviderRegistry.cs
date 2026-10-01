using Hongdal.Contracts.Common.Operations;

namespace Hongdal.Services.Operations;

public interface IOperatingMarketFreightServiceProviderRegistry
{
    string MarketCode { get; }

    OperatingMarketFreightServiceProviderVerification? Current { get; }
}

public sealed class DeploymentOperatingMarketFreightServiceProviderRegistry
    : IOperatingMarketFreightServiceProviderRegistry
{
    public DeploymentOperatingMarketFreightServiceProviderRegistry(
        string? marketCode,
        OperatingMarketFreightServiceProviderOptions? options)
    {
        if (!OperatingMarketCodes.TryNormalize(marketCode, out var normalizedMarketCode))
        {
            throw new ArgumentOutOfRangeException(
                nameof(marketCode),
                marketCode,
                "The freight service provider registry market must be KR or US.");
        }

        MarketCode = normalizedMarketCode;
        options ??= new OperatingMarketFreightServiceProviderOptions();
        if (!HasConfiguration(options))
        {
            return;
        }

        Current = new OperatingMarketFreightServiceProviderVerification
        {
            MarketCode = MarketCode,
            ServiceProviderParticipantId = Normalize(options.ParticipantId),
            ServiceProviderRoleCode = Normalize(options.ParticipantRoleCode),
            AuthorityReference = Normalize(options.AuthorityReference),
            VerifiedAtUtc = options.VerifiedAtUtc?.ToUniversalTime(),
            VerificationExpiresAtUtc = options.VerificationExpiresAtUtc?.ToUniversalTime(),
            SatisfiedRequirementCodes = (options.SatisfiedRequirementCodes ?? [])
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray()
        };
    }

    public string MarketCode { get; }

    public OperatingMarketFreightServiceProviderVerification? Current { get; }

    public static DeploymentOperatingMarketFreightServiceProviderRegistry FromLegacyDeployment(
        IOperatingMarketDeployment deployment)
    {
        ArgumentNullException.ThrowIfNull(deployment);

        return new DeploymentOperatingMarketFreightServiceProviderRegistry(
            deployment.MarketCode,
            new OperatingMarketFreightServiceProviderOptions
            {
                ParticipantId = deployment.VerifiedLicensedBrokerPartnerId
            });
    }

    private static bool HasConfiguration(OperatingMarketFreightServiceProviderOptions options)
        => !string.IsNullOrWhiteSpace(options.ParticipantId)
           || !string.IsNullOrWhiteSpace(options.ParticipantRoleCode)
           || !string.IsNullOrWhiteSpace(options.AuthorityReference)
           || options.VerifiedAtUtc.HasValue
           || options.VerificationExpiresAtUtc.HasValue
           || (options.SatisfiedRequirementCodes ?? [])
               .Any(value => !string.IsNullOrWhiteSpace(value));

    private static string Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
}
