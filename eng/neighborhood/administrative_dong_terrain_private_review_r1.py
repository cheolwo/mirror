#!/usr/bin/env python3
"""OA-22241 2025 단독 판본으로 30개 행정동 비공개 지형 검토 격자를 만든다.

이 생성물의 HEIGHT 값은 원천 수치일 뿐이다. 원천이 수직 기준면과 높이 단위를
명시하지 않았으므로 절대 표고, 물리 단위, 통행, gameplay 또는 운영 권위를
부여하지 않는다. OA-22160 역사 경계는 표시 mask에만 사용한다.
"""

from __future__ import annotations

import argparse
import copy
import hashlib
import importlib.util
import io
import json
import math
import os
import shutil
import stat
import struct
import sys
import uuid
import zipfile
from collections import defaultdict
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Iterable, Iterator, Sequence


ROOT_DEFAULT = Path(__file__).resolve().parents[2]
SOURCE_ARCHIVE = Path(
    "artifacts/local/public-data/admin-dong-terrain-contour-20260926-r1/raw/"
    "OA-22241-seoul-contour.zip"
)
SOURCE_RECEIPT = Path(
    "artifacts/local/public-data/admin-dong-terrain-contour-20260926-r1/raw/receipt.json"
)
BOUNDARY_ARCHIVE = Path(
    "artifacts/local/public-data/admin-dong/20260912-seoul-oa22160/"
    "seoul-administrative-dong-boundary.zip"
)
BASE_REVIEW_ROOT = Path(
    "artifacts/local/validation/admin-dong-diorama-base-review/r1/input/generations/"
    "c6cd2501cd2fc876bff671fb25c36c139afdaf14f78ec2fd3f7503e31a1153ac"
)
SCOPE_DEFINITION = Path(
    "eng/world-seedbeds/administrative-dong-dioramas/northeast-seoul-rider.r2.json"
)
BATCH_GENERATOR = Path("eng/neighborhood/administrative_dong_diorama_batch.py")
OUTPUT_ROOT = Path(
    "artifacts/local/validation/admin-dong-terrain-private-review/r1/input"
)

SOURCE_HASH = "4FBE3C7E061B5974E7403EC116855304ED8AE321EEBCC0D12C31CA8FB7BE30BF"
SOURCE_BYTES = 45_852_601
SOURCE_RECEIPT_HASH = "8911948581B53B07BF59BDE353EA2743948A9733B6EA24F341FB4364496A345D"
BOUNDARY_HASH = "969F7033BD3609A5FD586790F5B2CFEDC638D7647EF45C78F9C75E1DABF79F68"
BASE_INDEX_HASH = "C8D4999494767E9588E998100893A2D4227A8E116DEBD8F8B81611BA94D3E123"
BASE_GENERATION_HASH = "C6CD2501CD2FC876BFF671FB25C36C139AFDAF14F78EC2FD3F7503E31A1153AC"
EXPECTED_CONTOUR_RECORDS = 8_570
EXPECTED_POINT_RECORDS = 45_870
EXPECTED_AREA_COUNT = 30
EXPECTED_TILE_MEMBERSHIPS = 297
EXPECTED_UNIQUE_TILES = 148
EXPECTED_ASSIGNED_POINTS = 2_424
EXPECTED_SCOPE_CONTOURS = 377
EXPECTED_SCOPE_CONTOUR_INTERSECTIONS = 860

SCHEMA_MANIFEST = "administrative-dong-terrain-private-review-manifest.v1"
SCHEMA_AUDIT = "administrative-dong-terrain-private-review-audit.v1"
SCHEMA_COMPLETE = "administrative-dong-terrain-private-review-completion.v1"
REVISION = "northeast-seoul-admin-dong-terrain-private-review.r1"
GENERATOR_REVISION = "administrative-dong-terrain-private-review-generator.r1"
GENERATED_AT_UTC = "2026-09-26T00:00:00Z"
SOURCE_VINTAGE = (
    "oa22241-file-20250320-source-2023-digital-topographic-map+"
    "oa22160-file-20231031-mask-bootstrap+base-lattice-c6cd2501"
)

TILE_SIZE_METERS = 500
HALO_METERS = 60
SAMPLE_SPACING_METERS = 10
TILE_SAMPLE_SIDE = 63
CONTOUR_SAMPLE_SPACING_METERS = 40.0
SOURCE_SELECTION_MARGIN_METERS = 2_000.0
INTERPOLATION_INITIAL_WINDOW_METERS = 800.0
INTERPOLATION_REQUERY_THRESHOLD_METERS = 600.0
INTERPOLATION_SEARCH_RADIUS_METERS = 1_600.0
INTERPOLATION_NEIGHBOR_COUNT = 12
INTERPOLATION_POWER = 2.0
SPATIAL_BUCKET_METERS = 200.0
REVIEW_PREFERRED_NEAREST_SOURCE_DISTANCE_METERS = 500.0
REVIEW_MAX_NEAREST_SOURCE_DISTANCE_METERS = 1_500.0
REVIEW_MAX_POINT_HOLDOUT_P95_SOURCE_VALUE = 50.0
REVIEW_MAX_POINT_GRID_P95_SOURCE_VALUE = 25.0
REVIEW_MAX_CONTOUR_GRID_P95_SOURCE_VALUE = 25.0
FLOAT32_FORMAT = "height-f32-v1"
MASK_FORMAT = "administrative-boundary-mask-u8-v1"
ROW_ORDER = "north-to-south-then-west-to-east"


class TerrainReviewError(RuntimeError):
    """호출자가 재사용할 수 있는 안정적인 오류 코드."""


def require(condition: bool, code: str) -> None:
    if not condition:
        raise TerrainReviewError(code)


def safe_path(root: Path, relative: Path | str) -> Path:
    root = root.resolve()
    candidate = (root / relative).resolve()
    require(candidate == root or root in candidate.parents, "PathEscapesRepository")
    return candidate


def is_reparse(path: Path) -> bool:
    try:
        value = path.lstat()
    except OSError:
        return False
    flag = getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0x400)
    return path.is_symlink() or bool(getattr(value, "st_file_attributes", 0) & flag)


def sha_bytes(value: bytes) -> str:
    return hashlib.sha256(value).hexdigest().upper()


def sha_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest().upper()


def canonical_bytes(value: Any) -> bytes:
    return json.dumps(
        value,
        ensure_ascii=False,
        sort_keys=True,
        separators=(",", ":"),
        allow_nan=False,
    ).encode("utf-8")


def pretty_bytes(value: Any) -> bytes:
    return (
        json.dumps(value, ensure_ascii=False, indent=2, allow_nan=False) + "\n"
    ).encode("utf-8")


def content_hash(value: dict[str, Any]) -> str:
    candidate = copy.deepcopy(value)
    candidate.pop("contentHashSha256", None)
    return sha_bytes(canonical_bytes(candidate))


def apply_content_hash(value: dict[str, Any]) -> None:
    value["contentHashSha256"] = content_hash(value)


def read_json(path: Path) -> dict[str, Any]:
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        raise TerrainReviewError(f"JsonReadFailed:{path.name}") from exc
    require(isinstance(value, dict), f"JsonRootInvalid:{path.name}")
    return value


def validate_file(path: Path, length: int, digest: str, code: str) -> None:
    require(path.is_file() and not is_reparse(path), f"{code}MissingOrUnsafe")
    require(path.stat().st_size == length, f"{code}LengthChanged")
    require(sha_file(path) == digest, f"{code}HashChanged")


def load_module(path: Path, name: str) -> Any:
    spec = importlib.util.spec_from_file_location(name, path)
    require(spec is not None and spec.loader is not None, "BatchGeneratorImportSpecInvalid")
    module = importlib.util.module_from_spec(spec)
    sys.modules[name] = module
    spec.loader.exec_module(module)
    return module


@dataclass(frozen=True)
class TileMembership:
    area_stable_id: str
    area_code: str
    tile_x: int
    tile_z: int


@dataclass
class SourceConstraints:
    x: Any
    z: Any
    height: Any
    kind: Any
    stable_keys: list[str]
    source_point_indices: list[int]
    core_point_indices: list[int]
    core_contour_indices: list[int]
    selected_point_count: int
    selected_contour_record_count: int
    contour_sample_count: int
    source_numeric_minimum: float
    source_numeric_maximum: float
    area_coverage: dict[str, dict[str, int]]
    source_audit: dict[str, Any]


def load_context(root: Path) -> tuple[Any, Any, dict[str, Any], Any, list[Any]]:
    runtime = safe_path(root, "artifacts/local/public-data/gis-runtime-r1")
    spatial = safe_path(root, "artifacts/local/python-packages/spatial")
    for path in (runtime, spatial, safe_path(root, "eng/neighborhood")):
        if path.is_dir() and str(path) not in sys.path:
            sys.path.insert(0, str(path))
    batch_path = safe_path(root, BATCH_GENERATOR)
    batch = load_module(batch_path, "hongdal_terrain_boundary_batch")
    scope_path = safe_path(root, SCOPE_DEFINITION)
    scope = batch.load_scope(root, scope_path)
    dependencies = batch.load_dependencies(root)
    frame = batch.frame_from_scope(scope)
    boundaries, boundary_audit = batch.read_boundaries(
        root, scope, frame, dependencies
    )
    require(len(boundaries) == EXPECTED_AREA_COUNT, "BoundaryCoverageChanged")
    require(
        boundary_audit.get("historicalBootstrapOnly") is True
        and boundary_audit.get("currentAuthoritativeBoundaryAvailable") is False,
        "HistoricalBoundaryAuthorityChanged",
    )
    return batch, dependencies, scope, frame, boundaries


