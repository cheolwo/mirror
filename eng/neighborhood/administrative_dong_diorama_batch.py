#!/usr/bin/env python3
"""동북서울 30개 행정동의 비공개 디오라마 입력 모판을 결정적으로 생성한다.

OA-22160의 동결 파일은 2023년 경계의 역사적 부트스트랩일 뿐 현재 경계
정본이 아니다. 이 도구가 만드는 자료는 Unity 표현 후보이며 배포, 이동,
게임플레이 또는 업무 상태의 권위를 만들지 않는다.
"""

from __future__ import annotations

import argparse
import copy
import hashlib
import io
import json
import math
import os
import struct
import sys
import zipfile
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Iterable, Iterator, Sequence


SCRIPT_PATH = Path(__file__).resolve()
DEFAULT_REPOSITORY_ROOT = SCRIPT_PATH.parents[2]
DEFAULT_SCOPE_RELATIVE = Path(
    "eng/world-seedbeds/administrative-dong-dioramas/northeast-seoul-rider.r2.json"
)

SCOPE_SCHEMA_VERSION = "administrative-dong-diorama-generation-scope.v1"
OUTPUT_SCOPE_SCHEMA_VERSION = "administrative-dong-diorama-batch-scope.v1"
MODULE_SCHEMA_VERSION = "administrative-dong-diorama-batch-input.v1"
AUDIT_SCHEMA_VERSION = "administrative-dong-diorama-batch-audit.v1"
SCOPE_STABLE_ID = "scope:administrative-dong-diorama:northeast-seoul-rider:r2"
REVISION = "northeast-seoul-administrative-dong-dioramas.r2"
GENERATED_AT_UTC = "2026-09-14T00:00:00Z"
SOURCE_VINTAGE = (
    "oa22160-file-20231031+al-d010-20260809+"
    "nodelink-20260812+mois-jscode-20260301+projection-r2"
)
DESIRED_READINESS = "WaitingForAdministrativeBoundary"
DIAGNOSTIC_CODES = [
    "CurrentAuthoritativeBoundaryUnavailable",
    "HistoricalBoundaryBootstrapOnly",
    "DistributionNotApproved",
    "TraversalNotReady",
    "GameplayNotReady",
    "SupersedesGeometryUnsafeR1Candidate",
]
MAXIMUM_ROAD_SEGMENT_METERS = 100.0
INTERNAL_ROAD_SEGMENT_METERS = 99.99
ROAD_BOUNDARY_NUMERIC_TOLERANCE_METERS = 0.000001
SYMBOLIC_BUILDING_HEIGHT_METERS = 4.0

EXPECTED_SOURCES = {
    "OA-22160": {
        "sourceId": "source:seoul-open-data",
        "sourceRevision": "seoul-oa22160:file-modified:20231031:retrieved:20260912",
        "repositoryRelativePath": "artifacts/local/public-data/admin-dong/20260912-seoul-oa22160/seoul-administrative-dong-boundary.zip",
        "sha256": "969F7033BD3609A5FD586790F5B2CFEDC638D7647EF45C78F9C75E1DABF79F68",
        "byteLength": 1_676_539,
        "recordCount": 425,
        "licenseCode": "KOGL-Type1",
        "limitationCode": "HistoricalBootstrapBoundary;EPSG5181;CurrentAuthoritativeBoundaryUnavailable;DistributionNotApproved",
    },
    "data-go-kr-15083092": {
        "sourceId": "source:data-go-kr",
        "sourceRevision": "AL_D010:Seoul:20260809",
        "repositoryRelativePath": "artifacts/local/neighborhood-source-acquisition/AL_D010_11_20260809.zip",
        "sha256": "674C5A9583996DD6B8946525EDAD8197BE79A634F1DB00D39E2B3133D0D2A755",
        "byteLength": 135_675_376,
        "recordCount": 695_761,
        "licenseCode": "RightsConflictUnresolved",
        "limitationCode": "OfficialGisBuildingGeometry;EPSG5186;LargestExteriorOnly;DistributionNotApproved",
    },
    "data-go-kr-15025526": {
        "sourceId": "source:data-go-kr",
        "sourceRevision": "NodeLink:release:20260812",
        "repositoryRelativePath": "artifacts/local/public-data/sagajeong-nodelink-20260912-r1/nodelink.zip",
        "extractedRepositoryRelativeDirectory": "artifacts/local/public-data/sagajeong-nodelink-20260912-r1/source-link",
        "sha256": "5BBF5A01D677B6DCB941CC5954FCC256D6DED60D96F95336A23FC2B53B39D4E4",
        "byteLength": 269_611_477,
        "recordCount": 1_557_364,
        "licenseCode": "UseScopeUnrestricted",
        "limitationCode": "DirectedLinkPresentationOnly;NoWidthInference;NoTraversalAuthority;DistributionNotApproved",
    },
}

EXPECTED_AUDIT_COUNTS = {
    "assignedBuildings": 61_897,
    "representativePointOutside": 13,
    "malformedBuildingQuarantine": 11,
    "roundedFootprintQuarantine": 3,
    "ambiguousBuildings": 0,
    "interiorRingBuildings": 16,
    "interiorRings": 18,
    "multipartBuildings": 0,
    "v1ContractAssignmentAnchorOverrides": 2,
    "directedNodeLinks": 4_209,
    "administrativeDongLinkOccurrences": 4_620,
    "crossBoundaryDirectedLinks": 397,
    "exportedRoadSegments": 11_769,
    "exactRoadBoundaryContainmentFailures": 454,
}

EXPECTED_ADMINISTRATIVE_AREAS = {
    "1121574000": ("서울특별시 광진구 중곡제1동", "1121510100"),
    "1121575000": ("서울특별시 광진구 중곡제2동", "1121510100"),
    "1121576000": ("서울특별시 광진구 중곡제3동", "1121510100"),
    "1121577000": ("서울특별시 광진구 중곡제4동", "1121510100"),
    "1123056000": ("서울특별시 동대문구 전농제1동", "1123010400"),
    "1123057000": ("서울특별시 동대문구 전농제2동", "1123010400"),
    "1123060000": ("서울특별시 동대문구 답십리제1동", "1123010500"),
    "1123061000": ("서울특별시 동대문구 답십리제2동", "1123010500"),
    "1123065000": ("서울특별시 동대문구 장안제1동", "1123010600"),
    "1123066000": ("서울특별시 동대문구 장안제2동", "1123010600"),
    "1123072000": ("서울특별시 동대문구 휘경제1동", "1123010900"),
    "1123073000": ("서울특별시 동대문구 휘경제2동", "1123010900"),
    "1123074000": ("서울특별시 동대문구 이문제1동", "1123011000"),
    "1123075000": ("서울특별시 동대문구 이문제2동", "1123011000"),
    "1126052000": ("서울특별시 중랑구 면목제2동", "1126010100"),
    "1126054000": ("서울특별시 중랑구 면목제4동", "1126010100"),
    "1126055000": ("서울특별시 중랑구 면목제5동", "1126010100"),
    "1126056500": ("서울특별시 중랑구 면목본동", "1126010100"),
    "1126057000": ("서울특별시 중랑구 면목제7동", "1126010100"),
    "1126057500": ("서울특별시 중랑구 면목제3.8동", "1126010100"),
    "1126058000": ("서울특별시 중랑구 상봉제1동", "1126010200"),
    "1126059000": ("서울특별시 중랑구 상봉제2동", "1126010200"),
    "1126060000": ("서울특별시 중랑구 중화제1동", "1126010300"),
    "1126061000": ("서울특별시 중랑구 중화제2동", "1126010300"),
    "1126062000": ("서울특별시 중랑구 묵제1동", "1126010400"),
    "1126063000": ("서울특별시 중랑구 묵제2동", "1126010400"),
    "1126065500": ("서울특별시 중랑구 망우본동", "1126010500"),
    "1126066000": ("서울특별시 중랑구 망우제3동", "1126010500"),
    "1126068000": ("서울특별시 중랑구 신내1동", "1126010600"),
    "1126069000": ("서울특별시 중랑구 신내2동", "1126010600"),
}


class BatchGenerationError(RuntimeError):
    """CLI와 소비 도구가 재사용할 수 있는 안정적인 오류 코드."""


def require(condition: bool, code: str) -> None:
    if not condition:
        raise BatchGenerationError(code)


def canonical_json_bytes(value: Any) -> bytes:
    return json.dumps(
        value,
        ensure_ascii=False,
        allow_nan=False,
        sort_keys=True,
        separators=(",", ":"),
    ).encode("utf-8")


def content_hash(document: dict[str, Any]) -> str:
    candidate = copy.deepcopy(document)
    candidate["contentHashSha256"] = ""
    return hashlib.sha256(canonical_json_bytes(candidate)).hexdigest().upper()


def apply_content_hash(document: dict[str, Any]) -> None:
    document["contentHashSha256"] = ""
    document["contentHashSha256"] = content_hash(document)


def pretty_json_bytes(value: Any) -> bytes:
    return (
        json.dumps(value, ensure_ascii=False, allow_nan=False, indent=2) + "\n"
    ).encode("utf-8")


def sha256_bytes(value: bytes) -> str:
    return hashlib.sha256(value).hexdigest().upper()


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest().upper()


def write_json_atomic(path: Path, value: dict[str, Any]) -> bool:
    payload = pretty_json_bytes(value)
    if path.is_file() and path.read_bytes() == payload:
        return False
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(path.name + ".tmp")
    temporary.write_bytes(payload)
    os.replace(temporary, path)
    return True


def load_json(path: Path) -> dict[str, Any]:
    try:
        result = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        raise BatchGenerationError(f"JsonReadFailed:{path.name}") from exc
    require(isinstance(result, dict), f"JsonRootInvalid:{path.name}")
    return result


def repository_path(root: Path, relative: str | Path) -> Path:
    root = root.resolve()
    candidate = (root / Path(relative)).resolve()
    try:
        candidate.relative_to(root)
    except ValueError as exc:
        raise BatchGenerationError("PathOutsideRepository") from exc
    return candidate


def verify_frozen_file(root: Path, source: dict[str, Any]) -> Path:
    path = repository_path(root, source["repositoryRelativePath"])
    require(path.is_file() and not path.is_symlink(), f"SourceMissing:{source['datasetId']}")
    require(path.stat().st_size == source["byteLength"], f"SourceLengthMismatch:{source['datasetId']}")
    require(sha256_file(path) == source["contentHashSha256"], f"SourceHashMismatch:{source['datasetId']}")
    return path


@dataclass(frozen=True)
class Dependencies:
    shapefile: Any
    CRS: Any
    Transformer: Any
    make_valid: Any
    GeometryCollection: Any
    LineString: Any
    MultiLineString: Any
    MultiPolygon: Any
    Point: Any
    Polygon: Any
    orient: Any
    shape: Any
    transform: Any
    unary_union: Any
    canonical_ring: Any
    dbf_text: Any
    parse_integer: Any
    parse_number: Any
    parse_shp_polygon: Any
    polygonal: Any
    read_dbf_header: Any
    read_exact: Any
    read_shp_header: Any
    rings_to_geometry: Any
    transform_source_rings: Any
    zip_entry: Any
    pyshp_version: str
    pyproj_version: str
    shapely_version: str
    geos_version: str
    proj_version: str
    epsg_database_version: str


