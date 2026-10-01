#!/usr/bin/env python3
"""Build flat, private contour-trace candidates from the 2025 OA-22241 SHP.

Only the EPSG:5174 polyline geometry and frozen file identity are consumed.  The
DBF is deliberately not opened, so CONT/HEIGHT values cannot affect identity,
clipping, output, or review semantics.  Every result remains a historical,
private, display-only candidate; it is not elevation, terrain, collider,
runtime, traversal, gameplay, or current-state authority.
"""

from __future__ import annotations

import argparse
import hashlib
import importlib.util
import io
import json
import math
import os
import re
import shutil
import stat
import sys
import uuid
import zipfile
from collections import Counter, defaultdict
from pathlib import Path
from typing import Any, Iterable


ROOT = Path(__file__).resolve().parents[2]
SOURCE = Path(
    "artifacts/local/public-data/admin-dong-terrain-contour-20260926-r1/raw/"
    "OA-22241-seoul-contour.zip"
)
SOURCE_RECEIPT = Path(
    "artifacts/local/public-data/admin-dong-terrain-contour-20260926-r1/raw/receipt.json"
)
BOUNDARY = Path(
    "artifacts/local/public-data/admin-dong/20260912-seoul-oa22160/"
    "seoul-administrative-dong-boundary.zip"
)
SCOPE = Path(
    "eng/world-seedbeds/administrative-dong-dioramas/"
    "northeast-seoul-rider.r2.json"
)
BATCH = Path("eng/neighborhood/administrative_dong_diorama_batch.py")
OUTPUT = Path(
    "artifacts/local/validation/admin-dong-contour-trace-private-review/r1/input"
)

SOURCE_SHA = "4FBE3C7E061B5974E7403EC116855304ED8AE321EEBCC0D12C31CA8FB7BE30BF"
SOURCE_RECEIPT_SHA = "8911948581B53B07BF59BDE353EA2743948A9733B6EA24F341FB4364496A345D"
SOURCE_BYTES = 45_852_601
SOURCE_RECORDS = 8_570
SOURCE_VINTAGE = "file-2025-03-20"
BOUNDARY_SHA = "969F7033BD3609A5FD586790F5B2CFEDC638D7647EF45C78F9C75E1DABF79F68"
SCOPE_SHA = "CAAFC2BC60AE4EBF3A0B8C1D2AD6B0143141FF706637BECC9B6743924AF9E58B"
AREA_COUNT = 30
EXPECTED_SCOPE_FEATURES = 377
EXPECTED_AREA_INTERSECTIONS = 860
MIN_RENDERABLE_SEGMENT_MM = 1.0
FIXED_AT = "2026-09-26T00:00:00Z"


class ContourTraceError(RuntimeError):
    pass


def require(value: bool, code: str) -> None:
    if not value:
        raise ContourTraceError(code)


def safe(root: Path, relative: Path) -> Path:
    value = (root / relative).resolve()
    require(value == root or root in value.parents, "PathEscapesRepository")
    return value


def is_reparse(path: Path) -> bool:
    try:
        value = path.lstat()
    except OSError:
        return False
    flag = getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0x400)
    return path.is_symlink() or bool(getattr(value, "st_file_attributes", 0) & flag)


def sha(payload: bytes) -> str:
    return hashlib.sha256(payload).hexdigest().upper()


def sha_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest().upper()


def canonical(value: Any) -> bytes:
    return (
        json.dumps(
            value,
            ensure_ascii=False,
            sort_keys=True,
            separators=(",", ":"),
            allow_nan=False,
        )
        + "\n"
    ).encode("utf-8")


def pretty(value: Any) -> bytes:
    return (
        json.dumps(value, ensure_ascii=False, indent=2, allow_nan=False) + "\n"
    ).encode("utf-8")


def checked(path: Path, digest: str, length: int | None = None) -> Path:
    require(path.is_file() and not is_reparse(path), "InputMissingOrUnsafe:" + path.name)
    require(length is None or path.stat().st_size == length, "InputLengthChanged:" + path.name)
    require(sha_file(path) == digest, "InputHashChanged:" + path.name)
    return path


def load_module(path: Path, name: str) -> Any:
    spec = importlib.util.spec_from_file_location(name, path)
    require(spec is not None and spec.loader is not None, "ModuleLoadFailed:" + path.name)
    module = importlib.util.module_from_spec(spec)
    sys.modules[name] = module
    spec.loader.exec_module(module)
    return module


