#!/usr/bin/env python3
"""동결 OA-23081 횡단보도 점을 30개 역사 행정동 후보에 결정적으로 결속한다.

이 도구의 결과는 로컬 비공개 검토 후보일 뿐이다. 현행 행정동 경계, 실제
횡단보도 면, 보도 연결, 신호 현시, 통행 또는 Unity 권위를 만들지 않는다.
"""

from __future__ import annotations

import argparse
import copy
import hashlib
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
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Iterable, Sequence
from unittest import mock
from xml.etree import ElementTree


DEFAULT_REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
DEFAULT_SCOPE_RELATIVE = Path(
    "eng/world-seedbeds/administrative-dong-dioramas/"
    "northeast-seoul-rider-crosswalk.g3a.r2.json"
)
SCOPE_SCHEMA_VERSION = "administrative-dong-crosswalk-generation-scope.v1"
SCOPE_STABLE_ID = "scope:administrative-dong-crosswalk:northeast-seoul-rider:g3a:r2"
REVISION = "northeast-seoul-admin-dong-crosswalk-candidate.g3a.r2"
CANDIDATE_SCHEMA_VERSION = "administrative-dong-crosswalk-candidate.v1"
MANIFEST_SCHEMA_VERSION = "administrative-dong-crosswalk-candidate-manifest.v1"
AUDIT_SCHEMA_VERSION = "administrative-dong-crosswalk-candidate-audit.v1"
COMPLETE_SCHEMA_VERSION = "administrative-dong-crosswalk-candidate-complete.v1"
DESIGN_DOCUMENT_RELATIVE = Path(
    "docs/AI/Planning/시스템/PLAN-SYSTEM-ADMIN-DONG-DIORAMA/"
    "administrative-dong-crosswalk-candidate.implementation.r8.md"
)
WORK_ORDER_RELATIVE = Path(
    "docs/AI/Planning/시스템/PLAN-SYSTEM-ADMIN-DONG-DIORAMA/"
    "administrative-dong-crosswalk-candidate.e7-work-order.json"
)
DESIGN_HASH_SHA256 = "005DBDF1C5B501362FF0445C72F3BED91FAE2CC99A21372C6FD0AA3F09C18466"
DESIGN_REVISION = "administrative-dong-diorama:crosswalk-candidate.g3a.r2"
GENERATED_AT_UTC = "2026-09-15T00:00:00Z"
COORDINATE_STATUS = "EmpiricallyValidatedCandidateNotSourceDeclared"
UNIQUE_ASSIGNMENT = "UniqueHistoricalBoundaryCover"
MULTIPLE_ASSIGNMENT = "UnresolvedMultipleHistoricalBoundaryCover"
EXPECTED_HEADERS = (
    "연번",
    "자치구",
    "횡단보도관리번호",
    "교차로관리번호",
    "횡단보도종류",
    "보행등유무",
    "교차로명",
    "X좌표",
    "Y좌표",
)
EXPECTED_COUNTS = {
    "sourceRows": 21_776,
    "coordinatePresentRows": 21_775,
    "coordinateMissingRows": 1,
    "uniquelyAssignedRows": 1_533,
    "unresolvedMultipleBoundaryRows": 0,
    "outsideScopeRows": 20_242,
    "candidateRows": 1_533,
    "sourceDistrictSpatialAssignmentConflictRows": 21,
}
EXPECTED_FEASIBILITY_DIGEST = "DC34A66F2390281AA5BF4D70F77ABE87AEEBCFEC97328B5AE5C6D5D7FA62DDC3"
EXPECTED_DISTRIBUTION = {
    "1121574000": 36,
    "1121575000": 31,
    "1121576000": 37,
    "1121577000": 37,
    "1123056000": 75,
    "1123057000": 21,
    "1123060000": 75,
    "1123061000": 48,
    "1123065000": 61,
    "1123066000": 81,
    "1123072000": 33,
    "1123073000": 46,
    "1123074000": 47,
    "1123075000": 40,
    "1126052000": 50,
    "1126054000": 41,
    "1126055000": 34,
    "1126056500": 54,
    "1126057000": 47,
    "1126057500": 50,
    "1126058000": 19,
    "1126059000": 45,
    "1126060000": 21,
    "1126061000": 59,
    "1126062000": 78,
    "1126063000": 57,
    "1126065500": 133,
    "1126066000": 26,
    "1126068000": 108,
    "1126069000": 43,
}
EXPECTED_CONFLICT_DISTRIBUTION = {
    "1121574000": 0,
    "1121575000": 0,
    "1121576000": 5,
    "1121577000": 0,
    "1123056000": 0,
    "1123057000": 0,
    "1123060000": 5,
    "1123061000": 2,
    "1123065000": 3,
    "1123066000": 0,
    "1123072000": 0,
    "1123073000": 0,
    "1123074000": 0,
    "1123075000": 1,
    "1126052000": 0,
    "1126054000": 3,
    "1126055000": 0,
    "1126056500": 0,
    "1126057000": 0,
    "1126057500": 0,
    "1126058000": 0,
    "1126059000": 0,
    "1126060000": 0,
    "1126061000": 0,
    "1126062000": 0,
    "1126063000": 2,
    "1126065500": 0,
    "1126066000": 0,
    "1126068000": 0,
    "1126069000": 0,
}
EXPECTED_CONFLICT_PAIRS = {
    "광진구>중랑구": 3,
    "노원구>중랑구": 2,
    "성동구>동대문구": 10,
    "성북구>동대문구": 1,
    "중랑구>광진구": 5,
}
EXPECTED_BOROUGH_BY_CODE_PREFIX = {
    "11215": "광진구",
    "11230": "동대문구",
    "11260": "중랑구",
}
EXPECTED_CONFLICT_DISTANCE_RANGE_METERS = {"minimum": 0.286, "maximum": 22.076}
SOURCE_DISTRICT_CONFLICT_CODE = "SourceDistrictSpatialAssignmentConflict"
EXPECTED_ALTERNATIVE_CRS_RESULTS = [
    {
        "coordinateReference": "EPSG:5181",
        "minimumDistanceMeters": 83791.5854,
        "sourceSheetRow": 11913,
        "crosswalkManagementNumber": "06-0000005624",
        "thresholdCode": "GreaterThan83Kilometers",
    },
    {
        "coordinateReference": "EPSG:5174",
        "minimumDistanceMeters": 84094.5067,
        "sourceSheetRow": 11913,
        "crosswalkManagementNumber": "06-0000005624",
        "thresholdCode": "GreaterThan83Kilometers",
    },
    {
        "coordinateReference": "EPSG:2097",
        "minimumDistanceMeters": 84100.4069,
        "sourceSheetRow": 11913,
        "crosswalkManagementNumber": "06-0000005624",
        "thresholdCode": "GreaterThan83Kilometers",
    },
    {
        "coordinateReference": "EPSG:5179",
        "minimumDistanceMeters": 1581801.1322,
        "sourceSheetRow": 8482,
        "crosswalkManagementNumber": "06-0000046224",
        "thresholdCode": "GreaterThan1580Kilometers",
    },
]
HISTORICAL_BOUNDARY_CONVERSION_BASE = {
    "sourceCoordinateReference": "EPSG:5181",
    "assignmentCoordinateReferenceCandidate": "EPSG:5186",
    "xOffsetMeters": 0.0,
    "yOffsetMeters": 100000.0,
    "methodCode": "SameKgd2002CentralBeltFalseNorthingOffsetOnly",
    "rotationApplied": False,
    "scaleApplied": False,
    "inferredTranslationApplied": False,
}
ALTERNATIVE_CRS_EVIDENCE_BASE = {
    "methodCode": "WholeFileAlternativeCrsToEpsg5186ThenEuclideanDistanceToOfficialStationAnchor",
    "coordinatePresentRowsEvaluated": 21775,
    "results": EXPECTED_ALTERNATIVE_CRS_RESULTS,
}
COORDINATE_VALIDATION_BASE = {
    "statusCode": COORDINATE_STATUS,
    "sourceDeclared": False,
    "methodCode": "OfficialStationAnchorAndWholeFileAlternativeCrsEvidence",
    "stationAnchorProvenance": {
        "stationStableId": "station:kr:kric:s1107:0722",
        "stationName": "사가정",
        "stationNumber": "0722",
        "operatorName": "서울교통공사",
        "dataReferenceDate": "2024-12-31",
        "sourceDatasetId": "data-go-kr-15093755",
        "officialPageUrl": "https://www.data.go.kr/data/15093755/fileData.do",
        "selectionRelativePath": "artifacts/local/public-data/station-reference-20260913-r1/selected.json",
        "selectionHashSha256": "0EFE8A791CBD9D0171997B09ACD71A24C8E7D39F50FA62AD8D22E57CD9768CEC",
        "sourceWorkbookRelativePath": "artifacts/local/public-data/station-reference-20260913-r1/전체_도시철도역사정보_20260630.xlsx",
        "sourceWorkbookHashSha256": "CDF1D84A7E5C898B2AACD622783BA8BA9AF35C40BEE0561DC97D55CE8E063F94",
    },
    "sagajeongStationWgs84": {
        "longitude": 127.088502,
        "latitude": 37.580912,
    },
    "anchorEpsg5186": {"x": 207817.3774, "y": 553488.0505},
    "crosswalkManagementNumber": "06-0000016264",
    "sourceSheetRow": 21271,
    "distanceMeters": 8.0946,
    "alternativeCrsEvidenceHashSha256": "",
    "receiptCoordinateMetadataDispositionCode": "PriorInferenceNotSourceDeclaration",
}
AUTHORITY_FLAGS = {
    "privateReviewOnly": True,
    "historicalBoundaryBootstrapOnly": True,
    "observationCandidateOnly": True,
    "currentAdministrativeBoundaryEstablished": False,
    "sourceDeclaredCoordinateReference": False,
    "crosswalkGeometryEstablished": False,
    "stopLineEstablished": False,
    "signalPhaseTimingEstablished": False,
    "sidewalkConnectionEstablished": False,
    "distributionApproved": False,
    "publicDisplayAllowed": False,
    "runtimeAuthorized": False,
    "traversalReady": False,
    "gameplayReady": False,
    "unityApplyAllowed": False,
}
CANDIDATE_SET_HASH_HEADER_FIELD_ORDER = [
    "candidateSchemaVersion",
    "revision",
    "designHashSha256",
    "oa23081SourceHashSha256",
    "oa23081AcquisitionReceiptHashSha256",
    "oa22160BoundaryArchiveHashSha256",
    "r2ScopeDefinitionHashSha256",
    "r2ScopeManifestHashSha256",
    "actualGenerationScopeHashSha256",
    "officialStationAnchorSelectionHashSha256",
    "officialStationAnchorWorkbookHashSha256",
    "generatorHashSha256",
    "coordinateValidationHashSha256",
    "historicalBoundaryConversionHashSha256",
    "feasibilityDigestSha256",
]
CANDIDATE_SET_HASH_CANDIDATE_FIELD_ORDER = [
    "sourceFeatureKey",
    "administrativeAreaStableIdOrEmpty",
    "assignmentStateCode",
    "candidateQualityCode",
    "sourceBoroughName",
    "spatialAssignmentBoroughNameOrEmpty",
    "distanceToAssignedHistoricalBoundaryMetersCanonicalJson",
    "candidateAdministrativeAreaStableIdsCanonicalJson",
    "crosswalkManagementNumber",
    "intersectionReferenceCanonicalJson",
    "crosswalkType",
    "pedestrianSignalPresentLowercase",
    "sourcePointEpsg5186CanonicalJson",
    "commonEnuMillimetersCanonicalJson",
    "sourceDistrictSpatialAssignmentConflictLowercase",
    "qualityDiagnosticCodesCanonicalJson",
    "ownershipBasisCode",
]


