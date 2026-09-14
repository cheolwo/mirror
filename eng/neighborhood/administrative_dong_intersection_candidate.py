#!/usr/bin/env python3
"""OA-15534 교차로 점을 30개 역사 행정동 비공개 후보로 결정적 생성한다.

이 도구는 교차로 점 자체를 역사 경계에 귀속한 검토 자료만 만든다. 횡단보도
연결은 귀속 이후의 진단이며, 방향·제어기·차선·신호·통행·Unity 권위를
생성하지 않는다.
"""

from __future__ import annotations

import argparse
import hashlib
import io
import json
import math
import os
import re
import stat
import struct
import sys
import tempfile
import uuid
import zipfile
from collections import Counter, defaultdict
from dataclasses import dataclass
from pathlib import Path
from types import SimpleNamespace
from typing import Any, Callable, Iterable, Sequence
from unittest import mock


DEFAULT_REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
DEFAULT_SCOPE_RELATIVE = Path(
    "eng/world-seedbeds/administrative-dong-dioramas/"
    "northeast-seoul-rider-intersection.g3b.r1.json"
)
DESIGN_DOCUMENT_RELATIVE = Path(
    "docs/AI/Planning/시스템/PLAN-SYSTEM-ADMIN-DONG-DIORAMA/"
    "administrative-dong-intersection-point-candidate.implementation.r10.md"
)
DATA_IMPLEMENTATION_RELATIVE = Path(
    "docs/AI/Planning/시스템/PLAN-SYSTEM-ADMIN-DONG-DIORAMA/"
    "administrative-dong-intersection-point-candidate.data-implementation.v1.json"
)

SCOPE_SCHEMA_VERSION = "administrative-dong-intersection-generation-scope.v1"
SCOPE_STABLE_ID = "scope:administrative-dong-intersection:northeast-seoul-rider:g3b:r1"
REVISION = "northeast-seoul-admin-dong-intersection-point-candidate.g3b.r1"
DESIGN_REVISION = "administrative-dong-diorama:intersection-point-candidate.g3b.r1"
DATA_IMPLEMENTATION_SCHEMA_VERSION = "public-data-candidate-implementation.v1"
DATA_IMPLEMENTATION_STABLE_ID = (
    "data-implementation:administrative-dong-intersection-point:g3b:r1"
)
GENERATED_AT_UTC = "2026-09-15T00:00:00Z"
SOURCE_REVISION = "file-modified:20250814:retrieved:20260913"
SOURCE_COORDINATE_STATUS = "SourceDeclaredEpsg5186"
CROSSWALK_REVISION = "northeast-seoul-admin-dong-crosswalk-candidate.g3a.r2"
CROSSWALK_CANDIDATE_SET_HASH = (
    "78312E5F0DDB89BFFA9AD1881379CBF7E8037A6C6454D4F0ABDF2A3E4D22D30A"
)

CANDIDATE_SCHEMA_VERSION = "administrative-dong-intersection-point-candidate.v1"
MANIFEST_SCHEMA_VERSION = (
    "administrative-dong-intersection-point-candidate-manifest.v1"
)
AUDIT_SCHEMA_VERSION = "administrative-dong-intersection-point-candidate-audit.v1"
COMPLETE_SCHEMA_VERSION = (
    "administrative-dong-intersection-point-candidate-complete.v1"
)
ASSIGNMENT_STATE = "UniqueHistoricalBoundaryCover"
MULTIPLE_ASSIGNMENT_STATE = "UnresolvedMultipleHistoricalBoundaryCover"
OWNERSHIP_BASIS = "OwnSourcePointHistoricalBoundaryCover"
COMPLETION_CODE = "LocalPrivateHistoricalIntersectionPointCandidateGenerated"

AUTHORITY_FLAGS = {
    "privateReviewOnly": True,
    "historicalBoundaryBootstrapOnly": True,
    "observationCandidateOnly": True,
    "sourceDeclaredCoordinateReference": True,
    "currentAdministrativeBoundaryEstablished": False,
    "currentIntersectionTopologyEstablished": False,
    "approachDirectionEstablished": False,
    "controllerBindingEstablished": False,
    "laneBindingEstablished": False,
    "signalBindingEstablished": False,
    "crosswalkAssignmentInherited": False,
    "distributionApproved": False,
    "publicDisplayAllowed": False,
    "databasePersistenceCompleted": False,
    "runtimeAuthorized": False,
    "traversalReady": False,
    "gameplayReady": False,
    "unityApplyAllowed": False,
}

EXPECTED_COUNTS = {
    "sourceRows": 8_097,
    "sourcePointGeometryRows": 8_097,
    "sourceCoordinateAttributePresentRows": 7_796,
    "sourceCoordinateAttributeMissingRows": 301,
    "uniquelyAssignedRows": 554,
    "outsideScopeRows": 7_543,
    "unresolvedMultipleBoundaryRows": 0,
    "candidateRows": 554,
    "administrativeAreasWithCandidates": 30,
    "sourceDistrictSpatialAssignmentNormalRows": 553,
    "sourceDistrictSpatialAssignmentConflictRows": 1,
    "crosswalkLinkedCandidateRows": 524,
    "crosswalkUnlinkedCandidateRows": 30,
    "linkedCrosswalkRelations": 1_491,
    "linkedCrosswalkSameHistoricalHjdRelations": 1_271,
    "linkedCrosswalkOtherHistoricalHjdRelations": 220,
}

EXPECTED_PER_AREA = {
    "1121574000": [17, 17, 0, 43, 34, 9, 0],
    "1121575000": [11, 11, 0, 27, 18, 9, 0],
    "1121576000": [11, 11, 0, 24, 23, 1, 0],
    "1121577000": [16, 15, 1, 42, 30, 12, 0],
    "1123056000": [24, 23, 1, 65, 58, 7, 0],
    "1123057000": [10, 9, 1, 28, 19, 9, 0],
    "1123060000": [27, 24, 3, 68, 65, 3, 1],
    "1123061000": [19, 19, 0, 49, 36, 13, 0],
    "1123065000": [25, 22, 3, 54, 44, 10, 0],
    "1123066000": [28, 28, 0, 81, 72, 9, 0],
    "1123072000": [16, 13, 3, 39, 26, 13, 0],
    "1123073000": [16, 14, 2, 36, 34, 2, 0],
    "1123074000": [14, 14, 0, 38, 37, 1, 0],
    "1123075000": [14, 14, 0, 36, 36, 0, 0],
    "1126052000": [15, 15, 0, 45, 36, 9, 0],
    "1126054000": [13, 13, 0, 40, 35, 5, 0],
    "1126055000": [11, 11, 0, 42, 28, 14, 0],
    "1126056500": [19, 18, 1, 52, 44, 8, 0],
    "1126057000": [17, 17, 0, 58, 47, 11, 0],
    "1126057500": [17, 17, 0, 46, 40, 6, 0],
    "1126058000": [11, 8, 3, 17, 15, 2, 0],
    "1126059000": [11, 11, 0, 39, 29, 10, 0],
    "1126060000": [5, 4, 1, 13, 12, 1, 0],
    "1126061000": [26, 24, 2, 64, 52, 12, 0],
    "1126062000": [27, 26, 1, 73, 68, 5, 0],
    "1126063000": [19, 19, 0, 55, 50, 5, 0],
    "1126065500": [51, 47, 4, 137, 127, 10, 0],
    "1126066000": [7, 7, 0, 23, 17, 6, 0],
    "1126068000": [38, 35, 3, 106, 100, 6, 0],
    "1126069000": [19, 18, 1, 51, 39, 12, 0],
}
PER_AREA_FIELDS = (
    "candidateRows",
    "crosswalkLinkedCandidateRows",
    "crosswalkUnlinkedCandidateRows",
    "linkedCrosswalkRelations",
    "linkedCrosswalkSameHistoricalHjdRelations",
    "linkedCrosswalkOtherHistoricalHjdRelations",
    "sourceDistrictSpatialAssignmentConflictRows",
)

EXPECTED_ASSIGNED_SOURCE_DISTRICT_COUNTS = {
    "200": 1,
    "210": 55,
    "230": 192,
    "260": 306,
}
SOURCE_BOROUGH_NAMES = {
    "110": "종로구",
    "140": "중구",
    "170": "용산구",
    "200": "성동구",
    "210": "광진구",
    "230": "동대문구",
    "260": "중랑구",
    "290": "성북구",
    "305": "강북구",
    "320": "도봉구",
    "350": "노원구",
    "380": "은평구",
    "410": "서대문구",
    "440": "마포구",
    "470": "양천구",
    "500": "강서구",
    "530": "구로구",
    "545": "금천구",
    "560": "영등포구",
    "590": "동작구",
    "620": "관악구",
    "650": "서초구",
    "680": "강남구",
    "710": "송파구",
    "740": "강동구",
}

EXPECTED_CONFLICT = {
    "sourceRecordNumber": 4_301,
    "sourceManagementNumber": "82-0000000970",
    "intersectionManagementNumber": "149",
    "sourceDistrictCode": "200",
    "sourceBoroughName": "성동구",
    "administrativeAreaStableId": "region:kr:hjd:1123060000",
    "spatialAssignmentBoroughName": "동대문구",
    "distanceToAssignedHistoricalBoundaryMeters": 0.255,
}