def load_tile_memberships(root: Path) -> tuple[list[TileMembership], list[tuple[int, int]], dict[str, Any]]:
    base_root = safe_path(root, BASE_REVIEW_ROOT)
    index_path = base_root / "index.json"
    validate_file(index_path, index_path.stat().st_size, BASE_INDEX_HASH, "BaseReviewIndex")
    index = read_json(index_path)
    require(
        index.get("schemaVersion") == "administrative-dong-diorama-unity-review-index.v1"
        and index.get("generationHashSha256") == BASE_GENERATION_HASH
        and index.get("administrativeAreaCount") == EXPECTED_AREA_COUNT
        and index.get("tileCount") == EXPECTED_TILE_MEMBERSHIPS,
        "BaseReviewIndexContractChanged",
    )
    memberships: list[TileMembership] = []
    unique: set[tuple[int, int]] = set()
    area_counts: dict[str, int] = {}
    entries = index.get("bundles")
    require(isinstance(entries, list) and len(entries) == EXPECTED_AREA_COUNT, "BaseBundleIndexChanged")
    for entry in sorted(entries, key=lambda item: str(item.get("administrativeAreaStableId"))):
        relative = str(entry.get("relativePath", ""))
        bundle_path = (base_root / relative).resolve()
        require(base_root in bundle_path.parents, "BaseBundlePathEscapesGeneration")
        validate_file(
            bundle_path,
            int(entry.get("byteLength", -1)),
            str(entry.get("sha256", "")),
            "BaseReviewBundle",
        )
        bundle = read_json(bundle_path)
        area_id = str(entry.get("administrativeAreaStableId", ""))
        area_code = area_id.removeprefix("region:kr:hjd:")
        require(
            bundle.get("administrativeAreaStableId") == area_id
            and bundle.get("tileCount") == len(bundle.get("tiles", []))
            and len(area_code) == 10,
            "BaseReviewBundleContractChanged",
        )
        seen: set[tuple[int, int]] = set()
        for tile in bundle["tiles"]:
            tile_x = int(tile["tileIndexX"])
            tile_z = int(tile["tileIndexZ"])
            key = (tile_x, tile_z)
            require(key not in seen, "BaseAreaTileDuplicate")
            expected_bounds = {
                "minX": tile_x * TILE_SIZE_METERS,
                "minZ": tile_z * TILE_SIZE_METERS,
                "maxX": (tile_x + 1) * TILE_SIZE_METERS,
                "maxZ": (tile_z + 1) * TILE_SIZE_METERS,
            }
            require(tile.get("bounds") == expected_bounds, "BaseTileLatticeChanged")
            seen.add(key)
            unique.add(key)
            memberships.append(TileMembership(area_id, area_code, tile_x, tile_z))
        area_counts[area_id] = len(seen)
    require(
        len(memberships) == EXPECTED_TILE_MEMBERSHIPS
        and len(unique) == EXPECTED_UNIQUE_TILES
        and len(area_counts) == EXPECTED_AREA_COUNT
        and all(value > 0 for value in area_counts.values()),
        "BaseTileCoverageChanged",
    )
    return memberships, sorted(unique), {
        "generationHashSha256": BASE_GENERATION_HASH,
        "indexSha256": BASE_INDEX_HASH,
        "administrativeAreaTileMembershipCount": len(memberships),
        "uniquePhysicalTileCount": len(unique),
        "role": "LatticeMembershipOnly;NoHeightConstraint;NoAuthority",
        "byAdministrativeArea": dict(sorted(area_counts.items())),
    }


def global_bounds(unique_tiles: Sequence[tuple[int, int]]) -> tuple[int, int, int, int]:
    require(len(unique_tiles) == EXPECTED_UNIQUE_TILES, "UniqueTileCountChanged")
    return (
        min(value[0] for value in unique_tiles) * TILE_SIZE_METERS - HALO_METERS,
        min(value[1] for value in unique_tiles) * TILE_SIZE_METERS - HALO_METERS,
        (max(value[0] for value in unique_tiles) + 1) * TILE_SIZE_METERS + HALO_METERS,
        (max(value[1] for value in unique_tiles) + 1) * TILE_SIZE_METERS + HALO_METERS,
    )


def required_union_mask(
    bounds: tuple[int, int, int, int],
    unique_tiles: Sequence[tuple[int, int]],
    numpy: Any,
) -> Any:
    min_x, min_z, max_x, max_z = bounds
    width = int((max_x - min_x) / SAMPLE_SPACING_METERS) + 1
    height = int((max_z - min_z) / SAMPLE_SPACING_METERS) + 1
    mask = numpy.zeros((height, width), dtype="bool")
    for tile_x, tile_z in unique_tiles:
        tile_min_x = tile_x * TILE_SIZE_METERS - HALO_METERS
        tile_min_z = tile_z * TILE_SIZE_METERS - HALO_METERS
        start_x = int((tile_min_x - min_x) / SAMPLE_SPACING_METERS)
        start_z = int((tile_min_z - min_z) / SAMPLE_SPACING_METERS)
        mask[
            start_z : start_z + TILE_SAMPLE_SIDE,
            start_x : start_x + TILE_SAMPLE_SIDE,
        ] = True
    require(bool(numpy.any(mask)), "RequiredGlobalLatticeUnionEmpty")
    return mask


def archive_entries(archive: zipfile.ZipFile, stem: str) -> dict[str, str]:
    required = (".dbf", ".prj", ".sbn", ".sbx", ".shp", ".shp.xml", ".shx")
    result: dict[str, str] = {}
    for extension in required:
        matches = [
            value
            for value in archive.namelist()
            if Path(value).name.lower() == (stem + extension).lower()
        ]
        require(len(matches) == 1, f"SourceArchiveEntryInvalid:{stem}:{extension}")
        result[extension] = matches[0]
    return result


def iter_line_members(geometry: Any, dependencies: Any) -> Iterator[Any]:
    if isinstance(geometry, dependencies.LineString):
        if not geometry.is_empty and geometry.length > 0:
            yield geometry
        return
    if isinstance(geometry, dependencies.MultiLineString):
        for member in geometry.geoms:
            yield from iter_line_members(member, dependencies)
        return
    if isinstance(geometry, dependencies.GeometryCollection):
        for member in geometry.geoms:
            yield from iter_line_members(member, dependencies)


def sample_line_coordinates(line: Any, spacing: float, numpy: Any) -> tuple[Any, Any]:
    coordinates = numpy.asarray(line.coords, dtype="float64")
    require(coordinates.ndim == 2 and coordinates.shape[0] >= 2, "ContourLineCoordinatesInvalid")
    deltas = coordinates[1:] - coordinates[:-1]
    segment_lengths = numpy.hypot(deltas[:, 0], deltas[:, 1])
    cumulative = numpy.concatenate((numpy.asarray([0.0]), numpy.cumsum(segment_lengths)))
    length = float(cumulative[-1])
    require(math.isfinite(length) and length > 0.0, "ContourLineLengthInvalid")
    distances = numpy.arange(0.0, length, spacing, dtype="float64")
    if distances.size == 0 or abs(float(distances[-1]) - length) > 1e-9:
        distances = numpy.concatenate((distances, numpy.asarray([length])))
    indices = numpy.searchsorted(cumulative, distances, side="right") - 1
    indices = numpy.clip(indices, 0, len(segment_lengths) - 1)
    denominator = segment_lengths[indices]
    require(bool(numpy.all(denominator > 0.0)), "ContourZeroLengthSegmentAfterClip")
    fractions = (distances - cumulative[indices]) / denominator
    sampled = coordinates[indices] + deltas[indices] * fractions[:, None]
    return sampled[:, 0], sampled[:, 1]


def source_xy_to_local(
    source_x: Any,
    source_y: Any,
    transformer: Any,
    frame: Any,
    numpy: Any,
) -> tuple[Any, Any]:
    longitudes, latitudes = transformer.transform(source_x, source_y)
    longitude_radians = numpy.radians(numpy.asarray(longitudes, dtype="float64"))
    latitude_radians = numpy.radians(numpy.asarray(latitudes, dtype="float64"))
    eccentricity_squared = 0.0066943799901413165
    radius = 6_378_137.0 / numpy.sqrt(
        1.0 - eccentricity_squared * numpy.sin(latitude_radians) ** 2
    )
    point_x = radius * numpy.cos(latitude_radians) * numpy.cos(longitude_radians)
    point_y = radius * numpy.cos(latitude_radians) * numpy.sin(longitude_radians)
    point_z = radius * (1.0 - eccentricity_squared) * numpy.sin(latitude_radians)
    origin_x, origin_y, origin_z = frame._ecef(
        frame.origin_latitude, frame.origin_longitude
    )
    dx = point_x - origin_x
    dy = point_y - origin_y
    dz = point_z - origin_z
    origin_latitude = math.radians(frame.origin_latitude)
    origin_longitude = math.radians(frame.origin_longitude)
    east = -math.sin(origin_longitude) * dx + math.cos(origin_longitude) * dy
    north = (
        -math.sin(origin_latitude) * math.cos(origin_longitude) * dx
        - math.sin(origin_latitude) * math.sin(origin_longitude) * dy
        + math.cos(origin_latitude) * dz
    )
    return east + frame.offset_x, north + frame.offset_z


def source_selection_bounds(
    bounds: tuple[int, int, int, int],
    margin: float,
    frame: Any,
    dependencies: Any,
) -> tuple[float, float, float, float]:
    min_x, min_z, max_x, max_z = bounds
    to_source = dependencies.Transformer.from_crs(4326, 5174, always_xy=True)
    transformed: list[tuple[float, float]] = []
    for x in (min_x - margin, max_x + margin):
        for z in (min_z - margin, max_z + margin):
            longitude, latitude = frame.local_to_wgs84(x, z, dependencies)
            source_x, source_y = to_source.transform(longitude, latitude)
            transformed.append((float(source_x), float(source_y)))
    return (
        min(value[0] for value in transformed) - 5.0,
        min(value[1] for value in transformed) - 5.0,
        max(value[0] for value in transformed) + 5.0,
        max(value[1] for value in transformed) + 5.0,
    )


def bbox_intersects(first: Sequence[float], second: Sequence[float]) -> bool:
    return not (
        first[2] < second[0]
        or first[0] > second[2]
        or first[3] < second[1]
        or first[1] > second[3]
    )