class CrosswalkCandidateError(RuntimeError):
    """호출자와 검증 도구가 재사용할 수 있는 안정 오류 코드."""


def require(condition: bool, code: str) -> None:
    if not condition:
        raise CrosswalkCandidateError(code)


def lexical_absolute(path: str | Path) -> Path:
    return Path(os.path.abspath(os.fspath(path)))


def same_path(left: Path, right: Path) -> bool:
    return os.path.normcase(os.fspath(left)) == os.path.normcase(os.fspath(right))


def path_is_within(root: Path, candidate: Path) -> bool:
    try:
        return same_path(Path(os.path.commonpath((root, candidate))), root)
    except ValueError:
        return False


def is_reparse_path(path: Path) -> bool:
    is_junction = getattr(os.path, "isjunction", lambda _: False)
    try:
        attributes = getattr(os.lstat(path), "st_file_attributes", 0)
    except OSError:
        attributes = 0
    return (
        path.is_symlink()
        or bool(is_junction(path))
        or bool(attributes & getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0))
    )


def require_repository_root(root: Path) -> Path:
    lexical = lexical_absolute(root)
    require(lexical.is_dir(), "RepositoryRootMissing")
    for component in reversed((lexical, *lexical.parents)):
        if os.path.lexists(component):
            require(not is_reparse_path(component), "RepositoryRootReparsePathRejected")
    resolved = Path(os.path.realpath(lexical))
    require(same_path(lexical, resolved), "RepositoryRootReparsePathRejected")
    return lexical


def repository_path(root: Path, relative: str | Path) -> Path:
    root = lexical_absolute(root)
    value = Path(relative)
    candidate = lexical_absolute(value if value.is_absolute() else root / value)
    require(path_is_within(root, candidate), "PathOutsideRepository")
    current = root
    require(os.path.lexists(current) and not is_reparse_path(current), "RepositoryRootInvalid")
    for component in candidate.relative_to(root).parts:
        current /= component
        if not os.path.lexists(current):
            break
        require(not is_reparse_path(current), "RepositoryReparsePathRejected")
        require(
            path_is_within(root, Path(os.path.realpath(current))),
            "ResolvedPathOutsideRepository",
        )
    return candidate


def require_plain_path(path: Path, code: str) -> None:
    require(path.exists() and not is_reparse_path(path), code)


def ensure_plain_directory(root: Path, path: Path, code: str) -> Path:
    root = require_repository_root(root)
    path = repository_path(root, path)
    path.mkdir(parents=True, exist_ok=True)
    path = repository_path(root, path)
    require(path.is_dir() and not is_reparse_path(path), code)
    require(same_path(Path(os.path.realpath(path)), path), code)
    return path


def sha256_bytes(value: bytes) -> str:
    return hashlib.sha256(value).hexdigest().upper()


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest().upper()


def canonical_json_bytes(value: Any) -> bytes:
    return json.dumps(
        value,
        ensure_ascii=False,
        allow_nan=False,
        sort_keys=True,
        separators=(",", ":"),
    ).encode("utf-8")


def canonical_json_text(value: Any) -> str:
    return canonical_json_bytes(value).decode("utf-8")


def pretty_json_bytes(value: Any) -> bytes:
    return (
        json.dumps(value, ensure_ascii=False, allow_nan=False, indent=2) + "\n"
    ).encode("utf-8")


def document_content_hash(document: dict[str, Any]) -> str:
    candidate = copy.deepcopy(document)
    candidate["contentHashSha256"] = ""
    return sha256_bytes(canonical_json_bytes(candidate))


def apply_content_hash(document: dict[str, Any]) -> None:
    document["contentHashSha256"] = ""
    document["contentHashSha256"] = document_content_hash(document)


def expected_historical_boundary_conversion() -> dict[str, Any]:
    value = copy.deepcopy(HISTORICAL_BOUNDARY_CONVERSION_BASE)
    value["contentHashSha256"] = ""
    apply_content_hash(value)
    return value


def expected_alternative_crs_evidence() -> dict[str, Any]:
    value = copy.deepcopy(ALTERNATIVE_CRS_EVIDENCE_BASE)
    value["contentHashSha256"] = ""
    apply_content_hash(value)
    return value


def expected_coordinate_validation() -> dict[str, Any]:
    alternative = expected_alternative_crs_evidence()
    value = copy.deepcopy(COORDINATE_VALIDATION_BASE)
    value["alternativeCrsEvidenceHashSha256"] = alternative["contentHashSha256"]
    value["contentHashSha256"] = ""
    apply_content_hash(value)
    return value


def load_json(path: Path) -> dict[str, Any]:
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        raise CrosswalkCandidateError(f"JsonReadFailed:{path.name}") from exc
    require(isinstance(value, dict), f"JsonRootInvalid:{path.name}")
    return value


@dataclass(frozen=True)
class Dependencies:
    shapefile: Any
    CRS: Any
    Transformer: Any
    translate: Any
    Point: Any
    Polygon: Any
    MultiPolygon: Any
    shape: Any
    unary_union: Any
    pyshp_version: str
    pyproj_version: str
    shapely_version: str
    geos_version: str
    proj_version: str
    epsg_database_version: str


def load_dependencies(root: Path) -> Dependencies:
    runtime = root / "artifacts/local/public-data/gis-runtime-r1"
    if str(runtime) not in sys.path:
        sys.path.insert(0, str(runtime))
    try:
        import pyproj
        import shapefile
        import shapely
        from pyproj import CRS, Transformer
        from shapely.affinity import translate
        from shapely.geometry import MultiPolygon, Point, Polygon, shape
        from shapely.ops import unary_union
    except (ModuleNotFoundError, ImportError) as exc:
        raise CrosswalkCandidateError("SpatialGenerationDependencyMissing") from exc
    return Dependencies(
        shapefile,
        CRS,
        Transformer,
        translate,
        Point,
        Polygon,
        MultiPolygon,
        shape,
        unary_union,
        shapefile.__version__,
        pyproj.__version__,
        shapely.__version__,
        shapely.geos_version_string,
        pyproj.proj_version_str,
        pyproj.database.get_database_metadata("EPSG.VERSION"),
    )


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


@dataclass(frozen=True)
class Boundary:
    code: str
    stable_id: str
    display_name: str
    borough_name: str
    geometry: Any


@dataclass(frozen=True)
class SourceRow:
    sheet_row: int
    serial_number: str
    district: str
    crosswalk_management_number: str
    intersection_management_number: str | None
    crosswalk_type: str
    pedestrian_signal_present: bool
    intersection_name: str | None
    x: float | None
    y: float | None


def normalized_dong_name(value: str) -> str:
    compact = value.replace("서울특별시", "").replace("광진구", "")
    compact = compact.replace("동대문구", "").replace("중랑구", "")
    return compact.replace("제", "").replace("·", ".").replace(" ", "")


def source_by_role(scope: dict[str, Any], role: str) -> dict[str, Any]:
    matches = [item for item in scope["sources"] if item.get("role") == role]
    require(len(matches) == 1, f"ScopeSourceMissing:{role}")
    return matches[0]


def verify_frozen_file(root: Path, source: dict[str, Any]) -> Path:
    path = repository_path(root, source["repositoryRelativePath"])
    require(path.is_file() and not is_reparse_path(path), f"SourceMissing:{source['role']}")
    require(path.stat().st_size == source["byteLength"], f"SourceLengthMismatch:{source['role']}")
    require(sha256_file(path) == source["contentHashSha256"], f"SourceHashMismatch:{source['role']}")
    return path


