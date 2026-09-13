"""역 중심 실제 공간 사본을 동결 원천에서 결정적으로 만드는 공통 도구.

이 모듈은 표현 후보만 만든다. 도로 폭, 보행 가능성, 통행 권위, gameplay 상태와
배포 권한을 만들지 않는다. 기존 사가정 파이프라인의 좌표·도형 정규화 코드를
재사용하되 역·창·원천은 :class:`StationSpatialDescriptor`가 소유한다.
"""

from __future__ import annotations

import copy
import hashlib
import io
import json
import math
import struct
import xml.etree.ElementTree as ET
import zipfile
from dataclasses import dataclass
from pathlib import Path
from typing import Any, BinaryIO, Iterable, Sequence

import shapefile
from pyproj import CRS, Transformer
from shapely.geometry import (
    GeometryCollection,
    LineString,
    MultiLineString,
    MultiPolygon,
    Point,
    Polygon,
    box,
    shape,
)
from shapely.ops import transform, unary_union

from sagajeong_spatial_presentation import (
    EnuFrame,
    SpatialPresentationError,
    _bbox_intersects,
    _dbf_text,
    _osm_tags,
    _parse_integer,
    _parse_number,
    _parse_shp_polygon,
    _polygonal,
    _read_dbf_header,
    _read_exact,
    _read_shp_header,
    _source_search_bbox,
    _stitch_way_rings,
    _transform_source_rings,
    _zip_entry,
    canonical_json_bytes,
    geometry_to_parts,
    parts_to_geometry,
    require,
    rings_to_geometry,
    sha256_file,
    surface_kind,
)


SCHEMA_VERSION = "ssalddel.station-spatial-snapshot.v1"
AUDIT_SCHEMA_VERSION = "ssalddel.station-spatial-snapshot-audit.v1"
STATUS = "LocalPrivateReview"
COORDINATE_METHOD = "WGS84-ECEF-ENU-at-zero-altitude"
HEIGHT_POLICY_REVISION = "station-building-height-presentation.r1"
SYMBOLIC_HEIGHT_METERS = 4.0
BUILDING_COVERAGE_THRESHOLD = 0.20
OPEN_COVERAGE_THRESHOLD = 0.20
QUALIFIED_OPEN_SURFACE_KINDS = frozenset(
    {"park", "garden", "school", "playground", "pitch", "parking", "water", "wood"}
)
ALL_SURFACE_KINDS = frozenset(
    set(QUALIFIED_OPEN_SURFACE_KINDS)
    | {"residential", "commercial", "industrial", "religious", "pedestrian", "grass", "cemetery"}
)


@dataclass(frozen=True)
class FrozenSource:
    path: str
    sha256: str
    byte_length: int

    def validate(self, name: str) -> None:
        require(bool(self.path), f"{name}PathMissing")
        require(
            len(self.sha256) == 64
            and self.sha256.upper() == self.sha256
            and all(character in "0123456789ABCDEF" for character in self.sha256),
            f"{name}HashInvalid",
        )
        require(self.byte_length > 0, f"{name}LengthInvalid")


@dataclass(frozen=True)
class StationSpatialDescriptor:
    transit_station_stable_id: str
    station_name: str
    line_code: str
    station_code: str
    latitude: float
    longitude: float
    width_meters: int
    depth_meters: int
    tile_size_meters: int
    revision: str
    building_source: FrozenSource
    nodelink_source: FrozenSource
    nodelink_source_directory: str
    administrative_boundary_source: FrozenSource
    osm_source: FrozenSource
    osm_source_id: str
    osm_receipt_path: str
    osm_source_url: str

    def validate(self) -> None:
        require(
            self.transit_station_stable_id.startswith("station:kr:kric:")
            and self.transit_station_stable_id.endswith(f":{self.station_code}"),
            "StationStableIdInvalid",
        )
        require(bool(self.station_name.strip()), "StationNameMissing")
        require(bool(self.line_code.strip()) and bool(self.station_code.strip()), "StationServiceIdMissing")
        require(math.isfinite(self.latitude) and -85.0 <= self.latitude <= 85.0, "StationLatitudeInvalid")
        require(math.isfinite(self.longitude) and -180.0 <= self.longitude <= 180.0, "StationLongitudeInvalid")
        require(self.width_meters == 1000 and self.depth_meters == 1000, "StationWindowMustBeOneKilometer")
        require(self.tile_size_meters == 500, "StationTileSizeMustBe500Meters")
        require(bool(self.revision.strip()), "StationSnapshotRevisionMissing")
        require(
            self.osm_source_id.startswith("openstreetmap:")
            and self.station_code in self.osm_source_id,
            "OsmSourceIdInvalid",
        )
        require(self.osm_source_url.startswith("https://api.openstreetmap.org/api/0.6/map?bbox="), "OsmSourceUrlInvalid")
        self.building_source.validate("BuildingSource")
        self.nodelink_source.validate("NodeLinkSource")
        self.administrative_boundary_source.validate("AdministrativeBoundarySource")
        self.osm_source.validate("OsmSource")

    @property
    def frame(self) -> EnuFrame:
        return EnuFrame(self.latitude, self.longitude, 0.0, 0.0, self.width_meters / 2.0)


def myeonmok_descriptor(
    *,
    building_path: str = "artifacts/local/neighborhood-source-acquisition/AL_D010_11_20260809.zip",
    nodelink_path: str = "artifacts/local/public-data/sagajeong-nodelink-20260912-r1/nodelink.zip",
    nodelink_source_directory: str = "artifacts/local/public-data/sagajeong-nodelink-20260912-r1/source-link",
    administrative_boundary_path: str = "artifacts/local/public-data/admin-dong/20260912-seoul-oa22160/seoul-administrative-dong-boundary.zip",
    osm_path: str = "artifacts/local/neighborhood-source-acquisition/myeonmok-station-0721-r1/map.osm",
    osm_receipt_path: str = "artifacts/local/neighborhood-source-acquisition/myeonmok-station-0721-r1/receipt.json",
) -> StationSpatialDescriptor:
    """현재 동결 원천에 결속한 면목역 표본 descriptor."""

    return StationSpatialDescriptor(
        transit_station_stable_id="station:kr:kric:s1107:0721",
        station_name="면목역",
        line_code="S1107",
        station_code="0721",
        latitude=37.588671,
        longitude=127.087503,
        width_meters=1000,
        depth_meters=1000,
        tile_size_meters=500,
        revision="myeonmok-station-spatial-snapshot.private-review.r1",
        building_source=FrozenSource(
            building_path,
            "674C5A9583996DD6B8946525EDAD8197BE79A634F1DB00D39E2B3133D0D2A755",
            135_675_376,
        ),
        nodelink_source=FrozenSource(
            nodelink_path,
            "5BBF5A01D677B6DCB941CC5954FCC256D6DED60D96F95336A23FC2B53B39D4E4",
            269_611_477,
        ),
        nodelink_source_directory=nodelink_source_directory,
        administrative_boundary_source=FrozenSource(
            administrative_boundary_path,
            "969F7033BD3609A5FD586790F5B2CFEDC638D7647EF45C78F9C75E1DABF79F68",
            1_676_539,
        ),
        osm_source=FrozenSource(
            osm_path,
            "BD218CB2D9CC49F996DB7BCF2384F6950CCEAC772D5322987879AA21570D0A66",
            1_503_543,
        ),
        osm_source_id="openstreetmap:myeonmok-station-0721-r1",
        osm_receipt_path=osm_receipt_path,
        osm_source_url=(
            "https://api.openstreetmap.org/api/0.6/map?"
            "bbox=127.0818422,37.5841659,127.0931645,37.5931758"
        ),
    )