def load_dependencies(root: Path) -> Dependencies:
    runtime = root / "artifacts/local/public-data/gis-runtime-r1"
    neighborhood = root / "eng/neighborhood"
    for path in (runtime, neighborhood):
        if str(path) not in sys.path:
            sys.path.insert(0, str(path))
    try:
        import shapefile
        import pyproj
        import shapely
        from pyproj import CRS, Transformer
        from shapely import make_valid
        from shapely.geometry import (
            GeometryCollection,
            LineString,
            MultiLineString,
            MultiPolygon,
            Point,
            Polygon,
            shape,
        )
        from shapely.geometry.polygon import orient
        from shapely.ops import transform, unary_union
        from sagajeong_spatial_presentation import (
            _canonical_ring,
            _dbf_text,
            _parse_integer,
            _parse_number,
            _parse_shp_polygon,
            _polygonal,
            _read_dbf_header,
            _read_exact,
            _read_shp_header,
            rings_to_geometry,
            _transform_source_rings,
            _zip_entry,
        )
    except (ModuleNotFoundError, ImportError) as exc:
        raise BatchGenerationError("SpatialGenerationDependencyMissing") from exc
    return Dependencies(
        shapefile,
        CRS,
        Transformer,
        make_valid,
        GeometryCollection,
        LineString,
        MultiLineString,
        MultiPolygon,
        Point,
        Polygon,
        orient,
        shape,
        transform,
        unary_union,
        _canonical_ring,
        _dbf_text,
        _parse_integer,
        _parse_number,
        _parse_shp_polygon,
        _polygonal,
        _read_dbf_header,
        _read_exact,
        _read_shp_header,
        rings_to_geometry,
        _transform_source_rings,
        _zip_entry,
        shapefile.__version__,
        pyproj.__version__,
        shapely.__version__,
        shapely.geos_version_string,
        pyproj.proj_version_str,
        pyproj.database.get_database_metadata("EPSG.VERSION"),
    )


def toolchain_receipt(
    root: Path,
    scope_path: Path,
    scope: dict[str, Any],
    dependencies: Dependencies,
) -> dict[str, Any]:
    generator_path = repository_path(
        root, scope["output"]["toolchain"]["generatorRelativePath"]
    )
    configured = scope["output"]["toolchain"]
    actual = {
        "generatorRelativePath": generator_path.relative_to(root).as_posix(),
        "generatorSha256": sha256_file(generator_path),
        "pythonVersion": f"{sys.version_info.major}.{sys.version_info.minor}.{sys.version_info.micro}",
        "pyshpVersion": dependencies.pyshp_version,
        "pyprojVersion": dependencies.pyproj_version,
        "shapelyVersion": dependencies.shapely_version,
        "geosVersion": dependencies.geos_version,
        "projVersion": dependencies.proj_version,
        "epsgDatabaseVersion": dependencies.epsg_database_version,
        "buildingFootprintCoordinatePrecisionMeters": 0.001,
        "roadCoordinatePolicyCode": "RoundTripFloatAfterPublishedBoundaryReclip",
        "roadBoundaryNumericToleranceMeters": ROAD_BOUNDARY_NUMERIC_TOLERANCE_METERS,
        "scopeDefinitionSha256": sha256_file(scope_path),
    }
    for field_name in (
        "generatorRelativePath",
        "generatorSha256",
        "pythonVersion",
        "pyshpVersion",
        "pyprojVersion",
        "shapelyVersion",
        "geosVersion",
        "projVersion",
        "epsgDatabaseVersion",
        "buildingFootprintCoordinatePrecisionMeters",
        "roadCoordinatePolicyCode",
        "roadBoundaryNumericToleranceMeters",
    ):
        require(
            configured.get(field_name) == actual[field_name],
            f"GenerationToolchainMismatch:{field_name}",
        )
    return actual


@dataclass(frozen=True)
class CommonEnuFrame:
    origin_latitude: float
    origin_longitude: float
    offset_x: float
    offset_z: float

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

    def local_to_wgs84(
        self, x: float, z: float, dependencies: Dependencies
    ) -> tuple[float, float]:
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
        transformer = dependencies.Transformer.from_crs(4978, 4326, always_xy=True)
        longitude, latitude, _ = transformer.transform(
            origin[0] + dx, origin[1] + dy, origin[2] + dz
        )
        return float(longitude), float(latitude)


@dataclass(frozen=True)
class BoundarySnapshot:
    code: str
    stable_id: str
    display_name: str
    legal_area_stable_id: str
    geometry: Any
    boundary: list[dict[str, float]]


def load_scope(root: Path, scope_path: Path) -> dict[str, Any]:
    scope = load_json(scope_path)
    require(scope.get("schemaVersion") == SCOPE_SCHEMA_VERSION, "ScopeSchemaMismatch")
    require(scope.get("scopeStableId") == SCOPE_STABLE_ID, "ScopeStableIdMismatch")
    require(scope.get("revision") == REVISION, "ScopeRevisionMismatch")
    require(scope.get("generatedAtUtc") == GENERATED_AT_UTC, "ScopeGeneratedAtMismatch")
    require(scope.get("sourceVintage") == SOURCE_VINTAGE, "ScopeSourceVintageMismatch")
    require(scope.get("desiredReadinessCode") == DESIRED_READINESS, "ScopeReadinessMismatch")
    require(scope.get("diagnosticCodes") == DIAGNOSTIC_CODES, "ScopeDiagnosticsMismatch")
    require(
        scope.get("distributionApproved") is False
        and scope.get("traversalReady") is False
        and scope.get("gameplayReady") is False,
        "ScopeAuthorityBoundaryInvalid",
    )
    require(scope.get("expectedAdministrativeAreaCount") == 30, "ScopeAreaCountMismatch")
    require(scope.get("expectedLegalAreaCount") == 12, "ScopeLegalAreaCountMismatch")
    output = scope.get("output", {})
    require(output.get("moduleSchemaVersion") == MODULE_SCHEMA_VERSION, "ScopeModuleSchemaMismatch")
    require(
        float(output.get("maximumRoadSegmentLengthMeters", 0))
        == MAXIMUM_ROAD_SEGMENT_METERS,
        "ScopeRoadSegmentLimitMismatch",
    )
    require(isinstance(output.get("toolchain"), dict), "ScopeToolchainMissing")
    revision_policy = scope.get("revisionBumpPolicy")
    require(
        isinstance(revision_policy, dict)
        and revision_policy.get("acceptedPackageMutationRequiresNewRevision") is True
        and revision_policy.get("sourceHashChangeRequiresNewRevision") is True
        and revision_policy.get("generatorHashChangeRequiresNewRevision") is True
        and revision_policy.get("geometryPolicyChangeRequiresNewRevision") is True,
        "ScopeRevisionBumpPolicyMissing",
    )
    frame = scope.get("coordinateFrame", {})
    require(
        frame
        == {
            "method": "WGS84-ECEF-ENU-at-zero-altitude",
            "originLatitude": 37.580912,
            "originLongitude": 127.088502,
            "worldOffsetX": 0.0,
            "worldOffsetZ": 0.0,
            "metersPerUnit": 1.0,
        },
        "ScopeCoordinateFrameMismatch",
    )
    areas = scope.get("administrativeAreas", [])
    require(isinstance(areas, list) and len(areas) == 30, "ScopeAdministrativeAreasInvalid")
    observed: dict[str, tuple[str, str]] = {}
    for area in areas:
        stable_id = area.get("administrativeAreaStableId", "")
        code = stable_id.removeprefix("region:kr:hjd:")
        require(stable_id == f"region:kr:hjd:{code}" and len(code) == 10, "ScopeAdministrativeIdInvalid")
        observed[code] = (
            area.get("displayName", ""),
            area.get("legalAreaStableId", "").removeprefix("region:kr:bjd:"),
        )
    require(observed == EXPECTED_ADMINISTRATIVE_AREAS, "ScopeAdministrativeAllowListMismatch")
    expected_legal = {value[1] for value in EXPECTED_ADMINISTRATIVE_AREAS.values()}
    legal = {
        item.get("legalAreaStableId", "").removeprefix("region:kr:bjd:")
        for item in scope.get("legalAreas", [])
    }
    require(legal == expected_legal and len(legal) == 12, "ScopeLegalAllowListMismatch")
    sources = scope.get("sources", [])
    require(isinstance(sources, list) and len(sources) == 3, "ScopeSourcesInvalid")
    for source in sources:
        expected = EXPECTED_SOURCES.get(source.get("datasetId"))
        require(expected is not None, "ScopeSourceUnexpected")
        require(source.get("contentHashSha256") == expected["sha256"], "ScopeSourceHashChanged")
        require(source.get("byteLength") == expected["byteLength"], "ScopeSourceLengthChanged")
        require(source.get("recordCount") == expected["recordCount"], "ScopeSourceCountChanged")
        for field_name in (
            "sourceId",
            "sourceRevision",
            "repositoryRelativePath",
            "licenseCode",
            "limitationCode",
        ):
            require(
                source.get(field_name) == expected[field_name],
                f"ScopeSourceMetadataChanged:{source['datasetId']}:{field_name}",
            )
        if "extractedRepositoryRelativeDirectory" in expected:
            require(
                source.get("extractedRepositoryRelativeDirectory")
                == expected["extractedRepositoryRelativeDirectory"],
                f"ScopeSourceMetadataChanged:{source['datasetId']}:extract",
            )
        repository_path(root, source["repositoryRelativePath"])
    return scope


def source_by_id(scope: dict[str, Any], dataset_id: str) -> dict[str, Any]:
    matches = [source for source in scope["sources"] if source["datasetId"] == dataset_id]
    require(len(matches) == 1, f"ScopeSourceMissing:{dataset_id}")
    return matches[0]


def frame_from_scope(scope: dict[str, Any]) -> CommonEnuFrame:
    value = scope["coordinateFrame"]
    return CommonEnuFrame(
        float(value["originLatitude"]),
        float(value["originLongitude"]),
        float(value["worldOffsetX"]),
        float(value["worldOffsetZ"]),
    )


def _transform_xy(
    transformer: Any, frame: CommonEnuFrame, x: Any, y: Any
) -> tuple[Any, Any]:
    longitudes, latitudes = transformer.transform(x, y)
    try:
        iterator = zip(longitudes, latitudes)
    except TypeError:
        return frame.wgs84_to_local(float(longitudes), float(latitudes))
    points = [
        frame.wgs84_to_local(float(longitude), float(latitude))
        for longitude, latitude in iterator
    ]
    return [point[0] for point in points], [point[1] for point in points]


def _local_to_wgs_transform(
    frame: CommonEnuFrame, dependencies: Dependencies, x: Any, y: Any
) -> tuple[Any, Any]:
    try:
        iterator = zip(x, y)
    except TypeError:
        return frame.local_to_wgs84(float(x), float(y), dependencies)
    points = [
        frame.local_to_wgs84(float(local_x), float(local_z), dependencies)
        for local_x, local_z in iterator
    ]
    return [point[0] for point in points], [point[1] for point in points]


