"""사가정 1 km 공간 표현 오버레이의 공통 생성·감사 로직.

이 모듈은 로컬 원본을 읽기만 한다. 생성 결과는 표현 후보이며 운영, 이동,
게임 상태 또는 Unity Scene의 권위가 아니다.
"""

from __future__ import annotations

import copy
import hmac
import hashlib
import html
import json
import math
import re
import struct
import zipfile
from dataclasses import dataclass
from pathlib import Path
from typing import Any, BinaryIO, Iterable, Iterator, Sequence
import xml.etree.ElementTree as ET

try:
    from pyproj import CRS, Transformer
    from shapely import make_valid
    from shapely.geometry import GeometryCollection, MultiPolygon, Polygon, box
    from shapely.geometry.polygon import orient
    from shapely.ops import unary_union
    from shapely.strtree import STRtree
except ModuleNotFoundError as exc:  # pragma: no cover - exercised by the PowerShell launcher
    raise RuntimeError(
        "SpatialPresentationDependencyMissing: install pyproj and shapely or expose the local GIS runtime"
    ) from exc


SCHEMA_VERSION = "ssalddel.spatial-presentation-overlay.v1"
PRIVATE_REVISION = "sagajeong-spatial-presentation.private-review.r2"
PUBLIC_REVISION = "sagajeong-spatial-presentation.public.r1"
PRIVATE_STATUS = "LocalPrivateReview"
PUBLIC_STATUS = "PublicOpenData"

AUDIT_SCHEMA_VERSION = "ssalddel.spatial-presentation-coverage-audit.v2"
AUDIT_REVISION = "sagajeong-spatial-presentation-coverage-audit.r2"

BASE_MAP_REVISION = "sagajeong-reference.r3"
BASE_MAP_HASH = "4B81E60C3C389A69AA765C8CC8D20E4457102C359A5FFBCD7EBDFA880F7E84E3"
BUILDING_ZIP_HASH = "674C5A9583996DD6B8946525EDAD8197BE79A634F1DB00D39E2B3133D0D2A755"
BUILDING_ZIP_LENGTH = 135_675_376
OSM_HASH = "3BDF9E9D36360FB32CB65C7C216215DCE60C762F7918FB7556D6695288A2D0A3"
OSM_LENGTH = 2_993_065
BUILDING_RECORD_COUNT = 695_761

BUILDING_FETCHED_AT_UTC = "2026-09-06T14:42:52.765Z"
OSM_FETCHED_AT_UTC = "2026-09-08T09:07:04.701Z"
BUILDING_DATASET_DATE = "2026-08-09"
OSM_DATASET_DATE = "2026-09-08"

COORDINATE_METHOD = "WGS84-ECEF-ENU-at-zero-altitude"
LEGAL_DONG_CODE = "1126010100"
HEIGHT_POLICY_REVISION = "sagajeong-building-height-presentation.r1"
SYMBOLIC_HEIGHT_METERS = 4.0
GRID_CELL_METERS = 100.0
BUILDING_COVERAGE_THRESHOLD = 0.20
OPEN_COVERAGE_THRESHOLD = 0.20

PROMOTION_CANDIDATE_REVISION = "sagajeong-spatial-presentation.public-candidate.r1"
PROMOTION_PENDING_STATUS = "PromotionPendingAudit"
PROMOTION_TOKEN_ENVIRONMENT_VARIABLE = "SSALDDEL_SPATIAL_PROMOTION_TOKEN"

LEGACY_MATCH_MIN_IOU = 0.20
LEGACY_MATCH_MIN_AREA_RATIO = 0.25
LEGACY_MATCH_MIN_SMALLER_COVERAGE = 0.30
LEGACY_MATCH_CENTROID_SCALE = 0.75
KNOWN_INCOMPLETE_OSM_RELATION_ID = "osm:relation:14428122"

OSM_SOURCE_ID = "openstreetmap:sagajeong-r2-map"
QUALIFIED_OPEN_SURFACE_KINDS = frozenset(
    {"park", "garden", "school", "playground", "pitch", "parking", "water", "wood"}
)
ALL_SURFACE_KINDS = frozenset(
    set(QUALIFIED_OPEN_SURFACE_KINDS)
    | {"residential", "commercial", "industrial", "religious", "pedestrian", "grass", "cemetery"}
)

PROMOTION_DATASET_ID = "data-go-kr-15083092"
PROMOTION_STATUS = "RightsReconciled"
PROMOTION_LICENSE_CODE = "KOGL-Type1"

IMMUTABLE_BUILDING_ACQUISITION_RECEIPT = {
    "provider": "국토교통부",
    "datasetId": PROMOTION_DATASET_ID,
    "datasetDate": BUILDING_DATASET_DATE,
    "fetchedAtUtc": BUILDING_FETCHED_AT_UTC,
    "rawHash": BUILDING_ZIP_HASH,
    "rawLength": BUILDING_ZIP_LENGTH,
    "originalCrs": "EPSG:5186",
}


class SpatialPresentationError(RuntimeError):
    """사용자에게 안정적인 오류 코드를 전달하는 도구 오류."""


def require(condition: bool, code: str) -> None:
    if not condition:
        raise SpatialPresentationError(code)


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest().upper()


def canonical_json_bytes(value: Any) -> bytes:
    """키 정렬·공백 없음·UTF-8인 프로젝트 고정 canonical JSON."""

    return json.dumps(
        value,
        ensure_ascii=False,
        allow_nan=False,
        sort_keys=True,
        separators=(",", ":"),
    ).encode("utf-8")


def hash_with_empty_field(document: dict[str, Any], field_name: str) -> str:
    candidate = copy.deepcopy(document)
    candidate[field_name] = ""
    return hashlib.sha256(canonical_json_bytes(candidate)).hexdigest().upper()


def require_manager_promotion_token(argument_token: str | None, environment_token: str | None) -> None:
    """승격 후보/승인 임시 파일은 한 manager 실행 안에서만 이어지게 한다."""

    require(bool(argument_token) and bool(environment_token), "PromotionGate:ManagerTokenMissing")
    require(
        hmac.compare_digest(str(argument_token), str(environment_token)),
        "PromotionGate:ManagerTokenMismatch",
    )


def promotion_receipt_evidence_hash(receipt: dict[str, Any]) -> str:
    candidate = copy.deepcopy(receipt)
    candidate["evidenceHash"] = ""
    return hashlib.sha256(canonical_json_bytes(candidate)).hexdigest().upper()


def _is_utc_timestamp(value: Any) -> bool:
    return isinstance(value, str) and re.fullmatch(
        r"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,6})?Z", value
    ) is not None


def apply_content_hash(document: dict[str, Any]) -> None:
    document["contentHash"] = ""
    document["contentHash"] = hash_with_empty_field(document, "contentHash")


def verify_content_hash(document: dict[str, Any]) -> None:
    actual = document.get("contentHash")
    require(isinstance(actual, str) and len(actual) == 64, "OverlayContentHashFormatInvalid")
    require(actual == hash_with_empty_field(document, "contentHash"), "OverlayContentHashMismatch")


