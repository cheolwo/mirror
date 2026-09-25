#!/usr/bin/env python3
"""Overture 공개 건물 윤곽으로 사가정 빈 공간 보조 오버레이를 만든다.

결과는 로컬 검토용 표현 자료다. 기존 SagajeongReference r3 건물과
의미 있게 겹치는 윤곽은 제외하며, 높이·용도·통행 권위를 만들지 않는다.
"""

from __future__ import annotations

import argparse
import copy
import hashlib
import json
import math
import sys
from pathlib import Path
from typing import Any

sys.dont_write_bytecode = True

from shapely.geometry import MultiPolygon, Polygon
from shapely.strtree import STRtree

from sagajeong_spatial_presentation import (
    BASE_MAP_HASH,
    BASE_MAP_REVISION,
    COORDINATE_METHOD,
    PRIVATE_STATUS,
    SCHEMA_VERSION,
    SpatialPresentationError,
    _polygonal,
    canonical_json_bytes,
    geometry_to_parts,
    load_base_map,
    read_legacy_base_buildings,
    require,
    rings_to_geometry,
    sha256_file,
    write_json_deterministic,
)


REVISION = "sagajeong-spatial-presentation.overture-private-review.r1"
SOURCE_PROVIDER = "Overture Maps Foundation"
SOURCE_DATASET_ID = "overture-buildings-2026-08-19.0-sagajeong"
SOURCE_URL = "https://docs.overturemaps.org/getting-data/"
LICENSE_CODE = "ODbL-1.0"
LICENSE_URL = "https://docs.overturemaps.org/attribution/"
DATASET_DATE = "2026-08-19"
FETCHED_AT_UTC = "2026-09-21T01:18:39.804683Z"
RAW_HASH = "96DC6451392391A9A31BB34AE17F3CB2501E1967EAF8155ECD74CC0FBAA560F9"
RAW_LENGTH = 1_738_246
HEIGHT_POLICY_REVISION = "sagajeong-overture-symbolic-height.r1"
SYMBOLIC_HEIGHT_METERS = 4.0
MINIMUM_BUILDING_AREA_SQUARE_METERS = 1.0
BASE_OVERLAP_MINIMUM_IOU = 0.15
BASE_OVERLAP_MINIMUM_SMALLER_COVERAGE = 0.25


def parse_arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--input",
        default=(
            "artifacts/local/neighborhood-source-acquisition/"
            "sagajeong-overture-current-buildings.geojson"
        ),
    )
    parser.add_argument(
        "--base-map",
        default="../../../ssalddel/Assets/Ssalddel/Resources/SagajeongReference.json",
    )
    parser.add_argument(
        "--output",
        default=(
            "artifacts/local/sagajeong-spatial-presentation/"
            "overture-private-review.json"
        ),
    )
    parser.add_argument(
        "--repository-root", default=str(Path(__file__).resolve().parents[2])
    )
    return parser.parse_args()


def repository_path(root: Path, value: str) -> Path:
    path = Path(value)
    return path.resolve() if path.is_absolute() else (root / path).resolve()


def local_output_path(root: Path, value: str) -> Path:
    path = repository_path(root, value)
    local_root = (root / "artifacts" / "local").resolve()
    try:
        path.relative_to(local_root)
    except ValueError as exc:
        raise SpatialPresentationError(f"OutputMustRemainUnderArtifactsLocal:{path}") from exc
    return path


