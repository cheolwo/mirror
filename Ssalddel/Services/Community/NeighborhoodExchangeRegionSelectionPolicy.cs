using Ssalddel.Contracts.Common.Community;

namespace Ssalddel.Services.Community;

/// <summary>글의 공개 위치는 사용자가 선택한 검증된 행정동 키만 저장합니다.</summary>
public static class NeighborhoodExchangeRegionSelectionPolicy
{
    private const string AdministrativeRegionPrefix = "region:kr:hjd:";
    public static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static async Task<string?> ValidateAsync(string? regionKey, string category,
        string? workflowTag, string? roleTag, bool isReport, I생활교류공개지역Source? source,
        CancellationToken cancellationToken)
    {
        var normalized = Normalize(regionKey);
        if (normalized is null) return null;
        if (category != PlatformCommunityPostCategories.General
            || workflowTag?.Trim() != NeighborhoodExchange.WorkflowTag
            || !NeighborhoodExchange.IsIntent(roleTag?.Trim()) || isReport)
            return "공개 동네는 제공·필요 생활 교류 글에만 지정할 수 있습니다.";
        if (normalized.Length != AdministrativeRegionPrefix.Length + 10
            || !normalized.StartsWith(AdministrativeRegionPrefix, StringComparison.Ordinal)
            || normalized[AdministrativeRegionPrefix.Length..].Any(character => character is < '0' or > '9'))
            return "지원하는 공개 동네를 선택해 주세요.";
        IReadOnlyList<NeighborhoodPublicRegionDto> regions = source is null ? [] : await source.목록Async(cancellationToken);
        return regions.Any(region => region.RegionKey == normalized)
            ? null : "대표점과 공식 코드가 검증된 공개 동네만 선택할 수 있습니다.";
    }
}