def authority() -> dict[str, bool]:
    return {
        "privateReviewOnly": True,
        "historicalBoundaryBootstrapOnly": True,
        "reviewVisualizationOnly": True,
        "privateReviewVisualizationAllowed": True,
        "sourceHorizontalGeometryObserved": True,
        "sourceAttributeDatabaseRead": False,
        "sourceElevationValuesRead": False,
        "sourceElevationValuesSerialized": False,
        "currentBoundaryEstablished": False,
        "currentTerrainEstablished": False,
        "contourMeaningAuthorized": False,
        "elevationMeaningAuthorized": False,
        "heightUnitVerified": False,
        "verticalDatumVerified": False,
        "terrainSurfaceAuthorized": False,
        "terrainMeshAuthorized": False,
        "colliderAuthorized": False,
        "publicDisplayAllowed": False,
        "databaseWriteAllowed": False,
        "mongoWriteAllowed": False,
        "sourceCandidateUnityApplyAllowed": False,
        "runtimeAuthorized": False,
        "traversalReady": False,
        "gameplayReady": False,
        "currentPointerUsed": False,
        "currentPointerUpdated": False,
    }


def load_context(root: Path) -> tuple[Any, Any, dict[str, Any], Any, list[Any]]:
    for path in (
        safe(root, Path("artifacts/local/public-data/gis-runtime-r1")),
        safe(root, Path("artifacts/local/python-packages/spatial")),
        safe(root, Path("eng/neighborhood")),
    ):
        if path.is_dir() and str(path) not in sys.path:
            sys.path.insert(0, str(path))
    batch_path = checked(safe(root, BATCH), sha_file(safe(root, BATCH)))
    batch = load_module(batch_path, "hongdal_contour_trace_boundary_batch")
    checked(safe(root, SCOPE), SCOPE_SHA)
    checked(safe(root, BOUNDARY), BOUNDARY_SHA)
    scope = batch.load_scope(root, safe(root, SCOPE))
    dependencies = batch.load_dependencies(root)
    frame = batch.frame_from_scope(scope)
    boundaries, boundary_audit = batch.read_boundaries(root, scope, frame, dependencies)
    require(
        len(boundaries) == AREA_COUNT
        and boundary_audit.get("historicalBootstrapOnly") is True
        and boundary_audit.get("currentAuthoritativeBoundaryAvailable") is False,
        "HistoricalScopeChanged",
    )
    return batch, dependencies, scope, frame, boundaries


def read_source_receipt(root: Path) -> dict[str, Any]:
    path = checked(safe(root, SOURCE_RECEIPT), SOURCE_RECEIPT_SHA)
    value = json.loads(path.read_text(encoding="utf-8"))
    require(
        value.get("schemaVersion") == "administrative-dong-terrain-source-receipt.v1"
        and value.get("datasetId") == "OA-22241"
        and value.get("revisionBoundary", {}).get("candidateSourceFile")
        == "OA-22241-seoul-contour.zip"
        and value.get("revisionBoundary", {}).get("doNotMergeDownloadRevisions") is True
        and value.get("coordinateReference", {}).get("horizontalCrs") == "EPSG:5174"
        and value.get("coordinateReference", {}).get("verticalDatumStatus") == "Unverified",
        "SourceReceiptContractChanged",
    )
    return value


def archive_entries(archive: zipfile.ZipFile, stem: str) -> dict[str, str]:
    result: dict[str, str] = {}
    for suffix in (".prj", ".shp", ".shx"):
        matches = [
            name
            for name in archive.namelist()
            if Path(name).name.lower() == (stem + suffix).lower()
        ]
        require(len(matches) == 1, "SourceArchiveEntryChanged:" + suffix)
        result[suffix] = matches[0]
    return result


def boundary_source_geometries(
    root: Path, dependencies: Any, boundaries: list[Any]
) -> list[tuple[Any, Any, Any]]:
    by_short = {
        boundary.stable_id.rsplit(":", 1)[-1][:-2]: boundary for boundary in boundaries
    }
    selected: dict[str, Any] = {}
    with zipfile.ZipFile(checked(safe(root, BOUNDARY), BOUNDARY_SHA)) as archive:
        entries: dict[str, str] = {}
        for suffix in (".dbf", ".prj", ".shp", ".shx"):
            matches = [name for name in archive.namelist() if name.lower().endswith(suffix)]
            require(len(matches) == 1, "BoundaryArchiveEntryChanged:" + suffix)
            entries[suffix] = matches[0]
        source_crs = dependencies.CRS.from_wkt(
            archive.read(entries[".prj"]).decode("utf-8-sig")
        )
        require(source_crs.to_authority() == ("EPSG", "5181"), "BoundaryCrsChanged")
        to_contour = dependencies.Transformer.from_crs(source_crs, 5174, always_xy=True)
        reader = dependencies.shapefile.Reader(
            shp=io.BytesIO(archive.read(entries[".shp"])),
            shx=io.BytesIO(archive.read(entries[".shx"])),
            dbf=io.BytesIO(archive.read(entries[".dbf"])),
            encoding="utf-8",
            encodingErrors="strict",
        )
        try:
            for item in reader.iterShapeRecords():
                short = str(item.record.as_dict().get("ADSTRD_CD", ""))
                if short not in by_short:
                    continue
                starts = list(item.shape.parts) + [len(item.shape.points)]
                rings = [item.shape.points[a:b] for a, b in zip(starts, starts[1:])]
                geometry = dependencies.rings_to_geometry(rings)
                geometry = dependencies.transform(to_contour.transform, geometry)
                require(
                    geometry.geom_type in {"Polygon", "MultiPolygon"}
                    and not geometry.is_empty
                    and geometry.is_valid,
                    "BoundaryGeometryChanged:" + short,
                )
                selected[short] = geometry
        finally:
            reader.close()
    require(set(selected) == set(by_short), "BoundaryCoverageChanged")
    return [
        (by_short[short], selected[short], by_short[short].geometry)
        for short in sorted(selected)
    ]