def feature_geometry(feature: dict[str, Any], frame: Any) -> tuple[Any, bool]:
    geometry = feature.get("geometry")
    require(isinstance(geometry, dict), "OvertureGeometryMissing")
    geometry_type = geometry.get("type")
    coordinates = geometry.get("coordinates")
    require(geometry_type in {"Polygon", "MultiPolygon"}, "OvertureGeometryTypeInvalid")
    require(isinstance(coordinates, list), "OvertureGeometryCoordinatesInvalid")

    polygons = coordinates if geometry_type == "MultiPolygon" else [coordinates]
    transformed: list[Any] = []
    for polygon in polygons:
        require(isinstance(polygon, list) and polygon, "OverturePolygonInvalid")
        rings: list[list[tuple[float, float]]] = []
        for ring in polygon:
            require(isinstance(ring, list) and len(ring) >= 4, "OvertureRingInvalid")
            points: list[tuple[float, float]] = []
            for coordinate in ring:
                require(
                    isinstance(coordinate, list) and len(coordinate) >= 2,
                    "OvertureCoordinateInvalid",
                )
                longitude = float(coordinate[0])
                latitude = float(coordinate[1])
                require(
                    math.isfinite(longitude) and math.isfinite(latitude),
                    "OvertureCoordinateNonFinite",
                )
                points.append(frame.wgs84_to_local(longitude, latitude))
            rings.append(points)
        transformed.append(rings_to_geometry(rings))

    if not transformed:
        return Polygon(), False
    merged = _polygonal(MultiPolygon([
        member
        for geometry_value in transformed
        for member in (
            [geometry_value]
            if isinstance(geometry_value, Polygon)
            else list(geometry_value.geoms)
            if isinstance(geometry_value, MultiPolygon)
            else []
        )
    ]))
    boundary_clipped = not frame.clip_polygon.covers(merged)
    return _polygonal(merged.intersection(frame.clip_polygon)), boundary_clipped


def overlaps_base(geometry: Any, tree: STRtree, base_geometries: list[Any]) -> bool:
    for raw_index in tree.query(geometry):
        candidate = base_geometries[int(raw_index)]
        intersection_area = geometry.intersection(candidate).area
        if intersection_area <= 0.01:
            continue
        union_area = geometry.union(candidate).area
        smaller_area = min(geometry.area, candidate.area)
        iou = intersection_area / union_area if union_area > 0 else 0.0
        smaller_coverage = intersection_area / smaller_area if smaller_area > 0 else 0.0
        if (
            iou >= BASE_OVERLAP_MINIMUM_IOU
            or smaller_coverage >= BASE_OVERLAP_MINIMUM_SMALLER_COVERAGE
        ):
            return True
    return False


def apply_content_hash(document: dict[str, Any]) -> None:
    candidate = copy.deepcopy(document)
    candidate["contentHash"] = ""
    document["contentHash"] = hashlib.sha256(canonical_json_bytes(candidate)).hexdigest().upper()