def read_source_constraints(
    root: Path,
    bounds: tuple[int, int, int, int],
    frame: Any,
    boundaries: Sequence[Any],
    dependencies: Any,
) -> SourceConstraints:
    try:
        import numpy
        import shapely
        from shapely.geometry import box
    except (ModuleNotFoundError, ImportError) as exc:
        raise TerrainReviewError("TerrainGenerationDependencyMissing") from exc

    archive_path = safe_path(root, SOURCE_ARCHIVE)
    validate_file(archive_path, SOURCE_BYTES, SOURCE_HASH, "TerrainSource2025")
    receipt_path = safe_path(root, SOURCE_RECEIPT)
    validate_file(
        receipt_path,
        receipt_path.stat().st_size,
        SOURCE_RECEIPT_HASH,
        "TerrainSourceReceipt",
    )
    receipt = read_json(receipt_path)
    require(
        receipt.get("datasetId") == "OA-22241"
        and receipt.get("revisionBoundary", {}).get("candidateSourceFile")
        == "OA-22241-seoul-contour.zip"
        and receipt.get("revisionBoundary", {}).get("doNotMergeDownloadRevisions") is True
        and receipt.get("coordinateReference", {}).get("horizontalCrs") == "EPSG:5174"
        and receipt.get("coordinateReference", {}).get("verticalDatumStatus") == "Unverified",
        "TerrainSourceReceiptContractChanged",
    )

    source_bounds = source_selection_bounds(
        bounds, SOURCE_SELECTION_MARGIN_METERS, frame, dependencies
    )
    min_x, min_z, max_x, max_z = bounds
    selection_box = box(
        min_x - SOURCE_SELECTION_MARGIN_METERS,
        min_z - SOURCE_SELECTION_MARGIN_METERS,
        max_x + SOURCE_SELECTION_MARGIN_METERS,
        max_z + SOURCE_SELECTION_MARGIN_METERS,
    )
    source_to_wgs = dependencies.Transformer.from_crs(5174, 4326, always_xy=True)

    point_x: list[float] = []
    point_z: list[float] = []
    point_height: list[float] = []
    point_keys: list[str] = []
    contour_x: list[float] = []
    contour_z: list[float] = []
    contour_height: list[float] = []
    contour_keys: list[str] = []
    area_point_counts = {boundary.stable_id: 0 for boundary in boundaries}
    area_contour_counts = {boundary.stable_id: 0 for boundary in boundaries}
    source_scope_contours: set[str] = set()
    source_scope_contour_intersections = 0
    contour_selected_records = 0
    contour_source_values: list[float] = []
    point_source_values: list[float] = []
    transform_probe_delta = 0.0

    with zipfile.ZipFile(archive_path, "r") as archive:
        names = archive.namelist()
        require(len(names) == len(set(names)) == 17, "TerrainSourceArchiveEntrySetChanged")
        require(sum(value.endswith("/") for value in names) == 2, "TerrainSourceArchiveDirectorySetChanged")
        require(sum(Path(value).suffix.lower() == ".txt" for value in names) == 1, "TerrainSourceArchiveTextEntryChanged")
        contour_entries = archive_entries(archive, "N3L_F001")
        point_entries = archive_entries(archive, "N3P_F002")
        for entries in (contour_entries, point_entries):
            projection = archive.read(entries[".prj"]).decode("utf-8-sig")
            crs = dependencies.CRS.from_wkt(projection)
            require(crs.to_authority() == ("EPSG", "5174"), "TerrainSourceCrsMismatch")

        point_reader = dependencies.shapefile.Reader(
            shp=io.BytesIO(archive.read(point_entries[".shp"])),
            shx=io.BytesIO(archive.read(point_entries[".shx"])),
            dbf=io.BytesIO(archive.read(point_entries[".dbf"])),
            encoding="cp949",
            encodingErrors="strict",
        )
        try:
            require(
                len(point_reader) == EXPECTED_POINT_RECORDS and point_reader.shapeType == 1,
                "TerrainPointRecordContractChanged",
            )
            fields = {value[0] for value in point_reader.fields[1:]}
            require({"UFID", "NUME", "HEIGHT"}.issubset(fields), "TerrainPointFieldsChanged")
            all_source_x: list[float] = []
            all_source_y: list[float] = []
            all_height: list[float] = []
            all_keys: list[str] = []
            observed_keys: set[str] = set()
            for record_index, shape_record in enumerate(point_reader.iterShapeRecords()):
                properties = shape_record.record.as_dict()
                stable_key = str(properties["UFID"]).strip()
                height = float(properties["HEIGHT"])
                require(
                    shape_record.shape.shapeType == 1
                    and len(shape_record.shape.points) == 1
                    and stable_key
                    and stable_key not in observed_keys
                    and math.isfinite(height)
                    and float(properties["NUME"]) == height,
                    f"TerrainPointRecordInvalid:{record_index}",
                )
                observed_keys.add(stable_key)
                source_x, source_y = shape_record.shape.points[0]
                all_source_x.append(float(source_x))
                all_source_y.append(float(source_y))
                all_height.append(height)
                all_keys.append(stable_key)
                point_source_values.append(height)
            local_x, local_z = source_xy_to_local(
                numpy.asarray(all_source_x),
                numpy.asarray(all_source_y),
                source_to_wgs,
                frame,
                numpy,
            )
            for index in range(len(all_height)):
                x = float(local_x[index])
                z = float(local_z[index])
                if not (
                    min_x - SOURCE_SELECTION_MARGIN_METERS <= x <= max_x + SOURCE_SELECTION_MARGIN_METERS
                    and min_z - SOURCE_SELECTION_MARGIN_METERS <= z <= max_z + SOURCE_SELECTION_MARGIN_METERS
                ):
                    continue
                point_x.append(x)
                point_z.append(z)
                point_height.append(all_height[index])
                point_keys.append(all_keys[index])
                point = dependencies.Point(x, z)
                for boundary in boundaries:
                    if boundary.geometry.covers(point):
                        area_point_counts[boundary.stable_id] += 1
            for index in range(min(16, len(point_x))):
                source_index = all_keys.index(point_keys[index])
                longitude, latitude = source_to_wgs.transform(
                    all_source_x[source_index], all_source_y[source_index]
                )
                scalar_x, scalar_z = frame.wgs84_to_local(longitude, latitude)
                transform_probe_delta = max(
                    transform_probe_delta,
                    abs(scalar_x - point_x[index]),
                    abs(scalar_z - point_z[index]),
                )
        finally:
            point_reader.close()

        contour_reader = dependencies.shapefile.Reader(
            shp=io.BytesIO(archive.read(contour_entries[".shp"])),
            shx=io.BytesIO(archive.read(contour_entries[".shx"])),
            dbf=io.BytesIO(archive.read(contour_entries[".dbf"])),
            encoding="cp949",
            encodingErrors="strict",
        )
        try:
            require(
                len(contour_reader) == EXPECTED_CONTOUR_RECORDS
                and contour_reader.shapeType == 3,
                "TerrainContourRecordContractChanged",
            )
            fields = {value[0] for value in contour_reader.fields[1:]}
            require({"UFID", "CONT", "HEIGHT"}.issubset(fields), "TerrainContourFieldsChanged")
            observed_keys: set[str] = set()
            for record_index, shape_record in enumerate(contour_reader.iterShapeRecords()):
                properties = shape_record.record.as_dict()
                stable_key = str(properties["UFID"]).strip()
                height = float(properties["HEIGHT"])
                require(
                    shape_record.shape.shapeType == 3
                    and stable_key
                    and stable_key not in observed_keys
                    and math.isfinite(height)
                    and float(properties["CONT"]) == height,
                    f"TerrainContourRecordInvalid:{record_index}",
                )
                observed_keys.add(stable_key)
                contour_source_values.append(height)
                if not bbox_intersects(shape_record.shape.bbox, source_bounds):
                    continue
                starts = list(shape_record.shape.parts) + [len(shape_record.shape.points)]
                local_lines: list[Any] = []
                for part_index in range(len(starts) - 1):
                    source_points = shape_record.shape.points[
                        starts[part_index] : starts[part_index + 1]
                    ]
                    if len(source_points) < 2:
                        continue
                    source_array = numpy.asarray(source_points, dtype="float64")
                    local_part_x, local_part_z = source_xy_to_local(
                        source_array[:, 0],
                        source_array[:, 1],
                        source_to_wgs,
                        frame,
                        numpy,
                    )
                    line = dependencies.LineString(
                        numpy.column_stack((local_part_x, local_part_z))
                    )
                    if line.is_empty or line.length <= 0:
                        continue
                    local_lines.append(line)
                if not local_lines:
                    continue
                geometry = (
                    local_lines[0]
                    if len(local_lines) == 1
                    else dependencies.MultiLineString(local_lines)
                )
                clipped = geometry.intersection(selection_box)
                members = list(iter_line_members(clipped, dependencies))
                if not members:
                    continue
                contour_selected_records += 1
                for boundary in boundaries:
                    if boundary.geometry.intersects(geometry):
                        area_contour_counts[boundary.stable_id] += 1
                        source_scope_contours.add(stable_key)
                        source_scope_contour_intersections += 1
                for member_index, member in enumerate(members):
                    sampled_x, sampled_z = sample_line_coordinates(
                        member, CONTOUR_SAMPLE_SPACING_METERS, numpy
                    )
                    for sample_index in range(len(sampled_x)):
                        contour_x.append(float(sampled_x[sample_index]))
                        contour_z.append(float(sampled_z[sample_index]))
                        contour_height.append(height)
                        contour_keys.append(
                            f"{stable_key}:{member_index}:{sample_index}"
                        )
        finally:
            contour_reader.close()

    require(
        sum(area_point_counts.values()) == EXPECTED_ASSIGNED_POINTS
        and len(source_scope_contours) == EXPECTED_SCOPE_CONTOURS
        and source_scope_contour_intersections == EXPECTED_SCOPE_CONTOUR_INTERSECTIONS
        and all(value > 0 for value in area_point_counts.values())
        and all(value > 0 for value in area_contour_counts.values()),
        "TerrainSourceAdministrativeCoverageChanged",
    )
    require(point_x and contour_x, "TerrainSourceSelectionEmpty")
    all_x = numpy.asarray(point_x + contour_x, dtype="float64")
    all_z = numpy.asarray(point_z + contour_z, dtype="float64")
    all_height = numpy.asarray(point_height + contour_height, dtype="float64")
    kinds = numpy.asarray(
        [0] * len(point_x) + [1] * len(contour_x), dtype="uint8"
    )
    require(
        bool(numpy.all(numpy.isfinite(all_x)))
        and bool(numpy.all(numpy.isfinite(all_z)))
        and bool(numpy.all(numpy.isfinite(all_height))),
        "TerrainSourceContainsNonFiniteValue",
    )
    source_point_indices = list(range(len(point_x)))
    core_point_indices = [
        index
        for index, (x, z) in enumerate(zip(point_x, point_z))
        if min_x <= x <= max_x and min_z <= z <= max_z
    ]
    core_contour_indices = [
        len(point_x) + index
        for index, (x, z) in enumerate(zip(contour_x, contour_z))
        if min_x <= x <= max_x and min_z <= z <= max_z
    ]
    area_coverage = {
        boundary.stable_id: {
            "sourceElevationPointCount": area_point_counts[boundary.stable_id],
            "sourceContourRecordIntersectionCount": area_contour_counts[
                boundary.stable_id
            ],
        }
        for boundary in boundaries
    }
    return SourceConstraints(
        x=all_x,
        z=all_z,
        height=all_height,
        kind=kinds,
        stable_keys=point_keys + contour_keys,
        source_point_indices=source_point_indices,
        core_point_indices=core_point_indices,
        core_contour_indices=core_contour_indices,
        selected_point_count=len(point_x),
        selected_contour_record_count=contour_selected_records,
        contour_sample_count=len(contour_x),
        source_numeric_minimum=float(numpy.min(all_height)),
        source_numeric_maximum=float(numpy.max(all_height)),
        area_coverage=area_coverage,
        source_audit={
            "archiveEntryCount": 17,
            "contourRecordCount": EXPECTED_CONTOUR_RECORDS,
            "elevationPointRecordCount": EXPECTED_POINT_RECORDS,
            "selectedElevationPointConstraintCount": len(point_x),
            "selectedContourRecordCount": contour_selected_records,
            "derivedContourConstraintCount": len(contour_x),
            "contourConstraintSpacingHorizontalMeters": CONTOUR_SAMPLE_SPACING_METERS,
            "sourceSelectionMarginHorizontalMeters": SOURCE_SELECTION_MARGIN_METERS,
            "scopeAssignedElevationPointCount": sum(area_point_counts.values()),
            "scopeUniqueContourRecordCount": len(source_scope_contours),
            "scopeContourRecordIntersectionCount": source_scope_contour_intersections,
            "sourceAllPointNumericMinimum": min(point_source_values),
            "sourceAllPointNumericMaximum": max(point_source_values),
            "sourceAllContourNumericMinimum": min(contour_source_values),
            "sourceAllContourNumericMaximum": max(contour_source_values),
            "selectedConstraintNumericMinimum": float(numpy.min(all_height)),
            "selectedConstraintNumericMaximum": float(numpy.max(all_height)),
            "horizontalTransformVectorScalarMaxDeltaMeters": transform_probe_delta,
            "sourceRevisionMixingDetected": False,
            "source2023SlopeZipRead": False,
        },
    )