def read_boundaries(
    root: Path,
    scope: dict[str, Any],
    frame: CommonEnuFrame,
    dependencies: Dependencies,
) -> tuple[list[BoundarySnapshot], dict[str, Any]]:
    source = source_by_id(scope, "OA-22160")
    archive_path = verify_frozen_file(root, source)
    with zipfile.ZipFile(archive_path, "r") as archive:
        names = archive.namelist()
        require(len(names) == len(set(names)), "AdministrativeBoundaryArchiveDuplicateEntry")
        entries: dict[str, str] = {}
        for extension in (".cpg", ".dbf", ".prj", ".shp", ".shx"):
            matches = [name for name in names if name.lower().endswith(extension)]
            require(len(matches) == 1, f"AdministrativeBoundaryArchiveEntryInvalid:{extension}")
            entries[extension] = matches[0]
        cpg = archive.read(entries[".cpg"]).decode("ascii").strip().upper()
        require(cpg in {"UTF-8", "UTF8", "65001"}, "AdministrativeBoundaryEncodingMismatch")
        projection_text = archive.read(entries[".prj"]).decode("utf-8-sig")
        source_crs = dependencies.CRS.from_wkt(projection_text)
        authority = source_crs.to_authority()
        require(authority == ("EPSG", "5181"), "AdministrativeBoundaryCrsMismatch")
        to_wgs = dependencies.Transformer.from_crs(source_crs, 4326, always_xy=True)
        reader = dependencies.shapefile.Reader(
            shp=io.BytesIO(archive.read(entries[".shp"])),
            shx=io.BytesIO(archive.read(entries[".shx"])),
            dbf=io.BytesIO(archive.read(entries[".dbf"])),
            encoding="utf-8",
            encodingErrors="strict",
        )
        try:
            require(len(reader) == source["recordCount"], "AdministrativeBoundaryRecordCountMismatch")
            field_names = {field[0] for field in reader.fields[1:]}
            require({"ADSTRD_CD", "ADSTRD_NM"}.issubset(field_names), "AdministrativeBoundaryFieldsMissing")
            selected: dict[str, BoundarySnapshot] = {}
            for shape_record in reader.iterShapeRecords():
                properties = shape_record.record.as_dict()
                short_code = str(properties["ADSTRD_CD"])
                code = short_code + "00"
                if code not in EXPECTED_ADMINISTRATIVE_AREAS:
                    continue
                require(shape_record.shape.shapeType == 5, f"AdministrativeBoundaryShapeTypeInvalid:{code}")
                starts = list(shape_record.shape.parts) + [len(shape_record.shape.points)]
                rings = [
                    shape_record.shape.points[starts[index] : starts[index + 1]]
                    for index in range(len(starts) - 1)
                ]
                local_rings = dependencies.transform_source_rings(rings, to_wgs, frame)
                geometry = dependencies.rings_to_geometry(local_rings)
                require(isinstance(geometry, dependencies.Polygon), f"AdministrativeBoundaryMultipartUnsupported:{code}")
                require(len(geometry.interiors) == 0, f"AdministrativeBoundaryHoleUnsupported:{code}")
                expected_name, legal_code = EXPECTED_ADMINISTRATIVE_AREAS[code]
                supplied_name = str(properties["ADSTRD_NM"]).strip()
                require(
                    _normalized_dong_name(expected_name) == _normalized_dong_name(supplied_name),
                    f"AdministrativeBoundaryNameMismatch:{code}",
                )
                require(code not in selected, f"AdministrativeBoundaryDuplicate:{code}")
                polygon = dependencies.orient(geometry, sign=-1.0)
                selected[code] = BoundarySnapshot(
                    code,
                    f"region:kr:hjd:{code}",
                    expected_name,
                    f"region:kr:bjd:{legal_code}",
                    polygon,
                    dependencies.canonical_ring(polygon.exterior.coords, clockwise=True),
                )
        finally:
            reader.close()
    require(set(selected) == set(EXPECTED_ADMINISTRATIVE_AREAS), "AdministrativeBoundaryCoverageIncomplete")
    boundaries = [selected[code] for code in sorted(selected)]
    union = dependencies.unary_union([boundary.geometry for boundary in boundaries])
    require(not union.is_empty and union.is_valid, "AdministrativeBoundaryUnionInvalid")
    overlap_area = sum(boundary.geometry.area for boundary in boundaries) - union.area
    require(overlap_area < 0.1, "AdministrativeBoundaryInteriorOverlapDetected")
    rounded_overlap_area = round(float(overlap_area), 6)
    if rounded_overlap_area == 0.0:
        rounded_overlap_area = 0.0
    return boundaries, {
        "sourceRecordCount": source["recordCount"],
        "selectedBoundaryCount": len(boundaries),
        "unionAreaSquareMeters": round(float(union.area), 3),
        "interiorOverlapAreaSquareMeters": rounded_overlap_area,
        "sourceCrs": "EPSG:5181",
        "outputCoordinateMethod": scope["coordinateFrame"]["method"],
        "historicalBootstrapOnly": True,
        "currentAuthoritativeBoundaryAvailable": False,
    }


def _source_bbox_for_local_geometry(
    local_geometry: Any,
    source_crs: Any,
    frame: CommonEnuFrame,
    dependencies: Dependencies,
) -> tuple[float, float, float, float]:
    to_wgs = lambda x, y, z=None: _local_to_wgs_transform(frame, dependencies, x, y)
    wgs_geometry = dependencies.transform(to_wgs, local_geometry)
    to_source = dependencies.Transformer.from_crs(4326, source_crs, always_xy=True)
    source_geometry = dependencies.transform(to_source.transform, wgs_geometry)
    min_x, min_y, max_x, max_y = source_geometry.bounds
    return min_x - 2.0, min_y - 2.0, max_x + 2.0, max_y + 2.0


def _bbox_intersects(first: Sequence[float], second: Sequence[float]) -> bool:
    return not (
        first[2] < second[0]
        or first[0] > second[2]
        or first[3] < second[1]
        or first[1] > second[3]
    )


def _normalized_dong_name(value: str) -> str:
    compact = value.replace("서울특별시", "").replace("광진구", "")
    compact = compact.replace("동대문구", "").replace("중랑구", "")
    return compact.replace("제", "").replace("·", ".").replace(" ", "")


def _polygon_members(geometry: Any, dependencies: Dependencies) -> list[Any]:
    geometry = dependencies.polygonal(geometry)
    if isinstance(geometry, dependencies.Polygon):
        return [geometry]
    if isinstance(geometry, dependencies.MultiPolygon):
        return list(geometry.geoms)
    return []


def _building_input(
    building_stable_id: str,
    geometry: Any,
    building_kind: str,
    observed_height: float | None,
    above_ground_floors: int | None,
    dependencies: Dependencies,
) -> tuple[dict[str, Any], int, bool]:
    polygons = _polygon_members(geometry, dependencies)
    require(bool(polygons), "BuildingGeometryContainsNoPolygon")
    polygons.sort(key=lambda polygon: (-polygon.area, polygon.wkb_hex))
    largest = dependencies.orient(polygons[0], sign=-1.0)
    interior_ring_count = sum(len(polygon.interiors) for polygon in polygons)
    multipart_omitted = len(polygons) > 1
    footprint = dependencies.canonical_ring(largest.exterior.coords, clockwise=True)
    height = (
        round(float(observed_height), 3)
        if observed_height is not None and observed_height > 0
        else SYMBOLIC_BUILDING_HEIGHT_METERS
    )
    floors = above_ground_floors if above_ground_floors and above_ground_floors > 0 else None
    area = round(float(geometry.area), 3)
    evidence = "OfficialGisBuildingFootprintCandidate"
    if interior_ring_count or multipart_omitted:
        evidence = "OfficialGisBuildingLargestExteriorCandidate"
    if observed_height is None or observed_height <= 0:
        evidence += ":SymbolicHeightFallback"
    return (
        {
            "buildingStableId": building_stable_id,
            "categoryCode": f"al-d010-kind:{building_kind or 'unknown'}",
            "evidenceKindCode": evidence,
            "aboveGroundFloorCount": floors,
            "heightMeters": height,
            "buildingAreaSquareMeters": area,
            "totalFloorAreaSquareMeters": round(area * floors, 3) if floors else 0.0,
            "footprint": footprint,
        },
        interior_ring_count,
        multipart_omitted,
    )


def _point_on_segment(
    point: tuple[float, float], start: tuple[float, float], end: tuple[float, float]
) -> bool:
    epsilon = 0.000001
    cross = (point[1] - start[1]) * (end[0] - start[0]) - (
        point[0] - start[0]
    ) * (end[1] - start[1])
    if abs(cross) > epsilon:
        return False
    return (
        min(start[0], end[0]) - epsilon
        <= point[0]
        <= max(start[0], end[0]) + epsilon
        and min(start[1], end[1]) - epsilon
        <= point[1]
        <= max(start[1], end[1]) + epsilon
    )


def _point_in_polygon(
    point: tuple[float, float], polygon: Sequence[tuple[float, float]]
) -> bool:
    inside = False
    for index in range(len(polygon)):
        previous = len(polygon) - 1 if index == 0 else index - 1
        if _point_on_segment(point, polygon[previous], polygon[index]):
            return True
        if (polygon[index][1] > point[1]) != (polygon[previous][1] > point[1]):
            intersection_x = (
                (polygon[previous][0] - polygon[index][0])
                * (point[1] - polygon[index][1])
                / (polygon[previous][1] - polygon[index][1])
                + polygon[index][0]
            )
            if point[0] < intersection_x:
                inside = not inside
    return inside


def contract_point_on_surface(
    footprint: Sequence[dict[str, float]],
) -> tuple[float, float]:
    """C# 행정동디오라마ProjectionBuilder.PointOnSurface와 같은 계산."""

    polygon = [(float(point["x"]), float(point["z"])) for point in footprint]
    if len(polygon) > 1 and polygon[0] == polygon[-1]:
        polygon = polygon[:-1]
    require(len(polygon) >= 3, "ContractFootprintTooSmall")
    signed_area = 0.0
    centroid_x = 0.0
    centroid_z = 0.0
    for index in range(len(polygon)):
        current = polygon[index]
        following = polygon[(index + 1) % len(polygon)]
        cross = current[0] * following[1] - following[0] * current[1]
        signed_area += cross
        centroid_x += (current[0] + following[0]) * cross
        centroid_z += (current[1] + following[1]) * cross
    if abs(signed_area) > 0.000001:
        candidate = (
            centroid_x / (3.0 * signed_area),
            centroid_z / (3.0 * signed_area),
        )
        if _point_in_polygon(candidate, polygon):
            return candidate
    z_values = sorted(set(point[1] for point in polygon))
    scan_lines = [
        (z_values[index] + z_values[index + 1]) / 2.0
        for index in range(len(z_values) - 1)
    ]
    best: tuple[float, float] | None = None
    best_width = -1.0
    for z in scan_lines:
        intersections: list[float] = []
        for index in range(len(polygon)):
            start = polygon[index]
            end = polygon[(index + 1) % len(polygon)]
            if (start[1] > z) == (end[1] > z):
                continue
            intersections.append(
                start[0] + (end[0] - start[0]) * (z - start[1]) / (end[1] - start[1])
            )
        intersections.sort()
        for index in range(0, len(intersections) - 1, 2):
            width = intersections[index + 1] - intersections[index]
            if width <= best_width:
                continue
            best_width = width
            best = ((intersections[index] + intersections[index + 1]) / 2.0, z)
    return best if best is not None else polygon[0]