def line_members(geometry: Any) -> tuple[list[Any], int]:
    if geometry.is_empty:
        return [], 0
    if geometry.geom_type == "LineString":
        return ([geometry] if len(geometry.coords) >= 2 and geometry.length > 0 else []), 0
    if geometry.geom_type == "MultiLineString":
        return [
            member
            for member in geometry.geoms
            if len(member.coords) >= 2 and member.length > 0
        ], 0
    if geometry.geom_type == "GeometryCollection":
        lines: list[Any] = []
        non_line = 0
        for member in geometry.geoms:
            nested, count = line_members(member)
            lines.extend(nested)
            non_line += count
            if member.geom_type not in {
                "LineString",
                "MultiLineString",
                "GeometryCollection",
                "Point",
                "MultiPoint",
            }:
                non_line += 1
        return lines, non_line
    if geometry.geom_type in {"Point", "MultiPoint"}:
        return [], 0
    return [], 1


def source_parts(shape: Any, dependencies: Any) -> list[tuple[int, Any]]:
    starts = list(shape.parts) + [len(shape.points)]
    result: list[tuple[int, Any]] = []
    for ordinal, (start, end) in enumerate(zip(starts, starts[1:])):
        if end - start < 2:
            continue
        line = dependencies.LineString(shape.points[start:end])
        if not line.is_empty and line.length > 0:
            result.append((ordinal, line))
    require(bool(result), "SourceFeatureHasNoLinePart")
    return result


def bbox_intersects(first: Iterable[float], second: Iterable[float]) -> bool:
    a = list(first)
    b = list(second)
    return not (a[2] < b[0] or a[0] > b[2] or a[3] < b[1] or a[1] > b[3])


def to_common_path(
    line: Any, transformer: Any, frame: Any
) -> list[dict[str, int]]:
    xs, ys = zip(*line.coords)
    longitudes, latitudes = transformer.transform(xs, ys)
    return [
        {
            "x": round(frame.wgs84_to_local(float(lon), float(lat))[0] * 1000),
            "z": round(frame.wgs84_to_local(float(lon), float(lat))[1] * 1000),
        }
        for lon, lat in zip(longitudes, latitudes)
    ]


def segment_key(
    first: dict[str, int], second: dict[str, int]
) -> tuple[tuple[int, int], tuple[int, int]]:
    a = (first["x"], first["z"])
    b = (second["x"], second["z"])
    return (a, b) if a <= b else (b, a)


def boundary_ring_audit(boundaries: list[Any]) -> dict[str, int]:
    parts = exteriors = interiors = vertices = segments = 0
    for boundary in boundaries:
        polygons = (
            [boundary.geometry]
            if boundary.geometry.geom_type == "Polygon"
            else list(boundary.geometry.geoms)
        )
        for polygon in polygons:
            parts += 1
            exteriors += 1
            vertices += len(polygon.exterior.coords)
            segments += len(polygon.exterior.coords) - 1
            for ring in polygon.interiors:
                interiors += 1
                vertices += len(ring.coords)
                segments += len(ring.coords) - 1
    return {
        "historicalBoundaryPolygonPartCount": parts,
        "historicalBoundaryExteriorRingCount": exteriors,
        "historicalBoundaryInteriorRingCount": interiors,
        "historicalBoundaryRingCount": exteriors + interiors,
        "historicalBoundaryRingVertexCount": vertices,
        "historicalBoundaryRingSegmentCount": segments,
    }