class BucketIndex:
    def __init__(self, source: SourceConstraints, numpy: Any) -> None:
        self.source = source
        self.numpy = numpy
        self.buckets: dict[tuple[int, int], list[int]] = defaultdict(list)
        for index in range(len(source.x)):
            key = (
                math.floor(float(source.x[index]) / SPATIAL_BUCKET_METERS),
                math.floor(float(source.z[index]) / SPATIAL_BUCKET_METERS),
            )
            self.buckets[key].append(index)
        for values in self.buckets.values():
            values.sort()

    def candidates(self, bucket_x: int, bucket_z: int, radius: float) -> Any:
        ring = int(math.ceil(radius / SPATIAL_BUCKET_METERS)) + 1
        values: list[int] = []
        for current_z in range(bucket_z - ring, bucket_z + ring + 1):
            for current_x in range(bucket_x - ring, bucket_x + ring + 1):
                values.extend(self.buckets.get((current_x, current_z), ()))
        require(values, "InterpolationSourceGap")
        return self.numpy.asarray(values, dtype="int64")

    def interpolate_points(
        self,
        x: Any,
        z: Any,
        candidate_indices: Any,
        excluded_indices: set[int] | None = None,
        enforce_search_radius: bool = True,
    ) -> tuple[Any, Any]:
        numpy = self.numpy
        if excluded_indices:
            candidate_indices = numpy.asarray(
                [value for value in candidate_indices if int(value) not in excluded_indices],
                dtype="int64",
            )
        require(candidate_indices.size > 0, "InterpolationCandidatesEmpty")
        dx = x[:, None] - self.source.x[candidate_indices][None, :]
        dz = z[:, None] - self.source.z[candidate_indices][None, :]
        distances_squared = dx * dx + dz * dz
        count = min(INTERPOLATION_NEIGHBOR_COUNT, candidate_indices.size)
        selected_columns = numpy.argpartition(
            distances_squared, count - 1, axis=1
        )[:, :count]
        selected_distance = numpy.take_along_axis(
            distances_squared, selected_columns, axis=1
        )
        selected_source_indices = candidate_indices[selected_columns]
        selected_height = self.source.height[selected_source_indices]
        valid = (
            selected_distance <= INTERPOLATION_SEARCH_RADIUS_METERS ** 2
            if enforce_search_radius
            else numpy.ones_like(selected_distance, dtype="bool")
        )
        nearest = numpy.sqrt(numpy.min(selected_distance, axis=1))
        if enforce_search_radius and not bool(
            numpy.all(nearest <= INTERPOLATION_SEARCH_RADIUS_METERS)
        ):
            maximum_index = int(numpy.argmax(nearest))
            raise TerrainReviewError(
                "InterpolationSearchRadiusExceeded:"
                + format(float(nearest[maximum_index]), ".6f")
                + ":x"
                + format(float(x[maximum_index]), ".3f")
                + ":z"
                + format(float(z[maximum_index]), ".3f")
            )
        exact = selected_distance <= 1e-12
        exact_count = numpy.sum(exact, axis=1)
        safe_distance = numpy.maximum(selected_distance, 1e-6)
        weights = numpy.where(valid, 1.0 / (safe_distance ** (INTERPOLATION_POWER / 2.0)), 0.0)
        weight_sum = numpy.sum(weights, axis=1)
        require(bool(numpy.all(weight_sum > 0.0)), "InterpolationWeightSumInvalid")
        values = numpy.sum(weights * selected_height, axis=1) / weight_sum
        exact_rows = exact_count > 0
        if bool(numpy.any(exact_rows)):
            values[exact_rows] = (
                numpy.sum(numpy.where(exact, selected_height, 0.0), axis=1)[exact_rows]
                / exact_count[exact_rows]
            )
        return values, nearest


def interpolate_global_grid(
    source: SourceConstraints,
    bounds: tuple[int, int, int, int],
    required_mask: Any,
) -> tuple[Any, Any, dict[str, Any], BucketIndex]:
    try:
        import numpy
    except (ModuleNotFoundError, ImportError) as exc:
        raise TerrainReviewError("TerrainGenerationDependencyMissing") from exc
    min_x, min_z, max_x, max_z = bounds
    x_axis = numpy.arange(min_x, max_x + SAMPLE_SPACING_METERS, SAMPLE_SPACING_METERS, dtype="float64")
    z_axis = numpy.arange(min_z, max_z + SAMPLE_SPACING_METERS, SAMPLE_SPACING_METERS, dtype="float64")
    expected_width = int((max_x - min_x) / SAMPLE_SPACING_METERS) + 1
    expected_height = int((max_z - min_z) / SAMPLE_SPACING_METERS) + 1
    require(
        len(x_axis) == expected_width and len(z_axis) == expected_height,
        "GlobalLatticeDimensionsInvalid",
    )
    require(
        required_mask.shape == (len(z_axis), len(x_axis)),
        "RequiredGlobalLatticeMaskDimensionsInvalid",
    )
    values = numpy.full((len(z_axis), len(x_axis)), numpy.nan, dtype="float64")
    nearest = numpy.full_like(values, numpy.nan)
    index = BucketIndex(source, numpy)
    x_buckets: dict[int, list[int]] = defaultdict(list)
    z_buckets: dict[int, list[int]] = defaultdict(list)
    for column, x in enumerate(x_axis):
        x_buckets[math.floor(float(x) / SPATIAL_BUCKET_METERS)].append(column)
    for row, z in enumerate(z_axis):
        z_buckets[math.floor(float(z) / SPATIAL_BUCKET_METERS)].append(row)
    for bucket_z, rows in sorted(z_buckets.items()):
        for bucket_x, columns in sorted(x_buckets.items()):
            required_block = required_mask[numpy.ix_(rows, columns)]
            local_rows, local_columns = numpy.nonzero(required_block)
            if local_rows.size == 0:
                continue
            flat_x = x_axis[numpy.asarray(columns, dtype="int64")[local_columns]]
            flat_z = z_axis[numpy.asarray(rows, dtype="int64")[local_rows]]
            candidates = index.candidates(
                bucket_x, bucket_z, INTERPOLATION_INITIAL_WINDOW_METERS
            )
            interpolated, distances = index.interpolate_points(
                flat_x,
                flat_z,
                candidates,
                enforce_search_radius=False,
            )
            if float(numpy.max(distances)) > INTERPOLATION_REQUERY_THRESHOLD_METERS:
                candidates = index.candidates(
                    bucket_x, bucket_z, INTERPOLATION_SEARCH_RADIUS_METERS
                )
                interpolated, distances = index.interpolate_points(
                    flat_x, flat_z, candidates
                )
            global_rows = numpy.asarray(rows, dtype="int64")[local_rows]
            global_columns = numpy.asarray(columns, dtype="int64")[local_columns]
            values[global_rows, global_columns] = interpolated
            nearest[global_rows, global_columns] = distances
    require(
        bool(numpy.all(numpy.isfinite(values[required_mask])))
        and bool(numpy.all(numpy.isfinite(nearest[required_mask])))
        and bool(numpy.all(numpy.isnan(values[~required_mask])))
        and bool(numpy.all(numpy.isnan(nearest[~required_mask]))),
        "GlobalLatticeContainsNonFiniteValue",
    )
    common_reference = source.source_numeric_minimum
    relative = (values - common_reference).astype("<f4")
    require(
        bool(numpy.all(numpy.isfinite(relative[required_mask])))
        and bool(numpy.all(numpy.isnan(relative[~required_mask]))),
        "RelativeHeightContainsInvalidRequiredValue",
    )
    required_values = values[required_mask]
    required_relative = relative[required_mask]
    required_nearest = nearest[required_mask]
    audit = {
        "algorithmCode": "DeterministicCombinedConstraintIdw",
        "algorithmRevision": GENERATOR_REVISION,
        "power": INTERPOLATION_POWER,
        "nearestConstraintCount": INTERPOLATION_NEIGHBOR_COUNT,
        "maximumSearchRadiusHorizontalMeters": INTERPOLATION_SEARCH_RADIUS_METERS,
        "spatialBucketHorizontalMeters": SPATIAL_BUCKET_METERS,
        "globalInterpolationPassCount": 1,
        "flatFallbackApplied": False,
        "missingSampleFallbackCount": 0,
        "commonVerticalReferenceSourceValue": common_reference,
        "rectangularEnvelopeSampleCount": int(values.size),
        "requiredUnionSampleCount": int(numpy.count_nonzero(required_mask)),
        "unusedEnvelopeSampleCount": int(numpy.count_nonzero(~required_mask)),
        "unusedEnvelopeSamplesInterpolated": False,
        "globalRawInterpolatedNumericMinimum": float(numpy.min(required_values)),
        "globalRawInterpolatedNumericMaximum": float(numpy.max(required_values)),
        "globalRelativeFloat32Minimum": float(numpy.min(required_relative)),
        "globalRelativeFloat32Maximum": float(numpy.max(required_relative)),
        "nearestConstraintDistanceMeters": statistics(required_nearest, numpy),
        "sampleCountBeyondPreferred500Meters": int(
            numpy.count_nonzero(
                required_nearest > REVIEW_PREFERRED_NEAREST_SOURCE_DISTANCE_METERS
            )
        ),
        "sampleCountBeyondReviewMaximumDistance": int(
            numpy.count_nonzero(
                required_nearest > REVIEW_MAX_NEAREST_SOURCE_DISTANCE_METERS
            )
        ),
    }
    return relative, values, audit, index