def read_buildings(
    root: Path,
    scope: dict[str, Any],
    frame: CommonEnuFrame,
    boundaries: Sequence[BoundarySnapshot],
    dependencies: Dependencies,
) -> tuple[dict[str, list[dict[str, Any]]], dict[str, Any]]:
    source = source_by_id(scope, "data-go-kr-15083092")
    archive_path = verify_frozen_file(root, source)
    union = dependencies.unary_union([boundary.geometry for boundary in boundaries])
    source_crs = dependencies.CRS.from_epsg(5186)
    search_bbox = _source_bbox_for_local_geometry(union, source_crs, frame, dependencies)
    to_wgs = dependencies.Transformer.from_crs(source_crs, 4326, always_xy=True)
    by_area = {boundary.code: [] for boundary in boundaries}
    assigned_identifiers: set[str] = set()
    source_feature_occurrences: dict[str, int] = {}
    quarantine: list[dict[str, Any]] = []
    outside: list[dict[str, Any]] = []
    ambiguous: list[dict[str, Any]] = []
    legal_mismatches: list[dict[str, Any]] = []
    contract_anchor_overrides: list[dict[str, Any]] = []
    rounded_boundary_geometries = {
        boundary.code: dependencies.Polygon(
            [(point["x"], point["z"]) for point in boundary.boundary]
        )
        for boundary in boundaries
    }
    counts = {
        "sourceRecords": 0,
        "deletedRecords": 0,
        "unionBoundingBoxCandidates": 0,
        "geometryIntersectsUnion": 0,
        "assigned": 0,
        "outsideAfterIntersection": 0,
        "ambiguous": 0,
        "malformedQuarantine": 0,
        "roundedFootprintQuarantine": 0,
        "interiorRingOmittedBuildings": 0,
        "interiorRingsOmitted": 0,
        "multipartOmittedBuildings": 0,
        "legalAreaMismatch": 0,
        "symbolicHeightFallback": 0,
        "duplicateSourceFeatureIdOccurrences": 0,
        "v1ContractAssignmentAnchorOverrides": 0,
    }
    with zipfile.ZipFile(archive_path, "r") as archive:
        shp_entry = dependencies.zip_entry(archive, ".shp")
        dbf_entry = dependencies.zip_entry(archive, ".dbf")
        prj_entry = dependencies.zip_entry(archive, ".prj")
        projection_text = archive.read(prj_entry).decode("utf-8-sig")
        declared = dependencies.CRS.from_wkt(projection_text)
        normalized_wkt = projection_text.replace(" ", "")
        require(
            'AUTHORITY["EPSG","5186"]' in normalized_wkt
            and "Korea_Central_Belt_2010" in declared.name
            and declared.is_projected,
            "BuildingSourceCrsMismatch",
        )
        with archive.open(shp_entry, "r") as shp_stream, archive.open(dbf_entry, "r") as dbf_stream:
            dependencies.read_shp_header(shp_stream)
            record_count, _, record_length, fields = dependencies.read_dbf_header(dbf_stream)
            require(record_count == source["recordCount"], "BuildingRecordCountMismatch")
            require(
                {"A1", "A3", "A8", "A16", "A26"}.issubset(fields),
                "BuildingDbfFieldsMissing",
            )
            for index in range(record_count):
                record = dependencies.read_exact(dbf_stream, record_length, "BuildingDbfRecordTruncated")
                record_header = dependencies.read_exact(shp_stream, 8, "BuildingShpRecordHeaderTruncated")
                record_number, content_words = struct.unpack(">II", record_header)
                require(record_number == index + 1, "BuildingShpRecordOrderMismatch")
                require(2 <= content_words <= 1_048_576, "BuildingShpRecordBudgetInvalid")
                body = dependencies.read_exact(shp_stream, content_words * 2, "BuildingShpRecordBodyTruncated")
                counts["sourceRecords"] += 1
                if counts["sourceRecords"] % 100_000 == 0:
                    print(
                        json.dumps(
                            {
                                "stage": "ReadBuildings",
                                "sourceRecords": counts["sourceRecords"],
                                "assigned": counts["assigned"],
                            },
                            ensure_ascii=False,
                        ),
                        file=sys.stderr,
                        flush=True,
                    )
                if record[0] == 0x2A:
                    counts["deletedRecords"] += 1
                    continue
                require(record[0] == 0x20, "BuildingDbfDeletionMarkerInvalid")
                if len(body) < 36:
                    continue
                record_bbox = struct.unpack_from("<4d", body, 4)
                if not _bbox_intersects(record_bbox, search_bbox):
                    continue
                counts["unionBoundingBoxCandidates"] += 1
                source_feature_id = dependencies.dbf_text(record, fields["A1"], "ascii")
                source_legal_code = dependencies.dbf_text(record, fields["A3"], "ascii")
                stable_id = (
                    f"vworld:al-d010:{source_feature_id}"
                    if source_feature_id
                    else f"vworld:al-d010:record:{index + 1}"
                )
                try:
                    source_rings = dependencies.parse_shp_polygon(body)
                    local_rings = dependencies.transform_source_rings(source_rings, to_wgs, frame)
                    geometry = dependencies.rings_to_geometry(local_rings)
                    require(not geometry.is_empty, "BuildingGeometryEmpty")
                except Exception as exc:  # source feature quarantine, not an authority fallback
                    counts["malformedQuarantine"] += 1
                    quarantine.append(
                        {
                            "buildingStableId": stable_id,
                            "sourceRecordIndex": index,
                            "sourceLegalAreaStableId": (
                                f"region:kr:bjd:{source_legal_code}" if source_legal_code else ""
                            ),
                            "diagnosticCode": "MalformedBuildingGeometryQuarantined",
                            "parserErrorCode": str(exc).split(":", 1)[0][:120],
                        }
                    )
                    continue
                if not geometry.intersects(union):
                    continue
                counts["geometryIntersectsUnion"] += 1
                representative = geometry.representative_point()
                matches = [
                    boundary
                    for boundary in boundaries
                    if boundary.geometry.covers(representative)
                ]
                if len(matches) == 0:
                    counts["outsideAfterIntersection"] += 1
                    outside.append(
                        {
                            "buildingStableId": stable_id,
                            "sourceRecordIndex": index,
                            "sourceLegalAreaStableId": (
                                f"region:kr:bjd:{source_legal_code}" if source_legal_code else ""
                            ),
                            "diagnosticCode": "RepresentativePointOutsideAdministrativeBoundarySet",
                        }
                    )
                    continue
                if len(matches) > 1:
                    counts["ambiguous"] += 1
                    ambiguous.append(
                        {
                            "buildingStableId": stable_id,
                            "sourceRecordIndex": index,
                            "candidateAdministrativeAreaStableIds": [
                                match.stable_id for match in matches
                            ],
                            "diagnosticCode": "RepresentativePointMatchesMultipleAdministrativeBoundaries",
                        }
                    )
                    continue
                assigned_boundary = matches[0]
                building_kind = dependencies.dbf_text(record, fields["A8"], "cp949")
                observed_height = dependencies.parse_number(
                    dependencies.dbf_text(record, fields["A16"], "ascii")
                )
                floors = dependencies.parse_integer(
                    dependencies.dbf_text(record, fields["A26"], "ascii")
                )
                building, interior_rings, multipart = _building_input(
                    stable_id,
                    geometry,
                    building_kind,
                    observed_height,
                    floors,
                    dependencies,
                )
                exported_polygon = dependencies.Polygon(
                    [(point["x"], point["z"]) for point in building["footprint"]]
                )
                if (
                    exported_polygon.is_empty
                    or not exported_polygon.is_valid
                    or not exported_polygon.exterior.is_simple
                    or exported_polygon.area <= 0.000001
                ):
                    counts["roundedFootprintQuarantine"] += 1
                    quarantine.append(
                        {
                            "buildingStableId": stable_id,
                            "sourceRecordIndex": index,
                            "sourceLegalAreaStableId": (
                                f"region:kr:bjd:{source_legal_code}" if source_legal_code else ""
                            ),
                            "canonicalAdministrativeAreaStableId": assigned_boundary.stable_id,
                            "diagnosticCode": "RoundedLargestExteriorFootprintInvalidQuarantined",
                            "sourceGeometryValidBeforeRounding": bool(geometry.is_valid),
                        }
                    )
                    continue
                require(bool(source_feature_id), "AssignedBuildingSourceFeatureIdMissing")
                occurrence = source_feature_occurrences.get(source_feature_id, 0) + 1
                source_feature_occurrences[source_feature_id] = occurrence
                if occurrence > 1:
                    counts["duplicateSourceFeatureIdOccurrences"] += 1
                    stable_id = f"vworld:al-d010:{source_feature_id}:record:{index + 1}"
                    building["buildingStableId"] = stable_id
                require(stable_id not in assigned_identifiers, "AssignedBuildingStableIdDuplicate")
                assigned_identifiers.add(stable_id)
                assignment_point = {
                    "x": round(float(representative.x), 6),
                    "z": round(float(representative.y), 6),
                }
                building["assignmentPoint"] = assignment_point
                building["assignmentMethodCode"] = (
                    "SourceGeometryPointOnSurfaceWithinUniqueAdministrativeBoundary"
                )
                building["assignmentConfidenceCode"] = "UniqueHistoricalBoundaryMatch"
                building["assignmentBoundarySourceRevision"] = (
                    "seoul-oa22160:file-modified:20231031:retrieved:20260912"
                )
                require(
                    rounded_boundary_geometries[assigned_boundary.code].covers(
                        dependencies.Point(assignment_point["x"], assignment_point["z"])
                    ),
                    "AssignmentAnchorOutsideExportedAdministrativeBoundary",
                )
                exported_point = contract_point_on_surface(building["footprint"])
                rounded_assignment_boundary = rounded_boundary_geometries[assigned_boundary.code]
                if not rounded_assignment_boundary.covers(
                    dependencies.Point(exported_point)
                ):
                    counts["v1ContractAssignmentAnchorOverrides"] += 1
                    exported_matches = [
                        boundary
                        for boundary in boundaries
                        if rounded_boundary_geometries[boundary.code].covers(
                            dependencies.Point(exported_point)
                        )
                    ]
                    contract_anchor_overrides.append(
                        {
                            "buildingStableId": stable_id,
                            "canonicalAdministrativeAreaStableId": assigned_boundary.stable_id,
                            "exportedFootprintPointCandidateAdministrativeAreaStableIds": [
                                boundary.stable_id for boundary in exported_matches
                            ],
                            "diagnosticCode": "ExplicitSourceGeometryAssignmentAnchorRequired",
                        }
                    )
                by_area[assigned_boundary.code].append(building)
                counts["assigned"] += 1
                if interior_rings:
                    counts["interiorRingOmittedBuildings"] += 1
                    counts["interiorRingsOmitted"] += interior_rings
                if multipart:
                    counts["multipartOmittedBuildings"] += 1
                if observed_height is None or observed_height <= 0:
                    counts["symbolicHeightFallback"] += 1
                expected_legal_id = assigned_boundary.legal_area_stable_id
                actual_legal_id = (
                    f"region:kr:bjd:{source_legal_code}" if source_legal_code else ""
                )
                if actual_legal_id != expected_legal_id:
                    counts["legalAreaMismatch"] += 1
                    legal_mismatches.append(
                        {
                            "buildingStableId": stable_id,
                            "administrativeAreaStableId": assigned_boundary.stable_id,
                            "expectedLegalAreaStableId": expected_legal_id,
                            "sourceLegalAreaStableId": actual_legal_id,
                            "diagnosticCode": "SourceLegalAreaDoesNotMatchFrozenAdministrativeCrosswalk",
                        }
                    )
            require(shp_stream.read(1) == b"", "BuildingShpContainsUnexpectedExtraRecords")
    for values in by_area.values():
        values.sort(key=lambda value: value["buildingStableId"])
    require(counts["assigned"] == sum(len(values) for values in by_area.values()), "BuildingAssignmentCountMismatch")
    return by_area, {
        **counts,
        "assignmentMethodCode": "SourceGeometryPointOnSurfaceWithinUniqueAdministrativeBoundary",
        "scopeUnresolvedCount": (
            counts["outsideAfterIntersection"]
            + counts["ambiguous"]
            + counts["malformedQuarantine"]
            + counts["roundedFootprintQuarantine"]
        ),
        "quarantined": quarantine,
        "outside": outside,
        "ambiguousAssignments": ambiguous,
        "legalAreaMismatches": legal_mismatches,
        "v1ContractAssignmentAnchorOverrideDetails": contract_anchor_overrides,
        "byAdministrativeArea": {
            f"region:kr:hjd:{code}": len(values)
            for code, values in sorted(by_area.items())
        },
        "limitations": [
            "LargestExteriorOnlyBecauseV1ContractHasSingleFootprintRing",
            "InvalidOneMillimeterRoundedFootprintsAreQuarantinedWithoutRepair",
            "InteriorRingsAndMultipartGeometryRemainInAuditOnly",
            "SymbolicFourMeterHeightUsedWhenObservedHeightMissing",
            "LegalDongCodeIsDiagnosticOnlyAndDoesNotOverrideSpatialAssignment",
            "ExplicitAssignmentAnchorPreservesFullSourceGeometryAssignmentWhenV1FootprintDiffers",
            "NoAddressOrPrivateDbfFieldsExported",
            "DistributionNotApproved",
        ],
    }