def yongmasan_descriptor(
    *,
    building_path: str = "artifacts/local/neighborhood-source-acquisition/AL_D010_11_20260809.zip",
    nodelink_path: str = "artifacts/local/public-data/sagajeong-nodelink-20260912-r1/nodelink.zip",
    nodelink_source_directory: str = "artifacts/local/public-data/sagajeong-nodelink-20260912-r1/source-link",
    administrative_boundary_path: str = "artifacts/local/public-data/admin-dong/20260912-seoul-oa22160/seoul-administrative-dong-boundary.zip",
    osm_path: str = "artifacts/local/neighborhood-source-acquisition/yongmasan-station-0723-r1/map.osm",
    osm_receipt_path: str = "artifacts/local/neighborhood-source-acquisition/yongmasan-station-0723-r1/receipt.json",
) -> StationSpatialDescriptor:
    """현재 동결 원천에 결속한 용마산역 표본 descriptor."""

    return StationSpatialDescriptor(
        transit_station_stable_id="station:kr:kric:s1107:0723",
        station_name="용마산역",
        line_code="S1107",
        station_code="0723",
        latitude=37.573752,
        longitude=127.086802,
        width_meters=1000,
        depth_meters=1000,
        tile_size_meters=500,
        revision="yongmasan-station-spatial-snapshot.private-review.r1",
        building_source=FrozenSource(
            building_path,
            "674C5A9583996DD6B8946525EDAD8197BE79A634F1DB00D39E2B3133D0D2A755",
            135_675_376,
        ),
        nodelink_source=FrozenSource(
            nodelink_path,
            "5BBF5A01D677B6DCB941CC5954FCC256D6DED60D96F95336A23FC2B53B39D4E4",
            269_611_477,
        ),
        nodelink_source_directory=nodelink_source_directory,
        administrative_boundary_source=FrozenSource(
            administrative_boundary_path,
            "969F7033BD3609A5FD586790F5B2CFEDC638D7647EF45C78F9C75E1DABF79F68",
            1_676_539,
        ),
        osm_source=FrozenSource(
            osm_path,
            "5E690398DD779E91EDF467A09424FBBBB4448E5B3DC7D6FE3A15865E543227F1",
            1_389_565,
        ),
        osm_source_id="openstreetmap:yongmasan-station-0723-r1",
        osm_receipt_path=osm_receipt_path,
        osm_source_url=(
            "https://api.openstreetmap.org/api/0.6/map?"
            "bbox=127.0811416,37.5692469,127.0924624,37.5782568"
        ),
    )


def repository_path(repository_root: Path, value: str) -> Path:
    path = Path(value)
    return path.resolve() if path.is_absolute() else (repository_root / path).resolve()


def local_output_path(repository_root: Path, value: str) -> Path:
    path = repository_path(repository_root, value)
    local_root = (repository_root / "artifacts" / "local").resolve()
    try:
        path.relative_to(local_root)
    except ValueError as exc:
        raise SpatialPresentationError(f"OutputMustRemainUnderArtifactsLocal:{path}") from exc
    return path


def load_json(path: Path) -> dict[str, Any]:
    require(path.is_file(), f"JsonMissing:{path}")
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
    except (UnicodeError, json.JSONDecodeError) as exc:
        raise SpatialPresentationError(f"JsonInvalid:{path.name}") from exc
    require(isinstance(value, dict), f"JsonObjectRequired:{path.name}")
    return value