def statistics(values: Any, numpy: Any) -> dict[str, float | int]:
    array = numpy.asarray(values, dtype="float64")
    require(array.size > 0 and bool(numpy.all(numpy.isfinite(array))), "StatisticsInputInvalid")
    return {
        "count": int(array.size),
        "minimum": float(numpy.min(array)),
        "p50": float(numpy.quantile(array, 0.50)),
        "p95": float(numpy.quantile(array, 0.95)),
        "p99": float(numpy.quantile(array, 0.99)),
        "maximum": float(numpy.max(array)),
        "mean": float(numpy.mean(array)),
    }


def error_statistics(errors: Any, numpy: Any) -> dict[str, float | int]:
    absolute = numpy.abs(numpy.asarray(errors, dtype="float64"))
    result = statistics(absolute, numpy)
    result["rmse"] = float(numpy.sqrt(numpy.mean(numpy.asarray(errors) ** 2)))
    result["bias"] = float(numpy.mean(numpy.asarray(errors)))
    return result


def sample_grid_bilinear(
    grid: Any,
    bounds: tuple[int, int, int, int],
    x: Any,
    z: Any,
    numpy: Any,
) -> Any:
    min_x, min_z, max_x, max_z = bounds
    x = numpy.asarray(x, dtype="float64")
    z = numpy.asarray(z, dtype="float64")
    require(
        bool(numpy.all((x >= min_x) & (x <= max_x)))
        and bool(numpy.all((z >= min_z) & (z <= max_z))),
        "GridSampleOutsideBounds",
    )
    fx = (x - min_x) / SAMPLE_SPACING_METERS
    fz = (z - min_z) / SAMPLE_SPACING_METERS
    x0 = numpy.floor(fx).astype("int64")
    z0 = numpy.floor(fz).astype("int64")
    x1 = numpy.minimum(x0 + 1, grid.shape[1] - 1)
    z1 = numpy.minimum(z0 + 1, grid.shape[0] - 1)
    tx = fx - x0
    tz = fz - z0
    return (
        grid[z0, x0] * (1.0 - tx) * (1.0 - tz)
        + grid[z0, x1] * tx * (1.0 - tz)
        + grid[z1, x0] * (1.0 - tx) * tz
        + grid[z1, x1] * tx * tz
    )


def bilinear_supported_indices(
    grid: Any,
    bounds: tuple[int, int, int, int],
    source: SourceConstraints,
    indices: Sequence[int],
    numpy: Any,
) -> Any:
    min_x, min_z, max_x, max_z = bounds
    candidate = numpy.asarray(indices, dtype="int64")
    x = source.x[candidate]
    z = source.z[candidate]
    inside = (x >= min_x) & (x <= max_x) & (z >= min_z) & (z <= max_z)
    candidate = candidate[inside]
    x = source.x[candidate]
    z = source.z[candidate]
    fx = (x - min_x) / SAMPLE_SPACING_METERS
    fz = (z - min_z) / SAMPLE_SPACING_METERS
    x0 = numpy.floor(fx).astype("int64")
    z0 = numpy.floor(fz).astype("int64")
    x1 = numpy.minimum(x0 + 1, grid.shape[1] - 1)
    z1 = numpy.minimum(z0 + 1, grid.shape[0] - 1)
    supported = (
        numpy.isfinite(grid[z0, x0])
        & numpy.isfinite(grid[z0, x1])
        & numpy.isfinite(grid[z1, x0])
        & numpy.isfinite(grid[z1, x1])
    )
    return candidate[supported]


def source_fit_audit(
    source: SourceConstraints,
    relative_grid: Any,
    bounds: tuple[int, int, int, int],
    bucket_index: BucketIndex,
) -> dict[str, Any]:
    numpy = bucket_index.numpy
    absolute_grid = relative_grid.astype("float64") + source.source_numeric_minimum
    point_indices = bilinear_supported_indices(
        absolute_grid,
        bounds,
        source,
        source.core_point_indices,
        numpy,
    )
    contour_indices = bilinear_supported_indices(
        absolute_grid,
        bounds,
        source,
        source.core_contour_indices,
        numpy,
    )
    require(
        point_indices.size >= 100 and contour_indices.size >= 100,
        "SourceFitRequiredUnionSampleTooSmall",
    )
    point_sample = sample_grid_bilinear(
        absolute_grid,
        bounds,
        source.x[point_indices],
        source.z[point_indices],
        numpy,
    )
    contour_sample = sample_grid_bilinear(
        absolute_grid,
        bounds,
        source.x[contour_indices],
        source.z[contour_indices],
        numpy,
    )
    point_errors = point_sample - source.height[point_indices]
    contour_errors = contour_sample - source.height[contour_indices]

    holdout_indices = [
        index
        for index in point_indices.tolist()
        if int(hashlib.sha256(source.stable_keys[index].encode("utf-8")).hexdigest()[:8], 16)
        % 10
        == 0
    ]
    require(len(holdout_indices) >= 100, "PointHoldoutSampleTooSmall")
    holdout_errors: list[float] = []
    holdout_nearest: list[float] = []
    for source_index in holdout_indices:
        x = float(source.x[source_index])
        z = float(source.z[source_index])
        bucket_x = math.floor(x / SPATIAL_BUCKET_METERS)
        bucket_z = math.floor(z / SPATIAL_BUCKET_METERS)
        candidates = bucket_index.candidates(
            bucket_x, bucket_z, INTERPOLATION_SEARCH_RADIUS_METERS
        )
        value, distance = bucket_index.interpolate_points(
            numpy.asarray([x]),
            numpy.asarray([z]),
            candidates,
            {source_index},
        )
        holdout_errors.append(float(value[0] - source.height[source_index]))
        holdout_nearest.append(float(distance[0]))
    return {
        "heightMeaningCode": "SourceNumericHeightOnly",
        "heightUnitSourceDeclared": False,
        "verticalDatumVerified": False,
        "absoluteElevationAuthorized": False,
        "elevationPointGridFitInSampleSourceValue": error_statistics(
            point_errors, numpy
        ),
        "contourConstraintGridFitInSampleSourceValue": error_statistics(
            contour_errors, numpy
        ),
        "elevationPointDeterministicTenPercentHoldoutSourceValue": error_statistics(
            numpy.asarray(holdout_errors), numpy
        ),
        "elevationPointHoldoutNearestConstraintDistanceMeters": statistics(
            numpy.asarray(holdout_nearest), numpy
        ),
        "limitations": [
            "VerticalDatumUnverified",
            "HeightUnitNotSourceDeclared",
            "IdwDoesNotGuaranteeContourHonoringBetweenSamples",
            "ContourLinesWereSampledEvery40HorizontalMeters",
            "InSampleResidualsAreNotIndependentAccuracyEvidence",
            "TenPercentPointHoldoutStillUsesNearbyContourConstraints",
            "HistoricalAdministrativeBoundaryIsMaskBootstrapOnly",
            "NoSlopeOrHydrologyConditioning",
            "NoAbsoluteElevationOrPlacementAuthority",
        ],
    }