def load_scope(
    root: Path, scope_path: Path, dependencies: Dependencies
) -> tuple[dict[str, Any], dict[str, str]]:
    root = require_repository_root(root)
    scope_path = repository_path(root, scope_path)
    require(scope_path.is_file() and not is_reparse_path(scope_path), "ScopePathInvalid")
    scope = load_json(scope_path)
    require(scope.get("schemaVersion") == SCOPE_SCHEMA_VERSION, "ScopeSchemaMismatch")
    require(scope.get("scopeStableId") == SCOPE_STABLE_ID, "ScopeStableIdMismatch")
    require(scope.get("revision") == REVISION, "ScopeRevisionMismatch")
    require(scope.get("generatedAtUtc") == GENERATED_AT_UTC, "ScopeGeneratedAtMismatch")
    require(
        scope.get("supersedesRevision")
        == "northeast-seoul-admin-dong-crosswalk-candidate.g3a.r1"
        and scope.get("supersededCandidateDispositionCode") == "PreservedNotPromoted",
        "ScopeSupersessionMismatch",
    )
    require(scope.get("designDocumentRef") == DESIGN_DOCUMENT_RELATIVE.as_posix(), "ScopeDesignRefMismatch")
    require(scope.get("designHashSha256") == DESIGN_HASH_SHA256, "ScopeDesignHashMismatch")
    require(scope.get("coordinateReferenceStatus") == COORDINATE_STATUS, "ScopeCoordinateStatusMismatch")
    require(scope.get("authorityFlags") == AUTHORITY_FLAGS, "ScopeAuthorityBoundaryInvalid")
    require(scope.get("expectedCounts") == EXPECTED_COUNTS, "ScopeExpectedCountsMismatch")
    require(scope.get("expectedDistribution") == EXPECTED_DISTRIBUTION, "ScopeExpectedDistributionMismatch")
    require(
        scope.get("expectedConflictDistribution") == EXPECTED_CONFLICT_DISTRIBUTION,
        "ScopeExpectedConflictDistributionMismatch",
    )
    require(
        scope.get("expectedConflictPairs") == EXPECTED_CONFLICT_PAIRS,
        "ScopeExpectedConflictPairsMismatch",
    )
    require(
        scope.get("expectedConflictDistanceRangeMeters")
        == EXPECTED_CONFLICT_DISTANCE_RANGE_METERS,
        "ScopeExpectedConflictDistanceRangeMismatch",
    )
    require(
        scope.get("expectedFeasibilityDigestSha256")
        == EXPECTED_FEASIBILITY_DIGEST,
        "ScopeFeasibilityDigestMismatch",
    )
    require(
        scope.get("historicalBoundaryConversion")
        == expected_historical_boundary_conversion(),
        "ScopeHistoricalBoundaryConversionMismatch",
    )
    require(
        scope.get("alternativeCrsEvidence") == expected_alternative_crs_evidence(),
        "ScopeAlternativeCrsEvidenceMismatch",
    )
    require(
        scope.get("coordinateFrame")
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

    design_path = repository_path(root, DESIGN_DOCUMENT_RELATIVE)
    require(sha256_file(design_path) == DESIGN_HASH_SHA256, "DesignDocumentHashMismatch")
    work_order = load_json(repository_path(root, WORK_ORDER_RELATIVE))
    planning_gate = work_order.get("planningGate", {})
    require(
        planning_gate.get("statusCode") == "Approved"
        and planning_gate.get("designDocumentRef")
        == DESIGN_DOCUMENT_RELATIVE.as_posix()
        and planning_gate.get("designRevision") == DESIGN_REVISION
        and planning_gate.get("designHashSha256") == DESIGN_HASH_SHA256,
        "WorkOrderPlanningGateMismatch",
    )

    coordinate_validation = scope.get("coordinateValidation")
    require(isinstance(coordinate_validation, dict), "ScopeCoordinateValidationMissing")
    require(
        coordinate_validation == expected_coordinate_validation(),
        "ScopeCoordinateValidationMismatch",
    )

    expected_toolchain = scope.get("output", {}).get("toolchain", {})
    generator_path = repository_path(root, expected_toolchain.get("generatorRelativePath", ""))
    actual_toolchain = {
        "generatorRelativePath": generator_path.relative_to(root).as_posix(),
        "generatorSha256": sha256_file(generator_path),
        "pythonVersion": f"{sys.version_info.major}.{sys.version_info.minor}.{sys.version_info.micro}",
        "pyshpVersion": dependencies.pyshp_version,
        "pyprojVersion": dependencies.pyproj_version,
        "shapelyVersion": dependencies.shapely_version,
        "geosVersion": dependencies.geos_version,
        "projVersion": dependencies.proj_version,
        "epsgDatabaseVersion": dependencies.epsg_database_version,
        "candidateCoordinatePrecisionMeters": 0.001,
        "boundaryNorthingOffsetMeters": 100000.0,
    }
    require(expected_toolchain == actual_toolchain, "ScopeToolchainMismatch")

    areas = scope.get("administrativeAreas")
    require(isinstance(areas, list) and len(areas) == 30, "ScopeAdministrativeAreasInvalid")
    area_ids = [item.get("administrativeAreaStableId") for item in areas]
    require(area_ids == sorted(area_ids), "ScopeAdministrativeAreaOrderInvalid")
    codes = [str(value).removeprefix("region:kr:hjd:") for value in area_ids]
    require(codes == sorted(EXPECTED_DISTRIBUTION), "ScopeAdministrativeAllowListMismatch")
    require(len(set(area_ids)) == 30, "ScopeAdministrativeAreaDuplicate")
    for area, code in zip(areas, codes, strict=True):
        require(
            area.get("boroughName") == EXPECTED_BOROUGH_BY_CODE_PREFIX[code[:5]],
            f"ScopeAdministrativeBoroughMismatch:{code}",
        )

    sources = scope.get("sources")
    require(isinstance(sources, list) and len(sources) == 7, "ScopeSourcesInvalid")
    source_hashes: dict[str, str] = {}
    for role in (
        "oa23081Xlsx",
        "oa23081AcquisitionReceipt",
        "oa22160BoundaryArchive",
        "r2ScopeDefinition",
        "r2ScopeManifest",
        "officialStationAnchorSelection",
        "officialStationAnchorWorkbook",
    ):
        source = source_by_role(scope, role)
        path = verify_frozen_file(root, source)
        source_hashes[role] = sha256_file(path)

    r2_scope = load_json(repository_path(root, source_by_role(scope, "r2ScopeDefinition")["repositoryRelativePath"]))
    r2_area_ids = sorted(
        item.get("administrativeAreaStableId")
        for item in r2_scope.get("administrativeAreas", [])
    )
    require(r2_area_ids == area_ids, "R2ScopeAdministrativeAllowListMismatch")
    r2_manifest = load_json(repository_path(root, source_by_role(scope, "r2ScopeManifest")["repositoryRelativePath"]))
    module_area_ids = sorted(item.get("administrativeAreaStableId") for item in r2_manifest.get("modules", []))
    require(module_area_ids == area_ids, "R2ManifestAdministrativeAllowListMismatch")
    require(r2_manifest.get("distributionApproved") is False, "R2ManifestAuthorityChanged")
    station_selection = load_json(
        repository_path(
            root,
            source_by_role(scope, "officialStationAnchorSelection")[
                "repositoryRelativePath"
            ],
        )
    )
    station_rows = [
        item
        for item in station_selection.get("rows", [])
        if item.get("StationStableId") == "station:kr:kric:s1107:0722"
    ]
    require(
        len(station_rows) == 1
        and station_rows[0].get("StationNumber") == "0722"
        and station_rows[0].get("StationName") == "사가정"
        and station_rows[0].get("OperatorName") == "서울교통공사"
        and station_rows[0].get("DataReferenceDate")
        == "2024-12-31T00:00:00+00:00"
        and station_rows[0].get("Latitude") == 37.580912
        and station_rows[0].get("Longitude") == 127.088502,
        "OfficialStationAnchorSelectionMismatch",
    )
    require(
        station_selection.get("sourceHashSha256", "").upper()
        == source_hashes["officialStationAnchorWorkbook"]
        and scope["coordinateValidation"]["stationAnchorProvenance"]
        ["selectionHashSha256"]
        == source_hashes["officialStationAnchorSelection"]
        and scope["coordinateValidation"]["stationAnchorProvenance"]
        ["sourceWorkbookHashSha256"]
        == source_hashes["officialStationAnchorWorkbook"],
        "OfficialStationAnchorLineageMismatch",
    )

    output = scope.get("output", {})
    require(
        output.get("repositoryRelativeDirectory")
        == "artifacts/local/public-data/admin-dong-crosswalk-northeast-seoul-20260915-g3a-r2"
        and output.get("generationDirectoryName") == "generations"
        and output.get("candidateSchemaVersion") == CANDIDATE_SCHEMA_VERSION,
        "ScopeOutputContractMismatch",
    )
    require(
        output.get("persistenceDatasetId")
        == "northeast-seoul-admin-dong-crosswalk-candidate-g3a-r2",
        "ScopePersistenceDatasetMismatch",
    )
    return scope, source_hashes


def _xml_name(local_name: str) -> str:
    return "{http://schemas.openxmlformats.org/spreadsheetml/2006/main}" + local_name


def _relationship_name(local_name: str) -> str:
    return "{http://schemas.openxmlformats.org/package/2006/relationships}" + local_name


def _office_relationship_name(local_name: str) -> str:
    return "{http://schemas.openxmlformats.org/officeDocument/2006/relationships}" + local_name


def _required_zip_entry(archive: zipfile.ZipFile, name: str) -> bytes:
    try:
        return archive.read(name)
    except KeyError as exc:
        raise CrosswalkCandidateError(f"CrosswalkXlsxEntryMissing:{name}") from exc


def _column_index(reference: str) -> int:
    value = 0
    letters = 0
    for character in reference:
        if not "A" <= character <= "Z":
            break
        value = value * 26 + ord(character) - ord("A") + 1
        letters += 1
    require(letters > 0, "CrosswalkCellReferenceInvalid")
    return value - 1


def _read_shared_strings(archive: zipfile.ZipFile) -> list[str]:
    root = ElementTree.fromstring(_required_zip_entry(archive, "xl/sharedStrings.xml"))
    return [
        "".join(text.text or "" for text in item.iter(_xml_name("t")))
        for item in root.iter(_xml_name("si"))
    ]


def _read_row(row: Any, shared_strings: Sequence[str]) -> list[str]:
    values = [""] * len(EXPECTED_HEADERS)
    for cell in row.findall(_xml_name("c")):
        require(cell.find(_xml_name("f")) is None, "CrosswalkFormulaUnexpected")
        reference = cell.attrib.get("r", "")
        column = _column_index(reference)
        require(0 <= column < len(values), "CrosswalkUnexpectedColumn")
        cell_type = cell.attrib.get("t")
        raw_element = cell.find(_xml_name("v"))
        raw = "" if raw_element is None or raw_element.text is None else raw_element.text
        if cell_type == "s":
            try:
                shared_index = int(raw)
            except ValueError as exc:
                raise CrosswalkCandidateError("CrosswalkSharedStringIndexInvalid") from exc
            require(0 <= shared_index < len(shared_strings), "CrosswalkSharedStringIndexInvalid")
            value = shared_strings[shared_index]
        elif cell_type == "inlineStr":
            value = "".join(text.text or "" for text in cell.iter(_xml_name("t")))
        else:
            require(cell_type in (None, "str", "n"), "CrosswalkCellTypeChanged")
            value = raw
        values[column] = value.strip()
    return values


def read_crosswalk_rows(path: Path) -> tuple[list[SourceRow], dict[str, Any]]:
    with zipfile.ZipFile(path, "r") as archive:
        names = archive.namelist()
        require(len(names) == len(set(names)), "CrosswalkXlsxDuplicateEntry")
        workbook = ElementTree.fromstring(_required_zip_entry(archive, "xl/workbook.xml"))
        relationships = ElementTree.fromstring(
            _required_zip_entry(archive, "xl/_rels/workbook.xml.rels")
        )
        sheets = list(workbook.iter(_xml_name("sheet")))
        require(len(sheets) == 1, "CrosswalkWorksheetCountChanged")
        require(
            sheets[0].attrib.get("name") == "교차로 및 횡단보도 시설 위치정보",
            "CrosswalkWorksheetChanged",
        )
        relationship_id = sheets[0].attrib.get(_office_relationship_name("id"), "")
        targets = [
            item.attrib.get("Target", "")
            for item in relationships.iter(_relationship_name("Relationship"))
            if item.attrib.get("Id") == relationship_id
        ]
        require(len(targets) == 1 and targets[0], "CrosswalkWorksheetRelationshipInvalid")
        target = targets[0].replace("\\", "/").lstrip("/")
        worksheet_name = target if target.startswith("xl/") else "xl/" + target
        worksheet = ElementTree.fromstring(_required_zip_entry(archive, worksheet_name))
        shared_strings = _read_shared_strings(archive)
        rows = list(worksheet.iter(_xml_name("row")))

    require(len(rows) == EXPECTED_COUNTS["sourceRows"] + 1, "CrosswalkWorksheetRowCountChanged")
    require(tuple(_read_row(rows[0], shared_strings)) == EXPECTED_HEADERS, "CrosswalkHeadersChanged")
    result: list[SourceRow] = []
    missing_coordinates = 0
    previous_sheet_row = 1
    for row in rows[1:]:
        try:
            sheet_row = int(row.attrib.get("r", ""))
        except ValueError as exc:
            raise CrosswalkCandidateError("CrosswalkSheetRowInvalid") from exc
        require(sheet_row > previous_sheet_row, "CrosswalkSheetRowOrderInvalid")
        previous_sheet_row = sheet_row
        values = _read_row(row, shared_strings)
        require(
            values[0] != ""
            and values[1] != ""
            and values[2] != ""
            and values[4] != ""
            and values[5] in {"유", "무"},
            "CrosswalkRequiredValueMissing",
        )
        x_text, y_text = values[7], values[8]
        if x_text == "" and y_text == "":
            missing_coordinates += 1
            x = None
            y = None
        else:
            require(x_text != "" and y_text != "", "CrosswalkPartialCoordinateMissing")
            try:
                x = float(x_text)
                y = float(y_text)
            except ValueError as exc:
                raise CrosswalkCandidateError("CrosswalkCoordinateInvalid") from exc
            require(math.isfinite(x) and math.isfinite(y), "CrosswalkCoordinateInvalid")
        result.append(
            SourceRow(
                sheet_row,
                values[0],
                values[1],
                values[2],
                values[3] or None,
                values[4],
                values[5] == "유",
                values[6] or None,
                x,
                y,
            )
        )
    require(len(result) == EXPECTED_COUNTS["sourceRows"], "CrosswalkSourceRowCountMismatch")
    require(missing_coordinates == EXPECTED_COUNTS["coordinateMissingRows"], "CrosswalkMissingCoordinateCountChanged")
    return result, {
        "sourceRows": len(result),
        "coordinateMissingRows": missing_coordinates,
        "coordinatePresentRows": len(result) - missing_coordinates,
    }


def read_boundaries(
    path: Path, scope: dict[str, Any], dependencies: Dependencies
) -> tuple[list[Boundary], dict[str, Any]]:
    expected_areas = {
        item["administrativeAreaStableId"].removeprefix("region:kr:hjd:"): item
        for item in scope["administrativeAreas"]
    }
    with zipfile.ZipFile(path, "r") as archive:
        names = archive.namelist()
        require(len(names) == len(set(names)), "BoundaryArchiveDuplicateEntry")
        entries: dict[str, str] = {}
        for extension in (".cpg", ".dbf", ".prj", ".shp", ".shx"):
            matches = [name for name in names if name.lower().endswith(extension)]
            require(len(matches) == 1, f"BoundaryArchiveEntryInvalid:{extension}")
            entries[extension] = matches[0]
        cpg = archive.read(entries[".cpg"]).decode("ascii").strip().upper()
        require(cpg in {"UTF-8", "UTF8", "65001"}, "BoundaryEncodingMismatch")
        source_crs = dependencies.CRS.from_wkt(
            archive.read(entries[".prj"]).decode("utf-8-sig")
        )
        require(source_crs.to_authority() == ("EPSG", "5181"), "BoundaryCrsMismatch")
        reader = dependencies.shapefile.Reader(
            shp=io.BytesIO(archive.read(entries[".shp"])),
            shx=io.BytesIO(archive.read(entries[".shx"])),
            dbf=io.BytesIO(archive.read(entries[".dbf"])),
            encoding="utf-8",
            encodingErrors="strict",
        )
        try:
            require(len(reader) == 425, "BoundaryRecordCountChanged")
            field_names = {field[0] for field in reader.fields[1:]}
            require({"ADSTRD_CD", "ADSTRD_NM"}.issubset(field_names), "BoundaryFieldsMissing")
            selected: dict[str, Boundary] = {}
            for shape_record in reader.iterShapeRecords():
                properties = shape_record.record.as_dict()
                code = str(properties["ADSTRD_CD"]) + "00"
                if code not in expected_areas:
                    continue
                require(shape_record.shape.shapeType == 5, f"BoundaryShapeTypeInvalid:{code}")
                geometry = dependencies.shape(shape_record.shape.__geo_interface__)
                require(
                    isinstance(geometry, (dependencies.Polygon, dependencies.MultiPolygon))
                    and not geometry.is_empty
                    and geometry.is_valid,
                    f"BoundaryGeometryInvalid:{code}",
                )
                expected_name = expected_areas[code]["displayName"]
                require(
                    normalized_dong_name(str(properties["ADSTRD_NM"]))
                    == normalized_dong_name(expected_name),
                    f"BoundaryNameMismatch:{code}",
                )
                require(code not in selected, f"BoundaryDuplicate:{code}")
                shifted = dependencies.translate(geometry, yoff=100_000.0)
                selected[code] = Boundary(
                    code,
                    f"region:kr:hjd:{code}",
                    expected_name,
                    expected_areas[code]["boroughName"],
                    shifted,
                )
        finally:
            reader.close()
    require(set(selected) == set(expected_areas), "BoundaryCoverageIncomplete")
    boundaries = [selected[code] for code in sorted(selected)]
    union = dependencies.unary_union([item.geometry for item in boundaries])
    require(not union.is_empty and union.is_valid, "BoundaryUnionInvalid")
    overlap_area = sum(item.geometry.area for item in boundaries) - union.area
    require(overlap_area < 0.1, "BoundaryInteriorOverlapDetected")
    return boundaries, {
        "sourceRecordCount": 425,
        "selectedBoundaryCount": len(boundaries),
        "sourceCoordinateReference": "EPSG:5181",
        "assignmentCoordinateReferenceCandidate": "EPSG:5186",
        "northingOffsetMeters": 100000.0,
        "unionAreaSquareMeters": round(float(union.area), 3),
        "interiorOverlapAreaSquareMeters": round(float(overlap_area), 6),
        "historicalBoundaryBootstrapOnly": True,
        "currentAdministrativeBoundaryEstablished": False,
    }


def _source_feature_key(row: SourceRow) -> str:
    return (
        f"oa23081:crosswalk:{row.crosswalk_management_number}:"
        f"sheet-row:{row.sheet_row:05d}"
    )


def _candidate_hash_fields(candidate: dict[str, Any]) -> list[str]:
    return [
        candidate["sourceFeatureKey"],
        candidate["administrativeAreaStableId"] or "",
        candidate["assignmentStateCode"],
        candidate["candidateQualityCode"],
        candidate["sourceBoroughName"],
        candidate["spatialAssignmentBoroughName"] or "",
        canonical_json_text(candidate["distanceToAssignedHistoricalBoundaryMeters"]),
        canonical_json_text(candidate["candidateAdministrativeAreaStableIds"]),
        candidate["crosswalkManagementNumber"],
        canonical_json_text(
            {
                "managementNumber": candidate["intersectionManagementNumber"],
                "name": candidate["intersectionName"],
            }
        ),
        candidate["crosswalkType"],
        "true" if candidate["pedestrianSignalPresent"] else "false",
        canonical_json_text(candidate["sourcePointEpsg5186"]),
        canonical_json_text(candidate["commonEnuMillimeters"]),
        "true" if candidate["sourceDistrictSpatialAssignmentConflict"] else "false",
        canonical_json_text(candidate["qualityDiagnosticCodes"]),
        candidate["ownershipBasisCode"],
    ]


def length_prefixed_hash(values: Iterable[str]) -> str:
    digest = hashlib.sha256()
    for value in values:
        encoded = value.encode("utf-8")
        require(len(encoded) <= 0xFFFFFFFF, "CandidateHashFieldTooLong")
        digest.update(struct.pack(">I", len(encoded)))
        digest.update(encoded)
    return digest.hexdigest().upper()


def candidate_set_hash(
    candidates: Sequence[dict[str, Any]],
    source_hashes: dict[str, str],
    generator_hash: str,
    coordinate_validation_hash: str,
    scope_definition_hash: str,
    boundary_conversion_hash: str,
    feasibility_digest: str,
) -> str:
    header = [
        CANDIDATE_SCHEMA_VERSION,
        REVISION,
        DESIGN_HASH_SHA256,
        source_hashes["oa23081Xlsx"],
        source_hashes["oa23081AcquisitionReceipt"],
        source_hashes["oa22160BoundaryArchive"],
        source_hashes["r2ScopeDefinition"],
        source_hashes["r2ScopeManifest"],
        scope_definition_hash,
        source_hashes["officialStationAnchorSelection"],
        source_hashes["officialStationAnchorWorkbook"],
        generator_hash,
        coordinate_validation_hash,
        boundary_conversion_hash,
        feasibility_digest,
    ]
    values = list(header)
    for candidate in candidates:
        values.extend(_candidate_hash_fields(candidate))
    return length_prefixed_hash(values)


def _feasibility_digest(candidates: Sequence[dict[str, Any]]) -> str:
    lines = []
    for candidate in candidates:
        require(candidate["assignmentStateCode"] == UNIQUE_ASSIGNMENT, "FeasibilityDigestUnresolvedCandidate")
        code = candidate["administrativeAreaStableId"].removeprefix("region:kr:hjd:")
        point = candidate["sourcePointEpsg5186"]
        lines.append(
            f"{candidate['crosswalkManagementNumber']}|{point['x']:.12f}|"
            f"{point['y']:.12f}|{code}"
        )
    payload = ("\n".join(sorted(lines)) + "\n").encode("utf-8")
    return sha256_bytes(payload)


def assign_candidates(
    rows: Sequence[SourceRow],
    boundaries: Sequence[Boundary],
    scope: dict[str, Any],
    dependencies: Dependencies,
) -> tuple[list[dict[str, Any]], dict[str, Any]]:
    frame_value = scope["coordinateFrame"]
    frame = CommonEnuFrame(
        float(frame_value["originLatitude"]),
        float(frame_value["originLongitude"]),
        float(frame_value["worldOffsetX"]),
        float(frame_value["worldOffsetZ"]),
    )
    to_wgs84 = dependencies.Transformer.from_crs(5186, 4326, always_xy=True)
    unique_count = 0
    multiple_count = 0
    outside_count = 0
    conflict_count = 0
    candidates: list[dict[str, Any]] = []
    for row in rows:
        if row.x is None or row.y is None:
            continue
        point = dependencies.Point(row.x, row.y)
        matches = [
            boundary
            for boundary in boundaries
            if boundary.geometry.bounds[0] <= row.x <= boundary.geometry.bounds[2]
            and boundary.geometry.bounds[1] <= row.y <= boundary.geometry.bounds[3]
            and boundary.geometry.covers(point)
        ]
        if not matches:
            outside_count += 1
            continue
        if len(matches) == 1:
            assignment_state = UNIQUE_ASSIGNMENT
            administrative_area_id: str | None = matches[0].stable_id
            spatial_borough_name: str | None = matches[0].borough_name
            distance_to_boundary: float | None = round(
                float(matches[0].geometry.boundary.distance(point)), 3
            )
            source_district_conflict = row.district != spatial_borough_name
            if source_district_conflict:
                conflict_count += 1
            unique_count += 1
        else:
            assignment_state = MULTIPLE_ASSIGNMENT
            administrative_area_id = None
            spatial_borough_name = None
            distance_to_boundary = None
            source_district_conflict = False
            multiple_count += 1
        candidate_area_ids = sorted(item.stable_id for item in matches)
        longitude, latitude = to_wgs84.transform(row.x, row.y)
        local_x, local_z = frame.wgs84_to_local(float(longitude), float(latitude))
        candidate: dict[str, Any] = {
            "schemaVersion": CANDIDATE_SCHEMA_VERSION,
            "revision": REVISION,
            "sourceFeatureKey": _source_feature_key(row),
            "administrativeAreaStableId": administrative_area_id,
            "assignmentStateCode": assignment_state,
            "candidateAdministrativeAreaStableIds": candidate_area_ids,
            "sourceSheetRow": row.sheet_row,
            "sourceSerialNumber": row.serial_number,
            "district": row.district,
            "sourceBoroughName": row.district,
            "spatialAssignmentBoroughName": spatial_borough_name,
            "sourceDistrictSpatialAssignmentConflict": source_district_conflict,
            "distanceToAssignedHistoricalBoundaryMeters": distance_to_boundary,
            "qualityDiagnosticCodes": (
                [SOURCE_DISTRICT_CONFLICT_CODE] if source_district_conflict else []
            ),
            "candidateQualityCode": (
                "PendingHumanReviewWithSourceDistrictSpatialAssignmentConflict"
                if source_district_conflict
                else "PendingHumanReview"
            ),
            "ownershipBasisCode": "HistoricalBoundaryCover",
            "crosswalkManagementNumber": row.crosswalk_management_number,
            "intersectionManagementNumber": row.intersection_management_number,
            "intersectionName": row.intersection_name,
            "crosswalkType": row.crosswalk_type,
            "pedestrianSignalPresent": row.pedestrian_signal_present,
            "coordinateReferenceStatus": COORDINATE_STATUS,
            "sourcePointEpsg5186": {"x": row.x, "y": row.y},
            "commonEnuMillimeters": {
                "x": int(round(local_x * 1000.0)),
                "z": int(round(local_z * 1000.0)),
            },
            **AUTHORITY_FLAGS,
        }
        candidates.append(candidate)
    candidates.sort(key=lambda item: item["sourceFeatureKey"])
    require(
        len({item["sourceFeatureKey"] for item in candidates}) == len(candidates),
        "CandidateSourceFeatureKeyCollision",
    )
    counts = {
        "sourceRows": len(rows),
        "coordinatePresentRows": len(rows) - EXPECTED_COUNTS["coordinateMissingRows"],
        "coordinateMissingRows": EXPECTED_COUNTS["coordinateMissingRows"],
        "uniquelyAssignedRows": unique_count,
        "unresolvedMultipleBoundaryRows": multiple_count,
        "outsideScopeRows": outside_count,
        "candidateRows": len(candidates),
        "sourceDistrictSpatialAssignmentConflictRows": conflict_count,
    }
    require(counts == EXPECTED_COUNTS, "CandidateAssignmentCountsChanged")
    distribution = {
        code: sum(
            item["administrativeAreaStableId"] == f"region:kr:hjd:{code}"
            for item in candidates
        )
        for code in sorted(EXPECTED_DISTRIBUTION)
    }
    require(distribution == EXPECTED_DISTRIBUTION, "CandidateDistributionChanged")
    conflict_distribution = {
        code: sum(
            item["administrativeAreaStableId"] == f"region:kr:hjd:{code}"
            and item["sourceDistrictSpatialAssignmentConflict"]
            for item in candidates
        )
        for code in sorted(EXPECTED_DISTRIBUTION)
    }
    require(
        conflict_distribution == EXPECTED_CONFLICT_DISTRIBUTION,
        "CandidateConflictDistributionChanged",
    )
    conflict_candidates = [
        item for item in candidates if item["sourceDistrictSpatialAssignmentConflict"]
    ]
    conflict_pairs = {
        pair: sum(
            f"{item['sourceBoroughName']}>{item['spatialAssignmentBoroughName']}"
            == pair
            for item in conflict_candidates
        )
        for pair in sorted(EXPECTED_CONFLICT_PAIRS)
    }
    require(conflict_pairs == EXPECTED_CONFLICT_PAIRS, "CandidateConflictPairsChanged")
    conflict_distance_range = {
        "minimum": min(
            item["distanceToAssignedHistoricalBoundaryMeters"]
            for item in conflict_candidates
        ),
        "maximum": max(
            item["distanceToAssignedHistoricalBoundaryMeters"]
            for item in conflict_candidates
        ),
    }
    require(
        conflict_distance_range == EXPECTED_CONFLICT_DISTANCE_RANGE_METERS,
        "CandidateConflictDistanceRangeChanged",
    )
    return candidates, {
        "counts": counts,
        "distribution": distribution,
        "conflictDistribution": conflict_distribution,
        "conflictPairs": conflict_pairs,
        "conflictDistanceRangeMeters": conflict_distance_range,
    }


def verify_anchor(
    root: Path,
    rows: Sequence[SourceRow],
    scope: dict[str, Any],
    dependencies: Dependencies,
) -> dict[str, Any]:
    validation = scope["coordinateValidation"]
    station_selection = load_json(
        repository_path(
            root,
            source_by_role(scope, "officialStationAnchorSelection")[
                "repositoryRelativePath"
            ],
        )
    )
    station_rows = [
        item
        for item in station_selection["rows"]
        if item.get("StationStableId")
        == validation["stationAnchorProvenance"]["stationStableId"]
    ]
    require(len(station_rows) == 1, "CoordinateOfficialStationAnchorMissing")
    station_row = station_rows[0]
    matches = [
        row
        for row in rows
        if row.crosswalk_management_number == validation["crosswalkManagementNumber"]
    ]
    require(len(matches) == 1 and matches[0].x is not None and matches[0].y is not None, "CoordinateAnchorRowMissing")
    row = matches[0]
    require(row.sheet_row == validation["sourceSheetRow"], "CoordinateAnchorSheetRowChanged")
    anchor = {
        "longitude": float(station_row["Longitude"]),
        "latitude": float(station_row["Latitude"]),
    }
    require(anchor == validation["sagajeongStationWgs84"], "CoordinateOfficialStationAnchorChanged")
    transformer = dependencies.Transformer.from_crs(4326, 5186, always_xy=True)
    anchor_x, anchor_y = transformer.transform(anchor["longitude"], anchor["latitude"])
    distance = math.hypot(row.x - anchor_x, row.y - anchor_y)
    require(round(anchor_x, 4) == validation["anchorEpsg5186"]["x"], "CoordinateAnchorXChanged")
    require(round(anchor_y, 4) == validation["anchorEpsg5186"]["y"], "CoordinateAnchorYChanged")
    require(round(distance, 4) == validation["distanceMeters"], "CoordinateAnchorDistanceChanged")
    coordinate_rows = [item for item in rows if item.x is not None and item.y is not None]
    source_x = [item.x for item in coordinate_rows]
    source_y = [item.y for item in coordinate_rows]
    alternative_results: list[dict[str, Any]] = []
    threshold_by_crs = {
        "EPSG:5181": (83_000.0, "GreaterThan83Kilometers"),
        "EPSG:5174": (83_000.0, "GreaterThan83Kilometers"),
        "EPSG:2097": (83_000.0, "GreaterThan83Kilometers"),
        "EPSG:5179": (1_580_000.0, "GreaterThan1580Kilometers"),
    }
    for coordinate_reference in ("EPSG:5181", "EPSG:5174", "EPSG:2097", "EPSG:5179"):
        alternative_transformer = dependencies.Transformer.from_crs(
            coordinate_reference, 5186, always_xy=True
        )
        transformed_x, transformed_y = alternative_transformer.transform(
            source_x, source_y
        )
        distances = [
            math.hypot(float(x) - anchor_x, float(y) - anchor_y)
            for x, y in zip(transformed_x, transformed_y, strict=True)
        ]
        require(all(math.isfinite(value) for value in distances), "AlternativeCrsDistanceInvalid")
        minimum_index = min(
            range(len(distances)),
            key=lambda index: (
                distances[index],
                coordinate_rows[index].sheet_row,
                coordinate_rows[index].crosswalk_management_number,
            ),
        )
        minimum_distance = round(distances[minimum_index], 4)
        threshold, threshold_code = threshold_by_crs[coordinate_reference]
        require(minimum_distance > threshold, "AlternativeCrsThresholdFailed")
        alternative_results.append(
            {
                "coordinateReference": coordinate_reference,
                "minimumDistanceMeters": minimum_distance,
                "sourceSheetRow": coordinate_rows[minimum_index].sheet_row,
                "crosswalkManagementNumber": coordinate_rows[
                    minimum_index
                ].crosswalk_management_number,
                "thresholdCode": threshold_code,
            }
        )
    alternative_evidence = {
        "methodCode": ALTERNATIVE_CRS_EVIDENCE_BASE["methodCode"],
        "coordinatePresentRowsEvaluated": len(coordinate_rows),
        "results": alternative_results,
        "contentHashSha256": "",
    }
    apply_content_hash(alternative_evidence)
    require(
        alternative_evidence == expected_alternative_crs_evidence()
        and alternative_evidence["contentHashSha256"]
        == validation["alternativeCrsEvidenceHashSha256"],
        "AlternativeCrsEvidenceChanged",
    )
    return {
        "crosswalkManagementNumber": row.crosswalk_management_number,
        "sourceSheetRow": row.sheet_row,
        "anchorEpsg5186": {"x": round(anchor_x, 4), "y": round(anchor_y, 4)},
        "distanceMeters": round(distance, 4),
        "statusCode": COORDINATE_STATUS,
        "sourceDeclared": False,
        "stationAnchorProvenance": validation["stationAnchorProvenance"],
        "alternativeCrsEvidence": alternative_evidence,
    }


def _candidate_bytes(candidates: Sequence[dict[str, Any]]) -> bytes:
    return b"".join(canonical_json_bytes(item) + b"\n" for item in candidates)


def build_documents(
    root: Path,
    scope_path: Path,
    scope: dict[str, Any],
    source_hashes: dict[str, str],
    candidates_without_set_hash: list[dict[str, Any]],
    assignment_audit: dict[str, Any],
    boundary_audit: dict[str, Any],
    anchor_audit: dict[str, Any],
) -> tuple[str, dict[str, bytes]]:
    generator_hash = scope["output"]["toolchain"]["generatorSha256"]
    scope_definition_hash = sha256_file(scope_path)
    coordinate_validation_hash = scope["coordinateValidation"]["contentHashSha256"]
    alternative_crs_evidence_hash = scope["alternativeCrsEvidence"][
        "contentHashSha256"
    ]
    boundary_conversion_hash = scope["historicalBoundaryConversion"][
        "contentHashSha256"
    ]
    feasibility_digest = _feasibility_digest(candidates_without_set_hash)
    require(feasibility_digest == EXPECTED_FEASIBILITY_DIGEST, "FeasibilityDigestChanged")
    set_hash = candidate_set_hash(
        candidates_without_set_hash,
        source_hashes,
        generator_hash,
        coordinate_validation_hash,
        scope_definition_hash,
        boundary_conversion_hash,
        feasibility_digest,
    )
    candidates = copy.deepcopy(candidates_without_set_hash)
    for candidate in candidates:
        candidate["sourceHashSha256"] = source_hashes["oa23081Xlsx"]
        candidate["historicalBoundaryHashSha256"] = source_hashes["oa22160BoundaryArchive"]
        candidate["candidateSetHashSha256"] = set_hash
    candidate_payload = _candidate_bytes(candidates)

    area_by_code = {
        item["administrativeAreaStableId"].removeprefix("region:kr:hjd:"): item
        for item in scope["administrativeAreas"]
    }
    per_area = []
    for code in sorted(EXPECTED_DISTRIBUTION):
        area_candidates = [
            item
            for item in candidates
            if item["administrativeAreaStableId"] == f"region:kr:hjd:{code}"
        ]
        per_area.append(
            {
                "administrativeAreaStableId": f"region:kr:hjd:{code}",
                "displayName": area_by_code[code]["displayName"],
                "boroughName": area_by_code[code]["boroughName"],
                "candidateCount": len(area_candidates),
                "sourceDistrictSpatialAssignmentConflictCount": sum(
                    item["sourceDistrictSpatialAssignmentConflict"]
                    for item in area_candidates
                ),
                "pedestrianSignalPresentCount": sum(
                    item["pedestrianSignalPresent"] for item in area_candidates
                ),
                "pedestrianSignalAbsentCount": sum(
                    not item["pedestrianSignalPresent"] for item in area_candidates
                ),
            }
        )
    conflict_diagnostic = {
        "diagnosticCode": SOURCE_DISTRICT_CONFLICT_CODE,
        "candidateQualityCode": (
            "PendingHumanReviewWithSourceDistrictSpatialAssignmentConflict"
        ),
        "ownershipDispositionCode": "RetainedWithHistoricalBoundaryOwner",
        "count": assignment_audit["counts"][
            "sourceDistrictSpatialAssignmentConflictRows"
        ],
        "perAdministrativeArea": assignment_audit["conflictDistribution"],
        "sourceToSpatialBoroughPairs": assignment_audit["conflictPairs"],
        "distanceToAssignedHistoricalBoundaryMeters": assignment_audit[
            "conflictDistanceRangeMeters"
        ],
    }
    audit: dict[str, Any] = {
        "schemaVersion": AUDIT_SCHEMA_VERSION,
        "revision": REVISION,
        "generatedAtUtc": GENERATED_AT_UTC,
        "designHashSha256": DESIGN_HASH_SHA256,
        "scopeStableId": SCOPE_STABLE_ID,
        "scopeDefinitionSha256": scope_definition_hash,
        "candidateSetHashSha256": set_hash,
        "supersedesRevision": scope["supersedesRevision"],
        "supersededCandidateDispositionCode": scope[
            "supersededCandidateDispositionCode"
        ],
        "coordinateReferenceStatus": COORDINATE_STATUS,
        "coordinateValidationHashSha256": coordinate_validation_hash,
        "alternativeCrsEvidenceHashSha256": alternative_crs_evidence_hash,
        "historicalBoundaryConversion": scope["historicalBoundaryConversion"],
        "historicalBoundaryConversionHashSha256": boundary_conversion_hash,
        "sourceHashes": source_hashes,
        "counts": assignment_audit["counts"],
        "perAdministrativeArea": per_area,
        "qualityDiagnostics": {
            "sourceDistrictSpatialAssignmentConflict": conflict_diagnostic,
        },
        "boundary": boundary_audit,
        "coordinateAnchor": anchor_audit,
        "feasibilityDigestSha256": feasibility_digest,
        "checks": {
            "exactThirtyAdministrativeAreas": len(per_area) == 30,
            "allSourceRowsAccountedFor": (
                assignment_audit["counts"]["coordinateMissingRows"]
                + assignment_audit["counts"]["uniquelyAssignedRows"]
                + assignment_audit["counts"]["unresolvedMultipleBoundaryRows"]
                + assignment_audit["counts"]["outsideScopeRows"]
                == assignment_audit["counts"]["sourceRows"]
            ),
            "uniqueCandidatesHaveOneOwner": all(
                item["assignmentStateCode"] != UNIQUE_ASSIGNMENT
                or (
                    item["administrativeAreaStableId"] is not None
                    and item["candidateAdministrativeAreaStableIds"]
                    == [item["administrativeAreaStableId"]]
                )
                for item in candidates
            ),
            "multipleBoundaryCandidatesAreUnresolved": all(
                item["assignmentStateCode"] != MULTIPLE_ASSIGNMENT
                or (
                    item["administrativeAreaStableId"] is None
                    and len(item["candidateAdministrativeAreaStableIds"]) > 1
                )
                for item in candidates
            ),
            "sourceCoordinateReferenceRemainsNonAuthoritative": True,
            "conflictingSourceDistrictCandidatesRemainHistoricalBoundaryOwned": all(
                item["ownershipBasisCode"] == "HistoricalBoundaryCover"
                and item["administrativeAreaStableId"] is not None
                and item["candidateQualityCode"]
                == "PendingHumanReviewWithSourceDistrictSpatialAssignmentConflict"
                and item["qualityDiagnosticCodes"]
                == [SOURCE_DISTRICT_CONFLICT_CODE]
                for item in candidates
                if item["sourceDistrictSpatialAssignmentConflict"]
            ),
            "allAuthorityFlagsExact": all(
                all(item.get(key) is value for key, value in AUTHORITY_FLAGS.items())
                for item in candidates
            ),
        },
        **AUTHORITY_FLAGS,
        "contentHashSha256": "",
    }
    apply_content_hash(audit)
    audit_payload = pretty_json_bytes(audit)

    manifest: dict[str, Any] = {
        "schemaVersion": MANIFEST_SCHEMA_VERSION,
        "scopeStableId": SCOPE_STABLE_ID,
        "revision": REVISION,
        "generatedAtUtc": GENERATED_AT_UTC,
        "sourceVintage": scope["sourceVintage"],
        "designDocumentRef": DESIGN_DOCUMENT_RELATIVE.as_posix(),
        "designHashSha256": DESIGN_HASH_SHA256,
        "scopeDefinitionSha256": scope_definition_hash,
        "candidateSetHashSha256": set_hash,
        "supersedesRevision": scope["supersedesRevision"],
        "supersededCandidateDispositionCode": scope[
            "supersededCandidateDispositionCode"
        ],
        "coordinateReferenceStatus": COORDINATE_STATUS,
        "coordinateValidationHashSha256": coordinate_validation_hash,
        "alternativeCrsEvidenceHashSha256": alternative_crs_evidence_hash,
        "historicalBoundaryConversionHashSha256": boundary_conversion_hash,
        "feasibilityDigestSha256": feasibility_digest,
        "candidateSetHashContract": {
            "algorithmCode": "Sha256LengthPrefixedUtf8BigEndianUInt32",
            "canonicalJsonCode": "Utf8SortedKeysCompactNoNaN",
            "headerFieldOrder": CANDIDATE_SET_HASH_HEADER_FIELD_ORDER,
            "candidateFieldOrder": CANDIDATE_SET_HASH_CANDIDATE_FIELD_ORDER,
        },
        "administrativeAreaIds": [
            item["administrativeAreaStableId"] for item in scope["administrativeAreas"]
        ],
        "counts": assignment_audit["counts"],
        "perAdministrativeArea": per_area,
        "qualityDiagnostics": {
            "sourceDistrictSpatialAssignmentConflict": conflict_diagnostic,
        },
        "sourceHashes": source_hashes,
        "toolchain": scope["output"]["toolchain"],
        "files": [
            {
                "relativePath": "audit.json",
                "sha256": sha256_bytes(audit_payload),
                "byteLength": len(audit_payload),
            },
            {
                "relativePath": "candidates.ndjson",
                "sha256": sha256_bytes(candidate_payload),
                "byteLength": len(candidate_payload),
                "recordCount": len(candidates),
            },
        ],
        **AUTHORITY_FLAGS,
        "contentHashSha256": "",
    }
    apply_content_hash(manifest)
    manifest_payload = pretty_json_bytes(manifest)

    generation_relative = (
        Path(scope["output"]["repositoryRelativeDirectory"])
        / scope["output"]["generationDirectoryName"]
        / set_hash.lower()
    ).as_posix()
    complete: dict[str, Any] = {
        "schemaVersion": COMPLETE_SCHEMA_VERSION,
        "scopeStableId": SCOPE_STABLE_ID,
        "revision": REVISION,
        "designHashSha256": DESIGN_HASH_SHA256,
        "candidateSetHashSha256": set_hash,
        "scopeDefinitionSha256": scope_definition_hash,
        "coordinateValidationHashSha256": coordinate_validation_hash,
        "alternativeCrsEvidenceHashSha256": alternative_crs_evidence_hash,
        "historicalBoundaryConversionHashSha256": boundary_conversion_hash,
        "feasibilityDigestSha256": feasibility_digest,
        "supersedesRevision": scope["supersedesRevision"],
        "supersededCandidateDispositionCode": scope[
            "supersededCandidateDispositionCode"
        ],
        "generationRelativePath": generation_relative,
        "completeMarker": True,
        "files": sorted(
            [
                {
                    "relativePath": "manifest.json",
                    "sha256": sha256_bytes(manifest_payload),
                    "byteLength": len(manifest_payload),
                },
                {
                    "relativePath": "candidates.ndjson",
                    "sha256": sha256_bytes(candidate_payload),
                    "byteLength": len(candidate_payload),
                    "recordCount": len(candidates),
                },
                {
                    "relativePath": "audit.json",
                    "sha256": sha256_bytes(audit_payload),
                    "byteLength": len(audit_payload),
                },
            ],
            key=lambda item: item["relativePath"],
        ),
        **AUTHORITY_FLAGS,
        "contentHashSha256": "",
    }
    apply_content_hash(complete)
    return set_hash, {
        "manifest.json": manifest_payload,
        "candidates.ndjson": candidate_payload,
        "audit.json": audit_payload,
        "complete.json": pretty_json_bytes(complete),
    }


def prepare(
    root: Path, scope_path: Path
) -> tuple[dict[str, Any], str, dict[str, bytes]]:
    dependencies = load_dependencies(root)
    scope, source_hashes = load_scope(root, scope_path, dependencies)
    workbook_path = repository_path(
        root, source_by_role(scope, "oa23081Xlsx")["repositoryRelativePath"]
    )
    boundary_path = repository_path(
        root, source_by_role(scope, "oa22160BoundaryArchive")["repositoryRelativePath"]
    )
    print(json.dumps({"stage": "ReadFrozenOA23081"}), file=sys.stderr, flush=True)
    rows, row_audit = read_crosswalk_rows(workbook_path)
    print(json.dumps({"stage": "ReadHistoricalAdministrativeBoundaries"}), file=sys.stderr, flush=True)
    boundaries, boundary_audit = read_boundaries(boundary_path, scope, dependencies)
    print(json.dumps({"stage": "AssignCrosswalkPointCandidates"}), file=sys.stderr, flush=True)
    candidates, assignment_audit = assign_candidates(
        rows, boundaries, scope, dependencies
    )
    require(
        row_audit["coordinatePresentRows"]
        == assignment_audit["counts"]["coordinatePresentRows"],
        "CoordinateAccountingMismatch",
    )
    anchor_audit = verify_anchor(root, rows, scope, dependencies)
    set_hash, payloads = build_documents(
        root,
        scope_path,
        scope,
        source_hashes,
        candidates,
        assignment_audit,
        boundary_audit,
        anchor_audit,
    )
    return scope, set_hash, payloads


def _generation_directory(root: Path, scope: dict[str, Any], set_hash: str) -> Path:
    output_root = repository_path(root, scope["output"]["repositoryRelativeDirectory"])
    generations_root = repository_path(
        root, output_root / scope["output"]["generationDirectoryName"]
    )
    return repository_path(root, generations_root / set_hash.lower())


def verify_payloads(
    root: Path,
    scope_path: Path,
    scope: dict[str, Any],
    set_hash: str,
    expected_payloads: dict[str, bytes],
) -> dict[str, Any]:
    generation = _generation_directory(root, scope, set_hash)
    require(
        generation.is_dir() and not is_reparse_path(generation),
        "CandidateGenerationMissing",
    )
    actual_entries = list(generation.iterdir())
    actual_names = sorted(item.name for item in actual_entries)
    require(actual_names == sorted(expected_payloads), "CandidateGenerationFileSetMismatch")
    for name, payload in expected_payloads.items():
        path = repository_path(root, generation / name)
        require(path.is_file() and not is_reparse_path(path), f"CandidateGenerationFileMissing:{name}")
        require(path.read_bytes() == payload, f"CandidateGenerationFileMismatch:{name}")

    complete = load_json(generation / "complete.json")
    require(
        complete.get("schemaVersion") == COMPLETE_SCHEMA_VERSION
        and complete.get("candidateSetHashSha256") == set_hash
        and complete.get("designHashSha256") == DESIGN_HASH_SHA256
        and complete.get("scopeDefinitionSha256") == sha256_file(scope_path)
        and complete.get("coordinateValidationHashSha256")
        == scope["coordinateValidation"]["contentHashSha256"]
        and complete.get("alternativeCrsEvidenceHashSha256")
        == scope["alternativeCrsEvidence"]["contentHashSha256"]
        and complete.get("historicalBoundaryConversionHashSha256")
        == scope["historicalBoundaryConversion"]["contentHashSha256"]
        and complete.get("feasibilityDigestSha256") == EXPECTED_FEASIBILITY_DIGEST
        and complete.get("completeMarker") is True
        and complete.get("contentHashSha256") == document_content_hash(complete),
        "CandidateCompleteMarkerInvalid",
    )
    for entry in complete.get("files", []):
        path = repository_path(root, generation / entry.get("relativePath", ""))
        require(
            path.is_file() and not is_reparse_path(path),
            "CandidateCompleteReferencedFileMissing",
        )
        require(path.stat().st_size == entry.get("byteLength"), "CandidateCompleteFileLengthMismatch")
        require(sha256_file(path) == entry.get("sha256"), "CandidateCompleteFileHashMismatch")

    manifest = load_json(generation / "manifest.json")
    audit = load_json(generation / "audit.json")
    require(manifest.get("contentHashSha256") == document_content_hash(manifest), "CandidateManifestContentHashMismatch")
    require(audit.get("contentHashSha256") == document_content_hash(audit), "CandidateAuditContentHashMismatch")
    require(
        manifest.get("candidateSetHashSha256") == set_hash
        and audit.get("candidateSetHashSha256") == set_hash,
        "CandidateSetHashBindingMismatch",
    )
    require(
        manifest.get("scopeDefinitionSha256") == sha256_file(scope_path)
        and audit.get("scopeDefinitionSha256") == sha256_file(scope_path)
        and manifest.get("designHashSha256") == DESIGN_HASH_SHA256
        and audit.get("designHashSha256") == DESIGN_HASH_SHA256
        and manifest.get("alternativeCrsEvidenceHashSha256")
        == scope["alternativeCrsEvidence"]["contentHashSha256"]
        and audit.get("alternativeCrsEvidenceHashSha256")
        == scope["alternativeCrsEvidence"]["contentHashSha256"],
        "CandidateLineageBindingMismatch",
    )
    require(
        manifest.get("candidateSetHashContract")
        == {
            "algorithmCode": "Sha256LengthPrefixedUtf8BigEndianUInt32",
            "canonicalJsonCode": "Utf8SortedKeysCompactNoNaN",
            "headerFieldOrder": CANDIDATE_SET_HASH_HEADER_FIELD_ORDER,
            "candidateFieldOrder": CANDIDATE_SET_HASH_CANDIDATE_FIELD_ORDER,
        },
        "CandidateSetHashContractMismatch",
    )
    candidate_lines = (generation / "candidates.ndjson").read_text(encoding="utf-8").splitlines()
    require(len(candidate_lines) == EXPECTED_COUNTS["candidateRows"], "CandidateNdjsonRowCountMismatch")
    parsed_candidates: list[dict[str, Any]] = []
    for line in candidate_lines:
        try:
            item = json.loads(line)
        except json.JSONDecodeError as exc:
            raise CrosswalkCandidateError("CandidateNdjsonInvalid") from exc
        require(isinstance(item, dict), "CandidateNdjsonRecordInvalid")
        require(
            item.get("candidateSetHashSha256") == set_hash
            and item.get("coordinateReferenceStatus") == COORDINATE_STATUS,
            "CandidateRecordBindingMismatch",
        )
        require(
            all(item.get(key) is value for key, value in AUTHORITY_FLAGS.items()),
            "CandidateRecordAuthorityInvalid",
        )
        conflict = item.get("sourceDistrictSpatialAssignmentConflict") is True
        require(
            item.get("ownershipBasisCode") == "HistoricalBoundaryCover"
            and item.get("sourceBoroughName") == item.get("district")
            and item.get("spatialAssignmentBoroughName")
            == EXPECTED_BOROUGH_BY_CODE_PREFIX[
                item["administrativeAreaStableId"].removeprefix("region:kr:hjd:")[:5]
            ]
            and isinstance(
                item.get("distanceToAssignedHistoricalBoundaryMeters"), (int, float)
            )
            and (
                (
                    item.get("candidateQualityCode")
                    == "PendingHumanReviewWithSourceDistrictSpatialAssignmentConflict"
                    and item.get("qualityDiagnosticCodes")
                    == [SOURCE_DISTRICT_CONFLICT_CODE]
                    and item.get("sourceBoroughName")
                    != item.get("spatialAssignmentBoroughName")
                )
                if conflict
                else (
                    item.get("candidateQualityCode") == "PendingHumanReview"
                    and item.get("qualityDiagnosticCodes") == []
                    and item.get("sourceBoroughName")
                    == item.get("spatialAssignmentBoroughName")
                )
            ),
            "CandidateQualityDiagnosticInvalid",
        )
        parsed_candidates.append(item)
    keys = [item["sourceFeatureKey"] for item in parsed_candidates]
    require(keys == sorted(keys) and len(keys) == len(set(keys)), "CandidateNdjsonOrderInvalid")
    require(
        sum(item["sourceDistrictSpatialAssignmentConflict"] for item in parsed_candidates)
        == EXPECTED_COUNTS["sourceDistrictSpatialAssignmentConflictRows"],
        "CandidateConflictCountMismatch",
    )
    return {
        "status": "PASS",
        "revision": REVISION,
        "candidateSetHashSha256": set_hash,
        "generationRelativePath": generation.relative_to(root).as_posix(),
        "candidateRows": len(parsed_candidates),
        "counts": manifest["counts"],
        "perAdministrativeArea": manifest["perAdministrativeArea"],
        "manifestSha256": sha256_file(generation / "manifest.json"),
        "candidatesSha256": sha256_file(generation / "candidates.ndjson"),
        "auditSha256": sha256_file(generation / "audit.json"),
        "completeSha256": sha256_file(generation / "complete.json"),
        "scopeDefinitionSha256": sha256_file(scope_path),
        "generatorSha256": sha256_file(Path(__file__).resolve()),
        "feasibilityDigestSha256": audit["feasibilityDigestSha256"],
        **AUTHORITY_FLAGS,
}


def remove_private_staging(root: Path, staging_root: Path, staging: Path) -> None:
    root = require_repository_root(root)
    staging_root = repository_path(root, staging_root)
    staging = repository_path(root, staging)
    require(staging.parent == staging_root, "CandidateStagingDeletePathInvalid")
    require(staging.is_dir() and not is_reparse_path(staging), "CandidateStagingDeletePathInvalid")
    shutil.rmtree(staging)


def build(root: Path, scope_path: Path) -> dict[str, Any]:
    root = require_repository_root(root)
    scope_path = repository_path(root, scope_path)
    scope, set_hash, payloads = prepare(root, scope_path)
    output_root = ensure_plain_directory(
        root,
        repository_path(root, scope["output"]["repositoryRelativeDirectory"]),
        "CandidateOutputRootInvalid",
    )
    staging_root = ensure_plain_directory(
        root, output_root / "staging", "CandidateStagingRootInvalid"
    )
    generations_root = ensure_plain_directory(
        root,
        output_root / scope["output"]["generationDirectoryName"],
        "CandidateGenerationsRootInvalid",
    )
    staging = repository_path(root, staging_root / ("candidate-" + uuid.uuid4().hex))
    final = repository_path(root, generations_root / set_hash.lower())
    staging.mkdir(parents=False, exist_ok=False)
    staging = repository_path(root, staging)
    require(staging.is_dir() and not is_reparse_path(staging), "CandidateStagingPathInvalid")
    try:
        for name, payload in payloads.items():
            path = repository_path(root, staging / name)
            with path.open("xb") as stream:
                stream.write(payload)
            require(
                path.is_file()
                and not is_reparse_path(path)
                and path.read_bytes() == payload,
                f"CandidateStagingFileMismatch:{name}",
            )
        require(
            sorted(item.name for item in staging.iterdir()) == sorted(payloads),
            "CandidateStagingFileSetMismatch",
        )
        output_root = repository_path(root, output_root)
        staging_root = repository_path(root, staging_root)
        generations_root = repository_path(root, generations_root)
        staging = repository_path(root, staging)
        final = repository_path(root, final)
        require(
            all(
                path.is_dir() and not is_reparse_path(path)
                for path in (output_root, staging_root, generations_root, staging)
            ),
            "CandidateAtomicMovePathInvalid",
        )
        if os.path.lexists(final):
            require(final.is_dir() and not is_reparse_path(final), "CandidateGenerationPathInvalid")
            require(
                sorted(item.name for item in final.iterdir()) == sorted(payloads),
                "CandidateGenerationCollision",
            )
            for name, payload in payloads.items():
                path = repository_path(root, final / name)
                require(
                    path.is_file()
                    and not is_reparse_path(path)
                    and path.read_bytes() == payload,
                    "CandidateGenerationCollision",
                )
            changed_files = 0
            remove_private_staging(root, staging_root, staging)
        else:
            os.replace(staging, final)
            final = repository_path(root, final)
            require(
                final.is_dir() and not is_reparse_path(final),
                "CandidateAtomicMoveResultInvalid",
            )
            changed_files = len(payloads)
    finally:
        if os.path.lexists(staging):
            remove_private_staging(root, staging_root, staging)
    result = verify_payloads(root, scope_path, scope, set_hash, payloads)
    result["changedFiles"] = changed_files
    return result


def verify(root: Path, scope_path: Path) -> dict[str, Any]:
    root = require_repository_root(root)
    scope_path = repository_path(root, scope_path)
    scope, set_hash, payloads = prepare(root, scope_path)
    return verify_payloads(root, scope_path, scope, set_hash, payloads)


def self_test(root: Path) -> dict[str, Any]:
    root = require_repository_root(root)
    dependencies = load_dependencies(root)
    tests = 0

    def test(condition: bool, code: str) -> None:
        nonlocal tests
        require(condition, f"SelfTestFailed:{code}")
        tests += 1

    test(length_prefixed_hash(["ab", "c"]) != length_prefixed_hash(["a", "bc"]), "HashFraming")
    left = dependencies.Polygon([(0, 0), (1, 0), (1, 1), (0, 1)])
    right = dependencies.Polygon([(1, 0), (2, 0), (2, 1), (1, 1)])
    test(left.covers(dependencies.Point(0.5, 0.5)) and not right.covers(dependencies.Point(0.5, 0.5)), "UniqueCover")
    test(left.covers(dependencies.Point(1, 0.5)) and right.covers(dependencies.Point(1, 0.5)), "SharedBoundaryCover")
    test(not left.covers(dependencies.Point(3, 3)) and not right.covers(dependencies.Point(3, 3)), "OutsideCover")
    test(sum(value is True for value in AUTHORITY_FLAGS.values()) == 3, "PositiveBoundaryFlags")
    test(sum(value is False for value in AUTHORITY_FLAGS.values()) == 12, "NegativeBoundaryFlags")
    marker = {"value": 1, "contentHashSha256": ""}
    apply_content_hash(marker)
    test(marker["contentHashSha256"] == document_content_hash(marker), "DocumentContentHash")
    changed = copy.deepcopy(marker)
    changed["value"] = 2
    test(changed["contentHashSha256"] != document_content_hash(changed), "DocumentTamper")
    test(canonical_json_text(["b", "a"]) == '["b","a"]', "CanonicalJson")
    test(len(CANDIDATE_SET_HASH_HEADER_FIELD_ORDER) == 15, "HashHeaderFieldCount")
    test(len(CANDIDATE_SET_HASH_CANDIDATE_FIELD_ORDER) == 17, "HashCandidateFieldCount")
    synthetic_candidate = {
        "sourceFeatureKey": "oa23081:crosswalk:test:sheet-row:00001",
        "administrativeAreaStableId": "region:kr:hjd:1126057500",
        "assignmentStateCode": UNIQUE_ASSIGNMENT,
        "candidateQualityCode": "PendingHumanReview",
        "sourceBoroughName": "중랑구",
        "spatialAssignmentBoroughName": "중랑구",
        "distanceToAssignedHistoricalBoundaryMeters": 1.25,
        "candidateAdministrativeAreaStableIds": ["region:kr:hjd:1126057500"],
        "crosswalkManagementNumber": "test",
        "intersectionManagementNumber": None,
        "intersectionName": None,
        "crosswalkType": "test",
        "pedestrianSignalPresent": False,
        "sourcePointEpsg5186": {"x": 1.0, "y": 2.0},
        "commonEnuMillimeters": {"x": 1000, "z": 2000},
        "sourceDistrictSpatialAssignmentConflict": False,
        "qualityDiagnosticCodes": [],
        "ownershipBasisCode": "HistoricalBoundaryCover",
    }
    synthetic_sources = {
        "oa23081Xlsx": "01",
        "oa23081AcquisitionReceipt": "02",
        "oa22160BoundaryArchive": "03",
        "r2ScopeDefinition": "04",
        "r2ScopeManifest": "05",
        "officialStationAnchorSelection": "06",
        "officialStationAnchorWorkbook": "07",
    }
    first_set_hash = candidate_set_hash(
        [synthetic_candidate], synthetic_sources, "08", "09", "10", "11", "12"
    )
    changed_sources = dict(synthetic_sources)
    changed_sources["oa22160BoundaryArchive"] = "changed-boundary"
    second_set_hash = candidate_set_hash(
        [synthetic_candidate], changed_sources, "08", "09", "10", "11", "12"
    )
    test(first_set_hash != second_set_hash, "BoundaryLineageChangesCandidateSetHash")
    conflict_candidate = copy.deepcopy(synthetic_candidate)
    conflict_candidate.update(
        {
            "candidateQualityCode": (
                "PendingHumanReviewWithSourceDistrictSpatialAssignmentConflict"
            ),
            "sourceBoroughName": "광진구",
            "sourceDistrictSpatialAssignmentConflict": True,
            "qualityDiagnosticCodes": [SOURCE_DISTRICT_CONFLICT_CODE],
        }
    )
    test(
        _candidate_hash_fields(conflict_candidate)
        != _candidate_hash_fields(synthetic_candidate),
        "ConflictDiagnosticChangesCandidateHashFields",
    )
    outside_rejected = False
    try:
        repository_path(root, root.parent / "scope-outside-repository.json")
    except CrosswalkCandidateError as exc:
        outside_rejected = str(exc) == "PathOutsideRepository"
    test(outside_rejected, "OutsideScopeRejected")
    reparse_rejected = False
    reparse_target = repository_path(root, Path(__file__).relative_to(root))
    real_is_reparse_path = is_reparse_path

    def synthetic_reparse_check(path: Path) -> bool:
        return same_path(lexical_absolute(path), reparse_target) or real_is_reparse_path(
            path
        )

    try:
        with mock.patch(
            f"{__name__}.is_reparse_path", side_effect=synthetic_reparse_check
        ):
            repository_path(root, reparse_target)
    except CrosswalkCandidateError as exc:
        reparse_rejected = str(exc) == "RepositoryReparsePathRejected"
    test(reparse_rejected, "ReparseScopeRejected")
    test(
        expected_historical_boundary_conversion()["contentHashSha256"]
        == document_content_hash(expected_historical_boundary_conversion()),
        "HistoricalBoundaryConversionDigest",
    )
    test(
        expected_coordinate_validation()["contentHashSha256"]
        == document_content_hash(expected_coordinate_validation()),
        "CoordinateValidationDigest",
    )
    return {
        "status": "PASS",
        "selfTestsPassed": tests,
        "generatorSha256": sha256_file(Path(__file__).resolve()),
    }


def parse_arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("mode", choices=("build", "verify", "self-test"))
    parser.add_argument("--root", type=Path, default=DEFAULT_REPOSITORY_ROOT)
    parser.add_argument("--scope", type=Path)
    return parser.parse_args()


def main() -> int:
    arguments = parse_arguments()
    try:
        root = require_repository_root(arguments.root)
        scope_path = repository_path(
            root,
            arguments.scope
            if arguments.scope is not None
            else DEFAULT_SCOPE_RELATIVE,
        )
        if arguments.mode == "build":
            result = build(root, scope_path)
        elif arguments.mode == "verify":
            result = verify(root, scope_path)
        else:
            result = self_test(root)
        print(json.dumps(result, ensure_ascii=False, indent=2, allow_nan=False))
        return 0
    except CrosswalkCandidateError as exc:
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
