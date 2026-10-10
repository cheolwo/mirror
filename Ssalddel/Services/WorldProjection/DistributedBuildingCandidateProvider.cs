using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Ssalddel.Application.Warehouse;
using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Domain.PublicData.Korea;

namespace Ssalddel.Services.WorldProjection;

public sealed class DistributedBuildingCandidateOptions
{
    public const string Section = "DistributedBuildingCandidates";
    public bool Enabled { get; set; }
    public string BundlePath { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public string MapRevision { get; set; } = "sagajeong-reference.r3";
    public string Locality { get; set; } = "서울특별시 중랑구";
}

/// <summary>기존 동결 OSM 도형의 도로명·건물번호를 주소 후보로만 읽습니다. 입주·소유권·출입구는 확정하지 않습니다.</summary>
public sealed class DistributedBuildingCandidateProvider(IOptions<DistributedBuildingCandidateOptions> options)
    : IDistributedBuildingCandidateProvider
{
    private readonly DistributedBuildingCandidateOptions settings = options.Value;
    private static readonly Regex BuildingId = new(@"^osm:(way|relation):[1-9][0-9]{0,18}$", RegexOptions.CultureInvariant);

    public async Task<IReadOnlyList<DistributedBuildingCandidateDto>> FindAsync(long warehouseId, string confirmedWarehouseAddress, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!settings.Enabled) return [];
        if (warehouseId <= 0 || string.IsNullOrWhiteSpace(confirmedWarehouseAddress)) return [];
        try
        {
            // 참고 번들의 적용 지역 밖 공간은 이 파일의 가용성에 의존하지 않습니다.
            var addressKey = StreetAddressKey(confirmedWarehouseAddress);
            if (addressKey is null) return [];
            return await ReadCandidatesAsync(addressKey, ct);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or ArgumentException)
        { throw Unavailable(); }
    }

    private async Task<IReadOnlyList<DistributedBuildingCandidateDto>> ReadCandidatesAsync(string addressKey, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(settings.Sha256) || settings.Sha256.Length != 64 || !settings.Sha256.All(Uri.IsHexDigit)
            || string.IsNullOrWhiteSpace(settings.BundlePath) || !Path.IsPathFullyQualified(settings.BundlePath)
            || string.IsNullOrWhiteSpace(settings.Locality)) throw Unavailable();
        var file = new FileInfo(settings.BundlePath);
        for (var parent = file.Directory; parent is not null; parent = parent.Parent)
            if (parent.Exists && parent.Attributes.HasFlag(FileAttributes.ReparsePoint)) throw Unavailable();
        if (!file.Exists || file.Length > 16 * 1024 * 1024 || file.Attributes.HasFlag(FileAttributes.ReparsePoint)) throw Unavailable();
        var bytes = await File.ReadAllBytesAsync(file.FullName, ct);
        if (bytes.Length > 16 * 1024 * 1024) throw Unavailable();
        if (!Convert.ToHexString(SHA256.HashData(bytes)).Equals(settings.Sha256, StringComparison.OrdinalIgnoreCase)) throw Unavailable();
        using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 32 });
        var root = document.RootElement;
        if (!root.TryGetProperty("revision", out var revision) || revision.GetString() != settings.MapRevision
            || !root.TryGetProperty("attribution", out var attribution) || string.IsNullOrWhiteSpace(attribution.GetString())
            || !root.TryGetProperty("buildings", out var buildings) || buildings.ValueKind != JsonValueKind.Array
            || buildings.GetArrayLength() > 20_000) throw Unavailable();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var candidates = new List<DistributedBuildingCandidateDto>();
        foreach (var building in buildings.EnumerateArray())
        {
            ct.ThrowIfCancellationRequested();
            if (!building.TryGetProperty("id", out var idValue) || idValue.ValueKind != JsonValueKind.String) throw Unavailable();
            var id = idValue.GetString()!;
            if (!BuildingId.IsMatch(id) || !ids.Add(id)) throw Unavailable();
            if (!building.TryGetProperty("street", out var streetValue) || !building.TryGetProperty("houseNumber", out var numberValue)) continue;
            var street = streetValue.GetString(); var number = numberValue.GetString();
            if (string.IsNullOrWhiteSpace(street) || string.IsNullOrWhiteSpace(number)) continue;
            var roadAddress = settings.Locality + " " + street + " " + number;
            if (StreetAddressKey(roadAddress) != addressKey) continue;
            candidates.Add(new(id, "osm-map:" + settings.Sha256.ToLowerInvariant() + "#" + id,
                street + " " + number + " · 건물 주소 후보"));
        }
        return candidates.OrderBy(item => item.PlaceId, StringComparer.Ordinal).ToArray();
    }

    private string? StreetAddressKey(string address)
    {
        var normalized = 공개사업장주소정규화Engine.NormalizeRoadAddress(address);
        var locality = 공개사업장주소정규화Engine.NormalizeRoadAddress(settings.Locality) ?? throw Unavailable();
        // 같은 지역의 명시적인 도로명만 비교하며 다른 구의 같은 도로번호는 자동 연결하지 않습니다.
        if (normalized is null || !normalized.StartsWith(locality + " ", StringComparison.Ordinal)) return null;
        return string.Concat(normalized[locality.Length..].Where(character => !char.IsWhiteSpace(character)));
    }
    private static DistributedFulfillmentException Unavailable() => new("DistributedBuildingCandidatesUnavailable", 503);
}

public static class DistributedBuildingCandidateRegistration
{
    public static IServiceCollection AddDistributedBuildingCandidates(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<DistributedBuildingCandidateOptions>(configuration.GetSection(DistributedBuildingCandidateOptions.Section));
        services.AddScoped<IDistributedBuildingCandidateProvider, DistributedBuildingCandidateProvider>();
        return services;
    }
}