def _line_members(geometry: Any, dependencies: Dependencies) -> list[Any]:
    if geometry.is_empty:
        return []
    if isinstance(geometry, dependencies.LineString):
        return [geometry] if geometry.length > 0.001 else []
    if isinstance(geometry, dependencies.MultiLineString):
        return [member for member in geometry.geoms if member.length > 0.001]
    if isinstance(geometry, dependencies.GeometryCollection):
        members: list[Any] = []
        for item in geometry.geoms:
            members.extend(_line_members(item, dependencies))
        return members
    return []


def _round_point(
    point: Sequence[float], precision: int | None = 3
) -> dict[str, float]:
    x = float(point[0]) if precision is None else round(float(point[0]), precision)
    z = float(point[1]) if precision is None else round(float(point[1]), precision)
    return {"x": 0.0 if x == 0.0 else x, "z": 0.0 if z == 0.0 else z}


def _two_point_segments(
    coordinates: Sequence[Sequence[float]],
    maximum_length: float,
    *,
    precision: int | None = 3,
) -> Iterator[tuple[dict[str, float], dict[str, float]]]:
    for index in range(len(coordinates) - 1):
        start = coordinates[index]
        end = coordinates[index + 1]
        dx = float(end[0]) - float(start[0])
        dz = float(end[1]) - float(start[1])
        length = math.hypot(dx, dz)
        if length <= 0.001:
            continue
        pieces = max(1, math.ceil(length / min(maximum_length, INTERNAL_ROAD_SEGMENT_METERS)))
        for piece in range(pieces):
            ratio_start = piece / pieces
            ratio_end = (piece + 1) / pieces
            first = _round_point(
                (float(start[0]) + dx * ratio_start, float(start[1]) + dz * ratio_start),
                precision,
            )
            second = _round_point(
                (float(start[0]) + dx * ratio_end, float(start[1]) + dz * ratio_end),
                precision,
            )
            rounded_length = math.hypot(
                second["x"] - first["x"], second["z"] - first["z"]
            )
            require(rounded_length <= maximum_length + 0.000001, "RoadSegmentLengthExceeded")
            if rounded_length > 0.001:
                yield first, second


def _nodelink_source_parts(shape_value: Any, dependencies: Dependencies) -> list[Any]:
    starts = list(shape_value.parts) + [len(shape_value.points)]
    parts = []
    for index in range(len(starts) - 1):
        coordinates = shape_value.points[starts[index] : starts[index + 1]]
        if len(coordinates) >= 2:
            parts.append(dependencies.LineString(coordinates))
    return parts


def verify_nodelink_extract(
    root: Path, source: dict[str, Any]
) -> tuple[Path, list[dict[str, Any]]]:
    archive_path = verify_frozen_file(root, source)
    source_directory = repository_path(root, source["extractedRepositoryRelativeDirectory"])
    require(source_directory.is_dir() and not source_directory.is_symlink(), "NodeLinkExtractDirectoryMissing")
    members = [f"MOCT_LINK.{extension}" for extension in ("cpg", "prj", "shp", "shx", "dbf")]
    receipts: list[dict[str, Any]] = []
    with zipfile.ZipFile(archive_path, "r") as archive:
        require(all(name in archive.namelist() for name in members), "NodeLinkArchiveMembersMissing")
        for name in members:
            extracted = source_directory / name
            require(extracted.is_file() and not extracted.is_symlink(), f"NodeLinkExtractedMemberMissing:{name}")
            digest = hashlib.sha256()
            with archive.open(name, "r") as stream:
                for chunk in iter(lambda: stream.read(1024 * 1024), b""):
                    digest.update(chunk)
            archive_hash = digest.hexdigest().upper()
            extracted_hash = sha256_file(extracted)
            require(archive_hash == extracted_hash, f"NodeLinkExtractedMemberHashMismatch:{name}")
            receipts.append(
                {"file": name, "sha256": extracted_hash, "byteLength": extracted.stat().st_size}
            )
    return source_directory, receipts


