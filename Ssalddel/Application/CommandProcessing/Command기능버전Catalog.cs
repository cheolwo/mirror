using Ssalddel.Contracts.Common.Versioning;

namespace Ssalddel.Application.CommandProcessing;

public sealed record Command기능버전정보(
    string Version,
    string DisplayName,
    int SortOrder,
    bool IsCurrentRelease);

public static class Command기능버전Catalog
{
    public const string CurrentRelease = SsalddelProductRoadmapCatalog.CurrentVersion;
    [Obsolete("단일 완성 목표는 UnifiedDeploymentTarget을 사용하세요. Get은 기존 Command 분류를 유지합니다.")]
    public const string DeploymentTarget = SsalddelProductRoadmapCatalog.UnifiedReleaseVersion;
    public static SsalddelUnifiedReleaseTarget UnifiedDeploymentTarget => SsalddelProductRoadmapCatalog.UnifiedReleaseTarget;

    private static readonly IReadOnlyDictionary<string, Command기능버전정보> Versions =
        SsalddelProductRoadmapCatalog.All.ToDictionary(
            stage => stage.Version,
            stage => new Command기능버전정보(
                stage.Version,
                stage.FullDisplayName,
                stage.SortOrder,
                stage.IsCurrent),
            StringComparer.OrdinalIgnoreCase);

    public static Command기능버전정보 Get(string? version)
    {
        var key = string.IsNullOrWhiteSpace(version) ? CurrentRelease : version.Trim();
        return Versions.TryGetValue(key, out var info)
            ? info
            : new Command기능버전정보(key, $"{key} 확장 기능", 900, false);
    }
}