def read_candidates(
    root: Path,
    dependencies: Any,
    frame: Any,
    boundaries: list[Any],
) -> tuple[list[dict[str, Any]], dict[str, Any]]:
    source = checked(safe(root, SOURCE), SOURCE_SHA, SOURCE_BYTES)
    read_source_receipt(root)
    source_boundaries = boundary_source_geometries(root, dependencies, boundaries)
    union = dependencies.unary_union([value[1] for value in source_boundaries])
    union_bounds = union.bounds
    to_wgs = dependencies.Transformer.from_crs(5174, 4326, always_xy=True)

    bbox_candidates = 0
    selected_features = 0
    area_intersections = 0
    line_assignments = 0
    multi_area_features = 0
    source_part_count = source_point_count = source_segment_count = 0
    source_length_millimeters = 0
    clip_fragment_count = clipped_vertices = clipped_segments = 0
    clipped_length_millimeters = 0
    short_segments = zero_segments = ten_mm_segments = duplicate_segments = 0
    rendered_segments = containment_failures = non_line_intersections = 0
    common_outside_points = 0
    common_maximum_outside_millimeters = 0
    candidate_rows: list[dict[str, Any]] = []
    area_counts: dict[str, Counter[str]] = {
        value[0].stable_id: Counter() for value in source_boundaries
    }
    physical_by_area: dict[
        str, set[tuple[tuple[int, int], tuple[int, int]]]
    ] = defaultdict(set)
    source_geometry_hashes: Counter[str] = Counter()

    with zipfile.ZipFile(source) as archive:
        require(archive.testzip() is None, "SourceArchiveCrcFailure")
        require(len(archive.namelist()) == len(set(archive.namelist())) == 17, "SourceArchiveEntrySetChanged")
        entries = archive_entries(archive, "N3L_F001")
        source_crs = dependencies.CRS.from_wkt(
            archive.read(entries[".prj"]).decode("utf-8-sig")
        )
        require(source_crs.to_authority() == ("EPSG", "5174"), "SourceCrsChanged")
        # Deliberately omit DBF.  No source attribute value enters this process.
        reader = dependencies.shapefile.Reader(
            shp=io.BytesIO(archive.read(entries[".shp"])),
            shx=io.BytesIO(archive.read(entries[".shx"])),
        )
        try:
            require(
                len(reader) == SOURCE_RECORDS
                and reader.shapeType == 3
                and reader.fields == [],
                "SourceGeometryContractChanged",
            )
            for record_ordinal in range(len(reader)):
                shape = reader.shape(record_ordinal)
                require(shape.shapeType == 3, "SourceShapeTypeChanged")
                if not bbox_intersects(shape.bbox, union_bounds):
                    continue
                bbox_candidates += 1
                parts = source_parts(shape, dependencies)
                full_geometry = (
                    parts[0][1]
                    if len(parts) == 1
                    else dependencies.MultiLineString([value[1] for value in parts])
                )
                if not full_geometry.intersects(union):
                    continue
                selected_features += 1
                source_part_count += len(parts)
                source_point_count += sum(len(value.coords) for _, value in parts)
                source_segment_count += sum(len(value.coords) - 1 for _, value in parts)
                source_length_millimeters += round(sum(value.length for _, value in parts) * 1000)
                geometry_hash = sha(full_geometry.wkb)
                source_geometry_hashes[geometry_hash] += 1
                source_feature_hash = sha(
                    canonical(
                        {
                            "datasetId": "OA-22241",
                            "sourceVintage": SOURCE_VINTAGE,
                            "sourceArchiveSha256": SOURCE_SHA,
                            "sourceRecordOrdinal": record_ordinal,
                            "sourceGeometrySha256": geometry_hash,
                        }
                    )
                ).lower()
                assignments: list[dict[str, Any]] = []
                intersected_areas: list[str] = []
                for boundary, source_boundary, common_boundary in source_boundaries:
                    if not bbox_intersects(full_geometry.bounds, source_boundary.bounds):
                        continue
                    if not full_geometry.intersects(source_boundary):
                        continue
                    area_intersections += 1
                    intersected_areas.append(boundary.stable_id)
                    clipped: list[tuple[int, int, Any]] = []
                    non_line = 0
                    for source_part_ordinal, part in parts:
                        result = part.intersection(source_boundary)
                        members, count = line_members(result)
                        non_line += count
                        for member_ordinal, member in enumerate(members):
                            clipped.append((source_part_ordinal, member_ordinal, member))
                    non_line_intersections += non_line
                    if not clipped:
                        area_counts[boundary.stable_id]["pointTouchOnlyFeatureCount"] += 1
                        continue
                    line_assignments += 1
                    clip_fragments: list[dict[str, Any]] = []
                    render_fragments: list[dict[str, Any]] = []
                    area_counts[boundary.stable_id]["candidateCount"] += 1
                    for source_part_ordinal, member_ordinal, member in clipped:
                        source_boundary_buffer = source_boundary.buffer(0.000001)
                        for source_x, source_y in member.coords:
                            if not source_boundary_buffer.covers(
                                dependencies.Point(source_x, source_y)
                            ):
                                containment_failures += 1
                        path = to_common_path(member, to_wgs, frame)
                        require(len(path) >= 2, "ClippedPathTooShort")
                        fragment_ordinal = len(clip_fragments)
                        clip_fragments.append(
                            {
                                "fragmentOrdinal": fragment_ordinal,
                                "sourcePartOrdinal": source_part_ordinal,
                                "sourcePartClipOrdinal": member_ordinal,
                                "commonEnuMillimeterPath": path,
                            }
                        )
                        clip_fragment_count += 1
                        clipped_vertices += len(path)
                        clipped_segments += len(path) - 1
                        clipped_length_millimeters += round(member.length * 1000)
                        area_counts[boundary.stable_id]["clipFragmentCount"] += 1
                        area_counts[boundary.stable_id]["clippedPathVertexCount"] += len(path)
                        area_counts[boundary.stable_id]["clippedSourceSegmentCount"] += len(path) - 1
                        buffered = common_boundary.buffer(0.0011)
                        for point in path:
                            common_point = dependencies.Point(
                                point["x"] / 1000.0, point["z"] / 1000.0
                            )
                            if not buffered.covers(common_point):
                                common_outside_points += 1
                                common_maximum_outside_millimeters = max(
                                    common_maximum_outside_millimeters,
                                    round(common_boundary.distance(common_point) * 1000),
                                )
                        for first, second in zip(path, path[1:]):
                            length = math.hypot(
                                first["x"] - second["x"], first["z"] - second["z"]
                            )
                            if length <= 10.0:
                                ten_mm_segments += 1
                            if length <= MIN_RENDERABLE_SEGMENT_MM:
                                short_segments += 1
                                area_counts[boundary.stable_id][
                                    "skippedAtOrBelowOneMillimeterSegmentCount"
                                ] += 1
                                if length == 0:
                                    zero_segments += 1
                                continue
                            key = segment_key(first, second)
                            if key in physical_by_area[boundary.stable_id]:
                                duplicate_segments += 1
                                area_counts[boundary.stable_id][
                                    "duplicatePhysicalSegmentInstanceCount"
                                ] += 1
                                continue
                            physical_by_area[boundary.stable_id].add(key)
                            render_fragments.append(
                                {
                                    "fragmentOrdinal": len(render_fragments),
                                    "commonEnuMillimeterPath": [first, second],
                                }
                            )
                    rendered_segments += len(render_fragments)
                    area_counts[boundary.stable_id]["renderedCandidateCount"] += bool(
                        render_fragments
                    )
                    area_counts[boundary.stable_id]["renderFragmentCount"] += len(
                        render_fragments
                    )
                    area_counts[boundary.stable_id]["renderedPathVertexCount"] += 2 * len(
                        render_fragments
                    )
                    area_counts[boundary.stable_id][
                        "renderedUniquePhysicalSegmentCount"
                    ] += len(render_fragments)
                    candidate_id = "contour-trace:sha256:" + sha(
                        canonical(
                            {
                                "sourceFeatureSha256": source_feature_hash,
                                "administrativeAreaStableId": boundary.stable_id,
                            }
                        )
                    ).lower()
                    assignments.append(
                        {
                            "administrativeAreaStableId": boundary.stable_id,
                            "candidateStableId": candidate_id,
                            "sourceFeatureSha256": source_feature_hash,
                            "sourceGeometrySha256": geometry_hash.lower(),
                            "sourcePartCount": len(parts),
                            "sourcePointCount": sum(len(value.coords) for _, value in parts),
                            "sourceSegmentCount": sum(
                                len(value.coords) - 1 for _, value in parts
                            ),
                            "clipFragments": clip_fragments,
                            "renderFragments": render_fragments,
                        }
                    )
                if len(intersected_areas) > 1:
                    multi_area_features += 1
                shared = sorted(intersected_areas)
                for candidate in assignments:
                    candidate["sharedAdministrativeAreaStableIds"] = shared
                    candidate_rows.append(candidate)
        finally:
            reader.close()

    candidate_rows.sort(
        key=lambda item: (
            item["administrativeAreaStableId"],
            item["candidateStableId"],
        )
    )
    require(selected_features == EXPECTED_SCOPE_FEATURES, "ScopeFeatureCountChanged")
    require(area_intersections == EXPECTED_AREA_INTERSECTIONS, "AreaIntersectionCountChanged")
    require(len(area_counts) == AREA_COUNT and all(value["candidateCount"] > 0 for value in area_counts.values()), "AreaClipCoverageChanged")
    require(containment_failures == 0, "BoundaryContainmentFailed")
    require(non_line_intersections == 0, "UnexpectedNonLineIntersection")
    require(
        clipped_segments == rendered_segments + short_segments + duplicate_segments,
        "SegmentAccountingChanged",
    )
    require(
        len({value["candidateStableId"] for value in candidate_rows})
        == len(candidate_rows),
        "CandidateStableIdCollision",
    )
    metric_keys = (
        "candidateCount",
        "pointTouchOnlyFeatureCount",
        "clipFragmentCount",
        "clippedPathVertexCount",
        "clippedSourceSegmentCount",
        "skippedAtOrBelowOneMillimeterSegmentCount",
        "duplicatePhysicalSegmentInstanceCount",
        "renderedCandidateCount",
        "renderFragmentCount",
        "renderedPathVertexCount",
        "renderedUniquePhysicalSegmentCount",
    )
    for value in area_counts.values():
        for key in metric_keys:
            value.setdefault(key, 0)
    counts = {
        "sourceRecordCount": SOURCE_RECORDS,
        "sourceBoundingBoxCandidateRecordCount": bbox_candidates,
        "sourceFeaturesIntersectingHistoricalScope": selected_features,
        "sourcePartCount": source_part_count,
        "sourcePointCount": source_point_count,
        "sourceSegmentCount": source_segment_count,
        "sourceLengthMillimeters": source_length_millimeters,
        "duplicateSourceGeometryValueCount": sum(
            count > 1 for count in source_geometry_hashes.values()
        ),
        "extraDuplicateSourceGeometryOccurrenceCount": sum(
            count - 1 for count in source_geometry_hashes.values() if count > 1
        ),
        "sourceFeaturesAssignedToMultipleAdministrativeAreas": multi_area_features,
        "administrativeAreaFeatureIntersectionCount": area_intersections,
        "administrativeAreaLineAssignmentCount": line_assignments,
        "candidateCount": len(candidate_rows),
        "clipFragmentCount": clip_fragment_count,
        "clippedPathVertexCount": clipped_vertices,
        "clippedSourceSegmentCount": clipped_segments,
        "clippedLengthMillimeters": clipped_length_millimeters,
        "nonLineIntersectionCount": non_line_intersections,
        "sourceBoundaryContainmentFailureCount": containment_failures,
        "commonBoundaryQuantizedOutsidePointCount": common_outside_points,
        "commonBoundaryMaximumOutsideDistanceMillimeters": common_maximum_outside_millimeters,
        "quantizedAtOrBelowOneMillimeterSegmentCount": short_segments,
        "quantizedZeroLengthSegmentCount": zero_segments,
        "quantizedAtOrBelowTenMillimeterSegmentCount": ten_mm_segments,
        "duplicatePhysicalSegmentInstanceCount": duplicate_segments,
        "renderedCandidateCount": sum(
            bool(value["renderFragments"]) for value in candidate_rows
        ),
        "renderedUniquePhysicalSegmentCount": rendered_segments,
        "renderedPathVertexCount": rendered_segments * 2,
        "administrativeAreasCovered": len(area_counts),
    }
    audit = {
        "counts": counts,
        "perAdministrativeArea": {
            key: dict(sorted(value.items())) for key, value in sorted(area_counts.items())
        },
        "sourceGeometryAudit": {
            "sourceDbfOpened": False,
            "sourceAttributeValuesRead": False,
            "contOrHeightValuesRead": False,
            "identityUsesSourceArchiveHashRecordOrdinalAndGeometryHashOnly": True,
            "sourceLinePartsReadWithoutSimplification": True,
            "clipFragmentsRetainSourcePartOrdinal": True,
            "clipFragmentsRetainEveryQuantizedPathVertex": True,
            "renderFragmentsAreTwoPointSegments": True,
        },
        "deduplicationAudit": {
            "scope": "WithinAdministrativeArea",
            "physicalSegmentDirectionIgnored": True,
            "duplicatePhysicalSegmentInstanceCount": duplicate_segments,
        },
        "historicalBoundaryRings": boundary_ring_audit(boundaries),
    }
    return candidate_rows, audit