EXPECTED_SOURCE_FIELDS = (
    ("CSS_NUM", "N", 38, 0),
    ("ESB_YMD", "C", 8, 0),
    ("CSS_NAM", "C", 100, 0),
    ("A008_KND_C", "C", 3, 0),
    ("LK_CS_CDE", "N", 38, 0),
    ("GU_CDE", "C", 3, 0),
    ("JIBUN", "C", 32, 0),
    ("OD_PE_CDE", "C", 3, 0),
    ("NW_PE_CDE", "C", 3, 0),
    ("WORK_CDE", "C", 3, 0),
    ("VIEW_CDE", "C", 3, 0),
    ("CUST_NUM", "C", 32, 0),
    ("CTT_CFN", "C", 32, 0),
    ("MSE_NUM", "C", 32, 0),
    ("ROD_GBN_CD", "C", 3, 0),
    ("TFC_BSS_CD", "C", 3, 0),
    ("MGRNU", "C", 13, 0),
    ("SIXID", "C", 11, 0),
    ("HISID", "N", 38, 0),
    ("DONG_CDE", "C", 5, 0),
    ("FRM_CDE", "C", 3, 0),
    ("VFLAG", "C", 1, 0),
    ("RN_CDE", "C", 7, 0),
    ("MNG_AGEN", "C", 3, 0),
    ("XCE", "N", 38, 0),
    ("YCE", "N", 38, 0),
)

EXPECTED_INTERSECTION_ARCHIVE_ENTRIES = {
    "A008_P/": (0, 0, "00000000", "E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855"),
    "A008_P/A008_P.cpg": (6, 8, "3178FEB1", "EC0FED838F6249DFC243F5AD3EACB38C28AED69F7C66BD9103CBB66DF9A9560B"),
    "A008_P/A008_P.dbf": (4_000_784, 370_906, "77E2664E", "300C982881050EFCEEF0C0845D3F72209AB93FF436AC9B476967BFF787D3C6CB"),
    "A008_P/A008_P.prj": (425, 276, "41324C4B", "D1E193C7D79D44DA8888B37D42DBEE5D772C92F0AF76A4190EA096DEBCDD99EC"),
    "A008_P/A008_P.qmd": (678, 334, "155DB081", "062248D2CC2ACE6B3367B7C29E6532E6E6222215E312A8DC8FEDBEECA13D6C7F"),
    "A008_P/A008_P.shp": (226_816, 142_381, "BF4ED566", "D8E5336CE7FD768F01AE0F526E210BF316A0540B7FEB09863DBBAD3C83C721E3"),
    "A008_P/A008_P.shx": (64_876, 19_177, "5DECF509", "867ED3B1272D3C402A56894E92ADAC7475947AC6DE8285D6158E346AB42BAED0"),
}

EXPECTED_OUTPUT_FILES = {
    "audit.json",
    "candidates.ndjson",
    "complete.json",
    "generator-source.py",
    "manifest.json",
}

CANDIDATE_HASH_HEADER_FIELDS = (
    "candidateSchemaVersion",
    "revision",
    "designHashSha256",
    "dataImplementationHashSha256",
    "scopeDefinitionHashSha256",
    "oa15534SourceHashSha256",
    "oa15534AcquisitionReceiptHashSha256",
    "oa22160BoundaryArchiveHashSha256",
    "r2ScopeDefinitionHashSha256",
    "r2ScopeManifestHashSha256",
    "g3aScopeDefinitionHashSha256",
    "g3aManifestHashSha256",
    "g3aCandidatesHashSha256",
    "g3aCompleteHashSha256",
    "g3aCandidateSetHashSha256",
    "generatorHashSha256",
    "authorityFlagsCanonicalJson",
)
CANDIDATE_HASH_FIELDS = (
    "candidateStableId",
    "sourceFeatureKey",
    "administrativeAreaStableId",
    "assignmentStateCode",
    "candidateQualityCode",
    "sourceManagementNumber",
    "intersectionManagementNumber",
    "sourceGeometryPointEpsg5186CanonicalJson",
    "commonEnuMillimetersCanonicalJson",
    "sourceDistrictCode",
    "sourceBoroughName",
    "spatialAssignmentBoroughName",
    "sourceDistrictSpatialAssignmentConflictLowercase",
    "sourceCoordinateAttributeStateCode",
    "linkedCrosswalksCanonicalJson",
    "qualityDiagnosticCodesCanonicalJson",
    "ownershipBasisCode",
)


class IntersectionCandidateError(RuntimeError):
    """동결 입력·범위·결정성 계약 위반."""


def require(condition: bool, code: str) -> None:
    if not condition:
        raise IntersectionCandidateError(code)


def canonical_json_text(value: Any) -> str:
    return json.dumps(
        value,
        ensure_ascii=False,
        sort_keys=True,
        separators=(",", ":"),
        allow_nan=False,
    )


def canonical_json_bytes(value: Any) -> bytes:
    return canonical_json_text(value).encode("utf-8")


def pretty_json_bytes(value: Any) -> bytes:
    return (
        json.dumps(value, ensure_ascii=False, indent=2, allow_nan=False) + "\n"
    ).encode("utf-8")


def sha256_bytes(value: bytes) -> str:
    return hashlib.sha256(value).hexdigest().upper()


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest().upper()


def framed_sha256(values: Iterable[str]) -> str:
    digest = hashlib.sha256()
    for value in values:
        encoded = value.encode("utf-8")
        require(len(encoded) <= 0xFFFFFFFF, "CanonicalHashFieldTooLarge")
        digest.update(struct.pack(">I", len(encoded)))
        digest.update(encoded)
    return digest.hexdigest().upper()


def with_content_hash(value: dict[str, Any]) -> dict[str, Any]:
    result = dict(value)
    result.pop("contentHashSha256", None)
    result["contentHashSha256"] = sha256_bytes(canonical_json_bytes(result))
    return result


def validate_content_hash(value: dict[str, Any], code: str) -> None:
    expected = value.get("contentHashSha256")
    unhashed = dict(value)
    unhashed.pop("contentHashSha256", None)
    require(expected == sha256_bytes(canonical_json_bytes(unhashed)), code)


def _stat_has_reparse_attribute(value: Any) -> bool:
    flag = getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0x400)
    return bool(getattr(value, "st_file_attributes", 0) & flag)


def is_reparse_path(path: Path) -> bool:
    try:
        info = path.lstat()
    except FileNotFoundError:
        return False
    return path.is_symlink() or _stat_has_reparse_attribute(info)


def require_repository_root(root: Path) -> Path:
    resolved = root.resolve(strict=True)
    require(resolved.is_dir(), "RepositoryRootNotDirectory")
    require((resolved / ".git").exists(), "RepositoryMarkerMissing")
    require(not is_reparse_path(resolved), "RepositoryRootReparsePointRejected")
    return resolved


def repository_path(root: Path, relative: str | Path) -> Path:
    candidate_relative = Path(relative)
    require(not candidate_relative.is_absolute(), "AbsoluteRepositoryPathRejected")
    require(
        candidate_relative.parts
        and all(part not in {"", ".", ".."} for part in candidate_relative.parts),
        "RepositoryPathTraversalRejected",
    )
    candidate = root.joinpath(candidate_relative)
    try:
        candidate.relative_to(root)
    except ValueError as exc:
        raise IntersectionCandidateError("RepositoryPathEscapeRejected") from exc
    current = root
    for part in candidate_relative.parts:
        current = current / part
        if current.exists() or current.is_symlink():
            require(not is_reparse_path(current), "RepositoryReparsePathRejected")
    return candidate


def create_plain_directory(root: Path, relative: str | Path) -> Path:
    target = repository_path(root, relative)
    current = root
    for part in Path(relative).parts:
        current = current / part
        if not current.exists():
            current.mkdir()
        require(current.is_dir(), "OutputDirectoryPathIsNotDirectory")
        require(not is_reparse_path(current), "OutputDirectoryReparsePointRejected")
    return target


def load_json(path: Path) -> dict[str, Any]:
    try:
        result = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        raise IntersectionCandidateError(f"JsonReadFailed:{path.name}") from exc
    require(isinstance(result, dict), f"JsonRootInvalid:{path.name}")
    return result


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
    runtime = repository_path(root, "artifacts/local/public-data/gis-runtime-r1")
    require(runtime.is_dir(), "SpatialGenerationDependencyMissing")
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
        raise IntersectionCandidateError("SpatialGenerationDependencyMissing") from exc
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
class IntersectionSourceRow:
    record_number: int
    source_management_number: str
    intersection_management_number: str
    source_district_code: str
    attribute_x: int | None
    attribute_y: int | None
    geometry_x: float
    geometry_y: float


@dataclass(frozen=True)
class CrosswalkLink:
    source_feature_key: str
    crosswalk_management_number: str
    administrative_area_stable_id: str


@dataclass(frozen=True)
class PreparedGeneration:
    candidate_set_hash: str
    generation_relative: Path
    files: dict[str, bytes]
    candidate_count: int
    counts: dict[str, int]


def source_by_role(scope: dict[str, Any], role: str) -> dict[str, Any]:
    matches = [item for item in scope.get("sources", []) if item.get("role") == role]
    require(len(matches) == 1, f"ScopeSourceMissing:{role}")
    return matches[0]


def verify_frozen_file(root: Path, source: dict[str, Any]) -> Path:
    path = repository_path(root, source.get("repositoryRelativePath", ""))
    require(path.is_file(), f"SourceMissing:{source.get('role', 'unknown')}")
    require(not is_reparse_path(path), f"SourceReparseRejected:{source.get('role', 'unknown')}")
    require(
        path.stat().st_size == source.get("byteLength"),
        f"SourceLengthMismatch:{source.get('role', 'unknown')}",
    )
    require(
        sha256_file(path) == source.get("contentHashSha256"),
        f"SourceHashMismatch:{source.get('role', 'unknown')}",
    )
    return path


def _expected_per_area_objects() -> dict[str, dict[str, int]]:
    return {
        code: dict(zip(PER_AREA_FIELDS, values, strict=True))
        for code, values in EXPECTED_PER_AREA.items()
    }