def build(source_path: Path, base_map_path: Path) -> tuple[dict[str, Any], dict[str, int]]:
    require(source_path.is_file(), "OvertureSourceMissing")
    require(source_path.stat().st_size == RAW_LENGTH, "OvertureSourceLengthMismatch")
    require(sha256_file(source_path) == RAW_HASH, "OvertureSourceHashMismatch")

    try:
        source = json.loads(source_path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        raise SpatialPresentationError("OvertureSourceReadFailed") from exc
    features = source.get("features")
    require(source.get("type") == "FeatureCollection", "OvertureFeatureCollectionInvalid")
    require(isinstance(features, list) and features, "OvertureFeaturesMissing")

    base_map, frame = load_base_map(base_map_path)
    base_buildings = read_legacy_base_buildings(base_map)
    base_geometries = [item["geometry"] for item in base_buildings]
    base_tree = STRtree(base_geometries)

    buildings: list[dict[str, Any]] = []
    identifiers: set[str] = set()
    clipped_count = 0
    overlap_excluded_count = 0
    tiny_excluded_count = 0
    vertex_count = 0

    for feature in features:
        require(isinstance(feature, dict), "OvertureFeatureInvalid")
        source_identifier = str(feature.get("id", "")).strip()
        require(source_identifier and source_identifier not in identifiers, "OvertureFeatureIdInvalid")
        identifiers.add(source_identifier)
        geometry, boundary_clipped = feature_geometry(feature, frame)
        if geometry.is_empty or geometry.area < MINIMUM_BUILDING_AREA_SQUARE_METERS:
            tiny_excluded_count += 1
            continue
        if boundary_clipped:
            clipped_count += 1
        if overlaps_base(geometry, base_tree, base_geometries):
            overlap_excluded_count += 1
            continue

        properties = feature.get("properties") or {}
        sources = properties.get("sources") or []
        source_family = "+".join(
            sorted({str(item.get("resource") or item.get("dataset") or "unknown") for item in sources})
        ) or "unknown"
        parts = geometry_to_parts(geometry)
        vertex_count += sum(
            len(part["outer"]["points"])
            + sum(len(hole["points"]) for hole in part["holes"])
            for part in parts
        )
        buildings.append(
            {
                "id": f"overture:building:{source_identifier}",
                "sourceFeatureId": source_identifier,
                "buildingKind": source_family,
                "purposeName": "",
                "heightMeters": SYMBOLIC_HEIGHT_METERS,
                "heightKind": "SymbolicFallback4m",
                "observedHeightMeters": None,
                "observedAboveGroundFloors": None,
                "heightPolicyRevision": HEIGHT_POLICY_REVISION,
                "reviewStatus": PRIVATE_STATUS,
                "legacyOsmIds": [],
                "legacyMatch": {
                    "method": "None",
                    "centroidDistanceMeters": None,
                    "intersectionOverUnion": None,
                    "smallerFootprintCoverage": None,
                    "ambiguous": False,
                },
                "parts": parts,
            }
        )

    buildings.sort(key=lambda item: item["id"])
    require(buildings, "OvertureSupplementBuildingsMissing")
    document: dict[str, Any] = {
        "schemaVersion": SCHEMA_VERSION,
        "revision": REVISION,
        "status": PRIVATE_STATUS,
        "presentationOnly": True,
        "distributionApproved": False,
        "baseMapRevision": BASE_MAP_REVISION,
        "baseMapHash": BASE_MAP_HASH,
        "coordinateMethod": COORDINATE_METHOD,
        "originLatitude": frame.origin_latitude,
        "originLongitude": frame.origin_longitude,
        "offsetX": frame.offset_x,
        "offsetZ": frame.offset_z,
        "halfExtent": frame.half_extent,
        "sourceReceipts": [
            {
                "provider": SOURCE_PROVIDER,
                "datasetId": SOURCE_DATASET_ID,
                "sourceUrl": SOURCE_URL,
                "licenseCode": LICENSE_CODE,
                "licenseUrl": LICENSE_URL,
                "datasetDate": DATASET_DATE,
                "fetchedAtUtc": FETCHED_AT_UTC,
                "rawHash": RAW_HASH,
                "rawLength": RAW_LENGTH,
                "originalCrs": "EPSG:4326",
                "reviewStatus": PRIVATE_STATUS,
            }
        ],
        "buildings": buildings,
        "surfaces": [],
        "contentHash": "",
    }
    apply_content_hash(document)
    return document, {
        "sourceBuildingCount": len(features),
        "supplementBuildingCount": len(buildings),
        "baseOverlapExcludedCount": overlap_excluded_count,
        "tinyOrOutsideExcludedCount": tiny_excluded_count,
        "boundaryClippedCount": clipped_count,
        "vertexCount": vertex_count,
    }


def main() -> int:
    args = parse_arguments()
    try:
        root = Path(args.repository_root).resolve()
        output = local_output_path(root, args.output)
        document, statistics = build(
            repository_path(root, args.input), repository_path(root, args.base_map)
        )
        write_json_deterministic(output, document)
        print(
            json.dumps(
                {
                    "status": "GeneratedLocalPrivateReview",
                    "output": str(output),
                    "contentHash": document["contentHash"],
                    **statistics,
                },
                ensure_ascii=False,
                sort_keys=True,
            )
        )
        return 0
    except (SpatialPresentationError, OSError, ValueError, TypeError) as exc:
        print(f"SagajeongOvertureOverlayBuildFailed:{exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