def candidate_scan(payload: bytes) -> dict[str, int]:
    candidates = clip_fragments = clip_segments = render_fragments = 0
    ids: set[str] = set()
    physical: dict[str, set[tuple[tuple[int, int], tuple[int, int]]]] = defaultdict(set)
    for line in payload.splitlines():
        row = json.loads(line)
        require(
            set(row)
            == {
                "administrativeAreaStableId",
                "candidateStableId",
                "sourceFeatureSha256",
                "sourceGeometrySha256",
                "sourcePartCount",
                "sourcePointCount",
                "sourceSegmentCount",
                "sharedAdministrativeAreaStableIds",
                "clipFragments",
                "renderFragments",
            },
            "CandidateSchemaChanged",
        )
        require(
            re.fullmatch(r"contour-trace:sha256:[0-9a-f]{64}", row["candidateStableId"])
            is not None
            and row["candidateStableId"] not in ids,
            "CandidateStableIdInvalidOrDuplicate",
        )
        ids.add(row["candidateStableId"])
        for expected, fragment in enumerate(row["clipFragments"]):
            require(
                set(fragment)
                == {
                    "fragmentOrdinal",
                    "sourcePartOrdinal",
                    "sourcePartClipOrdinal",
                    "commonEnuMillimeterPath",
                }
                and fragment["fragmentOrdinal"] == expected,
                "ClipFragmentSchemaChanged",
            )
            path = fragment["commonEnuMillimeterPath"]
            require(
                len(path) >= 2
                and all(
                    set(point) == {"x", "z"}
                    and isinstance(point["x"], int)
                    and not isinstance(point["x"], bool)
                    and isinstance(point["z"], int)
                    and not isinstance(point["z"], bool)
                    for point in path
                ),
                "ClipFragmentPathChanged",
            )
            clip_segments += len(path) - 1
        for expected, fragment in enumerate(row["renderFragments"]):
            require(
                set(fragment) == {"fragmentOrdinal", "commonEnuMillimeterPath"}
                and fragment["fragmentOrdinal"] == expected,
                "RenderFragmentSchemaChanged",
            )
            path = fragment["commonEnuMillimeterPath"]
            require(len(path) == 2, "RenderFragmentPathChanged")
            require(
                math.hypot(path[0]["x"] - path[1]["x"], path[0]["z"] - path[1]["z"])
                > MIN_RENDERABLE_SEGMENT_MM,
                "RenderFragmentTooShort",
            )
            key = segment_key(path[0], path[1])
            area = row["administrativeAreaStableId"]
            require(key not in physical[area], "RenderPhysicalSegmentDuplicated")
            physical[area].add(key)
        candidates += 1
        clip_fragments += len(row["clipFragments"])
        render_fragments += len(row["renderFragments"])
    return {
        "candidateRowsScanned": candidates,
        "clipFragmentsScanned": clip_fragments,
        "clipSegmentsScanned": clip_segments,
        "renderFragmentsScanned": render_fragments,
    }