def _expected_source_archive_entry_objects() -> list[dict[str, Any]]:
    return [
        {
            "name": name,
            "byteLength": values[0],
            "compressedByteLength": values[1],
            "crc32": values[2],
            "sha256": values[3],
        }
        for name, values in sorted(EXPECTED_INTERSECTION_ARCHIVE_ENTRIES.items())
    ]


def _expected_source_field_objects() -> list[dict[str, Any]]:
    return [
        {
            "name": name,
            "type": field_type,
            "length": length,
            "decimalCount": decimal_count,
        }
        for name, field_type, length, decimal_count in EXPECTED_SOURCE_FIELDS
    ]


def load_scope(
    root: Path, scope_relative: Path, dependencies: Dependencies
) -> tuple[dict[str, Any], dict[str, Path], dict[str, str]]:
    scope_path = repository_path(root, scope_relative)
    require(scope_path.is_file(), "ScopeDefinitionMissing")
    scope = load_json(scope_path)
    require(scope.get("schemaVersion") == SCOPE_SCHEMA_VERSION, "ScopeSchemaMismatch")
    require(scope.get("scopeStableId") == SCOPE_STABLE_ID, "ScopeStableIdMismatch")
    require(scope.get("revision") == REVISION, "ScopeRevisionMismatch")
    require(scope.get("generatedAtUtc") == GENERATED_AT_UTC, "ScopeGeneratedAtMismatch")
    require(
        scope.get("coordinateReferenceStatus") == SOURCE_COORDINATE_STATUS,
        "ScopeCoordinateReferenceStatusMismatch",
    )
    require(scope.get("expectedCounts") == EXPECTED_COUNTS, "ScopeExpectedCountsMismatch")
    require(
        scope.get("expectedPerAdministrativeArea") == _expected_per_area_objects(),
        "ScopeExpectedPerAreaMismatch",
    )
    require(
        scope.get("expectedAssignedSourceDistrictCodeCounts")
        == EXPECTED_ASSIGNED_SOURCE_DISTRICT_COUNTS,
        "ScopeSourceDistrictDistributionMismatch",
    )
    require(scope.get("expectedConflict") == EXPECTED_CONFLICT, "ScopeConflictMismatch")
    require(
        scope.get("sourceArchiveEntries")
        == _expected_source_archive_entry_objects(),
        "ScopeSourceArchiveEntriesMismatch",
    )
    require(
        scope.get("sourceDbfSchema") == _expected_source_field_objects(),
        "ScopeSourceDbfSchemaMismatch",
    )
    require(
        scope.get("sourceGeometryAuthorityCode") == "ShapefilePointGeometry"
        and scope.get("dbfCoordinateAttributeDispositionCode")
        == "NonAuthoritativeQualityDiagnosticOnly",
        "ScopeSourceGeometryAuthorityMismatch",
    )
    require(
        scope.get("crosswalkDiagnostic")
        == {
            "revision": CROSSWALK_REVISION,
            "candidateSetHashSha256": CROSSWALK_CANDIDATE_SET_HASH,
            "linkMethodCode": "CssNumEqualsG3aIntersectionManagementNumberAfterOwnAssignment",
            "crosswalkLinkCanAssignAdministrativeArea": False,
        },
        "ScopeCrosswalkDiagnosticMismatch",
    )
    require(scope.get("authorityFlags") == AUTHORITY_FLAGS, "ScopeAuthorityMismatch")
    require(
        scope.get("consumedTrafficSourceKeys") == ["intersection"],
        "ScopeConsumedTrafficSourcesInvalid",
    )
    require(
        scope.get("explicitlyExcludedTrafficLayers")
        == ["controller", "direction", "lane", "signal"],
        "ScopeExcludedTrafficLayersMismatch",
    )
    require(
        scope.get("walkNetworkDispositionCode")
        == "Oa21208Year2020NotConsumedNotCurrent",
        "ScopeWalkNetworkDispositionMismatch",
    )
    require(
        scope.get("laneDatasetDispositionCode")
        == "Oa15537LaneMarkingsNotConsumedNotDrivableLanes",
        "ScopeLaneDatasetDispositionMismatch",
    )
    require(
        scope.get("sourceReceiptSelectionDisposition")
        == {
            "receiptSagajeongBboxSelectedRows": 25,
            "usedForThirtyDongCandidateSelection": False,
            "selectionMethodCode": "WholeSourceOwnPointHistoricalBoundaryCover",
        },
        "ScopeReceiptSelectionDispositionMismatch",
    )

    design_path = repository_path(root, DESIGN_DOCUMENT_RELATIVE)
    implementation_path = repository_path(root, DATA_IMPLEMENTATION_RELATIVE)
    generator_path = repository_path(root, Path(__file__).resolve().relative_to(root))
    design_hash = sha256_file(design_path)
    implementation_hash = sha256_file(implementation_path)
    generator_hash = sha256_file(generator_path)
    planning = scope.get("planningGate", {})
    require(
        planning
        == {
            "statusCode": "ApprovedForLocalPrivateCandidateGeneration",
            "designDocumentRef": DESIGN_DOCUMENT_RELATIVE.as_posix(),
            "designRevision": DESIGN_REVISION,
            "designHashSha256": design_hash,
            "dataImplementationRef": DATA_IMPLEMENTATION_RELATIVE.as_posix(),
            "dataImplementationHashSha256": implementation_hash,
        },
        "ScopePlanningGateMismatch",
    )
    implementation = load_json(implementation_path)
    require(
        implementation.get("schemaVersion") == DATA_IMPLEMENTATION_SCHEMA_VERSION,
        "DataImplementationSchemaMismatch",
    )
    require(
        implementation.get("implementationStableId")
        == DATA_IMPLEMENTATION_STABLE_ID,
        "DataImplementationStableIdMismatch",
    )
    require(implementation.get("revision") == REVISION, "DataImplementationRevisionMismatch")
    require(
        implementation.get("playableLoopWorkOrder") is False
        and implementation.get("evidenceStageClaimed") is None,
        "DataImplementationMustNotClaimPlayableLoopEvidence",
    )
    require(
        implementation.get("planningGate", {}).get("designHashSha256") == design_hash,
        "DataImplementationDesignHashMismatch",
    )
    require(
        implementation.get("expectedCounts") == EXPECTED_COUNTS,
        "DataImplementationExpectedCountsMismatch",
    )
    require(
        implementation.get("authorityFlags") == AUTHORITY_FLAGS,
        "DataImplementationAuthorityMismatch",
    )
    require(
        implementation.get("completionCode") == COMPLETION_CODE,
        "DataImplementationCompletionCodeMismatch",
    )

    coordinate_frame = scope.get("coordinateFrame")
    require(
        coordinate_frame
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
    conversion = scope.get("historicalBoundaryConversion")
    require(
        conversion
        == {
            "sourceCoordinateReference": "EPSG:5181",
            "assignmentCoordinateReference": "EPSG:5186",
            "xOffsetMeters": 0.0,
            "yOffsetMeters": 100000.0,
            "methodCode": "SameKgd2002CentralBeltFalseNorthingOffsetOnly",
            "rotationApplied": False,
            "scaleApplied": False,
            "inferredTranslationApplied": False,
        },
        "ScopeBoundaryConversionMismatch",
    )

    areas = scope.get("administrativeAreas")
    require(isinstance(areas, list) and len(areas) == 30, "ScopeAdministrativeAreasInvalid")
    area_ids = [item.get("administrativeAreaStableId") for item in areas]
    require(area_ids == sorted(area_ids), "ScopeAdministrativeAreaOrderInvalid")
    codes = [str(item).removeprefix("region:kr:hjd:") for item in area_ids]
    require(codes == sorted(EXPECTED_PER_AREA), "ScopeAdministrativeAreaAllowListMismatch")
    require(len(set(area_ids)) == 30, "ScopeAdministrativeAreaDuplicate")

    toolchain = scope.get("output", {}).get("toolchain", {})
    actual_toolchain = {
        "generatorRelativePath": generator_path.relative_to(root).as_posix(),
        "generatorSha256": generator_hash,
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
    require(toolchain == actual_toolchain, "ScopeToolchainMismatch")
    output = scope.get("output", {})
    require(
        output.get("repositoryRelativeDirectory")
        == "artifacts/local/public-data/admin-dong-intersection-northeast-seoul-20260915-g3b-r1"
        and output.get("generationDirectoryName") == "generations"
        and output.get("candidateSchemaVersion") == CANDIDATE_SCHEMA_VERSION
        and output.get("currentPointerAllowed") is False,
        "ScopeOutputContractMismatch",
    )

    roles = (
        "oa15534IntersectionArchive",
        "oa15534AcquisitionReceipt",
        "oa22160BoundaryArchive",
        "r2ScopeDefinition",
        "r2ScopeManifest",
        "g3aScopeDefinition",
        "g3aManifest",
        "g3aCandidates",
        "g3aComplete",
    )
    require(len(scope.get("sources", [])) == len(roles), "ScopeSourceCountMismatch")
    paths: dict[str, Path] = {}
    hashes: dict[str, str] = {}
    for role in roles:
        source = source_by_role(scope, role)
        path = verify_frozen_file(root, source)
        paths[role] = path
        hashes[role] = sha256_file(path)

    r2_scope = load_json(paths["r2ScopeDefinition"])
    r2_area_ids = sorted(
        item.get("administrativeAreaStableId")
        for item in r2_scope.get("administrativeAreas", [])
    )
    require(r2_area_ids == area_ids, "R2ScopeAdministrativeAreaMismatch")
    r2_manifest = load_json(paths["r2ScopeManifest"])
    r2_manifest_ids = sorted(
        item.get("administrativeAreaStableId")
        for item in r2_manifest.get("modules", [])
    )
    require(r2_manifest_ids == area_ids, "R2ManifestAdministrativeAreaMismatch")
    require(
        r2_manifest.get("distributionApproved") is False,
        "R2ManifestAuthorityUnexpectedlyChanged",
    )

    receipt = load_json(paths["oa15534AcquisitionReceipt"])
    receipt_rows = [
        item for item in receipt.get("sources", []) if item.get("key") == "intersection"
    ]
    require(len(receipt_rows) == 1, "IntersectionReceiptEntryMissing")
    receipt_row = receipt_rows[0]
    require(
        receipt_row.get("infId") == "OA-15534"
        and receipt_row.get("fileName") == "A008_P_20250814.zip"
        and receipt_row.get("fileModifiedDate") == "2025-08-14T00:00:00+00:00"
        and receipt_row.get("coordinateReferenceSystem") == "EPSG:5186"
        and receipt_row.get("sha256") == hashes["oa15534IntersectionArchive"]
        and receipt_row.get("contentLength") == 534_342
        and receipt_row.get("totalRows") == 8_097
        and receipt_row.get("selectedRows") == 25
        and receipt_row.get("crsNote") == "PortalAndPrjDeclareEpsg5186",
        "IntersectionReceiptLineageMismatch",
    )

    g3a_scope = load_json(paths["g3aScopeDefinition"])
    require(
        g3a_scope.get("revision") == CROSSWALK_REVISION,
        "CrosswalkScopeRevisionMismatch",
    )
    g3a_manifest = load_json(paths["g3aManifest"])
    require(
        g3a_manifest.get("revision") == CROSSWALK_REVISION
        and g3a_manifest.get("candidateSetHashSha256")
        == CROSSWALK_CANDIDATE_SET_HASH,
        "CrosswalkManifestLineageMismatch",
    )
    g3a_complete = load_json(paths["g3aComplete"])
    require(
        g3a_complete.get("completeMarker") is True
        and g3a_complete.get("revision") == CROSSWALK_REVISION
        and g3a_complete.get("candidateSetHashSha256")
        == CROSSWALK_CANDIDATE_SET_HASH,
        "CrosswalkCompleteMarkerMismatch",
    )
    completed_candidate_files = [
        item
        for item in g3a_complete.get("files", [])
        if item.get("relativePath") == "candidates.ndjson"
    ]
    require(
        len(completed_candidate_files) == 1
        and completed_candidate_files[0].get("sha256") == hashes["g3aCandidates"]
        and completed_candidate_files[0].get("byteLength")
        == paths["g3aCandidates"].stat().st_size
        and completed_candidate_files[0].get("recordCount") == 1_533,
        "CrosswalkCandidateFileLineageMismatch",
    )
    return scope, paths, hashes


def normalized_dong_name(value: str) -> str:
    compact = value.replace("서울특별시", "").replace("광진구", "")
    compact = compact.replace("동대문구", "").replace("중랑구", "")
    return compact.replace("제", "").replace("·", ".").replace(" ", "")


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
            require(
                {"ADSTRD_CD", "ADSTRD_NM"}.issubset(field_names),
                "BoundaryFieldsMissing",
            )
            selected: dict[str, Boundary] = {}
            for shape_record in reader.iterShapeRecords():
                properties = shape_record.record.as_dict()
                code = str(properties["ADSTRD_CD"]) + "00"
                if code not in expected_areas:
                    continue
                require(shape_record.shape.shapeType == 5, f"BoundaryShapeTypeInvalid:{code}")
                geometry = dependencies.shape(shape_record.shape.__geo_interface__)
                require(
                    isinstance(
                        geometry, (dependencies.Polygon, dependencies.MultiPolygon)
                    )
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
                selected[code] = Boundary(
                    code,
                    f"region:kr:hjd:{code}",
                    expected_name,
                    expected_areas[code]["boroughName"],
                    dependencies.translate(geometry, yoff=100_000.0),
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
        "selectedBoundaryCount": 30,
        "sourceCoordinateReference": "EPSG:5181",
        "assignmentCoordinateReference": "EPSG:5186",
        "northingOffsetMeters": 100_000.0,
        "unionAreaSquareMeters": round(float(union.area), 3),
        "interiorOverlapAreaSquareMeters": round(float(overlap_area), 6),
        "historicalBoundaryBootstrapOnly": True,
        "currentAdministrativeBoundaryEstablished": False,
    }


def read_intersections(
    path: Path, scope: dict[str, Any], dependencies: Dependencies
) -> tuple[list[IntersectionSourceRow], dict[str, Any]]:
    with zipfile.ZipFile(path, "r") as archive:
        infos = archive.infolist()
        require(
            len(infos) == len({item.filename for item in infos}),
            "IntersectionArchiveDuplicateEntry",
        )
        require(
            {item.filename for item in infos} == set(EXPECTED_INTERSECTION_ARCHIVE_ENTRIES),
            "IntersectionArchiveEntrySetChanged",
        )
        observed_entries: list[dict[str, Any]] = []
        for info in sorted(infos, key=lambda item: item.filename):
            data = archive.read(info.filename)
            expected = EXPECTED_INTERSECTION_ARCHIVE_ENTRIES[info.filename]
            actual = (
                len(data),
                info.compress_size,
                f"{info.CRC:08X}",
                sha256_bytes(data),
            )
            require(actual == expected, f"IntersectionArchiveEntryChanged:{info.filename}")
            observed_entries.append(
                {
                    "name": info.filename,
                    "byteLength": actual[0],
                    "compressedByteLength": actual[1],
                    "crc32": actual[2],
                    "sha256": actual[3],
                }
            )
        require(archive.read("A008_P/A008_P.cpg") == b"EUC-KR", "IntersectionCpgChanged")
        source_crs = dependencies.CRS.from_wkt(
            archive.read("A008_P/A008_P.prj").decode("ascii")
        )
        require(
            source_crs.to_authority() == ("EPSG", "5186"),
            "IntersectionSourceCrsChanged",
        )
        reader = dependencies.shapefile.Reader(
            shp=io.BytesIO(archive.read("A008_P/A008_P.shp")),
            shx=io.BytesIO(archive.read("A008_P/A008_P.shx")),
            dbf=io.BytesIO(archive.read("A008_P/A008_P.dbf")),
            encoding="cp949",
            encodingErrors="strict",
        )
        try:
            require(reader.shapeType == 1, "IntersectionShapeTypeChanged")
            require(len(reader) == EXPECTED_COUNTS["sourceRows"], "IntersectionRowCountChanged")
            fields = tuple(tuple(item) for item in reader.fields[1:])
            require(fields == EXPECTED_SOURCE_FIELDS, "IntersectionDbfSchemaChanged")
            rows: list[IntersectionSourceRow] = []
            source_management_numbers: set[str] = set()
            intersection_management_numbers: set[str] = set()
            missing_attributes = 0
            present_attributes = 0
            for record_number, shape_record in enumerate(
                reader.iterShapeRecords(), start=1
            ):
                require(shape_record.shape.shapeType == 1, "IntersectionRecordShapeTypeChanged")
                require(
                    len(shape_record.shape.points) == 1,
                    "IntersectionPointCardinalityChanged",
                )
                x, y = shape_record.shape.points[0]
                require(math.isfinite(x) and math.isfinite(y), "IntersectionGeometryNotFinite")
                record = shape_record.record.as_dict()
                source_management_number = str(record["MGRNU"]).strip()
                require(
                    re.fullmatch(r"[0-9]{2}-[0-9]{10}", source_management_number)
                    is not None,
                    "IntersectionSourceManagementNumberInvalid",
                )
                require(
                    source_management_number not in source_management_numbers,
                    "IntersectionSourceManagementNumberDuplicate",
                )
                source_management_numbers.add(source_management_number)
                try:
                    intersection_number_value = int(record["CSS_NUM"])
                except (TypeError, ValueError) as exc:
                    raise IntersectionCandidateError(
                        "IntersectionManagementNumberInvalid"
                    ) from exc
                require(intersection_number_value > 0, "IntersectionManagementNumberInvalid")
                intersection_management_number = str(intersection_number_value)
                require(
                    intersection_management_number
                    not in intersection_management_numbers,
                    "IntersectionManagementNumberDuplicate",
                )
                intersection_management_numbers.add(intersection_management_number)
                source_district_code = str(record["GU_CDE"]).strip()
                require(
                    re.fullmatch(r"[0-9]{3}", source_district_code) is not None,
                    "IntersectionSourceDistrictCodeInvalid",
                )
                attribute_x = record["XCE"]
                attribute_y = record["YCE"]
                require(
                    (attribute_x is None) == (attribute_y is None),
                    "IntersectionPartialCoordinateAttributeMissing",
                )
                if attribute_x is None:
                    missing_attributes += 1
                    normalized_x = None
                    normalized_y = None
                else:
                    present_attributes += 1
                    normalized_x = int(attribute_x)
                    normalized_y = int(attribute_y)
                rows.append(
                    IntersectionSourceRow(
                        record_number,
                        source_management_number,
                        intersection_management_number,
                        source_district_code,
                        normalized_x,
                        normalized_y,
                        float(x),
                        float(y),
                    )
                )
        finally:
            reader.close()
    require(len(rows) == EXPECTED_COUNTS["sourceRows"], "IntersectionSourceRowsMismatch")
    require(
        missing_attributes == EXPECTED_COUNTS["sourceCoordinateAttributeMissingRows"]
        and present_attributes
        == EXPECTED_COUNTS["sourceCoordinateAttributePresentRows"],
        "IntersectionCoordinateAttributeCountsChanged",
    )
    return rows, {
        "sourceRows": len(rows),
        "sourcePointGeometryRows": len(rows),
        "sourceCoordinateAttributePresentRows": present_attributes,
        "sourceCoordinateAttributeMissingRows": missing_attributes,
        "sourceManagementNumberUniqueRows": len(source_management_numbers),
        "intersectionManagementNumberUniqueRows": len(
            intersection_management_numbers
        ),
        "sourceCoordinateReference": "EPSG:5186",
        "sourceCoordinateReferenceStatus": SOURCE_COORDINATE_STATUS,
        "sourceGeometryAuthorityCode": "ShapefilePointGeometry",
        "dbfCoordinateAttributeDispositionCode": "NonAuthoritativeQualityDiagnosticOnly",
        "dbfDecodeCode": "CpgEucKrReadWithCp949StrictSuperset",
        "archiveEntries": observed_entries,
    }


def read_crosswalk_links(
    path: Path, administrative_area_ids: set[str]
) -> tuple[dict[str, list[CrosswalkLink]], dict[str, Any]]:
    by_intersection: dict[str, list[CrosswalkLink]] = defaultdict(list)
    source_feature_keys: set[str] = set()
    row_count = 0
    with path.open("r", encoding="utf-8", newline="") as stream:
        for line_number, raw_line in enumerate(stream, start=1):
            require(raw_line.endswith("\n"), "CrosswalkNdjsonLineTerminationChanged")
            require(raw_line.strip() != "", "CrosswalkNdjsonBlankLine")
            try:
                value = json.loads(raw_line)
            except json.JSONDecodeError as exc:
                raise IntersectionCandidateError(
                    f"CrosswalkNdjsonInvalid:{line_number}"
                ) from exc
            require(isinstance(value, dict), "CrosswalkCandidateRootInvalid")
            require(
                value.get("schemaVersion") == "administrative-dong-crosswalk-candidate.v1"
                and value.get("revision") == CROSSWALK_REVISION
                and value.get("candidateSetHashSha256")
                == CROSSWALK_CANDIDATE_SET_HASH,
                "CrosswalkCandidateLineageMismatch",
            )
            feature_key = value.get("sourceFeatureKey")
            management_number = value.get("crosswalkManagementNumber")
            area_id = value.get("administrativeAreaStableId")
            require(
                isinstance(feature_key, str)
                and feature_key
                and feature_key not in source_feature_keys,
                "CrosswalkSourceFeatureKeyInvalid",
            )
            source_feature_keys.add(feature_key)
            require(
                isinstance(management_number, str) and management_number,
                "CrosswalkManagementNumberInvalid",
            )
            require(area_id in administrative_area_ids, "CrosswalkAdministrativeAreaInvalid")
            intersection_number = value.get("intersectionManagementNumber")
            if intersection_number is not None:
                normalized = str(intersection_number).strip()
                require(normalized != "", "CrosswalkIntersectionNumberInvalid")
                by_intersection[normalized].append(
                    CrosswalkLink(feature_key, management_number, area_id)
                )
            row_count += 1
    require(row_count == 1_533, "CrosswalkCandidateRowCountChanged")
    for links in by_intersection.values():
        links.sort(key=lambda item: item.source_feature_key)
    return dict(by_intersection), {
        "crosswalkCandidateRows": row_count,
        "crosswalkCandidatesWithIntersectionReference": sum(
            len(items) for items in by_intersection.values()
        ),
        "distinctReferencedIntersectionManagementNumbers": len(by_intersection),
    }


def candidate_stable_id(source_management_number: str) -> str:
    digest = framed_sha256(
        ("OA-15534", SOURCE_REVISION, source_management_number)
    ).lower()
    return f"candidate:kr:seoul:oa15534:intersection:{digest}"


def _link_documents(
    links: Sequence[CrosswalkLink], own_area_id: str
) -> tuple[list[dict[str, Any]], int, int]:
    documents: list[dict[str, Any]] = []
    same_count = 0
    other_count = 0
    for link in links:
        same = link.administrative_area_stable_id == own_area_id
        same_count += int(same)
        other_count += int(not same)
        documents.append(
            {
                "sourceFeatureKey": link.source_feature_key,
                "crosswalkManagementNumber": link.crosswalk_management_number,
                "administrativeAreaStableId": link.administrative_area_stable_id,
                "sameHistoricalAdministrativeArea": same,
            }
        )
    return documents, same_count, other_count


def assign_candidates(
    rows: Sequence[IntersectionSourceRow],
    boundaries: Sequence[Boundary],
    crosswalk_links: dict[str, list[CrosswalkLink]],
    dependencies: Dependencies,
    frame: CommonEnuFrame,
    transformer: Any,
) -> tuple[list[dict[str, Any]], dict[str, Any]]:
    candidates: list[dict[str, Any]] = []
    outside_rows = 0
    multiple_rows = 0
    source_district_counts: Counter[str] = Counter()
    per_area = {
        boundary.code: {field: 0 for field in PER_AREA_FIELDS}
        for boundary in boundaries
    }
    conflicts: list[dict[str, Any]] = []
    linked_candidates = 0
    unlinked_candidates = 0
    link_relations = 0
    same_relations = 0
    other_relations = 0

    for row in rows:
        point = dependencies.Point(row.geometry_x, row.geometry_y)
        covered = [boundary for boundary in boundaries if boundary.geometry.covers(point)]
        if not covered:
            outside_rows += 1
            continue
        if len(covered) > 1:
            multiple_rows += 1
            continue
        boundary = covered[0]
        own_area_id = boundary.stable_id
        require(
            row.source_district_code in SOURCE_BOROUGH_NAMES,
            "AssignedIntersectionSourceDistrictCodeUnknown",
        )
        source_borough_name = SOURCE_BOROUGH_NAMES[row.source_district_code]
        source_district_counts[row.source_district_code] += 1
        district_conflict = source_borough_name != boundary.borough_name
        links = crosswalk_links.get(row.intersection_management_number, [])
        link_documents, same_count, other_count = _link_documents(links, own_area_id)
        if links:
            linked_candidates += 1
        else:
            unlinked_candidates += 1
        link_relations += len(links)
        same_relations += same_count
        other_relations += other_count

        diagnostics = ["HistoricalBoundaryBootstrapOnly"]
        if row.attribute_x is None:
            attribute_state = "MissingBoth"
            diagnostics.append("SourceCoordinateAttributeMissing")
        else:
            attribute_state = "PresentNonAuthoritativeInteger"
        if district_conflict:
            diagnostics.append("SourceDistrictSpatialAssignmentConflict")
        if not links:
            diagnostics.append("NoInScopeCrosswalkLinkObserved")
            link_state = "NoInScopeCrosswalkLinkObserved"
        elif other_count:
            diagnostics.append("CrosswalkLinkObservedOtherHistoricalHjd")
            link_state = "ObservedWithOtherHistoricalHjd"
        else:
            link_state = "ObservedSameHistoricalHjdOnly"
        diagnostics.sort()

        longitude, latitude = transformer.transform(row.geometry_x, row.geometry_y)
        require(
            math.isfinite(longitude) and math.isfinite(latitude),
            "IntersectionWgs84TransformNotFinite",
        )
        local_x, local_z = frame.wgs84_to_local(longitude, latitude)
        boundary_distance = round(float(point.distance(boundary.geometry.boundary)), 3)
        candidate = {
            "schemaVersion": CANDIDATE_SCHEMA_VERSION,
            "revision": REVISION,
            "candidateStableId": candidate_stable_id(row.source_management_number),
            "sourceFeatureKey": f"oa15534:intersection:{row.source_management_number}",
            "sourceRecordNumber": row.record_number,
            "sourceManagementNumber": row.source_management_number,
            "intersectionManagementNumber": row.intersection_management_number,
            "sourceHashSha256": None,
            "sourceCoordinateReferenceStatus": SOURCE_COORDINATE_STATUS,
            "sourceGeometryPointEpsg5186": {
                "x": round(row.geometry_x, 3),
                "y": round(row.geometry_y, 3),
            },
            "commonEnuMillimeters": {
                "x": int(round(local_x * 1000.0)),
                "z": int(round(local_z * 1000.0)),
            },
            "sourceCoordinateAttributeStateCode": attribute_state,
            "administrativeAreaStableId": own_area_id,
            "assignmentStateCode": ASSIGNMENT_STATE,
            "ownershipBasisCode": OWNERSHIP_BASIS,
            "historicalBoundaryHashSha256": None,
            "distanceToAssignedHistoricalBoundaryMeters": boundary_distance,
            "sourceDistrictCode": row.source_district_code,
            "sourceBoroughName": source_borough_name,
            "spatialAssignmentBoroughName": boundary.borough_name,
            "sourceDistrictSpatialAssignmentConflict": district_conflict,
            "crosswalkDiagnosticCandidateSetHashSha256": CROSSWALK_CANDIDATE_SET_HASH,
            "crosswalkLinkStateCode": link_state,
            "linkedCrosswalks": link_documents,
            "qualityDiagnosticCodes": diagnostics,
            "candidateQualityCode": "PendingHumanReview",
            "authorityFlags": dict(AUTHORITY_FLAGS),
            "candidateSetHashSha256": None,
        }
        candidates.append(candidate)

        area_counts = per_area[boundary.code]
        area_counts["candidateRows"] += 1
        area_counts[
            "crosswalkLinkedCandidateRows"
            if links
            else "crosswalkUnlinkedCandidateRows"
        ] += 1
        area_counts["linkedCrosswalkRelations"] += len(links)
        area_counts["linkedCrosswalkSameHistoricalHjdRelations"] += same_count
        area_counts["linkedCrosswalkOtherHistoricalHjdRelations"] += other_count
        area_counts["sourceDistrictSpatialAssignmentConflictRows"] += int(
            district_conflict
        )
        if district_conflict:
            conflicts.append(
                {
                    "sourceRecordNumber": row.record_number,
                    "sourceManagementNumber": row.source_management_number,
                    "intersectionManagementNumber": row.intersection_management_number,
                    "sourceDistrictCode": row.source_district_code,
                    "sourceBoroughName": source_borough_name,
                    "administrativeAreaStableId": own_area_id,
                    "spatialAssignmentBoroughName": boundary.borough_name,
                    "distanceToAssignedHistoricalBoundaryMeters": boundary_distance,
                }
            )

    candidates.sort(key=lambda item: item["sourceManagementNumber"])
    stable_ids = [item["candidateStableId"] for item in candidates]
    feature_keys = [item["sourceFeatureKey"] for item in candidates]
    require(len(stable_ids) == len(set(stable_ids)), "CandidateStableIdDuplicate")
    require(len(feature_keys) == len(set(feature_keys)), "CandidateSourceFeatureKeyDuplicate")
    counts = {
        "sourceRows": len(rows),
        "sourcePointGeometryRows": len(rows),
        "sourceCoordinateAttributePresentRows": sum(
            row.attribute_x is not None for row in rows
        ),
        "sourceCoordinateAttributeMissingRows": sum(
            row.attribute_x is None for row in rows
        ),
        "uniquelyAssignedRows": len(candidates),
        "outsideScopeRows": outside_rows,
        "unresolvedMultipleBoundaryRows": multiple_rows,
        "candidateRows": len(candidates),
        "administrativeAreasWithCandidates": sum(
            item["candidateRows"] > 0 for item in per_area.values()
        ),
        "sourceDistrictSpatialAssignmentNormalRows": len(candidates) - len(conflicts),
        "sourceDistrictSpatialAssignmentConflictRows": len(conflicts),
        "crosswalkLinkedCandidateRows": linked_candidates,
        "crosswalkUnlinkedCandidateRows": unlinked_candidates,
        "linkedCrosswalkRelations": link_relations,
        "linkedCrosswalkSameHistoricalHjdRelations": same_relations,
        "linkedCrosswalkOtherHistoricalHjdRelations": other_relations,
    }
    require(counts == EXPECTED_COUNTS, "GeneratedCountsChanged")
    require(
        dict(sorted(source_district_counts.items()))
        == EXPECTED_ASSIGNED_SOURCE_DISTRICT_COUNTS,
        "GeneratedSourceDistrictCountsChanged",
    )
    require(per_area == _expected_per_area_objects(), "GeneratedPerAreaCountsChanged")
    require(conflicts == [EXPECTED_CONFLICT], "GeneratedConflictChanged")
    return candidates, {
        "counts": counts,
        "perAdministrativeArea": per_area,
        "assignedSourceDistrictCodeCounts": dict(
            sorted(source_district_counts.items())
        ),
        "sourceDistrictSpatialAssignmentConflicts": conflicts,
    }


def candidate_hash_field_values(candidate: dict[str, Any]) -> list[str]:
    return [
        candidate["candidateStableId"],
        candidate["sourceFeatureKey"],
        candidate["administrativeAreaStableId"],
        candidate["assignmentStateCode"],
        candidate["candidateQualityCode"],
        candidate["sourceManagementNumber"],
        candidate["intersectionManagementNumber"],
        canonical_json_text(candidate["sourceGeometryPointEpsg5186"]),
        canonical_json_text(candidate["commonEnuMillimeters"]),
        candidate["sourceDistrictCode"],
        candidate["sourceBoroughName"],
        candidate["spatialAssignmentBoroughName"],
        str(candidate["sourceDistrictSpatialAssignmentConflict"]).lower(),
        candidate["sourceCoordinateAttributeStateCode"],
        canonical_json_text(candidate["linkedCrosswalks"]),
        canonical_json_text(candidate["qualityDiagnosticCodes"]),
        candidate["ownershipBasisCode"],
    ]


def candidate_set_hash(
    header_values: Sequence[str], candidates: Sequence[dict[str, Any]]
) -> str:
    values = list(header_values)
    for candidate in candidates:
        values.extend(candidate_hash_field_values(candidate))
    return framed_sha256(values)


def independently_recalculate_candidate_set_hash(
    header_values: Sequence[str], candidates: Sequence[dict[str, Any]]
) -> str:
    chunks: list[bytes] = []
    for value in list(header_values) + [
        field
        for candidate in candidates
        for field in candidate_hash_field_values(candidate)
    ]:
        encoded = value.encode("utf-8")
        chunks.append(len(encoded).to_bytes(4, byteorder="big", signed=False))
        chunks.append(encoded)
    return hashlib.sha256(b"".join(chunks)).hexdigest().upper()


def prepare_generation(
    root: Path, scope_relative: Path, dependencies: Dependencies
) -> PreparedGeneration:
    scope, source_paths, source_hashes = load_scope(
        root, scope_relative, dependencies
    )
    scope_path = repository_path(root, scope_relative)
    scope_hash = sha256_file(scope_path)
    design_hash = sha256_file(repository_path(root, DESIGN_DOCUMENT_RELATIVE))
    implementation_hash = sha256_file(
        repository_path(root, DATA_IMPLEMENTATION_RELATIVE)
    )
    generator_path = Path(__file__).resolve()
    generator_bytes = generator_path.read_bytes()
    generator_hash = sha256_bytes(generator_bytes)

    boundaries, boundary_audit = read_boundaries(
        source_paths["oa22160BoundaryArchive"], scope, dependencies
    )
    source_rows, source_audit = read_intersections(
        source_paths["oa15534IntersectionArchive"], scope, dependencies
    )
    area_ids = {item.stable_id for item in boundaries}
    crosswalk_links, crosswalk_source_audit = read_crosswalk_links(
        source_paths["g3aCandidates"], area_ids
    )
    coordinate_frame = scope["coordinateFrame"]
    frame = CommonEnuFrame(
        coordinate_frame["originLatitude"],
        coordinate_frame["originLongitude"],
        coordinate_frame["worldOffsetX"],
        coordinate_frame["worldOffsetZ"],
    )
    transformer = dependencies.Transformer.from_crs(
        "EPSG:5186", "EPSG:4326", always_xy=True
    )
    candidates, assignment_audit = assign_candidates(
        source_rows,
        boundaries,
        crosswalk_links,
        dependencies,
        frame,
        transformer,
    )
    for candidate in candidates:
        candidate["sourceHashSha256"] = source_hashes["oa15534IntersectionArchive"]
        candidate["historicalBoundaryHashSha256"] = source_hashes[
            "oa22160BoundaryArchive"
        ]

    header_values = [
        CANDIDATE_SCHEMA_VERSION,
        REVISION,
        design_hash,
        implementation_hash,
        scope_hash,
        source_hashes["oa15534IntersectionArchive"],
        source_hashes["oa15534AcquisitionReceipt"],
        source_hashes["oa22160BoundaryArchive"],
        source_hashes["r2ScopeDefinition"],
        source_hashes["r2ScopeManifest"],
        source_hashes["g3aScopeDefinition"],
        source_hashes["g3aManifest"],
        source_hashes["g3aCandidates"],
        source_hashes["g3aComplete"],
        CROSSWALK_CANDIDATE_SET_HASH,
        generator_hash,
        canonical_json_text(AUTHORITY_FLAGS),
    ]
    require(
        len(header_values) == len(CANDIDATE_HASH_HEADER_FIELDS),
        "CandidateHashHeaderLengthInvalid",
    )
    set_hash = candidate_set_hash(header_values, candidates)
    require(
        set_hash
        == independently_recalculate_candidate_set_hash(header_values, candidates),
        "IndependentCandidateHashMismatch",
    )
    for candidate in candidates:
        candidate["candidateSetHashSha256"] = set_hash
    candidates_bytes = b"".join(
        canonical_json_bytes(candidate) + b"\n" for candidate in candidates
    )

    per_area_list = [
        {
            "administrativeAreaStableId": f"region:kr:hjd:{code}",
            **counts,
        }
        for code, counts in assignment_audit["perAdministrativeArea"].items()
    ]
    audit = with_content_hash(
        {
            "schemaVersion": AUDIT_SCHEMA_VERSION,
            "scopeStableId": SCOPE_STABLE_ID,
            "revision": REVISION,
            "generatedAtUtc": GENERATED_AT_UTC,
            "candidateSetHashSha256": set_hash,
            "scopeDefinitionSha256": scope_hash,
            "sourceAudit": source_audit,
            "historicalBoundaryAudit": boundary_audit,
            "assignmentAudit": {
                "methodCode": "OwnSourcePointUniqueHistoricalBoundaryCover",
                "crosswalkAssignmentInherited": False,
                "counts": assignment_audit["counts"],
                "assignedSourceDistrictCodeCounts": assignment_audit[
                    "assignedSourceDistrictCodeCounts"
                ],
                "perAdministrativeArea": per_area_list,
                "sourceDistrictSpatialAssignmentConflicts": assignment_audit[
                    "sourceDistrictSpatialAssignmentConflicts"
                ],
            },
            "crosswalkLinkAudit": {
                **crosswalk_source_audit,
                "linkMethodCode": "CssNumEqualsG3aIntersectionManagementNumberAfterOwnAssignment",
                "candidateSetHashSha256": CROSSWALK_CANDIDATE_SET_HASH,
                "crosswalkLinkCanAssignAdministrativeArea": False,
                "crosswalkLinkedCandidateRows": assignment_audit["counts"][
                    "crosswalkLinkedCandidateRows"
                ],
                "crosswalkUnlinkedCandidateRows": assignment_audit["counts"][
                    "crosswalkUnlinkedCandidateRows"
                ],
                "linkedCrosswalkRelations": assignment_audit["counts"][
                    "linkedCrosswalkRelations"
                ],
                "linkedCrosswalkSameHistoricalHjdRelations": assignment_audit[
                    "counts"
                ]["linkedCrosswalkSameHistoricalHjdRelations"],
                "linkedCrosswalkOtherHistoricalHjdRelations": assignment_audit[
                    "counts"
                ]["linkedCrosswalkOtherHistoricalHjdRelations"],
            },
            "sourceReceiptSelectionDisposition": {
                "receiptSagajeongBboxSelectedRows": 25,
                "usedForThirtyDongCandidateSelection": False,
                "selectionMethodCode": "WholeSourceOwnPointHistoricalBoundaryCover",
            },
            "excludedTrafficLayers": ["controller", "direction", "lane", "signal"],
            "walkNetworkDispositionCode": "Oa21208Year2020NotConsumedNotCurrent",
            "laneDatasetDispositionCode": "Oa15537LaneMarkingsNotConsumedNotDrivableLanes",
            "authorityFlags": dict(AUTHORITY_FLAGS),
        }
    )
    audit_bytes = pretty_json_bytes(audit)
    candidate_file_hash = sha256_bytes(candidates_bytes)
    audit_file_hash = sha256_bytes(audit_bytes)
    generator_file_hash = sha256_bytes(generator_bytes)

    lineage = [
        {
            "role": role,
            "repositoryRelativePath": path.relative_to(root).as_posix(),
            "sha256": source_hashes[role],
            "byteLength": path.stat().st_size,
        }
        for role, path in source_paths.items()
    ]
    manifest = with_content_hash(
        {
            "schemaVersion": MANIFEST_SCHEMA_VERSION,
            "scopeStableId": SCOPE_STABLE_ID,
            "revision": REVISION,
            "generatedAtUtc": GENERATED_AT_UTC,
            "sourceVintage": scope["sourceVintage"],
            "completionUpperBoundCode": COMPLETION_CODE,
            "designDocumentRef": DESIGN_DOCUMENT_RELATIVE.as_posix(),
            "designHashSha256": design_hash,
            "dataImplementationRef": DATA_IMPLEMENTATION_RELATIVE.as_posix(),
            "dataImplementationHashSha256": implementation_hash,
            "scopeDefinitionSha256": scope_hash,
            "candidateSetHashSha256": set_hash,
            "candidateSetHashContract": {
                "algorithmCode": "Sha256UnsignedBigEndianUInt32LengthPrefixedUtf8Fields",
                "canonicalJsonCode": "Utf8SortedKeysCompactNoNaN",
                "headerFieldOrder": list(CANDIDATE_HASH_HEADER_FIELDS),
                "headerValues": header_values,
                "candidateFieldOrder": list(CANDIDATE_HASH_FIELDS),
            },
            "administrativeAreaIds": sorted(area_ids),
            "counts": assignment_audit["counts"],
            "perAdministrativeArea": per_area_list,
            "lineage": lineage,
            "outputFiles": [
                {
                    "relativePath": "audit.json",
                    "sha256": audit_file_hash,
                    "byteLength": len(audit_bytes),
                },
                {
                    "relativePath": "candidates.ndjson",
                    "sha256": candidate_file_hash,
                    "byteLength": len(candidates_bytes),
                    "recordCount": len(candidates),
                },
                {
                    "relativePath": "generator-source.py",
                    "sha256": generator_file_hash,
                    "byteLength": len(generator_bytes),
                },
            ],
            "qualityCodes": [
                "CrosswalkLinkObservedOtherHistoricalHjd",
                "HistoricalBoundaryBootstrapOnly",
                "NoInScopeCrosswalkLinkObserved",
                "PendingHumanReview",
                "SourceCoordinateAttributeMissing",
                "SourceDistrictSpatialAssignmentConflict",
            ],
            "authorityFlags": dict(AUTHORITY_FLAGS),
        }
    )
    manifest_bytes = pretty_json_bytes(manifest)

    output_relative = Path(scope["output"]["repositoryRelativeDirectory"])
    generation_relative = (
        output_relative
        / scope["output"]["generationDirectoryName"]
        / set_hash.lower()
    )
    complete = with_content_hash(
        {
            "schemaVersion": COMPLETE_SCHEMA_VERSION,
            "scopeStableId": SCOPE_STABLE_ID,
            "revision": REVISION,
            "candidateSetHashSha256": set_hash,
            "scopeDefinitionSha256": scope_hash,
            "generationRelativePath": generation_relative.as_posix(),
            "completeMarker": True,
            "completionCode": COMPLETION_CODE,
            "files": [
                {
                    "relativePath": "audit.json",
                    "sha256": audit_file_hash,
                    "byteLength": len(audit_bytes),
                },
                {
                    "relativePath": "candidates.ndjson",
                    "sha256": candidate_file_hash,
                    "byteLength": len(candidates_bytes),
                    "recordCount": len(candidates),
                },
                {
                    "relativePath": "generator-source.py",
                    "sha256": generator_file_hash,
                    "byteLength": len(generator_bytes),
                },
                {
                    "relativePath": "manifest.json",
                    "sha256": sha256_bytes(manifest_bytes),
                    "byteLength": len(manifest_bytes),
                },
            ],
            "authorityFlags": dict(AUTHORITY_FLAGS),
        }
    )
    files = {
        "audit.json": audit_bytes,
        "candidates.ndjson": candidates_bytes,
        "generator-source.py": generator_bytes,
        "manifest.json": manifest_bytes,
        "complete.json": pretty_json_bytes(complete),
    }
    require(set(files) == EXPECTED_OUTPUT_FILES, "PreparedOutputFileSetInvalid")
    return PreparedGeneration(
        set_hash,
        generation_relative,
        files,
        len(candidates),
        assignment_audit["counts"],
    )


def verify_generation_files(path: Path, expected: dict[str, bytes]) -> None:
    require(path.is_dir(), "GenerationDirectoryMissing")
    require(not is_reparse_path(path), "GenerationDirectoryReparseRejected")
    observed = {item.name for item in path.iterdir()}
    require(observed == set(expected), "GenerationFileSetMismatch")
    for name, expected_bytes in expected.items():
        child = path / name
        require(child.is_file(), f"GenerationFileMissing:{name}")
        require(not is_reparse_path(child), f"GenerationFileReparseRejected:{name}")
        require(child.read_bytes() == expected_bytes, f"GenerationFileMismatch:{name}")


def _remove_staging_directory(path: Path) -> None:
    if not path.exists():
        return
    require(path.is_dir() and not is_reparse_path(path), "StagingCleanupTargetInvalid")
    for child in list(path.iterdir()):
        require(child.is_file() and not is_reparse_path(child), "StagingCleanupEntryInvalid")
        child.unlink()
    path.rmdir()


def publish_generation(
    root: Path,
    output_relative: Path,
    candidate_hash: str,
    files: dict[str, bytes],
) -> tuple[Path, int]:
    require(re.fullmatch(r"[0-9A-F]{64}", candidate_hash) is not None, "CandidateHashInvalid")
    output = create_plain_directory(root, output_relative)
    generations = create_plain_directory(root, output_relative / "generations")
    current = repository_path(root, output_relative / "current")
    require(not current.exists() and not current.is_symlink(), "CurrentPointerForbidden")
    target = repository_path(
        root, output_relative / "generations" / candidate_hash.lower()
    )
    if target.exists():
        verify_generation_files(target, files)
        return target, 0

    stage_name = f".staging-{candidate_hash.lower()}-{uuid.uuid4().hex}"
    stage_relative = output_relative / "generations" / stage_name
    stage = repository_path(root, stage_relative)
    require(not stage.exists() and not stage.is_symlink(), "StagingDirectoryAlreadyExists")
    stage.mkdir()
    require(not is_reparse_path(stage), "StagingDirectoryReparseRejected")
    try:
        write_order = [
            "audit.json",
            "candidates.ndjson",
            "generator-source.py",
            "manifest.json",
            "complete.json",
        ]
        require(set(write_order) == set(files), "PublishOutputFileSetInvalid")
        for name in write_order:
            child = stage / name
            with child.open("xb") as stream:
                stream.write(files[name])
                stream.flush()
                os.fsync(stream.fileno())
        verify_generation_files(stage, files)
        try:
            os.replace(stage, target)
        except OSError:
            if target.exists():
                verify_generation_files(target, files)
            else:
                raise
        require(not stage.exists(), "StagingDirectoryNotRenamed")
        verify_generation_files(target, files)
        require(not current.exists(), "CurrentPointerUnexpectedlyCreated")
        return target, len(files)
    finally:
        if stage.exists():
            _remove_staging_directory(stage)
        require(output.is_dir() and generations.is_dir(), "OutputDirectoryChanged")


def _read_candidate_documents(path: Path) -> list[dict[str, Any]]:
    result: list[dict[str, Any]] = []
    with path.open("r", encoding="utf-8", newline="") as stream:
        for raw_line in stream:
            require(raw_line.endswith("\n"), "CandidateNdjsonLineTerminationInvalid")
            result.append(json.loads(raw_line))
    return result


def verify_materialized_generation(
    root: Path, prepared: PreparedGeneration
) -> dict[str, Any]:
    generation = repository_path(root, prepared.generation_relative)
    verify_generation_files(generation, prepared.files)
    manifest = load_json(generation / "manifest.json")
    audit = load_json(generation / "audit.json")
    complete = load_json(generation / "complete.json")
    validate_content_hash(manifest, "ManifestContentHashMismatch")
    validate_content_hash(audit, "AuditContentHashMismatch")
    validate_content_hash(complete, "CompleteContentHashMismatch")
    require(complete.get("completeMarker") is True, "CompleteMarkerMissing")
    require(
        complete.get("candidateSetHashSha256") == prepared.candidate_set_hash,
        "CompleteCandidateSetHashMismatch",
    )
    candidates = _read_candidate_documents(generation / "candidates.ndjson")
    require(len(candidates) == prepared.candidate_count, "CandidateRecordCountMismatch")
    header_values = manifest.get("candidateSetHashContract", {}).get("headerValues")
    require(isinstance(header_values, list), "CandidateHashHeaderMissing")
    independent = independently_recalculate_candidate_set_hash(
        header_values, candidates
    )
    require(independent == prepared.candidate_set_hash, "IndependentMaterializedHashMismatch")
    require(
        (generation / "generator-source.py").read_bytes()
        == Path(__file__).resolve().read_bytes(),
        "GeneratorSourceSnapshotMismatch",
    )
    output_root = generation.parent.parent
    require(not (output_root / "current").exists(), "CurrentPointerForbidden")
    return {
        "statusCode": "Verified",
        "candidateSetHashSha256": prepared.candidate_set_hash,
        "generationRelativePath": prepared.generation_relative.as_posix(),
        "candidateRows": prepared.candidate_count,
        "independentCanonicalHashRecalculated": True,
        "generatorSourceSnapshotMatched": True,
        "currentPointerUsed": False,
    }


def expect_error(code: str, action: Callable[[], Any]) -> None:
    try:
        action()
    except IntersectionCandidateError as exc:
        require(str(exc).startswith(code), f"SelfTestWrongError:{code}:{exc}")
    else:
        raise IntersectionCandidateError(f"SelfTestExpectedErrorMissing:{code}")


def run_self_tests(
    root: Path, scope_relative: Path, dependencies: Dependencies
) -> dict[str, Any]:
    passed = 0

    require(
        canonical_json_text({"b": 2, "a": 1}) == '{"a":1,"b":2}',
        "SelfTestCanonicalJson",
    )
    passed += 1
    require(framed_sha256(("ab", "c")) != framed_sha256(("a", "bc")), "SelfTestFraming")
    passed += 1
    require(
        candidate_stable_id("82-0000000970")
        == candidate_stable_id("82-0000000970")
        and candidate_stable_id("82-0000000970")
        != candidate_stable_id("82-0000000971"),
        "SelfTestCandidateIdentity",
    )
    passed += 1
    expect_error(
        "AbsoluteRepositoryPathRejected",
        lambda: repository_path(root, Path(root.anchor) / "outside"),
    )
    passed += 1
    expect_error(
        "RepositoryPathTraversalRejected", lambda: repository_path(root, "../outside")
    )
    passed += 1
    require(
        _stat_has_reparse_attribute(SimpleNamespace(st_file_attributes=0x400)),
        "SelfTestReparseAttribute",
    )
    passed += 1
    own = "region:kr:hjd:1"
    links, same, other = _link_documents(
        [
            CrosswalkLink("a", "x", own),
            CrosswalkLink("b", "y", "region:kr:hjd:2"),
        ],
        own,
    )
    require(
        same == 1
        and other == 1
        and links[1]["administrativeAreaStableId"] != own
        and own == "region:kr:hjd:1",
        "SelfTestCrosswalkAssignmentNotInherited",
    )
    passed += 1
    header = ["h"] * len(CANDIDATE_HASH_HEADER_FIELDS)
    sample = {
        "candidateStableId": "c",
        "sourceFeatureKey": "f",
        "administrativeAreaStableId": own,
        "assignmentStateCode": ASSIGNMENT_STATE,
        "candidateQualityCode": "PendingHumanReview",
        "sourceManagementNumber": "82-0000000001",
        "intersectionManagementNumber": "1",
        "sourceGeometryPointEpsg5186": {"x": 1.0, "y": 2.0},
        "commonEnuMillimeters": {"x": 1, "z": 2},
        "sourceDistrictCode": "210",
        "sourceBoroughName": "광진구",
        "spatialAssignmentBoroughName": "광진구",
        "sourceDistrictSpatialAssignmentConflict": False,
        "sourceCoordinateAttributeStateCode": "MissingBoth",
        "linkedCrosswalks": [],
        "qualityDiagnosticCodes": ["HistoricalBoundaryBootstrapOnly"],
        "ownershipBasisCode": OWNERSHIP_BASIS,
    }
    require(
        candidate_set_hash(header, [sample])
        == independently_recalculate_candidate_set_hash(header, [sample]),
        "SelfTestIndependentCanonicalHash",
    )
    passed += 1
    synthetic_boundaries = [
        Boundary(
            "1",
            "region:kr:hjd:1",
            "a",
            "광진구",
            dependencies.Polygon([(0, 0), (2, 0), (2, 2), (0, 2)]),
        ),
        Boundary(
            "2",
            "region:kr:hjd:2",
            "b",
            "광진구",
            dependencies.Polygon([(1, 0), (3, 0), (3, 2), (1, 2)]),
        ),
    ]
    require(
        len(
            [
                boundary
                for boundary in synthetic_boundaries
                if boundary.geometry.covers(dependencies.Point(0.5, 0.5))
            ]
        )
        == 1
        and len(
            [
                boundary
                for boundary in synthetic_boundaries
                if boundary.geometry.covers(dependencies.Point(1.5, 0.5))
            ]
        )
        == 2,
        "SelfTestUniqueAndMultipleCovers",
    )
    passed += 1
    loaded_scope, _, _ = load_scope(root, scope_relative, dependencies)
    require(
        loaded_scope["expectedCounts"] == EXPECTED_COUNTS,
        "SelfTestScopeValidation",
    )
    passed += 1
    prepared = prepare_generation(root, scope_relative, dependencies)
    require(
        prepared.candidate_count == 554
        and prepared.counts == EXPECTED_COUNTS
        and set(prepared.files) == EXPECTED_OUTPUT_FILES,
        "SelfTestFrozenSourcePreparation",
    )
    passed += 1

    with tempfile.TemporaryDirectory(prefix="g3b-intersection-self-test-") as temporary:
        test_root = Path(temporary)
        (test_root / ".git").mkdir()
        test_root = require_repository_root(test_root)
        fake_files = {name: name.encode("ascii") for name in EXPECTED_OUTPUT_FILES}
        fake_hash = "A" * 64
        target, changed = publish_generation(
            test_root, Path("artifacts/local/test-output"), fake_hash, fake_files
        )
        require(target.is_dir() and changed == 5, "SelfTestAtomicFirstPublish")
        passed += 1
        _, changed_again = publish_generation(
            test_root, Path("artifacts/local/test-output"), fake_hash, fake_files
        )
        require(changed_again == 0, "SelfTestIdempotentPublish")
        passed += 1
        (target / "audit.json").write_bytes(b"tampered")
        expect_error(
            "GenerationFileMismatch",
            lambda: publish_generation(
                test_root,
                Path("artifacts/local/test-output"),
                fake_hash,
                fake_files,
            ),
        )
        passed += 1
        second_hash = "B" * 64
        with mock.patch("os.replace", side_effect=OSError("injected")):
            try:
                publish_generation(
                    test_root,
                    Path("artifacts/local/test-output"),
                    second_hash,
                    fake_files,
                )
            except OSError:
                pass
            else:
                raise IntersectionCandidateError("SelfTestAtomicFailureNotRaised")
        staging = list(
            (test_root / "artifacts/local/test-output/generations").glob(".staging-*")
        )
        require(not staging, "SelfTestAtomicFailureStagingLeak")
        passed += 1

    require(passed == 15, "SelfTestCountMismatch")
    return {
        "statusCode": "Passed",
        "passed": passed,
        "failed": 0,
        "candidateRowsPrepared": prepared.candidate_count,
        "candidateSetHashSha256": prepared.candidate_set_hash,
    }


def build(root: Path, scope_relative: Path, dependencies: Dependencies) -> dict[str, Any]:
    prepared = prepare_generation(root, scope_relative, dependencies)
    scope = load_json(repository_path(root, scope_relative))
    output_relative = Path(scope["output"]["repositoryRelativeDirectory"])
    target, changed_files = publish_generation(
        root,
        output_relative,
        prepared.candidate_set_hash,
        prepared.files,
    )
    return {
        "statusCode": "Generated" if changed_files else "AlreadyCurrentContentAddressedGeneration",
        "candidateSetHashSha256": prepared.candidate_set_hash,
        "generationRelativePath": target.relative_to(root).as_posix(),
        "candidateRows": prepared.candidate_count,
        "changedFiles": changed_files,
        "currentPointerUsed": False,
        "completionCode": COMPLETION_CODE,
    }


def parse_arguments(argv: Sequence[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("command", choices=("self-test", "build", "verify"))
    parser.add_argument("--root", type=Path, default=DEFAULT_REPOSITORY_ROOT)
    parser.add_argument("--scope", type=Path, default=DEFAULT_SCOPE_RELATIVE)
    return parser.parse_args(argv)


def main(argv: Sequence[str] | None = None) -> int:
    arguments = parse_arguments(sys.argv[1:] if argv is None else argv)
    try:
        root = require_repository_root(arguments.root)
        dependencies = load_dependencies(root)
        if arguments.command == "self-test":
            result = run_self_tests(root, arguments.scope, dependencies)
        elif arguments.command == "build":
            result = build(root, arguments.scope, dependencies)
        else:
            prepared = prepare_generation(root, arguments.scope, dependencies)
            result = verify_materialized_generation(root, prepared)
        print(json.dumps(result, ensure_ascii=False, indent=2, allow_nan=False))
        return 0
    except (IntersectionCandidateError, OSError, zipfile.BadZipFile) as exc:
        print(
            json.dumps(
                {
                    "statusCode": "Failed",
                    "errorCode": str(exc) or type(exc).__name__,
                },
                ensure_ascii=False,
            ),
            file=sys.stderr,
        )
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