def tile_payloads(
    relative_grid: Any,
    bounds: tuple[int, int, int, int],
    unique_tiles: Sequence[tuple[int, int]],
) -> tuple[dict[str, bytes], list[dict[str, Any]], dict[str, Any]]:
    try:
        import numpy
    except (ModuleNotFoundError, ImportError) as exc:
        raise TerrainReviewError("TerrainGenerationDependencyMissing") from exc
    min_x, min_z, _, _ = bounds
    files: dict[str, bytes] = {}
    entries: list[dict[str, Any]] = []
    coordinate_bytes: dict[tuple[int, int], bytes] = {}
    coordinate_occurrences: dict[tuple[int, int], int] = defaultdict(int)
    seam_mismatches = 0
    global_mismatches = 0
    for tile_x, tile_z in unique_tiles:
        tile_min_x = tile_x * TILE_SIZE_METERS - HALO_METERS
        tile_min_z = tile_z * TILE_SIZE_METERS - HALO_METERS
        start_x = int((tile_min_x - min_x) / SAMPLE_SPACING_METERS)
        start_z = int((tile_min_z - min_z) / SAMPLE_SPACING_METERS)
        ascending = relative_grid[
            start_z : start_z + TILE_SAMPLE_SIDE,
            start_x : start_x + TILE_SAMPLE_SIDE,
        ]
        require(
            ascending.shape == (TILE_SAMPLE_SIDE, TILE_SAMPLE_SIDE),
            "TileSliceDimensionsInvalid",
        )
        north_first = numpy.flipud(ascending).astype("<f4", copy=False)
        payload = north_first.tobytes(order="C")
        relative = f"tiles/x{tile_x}_z{tile_z}.{FLOAT32_FORMAT}.bin"
        files[relative] = payload
        entries.append(
            {
                "tileStableId": f"terrain-tile:common-enu:500m:x{tile_x}:z{tile_z}",
                "tileIndexX": tile_x,
                "tileIndexZ": tile_z,
                "coreBoundsCommonEnuMeters": {
                    "minX": tile_x * TILE_SIZE_METERS,
                    "minZ": tile_z * TILE_SIZE_METERS,
                    "maxX": (tile_x + 1) * TILE_SIZE_METERS,
                    "maxZ": (tile_z + 1) * TILE_SIZE_METERS,
                },
                "generationBoundsCommonEnuMeters": {
                    "minX": tile_min_x,
                    "minZ": tile_min_z,
                    "maxX": (tile_x + 1) * TILE_SIZE_METERS + HALO_METERS,
                    "maxZ": (tile_z + 1) * TILE_SIZE_METERS + HALO_METERS,
                },
                "relativePath": relative,
                "sha256": sha_bytes(payload),
                "byteLength": len(payload),
                "width": TILE_SAMPLE_SIDE,
                "height": TILE_SAMPLE_SIDE,
                "formatCode": FLOAT32_FORMAT,
                "rowOrder": ROW_ORDER,
            }
        )
        for row in range(TILE_SAMPLE_SIDE):
            z = (tile_z + 1) * TILE_SIZE_METERS + HALO_METERS - row * SAMPLE_SPACING_METERS
            global_row = int((z - min_z) / SAMPLE_SPACING_METERS)
            for column in range(TILE_SAMPLE_SIDE):
                x = tile_min_x + column * SAMPLE_SPACING_METERS
                global_column = int((x - min_x) / SAMPLE_SPACING_METERS)
                offset = (row * TILE_SAMPLE_SIDE + column) * 4
                value_bytes = payload[offset : offset + 4]
                global_bytes = struct.pack(
                    "<f", float(relative_grid[global_row, global_column])
                )
                if value_bytes != global_bytes:
                    global_mismatches += 1
                key = (x, z)
                previous = coordinate_bytes.get(key)
                if previous is not None and previous != value_bytes:
                    seam_mismatches += 1
                else:
                    coordinate_bytes[key] = value_bytes
                coordinate_occurrences[key] += 1
    shared_keys = sum(value > 1 for value in coordinate_occurrences.values())
    comparisons = sum(value - 1 for value in coordinate_occurrences.values())
    require(
        seam_mismatches == 0 and global_mismatches == 0 and shared_keys > 0,
        "TileSharedGridBitEqualityFailed",
    )
    return files, entries, {
        "sharedGridKeyCount": shared_keys,
        "sharedGridKeyComparisonCount": comparisons,
        "sharedGridKeyBitMismatchCount": seam_mismatches,
        "globalToTileBitMismatchCount": global_mismatches,
        "sharedEdgeBitEquality": True,
        "overlappingHaloBitEquality": True,
    }


def sparse_global_payloads(
    relative_grid: Any,
    required_mask: Any,
    bounds: tuple[int, int, int, int],
) -> tuple[bytes, bytes, dict[str, Any]]:
    try:
        import numpy
    except (ModuleNotFoundError, ImportError) as exc:
        raise TerrainReviewError("TerrainGenerationDependencyMissing") from exc
    min_x, min_z, _, _ = bounds
    key_parts: list[bytes] = []
    value_parts: list[bytes] = []
    previous: tuple[int, int] | None = None
    sample_count = 0
    for row in range(relative_grid.shape[0] - 1, -1, -1):
        z = min_z + row * SAMPLE_SPACING_METERS
        for column in numpy.flatnonzero(required_mask[row]):
            x = min_x + int(column) * SAMPLE_SPACING_METERS
            key = (x, z)
            if previous is not None:
                require(
                    key[1] < previous[1]
                    or (key[1] == previous[1] and key[0] > previous[0]),
                    "SparseGlobalGridKeyOrderInvalid",
                )
            value = float(relative_grid[row, int(column)])
            require(math.isfinite(value), "SparseGlobalHeightNonFinite")
            key_parts.append(struct.pack("<ii", x, z))
            value_parts.append(struct.pack("<f", value))
            previous = key
            sample_count += 1
    require(
        sample_count == int(numpy.count_nonzero(required_mask)),
        "SparseGlobalSampleCountMismatch",
    )
    return b"".join(key_parts), b"".join(value_parts), {
        "sampleCount": sample_count,
        "keyFormatCode": "common-enu-xz-i32-meters-v1",
        "keyStrideBytes": 8,
        "heightFormatCode": FLOAT32_FORMAT,
        "heightStrideBytes": 4,
        "rowOrder": ROW_ORDER,
        "containsUnusedEnvelopeSamples": False,
    }


def mask_payloads(
    memberships: Sequence[TileMembership],
    boundaries: Sequence[Any],
) -> tuple[dict[str, bytes], list[dict[str, Any]], dict[str, Any]]:
    try:
        import numpy
        import shapely
    except (ModuleNotFoundError, ImportError) as exc:
        raise TerrainReviewError("TerrainGenerationDependencyMissing") from exc
    boundary_by_code = {boundary.code: boundary for boundary in boundaries}
    files: dict[str, bytes] = {}
    areas: dict[str, dict[str, Any]] = {}
    zero_mask_count = 0
    total_inside = 0
    for membership in memberships:
        boundary = boundary_by_code.get(membership.area_code)
        require(boundary is not None, "MaskBoundaryMissing")
        min_x = membership.tile_x * TILE_SIZE_METERS - HALO_METERS
        max_z = (membership.tile_z + 1) * TILE_SIZE_METERS + HALO_METERS
        x_axis = numpy.arange(
            min_x,
            min_x + TILE_SAMPLE_SIDE * SAMPLE_SPACING_METERS,
            SAMPLE_SPACING_METERS,
            dtype="float64",
        )
        z_axis = numpy.arange(
            max_z,
            max_z - TILE_SAMPLE_SIDE * SAMPLE_SPACING_METERS,
            -SAMPLE_SPACING_METERS,
            dtype="float64",
        )
        xx, zz = numpy.meshgrid(x_axis, z_axis)
        mask = shapely.intersects_xy(boundary.geometry, xx, zz).astype("uint8")
        payload = mask.tobytes(order="C")
        inside = int(numpy.count_nonzero(mask))
        core = mask[6:57, 6:57]
        core_inside = int(numpy.count_nonzero(core))
        zero_mask_count += int(inside == 0)
        total_inside += inside
        relative = (
            f"masks/{membership.area_code}/x{membership.tile_x}_z{membership.tile_z}."
            f"{MASK_FORMAT}.bin"
        )
        require(relative not in files, "MaskArtifactDuplicate")
        files[relative] = payload
        entry = {
            "tileIndexX": membership.tile_x,
            "tileIndexZ": membership.tile_z,
            "relativePath": relative,
            "sha256": sha_bytes(payload),
            "byteLength": len(payload),
            "width": TILE_SAMPLE_SIDE,
            "height": TILE_SAMPLE_SIDE,
            "formatCode": MASK_FORMAT,
            "rowOrder": ROW_ORDER,
            "insideBoundarySampleCount": inside,
            "insideBoundaryCoreSampleCount": core_inside,
        }
        area = areas.setdefault(
            membership.area_stable_id,
            {
                "administrativeAreaStableId": membership.area_stable_id,
                "historicalBoundaryBootstrapOnly": True,
                "heightSurfaceReinterpolatedForArea": False,
                "maskTiles": [],
            },
        )
        area["maskTiles"].append(entry)
    for area in areas.values():
        area["maskTiles"].sort(key=lambda value: (value["tileIndexX"], value["tileIndexZ"]))
        area["tileMembershipCount"] = len(area["maskTiles"])
        area["insideBoundarySampleCount"] = sum(
            value["insideBoundarySampleCount"] for value in area["maskTiles"]
        )
        require(area["insideBoundarySampleCount"] > 0, "AdministrativeAreaMaskCoverageEmpty")
    require(len(areas) == EXPECTED_AREA_COUNT, "AdministrativeAreaMaskCoverageChanged")
    return files, [areas[key] for key in sorted(areas)], {
        "maskArtifactCount": len(files),
        "zeroInsideSampleMaskArtifactCount": zero_mask_count,
        "totalInsideBoundarySampleOccurrences": total_inside,
        "historicalBoundaryUsedForInterpolation": False,
        "historicalBoundaryUsedForClippingOrMaskOnly": True,
    }


def descriptor(relative: str, payload: bytes, **extra: Any) -> dict[str, Any]:
    return {
        "relativePath": relative,
        "sha256": sha_bytes(payload),
        "byteLength": len(payload),
        **extra,
    }


def authority_flags() -> dict[str, bool]:
    return {
        "privateReviewOnly": True,
        "publicDisplayAllowed": False,
        "databaseWriteAttempted": False,
        "mongoWriteAttempted": False,
        "currentPointerRead": False,
        "currentPointerUsed": False,
        "currentPointerUpdated": False,
        "publicationAttempted": False,
        "distributionApproved": False,
        "unityApplyAttempted": False,
        "runtimeAuthorized": False,
        "colliderAllowed": False,
        "traversalReady": False,
        "gameplayReady": False,
        "absoluteElevationAuthorized": False,
    }


