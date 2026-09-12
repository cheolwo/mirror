using System.Security.Cryptography;
using System.Text;
using Ssalddel.Services.WorldProjection.AdministrativeDongDiorama;
using Ssalddel.WorkflowRules.Contracts;
using 살뜰.Services.Versioning;

namespace Ssalddel.Application.WorldProjection;

public interface I행정동디오라마조회UseCase
{
    Task<AdministrativeDongDioramaManifest?> ManifestAsync(string administrativeAreaStableId, CancellationToken cancellationToken);
    Task<AdministrativeDongDioramaTile?> TileAsync(string administrativeAreaStableId, string tileStableId, CancellationToken cancellationToken);
    Task<AdministrativeDongDisplayOverlayResponse?> DisplayOverlaysAsync(string administrativeAreaStableId, DateTime asOfUtc, CancellationToken cancellationToken);
}

public sealed class 행정동디오라마조회UseCase(
    I행정동디오라마ProjectionStore store,
    I행정동디오라마DisplayOverlaySource displayOverlaySource,
    IVersionFeatureFlagService featureFlags) : I행정동디오라마조회UseCase
{
    public Task<AdministrativeDongDioramaManifest?> ManifestAsync(
        string administrativeAreaStableId,
        CancellationToken cancellationToken)
        => store.FindManifestAsync(ValidateArea(administrativeAreaStableId), cancellationToken);

    public async Task<AdministrativeDongDioramaTile?> TileAsync(
        string administrativeAreaStableId,
        string tileStableId,
        CancellationToken cancellationToken)
    {
        var area = ValidateArea(administrativeAreaStableId);
        if (string.IsNullOrWhiteSpace(tileStableId))
            throw new ArgumentException("AdministrativeDongDioramaTileStableIdRequired", nameof(tileStableId));
        var manifest = await store.FindManifestAsync(area, cancellationToken);
        if (manifest is null || !manifest.Tiles.Any(x => string.Equals(x.TileStableId, tileStableId, StringComparison.Ordinal)))
            return null;
        return await store.FindTileAsync(area, tileStableId.Trim(), cancellationToken);
    }

    public async Task<AdministrativeDongDisplayOverlayResponse?> DisplayOverlaysAsync(
        string administrativeAreaStableId,
        DateTime asOfUtc,
        CancellationToken cancellationToken)
    {
        var area = ValidateArea(administrativeAreaStableId);
        var stored = await store.FindDisplayOverlaysAsync(area, cancellationToken);
        if (stored is null) return null;
        var dynamicItems = featureFlags.IsEnabled(VersionFeatureFlagKeys.LocalDioramaSponsorship)
            ? await displayOverlaySource.FindActiveAsync(area, asOfUtc, cancellationToken)
            : [];
        var items = stored.Items.Concat(dynamicItems)
            .Where(item => IsSafe(item, asOfUtc))
            .GroupBy(item => item.OverlayStableId, StringComparer.Ordinal)
            .Select(group => group.Single())
            .OrderBy(item => item.OverlayKindCode, StringComparer.Ordinal)
            .ThenBy(item => item.OverlayStableId, StringComparer.Ordinal)
            .ToArray();
        return new AdministrativeDongDisplayOverlayResponse
        {
            AdministrativeAreaStableId = area,
            AsOfUtc = asOfUtc,
            Items = items,
            OverlayRevision = Hash(items)
        };
    }

    private static bool IsSafe(AdministrativeDongDisplayOverlay item, DateTime asOfUtc)
    {
        if (string.IsNullOrWhiteSpace(item.OverlayStableId)
            || string.IsNullOrWhiteSpace(item.DisplayLabel)
            || string.IsNullOrWhiteSpace(item.SemanticPlaceStableId)
            || item.StartsAtUtc > asOfUtc
            || item.EndsAtUtc <= asOfUtc)
            return false;
        if (string.Equals(item.OverlayKindCode, AdministrativeDongDisplayOverlayKinds.PublicBusiness, StringComparison.Ordinal))
            return !item.AdvertisementDisclosureRequired;
        if (string.Equals(item.OverlayKindCode, AdministrativeDongDisplayOverlayKinds.Sponsorship, StringComparison.Ordinal))
            return item.AdvertisementDisclosureRequired
                   && (item.BadgeText.Contains("광고", StringComparison.Ordinal)
                       || item.BadgeText.Contains("후원", StringComparison.Ordinal));
        return string.Equals(item.OverlayKindCode, AdministrativeDongDisplayOverlayKinds.UnresolvedOperationalCount, StringComparison.Ordinal);
    }

    private static string ValidateArea(string value)
    {
        if (!AdministrativeDongDioramaPolicy.IsAdministrativeAreaStableId(value))
            throw new ArgumentException("AdministrativeDongStableIdInvalid", nameof(value));
        return value.Trim();
    }

    private static string Hash(IEnumerable<AdministrativeDongDisplayOverlay> items)
    {
        var canonical = string.Join("\n", items.Select(item => string.Join("|",
            item.OverlayStableId,
            item.OverlayKindCode,
            item.DisplayLabel,
            item.CategoryCode,
            item.SemanticPlaceStableId,
            item.BadgeText,
            item.DetailCardText,
            item.AdvertisementDisclosureRequired,
            item.StartsAtUtc?.ToUniversalTime().ToString("O") ?? string.Empty,
            item.EndsAtUtc?.ToUniversalTime().ToString("O") ?? string.Empty)));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }
}
