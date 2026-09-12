using Ssalddel.WorkflowRules.Contracts;

namespace Ssalddel.Services.WorldProjection.AdministrativeDongDiorama;

public sealed record 행정동공간Boundary(
    string AdministrativeAreaStableId,
    AdministrativeDongDioramaPoint[] Boundary);

public sealed record 행정동공간BuildingAssignment(
    string BuildingStableId,
    string? AdministrativeAreaStableId,
    string AssignmentMethodCode,
    string ConfidenceCode);

public sealed record 행정동공간AssignmentResult(
    IReadOnlyList<행정동공간BuildingAssignment> Assignments,
    int AssignedCount,
    int UnresolvedCount,
    int AmbiguousBoundaryCount);

/// <summary>
/// 건물 윤곽 내부의 대표점을 기준으로 행정동 하나에만 귀속합니다.
/// 경계가 겹치거나 어느 경계에도 속하지 않으면 추정하지 않고 Unresolved로 남깁니다.
/// </summary>
public static class 행정동공간귀속Classifier
{
    public static 행정동공간AssignmentResult AssignBuildings(
        IEnumerable<행정동디오라마BuildingInput> buildings,
        IEnumerable<행정동공간Boundary> boundaries)
    {
        var areas = boundaries.OrderBy(item => item.AdministrativeAreaStableId, StringComparer.Ordinal).ToArray();
        if (areas.Length == 0 || areas.Any(item => !AdministrativeDongDioramaPolicy.IsAdministrativeAreaStableId(item.AdministrativeAreaStableId)
                                                  || item.Boundary.Length < 3))
            throw new ArgumentException("AdministrativeBoundarySetInvalid", nameof(boundaries));
        var assignments = new List<행정동공간BuildingAssignment>();
        var ambiguous = 0;
        foreach (var building in buildings.OrderBy(item => item.BuildingStableId, StringComparer.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(building.BuildingStableId) || building.Footprint.Length < 3)
            {
                assignments.Add(new(building.BuildingStableId, null, "PointOnSurface", "UnresolvedInvalidFootprint"));
                continue;
            }
            var point = 행정동디오라마ProjectionBuilder.PointOnSurface(building.Footprint);
            var matches = areas.Where(area => 행정동디오라마ProjectionBuilder.PointInPolygon(point, area.Boundary)).ToArray();
            if (matches.Length == 1)
                assignments.Add(new(building.BuildingStableId, matches[0].AdministrativeAreaStableId,
                    "PointOnSurface", "UniqueAdministrativeBoundary"));
            else
            {
                if (matches.Length > 1) ambiguous++;
                assignments.Add(new(building.BuildingStableId, null, "PointOnSurface",
                    matches.Length == 0 ? "UnresolvedOutsideBoundarySet" : "UnresolvedOverlappingBoundaries"));
            }
        }
        var assigned = assignments.Count(item => item.AdministrativeAreaStableId is not null);
        return new(assignments, assigned, assignments.Count - assigned, ambiguous);
    }
}