def prepare(root: Path) -> tuple[str, dict[str, bytes], dict[str, Any]]:
    root = root.resolve()
    source_path = safe_path(root, SOURCE_ARCHIVE)
    receipt_path = safe_path(root, SOURCE_RECEIPT)
    boundary_path = safe_path(root, BOUNDARY_ARCHIVE)
    validate_file(source_path, SOURCE_BYTES, SOURCE_HASH, "TerrainSource2025")
    validate_file(receipt_path, receipt_path.stat().st_size, SOURCE_RECEIPT_HASH, "TerrainSourceReceipt")
    validate_file(boundary_path, boundary_path.stat().st_size, BOUNDARY_HASH, "HistoricalBoundary")
    batch, dependencies, scope, frame, boundaries = load_context(root)
    memberships, unique_tiles, base_audit = load_tile_memberships(root)
    bounds = global_bounds(unique_tiles)
    try:
        import numpy
    except (ModuleNotFoundError, ImportError) as exc:
        raise TerrainReviewError("TerrainGenerationDependencyMissing") from exc
    required_mask = required_union_mask(bounds, unique_tiles, numpy)
    source = read_source_constraints(
        root, bounds, frame, boundaries, dependencies
    )
    relative_grid, _raw_grid, interpolation_audit, bucket_index = interpolate_global_grid(
        source, bounds, required_mask
    )
    fit_audit = source_fit_audit(source, relative_grid, bounds, bucket_index)
    tile_files, tile_entries, seam_audit = tile_payloads(
        relative_grid, bounds, unique_tiles
    )
    mask_files, area_entries, mask_audit = mask_payloads(memberships, boundaries)
    files: dict[str, bytes] = {}
    global_keys, global_heights, sparse_global_audit = sparse_global_payloads(
        relative_grid, required_mask, bounds
    )
    global_key_relative = "global/common-enu.grid-keys-i32-pair-v1.bin"
    global_height_relative = f"global/common-enu.{FLOAT32_FORMAT}.bin"
    files[global_key_relative] = global_keys
    files[global_height_relative] = global_heights
    files.update(tile_files)
    files.update(mask_files)

    generator_hash = sha_file(Path(__file__).resolve())
    batch_hash = sha_file(safe_path(root, BATCH_GENERATOR))
    scope_hash = sha_file(safe_path(root, SCOPE_DEFINITION))
    artifact_hashes = [
        {"relativePath": name, "sha256": sha_bytes(payload), "byteLength": len(payload)}
        for name, payload in sorted(files.items())
    ]
    generation_hash = sha_bytes(
        canonical_bytes(
            {
                "revision": REVISION,
                "generatorRevision": GENERATOR_REVISION,
                "generatorSha256": generator_hash,
                "boundaryReaderSha256": batch_hash,
                "scopeDefinitionSha256": scope_hash,
                "sourceSha256": SOURCE_HASH,
                "sourceReceiptSha256": SOURCE_RECEIPT_HASH,
                "historicalBoundarySha256": BOUNDARY_HASH,
                "baseReviewIndexSha256": BASE_INDEX_HASH,
                "algorithm": {
                    "tileSizeMeters": TILE_SIZE_METERS,
                    "haloMeters": HALO_METERS,
                    "sampleSpacingMeters": SAMPLE_SPACING_METERS,
                    "contourSampleSpacingMeters": CONTOUR_SAMPLE_SPACING_METERS,
                    "sourceSelectionMarginMeters": SOURCE_SELECTION_MARGIN_METERS,
                    "interpolationSearchRadiusMeters": INTERPOLATION_SEARCH_RADIUS_METERS,
                    "interpolationNeighborCount": INTERPOLATION_NEIGHBOR_COUNT,
                    "interpolationPower": INTERPOLATION_POWER,
                    "rowOrder": ROW_ORDER,
                },
                "artifacts": artifact_hashes,
            }
        )
    )

    point_fit = fit_audit["elevationPointGridFitInSampleSourceValue"]
    contour_fit = fit_audit["contourConstraintGridFitInSampleSourceValue"]
    holdout_fit = fit_audit[
        "elevationPointDeterministicTenPercentHoldoutSourceValue"
    ]
    review_gate = {
        "all30AdministrativeAreasHavePointAndContourCoverage": all(
            value["sourceElevationPointCount"] > 0
            and value["sourceContourRecordIntersectionCount"] > 0
            for value in source.area_coverage.values()
        ),
        "allGlobalSamplesFinite": interpolation_audit["missingSampleFallbackCount"] == 0,
        "maximumNearestSourceDistanceWithin1500Meters": interpolation_audit[
            "sampleCountBeyondReviewMaximumDistance"
        ]
        == 0,
        "allSamplesWithinPreferred500Meters": interpolation_audit[
            "sampleCountBeyondPreferred500Meters"
        ]
        == 0,
        "pointGridFitP95AtMost25SourceValue": point_fit["p95"]
        <= REVIEW_MAX_POINT_GRID_P95_SOURCE_VALUE,
        "contourGridFitP95AtMost25SourceValue": contour_fit["p95"]
        <= REVIEW_MAX_CONTOUR_GRID_P95_SOURCE_VALUE,
        "pointHoldoutP95AtMost50SourceValue": holdout_fit["p95"]
        <= REVIEW_MAX_POINT_HOLDOUT_P95_SOURCE_VALUE,
        "sharedGridAndHaloBitsEqual": seam_audit["sharedEdgeBitEquality"]
        and seam_audit["overlappingHaloBitEquality"],
        "sourceRevisionMixingDetected": False,
        "absoluteElevationAuthorized": False,
    }
    review_gate_passed = all(
        value is True
        for key, value in review_gate.items()
        if key
        not in {
            "sourceRevisionMixingDetected",
            "absoluteElevationAuthorized",
            "allSamplesWithinPreferred500Meters",
        }
    ) and review_gate["sourceRevisionMixingDetected"] is False and review_gate[
        "absoluteElevationAuthorized"
    ] is False
    require(review_gate_passed, "TerrainPrivateReviewQualityGateFailed")

    audit = {
        "schemaVersion": SCHEMA_AUDIT,
        "revision": REVISION,
        "generationHashSha256": generation_hash,
        "generatedAtUtc": GENERATED_AT_UTC,
        "sourceVintage": SOURCE_VINTAGE,
        "source": {
            "datasetId": "OA-22241",
            "officialPageUrl": "https://data.seoul.go.kr/dataList/OA-22241/F/1/datasetView.do",
            "officialFileName": "서울시 등고선.zip",
            "pageFileModifiedOn": "2025-03-20",
            "sourceReferenceYear": 2023,
            "horizontalCrs": "EPSG:5174",
            "horizontalUnit": "metre",
            "verticalDatumStatus": "Unverified",
            "heightUnitStatus": "NotExplicitlyDeclaredByDatasetMetadata",
            "sha256": SOURCE_HASH,
            "byteLength": SOURCE_BYTES,
            "receiptSha256": SOURCE_RECEIPT_HASH,
            "licenseCode": "KOGL-Type1",
            "mixedWith2023DownloadSequence1": False,
        },
        "toolchain": {
            "generatorRevision": GENERATOR_REVISION,
            "generatorRelativePath": Path(__file__).resolve().relative_to(root).as_posix(),
            "generatorSha256": generator_hash,
            "boundaryReaderRelativePath": BATCH_GENERATOR.as_posix(),
            "boundaryReaderSha256": batch_hash,
            "scopeDefinitionRelativePath": SCOPE_DEFINITION.as_posix(),
            "scopeDefinitionSha256": scope_hash,
            "pythonVersion": sys.version.split()[0],
            "numpyVersion": bucket_index.numpy.__version__,
            "pyshpVersion": dependencies.pyshp_version,
            "pyprojVersion": dependencies.pyproj_version,
            "shapelyVersion": dependencies.shapely_version,
            "geosVersion": dependencies.geos_version,
            "projVersion": dependencies.proj_version,
            "epsgDatabaseVersion": dependencies.epsg_database_version,
        },
        "coordinateFrame": copy.deepcopy(scope["coordinateFrame"]),
        "lattice": {
            "tileSizeHorizontalMeters": TILE_SIZE_METERS,
            "haloHorizontalMeters": HALO_METERS,
            "sampleSpacingHorizontalMeters": SAMPLE_SPACING_METERS,
            "tileSampleWidth": TILE_SAMPLE_SIDE,
            "tileSampleHeight": TILE_SAMPLE_SIDE,
            "rowOrder": ROW_ORDER,
            "globalBoundsCommonEnuMeters": {
                "minX": bounds[0],
                "minZ": bounds[1],
                "maxX": bounds[2],
                "maxZ": bounds[3],
            },
            "globalWidth": int(relative_grid.shape[1]),
            "globalHeight": int(relative_grid.shape[0]),
            "rectangularEnvelopeSampleCount": int(relative_grid.size),
            "globalSampleCount": sparse_global_audit["sampleCount"],
            "unusedEnvelopeSampleCount": int(
                relative_grid.size - sparse_global_audit["sampleCount"]
            ),
            "sparseRequiredTileHaloUnionOnly": True,
            "administrativeAreaTileMembershipCount": len(memberships),
            "uniquePhysicalTileCount": len(unique_tiles),
        },
        "sourceConstraintAudit": source.source_audit,
        "administrativeAreaCoverage": source.area_coverage,
        "interpolation": interpolation_audit,
        "sourceToGridFit": fit_audit,
        "tileSeamVerification": seam_audit,
        "historicalBoundaryMask": mask_audit,
        "baseLattice": base_audit,
        "reviewAcceptanceCriteria": {
            "preferredNearestConstraintDistanceMeters": REVIEW_PREFERRED_NEAREST_SOURCE_DISTANCE_METERS,
            "maximumNearestConstraintDistanceMeters": REVIEW_MAX_NEAREST_SOURCE_DISTANCE_METERS,
            "maximumPointGridFitP95SourceValue": REVIEW_MAX_POINT_GRID_P95_SOURCE_VALUE,
            "maximumContourGridFitP95SourceValue": REVIEW_MAX_CONTOUR_GRID_P95_SOURCE_VALUE,
            "maximumPointHoldoutP95SourceValue": REVIEW_MAX_POINT_HOLDOUT_P95_SOURCE_VALUE,
            "requireNoMissingOrFlatFallback": True,
            "requireAllSharedGridKeysBitEqual": True,
            "require30AreaPointAndContourCoverage": True,
        },
        "reviewGate": review_gate,
        "reviewGatePassed": review_gate_passed,
        "precisionTerrainReady": review_gate["allSamplesWithinPreferred500Meters"],
        "authority": authority_flags(),
    }
    apply_content_hash(audit)
    audit_payload = pretty_bytes(audit)
    files["audit.json"] = audit_payload

    global_key_descriptor = descriptor(
        global_key_relative,
        global_keys,
        formatCode=sparse_global_audit["keyFormatCode"],
        sampleCount=sparse_global_audit["sampleCount"],
        strideBytes=sparse_global_audit["keyStrideBytes"],
        rowOrder=ROW_ORDER,
    )
    global_descriptor = descriptor(
        global_height_relative,
        global_heights,
        formatCode=FLOAT32_FORMAT,
        sampleCount=sparse_global_audit["sampleCount"],
        strideBytes=sparse_global_audit["heightStrideBytes"],
        rowOrder=ROW_ORDER,
    )
    manifest = {
        "schemaVersion": SCHEMA_MANIFEST,
        "revision": REVISION,
        "generationHashSha256": generation_hash,
        "generatedAtUtc": GENERATED_AT_UTC,
        "sourceVintage": SOURCE_VINTAGE,
        "privateTerrainReviewReady": True,
        "precisionTerrainReady": review_gate["allSamplesWithinPreferred500Meters"],
        "heightMeaningCode": "RelativeSourceNumericHeightFromScopeGlobalReference",
        "commonVerticalReferenceSourceValue": interpolation_audit[
            "commonVerticalReferenceSourceValue"
        ],
        "heightUnitSourceDeclared": False,
        "verticalDatumVerified": False,
        "absoluteElevationAuthorized": False,
        "coordinateFrame": copy.deepcopy(scope["coordinateFrame"]),
        "source": audit["source"],
        "lattice": audit["lattice"],
        "globalGridKeyArtifact": global_key_descriptor,
        "globalHeightArtifact": global_descriptor,
        "terrainTiles": tile_entries,
        "administrativeAreas": area_entries,
        "audit": descriptor("audit.json", audit_payload),
        "reviewGatePassed": review_gate_passed,
        "authority": authority_flags(),
    }
    apply_content_hash(manifest)
    manifest_payload = pretty_bytes(manifest)
    files["manifest.json"] = manifest_payload

    complete = {
        "schemaVersion": SCHEMA_COMPLETE,
        "revision": REVISION,
        "generationHashSha256": generation_hash,
        "generatedAtUtc": GENERATED_AT_UTC,
        "fileCountExcludingCompletion": len(files),
        "administrativeAreaCount": EXPECTED_AREA_COUNT,
        "administrativeAreaTileMembershipCount": len(memberships),
        "uniquePhysicalTileCount": len(unique_tiles),
        "globalSampleCount": sparse_global_audit["sampleCount"],
        "sharedGridKeyBitMismatchCount": seam_audit[
            "sharedGridKeyBitMismatchCount"
        ],
        "nanOrInfiniteSampleCount": 0,
        "reviewGatePassed": review_gate_passed,
        "authority": authority_flags(),
        "files": [
            {
                "relativePath": name,
                "sha256": sha_bytes(payload),
                "byteLength": len(payload),
            }
            for name, payload in sorted(files.items())
        ],
    }
    apply_content_hash(complete)
    files["complete.json"] = pretty_bytes(complete)

    summary = {
        "status": "PASS",
        "generationHashSha256": generation_hash,
        "administrativeAreaCount": EXPECTED_AREA_COUNT,
        "administrativeAreaTileMembershipCount": len(memberships),
        "uniquePhysicalTileCount": len(unique_tiles),
        "globalGridWidth": int(relative_grid.shape[1]),
        "globalGridHeight": int(relative_grid.shape[0]),
        "globalSampleCount": sparse_global_audit["sampleCount"],
        "selectedElevationPointConstraintCount": source.selected_point_count,
        "selectedContourRecordCount": source.selected_contour_record_count,
        "derivedContourConstraintCount": source.contour_sample_count,
        "pointGridFitP95SourceValue": point_fit["p95"],
        "contourGridFitP95SourceValue": contour_fit["p95"],
        "pointHoldoutP95SourceValue": holdout_fit["p95"],
        "maximumNearestConstraintDistanceMeters": interpolation_audit[
            "nearestConstraintDistanceMeters"
        ]["maximum"],
        "sharedGridKeyCount": seam_audit["sharedGridKeyCount"],
        "sharedGridKeyBitMismatchCount": 0,
        "nanOrInfiniteSampleCount": 0,
        "commonVerticalReferenceSourceValue": interpolation_audit[
            "commonVerticalReferenceSourceValue"
        ],
        "heightUnitSourceDeclared": False,
        "verticalDatumVerified": False,
        "absoluteElevationAuthorized": False,
        "privateReviewOnly": True,
        "publicDisplayAllowed": False,
        "databaseWriteAttempted": False,
        "mongoWriteAttempted": False,
        "currentPointerUsed": False,
        "currentPointerUpdated": False,
        "unityApplyAttempted": False,
        "runtimeAuthorized": False,
        "traversalReady": False,
        "gameplayReady": False,
    }
    return generation_hash, files, summary