def write_json_deterministic(path: Path, value: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    text = json.dumps(value, ensure_ascii=False, allow_nan=False, indent=2) + "\n"
    temporary = path.with_name(path.name + ".tmp")
    temporary.write_text(text, encoding="utf-8", newline="\n")
    temporary.replace(path)


def write_text_atomic(path: Path, text: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(path.name + ".tmp")
    temporary.write_text(text, encoding="utf-8", newline="\n")
    temporary.replace(path)


def load_json(path: Path) -> dict[str, Any]:
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        raise SpatialPresentationError(f"JsonReadFailed:{path.name}:{exc}") from exc
    require(isinstance(value, dict), f"JsonRootInvalid:{path.name}")
    return value


@dataclass(frozen=True)
class EnuFrame:
    origin_latitude: float
    origin_longitude: float
    offset_x: float
    offset_z: float
    half_extent: float

    def __post_init__(self) -> None:
        for value in (
            self.origin_latitude,
            self.origin_longitude,
            self.offset_x,
            self.offset_z,
            self.half_extent,
        ):
            require(math.isfinite(value), "CoordinateFrameNonFinite")
        require(-85.0 <= self.origin_latitude <= 85.0, "CoordinateFrameLatitudeInvalid")
        require(-180.0 <= self.origin_longitude <= 180.0, "CoordinateFrameLongitudeInvalid")
        require(self.half_extent == 500.0, "CoordinateFrameExtentMustBeOneKilometer")

    @property
    def min_x(self) -> float:
        return self.offset_x - self.half_extent

    @property
    def max_x(self) -> float:
        return self.offset_x + self.half_extent

    @property
    def min_z(self) -> float:
        return self.offset_z - self.half_extent

    @property
    def max_z(self) -> float:
        return self.offset_z + self.half_extent

    @property
    def clip_polygon(self) -> Polygon:
        return box(self.min_x, self.min_z, self.max_x, self.max_z)

    @staticmethod
    def _ecef(latitude: float, longitude: float) -> tuple[float, float, float]:
        latitude_radians = math.radians(latitude)
        longitude_radians = math.radians(longitude)
        eccentricity_squared = 0.0066943799901413165
        radius = 6_378_137.0 / math.sqrt(
            1.0 - eccentricity_squared * math.sin(latitude_radians) ** 2
        )
        return (
            radius * math.cos(latitude_radians) * math.cos(longitude_radians),
            radius * math.cos(latitude_radians) * math.sin(longitude_radians),
            radius * (1.0 - eccentricity_squared) * math.sin(latitude_radians),
        )

    def wgs84_to_local(self, longitude: float, latitude: float) -> tuple[float, float]:
        origin_x, origin_y, origin_z = self._ecef(
            self.origin_latitude, self.origin_longitude
        )
        point_x, point_y, point_z = self._ecef(latitude, longitude)
        dx = point_x - origin_x
        dy = point_y - origin_y
        dz = point_z - origin_z
        latitude_radians = math.radians(self.origin_latitude)
        longitude_radians = math.radians(self.origin_longitude)
        east = -math.sin(longitude_radians) * dx + math.cos(longitude_radians) * dy
        north = (
            -math.sin(latitude_radians) * math.cos(longitude_radians) * dx
            - math.sin(latitude_radians) * math.sin(longitude_radians) * dy
            + math.cos(latitude_radians) * dz
        )
        return east + self.offset_x, north + self.offset_z

    def local_to_wgs84(self, x: float, z: float) -> tuple[float, float]:
        east = x - self.offset_x
        north = z - self.offset_z
        latitude_radians = math.radians(self.origin_latitude)
        longitude_radians = math.radians(self.origin_longitude)
        dx = (
            -math.sin(longitude_radians) * east
            - math.sin(latitude_radians) * math.cos(longitude_radians) * north
        )
        dy = (
            math.cos(longitude_radians) * east
            - math.sin(latitude_radians) * math.sin(longitude_radians) * north
        )
        dz = math.cos(latitude_radians) * north
        origin = self._ecef(self.origin_latitude, self.origin_longitude)
        transformer = Transformer.from_crs(4978, 4326, always_xy=True)
        longitude, latitude, _ = transformer.transform(
            origin[0] + dx, origin[1] + dy, origin[2] + dz
        )
        return float(longitude), float(latitude)


def load_base_map(path: Path) -> tuple[dict[str, Any], EnuFrame]:
    require(path.is_file(), f"BaseMapMissing:{path}")
    require(sha256_file(path) == BASE_MAP_HASH, "BaseMapHashMismatch")
    document = load_json(path)
    require(document.get("revision") == BASE_MAP_REVISION, "BaseMapRevisionMismatch")
    require(document.get("coordinateMethod") == COORDINATE_METHOD, "BaseMapCoordinateMethodMismatch")
    frame = EnuFrame(
        float(document["originLatitude"]),
        float(document["originLongitude"]),
        float(document["offsetX"]),
        float(document["offsetZ"]),
        float(document["halfExtent"]),
    )
    return document, frame


def _round_coordinate(value: float) -> float:
    rounded = round(float(value), 3)
    return 0.0 if rounded == 0.0 else rounded


def _signed_area(points: Sequence[tuple[float, float]]) -> float:
    return sum(
        points[index][0] * points[(index + 1) % len(points)][1]
        - points[(index + 1) % len(points)][0] * points[index][1]
        for index in range(len(points))
    ) / 2.0


def _canonical_ring(
    coordinates: Iterable[Sequence[float]], *, clockwise: bool
) -> list[dict[str, float]]:
    points = [(_round_coordinate(point[0]), _round_coordinate(point[1])) for point in coordinates]
    if len(points) > 1 and points[0] == points[-1]:
        points.pop()
    compact: list[tuple[float, float]] = []
    for point in points:
        if not compact or point != compact[-1]:
            compact.append(point)
    if len(compact) > 1 and compact[0] == compact[-1]:
        compact.pop()
    require(len(set(compact)) >= 3, "GeometryRingTooSmall")
    area = _signed_area(compact)
    require(abs(area) > 0.000001, "GeometryRingHasZeroArea")
    if clockwise != (area < 0.0):
        compact.reverse()
    minimum = min(compact)
    rotations = [
        compact[index:] + compact[:index]
        for index, point in enumerate(compact)
        if point == minimum
    ]
    compact = min(rotations)
    compact.append(compact[0])
    return [{"x": point[0], "z": point[1]} for point in compact]


def _polygonal(geometry: Any) -> Any:
    if geometry.is_empty:
        return GeometryCollection()
    if not geometry.is_valid:
        geometry = make_valid(geometry)
    polygons: list[Polygon] = []
    if isinstance(geometry, Polygon):
        polygons.append(geometry)
    elif isinstance(geometry, MultiPolygon):
        polygons.extend(geometry.geoms)
    elif isinstance(geometry, GeometryCollection):
        for member in geometry.geoms:
            polygonal = _polygonal(member)
            if isinstance(polygonal, Polygon):
                polygons.append(polygonal)
            elif isinstance(polygonal, MultiPolygon):
                polygons.extend(polygonal.geoms)
    polygons = [polygon for polygon in polygons if polygon.area > 0.01]
    if not polygons:
        return GeometryCollection()
    merged = unary_union(polygons)
    if not merged.is_valid:
        merged = make_valid(merged)
    return merged


def rings_to_geometry(rings: Sequence[Sequence[tuple[float, float]]]) -> Any:
    candidates: list[dict[str, Any]] = []
    for coordinates in rings:
        if len(coordinates) < 4:
            continue
        polygon = Polygon(coordinates)
        if polygon.is_empty or abs(polygon.area) <= 0.01:
            continue
        if not polygon.is_valid:
            polygon = make_valid(polygon)
        polygonal = _polygonal(polygon)
        if isinstance(polygonal, MultiPolygon):
            for member in polygonal.geoms:
                candidates.append({"polygon": member, "area": member.area})
        elif isinstance(polygonal, Polygon):
            candidates.append({"polygon": polygonal, "area": polygonal.area})
    require(bool(candidates), "GeometryContainsNoPolygon")
    candidates.sort(key=lambda item: -item["area"])
    for index, item in enumerate(candidates):
        representative = item["polygon"].representative_point()
        parents = [
            (parent_index, parent["area"])
            for parent_index, parent in enumerate(candidates[:index])
            if parent["polygon"].covers(representative)
        ]
        item["parent"] = min(parents, key=lambda value: value[1])[0] if parents else None
        depth = 0
        parent_index = item["parent"]
        while parent_index is not None:
            depth += 1
            parent_index = candidates[parent_index]["parent"]
        item["depth"] = depth
    constructed: list[Polygon] = []
    for index, item in enumerate(candidates):
        if item["depth"] % 2 != 0:
            continue
        holes = [
            list(child["polygon"].exterior.coords)
            for child in candidates
            if child.get("parent") == index and child["depth"] == item["depth"] + 1
        ]
        constructed.append(Polygon(item["polygon"].exterior.coords, holes))
    return _polygonal(unary_union(constructed))


def geometry_to_parts(geometry: Any) -> list[dict[str, Any]]:
    geometry = _polygonal(geometry)
    polygons = (
        [geometry]
        if isinstance(geometry, Polygon)
        else list(geometry.geoms)
        if isinstance(geometry, MultiPolygon)
        else []
    )
    parts: list[dict[str, Any]] = []
    for polygon in polygons:
        polygon = orient(polygon, sign=-1.0)
        outer = _canonical_ring(polygon.exterior.coords, clockwise=True)
        holes = [
            {"points": _canonical_ring(interior.coords, clockwise=False)}
            for interior in polygon.interiors
        ]
        holes.sort(
            key=lambda item: tuple((point["x"], point["z"]) for point in item["points"])
        )
        parts.append({"outer": {"points": outer}, "holes": holes})
    parts.sort(
        key=lambda part: tuple(
            (point["x"], point["z"]) for point in part["outer"]["points"]
        )
    )
    require(bool(parts), "GeometryContainsNoOutputPart")
    return parts


def parts_to_geometry(parts: Sequence[dict[str, Any]]) -> Any:
    polygons: list[Polygon] = []
    for part in parts:
        outer = [(float(point["x"]), float(point["z"])) for point in part["outer"]["points"]]
        holes = [
            [(float(point["x"]), float(point["z"])) for point in hole["points"]]
            for hole in part.get("holes", [])
        ]
        polygons.append(Polygon(outer, holes))
    return _polygonal(unary_union(polygons))


def canonical_parts_hash(parts: Sequence[dict[str, Any]]) -> str:
    return hashlib.sha256(canonical_json_bytes(list(parts))).hexdigest().upper()


def _touches_outer_boundary(geometry: Any, frame: EnuFrame, tolerance: float = 0.002) -> bool:
    min_x, min_z, max_x, max_z = geometry.bounds
    return (
        abs(min_x - frame.min_x) <= tolerance
        or abs(max_x - frame.max_x) <= tolerance
        or abs(min_z - frame.min_z) <= tolerance
        or abs(max_z - frame.max_z) <= tolerance
    )


def _read_exact(stream: BinaryIO, size: int, code: str) -> bytes:
    value = stream.read(size)
    require(len(value) == size, code)
    return value


@dataclass(frozen=True)
class DbfField:
    name: str
    offset: int
    length: int


def _read_dbf_header(stream: BinaryIO) -> tuple[int, int, int, dict[str, DbfField]]:
    header = _read_exact(stream, 32, "DbfHeaderTruncated")
    record_count = struct.unpack_from("<I", header, 4)[0]
    header_length, record_length = struct.unpack_from("<HH", header, 8)
    fields: dict[str, DbfField] = {}
    offset = 1
    while stream.tell() < header_length:
        first = _read_exact(stream, 1, "DbfFieldHeaderTruncated")
        if first == b"\r":
            break
        descriptor = first + _read_exact(stream, 31, "DbfFieldDescriptorTruncated")
        name = descriptor[:11].split(b"\0", 1)[0].decode("ascii", errors="strict")
        length = descriptor[16]
        require(name and name not in fields and length > 0, "DbfFieldContractInvalid")
        fields[name] = DbfField(name, offset, length)
        offset += length
    if stream.tell() < header_length:
        _read_exact(stream, header_length - stream.tell(), "DbfHeaderPaddingTruncated")
    require(offset == record_length, "DbfRecordLengthMismatch")
    return record_count, header_length, record_length, fields


def _dbf_text(record: bytes, field: DbfField, encoding: str) -> str:
    return record[field.offset : field.offset + field.length].decode(
        encoding, errors="strict"
    ).strip(" \0")


def _read_shp_header(stream: BinaryIO) -> None:
    header = _read_exact(stream, 100, "ShpHeaderTruncated")
    require(struct.unpack_from(">I", header, 0)[0] == 9994, "ShpFileCodeInvalid")
    require(struct.unpack_from("<I", header, 28)[0] == 1000, "ShpVersionInvalid")
    require(struct.unpack_from("<I", header, 32)[0] == 5, "ShpTypeMustBePolygon")


def _parse_shp_polygon(body: bytes) -> list[list[tuple[float, float]]]:
    require(len(body) >= 44, "ShpPolygonRecordTruncated")
    shape_type = struct.unpack_from("<I", body, 0)[0]
    require(shape_type == 5, "ShpRecordTypeMustBePolygon")
    part_count, point_count = struct.unpack_from("<II", body, 36)
    require(0 < part_count <= 10_000 and 3 < point_count <= 1_000_000, "ShpPolygonBudgetInvalid")
    points_offset = 44 + part_count * 4
    require(points_offset + point_count * 16 <= len(body), "ShpPolygonPointDataTruncated")
    starts = list(struct.unpack_from(f"<{part_count}I", body, 44)) + [point_count]
    require(starts[0] == 0 and starts == sorted(starts), "ShpPartIndexInvalid")
    points = [
        struct.unpack_from("<dd", body, points_offset + index * 16)
        for index in range(point_count)
    ]
    return [points[starts[index] : starts[index + 1]] for index in range(part_count)]


def _zip_entry(archive: zipfile.ZipFile, suffix: str) -> zipfile.ZipInfo:
    matches = [entry for entry in archive.infolist() if entry.filename.lower().endswith(suffix.lower())]
    require(len(matches) == 1, f"BuildingZipEntryInvalid:{suffix}")
    return matches[0]


def _source_search_bbox(source_crs: CRS, frame: EnuFrame) -> tuple[float, float, float, float]:
    wgs_to_source = Transformer.from_crs(4326, source_crs, always_xy=True)
    local_points: list[tuple[float, float]] = []
    steps = 20
    for index in range(steps + 1):
        ratio = index / steps
        x = frame.min_x + (frame.max_x - frame.min_x) * ratio
        z = frame.min_z + (frame.max_z - frame.min_z) * ratio
        local_points.extend(
            [(x, frame.min_z), (x, frame.max_z), (frame.min_x, z), (frame.max_x, z)]
        )
    longitudes: list[float] = []
    latitudes: list[float] = []
    for x, z in local_points:
        longitude, latitude = frame.local_to_wgs84(x, z)
        longitudes.append(longitude)
        latitudes.append(latitude)
    eastings, northings = wgs_to_source.transform(longitudes, latitudes)
    return (
        min(eastings) - 2.0,
        min(northings) - 2.0,
        max(eastings) + 2.0,
        max(northings) + 2.0,
    )


def _bbox_intersects(
    first: Sequence[float], second: Sequence[float]
) -> bool:
    return not (
        first[2] < second[0]
        or first[0] > second[2]
        or first[3] < second[1]
        or first[1] > second[3]
    )


def _parse_number(text: str) -> float | None:
    if not text:
        return None
    try:
        value = float(text)
    except ValueError:
        return None
    return value if math.isfinite(value) else None


def _parse_integer(text: str) -> int | None:
    number = _parse_number(text)
    if number is None or number < 0 or not number.is_integer():
        return None
    return int(number)


def _transform_source_rings(
    rings: Sequence[Sequence[tuple[float, float]]],
    source_to_wgs: Transformer,
    frame: EnuFrame,
) -> list[list[tuple[float, float]]]:
    transformed: list[list[tuple[float, float]]] = []
    for ring in rings:
        eastings = [point[0] for point in ring]
        northings = [point[1] for point in ring]
        longitudes, latitudes = source_to_wgs.transform(eastings, northings)
        transformed.append(
            [
                frame.wgs84_to_local(float(longitude), float(latitude))
                for longitude, latitude in zip(longitudes, latitudes)
            ]
        )
    return transformed


def read_official_buildings(
    building_zip_path: Path, frame: EnuFrame
) -> tuple[list[dict[str, Any]], dict[str, int]]:
    require(building_zip_path.is_file(), f"BuildingZipMissing:{building_zip_path}")
    require(building_zip_path.stat().st_size == BUILDING_ZIP_LENGTH, "BuildingZipLengthMismatch")
    require(sha256_file(building_zip_path) == BUILDING_ZIP_HASH, "BuildingZipHashMismatch")
    with zipfile.ZipFile(building_zip_path, "r") as archive:
        shp_entry = _zip_entry(archive, ".shp")
        dbf_entry = _zip_entry(archive, ".dbf")
        prj_entry = _zip_entry(archive, ".prj")
        projection_text = archive.read(prj_entry).decode("utf-8-sig", errors="strict")
        declared_crs = CRS.from_wkt(projection_text)
        normalized_wkt = projection_text.replace(" ", "")
        require(
            'AUTHORITY["EPSG","5186"]' in normalized_wkt
            and "Korea_Central_Belt_2010" in declared_crs.name
            and declared_crs.is_projected,
            "BuildingSourceCrsMismatch",
        )
        # 공급 WKT1은 pyproj가 authority를 역식별하지 못하므로, 위 선언을 검사한 뒤
        # 공식 EPSG 정의를 변환 권위로 사용한다.
        source_crs = CRS.from_epsg(5186)
        source_to_wgs = Transformer.from_crs(source_crs, 4326, always_xy=True)
        search_bbox = _source_search_bbox(source_crs, frame)
        buildings: list[dict[str, Any]] = []
        identifiers: set[str] = set()
        statistics = {
            "sourceRecords": 0,
            "legalDongRecords": 0,
            "bboxCandidates": 0,
            "clippedBuildings": 0,
            "observedHeightBuildings": 0,
            "symbolicHeightBuildings": 0,
            "deletedRecords": 0,
        }
        with archive.open(shp_entry, "r") as shp_stream, archive.open(dbf_entry, "r") as dbf_stream:
            _read_shp_header(shp_stream)
            record_count, _, record_length, fields = _read_dbf_header(dbf_stream)
            require(record_count == BUILDING_RECORD_COUNT, "BuildingRecordCountMismatch")
            required_fields = {"A1", "A3", "A8", "A9", "A16", "A26"}
            require(required_fields.issubset(fields), "BuildingDbfFieldsMissing")
            for index in range(record_count):
                record = _read_exact(dbf_stream, record_length, "DbfRecordTruncated")
                record_header = _read_exact(shp_stream, 8, "ShpRecordHeaderTruncated")
                record_number, content_words = struct.unpack(">II", record_header)
                require(record_number == index + 1, "ShpRecordOrderMismatch")
                require(2 <= content_words <= 1_048_576, "ShpRecordBudgetInvalid")
                body = _read_exact(shp_stream, content_words * 2, "ShpRecordBodyTruncated")
                statistics["sourceRecords"] += 1
                if record[0] == 0x2A:
                    statistics["deletedRecords"] += 1
                    continue
                require(record[0] == 0x20, "DbfRecordDeletionMarkerInvalid")
                legal_code = _dbf_text(record, fields["A3"], "ascii")
                if legal_code != LEGAL_DONG_CODE:
                    continue
                statistics["legalDongRecords"] += 1
                require(len(body) >= 36, "ShpRecordBoundingBoxTruncated")
                record_bbox = struct.unpack_from("<4d", body, 4)
                if not _bbox_intersects(record_bbox, search_bbox):
                    continue
                statistics["bboxCandidates"] += 1
                source_rings = _parse_shp_polygon(body)
                local_rings = _transform_source_rings(source_rings, source_to_wgs, frame)
                uncut_geometry = rings_to_geometry(local_rings)
                clipped_geometry = _polygonal(uncut_geometry.intersection(frame.clip_polygon))
                if clipped_geometry.is_empty:
                    continue
                source_feature_id = _dbf_text(record, fields["A1"], "ascii")
                require(source_feature_id != "", "BuildingSourceFeatureIdMissing")
                stable_id = f"vworld:al-d010:{source_feature_id}"
                require(stable_id not in identifiers, "BuildingSourceFeatureIdDuplicate")
                identifiers.add(stable_id)
                observed_height = _parse_number(_dbf_text(record, fields["A16"], "ascii"))
                observed_floors = _parse_integer(_dbf_text(record, fields["A26"], "ascii"))
                if observed_height is not None and observed_height > 0.0:
                    height_meters = round(observed_height, 3)
                    height_kind = "ObservedSourceHeight"
                    statistics["observedHeightBuildings"] += 1
                else:
                    height_meters = SYMBOLIC_HEIGHT_METERS
                    height_kind = "SymbolicFallback4m"
                    statistics["symbolicHeightBuildings"] += 1
                boundary_clipped = not frame.clip_polygon.covers(uncut_geometry)
                if boundary_clipped:
                    statistics["clippedBuildings"] += 1
                buildings.append(
                    {
                        "id": stable_id,
                        "sourceFeatureId": source_feature_id,
                        "buildingKind": _dbf_text(record, fields["A8"], "ascii"),
                        "purposeName": _dbf_text(record, fields["A9"], "cp949"),
                        "heightMeters": height_meters,
                        "heightKind": height_kind,
                        "observedHeightMeters": (
                            round(observed_height, 3) if observed_height is not None else None
                        ),
                        "observedAboveGroundFloors": observed_floors,
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
                        "parts": geometry_to_parts(clipped_geometry),
                        "_geometry": clipped_geometry,
                        "_boundaryClipped": boundary_clipped,
                    }
                )
            require(shp_stream.read(1) == b"", "ShpContainsUnexpectedExtraRecords")
        buildings.sort(key=lambda item: item["id"])
        return buildings, statistics


def _geometry_from_point_dicts(points: Sequence[dict[str, Any]]) -> Any:
    coordinates = [(float(point["x"]), float(point["z"])) for point in points]
    return _polygonal(Polygon(coordinates))


def read_legacy_base_buildings(base_map: dict[str, Any]) -> list[dict[str, Any]]:
    buildings: list[dict[str, Any]] = []
    identifiers: set[str] = set()
    for value in base_map.get("buildings", []):
        identifier = str(value.get("id", ""))
        require(identifier.startswith("osm:way:"), "BaseMapBuildingIdInvalid")
        require(identifier not in identifiers, "BaseMapBuildingIdDuplicate")
        identifiers.add(identifier)
        geometry = _geometry_from_point_dicts(value.get("points", []))
        require(not geometry.is_empty, "BaseMapBuildingGeometryInvalid")
        buildings.append({"id": identifier, "geometry": geometry})
    require(bool(buildings), "BaseMapBuildingsMissing")
    buildings.sort(key=lambda item: item["id"])
    return buildings


def bind_legacy_buildings(
    buildings: list[dict[str, Any]], legacy_buildings: list[dict[str, Any]]
) -> dict[str, int]:
    geometries = [item["geometry"] for item in legacy_buildings]
    tree = STRtree(geometries)
    for building in buildings:
        geometry = building["_geometry"]
        candidates: list[dict[str, Any]] = []
        weak_candidates: list[dict[str, Any]] = []
        for raw_index in tree.query(geometry):
            index = int(raw_index)
            legacy = legacy_buildings[index]
            intersection_area = geometry.intersection(legacy["geometry"]).area
            if intersection_area <= 0.01:
                continue
            union_area = geometry.union(legacy["geometry"]).area
            smaller_area = min(geometry.area, legacy["geometry"].area)
            larger_area = max(geometry.area, legacy["geometry"].area)
            iou = intersection_area / union_area if union_area > 0 else 0.0
            smaller_coverage = intersection_area / smaller_area if smaller_area > 0 else 0.0
            area_ratio = smaller_area / larger_area if larger_area > 0 else 0.0
            centroid_distance = geometry.centroid.distance(legacy["geometry"].centroid)
            centroid_limit = max(2.0, math.sqrt(smaller_area) * LEGACY_MATCH_CENTROID_SCALE)
            if iou < 0.20 and smaller_coverage < 0.55:
                continue
            candidate = {
                "id": legacy["id"],
                "centroidDistanceMeters": centroid_distance,
                "intersectionOverUnion": iou,
                "smallerFootprintCoverage": smaller_coverage,
                "_areaRatio": area_ratio,
                "_centroidLimit": centroid_limit,
            }
            if (
                iou >= LEGACY_MATCH_MIN_IOU
                and smaller_coverage >= LEGACY_MATCH_MIN_SMALLER_COVERAGE
                and area_ratio >= LEGACY_MATCH_MIN_AREA_RATIO
                and centroid_distance <= centroid_limit
            ):
                candidates.append(candidate)
            else:
                weak_candidates.append(candidate)
        candidates.sort(
            key=lambda item: (
                -item["smallerFootprintCoverage"],
                -item["intersectionOverUnion"],
                item["centroidDistanceMeters"],
                item["id"],
            )
        )
        weak_candidates.sort(
            key=lambda item: (
                -item["intersectionOverUnion"],
                -item["smallerFootprintCoverage"],
                item["centroidDistanceMeters"],
                item["id"],
            )
        )
        all_candidates = candidates + weak_candidates
        all_candidates.sort(
            key=lambda item: (
                -item["smallerFootprintCoverage"],
                -item["intersectionOverUnion"],
                item["centroidDistanceMeters"],
                item["id"],
            )
        )
        building["_legacyCandidates"] = all_candidates
        building["_credibleLegacyIds"] = {item["id"] for item in candidates}

    claims: dict[str, list[str]] = {}
    for building in buildings:
        for candidate in building["_legacyCandidates"]:
            claims.setdefault(candidate["id"], []).append(building["id"])
    globally_duplicated_aliases = {
        identifier for identifier, building_ids in claims.items() if len(building_ids) > 1
    }
    matched = 0
    ambiguous = 0
    weak = 0
    for building in buildings:
        candidates = building.pop("_legacyCandidates")
        credible_ids = building.pop("_credibleLegacyIds")
        if not candidates:
            continue
        best = candidates[0]
        has_global_duplicate = any(
            candidate["id"] in globally_duplicated_aliases for candidate in candidates
        )
        if len(candidates) > 1 or has_global_duplicate:
            # 한 OSM alias가 여러 공식 도형에 매핑되거나 한 공식 도형에 여러
            # alias 후보가 있으면 어느 쪽도 단일 lookup에 넣지 않는다.
            building["legacyOsmIds"] = []
            building["legacyMatch"] = {
                "method": (
                    "AmbiguousGlobalAliasExcluded"
                    if has_global_duplicate
                    else "AmbiguousMultipleFootprintsExcluded"
                ),
                "centroidDistanceMeters": round(best["centroidDistanceMeters"], 3),
                "intersectionOverUnion": round(best["intersectionOverUnion"], 6),
                "smallerFootprintCoverage": round(best["smallerFootprintCoverage"], 6),
                "ambiguous": True,
            }
            ambiguous += 1
            continue
        if best["id"] not in credible_ids:
            building["legacyOsmIds"] = []
            building["legacyMatch"] = {
                "method": "WeakFootprintCandidateExcluded",
                "centroidDistanceMeters": round(best["centroidDistanceMeters"], 3),
                "intersectionOverUnion": round(best["intersectionOverUnion"], 6),
                "smallerFootprintCoverage": round(best["smallerFootprintCoverage"], 6),
                "ambiguous": True,
            }
            weak += 1
            continue
        building["legacyOsmIds"] = [best["id"]]
        building["legacyMatch"] = {
            "method": "FootprintOverlap",
            "centroidDistanceMeters": round(best["centroidDistanceMeters"], 3),
            "intersectionOverUnion": round(best["intersectionOverUnion"], 6),
            "smallerFootprintCoverage": round(best["smallerFootprintCoverage"], 6),
            "ambiguous": False,
        }
        matched += 1
    return {
        "matchedBuildings": matched,
        "ambiguousBuildings": ambiguous,
        "weakCandidateBuildings": weak,
        "globallyDuplicatedAliasCount": len(globally_duplicated_aliases),
    }


def surface_kind(tags: dict[str, str]) -> str | None:
    natural = tags.get("natural", "")
    landuse = tags.get("landuse", "")
    leisure = tags.get("leisure", "")
    amenity = tags.get("amenity", "")
    highway = tags.get("highway", "")
    if natural in {"water", "wetland"} or landuse in {"reservoir", "basin"} or tags.get("water"):
        return "water"
    if natural == "wood" or landuse == "forest":
        return "wood"
    if leisure in {"park", "garden", "playground", "pitch"}:
        return leisure
    if amenity == "parking":
        return "parking"
    if amenity in {"school", "college", "kindergarten"} and tags.get("building", "no") == "no":
        return "school"
    if landuse in {"residential", "commercial", "retail", "industrial", "religious", "cemetery"}:
        return "commercial" if landuse == "retail" else landuse
    if highway == "pedestrian" and (tags.get("area") == "yes" or tags.get("type") == "multipolygon"):
        return "pedestrian"
    if natural == "grassland" or landuse in {"grass", "meadow", "recreation_ground", "village_green"}:
        return "grass"
    return None


def _stitch_way_rings(way_references: Sequence[Sequence[str]]) -> list[list[str]]:
    remaining = [list(references) for references in way_references if len(references) >= 2]
    rings: list[list[str]] = []
    while remaining:
        current = remaining.pop(0)
        while current[0] != current[-1]:
            match_index = None
            reverse = False
            prepend = False
            for index, candidate in enumerate(remaining):
                if current[-1] == candidate[0]:
                    match_index = index
                    break
                if current[-1] == candidate[-1]:
                    match_index = index
                    reverse = True
                    break
                if current[0] == candidate[-1]:
                    match_index = index
                    prepend = True
                    break
                if current[0] == candidate[0]:
                    match_index = index
                    reverse = True
                    prepend = True
                    break
            require(match_index is not None, "OsmMultipolygonRingOpen")
            candidate = remaining.pop(match_index)
            if reverse:
                candidate.reverse()
            if prepend:
                current = candidate[:-1] + current
            else:
                current.extend(candidate[1:])
        rings.append(current)
    return rings


def _osm_tags(element: ET.Element) -> dict[str, str]:
    return {tag.attrib["k"]: tag.attrib["v"] for tag in element.findall("tag")}


def read_osm_surfaces(
    osm_path: Path,
    frame: EnuFrame,
    legacy_building_ids: set[str],
) -> tuple[list[dict[str, Any]], list[dict[str, Any]], dict[str, int]]:
    require(osm_path.is_file(), f"OsmSourceMissing:{osm_path}")
    require(osm_path.stat().st_size == OSM_LENGTH, "OsmSourceLengthMismatch")
    require(sha256_file(osm_path) == OSM_HASH, "OsmSourceHashMismatch")
    try:
        root = ET.parse(osm_path).getroot()
    except ET.ParseError as exc:
        raise SpatialPresentationError(f"OsmXmlInvalid:{exc}") from exc
    nodes = {
        node.attrib["id"]: (float(node.attrib["lon"]), float(node.attrib["lat"]))
        for node in root.findall("node")
    }
    ways: dict[str, dict[str, Any]] = {}
    for way in root.findall("way"):
        ways[way.attrib["id"]] = {
            "refs": [node.attrib["ref"] for node in way.findall("nd")],
            "tags": _osm_tags(way),
        }

    legacy_buildings: list[dict[str, Any]] = []
    for identifier in sorted(legacy_building_ids):
        require(identifier.startswith("osm:way:"), "BaseMapBuildingIdInvalid")
        raw_identifier = identifier.removeprefix("osm:way:")
        require(raw_identifier in ways, f"OsmLegacyBuildingWayMissing:{identifier}")
        references = ways[raw_identifier]["refs"]
        require(
            len(references) >= 4
            and references[0] == references[-1]
            and all(reference in nodes for reference in references),
            f"OsmLegacyBuildingGeometryInvalid:{identifier}",
        )
        raw_geometry = rings_to_geometry(
            [[frame.wgs84_to_local(*nodes[reference]) for reference in references]]
        )
        clipped_geometry = _polygonal(raw_geometry.intersection(frame.clip_polygon))
        require(not clipped_geometry.is_empty, f"OsmLegacyBuildingOutsideFrame:{identifier}")
        legacy_buildings.append({"id": identifier, "geometry": clipped_geometry})
    relation_specs: list[dict[str, Any]] = []
    relation_member_way_ids: set[str] = set()
    omitted_incomplete_multipolygons = 0
    for relation in root.findall("relation"):
        tags = _osm_tags(relation)
        kind = surface_kind(tags)
        if tags.get("type") != "multipolygon" or kind is None:
            continue
        outer_ids = [
            member.attrib["ref"]
            for member in relation.findall("member")
            if member.attrib.get("type") == "way" and member.attrib.get("role", "") == "outer"
        ]
        inner_ids = [
            member.attrib["ref"]
            for member in relation.findall("member")
            if member.attrib.get("type") == "way" and member.attrib.get("role", "") == "inner"
        ]
        if not outer_ids or not all(identifier in ways for identifier in outer_ids + inner_ids):
            # bbox API 응답은 경계 밖 relation member를 생략할 수 있다. 열린 조각을
            # 임의로 막지 않고 이 relation만 제외한다.
            omitted_incomplete_multipolygons += 1
            continue
        relation_member_way_ids.update(outer_ids + inner_ids)
        relation_specs.append(
            {
                "id": f"osm:relation:{relation.attrib['id']}",
                "kind": kind,
                "outerIds": outer_ids,
                "innerIds": inner_ids,
            }
        )

    def references_to_ring(references: Sequence[str]) -> list[tuple[float, float]]:
        require(all(reference in nodes for reference in references), "OsmSurfaceNodeMissing")
        return [frame.wgs84_to_local(*nodes[reference]) for reference in references]

    surfaces: list[dict[str, Any]] = []
    clipped_count = 0

    def append_surface(identifier: str, kind: str, geometry: Any) -> None:
        nonlocal clipped_count
        uncut = _polygonal(geometry)
        if uncut.is_empty:
            return
        clipped = _polygonal(uncut.intersection(frame.clip_polygon))
        if clipped.is_empty:
            return
        boundary_clipped = not frame.clip_polygon.covers(uncut)
        if boundary_clipped:
            clipped_count += 1
        surfaces.append(
            {
                "id": identifier,
                "kind": kind,
                "evidenceKind": PUBLIC_STATUS,
                "presentationOnly": True,
                "sourceId": OSM_SOURCE_ID,
                "parts": geometry_to_parts(clipped),
                "_geometry": clipped,
                "_boundaryClipped": boundary_clipped,
            }
        )

    for identifier, way in ways.items():
        kind = surface_kind(way["tags"])
        if kind is None or identifier in relation_member_way_ids:
            continue
        references = way["refs"]
        if len(references) < 4 or references[0] != references[-1]:
            continue
        append_surface(f"osm:way:{identifier}", kind, rings_to_geometry([references_to_ring(references)]))

    for relation in relation_specs:
        try:
            outer_reference_rings = _stitch_way_rings([ways[value]["refs"] for value in relation["outerIds"]])
            inner_reference_rings = _stitch_way_rings([ways[value]["refs"] for value in relation["innerIds"]])
        except SpatialPresentationError as exc:
            if str(exc) != "OsmMultipolygonRingOpen":
                raise
            omitted_incomplete_multipolygons += 1
            continue
        outer_geometries = [
            rings_to_geometry([references_to_ring(references)]) for references in outer_reference_rings
        ]
        relation_geometry = unary_union(outer_geometries)
        if inner_reference_rings:
            inner_geometries = [
                rings_to_geometry([references_to_ring(references)])
                for references in inner_reference_rings
            ]
            relation_geometry = relation_geometry.difference(unary_union(inner_geometries))
        append_surface(relation["id"], relation["kind"], relation_geometry)

    surfaces.sort(key=lambda item: item["id"])
    require(len({item["id"] for item in surfaces}) == len(surfaces), "OsmSurfaceIdDuplicate")
    counts: dict[str, int] = {kind: 0 for kind in sorted(ALL_SURFACE_KINDS)}
    for surface in surfaces:
        counts[surface["kind"]] += 1
    counts["total"] = len(surfaces)
    counts["clipped"] = clipped_count
    counts["omittedIncompleteMultipolygons"] = omitted_incomplete_multipolygons
    return surfaces, legacy_buildings, counts


def validate_promotion_receipt(receipt: dict[str, Any] | None) -> dict[str, Any]:
    require(receipt is not None, "PromotionGate:LicenseReceiptMissing")
    require(receipt.get("datasetId") == PROMOTION_DATASET_ID, "PromotionGate:DatasetIdMismatch")
    require(str(receipt.get("rawHash", "")).upper() == BUILDING_ZIP_HASH, "PromotionGate:RawHashMismatch")
    require(receipt.get("status") == PROMOTION_STATUS, "PromotionGate:StatusMismatch")
    require(receipt.get("licenseCode") == PROMOTION_LICENSE_CODE, "PromotionGate:LicenseCodeMismatch")
    require(receipt.get("sourceIdentityVerified") is True, "PromotionGate:SourceIdentityNotVerified")
    require(
        receipt.get("acquisitionReceipt") == IMMUTABLE_BUILDING_ACQUISITION_RECEIPT,
        "PromotionGate:AcquisitionReceiptMismatch",
    )
    reviewer = receipt.get("reviewer")
    require(
        isinstance(reviewer, str) and 1 <= len(reviewer.strip()) <= 200,
        "PromotionGate:ReviewerMissing",
    )
    require(_is_utc_timestamp(receipt.get("reviewedAtUtc")), "PromotionGate:ReviewedAtUtcInvalid")
    evidence_hash = receipt.get("evidenceHash")
    require(
        isinstance(evidence_hash, str)
        and re.fullmatch(r"[0-9A-F]{64}", evidence_hash) is not None,
        "PromotionGate:EvidenceHashFormatInvalid",
    )
    require(
        evidence_hash == promotion_receipt_evidence_hash(receipt),
        "PromotionGate:EvidenceHashMismatch",
    )
    return receipt


def _source_receipts(mode: str, license_receipt: dict[str, Any] | None) -> list[dict[str, Any]]:
    if mode == "PromotionCandidate":
        receipt = validate_promotion_receipt(license_receipt)
        building_receipt = {
            "provider": "국토교통부",
            "datasetId": PROMOTION_DATASET_ID,
            "sourceUrl": "https://www.data.go.kr/data/15083092/fileData.do",
            "licenseCode": PROMOTION_LICENSE_CODE,
            "licenseUrl": "https://www.kogl.or.kr/info/licenseType1.do",
            "datasetDate": BUILDING_DATASET_DATE,
            "fetchedAtUtc": BUILDING_FETCHED_AT_UTC,
            "rawHash": BUILDING_ZIP_HASH,
            "rawLength": BUILDING_ZIP_LENGTH,
            "originalCrs": "EPSG:5186",
            "reviewStatus": PROMOTION_PENDING_STATUS,
        }
    else:
        building_receipt = {
            "provider": "국토교통부",
            "datasetId": PROMOTION_DATASET_ID,
            "sourceUrl": "https://www.data.go.kr/data/15083092/fileData.do",
            "licenseCode": "RightsConflictUnresolved",
            "licenseUrl": "https://creativecommons.org/licenses/by-nc-nd/2.0/kr/",
            "datasetDate": BUILDING_DATASET_DATE,
            "fetchedAtUtc": BUILDING_FETCHED_AT_UTC,
            "rawHash": BUILDING_ZIP_HASH,
            "rawLength": BUILDING_ZIP_LENGTH,
            "originalCrs": "EPSG:5186",
            "reviewStatus": PRIVATE_STATUS,
        }
    osm_receipt = {
        "provider": "OpenStreetMap contributors",
        "datasetId": "sagajeong-r2-map",
        "sourceUrl": "https://api.openstreetmap.org/api/0.6/map?bbox=127.0825,37.5762,127.0942,37.5854",
        "licenseCode": "ODbL-1.0",
        "licenseUrl": "https://www.openstreetmap.org/copyright",
        "datasetDate": OSM_DATASET_DATE,
        "fetchedAtUtc": OSM_FETCHED_AT_UTC,
        "rawHash": OSM_HASH,
        "rawLength": OSM_LENGTH,
        "originalCrs": "EPSG:4326",
        "reviewStatus": PUBLIC_STATUS,
    }
    return [building_receipt, osm_receipt]


def build_overlay(
    building_zip_path: Path,
    osm_path: Path,
    base_map_path: Path,
    *,
    mode: str = "PrivateReview",
    license_receipt: dict[str, Any] | None = None,
) -> tuple[dict[str, Any], dict[str, Any]]:
    require(mode in {"PrivateReview", "PromotionCandidate"}, "ModeInvalid")
    if mode == "PromotionCandidate":
        validate_promotion_receipt(license_receipt)
    base_map, frame = load_base_map(base_map_path)
    buildings, building_statistics = read_official_buildings(building_zip_path, frame)
    base_legacy_buildings = read_legacy_base_buildings(base_map)
    surfaces, source_legacy_buildings, surface_statistics = read_osm_surfaces(
        osm_path,
        frame,
        {item["id"] for item in base_legacy_buildings},
    )
    base_legacy_by_id = {item["id"]: item for item in base_legacy_buildings}
    for source_building in source_legacy_buildings:
        base_geometry = base_legacy_by_id[source_building["id"]]["geometry"]
        difference_area = source_building["geometry"].symmetric_difference(base_geometry).area
        require(difference_area <= 0.10, f"BaseMapLegacyGeometryDrift:{source_building['id']}")
    match_statistics = bind_legacy_buildings(buildings, source_legacy_buildings)
    status = PROMOTION_PENDING_STATUS if mode == "PromotionCandidate" else PRIVATE_STATUS
    revision = PROMOTION_CANDIDATE_REVISION if mode == "PromotionCandidate" else PRIVATE_REVISION
    for building in buildings:
        building["reviewStatus"] = status
    output_buildings = [
        {key: value for key, value in building.items() if not key.startswith("_")}
        for building in buildings
    ]
    output_surfaces = [
        {key: value for key, value in surface.items() if not key.startswith("_")}
        for surface in surfaces
    ]
    document: dict[str, Any] = {
        "schemaVersion": SCHEMA_VERSION,
        "revision": revision,
        "status": status,
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
        "sourceReceipts": _source_receipts(mode, license_receipt),
        "buildings": output_buildings,
        "surfaces": output_surfaces,
        "contentHash": "",
    }
    if mode == "PromotionCandidate":
        assert license_receipt is not None
        document["promotionReview"] = {
            "reviewer": license_receipt["reviewer"],
            "reviewedAtUtc": license_receipt["reviewedAtUtc"],
            "evidenceHash": license_receipt["evidenceHash"],
        }
    apply_content_hash(document)
    statistics = {
        "buildings": building_statistics,
        "legacyMatches": match_statistics,
        "surfaces": surface_statistics,
        "contentHash": document["contentHash"],
    }
    return document, statistics


def approve_promotion_candidate(
    document: dict[str, Any], license_receipt: dict[str, Any] | None
) -> dict[str, Any]:
    receipt = validate_promotion_receipt(license_receipt)
    require(document.get("revision") == PROMOTION_CANDIDATE_REVISION, "PromotionGate:CandidateRevisionInvalid")
    require(document.get("status") == PROMOTION_PENDING_STATUS, "PromotionGate:CandidateStatusInvalid")
    require(document.get("distributionApproved") is False, "PromotionGate:CandidateAlreadyApproved")
    require(
        document.get("promotionReview")
        == {
            "reviewer": receipt["reviewer"],
            "reviewedAtUtc": receipt["reviewedAtUtc"],
            "evidenceHash": receipt["evidenceHash"],
        },
        "PromotionGate:CandidateReviewBindingMismatch",
    )
    approved = copy.deepcopy(document)
    approved.pop("promotionReview", None)
    approved["revision"] = PUBLIC_REVISION
    approved["status"] = PUBLIC_STATUS
    approved["distributionApproved"] = True
    for building in approved["buildings"]:
        building["reviewStatus"] = PUBLIC_STATUS
    for source_receipt in approved["sourceReceipts"]:
        if source_receipt["rawHash"] == BUILDING_ZIP_HASH:
            source_receipt["reviewStatus"] = PUBLIC_STATUS
    apply_content_hash(approved)
    return approved


def _validate_ring(points: Any, frame: EnuFrame, code: str) -> None:
    require(isinstance(points, list) and len(points) >= 4, f"{code}:RingTooSmall")
    require(points[0] == points[-1], f"{code}:RingNotClosed")
    for point in points:
        require(isinstance(point, dict) and set(point) == {"x", "z"}, f"{code}:PointShape")
        x = float(point["x"])
        z = float(point["z"])
        require(math.isfinite(x) and math.isfinite(z), f"{code}:PointNonFinite")
        require(frame.min_x - 0.001 <= x <= frame.max_x + 0.001, f"{code}:PointOutsideX")
        require(frame.min_z - 0.001 <= z <= frame.max_z + 0.001, f"{code}:PointOutsideZ")


def _validate_parts(parts: Any, frame: EnuFrame, code: str) -> Any:
    require(isinstance(parts, list) and parts, f"{code}:PartsMissing")
    for part in parts:
        require(isinstance(part, dict) and set(part) == {"outer", "holes"}, f"{code}:PartShape")
        require(isinstance(part["outer"], dict) and set(part["outer"]) == {"points"}, f"{code}:OuterShape")
        _validate_ring(part["outer"]["points"], frame, code)
        require(isinstance(part["holes"], list), f"{code}:HolesShape")
        for hole in part["holes"]:
            require(isinstance(hole, dict) and set(hole) == {"points"}, f"{code}:HoleShape")
            _validate_ring(hole["points"], frame, code)
    geometry = parts_to_geometry(parts)
    require(not geometry.is_empty and geometry.is_valid and geometry.area > 0.01, f"{code}:GeometryInvalid")
    require(frame.clip_polygon.covers(geometry), f"{code}:GeometryOutsideClip")
    return geometry


def validate_overlay_document(
    document: dict[str, Any],
    building_zip_path: Path,
    osm_path: Path,
    base_map_path: Path,
) -> tuple[EnuFrame, list[dict[str, Any]], list[dict[str, Any]], list[dict[str, Any]]]:
    verify_content_hash(document)
    require(document.get("schemaVersion") == SCHEMA_VERSION, "OverlaySchemaVersionMismatch")
    require(
        document.get("revision")
        in {PRIVATE_REVISION, PROMOTION_CANDIDATE_REVISION, PUBLIC_REVISION},
        "OverlayRevisionInvalid",
    )
    expected_status = {
        PRIVATE_REVISION: PRIVATE_STATUS,
        PROMOTION_CANDIDATE_REVISION: PROMOTION_PENDING_STATUS,
        PUBLIC_REVISION: PUBLIC_STATUS,
    }[document["revision"]]
    require(document.get("status") == expected_status, "OverlayStatusMismatch")
    require(document.get("presentationOnly") is True, "OverlayPresentationBoundaryMissing")
    require(
        document.get("distributionApproved") is (expected_status == PUBLIC_STATUS),
        "OverlayDistributionBoundaryMismatch",
    )
    base_map, base_frame = load_base_map(base_map_path)
    require(document.get("baseMapRevision") == BASE_MAP_REVISION, "OverlayBaseMapRevisionMismatch")
    require(document.get("baseMapHash") == BASE_MAP_HASH, "OverlayBaseMapHashMismatch")
    require(document.get("coordinateMethod") == COORDINATE_METHOD, "OverlayCoordinateMethodMismatch")
    frame = EnuFrame(
        float(document["originLatitude"]),
        float(document["originLongitude"]),
        float(document["offsetX"]),
        float(document["offsetZ"]),
        float(document["halfExtent"]),
    )
    require(frame == base_frame, "OverlayCoordinateFrameMismatch")
    require(building_zip_path.is_file(), f"BuildingZipMissing:{building_zip_path}")
    require(osm_path.is_file(), f"OsmSourceMissing:{osm_path}")
    require(building_zip_path.stat().st_size == BUILDING_ZIP_LENGTH, "BuildingZipLengthMismatch")
    require(osm_path.stat().st_size == OSM_LENGTH, "OsmSourceLengthMismatch")
    require(sha256_file(building_zip_path) == BUILDING_ZIP_HASH, "BuildingZipHashMismatch")
    require(sha256_file(osm_path) == OSM_HASH, "OsmSourceHashMismatch")
    receipts = document.get("sourceReceipts")
    require(isinstance(receipts, list) and len(receipts) == 2, "OverlaySourceReceiptCountInvalid")
    receipt_by_hash = {str(receipt.get("rawHash", "")): receipt for receipt in receipts}
    require(set(receipt_by_hash) == {BUILDING_ZIP_HASH, OSM_HASH}, "OverlaySourceReceiptHashesInvalid")
    for raw_hash, expected_length in (
        (BUILDING_ZIP_HASH, BUILDING_ZIP_LENGTH),
        (OSM_HASH, OSM_LENGTH),
    ):
        receipt = receipt_by_hash[raw_hash]
        require(int(receipt.get("rawLength", -1)) == expected_length, "OverlaySourceReceiptLengthInvalid")
        require(
            set(receipt)
            == {
                "provider",
                "datasetId",
                "sourceUrl",
                "licenseCode",
                "licenseUrl",
                "datasetDate",
                "fetchedAtUtc",
                "rawHash",
                "rawLength",
                "originalCrs",
                "reviewStatus",
            },
            "OverlaySourceReceiptShapeInvalid",
        )
    building_receipt = receipt_by_hash[BUILDING_ZIP_HASH]
    if expected_status == PUBLIC_STATUS:
        require(building_receipt["datasetId"] == PROMOTION_DATASET_ID, "OverlayPromotedDatasetMismatch")
        require(building_receipt["licenseCode"] == PROMOTION_LICENSE_CODE, "OverlayPromotedLicenseMismatch")
        require(building_receipt["reviewStatus"] == PUBLIC_STATUS, "OverlayPromotedReviewMismatch")
        require("promotionReview" not in document, "OverlayPublicContainsCandidateReview")
    elif expected_status == PROMOTION_PENDING_STATUS:
        require(building_receipt["datasetId"] == PROMOTION_DATASET_ID, "OverlayCandidateDatasetMismatch")
        require(building_receipt["licenseCode"] == PROMOTION_LICENSE_CODE, "OverlayCandidateLicenseMismatch")
        require(
            building_receipt["reviewStatus"] == PROMOTION_PENDING_STATUS,
            "OverlayCandidateReviewMismatch",
        )
        promotion_review = document.get("promotionReview")
        require(
            isinstance(promotion_review, dict)
            and set(promotion_review) == {"reviewer", "reviewedAtUtc", "evidenceHash"},
            "OverlayCandidateReviewBindingInvalid",
        )
    else:
        require(building_receipt["reviewStatus"] == PRIVATE_STATUS, "OverlayPrivateReviewMismatch")
        require("promotionReview" not in document, "OverlayPrivateContainsCandidateReview")

    buildings = document.get("buildings")
    require(isinstance(buildings, list) and buildings, "OverlayBuildingsMissing")
    require([item.get("id") for item in buildings] == sorted(item.get("id") for item in buildings), "OverlayBuildingsNotSorted")
    require(len({item.get("id") for item in buildings}) == len(buildings), "OverlayBuildingIdDuplicate")
    validated_buildings: list[dict[str, Any]] = []
    expected_building_fields = {
        "id",
        "sourceFeatureId",
        "buildingKind",
        "purposeName",
        "heightMeters",
        "heightKind",
        "observedHeightMeters",
        "observedAboveGroundFloors",
        "heightPolicyRevision",
        "reviewStatus",
        "legacyOsmIds",
        "legacyMatch",
        "parts",
    }
    expected_match_fields = {
        "method",
        "centroidDistanceMeters",
        "intersectionOverUnion",
        "smallerFootprintCoverage",
        "ambiguous",
    }
    for building in buildings:
        require(isinstance(building, dict) and set(building) == expected_building_fields, "OverlayBuildingShapeInvalid")
        require(str(building["id"]).startswith("vworld:al-d010:"), "OverlayBuildingIdInvalid")
        require(building["reviewStatus"] == expected_status, "OverlayBuildingReviewStatusMismatch")
        require(building["heightPolicyRevision"] == HEIGHT_POLICY_REVISION, "OverlayHeightPolicyMismatch")
        height = float(building["heightMeters"])
        require(math.isfinite(height) and 0.0 < height <= 500.0, "OverlayHeightInvalid")
        observed = building["observedHeightMeters"]
        if building["heightKind"] == "ObservedSourceHeight":
            require(observed is not None and float(observed) > 0.0, "OverlayObservedHeightMissing")
            require(abs(height - float(observed)) <= 0.001, "OverlayObservedHeightChanged")
        elif building["heightKind"] == "SymbolicFallback4m":
            require(height == SYMBOLIC_HEIGHT_METERS, "OverlaySymbolicHeightChanged")
            require(observed is None or float(observed) <= 0.0, "OverlayPositiveHeightIgnored")
        else:
            raise SpatialPresentationError("OverlayHeightKindInvalid")
        legacy_ids = building["legacyOsmIds"]
        require(isinstance(legacy_ids, list) and legacy_ids == sorted(set(legacy_ids)), "OverlayLegacyIdsInvalid")
        legacy_match = building["legacyMatch"]
        require(isinstance(legacy_match, dict) and set(legacy_match) == expected_match_fields, "OverlayLegacyMatchShapeInvalid")
        require(
            legacy_match["method"]
            in {
                "None",
                "FootprintOverlap",
                "AmbiguousGlobalAliasExcluded",
                "AmbiguousMultipleFootprintsExcluded",
                "WeakFootprintCandidateExcluded",
            },
            "OverlayLegacyMatchMethodInvalid",
        )
        if legacy_match["method"] == "None":
            require(not legacy_ids and legacy_match["ambiguous"] is False, "OverlayLegacyNoneInvalid")
            require(
                legacy_match["centroidDistanceMeters"] is None
                and legacy_match["intersectionOverUnion"] is None
                and legacy_match["smallerFootprintCoverage"] is None,
                "OverlayLegacyNoneMetricsInvalid",
            )
        elif legacy_match["method"] == "FootprintOverlap":
            require(len(legacy_ids) == 1 and legacy_match["ambiguous"] is False, "OverlayLegacyMatchIdentityInvalid")
        else:
            require(not legacy_ids and legacy_match["ambiguous"] is True, "OverlayLegacyExcludedAmbiguityInvalid")
        geometry = _validate_parts(building["parts"], frame, "OverlayBuilding")
        validated_buildings.append(
            {
                "id": building["id"],
                "geometry": geometry,
                "boundaryClipped": _touches_outer_boundary(geometry, frame),
            }
        )

    claimed_aliases = [
        alias for building in buildings for alias in building["legacyOsmIds"]
    ]
    require(len(claimed_aliases) == len(set(claimed_aliases)), "OverlayLegacyAliasClaimedMoreThanOnce")

    surfaces = document.get("surfaces")
    require(isinstance(surfaces, list), "OverlaySurfacesInvalid")
    require([item.get("id") for item in surfaces] == sorted(item.get("id") for item in surfaces), "OverlaySurfacesNotSorted")
    require(len({item.get("id") for item in surfaces}) == len(surfaces), "OverlaySurfaceIdDuplicate")
    expected_surface_fields = {"id", "kind", "evidenceKind", "presentationOnly", "sourceId", "parts"}
    validated_surfaces: list[dict[str, Any]] = []
    for surface in surfaces:
        require(isinstance(surface, dict) and set(surface) == expected_surface_fields, "OverlaySurfaceShapeInvalid")
        require(surface["kind"] in ALL_SURFACE_KINDS, "OverlaySurfaceKindInvalid")
        require(surface["evidenceKind"] == PUBLIC_STATUS, "OverlaySurfaceEvidenceInvalid")
        require(surface["presentationOnly"] is True, "OverlaySurfacePresentationBoundaryMissing")
        require(surface["sourceId"] == OSM_SOURCE_ID, "OverlaySurfaceSourceMismatch")
        geometry = _validate_parts(surface["parts"], frame, "OverlaySurface")
        validated_surfaces.append(
            {
                "id": surface["id"],
                "kind": surface["kind"],
                "geometry": geometry,
                "boundaryClipped": _touches_outer_boundary(geometry, frame),
            }
        )
    return frame, validated_buildings, validated_surfaces, read_legacy_base_buildings(base_map)


def _audit_read_exact(stream: BinaryIO, size: int, code: str) -> bytes:
    value = stream.read(size)
    require(len(value) == size, f"IndependentAudit:{code}")
    return value


def _audit_read_dbf_layout(
    stream: BinaryIO,
) -> tuple[int, int, dict[str, tuple[int, int]]]:
    header = _audit_read_exact(stream, 32, "DbfHeaderTruncated")
    record_count = struct.unpack_from("<I", header, 4)[0]
    header_length, record_length = struct.unpack_from("<HH", header, 8)
    fields: dict[str, tuple[int, int]] = {}
    offset = 1
    while stream.tell() < header_length:
        first = _audit_read_exact(stream, 1, "DbfFieldHeaderTruncated")
        if first == b"\r":
            break
        descriptor = first + _audit_read_exact(stream, 31, "DbfFieldDescriptorTruncated")
        name = descriptor[:11].split(b"\0", 1)[0].decode("ascii", errors="strict")
        length = int(descriptor[16])
        require(name and name not in fields and length > 0, "IndependentAudit:DbfFieldInvalid")
        fields[name] = (offset, length)
        offset += length
    if stream.tell() < header_length:
        _audit_read_exact(stream, header_length - stream.tell(), "DbfHeaderPaddingTruncated")
    require(offset == record_length, "IndependentAudit:DbfRecordLengthMismatch")
    return record_count, record_length, fields


def _audit_dbf_value(
    record: bytes, fields: dict[str, tuple[int, int]], name: str, encoding: str
) -> str:
    offset, length = fields[name]
    return record[offset : offset + length].decode(encoding, errors="strict").strip(" \0")


def _audit_read_shp_header(stream: BinaryIO) -> None:
    header = _audit_read_exact(stream, 100, "ShpHeaderTruncated")
    require(struct.unpack_from(">I", header, 0)[0] == 9994, "IndependentAudit:ShpFileCodeInvalid")
    require(struct.unpack_from("<I", header, 28)[0] == 1000, "IndependentAudit:ShpVersionInvalid")
    require(struct.unpack_from("<I", header, 32)[0] == 5, "IndependentAudit:ShpTypeInvalid")


def _audit_parse_shp_rings(body: bytes) -> list[list[tuple[float, float]]]:
    require(len(body) >= 44, "IndependentAudit:ShpPolygonTruncated")
    require(struct.unpack_from("<I", body, 0)[0] == 5, "IndependentAudit:ShpRecordTypeInvalid")
    part_count, point_count = struct.unpack_from("<II", body, 36)
    require(
        0 < part_count <= 10_000 and 3 < point_count <= 1_000_000,
        "IndependentAudit:ShpPolygonBudgetInvalid",
    )
    points_offset = 44 + part_count * 4
    require(
        points_offset + point_count * 16 <= len(body),
        "IndependentAudit:ShpPointDataTruncated",
    )
    starts = list(struct.unpack_from(f"<{part_count}I", body, 44)) + [point_count]
    require(starts[0] == 0 and starts == sorted(starts), "IndependentAudit:ShpPartIndexInvalid")
    points = [
        struct.unpack_from("<dd", body, points_offset + index * 16)
        for index in range(point_count)
    ]
    return [points[starts[index] : starts[index + 1]] for index in range(part_count)]


def _audit_source_search_bbox(frame: EnuFrame) -> tuple[float, float, float, float]:
    wgs_to_source = Transformer.from_crs(4326, 5186, always_xy=True)
    local_points: list[tuple[float, float]] = []
    for index in range(21):
        ratio = index / 20
        x = frame.min_x + (frame.max_x - frame.min_x) * ratio
        z = frame.min_z + (frame.max_z - frame.min_z) * ratio
        local_points.extend(
            [(x, frame.min_z), (x, frame.max_z), (frame.min_x, z), (frame.max_x, z)]
        )
    wgs_points = [frame.local_to_wgs84(x, z) for x, z in local_points]
    eastings, northings = wgs_to_source.transform(
        [point[0] for point in wgs_points], [point[1] for point in wgs_points]
    )
    return (
        min(eastings) - 2.0,
        min(northings) - 2.0,
        max(eastings) + 2.0,
        max(northings) + 2.0,
    )


def _audit_read_official_source(
    building_zip_path: Path, frame: EnuFrame
) -> tuple[list[dict[str, Any]], dict[str, int]]:
    require(building_zip_path.is_file(), f"BuildingZipMissing:{building_zip_path}")
    require(building_zip_path.stat().st_size == BUILDING_ZIP_LENGTH, "BuildingZipLengthMismatch")
    require(sha256_file(building_zip_path) == BUILDING_ZIP_HASH, "BuildingZipHashMismatch")
    source_to_wgs = Transformer.from_crs(5186, 4326, always_xy=True)
    search_bbox = _audit_source_search_bbox(frame)
    expected: list[dict[str, Any]] = []
    counts = {"sourceRecords": 0, "comparedBuildings": 0}
    with zipfile.ZipFile(building_zip_path, "r") as archive:
        shp_entries = [entry for entry in archive.infolist() if entry.filename.lower().endswith(".shp")]
        dbf_entries = [entry for entry in archive.infolist() if entry.filename.lower().endswith(".dbf")]
        prj_entries = [entry for entry in archive.infolist() if entry.filename.lower().endswith(".prj")]
        require(
            len(shp_entries) == len(dbf_entries) == len(prj_entries) == 1,
            "IndependentAudit:BuildingZipEntryInvalid",
        )
        projection_text = archive.read(prj_entries[0]).decode("utf-8-sig", errors="strict")
        declared = CRS.from_wkt(projection_text)
        require(
            'AUTHORITY["EPSG","5186"]' in projection_text.replace(" ", "")
            and "Korea_Central_Belt_2010" in declared.name
            and declared.is_projected,
            "IndependentAudit:BuildingSourceCrsMismatch",
        )
        with archive.open(shp_entries[0], "r") as shp_stream, archive.open(
            dbf_entries[0], "r"
        ) as dbf_stream:
            _audit_read_shp_header(shp_stream)
            record_count, record_length, fields = _audit_read_dbf_layout(dbf_stream)
            require(record_count == BUILDING_RECORD_COUNT, "IndependentAudit:BuildingRecordCountMismatch")
            require(
                {"A1", "A3", "A8", "A9", "A16", "A26"}.issubset(fields),
                "IndependentAudit:BuildingFieldsMissing",
            )
            for index in range(record_count):
                record = _audit_read_exact(dbf_stream, record_length, "DbfRecordTruncated")
                record_header = _audit_read_exact(shp_stream, 8, "ShpRecordHeaderTruncated")
                record_number, content_words = struct.unpack(">II", record_header)
                require(record_number == index + 1, "IndependentAudit:ShpRecordOrderMismatch")
                require(2 <= content_words <= 1_048_576, "IndependentAudit:ShpRecordBudgetInvalid")
                body = _audit_read_exact(shp_stream, content_words * 2, "ShpRecordBodyTruncated")
                counts["sourceRecords"] += 1
                if record[0] == 0x2A:
                    continue
                require(record[0] == 0x20, "IndependentAudit:DbfDeletionMarkerInvalid")
                if _audit_dbf_value(record, fields, "A3", "ascii") != LEGAL_DONG_CODE:
                    continue
                require(len(body) >= 36, "IndependentAudit:ShpBoundingBoxTruncated")
                if not _bbox_intersects(struct.unpack_from("<4d", body, 4), search_bbox):
                    continue
                source_rings = _audit_parse_shp_rings(body)
                local_rings: list[list[tuple[float, float]]] = []
                for ring in source_rings:
                    longitudes, latitudes = source_to_wgs.transform(
                        [point[0] for point in ring], [point[1] for point in ring]
                    )
                    local_rings.append(
                        [
                            frame.wgs84_to_local(float(longitude), float(latitude))
                            for longitude, latitude in zip(longitudes, latitudes)
                        ]
                    )
                uncut = rings_to_geometry(local_rings)
                clipped = _polygonal(uncut.intersection(frame.clip_polygon))
                if clipped.is_empty:
                    continue
                source_feature_id = _audit_dbf_value(record, fields, "A1", "ascii")
                require(source_feature_id != "", "IndependentAudit:SourceFeatureIdMissing")
                observed_height = _parse_number(
                    _audit_dbf_value(record, fields, "A16", "ascii")
                )
                observed_floors = _parse_integer(
                    _audit_dbf_value(record, fields, "A26", "ascii")
                )
                if observed_height is not None and observed_height > 0.0:
                    height_meters = round(observed_height, 3)
                    height_kind = "ObservedSourceHeight"
                else:
                    height_meters = SYMBOLIC_HEIGHT_METERS
                    height_kind = "SymbolicFallback4m"
                parts = geometry_to_parts(clipped)
                expected.append(
                    {
                        "id": f"vworld:al-d010:{source_feature_id}",
                        "sourceFeatureId": source_feature_id,
                        "buildingKind": _audit_dbf_value(record, fields, "A8", "ascii"),
                        "purposeName": _audit_dbf_value(record, fields, "A9", "cp949"),
                        "heightMeters": height_meters,
                        "heightKind": height_kind,
                        "observedHeightMeters": (
                            round(observed_height, 3) if observed_height is not None else None
                        ),
                        "observedAboveGroundFloors": observed_floors,
                        "partsHash": canonical_parts_hash(parts),
                        "geometry": clipped,
                        "boundaryClipped": not frame.clip_polygon.covers(uncut),
                    }
                )
            require(shp_stream.read(1) == b"", "IndependentAudit:ShpExtraRecords")
    expected.sort(key=lambda item: item["id"])
    require(
        len({item["id"] for item in expected}) == len(expected),
        "IndependentAudit:BuildingIdDuplicate",
    )
    counts["comparedBuildings"] = len(expected)
    return expected, counts


def _audit_stitch_way_rings(way_references: Sequence[Sequence[str]]) -> list[list[str]]:
    remaining = [list(references) for references in way_references if len(references) >= 2]
    rings: list[list[str]] = []
    while remaining:
        current = remaining.pop(0)
        while current[0] != current[-1]:
            selected: int | None = None
            selected_references: list[str] | None = None
            for index, candidate in enumerate(remaining):
                variants = (
                    candidate,
                    list(reversed(candidate)),
                )
                for variant in variants:
                    if current[-1] == variant[0]:
                        selected = index
                        selected_references = variant
                        break
                    if current[0] == variant[-1]:
                        selected = index
                        selected_references = variant[:-1] + current
                        current = []
                        break
                if selected is not None:
                    break
            require(selected is not None and selected_references is not None, "IndependentAudit:OsmRingOpen")
            remaining.pop(selected)
            if current:
                current.extend(selected_references[1:])
            else:
                current = selected_references
        rings.append(current)
    return rings


def _cell_ids_for_geometry(geometry: Any, frame: EnuFrame) -> list[str]:
    if geometry.is_empty:
        return []
    identifiers: list[str] = []
    for z_index in range(10):
        for x_index in range(10):
            min_x = frame.min_x + x_index * GRID_CELL_METERS
            min_z = frame.min_z + z_index * GRID_CELL_METERS
            cell = box(min_x, min_z, min_x + GRID_CELL_METERS, min_z + GRID_CELL_METERS)
            if _bbox_intersects(geometry.bounds, cell.bounds) and geometry.intersection(cell).area > 0.01:
                identifiers.append(f"cell:x{x_index:02d}:z{z_index:02d}")
    return identifiers


def _audit_read_osm_source(
    osm_path: Path,
    frame: EnuFrame,
    legacy_building_ids: set[str],
) -> tuple[list[dict[str, Any]], list[dict[str, Any]], list[dict[str, Any]]]:
    require(osm_path.is_file(), f"OsmSourceMissing:{osm_path}")
    require(osm_path.stat().st_size == OSM_LENGTH, "OsmSourceLengthMismatch")
    require(sha256_file(osm_path) == OSM_HASH, "OsmSourceHashMismatch")
    try:
        root = ET.parse(osm_path).getroot()
    except ET.ParseError as exc:
        raise SpatialPresentationError(f"IndependentAudit:OsmXmlInvalid:{exc}") from exc
    nodes = {
        node.attrib["id"]: (float(node.attrib["lon"]), float(node.attrib["lat"]))
        for node in root.findall("node")
    }
    ways: dict[str, dict[str, Any]] = {}
    for way in root.findall("way"):
        ways[way.attrib["id"]] = {
            "refs": [node.attrib["ref"] for node in way.findall("nd")],
            "tags": {tag.attrib["k"]: tag.attrib["v"] for tag in way.findall("tag")},
        }

    def ring_from_references(references: Sequence[str]) -> list[tuple[float, float]]:
        require(
            all(reference in nodes for reference in references),
            "IndependentAudit:OsmNodeMissing",
        )
        return [frame.wgs84_to_local(*nodes[reference]) for reference in references]

    legacy_buildings: list[dict[str, Any]] = []
    for identifier in sorted(legacy_building_ids):
        require(identifier.startswith("osm:way:"), "IndependentAudit:LegacyIdInvalid")
        raw_identifier = identifier.removeprefix("osm:way:")
        require(raw_identifier in ways, f"IndependentAudit:LegacyAliasMissing:{identifier}")
        references = ways[raw_identifier]["refs"]
        require(
            len(references) >= 4 and references[0] == references[-1],
            f"IndependentAudit:LegacyWayOpen:{identifier}",
        )
        geometry = rings_to_geometry([ring_from_references(references)])
        geometry = _polygonal(geometry.intersection(frame.clip_polygon))
        require(not geometry.is_empty, f"IndependentAudit:LegacyWayOutside:{identifier}")
        legacy_buildings.append({"id": identifier, "geometry": geometry})

    relation_member_way_ids: set[str] = set()
    relation_specs: list[dict[str, Any]] = []
    omitted: list[dict[str, Any]] = []

    def available_outer_geometry(outer_ids: Sequence[str]) -> Any:
        if not outer_ids or not all(identifier in ways for identifier in outer_ids):
            return GeometryCollection()
        try:
            stitched = _audit_stitch_way_rings([ways[identifier]["refs"] for identifier in outer_ids])
            geometries = [
                rings_to_geometry([ring_from_references(references)])
                for references in stitched
            ]
        except SpatialPresentationError:
            return GeometryCollection()
        return _polygonal(unary_union(geometries).intersection(frame.clip_polygon))

    def append_omitted(
        identifier: str,
        kind: str,
        reason: str,
        missing_ids: Sequence[str],
        available_ids: Sequence[str],
        available_geometry: Any,
    ) -> None:
        parts_hash = None
        affected_cells: list[str] = []
        if not available_geometry.is_empty:
            parts_hash = canonical_parts_hash(geometry_to_parts(available_geometry))
            affected_cells = _cell_ids_for_geometry(available_geometry, frame)
        omitted.append(
            {
                "id": identifier,
                "kind": kind,
                "reason": reason,
                "missingMemberWayIds": sorted(set(missing_ids)),
                "availableMemberWayIds": sorted(set(available_ids)),
                "availableGeometryHash": parts_hash,
                "affectedCellIds": affected_cells,
            }
        )

    for relation in root.findall("relation"):
        tags = {tag.attrib["k"]: tag.attrib["v"] for tag in relation.findall("tag")}
        kind = surface_kind(tags)
        if tags.get("type") != "multipolygon" or kind is None:
            continue
        outer_ids = [
            member.attrib["ref"]
            for member in relation.findall("member")
            if member.attrib.get("type") == "way" and member.attrib.get("role", "") == "outer"
        ]
        inner_ids = [
            member.attrib["ref"]
            for member in relation.findall("member")
            if member.attrib.get("type") == "way" and member.attrib.get("role", "") == "inner"
        ]
        all_ids = outer_ids + inner_ids
        missing_ids = [identifier for identifier in all_ids if identifier not in ways]
        if not outer_ids or missing_ids:
            append_omitted(
                f"osm:relation:{relation.attrib['id']}",
                kind,
                "MissingMemberWays",
                missing_ids,
                [identifier for identifier in all_ids if identifier in ways],
                available_outer_geometry(outer_ids),
            )
            continue
        relation_member_way_ids.update(all_ids)
        relation_specs.append(
            {
                "id": f"osm:relation:{relation.attrib['id']}",
                "kind": kind,
                "outerIds": outer_ids,
                "innerIds": inner_ids,
            }
        )

    surfaces: list[dict[str, Any]] = []

    def append_surface(identifier: str, kind: str, uncut: Any) -> None:
        polygonal = _polygonal(uncut)
        if polygonal.is_empty:
            return
        clipped = _polygonal(polygonal.intersection(frame.clip_polygon))
        if clipped.is_empty:
            return
        parts = geometry_to_parts(clipped)
        surfaces.append(
            {
                "id": identifier,
                "kind": kind,
                "partsHash": canonical_parts_hash(parts),
                "geometry": clipped,
                "boundaryClipped": not frame.clip_polygon.covers(polygonal),
            }
        )

    for raw_identifier, way in ways.items():
        kind = surface_kind(way["tags"])
        if kind is None or raw_identifier in relation_member_way_ids:
            continue
        references = way["refs"]
        identifier = f"osm:way:{raw_identifier}"
        if len(references) < 4 or references[0] != references[-1] or not all(
            reference in nodes for reference in references
        ):
            append_omitted(
                identifier,
                kind,
                "OpenOrMissingWayGeometry",
                [reference for reference in references if reference not in nodes],
                [],
                GeometryCollection(),
            )
            continue
        append_surface(identifier, kind, rings_to_geometry([ring_from_references(references)]))

    for relation in relation_specs:
        try:
            outer_rings = _audit_stitch_way_rings(
                [ways[identifier]["refs"] for identifier in relation["outerIds"]]
            )
            inner_rings = _audit_stitch_way_rings(
                [ways[identifier]["refs"] for identifier in relation["innerIds"]]
            )
            outer_geometry = unary_union(
                [rings_to_geometry([ring_from_references(references)]) for references in outer_rings]
            )
            relation_geometry = outer_geometry
            if inner_rings:
                relation_geometry = relation_geometry.difference(
                    unary_union(
                        [
                            rings_to_geometry([ring_from_references(references)])
                            for references in inner_rings
                        ]
                    )
                )
            append_surface(relation["id"], relation["kind"], relation_geometry)
        except SpatialPresentationError as exc:
            if str(exc) != "IndependentAudit:OsmRingOpen":
                raise
            append_omitted(
                relation["id"],
                relation["kind"],
                "OpenMultipolygonRing",
                [],
                relation["outerIds"] + relation["innerIds"],
                available_outer_geometry(relation["outerIds"]),
            )

    surfaces.sort(key=lambda item: item["id"])
    omitted.sort(key=lambda item: item["id"])
    require(
        len({item["id"] for item in surfaces}) == len(surfaces),
        "IndependentAudit:SurfaceIdDuplicate",
    )
    require(
        any(item["id"] == KNOWN_INCOMPLETE_OSM_RELATION_ID for item in omitted),
        "IndependentAudit:KnownIncompleteRelationNotRecorded",
    )
    return surfaces, legacy_buildings, omitted


def _audit_expected_legacy_matches(
    buildings: Sequence[dict[str, Any]], legacy_buildings: Sequence[dict[str, Any]]
) -> tuple[dict[str, dict[str, Any]], dict[str, int]]:
    geometries = [item["geometry"] for item in legacy_buildings]
    tree = STRtree(geometries)
    all_candidates: dict[str, list[dict[str, Any]]] = {}
    credible_candidate_ids: dict[str, set[str]] = {}
    for building in buildings:
        geometry = building["geometry"]
        strong: list[dict[str, Any]] = []
        weak: list[dict[str, Any]] = []
        for raw_index in tree.query(geometry):
            legacy = legacy_buildings[int(raw_index)]
            intersection_area = geometry.intersection(legacy["geometry"]).area
            if intersection_area <= 0.01:
                continue
            union_area = geometry.union(legacy["geometry"]).area
            smaller_area = min(geometry.area, legacy["geometry"].area)
            larger_area = max(geometry.area, legacy["geometry"].area)
            iou = intersection_area / union_area if union_area > 0 else 0.0
            coverage = intersection_area / smaller_area if smaller_area > 0 else 0.0
            area_ratio = smaller_area / larger_area if larger_area > 0 else 0.0
            distance = geometry.centroid.distance(legacy["geometry"].centroid)
            if iou < 0.20 and coverage < 0.55:
                continue
            candidate = {
                "id": legacy["id"],
                "centroidDistanceMeters": distance,
                "intersectionOverUnion": iou,
                "smallerFootprintCoverage": coverage,
            }
            if (
                iou >= LEGACY_MATCH_MIN_IOU
                and coverage >= LEGACY_MATCH_MIN_SMALLER_COVERAGE
                and area_ratio >= LEGACY_MATCH_MIN_AREA_RATIO
                and distance <= max(2.0, math.sqrt(smaller_area) * LEGACY_MATCH_CENTROID_SCALE)
            ):
                strong.append(candidate)
            else:
                weak.append(candidate)
        strong.sort(
            key=lambda item: (
                -item["smallerFootprintCoverage"],
                -item["intersectionOverUnion"],
                item["centroidDistanceMeters"],
                item["id"],
            )
        )
        weak.sort(
            key=lambda item: (
                -item["intersectionOverUnion"],
                -item["smallerFootprintCoverage"],
                item["centroidDistanceMeters"],
                item["id"],
            )
        )
        combined = strong + weak
        combined.sort(
            key=lambda item: (
                -item["smallerFootprintCoverage"],
                -item["intersectionOverUnion"],
                item["centroidDistanceMeters"],
                item["id"],
            )
        )
        all_candidates[building["id"]] = combined
        credible_candidate_ids[building["id"]] = {item["id"] for item in strong}
    claims: dict[str, list[str]] = {}
    for building_id, candidates in all_candidates.items():
        for candidate in candidates:
            claims.setdefault(candidate["id"], []).append(building_id)
    duplicated = {identifier for identifier, values in claims.items() if len(values) > 1}
    result: dict[str, dict[str, Any]] = {}
    counts = {"matchedBuildings": 0, "ambiguousBuildings": 0, "weakCandidateBuildings": 0}
    for building in buildings:
        candidates = all_candidates[building["id"]]
        credible_ids = credible_candidate_ids[building["id"]]
        if not candidates:
            result[building["id"]] = {
                "legacyOsmIds": [],
                "legacyMatch": {
                    "method": "None",
                    "centroidDistanceMeters": None,
                    "intersectionOverUnion": None,
                    "smallerFootprintCoverage": None,
                    "ambiguous": False,
                },
            }
            continue
        best = candidates[0]
        metrics = {
            "centroidDistanceMeters": round(best["centroidDistanceMeters"], 3),
            "intersectionOverUnion": round(best["intersectionOverUnion"], 6),
            "smallerFootprintCoverage": round(best["smallerFootprintCoverage"], 6),
        }
        globally_ambiguous = any(candidate["id"] in duplicated for candidate in candidates)
        if len(candidates) > 1 or globally_ambiguous:
            result[building["id"]] = {
                "legacyOsmIds": [],
                "legacyMatch": {
                    "method": (
                        "AmbiguousGlobalAliasExcluded"
                        if globally_ambiguous
                        else "AmbiguousMultipleFootprintsExcluded"
                    ),
                    **metrics,
                    "ambiguous": True,
                },
            }
            counts["ambiguousBuildings"] += 1
        elif best["id"] not in credible_ids:
            result[building["id"]] = {
                "legacyOsmIds": [],
                "legacyMatch": {
                    "method": "WeakFootprintCandidateExcluded",
                    **metrics,
                    "ambiguous": True,
                },
            }
            counts["weakCandidateBuildings"] += 1
        else:
            result[building["id"]] = {
                "legacyOsmIds": [best["id"]],
                "legacyMatch": {
                    "method": "FootprintOverlap",
                    **metrics,
                    "ambiguous": False,
                },
            }
            counts["matchedBuildings"] += 1
    counts["globallyDuplicatedAliasCount"] = len(duplicated)
    return result, counts


def independently_verify_overlay_sources(
    document: dict[str, Any],
    building_zip_path: Path,
    osm_path: Path,
    base_map_path: Path,
    frame: EnuFrame,
) -> dict[str, Any]:
    """생성기 결과를 사용하지 않고 원본 바이트에서 기대값을 다시 만든다."""

    source_buildings, building_counts = _audit_read_official_source(building_zip_path, frame)
    overlay_buildings = {item["id"]: item for item in document["buildings"]}
    source_ids = [item["id"] for item in source_buildings]
    require(
        sorted(overlay_buildings) == source_ids,
        "IndependentAudit:OfficialBuildingSetMismatch",
    )
    comparison_fields = (
        "sourceFeatureId",
        "buildingKind",
        "purposeName",
        "heightMeters",
        "heightKind",
        "observedHeightMeters",
        "observedAboveGroundFloors",
    )
    for source in source_buildings:
        overlay = overlay_buildings[source["id"]]
        for field in comparison_fields:
            require(
                overlay[field] == source[field],
                f"IndependentAudit:BuildingFieldMismatch:{field}:{source['id']}",
            )
        require(
            canonical_parts_hash(overlay["parts"]) == source["partsHash"],
            f"IndependentAudit:BuildingGeometryMismatch:{source['id']}",
        )

    base_map, base_frame = load_base_map(base_map_path)
    require(base_frame == frame, "IndependentAudit:BaseFrameMismatch")
    base_legacy = read_legacy_base_buildings(base_map)
    source_surfaces, source_legacy, omitted = _audit_read_osm_source(
        osm_path,
        frame,
        {item["id"] for item in base_legacy},
    )
    base_legacy_by_id = {item["id"]: item for item in base_legacy}
    for source in source_legacy:
        require(
            source["geometry"].symmetric_difference(
                base_legacy_by_id[source["id"]]["geometry"]
            ).area
            <= 0.10,
            f"IndependentAudit:LegacyBaseGeometryMismatch:{source['id']}",
        )

    overlay_surfaces = {item["id"]: item for item in document["surfaces"]}
    require(
        sorted(overlay_surfaces) == [item["id"] for item in source_surfaces],
        "IndependentAudit:SurfaceSetMismatch",
    )
    for source in source_surfaces:
        overlay = overlay_surfaces[source["id"]]
        require(overlay["kind"] == source["kind"], f"IndependentAudit:SurfaceKindMismatch:{source['id']}")
        require(
            canonical_parts_hash(overlay["parts"]) == source["partsHash"],
            f"IndependentAudit:SurfaceGeometryMismatch:{source['id']}",
        )

    expected_matches, match_counts = _audit_expected_legacy_matches(
        source_buildings, source_legacy
    )
    for source in source_buildings:
        overlay = overlay_buildings[source["id"]]
        expected = expected_matches[source["id"]]
        require(
            overlay["legacyOsmIds"] == expected["legacyOsmIds"],
            f"IndependentAudit:LegacyAliasMismatch:{source['id']}",
        )
        require(
            overlay["legacyMatch"] == expected["legacyMatch"],
            f"IndependentAudit:LegacyMetricsMismatch:{source['id']}",
        )

    return {
        "buildings": source_buildings,
        "surfaces": source_surfaces,
        "legacyBuildings": source_legacy,
        "omittedSourceFeatures": omitted,
        "summary": {
            "method": "IndependentBinaryShpDbfAndOsmReparse",
            "buildingSourceRecords": building_counts["sourceRecords"],
            "comparedBuildingCount": building_counts["comparedBuildings"],
            "comparedSurfaceCount": len(source_surfaces),
            "verifiedLegacyOsmBuildingCount": len(source_legacy),
            "verifiedLegacyAliasCount": sum(
                len(item["legacyOsmIds"]) for item in document["buildings"]
            ),
            "comparedFields": ["A8", "A9", "A16", "A26", "sourceFeatureId"],
            "geometryComparison": "CanonicalPartsSha256",
            "legacyMatchPolicy": {
                "minimumIntersectionOverUnion": LEGACY_MATCH_MIN_IOU,
                "minimumAreaRatio": LEGACY_MATCH_MIN_AREA_RATIO,
                "minimumSmallerFootprintCoverage": LEGACY_MATCH_MIN_SMALLER_COVERAGE,
                "centroidLimit": "max(2m,sqrt(smallerArea)*0.75)",
            },
            "legacyMatchCounts": match_counts,
            "omittedSourceFeatureCount": len(omitted),
        },
    }


def _positive_intersection(geometry: Any, target: Any) -> bool:
    if not _bbox_intersects(geometry.bounds, target.bounds):
        return False
    return geometry.intersection(target).area > 0.01


def build_coverage_audit(
    document: dict[str, Any],
    overlay_file_hash: str,
    frame: EnuFrame,
    buildings: Sequence[dict[str, Any]],
    surfaces: Sequence[dict[str, Any]],
    legacy_buildings: Sequence[dict[str, Any]],
    omitted_source_features: Sequence[dict[str, Any]] = (),
    source_audit_summary: dict[str, Any] | None = None,
    promotion_review: dict[str, Any] | None = None,
) -> dict[str, Any]:
    cells: list[dict[str, Any]] = []
    classification_counts = {
        "ConfirmedBuilt": 0,
        "ConfirmedOpen": 0,
        "IncompleteSurfaceEvidence": 0,
        "MissingCoverage": 0,
    }
    evidence_counts = {
        "BothSources": 0,
        "PrivateSourceOnly": 0,
        "LegacyOsmOnly": 0,
        "NoBuildingEvidence": 0,
    }
    clipped_feature_ids = {
        item["id"]
        for item in list(buildings) + list(surfaces)
        if item["boundaryClipped"]
    }
    omitted_by_cell: dict[str, list[str]] = {}
    for omitted in omitted_source_features:
        for cell_id in omitted["affectedCellIds"]:
            omitted_by_cell.setdefault(cell_id, []).append(omitted["id"])
    for z_index in range(10):
        for x_index in range(10):
            min_x = frame.min_x + x_index * GRID_CELL_METERS
            min_z = frame.min_z + z_index * GRID_CELL_METERS
            cell_geometry = box(min_x, min_z, min_x + GRID_CELL_METERS, min_z + GRID_CELL_METERS)
            official = [item for item in buildings if _positive_intersection(item["geometry"], cell_geometry)]
            legacy = [item for item in legacy_buildings if _positive_intersection(item["geometry"], cell_geometry)]
            cell_surfaces = [item for item in surfaces if _positive_intersection(item["geometry"], cell_geometry)]
            building_intersections = [
                item["geometry"].intersection(cell_geometry) for item in official
            ]
            open_intersections = [
                item["geometry"].intersection(cell_geometry)
                for item in cell_surfaces
                if item["kind"] in QUALIFIED_OPEN_SURFACE_KINDS
            ]
            building_union = (
                _polygonal(unary_union(building_intersections))
                if building_intersections
                else GeometryCollection()
            )
            open_union = (
                _polygonal(unary_union(open_intersections))
                if open_intersections
                else GeometryCollection()
            )
            building_area = building_union.area
            open_only = (
                _polygonal(open_union.difference(building_union))
                if not open_union.is_empty
                else GeometryCollection()
            )
            open_area = open_only.area
            cell_area = GRID_CELL_METERS * GRID_CELL_METERS
            unknown_area = max(0.0, cell_area - building_area - open_area)
            building_ratio = min(1.0, building_area / cell_area)
            open_ratio = min(1.0, open_area / cell_area)
            unknown_ratio = min(1.0, unknown_area / cell_area)
            cell_id = f"cell:x{x_index:02d}:z{z_index:02d}"
            omitted_ids = sorted(set(omitted_by_cell.get(cell_id, [])))
            if building_ratio >= BUILDING_COVERAGE_THRESHOLD - 0.0000001:
                classification = "ConfirmedBuilt"
            elif omitted_ids:
                classification = "IncompleteSurfaceEvidence"
            elif open_ratio >= OPEN_COVERAGE_THRESHOLD - 0.0000001:
                classification = "ConfirmedOpen"
            else:
                classification = "MissingCoverage"
            if official and legacy:
                evidence_mix = "BothSources"
            elif official:
                evidence_mix = "PrivateSourceOnly"
            elif legacy:
                evidence_mix = "LegacyOsmOnly"
            else:
                evidence_mix = "NoBuildingEvidence"
            boundary_ids = sorted(
                {
                    item["id"]
                for item in list(official) + list(cell_surfaces)
                if item["id"] in clipped_feature_ids
                }
            )
            classification_counts[classification] += 1
            evidence_counts[evidence_mix] += 1
            cells.append(
                {
                    "id": cell_id,
                    "xIndex": x_index,
                    "zIndex": z_index,
                    "bounds": {
                        "minX": _round_coordinate(min_x),
                        "minZ": _round_coordinate(min_z),
                        "maxX": _round_coordinate(min_x + GRID_CELL_METERS),
                        "maxZ": _round_coordinate(min_z + GRID_CELL_METERS),
                    },
                    "classification": classification,
                    "evidenceMix": evidence_mix,
                    "officialBuildingCount": len(official),
                    "legacyOsmBuildingCount": len(legacy),
                    "buildingCoverageAreaSquareMeters": round(building_area, 3),
                    "buildingCoverageRatio": round(building_ratio, 6),
                    "openCoverageAreaSquareMeters": round(open_area, 3),
                    "openCoverageRatio": round(open_ratio, 6),
                    "unknownCoverageAreaSquareMeters": round(unknown_area, 3),
                    "unknownCoverageRatio": round(unknown_ratio, 6),
                    "surfaceEvidenceStatus": (
                        "IncompleteSurfaceEvidence" if omitted_ids else "Complete"
                    ),
                    "omittedSourceFeatureIds": omitted_ids,
                    "surfaceKinds": sorted({item["kind"] for item in cell_surfaces}),
                    "boundaryClipped": bool(boundary_ids),
                    "boundaryClippedFeatureCount": len(boundary_ids),
                    "boundaryClippedFeatureIds": boundary_ids,
                }
            )
    incomplete_affected_cells = sorted(omitted_by_cell)
    audit_status = (
        "PassedWithIncompleteSurfaceEvidence"
        if incomplete_affected_cells
        else "Passed"
    )
    audit: dict[str, Any] = {
        "schemaVersion": AUDIT_SCHEMA_VERSION,
        "revision": AUDIT_REVISION,
        "status": audit_status,
        "overlayRevision": document["revision"],
        "overlayStatus": document["status"],
        "overlayContentHash": document["contentHash"],
        "overlayFileHash": overlay_file_hash,
        "baseMapRevision": document["baseMapRevision"],
        "baseMapHash": document["baseMapHash"],
        "grid": {
            "cellSizeMeters": GRID_CELL_METERS,
            "xCount": 10,
            "zCount": 10,
            "classificationRule": {
                "confirmedBuilt": "official-building-coverage-ratio-at-least-0.20",
                "confirmedOpen": "building-coverage-below-0.20-complete-surface-evidence-and-open-coverage-ratio-at-least-0.20",
                "incompleteSurfaceEvidence": "building-coverage-below-0.20-and-an-omitted-surface-source-feature-affects-the-cell",
                "missingCoverage": "building-coverage-below-0.20-complete-surface-evidence-and-open-coverage-below-0.20",
                "buildingCoverageThreshold": BUILDING_COVERAGE_THRESHOLD,
                "openCoverageThreshold": OPEN_COVERAGE_THRESHOLD,
                "qualifiedOpenSurfaceKinds": sorted(QUALIFIED_OPEN_SURFACE_KINDS),
                "coveragePartition": "building-union,qualified-open-minus-building,remaining-unknown",
            },
        },
        "auditStatusRule": {
            "Passed": "independent-source-comparison-passed-and-no-omitted-surface-feature-affects-the-grid",
            "PassedWithIncompleteSurfaceEvidence": "independent-source-comparison-passed-and-one-or-more-omitted-surface-features-affect-the-grid",
            "Failed": "validation-raises-and-no-audit-report-is-published",
        },
        "sourceAudit": source_audit_summary or {},
        "sourceCompleteness": {
            "status": (
                "IncompleteSurfaceEvidence" if incomplete_affected_cells else "Complete"
            ),
            "knownIncompleteRelationId": KNOWN_INCOMPLETE_OSM_RELATION_ID,
            "omittedSourceFeatureCount": len(omitted_source_features),
            "affectedCellCount": len(incomplete_affected_cells),
            "affectedCellIds": incomplete_affected_cells,
            "omittedSourceFeatures": list(omitted_source_features),
        },
        "summary": {
            "cellCount": len(cells),
            "classificationCounts": classification_counts,
            "evidenceMixCounts": evidence_counts,
            "officialBuildingCount": len(buildings),
            "legacyOsmBuildingCount": len(legacy_buildings),
            "surfaceCount": len(surfaces),
            "boundaryClipped": bool(clipped_feature_ids),
            "boundaryClippedFeatureCount": len(clipped_feature_ids),
            "observedSourceHeightCount": sum(
                1 for building in document["buildings"] if building["heightKind"] == "ObservedSourceHeight"
            ),
            "symbolicFallbackHeightCount": sum(
                1 for building in document["buildings"] if building["heightKind"] == "SymbolicFallback4m"
            ),
            "incompleteSurfaceEvidenceCellCount": len(incomplete_affected_cells),
        },
        "cells": cells,
        "limitations": [
            "ConfirmedBuilt는 공식 건물 footprint의 셀 면적 비율이 20% 이상이라는 뜻이며 현재 건물 상태나 접근 가능성을 확인하지 않는다.",
            "ConfirmedOpen은 완전한 표면 근거 셀에서 건물 제외 지정 OSM 표현 면적 비율이 20% 이상이라는 뜻이며 공터의 소유권, 통행 또는 배치 허가가 아니다.",
            "IncompleteSurfaceEvidence는 누락 OSM 표면 feature의 가용 외곽이 닿는 셀이므로 열린 공간 또는 자료 부재로 단정하지 않는다.",
            "MissingCoverage는 실제 건물이나 토지의 부재를 뜻하지 않는다.",
            "LocalPrivateReview 결과는 Unity Resources, 공개 API, 게임 권위로 자동 승격하지 않는다.",
        ],
        "auditHash": "",
    }
    if promotion_review is not None:
        audit["promotionReview"] = copy.deepcopy(promotion_review)
    audit["auditHash"] = hash_with_empty_field(audit, "auditHash")
    return audit


def render_coverage_html(audit: dict[str, Any]) -> str:
    colors = {
        "ConfirmedBuilt": "#6f8f72",
        "ConfirmedOpen": "#87b8a0",
        "IncompleteSurfaceEvidence": "#e6b566",
        "MissingCoverage": "#d8c7ab",
    }
    cells = {(cell["xIndex"], cell["zIndex"]): cell for cell in audit["cells"]}
    rows: list[str] = []
    for z_index in range(9, -1, -1):
        entries: list[str] = []
        for x_index in range(10):
            cell = cells[(x_index, z_index)]
            title = html.escape(
                f"{cell['id']} | {cell['classification']} | {cell['evidenceMix']} | "
                f"official={cell['officialBuildingCount']} | legacy={cell['legacyOsmBuildingCount']} | "
                f"built={cell['buildingCoverageRatio']:.1%} | open={cell['openCoverageRatio']:.1%} | "
                f"unknown={cell['unknownCoverageRatio']:.1%} | clipped={cell['boundaryClipped']}"
            )
            entries.append(
                f'<td title="{title}" style="background:{colors[cell["classification"]]}">'
                f'<strong>{html.escape(cell["classification"])}</strong><br>'
                f'{html.escape(cell["evidenceMix"])}<br>'
                f'건물면 {cell["buildingCoverageRatio"]:.0%} / 열린면 {cell["openCoverageRatio"]:.0%}'
                f'{"<br>경계 절단" if cell["boundaryClipped"] else ""}</td>'
            )
        rows.append(f'<tr><th>z{z_index:02d}</th>{"".join(entries)}</tr>')
    summary = audit["summary"]
    classifications = summary["classificationCounts"]
    return f"""<!doctype html>
<html lang="ko">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>사가정 공간 표현 100m coverage 감사</title>
<style>
body{{font-family:system-ui,sans-serif;margin:24px;color:#1f2933;background:#f7f4ed}}
code{{overflow-wrap:anywhere}} table{{border-collapse:collapse;width:100%;table-layout:fixed}}
th,td{{border:1px solid #475569;padding:6px;font-size:11px;text-align:center;vertical-align:top}}
th{{background:#e2e8f0}} .legend span{{display:inline-block;padding:6px 10px;margin:0 6px 8px 0}}
</style>
</head>
<body>
<h1>사가정 공간 표현 100m coverage 감사</h1>
<p>상태: <strong>{html.escape(audit['overlayStatus'])}</strong> · overlay revision: <code>{html.escape(audit['overlayRevision'])}</code></p>
<p>content hash: <code>{html.escape(audit['overlayContentHash'])}</code></p>
<p>건물 {summary['officialBuildingCount']}개 · OSM 기존 건물 {summary['legacyOsmBuildingCount']}개 · 토지표현 {summary['surfaceCount']}개 · 경계 절단 {summary['boundaryClippedFeatureCount']}개</p>
<div class="legend">
<span style="background:{colors['ConfirmedBuilt']}">ConfirmedBuilt {classifications['ConfirmedBuilt']}</span>
<span style="background:{colors['ConfirmedOpen']}">ConfirmedOpen {classifications['ConfirmedOpen']}</span>
<span style="background:{colors['IncompleteSurfaceEvidence']}">IncompleteSurfaceEvidence {classifications['IncompleteSurfaceEvidence']}</span>
<span style="background:{colors['MissingCoverage']}">MissingCoverage {classifications['MissingCoverage']}</span>
</div>
<table>
<thead><tr><th>북↑</th>{''.join(f'<th>x{x:02d}</th>' for x in range(10))}</tr></thead>
<tbody>{''.join(rows)}</tbody>
</table>
<h2>판정 경계</h2>
<ul>{''.join(f'<li>{html.escape(value)}</li>' for value in audit['limitations'])}</ul>
<p>audit hash: <code>{html.escape(audit['auditHash'])}</code></p>
</body>
</html>
"""


def run_self_tests() -> dict[str, Any]:
    first = {"b": 2, "a": 1, "contentHash": ""}
    second = {"contentHash": "", "a": 1, "b": 2}
    require(
        hash_with_empty_field(first, "contentHash") == hash_with_empty_field(second, "contentHash"),
        "SelfTestCanonicalHash",
    )
    frame = EnuFrame(37.5806971, 127.0884106, 550.0, 8.0, 500.0)
    origin = frame.wgs84_to_local(frame.origin_longitude, frame.origin_latitude)
    require(abs(origin[0] - frame.offset_x) < 0.0001, "SelfTestOriginX")
    require(abs(origin[1] - frame.offset_z) < 0.0001, "SelfTestOriginZ")
    longitude, latitude = frame.local_to_wgs84(*origin)
    require(abs(longitude - frame.origin_longitude) < 0.0000001, "SelfTestInverseLongitude")
    require(abs(latitude - frame.origin_latitude) < 0.0000001, "SelfTestInverseLatitude")
    clipped = Polygon(
        [
            (frame.min_x - 10, frame.min_z + 10),
            (frame.min_x + 10, frame.min_z + 10),
            (frame.min_x + 10, frame.min_z + 30),
            (frame.min_x - 10, frame.min_z + 30),
            (frame.min_x - 10, frame.min_z + 10),
        ]
    ).intersection(frame.clip_polygon)
    parts = geometry_to_parts(clipped)
    restored = parts_to_geometry(parts)
    require(abs(restored.area - 200.0) < 0.01, "SelfTestClipArea")
    require(_touches_outer_boundary(restored, frame), "SelfTestBoundaryClip")
    require(surface_kind({"leisure": "park"}) == "park", "SelfTestSurfacePark")
    require(surface_kind({"amenity": "school", "building": "yes"}) is None, "SelfTestSchoolBuilding")
    valid_receipt = {
        "datasetId": PROMOTION_DATASET_ID,
        "rawHash": BUILDING_ZIP_HASH,
        "status": PROMOTION_STATUS,
        "licenseCode": PROMOTION_LICENSE_CODE,
        "sourceIdentityVerified": True,
        "acquisitionReceipt": copy.deepcopy(IMMUTABLE_BUILDING_ACQUISITION_RECEIPT),
        "reviewer": "SyntheticFixture:SelfTest",
        "reviewedAtUtc": "2026-09-13T00:00:00Z",
        "evidenceHash": "",
    }
    valid_receipt["evidenceHash"] = promotion_receipt_evidence_hash(valid_receipt)
    validate_promotion_receipt(valid_receipt)
    rejected = False
    try:
        validate_promotion_receipt({**valid_receipt, "sourceIdentityVerified": False})
    except SpatialPresentationError as exc:
        rejected = str(exc) == "PromotionGate:SourceIdentityNotVerified"
    require(rejected, "SelfTestPromotionRejection")
    require_manager_promotion_token("self-test-token", "self-test-token")
    token_rejected = False
    try:
        require_manager_promotion_token("self-test-token", "different-token")
    except SpatialPresentationError as exc:
        token_rejected = str(exc) == "PromotionGate:ManagerTokenMismatch"
    require(token_rejected, "SelfTestPromotionTokenRejection")
    evidence_rejected = False
    try:
        validate_promotion_receipt({**valid_receipt, "reviewer": "changed-after-signing"})
    except SpatialPresentationError as exc:
        evidence_rejected = str(exc) == "PromotionGate:EvidenceHashMismatch"
    require(evidence_rejected, "SelfTestPromotionEvidenceBinding")
    synthetic_candidate = {
        "revision": PROMOTION_CANDIDATE_REVISION,
        "status": PROMOTION_PENDING_STATUS,
        "distributionApproved": False,
        "promotionReview": {
            "reviewer": valid_receipt["reviewer"],
            "reviewedAtUtc": valid_receipt["reviewedAtUtc"],
            "evidenceHash": valid_receipt["evidenceHash"],
        },
        "buildings": [{"reviewStatus": PROMOTION_PENDING_STATUS}],
        "sourceReceipts": [
            {"rawHash": BUILDING_ZIP_HASH, "reviewStatus": PROMOTION_PENDING_STATUS}
        ],
        "contentHash": "",
    }
    approved_fixture = approve_promotion_candidate(synthetic_candidate, valid_receipt)
    require(
        approved_fixture["revision"] == PUBLIC_REVISION
        and approved_fixture["status"] == PUBLIC_STATUS
        and approved_fixture["distributionApproved"] is True
        and "promotionReview" not in approved_fixture
        and approved_fixture["buildings"][0]["reviewStatus"] == PUBLIC_STATUS,
        "SelfTestPromotionCandidateApproval",
    )

    weak_building = {
        "id": "official:weak",
        "_geometry": box(0, 0, 10, 10),
        "legacyOsmIds": [],
        "legacyMatch": {},
    }
    weak_match_counts = bind_legacy_buildings(
        [weak_building], [{"id": "osm:way:weak", "geometry": box(-20, -20, 30, 30)}]
    )
    require(
        weak_building["legacyMatch"]["method"] == "WeakFootprintCandidateExcluded"
        and weak_building["legacyOsmIds"] == []
        and weak_building["legacyMatch"]["ambiguous"] is True,
        "SelfTestWeakLegacyCandidateExcluded",
    )
    require(weak_match_counts["weakCandidateBuildings"] == 1, "SelfTestWeakLegacyCount")

    building_geometry = box(frame.min_x, frame.min_z, frame.min_x + 50, frame.min_z + 50)
    open_geometry = box(
        frame.min_x + 100,
        frame.min_z,
        frame.min_x + 150,
        frame.min_z + 50,
    )
    document = {
        "revision": PRIVATE_REVISION,
        "status": PRIVATE_STATUS,
        "contentHash": "A" * 64,
        "baseMapRevision": BASE_MAP_REVISION,
        "baseMapHash": BASE_MAP_HASH,
        "buildings": [
            {"heightKind": "ObservedSourceHeight"},
            {"heightKind": "SymbolicFallback4m"},
        ],
    }
    audit = build_coverage_audit(
        document,
        "B" * 64,
        frame,
        [{"id": "official:1", "geometry": building_geometry, "boundaryClipped": True}],
        [{"id": "osm:surface:1", "kind": "park", "geometry": open_geometry, "boundaryClipped": True}],
        [],
        [
            {
                "id": KNOWN_INCOMPLETE_OSM_RELATION_ID,
                "kind": "wood",
                "reason": "MissingMemberWays",
                "missingMemberWayIds": ["1"],
                "availableMemberWayIds": ["2"],
                "availableGeometryHash": "C" * 64,
                "affectedCellIds": ["cell:x02:z00"],
            }
        ],
        {"method": "SyntheticIndependentSourceFixture"},
    )
    require(audit["summary"]["cellCount"] == 100, "SelfTestCellCount")
    require(audit["summary"]["classificationCounts"]["ConfirmedBuilt"] == 1, "SelfTestConfirmedBuilt")
    require(audit["summary"]["classificationCounts"]["ConfirmedOpen"] == 1, "SelfTestConfirmedOpen")
    require(
        audit["summary"]["classificationCounts"]["IncompleteSurfaceEvidence"] == 1,
        "SelfTestIncompleteSurfaceEvidence",
    )
    require(audit["summary"]["classificationCounts"]["MissingCoverage"] == 97, "SelfTestMissingCoverage")
    require(audit["status"] == "PassedWithIncompleteSurfaceEvidence", "SelfTestAuditStatus")
    require(
        abs(
            audit["cells"][0]["buildingCoverageRatio"]
            + audit["cells"][0]["openCoverageRatio"]
            + audit["cells"][0]["unknownCoverageRatio"]
            - 1.0
        )
        <= 0.000001,
        "SelfTestCoveragePartition",
    )
    require(audit["summary"]["boundaryClipped"], "SelfTestAuditBoundaryClip")
    require(audit["auditHash"] == hash_with_empty_field(audit, "auditHash"), "SelfTestAuditHash")
    return {
        "passed": True,
        "checks": 26,
        "canonicalHash": hash_with_empty_field(first, "contentHash"),
        "coverage": audit["summary"]["classificationCounts"],
    }