def prepare(root: Path) -> tuple[str, dict[str, bytes], dict[str, Any]]:
    _, dependencies, scope, frame, boundaries = load_context(root)
    candidates, source_audit = read_candidates(root, dependencies, frame, boundaries)
    candidate_payload = b"".join(canonical(value) for value in candidates)
    scan = candidate_scan(candidate_payload)
    counts = source_audit["counts"]
    require(
        scan["candidateRowsScanned"] == counts["candidateCount"]
        and scan["clipFragmentsScanned"] == counts["clipFragmentCount"]
        and scan["clipSegmentsScanned"] == counts["clippedSourceSegmentCount"]
        and scan["renderFragmentsScanned"] == counts["renderedUniquePhysicalSegmentCount"],
        "CandidateScanCountChanged",
    )
    generator_bytes = Path(__file__).resolve().read_bytes()
    source_binding = {
        "datasetId": "OA-22241",
        "officialFileName": "서울시 등고선.zip",
        "sourceVintage": SOURCE_VINTAGE,
        "sourceArchiveSha256": SOURCE_SHA,
        "sourceArchiveBytes": SOURCE_BYTES,
        "sourceReceiptSha256": SOURCE_RECEIPT_SHA,
        "sourceCoordinateReferenceSystem": "EPSG:5174",
        "sourceGeometryType": "PolyLine",
        "sourceAttributeDatabaseRead": False,
        "sourceElevationValuesRead": False,
        "historicalBoundaryDatasetId": "OA-22160",
        "historicalBoundaryVintage": "2023-10-31",
        "historicalBoundarySha256": BOUNDARY_SHA,
        "scopeDefinitionSha256": SCOPE_SHA,
        "boundaryLoaderSha256": sha_file(safe(root, BATCH)),
        "generatorSha256": sha(generator_bytes),
    }
    generation_hash = sha(
        canonical(
            {
                "sourceBinding": source_binding,
                "candidatesSha256": sha(candidate_payload),
                "auditCounts": counts,
            }
        )
    )
    audit = {
        "schemaVersion": "administrative-dong-horizontal-contour-trace-private-audit.v1",
        "generationHashSha256": generation_hash,
        "generatedAtUtc": FIXED_AT,
        "scopeStableId": scope["scopeStableId"],
        "sourceBinding": source_binding,
        "sourceCrs": "EPSG:5174",
        "historicalBoundaryCrs": "EPSG:5181",
        "coordinateFrame": scope["coordinateFrame"],
        **source_audit,
        "candidateScan": scan,
        "renderPolicy": {
            "flatHorizontalTraceOnly": True,
            "minimumRenderableSegmentMillimeters": MIN_RENDERABLE_SEGMENT_MM,
            "skipAtOrBelowThreshold": True,
            "physicalSegmentDeduplicationScope": "WithinAdministrativeArea",
            "physicalSegmentDirectionIgnored": True,
            "sourceLinePartOrdinalPreservedInClipFragments": True,
            "sourcePathSimplificationApplied": False,
            "elevationAttributeRead": False,
        },
        "authority": authority(),
    }
    files: dict[str, bytes] = {
        "candidates.ndjson": candidate_payload,
        "audit.json": pretty(audit),
        "generator-source.py": generator_bytes,
    }
    manifest = {
        "schemaVersion": "administrative-dong-horizontal-contour-trace-private-manifest.v1",
        "generationHashSha256": generation_hash,
        "generatedAtUtc": FIXED_AT,
        "revision": "northeast-seoul-horizontal-contour-trace-private-review.r1",
        "scopeStableId": scope["scopeStableId"],
        "sourceBinding": source_binding,
        "candidateCount": counts["candidateCount"],
        "renderedCandidateCount": counts["renderedCandidateCount"],
        "administrativeAreaCount": AREA_COUNT,
        "clipFragmentCount": counts["clipFragmentCount"],
        "clippedSourceSegmentCount": counts["clippedSourceSegmentCount"],
        "renderedUniquePhysicalSegmentCount": counts[
            "renderedUniquePhysicalSegmentCount"
        ],
        "fileDescriptors": [
            {"path": name, "bytes": len(payload), "sha256": sha(payload)}
            for name, payload in sorted(files.items())
        ],
        "authority": authority(),
    }
    files["manifest.json"] = pretty(manifest)
    completion = {
        "schemaVersion": "administrative-dong-horizontal-contour-trace-private-completion.v1",
        "generationHashSha256": generation_hash,
        "status": "LocalPrivateHistoricalHorizontalTraceCandidateGenerated",
        "manifestSha256": sha(files["manifest.json"]),
        "candidateCount": counts["candidateCount"],
        "administrativeAreaCount": AREA_COUNT,
        "renderedUniquePhysicalSegmentCount": counts[
            "renderedUniquePhysicalSegmentCount"
        ],
        "fileCount": len(files) + 1,
        "authority": authority(),
    }
    files["complete.json"] = pretty(completion)
    return generation_hash, files, counts