def generation_path(root: Path, generation_hash: str) -> Path:
    return safe_path(root, OUTPUT_ROOT / "generations" / generation_hash.lower())


def validate_materialized(path: Path, files: dict[str, bytes]) -> None:
    require(path.is_dir() and not is_reparse(path), "TerrainGenerationMissingOrUnsafe")
    actual = sorted(
        value.relative_to(path).as_posix()
        for value in path.rglob("*")
        if value.is_file()
    )
    require(actual == sorted(files), "TerrainGenerationFileSetChanged")
    for name, expected in files.items():
        output = path / name
        require(
            output.is_file()
            and not is_reparse(output)
            and output.read_bytes() == expected,
            f"TerrainGenerationFileChanged:{name}",
        )


def build(root: Path) -> dict[str, Any]:
    generation_hash, files, result = prepare(root)
    target = generation_path(root, generation_hash)
    if target.exists():
        validate_materialized(target, files)
        result["changedFiles"] = 0
        result["generationRelativePath"] = target.relative_to(root).as_posix()
        return result
    generations = target.parent
    generations.mkdir(parents=True, exist_ok=True)
    require(not is_reparse(generations), "TerrainGenerationParentUnsafe")
    staging = generations / (".staging-" + uuid.uuid4().hex)
    staging.mkdir()
    try:
        for name, payload in files.items():
            output = staging / name
            output.parent.mkdir(parents=True, exist_ok=True)
            output.write_bytes(payload)
        validate_materialized(staging, files)
        os.replace(staging, target)
    except Exception:
        if staging.exists() and generations in staging.parents:
            shutil.rmtree(staging)
        raise
    validate_materialized(target, files)
    result["changedFiles"] = len(files)
    result["generationRelativePath"] = target.relative_to(root).as_posix()
    return result


def verify(root: Path) -> dict[str, Any]:
    generation_hash, files, result = prepare(root)
    target = generation_path(root, generation_hash)
    validate_materialized(target, files)
    result["verified"] = True
    result["generationRelativePath"] = target.relative_to(root).as_posix()
    return result


def self_test(root: Path) -> dict[str, Any]:
    try:
        import numpy
    except (ModuleNotFoundError, ImportError) as exc:
        raise TerrainReviewError("TerrainGenerationDependencyMissing") from exc
    passed = 0

    def test(value: bool, code: str) -> None:
        nonlocal passed
        require(value, "SelfTest:" + code)
        passed += 1

    test(TILE_SAMPLE_SIDE == (TILE_SIZE_METERS + 2 * HALO_METERS) // SAMPLE_SPACING_METERS + 1, "TileDimensions")
    test(SOURCE_ARCHIVE.name == "OA-22241-seoul-contour.zip", "Only2025SourceSelected")
    test("slope" not in SOURCE_ARCHIVE.name.lower(), "OldSourceExcluded")
    test(sha_bytes(b"terrain-a") != sha_bytes(b"terrain-b"), "HashDistinct")
    test(content_hash({"a": 1, "contentHashSha256": "x"}) == sha_bytes(b'{"a":1}'), "ContentHashExcludesSelf")
    tiny = numpy.arange(63 * 63, dtype="float32").reshape(63, 63)
    north = numpy.flipud(tiny).astype("<f4").tobytes()
    test(north[:4] == struct.pack("<f", float(tiny[-1, 0])), "NorthFirstEncoding")
    flags = authority_flags()
    test(
        flags["privateReviewOnly"] is True
        and all(
            flags[key] is False
            for key in (
                "publicDisplayAllowed",
                "databaseWriteAttempted",
                "mongoWriteAttempted",
                "currentPointerUsed",
                "runtimeAuthorized",
                "traversalReady",
                "gameplayReady",
                "absoluteElevationAuthorized",
            )
        ),
        "AuthorityDefaults",
    )
    return {
        "status": "PASS",
        "selfTestsPassed": passed,
        "generatorSourceHashSha256": sha_file(Path(__file__).resolve()),
    }


def arguments(argv: Sequence[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("mode", choices=("self-test", "build", "verify"))
    parser.add_argument("root", nargs="?", default=str(ROOT_DEFAULT))
    return parser.parse_args(argv)


def main(argv: Sequence[str] | None = None) -> int:
    args = arguments(sys.argv[1:] if argv is None else argv)
    root = Path(args.root).resolve()
    try:
        require((root / ".git").exists(), "RepositoryRootInvalid")
        if args.mode == "self-test":
            result = self_test(root)
        elif args.mode == "build":
            result = build(root)
        else:
            result = verify(root)
        print(json.dumps(result, ensure_ascii=False, indent=2, allow_nan=False))
        return 0
    except (TerrainReviewError, OSError, ValueError, zipfile.BadZipFile) as exc:
        print(
            json.dumps(
                {"status": "FAIL", "errorCode": str(exc)}, ensure_ascii=False
            ),
            file=sys.stderr,
        )
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