def write_json_deterministic(path: Path, value: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    text = json.dumps(value, ensure_ascii=False, allow_nan=False, indent=2) + "\n"
    pending = path.with_name(path.name + ".pending")
    require(not pending.exists(), f"PendingOutputAlreadyExists:{pending}")
    pending.write_text(text, encoding="utf-8", newline="\n")
    pending.replace(path)


def hash_with_empty_field(document: dict[str, Any], field_name: str) -> str:
    candidate = copy.deepcopy(document)
    candidate[field_name] = ""
    return hashlib.sha256(canonical_json_bytes(candidate)).hexdigest().upper()


def apply_content_hash(document: dict[str, Any], field_name: str = "contentHash") -> None:
    document[field_name] = ""
    document[field_name] = hash_with_empty_field(document, field_name)


def verify_content_hash(document: dict[str, Any], field_name: str = "contentHash") -> None:
    actual = document.get(field_name)
    require(isinstance(actual, str) and len(actual) == 64, f"{field_name}FormatInvalid")
    require(actual == hash_with_empty_field(document, field_name), f"{field_name}Mismatch")


def _verify_frozen_file(repository_root: Path, source: FrozenSource, name: str) -> Path:
    path = repository_path(repository_root, source.path)
    require(path.is_file(), f"{name}Missing:{path}")
    require(path.stat().st_size == source.byte_length, f"{name}LengthMismatch")
    require(sha256_file(path) == source.sha256, f"{name}HashMismatch")
    return path


def _round_coordinate(value: float) -> float:
    rounded = round(float(value), 3)
    return 0.0 if rounded == 0.0 else rounded


def _bounds(geometry: Any) -> dict[str, float]:
    min_x, min_z, max_x, max_z = geometry.bounds
    return {
        "minX": _round_coordinate(min_x),
        "minZ": _round_coordinate(min_z),
        "maxX": _round_coordinate(max_x),
        "maxZ": _round_coordinate(max_z),
    }


def _line_members(geometry: Any) -> list[LineString]:
    if geometry.is_empty:
        return []
    if isinstance(geometry, LineString):
        return [geometry]
    if isinstance(geometry, MultiLineString):
        return list(geometry.geoms)
    if isinstance(geometry, GeometryCollection):
        result: list[LineString] = []
        for member in geometry.geoms:
            result.extend(_line_members(member))
        return result
    return []


def line_to_parts(geometry: Any) -> list[dict[str, Any]]:
    parts: list[dict[str, Any]] = []
    for line in _line_members(geometry):
        points: list[dict[str, float]] = []
        for coordinate in line.coords:
            point = {"x": _round_coordinate(coordinate[0]), "z": _round_coordinate(coordinate[1])}
            if not points or point != points[-1]:
                points.append(point)
        if len(points) >= 2:
            parts.append({"points": points})
    parts.sort(key=lambda item: tuple((point["x"], point["z"]) for point in item["points"]))
    require(bool(parts), "LineContainsNoOutputPart")
    return parts


def line_parts_to_geometry(parts: Sequence[dict[str, Any]]) -> Any:
    lines = [
        LineString([(float(point["x"]), float(point["z"])) for point in part["points"]])
        for part in parts
        if len(part.get("points", [])) >= 2
    ]
    require(bool(lines), "LinePartsInvalid")
    return unary_union(lines)


def _wgs84_window(frame: EnuFrame) -> dict[str, Any]:
    corners = [
        ("southWest", frame.min_x, frame.min_z),
        ("southEast", frame.max_x, frame.min_z),
        ("northEast", frame.max_x, frame.max_z),
        ("northWest", frame.min_x, frame.max_z),
    ]
    values = []
    for name, x, z in corners:
        longitude, latitude = frame.local_to_wgs84(x, z)
        values.append({"name": name, "longitude": longitude, "latitude": latitude})
    return {
        "minLongitude": min(value["longitude"] for value in values),
        "minLatitude": min(value["latitude"] for value in values),
        "maxLongitude": max(value["longitude"] for value in values),
        "maxLatitude": max(value["latitude"] for value in values),
        "corners": values,
    }


def read_official_buildings(
    repository_root: Path, descriptor: StationSpatialDescriptor
) -> tuple[list[dict[str, Any]], dict[str, Any]]:
    path = _verify_frozen_file(repository_root, descriptor.building_source, "BuildingZip")
    frame = descriptor.frame
    with zipfile.ZipFile(path, "r") as archive:
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
        source_crs = CRS.from_epsg(5186)
        source_to_wgs = Transformer.from_crs(source_crs, 4326, always_xy=True)
        search_bbox = _source_search_bbox(source_crs, frame)
        buildings: list[dict[str, Any]] = []
        identifiers: set[str] = set()
        legal_counts: dict[str, int] = {}
        statistics: dict[str, Any] = {
            "sourceRecords": 0,
            "bboxCandidates": 0,
            "selectedBuildings": 0,
            "boundaryClippedBuildings": 0,
            "observedHeightBuildings": 0,
            "symbolicHeightBuildings": 0,
            "deletedRecords": 0,
            "selectedLegalDongCounts": legal_counts,
        }
        with archive.open(shp_entry, "r") as shp_stream, archive.open(dbf_entry, "r") as dbf_stream:
            _read_shp_header(shp_stream)
            record_count, _, record_length, fields = _read_dbf_header(dbf_stream)
            require(record_count == 695_761, "BuildingRecordCountMismatch")
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
                require(len(body) >= 36, "ShpRecordBoundingBoxTruncated")
                if not _bbox_intersects(struct.unpack_from("<4d", body, 4), search_bbox):
                    continue
                statistics["bboxCandidates"] += 1
                source_rings = _parse_shp_polygon(body)
                local_rings = _transform_source_rings(source_rings, source_to_wgs, frame)
                uncut_geometry = rings_to_geometry(local_rings)
                clipped_geometry = _polygonal(uncut_geometry.intersection(frame.clip_polygon))
                if clipped_geometry.is_empty:
                    continue
                source_feature_id = _dbf_text(record, fields["A1"], "ascii")
                legal_code = _dbf_text(record, fields["A3"], "ascii")
                require(source_feature_id != "", "BuildingSourceFeatureIdMissing")
                require(len(legal_code) == 10 and legal_code.isdigit(), "BuildingLegalDongCodeInvalid")
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
                    statistics["boundaryClippedBuildings"] += 1
                legal_counts[legal_code] = legal_counts.get(legal_code, 0) + 1
                buildings.append(
                    {
                        "id": stable_id,
                        "sourceFeatureId": source_feature_id,
                        "legalDongCode": legal_code,
                        "buildingKind": _dbf_text(record, fields["A8"], "ascii"),
                        "purposeName": _dbf_text(record, fields["A9"], "cp949"),
                        "heightMeters": height_meters,
                        "heightKind": height_kind,
                        "observedHeightMeters": round(observed_height, 3) if observed_height is not None else None,
                        "observedAboveGroundFloors": observed_floors,
                        "heightPolicyRevision": HEIGHT_POLICY_REVISION,
                        "reviewStatus": STATUS,
                        "boundaryClipped": boundary_clipped,
                        "parts": geometry_to_parts(clipped_geometry),
                        "_geometry": clipped_geometry,
                    }
                )
            require(shp_stream.read(1) == b"", "ShpContainsUnexpectedExtraRecords")
        buildings.sort(key=lambda item: item["id"])
        statistics["selectedBuildings"] = len(buildings)
        statistics["selectedLegalDongCounts"] = dict(sorted(legal_counts.items()))
        return buildings, statistics


def _hash_stream(stream: BinaryIO) -> str:
    digest = hashlib.sha256()
    for chunk in iter(lambda: stream.read(1024 * 1024), b""):
        digest.update(chunk)
    return digest.hexdigest().upper()


def _verify_nodelink_extract(
    repository_root: Path, descriptor: StationSpatialDescriptor
) -> tuple[Path, list[dict[str, str]]]:
    archive_path = _verify_frozen_file(repository_root, descriptor.nodelink_source, "NodeLinkArchive")
    source_directory = repository_path(repository_root, descriptor.nodelink_source_directory)
    require(source_directory.is_dir(), "NodeLinkSourceDirectoryMissing")
    members = [f"MOCT_LINK.{extension}" for extension in ("cpg", "prj", "shp", "shx", "dbf")]
    receipts: list[dict[str, str]] = []
    with zipfile.ZipFile(archive_path, "r") as archive:
        require(all(name in archive.namelist() for name in members), "NodeLinkArchiveMembersMissing")
        for name in members:
            extracted = source_directory / name
            require(extracted.is_file(), f"NodeLinkExtractedMemberMissing:{name}")
            with archive.open(name, "r") as stream:
                archived_hash = _hash_stream(stream)
            extracted_hash = sha256_file(extracted)
            require(archived_hash == extracted_hash, f"NodeLinkExtractedMemberHashMismatch:{name}")
            receipts.append({"file": name, "sha256": extracted_hash})
    return source_directory, receipts


def read_nodelink_roads(
    repository_root: Path, descriptor: StationSpatialDescriptor
) -> tuple[list[dict[str, Any]], dict[str, Any]]:
    source_directory, source_files = _verify_nodelink_extract(repository_root, descriptor)
    frame = descriptor.frame
    cpg = (source_directory / "MOCT_LINK.cpg").read_text(encoding="ascii").strip()
    require(cpg == "949", "NodeLinkEncodingMismatch")
    wkt = (source_directory / "MOCT_LINK.prj").read_text(encoding="ascii")
    source_crs = CRS.from_wkt(wkt)
    to_geo = Transformer.from_crs(source_crs, 4326, always_xy=True)
    search_bbox = _source_search_bbox(source_crs, frame)
    roads: list[dict[str, Any]] = []
    identifiers: set[str] = set()
    candidates = 0
    operations: dict[str, float] = {}
    with shapefile.Reader(
        str(source_directory / "MOCT_LINK.shp"), encoding="cp949", encodingErrors="strict"
    ) as reader:
        fields = reader.fields[1:]
        field_names = {field[0] for field in fields}
        require(
            {"LINK_ID", "F_NODE", "T_NODE", "LANES", "ROAD_NAME", "UPDATEDATE"}.issubset(field_names),
            "NodeLinkDbfFieldsMissing",
        )
        national_count = len(reader)
        require(national_count == 1_557_364, "NodeLinkNationalRecordCountMismatch")
        for shape_record in reader.iterShapeRecords(bbox=search_bbox):
            candidates += 1
            source_geometry = shape(shape_record.shape.__geo_interface__)
            geo = transform(to_geo.transform, source_geometry)
            operation = to_geo.get_last_used_operation()
            operations[operation.description] = operation.accuracy
            local = transform(lambda x, y, z=None: frame.wgs84_to_local(x, y), geo)
            clipped = local.intersection(frame.clip_polygon)
            if clipped.is_empty or not _line_members(clipped):
                continue
            properties = shape_record.record.as_dict()
            require(isinstance(properties["LANES"], int) and 0 <= properties["LANES"] <= 30, "NodeLinkLaneCountInvalid")
            stable_id = f"nodelink:link:{properties['LINK_ID']}"
            require(stable_id not in identifiers, "NodeLinkStableIdDuplicate")
            identifiers.add(stable_id)
            boundary_clipped = not frame.clip_polygon.covers(local)
            roads.append(
                {
                    "id": stable_id,
                    "sourceRecordIndex": shape_record.record.oid,
                    "fromNodeId": str(properties["F_NODE"]),
                    "toNodeId": str(properties["T_NODE"]),
                    "roadName": str(properties["ROAD_NAME"]),
                    "laneCount": properties["LANES"],
                    "updatedDate": str(properties["UPDATEDATE"]),
                    "reviewStatus": STATUS,
                    "presentationOnly": True,
                    "traversalReady": False,
                    "boundaryClipped": boundary_clipped,
                    "parts": line_to_parts(clipped),
                    "_geometry": clipped,
                }
            )
    roads.sort(key=lambda item: item["id"])
    return roads, {
        "nationalRecordCount": national_count,
        "bboxCandidates": candidates,
        "selectedDirectedLinks": len(roads),
        "boundaryClippedDirectedLinks": sum(1 for road in roads if road["boundaryClipped"]),
        "roadNames": sorted({road["roadName"] for road in roads}),
        "coordinateOperations": dict(sorted(operations.items())),
        "sourceFiles": source_files,
    }


def _validate_osm_receipt(
    repository_root: Path, descriptor: StationSpatialDescriptor
) -> tuple[Path, dict[str, Any]]:
    osm_path = _verify_frozen_file(repository_root, descriptor.osm_source, "OsmSource")
    receipt = load_json(repository_path(repository_root, descriptor.osm_receipt_path))
    require(receipt.get("schemaVersion") == "station-osm-source-acquisition.v1", "OsmReceiptSchemaMismatch")
    require(receipt.get("transitStationStableId") == descriptor.transit_station_stable_id, "OsmReceiptStationMismatch")
    require(receipt.get("sourceUrl") == descriptor.osm_source_url, "OsmReceiptUrlMismatch")
    raw = receipt.get("raw", {})
    require(raw.get("sha256") == descriptor.osm_source.sha256, "OsmReceiptHashMismatch")
    require(raw.get("byteLength") == descriptor.osm_source.byte_length, "OsmReceiptLengthMismatch")
    license_value = receipt.get("license", {})
    require(license_value.get("code") == "ODbL-1.0", "OsmReceiptLicenseMismatch")
    require(license_value.get("attribution") == "© OpenStreetMap contributors", "OsmReceiptAttributionMismatch")
    policy = receipt.get("policy", {})
    require(
        policy.get("reviewStatus") == STATUS
        and policy.get("distributionApproved") is False
        and policy.get("gameplayReady") is False
        and policy.get("traversalReady") is False
        and policy.get("periodicAcquisition") is False
        and policy.get("relationMembersMayBeIncomplete") is True,
        "OsmReceiptPolicyMismatch",
    )
    return osm_path, receipt


def read_osm_surfaces(
    repository_root: Path, descriptor: StationSpatialDescriptor
) -> tuple[list[dict[str, Any]], list[dict[str, Any]], dict[str, Any]]:
    osm_path, receipt = _validate_osm_receipt(repository_root, descriptor)
    frame = descriptor.frame
    try:
        root = ET.parse(osm_path).getroot()
    except ET.ParseError as exc:
        raise SpatialPresentationError(f"OsmXmlInvalid:{exc}") from exc
    require(root.tag == "osm" and root.attrib.get("version") == "0.6", "OsmRootInvalid")
    require(root.attrib.get("copyright") == "OpenStreetMap and contributors", "OsmCopyrightMissing")
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

    relation_specs: list[dict[str, Any]] = []
    relation_member_ids: set[str] = set()
    incomplete: list[dict[str, Any]] = []
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
        relation_member_ids.update(outer_ids + inner_ids)
        missing_way_ids = [identifier for identifier in outer_ids + inner_ids if identifier not in ways]
        missing_node_ids = sorted(
            {
                reference
                for identifier in outer_ids + inner_ids
                if identifier in ways
                for reference in ways[identifier]["refs"]
                if reference not in nodes
            }
        )
        if not outer_ids or missing_way_ids or missing_node_ids:
            incomplete.append(
                {
                    "id": f"osm:relation:{relation.attrib['id']}",
                    "kind": kind,
                    "reason": "RelationMembersMissing",
                    "missingWayIds": sorted(missing_way_ids),
                    "missingNodeIds": missing_node_ids,
                    "availableWayIds": sorted(identifier for identifier in outer_ids + inner_ids if identifier in ways),
                }
            )
            continue
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
    identifiers: set[str] = set()

    def append_surface(identifier: str, kind: str, geometry: Any) -> None:
        uncut = _polygonal(geometry)
        if uncut.is_empty:
            return
        clipped = _polygonal(uncut.intersection(frame.clip_polygon))
        if clipped.is_empty:
            return
        require(identifier not in identifiers, "OsmSurfaceIdDuplicate")
        identifiers.add(identifier)
        surfaces.append(
            {
                "id": identifier,
                "kind": kind,
                "evidenceKind": "PublicOpenData",
                "presentationOnly": True,
                "sourceId": descriptor.osm_source_id,
                "boundaryClipped": not frame.clip_polygon.covers(uncut),
                "parts": geometry_to_parts(clipped),
                "_geometry": clipped,
            }
        )

    for identifier, way in ways.items():
        kind = surface_kind(way["tags"])
        if kind is None or identifier in relation_member_ids:
            continue
        references = way["refs"]
        if len(references) < 4 or references[0] != references[-1] or not all(ref in nodes for ref in references):
            continue
        append_surface(f"osm:way:{identifier}", kind, rings_to_geometry([references_to_ring(references)]))

    for relation in relation_specs:
        try:
            outer_rings = _stitch_way_rings([ways[value]["refs"] for value in relation["outerIds"]])
            inner_rings = _stitch_way_rings([ways[value]["refs"] for value in relation["innerIds"]])
        except SpatialPresentationError as exc:
            if str(exc) != "OsmMultipolygonRingOpen":
                raise
            incomplete.append(
                {
                    "id": relation["id"],
                    "kind": relation["kind"],
                    "reason": "RelationRingOpen",
                    "missingWayIds": [],
                    "missingNodeIds": [],
                    "availableWayIds": sorted(relation["outerIds"] + relation["innerIds"]),
                }
            )
            continue
        relation_geometry = unary_union(
            [rings_to_geometry([references_to_ring(references)]) for references in outer_rings]
        )
        if inner_rings:
            relation_geometry = relation_geometry.difference(
                unary_union([rings_to_geometry([references_to_ring(references)]) for references in inner_rings])
            )
        append_surface(relation["id"], relation["kind"], relation_geometry)

    incomplete.sort(key=lambda item: item["id"])
    surfaces.sort(key=lambda item: item["id"])
    counts = {kind: 0 for kind in sorted(ALL_SURFACE_KINDS)}
    for surface in surfaces:
        counts[surface["kind"]] += 1
    counts.update(
        {
            "total": len(surfaces),
            "boundaryClipped": sum(1 for surface in surfaces if surface["boundaryClipped"]),
            "omittedIncompleteMultipolygons": len(incomplete),
            "sourceNodes": len(nodes),
            "sourceWays": len(ways),
            "sourceRelations": len(root.findall("relation")),
        }
    )
    for item in incomplete:
        available_geometries = []
        for way_id in item["availableWayIds"]:
            references = ways[way_id]["refs"]
            coordinates = [frame.wgs84_to_local(*nodes[ref]) for ref in references if ref in nodes]
            if len(coordinates) >= 2:
                available_geometries.append(LineString(coordinates))
        item["_availableGeometry"] = unary_union(available_geometries) if available_geometries else GeometryCollection()
    return surfaces, incomplete, {"counts": counts, "receipt": receipt}


def read_administrative_areas(
    repository_root: Path, descriptor: StationSpatialDescriptor
) -> tuple[list[dict[str, Any]], dict[str, Any]]:
    path = _verify_frozen_file(
        repository_root, descriptor.administrative_boundary_source, "AdministrativeBoundaryZip"
    )
    frame = descriptor.frame
    with zipfile.ZipFile(path, "r") as archive:
        entries = {Path(entry.filename).suffix.lower(): entry for entry in archive.infolist()}
        require({".shp", ".shx", ".dbf", ".cpg", ".prj"}.issubset(entries), "AdministrativeBoundaryEntriesMissing")
        require(archive.read(entries[".cpg"]).decode("ascii").strip() in {"UTF-8", "UTF8", "utf_8"}, "AdministrativeBoundaryEncodingUnsupported")
        projection = archive.read(entries[".prj"]).decode("ascii")
        require(
            "Korea_2000_Korea_Central_Belt" in projection
            and 'False_Easting",200000' in projection
            and 'False_Northing",500000' in projection
            and 'Central_Meridian",127' in projection,
            "AdministrativeBoundaryCrsUnsupported",
        )
        reader = shapefile.Reader(
            shp=io.BytesIO(archive.read(entries[".shp"])),
            shx=io.BytesIO(archive.read(entries[".shx"])),
            dbf=io.BytesIO(archive.read(entries[".dbf"])),
            encoding="utf-8",
            encodingErrors="strict",
        )
        source_record_count = len(reader)
        field_names = [field[0] for field in reader.fields[1:]]
        require({"ADSTRD_CD", "ADSTRD_NM"}.issubset(field_names), "AdministrativeBoundaryDbfFieldsMissing")
        to_wgs = Transformer.from_crs(5181, 4326, always_xy=True)
        areas: list[dict[str, Any]] = []
        for shape_record in reader.iterShapeRecords():
            properties = dict(zip(field_names, list(shape_record.record)))
            source_geometry = shape(shape_record.shape.__geo_interface__)
            wgs_geometry = transform(to_wgs.transform, source_geometry)
            local_geometry = transform(lambda x, y, z=None: frame.wgs84_to_local(x, y), wgs_geometry)
            clipped = _polygonal(local_geometry.intersection(frame.clip_polygon))
            if clipped.is_empty:
                continue
            code = str(properties["ADSTRD_CD"]).strip()
            name = str(properties["ADSTRD_NM"]).strip()
            require(len(code) == 8 and code.isdigit(), "AdministrativeBoundaryCodeInvalid")
            require(bool(name), "AdministrativeBoundaryNameMissing")
            area = clipped.area
            areas.append(
                {
                    "administrativeAreaStableId": f"region:kr:hjd:{code}00",
                    "displayName": name,
                    "sourceVintage": "seoul-oa22160:updated:20260611:retrieved:20260912",
                    "containsStation": local_geometry.covers(Point(0.0, 0.0)),
                    "coverageSquareMeters": round(area, 3),
                    "coverageRatio": round(area / frame.clip_polygon.area, 8),
                    "boundaryClipped": not frame.clip_polygon.covers(local_geometry),
                    "parts": geometry_to_parts(clipped),
                    "_geometry": clipped,
                }
            )
        reader.close()
    areas.sort(key=lambda item: item["administrativeAreaStableId"])
    require(bool(areas), "AdministrativeBoundaryCoverageMissing")
    covered = unary_union([area["_geometry"] for area in areas])
    overlap_area = sum(area["_geometry"].area for area in areas) - covered.area
    missing_area = frame.clip_polygon.difference(covered).area
    require(overlap_area <= 2.0, "AdministrativeBoundaryOverlapTooLarge")
    require(missing_area <= 2.0, "AdministrativeBoundaryGapTooLarge")
    require(sum(1 for area in areas if area["containsStation"]) == 1, "StationAdministrativeAreaAmbiguous")
    return areas, {
        "sourceRecordCount": source_record_count,
        "intersectingAreaCount": len(areas),
        "coveredSquareMeters": round(covered.area, 3),
        "overlapSquareMeters": round(overlap_area, 6),
        "missingSquareMeters": round(missing_area, 6),
    }


def _feature_fragment(feature: dict[str, Any], clip: Any, geometry_kind: str) -> dict[str, Any] | None:
    clipped = feature["_geometry"].intersection(clip)
    if clipped.is_empty:
        return None
    if geometry_kind == "polygon":
        clipped = _polygonal(clipped)
        if clipped.is_empty:
            return None
        parts = geometry_to_parts(clipped)
    else:
        if not _line_members(clipped):
            return None
        parts = line_to_parts(clipped)
    return {"id": feature["id"], "parts": parts}


def build_tiles(
    descriptor: StationSpatialDescriptor,
    buildings: Sequence[dict[str, Any]],
    roads: Sequence[dict[str, Any]],
    surfaces: Sequence[dict[str, Any]],
    coverage_cells: Sequence[dict[str, Any]],
) -> list[dict[str, Any]]:
    frame = descriptor.frame
    tiles: list[dict[str, Any]] = []
    for index_x in (-1, 0):
        for index_z in (-1, 0):
            min_x = index_x * descriptor.tile_size_meters
            min_z = index_z * descriptor.tile_size_meters
            tile_geometry = box(
                min_x,
                min_z,
                min_x + descriptor.tile_size_meters,
                min_z + descriptor.tile_size_meters,
            ).intersection(frame.clip_polygon)
            building_fragments = [
                fragment
                for feature in buildings
                if (fragment := _feature_fragment(feature, tile_geometry, "polygon")) is not None
            ]
            road_fragments = [
                fragment
                for feature in roads
                if (fragment := _feature_fragment(feature, tile_geometry, "line")) is not None
            ]
            surface_fragments = [
                fragment
                for feature in surfaces
                if (fragment := _feature_fragment(feature, tile_geometry, "polygon")) is not None
            ]
            tile = {
                "tileStableId": (
                    f"tile:{descriptor.transit_station_stable_id}:{descriptor.tile_size_meters}m:"
                    f"x{index_x}:z{index_z}"
                ),
                "tileIndexX": index_x,
                "tileIndexZ": index_z,
                "bounds": _bounds(tile_geometry),
                "tileHash": "",
                "buildingCount": len(building_fragments),
                "roadCount": len(road_fragments),
                "surfaceCount": len(surface_fragments),
                "buildingFragments": building_fragments,
                "roadFragments": road_fragments,
                "surfaceFragments": surface_fragments,
                "coverageCellIds": sorted(
                    cell["id"]
                    for cell in coverage_cells
                    if box(
                        cell["bounds"]["minX"],
                        cell["bounds"]["minZ"],
                        cell["bounds"]["maxX"],
                        cell["bounds"]["maxZ"],
                    ).intersection(tile_geometry).area > 0.01
                ),
            }
            tile["tileHash"] = hash_with_empty_field(tile, "tileHash")
            tiles.append(tile)
    tiles.sort(key=lambda item: (item["tileIndexX"], item["tileIndexZ"]))
    return tiles


def build_coverage(
    descriptor: StationSpatialDescriptor,
    buildings: Sequence[dict[str, Any]],
    surfaces: Sequence[dict[str, Any]],
    incomplete_relations: Sequence[dict[str, Any]],
) -> dict[str, Any]:
    building_union = unary_union([building["_geometry"] for building in buildings]) if buildings else GeometryCollection()
    open_union = unary_union(
        [surface["_geometry"] for surface in surfaces if surface["kind"] in QUALIFIED_OPEN_SURFACE_KINDS]
    ) if surfaces else GeometryCollection()
    surface_union = unary_union([surface["_geometry"] for surface in surfaces]) if surfaces else GeometryCollection()
    cells: list[dict[str, Any]] = []
    classification_counts = {
        "ConfirmedBuilt": 0,
        "ConfirmedOpen": 0,
        "IncompleteSurfaceEvidence": 0,
        "MissingCoverage": 0,
    }
    for index_x in range(10):
        for index_z in range(10):
            min_x = -500.0 + index_x * 100.0
            min_z = -500.0 + index_z * 100.0
            cell_geometry = box(min_x, min_z, min_x + 100.0, min_z + 100.0)
            cell_area = cell_geometry.area
            building_area = building_union.intersection(cell_geometry).area
            qualified_open = open_union.difference(building_union).intersection(cell_geometry).area
            any_surface = surface_union.difference(building_union).intersection(cell_geometry).area
            affected = sorted(
                item["id"]
                for item in incomplete_relations
                if not item["_availableGeometry"].is_empty
                and item["_availableGeometry"].intersection(cell_geometry).length > 0.001
            )
            building_ratio = building_area / cell_area
            open_ratio = qualified_open / cell_area
            surface_ratio = any_surface / cell_area
            # 기존 coverage 계약처럼 건물·검증된 열린 면·미확인 면을 1로 분할한다.
            # 다른 OSM 토지표현은 surfaceCoverageRatio로 별도 노출하되 미확인을
            # 사실로 승격하지 않는다.
            unknown_ratio = max(0.0, 1.0 - min(1.0, (building_area + qualified_open) / cell_area))
            if building_ratio >= BUILDING_COVERAGE_THRESHOLD:
                classification = "ConfirmedBuilt"
            elif affected:
                classification = "IncompleteSurfaceEvidence"
            elif open_ratio >= OPEN_COVERAGE_THRESHOLD:
                classification = "ConfirmedOpen"
            else:
                classification = "MissingCoverage"
            classification_counts[classification] += 1
            cells.append(
                {
                    "id": f"coverage-cell:x{index_x}:z{index_z}",
                    "xIndex": index_x,
                    "zIndex": index_z,
                    "bounds": _bounds(cell_geometry),
                    "buildingCoverageRatio": round(building_ratio, 6),
                    "openCoverageRatio": round(open_ratio, 6),
                    "surfaceCoverageRatio": round(surface_ratio, 6),
                    "unknownCoverageRatio": round(unknown_ratio, 6),
                    "classification": classification,
                    "evidenceMix": sorted(
                        (["VWorldBuildingFootprint"] if building_area > 0.01 else [])
                        + (["OpenStreetMapSurface"] if any_surface > 0.01 else [])
                        + (["IncompleteOsmRelation"] if affected else [])
                    ),
                    "boundaryClipped": index_x in {0, 9} or index_z in {0, 9},
                    "incompleteSourceFeatureIds": affected,
                }
            )
    return {
        "cellSizeMeters": 100,
        "columns": 10,
        "rows": 10,
        "buildingCoverageThreshold": BUILDING_COVERAGE_THRESHOLD,
        "qualifiedOpenCoverageThreshold": OPEN_COVERAGE_THRESHOLD,
        "cells": cells,
        "summary": {
            "classificationCounts": classification_counts,
            "buildingCoverageSquareMeters": round(building_union.intersection(descriptor.frame.clip_polygon).area, 3),
            "surfaceCoverageSquareMeters": round(surface_union.intersection(descriptor.frame.clip_polygon).area, 3),
            "qualifiedOpenCoverageSquareMeters": round(open_union.difference(building_union).intersection(descriptor.frame.clip_polygon).area, 3),
            "incompleteRelationCount": len(incomplete_relations),
        },
    }


def _source_receipts(
    descriptor: StationSpatialDescriptor,
    building_statistics: dict[str, Any],
    nodelink_statistics: dict[str, Any],
    osm_metadata: dict[str, Any],
) -> list[dict[str, Any]]:
    osm_receipt = osm_metadata["receipt"]
    return [
        {
            "sourceId": "kric:urban-rail-stations",
            "datasetId": "data-go-kr-15093755",
            "sourceUrl": "https://www.data.go.kr/data/15093755/fileData.do",
            "providerSourceUrl": "https://data.kric.go.kr/rips/M_01_01/detail.do?id=32",
            "sourceRevision": "file:20260630;target-row-reference:2024-12-31",
            "rawHash": "CDF1D84A7E5C898B2AACD622783BA8BA9AF35C40BEE0561DC97D55CE8E063F94",
            "originalCrs": "EPSG:4326",
            "licenseCode": "NoUseRestrictionObserved",
            "reviewStatus": STATUS,
            "distributionApproved": False,
            "limitations": [
                "StationAnchorDescriptorEvidenceOnly",
                "SourceReportedPointNotPlatformFieldSurvey",
                "RawStationWorkbookNotReparsedByThisSpatialBuilder",
            ],
        },
        {
            "sourceId": "vworld:gis-building-integrated-information",
            "datasetId": "data-go-kr-15083092",
            "sourceUrl": "https://www.data.go.kr/data/15083092/fileData.do",
            "sourceRevision": "AL_D010:Seoul:20260809",
            "rawHash": descriptor.building_source.sha256,
            "rawLength": descriptor.building_source.byte_length,
            "originalCrs": "EPSG:5186",
            "licenseCode": "RightsConflictUnresolved",
            "licenseUrl": "https://creativecommons.org/licenses/by-nc-nd/2.0/kr/",
            "reviewStatus": STATUS,
            "distributionApproved": False,
            "limitations": ["PresentationOnly", "NotOfficialCompletenessClaim", "HeightFallbackIsSymbolic"],
            "statistics": building_statistics,
        },
        {
            "sourceId": "data-go-kr:standard-nodelink",
            "datasetId": "data-go-kr-15025526",
            "sourceUrl": "https://www.data.go.kr/data/15025526/fileData.do",
            "sourceRevision": "2026-08-12",
            "rawHash": descriptor.nodelink_source.sha256,
            "rawLength": descriptor.nodelink_source.byte_length,
            "originalCrs": "SourceWktInArchive",
            "licenseCode": "NoUseRestrictionObserved",
            "reviewStatus": STATUS,
            "distributionApproved": False,
            "limitations": [
                "DirectedLinkNotPhysicalRoad",
                "LaneCountNotRoadWidth",
                "NoTraversalAuthority",
                "NoSurveyAccuracyGuarantee",
            ],
            "statistics": nodelink_statistics,
        },
        {
            "sourceId": "seoul-open-data:administrative-dong-boundary",
            "datasetId": "OA-22160",
            "sourceUrl": "https://data.seoul.go.kr/dataList/OA-22160/S/1/datasetView.do",
            "sourceRevision": "seoul-oa22160:updated:20260611:retrieved:20260912",
            "rawHash": descriptor.administrative_boundary_source.sha256,
            "rawLength": descriptor.administrative_boundary_source.byte_length,
            "originalCrs": "EPSG:5181",
            "licenseCode": "KOGL-Type1",
            "reviewStatus": STATUS,
            "distributionApproved": False,
            "limitations": ["AdministrativeBoundaryObservation", "NoLegalDongBoundary"],
        },
        {
            "sourceId": descriptor.osm_source_id,
            "datasetId": "osm-api-0.6-map-bbox",
            "sourceUrl": descriptor.osm_source_url,
            "sourceRevision": osm_receipt["fetchedAtUtc"],
            "rawHash": descriptor.osm_source.sha256,
            "rawLength": descriptor.osm_source.byte_length,
            "originalCrs": "EPSG:4326",
            "licenseCode": "ODbL-1.0",
            "licenseUrl": osm_receipt["license"]["licenseUrl"],
            "attribution": osm_receipt["license"]["attribution"],
            "reviewStatus": STATUS,
            "distributionApproved": False,
            "limitations": ["PresentationOnly", "BboxRelationMembersMayBeIncomplete", "NoTraversalAuthority"],
            "statistics": osm_metadata["counts"],
        },
    ]


def _public_feature(feature: dict[str, Any]) -> dict[str, Any]:
    return {key: value for key, value in feature.items() if not key.startswith("_")}


def build_snapshot(
    repository_root: Path, descriptor: StationSpatialDescriptor
) -> tuple[dict[str, Any], dict[str, Any]]:
    descriptor.validate()
    buildings, building_statistics = read_official_buildings(repository_root, descriptor)
    roads, nodelink_statistics = read_nodelink_roads(repository_root, descriptor)
    surfaces, incomplete_relations, osm_metadata = read_osm_surfaces(repository_root, descriptor)
    administrative_areas, administrative_statistics = read_administrative_areas(repository_root, descriptor)
    coverage = build_coverage(descriptor, buildings, surfaces, incomplete_relations)
    tiles = build_tiles(descriptor, buildings, roads, surfaces, coverage["cells"])

    missing: list[dict[str, Any]] = [
        {
            "code": "BuildingRightsConflictUnresolved",
            "layer": "Buildings",
            "detail": "Provider display and linked license conflict; no redistribution or game application approval.",
            "affectedCellIds": [],
        },
        {
            "code": "LegalDongBoundaryCoverageUnverified",
            "layer": "LegalAdministrativeBoundary",
            "detail": "Building rows retain legal-dong codes, but no frozen official legal-dong polygon is available.",
            "affectedCellIds": [cell["id"] for cell in coverage["cells"]],
        },
        {
            "code": "RoadWidthUnavailable",
            "layer": "Roads",
            "detail": "NodeLink lane count is not physical road width.",
            "affectedCellIds": [],
        },
        {
            "code": "TraversalAuthorityUnavailable",
            "layer": "RoadsAndSurfaces",
            "detail": "Presentation geometry does not establish walkable or drivable routes.",
            "affectedCellIds": [cell["id"] for cell in coverage["cells"]],
        },
    ]
    missing_cells = [cell["id"] for cell in coverage["cells"] if cell["classification"] == "MissingCoverage"]
    if missing_cells:
        missing.append(
            {
                "code": "SurfaceCoverageIncomplete",
                "layer": "Surfaces",
                "detail": "No qualifying complete surface polygon establishes these cells.",
                "affectedCellIds": missing_cells,
            }
        )
    for relation in incomplete_relations:
        affected = [
            cell["id"]
            for cell in coverage["cells"]
            if relation["id"] in cell["incompleteSourceFeatureIds"]
        ]
        missing.append(
            {
                "code": "OsmRelationMembersMissing",
                "layer": "Surfaces",
                "sourceFeatureId": relation["id"],
                "detail": relation["reason"],
                "missingWayIds": relation["missingWayIds"],
                "missingNodeIds": relation["missingNodeIds"],
                "affectedCellIds": affected,
            }
        )
    missing.sort(key=lambda item: (item["code"], item.get("sourceFeatureId", "")))

    frame = descriptor.frame
    document: dict[str, Any] = {
        "schemaVersion": SCHEMA_VERSION,
        "revision": descriptor.revision,
        "contentHash": "",
        "status": STATUS,
        "transitStationStableId": descriptor.transit_station_stable_id,
        "stationName": descriptor.station_name,
        "service": {"lineCode": descriptor.line_code, "stationCode": descriptor.station_code},
        "presentationOnly": True,
        "distributionApproved": False,
        "gameplayReady": False,
        "traversalReady": False,
        "coordinateMethod": COORDINATE_METHOD,
        "coordinateFrame": {
            "originLatitude": descriptor.latitude,
            "originLongitude": descriptor.longitude,
            "offsetX": 0.0,
            "offsetZ": 0.0,
            "metersPerUnit": 1.0,
            "halfExtentMeters": 500.0,
        },
        "window": {
            "widthMeters": descriptor.width_meters,
            "depthMeters": descriptor.depth_meters,
            "bounds": {
                "minX": frame.min_x,
                "minZ": frame.min_z,
                "maxX": frame.max_x,
                "maxZ": frame.max_z,
            },
            "wgs84Bounds": _wgs84_window(frame),
        },
        "sourceReceipts": _source_receipts(
            descriptor, building_statistics, nodelink_statistics, osm_metadata
        ),
        "buildings": [_public_feature(feature) for feature in buildings],
        "roads": [_public_feature(feature) for feature in roads],
        "surfaces": [_public_feature(feature) for feature in surfaces],
        "administrativeAreas": [_public_feature(area) for area in administrative_areas],
        "tiles": tiles,
        "coverage": coverage,
        "missingCoverage": missing,
    }
    apply_content_hash(document)
    statistics = {
        "building": building_statistics,
        "road": nodelink_statistics,
        "surface": osm_metadata["counts"],
        "administrative": administrative_statistics,
        "tileCount": len(tiles),
        "coverage": coverage["summary"],
        "contentHash": document["contentHash"],
    }
    return document, statistics


def validate_snapshot_invariants(document: dict[str, Any]) -> dict[str, Any]:
    verify_content_hash(document)
    require(document.get("schemaVersion") == SCHEMA_VERSION, "SnapshotSchemaMismatch")
    require(document.get("status") == STATUS, "SnapshotStatusMismatch")
    require(document.get("presentationOnly") is True, "SnapshotPresentationBoundaryMissing")
    require(document.get("distributionApproved") is False, "SnapshotDistributionApproved")
    require(document.get("gameplayReady") is False, "SnapshotGameplayReady")
    require(document.get("traversalReady") is False, "SnapshotTraversalReady")
    frame_value = document.get("coordinateFrame", {})
    frame = EnuFrame(
        float(frame_value["originLatitude"]),
        float(frame_value["originLongitude"]),
        float(frame_value["offsetX"]),
        float(frame_value["offsetZ"]),
        float(frame_value["halfExtentMeters"]),
    )
    for group, geometry_kind in (("buildings", "polygon"), ("surfaces", "polygon"), ("roads", "line")):
        values = document.get(group, [])
        require(isinstance(values, list), f"Snapshot{group}Invalid")
        identifiers = [value.get("id") for value in values]
        require(all(isinstance(value, str) and value for value in identifiers), f"Snapshot{group}IdMissing")
        require(len(identifiers) == len(set(identifiers)), f"Snapshot{group}IdDuplicate")
        for value in values:
            geometry = parts_to_geometry(value["parts"]) if geometry_kind == "polygon" else line_parts_to_geometry(value["parts"])
            require(frame.clip_polygon.buffer(0.01).covers(geometry), f"Snapshot{group}OutsideWindow:{value['id']}")
    coverage = document.get("coverage", {})
    cells = coverage.get("cells", [])
    require(len(cells) == 100, "SnapshotCoverageCellCountMismatch")
    allowed = {"ConfirmedBuilt", "ConfirmedOpen", "IncompleteSurfaceEvidence", "MissingCoverage"}
    require(all(cell.get("classification") in allowed for cell in cells), "SnapshotCoverageClassificationInvalid")
    tiles = document.get("tiles", [])
    require(len(tiles) == 4, "SnapshotTileCountMismatch")
    require(
        {(tile["tileIndexX"], tile["tileIndexZ"]) for tile in tiles}
        == {(-1, -1), (-1, 0), (0, -1), (0, 0)},
        "SnapshotTileIndicesInvalid",
    )
    for tile in tiles:
        require(tile.get("tileHash") == hash_with_empty_field(tile, "tileHash"), f"SnapshotTileHashMismatch:{tile.get('tileStableId')}")
    areas = document.get("administrativeAreas", [])
    require(len(areas) >= 1 and sum(1 for area in areas if area.get("containsStation")) == 1, "SnapshotAdministrativeCoverageInvalid")
    source_ids = {receipt.get("sourceId") for receipt in document.get("sourceReceipts", [])}
    require(
        {
            "kric:urban-rail-stations",
            "vworld:gis-building-integrated-information",
            "data-go-kr:standard-nodelink",
            "seoul-open-data:administrative-dong-boundary",
        }.issubset(source_ids),
        "SnapshotSourceReceiptsMissing",
    )
    osm_source_ids = {
        value for value in source_ids
        if isinstance(value, str) and value.startswith("openstreetmap:")
    }
    require(len(osm_source_ids) == 1, "SnapshotOsmSourceReceiptInvalid")
    require(
        all(surface.get("sourceId") in osm_source_ids for surface in document.get("surfaces", [])),
        "SnapshotSurfaceSourceReceiptMismatch",
    )
    missing_codes = {item.get("code") for item in document.get("missingCoverage", [])}
    require(
        {"BuildingRightsConflictUnresolved", "LegalDongBoundaryCoverageUnverified", "RoadWidthUnavailable", "TraversalAuthorityUnavailable"}.issubset(missing_codes),
        "SnapshotRequiredMissingCoverageAbsent",
    )
    return {
        "buildings": len(document["buildings"]),
        "roads": len(document["roads"]),
        "surfaces": len(document["surfaces"]),
        "administrativeAreas": len(areas),
        "tiles": len(tiles),
        "coverageCells": len(cells),
    }


def audit_snapshot(
    repository_root: Path,
    descriptor: StationSpatialDescriptor,
    document: dict[str, Any],
) -> dict[str, Any]:
    observed = validate_snapshot_invariants(document)
    rebuilt, statistics = build_snapshot(repository_root, descriptor)
    require(
        canonical_json_bytes(rebuilt) == canonical_json_bytes(document),
        "IndependentRebuildMismatch",
    )
    return {
        "schemaVersion": AUDIT_SCHEMA_VERSION,
        "status": "PassedWithMissingCoverage" if document["missingCoverage"] else "Passed",
        "snapshotRevision": document["revision"],
        "snapshotContentHash": document["contentHash"],
        "checks": [
            "Frozen source length and SHA-256",
            "Descriptor and station-centered one-kilometer frame",
            "All-layer identifier uniqueness and window containment",
            "Tile hashes and four station-local 500-meter tiles",
            "One-hundred-cell coverage classifications",
            "Private non-distribution non-gameplay non-traversal boundary",
            "Full deterministic source rebuild",
        ],
        "observed": observed,
        "statistics": statistics,
        "missingCoverageCodes": sorted({item["code"] for item in document["missingCoverage"]}),
        "auditHash": "",
    }


def finalize_audit(audit: dict[str, Any]) -> dict[str, Any]:
    apply_content_hash(audit, "auditHash")
    return audit


def run_self_tests() -> dict[str, Any]:
    checks = 0

    def check(condition: bool, code: str) -> None:
        nonlocal checks
        require(condition, f"SelfTest:{code}")
        checks += 1

    sample = {"b": 2, "a": 1, "contentHash": ""}
    apply_content_hash(sample)
    check(len(sample["contentHash"]) == 64, "ContentHashLength")
    verify_content_hash(sample)
    checks += 1
    line = line_parts_to_geometry([{"points": [{"x": -1.0, "z": 0.0}, {"x": 1.0, "z": 0.0}]}])
    check(round(line.length, 6) == 2.0, "LineRoundTrip")
    polygon_parts = geometry_to_parts(box(-1.0, -1.0, 1.0, 1.0))
    check(round(parts_to_geometry(polygon_parts).area, 6) == 4.0, "PolygonRoundTrip")
    frame = EnuFrame(37.588671, 127.087503, 0.0, 0.0, 500.0)
    check(frame.clip_polygon.area == 1_000_000.0, "OneKilometerArea")
    window = _wgs84_window(frame)
    check(window["minLongitude"] < 127.087503 < window["maxLongitude"], "WindowLongitude")
    check(window["minLatitude"] < 37.588671 < window["maxLatitude"], "WindowLatitude")
    check(surface_kind({"leisure": "park"}) == "park", "SurfaceKind")
    check(surface_kind({"building": "yes"}) is None, "BuildingNotSurface")
    invalid_failed = False
    try:
        FrozenSource("x", "bad", 1).validate("Synthetic")
    except SpatialPresentationError:
        invalid_failed = True
    check(invalid_failed, "InvalidFrozenHashRejected")
    return {"passed": True, "checks": checks}