def validate_generation(target: Path, files: dict[str, bytes]) -> None:
    require(target.is_dir() and not is_reparse(target), "GenerationMissingOrUnsafe")
    actual = {
        file.relative_to(target).as_posix()
        for file in target.rglob("*")
        if file.is_file()
    }
    require(actual == set(files), "GenerationFileSetChanged")
    for name, payload in files.items():
        file = target / name
        require(
            file.is_file() and not is_reparse(file) and file.read_bytes() == payload,
            "GenerationFileChanged:" + name,
        )
    audit = json.loads(files["audit.json"])
    manifest = json.loads(files["manifest.json"])
    completion = json.loads(files["complete.json"])
    require(
        audit["generationHashSha256"]
        == manifest["generationHashSha256"]
        == completion["generationHashSha256"]
        and completion["manifestSha256"] == sha(files["manifest.json"]),
        "GenerationBindingChanged",
    )


def self_test(root: Path) -> dict[str, Any]:
    require(
        segment_key({"x": 1, "z": 2}, {"x": 3, "z": 4})
        == segment_key({"x": 3, "z": 4}, {"x": 1, "z": 2}),
        "SelfTestUndirectedSegment",
    )
    flags = authority()
    require(
        all(
            value is False
            for key, value in flags.items()
            if key
            not in {
                "privateReviewOnly",
                "historicalBoundaryBootstrapOnly",
                "reviewVisualizationOnly",
                "privateReviewVisualizationAllowed",
                "sourceHorizontalGeometryObserved",
            }
        ),
        "SelfTestAuthority",
    )
    _, files, counts = prepare(root)
    require(
        counts["administrativeAreasCovered"] == AREA_COUNT
        and counts["sourceBoundaryContainmentFailureCount"] == 0
        and counts["clippedSourceSegmentCount"]
        == counts["renderedUniquePhysicalSegmentCount"]
        + counts["quantizedAtOrBelowOneMillimeterSegmentCount"]
        + counts["duplicatePhysicalSegmentInstanceCount"],
        "SelfTestCounts",
    )
    require(
        json.loads(files["audit.json"])["sourceGeometryAudit"]["contOrHeightValuesRead"]
        is False,
        "SelfTestAttributeBoundary",
    )
    return {
        "status": "PASS",
        "selfTestsPassed": 8,
        "generatorSourceHashSha256": sha_file(Path(__file__).resolve()),
        **counts,
    }