def read_roads(
    root: Path,
    scope: dict[str, Any],
    frame: CommonEnuFrame,
    boundaries: Sequence[BoundarySnapshot],
    dependencies: Dependencies,
) -> tuple[dict[str, list[dict[str, Any]]], dict[str, Any]]:
    source = source_by_id(scope, "data-go-kr-15025526")
    source_directory, source_files = verify_nodelink_extract(root, source)
    require((source_directory / "MOCT_LINK.cpg").read_text(encoding="ascii").strip() == "949", "NodeLinkEncodingMismatch")
    projection_text = (source_directory / "MOCT_LINK.prj").read_text(encoding="ascii")
    source_crs = dependencies.CRS.from_wkt(projection_text)
    to_wgs = dependencies.Transformer.from_crs(source_crs, 4326, always_xy=True)
    union = dependencies.unary_union([boundary.geometry for boundary in boundaries])
    search_bbox = _source_bbox_for_local_geometry(union, source_crs, frame, dependencies)
    by_area = {boundary.code: [] for boundary in boundaries}
    identifiers_by_area = {boundary.code: set() for boundary in boundaries}
    output_boundary_geometries = {
        boundary.code: dependencies.Polygon(
            [(point["x"], point["z"]) for point in boundary.boundary]
        )
        for boundary in boundaries
    }
    link_ids: set[str] = set()
    counts = {
        "nationalRecordCount": 0,
        "unionBoundingBoxCandidates": 0,
        "directedLinksIntersectingUnion": 0,
        "administrativeDongLinkOccurrences": 0,
        "crossBoundaryDirectedLinks": 0,
        "exportedTwoPointSegments": 0,
        "maximumExportedSegmentLengthMeters": 0.0,
        "exactBoundaryContainmentFailureSegments": 0,
    }
    reader = dependencies.shapefile.Reader(
        str(source_directory / "MOCT_LINK.shp"),
        encoding="cp949",
        encodingErrors="strict",
    )
    try:
        require(len(reader) == source["recordCount"], "NodeLinkRecordCountMismatch")
        counts["nationalRecordCount"] = len(reader)
        field_names = {field[0] for field in reader.fields[1:]}
        require(
            {"LINK_ID", "F_NODE", "T_NODE", "LANES", "ROAD_NAME", "UPDATEDATE"}.issubset(field_names),
            "NodeLinkDbfFieldsMissing",
        )
        for candidate_index, shape_record in enumerate(
            reader.iterShapeRecords(bbox=search_bbox), start=1
        ):
            counts["unionBoundingBoxCandidates"] += 1
            if candidate_index % 5_000 == 0:
                print(
                    json.dumps(
                        {
                            "stage": "ReadNodeLink",
                            "bboxCandidates": candidate_index,
                            "selectedDirectedLinks": counts["directedLinksIntersectingUnion"],
                        },
                        ensure_ascii=False,
                    ),
                    file=sys.stderr,
                    flush=True,
                )
            properties = shape_record.record.as_dict()
            link_id = str(properties["LINK_ID"]).strip()
            require(bool(link_id), "NodeLinkStableIdMissing")
            require(link_id not in link_ids, "NodeLinkStableIdDuplicate")
            link_ids.add(link_id)
            source_parts = _nodelink_source_parts(shape_record.shape, dependencies)
            local_parts = [
                dependencies.transform(
                    lambda x, y, z=None: _transform_xy(to_wgs, frame, x, y),
                    part,
                )
                for part in source_parts
            ]
            local_parts = [part for part in local_parts if not part.is_empty]
            if not local_parts:
                continue
            local_geometry = (
                local_parts[0]
                if len(local_parts) == 1
                else dependencies.MultiLineString([list(part.coords) for part in local_parts])
            )
            if not local_geometry.intersects(union):
                continue
            counts["directedLinksIntersectingUnion"] += 1
            matched_area_count = 0
            for boundary in boundaries:
                output_boundary = output_boundary_geometries[boundary.code]
                if not _bbox_intersects(local_geometry.bounds, output_boundary.bounds):
                    continue
                boundary_has_link = False
                for source_part_index, local_part in enumerate(local_parts):
                    clipped = local_part.intersection(output_boundary)
                    members = _line_members(clipped, dependencies)
                    oriented_members: list[tuple[float, Any]] = []
                    for member in members:
                        coordinates = list(member.coords)
                        first_distance = local_part.project(dependencies.Point(coordinates[0]))
                        last_distance = local_part.project(dependencies.Point(coordinates[-1]))
                        if last_distance < first_distance:
                            coordinates.reverse()
                            member = dependencies.LineString(coordinates)
                            first_distance, last_distance = last_distance, first_distance
                        oriented_members.append((float(first_distance), member))
                    oriented_members.sort(key=lambda item: (round(item[0], 6), item[1].wkb_hex))
                    for clip_index, (_, member) in enumerate(oriented_members):
                        segment_index = 0
                        for start, end in _two_point_segments(
                            list(member.coords),
                            MAXIMUM_ROAD_SEGMENT_METERS,
                            precision=None,
                        ):
                            candidate_line = dependencies.LineString(
                                [(start["x"], start["z"]), (end["x"], end["z"])]
                            )
                            final_clipped = candidate_line.intersection(output_boundary)
                            for final_member in _line_members(final_clipped, dependencies):
                                final_coordinates = list(final_member.coords)
                                for coordinate_index in range(len(final_coordinates) - 1):
                                    final_start = _round_point(
                                        final_coordinates[coordinate_index], None
                                    )
                                    final_end = _round_point(
                                        final_coordinates[coordinate_index + 1], None
                                    )
                                    final_line = dependencies.LineString(
                                        [
                                            (final_start["x"], final_start["z"]),
                                            (final_end["x"], final_end["z"]),
                                        ]
                                    )
                                    length = float(final_line.length)
                                    if length <= 0.001:
                                        continue
                                    require(
                                        length <= MAXIMUM_ROAD_SEGMENT_METERS + 0.000001,
                                        "FinalRoadSegmentLengthExceeded",
                                    )
                                    if not output_boundary.covers(final_line):
                                        counts["exactBoundaryContainmentFailureSegments"] += 1
                                    require(
                                        output_boundary.buffer(
                                            ROAD_BOUNDARY_NUMERIC_TOLERANCE_METERS
                                        ).covers(final_line),
                                        "FinalRoadSegmentOutsideExportedBoundaryTolerance",
                                    )
                                    stable_id = (
                                        f"nodelink:link:{link_id}:hjd:{boundary.code}:"
                                        f"part:{source_part_index}:clip:{clip_index}:segment:{segment_index}"
                                    )
                                    require(
                                        stable_id not in identifiers_by_area[boundary.code],
                                        "RoadSegmentStableIdDuplicate",
                                    )
                                    identifiers_by_area[boundary.code].add(stable_id)
                                    counts["maximumExportedSegmentLengthMeters"] = max(
                                        counts["maximumExportedSegmentLengthMeters"], length
                                    )
                                    by_area[boundary.code].append(
                                        {
                                            "roadStableId": stable_id,
                                            "from": final_start,
                                            "to": final_end,
                                            "evidenceKindCode": "OfficialStandardNodeLinkPresentationOnly",
                                        }
                                    )
                                    counts["exportedTwoPointSegments"] += 1
                                    segment_index += 1
                                    boundary_has_link = True
                if boundary_has_link:
                    counts["administrativeDongLinkOccurrences"] += 1
                    matched_area_count += 1
            if matched_area_count > 1:
                counts["crossBoundaryDirectedLinks"] += 1
    finally:
        reader.close()
    for values in by_area.values():
        values.sort(key=lambda value: value["roadStableId"])
    require(
        counts["exportedTwoPointSegments"] == sum(len(values) for values in by_area.values()),
        "RoadSegmentCountMismatch",
    )
    counts["maximumExportedSegmentLengthMeters"] = round(
        counts["maximumExportedSegmentLengthMeters"], 6
    )
    return by_area, {
        **counts,
        "sourceCrsAuthority": source_crs.to_authority(),
        "sourceFiles": source_files,
        "clippingMethodCode": "DirectedNodeLinkReclippedToPublishedRoundedHistoricalAdministrativeBoundary",
        "segmentPolicyCode": "ConsecutiveTwoPointSegmentsAtMost100Meters",
        "boundaryContainmentToleranceMeters": ROAD_BOUNDARY_NUMERIC_TOLERANCE_METERS,
        "byAdministrativeArea": {
            f"region:kr:hjd:{code}": len(values)
            for code, values in sorted(by_area.items())
        },
        "limitations": [
            "NodeLinkIsDirectedCenterlineNotPhysicalRoadWidth",
            "NoAlleyCompletenessGuarantee",
            "NoSidewalkOrTrafficSignalAuthority",
            "NoTraversalGraphAuthority",
            "DistributionNotApproved",
        ],
    }


def crosswalk_source(scope: dict[str, Any]) -> dict[str, Any]:
    crosswalk = [
        {
            "administrativeAreaStableId": area["administrativeAreaStableId"],
            "displayName": area["displayName"],
            "legalAreaStableId": area["legalAreaStableId"],
        }
        for area in sorted(
            scope["administrativeAreas"],
            key=lambda item: item["administrativeAreaStableId"],
        )
    ]
    return {
        "sourceId": "source:mois-standard-codes",
        "datasetId": "korea-administrative-legal-crosswalk",
        "sourceRevision": "mois-jscode:20260301",
        "contentHashSha256": sha256_bytes(canonical_json_bytes(crosswalk)),
        "licenseCode": "PublicOpenData",
        "limitationCode": "CodeAndJurisdictionOnly;NoBoundary;FrozenAllowList",
    }


def module_sources(scope: dict[str, Any]) -> list[dict[str, Any]]:
    return [
        {
            "sourceId": source["sourceId"],
            "datasetId": source["datasetId"],
            "sourceRevision": source["sourceRevision"],
            "contentHashSha256": source["contentHashSha256"],
            "licenseCode": source["licenseCode"],
            "limitationCode": source["limitationCode"],
        }
        for source in scope["sources"]
    ] + [crosswalk_source(scope)]


def build_module(
    scope: dict[str, Any],
    boundary: BoundarySnapshot,
    buildings: list[dict[str, Any]],
    roads: list[dict[str, Any]],
) -> dict[str, Any]:
    document = {
        "schemaVersion": MODULE_SCHEMA_VERSION,
        "projectionRevision": REVISION,
        "desiredReadinessCode": DESIRED_READINESS,
        "diagnosticCodes": DIAGNOSTIC_CODES,
        "contentHashSha256": "",
        "administrativeAreaStableId": boundary.stable_id,
        "displayName": boundary.display_name,
        "sourceVintage": SOURCE_VINTAGE,
        "generatedAtUtc": GENERATED_AT_UTC,
        "coordinateFrame": copy.deepcopy(scope["coordinateFrame"]),
        "boundary": boundary.boundary,
        "legalAreaStableIds": [boundary.legal_area_stable_id],
        "operationalAreaStableIds": [],
        "semanticPlaceStableIds": [],
        "sources": module_sources(scope),
        "buildings": buildings,
        "roads": roads,
        "publicBusinesses": [],
        "unresolvedBuildingCount": 0,
    }
    apply_content_hash(document)
    return document


def build_audit(
    root: Path,
    scope_path: Path,
    scope: dict[str, Any],
    boundary_audit: dict[str, Any],
    building_audit: dict[str, Any],
    road_audit: dict[str, Any],
    toolchain: dict[str, Any],
) -> dict[str, Any]:
    document = {
        "schemaVersion": AUDIT_SCHEMA_VERSION,
        "scopeStableId": SCOPE_STABLE_ID,
        "revision": REVISION,
        "supersedesRevision": "northeast-seoul-administrative-dong-dioramas.r1",
        "supersededCandidateDispositionCode": "PreservedNotPromoted",
        "generatedAtUtc": GENERATED_AT_UTC,
        "sourceVintage": SOURCE_VINTAGE,
        "scopeDefinition": {
            "relativePath": scope_path.resolve().relative_to(root.resolve()).as_posix(),
            "sha256": sha256_file(scope_path),
        },
        "desiredReadinessCode": DESIRED_READINESS,
        "diagnosticCodes": DIAGNOSTIC_CODES,
        "distributionApproved": False,
        "traversalReady": False,
        "gameplayReady": False,
        "toolchain": copy.deepcopy(toolchain),
        "revisionBumpPolicy": copy.deepcopy(scope["revisionBumpPolicy"]),
        "boundary": boundary_audit,
        "buildings": building_audit,
        "roads": road_audit,
        "completionGate": {
            "allAdministrativeAreasHaveBoundary": boundary_audit["selectedBoundaryCount"] == 30,
            "allAdministrativeAreasHaveBuildings": all(
                count > 0 for count in building_audit["byAdministrativeArea"].values()
            ),
            "allAdministrativeAreasHaveRoadSegments": all(
                count > 0 for count in road_audit["byAdministrativeArea"].values()
            ),
            "currentAuthoritativeBoundaryAvailable": False,
            "publicDistributionApproved": False,
        },
        "contentHashSha256": "",
    }
    apply_content_hash(document)
    return document


def build_scope_manifest(
    scope: dict[str, Any],
    audit_path: Path,
    output_directory: Path,
    module_entries: list[dict[str, Any]],
    building_audit: dict[str, Any],
    road_audit: dict[str, Any],
    toolchain: dict[str, Any],
) -> dict[str, Any]:
    document = {
        "schemaVersion": OUTPUT_SCOPE_SCHEMA_VERSION,
        "scopeStableId": SCOPE_STABLE_ID,
        "revision": REVISION,
        "supersedesRevision": "northeast-seoul-administrative-dong-dioramas.r1",
        "supersededCandidateDispositionCode": "PreservedNotPromoted",
        "generatedAtUtc": GENERATED_AT_UTC,
        "sourceVintage": SOURCE_VINTAGE,
        "desiredReadinessCode": DESIRED_READINESS,
        "diagnosticCodes": DIAGNOSTIC_CODES,
        "distributionApproved": False,
        "traversalReady": False,
        "gameplayReady": False,
        "coordinateFrame": copy.deepcopy(scope["coordinateFrame"]),
        "toolchain": copy.deepcopy(toolchain),
        "revisionBumpPolicy": copy.deepcopy(scope["revisionBumpPolicy"]),
        "sources": module_sources(scope),
        "counts": {
            "administrativeAreaCount": len(module_entries),
            "buildingCount": sum(entry["buildingCount"] for entry in module_entries),
            "roadSegmentCount": sum(entry["roadSegmentCount"] for entry in module_entries),
            "scopeUnresolvedBuildingCount": building_audit["scopeUnresolvedCount"],
            "unresolvedRepresentativePointCount": building_audit["outsideAfterIntersection"],
            "quarantinedMalformedBuildingCount": building_audit["malformedQuarantine"],
            "quarantinedRoundedFootprintBuildingCount": building_audit[
                "roundedFootprintQuarantine"
            ],
            "ambiguousBuildingAssignmentCount": building_audit["ambiguous"],
            "buildingLegalAreaMismatchCount": building_audit["legalAreaMismatch"],
            "interiorRingOmittedBuildingCount": building_audit["interiorRingOmittedBuildings"],
            "interiorRingsOmitted": building_audit["interiorRingsOmitted"],
            "multipartOmittedBuildingCount": building_audit["multipartOmittedBuildings"],
            "v1ContractAssignmentAnchorOverrideCount": building_audit[
                "v1ContractAssignmentAnchorOverrides"
            ],
            "directedRoadLinkCount": road_audit["directedLinksIntersectingUnion"],
            "crossBoundaryDirectedRoadLinkCount": road_audit["crossBoundaryDirectedLinks"],
            "exactRoadBoundaryContainmentFailureSegmentCount": road_audit[
                "exactBoundaryContainmentFailureSegments"
            ],
            "roadBoundaryNumericToleranceMeters": road_audit[
                "boundaryContainmentToleranceMeters"
            ],
        },
        "audit": {
            "relativePath": audit_path.relative_to(output_directory).as_posix(),
            "sha256": sha256_file(audit_path),
        },
        "modules": module_entries,
        "contentHashSha256": "",
    }
    apply_content_hash(document)
    return document


