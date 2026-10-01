using Hongdal.Application.Operations;
using Hongdal.Contracts.Common.Operations;

namespace Hongdal.Services.Operations;

public sealed class OperatingMarketDeploymentOptions
{
    public const string SectionName = "OperatingMarket";

    public string MarketCode { get; set; } = OperatingMarketCodes.Korea;

    public string? TimeZoneId { get; set; }

    // Legacy flat configuration. A partner ID by itself no longer passes compliance checks.
    public string? VerifiedLicensedBrokerPartnerId { get; set; }

    public OperatingMarketFreightServiceProviderOptions FreightServiceProvider { get; set; } = new();
}

public sealed class OperatingMarketFreightServiceProviderOptions
{
    public string? ParticipantId { get; set; }

    public string? ParticipantRoleCode { get; set; }

    public string? AuthorityReference { get; set; }

    public DateTimeOffset? VerifiedAtUtc { get; set; }

    public DateTimeOffset? VerificationExpiresAtUtc { get; set; }

    public string[] SatisfiedRequirementCodes { get; set; } = [];
}

public interface IOperatingMarketDeployment
{
    string MarketCode { get; }

    OperatingMarketProfile Profile { get; }

    string TimeZoneId { get; }

    string? VerifiedLicensedBrokerPartnerId { get; }
}

public sealed class OperatingMarketDeployment : IOperatingMarketDeployment
{
    public OperatingMarketDeployment(
        string? marketCode,
        string? verifiedLicensedBrokerPartnerId = null,
        string? timeZoneId = null)
    {
        if (!OperatingMarketCodes.TryNormalize(marketCode, out var normalizedMarketCode))
        {
            throw new ArgumentOutOfRangeException(
                nameof(marketCode),
                marketCode,
                "The deployment market must be KR or US.");
        }

        MarketCode = normalizedMarketCode;
        Profile = OperatingMarketProfileCatalog.Get(normalizedMarketCode);
        TimeZoneId = ResolveTimeZoneId(normalizedMarketCode, timeZoneId);
        VerifiedLicensedBrokerPartnerId = normalizedMarketCode == OperatingMarketCodes.UnitedStates
            ? NullIfWhiteSpace(verifiedLicensedBrokerPartnerId)
            : null;
    }

    public string MarketCode { get; }

    public OperatingMarketProfile Profile { get; }

    public string TimeZoneId { get; }

    public string? VerifiedLicensedBrokerPartnerId { get; }

    private static string? NullIfWhiteSpace(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string ResolveTimeZoneId(string marketCode, string? configuredTimeZoneId)
    {
        var candidate = string.IsNullOrWhiteSpace(configuredTimeZoneId)
            ? marketCode == OperatingMarketCodes.Korea
                ? OperatingTimeZoneIds.Korea
                : OperatingTimeZoneIds.CoordinatedUniversal
            : configuredTimeZoneId.Trim();

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(candidate).Id;
        }
        catch (TimeZoneNotFoundException ex)
        {
            throw new ArgumentException(
                $"Unknown deployment time zone: {candidate}.",
                nameof(configuredTimeZoneId),
                ex);
        }
        catch (InvalidTimeZoneException ex)
        {
            throw new ArgumentException(
                $"Invalid deployment time zone: {candidate}.",
                nameof(configuredTimeZoneId),
                ex);
        }
    }
}

public sealed class DeploymentOperatingMarketContextAccessor : IOperatingMarketContextAccessor
{
    private readonly OperatingMarketContextSnapshot _current;

    public DeploymentOperatingMarketContextAccessor(IOperatingMarketDeployment deployment)
    {
        ArgumentNullException.ThrowIfNull(deployment);
        _current = new OperatingMarketContextSnapshot(
            deployment.MarketCode,
            OperatingMarketContextSourceCodes.Deployment,
            deployment.TimeZoneId);
    }

    public OperatingMarketContextSnapshot Current => _current;
}