def run(root: Path, mode: str) -> dict[str, Any]:
    if mode == "self-test":
        return self_test(root)
    generation_hash, files, counts = prepare(root)
    target = safe(root, OUTPUT / "generations" / generation_hash.lower())
    if mode == "verify" or target.exists():
        validate_generation(target, files)
        changed = 0
    else:
        target.parent.mkdir(parents=True, exist_ok=True)
        staging = target.parent / (".staging-" + uuid.uuid4().hex)
        staging.mkdir()
        try:
            for name, payload in files.items():
                destination = staging / name
                destination.parent.mkdir(parents=True, exist_ok=True)
                destination.write_bytes(payload)
            validate_generation(staging, files)
            os.replace(staging, target)
        except Exception:
            if staging.exists():
                shutil.rmtree(staging)
            raise
        changed = len(files)
    return {
        "status": "PASS",
        "mode": mode,
        "generationHashSha256": generation_hash,
        "generationRelativePath": target.relative_to(root).as_posix(),
        "changedFiles": changed,
        "sourceArchiveSha256": SOURCE_SHA,
        "sourceReceiptSha256": SOURCE_RECEIPT_SHA,
        **counts,
        "authority": authority(),
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("mode", choices=("self-test", "build", "verify"))
    parser.add_argument("root", nargs="?", default=str(ROOT))
    args = parser.parse_args()
    root = Path(args.root).resolve()
    try:
        require((root / ".git").exists(), "RepositoryRootInvalid")
        print(json.dumps(run(root, args.mode), ensure_ascii=False, sort_keys=True, indent=2))
        return 0
    except (
        ContourTraceError,
        OSError,
        ValueError,
        KeyError,
        TypeError,
        zipfile.BadZipFile,
    ) as exc:
        print(
            json.dumps({"status": "FAIL", "errorCode": str(exc)}, ensure_ascii=False),
            file=sys.stderr,
        )
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