def build(root: Path, scope_path: Path) -> dict[str, Any]:
    root = root.resolve()
    scope_path = scope_path.resolve()
    scope = load_scope(root, scope_path)
    dependencies = load_dependencies(root)
    toolchain = toolchain_receipt(root, scope_path, scope, dependencies)
    frame = frame_from_scope(scope)
    print(json.dumps({"stage": "ReadHistoricalAdministrativeBoundaries"}), file=sys.stderr, flush=True)
    boundaries, boundary_audit = read_boundaries(root, scope, frame, dependencies)
    print(json.dumps({"stage": "AssignOfficialBuildingGeometry"}), file=sys.stderr, flush=True)
    buildings, building_audit = read_buildings(root, scope, frame, boundaries, dependencies)
    print(json.dumps({"stage": "ClipNodeLinkRoads"}), file=sys.stderr, flush=True)
    roads, road_audit = read_roads(root, scope, frame, boundaries, dependencies)
    output_directory = repository_path(root, scope["output"]["repositoryRelativeDirectory"])
    output_directory.mkdir(parents=True, exist_ok=True)
    modules_directory = output_directory / "modules"
    modules_directory.mkdir(parents=True, exist_ok=True)
    changed_files = 0
    module_entries: list[dict[str, Any]] = []
    for boundary in boundaries:
        module = build_module(scope, boundary, buildings[boundary.code], roads[boundary.code])
        module_path = modules_directory / f"{boundary.code}.json"
        changed_files += int(write_json_atomic(module_path, module))
        module_entries.append(
            {
                "administrativeAreaStableId": boundary.stable_id,
                "displayName": boundary.display_name,
                "relativePath": module_path.relative_to(output_directory).as_posix(),
                "sha256": sha256_file(module_path),
                "contentHashSha256": module["contentHashSha256"],
                "buildingCount": len(buildings[boundary.code]),
                "roadSegmentCount": len(roads[boundary.code]),
                "unresolvedBuildingCount": 0,
            }
        )
    module_entries.sort(key=lambda item: item["administrativeAreaStableId"])
    audit = build_audit(
        root,
        scope_path,
        scope,
        boundary_audit,
        building_audit,
        road_audit,
        toolchain,
    )
    audit_path = output_directory / "audit.json"
    changed_files += int(write_json_atomic(audit_path, audit))
    manifest = build_scope_manifest(
        scope,
        audit_path,
        output_directory,
        module_entries,
        building_audit,
        road_audit,
        toolchain,
    )
    manifest_path = output_directory / "scope-manifest.json"
    changed_files += int(write_json_atomic(manifest_path, manifest))
    summary = verify(root, scope_path, check_sources=False)
    summary["changedFiles"] = changed_files
    return summary


def _verify_point(value: Any, code: str) -> tuple[float, float]:
    require(isinstance(value, dict), code)
    x = value.get("x")
    z = value.get("z")
    require(isinstance(x, (int, float)) and isinstance(z, (int, float)), code)
    require(math.isfinite(float(x)) and math.isfinite(float(z)), code)
    return float(x), float(z)


