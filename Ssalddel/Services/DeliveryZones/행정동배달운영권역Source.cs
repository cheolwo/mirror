using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common.DeliveryZones;
using Ssalddel.Infrastructure.Persistence.PublicData;
using 살뜰.Services.External.PublicData.Korea;

namespace 살뜰.Services.DeliveryZones;

public sealed record 배달운영권역법정동SourceItem(
    string LegalAreaStableId,
    string DisplayName);

public sealed record 배달운영권역행정동SourceItem(
    string AdministrativeAreaStableId,
    string DisplayName,
    IReadOnlyList<배달운영권역법정동SourceItem> LegalAreas,
    string SourceId,
    string DatasetId,
    string SourceVersion,
    string DataRevision,
    DateTimeOffset EvidenceAsOfUtc);

public interface I행정동배달운영권역Source
{
    Task<IReadOnlyList<배달운영권역행정동SourceItem>> 조회Async(
        string sourceScopeStableId,
        CancellationToken cancellationToken = default);
}

public static class 행정동배달운영권역SourceScopeCatalog
{
    public const int NortheastSeoulRiderR1ExpectedAdministrativeDongCount = 30;
    public const string NortheastSeoulRiderR1SourceVersion =
        "mois-jscode:20260301:retrieved:2026-08-12";
    public const string NortheastSeoulRiderR1DataRevision =
        "mois-hjd-bjd-20260301-8af8c1f122d67d43518f";

    public static IReadOnlyDictionary<string, string> 법정동목록(string sourceScopeStableId)
    {
        if (!string.Equals(
                sourceScopeStableId?.Trim(),
                배달운영권역SourceScopes.NortheastSeoulRiderR1,
                StringComparison.Ordinal))
        {
            throw new ArgumentException("DeliveryTerritorySourceScopeUnsupported", nameof(sourceScopeStableId));
        }

        return NortheastSeoulRiderR1LegalAreas;
    }

    private static readonly IReadOnlyDictionary<string, string> NortheastSeoulRiderR1LegalAreas =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["region:kr:bjd:1121510100"] = "서울특별시 광진구 중곡동",
            ["region:kr:bjd:1123010400"] = "서울특별시 동대문구 전농동",
            ["region:kr:bjd:1123010500"] = "서울특별시 동대문구 답십리동",
            ["region:kr:bjd:1123010600"] = "서울특별시 동대문구 장안동",
            ["region:kr:bjd:1123010900"] = "서울특별시 동대문구 휘경동",
            ["region:kr:bjd:1123011000"] = "서울특별시 동대문구 이문동",
            ["region:kr:bjd:1126010100"] = "서울특별시 중랑구 면목동",
            ["region:kr:bjd:1126010200"] = "서울특별시 중랑구 상봉동",
            ["region:kr:bjd:1126010300"] = "서울특별시 중랑구 중화동",
            ["region:kr:bjd:1126010400"] = "서울특별시 중랑구 묵동",
            ["region:kr:bjd:1126010500"] = "서울특별시 중랑구 망우동",
            ["region:kr:bjd:1126010600"] = "서울특별시 중랑구 신내동"
        };
}