def verify(root: Path, scope_path: Path, *, check_sources: bool = True) -> dict[str, Any]:
    root = root.resolve()
    scope_path = scope_path.resolve()
    scope = load_scope(root, scope_path)
    dependencies = load_dependencies(root)
    expected_toolchain = toolchain_receipt(root, scope_path, scope, dependencies)
    if check_sources:
        for source in scope["sources"]:
            verify_frozen_file(root, source)
        verify_nodelink_extract(root, source_by_id(scope, "data-go-kr-15025526"))
    output_directory = repository_path(root, scope["output"]["repositoryRelativeDirectory"])
    manifest_path = output_directory / "scope-manifest.json"
    audit_path = output_directory / "audit.json"
    manifest = load_json(manifest_path)
    audit = load_json(audit_path)
    require(manifest.get("schemaVersion") == OUTPUT_SCOPE_SCHEMA_VERSION, "OutputScopeSchemaMismatch")
    require(audit.get("schemaVersion") == AUDIT_SCHEMA_VERSION, "OutputAuditSchemaMismatch")
    require(manifest.get("contentHashSha256") == content_hash(manifest), "OutputScopeContentHashMismatch")
    require(audit.get("contentHashSha256") == content_hash(audit), "OutputAuditContentHashMismatch")
    require(manifest.get("scopeStableId") == SCOPE_STABLE_ID, "OutputScopeStableIdMismatch")
    require(manifest.get("revision") == REVISION, "OutputScopeRevisionMismatch")
    require(
        manifest.get("supersedesRevision")
        == "northeast-seoul-administrative-dong-dioramas.r1"
        and audit.get("supersedesRevision")
        == "northeast-seoul-administrative-dong-dioramas.r1"
        and manifest.get("supersededCandidateDispositionCode") == "PreservedNotPromoted"
        and audit.get("supersededCandidateDispositionCode") == "PreservedNotPromoted",
        "OutputSupersededRevisionBoundaryMismatch",
    )
    require(manifest.get("desiredReadinessCode") == DESIRED_READINESS, "OutputScopeReadinessMismatch")
    require(manifest.get("diagnosticCodes") == DIAGNOSTIC_CODES, "OutputScopeDiagnosticsMismatch")
    require(
        manifest.get("distributionApproved") is False
        and manifest.get("traversalReady") is False
        and manifest.get("gameplayReady") is False,
        "OutputScopeAuthorityBoundaryInvalid",
    )
    require(manifest.get("audit", {}).get("sha256") == sha256_file(audit_path), "OutputAuditFileHashMismatch")
    require(
        audit.get("scopeDefinition")
        == {
            "relativePath": scope_path.relative_to(root).as_posix(),
            "sha256": sha256_file(scope_path),
        },
        "OutputAuditScopeDefinitionMismatch",
    )
    require(
        manifest.get("toolchain") == expected_toolchain
        and audit.get("toolchain") == expected_toolchain,
        "OutputToolchainReceiptMismatch",
    )
    require(
        manifest.get("revisionBumpPolicy") == scope["revisionBumpPolicy"]
        and audit.get("revisionBumpPolicy") == scope["revisionBumpPolicy"],
        "OutputRevisionBumpPolicyMismatch",
    )
    expected_sources = module_sources(scope)
    require(manifest.get("sources") == expected_sources, "OutputScopeSourcesMismatch")
    entries = manifest.get("modules", [])
    require(isinstance(entries, list) and len(entries) == 30, "OutputModuleCountMismatch")
    expected_ids = {f"region:kr:hjd:{code}" for code in EXPECTED_ADMINISTRATIVE_AREAS}
    require(
        {entry.get("administrativeAreaStableId") for entry in entries} == expected_ids,
        "OutputModuleCoverageMismatch",
    )
    global_building_ids: set[str] = set()
    total_buildings = 0
    total_roads = 0
    maximum_road_length = 0.0
    for entry in entries:
        relative_path = entry.get("relativePath", "")
        module_path = (output_directory / relative_path).resolve()
        try:
            module_path.relative_to(output_directory.resolve())
        except ValueError as exc:
            raise BatchGenerationError("OutputModulePathOutsideDirectory") from exc
        require(module_path.is_file() and not module_path.is_symlink(), "OutputModuleMissing")
        require(entry.get("sha256") == sha256_file(module_path), "OutputModuleFileHashMismatch")
        module = load_json(module_path)
        require(module.get("schemaVersion") == MODULE_SCHEMA_VERSION, "OutputModuleSchemaMismatch")
        require(module.get("projectionRevision") == REVISION, "OutputModuleRevisionMismatch")
        require(module.get("contentHashSha256") == content_hash(module), "OutputModuleContentHashMismatch")
        require(
            entry.get("contentHashSha256") == module["contentHashSha256"],
            "OutputModuleManifestContentHashMismatch",
        )
        area_id = entry["administrativeAreaStableId"]
        code = area_id.removeprefix("region:kr:hjd:")
        expected_name, expected_legal_code = EXPECTED_ADMINISTRATIVE_AREAS[code]
        require(module.get("administrativeAreaStableId") == area_id, "OutputModuleAreaMismatch")
        require(module.get("displayName") == expected_name, "OutputModuleDisplayNameMismatch")
        require(module.get("sourceVintage") == SOURCE_VINTAGE, "OutputModuleVintageMismatch")
        require(module.get("generatedAtUtc") == GENERATED_AT_UTC, "OutputModuleGeneratedAtMismatch")
        require(module.get("coordinateFrame") == scope["coordinateFrame"], "OutputModuleCoordinateFrameMismatch")
        require(module.get("desiredReadinessCode") == DESIRED_READINESS, "OutputModuleReadinessMismatch")
        require(module.get("diagnosticCodes") == DIAGNOSTIC_CODES, "OutputModuleDiagnosticsMismatch")
        require(module.get("sources") == expected_sources, "OutputModuleSourcesMismatch")
        require(
            module.get("legalAreaStableIds") == [f"region:kr:bjd:{expected_legal_code}"],
            "OutputModuleLegalAreaMismatch",
        )
        require(module.get("operationalAreaStableIds") == [], "OutputModuleUnexpectedOperationalArea")
        require(module.get("semanticPlaceStableIds") == [], "OutputModuleUnexpectedSemanticPlace")
        require(module.get("publicBusinesses") == [], "OutputModuleUnexpectedBusiness")
        require(module.get("unresolvedBuildingCount") == 0, "OutputModuleUnresolvedMustBeScopeLevel")
        boundary = module.get("boundary", [])
        require(isinstance(boundary, list) and len(boundary) >= 4, "OutputModuleBoundaryInvalid")
        for point in boundary:
            _verify_point(point, "OutputModuleBoundaryPointInvalid")
        boundary_geometry = dependencies.Polygon(
            [(float(point["x"]), float(point["z"])) for point in boundary]
        )
        require(
            not boundary_geometry.is_empty
            and boundary_geometry.is_valid
            and boundary_geometry.exterior.is_simple
            and boundary_geometry.area > 0.000001,
            "OutputModuleBoundaryGeometryInvalid",
        )
        buildings = module.get("buildings", [])
        roads = module.get("roads", [])
        require(isinstance(buildings, list) and len(buildings) > 0, "OutputModuleBuildingsMissing")
        require(isinstance(roads, list) and len(roads) > 0, "OutputModuleRoadsMissing")
        require(entry.get("buildingCount") == len(buildings), "OutputModuleBuildingCountMismatch")
        require(entry.get("roadSegmentCount") == len(roads), "OutputModuleRoadCountMismatch")
        local_building_ids: set[str] = set()
        for building in buildings:
            stable_id = building.get("buildingStableId", "")
            require(bool(stable_id) and stable_id not in local_building_ids, "OutputBuildingIdDuplicate")
            require(stable_id not in global_building_ids, "OutputBuildingAssignedToMultipleAreas")
            local_building_ids.add(stable_id)
            global_building_ids.add(stable_id)
            footprint = building.get("footprint", [])
            require(isinstance(footprint, list) and len(footprint) >= 4, "OutputBuildingFootprintInvalid")
            for point in footprint:
                _verify_point(point, "OutputBuildingFootprintPointInvalid")
            footprint_geometry = dependencies.Polygon(
                [(float(point["x"]), float(point["z"])) for point in footprint]
            )
            require(
                not footprint_geometry.is_empty
                and footprint_geometry.is_valid
                and footprint_geometry.exterior.is_simple
                and footprint_geometry.area > 0.000001,
                "OutputBuildingFootprintGeometryInvalid",
            )
            assignment_point = _verify_point(
                building.get("assignmentPoint"), "OutputBuildingAssignmentPointInvalid"
            )
            require(
                boundary_geometry.covers(dependencies.Point(assignment_point)),
                "OutputBuildingAssignmentPointOutsideBoundary",
            )
            require(
                building.get("assignmentMethodCode")
                == "SourceGeometryPointOnSurfaceWithinUniqueAdministrativeBoundary",
                "OutputBuildingAssignmentMethodInvalid",
            )
            require(
                building.get("assignmentConfidenceCode") == "UniqueHistoricalBoundaryMatch",
                "OutputBuildingAssignmentConfidenceInvalid",
            )
            require(
                building.get("assignmentBoundarySourceRevision")
                == "seoul-oa22160:file-modified:20231031:retrieved:20260912",
                "OutputBuildingBoundaryRevisionInvalid",
            )
        local_road_ids: set[str] = set()
        for road in roads:
            stable_id = road.get("roadStableId", "")
            require(bool(stable_id) and stable_id not in local_road_ids, "OutputRoadIdDuplicate")
            local_road_ids.add(stable_id)
            start = _verify_point(road.get("from"), "OutputRoadStartInvalid")
            end = _verify_point(road.get("to"), "OutputRoadEndInvalid")
            length = math.hypot(end[0] - start[0], end[1] - start[1])
            require(0.001 < length <= MAXIMUM_ROAD_SEGMENT_METERS + 0.000001, "OutputRoadLengthInvalid")
            require(
                boundary_geometry.buffer(
                    ROAD_BOUNDARY_NUMERIC_TOLERANCE_METERS
                ).covers(dependencies.LineString([start, end])),
                "OutputRoadOutsidePublishedBoundary",
            )
            maximum_road_length = max(maximum_road_length, length)
        total_buildings += len(buildings)
        total_roads += len(roads)
    counts = manifest.get("counts", {})
    require(counts.get("administrativeAreaCount") == 30, "OutputScopeAreaCountMismatch")
    require(counts.get("buildingCount") == total_buildings, "OutputScopeBuildingCountMismatch")
    require(counts.get("roadSegmentCount") == total_roads, "OutputScopeRoadCountMismatch")
    building_audit = audit.get("buildings", {})
    road_audit = audit.get("roads", {})
    require(building_audit.get("assigned") == total_buildings, "OutputAuditBuildingCountMismatch")
    require(road_audit.get("exportedTwoPointSegments") == total_roads, "OutputAuditRoadCountMismatch")
    require(
        building_audit.get("assigned") == EXPECTED_AUDIT_COUNTS["assignedBuildings"]
        and building_audit.get("outsideAfterIntersection")
        == EXPECTED_AUDIT_COUNTS["representativePointOutside"]
        and building_audit.get("malformedQuarantine")
        == EXPECTED_AUDIT_COUNTS["malformedBuildingQuarantine"]
        and building_audit.get("roundedFootprintQuarantine")
        == EXPECTED_AUDIT_COUNTS["roundedFootprintQuarantine"]
        and building_audit.get("ambiguous") == EXPECTED_AUDIT_COUNTS["ambiguousBuildings"]
        and building_audit.get("interiorRingOmittedBuildings")
        == EXPECTED_AUDIT_COUNTS["interiorRingBuildings"]
        and building_audit.get("interiorRingsOmitted")
        == EXPECTED_AUDIT_COUNTS["interiorRings"]
        and building_audit.get("multipartOmittedBuildings")
        == EXPECTED_AUDIT_COUNTS["multipartBuildings"]
        and building_audit.get("v1ContractAssignmentAnchorOverrides")
        == EXPECTED_AUDIT_COUNTS["v1ContractAssignmentAnchorOverrides"],
        "OutputFrozenBuildingAuditCountMismatch",
    )
    require(
        road_audit.get("directedLinksIntersectingUnion")
        == EXPECTED_AUDIT_COUNTS["directedNodeLinks"]
        and road_audit.get("administrativeDongLinkOccurrences")
        == EXPECTED_AUDIT_COUNTS["administrativeDongLinkOccurrences"]
        and road_audit.get("crossBoundaryDirectedLinks")
        == EXPECTED_AUDIT_COUNTS["crossBoundaryDirectedLinks"]
        and road_audit.get("exportedTwoPointSegments")
        == EXPECTED_AUDIT_COUNTS["exportedRoadSegments"]
        and road_audit.get("exactBoundaryContainmentFailureSegments")
        == EXPECTED_AUDIT_COUNTS["exactRoadBoundaryContainmentFailures"],
        "OutputFrozenRoadAuditCountMismatch",
    )
    expected_module_files = {Path(entry["relativePath"]).name for entry in entries}
    actual_module_files = {path.name for path in (output_directory / "modules").glob("*.json")}
    require(actual_module_files == expected_module_files, "OutputUnexpectedModuleFiles")
    return {
        "status": "PASS",
        "scopeStableId": SCOPE_STABLE_ID,
        "revision": REVISION,
        "administrativeAreaCount": len(entries),
        "buildingCount": total_buildings,
        "scopeUnresolvedBuildingCount": counts.get("scopeUnresolvedBuildingCount"),
        "roadSegmentCount": total_roads,
        "maximumRoadSegmentLengthMeters": round(maximum_road_length, 6),
        "historicalBoundaryBootstrapOnly": True,
        "desiredReadinessCode": DESIRED_READINESS,
        "distributionApproved": False,
        "scopeManifestSha256": sha256_file(manifest_path),
        "scopeManifestContentHashSha256": manifest["contentHashSha256"],
        "auditSha256": sha256_file(audit_path),
        "auditContentHashSha256": audit["contentHashSha256"],
    }


def self_test(root: Path) -> dict[str, Any]:
    dependencies = load_dependencies(root)
    left = dependencies.Polygon([(0, 0), (10, 0), (10, 10), (0, 10), (0, 0)])
    right = dependencies.Polygon([(10, 0), (20, 0), (20, 10), (10, 10), (10, 0)])
    point_left = dependencies.Polygon([(1, 1), (2, 1), (2, 2), (1, 2), (1, 1)]).representative_point()
    point_outside = dependencies.Polygon([(30, 1), (31, 1), (31, 2), (30, 2), (30, 1)]).representative_point()
    require(sum(polygon.covers(point_left) for polygon in (left, right)) == 1, "SelfTestUniqueAssignmentFailed")
    require(sum(polygon.covers(point_outside) for polygon in (left, right)) == 0, "SelfTestOutsideAssignmentFailed")
    shared_point = dependencies.Point(10, 5)
    require(sum(polygon.covers(shared_point) for polygon in (left, right)) == 2, "SelfTestAmbiguousBoundaryFailed")
    clipped = dependencies.LineString([(-5, 5), (25, 5)]).intersection(left)
    segments = list(_two_point_segments(list(clipped.coords), 4.0))
    require(len(segments) == 3, "SelfTestRoadSegmentationFailed")
    require(
        all(
            math.hypot(end["x"] - start["x"], end["z"] - start["z"]) <= 4.0
            for start, end in segments
        ),
        "SelfTestRoadSegmentLengthFailed",
    )
    rounded_self_intersection = dependencies.Polygon(
        [(0.0, 0.0), (1.0, 1.0), (0.0, 1.0), (1.0, 0.0), (0.0, 0.0)]
    )
    require(
        not rounded_self_intersection.is_valid
        and not rounded_self_intersection.exterior.is_simple,
        "SelfTestRoundedFootprintQuarantineFailed",
    )
    published_boundary = dependencies.Polygon(
        [(0.0, 0.0), (10.0, 0.0), (10.0, 10.0), (0.0, 10.0), (0.0, 0.0)]
    )
    outside_candidate = dependencies.LineString([(-0.056, 5.0), (5.0, 5.0)])
    final_road = outside_candidate.intersection(published_boundary)
    require(
        published_boundary.covers(final_road),
        "SelfTestPublishedBoundaryRoadReclipFailed",
    )
    first = {"schemaVersion": "test", "value": [2, 1], "contentHashSha256": ""}
    second = copy.deepcopy(first)
    apply_content_hash(first)
    apply_content_hash(second)
    require(first == second and first["contentHashSha256"] == content_hash(first), "SelfTestHashFailed")
    return {
        "status": "PASS",
        "uniqueAssignment": True,
        "outsideUnresolved": True,
        "ambiguousBoundaryDetected": True,
        "roadClipAndMaximumLength": True,
        "roundedFootprintQuarantine": True,
        "publishedBoundaryRoadReclip": True,
        "deterministicContentHash": True,
    }


def parse_arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("mode", choices=("build", "verify", "self-test"))
    parser.add_argument("--root", type=Path, default=DEFAULT_REPOSITORY_ROOT)
    parser.add_argument("--scope", type=Path)
    return parser.parse_args()


def main() -> int:
    arguments = parse_arguments()
    root = arguments.root.resolve()
    scope_path = (
        arguments.scope.resolve()
        if arguments.scope is not None
        else repository_path(root, DEFAULT_SCOPE_RELATIVE)
    )
    try:
        if arguments.mode == "build":
            result = build(root, scope_path)
        elif arguments.mode == "verify":
            result = verify(root, scope_path)
        else:
            result = self_test(root)
        print(json.dumps(result, ensure_ascii=False, indent=2, allow_nan=False))
        return 0
    except BatchGenerationError as exc:
        print(
            json.dumps(
                {"status": "FAIL", "errorCode": str(exc)},
                ensure_ascii=False,
                indent=2,
            ),
            file=sys.stderr,
        )
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