public sealed class Official행정동배달운영권역Source(
    PublicDataIngestionDbContext db) : I행정동배달운영권역Source
{
    public async Task<IReadOnlyList<배달운영권역행정동SourceItem>> 조회Async(
        string sourceScopeStableId,
        CancellationToken cancellationToken = default)
    {
        var scope = 행정동배달운영권역SourceScopeCatalog.법정동목록(sourceScopeStableId);
        var frozenRows = await db.NormalizedRecords
            .AsNoTracking()
            .Where(x => x.SourceId == 대한민국행정동관할CodeDataset.SourceId
                        && x.DatasetId == 대한민국행정동관할CodeDataset.DatasetId
                        && x.DataRevision
                        == 행정동배달운영권역SourceScopeCatalog.NortheastSeoulRiderR1DataRevision)
            .Select(x => new
            {
                x.DataRevision,
                x.SourceVersion,
                x.EvidenceAsOfUtc
            })
            .ToListAsync(cancellationToken);
        if (frozenRows.Count == 0)
            throw new 배달운영권역SourceUnavailableException(
                "AdministrativeDongReferenceUnavailable");
        var frozenSourceVersions = frozenRows
            .Select(x => x.SourceVersion)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (frozenSourceVersions.Length != 1
            || !string.Equals(
                frozenSourceVersions[0],
                행정동배달운영권역SourceScopeCatalog.NortheastSeoulRiderR1SourceVersion,
                StringComparison.Ordinal))
        {
            throw new 배달운영권역SourceUnavailableException(
                "AdministrativeDongReferenceFrozenVintageMismatch");
        }

        var relationRows = await db.NormalizedRecords
            .AsNoTracking()
            .Where(x => x.SourceId == 대한민국행정동관할CodeDataset.SourceId
                        && x.DatasetId == 대한민국행정동관할CodeDataset.DatasetId
                        && x.DataRevision
                        == 행정동배달운영권역SourceScopeCatalog.NortheastSeoulRiderR1DataRevision
                        && x.MetricCode == 대한민국행정동관할CodeDataset.JurisdictionMetricCode)
            .ToListAsync(cancellationToken);

        var parsedRelations = relationRows
            .Where(x => HasDimensionValue(x.DimensionKey, "status", "active"))
            .Select(x => TryParseRelation(x.StableId, out var administrativeId, out var legalId)
                ? new { Row = x, AdministrativeId = administrativeId, LegalId = legalId }
                : null)
            .Where(x => x is not null && scope.ContainsKey(x.LegalId))
            .Select(x => x!)
            .GroupBy(x => new { x.AdministrativeId, x.LegalId })
            .Select(group => group.Single())
            .ToArray();

        if (parsedRelations.Select(x => x.LegalId).Distinct(StringComparer.Ordinal).Count() != scope.Count)
            throw new 배달운영권역SourceUnavailableException(
                "AdministrativeDongReferenceLegalAreaCoverageMismatch");

        var administrativeIds = parsedRelations
            .Select(x => x.AdministrativeId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();
        if (administrativeIds.Length
            != 행정동배달운영권역SourceScopeCatalog.NortheastSeoulRiderR1ExpectedAdministrativeDongCount)
        {
            throw new 배달운영권역SourceUnavailableException(
                "AdministrativeDongReferenceCountMismatch");
        }

        var administrativeRows = await db.NormalizedRecords
            .AsNoTracking()
            .Where(x => x.SourceId == 대한민국행정동관할CodeDataset.SourceId
                        && x.DatasetId == 대한민국행정동관할CodeDataset.DatasetId
                        && x.DataRevision
                        == 행정동배달운영권역SourceScopeCatalog.NortheastSeoulRiderR1DataRevision
                        && x.MetricCode == 대한민국행정동관할CodeDataset.AdministrativeMetricCode)
            .ToListAsync(cancellationToken);
        var names = administrativeRows
            .Where(x => administrativeIds.Contains(x.RegionStableId, StringComparer.Ordinal))
            .Where(x => HasDimensionValue(x.DimensionKey, "status", "active"))
            .GroupBy(x => x.RegionStableId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Single().TextValue,
                StringComparer.Ordinal);
        if (names.Count != administrativeIds.Length
            || names.Any(x => string.IsNullOrWhiteSpace(x.Value)))
        {
            throw new 배달운영권역SourceUnavailableException(
                "AdministrativeDongReferenceNameCoverageMismatch");
        }

        var sourceVersions = parsedRelations
            .Select(x => x.Row.SourceVersion)
            .Append(frozenSourceVersions[0])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (sourceVersions.Length > 1)
            throw new 배달운영권역SourceUnavailableException(
                "AdministrativeDongReferenceMixedSourceVersion");

        return administrativeIds.Select(administrativeId =>
            new 배달운영권역행정동SourceItem(
                administrativeId,
                names[administrativeId],
                parsedRelations
                    .Where(x => string.Equals(x.AdministrativeId, administrativeId, StringComparison.Ordinal))
                    .Select(x => new 배달운영권역법정동SourceItem(
                        x.LegalId,
                        scope[x.LegalId]))
                    .OrderBy(x => x.LegalAreaStableId, StringComparer.Ordinal)
                    .ToArray(),
                대한민국행정동관할CodeDataset.SourceId,
                대한민국행정동관할CodeDataset.DatasetId,
                sourceVersions.SingleOrDefault() ?? string.Empty,
                행정동배달운영권역SourceScopeCatalog.NortheastSeoulRiderR1DataRevision,
                parsedRelations
                    .Where(x => string.Equals(x.AdministrativeId, administrativeId, StringComparison.Ordinal))
                    .Max(x => x.Row.EvidenceAsOfUtc)))
            .ToArray();
    }

    private static bool TryParseRelation(
        string stableId,
        out string administrativeAreaStableId,
        out string legalAreaStableId)
    {
        const string prefix = "region:kr:hjd-bjd:";
        administrativeAreaStableId = string.Empty;
        legalAreaStableId = string.Empty;
        if (string.IsNullOrWhiteSpace(stableId)
            || !stableId.StartsWith(prefix, StringComparison.Ordinal))
            return false;

        var parts = stableId[prefix.Length..].Split('-', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2
            || parts[0].Length != 10
            || parts[1].Length != 10
            || !parts[0].All(char.IsAsciiDigit)
            || !parts[1].All(char.IsAsciiDigit))
            return false;

        administrativeAreaStableId = $"region:kr:hjd:{parts[0]}";
        legalAreaStableId = $"region:kr:bjd:{parts[1]}";
        return true;
    }

    private static bool HasDimensionValue(string dimensionKey, string key, string expected)
        => (dimensionKey ?? string.Empty)
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(part => string.Equals(part, $"{key}={expected}", StringComparison.Ordinal));
}

public sealed class 배달운영권역SourceUnavailableException(string message)
    : Exception(message);
